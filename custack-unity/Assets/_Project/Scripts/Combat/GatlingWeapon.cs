using UnityEngine;
using Custack.Equipment;

namespace Custack.Combat
{
    /// <summary>
    /// デバイスID 0x02: ガトリング武器 (連射・短射程・弾幕)
    /// </summary>
    public class GatlingWeapon : WeaponBase
    {
        protected override void ExecuteFire(Vector2 forwardDir, Transform targetTransform)
        {
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

            // 拡散角の計算
            float spread = config != null ? config.spreadAngle : 7.5f;
            float randomSpread = Random.Range(-spread, spread);
            Vector2 spreadDir = Quaternion.Euler(0, 0, randomSpread) * forwardDir;

            GameObject bulletObj;
            if (config != null && config.projectilePrefab != null)
            {
                bulletObj = Instantiate(config.projectilePrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                bulletObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bulletObj.name = "GatlingBullet";
                bulletObj.transform.position = spawnPos;
                bulletObj.transform.localScale = new Vector3(0.18f, 0.08f, 1f);
                var col = bulletObj.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
            }

            var proj = bulletObj.GetComponent<Projectile>();
            if (proj == null) proj = bulletObj.AddComponent<Projectile>();

            float speed = config != null ? config.projectileSpeed : 16f;
            float dmg = config != null ? config.damage : 8f;
            float life = config != null ? config.projectileLifeTime : 0.7f;
            Color colVal = config != null ? config.weaponColor : new Color(1f, 0.85f, 0.2f);

            proj.Initialize(OwnerRobotId, spreadDir, speed, dmg, life, colVal);
        }
    }
}
