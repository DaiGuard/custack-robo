import rclpy
from .web_server_node import WebServerNode
import threading
import uvicorn
from fastapi import FastAPI, Request, Response
from fastapi.templating import Jinja2Templates
from fastapi.responses import HTMLResponse, StreamingResponse
import time
import os
from ament_index_python.packages import get_package_share_directory
import subprocess
import re


# FastAPIのインスタンス作成
app = FastAPI()

# HTMLファイルの読み込み
package_name = 'custack_web'
base_path = get_package_share_directory(package_name)
template_path = os.path.join(base_path, 'templates')
templates = Jinja2Templates(directory=template_path)

@app.get("/", response_class=HTMLResponse)
async def index(request: Request):
    return templates.TemplateResponse(
        request=request, 
        name="index.html", 
        context={"request": request}
    )

def gen_frames():
    while True:
        if ros_node is not None:
            image = ros_node.get_latest_image()
            if image is not None:
                yield (b'--frame\r\n'
                    b'Content-Type: image/jpeg\r\n\r\n' + image + b'\r\n')

        time.sleep(0.03)

@app.get("/video_feed")
async def video_feed():
    return StreamingResponse(
        gen_frames(),
        media_type="multipart/x-mixed-replace; boundary=frame"
    )

@app.get("/api/calib_capture")
async def calib_capture(id: int = None, change: bool = True):
    if ros_node is not None:
        image = ros_node.get_calib_image(id, change)
        return Response(content=image, media_type="image/jpeg")

    return {"status": "error", "message": "no capture image"}

@app.get("/api/calib_save")
async def calib_save():
    if ros_node is not None:
        res = ros_node.save_calib()
        return {"status": "ok" if res["status"] else "error", "message": res["message"]}

@app.get("/api/undistort")
async def undistort(enable: bool = False):
    if ros_node is not None:
        ros_node.set_undistort(enable)
        return {"status": "ok", "message": f"Undistortion {'enabled' if enable else 'disabled'}"}

    return {"status": "error", "message": "ROS node not initialized"}

@app.get("/api/homography")
async def homography(enable: bool = False):
    if ros_node is not None:
        ros_node.set_homography(enable)
        return {"status": "ok", "message": f"Homography {'enabled' if enable else 'disabled'}"}

    return {"status": "error", "message": "ROS node not initialized"}

@app.get("/api/homography_capture")
async def homography_capture():
    if ros_node is not None:
        image = ros_node.get_homography_image()
        return Response(content=image, media_type="image/jpeg")

    return {"status": "error", "message": "no capture image"}

@app.get("/api/homography_save")
async def homography_save():
    if ros_node is not None:
        res = ros_node.save_homography()
        return {"status": "ok" if res["status"] else "error", "message": res["message"]}

@app.get("/api/get_camera")
async def get_camera():
    results = {}
    try:
        cmd = ["v4l2-ctl", "-d", "/dev/video0", "-C", f"exposure"]
        res = subprocess.run(cmd, check=True, capture_output=True, text=True)
        match = re.search(r'exposure:\s*(\d+)', res.stdout)
        results['exposure'] = int(match.group(1)) if match else None

        cmd = ["v4l2-ctl", "-d", "/dev/video0", "-C", f"analogue_gain"]
        res = subprocess.run(cmd, check=True, capture_output=True, text=True)
        match = re.search(r'analogue_gain:\s*(\d+)', res.stdout)
        results['gain'] = int(match.group(1)) if match else None

        return {"status": "ok", "camera_values": results}
    except subprocess.CalledProcessError as e:
        return {"status": "error", "message": e.stderr}
    except Exception as e:
        return {"status": "error", "message": str(e)}

@app.get("/api/set_camera")
async def set_camera(exposure: int = None, gain: int = None):

    ros_node.get_logger().info(f'リクエスト受信: exposure={exposure}, gain={gain}')
    results = {}
    try:
        if exposure is not None:
            if 2 <= exposure <= 65535:
                cmd = ["v4l2-ctl", "-d", "/dev/video0", "-c", f"exposure={exposure}"]
                subprocess.run(cmd, check=True)
                cmd = ["v4l2-ctl", "-d", "/dev/video0", "-C", f"exposure"]
                res = subprocess.run(cmd, check=True, capture_output=True, text=True)
                match = re.search(r'exposure:\s*(\d+)', res.stdout)
                results['exposure'] = int(match.group(1)) if match else None
            else:
                return {"status": "error", "message": "Invalid exposure value"}
        if gain is not None:
            if 100 <= gain <= 1200:
                cmd = ["v4l2-ctl", "-d", "/dev/video0", "-c", f"analogue_gain={gain}"]
                subprocess.run(cmd, check=True)
                cmd = ["v4l2-ctl", "-d", "/dev/video0", "-C", f"analogue_gain"]
                res = subprocess.run(cmd, check=True, capture_output=True, text=True)
                match = re.search(r'analogue_gain:\s*(\d+)', res.stdout)
                results['gain'] = int(match.group(1)) if match else None                
            else:
                return {"status": "error", "message": "Invalid gain value"}

        print(results)
        return {"status": "ok", "applied_values": results}

    except subprocess.CalledProcessError as e:
        return {"status": "error", "message": e.stderr}
    except Exception as e:
        return {"status": "error", "message": str(e)}

ros_node = None

def main(args=None):

    # ~~~ ROS関係処理 ~~~
    global ros_node

    # ROSの初期化
    rclpy.init(args=args)

    # ROSノーノ初期化
    ros_node = WebServerNode()
    ros_thread = threading.Thread(
        target=rclpy.spin,
        args=(ros_node,),
        daemon=True)
    ros_thread.start()

    try:
        uvicorn.run(
            app,
            host="0.0.0.0",
            port=8000,
            log_level="info")
    except KeyboardInterrupt:
        pass
    finally:
        ros_node.destroy_node()
        rclpy.shutdown()

if __name__ == '__main__':
    main()
