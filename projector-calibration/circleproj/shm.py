"""サークルグリッド投影プログラムの共有データ."""
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
        # サークル色 (H, S, V)
        ("_circle_color", ctypes.c_uint8 * 3),
        # サークル半径 [px]
        ("_circle_radius", ctypes.c_uint32)
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
        self._circle_color[0] = 60
        self._circle_color[1] = 255
        self._circle_color[2] = 255
        self._circle_radius = 50

    def reset(self):
        """データリセット."""
        self._app_sync = 1
        self._grid_size[0] = 6
        self._grid_size[1] = 4
        self._winsize[0] = 1
        self._winsize[1] = 1
        self._grid_pitch = 150
        self._board_pose[0] = 100
        self._board_pose[1] = 100
        self._board_pose[2] = 0
        self._circle_color[0] = 60
        self._circle_color[1] = 255
        self._circle_color[2] = 255
        self._circle_radius = 50

    def save(self):
        """データ保存."""
        data = {
            "winsize": (self._winsize[0], self._winsize[1]),
            "grid_size": (self._grid_size[0], self._grid_size[1]),
            "grid_pitch": self._grid_pitch,
            "board_pose": (self._board_pose[0], self._board_pose[1], self._board_pose[2]),
            "circle_color": (self._circle_color[0], self._circle_color[1], self._circle_color[2]),
            "circle_radius": self._circle_radius
        }
        json_data = json.dumps(data, indent=4)
        with open("cp_config.json", "w", encoding="utf-8") as f:
            f.write(json_data)
    
    def load(self) -> bool:
        """データ読み込み."""
        if os.path.isfile("cp_config.json"):
            with open("cp_config.json", "r", encoding="utf-8") as f:
                data = json.load(f)
                self._winsize[0] = data["winsize"][0]
                self._winsize[1] = data["winsize"][1]
                self._grid_size[0] = data["grid_size"][0]
                self._grid_size[1] = data["grid_size"][1]
                self._grid_pitch = data["grid_pitch"]
                self._board_pose[0] = data["board_pose"][0]
                self._board_pose[1] = data["board_pose"][1]
                self._board_pose[2] = data["board_pose"][2]
                self._circle_color[0] = data["circle_color"][0]
                self._circle_color[1] = data["circle_color"][1]
                self._circle_color[2] = data["circle_color"][2]
                self._circle_radius = data["circle_radius"]
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
    def circle_color(self) -> tuple[int, int, int]:
        """サークル色."""
        return (self._circle_color[0], self._circle_color[1], self._circle_color[2])

    @property
    def circle_radius(self) -> int:
        """サークル半径."""
        return self._circle_radius

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

    @circle_color.setter
    def circle_color(self, color: tuple[int, int, int]):
        """サークル色."""
        self._circle_color[0] = color[0]
        self._circle_color[1] = color[1]
        self._circle_color[2] = color[2]

    @circle_radius.setter
    def circle_radius(self, radius: int):
        """サークル半径."""
        self._circle_radius = radius
