using UnityEngine;

namespace Custack.Combat
{
    /// <summary>
    /// ターゲットを緩やかに追尾し、着弾時に範囲爆発を起こす誘導ミサイル。
    /// </summary>
    public class HomingMissile : Projectile
    {
        [Header("ホーミング設定")]
        public Transform targetTransform;
        public float turnSpeed = 120f;       // 旋回角速度 (度/秒)
        public float explosionRadius = 1.8f; // 爆発半径
        public float homingDelay = 0.2f;     // 発射直後の直進ディレイ

        private Vector2 currentDir;
        private float launchTime;

        public void SetTarget(Transform target, float homingTurnSpeed, float explRadius)
        {
            targetTransform = target;
            turnSpeed = homingTurnSpeed;
            explosionRadius = explRadius;
        }

        public override void Initialize(int ownerId, Vector2 direction, float speedVal, float dmgVal, float lifeVal, Color col)
        {
            base.Initialize(ownerId, direction, speedVal, dmgVal, lifeVal, col);
            currentDir = direction.normalized;
            launchTime = Time.time;
        }

        protected override void Update()
        {
            float elapsed = Time.time - launchTime;

            // ターゲットが存在し、一定時間経過後に緩やかに追尾開始
            if (targetTransform != null && elapsed >= homingDelay)
            {
                Vector2 targetDir = ((Vector2)targetTransform.position - (Vector2)transform.position).normalized;
                if (targetDir != Vector2.zero)
                {
                    // 現在の向きからターゲットの向きへ滑らかに回転
                    float currentAngle = Mathf.Atan2(currentDir.y, currentDir.x) * Mathf.Rad2Deg;
                    float targetAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
                    float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnSpeed * Time.deltaTime);

                    currentDir = new Vector2(Mathf.Cos(newAngle * Mathf.Rad2Deg), Mathf.Sin(newAngle * Mathf.Rad2Deg));
                }
            }

            // 前進移動
            transform.position += (Vector3)(currentDir * speed * Time.deltaTime);

            // ミサイルの姿勢更新
            float rotAngle = Mathf.Atan2(currentDir.y, currentDir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, rotAngle);

            // 寿命判定
            if (elapsed >= lifeTime)
            {
                OnLifeTimeExpired();
            }
        }

        protected override void OnHitTarget(GameObject target)
        {
            // 着弾時範囲爆発
            ExplosionArea.Explode(transform.position, explosionRadius, damage, ownerRobotId);
            Destroy(gameObject);
        }

        protected override void OnHitObstacle()
        {
            ExplosionArea.Explode(transform.position, explosionRadius, damage, ownerRobotId);
            Destroy(gameObject);
        }

        protected override void OnLifeTimeExpired()
        {
            ExplosionArea.Explode(transform.position, explosionRadius * 0.7f, damage * 0.5f, ownerRobotId);
            Destroy(gameObject);
        }
    }
}
