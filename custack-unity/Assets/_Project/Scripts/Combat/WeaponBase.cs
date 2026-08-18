using System;
using UnityEngine;
using Custack.Equipment;

namespace Custack.Combat
{
    /// <summary>
    /// アーム武器の基底コンポーネント。
    /// 発射クールダウン、実機アーム連動フラグ、Prefab生成、撃破時/スタン時の発射遮断を管理。
    /// </summary>
    public abstract class WeaponBase : MonoBehaviour
    {
        [Header("武器共通設定")]
        public ArmDeviceType weaponType = ArmDeviceType.None;
        public ArmWeaponConfig config;
        public Transform firePoint; // 弾丸生成位置

        public bool IsRightArm = true; // 右アームか左アームか
        public int OwnerRobotId = 0;

        public bool CanFire => Time.time >= nextFireTime;
        public float CooldownProgress => Mathf.Clamp01((nextFireTime - Time.time) / (config != null ? config.cooldown : 1f));

        // 実機アーム連動フラグ (発射直後に 1 になり、一定時間で 0 に戻る)
        public byte HardwareArmFlag { get; protected set; } = 0;

        protected float nextFireTime = 0f;
        protected float armActiveEndTime = 0f;
        protected Health ownerHealth;

        public virtual void Initialize(int ownerId, bool isRight, ArmWeaponConfig cfg, Transform muzzlePoint)
        {
            OwnerRobotId = ownerId;
            IsRightArm = isRight;
            config = cfg;
            firePoint = muzzlePoint != null ? muzzlePoint : transform;
            weaponType = cfg != null ? cfg.type : ArmDeviceType.None;
            ownerHealth = GetComponent<Health>() ?? GetComponentInParent<Health>();
        }

        protected virtual void Update()
        {
            // 実機アームフラグのリセットタイマー
            if (HardwareArmFlag > 0 && Time.time >= armActiveEndTime)
            {
                HardwareArmFlag = 0;
            }
        }

        /// <summary>
        /// 発射トリガー（単発・連射共通）
        /// HP 0 (撃破) または スタン中は発射を完全遮断。
        /// </summary>
        public bool TryFire(Vector2 forwardDir, Transform targetTransform = null)
        {
            if (!CanFire) return false;

            // オーナーの Health が撃破済み (HP 0) またはスタン中の場合は発射不可
            if (ownerHealth == null) ownerHealth = GetComponent<Health>() ?? GetComponentInParent<Health>();
            if (ownerHealth != null && (ownerHealth.IsDead || ownerHealth.IsStunned))
            {
                HardwareArmFlag = 0;
                return false;
            }

            nextFireTime = Time.time + (config != null ? config.cooldown : 0.3f);
            
            // 実機アームを 0.15秒間 駆動フラグ ON
            HardwareArmFlag = 1;
            armActiveEndTime = Time.time + 0.15f;

            ExecuteFire(forwardDir, targetTransform);
            return true;
        }

        protected abstract void ExecuteFire(Vector2 forwardDir, Transform targetTransform);
    }
}
