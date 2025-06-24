import traceback
import cv2
import cv2.aruco as aruco
import json
import os
import gopro_webcam
import time

def main(args=None):

    # 初回は強制的に終了
    gopro_webcam.webcam_stop()
    ret, proc = gopro_webcam.webcam_start("1080")
    if not ret:
        exit()
    time.sleep(5)

    ret = gopro_webcam.webcam_setting("linear")
    if not ret:
        exit()
    time.sleep(5)

    try:
        cap = cv2.VideoCapture(f"/dev/video{gopro_webcam.GOPRO_VIDEO_ID}")
        if not cap.isOpened():
            raise RuntimeError("[E]: cannot open camera")
        
        # cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1920)
        # cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 1080)

        cv2.namedWindow("marker", cv2.WINDOW_AUTOSIZE)

        # if os.path.exists("camera_config.json"):
        #     with open("camera_config.json", "r") as f:
        #         config = json.load(f)

        #         cap.set(cv2.CAP_PROP_AUTOFOCUS, config["autofocus"])
        #         cap.set(cv2.CAP_PROP_AUTO_EXPOSURE, config["autoexposure"])
        #         cap.set(cv2.CAP_PROP_BRIGHTNESS, config["brightness"])
        #         cap.set(cv2.CAP_PROP_FOCUS, config["focus"])
        #         cap.set(cv2.CAP_PROP_EXPOSURE, config["exposure"])
        #         cap.set(cv2.CAP_PROP_GAIN, config["gain"])
        #         cap.set(cv2.CAP_PROP_CONTRAST, config["contrast"])

        # ArUcoマーカーの設定
        aruco_dict = aruco.getPredefinedDictionary(aruco.DICT_4X4_50)
        parameters = aruco.DetectorParameters()
        detector = aruco.ArucoDetector(aruco_dict, parameters)

        while cv2.getWindowProperty("marker", cv2.WND_PROP_VISIBLE) >= 1:
            ret, frame = cap.read()
            if not ret:
                break

            # ArUcoマーカーの検出
            corners, ids, rejectedImgPoints = detector.detectMarkers(frame)

            # 検出されたマーカーを描画
            image = frame.copy()
            if ids is not None:
                print(f"[I]: found marker {ids.tolist()}")
                image = aruco.drawDetectedMarkers(image, corners, ids)
            else:
                print("[I]: not found marker")

            image = cv2.resize(image, None, fx=0.5, fy=0.5)
            cv2.imshow("marker", image)
            key = cv2.waitKey(30)
            if key == ord("q"):
                break
    except KeyboardInterrupt:
        pass
    except Exception as e:
        print(e)
        traceback.print_exc()

    gopro_webcam.webcam_stop(proc)

    cap.release()
    cv2.destroyAllWindows()



if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
    except Exception:
        traceback.print_exc()