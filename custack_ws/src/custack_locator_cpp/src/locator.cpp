#include <rclcpp/rclcpp.hpp>
#include "locator_node.hpp"

int main(int argc, char **argv) {
    rclcpp::init(argc, argv);
    auto node = std::make_shared<LocatorNode>();

    rclcpp::executors::MultiThreadedExecutor executor(rclcpp::ExecutorOptions(), 4);
    executor.add_node(node);
    executor.spin();

    rclcpp::shutdown();
    return 0;
}