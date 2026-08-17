using System.Collections.Generic;
using UnityEngine;
using Custack.Terrain;

namespace Custack.Equipment
{
    /// <summary>
    /// 脚ユニットの移動特性・地形耐性設定クラス
    /// ※ キネマティクス制限（差動二輪の横移動無効化やステアリング動作など）は実機マイコン(robot_leg)側で実行されるため、
    ///   Unity側では地形適用の比率（0.0〜1.0）のみを管理します。
    /// </summary>
    [System.Serializable]
    public class LegMovementConfig
    {
        public LegDeviceType type = LegDeviceType.Omni;
        public string legName = "Omni Wheels";

        [Header("基本運動比率 (平地 = 1.0)")]
        public float baseSpeedMultiplier = 1.0f; // 基本速度倍率 (平地: 1.0)
        public float baseTurnMultiplier = 1.0f;  // 基本旋回倍率 (平地: 1.0)

        [Header("地形ごとの速度適用比率 (平地 = 1.0)")]
        public float normalSpeedMul = 1.0f;
        public float forestSpeedMul = 0.5f;
        public float mudSpeedMul = 0.3f;
        public float iceSpeedMul = 1.0f;
        public float lavaSpeedMul = 0.7f;

        [Header("地形ごとの旋回適用比率 (平地 = 1.0)")]
        public float normalTurnMul = 1.0f;
        public float forestTurnMul = 0.7f;
        public float mudTurnMul = 0.4f;
        public float iceTurnMul = 0.5f;
        public float lavaTurnMul = 0.7f;

        [Header("氷地形でのスリップ慣性係数 (0:スリップなし, 1:最大スリップ)")]
        public float iceSlipFactor = 0.8f;

        [Header("溶岩でのダメージ軽減率 (0.0:軽減なし, 1.0:完全無効)")]
        public float lavaDamageReduction = 0.0f;

        public float GetSpeedMultiplier(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Forest: return forestSpeedMul;
                case TerrainType.Mud:    return mudSpeedMul;
                case TerrainType.Ice:    return iceSpeedMul;
                case TerrainType.Lava:   return lavaSpeedMul;
                default:                 return normalSpeedMul;
            }
        }

        public float GetTurnMultiplier(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Forest: return forestTurnMul;
                case TerrainType.Mud:    return mudTurnMul;
                case TerrainType.Ice:    return iceTurnMul;
                case TerrainType.Lava:   return lavaTurnMul;
                default:                 return normalTurnMul;
            }
        }

        public static LegMovementConfig CreateDefault(LegDeviceType type)
        {
            switch (type)
            {
                case LegDeviceType.Omni:
                    return new LegMovementConfig
                    {
                        type = LegDeviceType.Omni,
                        legName = "Omni Wheels (4-Wheel)",
                        baseSpeedMultiplier = 1.0f,
                        baseTurnMultiplier = 1.0f,
                        normalSpeedMul = 1.0f,
                        forestSpeedMul = 0.5f,  // 森で50%減速
                        mudSpeedMul = 0.3f,     // 泥で30%大減速
                        iceSpeedMul = 1.0f,
                        lavaSpeedMul = 0.7f,
                        iceSlipFactor = 0.85f,  // 氷で大スリップ
                        lavaDamageReduction = 0.0f
                    };

                case LegDeviceType.Tire:
                    return new LegMovementConfig
                    {
                        type = LegDeviceType.Tire,
                        legName = "High-Speed Tires",
                        baseSpeedMultiplier = 1.0f,
                        baseTurnMultiplier = 1.0f,
                        normalSpeedMul = 1.0f,
                        forestSpeedMul = 0.4f,  // 木枝で失速 (40%)
                        mudSpeedMul = 0.2f,     // 泥で空転・スタック (20%)
                        iceSpeedMul = 1.0f,
                        lavaSpeedMul = 0.7f,
                        iceSlipFactor = 0.6f,   // ドリフト
                        lavaDamageReduction = 0.0f
                    };

                case LegDeviceType.Crawler:
                    return new LegMovementConfig
                    {
                        type = LegDeviceType.Crawler,
                        legName = "Heavy Crawler Tracks",
                        baseSpeedMultiplier = 1.0f,
                        baseTurnMultiplier = 1.0f,
                        normalSpeedMul = 1.0f,
                        normalTurnMul = 1.0f,
                        forestSpeedMul = 1.0f, // 走破性最強: 地形影響完全無効 (森でも100%走行)
                        forestTurnMul = 1.0f,
                        mudSpeedMul = 1.0f,    // 走破性最強: 泥沼でも100%走行・スタックなし
                        mudTurnMul = 1.0f,
                        iceSpeedMul = 1.0f,    // 走破性最強: 氷上でも100%グリップ
                        iceTurnMul = 1.0f,
                        lavaSpeedMul = 1.0f,   // 溶岩でも100%走行
                        lavaTurnMul = 1.0f,
                        iceSlipFactor = 0.0f,  // スパイク履帯でスリップ完全ゼロ
                        lavaDamageReduction = 0.8f // 耐熱キャタピラで溶岩ダメージ80%カット
                    };

                default:
                    return new LegMovementConfig();
            }
        }
    }
}
