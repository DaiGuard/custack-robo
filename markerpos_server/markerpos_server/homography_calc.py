import argparse
import logging
from .gopro_webcam.gopro_webcam import GoProCapture

import cv2
import cv2.aruco as aruco
import numpy as np


def main():

    # 引数を設定
    parser = argparse.ArgumentParser(
        description="GoPro Webcam capture software")
    
    parser.add_argument("-i", "--ip", type=str, default="172.24.169.51",
                        help="gopro IP address")
    parser.add_argument("-d", "--device", type=int, default=42,
                        help="gopro device id")    
    parser.add_argument("-s", "--size", type=int, nargs=2, default=[1920, 1080],
                        help="projection window size")
    args = parser.parse_args()

    # GoPro Webcam キャプチャーデバイスの確保
    gopro = GoProCapture(args.ip, args.device)

    # GoPro Webcamデバイスを開く
    if not gopro.open("1080", "linear"):
        logging.error("cannot open GoPro Webcam")
        return

    # デバックウインドウを準備
    cv2.namedWindow("GoPro Webcam", cv2.WINDOW_AUTOSIZE)

    # ArUcoマーカーの設定
    dictionary = aruco.getPredefinedDictionary(aruco.DICT_4X4_100)
    parameters = aruco.DetectorParameters()

    # 四つ角の点を格納する
    squares = [
        ("left-top", 96),
        ("right-top", 97),
        ("left-bottom", 98),
        ("right-bottom", 99)
    ]
    dst_points = {
        "left-top": np.array([0.0, 0.0]),
        "right-top": np.array([args.size[0], 0.0]),
        "left-bottom": np.array([0.0, args.size[1]]),
        "right-bottom": np.array([args.size[0], args.size[1]])
    }

    try:
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

            # 特徴点を抽出
            src_points = {}
            for square in squares:
                for id, corner in zip(ids, corners):
                    if square[1] == id:
                        src_points[square[0]] = corner[0][0]
                        break

            # ４つが見つかった場合のみ
            H = None
            if len(src_points) == 4:
                src = np.array(list(src_points.values()), dtype=np.float32)
                dst = np.array(list(dst_points.values()), dtype=np.float32)
                H, mask = cv2.findHomography(src, dst, cv2.RANSAC, 5.0)

                # 投影画像を生成
                warp = None
                if H is not None:
                    warp = cv2.warpPerspective(frame, H, (frame.shape[1], frame.shape[0]))
                else:
                    warp = np.zeros((frame.shape[0], frame.shape[1], 3), dtype=np.uint8)
                debug =cv2.hconcat([debug, warp])

            # デバック表示
            cv2.putText(debug, "s: save homography",
                        (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 1,
                        (0, 255, 0), 2, cv2.LINE_AA)
            cv2.putText(debug, "q: quit",
                        (10, 60), cv2.FONT_HERSHEY_SIMPLEX, 1,
                        (0, 255, 0), 2, cv2.LINE_AA)
            cv2.imshow("GoPro Webcam", cv2.resize(debug, None, fx=0.4, fy=0.4))        
            key = cv2.waitKey(30)        
            if key == ord("q"):
                break
            elif key == ord("s"):
                if H is not None:
                    np.savetxt('homography.txt', H, delimiter=',')
                    
    except KeyboardInterrupt:
        pass

    # GoPro Webcamデバイスを閉じる
    gopro.release()
    # デバックウインドウを閉じる
    cv2.destroyAllWindows()
