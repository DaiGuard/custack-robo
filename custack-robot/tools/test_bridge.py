import serial
import time
import argparse

# シリアルポートのデフォルト設定
DEFAULT_SERIAL_PORT = "/dev/ttyUSB0"  # ご自身の環境に合わせて変更してください (Windowsなら "COM3" など)
DEFAULT_BAUD_RATE = 115200
DEFAULT_CMD_TYPE = "SET"

def main(port, baudrate, cmd_type):
    """
    シリアルポートに接続し、コマンドを送信して、応答を表示します。
    """
    ser = None
    try:
        # シリアルポートに接続
        ser = serial.Serial(port, baudrate, timeout=1.0)
        print(f"シリアルポート {port} (ボーレート: {baudrate}) に接続しました。")
        
        # M5Atomの起動を待機
        time.sleep(2)

        # コマンドリスト
        commands = []
        if cmd_type == "SET":
            commands = [
                "SET,200,0,0,0,0",
                "SET,-200,0,0,0,0",
                "SET,0,200,0,0,0",
                "SET,0,-200,0,0,0",
                "SET,0,0,200,0,0",
                "SET,0,0,-200,0,0",
            ]
        elif cmd_type == "TGT":
            commands = [
                "TGT,00:00:00:00:00:00",
                "TGT,3C:8A:1F:D7:49:C0",
            ]
        elif cmd_type == "STS":
            commands = [
                "STS"
            ]
        elif cmd_type == "VEL":
            commands = [
                "VEL,-100,-200,-300",
                "VEL,400,500,600",
                "VEL,2000,2000,2000",
                "VEL,-2000,-2000,-2000",
            ]
        elif cmd_type == "ARM":
            commands = [
                "ARM,0,0",
                "ARM,0,1",
                "ARM,1,0",
                "ARM,0,1",
                "ARM,1,0",
                "ARM,1,1",
                "ARM,0,0"
                # "ARM,0,0",
                # "ARM,1,0",
                # "ARM,0,1",
                # "ARM,10,10",
                # "ARM,-10,-10"
            ]
        elif cmd_type == "CAL":
            commands = [
                "CAL,0,4,40"
            ]
        

        # コマンドライン引数でコマンドが指定されている場合
        for command in commands:
            for i in range(100):
                # コマンドを送信
                ser.write((command + '\n').encode('ascii'))
                print(f"送信: {command}")

                # 返信を確認
                if ser.in_waiting > 0:
                    res = ser.read(ser.in_waiting)
                    print(f"受信: {res.decode('ascii')}")
                    
                time.sleep(0.010)

    except serial.SerialException as e:
        print(f"エラー: シリアルポート {port} を開けませんでした。")
        print(f"詳細: {e}")
        print("ポート名が正しいか、デバイスがPCに接続されているか確認してください。")

    except Exception as e:
        print(f"予期せぬエラーが発生しました: {e}")

    finally:
        # シリアルポートを閉じる
        if ser and ser.is_open:
            ser.close()
            print("シリアルポートを閉じました。")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="M5Atomシリアル通信テストブリッジ")
    parser.add_argument('-p', '--port', type=str, default=DEFAULT_SERIAL_PORT,
                        help=f"シリアルポート名 (デフォルト: {DEFAULT_SERIAL_PORT})")
    parser.add_argument('-b', '--baud', type=int, default=DEFAULT_BAUD_RATE,
                        help=f"ボーレート (デフォルト: {DEFAULT_BAUD_RATE})")
    parser.add_argument('-c', '--command', type=str, default=DEFAULT_CMD_TYPE,
                        help=f"コマンドタイプ (デフォルト: {DEFAULT_CMD_TYPE})")
    args = parser.parse_args()

    main(args.port, args.baud, args.command)