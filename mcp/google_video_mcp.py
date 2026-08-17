import os
import time
from pathlib import Path
from mcp.server.fastmcp import FastMCP
from google import genai
from google.genai import types

# MCPサーバーの初期化
mcp = FastMCP("google-veo-promoter")

# 出力ディレクトリ
OUTPUT_DIR = Path("./output/videos")
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

@mcp.tool()
def generate_commercial_clip(prompt: str, filename: str = "scene.mp4", aspect_ratio: str = "16:9") -> str:
    """
    Google Veo を使用してコマーシャル用の動画クリップを生成します。

    :param prompt: 動画のビジュアルや動きを詳細に指示するプロンプト（英語推奨）
    :param filename: 保存する動画ファイル名 (例: scene1.mp4)
    :param aspect_ratio: アスペクト比 ("16:9" または "9:16")
    :return: 保存先ファイルパスまたはステータスメッセージ
    """
    api_key = os.environ.get("GEMINI_API_KEY")
    if not api_key:
        return "エラー: GEMINI_API_KEY 環境変数が設定されていません。"

    client = genai.Client(api_key=api_key)

    try:
        # Veoモデルで非同期生成ジョブを開始
        operation = client.models.generate_videos(
            model="veo-3.1-generate-preview",
            prompt=prompt,
            config=types.GenerateVideosConfig(
                aspect_ratio=aspect_ratio,
                duration_seconds=8
            )
        )

        # 動画生成完了までポーリング
        while not operation.done:
            time.sleep(10)
            operation = client.operations.get(operation)

        # 生成結果の取得と保存
        generated_video = operation.result.generated_videos[0]
        client.files.download(file=generated_video.video)

        save_path = OUTPUT_DIR / filename
        generated_video.video.save(str(save_path))

        return f"動画の生成が完了しました: {save_path.resolve()}"

    except Exception as e:
        return f"動画生成中にエラーが発生しました: {str(e)}"

if __name__ == "__main__":
    mcp.run()