#!/usr/bin/env bash
set -e

# スクリプトの配置ディレクトリ（ワークスペースルート）を取得して移動
WS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$WS_DIR"

echo "========================================="
echo " 🧹 Host Build Script (Clean & Build)"
echo " 📁 Workspace : $WS_DIR"
echo " 📦 Packages  : custack_router, custack_msgs"
echo "========================================="

# 1. ROS 2 アンダーレイ環境変数の読み込み
if [ -n "$ROS_DISTRO" ] && [ -f "/opt/ros/$ROS_DISTRO/setup.bash" ]; then
    # shellcheck source=/dev/null
    source "/opt/ros/$ROS_DISTRO/setup.bash"
elif [ -f "/opt/ros/humble/setup.bash" ]; then
    # shellcheck source=/dev/null
    source /opt/ros/humble/setup.bash
elif [ -f "/opt/ros/jazzy/setup.bash" ]; then
    # shellcheck source=/dev/null
    source /opt/ros/jazzy/setup.bash
else
    echo "❌ エラー: ROS 2 setup.bash (/opt/ros/humble または /opt/ros/jazzy) が見つかりません。" >&2
    exit 1
fi

# 2. クリーン処理 (build, install, log の削除)
echo "🗑️  クリーン中 (build, install, log を削除)..."
rm -rf "$WS_DIR/build" "$WS_DIR/install" "$WS_DIR/log"

# 3. ビルド実行
echo "🔨 ビルド開始..."
colcon build --symlink-install --packages-up-to custack_router "$@"

echo "========================================="
echo " ✅ Host ビルド完了!"
echo " 💡 環境変数を反映するには以下のコマンドを実行してください:"
echo "    source install/setup.bash"
echo "========================================="
