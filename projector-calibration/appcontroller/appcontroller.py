"""サークリグリッド投影プログラム."""
import tkinter as tk
from circleproj import shm
from appcontroller.range_slider import RangeSlider


class MainApplication(tk.Frame):
    """メインアプリケーションクラス."""

    def __init__(self, parent=None, shared_data: shm.SharedMemData = None):
        """コンストラクタ."""
        super().__init__(parent)
        self._parent = parent
        self._shared_data = shared_data

        self._grid_pitch_fm = tk.Frame(parent)
        self._grid_pitch_fm.grid(row=0, column=0)
        self._grid_pitch_lb = tk.Label(self._grid_pitch_fm, text="Grid Pitch [px]",
                                       width=15, anchor=tk.W)
        self._grid_pitch_lb.pack(side=tk.LEFT)
        self._grid_pitch_sb = tk.Spinbox(self._grid_pitch_fm,
                                         from_=10, to=2000, increment=1,
                                         command=self.girdPitchChangeValue,
                                         width=22,
                                         textvariable=tk.IntVar(value=self._shared_data.grid_pitch))
        self._grid_pitch_sb.pack(side=tk.LEFT)

        self._grid_size_fb = tk.Frame(parent)
        self._grid_size_fb.grid(row=1, column=0)
        self._grid_size_lb = tk.Label(self._grid_size_fb, text="Grid Size(x, y)",
                                      width=15, anchor=tk.W)
        self._grid_size_lb.pack(side=tk.LEFT)
        self._grid_size_x_sb = tk.Spinbox(self._grid_size_fb,
                                          from_=1, to=10, increment=1,
                                          command=self.gridSizeXChangeValue,
                                          width=10,
                                          textvariable=tk.IntVar(value=self._shared_data.grid_size[0]))
        self._grid_size_x_sb.pack(side=tk.LEFT)
        self._grid_size_y_sb = tk.Spinbox(self._grid_size_fb,
                                          from_=1, to=10, increment=1,
                                          command=self.gridSizeYChangeValue,
                                          width=10,
                                          textvariable=tk.IntVar(value=self._shared_data.grid_size[1]))
        self._grid_size_y_sb.pack(side=tk.LEFT)

        self._circle_radius_fb = tk.Frame(parent)
        self._circle_radius_fb.grid(row=2, column=0)
        self._circle_radius_lb = tk.Label(self._circle_radius_fb, text="Circle Radius [px]",
                                          width=15, anchor=tk.W)
        self._circle_radius_lb.pack(side=tk.LEFT)
        self._circle_radius_sb = tk.Spinbox(self._circle_radius_fb,
                                            from_=10, to=200, increment=1,
                                            width=22,
                                            command=self.circleRadiusChangeValue,
                                            textvariable=tk.IntVar(value=self._shared_data.circle_radius))
        self._circle_radius_sb.pack(side=tk.LEFT)

        self._circle_color_fb = tk.Frame(parent)
        self._circle_color_fb.grid(row=3, column=0)
        self._circle_color_lb = tk.Label(self._circle_color_fb, text="Circle Color(H,S,V)",
                                         width=15, anchor=tk.W)
        self._circle_color_lb.pack(side=tk.LEFT)
        self._circle_color_h_sb = tk.Spinbox(self._circle_color_fb,
                                             from_=0, to=255, increment=1,
                                             width=6,
                                             command=self.circleColorHChangeValue,
                                             textvariable=tk.IntVar(value=self._shared_data.circle_color[0]))
        self._circle_color_h_sb.pack(side=tk.LEFT)
        self._circle_color_s_sb = tk.Spinbox(self._circle_color_fb,
                                             from_=0, to=255, increment=1,
                                             width=6,
                                             command=self.circleColorSChangeValue,
                                             textvariable=tk.IntVar(value=self._shared_data.circle_color[1]))
        self._circle_color_s_sb.pack(side=tk.LEFT)
        self._circle_color_v_sb = tk.Spinbox(self._circle_color_fb,
                                             from_=0, to=255, increment=1,
                                             width=6,
                                             command=self.circleColorVChangeValue,
                                             textvariable=tk.IntVar(value=self._shared_data.circle_color[1]))
        self._circle_color_v_sb.pack(side=tk.LEFT)

        self._grid_pos_fm = tk.Frame(parent)
        self._grid_pos_fm.grid(row=4, column=0)
        self._grid_pos_mx_bt = tk.Button(self._grid_pos_fm, text="←",
                                         command=self.gridPosMinusXClick)
        self._grid_pos_px_bt = tk.Button(self._grid_pos_fm, text="→",
                                         command=self.gridPosPlusXClick)
        self._grid_pos_my_bt = tk.Button(self._grid_pos_fm, text="↑",
                                         command=self.gridPosMinusYClick)
        self._grid_pos_py_bt = tk.Button(self._grid_pos_fm, text="↓",
                                         command=self.gridPosPlusYClick)
        self._grid_pos_mmx_bt = tk.Button(self._grid_pos_fm, text="⇐",
                                          command=self.gridPosMMinusXClick)
        self._grid_pos_ppx_bt = tk.Button(self._grid_pos_fm, text="⇒",
                                          command=self.gridPosPPlusXClick)
        self._grid_pos_mmy_bt = tk.Button(self._grid_pos_fm, text="⇑",
                                          command=self.gridPosMMinusYClick)
        self._grid_pos_ppy_bt = tk.Button(self._grid_pos_fm, text="⇓",
                                          command=self.gridPosPPlusYClick)
        self._grid_pos_rp_bt = tk.Button(self._grid_pos_fm, text="➘",
                                         command=self.gridPosPRotClick)
        self._grid_pos_rm_bt = tk.Button(self._grid_pos_fm, text="↙",
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

    def girdPitchChangeValue(self):
        """グリッドピッチ値変更コールバック."""
        self._shared_data.grid_pitch = int(self._grid_pitch_sb.get())

    def gridSizeXChangeValue(self):
        """グリッドサイズX変更コールバック."""
        current = self._shared_data.grid_size
        value = int(self._grid_size_x_sb.get())
        self._shared_data.grid_size = (value, current[1])

    def gridSizeYChangeValue(self):
        """グリッドサイズY変更コールバック."""
        current = self._shared_data.grid_size
        value = int(self._grid_size_y_sb.get())
        self._shared_data.grid_size = (current[0], value)

    def circleRadiusChangeValue(self):
        """サークル半径変更コールバック."""
        self._shared_data.circle_radius = int(self._circle_radius_sb.get())

    def circleColorHChangeValue(self):
        """サークル色変更コールバック."""
        current = self._shared_data.circle_color
        value = int(self._circle_color_h_sb.get())
        self._shared_data.circle_color = (value, current[1], current[2])

    def circleColorSChangeValue(self):
        """サークル色変更コールバック."""
        current = self._shared_data.circle_color
        value = int(self._circle_color_s_sb.get())
        self._shared_data.circle_color = (current[0], value, current[2])

    def circleColorVChangeValue(self):
        """サークル色変更コールバック."""
        current = self._shared_data.circle_color
        value = int(self._circle_color_v_sb.get())
        self._shared_data.circle_color = (current[0], current[1], value)

    def gridPosMinusXClick(self):
        """グリッド位置Xマイナスボタンクリックコールバック."""
        current = self._shared_data.board_pose
        self._shared_data.board_pose = (current[0] - 1, current[1], current[2])

    def gridPosPlusXClick(self):
        """グリッド位置Xマイナスボタンクリックコールバック."""
        current = self._shared_data.board_pose
        self._shared_data.board_pose = (current[0] + 1, current[1], current[2])

    def gridPosMinusYClick(self):
        """グリッド位置Yマイナスボタンクリックコールバック."""
        current = self._shared_data.board_pose
        self._shared_data.board_pose = (current[0], current[1] - 1, current[2])

    def gridPosPlusYClick(self):
        """グリッド位置Yプラスボタンクリックコールバック."""
        current = self._shared_data.board_pose
        self._shared_data.board_pose = (current[0], current[1] + 1, current[2])

    def gridPosMMinusXClick(self):
        """グリッド位置Xマイナスボタンクリックコールバック."""
        current = self._shared_data.board_pose
        self._shared_data.board_pose = (current[0] - 30, current[1], current[2])

    def gridPosPPlusXClick(self):
        """グリッド位置Xマイナスボタンクリックコールバック."""
        current = self._shared_data.board_pose
        self._shared_data.board_pose = (current[0] + 30, current[1], current[2])

    def gridPosMMinusYClick(self):
        """グリッド位置Yマイナスボタンクリックコールバック."""
        current = self._shared_data.board_pose
        self._shared_data.board_pose = (current[0], current[1] - 30, current[2])

    def gridPosPPlusYClick(self):
        """グリッド位置Yプラスボタンクリックコールバック."""
        current = self._shared_data.board_pose
        self._shared_data.board_pose = (current[0], current[1] + 30, current[2])

    def gridPosPRotClick(self):
        """グリッド位置右回転ボタンクリックコールバック."""
        current = self._shared_data.board_pose
        self._shared_data.board_pose = (current[0], current[1], current[2] + 1)

    def gridPosMRotClick(self):
        """グリッド位置左回転ボタンクリックコールバック."""
        current = self._shared_data.board_pose
        self._shared_data.board_pose = (current[0], current[1], current[2] - 1) 

    def update(self):
        """定期実行処理."""
        if self._shared_data is not None:
            print(self._shared_data.app_sync)
            if self._shared_data.app_sync == 0:
                self._parent.quit()

        self.after(100, self.update)


def main(shared_data: shm.SharedMemData) -> None:
    """メイン関数."""
    window = tk.Tk()
    window.title("application_controller")

    def exitApplication():
        shared_data.app_sync = 0
        window.quit()

    window.protocol("WM_DELETE_WINDOW", exitApplication)

    app = MainApplication(window, shared_data=shared_data)
    app.update()

    window.mainloop()
