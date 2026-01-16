using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Game/WeaponData")]
public class WeaponData : ScriptableObject
{
[Header("Basic Stats")]
    public string weaponName;
    public int attackPower;
    public GameObject weaponPrefab;   // 手に持つモデル
    public GameObject hitEffectPrefab;

    [Header("Ranged (GameObject)")]
    public bool isRanged;             // Trueならミサイル/銃
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f;
    public float lifeTime = 5f;

    [Header("Homing Settings")]
    public float homingSensitivity = 0f; // 0なら直進、高いほど強く追尾

    [Header("Particle System")]
    public bool useParticleSystem;    // Trueならパーティクル制御
    // パーティクル使用時は weaponPrefab に ParticleProjectile がついている想定
}