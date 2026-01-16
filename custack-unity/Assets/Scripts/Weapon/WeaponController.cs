using UnityEngine;

public class WeaponController : MonoBehaviour
{
    private WeaponData data;
    private int ownerId;
    private Transform enemyTarget;
    private Collider meleeCollider;
    private ParticleProjectile particleScript;

    void Awake()
    {
        meleeCollider = GetComponent<Collider>();
        particleScript = GetComponent<ParticleProjectile>();
        if (meleeCollider) meleeCollider.enabled = false;
    }

    public void Initialize(WeaponData d, int id, Transform target)
    {
        data = d;
        ownerId = id;
        enemyTarget = target;

        // パーティクル制御スクリプトがあれば初期化
        if (particleScript != null && data.useParticleSystem)
        {
            particleScript.Initialize(data, ownerId, target);
        }
    }

    // 攻撃開始 (Animation Event)
    public void EnableHitbox()
    {
        if (data.useParticleSystem)
        {
            // パーティクル発射 (1発)
            GetComponent<ParticleSystem>()?.Emit(1); 
        }
        else if (data.isRanged)
        {
            // GameObjectミサイル発射
            FireProjectile();
        }
        else
        {
            // 近接攻撃ON
            if (meleeCollider) meleeCollider.enabled = true;
        }
    }

    // 攻撃終了 (Animation Event)
    public void DisableHitbox()
    {
        if (!data.isRanged && !data.useParticleSystem && meleeCollider)
        {
            meleeCollider.enabled = false;
        }
    }

    void FireProjectile()
    {
        if (!data.projectilePrefab) return;
        Vector3 spawnPos = transform.position + transform.forward * 0.5f;
        GameObject p = Instantiate(data.projectilePrefab, spawnPos, transform.rotation);
        HomingProjectile hp = p.GetComponent<HomingProjectile>();
        if (hp) hp.Initialize(data, ownerId, enemyTarget);
    }

    // 近接攻撃ヒット処理
    private void OnTriggerEnter(Collider other)
    {
        // 飛び道具モードなら本体の当たり判定は無視
        if (data.isRanged || data.useParticleSystem) return;

        var target = other.GetComponent<PlayerController>();
        if (target && target.PlayerID != ownerId)
        {
            target.TakeDamage(data.attackPower);
            if (data.hitEffectPrefab) Instantiate(data.hitEffectPrefab, other.ClosestPoint(transform.position), Quaternion.identity);
        }
    }
}