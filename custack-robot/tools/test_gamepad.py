#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
PS5コントローラ (DualSense) 入力によるロボットシリアル制御スクリプト

BluetoothまたはUSB接続されたPS5コントローラの入力を読み取り、
M5Atom Matrix (robot_bridge) に対してシリアル SET コマンドを定期送信します。

【入力仕様】
- Lアナログスティック 上下 : Vx (前後進, 上方向で正 +Vx)
- Lアナログスティック 左右 : Vy (左右平行移動, 右方向で正 +Vy)
- Rアナログスティック 左右 : Omega (旋回速度, 右方向/時計回りで正 +Omega)
- L1ボタン                : LARM (左アーム起動, 押下中のみ 1, 離すと 0)
- R1ボタン                : RARM (右アーム起動, 押下中のみ 1, 離すと 0)

【シリアル通信仕様】
- SETコマンドフォーマット: SET,vx,vy,omega,arm_r,arm_l
- ボーレート: 115200 bps (デフォルト)
- ポート: /dev/ttyUSB0 (デフォルト)
- 送信周期: 50Hz (20ms周期)
"""

import os
import sys
import time
import signal
import argparse
from typing import Optional, Tuple, Dict, Any

# ヘッドレス環境 (ディスプレイサーバーなし) でも動作するようにSDLドライバを設定
if "DISPLAY" not in os.environ and "WAYLAND_DISPLAY" not in os.environ:
    os.environ["SDL_VIDEODRIVER"] = "dummy"

try:
    import pygame
except ImportError:
    print("エラー: 'pygame' モジュールが見つかりません。")
    print("以下のコマンドでインストールしてください:")
    print("  pip install pygame")
    sys.exit(1)

try:
    import serial
except ImportError:
    print("エラー: 'pyserial' モジュールが見つかりません。")
    print("以下のコマンドでインストールしてください:")
    print("  pip install pyserial")
    sys.exit(1)

# デフォルト設定
DEFAULT_SERIAL_PORT = "/dev/ttyUSB0"
DEFAULT_BAUD_RATE = 115200
DEFAULT_SEND_RATE_HZ = 50
DEFAULT_MAX_SPEED = 500       # Vx, Vy の最大値 (最大 1000)
DEFAULT_MAX_OMEGA = 500       # Omega の最大値 (最大 1000)
DEFAULT_DEADZONE = 0.08       # アナログスティックの不感帯

# PS5 コントローラ (DualSense on Linux/SDL2) の標準軸インデックス
DEFAULT_AXIS_VX = 1           # Axis 1: Lスティック上下 (Up: -1.0, Down: +1.0)
DEFAULT_AXIS_VY = 0           # Axis 0: Lスティック左右 (Left: -1.0, Right: +1.0)
DEFAULT_AXIS_OMEGA = 3        # Axis 3: Rスティック左右 (Left: -1.0, Right: +1.0) ※Axis 2はL2トリガー
DEFAULT_BTN_L1 = 4            # Button 4: L1バンパー
DEFAULT_BTN_R1 = 5            # Button 5: R1バンパー


def clamp(val: float, min_val: float, max_val: float) -> float:
    """値を指定した範囲内に制限します。"""
    return max(min_val, min(val, max_val))


def apply_deadzone(val: float, deadzone: float) -> float:
    """デッドゾーン（不感帯）を適用し、線形にスケールします。"""
    if abs(val) < deadzone:
        return 0.0
    sign = 1.0 if val > 0 else -1.0
    return sign * (abs(val) - deadzone) / (1.0 - deadzone)


class GamepadController:
    """PS5コントローラの入力を管理するクラス"""

    def __init__(self, deadzone: float = DEFAULT_DEADZONE):
        self.deadzone = deadzone
        self.joystick: Optional[pygame.joystick.Joystick] = None
        self._init_pygame()

    def _init_pygame(self):
        """Pygameおよびジョイスティックモジュールの初期化"""
        pygame.init()
        pygame.joystick.init()

    def connect(self) -> bool:
        """コントローラを検出して接続します。"""
        pygame.joystick.quit()
        pygame.joystick.init()
        
        count = pygame.joystick.get_count()
        if count == 0:
            return False

        self.joystick = pygame.joystick.Joystick(0)
        self.joystick.init()
        print(f"コントローラを検出しました: '{self.joystick.get_name()}'")
        print(f"  - 軸数 (Axes): {self.joystick.get_numaxes()}")
        print(f"  - ボタン数 (Buttons): {self.joystick.get_numbuttons()}")
        return True

    def get_inputs(self, max_speed: int, max_omega: int, 
                   axis_vx: int = DEFAULT_AXIS_VX,
                   axis_vy: int = DEFAULT_AXIS_VY,
                   axis_omega: int = DEFAULT_AXIS_OMEGA,
                   btn_l1: Optional[int] = None,
                   btn_r1: Optional[int] = None,
                   invert_vx: bool = False,
                   invert_vy: bool = False,
                   invert_omega: bool = False) -> Tuple[int, int, int, int, int, Dict[str, Any]]:
        """
        コントローラの入力値を読み取り、(vx, vy, omega, arm_r, arm_l, raw_data) を返します。
        
        戻り値:
            vx (int): -1000 〜 1000
            vy (int): -1000 〜 1000
            omega (int): -1000 〜 1000
            arm_r (int): 0 または 1 (R1ボタン押下時 1)
            arm_l (int): 0 または 1 (L1ボタン押下時 1)
            raw_data (dict): デバッグ用生データ
        """
        pygame.event.pump()
        if not self.joystick:
            return 0, 0, 0, 0, 0, {}

        num_axes = self.joystick.get_numaxes()
        num_buttons = self.joystick.get_numbuttons()

        # 全軸の生値取得
        axes = [self.joystick.get_axis(i) for i in range(num_axes)]
        buttons = [i for i in range(num_buttons) if self.joystick.get_button(i)]

        # スティック入力取得
        raw_vx = axes[axis_vx] if axis_vx < num_axes else 0.0
        raw_vy = axes[axis_vy] if axis_vy < num_axes else 0.0
        raw_omega = axes[axis_omega] if axis_omega < num_axes else 0.0

        # 通常、スティック上方向は負の値 (-1.0) になるため反転して上を正 (+Vx) にする
        # 引数 invert_vx でさらに反転可能
        scaled_vx = -raw_vx if not invert_vx else raw_vx
        scaled_vy = -raw_vy if invert_vy else raw_vy
        scaled_omega = -raw_omega if invert_omega else raw_omega

        # デッドゾーン処理
        val_vx = apply_deadzone(scaled_vx, self.deadzone)
        val_vy = apply_deadzone(scaled_vy, self.deadzone)
        val_omega = apply_deadzone(scaled_omega, self.deadzone)

        # 速度値の整数変換 (-1000 〜 1000 にクリップ)
        vx = int(clamp(val_vx * max_speed, -1000, 1000))
        vy = int(clamp(val_vy * max_speed, -1000, 1000))
        omega = int(clamp(val_omega * max_omega, -1000, 1000))

        # L1 / R1 ボタンの読み取り (押下中のみ 1)
        l1_idx = btn_l1 if btn_l1 is not None else DEFAULT_BTN_L1
        r1_idx = btn_r1 if btn_r1 is not None else DEFAULT_BTN_R1

        arm_l = 1 if (l1_idx < num_buttons and self.joystick.get_button(l1_idx)) else 0
        arm_r = 1 if (r1_idx < num_buttons and self.joystick.get_button(r1_idx)) else 0

        # デバッグ情報
        raw_data = {
            "axes": axes,
            "buttons": buttons,
            "raw_vx": raw_vx,
            "raw_vy": raw_vy,
            "raw_omega": raw_omega,
            "l1_pressed": bool(arm_l),
            "r1_pressed": bool(arm_r)
        }

        return vx, vy, omega, arm_r, arm_l, raw_data


def main():
    parser = argparse.ArgumentParser(
        description="PS5コントローラによるロボットシリアル制御スクリプト",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter
    )
    parser.add_argument('-p', '--port', type=str, default=DEFAULT_SERIAL_PORT,
                        help="シリアルポート名")
    parser.add_argument('-b', '--baud', type=int, default=DEFAULT_BAUD_RATE,
                        help="ボーレート")
    parser.add_argument('-r', '--rate', type=float, default=DEFAULT_SEND_RATE_HZ,
                        help="送信周期 (Hz)")
    parser.add_argument('--max-speed', type=int, default=DEFAULT_MAX_SPEED,
                        help="Vx, Vyの最大速度 (1〜1000)")
    parser.add_argument('--max-omega', type=int, default=DEFAULT_MAX_OMEGA,
                        help="Omegaの最大旋回速度 (1〜1000)")
    parser.add_argument('--deadzone', type=float, default=DEFAULT_DEADZONE,
                        help="アナログスティックの不感帯 (0.0〜0.5)")
    
    # 軸・ボタンカスタマイズ用オプション
    parser.add_argument('--axis-vx', type=int, default=DEFAULT_AXIS_VX,
                        help="Vx (Lスティック上下) の軸インデックス (PS5標準: 1)")
    parser.add_argument('--axis-vy', type=int, default=DEFAULT_AXIS_VY,
                        help="Vy (Lスティック左右) の軸インデックス (PS5標準: 0)")
    parser.add_argument('--axis-omega', type=int, default=DEFAULT_AXIS_OMEGA,
                        help="Omega (Rスティック左右) の軸インデックス (PS5標準: 3, ※Axis 2はL2トリガー)")
    parser.add_argument('--btn-l1', type=int, default=DEFAULT_BTN_L1,
                        help="L1ボタンのインデックス (PS5標準: 4)")
    parser.add_argument('--btn-r1', type=int, default=DEFAULT_BTN_R1,
                        help="R1ボタンのインデックス (PS5標準: 5)")
    parser.add_argument('--invert-vx', action='store_true',
                        help="Vxの進行方向を反転")
    parser.add_argument('--invert-vy', action='store_true',
                        help="Vyの進行方向を反転")
    parser.add_argument('--invert-omega', action='store_true',
                        help="Omegaの旋回方向を反転")
    
    # 動作モード
    parser.add_argument('--dry-run', action='store_true',
                        help="シリアル通信を行わずにSETコマンドの生成結果を表示")
    parser.add_argument('--debug', action='store_true',
                        help="全軸・ボタンの生入力値(printfデバッグ)をリアルタイム表示")

    args = parser.parse_args()

    # 最大速度制限
    args.max_speed = int(clamp(args.max_speed, 1, 1000))
    args.max_omega = int(clamp(args.max_omega, 1, 1000))
    interval = 1.0 / args.rate if args.rate > 0 else 0.02

    print("================================================================================")
    print("                 PS5 Gamepad Serial Controller (SET Command)                    ")
    print("================================================================================")
    print(f"送信レート   : {args.rate} Hz (間隔: {interval*1000:.1f} ms)")
    print(f"最大速度     : Vx/Vy: ±{args.max_speed}, Omega: ±{args.max_omega}")
    print(f"デッドゾーン : {args.deadzone}")
    print(f"軸割り当て   : Lスティック上下=Axis{args.axis_vx}(Vx), Lスティック左右=Axis{args.axis_vy}(Vy), Rスティック左右=Axis{args.axis_omega}(Omega)")
    print(f"ボタン割り当て: L1=Btn{args.btn_l1}(LARM), R1=Btn{args.btn_r1}(RARM)")
    print(f"動作モード   : {'[Dry-Run (シリアル送信なし)]' if args.dry_run else f'[Serial: {args.port} @ {args.baud}bps]'}")
    if args.debug:
        print("デバッグモード: ON (全軸・ボタンの生値をprintf表示)")
    print("--------------------------------------------------------------------------------")

    # ゲームパッド初期化
    controller = GamepadController(deadzone=args.deadzone)
    if not controller.connect():
        print("警告: コントローラが見つかりません。接続されるまで待機します...")
        while not controller.connect():
            time.sleep(1.0)

    # シリアル接続初期化 (Dry-Run以外)
    ser: Optional[serial.Serial] = None
    if not args.dry_run:
        try:
            ser = serial.Serial(args.port, args.baud, timeout=0.05)
            print(f"シリアルポート {args.port} に接続しました。")
            # マイコンの起動安定化待ち
            time.sleep(1.5)
        except serial.SerialException as e:
            print(f"エラー: シリアルポート {args.port} を開けませんでした。")
            print(f"詳細: {e}")
            print("ヒント: --dry-run オプションを使用するとシリアルなしで動作確認できます。")
            sys.exit(1)

    running = True

    def sig_handler(signum, frame):
        nonlocal running
        running = False

    signal.signal(signal.SIGINT, sig_handler)
    signal.signal(signal.SIGTERM, sig_handler)

    print("\n[制御開始] コントローラを操作してください。(Ctrl+C で安全停止)")
    print("Lスティック: 移動 (Vx/Vy) | Rスティック: 旋回 (Omega) | L1: LARM | R1: RARM\n")

    try:
        while running:
            loop_start = time.time()

            # 入力読み取り
            vx, vy, omega, arm_r, arm_l, raw_data = controller.get_inputs(
                max_speed=args.max_speed,
                max_omega=args.max_omega,
                axis_vx=args.axis_vx,
                axis_vy=args.axis_vy,
                axis_omega=args.axis_omega,
                btn_l1=args.btn_l1,
                btn_r1=args.btn_r1,
                invert_vx=args.invert_vx,
                invert_vy=args.invert_vy,
                invert_omega=args.invert_omega
            )

            # SETコマンド作成: SET,vx,vy,omega,arm_r,arm_l
            command_str = f"SET,{vx},{vy},{omega},{arm_r},{arm_l}"

            # コンソール出力 (デバッグモードまたは通常モード)
            if args.debug:
                axes = raw_data.get("axes", [])
                axes_str = " ".join([f"A{i}:{val:+0.2f}" for i, val in enumerate(axes)])
                btns = raw_data.get("buttons", [])
                btns_str = ",".join(map(str, btns)) if btns else "None"

                debug_line = (
                    f"\r[RAW] 軸:[{axes_str}] | 押下ボタン:[{btns_str:<5}] "
                    f"=> CMD: {command_str:<22}"
                )
                print(debug_line, end="", flush=True)
            else:
                status_line = (
                    f"\r送信: {command_str:<24} "
                    f"[Vx:{vx:+5d} Vy:{vy:+5d} W:{omega:+5d} | RARM:{arm_r} LARM:{arm_l}]"
                )
                print(status_line, end="", flush=True)

            # シリアル送信
            if ser and ser.is_open:
                ser.write((command_str + "\n").encode('ascii'))
                
                # 受信データがあれば確認 (ノンブロッキング)
                if ser.in_waiting > 0:
                    try:
                        res = ser.read(ser.in_waiting).decode('ascii', errors='ignore')
                        if res.strip():
                            print(f"\n[受信]: {res.strip()}")
                    except Exception:
                        pass

            # 周期制御 (送信レート維持)
            elapsed = time.time() - loop_start
            sleep_time = interval - elapsed
            if sleep_time > 0:
                time.sleep(sleep_time)

    except KeyboardInterrupt:
        pass
    finally:
        print("\n\n終了処理を実行中...")
        # 停止コマンド送信 (SET,0,0,0,0,0)
        if ser and ser.is_open:
            try:
                stop_cmd = "SET,0,0,0,0,0\n"
                for _ in range(3):
                    ser.write(stop_cmd.encode('ascii'))
                    time.sleep(0.02)
                print("安全停止コマンド (SET,0,0,0,0,0) を送信しました。")
            except Exception as e:
                print(f"停止コマンド送信時エラー: {e}")
            finally:
                ser.close()
                print("シリアルポートを閉じました。")

        pygame.quit()
        print("プログラムを終了しました。")


if __name__ == "__main__":
    main()
