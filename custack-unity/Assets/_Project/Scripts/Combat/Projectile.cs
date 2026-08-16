using UnityEngine;

namespace Custack.Combat
{
    /// <summary>
    /// 直線移動する飛翔体（ピストル・ガトリング弾）の基底コンポーネント。
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [Header("弾丸パラメータ")]
        public float speed = 20f;
        public float damage = 15f;
        public float lifeTime = 2.0f;
        public int ownerRobotId = -1;
        public Color bulletColor = Color.yellow;

        private float spawnTime;
        private Vector2 moveDirection;
        private Rigidbody2D rb;

        public virtual void Initialize(int ownerId, Vector2 direction, float speedVal, float dmgVal, float lifeVal, Color col)
        {
            ownerRobotId = ownerId;
            moveDirection = direction.normalized;
            speed = speedVal;
            damage = dmgVal;
            lifeTime = lifeVal;
            bulletColor = col;
            spawnTime = Time.time;

            // 弾の向きを設定
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            // SpriteRenderer があれば色を設定
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = bulletColor;
        }

        protected virtual void Start()
        {
            spawnTime = Time.time;
            rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.linearVelocity = moveDirection * speed;
            }
        }

        protected virtual void Update()
        {
            if (rb == null)
            {
                transform.position += (Vector3)(moveDirection * speed * Time.deltaTime);
            }

            // 寿命判定
            if (Time.time - spawnTime >= lifeTime)
            {
                OnLifeTimeExpired();
            }
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            // 自身の機体との衝突は無視
            var robot = other.GetComponentInParent<Custack.Robot.RobotEntity>();
            if (robot != null && robot.RobotId == ownerRobotId) return;

            // 敵の Health を取得
            var health = other.GetComponentInParent<Health>();
            if (health != null)
            {
                health.TakeDamage(damage, transform.position);
                OnHitTarget(other.gameObject);
            }
            else
            {
                // 障害物等の場合
                OnHitObstacle();
            }
        }

        protected virtual void OnHitTarget(GameObject target)
        {
            HitEffect.CreateHitEffect(transform.position, bulletColor, 0.4f);
            Destroy(gameObject);
        }

        protected virtual void OnHitObstacle()
        {
            HitEffect.CreateHitEffect(transform.position, Color.gray, 0.3f);
            Destroy(gameObject);
        }

        protected virtual void OnLifeTimeExpired()
        {
            Destroy(gameObject);
        }
    }
}
