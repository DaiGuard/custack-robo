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
    parser.add_argument("-t1", "--topic1", type=str, default="p1_cmdvel",
                        help="zeromq topic name")
    parser.add_argument("-t2", "--topic2", type=str, default="p2_cmdvel",
                        help="zeromq topic name")    
    parser.add_argument("-t3", "--topic3", type=str, default="p3_cmdvel",
                        help="zeromq topic name")    
    parser.add_argument("-p1", "--port1", type=str, default="/dev/ttyUSB0",
                        help="serial port 1 name")
    parser.add_argument("-p2", "--port2", type=str, default="/dev/ttyUSB1",
                        help="serial port 2 name")
    parser.add_argument("-p3", "--port3", type=str, default="/dev/ttyUSB2",
                        help="serial port 3 name")
    args = parser.parse_args()

    # ZeroMQサブスクライバの準備
    sub = Subscriber(args.zmqip, args.zmqport)

    # シリアルポートの準備
    try:
        ser1 = serial.Serial(args.port1, 115200, timeout=0.1)
        ser2 = serial.Serial(args.port2, 115200, timeout=0.1)
        ser3 = serial.Serial(args.port3, 115200, timeout=0.1)
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

            data_payload = struct.pack('<fff', x, y, w)
            data_length = len(data_payload)
            data_header = b'\xaa' + struct.pack('<B', data_length)
            checksum = 0
            for b in data_payload:
                checksum ^= b
            data_checksum = struct.pack('<B', checksum)

            data_bytes = data_header + data_payload + data_checksum

            if topic == args.topic1:
                ser1.write(data_bytes)
                debug[0] = data_bytes
            elif topic == args.topic2:
                ser2.write(data_bytes)
                debug[1] = data_bytes
            elif topic == args.topic3:
                ser3.write(data_bytes)
                debug[2] = data_bytes
            else:
                logging.warning("invalid topic")
                continue

            print(debug)

    except KeyboardInterrupt:
        pass

    # シリアルポートを閉じる
    ser1.close()
    ser2.close()
    ser3.close()

    # サブスクライバを閉じる
    sub.close()

