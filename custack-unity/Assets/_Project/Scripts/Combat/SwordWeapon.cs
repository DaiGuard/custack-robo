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
            float dmg = config != null ? config.damage : 80f; // ユーザー指示: 基礎威力80 (40 * 2)

            // ロボットの原点を中心とした三日月型近接スイングを生成 & SE再生
            GameObject slashObj = new GameObject("MeleeSwordSlash");
            var meleeSlash = slashObj.AddComponent<MeleeSwordSlash>();
            meleeSlash.Initialize(OwnerRobotId, transform, forwardDir, IsRightArm, dmg, colVal);
            Custack.Audio.AudioManager.Instance?.PlaySE(Custack.Audio.SoundEffectType.SwordSlash, 0.9f, 0.05f);
        }
    }
}
