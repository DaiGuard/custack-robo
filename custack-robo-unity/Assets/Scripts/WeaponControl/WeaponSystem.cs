using UnityEngine;

public class WeaponSystem : MonoBehaviour
{
    [SerializeField]
    private GameObject _weaponObject = null; // The weapon object to be triggered

    private GameObject _targetObject = null;
    public GameObject targetObject { get; set; }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }


    public void Play()
    {
        var obj = Instantiate(_weaponObject,
            transform.position, transform.rotation); // Trigger the weapon by instantiating i

        var trajectory = obj.GetComponent<WeaponTrajectory>();
        if (trajectory != null)
        {
            trajectory.SetTargetObject(_targetObject);
        }
    }

    public bool isPlaying()
    {
        return true;
    }
}
