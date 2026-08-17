using UnityEngine;
using Custack.Equipment;

namespace Custack.Combat
{
    /// <summary>
    /// デバイスID 0x03: 大型レーザーキャノン (極大威力・長射程・ビーム)
    /// </summary>
    public class LaserCannonWeapon : WeaponBase
    {
        protected override void ExecuteFire(Vector2 forwardDir, Transform targetTransform)
        {
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

            GameObject beamObj;
            if (config != null && config.projectilePrefab != null)
            {
                beamObj = Instantiate(config.projectilePrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // フォールバック用 GameObject 生成 (太い大口径レーザー)
                beamObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                beamObj.name = "HeavyLaserBeam";
                beamObj.transform.position = spawnPos;
                beamObj.transform.localScale = new Vector3(1.2f, 0.2f, 1f);
                var col = beamObj.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
            }

            var proj = beamObj.GetComponent<Projectile>();
            if (proj == null) proj = beamObj.AddComponent<Projectile>();

            float speed = config != null ? config.projectileSpeed : 30f;
            float dmg = config != null ? config.damage : 75f;
            float life = config != null ? config.projectileLifeTime : 3.5f;
            Color colVal = config != null ? config.weaponColor : new Color(0.3f, 0.7f, 1f);

            proj.Initialize(OwnerRobotId, forwardDir, speed, dmg, life, colVal);
        }
    }
}
