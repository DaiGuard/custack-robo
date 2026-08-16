#!/usr/bin/env python3
"""
CuStack-Robo: robot_bridge (M5Atom Matrix) シリアル通信テストツール

使用例:
  python test_bridge.py -c "PIN" -p /dev/ttyUSB0 -b 115200 -n 1
  python test_bridge.py -c "TLM" -n 5
  python test_bridge.py -c "SET,200,0,0,1,0" -n 10
  python test_bridge.py -c "STS"
  python test_bridge.py -c "STP"
  python test_bridge.py -c "MAC"
  python test_bridge.py -c "TGT,3C:8A:1F:D7:49:C0"
  python test_bridge.py -c ALL
"""

import sys
import time
import argparse
import serial

# デフォルト設定
DEFAULT_SERIAL_PORT = "/dev/ttyUSB0"
DEFAULT_BAUD_RATE = 115200
DEFAULT_COUNT = 1
DEFAULT_INTERVAL = 0.05  # 50ms周期 (20Hz)

# プリセットコマンド群
PRESETS = {
    "PIN": ["PIN"],
    "MAC": ["MAC"],
    "TLM": ["TLM"],
    "STS": ["STS"],
    "STP": ["STP"],
    "SET": [
        "SET,200,0,0,0,0",
        "SET,-200,0,0,0,0",
        "SET,0,200,0,0,0",
        "SET,0,-200,0,0,0",
        "SET,0,0,200,0,0",
        "SET,0,0,-200,0,0",
        "SET,0,0,0,1,0",
        "SET,0,0,0,0,1",
        "SET,0,0,0,0,0",
    ],
    "VEL": [
        "VEL,200,0,0",
        "VEL,-200,0,0",
        "VEL,0,200,0",
        "VEL,0,-200,0",
        "VEL,0,0,200",
        "VEL,0,0,0",
    ],
    "ARM": [
        "ARM,1,0",
        "ARM,0,1",
        "ARM,1,1",
        "ARM,0,0",
    ],
    "TGT": [
        "TGT,3C:8A:1F:D7:49:C0",
    ],
    "ALL": [
        "PIN",
        "MAC",
        "STS",
        "TLM",
        "SET,100,0,0,0,0",
        "STS",
        "SET,0,0,0,1,0",
        "STS",
        "STP",
        "STS",
    ]
}


def parse_and_print_response(raw_line: str):
    """
    受信したレスポンス文字列を解析し、分かりやすく表示する。
    """
    line = raw_line.strip()
    if not line:
        return

    if line == "PON":
        print(f"  └─ 🟢 [PING応答] PON (Ping OK)")
    elif line.startswith("MAC,"):
        parts = line.split(",")
        own_mac = parts[1] if len(parts) > 1 else "?"
        tgt_mac = parts[2] if len(parts) > 2 else "?"
        print(f"  └─ 📡 [MAC情報] 自機(Bridge): {own_mac} | 宛先(Core2): {tgt_mac}")
    elif line.startswith("TLM,"):
        parts = line.split(",")
        if len(parts) >= 5:
            leg_id = parts[1]
            rarm_id = parts[2]
            larm_id = parts[3]
            bat_mv = parts[4]
            print(f"  └─ 🔋 [テレメトリ] 脚ID: 0x{int(leg_id):02X} | 右腕ID: 0x{int(rarm_id):02X} | 左腕ID: 0x{int(larm_id):02X} | 電圧: {bat_mv} mV")
        else:
            print(f"  └─ 🔋 [テレメトリ] {line}")
    elif line.startswith("STS,"):
        parts = line.split(",")
        if len(parts) >= 9:
            s_st = "OK" if parts[1] == "1" else "FAIL"
            e_st = "OK" if parts[2] == "1" else "DISCONN"
            vx, vy, omega = parts[3], parts[4], parts[5]
            rarm, larm = parts[6], parts[7]
            wd = parts[8]
            print(f"  └─ 📊 [ステータス] Serial:{s_st} | ESP-NOW:{e_st} | V=({vx},{vy},{omega}) | Arm=({rarm},{larm}) | WD:{wd}")
        else:
            print(f"  └─ 📊 [ステータス] {line}")
    elif line.startswith("TGT,"):
        parts = line.split(",")
        tgt = parts[1] if len(parts) > 1 else line
        print(f"  └─ 🎯 [宛先MAC設定] 登録完了: {tgt}")
    elif line.startswith("[E]:"):
        print(f"  └─ ❌ [ERROR] {line[4:]}")
    elif line.startswith("[W]:"):
        print(f"  └─ ⚠️  [WARN] {line[4:]}")
    elif line.startswith("[I]:"):
        print(f"  └─ ℹ️  [INFO] {line[4:]}")
    elif line.startswith("[D]:"):
        print(f"  └─ 🔍 [DEBUG] {line[4:]}")
    else:
        print(f"  └─ 📥 [受信] {line}")


def run_bridge_test(port: str, baud: int, command_input: str, count: int, interval: float):
    """
    シリアルポートを開き、コマンドを指定回数送信して応答を表示する。
    """
    ser = None
    try:
        ser = serial.Serial(port, baud, timeout=0.2)
        print("=" * 60)
        print(f"  CuStack-Robo Bridge Serial Test Tool")
        print(f"  ポート: {port} | ボーレート: {baud} | 繰返: {count}回")
        print("=" * 60)

        # M5Atomのリセット待機・バッファクリア
        time.sleep(1.0)
        ser.reset_input_buffer()
        ser.reset_output_buffer()

        # コマンド列の展開 (プリセット名なら展開、それ以外は直接文字列として使用)
        cmd_upper = command_input.strip().upper()
        if cmd_upper in PRESETS:
            commands_to_send = PRESETS[cmd_upper]
        else:
            commands_to_send = [command_input.strip()]

        total_loops = count if count > 0 else 999999999
        loop_idx = 0

        while loop_idx < total_loops:
            loop_idx += 1
            if count > 1 or count <= 0:
                print(f"\n--- [Cycle {loop_idx}/{count if count > 0 else '∞'}] ---")

            for cmd in commands_to_send:
                # コマンド送信 (改行 \n を付与)
                send_data = (cmd + "\n").encode("ascii")
                ser.write(send_data)
                ser.flush()
                print(f"📤 [送信] {cmd}")

                # 応答受信 (少し待機して読み取り)
                time.sleep(interval)
                while ser.in_waiting > 0:
                    try:
                        raw_line = ser.readline().decode("ascii", errors="replace")
                        parse_and_print_response(raw_line)
                    except Exception as ex:
                        print(f"  └─ ⚠️ 受信デコードエラー: {ex}")

        print("\n" + "=" * 60)
        print("  テスト完了")
        print("=" * 60)

    except serial.SerialException as e:
        print(f"\n❌ エラー: シリアルポート {port} を開けませんでした。")
        print(f"   詳細: {e}")
        print("   ※ ポート名が正しいか、ケーブルが接続されているか確認してください。")
        sys.exit(1)

    except KeyboardInterrupt:
        print("\n\n⏹️ ユーザー操作によりテストを中断しました。")

    except Exception as e:
        print(f"\n❌ 予期せぬエラーが発生しました: {e}")
        sys.exit(1)

    finally:
        if ser and ser.is_open:
            ser.close()
            print("🔒 シリアルポートを閉じました。")


def main():
    parser = argparse.ArgumentParser(
        description="M5Atom (robot_bridge) シリアル通信テストスクリプト",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
対応コマンド例:
  -c "PIN"                   : 死活確認 (PON応答)
  -c "MAC"                   : 自身および宛先のMACアドレス取得
  -c "TLM"                   : 実機テレメトリ取得 (脚/腕ID, バッテリー電圧)
  -c "STS"                   : 通信ステータスおよび速度状態取得
  -c "SET,200,0,0,1,0"       : 速度(Vx,Vy,Omega)および左右アーム起動
  -c "VEL,100,200,0"         : 移動速度設定
  -c "ARM,1,0"               : アーム起動設定 (右1, 左0)
  -c "STP"                   : 全停止
  -c "TGT,3C:8A:1F:D7:49:C0" : 送信先Core2のMACアドレス更新
  -c "ALL"                   : 全コマンド総合シーケンステスト

実行例:
  python test_bridge.py -c "PIN" -n 1 -p /dev/ttyUSB0 -b 115200
  python test_bridge.py -c "TLM" -n 10 -p /dev/ttyUSB0
  python test_bridge.py -c "SET,200,0,0,1,0" -n 50
  python test_bridge.py -c ALL -n 1
        """
    )

    parser.add_argument("-c", "--command", type=str, required=True,
                        help="送信コマンド文字列 (例: 'PIN', 'TLM', 'SET,100,0,0,1,0', 'STS', 'MAC', 'STP', 'ALL' 等)")
    parser.add_argument("-n", "--count", type=int, default=DEFAULT_COUNT,
                        help=f"繰返回数 (デフォルト: {DEFAULT_COUNT}回, 0で無限ループ)")
    parser.add_argument("-p", "--port", type=str, default=DEFAULT_SERIAL_PORT,
                        help=f"シリアルポート名 (デフォルト: {DEFAULT_SERIAL_PORT})")
    parser.add_argument("-b", "--baud", type=int, default=DEFAULT_BAUD_RATE,
                        help=f"ボーレート (デフォルト: {DEFAULT_BAUD_RATE})")
    parser.add_argument("-i", "--interval", type=float, default=DEFAULT_INTERVAL,
                        help=f"送信・受信ポーリング間隔 [秒] (デフォルト: {DEFAULT_INTERVAL}s)")

    args = parser.parse_args()

    run_bridge_test(
        port=args.port,
        baud=args.baud,
        command_input=args.command,
        count=args.count,
        interval=args.interval
    )


if __name__ == "__main__":
    main()