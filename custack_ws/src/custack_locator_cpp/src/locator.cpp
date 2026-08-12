#include <rclcpp/rclcpp.hpp>
#include "locator_node.hpp"
#include <string>
#include <vector>
#include <algorithm>

int main(int argc, char **argv) {
    rclcpp::init(argc, argv);

    bool headless = false;
    std::vector<std::string> args(argv, argv + argc);
    if (std::find(args.begin(), args.end(), "--headless") != args.end()) {
        headless = true;
    }
    auto node = std::make_shared<LocatorNode>(headless);

    rclcpp::executors::MultiThreadedExecutor executor(rclcpp::ExecutorOptions(), 4);
    executor.add_node(node);
    executor.spin();

    rclcpp::shutdown();
    return 0;
}