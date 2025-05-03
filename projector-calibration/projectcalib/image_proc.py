"""画像処理."""

import cv2
import numpy as np

def getColorRangeImage(
        src: np.ndarray,
        lower: tuple[int, int, int],
        upper: tuple[int, int, int]) -> np.ndarray:
    """特定の色域領域を抽出する

    Args:
        src (np.ndarray): 元画像(HSV)
        lower (tuple[int, int, int]): 下限色
        upper (tuple[int, int, int]): 上限色

    Returns:
        np.ndarray: 特定色域抽出画像(Gray)
    """

    # 色域抽出
    dst = cv2.inRange(src, np.array(lower), np.array(upper))

    # ノイズ除去
    kernel = np.ones((5, 5), np.uint8)
    dst = cv2.morphologyEx(dst, cv2.MORPH_OPEN, kernel, iterations=1)
    dst = cv2.morphologyEx(dst, cv2.MORPH_CLOSE, kernel, iterations=1)

    # ビット反転
    dst = cv2.bitwise_not(dst)

    return dst
