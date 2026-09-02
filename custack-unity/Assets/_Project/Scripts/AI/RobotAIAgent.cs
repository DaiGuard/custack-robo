using System.Collections.Generic;
using UnityEngine;
using Custack.Combat;
using Custack.Equipment;
using Custack.Input;
using Custack.Robot;
using Custack.Terrain;

namespace Custack.AI
{
    /// <summary>
    /// AIエージェントの戦術行動ステート
    /// </summary>
    public enum AICombatState
    {
        Idle = 0,         // 待機・索敵
        Engage = 1,       // 接近・間合い詰め
        Orbit = 2,        // 旋回周回・中距離射撃
        MeleeRush = 3,    // ソード近接突進
        Snipe = 4,        // 遠距離狙撃・射線合わせ
        Evade = 5         // 緊急回避・離脱
    }

    /// <summary>
    /// AIの性格・作戦方針プリセット
    /// </summary>
    public enum AIPersonality
    {
        Balanced = 0,     // 標準バランス型
        Aggressive = 1,   // 積極突進型（間合い詰め・高頻度攻撃）
        Defensive = 2     // 慎重・引き撃ち型（距離維持・回避優先）
    }

    /// <summary>
    /// 1台のロボットを自律制御するバトルAIエージェント。
    /// 環境認識、敵機追尾、最適交戦距離の維持、精密エイム、自動射撃、危険地形・弾丸回避を実行します。
    /// </summary>
    public class RobotAIAgent
    {
        public int RobotId { get; private set; }
        public AIPersonality Personality { get; set; } = AIPersonality.Balanced;
        public AICombatState CurrentState { get; private set; } = AICombatState.Idle;
        public RobotEntity TargetRobot { get; private set; }
        public float TargetDistance { get; private set; } = 0f;

        // AI 思考用タイマー & パラメータ
        private float strafeDirection = 1f;
        private float nextStrafeChangeTime = 0f;
        private float nextEvadeCheckTime = 0f;
        private Vector2 evadeVector = Vector2.zero;
        private float evadeUntilTime = 0f;

        // アリーナ外周限界（ソフト境界）
        private const float ArenaLimitX = 5.2f;
        private const float ArenaLimitY = 3.2f;

        public RobotAIAgent(int robotId, AIPersonality personality = AIPersonality.Balanced)
        {
            RobotId = robotId;
            Personality = personality;
        }

        /// <summary>
        /// 毎フレームのAI思考を実行し、PlayerInputCommand を生成して返却
        /// </summary>
        public PlayerInputCommand GenerateCommand()
        {
            PlayerInputCommand cmd = default;
            cmd.IsConnected = true;

            var robotMgr = RobotManager.Instance;
            if (robotMgr == null) return cmd;

            // 自機の RobotEntity を取得
            RobotEntity self = (RobotId < robotMgr.robots.Count) ? robotMgr.robots[RobotId] : null;
            if (self == null || self.HealthComponent == null || self.HealthComponent.IsDead)
            {
                CurrentState = AICombatState.Idle;
                return cmd;
            }

            // 1. ターゲット敵機の選定 & 距離・方位計算
            SelectTarget(self, robotMgr);
            if (TargetRobot == null)
            {
                CurrentState = AICombatState.Idle;
                return cmd;
            }

            Vector2 selfPos = self.transform.position;
            Vector2 targetPos = TargetRobot.transform.position;
            Vector2 toTarget = targetPos - selfPos;
            TargetDistance = toTarget.magnitude;
            Vector2 dirToTarget = TargetDistance > 0.001f ? toTarget.normalized : Vector2.right;

            // 2. 装備構成に基づく理想交戦距離の決定
            float idealDist = DetermineIdealDistance(self);

            // 3. AIステートマシンの更新
            UpdateState(self, idealDist);

            // 4. 移動ベクトル (World -> Robot Local) の計算
            Vector2 worldMove = CalculateWorldMoveVector(self, targetPos, dirToTarget, idealDist);
            
            // ロボットのローカル座標系へ射影 (前方: transform.right, 左: transform.up)
            // Move.y = 前後 (Vx), Move.x = 左右 (Vy)
            Vector2 forwardDir = self.transform.right;
            Vector2 leftDir = self.transform.up;

            float vx = Vector2.Dot(worldMove, forwardDir);
            float vy = Vector2.Dot(worldMove, -leftDir); // +x: 右スライド, -x: 左スライド

            cmd.Move = new Vector2(Mathf.Clamp(vy, -1f, 1f), Mathf.Clamp(vx, -1f, 1f));

            // 5. 旋回 (Omega) の計算: 敵機へのPDエイム
            float targetAngleDeg = Mathf.Atan2(dirToTarget.y, dirToTarget.x) * Mathf.Rad2Deg;
            float currentAngleDeg = self.transform.eulerAngles.z;
            float angleErrorDeg = Mathf.DeltaAngle(currentAngleDeg, targetAngleDeg);

            // 比例ゲイン (誤差が大きい時はフル旋回、小さい時は精密微調整)
            float omega = Mathf.Clamp(angleErrorDeg * 0.04f, -1f, 1f);
            if (Mathf.Abs(angleErrorDeg) < 1.5f) omega = 0f;
            cmd.Omega = omega;

            // 6. 武器攻撃トリガー (左右アーム独立判定)
            EvaluateWeapons(self, angleErrorDeg, ref cmd);

            return cmd;
        }

        /// <summary>
        /// 生存している最も近い敵機を自動ロックオン
        /// </summary>
        private void SelectTarget(RobotEntity self, RobotManager robotMgr)
        {
            if (TargetRobot != null && TargetRobot.HealthComponent != null && !TargetRobot.HealthComponent.IsDead && TargetRobot.gameObject.activeInHierarchy)
            {
                // 現在のターゲットが生存していれば維持
                return;
            }

            TargetRobot = null;
            float nearestDist = float.MaxValue;
            Vector2 selfPos = self.transform.position;

            for (int i = 0; i < robotMgr.robots.Count; i++)
            {
                var candidate = robotMgr.robots[i];
                if (candidate == null || candidate.RobotId == RobotId) continue;
                if (!robotMgr.IsRobotEnabled(candidate.RobotId)) continue;
                if (candidate.HealthComponent == null || candidate.HealthComponent.IsDead) continue;
                if (!candidate.gameObject.activeInHierarchy) continue;

                float d = Vector2.Distance(selfPos, candidate.transform.position);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    TargetRobot = candidate;
                }
            }
        }

        /// <summary>
        /// 装備武器から最適交戦距離を算出
        /// </summary>
        private float DetermineIdealDistance(RobotEntity self)
        {
            var equip = self.EquipmentComponent;
            if (equip == null) return 3.0f;

            bool hasSword = (equip.RightArmType == ArmDeviceType.Sword || equip.LeftArmType == ArmDeviceType.Sword);
            bool hasCannon = (equip.RightArmType == ArmDeviceType.Cannon || equip.LeftArmType == ArmDeviceType.Cannon);
            bool hasGatling = (equip.RightArmType == ArmDeviceType.Gatling || equip.LeftArmType == ArmDeviceType.Gatling);

            if (hasSword && !hasCannon)
            {
                // ソード主体: 突進近接 (0.8m 〜 1.5m)
                return Personality == AIPersonality.Aggressive ? 0.9f : 1.3f;
            }
            if (hasCannon)
            {
                // キャノン主体: 遠距離狙撃 (5.0m 〜 6.5m)
                return Personality == AIPersonality.Defensive ? 6.0f : 5.0f;
            }
            if (hasGatling)
            {
                // ガトリング主体: 中距離制圧 (3.0m 〜 4.5m)
                return Personality == AIPersonality.Aggressive ? 2.8f : 3.8f;
            }

            return 3.5f;
        }

        /// <summary>
        /// AIステートマシンの更新
        /// </summary>
        private void UpdateState(RobotEntity self, float idealDist)
        {
            // ピンチ時または溶岩侵入時の緊急回避判定
            if (Time.time < evadeUntilTime)
            {
                CurrentState = AICombatState.Evade;
                return;
            }

            var equip = self.EquipmentComponent;
            bool hasSword = equip != null && (equip.RightArmType == ArmDeviceType.Sword || equip.LeftArmType == ArmDeviceType.Sword);
            bool hasCannon = equip != null && (equip.RightArmType == ArmDeviceType.Cannon || equip.LeftArmType == ArmDeviceType.Cannon);

            if (hasSword && TargetDistance < 2.5f)
            {
                CurrentState = AICombatState.MeleeRush;
            }
            else if (hasCannon && TargetDistance > 4.0f)
            {
                CurrentState = AICombatState.Snipe;
            }
            else if (Mathf.Abs(TargetDistance - idealDist) < 1.2f)
            {
                CurrentState = AICombatState.Orbit;
            }
            else
            {
                CurrentState = AICombatState.Engage;
            }
        }

        /// <summary>
        /// ワールド空間での目標移動ベクトル（ポテンシャル法）
        /// </summary>
        private Vector2 CalculateWorldMoveVector(RobotEntity self, Vector2 targetPos, Vector2 dirToTarget, float idealDist)
        {
            Vector2 selfPos = self.transform.position;
            Vector2 result = Vector2.zero;

            // 1. 緊急回避中
            if (Time.time < evadeUntilTime && evadeVector.sqrMagnitude > 0.01f)
            {
                result += evadeVector * 1.2f;
            }
            else
            {
                // 2. ターゲットとの間合い制御ベクトル (引力 / 斥力)
                float distDiff = TargetDistance - idealDist;
                Vector2 approachVec = Vector2.zero;

                if (CurrentState == AICombatState.MeleeRush)
                {
                    // 近接突進: 一気に接近
                    approachVec = dirToTarget * 1.0f;
                }
                else if (distDiff > 0.6f)
                {
                    // 遠すぎる: 前進
                    approachVec = dirToTarget * Mathf.Clamp01(distDiff / 2.0f);
                }
                else if (distDiff < -0.6f)
                {
                    // 近すぎる: 後退
                    approachVec = -dirToTarget * Mathf.Clamp01(-distDiff / 1.5f);
                }

                // 3. 周回ストラフィング (横移動) ベクトル
                if (Time.time > nextStrafeChangeTime)
                {
                    strafeDirection = (Random.value > 0.5f) ? 1f : -1f;
                    nextStrafeChangeTime = Time.time + Random.Range(1.5f, 3.5f);
                }

                Vector2 strafeVec = new Vector2(-dirToTarget.y, dirToTarget.x) * strafeDirection;
                float strafeWeight = (CurrentState == AICombatState.Orbit) ? 0.75f : 0.35f;

                result = approachVec + strafeVec * strafeWeight;
            }

            // 4. アリーナ外周壁からの復元斥力
            if (Mathf.Abs(selfPos.x) > ArenaLimitX - 1.0f)
            {
                float pushX = -Mathf.Sign(selfPos.x) * Mathf.InverseLerp(ArenaLimitX - 1.0f, ArenaLimitX, Mathf.Abs(selfPos.x));
                result.x += pushX * 1.5f;
            }
            if (Mathf.Abs(selfPos.y) > ArenaLimitY - 0.8f)
            {
                float pushY = -Mathf.Sign(selfPos.y) * Mathf.InverseLerp(ArenaLimitY - 0.8f, ArenaLimitY, Mathf.Abs(selfPos.y));
                result.y += pushY * 1.5f;
            }

            // 5. 地形回避 (溶岩・泥)
            var terrainMgr = TerrainManager.Instance;
            if (terrainMgr != null)
            {
                TerrainType curTerrain = terrainMgr.GetTerrainAt(selfPos);
                var legCfg = self.EquipmentComponent?.CurrentLegConfig;
                bool isCrawler = legCfg != null && self.EquipmentComponent.LegType == LegDeviceType.Crawler;

                if (curTerrain == TerrainType.Lava && !isCrawler)
                {
                    // 溶岩から直ちに脱出 (中心方向へ)
                    result += -selfPos.normalized * 1.5f;
                }
            }

            // 6. 弾丸接近回避チェック (100ms周期)
            if (Time.time > nextEvadeCheckTime)
            {
                nextEvadeCheckTime = Time.time + 0.12f;
                CheckIncomingProjectiles(selfPos);
            }

            return result.normalized;
        }

        /// <summary>
        /// 自機に迫る敵の弾丸を検知し緊急ステップ回避
        /// </summary>
        private void CheckIncomingProjectiles(Vector2 selfPos)
        {
            var projectiles = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            for (int i = 0; i < projectiles.Length; i++)
            {
                var p = projectiles[i];
                if (p == null || p.ownerRobotId == RobotId) continue;

                Vector2 pPos = p.transform.position;
                float d = Vector2.Distance(selfPos, pPos);
                if (d < 3.0f)
                {
                    // 弾丸の進行方向と自機へのベクトル
                    Vector2 bulletDir = p.transform.right; // Projectile direction
                    Vector2 toSelf = selfPos - pPos;
                    float dot = Vector2.Dot(bulletDir, toSelf.normalized);

                    if (dot > 0.7f) // 自機に向かって飛んできている
                    {
                        // 弾丸の進行方向に対して直角にステップ回避
                        Vector2 dodgeDir = new Vector2(-bulletDir.y, bulletDir.x);
                        if (Vector2.Dot(dodgeDir, toSelf) < 0) dodgeDir = -dodgeDir;

                        evadeVector = dodgeDir;
                        evadeUntilTime = Time.time + 0.45f;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 射角・射程判定による左右武器の発射判定
        /// </summary>
        private void EvaluateWeapons(RobotEntity self, float angleErrorDeg, ref PlayerInputCommand cmd)
        {
            var equip = self.EquipmentComponent;
            if (equip == null) return;

            float absAngle = Mathf.Abs(angleErrorDeg);

            // --- 右武器判定 ---
            cmd.ArmRightPressed = ShouldFireWeapon(equip.RightArmType, equip.CurrentRightArmConfig, absAngle, TargetDistance);
            cmd.ArmRightHeld = cmd.ArmRightPressed;

            // --- 左武器判定 ---
            cmd.ArmLeftPressed = ShouldFireWeapon(equip.LeftArmType, equip.CurrentLeftArmConfig, absAngle, TargetDistance);
            cmd.ArmLeftHeld = cmd.ArmLeftPressed;
        }

        private bool ShouldFireWeapon(ArmDeviceType type, ArmWeaponConfig cfg, float absAngleDeg, float dist)
        {
            switch (type)
            {
                case ArmDeviceType.Gatling:
                    // 拡散連射: 正面 ±22度以内 & 距離 5.5m 以内
                    return absAngleDeg < 22f && dist <= 5.5f;

                case ArmDeviceType.Sword:
                    // 近接斬撃: 正面 ±65度以内 & 距離 1.85m 以内
                    return absAngleDeg < 65f && dist <= 1.85f;

                case ArmDeviceType.Cannon:
                    // 精密キャノン: 正面 ±10度以内 & 距離 8.0m 以内
                    return absAngleDeg < 10f && dist <= 8.0f;

                default:
                    return false;
            }
        }

        /// <summary>
        /// UI 表示用の AI 状態サマリー文字列を取得
        /// </summary>
        public string GetStatusString()
        {
            string stateStr = CurrentState switch
            {
                AICombatState.Engage => "接近追従",
                AICombatState.Orbit => "旋回周回",
                AICombatState.MeleeRush => "近接突進",
                AICombatState.Snipe => "長距離狙撃",
                AICombatState.Evade => "緊急回避",
                _ => "索敵待機"
            };

            if (TargetRobot != null)
            {
                return $"[{stateStr}] 🎯#{TargetRobot.RobotId} ({TargetDistance:F1}m)";
            }
            return $"[{stateStr}] 待機中";
        }
    }
}
