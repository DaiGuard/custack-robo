using UnityEngine;

namespace Custack.Terrain
{
    /// <summary>
    /// 各地形の基本定義データ
    /// </summary>
    [System.Serializable]
    public class TerrainData
    {
        public TerrainType type = TerrainType.Normal;
        public string displayName = "Normal Plains";

        [Header("基本地形効果")]
        public float baseSpeedMultiplier = 1.0f; // 基本速度倍率
        public float baseTurnMultiplier = 1.0f;  // 基本旋回倍率
        public float damagePerSecond = 0f;       // スリップダメージ (溶岩等)
        public bool isSlippery = false;          // 氷などスリップ特性

        [Header("視覚エフェクト・カラー")]
        public Color zoneColor = new Color(0.3f, 0.8f, 0.3f, 0.3f);
        public GameObject enterEffectPrefab;

        public static TerrainData CreateDefault(TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Forest:
                    return new TerrainData
                    {
                        type = TerrainType.Forest,
                        displayName = "Forest Area",
                        baseSpeedMultiplier = 0.5f,
                        baseTurnMultiplier = 0.7f,
                        damagePerSecond = 0f,
                        zoneColor = new Color(0.1f, 0.6f, 0.2f, 0.35f)
                    };
                case TerrainType.Mud:
                    return new TerrainData
                    {
                        type = TerrainType.Mud,
                        displayName = "Mud Swamp",
                        baseSpeedMultiplier = 0.3f,
                        baseTurnMultiplier = 0.4f,
                        damagePerSecond = 15f, // 毒沼・泥沼: 毎秒15スリップダメージ
                        zoneColor = new Color(0.55f, 0.35f, 0.15f, 0.4f)
                    };
                case TerrainType.Ice:
                    return new TerrainData
                    {
                        type = TerrainType.Ice,
                        displayName = "Frozen Ice",
                        baseSpeedMultiplier = 1.0f,
                        baseTurnMultiplier = 0.5f,
                        isSlippery = true,
                        zoneColor = new Color(0.4f, 0.8f, 1.0f, 0.35f)
                    };
                case TerrainType.Lava:
                    return new TerrainData
                    {
                        type = TerrainType.Lava,
                        displayName = "Lava Zone",
                        baseSpeedMultiplier = 0.7f,
                        baseTurnMultiplier = 0.7f,
                        damagePerSecond = 30f, // 溶岩・電磁ハザード: 毎秒30スリップダメージ
                        zoneColor = new Color(0.9f, 0.2f, 0.1f, 0.45f)
                    };
                default:
                    return new TerrainData
                    {
                        type = TerrainType.Normal,
                        displayName = "Normal Ground",
                        baseSpeedMultiplier = 1.0f,
                        baseTurnMultiplier = 1.0f,
                        zoneColor = Color.clear
                    };
            }
        }
    }
}
