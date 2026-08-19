using UnityEngine;

namespace Custack.Combat
{
    /// <summary>
    /// URP プロジェクション演出向けのパーティクル・マズルフラッシュ・着弾スパーク・爆発エフェクト生成ファクトリー。
    /// 外部プレハブ不要で、コードから完全自立して発光エフェクトを生成・自動破棄します。
    /// </summary>
    public static class EffectFactory
    {
        private static Material particleAdditiveMaterial;
        private static Texture2D softCircleTexture;

        /// <summary>
        /// 発光用 Additive マテリアルを取得または生成
        /// </summary>
        public static Material GetParticleAdditiveMaterial()
        {
            if (particleAdditiveMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                          ?? Shader.Find("Particles/Standard Unlit")
                          ?? Shader.Find("Universal Render Pipeline/Unlit")
                          ?? Shader.Find("Sprites/Default");

                particleAdditiveMaterial = new Material(shader)
                {
                    name = "Mat_Effect_Additive"
                };

                // ブレンドモードを Additive (加算合成) に設定
                particleAdditiveMaterial.SetFloat("_Surface", 1); // Transparent
                particleAdditiveMaterial.SetFloat("_Blend", 1);   // Additive
                particleAdditiveMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                particleAdditiveMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                particleAdditiveMaterial.SetInt("_ZWrite", 0);
                particleAdditiveMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100;
                
                if (softCircleTexture == null) softCircleTexture = CreateSoftCircleTexture(64);
                particleAdditiveMaterial.mainTexture = softCircleTexture;
            }
            return particleAdditiveMaterial;
        }

        /// <summary>
        /// 滑らかな円形グラデーションテクスチャを動的生成
        /// </summary>
        private static Texture2D CreateSoftCircleTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Tex_SoftCircle"
            };

            float center = (size - 1) * 0.5f;
            float maxDist = center;

            Color[] cols = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float normDist = Mathf.Clamp01(dist / maxDist);
                    // 中心が白く、外側へ向けて滑らかにアルファ減衰
                    float alpha = Mathf.Pow(1f - normDist, 2f);
                    cols[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 銃口のマズルフラッシュエフェクト
        /// </summary>
        public static void PlayMuzzleFlash(Vector3 position, Vector2 forwardDir, Color color, float scale = 0.4f)
        {
            GameObject obj = new GameObject("MuzzleFlash_FX");
            obj.transform.position = position;

            float angle = Mathf.Atan2(forwardDir.y, forwardDir.x) * Mathf.Rad2Deg;
            obj.transform.rotation = Quaternion.Euler(0, 0, angle);

            var ps = obj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var psr = obj.GetComponent<ParticleSystemRenderer>();
            psr.material = GetParticleAdditiveMaterial();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f * scale, 8f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f * scale, 0.25f * scale);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, (short)(8 * scale + 4))
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.05f * scale;

            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(color, 0.4f), new GradientColorKey(color * 0.5f, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            colOverLife.color = grad;

            ps.Play();
            Object.Destroy(obj, 0.35f);
        }

        /// <summary>
        /// 着弾・被弾火花スパークエフェクト
        /// </summary>
        public static void PlayHitSparks(Vector3 position, Vector2 hitNormal, Color color, float scale = 1.0f, int count = 12)
        {
            GameObject obj = new GameObject("HitSparks_FX");
            obj.transform.position = position;

            float angle = Mathf.Atan2(hitNormal.y, hitNormal.x) * Mathf.Rad2Deg;
            obj.transform.rotation = Quaternion.Euler(0, 0, angle);

            var ps = obj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var psr = obj.GetComponent<ParticleSystemRenderer>();
            psr.material = GetParticleAdditiveMaterial();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f * scale, 12f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f * scale, 0.18f * scale);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, (short)count)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 60f;
            shape.radius = 0.08f * scale;

            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.3f), new GradientColorKey(color * 0.3f, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0f), new GradientAlphaKey(0.9f, 0.4f), new GradientAlphaKey(0.0f, 1f) }
            );
            colOverLife.color = grad;

            // スピード減速
            var limitVel = ps.limitVelocityOverLifetime;
            limitVel.enabled = true;
            limitVel.dampen = 0.4f;

            ps.Play();
            Object.Destroy(obj, 0.5f);
        }

        /// <summary>
        /// ソードの斬撃ヒット光・クロススパーク
        /// </summary>
        public static void PlaySlashImpact(Vector3 position, Vector2 dir, Color color, float scale = 1.2f)
        {
            PlayHitSparks(position, dir, color, scale * 1.2f, 16);
            CreateShockwave(position, color, 0.1f, 0.8f * scale, 0.18f);
        }

        /// <summary>
        /// 大爆発エフェクト（キャノン着弾・撃破時）
        /// </summary>
        public static void PlayExplosion(Vector3 position, Color color, float radius = 1.2f, int sparkCount = 28)
        {
            GameObject obj = new GameObject("Explosion_FX");
            obj.transform.position = position;

            var ps = obj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var psr = obj.GetComponent<ParticleSystemRenderer>();
            psr.material = GetParticleAdditiveMaterial();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f * radius, 14f * radius);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f * radius, 0.35f * radius);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, (short)sparkCount)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f * radius;

            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.25f), new GradientColorKey(new Color(1f, 0.3f, 0.1f), 0.7f), new GradientColorKey(Color.black, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0f), new GradientAlphaKey(0.9f, 0.4f), new GradientAlphaKey(0.0f, 1f) }
            );
            colOverLife.color = grad;

            var limitVel = ps.limitVelocityOverLifetime;
            limitVel.enabled = true;
            limitVel.dampen = 0.35f;

            ps.Play();

            // 衝撃波リングを併せて展開 & SE再生
            CreateShockwave(position, color, 0.15f, radius * 1.5f, 0.3f);
            Custack.Audio.AudioManager.Instance?.PlaySE(Custack.Audio.SoundEffectType.Explosion, Mathf.Clamp(radius, 0.7f, 1.2f), 0.05f);

            Object.Destroy(obj, 0.8f);
        }

        /// <summary>
        /// キャノン着弾時の電撃・衝撃波エフェクト
        /// </summary>
        public static void PlayLaserImpact(Vector3 position, Color color)
        {
            PlayExplosion(position, color, 1.4f, 24);
        }

        /// <summary>
        /// 衝撃波リング (Shockwave Ring) のアニメーション生成
        /// </summary>
        public static void CreateShockwave(Vector3 position, Color color, float startRadius, float maxRadius, float duration)
        {
            GameObject ringObj = new GameObject("ShockwaveRing_FX");
            ringObj.transform.position = position;

            var waveComp = ringObj.AddComponent<ShockwaveEffect>();
            waveComp.Initialize(color, startRadius, maxRadius, duration);
        }

        /// <summary>
        /// 機体撃破時の連続大爆発エフェクト
        /// </summary>
        public static void PlayRobotDestructionExplosion(Vector3 position, Color color)
        {
            // 中心大爆発
            PlayExplosion(position, color, 1.8f, 36);

            // 周囲の追従小爆発
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * 0.45f, Mathf.Sin(angle) * 0.45f, 0);
                PlayExplosion(position + offset, new Color(1f, 0.5f, 0.1f), 1.0f, 16);
            }
        }

        /// <summary>
        /// 撃破された機体への黒煙 & 放電スパークエフェクトのアタッチ
        /// </summary>
        public static GameObject AttachWreckageSmokeAndSparks(Transform target)
        {
            if (target == null) return null;

            GameObject root = new GameObject("WreckageSmoke_FX");
            root.transform.SetParent(target);
            root.transform.localPosition = Vector3.zero;

            // 黒煙パーティクル
            var ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var psr = root.GetComponent<ParticleSystemRenderer>();
            psr.material = GetParticleAdditiveMaterial();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startColor = new Color(0.15f, 0.15f, 0.15f, 0.6f); // 暗い煙
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 12;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;

            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.3f, 0.3f, 0.3f), 0f), new GradientColorKey(Color.black, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.0f, 1f) }
            );
            colOverLife.color = grad;

            ps.Play();
            return root;
        }
    }
}
