#include <iostream>
#include <chrono>
#include <string>
#include <vector>
#include <memory>
#include <ranges>

#include <rclcpp/rclcpp.hpp>
#include <sensor_msgs/msg/image.hpp>
#include <geometry_msgs/msg/pose_stamped.hpp>
#include <cv_bridge/cv_bridge.h>

#include <opencv2/opencv.hpp>
#include <opencv2/imgproc.hpp>
#include <opencv2/aruco.hpp>

using namespace std::chrono_literals;

class MarkerPosPublisher : public rclcpp::Node
{
public:
    MarkerPosPublisher() : Node("markerpos_publisher")
    {
        // Declare parameter to control image publishing
        this->declare_parameter("debug", false);

        // Create a publisher for the image topic
        image_pub_ = this->create_publisher<sensor_msgs::msg::Image>("/camera/image_raw", 10);
        
        // Create publishers for marker poses
        pose_pubs_ = std::make_shared<std::vector<rclcpp::Publisher<geometry_msgs::msg::PoseStamped>::SharedPtr>>();
        for(int i=0; i<4; ++i) {
            auto pose_pub = this->create_publisher<geometry_msgs::msg::PoseStamped>("/marker_" + std::to_string(i) + "/pose", 10);
            pose_pubs_->push_back(pose_pub);
        }

        // Initialize the timer to call the callback every 10 milliseconds
        timer_ = this->create_wall_timer(
            10ms,
            std::bind(&MarkerPosPublisher::timerCallback, this)
        );

        // GStreamer pipeline for camera capture
        // auto pipeline = "nvarguscamerasrc sensor-id=0 ! \
        //                 video/x-raw(memory:NVMM), width=(int)1280, height=(int)720, format=(string)NV12, framerate=(fraction)60/1 ! \
        //                 nvvidconv flip-method=0 ! \
        //                 video/x-raw, format=(string)GRAY8 ! \
        //                 appsink drop=1 sync=false";
        auto pipeline = "nvarguscamerasrc sensor-id=0 ! \
                        video/x-raw(memory:NVMM), width=(int)1920, height=(int)1080, format=(string)NV12, framerate=(fraction)30/1 ! \
                        nvvidconv flip-method=0 ! \
                        video/x-raw, format=(string)GRAY8 ! \
                        appsink drop=1 sync=false";

        // Initialize video capture with GStreamer pipeline
        cap_ = cv::makePtr<cv::VideoCapture>(pipeline, cv::CAP_GSTREAMER);
        if (!cap_->isOpened()) {
            RCLCPP_ERROR(this->get_logger(), "Error opening video stream or file");
        } else {            
            RCLCPP_INFO(this->get_logger(), "Video capture initialized successfully.");
        }

        // Initialize ArUco marker detection parameters
        dictionary_ = cv::makePtr<cv::aruco::Dictionary>(cv::aruco::getPredefinedDictionary(cv::aruco::DICT_4X4_50));
        parameters_ = cv::makePtr<cv::aruco::DetectorParameters>(cv::aruco::DetectorParameters());

        // Initialize storage for detected marker data
        ids_ = std::make_shared<std::vector<int>>();
        corners_ = std::make_shared<std::vector<std::vector<cv::Point2f>>>();
        rejected_ =  std::make_shared<std::vector<std::vector<cv::Point2f>>>();
    }

private:
    void timerCallback()
    {
        // Get parameter values
        bool debug = this->get_parameter("debug").as_bool();

        // Create a header for the image message
        std_msgs::msg::Header header;
        header.stamp = this->get_clock()->now();
        header.frame_id = "camera_frame";

        // Capture frame from gstreamer pipeline
        cv::Mat frame;
        if(!cap_->read(frame)) {
            RCLCPP_ERROR(this->get_logger(), "Failed to capture frame");
            rclcpp::shutdown();
            return;
        }

        // Detect ArUco markers
        cv::aruco::detectMarkers(frame, dictionary_, *corners_, *ids_, parameters_, *rejected_);
        RCLCPP_DEBUG(this->get_logger(), "Detected %zu markers", ids_->size());

        if(ids_->size() > 0) {
            for(size_t i=0; i<ids_->size(); ++i) {
                const auto &id = ids_->at(i);
                const auto &corner = corners_->at(i);

                // Print marker ID and corner coordinates
                if(id >= static_cast<int>(pose_pubs_->size())) {
                    RCLCPP_WARN(this->get_logger(), "No publisher for marker ID %d", id);
                    continue;
                }

                // Calculate center and front point of the marker
                cv::Point2f center = (corner[0] + corner[1] + corner[2] + corner[3]) / 4.0f;
                cv::Point2f front = (corner[0] + corner[1]) / 2.0f;

                // Normalize positions to be relative to image center and scaled by image size
                float img_w = static_cast<float>(frame.cols);
                float img_h = static_cast<float>(frame.rows);
                center.x = (center.x - img_w / 2.0f) / img_w;
                center.y = (center.y - img_h / 2.0f) / img_h;
                front.x = (front.x - img_w / 2.0f) / img_w;
                front.y = (front.y - img_h / 2.0f) / img_h;

                // Calculate direction vector and normalize it
                cv::Point2f direction = front - center;
                double norm = cv::norm(direction);
                if (norm > 1e-6) {
                    direction /= static_cast<float>(norm);
                }

                // Calculate quaternion from direction vector (assuming +z is up)
                double yaw = std::atan2(direction.y, direction.x) + 3.14159;
                geometry_msgs::msg::Quaternion orientation;
                orientation.x = 0.0;
                orientation.y = 0.0;
                orientation.z = std::sin(yaw / 2.0);
                orientation.w = std::cos(yaw / 2.0);

                // Create and publish PoseStamped message
                auto pose_msg = geometry_msgs::msg::PoseStamped();
                pose_msg.header = header;
                pose_msg.pose.position.x = - center.x * 2.4 + 0.08;
                pose_msg.pose.position.y = - center.y * 1.6 + 0.1;
                pose_msg.pose.position.z = 0.0;
                pose_msg.pose.orientation = orientation;

                pose_pubs_->at(id)->publish(pose_msg);            
            }

            // Draw detected markers on the frame
            if(debug) {
                cv::aruco::drawDetectedMarkers(frame, *corners_, *ids_);
            }
        }

        // Convert OpenCV image to ROS Image message and publish
        if (debug) {
            sensor_msgs::msg::Image::SharedPtr img_msg = cv_bridge::CvImage(header, "mono8", frame).toImageMsg();
            image_pub_->publish(*img_msg);
        }
    }

    rclcpp::TimerBase::SharedPtr timer_;
    rclcpp::Publisher<sensor_msgs::msg::Image>::SharedPtr image_pub_;
    std::shared_ptr<std::vector<rclcpp::Publisher<geometry_msgs::msg::PoseStamped>::SharedPtr>> pose_pubs_;

    cv::Ptr<cv::VideoCapture> cap_;
    cv::Ptr<cv::aruco::Dictionary> dictionary_;
    cv::Ptr<cv::aruco::DetectorParameters> parameters_;

    std::shared_ptr<std::vector<int>> ids_;
    std::shared_ptr<std::vector<std::vector<cv::Point2f>>> corners_;
    std::shared_ptr<std::vector<std::vector<cv::Point2f>>> rejected_;
};

int main(int argc, char *argv[])
{
    rclcpp::init(argc, argv);
    rclcpp::spin(std::make_shared<MarkerPosPublisher>());
    rclcpp::shutdown();
    return 0;
}
