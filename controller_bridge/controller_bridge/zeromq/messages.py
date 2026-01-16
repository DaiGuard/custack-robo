from dataclasses import dataclass, field
import time

@dataclass
class Header:
    seq: int = 0
    stamp: float = field(default_factory=time.time)

@dataclass
class Vector3:
    x: float = 0.0
    y: float = 0.0
    z: float = 0.0

@dataclass
class Twist:
    linear: Vector3 = field(default_factory=Vector3)
    angular: Vector3 = field(default_factory=Vector3)

@dataclass
class TwistStamped:
    header: Header = field(default_factory=Header)
    twist: Twist = field(default_factory=Twist)
