"""プロジェクタキャリブレーション."""
import cv2
from . import shm

def main(shared_data: shm.SharedMemData) -> None:
    """メイン関数."""
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("[E]: cannot open camera device")
        return
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1920)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 1080)
    
    while True:
        ret, frame = cap.read()