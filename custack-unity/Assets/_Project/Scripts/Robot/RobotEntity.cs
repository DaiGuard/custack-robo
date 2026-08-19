using System;
using System.Collections.Generic;
using UnityEngine;
using Custack.Combat;
using Custack.Equipment;
using Custack.Input;
using Custack.SharedMemory;
using Custack.Terrain;

namespace Custack.Robot
{
    /// <summary>
    /// ロボット1台の統括エンティティ。
    /// 位置姿勢の反映、コントローラ入力処理、地形補正、武器発射、ターゲットロックを管理。
    /// </summary>
    [RequireComponent(typeof(Health), typeof(RobotEquipment), typeof(RobotVisual))]
    public class RobotEntity : MonoBehaviour
    {
        [Header("ロボット識別設定")]
        public int robotId = 0; // 0: Player 1 (AprilTag 0), 1: Player 2 (AprilTag 1)
        public string playerName = "Player 1";

        [Header("移動パラメータ")]
        public float maxSpeed = 1000f; // 実機への最大速度指令 (-1000 ~ 1000)
        public float maxOmega = 1000f; // 実機への最大旋回指令 (-1000 ~ 1000)

        [Header("マズル位置 (左右アーム)")]
        public Transform rightMuzzlePoint;
        public Transform leftMuzzlePoint;

        [Header("ターゲットロックオン (ミサイル用)")]
        public List<RobotEntity> potentialTargets = new List<RobotEntity>();
        public int currentTargetIndex = -1; // -1: ノンターゲット

        [Header("最新の補正後コマンド (実機出力用)")]
        [SerializeField]
        private SingleControllerCommand currentCommand;

        public int RobotId => robotId;
        public SingleControllerCommand CurrentCommand => currentCommand;
        public Health HealthComponent { get; private set; }
        public RobotEquipment EquipmentComponent { get; private set; }
        public RobotVisual VisualComponent { get; private set; }

        private WeaponBase rightWeapon;
        private WeaponBase leftWeapon;

        void Awake()
        {
            HealthComponent = GetComponent<Health>();
            EquipmentComponent = GetComponent<RobotEquipment>();
            VisualComponent = GetComponent<RobotVisual>();

            if (EquipmentComponent != null)
            {
                EquipmentComponent.OnEquipmentChanged += RebuildWeapons;
            }
        }

        void Start()
        {
            RebuildWeapons();
        }

        /// <summary>
        /// 装備変更時に武器コンポーネントを再構築
        /// </summary>
        public void RebuildWeapons()
        {
            // 既存の武器コンポーネントを破棄
            if (rightWeapon != null) Destroy(rightWeapon);
            if (leftWeapon != null) Destroy(leftWeapon);

            // 右武器の生成
            rightWeapon = CreateWeaponComponent(EquipmentComponent.RightArmType, true, EquipmentComponent.CurrentRightArmConfig, rightMuzzlePoint);
            // 左武器の生成
            leftWeapon = CreateWeaponComponent(EquipmentComponent.LeftArmType, false, EquipmentComponent.CurrentLeftArmConfig, leftMuzzlePoint);
        }

        private WeaponBase CreateWeaponComponent(ArmDeviceType type, bool isRight, ArmWeaponConfig cfg, Transform muzzle)
        {
            WeaponBase weapon = null;
            switch (type)
            {
                case ArmDeviceType.Gatling:
                    weapon = gameObject.AddComponent<GatlingWeapon>();
                    break;
                case ArmDeviceType.Sword:
                    weapon = gameObject.AddComponent<SwordWeapon>();
                    break;
                case ArmDeviceType.Cannon:
                    weapon = gameObject.AddComponent<LaserCannonWeapon>();
                    break;
            }

            if (weapon != null)
            {
                weapon.Initialize(robotId, isRight, cfg, muzzle);
            }
            return weapon;
        }

        /// <summary>
        /// フレーム毎の入力処理 & 地形補正 & 武器発射
        /// </summary>
        public void ProcessFrame(PlayerInputCommand input, TerrainManager terrainMgr)
        {
            if (HealthComponent.IsDead)
            {
                currentCommand = default;
                currentCommand.active = 0;
                return;
            }

            // 1. スタン状態チェック (指示: スタン中は移動速度を0とする)
            if (HealthComponent != null && HealthComponent.IsStunned)
            {
                currentCommand.vx = 0;
                currentCommand.vy = 0;
                currentCommand.omega = 0;
                currentCommand.armRight = 0;
                currentCommand.armLeft = 0;
                currentCommand.active = (byte)(input.IsConnected ? 1 : 0);
                return;
            }

            // 2. △ボタンによるホーミングターゲット切り替え
            if (input.TargetSwitchPressed)
            {
                CycleTarget();
            }

            // 3. 地形特性 × 脚ユニットによる移動値補正
            Vector2 rawMove = input.Move;
            float rawOmega = input.Omega;

            Vector3 modifiedMove = rawMove;
            if (terrainMgr != null)
            {
                modifiedMove = terrainMgr.CalculateModifiedMovement(
                    transform.position, rawMove, rawOmega, EquipmentComponent.CurrentLegConfig
                );
            }

            // 4. 武器発射判定 (画面から見て時計回りに90度ずらした正面方向: transform.right)
            Transform targetTransform = GetCurrentTargetTransform();
            Vector2 forwardDir = transform.right; // 2D 前方 (時計回りに90度回転)

            // 右武器 (単発 or フルオート連射)
            if (rightWeapon != null)
            {
                bool trigger = EquipmentComponent.CurrentRightArmConfig.isAutomatic ? input.ArmRightHeld : input.ArmRightPressed;
                if (trigger)
                {
                    rightWeapon.TryFire(forwardDir, targetTransform);
                }
            }

            // 左武器 (単発 or フルオート連射)
            if (leftWeapon != null)
            {
                bool trigger = EquipmentComponent.CurrentLeftArmConfig.isAutomatic ? input.ArmLeftHeld : input.ArmLeftPressed;
                if (trigger)
                {
                    leftWeapon.TryFire(forwardDir, targetTransform);
                }
            }

            // 5. 実機向け最終コマンド構造体の構築 (-1000 〜 1000)
            // modifiedMove.x: 前後移動 (Vx: スティック上下), modifiedMove.y: 左右移動 (Vy: スティック左右), modifiedMove.z: 旋回 (Omega)
            currentCommand.vx = (short)Mathf.Clamp(Mathf.RoundToInt(modifiedMove.x * maxSpeed), -1000, 1000);
            currentCommand.vy = (short)Mathf.Clamp(Mathf.RoundToInt(modifiedMove.y * maxSpeed), -1000, 1000);
            currentCommand.omega = (short)Mathf.Clamp(Mathf.RoundToInt(modifiedMove.z * maxOmega), -1000, 1000);

            currentCommand.armRight = rightWeapon != null ? rightWeapon.HardwareArmFlag : (byte)0;
            currentCommand.armLeft = leftWeapon != null ? leftWeapon.HardwareArmFlag : (byte)0;
            currentCommand.active = (byte)(input.IsConnected ? 1 : 0);
        }

        private void CycleTarget()
        {
            if (potentialTargets == null || potentialTargets.Count == 0)
            {
                currentTargetIndex = -1;
                return;
            }

            // 次のターゲットへ切り替え (-1 -> 0 -> 1 -> ... -> -1)
            currentTargetIndex++;
            if (currentTargetIndex >= potentialTargets.Count)
            {
                currentTargetIndex = -1; // ロック解除
            }

            // ターゲットマーカー表示の更新 & SE再生
            UpdateTargetMarkers();
            Custack.Audio.AudioManager.Instance?.PlaySE(Custack.Audio.SoundEffectType.LockOn, 0.75f, 0.05f);
        }

        private void UpdateTargetMarkers()
        {
            for (int i = 0; i < potentialTargets.Count; i++)
            {
                if (potentialTargets[i] != null && potentialTargets[i].VisualComponent != null)
                {
                    potentialTargets[i].VisualComponent.SetLockOnStatus(i == currentTargetIndex);
                }
            }
        }

        public Transform GetCurrentTargetTransform()
        {
            if (currentTargetIndex >= 0 && currentTargetIndex < potentialTargets.Count)
            {
                var target = potentialTargets[currentTargetIndex];
                if (target != null && !target.HealthComponent.IsDead)
                {
                    return target.transform;
                }
            }
            return null;
        }

        [Header("トラッキング & 速度予測 (見失い補間)")]
        [Tooltip("最後にマーカを受信した時刻")]
        public float lastDetectedTime = -1f;
        [Tooltip("現在の推定ワールド移動速度 (m/s)")]
        public Vector3 estimatedVelocity = Vector3.zero;
        [Tooltip("現在の推定角速度 (deg/s)")]
        public float estimatedAngularVelocity = 0f;
        [Tooltip("一時的な未検出時に慣性移動を継続する最大秒数 (default: 0.35s)")]
        public float maxExtrapolationDuration = 0.35f;
        [Tooltip("速度フィードフォワード先回り時間 (秒) (プロジェクター遅延相殺 default: 0.035s = 35ms)")]
        public float leadTimeSeconds = 0.035f;

        private Vector3 lastRawWorldPos;
        private float lastRawRotDeg;
        private float lastRawPoseTime;
        private bool hasReceivedPose = false;

        /// <summary>
        /// 共有メモリから受信した位置姿勢を反映 (速度推定 & スムージング & 遅延先回り補正)
        /// </summary>
        public void ApplyPose(Vector3 worldPos, float unityRotDeg)
        {
            float now = Time.time;
            if (hasReceivedPose)
            {
                float dt = now - lastRawPoseTime;
                if (dt > 0.001f && dt < 0.2f)
                {
                    // 瞬間速度と角速度を計算
                    Vector3 instantVel = (worldPos - lastRawWorldPos) / dt;
                    float instantAngVel = Mathf.DeltaAngle(lastRawRotDeg, unityRotDeg) / dt;

                    // 急激なワープ・ノイズを弾く (速度が 8m/s 以上の異常値は無視)
                    if (instantVel.magnitude < 8.0f)
                    {
                        // 指数移動平均 (EMA) で速度を滑らかに更新
                        estimatedVelocity = Vector3.Lerp(estimatedVelocity, instantVel, 0.4f);
                        estimatedAngularVelocity = Mathf.Lerp(estimatedAngularVelocity, instantAngVel, 0.4f);
                    }
                }
            }

            lastRawWorldPos = worldPos;
            lastRawRotDeg = unityRotDeg;
            lastRawPoseTime = now;
            lastDetectedTime = now;
            hasReceivedPose = true;

            // プロジェクターの描画遅延（約35ms）を相殺するため、速度ベクトル分だけわずかに先回り（Lead）して投影
            Vector3 leadPos = worldPos + estimatedVelocity * leadTimeSeconds;
            float leadDeg = unityRotDeg + estimatedAngularVelocity * leadTimeSeconds;

            transform.position = leadPos;
            transform.rotation = Quaternion.Euler(0, 0, leadDeg);
        }

        /// <summary>
        /// 未検出フレームでの慣性予測移動 (Update から毎フレーム呼び出し)
        /// </summary>
        public void UpdateExtrapolation()
        {
            if (!hasReceivedPose || lastDetectedTime < 0) return;

            float timeSinceDetection = Time.time - lastDetectedTime;

            // 受信直後のフレーム (dt <= 0.02s) は ApplyPose で更新済みなのでスキップ
            if (timeSinceDetection <= 0.02f) return;

            // 一時的な見失い期間中 (0.02s < t <= maxExtrapolationDuration) は直前速度で先回り補間
            if (timeSinceDetection <= maxExtrapolationDuration)
            {
                // 緩やかに減速 (空気抵抗・摩擦シミュレーション)
                estimatedVelocity *= Mathf.Clamp01(1.0f - Time.deltaTime * 2.5f);
                estimatedAngularVelocity *= Mathf.Clamp01(1.0f - Time.deltaTime * 2.5f);

                transform.position += estimatedVelocity * Time.deltaTime;
                transform.rotation = Quaternion.Euler(0, 0, transform.eulerAngles.z + estimatedAngularVelocity * Time.deltaTime);
            }
            else
            {
                // 長時間ロスト時は静止
                estimatedVelocity = Vector3.zero;
                estimatedAngularVelocity = 0f;
            }
        }
    }
}
