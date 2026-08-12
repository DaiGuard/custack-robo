#!/usr/bin/env bash
set -e

# スクリプトの配置ディレクトリ（ワークスペースルート）を取得して移動
WS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$WS_DIR"

# オプション解析（--build でビルド後に実行、--help でヘルプ表示）
BUILD_FIRST=false
ARGS=()

for arg in "$@"; do
    case "$arg" in
        --build|-b)
            BUILD_FIRST=true
            ;;
        --help|-h)
            echo "使用方法: ./run_locator.sh [オプション] [ノード引数...]"
            echo ""
            echo "スクリプトオプション:"
            echo "  -b, --build       実行前に colcon build を実行する"
            echo "  -h, --help        このヘルプを表示する"
            echo ""
            echo "ノード引数の例:"
            echo "  --headless        ヘッドレスモードで起動"
            echo "  --ros-args -p camera_index:=0"
            exit 0
            ;;
        *)
            ARGS+=("$arg")
            ;;
    esac
done

# 1. ROS 2 アンダーレイ環境変数の読み込み
if [ -f "/opt/ros/humble/setup.bash" ]; then
    # shellcheck source=/dev/null
    source /opt/ros/humble/setup.bash
else
    echo "❌ エラー: /opt/ros/humble/setup.bash が見つかりません。" >&2
    exit 1
fi

# 2. --build 指定時のビルド
if [ "$BUILD_FIRST" = true ]; then
    echo "🔨 custack_locator_cpp をビルド中..."
    colcon build --symlink-install --packages-select custack_locator_cpp
fi

# 3. ワークスペースオーバーレイ環境変数の読み込み
if [ -f "install/setup.bash" ]; then
    # shellcheck source=/dev/null
    source install/setup.bash
else
    echo "⚠️  install/setup.bash が見つかりません。ビルドを実行します..."
    colcon build --symlink-install --packages-select custack_locator_cpp
    # shellcheck source=/dev/null
    source install/setup.bash
fi

# 4. ネットワークおよびROS環境設定
export ROS_DOMAIN_ID=1
export RCUTILS_COLORIZED_OUTPUT=1  # ログの色付けを有効化

echo "========================================="
echo " 🚀 Starting custack_locator_cpp"
echo " 📁 Working Directory : $WS_DIR"
echo " 🌐 ROS_DOMAIN_ID     : $ROS_DOMAIN_ID"
echo "========================================="

# 5. プログラム実行（引数をそのままパススルー）
exec ros2 run custack_locator_cpp locator_node "${ARGS[@]}"
