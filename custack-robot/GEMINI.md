# custack-robot プロジェクト全体仕様

## プロジェクト概要

M5Stack Core2をメインユニットとし、ESP-NOWによる上位コマンド受渡と、I2C通信による子局（ATtiny1614）制御を行うロボット開発プロジェクト。

## 推奨AIモデル

- 全体設計・連携タスク: **Gemini 3.1 Pro**
- 各コンポーネント開発: **Gemini 3.6 Flash**

## システムアーキテクチャ & 通信仕様

- **上位通信**: ESP-NOW (固定長16Byteバイナリパケットフォーマット)
- **内部通信**: I2C (Master: M5Stack Core2 / Slave: ATtiny1614, 400kHz)
- **I2Cスレーブアドレス定義**:
  - `0x30`: 脚ユニット (`robot_leg`)
  - `0x31`: 右アームユニット (`robot_arm`)
  - `0x32`: 左アームユニット (`robot_arm`)

## ディレクトリ構成

- `robot_main/`: M5Stack Core2用 メイン統括プログラム
- `robot_bridge/`: M5Atom Matrix用 ESP-NOWブリッジユニットプログラム (USBシリアル ⇔ ESP-NOW)
- `robot_arm/`: ATtiny1614用 アーム制御プログラム
- `robot_leg/`: ATtiny1614用 脚・オムニホイール制御プログラム
- `tools/`: テスト用スクリプト群