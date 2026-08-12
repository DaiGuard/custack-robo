# ｶｽﾀｯｸﾛﾎﾞ (Custack-robo)

custack-robo のゲーム演出およびロボット連携を行うための統合リポジトリです。
Jetson 上での画像認識・位置推定、ホストPC/Unity 上でのプロジェクション演出・共有メモリ連携、M5Stack/ATtiny による実機制御を行います。

<img src="https://github.com/user-attachments/assets/501e7aa1-9e15-4bd2-bd83-8cfae46a2b0e" width="80%" />

---

## 📁 フォルダ構成

```text
jetson/ (ワークスペースルート)
├── README.md
├── aiplan/            # システム設計・実装計画書
├── custack_ws/        # ROS 2 (Humble) ワークスペース
│   ├── build_jetson.sh    # Jetson 用クリーン＆ビルドスクリプト (locator + web)
│   ├── build_host.sh      # ホスト用クリーン＆ビルドスクリプト (router)
│   ├── run_locator.sh     # カメラ認識・自己位置推定ノード起動スクリプト
│   ├── run_router.sh      # 共有メモリ・シリアルルーターノード起動スクリプト
│   ├── run_web.sh         # キャリブレーション用 Web UI サーバー起動スクリプト
│   └── src/               # ROS 2 パッケージ群 (custack_locator_cpp, custack_router, custack_web, custack_msgs)
├── custack-unity/     # Unity プロジェクト (ゲーム演出・共有メモリ高速連携)
│   └── Assets/Scenes/MultiDisplayProjects/Scripts/
│       ├── SharedMemoryPoseViewer.cs  # 共有メモリ 3台位置姿勢インスペクターモニター
│       ├── SharedMemoryManager.cs     # 共有メモリ送受信・コントローラ連携マネージャー
│       └── RosManager.cs              # ROS-TCP 連携マネージャー (レガシー)
├── custack-robot/     # 実機ロボット制御ファームウェア
│   ├── robot_main/        # M5Stack Core2 メイン統括プログラム
│   ├── robot_bridge/      # M5Atom Matrix ESP-NOW ブリッジプログラム
│   ├── robot_arm/         # ATtiny1614 アーム制御プログラム
│   └── robot_leg/         # ATtiny1614 脚・オムニホイール制御プログラム
└── custack-hardware/  # ハードウェア CAD・回路設計データ
```

---

## 🚀 クイックスタート

### 1. Jetson 環境 (位置推定・カメラ認識)
```bash
# ワークスペースディレクトリへ移動
cd custack_ws

# クリーン＆ビルド
./build_jetson.sh

# 位置推定ノード起動
./run_locator.sh
```

### 2. ホストPC環境 (ルーターノード & Unity)
```bash
# ワークスペースディレクトリへ移動
cd custack_ws

# クリーン＆ビルド
./build_host.sh

# ルーターノード起動 (共有メモリ & M5Atom シリアル中継)
./run_router.sh
```
Unity 側で `custack-unity` プロジェクトを開き、`SharedMemoryPoseViewer` または `SharedMemoryManager` を利用してゲームを実行します。
