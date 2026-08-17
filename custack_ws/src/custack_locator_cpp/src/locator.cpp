#include <rclcpp/rclcpp.hpp>
#include "locator_node.hpp"
#include <string>
#include <vector>
#include <algorithm>

int main(int argc, char **argv) {
    rclcpp::init(argc, argv);

    bool headless = false;
    int max_detections = -1;
    std::string tag_ids = "";

    for (int i = 1; i < argc; ++i) {
        std::string arg = argv[i];
        if (arg == "--headless") {
            headless = true;
        } else if (arg == "--max-detections" || arg == "--max_detections" || arg == "-m") {
            if (i + 1 < argc) {
                try {
                    max_detections = std::stoi(argv[++i]);
                } catch (...) {
                    RCLCPP_WARN(rclcpp::get_logger("locator_node"), "⚠️: --max-detections の値が無効です");
                }
            }
        } else if (arg == "--tag-ids" || arg == "--tag_ids" || arg == "--ids" || arg == "-t") {
            if (i + 1 < argc) {
                tag_ids = argv[++i];
            }
        }
    }
    auto node = std::make_shared<LocatorNode>(headless, max_detections, tag_ids);

    rclcpp::executors::MultiThreadedExecutor executor(rclcpp::ExecutorOptions(), 4);
    executor.add_node(node);
    executor.spin();

    rclcpp::shutdown();
    return 0;
}