import sys
import logging
import zmq
import time
import json


logging.basicConfig(
    level=logging.INFO, stream=sys.stdout,
    format="%(levelname)s:[Publisher] %(message)s")

class Publisher:

    def __init__(self, ip: str, port: int, topic: str):
        self.__context = zmq.Context()
        self.__socket = self.__context.socket(zmq.PUB)
        self.__socket.setsockopt(zmq.SNDTIMEO, 30)
        self.__socket.connect(f"tcp://{ip}:{port}")

        self.__topic = topic.encode("utf-8")

    def __del__(self):
        self.close()

    def publish(self, data: dict) -> bool:
        try:
            # JSON文字列のデコード
            byte_data = json.dumps(data).encode("utf-8")

            # メッセージ送信
            if self.__socket is not None:
                self.__socket.send_multipart([
                    self.__topic,
                    byte_data
                ])

                return True
            
            logging.error("not connecte server")
            return False
        except zmq.Again:
            logging.warning("no message sent within the timeout period.")
        except json.JSONDecodeError:
            logging.error("cannot decode json data")

        return False
    
    def close(self):
        if self.__socket is not None:
            self.__socket.close()
            self.__socket = None            

        if self.__context is not None:
            self.__context.term()
            self.__context = None