"""サークリグリッド投影プログラム."""
import tkinter as tk
import numpy as np
import cv2
import math
from PIL import Image, ImageTk
from . import shm


class MainApplication(tk.Frame):
    """メインアプリケーションクラス."""

    def __init__(self, parent=None, shared_data: shm.SharedMemData = None):
        """コンストラクタ."""
        super().__init__(parent)
        self._parent = parent
        self._shared_data = shared_data

        # 最大範囲拡大
        self.pack(expand=True, fill=tk.BOTH)
        # 画像ラベルの追加
        self._label = tk.Label(self)
        self._label.pack(expand=True, fill=tk.BOTH)

    def update(self) -> None:
        """定期実行処理."""
        # 現在のウインドウサイズを取得
        w = self.winfo_width()
        h = self.winfo_height()

        if self._shared_data is not None and w > 1 and h > 1:
            # アプリケーション起動確認
            if self._shared_data.app_sync == 0:
                self._parent.quit()

            self._shared_data.app_sync += 1
            self._shared_data.winsize = (w, h)

            # 背景画像を作成
            hsv_image = np.full((h, w, 3), (255, 0, 255), dtype=np.uint8)

            # 共有データを取得する
            size = self._shared_data.grid_size
            pitch = self._shared_data.grid_pitch
            pos = self._shared_data.board_pose
            radius = self._shared_data.circle_radius
            color = self._shared_data.circle_color

            # 円を描画
            for i in range(size[0]):
                for j in range(size[1]):
                    dx = i * pitch
                    dy = j * pitch

                    dbx = dx * math.cos(math.radians(pos[2])) - dy * math.sin(math.radians(pos[2]))
                    dby = dx * math.sin(math.radians(pos[2])) + dy * math.cos(math.radians(pos[2]))

                    px = pos[0] + int(dbx)
                    py = pos[1] + int(dby)
                    cv2.circle(hsv_image, (px, py), radius, color, -1)
            
            rgb_image = cv2.cvtColor(hsv_image, cv2.COLOR_HSV2RGB)
            pil_image = Image.fromarray(rgb_image)
            tk_image = ImageTk.PhotoImage(image=pil_image)
            self._label.configure(image=tk_image)
            self._label.image = tk_image

        self.after(100, self.update)


def main(shared_data: shm.SharedMemData) -> None:
    """メイン関数."""
    window = tk.Tk()
    window.title("circle_projector")
    window.geometry("960x540")
    window.attributes("-fullscreen", True)

    def exitApplication(event):
        shared_data.app_sync = 0
        window.quit()

    def zoomSwitch(event):
        window.attributes("-fullscreen", not window.attributes("-fullscreen"))

    window.bind("<Escape>", exitApplication)
    window.bind("z", zoomSwitch)
    app = MainApplication(parent=window, shared_data=shared_data)
    app.update()

    window.mainloop()

