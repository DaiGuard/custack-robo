using System;
using UnityEngine;

namespace Custack.Combat
{
    /// <summary>
    /// 機体の HP 管理、被弾ダメージ、無敵時間、撃破イベントを処理するコンポーネント。
    /// </summary>
    public class Health : MonoBehaviour
    {
        [Header("HP 設定")]
        public float maxHp = 100f;
        public float currentHp = 100f;

        [Header("無敵時間")]
        public float invincibilityDuration = 0.4f;

        public bool IsDead => currentHp <= 0f;
        public bool IsInvincible => Time.time < invincibilityEndTime;
        public float HealthPercent => Mathf.Clamp01(currentHp / maxHp);

        public event Action<float, float> OnHealthChanged; // (current, max)
        public event Action<float, Vector2> OnDamaged;      // (damage, hitPoint)
        public event Action OnDeath;
        public event Action OnRespawn;

        private float invincibilityEndTime = 0f;

        void Awake()
        {
            currentHp = maxHp;
        }

        public void TakeDamage(float damage, Vector2 hitPoint = default)
        {
            if (IsDead || IsInvincible) return;

            currentHp = Mathf.Max(0f, currentHp - damage);
            invincibilityEndTime = Time.time + invincibilityDuration;

            OnDamaged?.Invoke(damage, hitPoint);
            OnHealthChanged?.Invoke(currentHp, maxHp);

            if (IsDead)
            {
                OnDeath?.Invoke();
            }
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
            invincibilityEndTime = Time.time + 1.0f; // リスポーン時1秒無敵
            OnHealthChanged?.Invoke(currentHp, maxHp);
            OnRespawn?.Invoke();
        }
    }
}
