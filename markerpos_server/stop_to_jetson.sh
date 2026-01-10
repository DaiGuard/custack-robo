#!/bin/bash
set -e

# 同期先の設定
REMOTE_USER="nvidia"
REMOTE_HOST="jetson.local"
ROS_DISTRO="humble"
WORKSPACE_DIR="/home/nvidia/Workspaces/custack_ws"
REMOTE_DIR="${WORKSPACE_DIR}/src/markerpos_server"

echo "Stopping markerpos_server on remote Jetson device..."

ssh ${REMOTE_USER}@${REMOTE_HOST} \
    pkill -e -f markerpos_pub

echo " stopped markerpos_server."
