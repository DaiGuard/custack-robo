using UnityEngine;
using Custack.Combat;

namespace Custack.Robot
{
    /// <summary>
    /// ロボットの視覚表現（アバター、HPバー、被弾点滅、スタン放電、無敵時間点滅、大破爆発・煙、向き矢印、ロックオンレティクル）を制御するコンポーネント。
    /// </summary>
    public class RobotVisual : MonoBehaviour
    {
        [Header("プレイヤー表示カラー")]
        public Color playerColor = Color.cyan;
        public Color hitFlashColor = Color.white;

        [Header("AprilTag 番号表示")]
        public TextMesh tagIdTextMesh;
        [Tooltip("機体が回転しても番号は常に正位置（上向き）を保つ")]
        public bool keepTextUpright = true;

        [Header("ロックオン表示")]
        [SerializeField]
        private bool isLockedOn = false;
        public GameObject lockOnMarkerObject;

        [Header("頭上 HP バー設定")]
        public Transform hpBarRoot;
        public Transform hpBarFillTransform;
        public Transform hpBarDamageTransform;
        public Renderer hpBarFillRenderer;
        public Renderer hpBarDamageRenderer;
        public TextMesh hpTextMesh;
        public Vector3 hpBarOffset = new Vector3(0, 0.88f, -0.1f);
        public bool showHpBar = true;

        [Header("レンダラー参照")]
        [Tooltip("外周ドーナツ型リングの Renderer (プレイヤーカラー・被弾点滅・無敵点滅を適用)")]
        public Renderer donutRenderer;

        [Tooltip("中央完全黒マスクの Renderer (地形線やエフェクトを遮蔽しプロジェクター光を100%消灯)")]
        public Renderer blackMaskRenderer;

        private SpriteRenderer spriteRenderer;
        private Health health;
        private float flashEndTime = 0f;
        private MaterialPropertyBlock propertyBlock;
        private GameObject wreckageSmokeObj;
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");

        private const float MaxBarWidth = 0.98f;
        private const float BarHeight = 0.09f;
        private float targetHpRatio = 1.0f;
        private float currentFillRatio = 1.0f;
        private float damageFillRatio = 1.0f;

        void Awake()
        {
            if (donutRenderer == null)
            {
                var donutObj = transform.Find("DonutRing") ?? transform.Find("Avatar");
                if (donutObj != null) donutRenderer = donutObj.GetComponent<Renderer>();
            }

            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (tagIdTextMesh == null)
            {
                tagIdTextMesh = GetComponentInChildren<TextMesh>();
            }

            health = GetComponent<Health>();

            if (health != null)
            {
                health.OnDamaged += OnDamaged;
                health.OnHealthChanged += UpdateHpBar;
                health.OnDeath += OnDeath;
                health.OnRespawn += OnRespawn;
                targetHpRatio = health.HealthPercent;
                currentFillRatio = targetHpRatio;
                damageFillRatio = targetHpRatio;
            }

            EnsureHpBar();
            ApplyColor(playerColor);
        }

        void Start()
        {
            EnsureHpBar();
            if (health != null)
            {
                UpdateHpBar(health.currentHp, health.maxHp);
            }
        }

        /// <summary>
        /// 頭上 HP バー（外枠、背景、ダメージ遅延バー、現在HPバー、HP数値テキスト）を自動構築
        /// </summary>
        public void EnsureHpBar()
        {
            if (!showHpBar) return;
            if (hpBarRoot != null && hpBarFillTransform != null) return;

            var existingHpBar = transform.Find("HpBar_Root");
            if (existingHpBar != null)
            {
                hpBarRoot = existingHpBar;
                var fill = hpBarRoot.Find("HpBar_Fill");
                if (fill != null)
                {
                    hpBarFillTransform = fill;
                    hpBarFillRenderer = fill.GetComponent<Renderer>();
                }
                var dmg = hpBarRoot.Find("HpBar_Damage");
                if (dmg != null)
                {
                    hpBarDamageTransform = dmg;
                    hpBarDamageRenderer = dmg.GetComponent<Renderer>();
                }
                var txt = hpBarRoot.Find("HpText");
                if (txt != null) hpTextMesh = txt.GetComponent<TextMesh>();
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Sprites/Default")
                      ?? Shader.Find("Unlit/Color");

            var rootObj = new GameObject("HpBar_Root");
            rootObj.transform.SetParent(transform);
            rootObj.transform.localPosition = hpBarOffset;
            rootObj.transform.localRotation = Quaternion.identity;
            hpBarRoot = rootObj.transform;

            // 1. 外枠ボーダー (Z = 0.006)
            var borderObj = new GameObject("HpBar_Border");
            borderObj.transform.SetParent(rootObj.transform);
            borderObj.transform.localPosition = new Vector3(0, 0, 0.006f);
            borderObj.transform.localScale = new Vector3(1.06f, 0.16f, 1f);
            var borderMf = borderObj.AddComponent<MeshFilter>();
            borderMf.sharedMesh = RobotMeshHelper.GetOrCreateQuadMesh();
            var borderMr = borderObj.AddComponent<MeshRenderer>();
            borderMr.material = new Material(shader) { color = new Color(0.2f, 0.25f, 0.35f, 0.85f) };

            // 2. 背景バー (Z = 0.004)
            var bgObj = new GameObject("HpBar_Bg");
            bgObj.transform.SetParent(rootObj.transform);
            bgObj.transform.localPosition = new Vector3(0, 0, 0.004f);
            bgObj.transform.localScale = new Vector3(1.02f, 0.13f, 1f);
            var bgMf = bgObj.AddComponent<MeshFilter>();
            bgMf.sharedMesh = RobotMeshHelper.GetOrCreateQuadMesh();
            var bgMr = bgObj.AddComponent<MeshRenderer>();
            bgMr.material = new Material(shader) { color = new Color(0.04f, 0.05f, 0.08f, 0.92f) };

            // 3. ダメージ遅延バー (Z = 0.002, 暗赤色)
            var dmgObj = new GameObject("HpBar_Damage");
            dmgObj.transform.SetParent(rootObj.transform);
            dmgObj.transform.localPosition = new Vector3(0, 0, 0.002f);
            dmgObj.transform.localScale = new Vector3(MaxBarWidth, BarHeight, 1f);
            var dmgMf = dmgObj.AddComponent<MeshFilter>();
            dmgMf.sharedMesh = RobotMeshHelper.GetOrCreateQuadMesh();
            hpBarDamageRenderer = dmgObj.AddComponent<MeshRenderer>();
            hpBarDamageRenderer.material = new Material(shader) { color = new Color(0.85f, 0.2f, 0.2f, 0.9f) };
            hpBarDamageTransform = dmgObj.transform;

            // 4. 現在 HP バー (Z = 0, プレイヤーカラー/ネオン)
            var fillObj = new GameObject("HpBar_Fill");
            fillObj.transform.SetParent(rootObj.transform);
            fillObj.transform.localPosition = new Vector3(0, 0, 0f);
            fillObj.transform.localScale = new Vector3(MaxBarWidth, BarHeight, 1f);
            var fillMf = fillObj.AddComponent<MeshFilter>();
            fillMf.sharedMesh = RobotMeshHelper.GetOrCreateQuadMesh();
            hpBarFillRenderer = fillObj.AddComponent<MeshRenderer>();
            hpBarFillRenderer.material = new Material(shader) { color = playerColor };
            hpBarFillTransform = fillObj.transform;

            // 5. HP 数値テキスト (Z = -0.01, 中央ネオン表示)
            var textObj = new GameObject("HpText");
            textObj.transform.SetParent(rootObj.transform);
            textObj.transform.localPosition = new Vector3(0, 0, -0.01f);
            hpTextMesh = textObj.AddComponent<TextMesh>();
            hpTextMesh.characterSize = 0.045f;
            hpTextMesh.fontSize = 32;
            hpTextMesh.anchor = TextAnchor.MiddleCenter;
            hpTextMesh.alignment = TextAlignment.Center;
            hpTextMesh.color = Color.white;
            hpTextMesh.fontStyle = FontStyle.Bold;
            hpTextMesh.text = health != null ? $"{Mathf.CeilToInt(health.currentHp)}" : "1000";
        }

        public void Initialize(int playerId, Color color)
        {
            playerColor = color;
            EnsureHpBar();
            ApplyColor(playerColor);

            if (tagIdTextMesh != null)
            {
                tagIdTextMesh.text = playerId.ToString();
            }
            if (hpBarFillRenderer != null)
            {
                hpBarFillRenderer.material.color = playerColor;
            }
        }

        public void SetLockOnStatus(bool locked)
        {
            isLockedOn = locked;
            if (lockOnMarkerObject != null)
            {
                lockOnMarkerObject.SetActive(isLockedOn);
            }
        }

        private void OnDamaged(float damage, Vector2 hitPoint)
        {
            flashEndTime = Time.time + 0.1f;
        }

        private void OnDeath()
        {
            // 大破時の大爆発パーティクル再生
            EffectFactory.PlayRobotDestructionExplosion(transform.position, playerColor);

            // 黒煙 & 放電スパークエフェクトのアタッチ
            if (wreckageSmokeObj == null)
            {
                wreckageSmokeObj = EffectFactory.AttachWreckageSmokeAndSparks(transform);
            }

            if (tagIdTextMesh != null)
            {
                tagIdTextMesh.color = Color.red;
            }

            targetHpRatio = 0f;
            if (hpTextMesh != null)
            {
                hpTextMesh.text = "DEAD";
                hpTextMesh.color = Color.red;
            }
        }

        private void OnRespawn()
        {
            // 煙エフェクトの破棄
            if (wreckageSmokeObj != null)
            {
                Destroy(wreckageSmokeObj);
                wreckageSmokeObj = null;
            }

            if (tagIdTextMesh != null)
            {
                tagIdTextMesh.color = Color.white;
            }

            targetHpRatio = 1.0f;
            currentFillRatio = 1.0f;
            damageFillRatio = 1.0f;
            if (hpTextMesh != null)
            {
                hpTextMesh.text = health != null ? $"{Mathf.CeilToInt(health.currentHp)}" : "1000";
                hpTextMesh.color = Color.white;
            }

            ApplyColor(playerColor);
        }

        private void UpdateHpBar(float current, float max)
        {
            targetHpRatio = Mathf.Clamp01(current / max);
            if (hpTextMesh != null && (health == null || !health.IsDead))
            {
                hpTextMesh.text = $"{Mathf.CeilToInt(current)}";
            }
        }

        public void ApplyColor(Color col)
        {
            if (donutRenderer != null)
            {
                if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
                donutRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(ColorPropId, col);
                propertyBlock.SetColor(BaseColorPropId, col);
                donutRenderer.SetPropertyBlock(propertyBlock);
            }
            else if (spriteRenderer != null)
            {
                spriteRenderer.color = col;
            }
        }

        void Update()
        {
            // 1. HP バーの滑らかな伸縮補間 (ダメージ遅延バー & 現在HPバー)
            if (showHpBar)
            {
                currentFillRatio = Mathf.MoveTowards(currentFillRatio, targetHpRatio, Time.deltaTime * 3.5f);
                damageFillRatio = Mathf.MoveTowards(damageFillRatio, currentFillRatio, Time.deltaTime * 0.8f);

                if (hpBarFillTransform != null)
                {
                    hpBarFillTransform.localScale = new Vector3(MaxBarWidth * currentFillRatio, BarHeight, 1f);
                    hpBarFillTransform.localPosition = new Vector3(-MaxBarWidth * 0.5f + (MaxBarWidth * currentFillRatio) * 0.5f, 0, 0f);
                }

                if (hpBarDamageTransform != null)
                {
                    hpBarDamageTransform.localScale = new Vector3(MaxBarWidth * damageFillRatio, BarHeight, 1f);
                    hpBarDamageTransform.localPosition = new Vector3(-MaxBarWidth * 0.5f + (MaxBarWidth * damageFillRatio) * 0.5f, 0, 0.002f);
                }

                // HP 残量に応じた HP バーカラー
                if (hpBarFillRenderer != null)
                {
                    Color barCol = playerColor;
                    if (health != null && health.IsDead)
                    {
                        barCol = new Color(0.3f, 0.05f, 0.05f, 0.5f);
                    }
                    else if (currentFillRatio <= 0.25f)
                    {
                        // 25%以下: 赤色警告 (ピンチ点滅)
                        bool blink = (Mathf.FloorToInt(Time.time * 8f) % 2) == 0;
                        barCol = blink ? new Color(1f, 0.2f, 0.2f) : new Color(0.6f, 0.05f, 0.05f);
                    }
                    else if (currentFillRatio <= 0.5f)
                    {
                        // 50%以下: イエロー/アンバー
                        barCol = new Color(1f, 0.85f, 0.15f);
                    }
                    hpBarFillRenderer.material.color = barCol;
                }
            }

            if (health != null && health.IsDead)
            {
                // 撃破時は暗赤色の低輝度点滅 (プロジェクター光を落とし大破を演出)
                bool blink = (Mathf.FloorToInt(Time.time * 3f) % 2) == 0;
                ApplyColor(blink ? new Color(0.35f, 0.05f, 0.05f, 0.4f) : new Color(0.1f, 0.02f, 0.02f, 0.2f));
                return;
            }

            // 2. 被弾直後の白点滅 (0.1s)
            if (Time.time < flashEndTime)
            {
                ApplyColor(hitFlashColor);
            }
            // 3. スタン中の黄色放電点滅
            else if (health != null && health.IsStunned)
            {
                bool blink = (Mathf.FloorToInt(Time.time * 10f) % 2) == 0;
                ApplyColor(blink ? new Color(1f, 0.9f, 0.1f) : playerColor * 0.4f);
            }
            // 4. 無敵時間中の外周円点滅 (指示: 無敵時間中はロボット周辺の円を点滅)
            else if (health != null && health.IsInvincible)
            {
                bool blink = (Mathf.FloorToInt(Time.time * 8f) % 2) == 0;
                Color invincibleColor = playerColor;
                invincibleColor.a = blink ? 1.0f : 0.15f;
                ApplyColor(invincibleColor);
            }
            // 5. 通常表示
            else
            {
                ApplyColor(playerColor);
            }
        }

        void LateUpdate()
        {
            // 機体が回転しても HP バーは常に画面上で正位置（水平上向き）を保つ
            if (showHpBar && hpBarRoot != null)
            {
                hpBarRoot.position = transform.position + hpBarOffset;
                hpBarRoot.rotation = Quaternion.identity;
            }

            // 機体が回転しても番号は常に正位置（上向き）を保つ
            if (keepTextUpright && tagIdTextMesh != null)
            {
                tagIdTextMesh.transform.rotation = Quaternion.identity;
            }
        }

        void OnDrawGizmos()
        {
            // ロックオンマーカーのギズモ描画
            if (isLockedOn)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(transform.position, Vector3.one * 0.9f);
                Gizmos.DrawWireSphere(transform.position, 0.6f);
            }
        }
    }
}
