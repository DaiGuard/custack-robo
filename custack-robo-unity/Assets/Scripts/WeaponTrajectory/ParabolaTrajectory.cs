using System;
using System.IO;
using UnityEngine;

public class ParabolaTrajectory : WeaponTrajectory
{
    [SerializeField]
    private float _gravityScale = 9.81f; // Gravity constant

    [SerializeField]
    private float _initializeVelocity = 10f; // Initial velocity of the projectile

    private Vector3 _launchDirection = new Vector3(0, 0, 1); // Direction of the launch

    [SerializeField]
    private Vector3 _gravityDirection = new Vector3(0, -1, 0); // Direction of gravity

    private Vector3 _direction;
    private Vector3 _velocity;
    private Vector3 _gravity;

    void Start()
    {
        var distance = Vector3.Distance(_targetObject.transform.position, transform.position);
        // var angle = Mathf.Asin(distance * _gravityScale / (2.0f * _initializeVelocity * _initializeVelocity)) * 0.5f;
        var angle = Mathf.Atan2(distance * _gravityScale, 2.0f * _initializeVelocity * _initializeVelocity);
        

        Vector3 v1 = (_targetObject.transform.position - transform.position).normalized;
        Vector3 v2 = Vector3.Cross(_gravityDirection, v1).normalized;

        var term1 = v1 * Mathf.Cos(angle);
        var term2 = Vector3.Cross(v2, v1) * Mathf.Sin(angle);
        var term3 = v2 * Vector3.Dot(v2, v1) * (1.0f - Mathf.Cos(angle));

        var direction = term1 + term2 + term3;

        _direction = direction;
        _velocity = _initializeVelocity * _direction;
        _gravity = _gravityDirection.normalized * _gravityScale;
    }

    void Update()
    {
        _velocity += _gravity * Time.deltaTime;
        var position = transform.position;
        var updatedPosition = position + _velocity * Time.deltaTime;
        _direction = (updatedPosition - position).normalized;
        var updatedRotation = Quaternion.LookRotation(_direction);

        transform.position = updatedPosition;
        transform.rotation = updatedRotation;
    }
}
