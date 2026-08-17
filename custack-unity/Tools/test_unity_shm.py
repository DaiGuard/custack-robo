#!/usr/bin/env python3
"""
CuStack-Robo Unity 共有メモリ & デバイスID デバッグ・テストツール
Unity (custack-unity) との間で POSIX 共有メモリを介した双方向通信をシミュレート・検証します。

機能:
  1. ロボット位置姿勢・デバイスID (Leg/ArmR/ArmL) の共有メモリ書き込みモック
  2. Unity から送信される操作コマンド (/dev/shm/custack_controller_cmd) のリアルタイム受信モニター
  3. 双方向シミュレーション & インタラクティブなデバイスID切替

使用方法:
  # 1. ロボット位置姿勢 & デバイスID を共有メモリへ送信 (Unity 側の受信テスト)
  python3 test_unity_shm.py --mode pose

  # 2. Unity からの操作コマンドを受信・表示 (Unity 側の送信テスト)
  python3 test_unity_shm.py --mode cmd

  # 3. 双方向統合テスト (位置・デバイスID送信 + 操作コマンド受信)
  python3 test_unity_shm.py --mode full
"""

import os
import sys
import time
import math
import struct
import argparse

# --- 共有メモリ構造体定義 (C++ / C# と Pack=1 で完全一致) ---
MAX_SHARED_ROBOTS = 32
MAX_SHARED_CONTROLLERS = 8

# SharedRobotPose: id(int32), x(float), y(float), theta(float), leg_id(uint8), arm_r(uint8), arm_l(uint8), status(uint8) = 20 bytes
POSE_STRUCT_FMT = "<ifffBBBB"
POSE_STRUCT_SIZE = struct.calcsize(POSE_STRUCT_FMT)  # 20 bytes

# SharedRobotPoseData: version(uint32), sequence(uint32), timestamp_ns(uint64), count(uint32), poses[32*20] = 660 bytes
HEADER_POSE_FMT = "<IIQI"
HEADER_POSE_SIZE = struct.calcsize(HEADER_POSE_FMT)  # 20 bytes
TOTAL_POSE_DATA_SIZE = HEADER_POSE_SIZE + (POSE_STRUCT_SIZE * MAX_SHARED_ROBOTS)  # 660 bytes

# SingleControllerCommand: vx(int16), vy(int16), omega(int16), arm_r(uint8), arm_l(uint8), active(uint8), reserved(uint8) = 10 bytes
CMD_STRUCT_FMT = "<hhhBBBB"
CMD_STRUCT_SIZE = struct.calcsize(CMD_STRUCT_FMT)  # 10 bytes

# SharedControllerData: version(uint32), sequence(uint32), timestamp_ms(uint64), count(uint32), controllers[8*10] = 100 bytes
HEADER_CMD_FMT = "<IIQI"
HEADER_CMD_SIZE = struct.calcsize(HEADER_CMD_FMT)  # 20 bytes
TOTAL_CMD_DATA_SIZE = HEADER_CMD_SIZE + (CMD_STRUCT_SIZE * MAX_SHARED_CONTROLLERS)  # 100 bytes

# デバイスID名称定義
LEG_NAMES = {0: "None", 1: "Omni (オムニ)", 2: "Tire (二輪差動)", 3: "Crawler (キャタピラ)"}
ARM_NAMES = {0: "None", 1: "Gatling (ガトリング)", 2: "Sword (ソード)", 3: "Cannon (キャノン)"}


def get_shm_path(name: str) -> str:
    """Linux /dev/shm パスを解決"""
    if os.path.exists("/dev/shm"):
        return f"/dev/shm/{name}"
    # フォールバック (非Linux環境等)
    tmp_path = f"/tmp/{name}"
    return tmp_path


class ShmPoseWriter:
    """POSIX 共有メモリへの RobotPoseData 書き込みクラス (Seqlock 方式)"""

    def __init__(self, shm_path: str):
        self.shm_path = shm_path
        self.sequence = 0
        self.fd = os.open(self.shm_path, os.O_RDWR | os.O_CREAT, 0o666)
        # ファイルサイズを 660 bytes に設定
        os.ftruncate(self.fd, TOTAL_POSE_DATA_SIZE)
        print(f"[ShmPoseWriter] Opened SHM: {self.shm_path} ({TOTAL_POSE_DATA_SIZE} bytes)")

    def write_poses(self, robot_list: list):
        """
        robot_list: [{'id': 0, 'x': 0.0, 'y': 0.0, 'theta': 0.0, 'leg_id': 1, 'arm_r': 1, 'arm_l': 2, 'status': 1}, ...]
        """
        count = min(len(robot_list), MAX_SHARED_ROBOTS)
        timestamp_ns = int(time.time() * 1e9)

        # Seqlock: 書き込み開始 (奇数)
        self.sequence += 1

        # poses バッファ構築
        poses_bytes = bytearray()
        for r in robot_list[:count]:
            p_bytes = struct.pack(
                POSE_STRUCT_FMT,
                int(r.get('id', 0)),
                float(r.get('x', 0.0)),
                float(r.get('y', 0.0)),
                float(r.get('theta', 0.0)),
                int(r.get('leg_id', 1)),
                int(r.get('arm_r', 1)),
                int(r.get('arm_l', 1)),
                int(r.get('status', 1))
            )
            poses_bytes.extend(p_bytes)

        # 残りのスロットをゼロパディング
        for _ in range(count, MAX_SHARED_ROBOTS):
            poses_bytes.extend(b'\x00' * POSE_STRUCT_SIZE)

        # Seqlock: 書き込み完了 (偶数)
        self.sequence += 1
        header_bytes = struct.pack(HEADER_POSE_FMT, 1, self.sequence, timestamp_ns, count)

        full_data = header_bytes + poses_bytes

        # SHM 書き込み
        os.lseek(self.fd, 0, os.SEEK_SET)
        os.write(self.fd, full_data)

    def close(self):
        if self.fd is not None:
            os.close(self.fd)
            self.fd = None


class ShmCmdReader:
    """POSIX 共有メモリからの ControllerData 読み取りクラス (Seqlock 方式)"""

    def __init__(self, shm_path: str):
        self.shm_path = shm_path
        self.fd = None
        self.try_open()

    def try_open(self) -> bool:
        if self.fd is not None:
            return True
        if os.path.exists(self.shm_path):
            try:
                self.fd = os.open(self.shm_path, os.O_RDONLY)
                print(f"[ShmCmdReader] Connected to SHM: {self.shm_path}")
                return True
            except Exception as e:
                return False
        return False

    def read_commands(self):
        """
        コントローラーコマンドを Seqlock で読み取り
        戻り値: (count, [cmd0, cmd1, ...], timestamp_ms) または None
        """
        if self.fd is None and not self.try_open():
            return None

        for _ in range(5):
            try:
                os.lseek(self.fd, 0, os.SEEK_SET)
                raw = os.read(self.fd, TOTAL_CMD_DATA_SIZE)
                if len(raw) < TOTAL_CMD_DATA_SIZE:
                    return None

                version, seq, ts_ms, count = struct.unpack_from(HEADER_CMD_FMT, raw, 0)
                if seq == 0 or (seq & 1) != 0:
                    continue  # 書き込み中

                commands = []
                offset = HEADER_CMD_SIZE
                for i in range(min(count, MAX_SHARED_CONTROLLERS)):
                    vx, vy, omega, arm_r, arm_l, active, reserved = struct.unpack_from(CMD_STRUCT_FMT, raw, offset)
                    commands.append({
                        'index': i,
                        'vx': vx,
                        'vy': vy,
                        'omega': omega,
                        'arm_r': arm_r,
                        'arm_l': arm_l,
                        'active': active
                    })
                    offset += CMD_STRUCT_SIZE

                # 読み取り完了後に sequence を再確認
                version2, seq2, _, _ = struct.unpack_from(HEADER_CMD_FMT, raw, 0)
                if seq == seq2:
                    return count, commands, ts_ms
            except Exception:
                pass
        return None

    def close(self):
        if self.fd is not None:
            os.close(self.fd)
            self.fd = None


def run_pose_publisher(rate_hz: float = 60.0, num_robots: int = 2, auto_switch_equip: bool = True):
    """ダミーロボットの座標とデバイスIDを 60Hz で共有メモリへ送信"""
    pose_shm = get_shm_path("custack_robot_poses")
    writer = ShmPoseWriter(pose_shm)

    print("\n========================================================")
    print(" 🚀 CuStack Robot Pose & DeviceID Mock Publisher")
    print(f"  - SHM Path     : {pose_shm}")
    print(f"  - Rate         : {rate_hz} Hz")
    print(f"  - Robots Count : {num_robots} 台 (P1 / P2)")
    print(f"  - Auto Equip   : {'有効 (5秒毎に装備切替)' if auto_switch_equip else '固定'}")
    print("========================================================\n")
    print("💡 Unity で Main.unity または SharedMemorySandbox.unity を起動して確認してください。")
    print("   [Ctrl+C] で終了します。\n")

    t_start = time.time()
    equip_index = 0
    equip_presets = [
        # (leg_id, arm_r_id, arm_l_id)
        (1, 1, 2),  # Omni, Gatling, Sword
        (2, 3, 1),  # Tire, Cannon, Gatling
        (3, 2, 3),  # Crawler, Sword, Cannon
        (1, 3, 3),  # Omni, Cannon, Cannon
    ]

    last_equip_switch = time.time()

    try:
        while True:
            t = time.time() - t_start
            now = time.time()

            # 5秒毎に装備を巡回切替
            if auto_switch_equip and (now - last_equip_switch > 5.0):
                equip_index = (equip_index + 1) % len(equip_presets)
                last_equip_switch = now
                leg, ar, al = equip_presets[equip_index]
                print(f"🔄 [Equip Switched] -> Leg: 0x{leg:02X} ({LEG_NAMES.get(leg)}), "
                      f"ArmR: 0x{ar:02X} ({ARM_NAMES.get(ar)}), ArmL: 0x{al:02X} ({ARM_NAMES.get(al)})")

            leg_p1, ar_p1, al_p1 = equip_presets[equip_index]
            # P2 は異なる装備構成
            leg_p2, ar_p2, al_p2 = equip_presets[(equip_index + 1) % len(equip_presets)]

            # ロボット 1 (P1): 左側で円運動
            r0_x = -0.4 + 0.25 * math.cos(t * 0.8)
            r0_y = 0.0 + 0.25 * math.sin(t * 0.8)
            r0_th = (t * 0.8) + (math.pi / 2.0)

            # ロボット 2 (P2): 右側で 8の字運動
            r1_x = 0.4 + 0.25 * math.sin(t * 1.0)
            r1_y = 0.0 + 0.25 * math.sin(t * 2.0) * 0.5
            r1_th = -(t * 1.0)

            robots = [
                {'id': 0, 'x': r0_x, 'y': r0_y, 'theta': r0_th, 'leg_id': leg_p1, 'arm_r': ar_p1, 'arm_l': al_p1, 'status': 1},
                {'id': 1, 'x': r1_x, 'y': r1_y, 'theta': r1_th, 'leg_id': leg_p2, 'arm_r': ar_p2, 'arm_l': al_p2, 'status': 1},
            ]

            if num_robots >= 3:
                # ロボット 3 (P3)
                r2_x = 0.0
                r2_y = 0.5 + 0.2 * math.cos(t * 1.2)
                r2_th = 0.0
                robots.append({'id': 2, 'x': r2_x, 'y': r2_y, 'theta': r2_th, 'leg_id': 3, 'arm_r': 1, 'arm_l': 1, 'status': 1})

            writer.write_poses(robots)
            time.sleep(1.0 / rate_hz)

    except KeyboardInterrupt:
        print("\n🛑 Stopped Mock Publisher.")
    finally:
        writer.close()


def run_cmd_monitor(rate_hz: float = 20.0):
    """Unity から書き込まれたコントローラー操作コマンドを受信・表示"""
    cmd_shm = get_shm_path("custack_controller_cmd")
    reader = ShmCmdReader(cmd_shm)

    print("\n========================================================")
    print(" 🎮 CuStack Controller Command Monitor (Unity -> Host)")
    print(f"  - SHM Path : {cmd_shm}")
    print("========================================================\n")
    print("⏳ Unity からのコマンド書き込みを待機中... (Game を Play してキーボード/Gamepadを操作してください)")

    last_print = 0
    try:
        while True:
            result = reader.read_commands()
            now = time.time()
            if result is not None:
                count, cmds, ts_ms = result
                if now - last_print >= (1.0 / rate_hz):
                    last_print = now
                    output_lines = [f"⏱️ {ts_ms}ms (Robots: {count})"]
                    for cmd in cmds:
                        idx = cmd['index']
                        vx = cmd['vx']
                        vy = cmd['vy']
                        om = cmd['omega']
                        ar = cmd['arm_r']
                        al = cmd['arm_l']
                        act = cmd['active']
                        line = f"[P{idx+1}] Act={act} Vx={vx:+5d} Vy={vy:+5d} Ω={om:+5d} R={'🔥' if ar else '-'} L={'🔥' if al else '-'}"
                        output_lines.append(line)
                    print("\r" + " | ".join(output_lines), end="", flush=True)

            time.sleep(1.0 / (rate_hz * 2))
    except KeyboardInterrupt:
        print("\n🛑 Stopped Command Monitor.")
    finally:
        reader.close()


def run_full_duplex():
    """双方向統合テスト (バックグラウンド送信 + 受信表示)"""
    import threading

    pose_shm = get_shm_path("custack_robot_poses")
    cmd_shm = get_shm_path("custack_controller_cmd")
    writer = ShmPoseWriter(pose_shm)
    reader = ShmCmdReader(cmd_shm)

    print("\n========================================================")
    print(" 🔄 CuStack Unity Full-Duplex SHM Tester")
    print(f"  - Pose SHM : {pose_shm} (60Hz TX)")
    print(f"  - Cmd  SHM : {cmd_shm} (RX Monitor)")
    print("========================================================\n")

    running = True

    def tx_loop():
        t_start = time.time()
        while running:
            t = time.time() - t_start
            # 2台のロボット
            r0_x = -0.3 + 0.2 * math.cos(t)
            r0_y = 0.0 + 0.2 * math.sin(t)
            r0_th = t

            r1_x = 0.3 + 0.2 * math.sin(t * 1.2)
            r1_y = 0.0 + 0.2 * math.cos(t * 1.2)
            r1_th = -t

            robots = [
                {'id': 0, 'x': r0_x, 'y': r0_y, 'theta': r0_th, 'leg_id': 1, 'arm_r': 1, 'arm_l': 2, 'status': 1},
                {'id': 1, 'x': r1_x, 'y': r1_y, 'theta': r1_th, 'leg_id': 2, 'arm_r': 3, 'arm_l': 1, 'status': 1},
            ]
            writer.write_poses(robots)
            time.sleep(1.0 / 60.0)

    tx_thread = threading.Thread(target=tx_loop, daemon=True)
    tx_thread.start()

    print("✅ Pose publisher running in background.")
    print("🕹️ Reading controller commands from Unity...\n")

    try:
        while True:
            res = reader.read_commands()
            if res:
                count, cmds, ts = res
                for c in cmds:
                    if c['active'] or abs(c['vx']) > 10 or abs(c['vy']) > 10 or abs(c['omega']) > 10 or c['arm_r'] or c['arm_l']:
                        print(f"[CMD] P{c['index']+1}: Vx={c['vx']:+4d}, Vy={c['vy']:+4d}, Ω={c['omega']:+4d}, ArmR={c['arm_r']}, ArmL={c['arm_l']}")
            time.sleep(0.05)
    except KeyboardInterrupt:
        running = False
        print("\n🛑 Terminated.")
    finally:
        writer.close()
        reader.close()


def main():
    parser = argparse.ArgumentParser(description="CuStack-Robo Unity 共有メモリ & デバイスID テストツール")
    parser.add_argument("--mode", choices=["pose", "cmd", "full"], default="pose",
                        help="テストモード: pose (ロボット座標・デバイスID送信), cmd (Unityコマンド受信), full (双方向)")
    parser.add_argument("--rate", type=float, default=60.0, help="Pose 送信レート (Hz)")
    parser.add_argument("--robots", type=int, default=2, help="シミュレート機体数 (1〜3)")
    parser.add_argument("--no-auto-equip", action="store_true", help="装備の自動巡回切替を無効化")

    args = parser.parse_args()

    if args.mode == "pose":
        run_pose_publisher(rate_hz=args.rate, num_robots=args.robots, auto_switch_equip=not args.no_auto_equip)
    elif args.mode == "cmd":
        run_cmd_monitor()
    elif args.mode == "full":
        run_full_duplex()


if __name__ == "__main__":
    main()
