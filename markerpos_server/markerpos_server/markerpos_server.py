import sys
import logging
import argparse
from .gopro_webcam import GoProCapture

import cv2
import cv2.aruco as aruco
import numpy as np

logging.basicConfig(
    level=logging.INFO, stream=sys.stdout,
    format="%(levelname)s:[markerpos_server] %(message)s")

def main():

    # 引数を設定
    parser = argparse.ArgumentParser(
        description="GoPro Webcam capture software")
    
    parser.add_argument("-i", "--ip", type=str, default="172.24.169.51",
                        help="gopro IP address")
    parser.add_argument("-d", "--device", type=int, default=42,
                        help="gopro device id")
    parser.add_argument("-c", "--calib", type=str, default="homography.txt",
                        help="homography calibration file")
    args = parser.parse_args()

    H = np.loadtxt(args.calib, delimiter=',')
    if H is None:
        logging.error("cannot load homography calibration file")
        return

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

        p1 = None
        p2 = None

        for id, corner in zip(ids, corners):
            corner = np.reshape(corner, [-1, 2])
            center = np.mean(corner, axis=0)
            front = np.mean(corner[:2], axis=0)

            # デバック描画
            debug = cv2.circle(debug, (int(center[0]), int(center[1])),
                       10, (0, 0, 255), -1)
            debug = cv2.line(debug, (int(center[0]), int(center[1])),
                       (int(front[0]), int(front[1])),
                       (0, 255, 0), 2)

            if id == 0:
                p1 = (center, front)
            elif id == 1:
                p2 =(center, front)

        # ワープ投影
        warp =cv2.warpPerspective(frame, H, (frame.shape[1], frame.shape[0]))
        if p1 is not None and p2 is not None:
            src = np.array([p1[0], p1[1], p2[0], p2[1]]).reshape(-1, 1, 2)
            dst = cv2.perspectiveTransform(src, H).reshape(-1, 2)

            warp = cv2.circle(warp, (int(dst[0][0]), int(dst[0][1])),
                       10, (0, 0, 255), -1)
            warp = cv2.circle(warp, (int(dst[2][0]), int(dst[2][1])),
                       10, (0, 0, 255), -1)
            warp = cv2.line(warp, (int(dst[0][0]), int(dst[0][1])),
                       (int(dst[1][0]), int(dst[1][1])),
                       (0, 255, 0), 2)
            warp = cv2.line(warp, (int(dst[2][0]), int(dst[2][1])),
                       (int(dst[3][0]), int(dst[3][1])),
                       (0, 255, 0), 2)

        debug = cv2.hconcat([debug, warp])

        # デバック表示
        cv2.imshow("GoPro Webcam", cv2.resize(debug, None, fx=0.4, fy=0.4))
        key = cv2.waitKey(30)
        if key == ord("q"):
            break

    # GoPro Webcamデバイスを閉じる
    gopro.release()
    # デバックウインドウを閉じる
    cv2.destroyAllWindows()
