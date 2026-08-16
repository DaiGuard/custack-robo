using UnityEngine;
using Custack.Combat;

namespace Custack.Robot
{
    /// <summary>
    /// ロボットの視覚表現（アバター、HPバー、被弾点滅、向き矢印、ロックオンレティクル）を制御するコンポーネント。
    /// </summary>
    public class RobotVisual : MonoBehaviour
    {
        [Header("プレイヤー表示カラー")]
        public Color playerColor = Color.cyan;
        public Color hitFlashColor = Color.white;

        [Header("ロックオン表示")]
        [SerializeField]
        private bool isLockedOn = false;
        public GameObject lockOnMarkerObject;

        [Header("頭上 HP バー設定")]
        public Transform hpBarFillTransform;
        public Vector3 hpBarOffset = new Vector3(0, 0.8f, 0);

        private SpriteRenderer mainRenderer;
        private Health health;
        private float flashEndTime = 0f;

        void Awake()
        {
            mainRenderer = GetComponentInChildren<SpriteRenderer>();
            health = GetComponent<Health>();

            if (health != null)
            {
                health.OnDamaged += OnDamaged;
                health.OnHealthChanged += UpdateHpBar;
            }
        }

        public void Initialize(int playerId, Color color)
        {
            playerColor = color;
            if (mainRenderer != null) mainRenderer.color = playerColor;
        }

        public void SetLockOnStatus(bool locked)
        {
            isLockedOn = locked;
            if (lockOnMarkerObject != null)
            {
                lockOnMarkerObject.SetActive(isLockedOn);
            }
        }

        private void OnDamaged(float damage, Vector2 hitPoint)
        {
            flashEndTime = Time.time + 0.1f;
        }

        private void UpdateHpBar(float current, float max)
        {
            if (hpBarFillTransform != null)
            {
                float ratio = Mathf.Clamp01(current / max);
                Vector3 scale = hpBarFillTransform.localScale;
                scale.x = ratio;
                hpBarFillTransform.localScale = scale;
            }
        }

        void Update()
        {
            // 被弾点滅処理
            if (mainRenderer != null)
            {
                if (Time.time < flashEndTime)
                {
                    mainRenderer.color = hitFlashColor;
                }
                else
                {
                    mainRenderer.color = playerColor;
                }
            }
        }

        void OnDrawGizmos()
        {
            // ロックオンマーカーのギズモ描画
            if (isLockedOn)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(transform.position, Vector3.one * 0.9f);
                Gizmos.DrawWireSphere(transform.position, 0.6f);
            }
        }
    }
}
