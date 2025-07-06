using UnityEngine;
using System.Collections.Generic;

public class WeaponLanding : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> _bulletObjects = null;

    [SerializeField]
    private GameObject _landingEffect = null;

    [SerializeField]
    private float _landingEffectDuration = 2.0f;

    [SerializeField]
    private float _lifeTime = 5.0f;

    private float _creationTime = 0.0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _creationTime = Time.realtimeSinceStartup;
    }

    // Update is called once per frame
    void Update()
    {
        if(Time.realtimeSinceStartup - _creationTime > _lifeTime)
        {
            Destroy(gameObject); // Destroy the object after its lifetime
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_bulletObjects != null)
        {
            foreach (var bullet in _bulletObjects)
            {
                if (bullet != null)
                {
                    bullet.SetActive(false); // Deactivate the bullet object
                }
            }
        }

        if (_landingEffect != null)
        {
            var effectObject = Instantiate(_landingEffect,
                collision.contacts[0].point, Quaternion.identity); // Instantiate the landing effect at the current position

            Destroy(effectObject, _landingEffectDuration); // Destroy the effect after the specified duration
        }

        // Optionally, destroy the landing effect after a delay
        Destroy(gameObject); // Adjust the delay as needed
    }
}
