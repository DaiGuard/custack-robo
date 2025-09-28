using UnityEngine;

public class WeaponTrajectory : MonoBehaviour
{
    [SerializeField]
    protected GameObject _targetObject = null; // The target object for the weapon trajectory

    public void SetTargetObject(GameObject target)
    {
        _targetObject = target; // Set the target object for the weapon trajectory
    }
    
    public GameObject GetTargetObject()
    {
        return _targetObject; // Get the target object for the weapon trajectory
    }
}
