# markerpos_server

Arucoマーカの位置を配信するサーバー

---

## 配信

- **/maker_*/pose (geometory_msgs/PoseStamped)**

    マーカの位置を出力する。マーカIDにトピック名称を合わせる

## パラメータ

- **/debug [bool] default: false**

    デバックモードの切り替え

## 起動方法

```bash
# ソースコードの同期
./sync_to_jetson.sh

# サーバーの起動
./start_to_jetson.sh

# サーバーの停止
./stop_to_jetson.sh
```

---

## チートシート

```bash
# コンテナ起動（バックグラウンド）
docker compose up -d

# コンテナにアタッチ
docker compose exec ros-jazzy bash

# コンテナ終了
docker compose down
```


```bash
# GUI有効
sudo systemctl set-default graphical.target

# GUI無効
sudo systemctl set-default multi-user.target
```