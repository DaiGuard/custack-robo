"""WEBカメラコントローラ."""
import cv2


def main():
    device_id = 0

    cap = cv2.VideoCapture(device_id)
    if not cap.isOpened():
        print(f"cannot open camera device: {device_id}")
        return
    # 解像度決定
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1920)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 1080)
    # 自動フォーカス、自動露出の無効化
    cap.set(cv2.CAP_PROP_AUTOFOCUS, 0)
    cap.set(cv2.CAP_PROP_AUTO_EXPOSURE, 0)

    cv2.namedWindow("camera", cv2.WINDOW_AUTOSIZE)

    while True:
        focus = cap.get(cv2.CAP_PROP_FOCUS)
        expsure = cap.get(cv2.CAP_PROP_EXPOSURE)
        gain = cap.get(cv2.CAP_PROP_GAIN)
        brightness = cap.get(cv2.CAP_PROP_BRIGHTNESS)
        contrast = cap.get(cv2.CAP_PROP_CONTRAST)

        ret, frame = cap.read()
        if not ret:
            print("cannot capture image")
            break

        cv2.imshow("camera", frame)
        if cv2.waitKey(1) & 0xFF == ord("q"):
            break

    cap.release()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()