#ifndef __CUSTACK_LOCATOR_NODE_HPP__
#define __CUSTACK_LOCATOR_NODE_HPP__

#include <rclcpp/rclcpp.hpp>
#include <cv_bridge/cv_bridge.h>
#include <opencv2/opencv.hpp>
#include <thread>
#include <mutex>
#include <sensor_msgs/msg/image.hpp>
#include "custack_msgs/msg/robot_pose_array.hpp"
#include "custack_msgs/msg/robot_pose.hpp"
#include <vpi/VPI.h>
#include <vpi/WarpMap.h>
#include <vpi/LensDistortionModels.h>
#include <vpi/algo/ConvertImageFormat.h>

struct InfoData {
    uint32_t cap_framecount;
    uint32_t process_framecount;
};


class LocatorNode : public rclcpp::Node {
    public:
        LocatorNode();
        ~LocatorNode();
    private:
        std::string cache_path_str_;

        cv::VideoCapture cap_;
        std::thread capture_thread_;
        std::thread process_thread_;
        std::mutex frame_mutex_;
        std::mutex info_mutex_;
        bool is_running_;

        cv::Mat camera_matrix_;
        cv::Mat dist_coeffs_;
        cv::Mat homography_;

        InfoData info_data_;
        cv::Mat latest_frame_;
        cv::Mat current_frame_;
        cv::Mat processed_frame_;
        float image_width_;
        float image_height_;
        float detect_scale_;
        rclcpp::CallbackGroup::SharedPtr group_process_;
        rclcpp::CallbackGroup::SharedPtr group_info_;        
        rclcpp::TimerBase::SharedPtr process_timer_;
        rclcpp::TimerBase::SharedPtr info_timer_;
        std::chrono::steady_clock::time_point last_info_time_;

        rclcpp::Publisher<sensor_msgs::msg::Image>::SharedPtr image_pub_;
        rclcpp::Publisher<custack_msgs::msg::RobotPoseArray>::SharedPtr robot_pose_pub_;

        void captureLoop();
        void processLoop();
        void processCallback();
        void infoCallback();

        VPIStream stream_ = nullptr;
        VPIPayload ldc_payload_ = nullptr;
        VPIPolynomialLensDistortionModel ldc_model_;
        VPICameraIntrinsic Kin_;
        VPICameraIntrinsic Kout_;
        VPICameraExtrinsic Kex_;
        VPIWarpMap warpMap_;
        VPIPerspectiveTransform homography_transform_;
        VPIConvertImageFormatParams convert_params_;

        VPIPayload apriltags_payload_ = nullptr;
        const int max_detections_ = 16;
        VPIArray apriltag_detections_ = nullptr;
        VPIArray apriltag_poses_ = nullptr;

        VPIImage input_image_ = nullptr;
        VPIImage gray_image_ = nullptr;
        VPIImage undistorted_image_ = nullptr;
        VPIImage homography_image_ = nullptr;
        VPIImage resized_image_ = nullptr;
        VPIImage output_image_ = nullptr;        

        void setupVPI(int width, int height, float scale);
        void processWithVPI(cv::Mat &src, cv::Mat &dst, std::vector<VPIAprilTagDetection> &detections);

        void loadAndApplyV4L2Params(const std::string& filename);
        void saveCurrentV4L2Params(const std::string& filename);
};

#endif // __CUSTACK_LOCATOR_NODE_HPP__ 