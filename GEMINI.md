# CuStack-Robo (ｶｽﾀｯｸﾛﾎﾞ) プロジェクト全体仕様・統合アーキテクチャ

本ドキュメントは、**CuStack-Robo (ｶｽﾀｯｸﾛﾎﾞ)** システムの全体像、各サブシステム（Hardware / Firmware / ROS 2 / Unity）の仕様、およびモジュール間連携プロトコルをまとめた統合仕様書です。

---

## 1. 概要

**CuStack-Robo** は、床面プロジェクションマッピング演出と小型自律/遠隔制御ロボットがリアルタイムにインタラクションする複合ゲーム・ロボットシステムです。

- **カメラ認識・位置推定 (Jetson / ROS 2 Humble)**: 天頂カメラ映像から NVIDIA VPI を用いて歪み補正・透視変換を行い、AprilTag (16H5, ID 0〜15 / 最大32台) により最大60Hzで複数台ロボットの絶対位置姿勢を高精度推定。1秒ごとのリアルタイム速度・レイテンシ統計を出力。
- **共有メモリ・通信中継 (Host PC / C++)**: POSIX 共有メモリ（Seqlock 方式）を介して Unity と超低遅延（>200Hz）で座標共有。Unity からの操作指令を 3ch シリアル通信（115200bps）で M5Atom ブリッジへ転送。実機から返信されるテレメトリ（デバイスID・バッテリー電圧）を共有メモリへ同期。
- **演出・バトルゲーム制御 (Host PC / Unity 6 URP)**: 2画面出力（PC管理画面 / プロジェクター床面投影）。リアルタイム位置追従、**実機装着ユニットのデバイスID自動認識**、装備（アーム武器・脚特性）×地形効果（泥・氷・溶岩等）による挙動変化、誘導ミサイルや弾幕を含むバトルゲームシステムとHUD。
- **実機ロボット制御 (M5Stack Core2 + ATtiny1614 x3)**: ESP-NOW（100Hz指令 / 500msテレメトリ）双方向無線通信。内部 I2C バス経由で 4 輪オムニホイール（`robot_leg`）および左右アーム（`robot_arm`）のデバイスID取得および高精度分散制御。攻撃時には実機ソレノイド/モーターが同期作動。
- **専用ハードウェア基板 (KiCad v10)**: 2S Li-ion 電源回路、6A 大電流 DCDC、M5Stack スタック用メイン基板、ポゴピン接続可能な 3 種の子局基板（パネライゼーション設計）。

---

## 2. システム全体アーキテクチャ & データフロー

```mermaid
flowchart TD
    subgraph Vision ["Jetson 認識系 (custack_ws)"]
        Cam["天頂カメラ (/dev/video0)"] -->|Y16 1080p 60fps| Loc["locator_node (VPI / AprilTag)"]
        Loc -->|ROS 2: /robot_poses| LocPub["RobotPoseArray (60Hz)"]
        Web["web_server (FastAPI:8000)"] -.->|Web UI| Loc
    end

    subgraph Host ["ホスト PC 演出・対戦ゲーム系"]
        LocPub -->|DDS / Network| Router["router_node (custack_router)"]
        
        subgraph SHM ["POSIX 共有メモリ (/dev/shm)"]
            SHM_Pose["/custack_robot_poses<br/>(Seqlock, 200Hz, 20B/台<br/>Pos + DeviceID)"]
            SHM_Cmd["/custack_controller_cmd<br/>(Seqlock, 50Hz)"]
        end
        
        Router -->|Write Pose + DeviceID| SHM_Pose
        SHM_Pose -->|Read| Unity["Unity 6 URP (custack-unity)"]
        
        Gamepads["PS5 コントローラ (1〜2台)"] --> Unity
        
        subgraph UnityGame ["Unity バトルシステム"]
            InputSys["ControllerInputManager<br/>(P1/P2/Keyboard)"] --> RM["RobotManager"]
            TerrainSys["TerrainManager<br/>(平地/森/泥/氷/溶岩)"] --> RM
            RM --> EqSys["RobotEquipment<br/>(デバイスID自動反映)"]
            EqSys --> CombatSys["Combat / WeaponBase<br/>(ピストル/ガトリング/ミサイル)"]
            CombatSys --> HealthSys["Health / BattleHUD<br/>(HP管理・勝敗判定)"]
        end

        Unity -->|Write| SHM_Cmd
        SHM_Cmd -->|Read| Router
        
        Unity -->|Display 1| Mon["PC ディスプレイ (操作・HUD)"]
        Unity -->|Display 2| Proj["プロジェクター (床面 1:1 投影)"]
    end

    subgraph RobotBridge ["通信ブリッジ (custack-robot)"]
        Router -->|USB CDC / 115200bps (SET/STP)| Atom["M5Atom Matrix (robot_bridge) x3"]
        Atom -->|USB CDC / 115200bps (TLM)| Router
        Atom -->|ESP-NOW 100Hz / 16B Command| Core2["M5Stack Core2 (robot_main)"]
        Core2 -->|ESP-NOW 500ms / 16B Telemetry| Atom
    end

    subgraph RobotBody ["実機ロボット本体 (custack-hardware & robot)"]
        Core2 -->|LCD & Sound| Face["表情アニメーション / バッテリーHUD"]
        Core2 -->|Power Switch| DCDC["5V 6A DCDC (OKL-T/6-W12N-C)"]
        
        subgraph I2C_Bus ["内部 I2C バス (400kHz, 4P Pogopin)"]
            Core2 -->|I2C: 0x30 Read ID / Write Vx,Vy,Omega| Leg["脚ユニット (ATtiny1614: robot_leg)"]
            Core2 -->|I2C: 0x31 Read ID / Write Duty,Pattern| ArmR["右腕 (ATtiny1614: robot_arm)"]
            Core2 -->|I2C: 0x32 Read ID / Write Duty,Pattern| ArmL["左腕 (ATtiny1614: robot_arm)"]
        end
        
        Leg -->|50Hz PWM x4| Servos["4輪 X配置 オムニホイール"]
        ArmR -->|20kHz PWM| ActR["右ソレノイド / モータ"]
        ArmL -->|20kHz PWM| ActL["左ソレノイド / モータ"]
    end
```

---

## 3. ディレクトリ構成 & サブエージェント体制

| ディレクトリ | 専任サブエージェント | 主な技術スタック | 管理領域・担当機能 | 詳細仕様書 |
| :--- | :--- | :--- | :--- | :--- |
| **`custack-hardware/`** | `agent-hardware` | KiCad v10, 回路・CAD | メイン基板、電源基板、アーム/脚基板、ポゴピン配置、回路設計 | [`custack-hardware/GEMINI.md`](file:///home/dai_guard/Workspaces/custack-robo/agent/custack-hardware/GEMINI.md) |
| **`custack-robot/`** | `agent-robot` | PlatformIO, C++, ATtiny | M5Stack Core2 メイン、M5Atom ブリッジ、ATtiny1614 脚・腕制御、デバイスIDテレメトリ | [`custack-robot/GEMINI.md`](file:///home/dai_guard/Workspaces/custack-robo/agent/custack-robot/GEMINI.md) |
| **`custack_ws/`** | `agent-ros` | ROS 2 Humble, C++, Python | VPI 画像処理・位置推定、共有メモリルーター、シリアルテレメトリパース、キャリブレーション Web | [`custack_ws/GEMINI.md`](file:///home/dai_guard/Workspaces/custack-robo/agent/custack_ws/GEMINI.md) |
| **`custack-unity/`** | `agent-unity` | Unity 6, URP, C#, Input System | プロジェクション演出、共有メモリ連携、対戦バトルシステム、デバイスID自動認識×地形効果 | [`custack-unity/GEMINI.md`](file:///home/dai_guard/Workspaces/custack-robo/agent/custack-unity/GEMINI.md) |
| **`mcp/`** | `agent-promoter` | Google Veo 3.1, Python, FastMCP | プロモーション戦略、CM絵コンテ・キービジュアル、Google Veo動画クリップ自動生成 | [`.agents/agent-promoter/agent.md`](file:///home/dai_guard/Workspaces/custack-robo/agent/.agents/agent-promoter/agent.md) |

---

## 4. エンドツーエンド デバイスID・兵装連携仕様

### 4.1 デバイスID定義対応表
| ユニット種別 | デバイスID | 兵装 / 機構名 | Unity 側ゲーム挙動・エフェクト | 実機側アクチュエータ動作 |
| :--- | :---: | :--- | :--- | :--- |
| **脚ユニット** (`0x30`) | `0x01` | **Omni (オムニホイール)** | 全方向スライド移動 / 泥沼で減速大 / 氷上で大スリップ | 4輪 50Hz PWM キネマティクス |
| | `0x02` | **Tire (二輪差動)** | 平地最高速 (125%) / ドリフト旋回 / 泥沼スタック | 左右差動 PWM |
| | `0x03` | **Crawler (キャタピラ)** | 悪路走破（泥・森の減速大幅カット） / 溶岩ダメージ 60% カット | 履帯 PWM |
| **アームユニット** (`0x31`/`0x32`) | `0x01` | **ガトリング** | 拡散黄色弾幕連射（威力 8, 15発/秒, $\pm 7.5^\circ$ 拡散） | 200ms 点滅振動駆動 |
| | `0x02` | **ソード** | 近接高威力・エメラルド斬撃波（威力 45, 速度 18, 寿命 0.35s, CD 0.45s） | 100ms 単発パルス駆動 |
| | `0x03` | **大型レーザーキャノン** | 極大高出力ブルーレーザー（威力 75, 速度 30, 寿命 3.5s, CD 1.8s） | 高出力トリガー駆動 |

---

---

## 5. モジュール間通信プロトコル & 機体・コントローラー対応関係

### 5.0 ゲームパッド・機体 (AprilTag)・シリアルブリッジ対応仕様 (3台専用構成)
| プレイヤー | 入力デバイス | 操作 AprilTag ID | 共有メモリスロット | USB シリアルポート | 無線中継 (M5Atom) | 実機ロボット (M5Stack) |
| :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **P1** | **Gamepad 0** (KB: WASD) | **タグ ID: 1** | `controllers[0]` | `/dev/custack_bridge_1` | Bridge #1 | ロボット #1 (Core2) |
| **P2** | **Gamepad 1** (KB: 矢印) | **タグ ID: 2** | `controllers[1]` | `/dev/custack_bridge_2` | Bridge #2 | ロボット #2 (Core2) |
| **P3** | **Gamepad 2** (KB: IJKL) | **タグ ID: 3** | `controllers[2]` | `/dev/custack_bridge_3` | Bridge #3 | ロボット #3 (Core2) |

### 5.1 POSIX 共有メモリ (C++ ↔ Unity 連携)
- メモリ名: `/custack_robot_poses` (Pose + DeviceID), `/custack_controller_cmd` (Command)
- 同期方式: **Seqlock**（ロックフリー・シーケンスロック、偶数: 確定 / 奇数: 更新中）
- `SharedRobotPose` 構造体レイアウト (`Pack = 1` / 20 Bytes):
  `int32_t id`, `float x`, `float y`, `float theta`, `uint8_t leg_id`, `uint8_t arm_right_id`, `uint8_t arm_left_id`, `uint8_t status`

### 5.2 シリアルプロトコル (Host PC ↔ M5Atom ブリッジ / 115200bps)
- `SET,vx,vy,omega,arm_r,arm_l\n`: PC ➔ Bridge（全軸速度 -1000〜1000, アーム起動 0/1）
- `STP\n`: PC ➔ Bridge（全停止フェールセーフ）
- `TLM\n`: PC ➔ Bridge（テレメトリ要求・1秒周期） / `TLM,leg_id,arm_r_id,arm_l_id,bat_mv\n`: Bridge ➔ PC（テレメトリ応答）

### 5.3 ESP-NOW プロトコル (M5Atom ↔ M5Stack Core2 / Ch 1)
- `EspNowCommandPacket` (Bridge ➔ Core2, 100Hz / 16B): 速度指令 & アーム起動フラグ
- `EspNowTelemetryPacket` (Core2 ➔ Bridge, 500ms / 16B): 各ユニットデバイスID (`leg_id`, `arm_r_id`, `arm_l_id`) & バッテリー電圧

### 5.4 I2C バスプロトコル (M5Stack Core2 ↔ ATtiny1614 / 10k〜400kHz)
- アドレス: `0x30` (脚), `0x31` (右腕), `0x32` (左腕)
- `REG_DEVICE_ID (0x00)`: 読み出しでデバイス種別を識別
- `REG_WATCHDOG (0x02)`: 毎周期書き込み（1000ms 未更新で安全停止）

---

## 6. クイックスタート & 実行手順

### 1. Jetson 環境 (位置推定・カメラ認識 & Web UI)
```bash
cd custack_ws
./build_jetson.sh        # クリーン＆ビルド
./run_locator.sh         # 位置推定ノード起動
# 別ターミナルで Web UI 起動 (http://<Jetson-IP>:8000)
./run_web.sh
```

### 2. ホスト PC 環境 (ルーター & Unity)
```bash
# 初回のみ: M5Atom USBシリアルポート固定化ルールの登録
sudo cp custack-robot/tools/99-custack-bridge.rules /etc/udev/rules.d/
sudo udevadm control --reload-rules && sudo udevadm trigger

cd custack_ws
./build_host.sh          # クリーン＆ビルド
./run_router.sh          # 共有メモリ・シリアルルーター起動 (/dev/custack_bridge_1..3)
```
Unity Hub から `custack-unity` を開き、`Assets/_Project/Scenes/Main/Main.unity` を実行します。

### 3. ロボットファームウェア書き込み (PlatformIO)
```bash
cd custack-robot
# ブリッジユニット書き込み (M5Atom)
pio run -d robot_bridge -t upload --upload-port /dev/ttyUSB0

# メインユニット書き込み (M5Stack Core2)
pio run -d robot_main -t upload --upload-port /dev/ttyUSB0

# 脚ユニット書き込み (ATtiny1614: -e omni / tire / crawler)
pio run -d robot_leg -e omni -t upload --upload-port /dev/ttyUSB0

# 右アーム書き込み (ATtiny1614: -e arm_right_gatling / arm_right_sword / arm_right_cannon)
pio run -d robot_arm -e arm_right_gatling -t upload --upload-port /dev/ttyUSB0

# 左アーム書き込み (ATtiny1614: -e arm_left_gatling / arm_left_sword / arm_left_cannon)
pio run -d robot_arm -e arm_left_sword -t upload --upload-port /dev/ttyUSB0
```
