#!/usr/bin/env python3
"""
CuStack-Robo Promotional Video Generator using Google Veo (veo-3.1-generate-preview)
Special Mobile Machinery Works (特殊移動機械製作所) / Maker Faire Tokyo 2026

This script generates high-definition cinematic promotional clips for CuStack-Robo
using Google GenAI SDK and the Veo 3.1 video generation model.
"""

import os
import json
import time
from pathlib import Path
from google import genai
from google.genai import types

# Output directory for rendered promotional videos
OUTPUT_DIR = Path(__file__).resolve().parent / "output" / "videos"
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

# Path to .gemini/setting.json to automatically load API Key
SETTING_JSON_PATH = Path(__file__).resolve().parent.parent / ".gemini" / "setting.json"

def get_api_key() -> str:
    """Retrieve GEMINI_API_KEY from environment or .gemini/setting.json."""
    api_key = os.environ.get("GEMINI_API_KEY")
    if api_key:
        return api_key
    
    if SETTING_JSON_PATH.exists():
        try:
            with open(SETTING_JSON_PATH, "r", encoding="utf-8") as f:
                data = json.load(f)
                key = data.get("mcpServers", {}).get("google-veo-mcp", {}).get("env", {}).get("GEMINI_API_KEY")
                if key:
                    return key
        except Exception as e:
            print(f"Warning: Failed to parse {SETTING_JSON_PATH}: {e}")
            
    raise ValueError("GEMINI_API_KEY is not set in environment or .gemini/setting.json")

# Storyboard Definition for 30s Commercial (4 Scenes)
SCENES = [
    {
        "scene_id": 1,
        "filename": "scene1_snap_and_play.mp4",
        "title": "Scene 1: Snap & Play - Awakening of the Modular Core",
        "duration_seconds": 6,
        "aspect_ratio": "16:9",
        "prompt": (
            "Cinematic dynamic close-up shot on a sci-fi tech workbench. A person's hands attach a mechanical "
            "dual gatling gun arm module onto a compact cube robot body with a satisfying magnetic click and shining "
            "gold pogo pin connectors. The robot's front LCD screen boots up with glowing cyan robot eyes (._.). "
            "Instantly, an ultra-crisp neon cyan holographic circular HUD ring expands across the floor displaying "
            "'DEVICE ID: 0x01 GATLING CONNECTED'. Macro camera move, smooth lighting transition, 4K sci-fi tech aesthetic."
        )
    },
    {
        "scene_id": 2,
        "filename": "scene2_modular_loadouts.mp4",
        "title": "Scene 2: Modular Customization - Endless Tactics",
        "duration_seconds": 8,
        "aspect_ratio": "16:9",
        "prompt": (
            "Dynamic sweeping camera panning across three distinct custom mini battle robots on a reflective cyber floor. "
            "First robot has tank crawler tracks and dual laser cannons; middle robot has 4-wheel omnidirectional drive "
            "and twin rotating gatling barrels; third robot has sports differential tires and glowing emerald beam swords. "
            "The omni-wheel robot smoothly strafes diagonally at high speed while the tire robot drifts with smoke sparks. "
            "Cinematic motion blur, lens flare, intense studio lighting, futuristic robotics."
        )
    },
    {
        "scene_id": 3,
        "filename": "scene3_floor_projection_battle.mp4",
        "title": "Scene 3: Floor Projection Arena Battle Climax",
        "duration_seconds": 8,
        "aspect_ratio": "16:9",
        "prompt": (
            "Epic wide arena action shot of two real mini modular battle robots fighting in a darkened hall on a high-brightness "
            "floor projection mapping arena. The floor projection shows dynamic glowing red lava fissures and icy grid terrain. "
            "The omni robot fires rapid-fire yellow energy laser bullet barrages projected across the floor, while its real mini gun "
            "barrels rapidly vibrate. The crawler tank robot fires a massive neon blue laser beam with ground impact particle explosions. "
            "Real-time projected HUD circles and glowing health bars follow the robots on the floor. Photorealistic, dramatic esports battle atmosphere."
        )
    },
    {
        "scene_id": 4,
        "filename": "scene4_title_climax.mp4",
        "title": "Scene 4: Climax Title Call - Maker Faire Tokyo 2026",
        "duration_seconds": 8,
        "aspect_ratio": "16:9",
        "prompt": (
            "Heroic low-angle slow dolly-in shot of the CuStack-Robo battle mecha posing victoriously on an illuminated hexagonal "
            "stage with celebratory arena fireworks and cheering crowd bokeh in the background. Glowing holographic typography floats "
            "above displaying 'CuStack-Robo' in gleaming metallic neon chrome, with subtext 'MAKER FAIRE TOKYO 2026 - Special Mobile Machinery Works'. "
            "Golden lens flare, cinematic particles, 8k resolution promotional trailer finish."
        )
    }
]

def generate_scene_video(client: genai.Client, scene_info: dict) -> Path:
    """Generate a single video scene using Google Veo."""
    filename = scene_info["filename"]
    save_path = OUTPUT_DIR / filename
    prompt = scene_info["prompt"]
    duration = scene_info.get("duration_seconds", 8)
    aspect_ratio = scene_info.get("aspect_ratio", "16:9")

    print(f"\n==========================================")
    print(f"🎬 Generating {scene_info['title']} ...")
    print(f"📁 Target: {save_path}")
    print(f"📝 Prompt: {prompt[:120]}...")
    print(f"⏱️ Duration: {duration}s | Aspect: {aspect_ratio}")
    print(f"==========================================")

    operation = client.models.generate_videos(
        model="veo-3.1-generate-preview",
        prompt=prompt,
        config=types.GenerateVideosConfig(
            aspect_ratio=aspect_ratio,
            duration_seconds=duration
        )
    )

    poll_count = 0
    while not operation.done:
        time.sleep(10)
        poll_count += 1
        operation = client.operations.get(operation)
        print(f"⏳ Waiting for video rendering... ({poll_count * 10}s elapsed)")

    if operation.error:
        raise RuntimeError(f"Veo video generation failed: {operation.error}")

    generated_video = operation.result.generated_videos[0]
    client.files.download(file=generated_video.video)
    generated_video.video.save(str(save_path))

    print(f"✅ Successfully saved video: {save_path.resolve()}")
    return save_path

def main():
    api_key = get_api_key()
    client = genai.Client(api_key=api_key)
    print(f"✨ Initialized Google GenAI Client with Veo 3.1.")
    print(f"📂 Output directory: {OUTPUT_DIR}")

    generated_files = []
    for scene in SCENES:
        try:
            path = generate_scene_video(client, scene)
            generated_files.append(path)
        except Exception as e:
            print(f"❌ Error generating {scene['filename']}: {e}")

    print("\n🎉 Promo Video Generation Process Finished!")
    for f in generated_files:
        print(f" - {f}")

if __name__ == "__main__":
    main()
