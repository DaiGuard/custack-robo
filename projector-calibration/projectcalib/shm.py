"""プロジェクタキャリブレーションプログラムの共有データ."""
import dataclasses
import ctypes
import json
import os

@dataclasses.dataclass
class SharedMemData(ctypes.Structure):
    """共有メモリデータ配置マップ."""

    _fields_ = [
        # アプリケーション同期
        ("_app_sync", ctypes.c_uint64),
        # ウインドウサイズ
        ("_winsize", ctypes.c_uint32 * 2),
        # グリッドサイズ (x[px], y[px])
        ("_grid_size", ctypes.c_uint32 * 2),
        # グリッドピッチ [px]
        ("_grid_pitch", ctypes.c_uint32),
        # ボード位置姿勢 (x[px], y[px], rotation[deg])
        ("_board_pose", ctypes.c_int32 * 3),
        # 画像1 色域HSV (lo_H, up_H, lo_S, up_S, lo_V, up_V)
        ("_color_range1", ctypes.c_uint32 * 6),
        # 画像2 色域HSV (lo_H, up_H, lo_S, up_S, lo_V, up_V)
        ("_color_range2", ctypes.c_uint32 * 6),
        # 画像保存トリガ
        ("_capture_trigger", ctypes.c_bool)
    ]

    def __init__(self):
        """コンストラクタ."""
        super().__init__()
        self._app_sync = 1
        self._winsize[0] = 1
        self._winsize[1] = 1
        self._grid_size[0] = 6
        self._grid_size[1] = 4
        self._grid_pitch = 150
        self._board_pose[0] = 100
        self._board_pose[1] = 100
        self._board_pose[2] = 0
        self._color_range1[0] = 80
        self._color_range1[1] = 110
        self._color_range1[2] = 50
        self._color_range1[3] = 255
        self._color_range1[4] = 50
        self._color_range1[5] = 255
        self._color_range2[0] = 50
        self._color_range2[1] = 95
        self._color_range2[2] = 50
        self._color_range2[3] = 255
        self._color_range2[4] = 50
        self._color_range2[5] = 255
        self._capture_trigger = False

    def reset(self):
        """データリセット."""
        self._app_sync = 1
        self._winsize[0] = 1
        self._winsize[1] = 1
        self._grid_size[0] = 6
        self._grid_size[1] = 4
        self._grid_pitch = 150
        self._board_pose[0] = 100
        self._board_pose[1] = 100
        self._board_pose[2] = 0
        self._color_range1[0] = 50
        self._color_range1[1] = 95
        self._color_range1[2] = 50
        self._color_range1[3] = 255
        self._color_range1[4] = 50
        self._color_range1[5] = 255
        self._color_range2[0] = 50
        self._color_range2[1] = 95
        self._color_range2[2] = 50
        self._color_range2[3] = 255
        self._color_range2[4] = 50
        self._color_range2[5] = 255
        self._capture_trigger = False

    def save(self):
        """データ保存."""
        data = {
            "grid_size": (self._grid_size[0], self._grid_size[1]),
            "grid_pitch": self._grid_pitch,
            "board_pose": (
                self._board_pose[0], self._board_pose[1], self._board_pose[2]),
            "color_range1": (
                self._color_range1[0], self._color_range1[1], self._color_range1[2],
                self._color_range1[3], self._color_range1[4], self._color_range1[5]),
            "color_range2": (
                self._color_range2[0], self._color_range2[1], self._color_range2[2],
                self._color_range2[3], self._color_range2[4], self._color_range2[5])
        }
        json_data = json.dumps(data, indent=4)
        with open("pc_config.json", "w", encoding="utf-8") as f:
            f.write(json_data)

    def load(self) -> bool:
        """データ読み込み."""
        if os.path.isfile("pc_config.json"):
            with open("pc_config.json", "r", encoding="utf-8") as f:
                data = json.load(f)
                self._grid_size[0] = data["grid_size"][0]
                self._grid_size[1] = data["grid_size"][1]
                self._grid_pitch = data["grid_pitch"]
                self._board_pose[0] = data["board_pose"][0]
                self._board_pose[1] = data["board_pose"][1]
                self._board_pose[2] = data["board_pose"][2]
                self._color_range1[0] = data["color_range1"][0]
                self._color_range1[1] = data["color_range1"][1]
                self._color_range1[2] = data["color_range1"][2]
                self._color_range1[3] = data["color_range1"][3] 
                self._color_range1[4] = data["color_range1"][4]
                self._color_range1[5] = data["color_range1"][5]
                self._color_range2[0] = data["color_range2"][0]
                self._color_range2[1] = data["color_range2"][1]
                self._color_range2[2] = data["color_range2"][2]
                self._color_range2[3] = data["color_range2"][3]
                self._color_range2[4] = data["color_range2"][4]
                self._color_range2[5] = data["color_range2"][5]
        else:
            return False
        return True

    @property
    def app_sync(self) -> int:
        """アプリケーション同期."""
        return self._app_sync

    @property
    def winsize(self) -> tuple[int, int]:
        """ウインドウサイズ."""
        return (self._winsize[0], self._winsize[1])

    @property
    def grid_size(self) -> tuple[int, int]:
        """グリッドサイズ."""
        return (self._grid_size[0], self._grid_size[1])

    @property
    def grid_pitch(self) -> int:
        """グリッドピッチ."""
        return self._grid_pitch

    @property
    def board_pose(self) -> tuple[int, int, int]:
        """ボード位置姿勢."""
        return (self._board_pose[0], self._board_pose[1], self._board_pose[2])

    @property
    def color_range1(self) -> tuple[int, int, int, int, int, int]:
        """画像1 色域."""
        return (self._color_range1[0],
                self._color_range1[1],
                self._color_range1[2],
                self._color_range1[3],
                self._color_range1[4],
                self._color_range1[5])

    @property
    def color_range2(self) -> tuple[int, int, int, int, int, int]:
        """画像2 色域."""
        return (self._color_range2[0],
                self._color_range2[1],
                self._color_range2[2],
                self._color_range2[3],
                self._color_range2[4],
                self._color_range2[5])

    @property
    def capture_trigger(self) -> bool:
        """画像保存トリガ."""
        return self._capture_trigger

    @app_sync.setter
    def app_sync(self, sync: int):
        """アプリケーション同期."""
        self._app_sync = sync

    @winsize.setter
    def winsize(self, size: tuple[int, int]):
        """ウインドウサイズ."""
        self._winsize[0] = size[0]
        self._winsize[1] = size[1]
    
    @grid_size.setter
    def grid_size(self, size: tuple[int, int]):
        """グリッドサイズ."""
        self._grid_size[0] = size[0]
        self._grid_size[1] = size[1]

    @grid_pitch.setter
    def grid_pitch(self, pitch: int):
        """グリッドピッチ."""
        self._grid_pitch = pitch

    @board_pose.setter
    def board_pose(self, pose: tuple[int, int, int]):
        """ボード位置姿勢."""
        self._board_pose[0] = pose[0]
        self._board_pose[1] = pose[1]
        self._board_pose[2] = pose[2]

    @color_range1.setter
    def color_range1(self, color_range: tuple[int, int, int, int, int, int]):
        """画像1 色域."""
        self._color_range1[0] = color_range[0]
        self._color_range1[1] = color_range[1]
        self._color_range1[2] = color_range[2]
        self._color_range1[3] = color_range[3]
        self._color_range1[4] = color_range[4]
        self._color_range1[5] = color_range[5]

    @color_range2.setter
    def color_range2(self, color_range: tuple[int, int, int, int, int, int]):
        """画像2 色域."""
        self._color_range2[0] = color_range[0]
        self._color_range2[1] = color_range[1]
        self._color_range2[2] = color_range[2]
        self._color_range2[3] = color_range[3]
        self._color_range2[4] = color_range[4]
        self._color_range2[5] = color_range[5]

    @capture_trigger.setter
    def capture_trigger(self, trigger: bool):
        """画像保存トリガ."""
        self._capture_trigger = trigger