import sys
import logging
import argparse
from .gopro_webcam.gopro_webcam import GoProCapture

import cv2
import cv2.aruco as aruco
import numpy as np

logging.basicConfig(
    level=logging.INFO, stream=sys.stdout,
    format="%(levelname)s:[homography_calc] %(message)s")

def main():

    # 引数を設定
    parser = argparse.ArgumentParser(
        description="Homography calibration software")
    
    parser.add_argument("-i", "--ip", type=str, default="172.24.169.51",
                        help="gopro IP address")
    parser.add_argument("-d", "--device", type=int, default=42,
                        help="gopro device id")    
    # parser.add_argument("-s", "--size", type=int, nargs=2, default=[1920, 1080],
    #                     help="projection window size")
    parser.add_argument("-s", "--size", type=float, nargs=2, default=[2.3, 1.36],
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
        "left-top"    : np.array([-args.size[0]/2.0,  args.size[1]/2.0]),
        "right-top"   : np.array([ args.size[0]/2.0,  args.size[1]/2.0]),
        "left-bottom" : np.array([-args.size[0]/2.0, -args.size[1]/2.0]),
        "right-bottom": np.array([ args.size[0]/2.0, -args.size[1]/2.0])
    }

    # 停止処理用フラグ
    stop_request = False
    stop_enable = False

    # フレーム保存
    frame = None
    poses = np.array([
        [0.0, 0.0],
        [0.0, 0.0]
    ])
    try:
        while True:

            if not stop_enable:
                # フレームを取得
                ret, frame = gopro.read()
                if not ret:
                    continue
            if frame is None:
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
            if ids is not None and corners is not None:
                for square in squares:
                    for id, corner in zip(ids, corners):
                        if square[1] == id:
                            src_points[square[0]] = corner[0][0]
                            break

                for id, corner in zip(ids, corners):
                    if id in [0, 1]:
                        corner = np.reshape(corner, [-1, 2])
                        center = np.mean(corner, axis=0)
                        poses[int(id)] = center

            # ４つが見つかった場合のみ
            H = None
            if len(src_points) == 4:
                src = np.array(list(src_points.values()), dtype=np.float32)
                dst = np.array(list(dst_points.values()), dtype=np.float32)
                H, mask = cv2.findHomography(src, dst, cv2.RANSAC, 5.0)

                # 投影画像を生成
                warp = None
                if H is not None:
                    proj_poses = cv2.perspectiveTransform(np.reshape(poses, [-1, 1, 2]), H)
                    print(np.reshape(proj_poses, [-1, 2]))
                    if stop_request:
                        stop_enable = True
                        stop_request = False

            # デバック表示
            cv2.putText(debug, "s: save homography",
                        (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 1,
                        (0, 255, 0), 2, cv2.LINE_AA)
            cv2.putText(debug, "q: quit",
                        (10, 60), cv2.FONT_HERSHEY_SIMPLEX, 1,
                        (0, 255, 0), 2, cv2.LINE_AA)
            cv2.putText(debug, "t: stop capture",
                        (10, 90), cv2.FONT_HERSHEY_SIMPLEX, 1,
                        (0, 255, 0), 2, cv2.LINE_AA)
            cv2.imshow("GoPro Webcam", cv2.resize(debug, None, fx=0.4, fy=0.4))        
            key = cv2.waitKey(30)        
            if key == ord("q"):
                break
            elif key == ord("s"):
                if H is not None:
                    logging.info("saving homography.txt ...")
                    np.savetxt('homography.txt', H, delimiter=',')
                    logging.info("saved homography.txt !!")
            elif key == ord("t"):
                if stop_request or stop_enable:
                    stop_request = False
                    stop_enable = False
                else:
                    stop_request = True
                    stop_enable = False
                    
    except KeyboardInterrupt:
        pass

    # GoPro Webcamデバイスを閉じる
    gopro.release()
    # デバックウインドウを閉じる
    cv2.destroyAllWindows()
