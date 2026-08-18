using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Custack.Terrain
{
    /// <summary>
    /// プロジェクター床面投影用の鮮明でスタイリッシュな地形テクスチャを自動生成するツール。
    /// 高コントラストなネオングラフィック、幾何学グリッド、シンボルアイコンを描画し、
    /// プロジェクションマッピング下で一目で地形属性がわかる視覚演出を提供します。
    /// </summary>
    public static class TerrainTextureGenerator
    {
        public const int TexSize = 512;
        public const string TextureDir = "Assets/_Project/Textures/Terrain";

        public static void GenerateAllTerrainTextures()
        {
            EnsureDirectory(TextureDir);

            SaveAndImportTexture("Tex_Terrain_Forest", GenerateForestTexture());
            SaveAndImportTexture("Tex_Terrain_Mud", GenerateMudTexture());
            SaveAndImportTexture("Tex_Terrain_Ice", GenerateIceTexture());
            SaveAndImportTexture("Tex_Terrain_Lava", GenerateLavaTexture());
            SaveAndImportTexture("Tex_Terrain_CityRoad", GenerateCityRoadTexture());

#if UNITY_EDITOR
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF88>[TerrainTextureGenerator]</color> 🎨 高品質ネオン地形テクスチャ全5種を正常に生成・インポートしました！");
#endif
        }

        public static Texture2D GenerateForestTexture()
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            Color bgCol = new Color(0.04f, 0.14f, 0.07f, 0.90f);
            Color lineBright = new Color(0.2f, 1.0f, 0.4f, 1.0f);
            Color lineSoft = new Color(0.1f, 0.6f, 0.25f, 0.85f);

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    float u = (float)x / TexSize;
                    float v = (float)y / TexSize;

                    float hx = (x % 64) - 32;
                    float hy = (y % 64) - 32;
                    float dist = Mathf.Sqrt(hx * hx + hy * hy);
                    bool isHex = Mathf.Abs(dist - 28f) < 2.5f;

                    bool isVein = ((x + y) % 64 < 3) || ((x - y + TexSize) % 64 < 3);

                    float cx = (u - 0.5f) * 2f;
                    float cy = (v - 0.5f) * 2f;
                    float rCenter = Mathf.Sqrt(cx * cx + cy * cy);
                    bool isTreeRing = Mathf.Abs(rCenter - 0.35f) < 0.03f || Mathf.Abs(rCenter - 0.6f) < 0.02f;

                    bool isBorder = (x < 12 || x > TexSize - 13 || y < 12 || y > TexSize - 13);

                    Color c = bgCol;
                    if (isVein) c = Color.Lerp(c, lineSoft, 0.7f);
                    if (isHex) c = Color.Lerp(c, lineBright, 0.85f);
                    if (isTreeRing) c = Color.Lerp(c, lineBright, 0.95f);
                    if (isBorder) c = lineBright;

                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        public static Texture2D GenerateMudTexture()
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            Color bgCol = new Color(0.18f, 0.09f, 0.03f, 0.90f);
            Color rippleBright = new Color(1.0f, 0.55f, 0.15f, 1.0f);
            Color rippleSoft = new Color(0.7f, 0.35f, 0.08f, 0.85f);

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    float u = (float)x / TexSize;
                    float v = (float)y / TexSize;

                    float dx = (u - 0.5f) * 2f;
                    float dy = (v - 0.5f) * 2f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float wave = Mathf.Sin(r * 28f);
                    bool isWave = Mathf.Abs(wave) > 0.75f;

                    float bx = (x % 48) - 24;
                    float by = (y % 48) - 24;
                    float bDist = Mathf.Sqrt(bx * bx + by * by);
                    bool isBubble = Mathf.Abs(bDist - 12f) < 2.0f;

                    bool isBorder = (x < 12 || x > TexSize - 13 || y < 12 || y > TexSize - 13);

                    Color c = bgCol;
                    if (isBubble) c = Color.Lerp(c, rippleSoft, 0.7f);
                    if (isWave) c = Color.Lerp(c, rippleBright, 0.9f);
                    if (isBorder) c = rippleBright;

                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        public static Texture2D GenerateIceTexture()
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            Color bgCol = new Color(0.03f, 0.15f, 0.25f, 0.90f);
            Color iceBright = new Color(0.4f, 0.95f, 1.0f, 1.0f);
            Color iceSoft = new Color(0.2f, 0.6f, 0.85f, 0.85f);

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    float u = (float)x / TexSize;
                    float v = (float)y / TexSize;

                    float diag1 = Mathf.Abs((x + y) % 64 - 32);
                    float diag2 = Mathf.Abs((x - y + TexSize) % 64 - 32);
                    bool isDiamond = (diag1 < 2.5f) || (diag2 < 2.5f);

                    float crack1 = Mathf.Sin(x * 0.06f) * 20f + Mathf.Cos(y * 0.04f) * 15f;
                    float crack2 = Mathf.Cos((x + y) * 0.05f) * 25f;
                    bool isCrack = Mathf.Abs(crack1 - crack2) < 2.2f;

                    float cx = (u - 0.5f) * 2f;
                    float cy = (v - 0.5f) * 2f;
                    float rCenter = Mathf.Sqrt(cx * cx + cy * cy);
                    bool isSnowRing = Mathf.Abs(rCenter - 0.45f) < 0.025f;

                    bool isBorder = (x < 12 || x > TexSize - 13 || y < 12 || y > TexSize - 13);

                    Color c = bgCol;
                    if (isDiamond) c = Color.Lerp(c, iceSoft, 0.75f);
                    if (isCrack) c = Color.Lerp(c, iceBright, 0.95f);
                    if (isSnowRing) c = Color.Lerp(c, iceBright, 0.9f);
                    if (isBorder) c = iceBright;

                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        public static Texture2D GenerateLavaTexture()
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            Color bgCol = new Color(0.22f, 0.04f, 0.03f, 0.95f);
            Color coreBright = new Color(1.0f, 0.9f, 0.3f, 1.0f);
            Color magmaRed = new Color(1.0f, 0.25f, 0.05f, 0.95f);

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    float u = (float)x / TexSize;
                    float v = (float)y / TexSize;

                    float flow = Mathf.Sin(x * 0.05f + Mathf.Cos(y * 0.04f) * 2.5f) +
                                 Mathf.Cos(y * 0.05f + Mathf.Sin(x * 0.04f) * 2.5f);

                    bool isCore = Mathf.Abs(flow) < 0.18f;
                    bool isEdge = Mathf.Abs(flow) < 0.45f;

                    bool isStripe = ((x + y) % 40 < 4);
                    bool isBorder = (x < 12 || x > TexSize - 13 || y < 12 || y > TexSize - 13);

                    Color c = bgCol;
                    if (isStripe) c = Color.Lerp(c, new Color(0.8f, 0.4f, 0.1f, 0.6f), 0.5f);
                    if (isEdge) c = Color.Lerp(c, magmaRed, 0.85f);
                    if (isCore) c = Color.Lerp(c, coreBright, 0.98f);
                    if (isBorder) c = magmaRed;

                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        public static Texture2D GenerateCityRoadTexture()
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            Color bgCol = new Color(0.09f, 0.10f, 0.14f, 0.90f);
            Color yellowPaint = new Color(1.0f, 0.90f, 0.15f, 1.0f);
            Color cyanGrid = new Color(0.2f, 0.6f, 0.9f, 0.75f);

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    bool isCenterDash = Mathf.Abs(x - TexSize / 2) < 6 && (y % 80 < 50);
                    bool isSideLineL = Mathf.Abs(x - 48) < 4;
                    bool isSideLineR = Mathf.Abs(x - (TexSize - 48)) < 4;
                    bool isGrid = (x % 64 < 2) || (y % 64 < 2);
                    bool isBorder = (x < 12 || x > TexSize - 13 || y < 12 || y > TexSize - 13);

                    Color c = bgCol;
                    if (isGrid) c = Color.Lerp(c, cyanGrid, 0.5f);
                    if (isSideLineL || isSideLineR) c = Color.Lerp(c, Color.white, 0.9f);
                    if (isCenterDash) c = Color.Lerp(c, yellowPaint, 1.0f);
                    if (isBorder) c = yellowPaint;

                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        private static void SaveAndImportTexture(string fileName, Texture2D tex)
        {
            byte[] pngData = tex.EncodeToPNG();
            string filePath = $"{TextureDir}/{fileName}.png";
            File.WriteAllBytes(filePath, pngData);

#if UNITY_EDITOR
            AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
#endif
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
