using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Custack.Equipment;
using Custack.Robot;
using Custack.Terrain;

namespace Custack.Combat
{
    /// <summary>
    /// 4種類の地形マップ（森・雪山・市街地・火山）と3種類の脚ユニット（オムニ・タイヤ・キャタピラ）の
    /// 地形効果（減速・スリップ・スタック・溶岩ダメージ・走破性）を直感的にテスト・体感するための専用コントローラー。
    /// </summary>
    public class TerrainSandboxController : MonoBehaviour
    {
        [Header("プレイヤーテスト機体")]
        public Transform playerRobotTransform;
        public float baseMoveSpeed = 5.0f;
        public float rotateSpeed = 360f;

        [Header("現在装備中の脚 & 武器")]
        public LegDeviceType currentLegType = LegDeviceType.Omni;
        public ArmDeviceType currentWeaponType = ArmDeviceType.Gatling;
        private LegMovementConfig currentLegConfig;
        private ArmWeaponConfig currentArmConfig;
        private WeaponBase activeWeapon;
        private Health playerHealth;

        [Header("走行物理パラメータ")]
        private Vector2 currentVelocity = Vector2.zero;
        private float currentActualSpeed = 0f;

        private Camera mainCam;
        private Vector3 startPosition;
        private GameObject wreckageSmokeObj;

        void Awake()
        {
            mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
        }

        void Start()
        {
            if (playerRobotTransform == null) playerRobotTransform = transform;
            startPosition = playerRobotTransform.position;

            playerHealth = playerRobotTransform.GetComponent<Health>();
            if (playerHealth == null) playerHealth = playerRobotTransform.gameObject.AddComponent<Health>();

            SetupLeg(currentLegType);
            SetupWeapon(currentWeaponType);

            playerHealth.OnDeath += () =>
            {
                EffectFactory.PlayRobotDestructionExplosion(playerRobotTransform.position, new Color(0.2f, 0.8f, 1f));
                if (wreckageSmokeObj == null)
                {
                    wreckageSmokeObj = EffectFactory.AttachWreckageSmokeAndSparks(playerRobotTransform);
                }
            };

            playerHealth.OnRespawn += () =>
            {
                if (wreckageSmokeObj != null)
                {
                    Destroy(wreckageSmokeObj);
                    wreckageSmokeObj = null;
                }
            };
        }

        public void SetupLeg(LegDeviceType type)
        {
            currentLegType = type;
            currentLegConfig = LegMovementConfig.CreateDefault(type);
        }

        public void SetupWeapon(ArmDeviceType type)
        {
            currentWeaponType = type;
            currentArmConfig = ArmWeaponConfig.CreateDefault(type);

            if (activeWeapon != null) Destroy(activeWeapon);

            switch (type)
            {
                case ArmDeviceType.Gatling:
                    activeWeapon = playerRobotTransform.gameObject.AddComponent<GatlingWeapon>();
                    break;
                case ArmDeviceType.Sword:
                    activeWeapon = playerRobotTransform.gameObject.AddComponent<SwordWeapon>();
                    break;
                case ArmDeviceType.Cannon:
                    activeWeapon = playerRobotTransform.gameObject.AddComponent<LaserCannonWeapon>();
                    break;
            }

            if (activeWeapon != null)
            {
                activeWeapon.Initialize(0, true, currentArmConfig, playerRobotTransform);
            }
        }

        public void ResetPositionAndHp()
        {
            if (playerRobotTransform != null)
            {
                playerRobotTransform.position = startPosition;
                currentVelocity = Vector2.zero;
            }
            if (playerHealth != null)
            {
                playerHealth.Respawn(1000f);
            }
        }

        void Update()
        {
            HandleMapSwitchInput();
            HandleLegAndWeaponSwitchInput();
            HandlePlayerMovementPhysics();
            HandleWeaponInput();
            HandleTerrainDamage();
        }

        private void HandleMapSwitchInput()
        {
            var kb = Keyboard.current;
            if (kb == null || TerrainMapManager.Instance == null) return;

            if (kb.f5Key.wasPressedThisFrame || kb.digit1Key.wasPressedThisFrame) TerrainMapManager.Instance.SwitchMap(MapType.Forest);
            if (kb.f6Key.wasPressedThisFrame || kb.digit2Key.wasPressedThisFrame) TerrainMapManager.Instance.SwitchMap(MapType.Snow);
            if (kb.f7Key.wasPressedThisFrame || kb.digit3Key.wasPressedThisFrame) TerrainMapManager.Instance.SwitchMap(MapType.City);
            if (kb.f8Key.wasPressedThisFrame || kb.digit4Key.wasPressedThisFrame) TerrainMapManager.Instance.SwitchMap(MapType.Volcano);
        }

        private void HandleLegAndWeaponSwitchInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Q / E キーで脚ユニット切替
            if (kb.qKey.wasPressedThisFrame)
            {
                int next = ((int)currentLegType + 2) % 3; // 前へ
                SetupLeg((LegDeviceType)next);
            }
            if (kb.eKey.wasPressedThisFrame)
            {
                int next = ((int)currentLegType + 1) % 3; // 次へ
                SetupLeg((LegDeviceType)next);
            }

            // Z / X / C キーで武器切替
            if (kb.zKey.wasPressedThisFrame) SetupWeapon(ArmDeviceType.Gatling);
            if (kb.xKey.wasPressedThisFrame) SetupWeapon(ArmDeviceType.Sword);
            if (kb.cKey.wasPressedThisFrame) SetupWeapon(ArmDeviceType.Cannon);

            // R キーでリセット
            if (kb.rKey.wasPressedThisFrame) ResetPositionAndHp();
        }

        private void HandlePlayerMovementPhysics()
        {
            if (playerRobotTransform == null) return;

            if (playerHealth != null && (playerHealth.IsDead || playerHealth.IsStunned))
            {
                currentVelocity = Vector2.zero;
                currentActualSpeed = 0f;
                return;
            }

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // 1. 生のスティック/WASD入力
            Vector2 rawMove = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) rawMove.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) rawMove.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) rawMove.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) rawMove.x += 1f;
            }

            if (rawMove.sqrMagnitude > 0.001f) rawMove.Normalize();

            // 2. 現在地の地形を取得
            TerrainType currentTerrain = TerrainType.Normal;
            if (TerrainManager.Instance != null)
            {
                currentTerrain = TerrainManager.Instance.GetTerrainAt(playerRobotTransform.position);
            }

            // 3. 地形 × 脚ユニットの速度倍率
            float speedMultiplier = currentLegConfig != null ? currentLegConfig.GetSpeedMultiplier(currentTerrain) : 1.0f;
            float targetSpeed = baseMoveSpeed * speedMultiplier;
            Vector2 targetVelocity = rawMove * targetSpeed;

            // 4. 氷上スリップ / 摩擦慣性の計算
            float slipFactor = (currentTerrain == TerrainType.Ice && currentLegConfig != null) ? currentLegConfig.iceSlipFactor : 0.0f;
            if (slipFactor > 0.01f)
            {
                // 氷上: 慣性で滑る (Lerpの追従率を下げる)
                float gripRate = Mathf.Lerp(15f, 1.2f, slipFactor);
                currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, Time.deltaTime * gripRate);
            }
            else
            {
                // 通常: 即座に追従
                currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, Time.deltaTime * 20f);
            }

            // 5. 座標更新
            playerRobotTransform.position += (Vector3)(currentVelocity * Time.deltaTime);
            currentActualSpeed = currentVelocity.magnitude;

            // 6. マウスカーソル方向へ旋回
            if (mouse != null && mainCam != null)
            {
                Vector2 mouseScreenPos = mouse.position.ReadValue();
                Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, -mainCam.transform.position.z));
                mouseWorldPos.z = 0;
                Vector3 dir = (mouseWorldPos - playerRobotTransform.position).normalized;
                if (dir.sqrMagnitude > 0.001f)
                {
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                    playerRobotTransform.rotation = Quaternion.Euler(0, 0, angle);
                }
            }
        }

        private void HandleTerrainDamage()
        {
            if (playerRobotTransform == null || playerHealth == null || playerHealth.IsDead || playerHealth.IsInvincible) return;

            if (TerrainManager.Instance != null)
            {
                TerrainType t = TerrainManager.Instance.GetTerrainAt(playerRobotTransform.position);
                if (t == TerrainType.Lava)
                {
                    float baseDmg = 25f * Time.deltaTime; // 溶岩/ハザード: 毎秒25ダメージ
                    float reduction = currentLegConfig != null ? currentLegConfig.lavaDamageReduction : 0f;
                    float finalDmg = baseDmg * (1.0f - reduction);

                    playerHealth.TakeDamage(finalDmg, playerRobotTransform.position);
                }
            }
        }

        private void HandleWeaponInput()
        {
            if (activeWeapon == null || currentArmConfig == null) return;
            if (playerHealth != null && (playerHealth.IsDead || playerHealth.IsStunned)) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            bool isFire = false;
            if (currentArmConfig.isAutomatic)
            {
                isFire = (mouse != null && mouse.leftButton.isPressed) || (kb != null && (kb.spaceKey.isPressed || kb.jKey.isPressed));
            }
            else
            {
                isFire = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame));
            }

            if (isFire)
            {
                activeWeapon.TryFire(playerRobotTransform.up, null);
            }
        }

        [Header("ビジュアルデバッグ")]
        public bool showColliderOverlay = true;
        private static Material lineMaterial;

        private static void CreateLineMaterial()
        {
            if (!lineMaterial)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                lineMaterial = new Material(shader);
                lineMaterial.hideFlags = HideFlags.HideAndDontSave;
                lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                lineMaterial.SetInt("_ZWrite", 0);
            }
        }

        void OnRenderObject()
        {
            if (!showColliderOverlay) return;

            CreateLineMaterial();
            lineMaterial.SetPass(0);

            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);

            var zones = FindObjectsByType<TerrainZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int z = 0; z < zones.Length; z++)
            {
                var zone = zones[z];
                if (zone == null || !zone.gameObject.activeInHierarchy) continue;

                Color c = zone.Type switch
                {
                    TerrainType.Forest => new Color(0f, 1f, 0.4f, 0.9f),
                    TerrainType.Mud => new Color(1f, 0.6f, 0.1f, 0.9f),
                    TerrainType.Ice => new Color(0f, 0.9f, 1f, 0.9f),
                    TerrainType.Lava => new Color(1f, 0.2f, 0.1f, 0.9f),
                    _ => Color.white
                };
                GL.Color(c);

                var col = zone.GetComponent<Collider2D>();
                if (col is BoxCollider2D box)
                {
                    Vector2 size = box.size;
                    Vector2 offset = box.offset;
                    Vector3 p1 = zone.transform.TransformPoint(new Vector3(offset.x - size.x / 2f, offset.y - size.y / 2f, 0));
                    Vector3 p2 = zone.transform.TransformPoint(new Vector3(offset.x + size.x / 2f, offset.y - size.y / 2f, 0));
                    Vector3 p3 = zone.transform.TransformPoint(new Vector3(offset.x + size.x / 2f, offset.y + size.y / 2f, 0));
                    Vector3 p4 = zone.transform.TransformPoint(new Vector3(offset.x - size.x / 2f, offset.y + size.y / 2f, 0));

                    GL.Vertex(p1); GL.Vertex(p2);
                    GL.Vertex(p2); GL.Vertex(p3);
                    GL.Vertex(p3); GL.Vertex(p4);
                    GL.Vertex(p4); GL.Vertex(p1);
                }
                else if (col is PolygonCollider2D poly)
                {
                    for (int pathIdx = 0; pathIdx < poly.pathCount; pathIdx++)
                    {
                        Vector2[] pts = poly.GetPath(pathIdx);
                        for (int i = 0; i < pts.Length; i++)
                        {
                            Vector3 wp1 = zone.transform.TransformPoint(pts[i]);
                            Vector3 wp2 = zone.transform.TransformPoint(pts[(i + 1) % pts.Length]);
                            GL.Vertex(wp1);
                            GL.Vertex(wp2);
                        }
                    }
                }
            }

            GL.End();
            GL.PopMatrix();
        }

        void OnGUI()
        {
            // サンドボックス操作・計測パネル
            float panelWidth = 440;
            float panelHeight = 660;
            GUILayout.BeginArea(new Rect(20, 20, panelWidth, panelHeight), "【🌍 地形効果 & 脚ユニット テストベンチ】", GUI.skin.window);
            GUILayout.Space(6);

            // 1. マップ切り替え
            GUILayout.Label("🗺️ <b>1. 地形マップ選択 (F5〜F8 / 1〜4キー)</b>");
            var mapMgr = TerrainMapManager.Instance;
            if (mapMgr != null)
            {
                GUILayout.Label($"現在: <b><color=#00FF88>{mapMgr.GetMapDisplayName(mapMgr.currentMapType)}</color></b>");
                GUILayout.Label($"<color=#AAAAAA><size=10>{mapMgr.GetMapDescription(mapMgr.currentMapType)}</size></color>");

                GUILayout.BeginHorizontal();
                GUI.backgroundColor = mapMgr.currentMapType == MapType.Forest ? Color.green : Color.white;
                if (GUILayout.Button("🌲 1.森", GUILayout.Height(28))) mapMgr.SwitchMap(MapType.Forest);

                GUI.backgroundColor = mapMgr.currentMapType == MapType.Snow ? Color.cyan : Color.white;
                if (GUILayout.Button("❄️ 2.雪山", GUILayout.Height(28))) mapMgr.SwitchMap(MapType.Snow);

                GUI.backgroundColor = mapMgr.currentMapType == MapType.City ? Color.yellow : Color.white;
                if (GUILayout.Button("🏙️ 3.市街地", GUILayout.Height(28))) mapMgr.SwitchMap(MapType.City);

                GUI.backgroundColor = mapMgr.currentMapType == MapType.Volcano ? new Color(1f, 0.4f, 0.2f) : Color.white;
                if (GUILayout.Button("🌋 4.火山", GUILayout.Height(28))) mapMgr.SwitchMap(MapType.Volcano);
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);

            // 2. 脚ユニット選択
            GUILayout.Label("🦿 <b>2. 脚ユニット選択 (Q / E キー)</b>");
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = currentLegType == LegDeviceType.Omni ? Color.cyan : Color.white;
            if (GUILayout.Button("1. Omni (オムニ)\n<size=9>全方向移動 / 泥に弱い</size>", GUILayout.Height(36))) SetupLeg(LegDeviceType.Omni);

            GUI.backgroundColor = currentLegType == LegDeviceType.Tire ? Color.yellow : Color.white;
            if (GUILayout.Button("2. Tire (タイヤ)\n<size=9>平地最速125% / ドリフト</size>", GUILayout.Height(36))) SetupLeg(LegDeviceType.Tire);

            GUI.backgroundColor = currentLegType == LegDeviceType.Crawler ? Color.green : Color.white;
            if (GUILayout.Button("3. Crawler (履帯)\n<size=9>悪路走破100% / 溶岩80%軽減</size>", GUILayout.Height(36))) SetupLeg(LegDeviceType.Crawler);
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // 3. 現在のテレメトリ & 地形効果リアルタイム計測
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("📊 <b>【走行テレメトリ & 地形判定】</b>");

            TerrainType currentT = TerrainManager.Instance != null ? TerrainManager.Instance.GetTerrainAt(playerRobotTransform.position) : TerrainType.Normal;
            string tColor = currentT switch
            {
                TerrainType.Forest => "#00FF88",
                TerrainType.Mud => "#FFAA00",
                TerrainType.Ice => "#00FFFF",
                TerrainType.Lava => "#FF4444",
                _ => "#FFFFFF"
            };

            float speedMul = currentLegConfig != null ? currentLegConfig.GetSpeedMultiplier(currentT) : 1.0f;
            float slipVal = (currentT == TerrainType.Ice && currentLegConfig != null) ? currentLegConfig.iceSlipFactor : 0.0f;
            float dmgRed = currentLegConfig != null ? currentLegConfig.lavaDamageReduction : 0.0f;

            GUILayout.Label($"  現在地 地形: <color={tColor}><b>{currentT}</b></color> (Pos: {playerRobotTransform.position.x:F1}, {playerRobotTransform.position.y:F1})");
            GUILayout.Label($"  実効移動速度: <b><color=#00FF88>{currentActualSpeed:F2} m/s</color></b> (補正比率: <b>{speedMul:P0}</b>)");
            GUILayout.Label($"  氷上スリップ係数: <b>{(slipVal > 0 ? $"<color=cyan>{slipVal:F2} (スリップ中)</color>" : "<color=gray>0.00 (グリップ)</color>")}</b>");
            GUILayout.Label($"  溶岩ダメージ耐性: <b>{(dmgRed > 0 ? $"<color=green>{dmgRed:P0} カット</color>" : "<color=red>軽減なし (毎秒25ダメ)</color>")}</b>");
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // 4. HP & バトルステータス
            if (playerHealth != null)
            {
                string stunTxt = playerHealth.IsStunned ? $"<color=red>STUNNED! ({playerHealth.StunRemaining:F1}s)</color>" : "<color=green>NORMAL</color>";
                string invTxt = playerHealth.IsInvincible ? $"<color=cyan>INVINCIBLE ({playerHealth.InvincibleRemaining:F1}s)</color>" : "<color=gray>None</color>";
                GUILayout.Label($"❤️ <b>HP:</b> {playerHealth.currentHp:F0} / {playerHealth.maxHp:F0} | {stunTxt} | {invTxt}");
            }

            // 5. ポリゴン可視化トグル & リセット
            GUILayout.Space(6);
            showColliderOverlay = GUILayout.Toggle(showColliderOverlay, " 📐 <b>当たり判定ポリゴン枠線を表示 (Collider Overlay)</b>");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 位置 & HP全回復 (Rキー)", GUILayout.Height(28)))
            {
                ResetPositionAndHp();
            }
            if (GUILayout.Button("⚡ スタン検証", GUILayout.Height(28)))
            {
                playerHealth?.TriggerStun();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("<color=#888888><size=10>操作: WASD(移動) / マウス(旋回・発射) / Q,E(脚切替) / F5~F8(マップ切替) / R(リセット)</size></color>");

            GUILayout.EndArea();
        }
    }
}
