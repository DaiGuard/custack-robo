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

            // 1. △ボタンによるホーミングターゲット切り替え
            if (input.TargetSwitchPressed)
            {
                CycleTarget();
            }

            // 2. 地形特性 × 脚ユニットによる移動値補正
            Vector2 rawMove = input.Move;
            float rawOmega = input.Omega;

            Vector3 modifiedMove = rawMove;
            if (terrainMgr != null)
            {
                modifiedMove = terrainMgr.CalculateModifiedMovement(
                    transform.position, rawMove, rawOmega, EquipmentComponent.CurrentLegConfig
                );
            }

            // 3. 武器発射判定
            Transform targetTransform = GetCurrentTargetTransform();
            Vector2 forwardDir = transform.up; // 2D 前方 (上方向)

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

            // 4. 実機向け最終コマンド構造体の構築
            currentCommand.vx = (short)Mathf.Clamp(modifiedMove.x * maxSpeed, -1000f, 1000f);
            currentCommand.vy = (short)Mathf.Clamp(modifiedMove.y * maxSpeed, -1000f, 1000f);
            currentCommand.omega = (short)Mathf.Clamp(modifiedMove.z * maxOmega, -1000f, 1000f);

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

            // ターゲットマーカー表示の更新
            UpdateTargetMarkers();
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

        /// <summary>
        /// 共有メモリから受信した位置姿勢を反映
        /// </summary>
        public void ApplyPose(Vector3 worldPos, float unityRotDeg)
        {
            transform.position = worldPos;
            transform.rotation = Quaternion.Euler(0, 0, unityRotDeg);
        }
    }
}
