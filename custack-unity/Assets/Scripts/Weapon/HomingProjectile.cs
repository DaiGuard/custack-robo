using UnityEngine;

public class HomingProjectile : MonoBehaviour
{
    private int damage;
    private float speed;
    private float turnSpeed;
    private int ownerId;
    private Transform target;

    public void Initialize(WeaponData data, int id, Transform t)
    {
        damage = data.attackPower;
        speed = data.projectileSpeed;
        turnSpeed = data.homingSensitivity;
        ownerId = id;
        target = t;
        Destroy(gameObject, data.lifeTime);
    }

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);

        if (target != null && turnSpeed > 0)
        {
            Vector3 dir = (target.position - transform.position).normalized;
            Quaternion rot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, turnSpeed * 100f * Time.deltaTime);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var p = other.GetComponent<PlayerController>();
        if (p && p.PlayerID != ownerId)
        {
            p.TakeDamage(damage);
            Destroy(gameObject);
        }
        else if (!other.CompareTag("Player") && !other.CompareTag("Weapon"))
        {
            Destroy(gameObject); // 壁などで消滅
        }
    }
}