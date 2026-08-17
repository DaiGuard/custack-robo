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
│   ├── custack_msgs        # ROS 2 カスタムメッセージ定義 (RobotPoseArray, RobotPose)
│   ├── ROS-TCP-Endpoint    # Unity 連携用 TCP エンドポイント (レガシー)
│   └── vision_opencv       # ROS 2 と OpenCV の相互変換 (cv_bridge 等)
├── install/                # ビルド生成物
├── build/                  # 中間ビルドディレクトリ
└── log/                    # ビルド・実行ログ
```

---

## 🌐 ノード間トピック & 通信仕様

```mermaid
flowchart LR
    Cam[天井カメラ] --> Locator["locator_node (Jetson)"]
    Locator -->|/camera/image_raw [sensor_msgs/Image]| Web["web_server (Jetson)"]
    Locator -->|/robot_poses [custack_msgs/RobotPoseArray]| Router["router_node (Host PC)"]
    Router -->|POSIX SHM /dev/shm/custack_robot_poses (20B/台)| Unity["Unity 6 (Host PC)"]
    Unity -->|POSIX SHM /dev/shm/custack_controller_cmd| Router
    Router -->|SET/STP (115200bps)| Atoms["M5Atom x 3台 (robot_bridge)"]
    Atoms -->|TLM (115200bps)| Router
```

### 1. ROS 2 トピック一覧
| トピック名 | メッセージ型 | 送信元 | 受信先 | 説明 |
| :--- | :--- | :--- | :--- | :--- |
| `/robot_poses` | `custack_msgs/msg/RobotPoseArray` | `locator_node` | `router_node` | 推定されたロボット 3 台分の正規化座標 (-1.0〜1.0) と回転角 |
| `/camera/image_raw` | `sensor_msgs/msg/Image` | `locator_node` | `web_server` | カメラ生画像またはキャリブレーション重畳映像 |

### 2. POSIX 共有メモリ仕様 (`custack_router`)
* **共有メモリパス**: `/dev/shm/custack_robot_poses`
* **アクセス制御**: Seqlock (ロックフリー、奇数: 書込中, 偶数: 確定)
* **構造体レイアウト** (`shared_memory_types.h` / `Pack = 1` / 20 Bytes):
  ```cpp
  struct SharedRobotPose {
      int32_t id;           // AprilTag ID (-1: 未検出)
      float x;              // 投影面座標 (-1.0 〜 1.0)
      float y;              // 投影面座標 (-1.0 〜 1.0)
      float theta;          // 姿勢角 (radian)
      uint8_t leg_id;       // 脚ユニットID (0x01: Omni, 0x02: Tire, 0x03: Crawler)
      uint8_t arm_right_id; // 右アーム武器ID (0x01: Gatling, 0x02: Sword, 0x03: LaserCannon)
      uint8_t arm_left_id;  // 左アーム武器ID (0x01: Gatling, 0x02: Sword, 0x03: LaserCannon)
      uint8_t status;       // テレメトリ受信ステータス (0: 未受信, 1: 正常受信)
  };
  struct SharedRobotPoseData {
      uint32_t version;               // プロトコルバージョン (1)
      uint32_t sequence;              // Seqlock シーケンス
      uint64_t timestamp_ns;          // ナノ秒タイムスタンプ
      uint32_t count;                 // 検出台数 (最大32)
      SharedRobotPose poses[32];      // 機体姿勢 & デバイス情報配列
  };
  ```

---

## 🔨 クリーン＆ビルド手順

```bash
# Jetson 環境
cd custack_ws && ./build_jetson.sh

# ホストPC環境
cd custack_ws && ./build_host.sh
```

---

## 🚀 起動方法

```bash
# Jetson: 位置推定ノード起動
./run_locator.sh

# Jetson: Web UI 起動
./run_web.sh

# ホストPC: ルーターノード起動 (SHM ↔ 3ch シリアル)
./run_router.sh
```
