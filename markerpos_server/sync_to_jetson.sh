#!/bin/bash
set -e

# 同期先の設定
REMOTE_USER="nvidia"
REMOTE_HOST="jetson.local"
ROS_DISTRO="humble"
WORKSPACE_DIR="/home/nvidia/Workspaces/custack_ws"
REMOTE_DIR="${WORKSPACE_DIR}/src/markerpos_server"


# SSH接続を共有するための設定 (ControlMaster)
SSH_SOCKET="/tmp/ssh_socket_jetson_sync_$$"
# スクリプト終了時にマスター接続を閉じる
trap "ssh -S $SSH_SOCKET -O exit ${REMOTE_USER}@${REMOTE_HOST} >/dev/null 2>&1" EXIT

# マスター接続を確立 (ここで1回だけパスワード入力)
ssh -M -S $SSH_SOCKET -f -N ${REMOTE_USER}@${REMOTE_HOST}


# 時刻同期チェック
echo "Checking time synchronization..."
LOCAL_TIME=$(date +%s)
REMOTE_TIME=$(ssh -S $SSH_SOCKET "${REMOTE_USER}@${REMOTE_HOST}" date +%s)
DIFF=$((LOCAL_TIME - REMOTE_TIME))
# 絶対値を取得
if [ $DIFF -lt 0 ]; then DIFF=$(( -DIFF )); fi

if [ $DIFF -gt 10 ]; then
    echo "Error: Time difference is too large (${DIFF}s). Please sync clocks."
    exit 1
fi

# スクリプトのあるディレクトリを同期元とする
SOURCE_DIR="$(cd "$(dirname "$0")" && pwd)/markerpos_server"

echo "Syncing from ${SOURCE_DIR} to ${REMOTE_USER}@${REMOTE_HOST}:${REMOTE_DIR}..."

# rsyncで同期
# -a: アーカイブモード (再帰的、属性維持など)
# -v: 詳細出力
# -z: 圧縮転送
# -u: 転送先のファイルの方が新しい場合はスキップ
# --delete: 元にないファイルを削除 (完全同期)
rsync -avzu --delete \
    -e "ssh -S $SSH_SOCKET" \
    --exclude '.git/' \
    --exclude 'build/' \
    --exclude 'install/' \
    --exclude 'log/' \
    "${SOURCE_DIR}/" "${REMOTE_USER}@${REMOTE_HOST}:${REMOTE_DIR}/"

echo "Building on remote Jetson device..."

ssh -S $SSH_SOCKET ${REMOTE_USER}@${REMOTE_HOST} \
    "cd ${WORKSPACE_DIR} && \
    rm -rf build install log && \
    source /opt/ros/${ROS_DISTRO}/setup.bash && \
    colcon build --symlink-install \
        --allow-overriding cv_bridge image_geometry \
        --cmake-args -DOpenCV_DIR=/usr/local/lib/cmake/opencv4 \
        --packages-select cv_bridge image_geometry && \
    source install/setup.bash && \
    colcon build --symlink-install \
        --cmake-args -DOpenCV_DIR=/usr/local/lib/cmake/opencv4 \
        --packages-select markerpos_server ros_tcp_endpoint"

echo "Sync and build complete."