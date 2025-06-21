# ｶｽﾀｯｸﾛﾎﾞ(Custack-robo)

custack-roboのゲーム演出をするためのプログラムです
Unityで構成されていて、Unityゲーム画面をプロジェクタで投影して、ロボットのバトル描画を行う

<img src="https://github.com/user-attachments/assets/501e7aa1-9e15-4bd2-bd83-8cfae46a2b0e" width="80%" />

## システム構成

<img src="docs/systemdiagram.drawio.png" width="80%" />

## フォルダ構成

```tree
.
├── projector-calibration # プロジェクタキャリブレーションソフト
├── custack-robo-unity # Unityプログラム
├── tools # ツール
└── README.md
```

## ToDoリスト

- [ ] 武器切替機能の実装
- [ ] ロボット投影モデルの試作
- [ ] ZeroMQサーバ、クライアントの試作
- [ ] GoProカメラキャプチャの試作
