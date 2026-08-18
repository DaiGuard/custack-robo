using UnityEngine;

namespace Custack.Equipment
{
    /// <summary>
    /// アーム武器の性能設定パラメータ
    /// </summary>
    [System.Serializable]
    public class ArmWeaponConfig
    {
        public ArmDeviceType type = ArmDeviceType.None;
        public string weaponName = "None";

        [Header("射撃・攻撃性能")]
        public float damage = 10f;             // 単発ダメージ
        public float projectileSpeed = 15f;    // 弾速
        public float projectileLifeTime = 2f;  // 弾丸寿命 (射程)
        public float cooldown = 0.4f;          // 発射クールダウン (秒)
        public bool isAutomatic = false;       // フルオート連射対応か
        public float spreadAngle = 0f;         // 拡散角 (度)

        [Header("特殊性能")]
        public bool isHoming = false;
        public float homingTurnSpeed = 120f;   // 旋回角速度 (度/秒)
        public float explosionRadius = 0f;     // 着弾時爆発半径

        [Header("Prefab & 演出")]
        public GameObject projectilePrefab;
        public GameObject muzzleFlashPrefab;
        public Color weaponColor = Color.white;

        public static ArmWeaponConfig CreateDefault(ArmDeviceType type)
        {
            switch (type)
            {
                case ArmDeviceType.Gatling:
                    return new ArmWeaponConfig
                    {
                        type = ArmDeviceType.Gatling,
                        weaponName = "Gatling Gun",
                        damage = 8f,
                        projectileSpeed = 16f,       // 中速
                        projectileLifeTime = 0.7f,   // 短射程 (近〜中距離)
                        cooldown = 0.067f,           // 15発/秒 超高速連射
                        isAutomatic = true,
                        spreadAngle = 7.5f,          // 拡散あり
                        weaponColor = new Color(1f, 0.8f, 0.2f) // 黄色弾幕
                    };

                case ArmDeviceType.Sword:
                    return new ArmWeaponConfig
                    {
                        type = ArmDeviceType.Sword,
                        weaponName = "Beam Sword",
                        damage = 40f,                // 近接高威力 (基本威力 40)
                        projectileSpeed = 18f,       // 高速スイング
                        projectileLifeTime = 0.35f,  // 短射程・近接スラッシュ
                        cooldown = 0.45f,            // セミオート
                        isAutomatic = false,
                        spreadAngle = 0f,
                        weaponColor = new Color(0.2f, 1f, 0.4f) // エメラルドグリーン斬撃
                    };

                case ArmDeviceType.Cannon:
                    return new ArmWeaponConfig
                    {
                        type = ArmDeviceType.Cannon,
                        weaponName = "Heavy Laser Cannon",
                        damage = 30f,                // レーザーキャノン (基本威力 30)
                        projectileSpeed = 7.5f,      // 弾速 (重厚な巨大エネルギー弾速)
                        projectileLifeTime = 3.5f,   // 画面端まで届く長射程
                        cooldown = 1.8f,             // ロングCD
                        isAutomatic = false,
                        spreadAngle = 0f,
                        weaponColor = new Color(0.3f, 0.7f, 1f) // 高出力ブルーレーザー
                    };

                default:
                    return new ArmWeaponConfig();
            }
        }
    }
}
