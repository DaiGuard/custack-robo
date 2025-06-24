"""WEBカメラコントローラ."""

import cv2
import numpy as np
import json


zoom_on = False
zoom_move_on = True
mouse_pos = (0, 0)
zoom_mouse_pos = (0, 0)


def main():
    global zoom_on, zoom_move_on, mouse_pos, zoom_mouse_pos

    device_id = 0
    launch = "v4l2src device=/dev/video0 \
        ! video/x-raw,width=640,height=480 \
        ! videoconvert \
        ! video/x-raw,format=BGR \
        ! appsink"
    cap = cv2.VideoCapture(device_id, cv2.CAP_V4L2)
    # cap = cv2.VideoCapture(launch, cv2.CAP_GSTREAMER)
    if not cap.isOpened():
        print(f"cannot open camera device: {device_id}")
        return
    # 解像度決定
    cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc('M', 'J', 'P', 'G'))
    # cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc('Y', 'U', 'Y', 'V'))
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1920)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 1080)

    cv2.namedWindow("camera", cv2.WINDOW_AUTOSIZE)

    autofocus = cap.get(cv2.CAP_PROP_AUTOFOCUS)
    autoexposure = cap.get(cv2.CAP_PROP_AUTO_EXPOSURE)
    brightness = cap.get(cv2.CAP_PROP_BRIGHTNESS)
    focus = cap.get(cv2.CAP_PROP_FOCUS)
    exposure = cap.get(cv2.CAP_PROP_EXPOSURE)
    gain = cap.get(cv2.CAP_PROP_GAIN)
    contrast = cap.get(cv2.CAP_PROP_CONTRAST)

    def change_autofocus(value):
        if value == 0 or value == 1:
            cap.set(cv2.CAP_PROP_AUTOFOCUS, value)

    def change_autoexposure(value):
        if value == 1 or value == 3:
            cap.set(cv2.CAP_PROP_AUTO_EXPOSURE, value)

    def change_brightness(value):
        if value >= 0 and value <= 255:
            cap.set(cv2.CAP_PROP_BRIGHTNESS, value)

    def change_focus(value):
        if value >= 0 and value <= 250 and value%5==0:
            cap.set(cv2.CAP_PROP_FOCUS, value)

    def change_expsure(value):
        if value >= 3 and value <= 2047:
            cap.set(cv2.CAP_PROP_EXPOSURE, value)

    def change_gain(value):
        if value >= 0 and value <= 255:
            cap.set(cv2.CAP_PROP_GAIN, value)

    def change_contrast(value):
        if value >= 0 and value <= 255:
            cap.set(cv2.CAP_PROP_CONTRAST, value)

    cv2.createTrackbar('autofocus','camera', 1, 1, change_autofocus)
    cv2.createTrackbar('autoexposure','camera', 3, 3, change_autoexposure)
    cv2.createTrackbar('brightness','camera', int(brightness) , 255, change_brightness)
    cv2.createTrackbar('focus','camera', int(focus) , 250, change_focus)
    cv2.createTrackbar('exposure','camera', int(exposure) , 2047, change_expsure)
    cv2.createTrackbar('gain','camera', int(gain) , 255, change_gain)
    cv2.createTrackbar('contrast','camera', int(contrast) , 255, change_contrast)

    zoom_move_on = True
    zoom_on = False
    saving = 0

    def zoom_in_out(event, x, y, flags, param):
        global zoom_mouse_pos, zoom_on, zoom_move_on, mouse_pos

        if event == cv2.EVENT_MOUSEMOVE:
            if zoom_move_on:
                zoom_mouse_pos = (x, y)
            mouse_pos = (x, y)
        if event == cv2.EVENT_LBUTTONDOWN:
            if zoom_on:
                zoom_move_on = not zoom_move_on

    cv2.setMouseCallback("camera", zoom_in_out)

    while cv2.getWindowProperty("camera", 0) is not None:
        # カメラパラメータ変更
        autofocus = cap.get(cv2.CAP_PROP_AUTOFOCUS)
        autoexposure = cap.get(cv2.CAP_PROP_AUTO_EXPOSURE)
        brightness = cap.get(cv2.CAP_PROP_BRIGHTNESS)
        focus = cap.get(cv2.CAP_PROP_FOCUS)
        exposure = cap.get(cv2.CAP_PROP_EXPOSURE)
        gain = cap.get(cv2.CAP_PROP_GAIN)
        contrast = cap.get(cv2.CAP_PROP_CONTRAST)

        print(f"focus: {focus}, exposure: {exposure}, gain: {gain}, brightness: {brightness}, contrast: {contrast}")

        ret, frame = cap.read()
        if not ret:
            print("cannot capture image")
            break

        # ズーム画像作成
        scaled_size = (960, 540)
        crop_base_size = 200
        h, w, _ = frame.shape
        scale = (scaled_size[0] / w, scaled_size[1] / h)
        crop_size = (int(crop_base_size * w / h), int(crop_base_size))
        origin_mouse_pos = (int(zoom_mouse_pos[0] / scale[0]), int(zoom_mouse_pos[1] / scale[1]))
        anchor = [0, 0]
        if origin_mouse_pos[0] > crop_size[0]:
            llx = origin_mouse_pos[0] - crop_size[0]
        else:
            llx = 0
            anchor[0] = crop_size[0] -origin_mouse_pos[0]

        if origin_mouse_pos[1] > crop_size[1]:
            lly = origin_mouse_pos[1] - crop_size[1]
        else:
            lly = 0
            anchor[1] = crop_size[1] - origin_mouse_pos[1]

        if origin_mouse_pos[0] < w - crop_size[0]:
            urx = origin_mouse_pos[0] + crop_size[0]
        else:
            urx = w

        if origin_mouse_pos[1] < h - crop_size[1]:
            ury = origin_mouse_pos[1] + crop_size[1]
        else:
            ury = h

        crop_image = frame[lly:ury, llx:urx]
        zoom_image = np.full((crop_size[1]*2, crop_size[0]*2, 3), (0, 0, 0), dtype=np.uint8)
        zoom_image[anchor[1]:anchor[1]+crop_image.shape[0], anchor[0]:anchor[0]+crop_image.shape[1]] = crop_image

        if not zoom_on:
            dst = cv2.resize(frame, scaled_size)
        else:
            dst = cv2.resize(zoom_image, scaled_size)
        dst_hsv = cv2.cvtColor(dst, cv2.COLOR_BGR2HSV)
        pick_hsv = dst_hsv[mouse_pos[1], mouse_pos[0]]
        cv2.putText(
            dst, "s: save, z: zoom, q: quit", (10, 30),
            cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2, cv2.LINE_AA)
        cv2.putText(
            dst, f"{pick_hsv}", (10, 60),
            cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2, cv2.LINE_AA)
        if saving > 0:
            cv2.putText(
                dst, "saving...", (10, 120),
                cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 0, 255), 2, cv2.LINE_AA)
            saving += 1
            if saving > 100:
                saving = 0
        cv2.imshow("camera", dst)
        key = cv2.waitKey(30)
        if key == ord("s"):
            saving = 1
            json_data = json.dumps({
                "autofocus": autofocus,
                "autoexposure": autoexposure,
                "brightness": brightness,
                "focus": focus,
                "exposure": exposure,
                "gain": gain,
                "contrast": contrast
            }, indent=4)
            with open("camera_config.json", "w", encoding="utf-8") as f:
                f.write(json_data)
            print("save camera config")
        elif key == ord("z"):
            zoom_on = not zoom_on
        elif key == ord("q"):
            break

    cap.release()
    cv2.destroyAllWindows()


if __name__ == "__main__":
    main()