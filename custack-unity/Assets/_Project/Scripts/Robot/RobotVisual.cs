using UnityEngine;
using Custack.Combat;

namespace Custack.Robot
{
    /// <summary>
    /// ロボットの視覚表現（アバター、HPバー、被弾点滅、スタン放電、無敵時間点滅、向き矢印、ロックオンレティクル）を制御するコンポーネント。
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

        [Header("レンダラー参照")]
        [Tooltip("外周ドーナツ型リングの Renderer (プレイヤーカラー・被弾点滅・無敵点滅を適用)")]
        public Renderer donutRenderer;

        [Tooltip("中央完全黒マスクの Renderer (地形線やエフェクトを遮蔽しプロジェクター光を100%消灯)")]
        public Renderer blackMaskRenderer;

        private SpriteRenderer spriteRenderer;
        private Health health;
        private float flashEndTime = 0f;
        private MaterialPropertyBlock propertyBlock;
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            if (donutRenderer == null)
            {
                var donutObj = transform.Find("DonutRing") ?? transform.Find("Avatar");
                if (donutObj != null) donutRenderer = donutObj.GetComponent<Renderer>();
            }

            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

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

            ApplyColor(playerColor);
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

        public void ApplyColor(Color col)
        {
            if (donutRenderer != null)
            {
                if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
                donutRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(ColorPropId, col);
                propertyBlock.SetColor(BaseColorPropId, col);
                donutRenderer.SetPropertyBlock(propertyBlock);
            }
            else if (spriteRenderer != null)
            {
                spriteRenderer.color = col;
            }
        }

        void Update()
        {
            if (health != null && health.IsDead)
            {
                ApplyColor(new Color(0.2f, 0.2f, 0.2f, 0.25f)); // 撃破時消灯
                return;
            }

            // 1. 被弾直後の白点滅 (0.1s)
            if (Time.time < flashEndTime)
            {
                ApplyColor(hitFlashColor);
            }
            // 2. スタン中の黄色放電点滅
            else if (health != null && health.IsStunned)
            {
                bool blink = (Mathf.FloorToInt(Time.time * 10f) % 2) == 0;
                ApplyColor(blink ? new Color(1f, 0.9f, 0.1f) : playerColor * 0.4f);
            }
            // 3. 無敵時間中の外周円点滅 (指示: 無敵時間中はロボット周辺の円を点滅)
            else if (health != null && health.IsInvincible)
            {
                bool blink = (Mathf.FloorToInt(Time.time * 8f) % 2) == 0;
                Color invincibleColor = playerColor;
                invincibleColor.a = blink ? 1.0f : 0.15f;
                ApplyColor(invincibleColor);
            }
            // 4. 通常表示
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
