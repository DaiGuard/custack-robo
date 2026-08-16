# custack-hardware ハードウェア・回路仕様書

本ディレクトリ (`custack-hardware`) は、**CuStack ロボットシステム** における基板回路（KiCad v10: `custack-electric`）およびハードウェア機構設計データを管理するディレクトリです。

---

## 1. 🏗️ システム全体アーキテクチャ

CU-Stack ロボットは、**M5Stack**（ホストコントローラ）を中心に、各モジュールに **ATtiny1614**（サブマイコン）を配置した **I2Cバス分散制御アーキテクチャ** を採用しています。

- **ホストマイコン**: M5Stack Core / Core2 / Basic（メイン基板のバスソケット `J3` にスタック接続）
- **内部通信バス**: I2Cバス (`EXT_SCL`, `EXT_SDA`) + 電源 (`5V/VDD`, `GND`) の4線式
- **各ユニット接続**: 4ピン ポゴピン/マグネットコネクタ (`C-C5-05 4P`) により、モジュール着脱が容易な構造
- **基板製造形態**: 5種類の基板が1枚のパネルに面付け（パネライゼーション）され、マウスバイト（ミシン目）で分離可能（2層 FR4 1.6mm）

---

## 2. 🔌 基板別ハードウェア構成 & 回路仕様

### ① バッテリー基板 (`battery_board.kicad_sch`)
* **電源**: 18350 Li-ion 2S（公称7.4V / 満充電8.4V）ホルダー
* **保護回路**: 6A リセッタブルヒューズ (`MF-USML300/12-2`), 逆接保護ショットキー (`B320A-13-F`)
* **主電源スイッチ**: スライドスイッチ (`CSS-1210TB`) + P-MOSFET (`SSM3J328R`, 20V 6A) によるハイサイドスイッチ回路
* **出力**: JST XH 2P コネクタ (`J1`) → メイン基板へ `+VSW`, `GND` を供給

### ② メイン基板 (`main_board.kicad_sch`)
* **M5Stack バスソケット**: 2x15P 2.54mm SMD (`J3`)
* **サーボ用大電流DCDC**: Murata `OKL-T/6-W12N-C`（出力5V / **最大6A**）
  * NMOSFET (`SSM3K376R`) による M5Stack からのサーボ電源 ON/OFF 制御対応 (GPIO19)
* **ロジック用DCDC**: TI `TPS562200DDCR`（出力3.3V / 最大2A）
* **バッテリー監視**: 抵抗分圧 (約2.96:1) による電圧検出信号 `BATSEN` を M5Stack ADC (GPIO34) へ入力
* **末端ユニット中継**: JST PH 4P コネクタ x3 (`J4`, `J5`, `J6`) → 左腕・右腕・脚ユニットへ配線

### ③ 脚ユニット基板 (`legunit_board.kicad_sch`)
* **サブマイコン**: Microchip `ATTINY1614-SSNR` (20MHz, 16KB Flash) - I2Cスレーブ (`0x30`)
* **電源**: 3.3V LDO (`MCP1702T-3302E/CB`, 250mA)
* **サーボ出力**: JST PH 3P コネクタ x4 (`J9`, `J10`, `J11`, `J12` / `PWM1`〜`PWM4`)
* **信号レベルシフト**: TI `SN74LVC1G07DBVR` (オープンドレインバッファ x4) により 3.3V PWM を 5V サーボ信号レベルに昇圧・保護
* **デバッグ/書込**: JST PH 3P (`J8`: UPDI, 3.3V, GND)

### ④ 左腕 / 右腕ユニット基板 (`leftarmunit_board.kicad_sch`, `rightarmunit_board.kicad_sch`)
* **サブマイコン**: Microchip `ATTINY1614-SSNR` - I2Cスレーブ (`0x31` 右 / `0x32` 左)
* **電源**: 3.3V LDO (`MCP1702T-3302E/CB`, 250mA)
* **アクチュエータ出力**: JST PH 2P コネクタ (`J18` / `J15`)
  * パワーNMOSFET (`SSM3K376R`, 30V 4A) + フライホイールダイオード (`MBR230LSFT1G`) によるモーター/ソレノイド/電磁石の20kHz PWM駆動
* **デバッグ/書込**: JST PH 3P (`J17` / `J14`: UPDI, 3.3V, GND)

---

## 3. 📌 コネクタ・ピンアサイン一覧

| コネクタ | 基板 | ピン数/形状 | ピンアサイン (1→4) | 接続先 / 用途 |
|---|---|---|---|---|
| `J1` | バッテリー | 2P XH 2.5mm | +VSW, GND | メイン基板電源入力へ |
| `J2` | メイン | 2P XH 2.5mm | +VSW, GND | バッテリー基板から受電 |
| `J3` | メイン | 30P Bus | M5Stack Bus 定義 (I2C, ADC, GPIO, 5V, 3V3, GND) | M5Stack スタック |
| `J4` | メイン | 4P PH 2.0mm | VDD(5V), EXT_SCL, EXT_SDA, GND | 左腕ユニットへ中継 |
| `J5` | メイン | 4P PH 2.0mm | VDD(5V), EXT_SCL, EXT_SDA, GND | 右腕ユニットへ中継 |
| `J6` | メイン | 4P PH 2.0mm | VDD(5V), EXT_SCL, EXT_SDA, GND | 脚ユニットへ中継 |
| `J7` | 脚ユニット | 4P Pogopin | VDD(5V), EXT_SCL, EXT_SDA, GND | メイン基板からの受電&通信 |
| `J8` | 脚ユニット | 3P PH 2.0mm | UPDI, +3.3V, GND | マイコン書き込み |
| `J9~J12` | 脚ユニット | 3P PH 2.0mm | PWM1~4, VDD(5V), GND | サーボモータ x4 |
| `J13` | 右腕ユニット | 4P Pogopin | VDD(5V), EXT_SCL, EXT_SDA, GND | メイン基板からの受電&通信 |
| `J14` | 右腕ユニット | 3P PH 2.0mm | UPDI, +3.3V, GND | マイコン書き込み |
| `J15` | 右腕ユニット | 2P PH 2.0mm | VDD(5V), FET Drain | アクチュエータ出力 |
| `J16` | 左腕ユニット | 4P Pogopin | VDD(5V), EXT_SCL, EXT_SDA, GND | メイン基板からの受電&通信 |
| `J17` | 左腕ユニット | 3P PH 2.0mm | UPDI, +3.3V, GND | マイコン書き込み |
| `J18` | 左腕ユニット | 2P PH 2.0mm | VDD(5V), FET Drain | アクチュエータ出力 |
