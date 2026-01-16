using UnityEngine;

public class PlayerController : MonoBehaviour
{
[Header("Status")]
    public int PlayerID = 1; // 1 or 2
    public int maxHP = 100;
    private int currentHP;

    [Header("Movement")]
    public float baseSpeed = 5f;
    private Rigidbody rb;

    [Header("Equipment")]
    public WeaponData rightWeaponData;
    public WeaponData leftWeaponData;
    public Transform rightHandBone;
    public Transform leftHandBone;

    private WeaponController rightWeaponInstance;
    private WeaponController leftWeaponInstance;
    private Animator animator;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        currentHP = maxHP;

        // 相手プレーヤの検索 (簡易実装)
        Transform target = FindEnemy();

        // 武器生成
        rightWeaponInstance = Equip(rightWeaponData, rightHandBone, target);
        leftWeaponInstance = Equip(leftWeaponData, leftHandBone, target);
    }

    void Update()
    {
        HandleMovement();
        HandleInput();
    }

    Transform FindEnemy()
    {
        // 自分以外のPlayerタグを持つオブジェクトを探す
        foreach(var p in GameObject.FindGameObjectsWithTag("Player"))
        {
            if(p.GetComponent<PlayerController>()?.PlayerID != this.PlayerID)
                return p.transform;
        }
        return null;
    }

    // --- 移動処理 ---
    void HandleMovement()
    {
        float h = Input.GetAxis($"P{PlayerID}_Horizontal");
        float v = Input.GetAxis($"P{PlayerID}_Vertical");
        Vector3 inputDir = new Vector3(h, 0, v).normalized;

        if (inputDir.magnitude > 0.1f)
        {
            float envMultiplier = 1.0f;
            RaycastHit hit;
            // 足元の地形判定
            if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out hit, 2f))
            {
                if (hit.collider.CompareTag("Water")) envMultiplier = 0.5f;
                else if (hit.collider.CompareTag("Grass")) envMultiplier = 0.8f;
            }

            // 移動 (Z=0平面)
            Vector3 velocity = inputDir * baseSpeed * envMultiplier;
            rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
            
            // 回転
            transform.LookAt(transform.position + inputDir);
        }
    }

    // --- 攻撃入力 ---
    void HandleInput()
    {
        if (Input.GetButtonDown($"P{PlayerID}_AttackRight")) animator.SetTrigger("RightAttack");
        if (Input.GetButtonDown($"P{PlayerID}_AttackLeft")) animator.SetTrigger("LeftAttack");
    }

    // --- Animation Events (Inspectorで指定) ---
    public void RightAttackEvent() => rightWeaponInstance?.EnableHitbox();
    public void RightAttackEndEvent() => rightWeaponInstance?.DisableHitbox();
    public void LeftAttackEvent() => leftWeaponInstance?.EnableHitbox();
    public void LeftAttackEndEvent() => leftWeaponInstance?.DisableHitbox();

    // --- 装備処理 ---
    WeaponController Equip(WeaponData data, Transform bone, Transform target)
    {
        if (!data || !data.weaponPrefab) return null;
        GameObject obj = Instantiate(data.weaponPrefab, bone);
        WeaponController wc = obj.GetComponent<WeaponController>();
        if(wc) wc.Initialize(data, PlayerID, target);
        return wc;
    }

    // --- ダメージ処理 ---
    public void TakeDamage(int damage)
    {
        currentHP -= damage;
        Debug.Log($"P{PlayerID} took damage: {damage}. Remaining: {currentHP}");
        if (currentHP <= 0) Debug.Log($"P{PlayerID} Defeated.");
    }
}