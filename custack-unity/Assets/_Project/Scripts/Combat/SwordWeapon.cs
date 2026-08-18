using UnityEngine;
using Custack.Equipment;

namespace Custack.Combat
{
    /// <summary>
    /// デバイスID 0x02: ソード武器 (近接高威力・ロボット周辺三日月スイング斬撃)
    /// </summary>
    public class SwordWeapon : WeaponBase
    {
        protected override void ExecuteFire(Vector2 forwardDir, Transform targetTransform)
        {
            Color colVal = config != null ? config.weaponColor : new Color(0.2f, 1f, 0.4f);
            float dmg = config != null ? config.damage : 40f; // ユーザー指示: 基礎威力40

            // ロボットの原点を中心とした三日月型近接スイングを生成
            GameObject slashObj = new GameObject("MeleeSwordSlash");
            var meleeSlash = slashObj.AddComponent<MeleeSwordSlash>();
            meleeSlash.Initialize(OwnerRobotId, transform, forwardDir, IsRightArm, dmg, colVal);
        }
    }
}
