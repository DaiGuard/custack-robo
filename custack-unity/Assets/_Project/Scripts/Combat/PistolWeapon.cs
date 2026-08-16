using UnityEngine;
using Custack.Equipment;

namespace Custack.Combat
{
    /// <summary>
    /// デバイスID 0x01: ピストル武器 (単発・高速・長射程)
    /// </summary>
    public class PistolWeapon : WeaponBase
    {
        protected override void ExecuteFire(Vector2 forwardDir, Transform targetTransform)
        {
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

            GameObject bulletObj;
            if (config != null && config.projectilePrefab != null)
            {
                bulletObj = Instantiate(config.projectilePrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // フォールバック用 GameObject 生成
                bulletObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bulletObj.name = "PistolLaser";
                bulletObj.transform.position = spawnPos;
                bulletObj.transform.localScale = new Vector3(0.4f, 0.08f, 1f);
                var col = bulletObj.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
            }

            var proj = bulletObj.GetComponent<Projectile>();
            if (proj == null) proj = bulletObj.AddComponent<Projectile>();

            float speed = config != null ? config.projectileSpeed : 25f;
            float dmg = config != null ? config.damage : 25f;
            float life = config != null ? config.projectileLifeTime : 3.0f;
            Color colVal = config != null ? config.weaponColor : new Color(0.2f, 0.8f, 1f);

            proj.Initialize(OwnerRobotId, forwardDir, speed, dmg, life, colVal);
        }
    }
}
