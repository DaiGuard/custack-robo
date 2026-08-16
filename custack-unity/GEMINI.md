# custack-unity (Unity 6 / URP) プロジェクト仕様書

## 1. プロジェクト概要

- **Unity バージョン**: Unity 6 (`6000.3.14f1`)
- **レンダーパイプライン**: Universal Render Pipeline (URP 17.3.0)
- **入力システム**: Unity Input System (1.19.0)
- **役割**:
  - Jetson ➔ `custack_router` から **POSIX 共有メモリ (`/dev/shm/custack_robot_poses`)** 経由でロボット（AprilTag ID 0: P1, ID 1: P2）の位置姿勢を Seqlock ロックフリーで超低遅延受信し、プロジェクション画面上にレンダリング。
  - **PS5 コントローラー 2 台** による 2 プレイヤー独立操作（移動、旋回、左右武器発射、△ボタンでのホーミングターゲット切替）。
  - **実機デバイスID連動**: ロボットに装着された右/左アーム・脚ユニットのデバイスIDに応じた武器エフェクト・移動特性の付与。
  - **地形特性 × 脚ユニット補正**: 森・泥・氷などのフィールド効果と脚ユニット（全方向/タイヤ/キャタピラ）の耐性に応じた移動値の複合補正。
  - **補正後コマンド送信**: 補正後の移動値・武器状態を共有メモリ (`/dev/shm/custack_controller_cmd`) 経由で `custack_router` ➔ M5Atom ➔ 実機へ 50Hz 周期で送信。
  - **戦闘・ダメージシステム**: 武器発射、飛翔・誘導、相手機体との衝突判定、HP減少、被弾エフェクト、勝敗判定。

---

## 2. デバイスID & ゲームメカニクス仕様

### 2.1. アームユニット (`ArmDeviceType`) 武器仕様
左右のアームで独立して装備可能（例: 右＝ピストル、左＝ミサイル）。

| デバイスID | 武器名 | 攻撃特性 | 弾速 / 射程 | 連射性能 | 操作 & 演出 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`0x01`** | **ピストル (Pistol)** | 直線高精度・中威力 | **高速 / 長射程** (画面端まで到達) | **単発** (セミオート、CD: 0.4s) | 高速レーザービーム弾、マズルフラッシュ |
| **`0x02`** | **ガトリング (Gatling)** | 弾幕制圧・低威力 | **中速 / 短射程** (寿命短め、拡散角あり) | **超高速連射** (ホールドで15発/s) | 黄色連続小弾幕、薬莢排出 |
| **`0x03`** | **ミサイル (Missile)** | 範囲爆発・大威力 | **低〜中速 / 中射程** | **長CD** (2.5s) | **緩やかなホーミング**、煙トレイル、着弾時大爆発 |

#### 🎯 ミサイルホーミング & ターゲット切替
* **コントローラー △ボタン (Triangle)** でホーミング対象（敵機体 1 ➔ 敵機体 2 ➔ ロック解除）をトグル切り替え。
* ロックオン中の敵機体頭上に **ターゲットレティクルマーカー** を表示。
* ミサイルは角速度制限（$\omega_{\max} = 120^\circ/\text{s}$）で滑らかに敵を追尾。

---

### 2.2. 脚ユニット (`LegDeviceType`) × 地形特性マトリクス

| 脚ユニット | 平地 (`Normal`) | 森 (`Forest`) | 泥 / 沼 (`Mud`) | 氷 (`Ice`) | 移動挙動の特徴 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`0x01: 全方向 (Omni)`** | 速度: `100%` | 速度: **`50%`** (通常減速) | 速度: **`30%`** (大減速) | スリップ: **大** | $V_x, V_y, \Omega$ 全方向自由スライド |
| **`0x02: タイヤ (Tire)`** | 速度: **`125%` (最速)** | 速度: **`40%`** (枝で失速) | 速度: **`20%`** (スタック) | ドリフト旋回 | 前後 $V_y$ 高速・鋭い旋回 ($V_x$制限) |
| **`0x03: キャタピラ (Crawler)`** | 速度: `90%` | 速度: **`85%` (走破)** | 速度: **`80%` (走破)** | スリップ: **小 (安定)** | 超信地旋回、**悪路の影響をほぼ無効化** |

---

## 3. ディレクトリ & スクリプト構成 (`Assets/_Project/Scripts/`)

```text
custack-unity/
├── Assets/
│   ├── _Project/
│   │   ├── Scripts/
│   │   │   ├── SharedMemory/            # 共有メモリ低レベル送受信 (Seqlock)
│   │   │   │   ├── SharedMemoryTypes.cs # C++ 共有メモリ構造体定義 (Pack=1)
│   │   │   │   ├── SharedMemoryReader.cs# /dev/shm/custack_robot_poses 読取
│   │   │   │   └── SharedMemoryWriter.cs# /dev/shm/custack_controller_cmd 書込 (50Hz)
│   │   │   ├── Equipment/               # 装備・デバイスID管理
│   │   │   │   ├── DeviceIdEnums.cs     # ArmDeviceType, LegDeviceType 定義
│   │   │   │   ├── ArmWeaponConfig.cs   # アーム武器スペック設定
│   │   │   │   ├── LegMovementConfig.cs # 脚ユニットスペック・地形耐性設定
│   │   │   │   └── RobotEquipment.cs    # ロボット装備コンポーネント (インスペクター切替対応)
│   │   │   ├── Input/                   # PS5 コントローラー入力
│   │   │   │   ├── ControllerInputManager.cs # 2台の Gamepad 検出 & 個別バインド
│   │   │   │   └── PlayerInputCommand.cs# 入力データ構造体
│   │   │   ├── Terrain/                 # 地形システム
│   │   │   │   ├── TerrainType.cs       # 地形列挙 (Normal, Forest, Mud, Ice, Lava)
│   │   │   │   ├── TerrainData.cs       # 地形パラメータ
│   │   │   │   ├── TerrainZone.cs       # 2D Trigger Collider エリア判定
│   │   │   │   └── TerrainManager.cs    # 地形 × 脚ユニットの複合移動値補正
│   │   │   ├── Combat/                  # 戦闘・武器・ダメージシステム
│   │   │   │   ├── Health.cs            # HP管理、無敵時間、被弾/撃破イベント
│   │   │   │   ├── WeaponBase.cs        # 武器基底クラス (実機アームフラグ連動)
│   │   │   │   ├── PistolWeapon.cs      # 0x01: ピストル
│   │   │   │   ├── GatlingWeapon.cs     # 0x02: ガトリング
│   │   │   │   ├── MissileWeapon.cs     # 0x03: ミサイル
│   │   │   │   ├── Projectile.cs        # 飛翔体基底
│   │   │   │   ├── HomingMissile.cs     # 誘導ミサイル
│   │   │   │   ├── ExplosionArea.cs     # 範囲爆発ダメージ
│   │   │   │   └── HitEffect.cs         # 被弾エフェクト
│   │   │   ├── Robot/                   # ロボット実体 & 演出
│   │   │   │   ├── RobotEntity.cs       # ロボット統括 (ID, 入力, 装備, 地形補正, Health)
│   │   │   │   ├── RobotManager.cs      # 全機体の同期・管理
│   │   │   │   ├── RobotVisual.cs       # アバター描画、HPバー、ターゲットマーカー
│   │   │   │   └── ProjectionScaler.cs  # プロジェクション座標スケーリング
│   │   │   ├── UI/                      # 対戦HUD & モニター
│   │   │   │   ├── BattleHUD.cs         # 対戦HUD (HPゲージ, 装備アイコン, 勝敗表示)
│   │   │   │   └── SharedMemoryPoseViewer.cs # インスペクター用デバッグモニター
│   │   │   └── Core/                    # ゲーム進行
│   │   │       └── GameManager.cs       # 対戦開始・ラウンド管理・リセット
│   │   ├── Scenes/
│   │   │   ├── Main/Main.unity          # 本番対戦シーン
│   │   │   └── Sandbox/SharedMemorySandbox.unity # 動作検証・デバッグシーン
│   │   ├── Prefabs/                     # ロボット、弾丸、エフェクト Prefab
│   │   └── Materials/                   # URP マテリアル
```

---

## 4. 共有メモリ連携データ構造

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SharedRobotPose
{
    public int id;          // AprilTag ID (-1: 未検出)
    public float x;         // 投影面座標 -1.0 ~ 1.0
    public float y;         // 投影面座標 -1.0 ~ 1.0
    public float theta;     // 回転角 (radian)
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SingleControllerCommand
{
    public short vx;        // 移動速度 Vx (-1000 ~ 1000)
    public short vy;        // 移動速度 Vy (-1000 ~ 1000)
    public short omega;     // 旋回速度 Omega (-1000 ~ 1000)
    public byte armRight;   // 右アーム起動フラグ (0: 停止, 1: 起動)
    public byte armLeft;    // 左アーム起動フラグ (0: 停止, 1: 起動)
    public byte active;     // コントローラ有効フラグ (0: 無効, 1: 有効)
    public byte reserved;
}
```

---

## 5. インスペクター装備テスト機能

* `RobotEntity` に `[SerializeField] bool overrideEquipment` トグルを装備。
* インスペクターから `LegType`、`RightArmType`、`LeftArmType` を自由に切り替え可能。
* 実機との通信がないスタンドアロン環境でも、すべての武器・脚・地形の挙動をフルテスト可能。
