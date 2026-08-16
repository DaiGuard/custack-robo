# custack-unity プロジェクト仕様書

本ディレクトリ (`custack-unity`) は、**CuStack ロボットシステム** における Unity (Unity 6 / URP) プロジェクトです。
床面プロジェクションマッピング演出、マルチディスプレイ出力、POSIX共有メモリを介した超低遅延なロボット位置姿勢追従およびゲームパッド操作指令の送信を担当します。

---

## 1. 🛠️ 環境・パッケージ構成

* **Unity バージョン**: `Unity 6 (6000.3.14f1)`
* **レンダーパイプライン**: Universal Render Pipeline (URP 17.3.0)
* **主要パッケージ**:
  * **Input System (`com.unity.inputsystem 1.19.0`)**: 複数台のゲームパッド入力（DirectInput / XInput）を管理
  * **ROS-TCP-Connector (`com.unity.robotics.ros-tcp-connector`)**: ROS 2 トピック購読用（TCPフォールバック）
  * **URDF Importer (`com.unity.robotics.urdf-importer 0.5.2`)**: ロボット3Dモデル・URDFインポート
  * **UGUI / Navigation / 2D / Timeline**: 演出およびUI表示

---

## 2. 📁 ディレクトリ構成

```text
custack-unity/
├── Assets/
│   ├── RosMessages/Custack/msg/
│   │   ├── RobotPoseMsg.cs          # ROS RobotPose メッセージ C# 定義 (ID, x, y, theta)
│   │   └── RobotPoseArrayMsg.cs     # ROS RobotPoseArray メッセージ C# 定義
│   ├── Scenes/
│   │   ├── MultiDisplay.unity       # プロジェクション＆マルチディスプレイ主シーン
│   │   ├── SampleScene.unity        # テスト用初期シーン
│   │   └── MultiDisplayProjects/
│   │       ├── Materials/           # 演出・マーカー用マテリアル
│   │       └── Scripts/
│   │           ├── MultiDisplayActivator.cs  # 起動時マルチディスプレイ自動有効化
│   │           ├── SharedMemoryManager.cs   # POSIX共有メモリ双方向通信マネージャー
│   │           ├── SharedMemoryPoseViewer.cs # 共有メモリ状態＆位置姿勢デバッグモニタ
│   │           └── RosManager.cs            # ROS-TCP 経由でのポーズ受信・追従（レガシー）
│   ├── Resources/                   # ROSConnectionPrefab 等
│   └── Settings/                    # URP 設定アセット
├── Packages/manifest.json
└── ProjectSettings/ProjectVersion.txt
```

---

## 3. 🖥️ シーン構成 (`MultiDisplay.unity`)

マルチディスプレイおよびプロジェクション投影、ロボット位置姿勢追従演出を実現するメインシーンです。

### 1. ディスプレイ & カメラ構成
* **Display 1 (ホストPC画面 / モニター)**:
  * **Sub Camera**: 透視投影 (Perspective, FOV=60)。全体俯瞰、ステータスUI、デバッグ用表示。
* **Display 2 (プロジェクター / 床面投影)**:
  * **Main Camera**: 平行投影 (Orthographic, Size=5.0)。床面への 1:1 プロジェクションマッピング用。

### 2. 主要ゲームオブジェクト
* **`RosManager`**: ROS-TCP 通信マネージャー（フォールバック用）。
* **`SharedMemPreview`**: `SharedMemoryPoseViewer` がアタッチされ、共有メモリのヘルスチェックや Scene ビュー上でのギズモ表示を担当。
* **`R1`, `R2`**: 検出されたロボットの位置・回転に追従するオブジェクト（演出エフェクトの親ノード）。
* **`Canvas / UI`**: ディスプレイ1側に動作状態や周波数を表示。
* **`X`, `Y`, `World`**: 投影フィールドの原点・境界表示用オブジェクト。

---

## 4. 📜 スクリプト一覧と詳細機能

### 1. `MultiDisplayActivator.cs` (`DisplayInitializer`)
* **役割**: Unity 起動時に追加接続されている全ディスプレイを自動アクティベート。
* **実装方式**: `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]` により、シーンロード直後に `Display.displays[i].Activate()` を実行。
* **用途**: プロジェクター（Display 2）への映像出力を手動操作なしで即座に開始。

### 2. `SharedMemoryManager.cs` (`Custack.SharedMemoryManager`)
* **役割**: POSIX 共有メモリを用いた **超低遅延・双方向データ通信** の中核コンポーネント。
* **動作仕様**:
  1. **受信 (`/dev/shm/custack_robot_poses`)**:
     * C++ 側（`custack_router` / `locator_node`）が書き込む共有メモリを **Seqlock (ロックフリーシーケンスロック)** 方式で読み出し（ターゲット 200Hz）。
     * ロボット各台（ID 0, 1, 2）の正規化座標 `(x, y, theta)` を取得。
     * 外れ値除去フィルタ（Z-Score トリム平均）を適用し、ジッターや認識ノイズを除去。
     * 投影カメラの平行投影スケール（`positionScale`）に基づき、Unity 座標系（X: 右+, Y: 上+, Z回転）に変換してプレイヤーオブジェクトに反映。
  2. **送信 (`/dev/shm/custack_controller_cmd`)**:
     * Unity 側で接続されているゲームパッド（`Gamepad.all`）から 50Hz 定周期でスティック・ボタン入力をポーリング。
     * $V_x, V_y$（移動速度: -1000〜1000）、$\Omega$（旋回速度: -1000〜1000）、左右アーム起動フラグを構造体にパック。
     * Seqlock 方式で共有メモリへ書き込み。`custack_router` がこれを読み取り、M5Atom へシリアル転送。

### 3. `SharedMemoryPoseViewer.cs` (`Custack.SharedMemoryPoseViewer`)
* **役割**: 共有メモリ通信のリアルタイム・インスペクターモニターおよび Scene ビューでのデバッグギズモ描画。
* **表示情報**:
  * **SHM 受信ステータス**: 接続有無、プロトコルバージョン、Seqlock シーケンス番号、ナノ秒タイムスタンプ、受信レート (Hz)。
  * **ロボット 3 台個別モニタ**: ID 0, 1, 2 の検出状態、共有メモリ生値、Unity 換算座標、最終更新時刻。
  * **全検出ロボット一覧**: AprilTag ID の一覧リスト。
* **Scene ギズモ描画**:
  * 検出位置にワイヤー球体、進行方向矢印、ロボットIDおよび座標テキストを表示。

### 4. `RosManager.cs` (`RosManager`)
* **役割**: `ROS-TCP-Connector` を使用した TCP 経由のトピック購読（レガシー / ネットワーク分散実行用フォールバック）。
* **購読トピック**: `/robot_poses` (`custack_msgs/RobotPoseArray`)
* **機能**: `ConcurrentQueue` を用いたスレッドセーフなメッセージ受領、Z-Score トリム平均フィルタ、周波数計測ログ出力。

---

## 5. ⚡ 共有メモリ (POSIX SHM) 連携仕様

C++ 側（`custack_ws/src/custack_router/include/custack_router/posix_shm.hpp`）とバイナリレイアウト（`Pack = 1`）が完全に一致しています。

### 1. ロボット位置姿勢構造体 (`/dev/shm/custack_robot_poses`)
* **`SharedRobotPose`** (16 bytes):
  * `int id`: AprilTag ID
  * `float x`: 投影面正規化座標 ($-1.0 \sim 1.0$)
  * `float y`: 投影面正規化座標 ($-1.0 \sim 1.0$)
  * `float theta`: 回転角 (radian)
* **`SharedRobotPoseData`** (536 bytes):
  * `uint version`: 1
  * `uint sequence`: Seqlock (偶数: 確定, 奇数: 書込中)
  * `ulong timestampNs`: ナノ秒タイムスタンプ
  * `uint count`: 検出台数
  * `posesRaw[32 * 16]`: 最大32台のポーズ配列

### 2. コントローラ操作指令構造体 (`/dev/shm/custack_controller_cmd`)
* **`SingleControllerCommand`** (8 bytes):
  * `short vx`: 移動速度 Vx ($-1000 \sim 1000$)
  * `short vy`: 移動速度 Vy ($-1000 \sim 1000$)
  * `short omega`: 旋回速度 Omega ($-1000 \sim 1000$)
  * `byte armRight`: 右アーム起動 (0/1)
  * `byte armLeft`: 左アーム起動 (0/1)
  * `byte active`: コントローラ有効フラグ (0/1)
  * `byte reserved`: 1 byte パディング
* **`SharedControllerData`** (88 bytes):
  * `uint version`: 1
  * `uint sequence`: Seqlock シーケンス番号
  * `ulong timestampMs`: ミリ秒タイムスタンプ
  * `uint count`: コントローラ数 (3)
  * `controllersRaw[8 * 8]`: 最大8台のコントローラ配列

### 3. 座標系変換仕様
| 項目 | 認識系・共有メモリ (C++/OpenCV) | Unity 2D 投影面 (XY平面) | 変換式 |
| :--- | :--- | :--- | :--- |
| **X軸** | 右が正 ($-1.0 \sim 1.0$) | 右が正 | $X_{unity} = X_{shm} \times (\text{orthographicSize} \times \text{aspect})$ |
| **Y軸** | 下が正 ($-1.0 \sim 1.0$) | 上が正 | $Y_{unity} = -Y_{shm} \times \text{orthographicSize}$ |
| **回転角** | ラジアン | 度数法 (Degree) | $\theta_{unity} = -\theta_{shm} \times \frac{180}{\pi}$ |

---

## 6. 🚀 今後の課題・拡張ロードマップ
1. **プロジェクションマッピング幾何補正**:
   * プロジェクター設置角度による台形歪みやレンズ歪みを補正するキャリブレーションメッシュ（Keystone補正シェーダー/グリッド調整機能）の実装。
2. **ゲーム演出・エフェクトの強化**:
   * ロボット足元へのサークルUI・残像エフェクト・テリトリー表示（URP シェーダーグラフ / VFX Graph）。
   * ロボット同士のインタラクション（衝突エフェクト、アーム展開演出、エリア占有判定ロジック）。
3. **ゲームパッド・アサインの柔軟化**:
   * コントローラの接続順序入れ替え（再接続時のID追従）やキーコンフィグUIの提供。
4. **オクルージョン対策（位置推定補間）**:
   * AprilTag が手や障害物で一時的に隠れた際の速度デッドレコニング（慣性補間）ロジックの強化。
