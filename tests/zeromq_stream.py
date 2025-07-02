import zmq
import time
import json
import numpy as np
import quaternion

def main():
    context = zmq.Context()

    subscriber = context.socket(zmq.SUB)
    subscriber.setsockopt(zmq.SUBSCRIBE, b"")
    subscriber.setsockopt(zmq.RCVTIMEO, 30)
    subscriber.connect("tcp://localhost:5555")

    publisher = context.socket(zmq.PUB)
    publisher.setsockopt(zmq.SNDTIMEO, 30)
    # publisher.setsockopt(zmq.SNDHWM, 10)
    publisher.connect("tcp://localhost:5556")


    sub_data = {}

    pub_topic = "p1_pose"
    pub_data = {
        "header": {
            "seq": 0,
            "stamp": 0.0
        },
        "pose": {
            "position": {
                "x": -0.5,
                "y": 0.0,
                "z": 0.0
            },
            "orientation": {
                "x": 0.0,
                "y": 0.0,
                "z": 0.0,
                "w": 1.0
            }
        }
    }

    try:
        count = 0

        while True:
            try:
                [topic, msg] = subscriber.recv_multipart()
                topic_name = topic.decode("utf-8")
                data = json.loads(msg.decode("utf-8"))

                sub_data[topic_name] = data
            except zmq.Again:
                print("No message sent within the timeout period.")

            print(f'SUB: {sub_data["p1_cmdvel"]["twist"]}')

            try:
                if count < 10:
                    count += 1
                    continue
                else:
                    count = 0

                pub_data["header"]["seq"] += 1
                pub_data["header"]["stamp"] = time.time()

                if "p1_cmdvel" in sub_data:
                    pub_data["pose"]["position"]["x"] \
                        += sub_data["p1_cmdvel"]["twist"]["linear"]["y"] * 0.01
                    pub_data["pose"]["position"]["z"] \
                        -= sub_data["p1_cmdvel"]["twist"]["linear"]["x"] * 0.01

                    q1 = quaternion.quaternion(
                        pub_data["pose"]["orientation"]["w"],
                        pub_data["pose"]["orientation"]["x"],
                        pub_data["pose"]["orientation"]["y"],
                        pub_data["pose"]["orientation"]["z"])
                    q2 = quaternion.from_euler_angles(
                        0.0, sub_data["p1_cmdvel"]["twist"]["angular"]["z"] * 0.1, 0)
                    q3 = q1 * q2

                    pub_data["pose"]["orientation"]["x"] = q3.x
                    pub_data["pose"]["orientation"]["y"] = q3.y
                    pub_data["pose"]["orientation"]["z"] = q3.z
                    pub_data["pose"]["orientation"]["w"] = q3.w

                publisher.send_multipart([
                    pub_topic.encode("utf-8"),
                    json.dumps(pub_data).encode("utf-8")
                ])

                print(f'PUB: {pub_data["pose"]}')

            except zmq.Again:
                print("No message sent within the timeout period.")
            except Exception as e:
                print(e)

    except KeyboardInterrupt:
        print("finish loop")
    finally:
        publisher.close()
        context.term()


if __name__ == "__main__":

    try:
        main()
    except Exception as ex:
        print(ex)
