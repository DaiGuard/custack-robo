using System;
using UnityEngine;

namespace Custack.Combat
{
    /// <summary>
    /// 機体の HP 管理、被弾ダメージ、スタン、無敵時間、撃破イベントを処理するコンポーネント。
    /// 最大 HP 1000、一定ダメージ蓄積によるスタン（移動停止）、無敵時間中の円点滅に対応。
    /// </summary>
    public class Health : MonoBehaviour
    {
        [Header("HP 設定")]
        public float maxHp = 1000f;
        public float currentHp = 1000f;

        [Header("スタン (Stun) 設定")]
        [Tooltip("スタンが発動する累計被ダメージ閾値 (default: 100)")]
        public float stunDamageThreshold = 100f;
        [Tooltip("被ダメージ蓄積がリセットされる時間 (秒)")]
        public float stunThresholdWindow = 1.2f;
        [Tooltip("スタン継続時間 (秒: この間は移動速度0)")]
        public float stunDuration = 1.5f;

        [Header("無敵時間 (Invincibility) 設定")]
        [Tooltip("スタン発生時に付与される無敵時間 (秒)")]
        public float invincibilityAfterStun = 2.0f;
        [Tooltip("通常被弾時の微小無敵時間 (秒)")]
        public float hitInvincibilityDuration = 0.08f;

        public bool IsDead => currentHp <= 0f;
        public bool IsStunned => Time.time < stunEndTime;
        public bool IsInvincible => Time.time < invincibilityEndTime;
        public float HealthPercent => Mathf.Clamp01(currentHp / maxHp);
        public float StunRemaining => Mathf.Max(0f, stunEndTime - Time.time);
        public float InvincibleRemaining => Mathf.Max(0f, invincibilityEndTime - Time.time);

        public event Action<float, float> OnHealthChanged; // (current, max)
        public event Action<float, Vector2> OnDamaged;      // (damage, hitPoint)
        public event Action OnStunned;
        public event Action OnStunRecovered;
        public event Action OnDeath;
        public event Action OnRespawn;

        private float stunEndTime = 0f;
        private float invincibilityEndTime = 0f;
        private float accumulatedDamage = 0f;
        private float recentDamageResetTime = 0f;
        private bool wasStunned = false;

        void Awake()
        {
            currentHp = maxHp;
        }

        void Update()
        {
            // スタンからの復帰検知
            if (wasStunned && !IsStunned)
            {
                wasStunned = false;
                OnStunRecovered?.Invoke();
            }
        }

        /// <summary>
        /// ダメージ適用処理
        /// </summary>
        public void TakeDamage(float damage, Vector2 hitPoint = default)
        {
            if (IsDead || IsInvincible) return;

            currentHp = Mathf.Max(0f, currentHp - damage);

            // 短時間内の被ダメージを蓄積
            if (Time.time > recentDamageResetTime)
            {
                accumulatedDamage = 0f;
            }
            accumulatedDamage += damage;
            recentDamageResetTime = Time.time + stunThresholdWindow;

            // スタン判定 (閾値到達)
            if (accumulatedDamage >= stunDamageThreshold && !IsStunned)
            {
                TriggerStun();
            }
            else
            {
                // 通常の短い無敵時間 (連続被弾の多重判定防止)
                invincibilityEndTime = Mathf.Max(invincibilityEndTime, Time.time + hitInvincibilityDuration);
            }

            OnDamaged?.Invoke(damage, hitPoint);
            OnHealthChanged?.Invoke(currentHp, maxHp);

            if (IsDead)
            {
                OnDeath?.Invoke();
            }
        }

        /// <summary>
        /// 強制スタン発動 (一定時間移動速度0 & 無敵付与)
        /// </summary>
        public void TriggerStun(float customDuration = -1f, float customInvincible = -1f)
        {
            if (IsDead) return;

            float dur = customDuration > 0 ? customDuration : stunDuration;
            float invDur = customInvincible > 0 ? customInvincible : (dur + invincibilityAfterStun);

            stunEndTime = Time.time + dur;
            invincibilityEndTime = Time.time + invDur;
            accumulatedDamage = 0f;
            wasStunned = true;

            OnStunned?.Invoke();
        }

        public void Heal(float amount)
        {
            if (IsDead) return;

            currentHp = Mathf.Min(maxHp, currentHp + amount);
            OnHealthChanged?.Invoke(currentHp, maxHp);
        }

        public void Respawn(float resetHp = -1f)
        {
            currentHp = resetHp > 0 ? resetHp : maxHp;
            stunEndTime = 0f;
            invincibilityEndTime = Time.time + 1.0f; // リスポーン時1秒無敵
            accumulatedDamage = 0f;
            wasStunned = false;

            OnHealthChanged?.Invoke(currentHp, maxHp);
            OnRespawn?.Invoke();
        }
    }
}
