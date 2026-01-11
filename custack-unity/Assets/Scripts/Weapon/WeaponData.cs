using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "Game/WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("Basic Stats")]
    public string weaponName;
    public int attackPower;
    public GameObject weaponPrefab;
    public GameObject hitEffectPrefab;

    [Header("Ranged (GameObject)")]
    public bool isRanged;
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f;
}
