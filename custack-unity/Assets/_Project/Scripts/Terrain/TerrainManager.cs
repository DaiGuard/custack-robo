using System.Collections.Generic;
using UnityEngine;
using Custack.Equipment;

namespace Custack.Terrain
{
    /// <summary>
    /// フィールド上の全地形ゾーンを統括し、
    /// ロボットの座標と脚ユニット種別に応じた移動値補正を計算するマネージャー。
    /// </summary>
    public class TerrainManager : MonoBehaviour
    {
        public static TerrainManager Instance { get; private set; }

        private readonly List<TerrainZone> registeredZones = new List<TerrainZone>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            RefreshZones();
        }

        public void RefreshZones()
        {
            registeredZones.Clear();
            var zones = FindObjectsByType<TerrainZone>(FindObjectsSortMode.None);
            registeredZones.AddRange(zones);
        }

        public void RegisterZone(TerrainZone zone)
        {
            if (!registeredZones.Contains(zone)) registeredZones.Add(zone);
        }

        public void UnregisterZone(TerrainZone zone)
        {
            registeredZones.Remove(zone);
        }

        /// <summary>
        /// 指定ワールド座標における地形タイプを取得
        /// </summary>
        public TerrainType GetTerrainAt(Vector2 worldPos)
        {
            // 登録ゾーンを優先度順または最後に見つかったもので判定
            for (int i = 0; i < registeredZones.Count; i++)
            {
                if (registeredZones[i] != null && registeredZones[i].ContainsPoint(worldPos))
                {
                    return registeredZones[i].Type;
                }
            }

            return TerrainType.Normal;
        }

        /// <summary>
        /// 生の移動・旋回コマンドに対し、現在地の地形と脚ユニット特性を考慮した補正コマンドを計算
        /// </summary>
        public Vector3 CalculateModifiedMovement(Vector2 worldPos, Vector2 rawMove, float rawOmega, LegMovementConfig legConfig)
        {
            if (legConfig == null)
            {
                return new Vector3(rawMove.x, rawMove.y, rawOmega);
            }

            // 1. 現在地の地形を取得
            TerrainType currentTerrain = GetTerrainAt(worldPos);

            // 2. 脚ユニット固有の地形耐性倍率を取得
            float speedMul = legConfig.GetSpeedMultiplier(currentTerrain) * legConfig.baseSpeedMultiplier;
            float turnMul = legConfig.GetTurnMultiplier(currentTerrain) * legConfig.baseTurnMultiplier;

            // 3. タイヤなどの横移動制限
            Vector2 adjustedMove = rawMove;
            if (!legConfig.allowLateralMovement)
            {
                adjustedMove.x *= 0.15f; // 横移動を大幅制限
            }

            // 4. 最終速度計算
            float finalVx = adjustedMove.x * speedMul;
            float finalVy = adjustedMove.y * speedMul;
            float finalOmega = rawOmega * turnMul;

            return new Vector3(finalVx, finalVy, finalOmega);
        }
    }
}
