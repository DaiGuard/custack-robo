using System.Collections.Generic;
using UnityEngine;

namespace Custack.Combat
{
    /// <summary>
    /// 近接武器ソードの三日月型斬撃スイングエフェクト & 当たり判定。
    /// ロボットの周辺で高速に扇形・三日月状の弧を描いてスイングし、近接範囲の敵を斬撃します。
    /// </summary>
    public class MeleeSwordSlash : MonoBehaviour
    {
        [Header("スイングパラメータ")]
        public float swingDuration = 0.18f;     // スイング時間 (秒)
        public float startAngleOffset = -65f;   // スイング開始角度 (正面基準)
        public float endAngleOffset = 65f;      // スイング終了角度 (正面基準)
        public float swordRadius = 1.875f;      // 斬撃の届く半径 (m) (1.25m * 1.5)
        public float swordThickness = 0.675f;   // 三日月の最大太さ (m) (0.45m * 1.5)

        private int ownerRobotId = 0;
        private float damage = 80f;
        private Color slashColor = new Color(0.2f, 1f, 0.4f);
        private Transform followTransform;
        private Vector2 baseForwardDir;
        private float startTime;
        private bool isSwingRight = true;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private PolygonCollider2D polyCollider;
        private static Mesh cachedCrescentMesh;
        private List<Health> hitTargets = new List<Health>();

        public void Initialize(int ownerId, Transform parent, Vector2 forwardDir, bool isRightArm, float dmgVal, Color col)
        {
            ownerRobotId = ownerId;
            followTransform = parent;
            baseForwardDir = forwardDir.normalized;
            isSwingRight = isRightArm;
            damage = dmgVal;
            slashColor = col;
            startTime = Time.time;

            // 右腕なら右から左へ、左腕なら左から右へスイング
            if (isSwingRight)
            {
                startAngleOffset = 65f;
                endAngleOffset = -65f;
            }
            else
            {
                startAngleOffset = -65f;
                endAngleOffset = 65f;
            }

            transform.position = followTransform != null ? followTransform.position : transform.position;

            // Rigidbody2D を追加 (Trigger発火用)
            var rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;

            // メッシュ & レンダラー設定
            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = GetOrCreateCrescentMesh(swordRadius, swordThickness, 100f, 24);

            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.material = EffectFactory.GetParticleAdditiveMaterial();
            meshRenderer.material.color = slashColor;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            // コライダー (近接判定用)
            polyCollider = gameObject.AddComponent<PolygonCollider2D>();
            polyCollider.isTrigger = true;
            UpdateColliderShape(swordRadius, swordThickness);

            // スイング開始時のマズルフラッシュ・スパーク
            EffectFactory.PlayMuzzleFlash(transform.position + (Vector3)baseForwardDir * 0.5f, baseForwardDir, slashColor, 0.5f);
        }

        void Update()
        {
            float elapsed = Time.time - startTime;
            float progress = Mathf.Clamp01(elapsed / swingDuration);

            // ロボットの位置に追従
            if (followTransform != null)
            {
                transform.position = followTransform.position;
            }

            if (progress >= 1.0f)
            {
                Destroy(gameObject);
                return;
            }

            // スイングの回転アニメーション (EaseOutCubic)
            float tEase = 1f - Mathf.Pow(1f - progress, 3f);
            float currentAngleOffset = Mathf.Lerp(startAngleOffset, endAngleOffset, tEase);

            // 正面方向を基準とした回転
            float baseAngleDeg = Mathf.Atan2(baseForwardDir.y, baseForwardDir.x) * Mathf.Rad2Deg - 90f;
            float targetZ = baseAngleDeg + currentAngleOffset;
            transform.rotation = Quaternion.Euler(0, 0, targetZ);

            // スイング後半でフェードアウト
            float alpha = progress < 0.6f ? 1.0f : (1.0f - (progress - 0.6f) / 0.4f);
            if (meshRenderer != null)
            {
                Color c = slashColor;
                c.a *= alpha;
                meshRenderer.material.color = c;
            }

            // 近接 Overlap 判定 (確実な当たり判定)
            CheckOverlapHits();
        }

        private void CheckOverlapHits()
        {
            Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, swordRadius);
            for (int i = 0; i < cols.Length; i++)
            {
                var col = cols[i];
                if (col == null || col.gameObject == gameObject) continue;

                // 自身の機体は無視
                var robot = col.GetComponentInParent<Robot.RobotEntity>();
                if (robot != null && robot.RobotId == ownerRobotId) continue;

                // ターゲットが正面扇形範囲（±75度以内）にあるか検証
                Vector2 toTarget = (col.transform.position - transform.position).normalized;
                float angle = Vector2.Angle(baseForwardDir, toTarget);
                if (angle > 75f) continue;

                var health = col.GetComponentInParent<Health>();
                if (health != null && !hitTargets.Contains(health) && !health.IsDead)
                {
                    hitTargets.Add(health);
                    Vector2 hitPoint = col.ClosestPoint(transform.position);
                    Vector2 hitDir = (hitPoint - (Vector2)transform.position).normalized;

                    // ダメージ付与
                    health.TakeDamage(damage, hitPoint);

                    // 切断スパーク・近接ヒットエフェクト
                    EffectFactory.PlaySlashImpact(hitPoint, hitDir, slashColor, 1.4f);
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            CheckHit(other);
        }

        private void CheckHit(Collider2D other)
        {
            // 自身の機体は無視
            var robot = other.GetComponentInParent<Robot.RobotEntity>();
            if (robot != null && robot.RobotId == ownerRobotId) return;

            var health = other.GetComponentInParent<Health>();
            if (health != null && !hitTargets.Contains(health) && !health.IsDead)
            {
                hitTargets.Add(health);
                Vector2 hitPoint = other.ClosestPoint(transform.position);
                Vector2 hitDir = (hitPoint - (Vector2)transform.position).normalized;

                // ダメージ付与
                health.TakeDamage(damage, hitPoint);

                // 切断スパーク・近接ヒットエフェクト
                EffectFactory.PlaySlashImpact(hitPoint, hitDir, slashColor, 1.4f);
            }
        }

        private void UpdateColliderShape(float radius, float thickness)
        {
            int pts = 12;
            Vector2[] points = new Vector2[pts * 2];
            float halfArc = 50f * Mathf.Deg2Rad;

            for (int i = 0; i < pts; i++)
            {
                float t = (i / (float)(pts - 1)) * 2f - 1f; // -1 ~ 1
                float angle = t * halfArc + Mathf.PI * 0.5f; // 正面上向き
                float rOuter = radius;
                points[i] = new Vector2(Mathf.Cos(angle) * rOuter, Mathf.Sin(angle) * rOuter);

                float rInner = radius - thickness * (1f - t * t);
                points[pts * 2 - 1 - i] = new Vector2(Mathf.Cos(angle) * rInner, Mathf.Sin(angle) * rInner);
            }

            polyCollider.points = points;
        }

        /// <summary>
        /// 中央が太く両端が鋭く尖った美しい三日月型メッシュ（Crescent Arc）を生成
        /// </summary>
        public static Mesh GetOrCreateCrescentMesh(float radius = 1.875f, float thickness = 0.675f, float arcDeg = 100f, int segments = 24)
        {
            if (cachedCrescentMesh != null) return cachedCrescentMesh;

            Mesh mesh = new Mesh();
            mesh.name = "Melee_CrescentArcMesh";

            int vertexCount = (segments + 1) * 2;
            Vector3[] verts = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] tris = new int[segments * 6];

            float halfArc = (arcDeg * 0.5f) * Mathf.Deg2Rad;

            for (int i = 0; i <= segments; i++)
            {
                float t = (i / (float)segments) * 2f - 1f; // -1 ~ 1
                float angle = t * halfArc + Mathf.PI * 0.5f; // 上向き
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // 外周
                float rOuter = radius;
                verts[i * 2 + 1] = new Vector3(cos * rOuter, sin * rOuter, 0f);
                uvs[i * 2 + 1] = new Vector2(1f, (t + 1f) * 0.5f);

                // 内周
                float rInner = radius - thickness * (1f - t * t);
                verts[i * 2] = new Vector3(cos * rInner, sin * rInner, 0f);
                uvs[i * 2] = new Vector2(0f, (t + 1f) * 0.5f);
            }

            for (int i = 0; i < segments; i++)
            {
                int i0 = i * 2;
                int i1 = i * 2 + 1;
                int i2 = (i + 1) * 2;
                int i3 = (i + 1) * 2 + 1;

                // Triangle 1
                tris[i * 6] = i0;
                tris[i * 6 + 1] = i3;
                tris[i * 6 + 2] = i1;

                // Triangle 2
                tris[i * 6 + 3] = i0;
                tris[i * 6 + 4] = i2;
                tris[i * 6 + 5] = i3;
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            cachedCrescentMesh = mesh;
            return mesh;
        }
    }
}
