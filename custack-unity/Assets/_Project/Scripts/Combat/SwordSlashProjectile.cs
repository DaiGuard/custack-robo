using UnityEngine;

namespace Custack.Combat
{
    /// <summary>
    /// ソードの三日月型・扇形斬撃波コンポーネント。
    /// 前方へ高速スイング・移動しながら拡大・発光し、敵にヒットすると鋭い切断スパークを発生させます。
    /// </summary>
    public class SwordSlashProjectile : Projectile
    {
        private static Mesh cachedArcMesh;

        public override void Initialize(int ownerId, Vector2 direction, float speedVal, float dmgVal, float lifeVal, Color col)
        {
            enableTrail = true;
            trailTime = 0.20f;
            trailStartWidth = 0.35f;
            trailEndWidth = 0.02f;

            base.Initialize(ownerId, direction, speedVal, dmgVal, lifeVal, col);

            // 三日月型メッシュを割り当て
            var mf = GetComponent<MeshFilter>();
            if (mf != null)
            {
                mf.sharedMesh = GetOrCreateArcMesh();
            }
        }

        protected override void Update()
        {
            base.Update();

            // 斬撃波が前進するにつれてわずかに拡大 (スイングの広がり演出)
            float elapsed = Time.time - spawnTime;
            float progress = Mathf.Clamp01(elapsed / lifeTime);
            float scaleMultiplier = Mathf.Lerp(1.0f, 1.4f, progress);
            transform.localScale = new Vector3(0.9f * scaleMultiplier, 0.4f * scaleMultiplier, 1f);
        }

        protected override void OnHitTarget(GameObject target)
        {
            EffectFactory.PlaySlashImpact(transform.position, moveDirection, bulletColor, 1.4f);
            Destroy(gameObject);
        }

        protected override void OnHitObstacle()
        {
            EffectFactory.PlaySlashImpact(transform.position, moveDirection, Color.gray, 0.8f);
            Destroy(gameObject);
        }

        /// <summary>
        /// 三日月・円弧状の斬撃メッシュを生成
        /// </summary>
        public static Mesh GetOrCreateArcMesh(float radius = 0.5f, float arcAngleDeg = 90f, int segments = 16)
        {
            if (cachedArcMesh != null) return cachedArcMesh;

            Mesh mesh = new Mesh();
            mesh.name = "SwordArcMesh";

            int vertexCount = (segments + 1) * 2;
            Vector3[] verts = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] tris = new int[segments * 6];

            float halfArc = (arcAngleDeg * 0.5f) * Mathf.Deg2Rad;
            float innerRadius = radius * 0.4f;
            float outerRadius = radius;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = Mathf.Lerp(-halfArc, halfArc, t);
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // 内側頂点
                verts[i * 2] = new Vector3(cos * innerRadius, sin * innerRadius, 0f);
                uvs[i * 2] = new Vector2(0f, t);

                // 外側頂点
                verts[i * 2 + 1] = new Vector3(cos * outerRadius, sin * outerRadius, 0f);
                uvs[i * 2 + 1] = new Vector2(1f, t);
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

            cachedArcMesh = mesh;
            return mesh;
        }
    }
}
