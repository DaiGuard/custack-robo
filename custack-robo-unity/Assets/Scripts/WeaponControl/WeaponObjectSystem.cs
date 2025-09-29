using UnityEngine;

public class WeaponObjectSystem : WeaponSystem
{
    [SerializeField]
    private GameObject _weaponObject = null; // The weapon object to be triggered

    public GameObject targetObject { get; set; }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void Start()
    {
        base.Start();

        _lastTime = Time.realtimeSinceStartup;
    }

    // Update is called once per frame
    protected override void Update()
    {
        base.Update();
    }

    public override void Play()
    {
        if(_weaponObject == null)
        {
            Debug.LogWarning("Weapon object is not assigned.");
            return;
        }

        if(Time.realtimeSinceStartup - _lastTime < _reloadTime)
        {
            return;
        }

        _lastTime = Time.realtimeSinceStartup;
        var obj = Instantiate(_weaponObject,
            transform.position, transform.rotation); // Trigger the weapon by instantiating i

        var trajectory = obj.GetComponent<WeaponTrajectory>();
        if (trajectory != null)
        {
            trajectory.SetTargetObject(_targetObject);
        }
    }
    
    public override bool isPlaying()
    {
        return true; // This can be modified to check if the weapon object is currently active or playing
    }
}
