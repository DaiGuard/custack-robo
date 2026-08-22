using System.Collections.Generic;
using UnityEngine;
using Custack.Equipment;

namespace Custack.Terrain
{
    /// <summary>
    /// フィールド上の全地形ゾーンを統括し、
    /// ロボットの座標と脚ユニット種別に応じた移動値補正および地形ダメージを計算するマネージャー。
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
            var zones = FindObjectsByType<TerrainZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            registeredZones.AddRange(zones);
        }

        public void RegisterZone(TerrainZone zone)
        {
            if (zone != null && !registeredZones.Contains(zone)) registeredZones.Add(zone);
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
            for (int i = 0; i < registeredZones.Count; i++)
            {
                if (registeredZones[i] != null && registeredZones[i].gameObject.activeInHierarchy && registeredZones[i].ContainsPoint(worldPos))
                {
                    return registeredZones[i].Type;
                }
            }

            return TerrainType.Normal;
        }

        /// <summary>
        /// 指定ワールド座標における地形データを取得
        /// </summary>
        public TerrainData GetTerrainDataAt(Vector2 worldPos)
        {
            for (int i = 0; i < registeredZones.Count; i++)
            {
                if (registeredZones[i] != null && registeredZones[i].gameObject.activeInHierarchy && registeredZones[i].ContainsPoint(worldPos))
                {
                    return registeredZones[i].Data;
                }
            }

            return TerrainData.CreateDefault(TerrainType.Normal);
        }

        /// <summary>
        /// 生の移動・旋回コマンドに対し、現在地の地形と脚ユニット特性に応じた適用比率（0.0〜1.0）のみを計算
        /// </summary>
        public Vector3 CalculateModifiedMovement(Vector2 worldPos, Vector2 rawMove, float rawOmega, LegMovementConfig legConfig)
        {
            float rawLateral = rawMove.x;  // スティック左右 (Vy)
            float rawForward = rawMove.y;  // スティック上下 (Vx: 前後)

            if (legConfig == null)
            {
                return new Vector3(rawForward, rawLateral, rawOmega);
            }

            // 1. 現在地の地形を取得
            TerrainType currentTerrain = GetTerrainAt(worldPos);

            // 2. 脚ユニット固有の地形適用比率（平地: 1.0）を取得
            float speedRatio = legConfig.GetSpeedMultiplier(currentTerrain);
            float turnRatio = legConfig.GetTurnMultiplier(currentTerrain);

            // 3. 地形適用の比率のみを乗算
            float finalVx = rawForward * speedRatio;
            float finalVy = rawLateral * speedRatio;
            float finalOmega = rawOmega * turnRatio;

            return new Vector3(finalVx, finalVy, finalOmega);
        }

        /// <summary>
        /// 現在地の地形による秒間スリップダメージ (DPS) を脚ユニットの耐性を考慮して計算
        /// </summary>
        public float CalculateTerrainDamage(Vector2 worldPos, LegMovementConfig legConfig)
        {
            TerrainType currentTerrain = GetTerrainAt(worldPos);
            TerrainData data = GetTerrainDataAt(worldPos);

            if (data == null || data.damagePerSecond <= 0f)
            {
                return 0f;
            }

            float damageMul = legConfig != null ? legConfig.GetDamageMultiplier(currentTerrain) : 1.0f;
            return data.damagePerSecond * damageMul;
        }
    }
}
