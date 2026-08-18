import os
import io
from pathlib import Path
from google import genai
from google.genai import types
from mcp.server.fastmcp import FastMCP, Image
from PIL import Image as PILImage

# MCPサーバーの初期化
mcp = FastMCP("Gemini Image Generator")

# 出力先ディレクトリの設定
OUTPUT_DIR = Path("./generated_images")
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)


@mcp.tool()
def generate_image(
    prompt: str,
    aspect_ratio: str = "1:1",
    output_filename: str = "generated.png",
) -> Image:
    """Gemini API (Imagen 3) を使用してテキストプロンプトから画像を生成します。

    Args:
        prompt: 生成したい画像の説明（英語推奨）
        aspect_ratio: アスペクト比 ("1:1", "3:4", "4:3", "9:16", "16:9")
        output_filename: 保存先ファイル名（例: output.png）

    Returns:
        生成された画像データ (MCP Image型)
    """
    # GEMINI_API_KEY 環境変数からクライアントを初期化
    api_key = os.environ.get("GEMINI_API_KEY")
    if not api_key:
        raise ValueError("GEMINI_API_KEY 環境変数が設定されていません。")

    client = genai.Client(api_key=api_key)

    # 画像生成リクエスト
    response = client.models.generate_images(
        model="imagen-3.0-generate-002",
        prompt=prompt,
        config=types.GenerateImagesConfig(
            number_of_images=1,
            aspect_ratio=aspect_ratio,
            output_mime_type="image/png",
        ),
    )

    if not response.generated_images:
        raise RuntimeError("画像の生成に失敗しました。")

    image_bytes = response.generated_images[0].image.image_bytes

    # ローカルファイルとしても保存
    save_path = OUTPUT_DIR / output_filename
    with open(save_path, "wb") as f:
        f.write(image_bytes)

    # MCPクライアントでプレビュー表示できるようにImageオブジェクトで返す
    pil_image = PILImage.open(io.BytesIO(image_bytes))
    return Image(data=pil_image.tobytes(), format="png")


if __name__ == "__main__":
    mcp.run(transport="stdio")
