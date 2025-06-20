using UnityEngine;

public class FireMissile : MonoBehaviour
{
    [SerializeField]
    private GameObject missilePrefab;

    [SerializeField]
    private GameObject targetObject;

    [SerializeField]
    private float fireCooldown = 1.0f;

    private GameObject missile = null;

    private float fireCooldownTimer = 0.0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        fireCooldownTimer = fireCooldown;
    }

    // Update is called once per frame
    void Update()
    {
        if (fireCooldownTimer >= fireCooldown)
        {
            if (Input.GetKey(KeyCode.Space))
            {
                missile = Instantiate(missilePrefab, transform.position, transform.rotation);
                missile.GetComponent<MissleTracking>().target = targetObject;

                fireCooldownTimer = 0.0f;
            }
        }
        else
        {
            fireCooldownTimer += Time.deltaTime;
        }
    }
}
