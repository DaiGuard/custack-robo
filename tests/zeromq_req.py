import zmq
import time
import json

def main():
    context = zmq.Context()
    socket = context.socket(zmq.REQ)
    socket.setsockopt(zmq.SNDTIMEO, 1000)
    socket.setsockopt(zmq.RCVTIMEO, 1000)
    socket.connect("tcp://localhost:5557")

    topic_name = "test"
    topic_data = {
        "header": {
            "seq": 0,
            "stamp": 0.0
        },
        "items": ["a", "b", "c"]
    }
    data = json.dumps(topic_data)

    try:
        print("Sending message...")
        socket.send_multipart([
            topic_name.encode("utf-8"),
            data.encode("utf-8")
        ])
        print(f"REQ: {topic_name}, {topic_data}")

        print("Waiting for reply...")
        [topic, msg] = socket.recv_multipart()
        print(f"REP: {topic}, {msg}")
    except zmq.Again:
        print("No message sent within the timeout period.")        

if __name__ == "__main__":

    try:
        main()
    except Exception as ex:
        print(ex)
