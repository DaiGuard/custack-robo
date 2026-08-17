using UnityEngine;

namespace Custack.Terrain
{
    /// <summary>
    /// フィールド上に配置する地形エリア（森、泥、氷、溶岩など）コンポーネント。
    /// Collider2D (Trigger) または 境界矩形により侵入判定を行う。
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class TerrainZone : MonoBehaviour
    {
        [Header("地形設定")]
        public TerrainType terrainType = TerrainType.Forest;

        [SerializeField]
        private TerrainData terrainData;

        public TerrainData Data => terrainData;
        public TerrainType Type => terrainType;

        private Collider2D zoneCollider;

        void Awake()
        {
            zoneCollider = GetComponent<Collider2D>();
            if (zoneCollider != null)
            {
                zoneCollider.isTrigger = true;
            }

            terrainData = TerrainData.CreateDefault(terrainType);
        }

        void OnValidate()
        {
            terrainData = TerrainData.CreateDefault(terrainType);
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
        }
    }
}
