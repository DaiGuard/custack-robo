using UnityEngine;
// using WeaponTrajectory; // Assuming this namespace contains the WeaponTrajectory class


public class WeaponTrigger : MonoBehaviour
{
    [SerializeField]
    private KeyCode _triggerKey = KeyCode.Space; // Key to trigger the weapon

    [SerializeField]
    private GameObject _weaponObject = null; // The weapon object to be triggered

    [SerializeField]
    private float _triggerCooldown = 0.5f; // Cooldown time for the trigger

    [SerializeField]
    private GameObject _targetObject = null; // The target object for the weapon

    private float _triggerIntervalCounter = 0.0f; // Counter to track the cooldown time

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _triggerIntervalCounter = _triggerCooldown; // Initialize the cooldown counter
    }

    // Update is called once per frame
    void Update()
    {
        if (_triggerIntervalCounter >= _triggerCooldown)
        {
            if (Input.GetKeyDown(_triggerKey))
            {
                if (_weaponObject != null)
                {
                    var obj = Instantiate(_weaponObject,
                        transform.position, Quaternion.identity); // Trigger the weapon by instantiating it

                    obj.GetComponent<WeaponTrajectory>().SetTargetObject(_targetObject); // Set the target object for the weapon trajectory
                }

                _triggerIntervalCounter = 0.0f; // Reset the cooldown counter
            }
        }
        else
        {
            _triggerIntervalCounter += Time.deltaTime; // Increment the cooldown counter
        }
    }
}
