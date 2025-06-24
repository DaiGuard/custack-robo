"""

```bash
# 仮想カメラデバイスを作成する
sudo modprobe v4l2loopback exclusive_caps=1 card_label='GoPro' video_nr=42

# GoProをWebcamモードで起動する
curl -s 172.24.169.51/gp/gpWebcam/START?res=1080

# GoProのデジタルレンズモートを選択する
curl -s 172.24.169.51/gp/gpWebcam/SETTINGS?fov=4

# ffmpegを使用して仮想カメラデバイスにビデオストリームを出力する
ffmpeg -nostdin -threads 1 \
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
import os
import subprocess

GOPRO_IP = "172.24.169.51"
GOPRO_PATH = "/gp/gpWebcam/"
GOPRO_VIDEO_ID=42
GOPRO_CMDS = {
    "start": "START",
    "stop": "STOP",
    "setting": "SETTING"
}

def webcam_start(resolution: str) -> tuple[bool, subprocess.Popen]:
    if not os.path.exists(f"/dev/video{GOPRO_VIDEO_ID}"):
        res = subprocess.run([
            "sudo", "-S", 
            "modprobe", "v4l2loopback",
            "exclusive_caps=1",  "card_label='GoPro'",
            f"video_nr={GOPRO_VIDEO_ID}"])
        if res.returncode != 0:
            print("[E]: Failed to create virtual camera device")
            return False, None

    if resolution in ["1080", "720", "480"]:
        res = requests.get(
            f"http://{GOPRO_IP}{GOPRO_PATH}START?" \
                + f"res={resolution}"
        )
        if res.status_code != 200:
            print(f"[E]: Failed to start webcam ({res.status_code})")
            return False, None

        json_data = res.json()
        if json_data["status"] != 2:
            print(f"[E]: Failed to start webcam ({json_data['status']}")
            return False, None
        
        process = subprocess.Popen([
            "ffmpeg", "-nostdin", "-threads", "1",
            "-i", "udp://@0.0.0.0:8554?overrun_nonfatal=1&fifo_size=5000000",
            "-f:v", "mpegts", "-fflags", "nobuffer",
            "-flags", "low_delay", "-vf", "setpts=0",

            "-vf", "format=rgb24", "-f",  "v4l2", f"/dev/video{GOPRO_VIDEO_ID}"
            # "-vf", "format=yuv420p", "-f",  "v4l2", f"/dev/video{GOPRO_VIDEO_ID}"
            ])
        print(f"[I]: Start webcam PID[{process.pid}]")
        return True, process
    else:
        print("[E]: Invalid resolution")
        return False, None

def webcam_stop(process: subprocess.Popen=None) -> bool:

    if process:
        process.terminate()
        process.wait()
        print(f"[I]: Stop webcam PID[{process.pid}]")

    res = requests.get(f"http://{GOPRO_IP}{GOPRO_PATH}STOP")
    if res.status_code == 200:
        json_data = res.json()
        if json_data["status"] != 1:
            print(f"[E]: Failed to stop webcam ({json_data['status']})")
        else:
            print("[I]: Stop webcam")
    else:
        print(f"[E]: Failed to stop webcam ({res.status_code})")

    
    if os.path.exists(f"/dev/video{GOPRO_VIDEO_ID}"):
        res = subprocess.run([
            "sudo", "-S", 
            "modprobe", "-r", "v4l2loopback"])
        if res.returncode != 0:
            print("[E]: Failed to create virtual camera device")


def webcam_setting(fov: str):
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
            return True
        else:
            print(f"[E]: Failed to set webcam ({res.status_code})")
            return False
    else:
        print("[E]: Invalid fov")
        return False


if __name__ == "__main__":
    import time
    import cv2

    # WEBカメラモード強制停止
    webcam_stop()
    # WEBカメラモード開始
    ret, proc = webcam_start("1080")
    if not ret:
        exit()
    time.sleep(5)

    # カメラレンズモード設定
    # ret = webcam_setting("wide")
    ret = webcam_setting("linear")
    # ret = webcam_setting("narrow")
    if not ret:
        exit()
    time.sleep(5)

    cap = None
    try:
        cap = cv2.VideoCapture(f"/dev/video{GOPRO_VIDEO_ID}")
        if not cap.isOpened():
            raise RuntimeError("[E]: cannnot open device")

        while True:
            ret, frame = cap.read()
            cv2.imshow("frame", frame)
            if cv2.waitKey(1) & 0xFF == ord("q"):
                break
    except KeyboardInterrupt:
        pass
    except Exception as e:
        print(e)

    if cap is not None:
        cap.release()
    cv2.destroyAllWindows()

    webcam_stop(proc)
