# robot_main (M5Stack Core2) コンポーネント仕様書

## 1. ユニット概要

- **ハードウェア**: M5Stack Core2 (ESP32-D0WDQ6-V3 / ILI9342C 320x240 LCD / 静電容量式タッチボタン / 内蔵スピーカー / I2S 内蔵電源管理 AXP192)
- **役割**: ロボット本体のメイン統括ユニット。
  - ESP-NOW 経由で送信ブリッジ (`robot_bridge`) からの制御パケットを受信。
  - I2C バスを通じて脚ユニット (`robot_leg`) および左右アームユニット (`robot_arm`) へ制御指令を高速配信。
  - バッテリー電圧監視および外部 DCDC 5V 電源供給の制御。
  - LCD ディスプレイへの顔表情アニメーション・通信/バッテリーステータス描画、サウンド出力。
- **フレームワーク**: Arduino (PlatformIO)
- **主要ライブラリ**: M5Unified (`m5stack/M5Unified@^0.2.10`), LovyanGFX (`lovyan03/LovyanGFX@^1.2.21`)

### ディレクトリ構成
```text
robot_main/
├── include/
│   ├── espnow_com.h         # ESP-NOW 受信処理クラス定義
│   ├── espnow_protocol.h    # ESP-NOW 16Byte パケット構造体定義
│   ├── face_display.h       # LovyanGFX 顔表情・ステータス描画クラス定義
│   ├── i2c_protocol.h       # I2C レジスタ・送受信パケット定義
│   ├── robot_status.h       # ロボット全体状態構造体定義
│   └── unit_ctrl.h          # I2C 子局 (脚/腕) 統括制御クラス定義
├── src/
│   ├── espnow_com.cpp       # ESP-NOW 受信・MACアドレス取得実装
│   ├── face_display.cpp     # 表情アニメーション・UI描画・サウンド実装
│   ├── main.cpp             # セットアップ・メインループ・バックグラウンドタスク
│   └── unit_ctrl.cpp        # I2C マスター通信・スキャン・定期書き込み実装
└── platformio.ini           # PlatformIO ビルド設定
```

---

## 2. ハードウェア & ピンアサイン

| 機能 / ペリフェラル | ピン番号 (GPIO) | 接続先 / 説明 |
| :--- | :--- | :--- |
| **I2C SDA** | `GPIO 32` | Port.A (外部 I2C バス SDA, 4.7kΩプルアップ) |
| **I2C SCL** | `GPIO 33` | Port.A (外部 I2C バス SCL, 4.7kΩプルアップ) |
| **BAT_ADC** | `GPIO 34` | バッテリー電圧監視用 ADC (分圧比: $15.1 / 5.1 \approx 2.96$) |
| **DCDC_CTRL** | `GPIO 19` | 外部 DCDC 5V 電源イネーブル信号 (`HIGH`: ON, `LOW`: OFF) |
| **タッチボタン** | タッチパネル | `BtnA`: 情報表示切替 (長押し), `BtnB`: 5V給電切替 (長押し) |

---

## 3. 通信仕様

### 3.1. ESP-NOW 受信仕様 (上位通信)
- **通信モード**: Wi-Fi Station (`WIFI_STA`), Channel 1
- **パケットフォーマット**: 固定長 16Byte バイナリパケット ([`include/espnow_protocol.h`](file:///home/dai_guard/Workspaces/custack-robo/unity/custack-robot/robot_main/include/espnow_protocol.h))

```cpp
typedef struct {
    int16_t vx;          // 移動速度Vx (-1000 〜 1000)
    int16_t vy;          // 移動速度Vy (-1000 〜 1000)
    int16_t omega;       // 移動速度Omega (-1000 〜 1000)
    uint8_t arm_right;   // 右アーム起動フラグ (0x00:停止, 0x01:起動)
    uint8_t arm_left;    // 左アーム起動フラグ (0x00:停止, 0x01:起動)
    uint8_t reserved[7]; // 予約領域 (パディング)
    uint8_t watchdog;    // ウォッチドッグカウンタ
} EspNowCommandPacket;
```

- **タイムアウト保護**: 受信途絶から **500ms** 経過で内部パケットを全停止 (`0`) にリセット。

### 3.2. I2C マスター制御仕様 (内部バス)
- **クロック周波数**: `10kHz` (`I2C_FREQ`)
- **スレーブ局構成**:
  - `0x30`: 脚ユニット (`robot_leg`) ➔ レジスタ `0x10` から `I2cLegData` (6Bytes: `vx, vy, omega`) を連続書き込み
  - `0x31`: 右アームユニット (`robot_arm`) ➔ レジスタ `0x10` から `I2cArmData` (2Bytes: `duty, pattern`) を書き込み
  - `0x32`: 左アームユニット (`robot_arm`) ➔ レジスタ `0x10` から `I2cArmData` (2Bytes: `duty, pattern`) を書き込み
- **ウォッチドッグ送信**: 各ユニットのレジスタ `0x02` (`REG_WATCHDOG`) に対し、上位の `watchdog` 値を毎周期書き込み。
- **子局スキャン**: 1000ms 周期で各子局の存在確認スキャンを実行。

---

## 4. 電源管理 & バッテリー監視仕様

- **ADC 読み取り**: `GPIO 34` の ADC 値をローパスフィルタ処理し、実電圧を算出。
  $$V_{batt} = \left(\frac{ADC \times 3.3}{4095}\right) \times \frac{15.1}{5.1}$$
- **判定閾値**:
  - `V_batt < 5.7V`: **危険閾値 (Critical)** ➔ バッテリーステータス `2` (疲労表情、全停止推奨)
  - `V_batt < 6.3V`: **警告閾値 (Warning)** ➔ バッテリーステータス `1` (低電圧警告)
  - `4.2V < V_batt < 5.0V`: **USB給電モード** ➔ バッテリーステータス `3`

---

## 5. 表情・UI表示 (LovyanGFX)

Core 0 の `backgroundTask` (50ms 周期) にて M5Canvas ダブルバッファによる描画を処理。

1. **表情モード (`face_type`)**:
   - `0` (**待機表情 / Idle**): 停止中の標準表情（まばたきアニメーション）
   - `1` (**戦闘表情 / Battle**): 移動・アーム動作時のアクティブ表情
   - `2` (**疲労表情 / Tired**): バッテリー低下時の表情
2. **ステータスオーバーレイ (`BtnA` 長押しでトグル)**:
   - バッテリー電圧・残量表示
   - ESP-NOW 接続状態 & 自身の MAC アドレス
   - I2C 各子局（脚・右腕・左腕）の接続検知 & **デバイスID表示**（接続時: `01`, `02`, `03` 等の16進ID表示 / 非接続時: `--`）
   - 受信中の制御データ (`Vx, Vy, Omega, ArmR, ArmL, WD`)

---

## 6. ビルド & 書き込み手順 (PlatformIO)

### platformio.ini
```ini
[env:m5stack-core2]
platform = espressif32
board = m5stack-core2
framework = arduino
monitor_speed = 115200
lib_deps =
    m5stack/M5Unified@^0.2.10
    lovyan03/LovyanGFX@^1.2.21
```

### コマンド
```bash
# ビルド
pio run -d custack-robot/robot_main

# ファームウェア書き込み (ポート例: /dev/ttyACM0 または /dev/ttyUSB0)
pio run -d custack-robot/robot_main -t upload --upload-port /dev/ttyACM0

# シリアルモニタ
pio device monitor -d custack-robot/robot_main -b 115200
```
