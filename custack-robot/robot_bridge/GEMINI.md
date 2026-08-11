# robot_bridge (M5Atom Matrix) コンポーネント仕様書

## 1. ユニット概要

- **ハードウェア**: M5Atom Matrix (ESP32-PICO-D4 / 5x5 RGB LEDマトリックス / 本体ボタン)
- **役割**: ホストマシン (PC / WebGUI / ROS等) からUSBシリアル経由で制御コマンドを受信し、メインユニット **M5Stack Core2 (`robot_main`)** へ **ESP-NOW (固定長16Byteバイナリパケット)** でリアルタイム中継・送信するブリッジユニット。
- **フレームワーク**: Arduino (PlatformIO)
- **主要ライブラリ**: M5Atom (`m5stack/M5Atom@^0.1.3`), FastLED (`fastled/FastLED@^3.10.3`)

### ディレクトリ構成
```
robot_bridge/
├── include/
│   ├── espnow_com.h         # ESP-NOW通信クラス定義
│   ├── espnow_protocol.h    # ESP-NOWパケット構造体 & 定数定義
│   ├── matrix_display.h     # 5x5 LEDマトリックス表示クラス定義
│   └── serial_command.h     # シリアルコマンド解析・送受信クラス定義
├── src/
│   ├── espnow_com.cpp       # ESP-NOW送受信・ピア管理実装
│   ├── main.cpp             # メインループ & 各コンポーネント統合
│   ├── matrix_display.cpp   # LEDアニメーション・状態インジケータ描画実装
│   └── serial_command.cpp   # シリアルバッファリング & コマンドパーサー実装
└── platformio.ini           # PlatformIOビルド設定
```

---

## 2. 通信仕様 & パケットフォーマット

### ESP-NOW 送信仕様
- **動作モード**: Wi-Fi Station (`WIFI_STA`)
- **チャンネル**: 1
- **送信周期**: 10ms (100Hz)
- **宛先MACアドレス**: デフォルト `3C:8A:1F:D7:49:C0` (M5Stack Core2) ※シリアルコマンド `TGT` で動的変更可能
- **固定長パケット (16Bytes)**: `include/espnow_protocol.h`

```cpp
#pragma pack(push, 1)

// ESP-NOW 上位通信用 16Byte固定長パケット構造体
typedef struct {
    int16_t vx;          // 移動速度Vx (-1000 〜 1000)
    int16_t vy;          // 移動速度Vy (-1000 〜 1000)
    int16_t omega;       // 移動速度Omega (-1000 〜 1000)
    uint8_t arm_right;   // 右アームユニット起動フラグ (0x00:停止, 0x01:起動)
    uint8_t arm_left;    // 左アームユニット起動フラグ (0x00:停止, 0x01:起動)
    uint8_t reserved[7]; // 予約領域 (7Bytes padding)
    uint8_t watchdog;    // ウォッチドッグ (送信毎にインクリメント)
} EspNowCommandPacket;

#pragma pack(pop)
```

#### 定数定義
- `ESPNOW_ARM_STOP`: `0x00`
- `ESPNOW_ARM_START`: `0x01`

---

## 3. シリアルコマンド体系 (USB CDC / 115200bps)

改行コード `\n` 区切りでASCIIテキスト形式のコマンドを受信・解析します。大文字小文字は受信時に大文字に正規化されます。

| コマンド | フォーマット | 説明 | 返答 (レスポンス) |
| --- | --- | --- | --- |
| `SET` | `SET,vx,vy,omega,arm_r,arm_l` | 速度（-1000〜1000）およびアーム起動状態（0:停止, 1以上:起動）を一括設定 | なし |
| `VEL` | `VEL,vx,vy,omega` | 移動速度（-1000〜1000）のみを設定 | なし |
| `ARM` | `ARM,arm_r,arm_l` | アーム起動状態（0:停止, 1以上:起動）のみを設定 | なし |
| `STP` | `STP` | 全停止（Vx=0, Vy=0, Omega=0, arm_right=0, arm_left=0）を設定 | なし |
| `MAC` | `MAC` | 自身のWi-Fi MACアドレスを取得 | `MAC,XX:XX:XX:XX:XX:XX` |
| `TGT` | `TGT,XX:XX:XX:XX:XX:XX` | ESP-NOW 送信先端末のMACアドレスを動的更新 | `TGT,XX:XX:XX:XX:XX:XX` (登録失敗時はデバッグログ出力) |
| `PIN` | `PIN` | 死活確認 (Ping) | `PON` |
| `STS` | `STS` | 通信ステータスおよび現在の制御パケット値を即時返答 | `STS,serial_status,espnow_status,vx,vy,omega,rarm,larm,watchdog`<br>例: `STS,1,1,100,200,-50,1,0,42` |

### デバッグ出力 (DEBUG_LEVEL)
`platformio.ini` の `DEBUG_LEVEL` 定義（デフォルト: `4`）に応じてシリアルへログを出力します。
- `DEBUG_LEVEL > 0` (Error): `[E]:<message>`
- `DEBUG_LEVEL > 1` (Warn): `[W]:<message>`
- `DEBUG_LEVEL > 2` (Info): `[I]:<message>`
- `DEBUG_LEVEL > 3` (Debug): `[D]:<message>`

---

## 4. 安全保護 & ウォッチドッグ仕様

1. **ホスト通信途絶監視（タイムアウト保護）**:
   - ホストマシンからのシリアルコマンドが **1000ms** 途絶した場合 (`now - last_serial_ms > 1000`)、自動的に `setStop()` が呼び出され、全停止パケット（速度0、アーム停止）をCore2へ送信します。
2. **ESP-NOW 送信監視**:
   - 10ms毎の定期送信時、`esp_now_register_send_cb` のコールバックステータス (`ESP_NOW_SEND_SUCCESS`) によりCore2との接続状態 (`espnow_status`) を判定します。
3. **パケットウォッチドッグ**:
   - ESP-NOW送信周期 (10ms) ごとにパケット内の `watchdog` フィールドをインクリメントし、受信側 (Core2) での通信生存監視を可能にします。

---

## 5. 5x5 RGB LED マトリックス表示仕様

5x5 LEDマトリックスを用いて、通信状態およびアクチュエータの稼働状態をリアルタイム表示します (更新周期: 50ms)。

### LEDレイアウト座標 (X: 0〜4, Y: 0〜4)

```
       [X=0]   [X=1]   [X=2]   [X=3]   [X=4]
[Y=0]    .       .     [ R ]     .       .       ← ロボット通信 (ESP-NOW)
[Y=1]    .     [ R ]   [ R ]   [ R ]     .
[Y=2]  [ RA ]    .     [LEG]     .     [ LA ]    ← RA:右アーム, LEG:脚, LA:左アーム
[Y=3]    .     [ H ]   [ H ]   [ H ]     .
[Y=4]    .       .     [ H ]     .       .       ← ホスト通信 (Serial)
```

### 各インジケータの表示仕様

| 部位 | 座標 (X, Y) | 状態 | 表示色 & 動作 |
| --- | --- | --- | --- |
| **ロボット通信インジケータ (R)** | (2,0), (1,1), (2,1), (3,1) | **ESP-NOW 正常**<br>**ESP-NOW 途絶/エラー** | **黄色 点滅** (200ms周期: 100ms ON / 100ms OFF)<br>**赤色 常時点灯** |
| **ホスト通信インジケータ (H)** | (2,4), (1,3), (2,3), (3,3) | **シリアル通信 正常**<br>**シリアル タイムアウト (>1s)** | **白色 点滅** (200ms周期: 100ms ON / 100ms OFF)<br>**赤色 常時点灯** |
| **脚アクティブ (LEG)** | (2,2) | **移動中 (Vx/Vy/Omega ≠ 0)**<br>**停止中 (全速度 = 0)** | **緑色 点灯**<br>消灯 |
| **右アーム (RA)** | (0,2) | **右アーム起動中 (rarm > 0)**<br>**右アーム停止中** | **青色 点灯**<br>消灯 |
| **左アーム (LA)** | (4,2) | **左アーム起動中 (larm > 0)**<br>**左アーム停止中** | **青色 点灯**<br>消灯 |

> **起動時セルフテスト**: 起動時に全LEDが 赤 → 緑 → 青 → 白 の順に順次点灯テストされます。

---

## 6. ソフトウェア設計

### 主要クラス構成
- **`SerialCommand`**:
  - 改行区切りシリアル受信バッファ管理 (`pop` / `push`)
  - `SET`, `VEL`, `ARM` 等のコマンドパース処理
  - デバッグレベル別ログ出力
- **`ESPNowCom`**:
  - Wi-Fi Stationモード設定 & ESP-NOW初期化
  - ピア登録・削除・動的ターゲットMAC変更
  - 10ms周期のパケット送信 & ウォッチドッグ更新
  - 送信成否コールバックによる接続判定
- **`MatrixDisplay`**:
  - M5Atom 5x5 RGB LEDマトリックス制御
  - 通信状態（点滅/エラー色）および動作状態（移動/アーム色）のオーバーレイ表示

---

## 7. ビルド & 書き込み手順 (PlatformIO)

### platformio.ini

```ini
[env:m5stack-atom]
platform = espressif32
board = m5stack-atom
framework = arduino
monitor_speed = 115200
upload_speed = 115200
lib_deps =
    m5stack/M5Atom@^0.1.3
    fastled/FastLED@^3.10.3
build_flags = 
    -DCORE_DEBUG_LEVEL=0
    -DDEBUG_LEVEL=4
```

### コマンド例 (プロジェクトルートから実行する場合)

```bash
# ビルド
pio run -d robot_bridge

# 書き込み (ポート例: /dev/ttyUSB0)
pio run -d robot_bridge -t upload --upload-port /dev/ttyUSB0

# シリアルモニタ起動
pio device monitor -d robot_bridge -b 115200
```

> **Note**: `robot_bridge/` ディレクトリ内で実行する場合は `-d robot_bridge` は不要です（例: `pio run`, `pio run -t upload`）。


