using UnityEngine;
using Custack.Equipment;

namespace Custack.Combat
{
    /// <summary>
    /// デバイスID 0x02: ソード武器 (近接高威力・斬撃波 / 薙ぎ払い)
    /// </summary>
    public class SwordWeapon : WeaponBase
    {
        protected override void ExecuteFire(Vector2 forwardDir, Transform targetTransform)
        {
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

            GameObject slashObj;
            if (config != null && config.projectilePrefab != null)
            {
                slashObj = Instantiate(config.projectilePrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // フォールバック用 GameObject 生成 (弧状・三日月状の近接スラッシュ)
                slashObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                slashObj.name = "BeamSlash";
                slashObj.transform.position = spawnPos;
                slashObj.transform.localScale = new Vector3(0.8f, 0.25f, 1f);
                var col = slashObj.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
            }

            var proj = slashObj.GetComponent<Projectile>();
            if (proj == null) proj = slashObj.AddComponent<Projectile>();

            float speed = config != null ? config.projectileSpeed : 18f;
            float dmg = config != null ? config.damage : 45f;
            float life = config != null ? config.projectileLifeTime : 0.35f;
            Color colVal = config != null ? config.weaponColor : new Color(0.2f, 1f, 0.4f);

            proj.Initialize(OwnerRobotId, forwardDir, speed, dmg, life, colVal);
        }
    }
}
