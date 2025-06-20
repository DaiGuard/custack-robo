using UnityEngine;
using System.Collections.Generic;

public class MissleTracking : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> visibleObjects = null;

    [SerializeField]
    private GameObject explosionEffect = null;

    [SerializeField]
    private float initlizeVelocity = 0.0f;

    [SerializeField]
    private float maxVelocity = 0.0f;

    [SerializeField]
    private float acceleration = 0.0f;

    [SerializeField]
    private float directMaxRatio = 0.0f;

    [SerializeField]
    private float directMinRatio = 0.0f;

    [SerializeField]
    public GameObject target = null;

    [SerializeField]
    private float triggerTimeout = 0.5f;


    private bool enableTracking = true;
    private bool isTrigger = false;
    private float triggerCountdown = 0.0f;
    private float velocity;
    private float directRatio;
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        enableTracking = true;
        velocity = initlizeVelocity;
        directRatio = directMaxRatio;
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    // Update is called once per frame
    void Update()
    {
        // Calculate the new velocity based on acceleration
        velocity += acceleration * Time.deltaTime;
        if (velocity > maxVelocity)
        {
            velocity = maxVelocity;
        }

        if (enableTracking)
        {
            if (isTrigger)
            {
                triggerCountdown += Time.deltaTime;
                if (triggerCountdown >= triggerTimeout)
                {
                    Explosion();
                }
            }

            if (target != null)
            {
                // Calculate the direction to the target
                Vector3 direction = (target.transform.position - transform.position).normalized;
                direction = Vector3.Lerp(transform.forward, direction, directRatio);

                // Move the missile towards the target
                transform.position += direction * velocity * Time.deltaTime;

                // Rotate the missile to face the target
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                // Smoothly rotate towards the target
                lookRotation *= initialRotation;
                transform.rotation = lookRotation;
                // transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
            }
            else
            {
                // If no target is set, just move forward with the initial velocity
                transform.position += transform.forward * velocity * Time.deltaTime;
            }
        }
    }

    void Explosion()
    {
        enableTracking = false;
        
        if (visibleObjects != null)
        {
            foreach (GameObject obj in visibleObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
        
        if (explosionEffect != null)
        {
            explosionEffect.GetComponent<ParticleSystem>().Play();
        }

        Destroy(gameObject, 2.0f);
    }


    void OnCollisionEnter(Collision collision)
    {
        Explosion();
    }
    void OnTriggerEnter(Collider other)
    {
        directRatio = directMinRatio;
        isTrigger = true;
        triggerCountdown = 0.0f;
    }
}
