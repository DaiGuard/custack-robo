#!/bin/bash
set -e

# 同期先の設定
REMOTE_USER="nvidia"
REMOTE_HOST="jetson.local"
ROS_DISTRO="humble"
DOMAIN_ID=10
WORKSPACE_DIR="/home/nvidia/Workspaces/custack_ws"
REMOTE_DIR="${WORKSPACE_DIR}/src/markerpos_server"


# SSH接続を共有するための設定 (ControlMaster)
SSH_SOCKET="/tmp/ssh_socket_jetson_sync_$$"
# スクリプト終了時にマスター接続を閉じる
trap "ssh -S $SSH_SOCKET -O exit ${REMOTE_USER}@${REMOTE_HOST} >/dev/null 2>&1" EXIT

# マスター接続を確立 (ここで1回だけパスワード入力)
ssh -M -S $SSH_SOCKET -f -N ${REMOTE_USER}@${REMOTE_HOST}


# JetsonのIPアドレスを取得
JETSON_IP=$(ssh -S $SSH_SOCKET ${REMOTE_USER}@${REMOTE_HOST} "hostname -I | awk '{print \$1}'")
echo "Jetson IP: ${JETSON_IP}"

echo "Starting markerpos_server on remote Jetson device..."

ssh -S $SSH_SOCKET \
    -f ${REMOTE_USER}@${REMOTE_HOST} \
    "cd ${WORKSPACE_DIR} && \
    source /opt/ros/${ROS_DISTRO}/setup.bash && \
    source install/setup.bash && \
    nohup ros2 launch markerpos_server markerpos_server.launch.py \
        domain_id:=${DOMAIN_ID} client_ip:=${JETSON_IP} > /dev/null 2>&1 &"

echo "start markerpos_server."
