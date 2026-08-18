using UnityEngine;
using Custack.Equipment;
using Custack.Robot;

namespace Custack.Combat
{
    /// <summary>
    /// デバイスID 0x01: ガトリング武器 (連射・短射程・黄色弾幕)
    /// </summary>
    public class GatlingWeapon : WeaponBase
    {
        protected override void ExecuteFire(Vector2 forwardDir, Transform targetTransform)
        {
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            Color colVal = config != null ? config.weaponColor : new Color(1f, 0.85f, 0.2f);

            // 1. 発射マズルフラッシュエフェクト
            EffectFactory.PlayMuzzleFlash(spawnPos, forwardDir, colVal, 0.35f);

            // 2. 拡散角の計算
            float spread = config != null ? config.spreadAngle : 7.5f;
            float randomSpread = Random.Range(-spread, spread);
            Vector2 spreadDir = Quaternion.Euler(0, 0, randomSpread) * forwardDir;

            // 3. 弾丸オブジェクトの生成 (2Dに完全特化したクリーンな構成)
            GameObject bulletObj;
            if (config != null && config.projectilePrefab != null)
            {
                bulletObj = Instantiate(config.projectilePrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                bulletObj = new GameObject("GatlingBullet");
                bulletObj.transform.position = spawnPos;
                bulletObj.transform.localScale = new Vector3(0.22f, 0.08f, 1f);

                var mf = bulletObj.AddComponent<MeshFilter>();
                mf.sharedMesh = RobotMeshHelper.GetOrCreateQuadMesh();

                var mr = bulletObj.AddComponent<MeshRenderer>();
                mr.material = EffectFactory.GetParticleAdditiveMaterial();

                var col = bulletObj.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(1f, 1f);
            }

            var proj = bulletObj.GetComponent<Projectile>();
            if (proj == null) proj = bulletObj.AddComponent<Projectile>();

            float speed = config != null ? config.projectileSpeed : 16f;
            float dmg = config != null ? config.damage : 8f;
            float life = config != null ? config.projectileLifeTime : 0.7f;

            proj.trailTime = 0.12f;
            proj.trailStartWidth = 0.10f;
            proj.trailEndWidth = 0.01f;

            proj.Initialize(OwnerRobotId, spreadDir, speed, dmg, life, colVal);
        }
    }
}
