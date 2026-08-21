# custack-robot 実機ロボット制御・組込みファームウェア仕様書

本ディレクトリ (`custack-robot`) は、**CuStack ロボットシステム** における実機ロボット制御ファームウェアおよびテストツールの開発環境です。

---

## 1. 🏗️ システム全体アーキテクチャ

`custack-robot` は、**M5Stack Core2** をメイン統括ユニットとし、上位コントローラからの **ESP-NOW** 双方向無線通信、および下位アクチュエータ子局群 (**ATtiny1614**) との **I2C** 有線通信によって構成される階層分散制御型ロボットシステムです。

### 1.1 システム構成図

```mermaid
graph TD
    subgraph Host ["上位コントローラ / 操作系"]
        PC["PC / Gamepad / WebGUI / ROS 2"]
    end

    subgraph BridgeUnit ["robot_bridge (M5Atom Matrix)"]
        Bridge["ESP-NOW ブリッジ<br/>(USB CDC ⇔ ESP-NOW 100Hz)"]
        MatrixLED["5x5 RGB LED マトリックス<br/>(通信 & 稼働ステータス表示)"]
    end

    subgraph MainUnit ["robot_main (M5Stack Core2)"]
        MainCore["メインコントローラ<br/>(ESP-NOW双方向 / I2Cマスター / 電源監視)"]
        FaceLCD["320x240 LCD<br/>(顔表情アニメーション & ステータスHUD)"]
        PowerCtrl["5V DCDC電源制御<br/>(GPIO19) & バッテリーADC(GPIO34)"]
    end

    subgraph SlaveUnits ["子局アクチュエータユニット群 (ATtiny1614)"]
        LegUnit["robot_leg (0x30)<br/>4輪X配置オムニホイール<br/>50Hz PWM x 4ch"]
        RightArm["robot_arm 右 (0x31)<br/>ソレノイド/高負荷モータ<br/>20kHz PWM (TCA0)"]
        LeftArm["robot_arm 左 (0x32)<br/>ソレノイド/高負荷モータ<br/>20kHz PWM (TCA0)"]
    end

    PC -->|USB Serial / 115200bps (SET/STP)| Bridge
    Bridge -->|USB Serial / 115200bps (TLM)| PC
    Bridge -->|ESP-NOW 100Hz / 16Byte Command| MainCore
    MainCore -->|ESP-NOW 500ms / 16Byte Telemetry| Bridge
    MainCore -->|I2C 10kHz-400kHz / Master (Write Data & Read DeviceID)| LegUnit
    MainCore -->|I2C 10kHz-400kHz / Master (Write Duty & Read DeviceID)| RightArm
    MainCore -->|I2C 10kHz-400kHz / Master (Write Duty & Read DeviceID)| LeftArm
```

### 1.2 ハードウェア構成 & マイコンの役割

| ユニット名 | ハードウェア / MCU | 通信 I/F | 役割・主な機能 |
| :--- | :--- | :--- | :--- |
| **`robot_bridge`** | M5Atom Matrix<br>(ESP32-PICO-D4) | USB CDC ⇔ ESP-NOW (Ch 1) | ホストのシリアルコマンドを受信しCore2へESP-NOW転送 (100Hz)。Core2からのテレメトリをシリアル (`TLM`) へ転送。5x5 RGB LEDで状態可視化。 |
| **`robot_main`** | M5Stack Core2<br>(ESP32-D0WDQ6-V3) | ESP-NOW ⇔ I2C (Master) | ロボット統括ユニット。I2Cで子局のデバイスID・死活を定期スキャン (500ms)。テレメトリをBridgeへ定期送信 (500ms)。表情描画・電源監視。 |
| **`robot_leg`** | ATtiny1614 | I2C Slave (`0x30`) | 脚制御ユニット。デバイスID `0x01: Omni (オムニホイール)`, `0x02: Tire (二輪差動)`, `0x03: Crawler (キャタピラ)`。キネマティクス演算、EEPROMキャリブレーション、50Hz PWM出力。 |
| **`robot_arm` (右)** | ATtiny1614 | I2C Slave (`0x31`) | 右アーム制御ユニット。デバイスID `0x01: Gatling (ガトリング)`, `0x02: Sword (ソード)`, `0x03: Cannon (大型レーザーキャノン)`。20kHz 高速PWM (TCA0) 出力。 |
| **`robot_arm` (左)** | ATtiny1614 | I2C Slave (`0x32`) | 左アーム制御ユニット。デバイスID `0x01: Gatling (ガトリング)`, `0x02: Sword (ソード)`, `0x03: Cannon (大型レーザーキャノン)`。左右・兵装別ビルド対応。 |

---

## 2. 📡 通信プロトコル & データ仕様

```text
[Host PC] 
   │  (1) USB CDC ASCII Command ("SET,100,200,0,1,0\n") ➔
   │  (4) ☕ USB CDC Telemetry ("TLM,1,1,2,7400\n")
   ▼
[robot_bridge]
   │  (2) ESP-NOW 16Byte Command Packet (100Hz 定期送信) ➔
   │  (3) ☕ ESP-NOW 16Byte Telemetry Packet (500ms 定期受信)
   ▼
[robot_main]
   │  (I2C Read REG_DEVICE_ID / Write Data)
   ├───> [robot_leg (0x30)]  : Read DeviceID (0x01..03), Write 6Bytes (Vx,Vy,Omega) + WD
   ├───> [robot_arm_r (0x31)]: Read DeviceID (0x01..03), Write 2Bytes (Duty,Pattern) + WD
   └───> [robot_arm_l (0x32)]: Read DeviceID (0x01..03), Write 2Bytes (Duty,Pattern) + WD
```

### 2.1 上位 ⇔ ブリッジ: シリアルコマンド仕様 (115200 bps, 8N1)

改行コード `\n` 区切りの大文字ASCIIテキスト形式。

| コマンド | フォーマット | 方向 | 説明 |
| :--- | :--- | :---: | :--- |
| **`SET`** | `SET,vx,vy,omega,arm_r,arm_l` | PC ➔ Bridge | 全軸速度 (-1000〜1000) および両アーム起動 (0/1) 一括更新 |
| **`STP`** | `STP` | PC ➔ Bridge | 全停止 (速度0, アーム停止) |
| **`TLM`** | `TLM` ➔ `TLM,leg_id,arm_r_id,arm_l_id,bat_mv` | PC ⇄ Bridge | **実機テレメトリ（脚ID, 右腕ID, 左腕ID, バッテリーmV）問い合わせ・応答** |
| **`STS`** | `STS` | PC ➔ Bridge | ステータス問い合わせ ➔ `STS,serial_st,espnow_st,vx,vy,omega,rarm,larm,wd` 返答 |
| **`MAC`** | `MAC` | PC ➔ Bridge | 自身のMACとCore2宛先MACを返信 |
| **`TGT`** | `TGT,XX:XX:XX:XX:XX:XX` | PC ➔ Bridge | 送信先Core2のMACを更新しEEPROM保存 |
| **`PIN`** | `PIN` | PC ➔ Bridge | 死活監視 (Ping) ➔ `PON` 返答 |

### 2.2 ブリッジ ⇔ メイン: ESP-NOW 双方向パケット仕様 (16Byte 固定長)

- **Wi-Fi モード**: `WIFI_STA` (Channel 1 固定)
- **コマンドパケット** (`Bridge` ➔ `Core2`, 100Hz):
```cpp
#pragma pack(push, 1)
typedef struct {
    int16_t vx;          // 前後移動速度 (-1000 〜 +1000)
    int16_t vy;          // 左右移動速度 (-1000 〜 +1000)
    int16_t omega;       // 旋回速度     (-1000 〜 +1000)
    uint8_t arm_right;   // 右アーム (0x00: 停止, 0x01: 起動)
    uint8_t arm_left;    // 左アーム (0x00: 停止, 0x01: 起動)
    uint8_t reserved[7]; // 予約領域 (7 Bytes)
    uint8_t watchdog;    // 送信毎にインクリメント
} EspNowCommandPacket;
#pragma pack(pop)
```

- **テレメトリパケット** (`Core2` ➔ `Bridge`, 500ms周期):
```cpp
#pragma pack(push, 1)
typedef struct {
    uint8_t leg_id;        // 脚ユニット デバイスID (0x01: Omni, 0x02: Tire, 0x03: Crawler)
    uint8_t arm_right_id;  // 右アーム デバイスID (0x01: Gatling, 0x02: Sword, 0x03: Cannon)
    uint8_t arm_left_id;   // 左アーム デバイスID (0x01: Gatling, 0x02: Sword, 0x03: Cannon)
    uint8_t status_flags;  // 状態フラグ (bit0: DCDC_5V_ON, bit1: BatLowWarn, bit2: BatCrit)
    uint16_t battery_mv;   // バッテリー電圧 (mV単位)
    uint8_t reserved[9];   // 予約領域 (9 Bytes)
    uint8_t watchdog;      // テレメトリウォッチドッグ
} EspNowTelemetryPacket;
#pragma pack(pop)
```

---

## 3. 🛠️ ビルド & 書き込み手順 (PlatformIO)

```bash
# 1. robot_bridge ビルド & 書込 (M5Atom Matrix)
pio run -d robot_bridge -t upload --upload-port /dev/ttyUSB0

# 2. robot_main ビルド & 書込 (M5Stack Core2)
pio run -d robot_main -t upload --upload-port /dev/ttyUSB0

# 3. robot_leg ビルド & 書込 (ATtiny1614: オプションで脚タイプ指定)
# 3.1 オムニホイール (0x01: デフォルト)
pio run -d robot_leg -e omni -t upload --upload-port /dev/ttyUSB0
# 3.2 二輪差動 (0x02)
pio run -d robot_leg -e tire -t upload --upload-port /dev/ttyUSB0
# 3.3 キャタピラ (0x03)
pio run -d robot_leg -e crawler -t upload --upload-port /dev/ttyUSB0

# 4. robot_arm ビルド & 書込 (ATtiny1614: オプションで左右・兵装指定)
# 4.1 右アーム: ガトリング (0x01) / ソード (0x02) / 大型レーザーキャノン (0x03)
pio run -d robot_arm -e arm_right_gatling -t upload --upload-port /dev/ttyUSB0
pio run -d robot_arm -e arm_right_sword -t upload --upload-port /dev/ttyUSB0
pio run -d robot_arm -e arm_right_cannon -t upload --upload-port /dev/ttyUSB0

# 4.2 左アーム: ガトリング (0x01) / ソード (0x02) / 大型レーザーキャノン (0x03)
pio run -d robot_arm -e arm_left_gatling -t upload --upload-port /dev/ttyUSB0
pio run -d robot_arm -e arm_left_sword -t upload --upload-port /dev/ttyUSB0
pio run -d robot_arm -e arm_left_cannon -t upload --upload-port /dev/ttyUSB0
```

---

## 4. 🧪 シリアル通信テストツール (`test_bridge.py`)

ホストPCから `robot_bridge` (M5Atom) の動作確認を行う Python テストスクリプトです。

### 実行引数仕様
```bash
python tools/test_bridge.py -c "<コマンド文字列>" -n <繰返回数> -p <使用ポート> -b <ボーレート>
```

### コマンド実行例
```bash
cd custack-robot

# 1. 死活確認 (Ping -> PON)
python tools/test_bridge.py -c "PIN" -p /dev/ttyUSB0 -b 115200

# 2. テレメトリ取得 (TLM -> 脚ID, 腕ID, 電圧)
python tools/test_bridge.py -c "TLM" -n 5 -p /dev/ttyUSB0

# 3. 制御パケット送信 (SET -> 速度 & アーム起動)
python tools/test_bridge.py -c "SET,200,0,0,1,0" -n 50 -p /dev/ttyUSB0

# 4. MACアドレス取得 (MAC)
python tools/test_bridge.py -c "MAC" -p /dev/ttyUSB0

# 5. ステータス取得 (STS)
python tools/test_bridge.py -c "STS" -p /dev/ttyUSB0

# 6. 送信先Core2 MACアドレスの変更登録 (TGT)
python tools/test_bridge.py -c "TGT,3C:8A:1F:D7:49:C0" -p /dev/ttyUSB0

# 7. 全コマンド総合自動テスト (ALL)
python tools/test_bridge.py -c "ALL" -p /dev/ttyUSB0
```

---

## 5. 💾 EEPROM 設定・書き換え支援ツール (`eeprom_tool.py`)

`robot_leg` (ATtiny1614) のサーボキャリブレーション値（オフセット・スケール）を保持する Intel HEX ファイルのパース・モニタ・生成・チェックサム自動計算・実機アップロードを行う CLI ツールです。

### 主なサブコマンド
- `monitor`: `.hex` ファイルまたは実機マイコンから読み出した EEPROM のパラメータを解析して一覧表示
- `set`: 既存の `.hex` ファイルを読み込み、指定パラメータのみを更新して保存（`-u` で実機書き込み可）
- `create`: 指定パラメータまたはプリセット (`omni` / `tire` / `crawler`) から新規 `.hex` ファイルを生成
- `upload`: 指定した `.hex` ファイルを `avrdude` 経由で実機 ATtiny1614 マイコンに書き込み
- `read`: 実機マイコンから EEPROM を読み出して `.hex` ファイルに保存

### コマンド実行例
```bash
cd custack-robot

# 1. 既存の .hex ファイル内容をモニタ表示
python tools/eeprom_tool.py monitor robot_leg/data/omni_eeprom.hex

# 2. 実機 ATtiny1614 から現在の EEPROM を読み出してモニタ
python tools/eeprom_tool.py monitor -p /dev/ttyUSB0

# 3. 既存 hex の特定チャンネルのオフセット/スケールを変更して保存
python tools/eeprom_tool.py set robot_leg/data/tire_eeprom.hex -o1 130 -s1 90

# 4. パラメータを変更して保存し、同時に実機マイコンへアップロード
python tools/eeprom_tool.py set robot_leg/data/omni_eeprom.hex --offsets 10,20,-10,0 -u /dev/ttyUSB0

# 5. プリセットから新規 hex を生成
python tools/eeprom_tool.py create robot_leg/data/new_crawler.hex --preset crawler

# 6. hex ファイルを実機マイコンへアップロード
python tools/eeprom_tool.py upload robot_leg/data/omni_eeprom.hex -p /dev/ttyUSB0

# 7. 実機から EEPROM をバックアップ保存
python tools/eeprom_tool.py read backup_eeprom.hex -p /dev/ttyUSB0
```

---

## 6. 🔌 USBシリアルポート固定化 (udev rules)

PC 接続時に 3台の M5Atom Matrix のポート番号が入れ替わらないよう、固定シンボリックリンクを作成します。

### ルール設定ファイル (`tools/99-custack-bridge.rules`)
- **1号機**: `ATTRS{serial}=="4D525B1DF0"` ➔ `/dev/custack_bridge_1`
- **2号機**: `ATTRS{serial}=="D1568C18F0"` ➔ `/dev/custack_bridge_2`
- **3号機**: `ATTRS{serial}=="695EF516F0"` ➔ `/dev/custack_bridge_3`

### 登録・反映コマンド (ホストPCで実行)
```bash
sudo cp tools/99-custack-bridge.rules /etc/udev/rules.d/
sudo udevadm control --reload-rules
sudo udevadm trigger
```

