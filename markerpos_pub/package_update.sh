#!/bin/sh

USERNAME=$1
REMOTE_HOST=$2

WORKSPACE="/home/${USERNAME}/Workspaces/custack_ws"
ROS_DISTRO="humble"

if [ -z "$USERNAME" ] || [ -z "$REMOTE_HOST" ]; then
    echo "Usage: $0 <username> <remote_host>"
    exit 1
fi

echo "package upload start ..."
rsync -avzd markerpos_pub ${USERNAME}@${REMOTE_HOST}:${WORKSPACE}/src/
if [ $? -ne 0 ]; then
    echo -e "\e[31m[E]: package upload failed!\e[0m"
    exit 1
fi

echo "building package start ...."
ssh ${USERNAME}@${REMOTE_HOST} \
    "cd ${WORKSPACE} && \
    rm -rf build install log && \
    source /opt/ros/${ROS_DISTRO}/setup.bash && \
    colcon build --symlink-install \
        --allow-overriding cv_bridge image_geometry \
        --cmake-args -DOpenCV_DIR=/usr/local/lib/cmake/opencv4 \
        --packages-select cv_bridge image_geometry && \
    source install/setup.bash && \
    colcon build --symlink-install \
        --cmake-args -DOpenCV_DIR=/usr/local/lib/cmake/opencv4 \
        --packages-select markerpos_pub"
if [ $? -ne 0 ]; then
    echo -e "\e[31m[E]: package build failed!\e[0m]"
    exit 1
fi
