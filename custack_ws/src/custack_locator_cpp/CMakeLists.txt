cmake_minimum_required(VERSION 3.8)
project(custack_locator_cpp)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

if(NOT CMAKE_BUILD_TYPE)
  set(CMAKE_BUILD_TYPE Release)
endif()

# Releaseモード時の最適化フラグを明示的に指定（任意）
set(CMAKE_CXX_FLAGS_RELEASE "-O3 -DNDEBUG")

if(CMAKE_COMPILER_IS_GNUCXX OR CMAKE_CXX_COMPILER_ID MATCHES "Clang")
  add_compile_options(-Wall -Wextra -Wpedantic)
endif()

# find dependencies
find_package(ament_cmake REQUIRED)
find_package(rclcpp REQUIRED)
find_package(cv_bridge REQUIRED)
find_package(sensor_msgs REQUIRED)
find_package(custack_msgs REQUIRED)

# OpenCV4専用設定
# set(OpenCV_DIR "/usr/local/opencv4/lib/cmake/opencv4")
# find_package(OpenCV REQUIRED)
set(OpenCV_DIR "/usr/lib/cmake/opencv4")
find_package(OpenCV 4.8 REQUIRED NO_DEFAULT_PATH)
message(STATUS "OpenCV library status:")
message(STATUS "    version: ${OpenCV_VERSION}")
message(STATUS "    include path: ${OpenCV_INCLUDE_DIRS}")
message(STATUS "    library path: ${OpenCV_LIBRARY_DIRS}")

# VIPパッケージの設定
find_package(vpi 3 REQUIRED)

# リンカオプションの追加
add_link_options("-fuse-ld=gold")

# 実行ファイルの設定
add_executable(locator_node
  src/locator.cpp
  src/locator_node.cpp
  src/locator_node.hpp
)

target_include_directories(locator_node
  PRIVATE
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
    $<INSTALL_INTERFACE:include>  
    ${sensor_msgs_INCLUDE_DIRS}
    ${custack_msgs_INCLUDE_DIRS}
    ${OpenCV_INCLUDE_DIRS}
    ${vpi_INCLUDE_DIRS}
)

target_link_libraries(locator_node
  rclcpp::rclcpp
  cv_bridge::cv_bridge
  ${sensor_msgs_LIBRARIES}
  ${custack_msgs_LIBRARIES}
  ${OpenCV_LIBRARIES}
  ${vpi_LIBRARIES}
  vpi
  nvvpi
)

ament_target_dependencies(locator_node
  rclcpp
  cv_bridge
  sensor_msgs
  custack_msgs
)


if(BUILD_TESTING)
  find_package(ament_lint_auto REQUIRED)
  # the following line skips the linter which checks for copyrights
  # comment the line when a copyright and license is added to all source files
  set(ament_cmake_copyright_FOUND TRUE)
  # the following line skips cpplint (only works in a git repo)
  # comment the line when this package is in a git repo and when
  # a copyright and license is added to all source files
  set(ament_cmake_cpplint_FOUND TRUE)
  ament_lint_auto_find_test_dependencies()
endif()

install(TARGETS locator_node
  DESTINATION lib/${PROJECT_NAME})

ament_package()
