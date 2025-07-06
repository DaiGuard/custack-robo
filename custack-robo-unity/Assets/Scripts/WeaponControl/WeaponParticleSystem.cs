using UnityEngine;

public class WeaponParticleSystem : WeaponSystem
{
    private ParticleSystem _particleSystem = null;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void Start()
    {
        base.Start();
        
        _particleSystem = GetComponent<ParticleSystem>();
    }

    // Update is called once per frame
    protected override void Update()
    {
        base.Update();
    }

    public override void Play()
    {
        if (_particleSystem == null)
        {
            Debug.LogWarning("ParticleSystem is not assigned.");
            return;
        }

        if (Time.realtimeSinceStartup - _lastTime < _reloadTime)
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
