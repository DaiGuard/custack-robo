# robot_leg (ATtiny1614) コンポーネント仕様書

## 1. 役割 & 概要

- **ハードウェア**: Microchip ATtiny1614 (20MHz 内部発振, 3.3V 駆動)
- **役割**: 脚ユニット (SLAVE: `0x30`) として 4 軸無限回転ラジコンサーボ（オムニホイール）を制御するプログラム。
  - **メインユニット** (MASTER: M5Stack Core2) と **脚ユニット** (SLAVE) は **I2C** (10kHz〜400kHz) で通信。
  - 4 本の PWM 信号を出力し、4 輪 X 配置オムニホイールの移動・旋回キネマティクスを演算。
  - スキャンモード (SMBus Quick Write) および連続バイト書き込み (Auto-Increment) に対応。
  - EEPROM に保存された各サーボのキャリブレーションオフセット・スケール値を適用。
  - I2C 通信途絶監視（ウォッチドッグ 1000ms）により安全停止。

---

## 2. 駆動機構・PWM割り当て仕様

移動機構は **4輪X配置オムニホイール** とし、PWM割り当ておよびデータ仕様は以下の通りとする。

```text
       Front (+Vy)
    FL(PWM4)    FR(PWM2)
        \      /
         \    /
          [  ]
         /    \
        /      \
    RL(PWM3)    RR(PWM1)
       Rear (-Vy)
```

### ピン・モーター割り当て

* **PWM1** (PA4 / Pin 2): **右後 (Rear-Right, RR)** サーボ信号
* **PWM2** (PA5 / Pin 3): **右前 (Front-Right, FR)** サーボ信号
* **PWM3** (PA6 / Pin 4): **左後 (Rear-Left, RL)** サーボ信号
* **PWM4** (PA7 / Pin 5): **左前 (Front-Left, FL)** サーボ信号

### 出力動作

* **パルス幅仕様**: 50Hz PWM (周期 20ms)
  * **停止 (ニュートラル)**: `1500 us`
  * **最大正転 (+1000)**: `2000 us`
  * **最大逆転 (-1000)**: `1000 us`
* **停止時**: サーボパルス出力を遮断するのではなく、ニュートラルパルス (`1500us` + オフセット) を連続出力し続ける。

---

## 3. 制御データ仕様 [Vx, Vy, Omega]

* **スケール**: `-1000` ～ `1000` (パーセント単位 `%`、`-100.0%` ～ `+100.0%`)
* **キネマティクス式**:
  * **FL (PWM4 / PA7)**: $\text{clamp}(-Vx + Vy + Omega)$
  * **FR (PWM2 / PA5)**: $\text{clamp}(+Vx + Vy + Omega)$
  * **RL (PWM3 / PA6)**: $\text{clamp}(-Vx - Vy + Omega)$
  * **RR (PWM1 / PA4)**: $\text{clamp}(+Vx - Vy + Omega)$

---

## 4. ハードウェア仕様

### 基本スペック

| 項目 | 仕様 |
| :--- | :--- |
| MCU | ATtiny1614 |
| Clock | 20MHz (内部発振) |
| 動作電圧 | 3.3V (内部LDOにて降圧) / サーボ電源: 5V (DCDC) |
| BOD (Brown-out Detection) | **2.6V** (電源立ち上がり遅延による起動不安定対策) |

### ピン配置 & マッピング

megaTinyCore の `Wire.swap(1)` により、Alternative TWI0 ピンへマッピング：

| 名称 | 機能 | ピン番号 | 割り当て・用途 |
| :--- | :--- | :--- | :--- |
| **UPDI** | UPDI | PA0 (Pin 14) | プログラム書込 & デバッグ (SerialUPDI: 115200bps) |
| **EXT_SDA** | I2C (SDA) | PA1 (Pin 11) | 外部I2C データ線 (Port.A Core2 GPIO32接続) |
| **EXT_SCL** | I2C (SCL) | PA2 (Pin 12) | 外部I2C クロック線 (Port.A Core2 GPIO33接続) |
| **PWM1** | OUTPUT | PA4 (Pin 2)  | **右後 (Rear-Right, RR)** サーボ信号 |
| **PWM2** | OUTPUT | PA5 (Pin 3)  | **右前 (Front-Right, FR)** サーボ信号 |
| **PWM3** | OUTPUT | PA6 (Pin 4)  | **左後 (Rear-Left, RL)** サーボ信号 |
| **PWM4** | OUTPUT | PA7 (Pin 5)  | **左前 (Front-Left, FL)** サーボ信号 |

---

## 5. レジスタマップ

* I2C通信時、レジスタ指定からの **連続バイト書き込み (Auto-Increment)** に対応。

| アドレス | R/W | 型 | 初期値 | 定数名 | 説明 | 詳細・備考 |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`0x00`** | R | `uint8_t` | `0x30` | `REG_DEVICE_ID` | ユニット識別番号 | `I2C_ADDR_LEG` (脚ユニットを示す) |
| **`0x01`** | R | `uint8_t` | `0x10` | `REG_FW_VERSION` | FWバージョン | ソフトウェアバージョン v1.0 |
| **`0x02`** | W | `uint8_t` | `0x00` | `REG_WATCHDOG` | ウォッチドッグ | MASTERが毎周期更新。**1000ms** 無更新で全輪安全停止 |
| **`0x10`** | W | `int16_t` | `0x0000` | `REG_LEG_VX` | 移動速度 **Vx** | スケール: `-1000` ～ `1000` (Little-Endian) |
| **`0x12`** | W | `int16_t` | `0x0000` | `REG_LEG_VY` | 移動速度 **Vy** | スケール: `-1000` ～ `1000` (Little-Endian) |
| **`0x14`** | W | `int16_t` | `0x0000` | `REG_LEG_OMEGA` | 移動速度 **Omega** | スケール: `-1000` ～ `1000` (Little-Endian) |
| **`0x16`** | R/W | `int16_t` | EEPROM | `REG_LEG_OFFSET1` | PWM1 (RR) ニュートラルオフセット | パルス幅微調整値 [us] |
| **`0x18`** | R/W | `int16_t` | EEPROM | `REG_LEG_OFFSET2` | PWM2 (FR) ニュートラルオフセット | パルス幅微調整値 [us] |
| **`0x1A`** | R/W | `int16_t` | EEPROM | `REG_LEG_OFFSET3` | PWM3 (RL) ニュートラルオフセット | パルス幅微調整値 [us] |
| **`0x1C`** | R/W | `int16_t` | EEPROM | `REG_LEG_OFFSET4` | PWM4 (FL) ニュートラルオフセット | パルス幅微調整値 [us] |
| **`0x1E`** | R/W | `uint8_t` | EEPROM | `REG_LEG_SCALE1` | PWM1 (RR) スケール補正 | 出力ゲイン補正 |
| **`0x1F`** | R/W | `uint8_t` | EEPROM | `REG_LEG_SCALE2` | PWM2 (FR) スケール補正 | 出力ゲイン補正 |
| **`0x20`** | R/W | `uint8_t` | EEPROM | `REG_LEG_SCALE3` | PWM3 (RL) スケール補正 | 出力ゲイン補正 |
| **`0x21`** | R/W | `uint8_t` | EEPROM | `REG_LEG_SCALE4` | PWM4 (FL) スケール補正 | 出力ゲイン補正 |

---

## 6. ビルド & 書き込み手順 (PlatformIO)

### platformio.ini

```ini
[env:ATtiny1614]
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
```

### コマンド例

```bash
# ヒューズ書き込み (BOD 2.6V 設定)
pio run -d custack-robot/robot_leg -t fuses --upload-port /dev/ttyUSB0

# ファームウェア書き込み
pio run -d custack-robot/robot_leg -t upload --upload-port /dev/ttyUSB0

# EEPROM書き込み (初期キャリブレーションデータ書き込み時)
avrdude -c serialupdi -p t1614 -P /dev/ttyUSB0 -U eeprom:w:data/eeprom.hex:i
```