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
    image: cv2.typing.MatLike

    board_upper_color: np.typing.NDArray
    board_lower_color: np.typing.NDArray

    board_image_points: list[np.typing.NDArray]
    board_object_points: list[np.typing.NDArray]

    project_upper_color: np.typing.NDArray
    project_lower_color: np.typing.NDArray

    project_image_points: list[np.typing.NDArray]
    project_object_points: list[np.typing.NDArray]

    def __init__(self):
        self.image = None

        self.board_upper_color = None
        self.board_lower_color = None

        self.board_image_points = []
        self.board_object_points = []

        self.project_upper_color = None
        self.project_lower_color = None

        self.project_image_points = []
        self.project_object_points = []


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
                            
                            # ボード側オブジェクト点
                            bw = grid_size[0]
                            bh = grid_size[1]
                            data.board_object_points = np.zeros(
                                (bw * bh, 3), np.float32)
                            data.board_object_points[:, :2] = np.mgrid[
                                0:bw, 0:bh].T.reshape(-1, 2)
                            data.board_object_points *= grid_pitch

                            # ボード側イメージ点
                            data.board_image_points = np.zeros(
                                (bw * bh, 2), np.float32)
                            
                            # プロジェクタ側オブジェクト点
                            pcx = json_data["project"]["board_pose"][0]
                            pcy = json_data["project"]["board_pose"][1]
                            pcr = json_data["project"]["board_pose"][2]
                            pp = json_data["project"]["grid_pitch"]
                            pw = json_data["project"]["grid_size"][0]
                            ph = json_data["project"]["grid_size"][1]
                            data.project_object_points = np.zeros(
                                (pw * ph, 3), np.float32)
                            
                            # プロジェクタ側イメージ点
                            data.project_image_points = np.zeros(
                                (pw * ph, 2), np.float32)
                            for j in range(ph):
                                for i in range(pw):
                                    dx = i * pp
                                    dy = j * pp

                                    dbx = dx * math.cos(math.radians(pcr))
                                    - dy * math.sin(math.radians(pcr))
                                    dby = dx * math.sin(math.radians(pcr))
                                    + dy * math.cos(math.radians(pcr))

                                    data.project_image_points[i + j * pw][0] = pcx + dbx
                                    data.project_image_points[i + j * pw][1] = pcy + dby

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

def main(args):
    folder_path = args.folder_path
    grid_pitch = args.grid_pitch
    grid_size = (args.grid_width, args.grid_height)

    # データセットの読み込み
    dataset = load_dataset(folder_path, grid_pitch, grid_size)

    # ボードイメージ+ボードイメージ点の取得
    for data in dataset:
        image = get_color_range_image(
            cv2.cvtColor(data.image, cv2.COLOR_BGR2HSV),
            data.board_lower_color, data.board_upper_color)
        
        ret, centers = cv2.findCirclesGrid(
            image, grid_size, flags=cv2.CALIB_CB_SYMMETRIC_GRID)
        
        tmp = data.image.copy()
        cv2.drawChessboardCorners(tmp, grid_size, centers, ret)
        cv2.imshow("image", tmp)
        cv2.waitKey(0)

    # カメラキャリブレーション実施

    # プロジェクタ側オブジェクト点推定

    # プロジェクタキャリブレーション実施


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
