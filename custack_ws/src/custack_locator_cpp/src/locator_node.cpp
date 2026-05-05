#include "locator_node.hpp"

#include <iostream>
#include <filesystem>
#include <fstream>
#include <string>
#include <cv_bridge/cv_bridge.h>
#include <vpi/OpenCVInterop.hpp>
#include <vpi/Array.h>
#include <vpi/algo/Remap.h>
#include <vpi/algo/PerspectiveWarp.h>
#include <vpi/algo/ConvertImageFormat.h>
#include <vpi/algo/AprilTags.h>
#include <vpi/algo/Rescale.h>

namespace fs = std::filesystem;

#define CHECK_VPI_STATUS(stmt)                                           \
    do                                                                   \
    {                                                                    \
        VPIStatus status = (stmt);                                       \
        if (status != VPI_SUCCESS)                                       \
        {                                                                \
            const char* buf =vpiStatusGetName(status);            \
            RCLCPP_ERROR(this->get_logger(), "VPI Error in %s: %s", #stmt, buf); \
            throw std::runtime_error("VPI function failed");             \
        }                                                                \
    } while (0)

LocatorNode::LocatorNode() : rclcpp::Node("locator_node") {
    RCLCPP_INFO(this->get_logger(), "ℹ️: OpenCV %s", CV_VERSION);

    // デフォルトファイルパスを設定する
    fs::path current_path = fs::current_path();
    fs::path calib_save_path = current_path / "camera_calib.yaml";
    std::string calib_save_path_str = calib_save_path.string();
    fs::path homography_save_path = current_path / "homography.yaml";
    std::string homography_save_path_str = homography_save_path.string();
    fs::path cache_path = current_path / "camera_config.yaml";
    cache_path_str_ = cache_path.string();
    
    // ROSパラメータを宣言する
    this->declare_parameter<int>("camera_index", 0);
    this->declare_parameter<int>("fps", 60);
    this->declare_parameter<int>("width", 1920);
    this->declare_parameter<int>("height", 1080);
    this->declare_parameter<std::string>("camera_topic", "camera/image_raw");
    this->declare_parameter<std::string>("robot_pose_topic", "robot_poses");
    this->declare_parameter<std::string>("calib_path", calib_save_path_str);
    this->declare_parameter<std::string>("homography_path", homography_save_path_str);

    // ROSパラメータを取得する
    int camera_index = this->get_parameter("camera_index").as_int();
    int fps = this->get_parameter("fps").as_int();
    int width = this->get_parameter("width").as_int();
    int height = this->get_parameter("height").as_int();
    std::string camera_topic = this->get_parameter("camera_topic").as_string();
    std::string robot_pose_topic = this->get_parameter("robot_pose_topic").as_string();
    calib_save_path_str = this->get_parameter("calib_path").as_string();
    homography_save_path_str = this->get_parameter("homography_path").as_string();
    
    // カメラパラメータファイルを開く
    if(fs::exists(calib_save_path_str)) {
        cv::FileStorage fs(calib_save_path_str, cv::FileStorage::READ);
        if(fs.isOpened()) {
            fs["camera_matrix"] >> camera_matrix_;
            fs["distortion_coefficients"] >> dist_coeffs_;
            fs.release();
        } else {
            RCLCPP_ERROR(this->get_logger(), "❌: カメラパラメータファイルを開けませんでした: %s", calib_save_path_str.c_str());
            throw std::runtime_error("Failed to open calibration file.");
        }
    } else {
        RCLCPP_ERROR(this->get_logger(), "❌: カメラパラメータファイルが存在しません: %s", calib_save_path_str.c_str());
        throw std::runtime_error("Calibration file does not exist.");
    }

    // ホモグラフィーファイルを開く
    if(fs::exists(homography_save_path_str)) {
        cv::FileStorage fs(homography_save_path_str, cv::FileStorage::READ);
        if(fs.isOpened()) {
            fs["homography_matrix"] >> homography_;
            fs.release();
        } else {
            RCLCPP_ERROR(this->get_logger(), "❌: ホモグラフィーファイルを開けませんでした: %s", homography_save_path_str.c_str());
            throw std::runtime_error("Failed to open homography file.");
        }
    } else {
        RCLCPP_ERROR(this->get_logger(), "❌: ホモグラフィーファイルが存在しません: %s", homography_save_path_str.c_str());
        throw std::runtime_error("Homography file does not exist.");
    }

    current_frame_ = cv::Mat(height, width, CV_16UC1);
    // current_frame_.setTo(0);
    processed_frame_ = cv::Mat(height, width, CV_8UC1);
    // processed_frame_.setTo(0);

    // VPIの初期化
    image_width_ = width;
    image_height_ = height;
    // detect_scale_ = 1.0;
    detect_scale_ = 0.5;
    setupVPI(width, height, detect_scale_);

    cap_.open(camera_index, cv::CAP_V4L2);
    if(!cap_.isOpened()) {
        RCLCPP_ERROR(this->get_logger(), "❌: カメラ%dを開けませんでした ", camera_index);
        throw std::runtime_error("Failed to open camera.");
    }

    cap_.set(cv::CAP_PROP_FOURCC, cv::VideoWriter::fourcc('Y', '1', '6', ' '));
    cap_.set(cv::CAP_PROP_CONVERT_RGB, false);
    cap_.set(cv::CAP_PROP_FRAME_WIDTH, width);
    cap_.set(cv::CAP_PROP_FRAME_HEIGHT, height);
    cap_.set(cv::CAP_PROP_FPS, fps);
    cap_.set(cv::CAP_PROP_BUFFERSIZE, 2);

    cap_.grab();

    loadAndApplyV4L2Params(cache_path_str_);

    // ROSメッセージ配信のための初期化
    // auto qos = rclcpp::QoS(rclcpp::KeepLast(1)).best_effort();
    // image_pub_ = this->create_publisher<sensor_msgs::msg::Image>(camera_topic, qos);
    auto image_qos = rclcpp::QoS(3)
        .best_effort()
        .durability_volatile();
    image_pub_ = this->create_publisher<sensor_msgs::msg::Image>(camera_topic, image_qos);

    auto robot_pose_qos = rclcpp::QoS(1)
         .best_effort()
        //.reliable()
        .durability_volatile();
        
    robot_pose_pub_ = this->create_publisher<custack_msgs::msg::RobotPoseArray>(
        robot_pose_topic, robot_pose_qos);

    // 処理時間 初期化
    last_info_time_ = std::chrono::steady_clock::now();

    // キャプチャースレッドを開始
    is_running_ = true;
    capture_thread_ = std::thread(&LocatorNode::captureLoop, this);

    // メイン処理タイマー（60Hz）
    process_thread_ = std::thread(&LocatorNode::processLoop, this);
    // group_process_ = this->create_callback_group(rclcpp::CallbackGroupType::Reentrant);
    // process_timer_ = this->create_wall_timer(
    //     std::chrono::milliseconds(static_cast<int>(1000.0 / 30.0)),        
    //     // std::chrono::milliseconds(static_cast<int>(1000.0 / fps)),
    //     // std::chrono::milliseconds(static_cast<int>(1000.0 / 10)),
    //     std::bind(&LocatorNode::processCallback, this),
    //     group_process_
    // );

    // インフォメーション処理(2Hz)
    group_info_ = this->create_callback_group(rclcpp::CallbackGroupType::Reentrant);
    info_timer_ = this->create_wall_timer(
        std::chrono::milliseconds(500),
        std::bind(&LocatorNode::infoCallback, this),
        group_info_
    );
}

LocatorNode::~LocatorNode() {
    saveCurrentV4L2Params(cache_path_str_);

    is_running_ = false;
    if(capture_thread_.joinable()) capture_thread_.join();
    if(process_thread_.joinable()) process_thread_.join();

    cap_.release();

    vpiImageDestroy(input_image_);
    vpiImageDestroy(gray_image_);
    vpiImageDestroy(undistorted_image_);
    vpiImageDestroy(homography_image_);
    vpiImageDestroy(resized_image_);
    vpiImageDestroy(output_image_);
    vpiArrayDestroy(apriltag_detections_);
    vpiArrayDestroy(apriltag_poses_);
    vpiStreamDestroy(stream_);
    vpiPayloadDestroy(ldc_payload_);
    vpiPayloadDestroy(apriltags_payload_);    
}

void LocatorNode::loadAndApplyV4L2Params(const std::string& filename) {
    // ファイルが存在しない場合は何もしない
    if (!std::filesystem::exists(filename)) {
        RCLCPP_INFO(this->get_logger(), "ℹ️: 前回設定ファイルが見つかりません。デフォルトで起動します。");
        return;
    }

    try {
        cv::FileStorage fs(filename, cv::FileStorage::READ);
        if (fs.isOpened()) {
            int exposure = 0;
            int gain = 0;

            // ファイルから値を取得
            fs["exposure"] >> exposure;
            fs["analogue_gain"] >> gain;
            fs.release();

            // 値が有効な場合（0以外など）に適用
            if (exposure > 0 || gain > 0) {
                std::string cmd = "v4l2-ctl -d /dev/video0 -c exposure=" + std::to_string(exposure) +
                                  ",analogue_gain=" + std::to_string(gain);
                
                if (system(cmd.c_str()) == 0) {
                    RCLCPP_INFO(this->get_logger(), "♻️: 前回のパラメータを復元しました (Exposure: %d, Gain: %d)", exposure, gain);
                } else {
                    RCLCPP_WARN(this->get_logger(), "⚠️: v4l2-ctl によるパラメータ適用に失敗しました。");
                }
            }
        }
    } catch (const std::exception& e) {
        RCLCPP_ERROR(this->get_logger(), "❌: 設定ファイル読み込みエラー: %s", e.what());
    }
}

void LocatorNode::saveCurrentV4L2Params(const std::string& filename) {
    std::map<std::string, int> params;
    std::vector<std::string> controls = {"exposure", "analogue_gain"};

    for (const auto& ctrl : controls) {
        std::string command = "v4l2-ctl -d /dev/video0 -C " + ctrl;
        std::array<char, 128> buffer;
        std::string result;

        std::unique_ptr<FILE, decltype(&pclose)> pipe(popen(command.c_str(), "r"), pclose);
        if (pipe) {
            while (fgets(buffer.data(), buffer.size(), pipe.get()) != nullptr) {
                result += buffer.data();
            }
            // "exposure: 157" の ':' 以降を数値として抽出
            size_t pos = result.find(':');
            if (pos != std::string::npos) {
                try {
                    params[ctrl] = std::stoi(result.substr(pos + 1));
                } catch (...) {
                    RCLCPP_WARN(this->get_logger(), "⚠️: %s の値のパースに失敗しました", ctrl.c_str());
                }
            }
        }
    }

    // iostreamではなく cv::FileStorage で保存
    try {
        cv::FileStorage fs(filename, cv::FileStorage::WRITE);
        if (fs.isOpened()) {
            // OpenCV形式で書き込み (%YAML:1.0 ヘッダーなどが自動付与される)
            fs << "exposure" << params["exposure"];
            fs << "analogue_gain" << params["analogue_gain"];
            fs.release();
            RCLCPP_INFO(this->get_logger(), "✅: V4L2パラメータを YAML 形式で保存しました");
        }
    } catch (const cv::Exception& e) {
        RCLCPP_ERROR(this->get_logger(), "❌: cv::FileStorage 保存エラー: %s", e.what());
    }
}
void LocatorNode::setupVPI(int width, int height, float scale) {
    // ストリームの作成
    vpiStreamCreate(VPI_BACKEND_CUDA | VPI_BACKEND_CPU, &stream_);

    // WarpMapの初期化と生成
    memset(&warpMap_, 0, sizeof(warpMap_));
    warpMap_.grid.numHorizRegions = 1;
    warpMap_.grid.numVertRegions = 1;
    warpMap_.grid.horizInterval[0] = 1;
    warpMap_.grid.vertInterval[0] = 1;
    warpMap_.grid.regionWidth[0] = width;
    warpMap_.grid.regionHeight[0] = height;
    CHECK_VPI_STATUS(vpiWarpMapAllocData(&warpMap_));

    // 歪みモデルの構築
    memset(&ldc_model_, 0, sizeof(ldc_model_));
    ldc_model_.k1 = dist_coeffs_.at<double>(0);
    ldc_model_.k2 = dist_coeffs_.at<double>(1);
    ldc_model_.k3 = dist_coeffs_.at<double>(4);
    ldc_model_.k4 = 0.0;
    ldc_model_.k5 = 0.0;
    ldc_model_.k6 = 0.0;
    ldc_model_.p1 = dist_coeffs_.at<double>(2);
    ldc_model_.p2 = dist_coeffs_.at<double>(3);

    // カメラ内部パラメータの設定
    for (int i = 0; i < 2; ++i) {
        for (int j = 0; j < 3; ++j) {
            float val= static_cast<float>(camera_matrix_.at<double>(i, j));
            Kin_[i][j] = val;
            Kout_[i][j] = val;
        }
    }

    // カメラ外部パラメータの仮委設定
    for (int i = 0; i < 3; ++i) {
        for (int j = 0; j < 4; ++j) {
            Kex_[i][j] = (i == j) ? 1.0f : 0.0f;
        }
    }

    // warpMapの生成
    CHECK_VPI_STATUS(vpiWarpMapGenerateFromPolynomialLensDistortionModel(
        Kin_, Kex_, Kout_, &ldc_model_, &warpMap_
    ));

    // 生成したWarpMapからLDCペイロードを作成
    CHECK_VPI_STATUS(vpiCreateRemap(VPI_BACKEND_CUDA, &warpMap_, &ldc_payload_));

    // 変換パラメータの設定
    vpiInitConvertImageFormatParams(&convert_params_);
    convert_params_.scale = 1.0f / 256.0f;
    convert_params_.offset = 0.0f;

    // 画像バッファを作成
    // cv::Mat dummy_y16_image(height, width, CV_16UC1);
    // cv::Mat dummy_8u_image(height, width, CV_8UC1);
    int sw = static_cast<int>(width * scale);
    int sh = static_cast<int>(height * scale);
    CHECK_VPI_STATUS(vpiImageCreateWrapperOpenCVMat(
        current_frame_, VPI_IMAGE_FORMAT_Y16, 0, &input_image_));
    CHECK_VPI_STATUS(vpiImageCreate(width, height,
        VPI_IMAGE_FORMAT_U8, 0, &gray_image_));
    CHECK_VPI_STATUS(vpiImageCreate(width, height,
        VPI_IMAGE_FORMAT_U8, 0, &undistorted_image_));
    CHECK_VPI_STATUS(vpiImageCreate(width, height,
        VPI_IMAGE_FORMAT_U8, 0, &homography_image_));
    CHECK_VPI_STATUS(vpiImageCreate(sw, sh,
        VPI_IMAGE_FORMAT_U8, 0, &resized_image_));
    CHECK_VPI_STATUS(vpiImageCreateWrapperOpenCVMat(
        processed_frame_, VPI_IMAGE_FORMAT_U8, 0, &output_image_));

    // ホモグラフィ行列の設定
    for(int i=0; i<3; ++i) {
        for(int j=0; j<3; ++j) {
            homography_transform_[i][j] = static_cast<float>(homography_.at<double>(i, j));
        }
    }

    // AprilTagsペイロードの作成
    VPIAprilTagDecodeParams params;
    static uint16_t tag_ids[] = {0, 1, 2};
    params.tagIdFilter = tag_ids;
    params.tagIdFilterSize = sizeof(tag_ids) / sizeof(tag_ids[0]);
    params.maxBitsCorrected = 1;
    // params.family = VPI_APRILTAG_36H11;
    // params.family = VPI_APRILTAG_25H9;
    params.family = VPI_APRILTAG_16H5;
    CHECK_VPI_STATUS(vpiCreateAprilTagDetector(
        VPI_BACKEND_CPU, sw, sh, &params, &apriltags_payload_));
        // VPI_BACKEND_CPU, width, height, &params, &apriltags_payload_));

    // AprilTagsの検出結果用配列の作成
    CHECK_VPI_STATUS(vpiArrayCreate(
        max_detections_, VPI_ARRAY_TYPE_APRILTAG_DETECTION, VPI_BACKEND_CPU,
        &apriltag_detections_));
    CHECK_VPI_STATUS(vpiArrayCreate(
        max_detections_, VPI_ARRAY_TYPE_POSE, VPI_BACKEND_CPU,
        &apriltag_poses_));
    RCLCPP_INFO(this->get_logger(), "✅: VPIのセットアップが完了しました");
}

void LocatorNode::processWithVPI(cv::Mat &src, cv::Mat&dst,
    std::vector<VPIAprilTagDetection> &detected) {
    CHECK_VPI_STATUS(vpiImageSetWrappedOpenCVMat(input_image_, src));
    CHECK_VPI_STATUS(vpiImageSetWrappedOpenCVMat(output_image_, dst));

    // Y16 -> U8 変換
    CHECK_VPI_STATUS(vpiSubmitConvertImageFormat(
        stream_, VPI_BACKEND_CUDA,
        input_image_, gray_image_, &convert_params_));

    // 歪み補正
    CHECK_VPI_STATUS(vpiSubmitRemap(
        stream_, VPI_BACKEND_CUDA, ldc_payload_,
        gray_image_, undistorted_image_,
        VPI_INTERP_LINEAR, VPI_BORDER_CLAMP, 0));

    // ホモグラフィー変換
    CHECK_VPI_STATUS(vpiSubmitPerspectiveWarp(
        stream_,
        VPI_BACKEND_CUDA,
        undistorted_image_,
        homography_transform_,
        homography_image_,
        NULL, VPI_INTERP_LINEAR, VPI_BORDER_ZERO, 0));

    CHECK_VPI_STATUS(vpiSubmitRescale(
        stream_, VPI_BACKEND_CUDA,
        homography_image_, resized_image_,
        VPI_INTERP_CATMULL_ROM, VPI_BORDER_ZERO, 0));

    // AprilTagsの検出
    CHECK_VPI_STATUS(vpiSubmitAprilTagDetector(
        stream_, VPI_BACKEND_CPU, apriltags_payload_,
        max_detections_, resized_image_, apriltag_detections_));

    CHECK_VPI_STATUS(vpiSubmitRescale(
        stream_, VPI_BACKEND_CUDA,
        resized_image_, output_image_,
        VPI_INTERP_CATMULL_ROM, VPI_BORDER_ZERO, 0));

    // 同期とクリーンアップ
    CHECK_VPI_STATUS(vpiStreamSync(stream_));

    // AprilTagsの検出結果を取得
    VPIArrayData detections_data;
    CHECK_VPI_STATUS(vpiArrayLockData(
        apriltag_detections_, VPI_LOCK_READ, 
        VPI_ARRAY_BUFFER_HOST_AOS, &detections_data));
    auto *detections = reinterpret_cast<VPIAprilTagDetection*>(detections_data.buffer.aos.data);
    int num_detections = *detections_data.buffer.aos.sizePointer;
    detected.assign(detections, detections + num_detections);
    CHECK_VPI_STATUS(vpiArrayUnlock(apriltag_detections_));     
}

void LocatorNode::captureLoop() {
    while(is_running_ && rclcpp::ok()) {
        // キャプチャーフレームカウントを更新
        {
            std::lock_guard<std::mutex> lock(info_mutex_);
            info_data_.cap_framecount++;
        }

        cv::Mat frame;
        if(cap_.read(frame)) {
            std::lock_guard<std::mutex> lock(frame_mutex_);
            latest_frame_ = frame;
        }
    }
}

void LocatorNode::processLoop() {
    while(is_running_ && rclcpp::ok()) {
        processCallback();
    }
}

void LocatorNode::processCallback() {
    // メイン処理フレームカウントを更新
    {
        std::lock_guard<std::mutex> lock(info_mutex_);
        info_data_.process_framecount++;
    }

    // cv::Mat current_frame;
    {
        std::lock_guard<std::mutex> lock(frame_mutex_);
        if(latest_frame_.empty()) return;
        // current_frame = latest_frame_.clone();
        // current_frame = latest_frame_;
        latest_frame_.copyTo(current_frame_);
    }

    // VPIで処理
    std::vector<VPIAprilTagDetection> detected;
    // processWithVPI(gray_frame, processed_frame);
    processWithVPI(current_frame_, processed_frame_, detected);

    // RCLCPP_INFO(this->get_logger(), "🔍: 検出されたAprilTagsの数: %ld", detected.size());
    // for(int i=0; i<detected.size(); ++i) {
    //     const auto &dct = detected[i];
    //     RCLCPP_INFO(this->get_logger(),
    //         "  - タグID: %d, 中心座標: (%.2f, %.2f)",
    //         dct.id, dct.center.x, dct.center.y);
    // }

    for (const auto &dct : detected) {
        cv::Point2f center(dct.center.x / detect_scale_,
            dct.center.y / detect_scale_); // スケールを元に戻す
        cv::circle(processed_frame_, center, 10, cv::Scalar(255), -1);
        cv::putText(processed_frame_, std::to_string(dct.id), 
            center + cv::Point2f(15, -15), cv::FONT_HERSHEY_SIMPLEX,
            2.0, cv::Scalar(255), 4);
    }

    custack_msgs::msg::RobotPoseArray robot_poses;
    for (const auto &dct : detected) {
        custack_msgs::msg::RobotPose robot_pose;        
        float cx = dct.center.x / detect_scale_;
        float cy = dct.center.y / detect_scale_;
        float dx = (dct.corners[2].x + dct.corners[3].x) / 2.0f / detect_scale_;
        float dy = (dct.corners[2].y + dct.corners[3].y) / 2.0f / detect_scale_;
        float theta = std::atan2(dy - cy, dx - cx);

        robot_pose.id = dct.id;
        robot_pose.x = 2.0 * cx / image_width_ - 1.0;
        robot_pose.y = 2.0 * cy / image_height_ - 1.0;
        robot_pose.theta = theta;

        robot_poses.poses.push_back(robot_pose);
    }
    robot_pose_pub_->publish(robot_poses);

    cv::Mat preview_frame;
    cv::resize(processed_frame_, preview_frame, cv::Size(), 0.5, 0.5, cv::INTER_AREA);
    cv::imshow("Processed Frame", preview_frame);
    cv::waitKey(1);
}

void LocatorNode::infoCallback() {
    auto now = std::chrono::steady_clock::now();
    auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(now - last_info_time_);
    if (elapsed.count() >= 1000) {
        float count = static_cast<float>(elapsed.count()/1000.0f);
        uint32_t cap_framecount = 0u;
        uint32_t process_framecount = 0u;
        {
            std::lock_guard<std::mutex> lock(info_mutex_);
            cap_framecount = info_data_.cap_framecount;
            process_framecount = info_data_.process_framecount;
            info_data_.cap_framecount = 0;
            info_data_.process_framecount = 0;
        }


        RCLCPP_INFO(this->get_logger(),
            "📊: キャプチャーフレーム数: %d, 処理フレーム数: %d",
            static_cast<int>(cap_framecount/count),
            static_cast<int>(process_framecount/count));
        last_info_time_ = now;
    }

    // ROSメッセージとして配信
    if(image_pub_->get_subscription_count() > 0) {
        cv::Mat current_frame;
        {
            std::lock_guard<std::mutex> lock(frame_mutex_);
            if(latest_frame_.empty()) return;
            latest_frame_.copyTo(current_frame);
        }

        // Y16(16bit)をグレースケール(CV_8UC1)に変換
        cv::Mat gray_frame;
        current_frame.convertTo(gray_frame, CV_8UC1, 0.00390625);

        auto msg = cv_bridge::CvImage(std_msgs::msg::Header(), "mono8", gray_frame).toImageMsg();
        msg->header.stamp = this->now();
        msg->header.frame_id = "camera_frame";
        image_pub_->publish(*msg);
    }
}
