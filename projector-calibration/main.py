"""プロジェクタキャリブレーションツール."""
import traceback
from multiprocessing import Process, RawArray
import ctypes
# from multiprocessing.sharedctypes import Value
from circleproj import shm as cpshm
from circleproj import circleproj
from appcontroller import appcontroller


def main():
    """メイン関数."""
    # 共有データの確保
    cp_value = RawArray(ctypes.c_byte, ctypes.sizeof(cpshm.SharedMemData))
    cp_data = cpshm.SharedMemData.from_buffer(cp_value)
    cp_data.reset()

    # サークルグリッドプロセス起動
    cp_proc = Process(target=circleproj.main, args=(cp_data,))

    # アプリケーションコントローラ起動
    app_proc = Process(target=appcontroller.main, args=(cp_data,))

    cp_proc.start()
    app_proc.start()

    cp_proc.join()
    app_proc.join()




if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
    except Exception:
        traceback.print_exc()