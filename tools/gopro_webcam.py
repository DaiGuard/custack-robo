"""

```bash
# 仮想カメラデバイスを作成する
sudo modprobe v4l2loopback exclusive_caps=1 card_label='GoPro' video_nr=42

# GoProをWebcamモードで起動する
curl -s 172.24.169.51/gp/gpWebcam/START?res=1080

# GoProのデジタルレンズモートを選択する
curl -s 172.24.169.51/gp/gpWebcam/SETTINGS?fov=4

# ffmpegを使用して仮想カメラデバイスにビデオストリームを出力する
ffmpeg -threads 1 \
    -i 'udp://@0.0.0.0:8554?overrun_nonfatal=1&fifo_size=5000000'\
    -f:v mpegts -fflags nobuffer -flags low_delay -vf setpts=0 \
    -vf format="yuv420p" -f v4l2 /dev/video42

# 低遅延のため
- -threads 1 が最速
- -probesize 32 -analyzeduration 1 はカメラの起動が早くなる
- fifo_size=1555200で少し遅延が減る

# 仮想カメラデバイスを削除する
sudo modprobe -rf v4l2loopback
```

"""

import requests

GOPRO_IP = "172.24.169.51"
GOPRO_PATH = "/gp/gpWebcam/"
GOPRO_CMDS = {
    "start": "START",
    "stop": "STOP",
    "setting": "SETTING"
}

def webcam_start(resolution: str) -> tuple[bool, int, int]:
    if resolution in ["1080", "720", "480"]:
        res = requests.get(
            f"http://{GOPRO_IP}{GOPRO_PATH}START?" \
                + f"res={resolution}"
        )

        if res.status_code == 200:
            json_data = res.json()
            return True, json_data["status"], json_data["error"]
        else:
            print(f"[E]: Failed to start webcam ({res.status_code})")
            return False, -1, 0
    else:
        print("[E]: Invalid resolution")
        return False, -1, 0

def webcam_stop() -> tuple[bool, int, int]:
    res = requests.get(f"http://{GOPRO_IP}{GOPRO_PATH}STOP")

    if res.status_code == 200:
        json_data = res.json()
        return True, json_data["status"], json_data["error"]
    else:
        print(f"[E]: Failed to stop webcam ({res.status_code})")
        return False, -1, 0

def webcam_setting(fov: str) -> tuple[bool, int, int]:
    if fov in ["wide", "superwide", "linear", "narrow"]:

        fov_id = 0
        if fov == "wide":
            fov_id = 0
        if fov == "superwide":
            fov_id = 3
        elif fov == "linear":
            fov_id = 4
        elif fov == "narrow":
            fov_id = 6

        res = requests.get(
            f"http://{GOPRO_IP}{GOPRO_PATH}SETTINGS?" \
                + f"fov={fov_id}"
        )

        if res.status_code == 200:
            json_data = res.json()
            return True, json_data["status"], json_data["error"]
        else:
            print(f"[E]: Failed to set webcam ({res.status_code})")
            return False, -1, 0
    else:
        print("[E]: Invalid fov")
        return False, -1, 0


if __name__ == "__main__":
    import time
    import cv2

    # webcam_start("1080")
    # time.sleep(5)
    # webcam_setting("wide")
    # time.sleep(5)
    # webcam_setting("linear")
    # time.sleep(5)
    # webcam_setting("narrow")
    # time.sleep(5)

    cap = cv2.VideoCapture("/dev/video42")
    while True:
        ret, frame = cap.read()
        cv2.imshow("frame", frame)
        if cv2.waitKey(1) & 0xFF == ord("q"):
            break

    cap.release()
    cv2.destroyAllWindows()

    # webcam_stop()
