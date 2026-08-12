# custack_router 実装計画書 (Implementation Plan)

## 1. 目的 & 概要 (Goal Description)

本タスクでは、ROS 2 Humble環境に新規 C++ パッケージ **`custack_router`** を作成します。
従来の `ROS-TCP-Endpoint` による通信オーバーヘッドと遅延を解消し、マルチロボット対応・超低遅延・高スループットなシステム連携を実現します。

### システム構成
- **Jetson**: `locator_node` (カメラ画像処理・AprilTag検出・位置推定) が動作
- **Linux 端末 (ホストPC / ゲーム実行環境)**: `custack_router` (C++) と **Unity プロジェクト** が同一マシン上で動作
- **ネットワーク**: Jetson と Linux 端末間を有線 LAN で直結し、ROS 2 (DDS) の `ROS_DOMAIN_ID` を共有して `/robot_poses` トピックを送受信
- **ハードウェア**: Linux 端末に 3台の M5Atom-Matrix (`robot_bridge`) が USB 接続 (`/dev/ttyUSB0`, `/dev/ttyUSB1`, `/dev/ttyUSB2`)

---

## 2. システムアーキテクチャ

```mermaid
flowchart TD
    subgraph Jetson
        LOC[locator_node (C++)]
    end

    subgraph Linux端末
        LOC -->|有線LAN / ROS 2 Topic\n/robot_poses| ROUTER[custack_router (C++)]
        
        subgraph POSIX共有メモリ (/dev/shm)
            SHM_POS[(/custack_robot_poses\nSeqlock ロックフリー)]
            SHM_CMD[(/custack_controller_cmd\n3コントローラ対応)]
        end

        ROUTER -->|Seqlock 書き込み| SHM_POS
        SHM_POS -->|ゼロコピー読取| UNITY_POS[Unity: SharedMemoryManager]

        UNITY_PAD[Unity: 3台のゲームパッド入力] -->|書き込み| SHM_CMD
        SHM_CMD -->|読取 (50Hz)| ROUTER

        ROUTER -->|/dev/ttyUSB0 115200bps| M5_0[M5Atom Matrix 0]
        ROUTER -->|/dev/ttyUSB1 115200bps| M5_1[M5Atom Matrix 1]
        ROUTER -->|/dev/ttyUSB2 115200bps| M5_2[M5Atom Matrix 2]
    end

    subgraph ロボット (各機体)
        M5_0 -->|ESP-NOW 100Hz| ROBOT_0[Robot 0 (Core2)]
        M5_1 -->|ESP-NOW 100Hz| ROBOT_1[Robot 1 (Core2)]
        M5_2 -->|ESP-NOW 100Hz| ROBOT_2[Robot 2 (Core2)]
    end
```

---

## 3. 共有メモリ・プロトコル設計

### (1) ロボット座標共有メモリ (`/custack_robot_poses`)
- **Seqlock 方式**: 書き込み側 (`custack_router`) が書き込み開始時に `sequence` を奇数化し、書き込み完了後に偶数化。Unity (C#) 側は `sequence` の一致と偶数チェックにより、OSロック (Mutex) なしに最新フレームを数マイクロ秒以下で取得可能。

```cpp
#pragma pack(push, 1)

struct SharedRobotPose {
    int32_t id;      // AprilTag ID
    float x;         // 投影面座標 -1.0 ~ 1.0
    float y;         // 投影面座標 -1.0 ~ 1.0
    float theta;     // 回転角 (radian)
};

struct SharedRobotPoseData {
    uint32_t version;          // 構造体バージョン (1)
    uint32_t sequence;         // Seqlock シーケンス番号 (奇数: 書き込み中, 偶数: 確定)
    uint64_t timestamp_ns;     // 受信タイムスタンプ (ナノ秒)
    uint32_t count;            // 検出ロボット数 (最大32)
    SharedRobotPose poses[32]; // 固定長配列
};

#pragma pack(pop)
```

### (2) コントローラ入力共有メモリ (`/custack_controller_cmd`)
- 最大 8 台（今回は 3 台使用）のコントローラ入力を配列で保持。

```cpp
#pragma pack(push, 1)

struct SingleControllerCommand {
    int16_t vx;                // 移動速度 Vx (-1000 ~ 1000)
    int16_t vy;                // 移動速度 Vy (-1000 ~ 1000)
    int16_t omega;             // 旋回速度 Omega (-1000 ~ 1000)
    uint8_t arm_right;         // 右アーム (0: 停止, 1: 起動)
    uint8_t arm_left;          // 左アーム (0: 停止, 1: 起動)
    uint8_t active;            // 有効フラグ (0: 無効, 1: 有効)
    uint8_t reserved;          // アライメント用
};

#define MAX_CONTROLLERS 8

struct SharedControllerData {
    uint32_t version;          // 構造体バージョン (1)
    uint32_t sequence;         // 更新シーケンス番号
    uint64_t timestamp_ms;     // 送信タイムスタンプ (ms)
    uint32_t count;            // コントローラ数 (3)
    SingleControllerCommand controllers[MAX_CONTROLLERS];
};

#pragma pack(pop)
```

### (3) シリアル通信・M5Atom Matrix コマンド
- 各ポート (`/dev/ttyUSB0`, `/dev/ttyUSB1`, `/dev/ttyUSB2`) へ独立したスレッドまたはタイマーで定期送信 (50Hz)。
- コマンド: `SET,vx,vy,omega,arm_r,arm_l\n`
- Unity または共有メモリからの入力途絶時 (タイムアウト: 500ms〜1000ms): 自動的に `STP\n` を送信して安全停止。
- USBの未接続・切断時の自動再接続機能 (Reconnect)。

---

## 4. 変更・追加ファイル一覧 (Proposed Changes)

### パッケージ: `custack_router` (`custack_ws/src/custack_router/`)

#### `custack_ws/src/custack_router/package.xml`
- ROS 2 Humble パッケージ定義 (`rclcpp`, `custack_msgs`)

#### `custack_ws/src/custack_router/CMakeLists.txt`
- C++17 ビルド設定、`pthread`, `rt` (POSIX SHM) リンク、実行可能ファイル `router_node`

#### `custack_ws/src/custack_router/include/custack_router/shared_memory_types.h`
- 共有メモリデータ構造体の共通ヘッダー定義

#### `custack_ws/src/custack_router/include/custack_router/posix_shm.hpp`
- POSIX 共有メモリ (`shm_open`, `mmap`, `ftruncate`) 管理クラス (Seqlock 書き込み/読み出し対応)

#### `custack_ws/src/custack_router/include/custack_router/serial_port.hpp`
- POSIX `termios` による低遅延・ノンブロッキングシリアルポート送受信クラス (自動再接続対応)

#### `custack_ws/src/custack_router/src/router_node.cpp`
- メインノード実装:
  - `/robot_poses` サブスクライブ → `SharedRobotPoseData` 共有メモリ書き込み
  - `SharedControllerData` 共有メモリ読み出し → 3台のシリアルポートへ `SET` コマンド分配送信 (50Hz)
  - 切断監視・タイムアウト保護

#### `custack_ws/src/custack_router/config/router_config.yaml`
- シリアルポート一覧 (`["/dev/ttyUSB0", "/dev/ttyUSB1", "/dev/ttyUSB2"]`)、ボーレート (`115200`)、送信周期 (`50.0` Hz)、タイムアウト等の設定

#### `custack_ws/run_router.sh`
- `custack_ws` 直下の起動スクリプト (ビルドオプション `-b` やヘルプ対応)

---

### Unity プロジェクト側 (C# 連携用スクリプトの提供)

#### `custack-unity/Assets/Scenes/MultiDisplayProjects/Scripts/SharedMemoryManager.cs`
- POSIX 共有メモリ (`/dev/shm/custack_robot_poses`, `/dev/shm/custack_controller_cmd`) を直接読み書きする Unity コンポーネント。
- `RosManager.cs` と同様の座標変換・フィルタ処理を共有メモリ入力に対して実行。
- 3台のゲームパッド入力を取得し、3つの `SingleControllerCommand` にパックして共有メモリに書き込む。

---

## 5. 検証計画 (Verification Plan)

### (1) ビルド検証
```bash
cd custack_ws
./build_host.sh
```

### (2) 単体・結合テスト
1. **共有メモリ整合性・ゼロコピー動作テスト**:
   - テストツールまたはノードを用いてダミーの `/robot_poses` を発行し、共有メモリおよび Unity 側で遅延なく正しく受信できることを確認。
2. **マルチシリアル通信テスト**:
   - Unity から 3 つのコントローラ入力を共有メモリに書き込み、各シリアルポート (`/dev/ttyUSB0`, `/dev/ttyUSB1`, `/dev/ttyUSB2`) へ正しい `SET,vx,vy,omega,arm_r,arm_l\n` コマンドが 50Hz で送信されることを確認。
3. **安全フェイルセーフ確認**:
   - Unity を停止または入力が途絶した際、自動的に `STP\n` が送信され M5Atom 側のセーフティが働くことを確認。
