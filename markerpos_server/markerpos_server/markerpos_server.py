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
        description="Maker position server software")
    
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

    dictionary = aruco.getPredefinedDictionary(aruco.DICT_4X4_50)
    parameters = aruco.DetectorParameters()

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

            # 特定IDのマーカ位置を抽出        
            if ids is not None and corners is not None:
                for id, corner in zip(ids, corners):
                    # マーカの中心と向きを計算する
                    corner = np.reshape(corner, [-1, 2])
                    center = np.mean(corner, axis=0)
                    front = np.mean(corner[:2], axis=0)

                    # 投影変換
                    proj_center = cv2.perspectiveTransform(np.reshape(center, [-1, 1, 2]), H)
                    proj_center = np.reshape(proj_center, [-1, 2])
                    proj_front = cv2.perspectiveTransform(np.reshape(front, [-1, 1, 2]), H)
                    proj_front = proj_front - proj_center
                    proj_front = proj_front / np.linalg.norm(proj_front)
                    proj_front = np.array([proj_front[0,0,0], 0.0, proj_front[0,0,1]])

                    # 向きをクォータニオンに変換
                    quat = quaternion_from_vectors(proj_front, np.array([0.0, 1.0, 0.0]))

                    # データを配信
                    pose = Pose()
                    pose.position.x = float(proj_center[0, 0])
                    pose.position.y = 0.0
                    pose.position.z = float(proj_center[0, 1])
                    pose.orientation.x = float(quat.x)
                    pose.orientation.y = float(quat.y)
                    pose.orientation.z = float(quat.z)
                    pose.orientation.w = float(quat.w)
                    if id == 0:
                        print(f"0: {pose}")
                        pub1.publish(pose)
                    elif id == 1:
                        print(f"1: {pose}")
                        pub2.publish(pose)
    except KeyboardInterrupt:
        pass


    # パブリッシャを閉じる
    pub1.close()
    pub2.close()

    # GoPro Webcamデバイスを閉じる
    gopro.release()
