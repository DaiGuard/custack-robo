import sys
import logging
import zmq
import time
import json
from typing import Tuple

logging.basicConfig(
    level=logging.INFO, stream=sys.stdout,
    format="%(levelname)s:[Subscriber] %(message)s")

class Subscriber:

    def __init__(self, ip: str, port: int):
        self.__context = zmq.Context()
        self.__socket = self.__context.socket(zmq.SUB)
        self.__socket.setsockopt(zmq.SUBSCRIBE, b"")
        self.__socket.setsockopt(zmq.RCVTIMEO, 30)
        self.__socket.connect(f"tcp://{ip}:{port}")


    def __del__(self):
        self.close()

    def subscribe(self) -> Tuple[str, dict]:
        try:
            if self.__socket is not None:
                [topic, msg] = self.__socket.recv_multipart()
                topic_name = topic.decode("utf-8")
                data = json.loads(msg.decode("utf-8"))
                return topic_name, data
            
            logging.error("not connecte server")
        except zmq.Again:
            logging.warning("no message sent within the timeout period.")
        except json.JSONDecodeError:
            logging.error("cannot decode json data")

        return None, None

    def close(self):
        if self.__socket is not None:
            self.__socket.close()
            self.__socket = None

        if self.__context is not None:
            self.__context.term()
            self.__context = None