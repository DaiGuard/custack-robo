using UnityEngine;

public class TrackingTrajectory : WeaponTrajectory
{
    [SerializeField] private float _initializeVelocity = 10f; // Initial velocity of the projectile
    [SerializeField] private float _maxVelocity = 20f; // Maximum velocity of the projectile
    [SerializeField] private float _acceleration = 5f; // Acceleration of the projectile
    [SerializeField] private float _directionMaxRatio = 0.1f; // Maximum ratio for direction adjustment
    [SerializeField] private float _directionMinRatio = 0.0f; // Minimum ratio for direction adjustment
    [SerializeField] private Vector3 _launchDirection = new Vector3(0, 0, 1); // Direction of the launch
    [SerializeField] private float _minTrackingDistance = 0.5f; // Minimum distance to start tracking the target

    private Vector3 _direction;
    private float _velocity;
    private bool _isTracking = true;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _direction = transform.rotation * _launchDirection.normalized;
        _velocity = _initializeVelocity;
        _velocity = Mathf.Clamp(_velocity, 0, _maxVelocity); // Ensure the initial velocity does not exceed the maximum
        _isTracking = true;
    }

    // Update is called once per frame
    void Update()
    {
        var position = transform.position;  // Get the current position of the projectile
        var targetObject = GetTargetObject(); // Get the target object for the trajectory
        var directionRaito = _directionMaxRatio; // Default direction ratio

        _velocity += _acceleration * Time.deltaTime;
        _velocity = Mathf.Clamp(_velocity, 0, _maxVelocity);

        if (targetObject != null)
        {
            // Calculate the direction to the target
            Vector3 distance = targetObject.transform.position - position;
            float distanceMagnitude = distance.magnitude;
            if (distanceMagnitude < _minTrackingDistance)
            {
                _isTracking = false; // Stop tracking if very close to the target
            }

            if(_isTracking)
            {
                Vector3 targetDirection = (targetObject.transform.position - position).normalized;
                _direction = Vector3.Lerp(_direction, targetDirection, directionRaito);
            }
        }
        else
        {
            position += _velocity * _direction * Time.deltaTime; // Update the position based on velocity
        }

        transform.position = position + _velocity * _direction* Time.deltaTime; // Set the new position of the projectile
        transform.rotation = Quaternion.LookRotation(_direction); // Set the new rotation of the projectile
    }
}
