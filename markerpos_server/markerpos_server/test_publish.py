import sys
import logging
import argparse
import time
from .zeromq import PoseStampedPublisher, Pose

logging.basicConfig(
    level=logging.INFO, stream=sys.stdout,
    format="%(levelname)s:[test_publish] %(message)s")

def main():

    # 引数を設定
    parser = argparse.ArgumentParser(
        description="Test publisher software")
    
    parser.add_argument("-i", "--ip", type=str, default="localhost",
                        help="serer address")
    parser.add_argument("-p", "--port", type=int, default=5556,
                        help="port number")    
    args = parser.parse_args()

    publisher = PoseStampedPublisher(args.ip, args.port, "p1_pose")

    data = Pose()
    data.position.x = 1.0
    data.position.y = 2.0
    data.position.z = 3.0
    data.orientation.x = 4.0
    data.orientation.y = 5.0
    data.orientation.z = 6.0
    data.orientation.w = 1.0

    try:
        while True:
            logging.info(f"publish {time.time()}")
            publisher.publish(data)
            time.sleep(0.1)
    except KeyboardInterrupt:
        pass

    publisher.close()

