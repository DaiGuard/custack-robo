from .publisher import Publisher
from .messages import PoseStamped, Header, Pose

from dataclasses import asdict
import time

class PoseStampedPublisher(Publisher):

    def __init__(self, ip: str, port: int, topic: str):
        super().__init__(ip, port, topic)

        self.__header = Header()
        self.__data = PoseStamped()

    def publish(self, pose: Pose) -> bool:
        # ヘッダーデータの更新
        self.__header.seq += 1
        self.__header.stamp = time.time()

        self.__data.pose = pose
        self.__data.header = self.__header

        return super().publish(asdict(self.__data))
