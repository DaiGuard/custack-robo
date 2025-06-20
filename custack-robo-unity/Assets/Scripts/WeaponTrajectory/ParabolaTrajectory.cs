using UnityEngine;

public class ParabolaTrajectory : WeaponTrajectory
{
    [SerializeField]
    private float _gravityScale = 9.81f; // Gravity constant

    [SerializeField]
    private float _initializeVelocity = 10f; // Initial velocity of the projectile

    [SerializeField]
    private Vector3 _launchDirection = new Vector3(0, 1, 1); // Direction of the launch

    [SerializeField]
    private Vector3 _gravityDirection = new Vector3(0, -1, 0); // Direction of gravity

    private Vector3 _direction;
    private Vector3 _velocity;
    private Vector3 _gravity;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _direction = transform.rotation *_launchDirection.normalized;
        _velocity = _initializeVelocity * _direction; // Calculate the initial velocity vector
        _gravity = transform.rotation * _gravityDirection.normalized * _gravityScale; // Calculate the gravity vector
    }

    // Update is called once per frame
    void Update()
    {
        _velocity += _gravity * Time.deltaTime; // Update the velocity with gravity
        var position = transform.position;  // Get the current position of the projectile
        var updatedPosition = position + _velocity * Time.deltaTime; // Update the position based on velocity
        _direction = (updatedPosition - position).normalized; // Calculate the new direction
        var updatedRotation = Quaternion.LookRotation(_direction); // Create a rotation based on the new direction

        transform.position = updatedPosition; // Set the new position of the projectile
        transform.rotation = updatedRotation; // Set the new rotation of the projectile
    }
}
