# custack_ws ワークスペース概要

本ディレクトリ (`custack_ws`) は、Jetson 上で動作する **CuStack ロボットシステム** の ROS 2 (Humble) ワークスペースです。
カメラ画像処理（VPI / OpenCV）、AprilTag 検出、自己位置推定、キャリブレーション用 Web UI、Unity 連携などのノードが含まれています。

---

## 📁 ディレクトリ・パッケージ構成

```text
custack_ws/
├── run_locator.sh          # locator_node 起動用スクリプト
├── run_web.sh              # web_server 起動用スクリプト
├── camera_calib.yaml       # カメラ内部パラメータ・歪み係数
├── camera_config.yaml      # カメラハードウェア設定（露出等）
├── homography.yaml         # 床面・俯瞰変換用ホモグラフィ行列
├── src/
│   ├── custack_locator_cpp # [C++] カメラ画像処理・AprilTag・位置推定ノード (locator_node)
│   ├── custack_web         # [Python] キャリブレーション・映像配信用 Web サーバー (web_server)
│   ├── custack_msgs        # ROS 2 カスタムメッセージ定義
│   ├── ROS-TCP-Endpoint    # Unity 連携用 TCP エンドポイント
│   └── vision_opencv       # ROS 2 と OpenCV の相互変換 (cv_bridge 等)
├── install/                # ビルド生成物
├── build/                  # 中間ビルドディレクトリ
└── log/                    # ビルド・実行ログ
```

---

## 🚀 起動方法

各ノードの実行に必要な ROS 環境変数やディレクトリ移動、設定ファイルの読み込みを自動化するスクリプトを用意しています。

### 1. 位置推定・カメラ認識ノード (`custack_locator_cpp`)

```bash
# 通常起動
./run_locator.sh

# ビルドしてから起動
./run_locator.sh -b

# ヘッドレスモード（GUI描画なし）で起動
./run_locator.sh --headless

# ROSパラメータを指定して起動
./run_locator.sh --ros-args -p camera_index:=0

# ヘルプ確認
./run_locator.sh -h
```

### 2. Web UI / キャリブレーションサーバー (`custack_web`)

```bash
# 通常起動
./run_web.sh

# ビルドしてから起動
./run_web.sh -b

# ヘルプ確認
./run_web.sh -h
```
* 起動後、ブラウザから `http://localhost:8000` または `http://<JetsonのIP>:8000` にアクセスしてください。

---

## ⚙️ 環境設定・共通仕様

* **ROS 2 ディストリビューション**: ROS 2 Humble
* **ROS_DOMAIN_ID**: `1`
* **設定ファイル仕様**:
  `locator_node` は起動時のカレントワーキングディレクトリにある `camera_calib.yaml`, `camera_config.yaml`, `homography.yaml` を読み書きします（各 `run_*.sh` スクリプト内でワークスペース直下へ自動移動して実行されます）。

---

## 🔨 手動ビルド手順

個別に `colcon build` を行う場合の手順です：

```bash
source /opt/ros/humble/setup.bash

# 全パッケージビルド
colcon build --symlink-install

# 特定パッケージのみビルド
colcon build --symlink-install --packages-select custack_locator_cpp custack_web
```
