#!/usr/bin/env bash
set -e

# スクリプトの配置ディレクトリ（ワークスペースルート）を取得して移動
WS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$WS_DIR"

BUILD_FIRST=false
ARGS=()

for arg in "$@"; do
    case "$arg" in
        --build|-b)
            BUILD_FIRST=true
            ;;
        --help|-h)
            echo "使用方法: ./run_router.sh [オプション] [ROS引数...]"
            echo ""
            echo "スクリプトオプション:"
            echo "  -b, --build       実行前に colcon build を実行する"
            echo "  -h, --help        このヘルプを表示する"
            echo ""
            echo "設定例:"
            echo "  ./run_router.sh --ros-args --params-file src/custack_router/config/router_config.yaml"
            echo "  ./run_router.sh --ros-args -p enable_serial:=false"
            exit 0
            ;;
        *)
            ARGS+=("$arg")
            ;;
    esac
done

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

# 2. --build 指定時のビルド
if [ "$BUILD_FIRST" = true ]; then
    echo "🔨 custack_router をビルド中..."
    colcon build --symlink-install --packages-select custack_router
fi

# 3. ワークスペースオーバーレイ環境変数の読み込み
if [ -f "install/setup.bash" ]; then
    # shellcheck source=/dev/null
    source install/setup.bash
else
    echo "⚠️  install/setup.bash が見つかりません。ビルドを実行します..."
    colcon build --symlink-install --packages-select custack_router
    # shellcheck source=/dev/null
    source install/setup.bash
fi

# 4. ネットワークおよびROS環境設定
export ROS_DOMAIN_ID=1
export RCUTILS_COLORIZED_OUTPUT=1  # ログの色付けを有効化

# デフォルト設定ファイルが存在する場合は引数に追加（未指定時）
CONFIG_FILE="$WS_DIR/src/custack_router/config/router_config.yaml"
if [ ${#ARGS[@]} -eq 0 ] && [ -f "$CONFIG_FILE" ]; then
    ARGS=("--ros-args" "--params-file" "$CONFIG_FILE")
fi

echo "========================================="
echo " 🚀 Starting custack_router"
echo " 📁 Working Directory : $WS_DIR"
echo " 🌐 ROS_DOMAIN_ID     : $ROS_DOMAIN_ID"
echo "========================================="

# 5. プログラム実行
exec ros2 run custack_router router_node "${ARGS[@]}"
