#!/bin/bash
set -e

# 同期先の設定
REMOTE_USER="nvidia"
REMOTE_HOST="jetson.local"
ROS_DISTRO="humble"
WORKSPACE_DIR="/home/nvidia/Workspaces/custack_ws"
REMOTE_DIR="${WORKSPACE_DIR}/src/markerpos_server"

echo "Starting markerpos_server on remote Jetson device..."

ssh -f ${REMOTE_USER}@${REMOTE_HOST} \
    "cd ${WORKSPACE_DIR} && \
    export ROS_DOMAIN_ID=10 && \
    source /opt/ros/${ROS_DISTRO}/setup.bash && \
    source install/setup.bash && \
    nohup ros2 run markerpos_server markerpos_pub > /dev/null 2>&1 &"

echo "start markerpos_server."
