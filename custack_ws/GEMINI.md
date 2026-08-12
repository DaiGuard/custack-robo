# custack_ws ワークスペース概要

本ディレクトリ (`custack_ws`) は、Jetson およびホストPC上で動作する **CuStack ロボットシステム** の ROS 2 (Humble) ワークスペースです。
カメラ画像処理（VPI / OpenCV）、AprilTag 検出、自己位置推定、キャリブレーション用 Web UI、Unity 連携用 POSIX 共有メモリ・マルチシリアルルーターなどのノードが含まれています。

---

## 📁 ディレクトリ・パッケージ構成

```text
custack_ws/
├── build_jetson.sh         # Jetson 用クリーン＆ビルドスクリプト (locator + web)
├── build_host.sh           # ホスト用クリーン＆ビルドスクリプト (router)
├── run_locator.sh          # locator_node 起動用スクリプト
├── run_router.sh           # router_node 起動用スクリプト (SHM & 3ch Serial)
├── run_web.sh              # web_server 起動用スクリプト (Web UI)
├── camera_calib.yaml       # カメラ内部パラメータ・歪み係数
├── camera_config.yaml      # カメラハードウェア設定（露出等）
├── homography.yaml         # 床面・俯瞰変換用ホモグラフィ行列
├── src/
│   ├── custack_locator_cpp # [C++] カメラ画像処理・AprilTag・位置推定ノード (locator_node)
│   ├── custack_router      # [C++] POSIX共有メモリ & 3台M5Atomシリアルルーター (router_node)
│   ├── custack_web         # [Python] キャリブレーション・映像配信用 Web サーバー (web_server)
│   ├── custack_msgs        # ROS 2 カスタムメッセージ定義
│   ├── ROS-TCP-Endpoint    # Unity 連携用 TCP エンドポイント (レガシー)
│   └── vision_opencv       # ROS 2 と OpenCV の相互変換 (cv_bridge 等)
├── install/                # ビルド生成物
├── build/                  # 中間ビルドディレクトリ
└── log/                    # ビルド・実行ログ
```

---

## 🔨 クリーン＆ビルド手順

環境ごとに必要なパッケージのみをクリーンビルドできるスクリプトを用意しています。
実行時に自動で `build/`, `install/`, `log/` を削除してからビルドを実行します。

### 1. Jetson 環境 (`custack_locator_cpp`, `custack_web`, `custack_msgs`)
```bash
cd custack_ws
./build_jetson.sh
```

### 2. ホストPC環境 (`custack_router`, `custack_msgs`)
```bash
cd custack_ws
./build_host.sh
```

---

## 🚀 起動方法

各ノードの実行に必要な ROS 環境変数やディレクトリ移動、設定ファイルの読み込みを自動化するスクリプトを用意しています。

### 1. 位置推定・カメラ認識ノード (`custack_locator_cpp`) 【Jetson】

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

### 2. ルーターノード (`custack_router`) 【ホストPC】

```bash
# 通常起動 (デフォルト設定ファイル src/custack_router/config/router_config.yaml を自動読み込み)
./run_router.sh

# ビルドしてから起動
./run_router.sh -b

# シリアル無効化 (SHM のみテスト)
./run_router.sh --ros-args -p enable_serial:=false

# ヘルプ確認
./run_router.sh -h
```

### 3. Web UI / キャリブレーションサーバー (`custack_web`) 【Jetson】

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
