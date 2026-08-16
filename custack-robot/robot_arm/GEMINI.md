# robot_arm (ATtiny1614) コンポーネント仕様書

## 1. ユニット概要

- **ハードウェア**: Microchip ATtiny1614 (20MHz 内部発振, 3.3V 駆動)
- **役割**: ロボットのアーム攻撃機構を駆動する小型 MCU ユニット。
  - メインユニット (**M5Stack Core2**) から I2C 通信経由で制御指令（Duty比・動作パターン）を受信。
  - TCA0 タイマーの Split Mode を用いて PA4 ピンから **20kHz 高周波 PWM** を出力し、モータドライバを制御。
  - 左右アームで同一ソースコードを使用し、ビルド環境定義 (`-D I2C_SLAVE_ADDR`) によって I2C アドレスを切り替え。
- **フレームワーク**: Arduino (PlatformIO / megaTinyCore)

### ディレクトリ構成
```text
robot_arm/
├── include/
│   └── i2c_protocol.h       # I2C レジスタ・プロトコル定義
├── src/
│   └── main.cpp             # PWM初期化・I2Cスレーブ・パターン制御実装
└── platformio.ini           # PlatformIO ビルド設定 (arm_right / arm_left)
```

---

## 2. ハードウェア仕様 & ピン配置

### 2.1. 基本スペック
| 項目 | 仕様 |
| :--- | :--- |
| MCU | ATtiny1614 (SOIC-14) |
| クロック | 20MHz (内部発振) |
| 動作電圧 | 3.3V (内部降圧) / モータ電源: 5V (DCDC) |
| BOD (Brown-out Detection) | **2.6V** (電源立ち上がり時のリセット安定化) |

### 2.2. ピン配置 & マッピング
`Wire.swap(1)` により Alternative TWI0 ピンを使用。

| ピン名 | 物理ピン | 機能 | 接続先・用途 |
| :--- | :--- | :--- | :--- |
| **UPDI** | Pin 14 (PA0) | UPDI | プログラム書き込み・ヒューズ設定 (SerialUPDI: 115200bps) |
| **EXT_SDA** | Pin 11 (PA1) | I2C SDA | Core2 Port.A I2C バス接続 (4.7kΩプルアップ) |
| **EXT_SCL** | Pin 12 (PA2) | I2C SCL | Core2 Port.A I2C バス接続 (4.7kΩプルアップ) |
| **PIN_PWM** | Pin 2 (PA4) | TCA0 WO4 | **アームモータ駆動 PWM 出力** (20kHz 高周波) |

---

## 3. I2C スレーブ仕様 & レジスタマップ

### 3.1. I2C アドレス定義
- **右アーム (`arm_right`)**: `0x31`
- **左アーム (`arm_left`)**: `0x32`
- **通信速度**: 10kHz〜400kHz (スキャンモード SMBus Quick Write 対応)

### 3.2. レジスタマップ
| アドレス | R/W | 型 | 初期値 | 定数名 | 説明 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`0x00`** | R | `uint8_t` | `0x01`〜`0x03` | `REG_DEVICE_ID` | ユニット識別ID (`0x01`: ガトリング, `0x02`: ソード, `0x03`: 大型レーザーキャノン) |
| **`0x01`** | R | `uint8_t` | `0x10` | `REG_FW_VERSION` | ファームウェアバージョン (`v1.0`) |
| **`0x02`** | W | `uint8_t` | `0x00` | `REG_WATCHDOG` | ウォッチドッグ (毎周期更新。1000ms途絶でモータ停止) |
| **`0x10`** | W | `uint8_t` | `0x00` | `REG_ARM_DUTY` | アーム駆動 Duty 比 (0〜255, 内部で最大クランプ) |
| **`0x11`** | W | `uint8_t` | `0x00` | `REG_ARM_PATTERN` | 動作パターン指定 (`0`: 手動, `1`: 単発, `2`: 点滅) |

---

## 4. PWM モータ駆動 & 動作パターン仕様

### 4.1. 20kHz 高周波 PWM 出力 (TCA0 Split Mode)
可聴域外の 20kHz で PWM 駆動することで、静音かつスムーズなモータ駆動を実現。
- **計算式** (プリスケーラ DIV8, 20MHz):
  $$HPER = \frac{F_{CPU}}{8 \times f_{PWM}} - 1 = \frac{20000000}{8 \times 20000} - 1 = 124$$
- **Duty 上限クランプ**: 安全のため `ARM_DUTY_MAX = 15` にクランプ処理。

### 4.2. 動作パターン (`REG_ARM_PATTERN`)
1. `ARM_PATTERN_MANUAL` (`0x00`): 設定された Duty で連続回転
2. `ARM_PATTERN_PULSE` (`0x01`): Duty が増加したトリガーから 100ms 間のみ出力し自動停止（単発攻撃）
3. `ARM_PATTERN_BLINK` (`0x02`): 200ms 周期（150ms ON / 50ms OFF）で断続駆動（点滅・連射振動攻撃）

---

## 5. 安全保護 (ウォッチドッグ)

- `REG_WATCHDOG` の更新時刻を常時監視。
- 最後の更新から **1000ms** (`WD_TIMEOUT_MS`) 以上経過した場合、自動的にモータ Duty を `0` にリセットして安全停止。

---

## 6. ビルド & 書き込み手順 (PlatformIO)

### platformio.ini
```ini
[platformio]
default_envs = arm_right_gatling, arm_left_gatling

[env]
platform = atmelmegaavr
board = ATtiny1614
framework = arduino
board_build.f_cpu = 20000000L
board_hardware.millistimer = rtc
upload_protocol = serialupdi
upload_flags =
    -b
    115200
board_hardware.bod = 2.6V
board_hardware.eesave = yes

; 右アーム (0x31)
[env:arm_right_gatling]  ; 0x01: ガトリング
[env:arm_right_sword]    ; 0x02: ソード
[env:arm_right_cannon]   ; 0x03: 大型レーザーキャノン

; 左アーム (0x32)
[env:arm_left_gatling]   ; 0x01: ガトリング
[env:arm_left_sword]     ; 0x02: ソード
[env:arm_left_cannon]    ; 0x03: 大型レーザーキャノン
```

### コマンド例 (オプション指定による左右・兵装切り替え書き込み)
```bash
# --- 右アーム書き込み (I2C: 0x31) ---
# 1. 右アーム: ガトリング (0x01)
pio run -d custack-robot/robot_arm -e arm_right_gatling -t upload --upload-port /dev/ttyUSB0

# 2. 右アーム: ソード (0x02)
pio run -d custack-robot/robot_arm -e arm_right_sword -t upload --upload-port /dev/ttyUSB0

# 3. 右アーム: 大型レーザーキャノン (0x03)
pio run -d custack-robot/robot_arm -e arm_right_cannon -t upload --upload-port /dev/ttyUSB0

# --- 左アーム書き込み (I2C: 0x32) ---
# 1. 左アーム: ガトリング (0x01)
pio run -d custack-robot/robot_arm -e arm_left_gatling -t upload --upload-port /dev/ttyUSB0

# 2. 左アーム: ソード (0x02)
pio run -d custack-robot/robot_arm -e arm_left_sword -t upload --upload-port /dev/ttyUSB0

# 3. 左アーム: 大型レーザーキャノン (0x03)
pio run -d custack-robot/robot_arm -e arm_left_cannon -t upload --upload-port /dev/ttyUSB0
```
