using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Custack.Combat;

namespace Custack.Terrain
{
    /// <summary>
    /// フィールド上に配置する地形エリア（森、泥、氷、溶岩など）コンポーネント。
    /// 床面投影用の背景またはオーバーレイ、および Collider2D (Trigger) による侵入判定を行います。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TerrainZone : MonoBehaviour
    {
        [Header("地形設定")]
        public TerrainType terrainType = TerrainType.Forest;

        [SerializeField]
        private TerrainData terrainData;

        public TerrainData Data => terrainData;
        public TerrainType Type => terrainType;

        private Collider2D zoneCollider;
        private static readonly Dictionary<TerrainType, Texture2D> cachedTextures = new Dictionary<TerrainType, Texture2D>();

        void Awake()
        {
            zoneCollider = GetComponent<Collider2D>();
            if (zoneCollider != null)
            {
                zoneCollider.isTrigger = true;
            }

            terrainData = TerrainData.CreateDefault(terrainType);
        }

        void Start()
        {
            SetupVisualQuad();
        }

        void OnValidate()
        {
            terrainData = TerrainData.CreateDefault(terrainType);
        }

        /// <summary>
        /// 床面投影用のテクスチャ Quad を初期化・適用 (個別ゾーン表示用)
        /// </summary>
        public void SetupVisualQuad()
        {
            var quad = transform.Find("FloorTextureQuad");
            if (quad == null) return;

            var box = GetComponent<BoxCollider2D>();
            Vector2 size = box != null ? box.size : new Vector2(3f, 3f);
            quad.localPosition = new Vector3(0, 0, 0.1f);
            quad.localScale = new Vector3(size.x, size.y, 1f);

            var mr = quad.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var tex = GetTerrainTexture(terrainType);

                // URP Unlit / Transparent マテリアル
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
                var mat = new Material(shader);
                mat.name = $"Mat_Runtime_Terrain_{terrainType}";

                mat.mainTexture = tex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetFloat("_Blend", 0);   // Alpha
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.SetFloat("_Cull", 0);    // Cull Off (両面)
                mat.renderQueue = 3000;

                mr.material = mat;
            }
        }

        /// <summary>
        /// ディスク上の PNG テクスチャを直接ロード
        /// </summary>
        public static Texture2D GetTerrainTexture(TerrainType type)
        {
            if (cachedTextures.TryGetValue(type, out var cached) && cached != null)
            {
                return cached;
            }

            string texName = type switch
            {
                TerrainType.Forest => "Tex_Terrain_Forest.png",
                TerrainType.Mud => "Tex_Terrain_Mud.png",
                TerrainType.Ice => "Tex_Terrain_Ice.png",
                TerrainType.Lava => "Tex_Terrain_Lava.png",
                _ => "Tex_Terrain_CityRoad.png"
            };

            string fullPath = Path.Combine(Application.dataPath, "_Project/Textures/Terrain", texName);
            if (File.Exists(fullPath))
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(bytes);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                cachedTextures[type] = tex;
                return tex;
            }

            return Texture2D.whiteTexture;
        }

        /// <summary>
        /// ワールド座標がこのゾーン内にあるかを判定
        /// </summary>
        public bool ContainsPoint(Vector2 worldPos)
        {
            if (zoneCollider == null) zoneCollider = GetComponent<Collider2D>();
            if (zoneCollider == null) return false;

            return zoneCollider.OverlapPoint(worldPos);
        }

        void OnDrawGizmos()
        {
            if (terrainData == null) terrainData = TerrainData.CreateDefault(terrainType);

            Gizmos.color = terrainData.zoneColor;
            var col = GetComponent<Collider2D>();
            if (col is BoxCollider2D box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.offset, box.size);
                Gizmos.DrawWireCube(box.offset, box.size);
            }
            else if (col is CircleCollider2D circle)
            {
                Gizmos.DrawSphere(transform.TransformPoint(circle.offset), circle.radius * transform.lossyScale.x);
            }
            else if (col is PolygonCollider2D poly)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                for (int pathIdx = 0; pathIdx < poly.pathCount; pathIdx++)
                {
                    Vector2[] points = poly.GetPath(pathIdx);
                    for (int i = 0; i < points.Length; i++)
                    {
                        Vector2 p1 = points[i];
                        Vector2 p2 = points[(i + 1) % points.Length];
                        Gizmos.DrawLine(p1, p2);
                    }
                }
            }
        }
    }
}
