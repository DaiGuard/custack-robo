# ｶｽﾀｯｸﾛﾎﾞ(Custack-robo)

custack-roboのゲーム演出をするためのプログラムです
Unityで構成されていて、Unityゲーム画面をプロジェクタで投影して、ロボットのバトル描画を行う

<img src="https://github.com/user-attachments/assets/501e7aa1-9e15-4bd2-bd83-8cfae46a2b0e" width="80%" />

---

## システム構成

<img src="docs/systemdiagram.drawio.png" width="80%" />

## フォルダ構成

```tree
.
├── controller_bridge/        #Unity⇔custackホスト間通信
├── custack-controller-host/  #custackホスト内プログラム
├── custack-controller-robot/ #custackロボット内プログラム
├── custack-robo-unity/       #Unityゲーム画面
├── docs/                     #ドキュメント
├── markerpos_server/         #マーカ位置サーバ
├── projector-calibration/    #(廃止) プロジェクタとカメラの校正
├── README.md
└── tests/                    #テストプログラム
```

## ToDoリスト

### 
- [x] 武器切替機能の実装
- [x] ロボット投影モデルの試作
    - [x] ロボットの内側を白くするように変更
- [x] ZeroMQサーバ、クライアントの試作
- [x] GoProカメラキャプチャの試作
- [ ] 接続状態をUIとして表示する
- [x] 終了ボタンを作る
- [ ] 障害物を設置する
- [x] マルチタスクスレッドで処理が固まる（廃止）
    - [x] Queueを使用する（廃止）
    - [x] UniTaskを使用する（廃止）
- [x] Homographyの4点を自由に設定する
- [x] コントローラの切断を検知する
- [ ] ダメージ表示の機能追加
