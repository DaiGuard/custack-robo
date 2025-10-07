import sys
import logging
import argparse
import serial
import struct
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
    parser.add_argument("-t", "--topic", type=str, default="p1_cmdvel",
                        help="zeromq topic name")
    parser.add_argument("-p", "--port", type=str, default="/dev/ttyUSB0",
                        help="serial port name")
    args = parser.parse_args()

    # ZeroMQサブスクライバの準備
    sub = Subscriber(args.zmqip, args.zmqport)

    # シリアルポートの準備
    try:
        ser = serial.Serial(args.port, 115200, timeout=0.1)
    except serial.SerialException as e:
        logging.error("can not open serial port")
        return

    debug = [None, None, None]

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

            rw = data["right_weapon"]
            lw = data["left_weapon"]

            data_payload = struct.pack('<fffBB', x, y, w, rw, lw)
            data_length = len(data_payload)
            data_header = b'\xaa' + struct.pack('<B', data_length)
            checksum = 0
            for b in data_payload:
                checksum ^= b
            data_checksum = struct.pack('<B', checksum)

            data_bytes = data_header + data_payload + data_checksum

            if topic == args.topic:
                ser.write(data_bytes)
                debug[0] = data_bytes
            else:
                logging.warning("invalid topic")
                continue

            print(debug)

    except KeyboardInterrupt:
        pass

    # シリアルポートを閉じる
    ser.close()

    # サブスクライバを閉じる
    sub.close()

