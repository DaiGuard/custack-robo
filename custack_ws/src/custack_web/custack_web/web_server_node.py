from rclpy.node import Node
from rclpy.qos import QoSProfile, ReliabilityPolicy, DurabilityPolicy
from sensor_msgs.msg import Image
import threading
import cv2
from cv_bridge import CvBridge
import copy
import numpy as np
import yaml
import os


class WebServerNode(Node):
    def __init__(self):
        super().__init__('web_server_node')
        # 画像トピックの購読
        image_qos = QoSProfile(
            depth=3,
            reliability=ReliabilityPolicy.BEST_EFFORT, # これが必須
            durability=DurabilityPolicy.VOLATILE
        )
        self.image_sub_ = self.create_subscription(
            Image,
            '/camera/image_raw',
            self.image_callback,
            image_qos)

        # 検出パラメータの設定
        self.calib_params_ = cv2.SimpleBlobDetector_Params()
        self.calib_params_.minThreshold = 10
        self.calib_params_.maxThreshold = 255
        self.calib_params_.filterByCircularity = True
        self.calib_params_.minCircularity = 0.3
        self.calib_params_.filterByArea = True
        self.calib_params_.minArea = 50
        self.calib_params_.maxArea = 15000
        self.calib_detector_ = cv2.SimpleBlobDetector_create(self.calib_params_)

        # マーカ検出パラメータの設定
        self.aruco_dictionary_ = cv2.aruco.getPredefinedDictionary(cv2.aruco.DICT_4X4_50)
        self.aruco_params_ = cv2.aruco.DetectorParameters()
        self.aruco_params_.cornerRefinementMethod = cv2.aruco.CORNER_REFINE_SUBPIX
        self.aruco_params_.adaptiveThreshWinSizeMin = 3
        self.aruco_params_.adaptiveThreshWinSizeMax = 31
        self.aruco_params_.adaptiveThreshWinSizeStep = 5
        self.aruco_params_.polygonalApproxAccuracyRate = 0.08
        self.aruco_params_.minMarkerPerimeterRate = 0.02
        self.aruco_params_.perspectiveRemovePixelPerCell = 8
        self.aruco_detector_ = cv2.aruco.ArucoDetector(self.aruco_dictionary_, self.aruco_params_)

        # キャリブレーションデータ関係
        self.calib_save_path_ = os.path.join(os.getcwd(), "camera_calib.yaml")
        self.camera_matrix_ = None
        self.dist_coeffs_ = None
        self.undistort_enabled_ = False

        # ホモグラフィデータ関係
        self.homography_save_path_ = os.path.join(os.getcwd(), "homography.yaml")
        self.homography_matrix_ = None
        self.homography_enabled_ = False

        self.latest_msg_ = None
        self.cv_bridge_ = CvBridge()
        self.lock_ = threading.Lock()
        self.calib_images_ = [None] * 20
        self.homography_image_ = None
        self.get_logger().info('🖥️ Webサーバが起動しました')

    def image_callback(self, msg):
        with self.lock_:
            self.latest_msg_ = msg

    def get_latest_image(self):
        try:
            msg = None
            with self.lock_:
                msg = self.latest_msg_

            # 画像取得に失敗した場合
            if msg is None:
                return None

            img = self.cv_bridge_.imgmsg_to_cv2(msg, desired_encoding='mono8')

            # ひずみ補正が有効であれば補正する
            if self.undistort_enabled_ and self.camera_matrix_ is not None and self.dist_coeffs_ is not None:
                img = cv2.undistort(img, self.camera_matrix_, self.dist_coeffs_)
            
            # ホモグラフィが有効であれば変換する
            if self.homography_enabled_ and self.homography_matrix_ is not None:
                h, w = img.shape[:2]
                img = cv2.warpPerspective(img, self.homography_matrix_, (w, h))

            _, buffer = cv2.imencode('.jpg', img)

            return buffer.tobytes()
        except Exception as e:
            self.get_logger().error(f'❌ 画像取得に失敗: {e}')

        return None

    def get_calib_image(self, id: int, change: bool = True):
        try:
            msg = None
            with self.lock_:
                msg = self.latest_msg_

            if msg is None:
                return None

            # 画像の取得
            img = None
            if change:
                img = self.cv_bridge_.imgmsg_to_cv2(msg, desired_encoding='mono8')
                self.calib_images_[id] = copy.deepcopy(img)
            else:
                img = self.calib_images_[id]

            # 画像を反転
            inverted = cv2.bitwise_not(img)

            # ボードを検出
            ret, centers = cv2.findCirclesGrid(
                inverted, (4, 5),
                flags=cv2.CALIB_CB_ASYMMETRIC_GRID,
                blobDetector=self.calib_detector_
            )

            # ボードを検出したら描画する
            img = cv2.cvtColor(img, cv2.COLOR_GRAY2BGR)
            if ret:                
                img = cv2.drawChessboardCorners(img, (4, 5), centers, ret)
                img = cv2.putText(img, "OK", (10, 180), cv2.FONT_HERSHEY_SIMPLEX, 8, (0, 255, 0), 12, cv2.LINE_AA)
            else:
                img = cv2.putText(img, "NG", (10, 180), cv2.FONT_HERSHEY_SIMPLEX, 8, (0, 0, 255), 12, cv2.LINE_AA)

            # JPEGに変換
            _, buffer = cv2.imencode('.jpg', img)

            return buffer.tobytes()
        except Exception as e:
            self.get_logger().error(f'❌ 画像取得に失敗: {e}')

        return None

    def calc_board_corner_points(self, board_size, square_size):
        width, height = board_size
        corners = []
        for i in range(height):
            for j in range(width):
                x = float((2 * j + i % 2) * square_size)
                y = float(i * square_size)
                corners.append([x, y, 0.0])

        return np.array(corners, dtype=np.float32)

    def save_calib(self):
        missing_indices = [i for i, x in enumerate(self.calib_images_) if x is None]

        if missing_indices:
            missing_str = ", ".join(map(str, missing_indices))
            return {
                "status": False, 
                "message": f"保存されていない番号があります: ID({missing_str})"
            }            
        else:
            objPoints = []
            imgPoints = []

            h, w = self.calib_images_[0].shape[:2]
            imageSize = (w, h)

            objp = self.calc_board_corner_points((4, 5), 50.0)

            for i in range(len(self.calib_images_)):
                # 画像を反転
                img = self.calib_images_[i]
                inverted = cv2.bitwise_not(img)

                # ボードを検出
                ret, centers = cv2.findCirclesGrid(
                    inverted, (4, 5),
                    flags=cv2.CALIB_CB_ASYMMETRIC_GRID,
                    blobDetector=self.calib_detector_
                )

                if ret:
                    objPoints.append(objp.astype(np.float32))
                    imgPoints.append(centers.astype(np.float32))
                else:
                    return {"status": False, "message": f"ID({i})のボード検出に失敗しました。撮り直してください。"}

            try:
                rms, mtx, dist, rvecs, tvecs = cv2.calibrateCamera(
                    objPoints, imgPoints, imageSize, None, None
                )

                # calib_data = {
                #     "rms": float(rms),
                #     "camera_matrix": mtx.tolist(), # ndarrayをリストに変換
                #     "distortion_coefficients": dist.tolist(),
                #     "image_width": w,
                #     "image_height": h
                # }

                # with open(self.calib_save_path_, 'w') as f:
                #     yaml.dump(calib_data, f, default_flow_style=False)
                fs = cv2.FileStorage(self.calib_save_path_, cv2.FileStorage_WRITE)
                fs.write("rms", float(rms))
                fs.write("camera_matrix", mtx)
                fs.write("distortion_coefficients", dist)
                fs.write("image_width", w)
                fs.write("image_height", h)
                fs.release()

                return {"status": True, "message": f"成功！ RMS: {rms:.4f}"}
            except Exception as e:
                return {"status": False, "message": f"計算エラー: {str(e)}"}

    def get_homography_image(self):
        try:
            msg = None
            with self.lock_:
                msg = self.latest_msg_

            if msg is None:
                return None

            img = self.cv_bridge_.imgmsg_to_cv2(msg, desired_encoding='mono8')            

            # ひずみ補正が有効であれば補正する
            if self.undistort_enabled_ and self.camera_matrix_ is not None and self.dist_coeffs_ is not None:
                img = cv2.undistort(img, self.camera_matrix_, self.dist_coeffs_)

            # Arucoマーカ検出器の準備
            detector = self.aruco_detector_

            # マーカー検出
            corners, ids, rejected = detector.detectMarkers(img)
            
            # マーカー描画
            viz_img = cv2.cvtColor(img, cv2.COLOR_GRAY2BGR)                    
            cv2.aruco.drawDetectedMarkers(viz_img, corners, ids)

            if all(id in ids for id in [11, 12, 13, 14]):
                viz_img = cv2.putText(viz_img, "OK", (10, 180), cv2.FONT_HERSHEY_SIMPLEX, 8, (0, 255, 0), 12, cv2.LINE_AA)
                self.homography_image_ = copy.deepcopy(img)
            else:
                viz_img = cv2.putText(viz_img, "NG", (10, 180), cv2.FONT_HERSHEY_SIMPLEX, 8, (0, 0, 255), 12, cv2.LINE_AA)

            # JPEGに変換
            _, buffer = cv2.imencode('.jpg', viz_img)

            return buffer.tobytes()
        except Exception as e:
            self.get_logger().error(f'❌ 画像取得に失敗: {e}')

        return None

    def save_homography(self):
        if self.homography_image_ is None:
            return {"status": False, "message": "ホモグラフィー用の画像がありません"}

        # Arucoマーカ検出器の準備
        detector = self.aruco_detector_

        # マーカー検出
        corners, ids, rejected = detector.detectMarkers(self.homography_image_)

        if all(id in ids for id in [11, 12, 13, 14]):
            # 対応するコーナーを抽出
            img_points = []
            obj_points = []
            scl_points = []

            obj_points.append([   0.0,    0.0]) # ID11
            obj_points.append([1920.0,    0.0]) # ID12
            obj_points.append([   0.0, 1080.0]) # ID13
            obj_points.append([1920.0, 1080.0]) # ID14

            scl_points.append([-1.0, 1.0]) # ID11
            scl_points.append([ 1.0, 1.0]) # ID12
            scl_points.append([-1.0,-1.0]) # ID13
            scl_points.append([ 1.0,-1.0]) # ID14

            for marker_id in [11, 12, 13, 14]:
                idx = np.where(ids == marker_id)[0][0]
                img_points.append(corners[idx][0][0])  # コーナー座標

            img_points = np.array(img_points, dtype=np.float32)
            obj_points = np.array(obj_points, dtype=np.float32)
            scl_points = np.array(scl_points, dtype=np.float32)

            print(img_points)

            H, _ = cv2.findHomography(img_points, obj_points)
            sH, _ = cv2.findHomography(img_points, scl_points)

            # homography_data = {
            #     "homography_matrix": H.tolist()
            # }

            # with open(self.homography_save_path_, 'w') as f:
            #     yaml.dump(homography_data, f, default_flow_style=False)
            fs = cv2.FileStorage(self.homography_save_path_, cv2.FileStorage_WRITE)
            fs.write("homography_matrix", H)
            fs.write("scaled_homography_matrix", sH)
            fs.release()

            return {"status": True, "message": "ホモグラフィ保存成功"}
        else:
            return {"status": False, "message": "必要なマーカーが検出できません"}

    def set_undistort(self, enable: bool):
        try:
            if os.path.exists(self.calib_save_path_):
                if enable:
                    # with open(self.calib_save_path_, 'r') as f:
                    #     calib_data = yaml.safe_load(f)
                    #     self.camera_matrix_ = np.array(calib_data["camera_matrix"])
                    #     self.dist_coeffs_ = np.array(calib_data["distortion_coefficients"])
                    #     self.undistort_enabled_ = True

                    fs = cv2.FileStorage(self.calib_save_path_, cv2.FileStorage_READ)
                    # 各ノードを読み込み、NumPy配列として取得
                    mtx = fs.getNode("camera_matrix").mat()
                    dist = fs.getNode("distortion_coefficients").mat()
                    fs.release()    
                    if mtx is not None and dist is not None:
                        self.camera_matrix_ = mtx.astype(np.float64)
                        self.dist_coeffs_ = dist.reshape(1, -1).astype(np.float64)
                        self.undistort_enabled_ = True                                    
                else:
                    self.camera_matrix_ = None
                    self.dist_coeffs_ = None
                    self.undistort_enabled_ = False
        except Exception as e:
            self.camera_matrix_ = None
            self.dist_coeffs_ = None
            self.undistort_enabled_ = False
            self.get_logger().error(f'❌ カメラキャリブレーションデータの読み込みに失敗: {e}')

    def set_homography(self, enable: bool):
        try:
            if os.path.exists(self.homography_save_path_):
                if enable:
                    # with open(self.homography_save_path_, 'r') as f:
                    #     homography_data = yaml.safe_load(f)
                    #     self.homography_matrix_ = np.array(homography_data["homography_matrix"])
                    #     self.homography_enabled_ = True
                    
                    fs = cv2.FileStorage(self.homography_save_path_, cv2.FileStorage_READ)
                    H = fs.getNode("homography_matrix").mat()
                    sH = fs.getNode("scaled_homography_matrix").mat()
                    fs.release()
                    if H is not None and sH is not None:
                        self.homography_matrix_ = H.astype(np.float64)
                        self.scaled_homography_matrix_ = sH.astype(np.float64)
                        self.homography_enabled_ = True
                else:
                    self.homography_matrix_ = None
                    self.homography_enabled_ = False
        except Exception as e:
            self.homography_matrix_ = None
            self.homography_enabled_ = False
            self.get_logger().error(f'❌ ホモグラフィデータの読み込みに失敗: {e}')