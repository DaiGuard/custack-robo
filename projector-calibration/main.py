"""プロジェクタキャリブレーションツール."""
import traceback
from multiprocessing import Process, RawArray
import ctypes
import threading
import time

from circleproj import shm as cpshm
from circleproj import circleproj
from appcontroller import appcontroller
from projectcalib import shm as pcshm
from projectcalib import projectcalib


def dataBridge(
        cp_data: cpshm.SharedMemData,
        pc_data: pcshm.SharedMemData):
    """共有データブリッジ."""
    while True:
        if cp_data.app_sync == 0 or pc_data.app_sync == 0:
            cp_data.app_sync = 0
            pc_data.app_sync = 0
            break

        time.sleep(0.1)

def main():
    """メイン関数."""
    # 共有データの確保
    cp_value = RawArray(ctypes.c_byte, ctypes.sizeof(cpshm.SharedMemData))
    cp_data = cpshm.SharedMemData.from_buffer(cp_value)
    cp_data.reset()
    cp_data.load()
        

    pc_value = RawArray(ctypes.c_byte, ctypes.sizeof(pcshm.SharedMemData))
    pc_data = pcshm.SharedMemData.from_buffer(pc_value)
    pc_data.reset()
    pc_data.load()

    # サークルグリッドプロセス起動
    cp_proc = Process(target=circleproj.main, args=(cp_data,))

    # アプリケーションコントローラ起動
    app_proc = Process(target=appcontroller.main, args=(cp_data, pc_data,))

    # プロジェクターキャリブレーション起動
    pc_proc = Process(target=projectcalib.main, args=(pc_data,))

    # 各アプリ共有データブリッジ
    bridge_th = threading.Thread(
        target=dataBridge, args=(cp_data, pc_data))    

    # 各プロセススタート
    cp_proc.start()
    app_proc.start()
    pc_proc.start()
    bridge_th.start()

    # 各プロセス終了待ち
    cp_proc.join()
    app_proc.join()
    pc_proc.join()
    bridge_th.join()

    # 初期設定ファイルを保存
    cp_data.save()
    pc_data.save()


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
    except Exception:
        traceback.print_exc()