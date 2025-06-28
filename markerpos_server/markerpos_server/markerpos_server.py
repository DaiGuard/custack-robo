import argparse
import logging
from .gopro_webcam import GoProCapture

import cv2
import cv2.aruco as aruco


def main():

    # 引数を設定
    parser = argparse.ArgumentParser(
        description="GoPro Webcam capture software")
    
    parser.add_argument("-i", "--ip", type=str, default="172.24.169.51",
                        help="gopro IP address")
    parser.add_argument("-d", "--device", type=int, default=42,
                        help="gopro device id")    
    args = parser.parse_args()

    # GoPro Webcam キャプチャーデバイスの確保
    gopro = GoProCapture(args.ip, args.device)

    # GoPro Webcamデバイスを開く
    if not gopro.open("1080", "linear"):
        logging.error("cannot open GoPro Webcam")
        return

    # デバックウインドウを準備
    cv2.namedWindow("GoPro Webcam", cv2.WINDOW_AUTOSIZE)

    dictionary = aruco.getPredefinedDictionary(aruco.DICT_4X4_50)
    parameters = aruco.DetectorParameters()

    while True:
        # フレームを取得
        ret, frame = gopro.read()
        if not ret:
            continue

        # カラー画像をグレースケール変換
        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)

        # マーカーを検出
        corners, ids, _ = aruco.detectMarkers(gray, dictionary, parameters=parameters)

        # デバック画像の作成
        debug = frame.copy()
        aruco.drawDetectedMarkers(debug, corners, ids)

        # デバック表示
        cv2.imshow("GoPro Webcam", cv2.resize(debug, None, fx=0.5, fy=0.5))
        key = cv2.waitKey(30)
        if key == ord("q"):
            break

    # GoPro Webcamデバイスを閉じる
    gopro.release()
    # デバックウインドウを閉じる
    cv2.destroyAllWindows()
