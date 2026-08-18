using UnityEngine;

namespace Custack.Combat
{
    /// <summary>
    /// 直線移動する飛翔体（ピストル・ガトリング弾・キャノンビーム）の基底コンポーネント。
    /// 高速移動時のすり抜け防止 Raycast 判定と OnTriggerEnter2D のハイブリッドで確実に当たり判定を処理します。
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [Header("弾丸パラメータ")]
        public float speed = 20f;
        public float damage = 15f;
        public float lifeTime = 2.0f;
        public int ownerRobotId = -1;
        public Color bulletColor = Color.yellow;

        [Header("トレイル演出")]
        public bool enableTrail = true;
        public float trailTime = 0.15f;
        public float trailStartWidth = 0.12f;
        public float trailEndWidth = 0.01f;

        protected float spawnTime;
        protected Vector2 moveDirection;
        protected Rigidbody2D rb;
        protected TrailRenderer trail;
        protected bool hasHit = false;

        public virtual void Initialize(int ownerId, Vector2 direction, float speedVal, float dmgVal, float lifeVal, Color col)
        {
            ownerRobotId = ownerId;
            moveDirection = direction.normalized;
            speed = speedVal;
            damage = dmgVal;
            lifeTime = lifeVal;
            bulletColor = col;
            spawnTime = Time.time;
            hasHit = false;

            // 弾の向きを設定
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            // Rigidbody2D & Collider2D の安全確保
            rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;

            var col2d = GetComponent<Collider2D>();
            if (col2d == null)
            {
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(0.4f, 0.2f);
                box.isTrigger = true;
            }
            else
            {
                col2d.isTrigger = true;
            }

            // レンダラーの色・マテリアル設定
            var mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material = EffectFactory.GetParticleAdditiveMaterial();
            }

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = bulletColor;
            }

            // 弾道トレイルの追加 & 設定
            if (enableTrail)
            {
                SetupTrail();
            }
        }

        protected virtual void SetupTrail()
        {
            trail = gameObject.GetComponent<TrailRenderer>();
            if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();

            trail.time = trailTime;
            trail.startWidth = trailStartWidth;
            trail.endWidth = trailEndWidth;
            trail.material = EffectFactory.GetParticleAdditiveMaterial();
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(bulletColor, 0.3f),
                    new GradientColorKey(bulletColor * 0.7f, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            trail.colorGradient = grad;
        }

        protected virtual void Start()
        {
            spawnTime = Time.time;
        }

        protected virtual void Update()
        {
            if (hasHit) return;

            // 1. Raycast による高速すり抜け防止判定 (フレーム間移動ベクトル)
            float stepDistance = speed * Time.deltaTime;
            Vector2 currentPos = transform.position;
            RaycastHit2D[] hits = Physics2D.RaycastAll(currentPos, moveDirection, stepDistance);

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.gameObject == gameObject) continue;

                // 自身の機体は無視
                var robot = hit.collider.GetComponentInParent<Robot.RobotEntity>();
                if (robot != null && robot.RobotId == ownerRobotId) continue;

                var health = hit.collider.GetComponentInParent<Health>();
                if (health != null && !health.IsDead)
                {
                    hasHit = true;
                    transform.position = hit.point;
                    health.TakeDamage(damage, hit.point);
                    OnHitTarget(hit.collider.gameObject);
                    return;
                }
            }

            // 2. 移動反映
            transform.position += (Vector3)(moveDirection * stepDistance);

            // 3. 寿命判定
            if (Time.time - spawnTime >= lifeTime)
            {
                OnLifeTimeExpired();
            }
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (hasHit || other.gameObject == gameObject) return;

            // 自身の機体との衝突は無視
            var robot = other.GetComponentInParent<Robot.RobotEntity>();
            if (robot != null && robot.RobotId == ownerRobotId) return;

            // 敵の Health を取得
            var health = other.GetComponentInParent<Health>();
            if (health != null && !health.IsDead)
            {
                hasHit = true;
                Vector2 hitPoint = other.ClosestPoint(transform.position);
                health.TakeDamage(damage, hitPoint);
                OnHitTarget(other.gameObject);
            }
            else if (!other.isTrigger)
            {
                hasHit = true;
                OnHitObstacle();
            }
        }

        protected virtual void OnHitTarget(GameObject target)
        {
            EffectFactory.PlayHitSparks(transform.position, -moveDirection, bulletColor, 1.0f, 14);
            Destroy(gameObject);
        }

        protected virtual void OnHitObstacle()
        {
            EffectFactory.PlayHitSparks(transform.position, -moveDirection, Color.gray, 0.6f, 8);
            Destroy(gameObject);
        }

        protected virtual void OnLifeTimeExpired()
        {
            Destroy(gameObject);
        }
    }
}
