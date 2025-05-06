"""プロジェクタキャリブレーション."""
import cv2
import numpy as np
import json
import uuid
import datetime
import pathlib
import os
from . import shm
from . import image_proc as imp


def main(shared_data: shm.SharedMemData) -> None:
    """メイン関数."""

    # カメラデバイスのオープン
    cap = cv2.VideoCapture(0, cv2.CAP_V4L2)
    if not cap.isOpened():
        print("[E]: cannot open camera device")
        return

    cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc('Y', 'U', 'Y', 'V'))
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1920)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 1080)
    
    # 画像保存フォルダの作成
    dt_now = datetime.datetime.now()
    save_prefix = dt_now.strftime("%Y%m%d-%H%M%S")
    save_path = os.path.join(".", "images", save_prefix)
    savefile_count = 0
    pathlib.Path(save_path).mkdir(parents=True, exist_ok=True)

    # カメラパラメータの設定
    with open("calib_config.json", "r", encoding="utf-8") as f:
        calib_config = json.load(f)
        cap.set(cv2.CAP_PROP_AUTOFOCUS, calib_config["autofocus"])
        cap.set(cv2.CAP_PROP_AUTO_EXPOSURE, calib_config["autoexposure"])
        cap.set(cv2.CAP_PROP_BRIGHTNESS, calib_config["brightness"])
        cap.set(cv2.CAP_PROP_FOCUS, calib_config["focus"])
        cap.set(cv2.CAP_PROP_EXPOSURE, calib_config["exposure"])
        cap.set(cv2.CAP_PROP_GAIN, calib_config["gain"])
        cap.set(cv2.CAP_PROP_CONTRAST, calib_config["contrast"])

    # ウインドウの作成
    cv2.namedWindow("projectcalib", cv2.WINDOW_AUTOSIZE)

    while cv2.getWindowProperty("projectcalib", cv2.WND_PROP_AUTOSIZE) > 0:
        # 画像のキャプチャ
        ret, frame = cap.read()
        if not ret:
            print("[E]: cannot read image capture")
            break
        # HSV変換
        hsv_image = cv2.cvtColor(frame, cv2.COLOR_BGR2HSV)

        # 色域取得
        board_range = (
            shared_data.color_range1[0],
            shared_data.color_range1[2],
            shared_data.color_range1[4],
            shared_data.color_range1[1],
            shared_data.color_range1[3],
            shared_data.color_range1[5]
        )
        project_range = (
            shared_data.color_range2[0],
            shared_data.color_range2[2],
            shared_data.color_range2[4],
            shared_data.color_range2[1],
            shared_data.color_range2[3],
            shared_data.color_range2[5]
        )

        board_image = imp.getColorRangeImage(
            hsv_image,
            board_range[0:3],
            board_range[3:6]
        )
        project_image = imp.getColorRangeImage(
            hsv_image, 
            project_range[0:3],
            project_range[3:6]
        )

        # ボードグリッドの検出
        board_grid = (6, 4)
        board_pitch = 0.056
        board_ret, board_centers = cv2.findCirclesGrid(
            board_image, board_grid, flags=cv2.CALIB_CB_SYMMETRIC_GRID)

        # プロジェクタ投光グリッドの検出
        project_grid = (shared_data.grid_size[0], shared_data.grid_size[1])
        project_ret, project_centers = cv2.findCirclesGrid(
            project_image, project_grid, flags=cv2.CALIB_CB_SYMMETRIC_GRID)

        # 画像保存
        if shared_data.capture_trigger:
            if board_ret and project_ret:
                shared_data.capture_trigger = False

                savefile_count += 1
                save_file = os.path.join(save_path, f"{save_prefix}-{savefile_count:04}")
                # save_file = os.path.join(save_path, str(uuid.uuid4()))
                cv2.imwrite(save_file + ".bmp", frame)

                json_data = json.dumps({
                    "board": {
                        "grid_size": [board_grid[0], board_grid[1]],
                        "grid_pitch": board_pitch,
                        "color_range": [
                            shared_data.color_range1[0],
                            shared_data.color_range1[1],
                            shared_data.color_range1[2],
                            shared_data.color_range1[3],
                            shared_data.color_range1[4],
                            shared_data.color_range1[5],
                        ],
                    },
                    "project": {
                        "winsize": [
                            shared_data.winsize[0],
                            shared_data.winsize[1]],
                        "grid_size": [
                            shared_data.grid_size[0],
                            shared_data.grid_size[1]],
                        "grid_pitch": shared_data.grid_pitch,
                        "board_pose": [
                            shared_data.board_pose[0],
                            shared_data.board_pose[1],
                            shared_data.board_pose[2]],
                        "color_range": [
                            shared_data.color_range2[0],
                            shared_data.color_range2[1],
                            shared_data.color_range2[2],
                            shared_data.color_range2[3],
                            shared_data.color_range2[4],
                            shared_data.color_range2[5],
                        ],
                    },
                }, indent=4)
                
                with open(save_file + ".json", "w", encoding="utf-8") as f:
                    f.write(json_data)

        # 結果表示
        board_image = cv2.cvtColor(board_image, cv2.COLOR_GRAY2BGR)
        if board_ret:
            cv2.drawChessboardCorners(
                board_image, board_grid, board_centers, board_ret)
        project_image = cv2.cvtColor(project_image, cv2.COLOR_GRAY2BGR)
        if project_ret:
            cv2.drawChessboardCorners(
                project_image, project_grid, project_centers, project_ret)

        debug_image = cv2.hconcat([
            frame,
            cv2.resize(
                cv2.vconcat([board_image, project_image]),
                (960, 1080))
            ])
        debug_image = cv2.putText(
            debug_image, f"{savefile_count:04}", (10, 30),
            cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
        if shared_data.capture_trigger:
            debug_image = cv2.putText(
                debug_image, "Capturing...", (100, 100),
                cv2.FONT_HERSHEY_SIMPLEX, 3, (0, 0, 255), 2)
        # debug_image = cv2.resize(debug_image, (1920, 720))
        debug_image = cv2.resize(debug_image, (1728, 648))
        cv2.imshow("projectcalib", debug_image)
        key = cv2.waitKey(30)
        if key == ord("q"):
            break
        if shared_data.app_sync == 0:
            break

        # アプリケーション同期
        shared_data.app_sync += 1

    # アプリケーション終了処理
    shared_data.app_sync = 0

    cap.release()
    cv2.destroyAllWindows()