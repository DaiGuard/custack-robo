"""サークリグリッド投影プログラム."""
import tkinter as tk
from circleproj import shm as cpshm
from projectcalib import shm as pcshm
from appcontroller.tkSliderWidget import Slider


class MainApplication(tk.Frame):
    """メインアプリケーションクラス."""

    def __init__(
            self, parent=None,
            cp_data: cpshm.SharedMemData = None,
            pc_data: pcshm.SharedMemData = None):
        """コンストラクタ."""
        super().__init__(parent)
        self._parent = parent
        self._cp_data = cp_data
        self._pc_data = pc_data

        # グリッドピッチ入力フレーム
        self._grid_pitch_fm = tk.Frame(parent)
        self._grid_pitch_fm.grid(row=0, column=0)
        self._grid_pitch_lb = tk.Label(
            self._grid_pitch_fm, text="Grid Pitch [px]",
            width=15, anchor=tk.W)
        self._grid_pitch_lb.pack(side=tk.LEFT)
        self._grid_pitch_sb = tk.Spinbox(
            self._grid_pitch_fm,
            from_=10, to=2000, increment=1, width=22,
            command=self.girdPitchChangeValue,
            textvariable=tk.IntVar(value=self._cp_data.grid_pitch))
        self._grid_pitch_sb.pack(side=tk.LEFT)

        # グリッドサイズ入力フレーム
        self._grid_size_fb = tk.Frame(parent)
        self._grid_size_fb.grid(row=1, column=0)
        self._grid_size_lb = tk.Label(
            self._grid_size_fb, text="Grid Size(x, y)",
            width=15, anchor=tk.W)
        self._grid_size_lb.pack(side=tk.LEFT)
        self._grid_size_x_sb = tk.Spinbox(
            self._grid_size_fb,
            from_=1, to=10, increment=1, width=10,
            command=self.gridSizeXChangeValue,
            textvariable=tk.IntVar(value=self._cp_data.grid_size[0]))
        self._grid_size_x_sb.pack(side=tk.LEFT)
        self._grid_size_y_sb = tk.Spinbox(
            self._grid_size_fb,
            from_=1, to=10, increment=1, width=10,
            command=self.gridSizeYChangeValue,
            textvariable=tk.IntVar(value=self._cp_data.grid_size[1]))
        self._grid_size_y_sb.pack(side=tk.LEFT)

        # 円形半径入力フレーム
        self._circle_radius_fb = tk.Frame(parent)
        self._circle_radius_fb.grid(row=2, column=0)
        self._circle_radius_lb = tk.Label(
            self._circle_radius_fb, text="Circle Radius [px]",
            width=15, anchor=tk.W)
        self._circle_radius_lb.pack(side=tk.LEFT)
        self._circle_radius_sb = tk.Spinbox(
            self._circle_radius_fb,
            from_=10, to=200, increment=1, width=22,
            command=self.circleRadiusChangeValue,
            textvariable=tk.IntVar(value=self._cp_data.circle_radius))
        self._circle_radius_sb.pack(side=tk.LEFT)

        # 円形カラー入力フレーム
        self._circle_color_fb = tk.Frame(parent)
        self._circle_color_fb.grid(row=3, column=0)
        self._circle_color_lb = tk.Label(
            self._circle_color_fb, text="Circle Color(H,S,V)",
            width=15, anchor=tk.W)
        self._circle_color_lb.pack(side=tk.LEFT)
        self._circle_color_h_sb = tk.Spinbox(
            self._circle_color_fb,
            from_=0, to=255, increment=1, width=6,
            command=self.circleColorHChangeValue,
            textvariable=tk.IntVar(value=self._cp_data.circle_color[0]))
        self._circle_color_h_sb.pack(side=tk.LEFT)
        self._circle_color_s_sb = tk.Spinbox(
            self._circle_color_fb,
            from_=0, to=255, increment=1, width=6,
            command=self.circleColorSChangeValue,
            textvariable=tk.IntVar(value=self._cp_data.circle_color[1]))
        self._circle_color_s_sb.pack(side=tk.LEFT)
        self._circle_color_v_sb = tk.Spinbox(
            self._circle_color_fb,
            from_=0, to=255, increment=1, width=6,
            command=self.circleColorVChangeValue,
            textvariable=tk.IntVar(value=self._cp_data.circle_color[1]))
        self._circle_color_v_sb.pack(side=tk.LEFT)

        # グリッド位置姿勢入力フレーム
        self._grid_pos_fm = tk.Frame(parent)
        self._grid_pos_fm.grid(row=4, column=0)
        self._grid_pos_mx_bt = tk.Button(
            self._grid_pos_fm, text="←",
            command=self.gridPosMinusXClick)
        self._grid_pos_px_bt = tk.Button(
            self._grid_pos_fm, text="→",
            command=self.gridPosPlusXClick)
        self._grid_pos_my_bt = tk.Button(
            self._grid_pos_fm, text="↑",
            command=self.gridPosMinusYClick)
        self._grid_pos_py_bt = tk.Button(
            self._grid_pos_fm, text="↓",
            command=self.gridPosPlusYClick)
        self._grid_pos_mmx_bt = tk.Button(
            self._grid_pos_fm, text="⇐",
            command=self.gridPosMMinusXClick)
        self._grid_pos_ppx_bt = tk.Button(
            self._grid_pos_fm, text="⇒",
            command=self.gridPosPPlusXClick)
        self._grid_pos_mmy_bt = tk.Button(
            self._grid_pos_fm, text="⇑",
            command=self.gridPosMMinusYClick)
        self._grid_pos_ppy_bt = tk.Button(
            self._grid_pos_fm, text="⇓",
            command=self.gridPosPPlusYClick)
        self._grid_pos_rp_bt = tk.Button(
            self._grid_pos_fm, text="➘",
            command=self.gridPosPRotClick)
        self._grid_pos_rm_bt = tk.Button(
            self._grid_pos_fm, text="↙",
            command=self.gridPosMRotClick)        
        self._grid_pos_mx_bt.grid(row=2, column=1)
        self._grid_pos_px_bt.grid(row=2, column=3)
        self._grid_pos_my_bt.grid(row=1, column=2)
        self._grid_pos_py_bt.grid(row=3, column=2)

        self._grid_pos_mmx_bt.grid(row=2, column=0)
        self._grid_pos_ppx_bt.grid(row=2, column=4)
        self._grid_pos_mmy_bt.grid(row=0, column=2)
        self._grid_pos_ppy_bt.grid(row=4, column=2)

        self._grid_pos_rp_bt.grid(row=0, column=4)
        self._grid_pos_rm_bt.grid(row=0, column=0)

        # 色域1入力フレーム
        self._color_range1_fm = tk.Frame(parent)
        self._color_range1_fm.grid(row=5, column=0)
        self._color_range1_lb = tk.Label(
            self._color_range1_fm, text="Color Range 1 (HSV)",
            width=37)
        self._color_range1_lb.pack(side=tk.TOP)
        self._color_range1_h_rs = Slider(
            master=self._color_range1_fm,
            width=350, height=38, min_val=0, max_val=255,
            init_lis=[
                self._pc_data.color_range1[0],
                self._pc_data.color_range1[1]],
            show_value=1
        )
        self._color_range1_s_rs = Slider(
            master=self._color_range1_fm,
            width=350, height=38, min_val=0, max_val=255,
            init_lis=[
                self._pc_data.color_range1[2],
                self._pc_data.color_range1[3]],
                show_value=1
        )
        self._color_range1_v_rs = Slider(
            master=self._color_range1_fm,
            width=350, height=38, min_val=0, max_val=255,
            init_lis=[
                self._pc_data.color_range1[4],
                self._pc_data.color_range1[5]],
                show_value=1
        )
        self._color_range1_h_rs.pack(side=tk.TOP)
        self._color_range1_s_rs.pack(side=tk.TOP)
        self._color_range1_v_rs.pack(side=tk.TOP)

        # 変更コールバック設定
        self._color_range1_h_rs.setValueChangeCallback(
            self.colorRange1HChangeValue
        )
        self._color_range1_s_rs.setValueChangeCallback(
            self.colorRange1SChangeValue
        )
        self._color_range1_v_rs.setValueChangeCallback(
            self.colorRange1VChangeValue
        )

        # 色域2入力フレーム
        self._color_range2_fm = tk.Frame(parent)
        self._color_range2_fm.grid(row=6, column=0)
        self._color_range2_lb = tk.Label(
            self._color_range2_fm, text="Color Range 2 (HSV)",
            width=37)
        self._color_range2_lb.pack(side=tk.TOP)
        self._color_range2_h_rs = Slider(
            master=self._color_range2_fm,
            width=350, height=38, min_val=0, max_val=255,
            init_lis=[
                self._pc_data.color_range2[0],
                self._pc_data.color_range2[1]],
            show_value=1
        )
        self._color_range2_s_rs = Slider(
            master=self._color_range2_fm,
            width=350, height=38, min_val=0, max_val=255,
            init_lis=[
                self._pc_data.color_range2[2],
                self._pc_data.color_range2[3]],
            show_value=1
        )
        self._color_range2_v_rs = Slider(
            master=self._color_range2_fm,
            width=350, height=38, min_val=0, max_val=255,
            init_lis=[
                self._pc_data.color_range2[4],
                self._pc_data.color_range2[5]],
            show_value=1
        )
        self._color_range2_h_rs.pack(side=tk.TOP)
        self._color_range2_s_rs.pack(side=tk.TOP)
        self._color_range2_v_rs.pack(side=tk.TOP)

        # 変更コールバック設定
        self._color_range2_h_rs.setValueChangeCallback(
            self.colorRange2HChangeValue
        )
        self._color_range2_s_rs.setValueChangeCallback(
            self.colorRange2SChangeValue
        )
        self._color_range2_v_rs.setValueChangeCallback(
            self.colorRange2VChangeValue
        )

        # キャプチャーボタン
        self._capture_btn = tk.Button(
            master=parent, text="□",
            command=self.captureBtnClick
        )
        self._capture_btn.grid(row=7, column=0)


    def girdPitchChangeValue(self):
        """グリッドピッチ値変更コールバック."""
        self._cp_data.grid_pitch = int(self._grid_pitch_sb.get())

    def gridSizeXChangeValue(self):
        """グリッドサイズX変更コールバック."""
        current = self._cp_data.grid_size
        value = int(self._grid_size_x_sb.get())
        self._cp_data.grid_size = (value, current[1])

    def gridSizeYChangeValue(self):
        """グリッドサイズY変更コールバック."""
        current = self._cp_data.grid_size
        value = int(self._grid_size_y_sb.get())
        self._cp_data.grid_size = (current[0], value)

    def circleRadiusChangeValue(self):
        """サークル半径変更コールバック."""
        self._cp_data.circle_radius = int(self._circle_radius_sb.get())

    def circleColorHChangeValue(self):
        """サークル色変更コールバック."""
        current = self._cp_data.circle_color
        value = int(self._circle_color_h_sb.get())
        self._cp_data.circle_color = (value, current[1], current[2])

    def circleColorSChangeValue(self):
        """サークル色変更コールバック."""
        current = self._cp_data.circle_color
        value = int(self._circle_color_s_sb.get())
        self._cp_data.circle_color = (current[0], value, current[2])

    def circleColorVChangeValue(self):
        """サークル色変更コールバック."""
        current = self._cp_data.circle_color
        value = int(self._circle_color_v_sb.get())
        self._cp_data.circle_color = (current[0], current[1], value)

    def gridPosMinusXClick(self):
        """グリッド位置Xマイナスボタンクリックコールバック."""
        current = self._cp_data.board_pose
        self._cp_data.board_pose = (current[0] - 1, current[1], current[2])

    def gridPosPlusXClick(self):
        """グリッド位置Xマイナスボタンクリックコールバック."""
        current = self._cp_data.board_pose
        self._cp_data.board_pose = (current[0] + 1, current[1], current[2])

    def gridPosMinusYClick(self):
        """グリッド位置Yマイナスボタンクリックコールバック."""
        current = self._cp_data.board_pose
        self._cp_data.board_pose = (current[0], current[1] - 1, current[2])

    def gridPosPlusYClick(self):
        """グリッド位置Yプラスボタンクリックコールバック."""
        current = self._cp_data.board_pose
        self._cp_data.board_pose = (current[0], current[1] + 1, current[2])

    def gridPosMMinusXClick(self):
        """グリッド位置Xマイナスボタンクリックコールバック."""
        current = self._cp_data.board_pose
        self._cp_data.board_pose = (current[0] - 30, current[1], current[2])

    def gridPosPPlusXClick(self):
        """グリッド位置Xマイナスボタンクリックコールバック."""
        current = self._cp_data.board_pose
        self._cp_data.board_pose = (current[0] + 30, current[1], current[2])

    def gridPosMMinusYClick(self):
        """グリッド位置Yマイナスボタンクリックコールバック."""
        current = self._cp_data.board_pose
        self._cp_data.board_pose = (current[0], current[1] - 30, current[2])

    def gridPosPPlusYClick(self):
        """グリッド位置Yプラスボタンクリックコールバック."""
        current = self._cp_data.board_pose
        self._cp_data.board_pose = (current[0], current[1] + 30, current[2])

    def gridPosPRotClick(self):
        """グリッド位置右回転ボタンクリックコールバック."""
        current = self._cp_data.board_pose
        self._cp_data.board_pose = (current[0], current[1], current[2] + 1)

    def gridPosMRotClick(self):
        """グリッド位置左回転ボタンクリックコールバック."""
        current = self._cp_data.board_pose
        self._cp_data.board_pose = (current[0], current[1], current[2] - 1) 

    def colorRange1HChangeValue(self, values):
        """色域1H変更コールバック."""
        v = sorted(values)
        current = self._pc_data.color_range1
        self._pc_data.color_range1 = (
            int(v[0]), int(v[1]),
            current[2], current[3],
            current[4], current[5]
        )

    def colorRange1SChangeValue(self, values):
        """色域1H変更コールバック."""
        v = sorted(values)
        current = self._pc_data.color_range1
        self._pc_data.color_range1 = (
            current[0], current[1],
            int(v[0]), int(v[1]),
            current[4], current[5]
        )

    def colorRange1VChangeValue(self, values):
        """色域1H変更コールバック."""
        v = sorted(values)
        current = self._pc_data.color_range1
        self._pc_data.color_range1 = (
            current[0], current[1],
            current[2], current[3],
            int(v[0]), int(v[1])
        )

    def colorRange2HChangeValue(self, values):
        """色域1H変更コールバック."""
        v = sorted(values)
        current = self._pc_data.color_range2
        self._pc_data.color_range2 = (
            int(v[0]), int(v[1]),
            current[2], current[3],
            current[4], current[5]
        )

    def colorRange2SChangeValue(self, values):
        """色域1H変更コールバック."""
        v = sorted(values)
        current = self._pc_data.color_range2
        self._pc_data.color_range2 = (
            current[0], current[1],
            int(v[0]), int(v[1]),
            current[4], current[5]
        )

    def colorRange2VChangeValue(self, values):
        """色域1H変更コールバック."""
        v = sorted(values)
        current = self._pc_data.color_range2
        self._pc_data.color_range2 = (
            current[0], current[1],
            current[2], current[3],
            int(v[0]), int(v[1])
        )

    def captureBtnClick(self):
        """キャプチャーボタンクリックコールバック."""
        self._pc_data.capture_trigger = True

    def update(self):
        """定期実行処理."""
        if self._cp_data is not None:
            if self._cp_data.app_sync == 0:
                self._parent.quit()

        self.after(100, self.update)


def main(cp_data: cpshm.SharedMemData, pc_data: pcshm.SharedMemData) -> None:
    """メイン関数."""
    window = tk.Tk()
    window.title("application_controller")

    def exitApplication():
        cp_data.app_sync = 0
        pc_data.app_sync = 0
        window.quit()

    window.protocol("WM_DELETE_WINDOW", exitApplication)

    app = MainApplication(window, cp_data=cp_data, pc_data=pc_data)
    app.update()

    window.mainloop()
