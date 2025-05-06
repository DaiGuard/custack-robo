import traceback
import os
import argparse
from pathlib import Path
import dataclasses
import cv2
import numpy as np
import json
import math


@dataclasses.dataclass
class CalibDataset:

    name: str
    image: cv2.typing.MatLike

    board_upper_color: np.typing.NDArray
    board_lower_color: np.typing.NDArray
    # board_image_points: list[np.typing.NDArray]
    # board_object_points: list[np.typing.NDArray]

    project_win_size: tuple[int, int]
    project_grid_size: tuple[int, int]
    project_grid_pitch: int
    project_board_pose: tuple[int, int, int]
    project_upper_color: np.typing.NDArray
    project_lower_color: np.typing.NDArray
    # project_image_points: list[np.typing.NDArray]
    # project_object_points: list[np.typing.NDArray]

    def __init__(self):
        self.name = ""
        self.image = None

        self.board_upper_color = None
        self.board_lower_color = None
        # self.board_image_points = []
        # self.board_object_points = []

        self.project_win_size = (0, 0)
        self.project_grid_size = (0, 0)
        self.project_grid_pitch = 0
        self.project_board_pose = (0, 0, 0)
        self.project_upper_color = None
        self.project_lower_color = None
        # self.project_image_points = []
        # self.project_object_points = []


def load_dataset(
        folder_path: str, 
        grid_pitch: float, grid_size: tuple[int, int]) -> list[CalibDataset]:

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

                            # ボード側色域
                            data.board_lower_color = np.array((
                                json_data["board"]["color_range"][0],
                                json_data["board"]["color_range"][2],
                                json_data["board"]["color_range"][4]))
                            data.board_upper_color = np.array((
                                json_data["board"]["color_range"][1],
                                json_data["board"]["color_range"][3],
                                json_data["board"]["color_range"][5]))
                            
                            # プロジェクタ側色域
                            data.project_lower_color = np.array((
                                json_data["project"]["color_range"][0],
                                json_data["project"]["color_range"][2],
                                json_data["project"]["color_range"][4]))
                            data.project_upper_color = np.array((
                                json_data["project"]["color_range"][1],
                                json_data["project"]["color_range"][3],
                                json_data["project"]["color_range"][5]))
                            
                            # ボード情報入手
                            data.project_win_size = (
                                json_data["project"]["winsize"][0],
                                json_data["project"]["winsize"][1])
                            data.project_grid_size = (
                                json_data["project"]["grid_size"][0],
                                json_data["project"]["grid_size"][1])
                            data.project_grid_pitch = json_data["project"]["grid_pitch"]
                            data.project_board_pose = (
                                json_data["project"]["board_pose"][0],
                                json_data["project"]["board_pose"][1],
                                json_data["project"]["board_pose"][2]
                            )
                            # bw = grid_size[0]
                            # bh = grid_size[1]
                            # data.board_object_points = np.zeros(
                            #     (bw * bh, 3), np.float32)
                            # data.board_object_points[:, :2] = np.mgrid[
                            #     0:bw, 0:bh].T.reshape(-1, 2)
                            # data.board_object_points *= grid_pitch

                            # # ボード側イメージ点
                            # data.board_image_points = np.zeros(
                            #     (bw * bh, 2), np.float32)

                            # # プロジェクタ側オブジェクト点
                            # pcx = json_data["project"]["board_pose"][0]
                            # pcy = json_data["project"]["board_pose"][1]
                            # pcr = json_data["project"]["board_pose"][2]
                            # pp = json_data["project"]["grid_pitch"]
                            # pw = json_data["project"]["grid_size"][0]
                            # ph = json_data["project"]["grid_size"][1]
                            # data.project_object_points = np.zeros(
                            #     (pw * ph, 3), np.float32)
                            
                            # # プロジェクトグリッドサイズ
                            # data.project_grid_size = (pw, ph)
                            
                            # # プロジェクタ側イメージ点
                            # data.project_image_points = np.zeros(
                            #     (pw * ph, 2), np.float32)
                            # for j in range(ph):
                            #     for i in range(pw):
                            #         dx = i * pp
                            #         dy = j * pp

                            #         dbx = dx * math.cos(math.radians(pcr))
                            #         - dy * math.sin(math.radians(pcr))
                            #         dby = dx * math.sin(math.radians(pcr))
                            #         + dy * math.cos(math.radians(pcr))

                            #         data.project_image_points[i + j * pw][0] = pcx + dbx
                            #         data.project_image_points[i + j * pw][1] = pcy + dby

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

    return dst

def get_cricle_grid(
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
    cv2.drawChessboardCorners(tmp, grid_size, centers, ret)

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
                5, (0, 0, 255), -1)

    # 点群の次数を合わせる
    num = grid_size[0] * grid_size[1]
    grid_points = grid_points.reshape([num, 1, -1])

    return grid_points, tmp

def main(args):
    # 引数の取得
    folder_path = args.folder_path
    grid_pitch = args.grid_pitch
    grid_size = (args.grid_width, args.grid_height)

    # ローカル変数
    used_image = []
    board_image_size = None
    board_image_points = []     # キャリブボード画像上点
    board_object_points = []    # キャリブボード物体上点

    project_image_size = None    # プロジェクト画像サイズ
    project_image_points = []   # プロジェクトグリッド投影点
    project_perspective_points = [] # プロジェクトグリット画像上点
    project_object_points = []  # プロジェクトグリッド物体上点

    # データセットの読み込み
    dataset = load_dataset(folder_path, grid_pitch, grid_size)

    # ボードオブジェクト点の作成
    objp = np.zeros((grid_size[0] * grid_size[1], 3), np.float32)
    objp[:, :2] = np.mgrid[0:grid_size[0], 0:grid_size[1]].T.reshape(-1, 2)
    objp *= grid_pitch

    for data in dataset:
        # ボードイメージ点の取得
        board_image_size = data.image.shape[:-1]
        board_ret, board_centers, board_tmp = get_cricle_grid(
            data.image, data.board_lower_color, data.board_upper_color,
            grid_size)
        
        # プロジェクタイメージ点の取得
        project_ret, project_centers, project_tmp = get_cricle_grid(
            data.image, data.project_lower_color, data.project_upper_color,
            data.project_grid_size)
        
        project_image_size = (data.project_win_size[1], data.project_win_size[0])
        grid_points, grid_image = create_project_grid(
            data.project_win_size, data.project_grid_size, data.project_grid_pitch,
            data.project_board_pose)
        
        # データの保存
        if board_ret and project_ret:
            used_image.append(data.image)
            board_image_points.append(board_centers)
            board_object_points.append(objp)
            project_image_points.append(grid_points)
            project_perspective_points.append(project_centers)

        # デバック出力
        tmp = cv2.hconcat([board_tmp, project_tmp])
        tmp = cv2.resize(tmp, (0, 0), fx=0.5, fy=0.5)
        cv2.putText(tmp, data.name, (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
        cv2.imshow("image", tmp)
        gird_tmp = cv2.resize(grid_image, (0, 0), fx=0.5, fy=0.5)
        cv2.imshow("debug", gird_tmp)
        cv2.waitKey(0)

    # エラー処理
    if len(board_image_points) < 5:
        print(f"[E]: 十分なデータ数がありません {len(board_image_points)}.")
        return

    # カメラキャリブレーション実施
    ret, mtx, dist, rvecs, tvecs = cv2.calibrateCamera(
        board_object_points, board_image_points, board_image_size, None, None)
    if not ret:
        print(f"[E]: キャリブレーションに失敗しました.")
        return
    
    # キャリブレーション結果出力
    print(f"mtx: {mtx}")
    print(f"dist: {dist}")

    # プロジェクタ側オブジェクト点推定
    for rvec, tvec, points, image in zip(rvecs, tvecs, project_perspective_points, used_image):

        # 投影平面の計算
        rvec = rvec.reshape(-1)
        rmat, _ = cv2.Rodrigues(rvec)
        tvec = tvec.reshape(-1)

        # 復元プロジェクタグリッド3次元点
        objp = []
        for point in points.reshape(-1, 2):
            # 視線作成
            eye = np.array([
                (point[0] - mtx[0, 2]) / mtx[0, 0],
                (point[1] - mtx[1, 2]) / mtx[1, 1],
                1.0
            ])

            # 視線と平面の交点を計算
            M = np.array([
                [rmat[0, x], rmat[1, x], -eye[x]] for x in range(3)])
            M_inv = np.linalg.inv(M)
            A = -1.0 * M_inv @ tvec

            p = eye * A[2]
            objp.append(p)
        object_points = np.array(objp).reshape(-1, 3).astype(np.float32)
        project_object_points.append(object_points)

        # デバック出力
        tmp = image.copy()
        coordinates = np.array([
            tvec, 
            tvec + rmat[0] * grid_pitch * 2.0,
            tvec + rmat[1] * grid_pitch * 2.0,
            tvec + rmat[2] * grid_pitch * 2.0])        
        coordinate_proj, _ = cv2.projectPoints(
            coordinates,
            np.array([0.0, 0.0, 0.0]), np.array([0.0, 0.0, 0.0]),
            mtx, dist)
        coordinate_proj = coordinate_proj.reshape(-1, 2).astype(np.int32)
        projboard, _ = cv2.projectPoints(
            object_points,
            np.array([0.0, 0.0, 0.0]), np.array([0.0, 0.0, 0.0]),
            mtx, dist)
        projboard = projboard.reshape(-1, 2).astype(np.int32)
        cv2.circle(tmp, coordinate_proj[0], 5, (0, 255, 255), -1)
        cv2.circle(tmp, coordinate_proj[1], 5, (0, 0, 255), -1)
        cv2.circle(tmp, coordinate_proj[2], 5, (0, 255, 0), -1)
        cv2.circle(tmp, coordinate_proj[3], 5, (255, 0, 0), -1)
        cv2.line(tmp, coordinate_proj[0], coordinate_proj[1], (0, 0, 255), 2)
        cv2.line(tmp, coordinate_proj[0], coordinate_proj[2], (0, 255, 0), 2)
        cv2.line(tmp, coordinate_proj[0], coordinate_proj[3], (255, 0, 0), 2)
        for p in projboard:
            cv2.circle(tmp, p, 5, (0, 255, 0), -1)
        tmp = cv2.resize(tmp, (0, 0), fx=0.5, fy=0.5)
        cv2.imshow("debug", tmp)
        cv2.waitKey(0)

    # プロジェクタキャリブレーション実施
    project_mtx = np.zeros((3, 3), np.float32)
    project_mtx[0, 0] = project_image_size[0] / 2
    project_mtx[1, 1] = project_image_size[1] / 2
    project_mtx[0, 2] = project_image_size[0] / 2
    project_mtx[1, 2] = project_image_size[1] / 2
    project_mtx[2, 2] = 1.0
    ret, mtx, dist, rvecs, tvecs = cv2.calibrateCamera(
        project_object_points, project_image_points, project_image_size,
        project_mtx, None, flags=cv2.CALIB_USE_INTRINSIC_GUESS)

    # 終了処理
    cv2.destroyAllWindows()

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
    args = parser.parse_args()

    try:
        main(args)
    except KeyboardInterrupt:
        pass
    except Exception:
        traceback.print_exc()
