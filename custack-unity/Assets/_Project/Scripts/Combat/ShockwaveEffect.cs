using UnityEngine;

namespace Custack.Combat
{
    /// <summary>
    /// キャノン着弾や斬撃時に広がるプロジェクション衝撃波リング。
    /// 円環を急速に拡大させ、アルファ値をフェードアウトさせて自動消滅します。
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ShockwaveEffect : MonoBehaviour
    {
        private LineRenderer lineRenderer;
        private Color baseColor;
        private float startRadius;
        private float maxRadius;
        private float duration;
        private float startTime;
        private int segments = 36;

        public void Initialize(Color color, float startR, float maxR, float dur)
        {
            baseColor = color;
            startRadius = startR;
            maxRadius = maxR;
            duration = dur;
            startTime = Time.time;

            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.positionCount = segments;
            lineRenderer.material = EffectFactory.GetParticleAdditiveMaterial();
            lineRenderer.startWidth = 0.08f;
            lineRenderer.endWidth = 0.08f;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;

            UpdateCircleMesh(startRadius, 1f);
        }

        void Update()
        {
            float elapsed = Time.time - startTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            if (progress >= 1.0f)
            {
                Destroy(gameObject);
                return;
            }

            // 加速する拡大イージング (EaseOutQuad)
            float currentRadius = Mathf.Lerp(startRadius, maxRadius, Mathf.Sin(progress * Mathf.PI * 0.5f));
            float alpha = 1.0f - progress;
            float width = Mathf.Lerp(0.12f, 0.02f, progress);

            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            UpdateCircleMesh(currentRadius, alpha);
        }

        private void UpdateCircleMesh(float radius, float alpha)
        {
            Color col = baseColor;
            col.a *= alpha;
            lineRenderer.startColor = col;
            lineRenderer.endColor = col;

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                lineRenderer.SetPosition(i, new Vector3(x, y, 0));
            }
        }
    }
}
