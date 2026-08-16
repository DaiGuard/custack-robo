using UnityEngine;
using Custack.Equipment;

namespace Custack.Combat
{
    /// <summary>
    /// デバイスID 0x03: 誘導ミサイル武器 (緩やかなホーミング・範囲爆発)
    /// </summary>
    public class MissileWeapon : WeaponBase
    {
        protected override void ExecuteFire(Vector2 forwardDir, Transform targetTransform)
        {
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

            GameObject missileObj;
            if (config != null && config.projectilePrefab != null)
            {
                missileObj = Instantiate(config.projectilePrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                missileObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                missileObj.name = "HomingMissile";
                missileObj.transform.position = spawnPos;
                missileObj.transform.localScale = new Vector3(0.5f, 0.2f, 1f);
                var col = missileObj.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
            }

            var missile = missileObj.GetComponent<HomingMissile>();
            if (missile == null) missile = missileObj.AddComponent<HomingMissile>();

            float speed = config != null ? config.projectileSpeed : 9f;
            float dmg = config != null ? config.damage : 60f;
            float life = config != null ? config.projectileLifeTime : 4.0f;
            float turnSpeed = config != null ? config.homingTurnSpeed : 120f;
            float explRadius = config != null ? config.explosionRadius : 1.8f;
            Color colVal = config != null ? config.weaponColor : new Color(1f, 0.3f, 0.2f);

            missile.Initialize(OwnerRobotId, forwardDir, speed, dmg, life, colVal);
            missile.SetTarget(targetTransform, turnSpeed, explRadius);
        }
    }
}
