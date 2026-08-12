# robot_leg 仕様書

## 役割

- **脚ユニット (SLAVE: 0x30)** 内の ATtiny1614 上で動作するプログラム。
- **メインユニット** (MASTER: M5Stack Core2) と **脚ユニット** (SLAVE) は **I2C** (400kHz) で通信を行う。
- **脚ユニット** は PWM 信号を 4 本持ち、4 軸無限回転ラジコンサーボ（オムニホイール）を制御する。
- **脚ユニット** は自分の I2C アドレスを回答するためスキャンモード (SMBus Quick Write) に対応する。
- **脚ユニット** は **メインユニット** からの I2C 通信によるレジスタ書き込み（連続バイト書き込み対応）、レジスタ読み込みに対応する。
- レジスタの移動指令データ [Vx, Vy, Omega] に従って PWM1〜4 を使用してロボットの移動・旋回を制御する。
- **脚ユニット** は `0x30`、**右アームユニット** は `0x31`、**左アームユニット** は `0x32` の I2C アドレスを持つ。

---

## 駆動機構・PWM割り当て仕様

移動機構は **4輪X配置オムニホイール** とし、PWM割り当ておよびデータ仕様は以下の通りとする。

```
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

* **PWM1** (PA4): **右後 (Rear-Right, RR)**
* **PWM2** (PA5): **右前 (Front-Right, FR)**
* **PWM3** (PA6): **左後 (Rear-Left, RL)**
* **PWM4** (PA7): **左前 (Front-Left, FL)**

### 出力動作

* **パルス幅仕様**: 50Hz PWM (周期 20ms)
  * **停止 (ニュートラル)**: `1500 us`
  * **最大正転 (+1000)**: `2000 us`
  * **最大逆転 (-1000)**: `1000 us`
* **停止時**: サーボパルス出力を停止（遮断）するのではなく、PWM ニュートラルパルス (`1500us`) を連続出力し続ける。

---

## 制御データ仕様 [Vx, Vy, Omega]

* **スケール**: `-1000` ～ `1000` (パーセント単位 `%`、`-100.0%` ～ `+100.0%`)
* **キネマティクス式**:
  * **FL (PWM4 / PA7)**: $\text{clamp}(-Vx + Vy + Omega)$
  * **FR (PWM2 / PA5)**: $\text{clamp}(+Vx + Vy + Omega)$
  * **RL (PWM3 / PA6)**: $\text{clamp}(-Vx - Vy + Omega)$
  * **RR (PWM1 / PA4)**: $\text{clamp}(+Vx - Vy + Omega)$

---

## ハードウェア仕様

### 基本スペック

| 項目 | 仕様 |
| --- | --- |
| MCU | ATtiny1614 |
| Clock | 20MHz (内部発振) |
| 動作電圧 | 3.3V (内部LDOにて降圧) / サーボ電源: 5V (DCDC) |
| BOD (Brown-out Detection) | **2.6V** (メインユニットからの電源立ち上がり遅延による起動不安定対策) |

### ピン配置 & マッピング

megaTinyCore の `Wire.swap(1)` により、Alternative TWI0 ピンへマッピング：

| 名称 | 機能 | ピン番号 | 割り当て・用途 |
| --- | --- | --- | --- |
| UPDI | UPDI | PA0 (Pin 14) | プログラム書込 & デバッグ (SerialUPDI: 115200bps) |
| EXT_SDA | I2C (SDA) | PA1 (Pin 11) | 外部I2C データ線 (Port.A Core2 GPIO32接続) |
| EXT_SCL | I2C (SCL) | PA2 (Pin 12) | 外部I2C クロック線 (Port.A Core2 GPIO33接続) |
| PWM1 | OUTPUT | PA4 (Pin 2)  | **右後 (Rear-Right, RR)** サーボ信号 |
| PWM2 | OUTPUT | PA5 (Pin 3)  | **右前 (Front-Right, FR)** サーボ信号 |
| PWM3 | OUTPUT | PA6 (Pin 4)  | **左後 (Rear-Left, RL)** サーボ信号 |
| PWM4 | OUTPUT | PA7 (Pin 5)  | **左前 (Front-Left, FL)** サーボ信号 |

---

## レジスタマップ

* I2C通信時、レジスタ指定からの **連続バイト書き込み (Auto-Increment)** に対応する。

| アドレス | R/W | 型 | 初期値 | 説明 | 詳細・備考 |
| --- | --- | --- | --- | --- | --- |
| **`0x00`** | R | `uint8_t` | `0x30` | ユニット識別番号 | `I2C_ADDR_LEG` (脚ユニットを示す) |
| **`0x01`** | R | `uint8_t` | `0x10` | FWバージョン | ソフトウェアバージョン v1.0 |
| **`0x02`** | W | `uint8_t` | `0x00` | ウォッチドック | MASTERが毎周期更新。500ms無更新で全輪安全停止 |
| **`0x10`** | W | `int16_t` | `0x0000` | 移動速度 **Vx** | スケール: `-1000` ～ `1000` (Little-Endian) |
| **`0x12`** | W | `int16_t` | `0x0000` | 移動速度 **Vy** | スケール: `-1000` ～ `1000` (Little-Endian) |
| **`0x14`** | W | `int16_t` | `0x0000` | 移動速度 **Omega** | スケール: `-1000` ～ `1000` (Little-Endian) |

---

## ビルド & 書き込み手順 (PlatformIO)

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

### ヒューズ書き込み手順 (BOD 2.6V 設定)
電源立ち上がり時の安定化のため、初回復帰時またはボード変更時に必ずヒューズ書き込みを実行してください。

```bash
pio run -d robot_leg -t fuses --upload-port /dev/ttyUSB0
```

### ファームウェア書き込み

```bash
pio run -d robot_leg -t upload --upload-port /dev/ttyUSB0
```

### EEPROM書き込み

```bash
avrdude -c serialupdi -p t1614 -P /dev/ttyUSB1 -U eeprom:w:data/eeprom.hex:i
```