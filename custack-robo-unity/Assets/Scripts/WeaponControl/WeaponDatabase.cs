using UnityEngine;
using System.Collections.Generic;


[CreateAssetMenu(fileName = "WeaponDatabase", menuName = "Scriptable Objects/WeaponDatabase")]
public class WeaponDatabase : ScriptableObject
{
    public List<GameObject> weapons = new List<GameObject>();

    public GameObject GetWeapon(int index)
    {
        if (index >= 0 && index < weapons.Count)
        {
            return weapons[index];
        }
        
        return null;
    }
    
}
