using UnityEngine;

public class WeaponParticleSystem : WeaponSystem
{
    [SerializeField]
    private GameObject _weaponObject = null; // The weapon object to be triggered
    private GameObject _instanceObject = null;
    private ParticleSystem _particleSystem = null;


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

    public override void Play(Transform parentTransform=null)
    {
        if(_weaponObject == null)
        {
            Debug.LogWarning("Weapon object is not assigned.");
            return;
        }

        if (_instanceObject == null)
        {
            _instanceObject = Instantiate(_weaponObject,
                parentTransform.position, transform.rotation, parentTransform);

            _particleSystem = _instanceObject.GetComponent<ParticleSystem>();
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
        _particleSystem.Stop();
        _particleSystem.Clear();
        _particleSystem.Play();
    }

    public override bool isPlaying()
    {
        return _particleSystem != null && _particleSystem.isPlaying;
    }
}
