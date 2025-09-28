import sys
import logging
import argparse
import serial
from .zeromq import Subscriber


logging.basicConfig(
    level=logging.INFO, stream=sys.stdout,
    format="%(levelname)s:[controller_bridge] %(message)s")


def main():

    # 引数を設定
    parser = argparse.ArgumentParser(
        description="Controller bridge software")

    parser.add_argument("-zi", "--zmqip", type=str, default="localhost",
                        help="zeromq server address")
    parser.add_argument("-zp", "--zmqport", type=int, default=5555,
                        help="zeromq server port number")
    parser.add_argument("-t1", "--topic1", type=str, default="p1_cmdvel",
                        help="zeromq topic name")
    parser.add_argument("-t2", "--topic2", type=str, default="p2_cmdvel",
                        help="zeromq topic name")    
    parser.add_argument("-p1", "--port1", type=str, default="/dev/ttyUSB0",
                        help="serial port 1 name")
    parser.add_argument("-p2", "--port2", type=str, default="/dev/ttyUSB1",
                        help="serial port 2 name")
    args = parser.parse_args()

    # ZeroMQサブスクライバの準備
    sub = Subscriber(args.zmqip, args.zmqport)

    # シリアルポートの準備
    try:
        ser1 = serial.Serial(args.port1, 115200, timeout=0.1)
        # ser2 = serial.Serial(args.port2, 115200, timeout=0.1)
    except serial.SerialException as e:
        logging.error("can not open serial port")
        return

    try:
        while True:
            topic, data = sub.subscribe()
            if topic is None or data is None:
                logging.warning("no message")
                continue
            if "twist" not in data:
                logging.warning("invalid message")
                continue
            
            x = data["twist"]["linear"]["x"]
            y = data["twist"]["linear"]["y"]
            w = data["twist"]["angular"]["z"]

            sx = f"{x:03.2f}"
            sy = f"{y:03.2f}"
            sw = f"{w:03.2f}"

            print(sx, sy, sw)

            if topic == args.topic1:
                pass
            elif topic == args.topic2:
                pass

    except KeyboardInterrupt:
        pass

    # シリアルポートを閉じる
    ser1.close()
    # ser2.close()

    # サブスクライバを閉じる
    sub.close()

