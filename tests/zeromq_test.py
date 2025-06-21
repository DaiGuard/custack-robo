import zmq
import time
import json


def main():
    context = zmq.Context()

    subscriber = context.socket(zmq.SUB)
    subscriber.setsockopt(zmq.SUBSCRIBE, b"")
    subscriber.setsockopt(zmq.RCVTIMEO, 30)
    subscriber.connect("tcp://localhost:5555")

    publisher = context.socket(zmq.PUB)
    publisher.setsockopt(zmq.SNDTIMEO, 30)
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
                "x": 0.3,
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
        while True:
            try:
                [topic, msg] = subscriber.recv_multipart()
                topic_name = topic.decode("utf-8")
                data = json.loads(msg.decode("utf-8"))

                sub_data[topic_name] = data
            except zmq.Again:
                print("No message sent within the timeout period.")

            print(f"SUB: {sub_data}")

            try:
                pub_data["header"]["seq"] += 1
                pub_data["header"]["stamp"] = time.time()

                publisher.send_multipart([
                    pub_topic.encode("utf-8"),
                    json.dumps(pub_data).encode("utf-8")
                ])
            except zmq.Again:
                print("No message sent within the timeout period.")

            print(f"PUB: {pub_data}")

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
