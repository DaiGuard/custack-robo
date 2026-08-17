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

        [Header("AprilTag 番号表示")]
        public TextMesh tagIdTextMesh;
        [Tooltip("機体が回転しても番号は常に正位置（上向き）を保つ")]
        public bool keepTextUpright = true;

        [Header("ロックオン表示")]
        [SerializeField]
        private bool isLockedOn = false;
        public GameObject lockOnMarkerObject;

        [Header("頭上 HP バー設定")]
        public Transform hpBarFillTransform;
        public Vector3 hpBarOffset = new Vector3(0, 0.8f, 0);

        private SpriteRenderer spriteRenderer;
        private Renderer meshOrGeneralRenderer;
        private Health health;
        private float flashEndTime = 0f;

        void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            meshOrGeneralRenderer = GetComponentInChildren<MeshRenderer>();
            if (meshOrGeneralRenderer == null && spriteRenderer != null)
            {
                meshOrGeneralRenderer = spriteRenderer;
            }

            if (tagIdTextMesh == null)
            {
                tagIdTextMesh = GetComponentInChildren<TextMesh>();
            }

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
            ApplyColor(playerColor);

            if (tagIdTextMesh != null)
            {
                tagIdTextMesh.text = playerId.ToString();
            }
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

        private void ApplyColor(Color col)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = col;
            }
            else if (meshOrGeneralRenderer != null && meshOrGeneralRenderer.material != null)
            {
                meshOrGeneralRenderer.material.color = col;
            }
        }

        void Update()
        {
            // 被弾点滅処理
            if (Time.time < flashEndTime)
            {
                ApplyColor(hitFlashColor);
            }
            else
            {
                ApplyColor(playerColor);
            }
        }

        void LateUpdate()
        {
            // 機体が回転しても番号は常に正位置（上向き）を保つ
            if (keepTextUpright && tagIdTextMesh != null)
            {
                tagIdTextMesh.transform.rotation = Quaternion.identity;
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
