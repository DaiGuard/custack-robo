using UnityEngine;

public class WeaponLifetime : MonoBehaviour
{
    [SerializeField]
    private float _lifetime = 5.0f; // Lifetime of the weapon in seconds

    private float _lifetimeCounter = 0.0f; // Counter to track the lifetime of the weapon

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        _lifetimeCounter += Time.deltaTime; // Increment the lifetime counter
        if (_lifetimeCounter >= _lifetime)
        {
            Destroy(gameObject); // Destroy the weapon object after its lifetime has expired
        }
    }
}
