using UnityEngine;
using Custack.Equipment;
using Custack.Robot;

namespace Custack.Combat
{
    /// <summary>
    /// デバイスID 0x03: 大型レーザーキャノン (高出力ブルーレーザー・大爆発)
    /// </summary>
    public class LaserCannonWeapon : WeaponBase
    {
        protected override void ExecuteFire(Vector2 forwardDir, Transform targetTransform)
        {
            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            Color colVal = config != null ? config.weaponColor : new Color(0.3f, 0.7f, 1f);

            // 1. 強烈なビーム発射マズルフラッシュ
            EffectFactory.PlayMuzzleFlash(spawnPos, forwardDir, colVal, 0.8f);

            // 2. レーザービームオブジェクトの生成 (2Dに完全特化した構成)
            GameObject beamObj;
            if (config != null && config.projectilePrefab != null)
            {
                beamObj = Instantiate(config.projectilePrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                beamObj = new GameObject("HeavyLaserBeam");
                beamObj.transform.position = spawnPos;
                beamObj.transform.localScale = new Vector3(1.6f, 0.48f, 1f);

                var mf = beamObj.AddComponent<MeshFilter>();
                mf.sharedMesh = RobotMeshHelper.GetOrCreateQuadMesh();

                var mr = beamObj.AddComponent<MeshRenderer>();
                mr.material = EffectFactory.GetParticleAdditiveMaterial();

                var col = beamObj.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(1f, 1f);
            }

            var proj = beamObj.GetComponent<LaserBeamProjectile>();
            if (proj == null) proj = beamObj.AddComponent<LaserBeamProjectile>();

            float speed = config != null ? config.projectileSpeed : 7.5f;
            float dmg = config != null ? config.damage : 30f; // ユーザー指示: 基礎威力30
            float life = config != null ? config.projectileLifeTime : 3.5f;

            proj.Initialize(OwnerRobotId, forwardDir, speed, dmg, life, colVal);
        }
    }
}
