import traceback
import os
import argparse
from pathlib import Path
import dataclasses
import cv2
import numpy as np
import json
import math
import matplotlib.pyplot as plt
import mpl_toolkits.mplot3d.art3d as art3d


@dataclasses.dataclass
class CalibDataset:

    name: str = ""
    image: cv2.typing.MatLike = None

    board_upper_color: np.typing.NDArray = None
    board_lower_color: np.typing.NDArray = None

    project_win_size: tuple[int, int] = (0, 0)
    project_grid_size: tuple[int, int] = (0, 0)
    project_grid_pitch: int = 0
    project_board_pose: tuple[int, int, int] = (0, 0, 0)
    project_upper_color: np.typing.NDArray = None
    project_lower_color: np.typing.NDArray = None

def load_dataset(folder_path: str) -> list[CalibDataset]:

    dataset = []

    try:
        # 画像ファイルの拡張子リスト
        image_extensions = [
            ".jpg", ".jpeg", ".png",
            ".gif", ".bmp", ".tiff", ".webp"]
        
        # フォルダ内の読み込み
        folder = Path(folder_path)
        if folder.is_dir():
            for item in folder.iterdir():
                if item.is_file() and item.suffix.lower() in image_extensions:
                    # 画像ファイルの読み込み
                    data = CalibDataset()
                    data.name = item.name
                    data.image = cv2.imread(
                        os.path.join(folder_path, item.name))
                    # 設定ファイルの読み込み
                    if os.path.isfile(item.with_suffix(".json")):
                        with open(item.with_suffix(".json"), "r", encoding="utf-8") as f:
                            json_data = json.load(f)

                            board_data = json_data["board"]
                            project_data = json_data["project"]

                            # ボード側色域
                            data.board_lower_color = np.array((
                                board_data["color_range"][0],
                                board_data["color_range"][2],
                                board_data["color_range"][4]))
                            data.board_upper_color = np.array((
                                board_data["color_range"][1],
                                board_data["color_range"][3],
                                board_data["color_range"][5]))
                            
                            # プロジェクタ側色域
                            data.project_lower_color = np.array((
                                project_data["color_range"][0],
                                project_data["color_range"][2],
                                project_data["color_range"][4]))
                            data.project_upper_color = np.array((
                                project_data["color_range"][1],
                                project_data["color_range"][3],
                                project_data["color_range"][5]))
                            
                            # ボード情報入手
                            data.project_win_size = tuple(project_data["winsize"])
                            data.project_grid_size = tuple(project_data["grid_size"])
                            data.project_grid_pitch = project_data["grid_pitch"]
                            data.project_board_pose = tuple(project_data["board_pose"])

                            dataset.append(data)

    except FileNotFoundError:
        print(f"[E]: not found calibration image folder \"{folder_path}\"")

    return dataset

def get_color_range_image(
        src: cv2.typing.MatLike,
        lower: np.typing.NDArray,
        upper: np.typing.NDArray) -> cv2.typing.MatLike:
    """特定の色域領域を抽出する

    Args:
        src (cv2.typing.MatLike): 元画像(HSV)
        lower (np.typing.NDArray): 下限色
        upper (np.typing.NDArray): 上限色

    Returns:
        cv2.typing.MatLike: 特定色域抽出画像(Gray)
    """

    # 色域抽出
    dst = cv2.inRange(src, lower, upper)

    # ノイズ除去
    kernel = np.ones((5, 5), np.uint8)
    dst = cv2.morphologyEx(dst, cv2.MORPH_OPEN, kernel, iterations=1)
    dst = cv2.morphologyEx(dst, cv2.MORPH_CLOSE, kernel, iterations=1)

    # ビット反転
    dst = cv2.bitwise_not(dst)

    return dst.copy()

def get_circle_grid(
        src: cv2.typing.MatLike,
        lower_color: np.typing.NDArray, upper_color: np.typing.NDArray,
        grid_size: tuple[int, int]
        ) -> tuple[bool, np.typing.NDArray, cv2.typing.MatLike]:
    
    image = get_color_range_image(
        cv2.cvtColor(src, cv2.COLOR_BGR2HSV),
        lower_color, upper_color)
    
    ret, centers = cv2.findCirclesGrid(
        image, grid_size, flags=cv2.CALIB_CB_SYMMETRIC_GRID)
    
    tmp = src.copy()
    tmp = cv2.drawChessboardCorners(tmp, grid_size, centers, ret)

    return ret, centers, tmp
        
def create_project_grid(
        winsize: tuple[int, int], 
        grid_size: tuple[int, int], grid_pitch: int,
        board_pose: tuple[int, int, int]) -> tuple[np.typing.NDArray, cv2.typing.MatLike]:
    
    # 空画像を作成
    tmp = np.full((winsize[1], winsize[0], 3), (255, 255, 255), np.uint8)
    # 空グリッド点を作成
    grid_points = np.zeros((grid_size[0] * grid_size[1], 2), np.float32)

    for j in range(grid_size[1]):
        for i in range(grid_size[0]):
            index = i + j * grid_size[0]
            dx = i * grid_pitch
            dy = j * grid_pitch

            dbx = dx * math.cos(math.radians(board_pose[2])) \
                - dy * math.sin(math.radians(board_pose[2])) \
                + board_pose[0]
            dby = dx * math.sin(math.radians(board_pose[2])) \
                + dy * math.cos(math.radians(board_pose[2])) \
                + board_pose[1]
            
            grid_points[index][0:2] = np.array([dbx, dby])

            cv2.circle(
                tmp, (int(dbx), int(dby)),
                5, (0, 255, 0), -1)

    # 点群の次数を合わせる
    num = grid_size[0] * grid_size[1]
    grid_points = grid_points.reshape([num, 1, -1])

    return grid_points, tmp

def rotationMatrixToEulerAnglesZYX(R):
    """回転行列をZYXオイラー角 (度) に変換します。
    Args:
        R (numpy.ndarray): 3x3 の回転行列。
    Returns:
        tuple: ZYXオイラー角 (degrees)。(yaw, pitch, roll)
    """
    sy = math.sqrt(R[0, 0] * R[0, 0] + R[1, 0] * R[1, 0])

    singular = sy < 1e-6

    if not singular:
        x = math.atan2(R[2, 1], R[2, 2])
        y = math.atan2(-R[2, 0], sy)
        z = math.atan2(R[1, 0], R[0, 0])
    else:
        x = math.atan2(-R[1, 2], R[1, 1])
        y = math.atan2(-R[2, 0], sy)
        z = 0

    return np.array([
        math.degrees(z),
        math.degrees(y),
        math.degrees(x)]) # yaw (Z), pitch (Y), roll (X)

def main(args):
    # 引数の取得
    folder_path = args.folder_path
    grid_pitch = args.grid_pitch
    grid_size = (args.grid_width, args.grid_height)

    # ローカル変数
    used_name = []
    used_image = []
    board_image_size = None
    board_image_points = []     # キャリブボード画像上点
    board_object_points = []    # キャリブボード物体上点

    project_image_size = None    # プロジェクト画像サイズ
    project_image_points = []   # プロジェクトグリッド投影点
    project_perspective_points = [] # プロジェクトグリット画像上点
    project_object_points = []  # プロジェクトグリッド物体上点
    virtual_object_points = []  # 仮想物体上点

    # データセットの読み込み
    dataset = load_dataset(folder_path)

    # ボードオブジェクト点の作成
    objp = np.zeros((grid_size[0] * grid_size[1], 3), np.float32)
    objp[:, :2] = np.mgrid[0:grid_size[0], 0:grid_size[1]].T.reshape(-1, 2)
    objp *= grid_pitch

    for data in dataset:
        # ボードイメージ点の取得
        board_image_size = (data.image.shape[1], data.image.shape[0])
        board_ret, board_centers, board_tmp = get_circle_grid(
            data.image, data.board_lower_color, data.board_upper_color,
            grid_size)
        
        # プロジェクタイメージ点の取得
        project_ret, project_centers, project_tmp = get_circle_grid(
            data.image, data.project_lower_color, data.project_upper_color,
            data.project_grid_size)
        
        project_image_size = (data.project_win_size[0], data.project_win_size[1])
        grid_points, grid_image = create_project_grid(
            data.project_win_size, data.project_grid_size, data.project_grid_pitch,
            data.project_board_pose)
        
        # データの保存
        if board_ret and project_ret:
            used_name.append(data.name)
            used_image.append(data.image)
            board_image_points.append(board_centers)
            board_object_points.append(objp)
            project_image_points.append(grid_points)
            project_perspective_points.append(project_centers)

        # デバック出力
        tmp = cv2.hconcat([board_tmp, project_tmp])
        tmp = cv2.resize(tmp, (0, 0), fx=0.5, fy=0.5)
        cv2.putText(tmp, data.name, (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
        gird_tmp = cv2.resize(grid_image, (0, 0), fx=0.5, fy=0.5)
        if args.debug:
            cv2.imshow("image", tmp)
            cv2.imshow("debug", gird_tmp)
            cv2.waitKey(0)

    cv2.destroyAllWindows()

    # エラー処理
    if len(board_image_points) < 10:
        print(f"[E]: 十分なデータ数がありません {len(board_image_points)}.")
        return

    # カメラキャリブレーション実施
    ret, cam_mtx, cam_dist, cam_rvecs, cam_tvecs = cv2.calibrateCamera(
        board_object_points, board_image_points, board_image_size, None, None)
    if not ret:
        print(f"[E]: キャリブレーションに失敗しました.")
        return
    
    # キャリブレーション結果出力
    print(f"camera calib")
    print(f"mtx:=\n{cam_mtx}")
    print(f"dist:=\n {cam_dist}")

    # プロジェクタ側オブジェクト点推定
    for i, (cam_rvec, cam_tvec, proj_img_pts_on_cam, image, name) in enumerate(zip(
        cam_rvecs, cam_tvecs, project_perspective_points,
        used_image, used_name)):

        # 投影平面の計算
        rmat, _ = cv2.Rodrigues(cam_rvec) # R_world_to_camera

        # 復元プロジェクタグリッド3次元点
        objp = []
        vobjp = []
        R_inv = rmat.T
        tvec_flat = cam_tvec.flatten()
        for point in proj_img_pts_on_cam.reshape(-1, 2):
            # 視線作成 (カメラ座標系正規化画像座標 [x', y', 1])
            eye = np.array([
                (point[0] - cam_mtx[0, 2]) / cam_mtx[0, 0],
                (point[1] - cam_mtx[1, 2]) / cam_mtx[1, 1],
                1.0
            ])
            
            den_s = R_inv[2,:] @ eye
            if abs(den_s) < 1e-9: # 視線がワールドXY平面に平行
                objp.append(np.array([np.nan, np.nan, np.nan]))
                vobjp.append(np.array([np.nan, np.nan, np.nan]))
                continue

            num_s = R_inv[2,:] @ tvec_flat
            s_val = num_s / den_s

            if s_val < 0: # 交点がカメラの背後にある
                objp.append(np.array([np.nan, np.nan, np.nan]))
                vobjp.append(np.array([np.nan, np.nan, np.nan]))
                continue

            Xc = s_val * eye # カメラ座標系での3D点
            Xw = R_inv @ (Xc - tvec_flat)
            
            # Xw[2] は理論上0に近い。数値安定性のために明示的に0に設定。
            Xw_stable = Xw.copy()
            Xw_stable[2] = 0.0

            objp.append(Xc) # カメラ座標系での3D点 (プロット用)
            vobjp.append(Xw_stable) # ワールド座標系での3D点 (Z=0, キャリブレーション用)

        object_points = np.array(objp).reshape(-1, 3).astype(np.float32)
        virtual_points = np.array(vobjp).reshape(-1, 3).astype(np.float32)
        project_object_points.append(object_points) # カメラ座標系での3D点 (プロット用)
        virtual_object_points.append(virtual_points)

        # デバック出力
        tmp = image.copy()
        length = grid_pitch * 2.0
        axis_points_world = np.float32([
            [0.0, 0.0, 0.0], [length, 0.0, 0.0], [0.0, length, 0.0], [0.0, 0.0, length] 
        ])
        imgpts, _ = cv2.projectPoints(axis_points_world, cam_rvec, cam_tvec, cam_mtx, cam_dist)
        imgpts = np.int32(imgpts.reshape(-1, 2))
        for p in proj_img_pts_on_cam.reshape(-1, 2): # project_perspective_points
            cv2.circle(tmp, tuple(np.int32(p)), 5, (255, 127, 0), -1)
        valid_project_points, _ = cv2.projectPoints(virtual_points, cam_rvec, cam_tvec, cam_mtx, cam_dist)
        for p in valid_project_points.reshape(-1, 2):
            cv2.circle(tmp, tuple(np.int32(p)), 7, (0, 0, 255), 2)
        cv2.line(tmp, tuple(imgpts[0]), tuple(imgpts[1]), (0,0,255), 2) # X軸 (赤)
        cv2.line(tmp, tuple(imgpts[0]), tuple(imgpts[2]), (0,255,0), 2) # Y軸 (緑)
        cv2.line(tmp, tuple(imgpts[0]), tuple(imgpts[3]), (255,0,0), 2) # Z軸 (青)

        tmp = cv2.resize(tmp, (0, 0), fx=0.5, fy=0.5)

        if args.debug:
            cv2.imshow("debug", tmp)
            cv2.waitKey(0)
            
    cv2.destroyAllWindows()


    # 3D描画デバック
    fig = plt.figure(figsize=(10, 10), dpi=120)
    ax = fig.add_subplot(111, projection="3d")
    ax.set_box_aspect((1, 1, 1))
    ax.view_init(elev=-45, azim=-22, roll=-75) # 視点設定
    
    for i, (name, cam_rvec_i, cam_tvec_i, board_obj_pts_i, proj_obj_pts_cam_coords_i) in enumerate(zip(
        used_name, cam_rvecs, cam_tvecs,
        board_object_points, project_object_points)): # project_object_points はカメラ座標系

        # プロジェクタ投影点の3Dプロット (カメラ座標系)
        px = [float(p[0]) for p in proj_obj_pts_cam_coords_i if not np.isnan(p).any()]
        py = [float(p[1]) for p in proj_obj_pts_cam_coords_i if not np.isnan(p).any()]
        pz = [float(p[2]) for p in proj_obj_pts_cam_coords_i if not np.isnan(p).any()]
        pline_label = "projector (cam coords)" if i == 0 else None
        pline = art3d.Line3D(
            px, py, pz, color='c', linewidth=0.5,
            label=pline_label)

        # ボードオブジェクト点の3Dプロット (カメラ座標系に変換)
        rmat_i, _ = cv2.Rodrigues(cam_rvec_i)
        tvec_i_flat = cam_tvec_i.flatten()
        bx = []
        by = []
        bz = []
        for point_world in board_obj_pts_i: # board_object_points はワールド座標 (Z=0)
            p_cam = rmat_i @ point_world.T + tvec_i_flat
            bx.append(p_cam[0])
            by.append(p_cam[1])
            bz.append(p_cam[2])
        bline_label = "board (cam coords)" if i == 0 else None
        bline = art3d.Line3D(
            bx, by, bz, color='m', linewidth=0.5,
            label=bline_label)

        ax.text(bx[0], by[0], bz[0], name, fontsize=5)
        ax.add_line(pline)
        ax.add_line(bline)

    ax.set_xlim(-1.5, 1.5)
    ax.set_ylim(-1.5, 1.5)
    ax.set_zlim(0, 3.0)
    ax.set_xlabel("X (camera coord)")
    ax.set_ylabel("Y (camera coord)")
    ax.set_zlabel("Z (camera coord)")
    ax.legend()
    if args.debug:
        plt.show()

    # プロジェクタキャリブレーション実施
    # project_mtx = np.zeros((3, 3), np.float32)
    # project_mtx[0, 0] = project_image_size[0]
    # project_mtx[1, 1] = project_image_size[1]
    # project_mtx[0, 2] = project_image_size[0] / 2
    # project_mtx[1, 2] = project_image_size[1] / 2
    # project_mtx[2, 2] = 1.0
    fov_x = math.radians(87.6)
    fov_y = math.radians(46.2)

    project_mtx = np.zeros((3, 3), np.float32)        
    project_mtx[0, 0] = project_image_size[0] / 2.0 / math.tan(fov_x / 2)
    project_mtx[1, 1] = project_image_size[1] / 2.0 / math.tan(fov_y / 2)
    project_mtx[0, 2] = project_image_size[0] / 2
    project_mtx[1, 2] = project_image_size[1] / 2
    project_mtx[2, 2] = 1.0
    project_dist = np.zeros((1,8), np.float32)

    ret, proj_mtx, proj_dist, proj_rvecs, proj_tvecs = cv2.calibrateCamera(
        virtual_object_points, project_image_points, project_image_size,
        project_mtx, project_dist, flags=cv2.CALIB_USE_INTRINSIC_GUESS | cv2.CALIB_RATIONAL_MODEL | cv2.CALIB_THIN_PRISM_MODEL)

    # プロジェクタキャリブレーション結果出力    
    print(f"projector calib")
    print(f"mtx:=\n{proj_mtx}")
    print(f"dist:=\n {proj_dist}")

    # ステレオキャリブレーション実施
    # ret, cam_mtx, cam_dist, proj_mtx, proj_dist, R, T, E, F = cv2.stereoCalibrate(
    #     virtual_object_points,
    #     project_perspective_points, project_image_points,
    #     cam_mtx, cam_dist, proj_mtx, proj_dist,
    #     project_image_size,
    #     cv2.CALIB_FIX_INTRINSIC)
    ret, cam_mtx, cam_dist, proj_mtx, proj_dist, R, T, E, F = cv2.stereoCalibrate(
        virtual_object_points,
        project_perspective_points, project_image_points,
        cam_mtx, cam_dist, project_mtx, project_dist,
        project_image_size,
        flags=cv2.CALIB_USE_INTRINSIC_GUESS | cv2.CALIB_RATIONAL_MODEL | cv2.CALIB_THIN_PRISM_MODEL)
    print(f"rms = {ret}")

    # ステレオカメラキャリブレーション結果出力
    euler_angles = rotationMatrixToEulerAnglesZYX(R)
    print(f"camera calib")
    print(f"mtx:=\n{cam_mtx}")
    print(f"dist:=\n {cam_dist}")
    print(f"projector calib")
    print(f"mtx:=\n{proj_mtx}")
    print(f"dist:=\n {proj_dist}")
    print(f"R:=\n{R}")
    print(f"T:=\n{-R.T @ T.flatten()}")

    # ファイル保存
    with open("proj_calib_config.json", "w", encoding="utf-8") as f:
        json_data = json.dump({
            "camera_mtx": cam_mtx.tolist(),
            "camera_dist": cam_dist.tolist(),
            "projector_mtx": proj_mtx.tolist(),
            "projector_dist": proj_dist.tolist(),
            "cam2proj_euler_angles": euler_angles.tolist(),
            "cam2proj_trans": (-R.T @ T.flatten()).tolist()
        }, f, indent=4)

    # ステレオキャリブレーション結果 (R, T) を使ったデバッグ表示 (matplotlib)
    fig_stereo_pose = plt.figure(figsize=(8, 8))
    ax_stereo_pose = fig_stereo_pose.add_subplot(111, projection='3d')

    axis_length = 0.3  # 軸の長さを設定 (grid_pitchを基準に)

    # カメラ座標系の描画 (原点に配置)
    cam_origin = np.array([0, 0, 0])
    cam_x_axis_end = np.array([axis_length, 0, 0])
    cam_y_axis_end = np.array([0, axis_length, 0])
    cam_z_axis_end = np.array([0, 0, axis_length])

    ax_stereo_pose.plot([cam_origin[0], cam_x_axis_end[0]], [cam_origin[1], cam_x_axis_end[1]], [cam_origin[2], cam_x_axis_end[2]], color='r', label='Camera X')
    ax_stereo_pose.plot([cam_origin[0], cam_y_axis_end[0]], [cam_origin[1], cam_y_axis_end[1]], [cam_origin[2], cam_y_axis_end[2]], color='g', label='Camera Y')
    ax_stereo_pose.plot([cam_origin[0], cam_z_axis_end[0]], [cam_origin[1], cam_z_axis_end[1]], [cam_origin[2], cam_z_axis_end[2]], color='b', label='Camera Z')
    ax_stereo_pose.text(cam_x_axis_end[0], cam_x_axis_end[1], cam_x_axis_end[2], "Cam X")
    ax_stereo_pose.text(cam_y_axis_end[0], cam_y_axis_end[1], cam_y_axis_end[2], "Cam Y")
    ax_stereo_pose.text(cam_z_axis_end[0], cam_z_axis_end[1], cam_z_axis_end[2], "Cam Z")

    # OpenCV stereoCalibrate の R, T は、第1カメラ座標系から第2カメラ座標系への変換
    # x_cam2 = R @ x_cam1 + T
    # プロジェクタ (第2カメラ) の原点 O_proj = [0,0,0]_proj をカメラ座標系 (第1カメラ) で表すと:
    # O_proj_in_cam1_coords = -R.T @ T
    # プロジェクタのX軸ベクトル [L,0,0]_proj をカメラ座標系で表すと: R.T @ [L,0,0]_proj
    T_flat = T.flatten()
    proj_origin_in_cam = -R.T @ T_flat # プロジェクタ原点のカメラ座標系での位置

    proj_x_axis_end_in_cam = proj_origin_in_cam + R.T @ np.array([axis_length, 0, 0])
    proj_y_axis_end_in_cam = proj_origin_in_cam + R.T @ np.array([0, axis_length, 0])
    proj_z_axis_end_in_cam = proj_origin_in_cam + R.T @ np.array([0, 0, axis_length])

    ax_stereo_pose.plot([proj_origin_in_cam[0], proj_x_axis_end_in_cam[0]], [proj_origin_in_cam[1], proj_x_axis_end_in_cam[1]], [proj_origin_in_cam[2], proj_x_axis_end_in_cam[2]], color='#FF00FF', linestyle='--', label='Projector X') # マゼンタ
    ax_stereo_pose.plot([proj_origin_in_cam[0], proj_y_axis_end_in_cam[0]], [proj_origin_in_cam[1], proj_y_axis_end_in_cam[1]], [proj_origin_in_cam[2], proj_y_axis_end_in_cam[2]], color='#00FFFF', linestyle='--', label='Projector Y') # シアン
    ax_stereo_pose.plot([proj_origin_in_cam[0], proj_z_axis_end_in_cam[0]], [proj_origin_in_cam[1], proj_z_axis_end_in_cam[1]], [proj_origin_in_cam[2], proj_z_axis_end_in_cam[2]], color='#FFFF00', linestyle='--', label='Projector Z') # 黄色
    ax_stereo_pose.text(proj_x_axis_end_in_cam[0], proj_x_axis_end_in_cam[1], proj_x_axis_end_in_cam[2], "Proj X")
    ax_stereo_pose.text(proj_y_axis_end_in_cam[0], proj_y_axis_end_in_cam[1], proj_y_axis_end_in_cam[2], "Proj Y")
    ax_stereo_pose.text(proj_z_axis_end_in_cam[0], proj_z_axis_end_in_cam[1], proj_z_axis_end_in_cam[2], "Proj Z")

    # プロット範囲とアスペクト比の設定
    all_plot_points = np.vstack([
        cam_origin, cam_x_axis_end, cam_y_axis_end, cam_z_axis_end,
        proj_origin_in_cam, proj_x_axis_end_in_cam, proj_y_axis_end_in_cam, proj_z_axis_end_in_cam
    ])
    ax_stereo_pose.set_box_aspect([1,1,1]) # アスペクト比を等しくする
    ax_stereo_pose.set_xlim(-1.0, 1.0)
    ax_stereo_pose.set_ylim(-1.0, 1.0)
    ax_stereo_pose.set_zlim(-1.0, 1.0)
    ax_stereo_pose.set_xlabel('X (Camera Coord)')
    ax_stereo_pose.set_ylabel('Y (Camera Coord)')
    ax_stereo_pose.set_zlabel('Z (Camera Coord)')
    ax_stereo_pose.view_init(elev=-45, azim=-22, roll=-75) # 視点設定

    ax_stereo_pose.set_title('Camera to Projector Relative Pose (R, T)')
    ax_stereo_pose.legend()
    if args.debug:
        plt.show()

if __name__ == "__main__":
    # 引数取得
    parser = argparse.ArgumentParser(
        description="プロジェクションマッチング用キャリブレーション")
    parser.add_argument(
        "folder_path", type=str, help="画像保存フォルダパス")
    parser.add_argument(
        "grid_pitch", type=float, help="グリッドピッチ")
    parser.add_argument(
        "grid_width", type=int, help="グリッド幅数")
    parser.add_argument(
        "grid_height", type=int, help="グリッド高さ数")
    parser.add_argument(
        "--debug", "-d", action="store_true", help="デバッグモード"
    )
    args = parser.parse_args()

    try:
        main(args)
    except KeyboardInterrupt:
        pass
    except Exception:
        traceback.print_exc()
