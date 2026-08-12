# ｶｽﾀｯｸﾛﾎﾞ (CuStack-robo) 統合システム仕様書

本リポジトリは、複数台の小型ロボットによる対戦・協調ゲームおよびプロジェクション演出を実現する **CuStack ロボットシステム** の統合リポジトリです。

---

## 🏗️ システム全体アーキテクチャ

システムは **認識層 (Jetson)**、**中継・演出層 (ホストPC / Unity)**、**実機制御層 (ロボット群)** の3つのレイヤーで構成されています。

```mermaid
flowchart TD
    subgraph Jetson [Jetson 環境 - 画像認識 & 自己位置推定]
        Cam[天井カメラ (USB/CSI)] --> LocNode["custack_locator_cpp (locator_node)"]
        LocNode -->|/robot_poses (ROS 2 DDS)| RouterNode
        LocNode -->|/camera/image_raw| WebServer["custack_web (web_server)"]
        WebServer --> Browser[Webブラウザ (キャリブレーションUI)]
    end

    subgraph HostPC [ホストPC環境 - ルーティング & ゲーム演出]
        RouterNode["custack_router (router_node)"]
        RouterNode -->|POSIX 共有メモリ /dev/shm/custack_robot_poses| UnityApp["custack-unity (Unity 6 URP)"]
        UnityApp -.->|プロジェクション演出| Projector[プロジェクター投影]
        Gamepad[ゲームコントローラー] -.-> UnityApp
        RouterNode -->|USB CDC シリアル (115200bps)| Bridge["robot_bridge (M5Atom Matrix)"]
    end

    subgraph Robot [実機ロボット層 (最大3台)]
        Bridge -->|ESP-NOW (100Hz / 16Bパケット)| MainUnit["robot_main (M5Stack Core2)"]
        MainUnit -->|I2C (10kHz) - 0x30| LegUnit["robot_leg (ATtiny1614: 4輪オムニ)"]
        MainUnit -->|I2C (10kHz) - 0x31| RArmUnit["robot_arm (ATtiny1614: 右アーム)"]
        MainUnit -->|I2C (10kHz) - 0x32| LArmUnit["robot_arm (ATtiny1614: 左アーム)"]
    end
```

---

## 📁 リポジトリ・サブプロジェクト構成

| ディレクトリ | 役割 / 主要技術 | ドキュメント |
| :--- | :--- | :--- |
| [`custack_ws/`](file:///home/dai_guard/Workspaces/custack-robo/unity/custack_ws) | **ROS 2 (Humble) ワークスペース**<br>カメラ位置推定、POSIX共有メモリ & 3chシリアルルーター、キャリブレーションWeb UI | [GEMINI.md](file:///home/dai_guard/Workspaces/custack-robo/unity/custack_ws/GEMINI.md) |
| [`custack-unity/`](file:///home/dai_guard/Workspaces/custack-robo/unity/custack-unity) | **Unity 6 プロジェクト (URP)**<br>POSIX 共有メモリによる高速姿勢取得、プロジェクションゲーム演出 | [GEMINI.md](file:///home/dai_guard/Workspaces/custack-robo/unity/custack-unity/GEMINI.md) |
| [`custack-robot/`](file:///home/dai_guard/Workspaces/custack-robo/unity/custack-robot) | **実機ロボットファームウェア群 (PlatformIO)**<br>M5Stack Core2, M5Atom Matrix, ATtiny1614 制御コード | [GEMINI.md](file:///home/dai_guard/Workspaces/custack-robo/unity/custack-robot/GEMINI.md) |
| [`custack-hardware/`](file:///home/dai_guard/Workspaces/custack-robo/unity/custack-hardware) | **ハードウェア・回路設計**<br>ロボット本体および基板設計データ | - |
| [`aiplan/`](file:///home/dai_guard/Workspaces/custack-robo/unity/aiplan) | **設計書・実装計画書**<br>各コンポーネントの設計ドキュメント | - |

---

## 📡 通信プロトコル全体像

1. **認識・自己位置推定 (`Jetson ➔ ホストPC`)**:
   - **通信方式**: ROS 2 Humble DDS (`ROS_DOMAIN_ID=1`)
   - **トピック**: `/robot_poses` ([`custack_msgs/msg/RobotPoseArray`](file:///home/dai_guard/Workspaces/custack-robo/unity/custack_ws/src/custack_msgs/msg/RobotPoseArray.msg))
2. **ホスト内高速通信 (`custack_router ➔ custack-unity`)**:
   - **通信方式**: POSIX 共有メモリ (`/dev/shm/custack_robot_poses`)
   - **同期方式**: Seqlock ロックフリー構造体（最大32機体対応、60Hz〜120Hz描画追従）
3. **ホスト ➔ 送信ブリッジ (`custack_router ➔ robot_bridge`)**:
   - **通信方式**: USB CDC シリアル (115200bps, 改行テキストコマンド `SET,vx,vy,omega,arm_r,arm_l`)
4. **無線ブリッジ ➔ ロボットメイン (`robot_bridge ➔ robot_main`)**:
   - **通信方式**: ESP-NOW (固定長 16Byte バイナリパケット, 100Hz 周期送信)
5. **ロボット内部バス (`robot_main ➔ 各ATtiny1614ユニット`)**:
   - **通信方式**: I2C (10kHz, Master: Core2 / Slave: ATtiny1614)
   - **アドレス**: 脚=`0x30`, 右アーム=`0x31`, 左アーム=`0x32`

---

## 🛡️ 安全保護 & ウォッチドッグ体系

| 階層 | 監視対象 | タイムアウト時間 | 遮断・フェールセーフ動作 |
| :--- | :--- | :--- | :--- |
| **robot_bridge** | ホストシリアル通信 | **1000ms** | 全停止パケット (`vx=0, vy=0, omega=0, arm=0`) を Core2 へ送信 |
| **robot_main** | ESP-NOW 受信 | **500ms** | 内部コマンドを全停止にリセット |
| **robot_main** | バッテリー低電圧 | `V_BATT < 5.7V` | DCDC 5V 供給遮断 (`PIN_DCDC_CTRL = LOW`)、疲労表情表示 |
| **robot_leg** | I2C ウォッチドッグ | **1000ms** | 全オムニホイールを PWM ニュートラル (`1500us`) で停止 |
| **robot_arm** | I2C ウォッチドッグ | **1000ms** | アーム駆動 PWM Duty を `0%` に遮断 |

---

## 🤖 推奨開発 AI モデル & ガイドライン

- **システム全体設計・クロスコンポーネント連携**: **Gemini 3.1 Pro**
- **各サブモジュール実装・個別機能修正**: **Gemini 3.6 Flash**
- 各サブモジュールを開発・調査する際は、該当ディレクトリ直下の `GEMINI.md` を必ず参照してください。
