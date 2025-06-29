from dataclasses import dataclass, field
import time

@dataclass
class Header:
    seq: int = 0
    stamp: float = time.time()

@dataclass
class Point:
    x: float = 0.0
    y: float = 0.0
    z: float = 0.0

@dataclass
class Quaternion:
    x: float = 0.0
    y: float = 0.0
    z: float = 0.0
    w: float = 1.0

@dataclass
class Pose:
    position: Point = Point()
    orientation: Quaternion = Quaternion()

@dataclass
class PoseStamped:
    header: Header = Header()
    pose: Pose = Pose()

