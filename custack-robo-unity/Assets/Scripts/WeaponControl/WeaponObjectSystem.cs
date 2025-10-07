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
    }

    // Update is called once per frame
    protected override void Update()
    {
        base.Update();
    }

    public override void Play(Transform parentTransform=null, GameObject targetObject=null)
    {
        if(_weaponObject == null)
        {
            Debug.LogWarning("Weapon object is not assigned.");
            return;
        }

        if(Time.realtimeSinceStartup < _lastTime)
        {
            _lastTime = Time.realtimeSinceStartup;
        }
        else if (Time.realtimeSinceStartup - _lastTime < _reloadTime)
        {
            return;
        }


        _lastTime = Time.realtimeSinceStartup;
        var obj = Instantiate(_weaponObject,
            parentTransform.position, parentTransform.rotation);
        // var obj = Instantiate(_weaponObject,
        //     parentTransform.localPosition, transform.localRotation, parentTransform);
        // var obj = Instantiate(_weaponObject,
        //                     Vector3.zero, Quaternion.identity,
        //                     parentTransform);

        var trajectory = obj.GetComponent<WeaponTrajectory>();
        if (trajectory != null && targetObject != null)
        {
            trajectory.SetTargetObject(targetObject);
        }
    }
    
    public override bool isPlaying()
    {
        return true; // This can be modified to check if the weapon object is currently active or playing
    }
}
