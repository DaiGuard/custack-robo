from typing import Tuple
import os
import sys
import subprocess
import cv2
import numpy as np
import requests
import logging
import time

logging.basicConfig(
    level=logging.INFO, stream=sys.stdout,
    format="%(levelname)s:[GoProCapture] %(message)s")

class GoProCapture():
    def __init__(self, ip_address: str, id: int):
        """コンストラクタ

        Args:
            ip_address (str): GoPro IPアドレス
            id (int): カメラデバイスID
        """
        self.__ip_address = ip_address
        self.__id = id
        self.__cmd_path = "/gp/gpWebcam/"
        self.__process = None
        self.__cap = None

        self.__supported_resolutions = ["1080", "720, 480"]
        self.__supported_fov = {
            "wide": 0,
            "superwide": 3,
            "linear": 4,
            "narrow": 6
        }

    def __del__(self):
        """デストラクタ
        """
        self.release()

    def open(self, resolution: str, fov: str) -> bool:
        """デバイスオープン

        Args:
            resolution (str): 解像度[1080, 720, 480]
            fov (str): レンズモード[wide, superwide, linear, narrow]

        Returns:
            bool: True:成功, False:失敗
        """
        # 解像度とFOVの設定値を確認
        if resolution not in self.__supported_resolutions:
            msg = f"not support resolution [{resolution}]\n"
            msg += f"support resolutions: {self.__supported_resolutions}"
            logging.error(msg)
            return False
        
        if fov not in self.__supported_fov.keys():
            msg = f"not support fov [{fov}]\n"
            msg += f"support fov: {self.__supported_fov.keys()}"
            logging.error(msg)
            return False
        
        # 強制停止
        self.release()

        # 仮想デバイスファイルの作成
        if not os.path.exists(f"/dev/video{self.__id}"):
            res = subprocess.run([
                "sudo", "-S", 
                "modprobe", "v4l2loopback",
                "devices=1",
                "exclusive_caps=1",  "card_label='GoPro'",
                f"video_nr={self.__id}"])
            if res.returncode != 0:
                return False
            
        # GoPro Webcamモードの開始
        res = requests.get(
            f"http://{self.__ip_address}{self.__cmd_path}START?" \
                + f"res={resolution}"
        )
        if res.status_code != 200:
            logging.error(f"failed to start webcam ({res.status_code})")
            return False

        json_data = res.json()
        if json_data["status"] != 2:
            logging.error(f"failed to start webcam ({json_data['status']}")
            return False

        # GoPro Webcamモード開始待ち
        logging.info("waiting gopro webcam start...")
        time.sleep(5)

        # GoPro Webcam Fov設定
        fov_id = self.__supported_fov[fov]
        res = requests.get(
            f"http://{self.__ip_address}{self.__cmd_path}SETTINGS?" \
                + f"fov={fov_id}"
        )
        if res.status_code != 200:
            logging.error(f"failed to set webcam ({res.status_code})")
            return False

        # GoPro Webcam Fov設定完了待ち
        logging.info("waiting gopro webcam setting...")
        time.sleep(5)

        # ffmpegのキャプチャプロセスを開始
        process = subprocess.Popen([
            "ffmpeg",
            "-nostdin",
            "-threads", "1",
            "-i", "udp://@0.0.0.0:8554?overrun_nonfatal=1&fifo_size=1000000&buffer_size=65536",
            "-f:v", "mpegts",
            "-flags", "low_delay",
            "-fflags", "nobuffer",
            "-avioflags", "direct",
            "-probesize", "8192",
            "-analyzeduration", "100000",
            "-vf", "setpts=0",
            "-vf", "format=yuv420p",
            "-f",  "v4l2", 
            "-vcodec", "rawvideo",
            "-pix_fmt", "yuv420p",
            "-vsync", "0",
            f"/dev/video{self.__id}"
        ], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

        self.__process = process

        # キャプチャ開始を待つ
        time.sleep(5)

        # キャプチャデバイスの確保
        try:
            self.__cap = cv2.VideoCapture(f"/dev/video{self.__id}")
            if not self.__cap.isOpened():
                logging.error(f"cannot open camera /dev/video{self.__id}")
                return False

            # バッファサイズの制限
            self.__cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
        except Exception as e:
            logging.error(e)
            return False
        
        return True
    
    def release(self) -> None:
        """デバイス開放
        """
        # キャプチャデバイスの開放
        if self.__cap:
            self.__cap.release()
            self.__cap = None

        # ffmpegのキャプチャプロセスを終了
        if self.__process:
            if self.__process.poll() is None:
                self.__process.terminate()
                self.__process.wait()

            logging.info(f"stop webcam PID[{self.__process.pid}]")
            self.__process = None

        # GoPro Webcamモードの停止
        res = requests.get(f"http://{self.__ip_address}{self.__cmd_path}STOP")
        if res.status_code == 200:
            json_data = res.json()
            if json_data["status"] != 1:
                logging.error(f"failed to stop webcam ({json_data['status']})")
            else:
                logging.info("successfully stop GoPro webcam")
        else:
            logging.error(f"failed to stop webcam ({res.status_code})")

        # 仮想デバイスファイルの削除
        if os.path.exists(f"/dev/video{self.__id}"):
            res = subprocess.run([
                "sudo", "-S", 
                "modprobe", "-r", "v4l2loopback"])
            if res.returncode != 0:
                logging.error("failed to create virtual camera device")

    def read(self) -> Tuple[bool, np.ndarray]:
        """フレーム読み出し

        Returns:
            Tuple[bool, np.ndarray]: 成功 or 失敗, フレーム
        """
        # キャプチャデバイスが開いているか確認
        if self.__cap is None:
            logging.error("camera is not opened")
            return False, None

        # フレームの読み取り
        ret, frame = self.__cap.read()
        if not ret:
            return False, None

        return True, frame