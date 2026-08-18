using UnityEngine;
using Custack.Robot;

namespace Custack.Combat
{
    /// <summary>
    /// 武器エフェクトテスト用のダミーターゲット。
    /// 被弾時に発光点滅し、スタン中の移動停止、無敵時間中の円点滅、頭上HPゲージを表示します。
    /// </summary>
    public class DummyTarget : MonoBehaviour
    {
        public float moveDistance = 0f; // 0なら静止、>0なら左右往復移動
        public float moveSpeed = 2f;
        public Color targetColor = new Color(1f, 0.3f, 0.3f);

        private Health health;
        private Vector3 startPos;
        private MeshRenderer meshRenderer;
        private float flashEndTime = 0f;
        private Transform hpBarTransform;

        void Awake()
        {
            health = GetComponent<Health>();
            if (health == null) health = gameObject.AddComponent<Health>();

            health.maxHp = 1000f;
            health.currentHp = 1000f;
            startPos = transform.position;

            health.OnDamaged += OnDamaged;
            health.OnDeath += OnDeath;
            health.OnRespawn += OnRespawn;

            SetupColliderAndPhysics();
            SetupVisual();
        }

        private void SetupColliderAndPhysics()
        {
            // 2D コライダー & Kinematic Rigidbody を確実に設定
            var col = GetComponent<CircleCollider2D>();
            if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;
            col.isTrigger = true;

            var rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
        }

        private void SetupVisual()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = RobotMeshHelper.GetOrCreateCircleMesh(0.45f);

            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.material = EffectFactory.GetParticleAdditiveMaterial();
            meshRenderer.material.color = targetColor;

            // 簡易HPバー (頭上)
            var hpBg = new GameObject("HpBg");
            hpBg.transform.SetParent(transform);
            hpBg.transform.localPosition = new Vector3(0, 0.7f, -0.1f);
            hpBg.transform.localScale = new Vector3(0.9f, 0.12f, 1f);
            var bgMf = hpBg.AddComponent<MeshFilter>();
            bgMf.sharedMesh = RobotMeshHelper.GetOrCreateQuadMesh();
            var bgMr = hpBg.AddComponent<MeshRenderer>();
            bgMr.material = EffectFactory.GetParticleAdditiveMaterial();
            bgMr.material.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            var hpFill = new GameObject("HpFill");
            hpFill.transform.SetParent(hpBg.transform);
            hpFill.transform.localPosition = new Vector3(0, 0, -0.05f);
            hpFill.transform.localScale = Vector3.one;
            var fillMf = hpFill.AddComponent<MeshFilter>();
            fillMf.sharedMesh = RobotMeshHelper.GetOrCreateQuadMesh();
            var fillMr = hpFill.AddComponent<MeshRenderer>();
            fillMr.material = EffectFactory.GetParticleAdditiveMaterial();
            fillMr.material.color = Color.green;

            hpBarTransform = hpFill.transform;
            health.OnHealthChanged += (cur, max) =>
            {
                if (hpBarTransform != null)
                {
                    float r = Mathf.Clamp01(cur / max);
                    hpBarTransform.localScale = new Vector3(r, 1f, 1f);
                    fillMr.material.color = Color.Lerp(Color.red, Color.green, r);
                }
            };
        }

        private void OnDamaged(float damage, Vector2 hitPoint)
        {
            flashEndTime = Time.time + 0.15f;
        }

        private void OnDeath()
        {
            gameObject.SetActive(false);
        }

        private void OnRespawn()
        {
            gameObject.SetActive(true);
            transform.position = startPos;
            if (meshRenderer != null)
            {
                meshRenderer.material.color = targetColor;
            }
        }

        void Update()
        {
            // スタン中は移動停止 (指示: スタン中は移動速度を0とする)
            if (health != null && !health.IsStunned && moveDistance > 0f)
            {
                float offset = Mathf.Sin(Time.time * moveSpeed) * moveDistance;
                transform.position = startPos + new Vector3(offset, 0, 0);
            }

            // 視覚状態 (被弾白点滅 / スタン黄色点滅 / 無敵時間点滅 / 通常)
            if (meshRenderer != null)
            {
                if (Time.time < flashEndTime)
                {
                    meshRenderer.material.color = Color.white;
                }
                else if (health != null && health.IsStunned)
                {
                    bool blink = (Mathf.FloorToInt(Time.time * 10f) % 2) == 0;
                    meshRenderer.material.color = blink ? new Color(1f, 0.9f, 0.1f) : targetColor * 0.4f;
                }
                else if (health != null && health.IsInvincible)
                {
                    bool blink = (Mathf.FloorToInt(Time.time * 8f) % 2) == 0;
                    Color c = targetColor;
                    c.a = blink ? 1.0f : 0.2f;
                    meshRenderer.material.color = c;
                }
                else
                {
                    meshRenderer.material.color = targetColor;
                }
            }
        }
    }
}
