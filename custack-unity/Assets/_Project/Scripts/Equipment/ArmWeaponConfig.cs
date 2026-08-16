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

        [Header("射撃性能")]
        public float damage = 10f;             // 単発ダメージ
        public float projectileSpeed = 15f;    // 弾速
        public float projectileLifeTime = 2f;  // 弾丸寿命 (射程)
        public float cooldown = 0.4f;          // 発射クールダウン (秒)
        public bool isAutomatic = false;       // フルオート連射対応か
        public float spreadAngle = 0f;         // 拡散角 (度)

        [Header("ホーミング性能 (ミサイル用)")]
        public bool isHoming = false;
        public float homingTurnSpeed = 120f;   // 旋回角速度 (度/秒)
        public float explosionRadius = 1.5f;   // 着弾時爆発半径

        [Header("Prefab & 演出")]
        public GameObject projectilePrefab;
        public GameObject muzzleFlashPrefab;
        public Color weaponColor = Color.white;

        public static ArmWeaponConfig CreateDefault(ArmDeviceType type)
        {
            switch (type)
            {
                case ArmDeviceType.Pistol:
                    return new ArmWeaponConfig
                    {
                        type = ArmDeviceType.Pistol,
                        weaponName = "Pistol",
                        damage = 25f,
                        projectileSpeed = 25f,       // 高速
                        projectileLifeTime = 3.0f,   // 長射程 (画面端まで届く)
                        cooldown = 0.35f,            // 単発セミオート
                        isAutomatic = false,
                        spreadAngle = 0f,
                        weaponColor = new Color(0.2f, 0.8f, 1f) // 水色レーザー
                    };

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

                case ArmDeviceType.Missile:
                    return new ArmWeaponConfig
                    {
                        type = ArmDeviceType.Missile,
                        weaponName = "Homing Missile",
                        damage = 60f,                // 大ダメージ
                        projectileSpeed = 9f,        // 低速〜中速
                        projectileLifeTime = 4.0f,
                        cooldown = 2.5f,             // ロングCD
                        isAutomatic = false,
                        isHoming = true,
                        homingTurnSpeed = 120f,      // 緩やかなホーミング
                        explosionRadius = 1.8f,
                        weaponColor = new Color(1f, 0.3f, 0.2f) // 赤ミサイル
                    };

                default:
                    return new ArmWeaponConfig();
            }
        }
    }
}
