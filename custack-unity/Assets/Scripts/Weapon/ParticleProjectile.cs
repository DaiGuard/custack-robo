using UnityEngine;
using System.Collections.Generic;

public class ParticleProjectile : MonoBehaviour
{
    private ParticleSystem ps;
    private List<ParticleCollisionEvent> colEvents = new List<ParticleCollisionEvent>();
    private ParticleSystem.Particle[] particles;
    
    private int damage;
    private int ownerId;
    private Transform target;
    private float homingStrength;

    void Awake() => ps = GetComponent<ParticleSystem>();

    public void Initialize(WeaponData data, int id, Transform t)
    {
        damage = data.attackPower;
        ownerId = id;
        target = t;
        homingStrength = data.homingSensitivity;
    }

    // 衝突判定 (InspectorでSend Collision MessagesをONにすること)
    void OnParticleCollision(GameObject other)
    {
        var p = other.GetComponent<PlayerController>();
        if (p && p.PlayerID != ownerId)
        {
            int num = ps.GetCollisionEvents(other, colEvents);
            for(int i=0; i<num; i++) p.TakeDamage(damage);
        }
    }

    // 追尾処理
    void LateUpdate()
    {
        if (homingStrength <= 0 || !target || !ps.isPlaying) return;

        if (particles == null || particles.Length < ps.main.maxParticles)
            particles = new ParticleSystem.Particle[ps.main.maxParticles];

        int count = ps.GetParticles(particles);
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = (target.position - particles[i].position).normalized;
            Vector3 newVel = Vector3.Lerp(particles[i].velocity.normalized, dir, Time.deltaTime * homingStrength);
            particles[i].velocity = newVel * particles[i].velocity.magnitude;
        }
        ps.SetParticles(particles, count);
    }
}