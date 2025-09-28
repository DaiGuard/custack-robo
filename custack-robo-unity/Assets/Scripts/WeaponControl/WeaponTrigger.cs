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

    private GameObject _currentWeaponObject = null; // Current weapon object instance

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _triggerIntervalCounter = _triggerCooldown; // Initialize the cooldown counter

        _currentWeaponObject = Instantiate(_weaponObject,
            transform.position, transform.rotation);
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
                    var weaponSystem = _currentWeaponObject.GetComponent<WeaponSystem>();
                    if (weaponSystem != null)
                    {
                        weaponSystem.Play(); // Trigger the weapon action
                    }
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
