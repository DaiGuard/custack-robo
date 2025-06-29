import sys
import logging
import argparse
from .gopro_webcam import GoProCapture
from .zeromq import PoseStampedPublisher, Pose

import cv2
import cv2.aruco as aruco
import numpy as np
import quaternion


logging.basicConfig(
    level=logging.INFO, stream=sys.stdout,
    format="%(levelname)s:[markerpos_server] %(message)s")


def quaternion_from_vectors(forward_vector, up_vector_hint) -> quaternion.quaternion:
    forward_vector = forward_vector / np.linalg.norm(forward_vector)
    up_vector_hint = up_vector_hint / np.linalg.norm(up_vector_hint)

    right_vector = np.cross(forward_vector, up_vector_hint)
    if np.linalg.norm(right_vector) < 1e-5:
        raise ValueError("Forward and up_vector_hint are nearly parallel.")
    right_vector = right_vector / np.linalg.norm(right_vector)

    up_vector = np.cross(right_vector, forward_vector)
    up_vector = up_vector / np.linalg.norm(up_vector)

    rotation_matrix = np.array([
        forward_vector,
        up_vector,
        right_vector
    ]).T

    return quaternion.from_rotation_matrix(rotation_matrix)

def main():

    # 引数を設定
    parser = argparse.ArgumentParser(
        description="GoPro Webcam capture software")
    
    # parser.add_argument("-d", "--debug", action="store_true",
    #                     help="debug mode")
    parser.add_argument("-gi", "--gopro_ip", type=str, default="172.24.169.51",
                        help="gopro IP address")
    parser.add_argument("-gd", "--gopro_device", type=int, default=42,
                        help="gopro device id")
    parser.add_argument("-c", "--calib", type=str, default="homography.txt",
                        help="homography calibration file")
    parser.add_argument("-zi", "--zmqip", type=str, default="localhost",
                        help="zeromq server address")
    parser.add_argument("-zp", "--zmqport", type=int, default=5556,
                        help="zeromq server port number")
    parser.add_argument("-t1", "--topic1", type=str, default="p1_pose",
                        help="zeromq topic name")
    parser.add_argument("-t2", "--topic2", type=str, default="p2_pose",
                        help="zeromq topic name")    
    args = parser.parse_args()

    # キャリブレーションファイルの読み込み
    H = np.loadtxt(args.calib, delimiter=',')
    if H is None:
        logging.error("cannot load homography calibration file")
        return

    # GoPro Webcam キャプチャーデバイスの確保
    gopro = GoProCapture(args.gopro_ip, args.gopro_device)

    # GoPro Webcamデバイスを開く
    if not gopro.open("1080", "linear"):
        logging.error("cannot open GoPro Webcam")
        return
    
    # ZeroMQパプリッシャの準備
    pub1 = PoseStampedPublisher(
        args.zmqip, args.zmqport, args.topic1)
    pub2 = PoseStampedPublisher(
        args.zmqip, args.zmqport, args.topic2)

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

        # 特定IDのマーカ位置を抽出        
        if ids is not None and corners is not None:
            for id, corner in zip(ids, corners):
                corner = np.reshape(corner, [-1, 2])
                center = np.mean(corner, axis=0)
                front = np.mean(corner[:2], axis=0) - center
                front = front / np.linalg.norm(front)
                front = np.array([front[0], 0.0, front[1]])
                quat = quaternion_from_vectors(front, np.array([0.0, 1.0, 0.0]))

                pose = Pose()
                pose.position.x = float(center[0])
                pose.position.y = 0.0
                pose.position.z = float(center[1])
                pose.orientation.x = float(quat.x)
                pose.orientation.y = float(quat.y)
                pose.orientation.z = float(quat.z)
                pose.orientation.w = float(quat.w)

                # # デバック描画
                # debug = cv2.circle(debug, (int(center[0]), int(center[1])),
                #            10, (0, 0, 255), -1)
                # debug = cv2.line(debug, (int(center[0]), int(center[1])),
                #            (int(front[0]), int(front[1])),
                #            (0, 255, 0), 2)

                if id == 0:
                    pub1.publish(pose)
                elif id == 1:
                    pub2.publish(pose)
        

        # # ワープ投影
        # warp =cv2.warpPerspective(frame, H, (frame.shape[1], frame.shape[0]))
        # if p1 is not None and p2 is not None:
        #     src = np.array([p1[0], p1[1], p2[0], p2[1]]).reshape(-1, 1, 2)
        #     dst = cv2.perspectiveTransform(src, H).reshape(-1, 2)

        #     warp = cv2.circle(warp, (int(dst[0][0]), int(dst[0][1])),
        #                10, (0, 0, 255), -1)
        #     warp = cv2.circle(warp, (int(dst[2][0]), int(dst[2][1])),
        #                10, (0, 0, 255), -1)
        #     warp = cv2.line(warp, (int(dst[0][0]), int(dst[0][1])),
        #                (int(dst[1][0]), int(dst[1][1])),
        #                (0, 255, 0), 2)
        #     warp = cv2.line(warp, (int(dst[2][0]), int(dst[2][1])),
        #                (int(dst[3][0]), int(dst[3][1])),
        #                (0, 255, 0), 2)

        # debug = cv2.hconcat([debug, warp])

        # デバック表示
        cv2.imshow("GoPro Webcam", cv2.resize(debug, None, fx=0.4, fy=0.4))
        key = cv2.waitKey(30)
        if key == ord("q"):
            break

    # GoPro Webcamデバイスを閉じる
    gopro.release()
    # デバックウインドウを閉じる
    cv2.destroyAllWindows()
