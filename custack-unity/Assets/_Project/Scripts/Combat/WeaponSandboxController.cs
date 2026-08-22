using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Custack.Equipment;
using Custack.Robot;
using Custack.Terrain;

namespace Custack.Combat
{
    /// <summary>
    /// 実機・共有メモリ不要で、武器エフェクトおよび4つの地形マップ（森・雪山・市街地・火山）の
    /// 地形効果（減速・スリップ・溶岩ダメージ・スタン）を単体で視覚確認・テストするためのサンドボックスコントローラー。
    /// </summary>
    public class WeaponSandboxController : MonoBehaviour
    {
        [Header("プレイヤーテスト機体")]
        public Transform playerRobotTransform;
        public float moveSpeed = 5.0f;
        public float rotateSpeed = 360f;

        [Header("現在装備中の武器 & 脚ユニット")]
        public ArmDeviceType currentWeaponType = ArmDeviceType.Gatling;
        public LegDeviceType currentLegType = LegDeviceType.Omni;
        private WeaponBase activeWeapon;
        private ArmWeaponConfig currentConfig;
        private LegMovementConfig currentLegConfig;
        private Health playerHealth;

        [Header("ダミーターゲット")]
        public List<Health> dummyTargets = new List<Health>();

        private Camera mainCam;
        private GameObject playerWreckageSmokeObj;

        void Awake()
        {
            mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
        }

        void Start()
        {
            if (playerRobotTransform == null)
            {
                playerRobotTransform = transform;
            }

            playerHealth = playerRobotTransform.GetComponent<Health>();
            if (playerHealth == null) playerHealth = playerRobotTransform.gameObject.AddComponent<Health>();

            currentLegConfig = LegMovementConfig.CreateDefault(currentLegType);

            SetupWeapon(currentWeaponType);
            SetupDummyTargets();
        }

        public void SetupWeapon(ArmDeviceType type)
        {
            currentWeaponType = type;
            currentConfig = ArmWeaponConfig.CreateDefault(type);

            if (activeWeapon != null)
            {
                Destroy(activeWeapon);
            }

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
                activeWeapon.Initialize(0, true, currentConfig, playerRobotTransform);
            }
        }

        public void SetupLeg(LegDeviceType type)
        {
            currentLegType = type;
            currentLegConfig = LegMovementConfig.CreateDefault(type);
        }

        private void SetupDummyTargets()
        {
            foreach (var dummy in dummyTargets)
            {
                if (dummy != null)
                {
                    dummy.OnDeath += () =>
                    {
                        EffectFactory.PlayRobotDestructionExplosion(dummy.transform.position, Color.red);
                        Invoke(nameof(ResetAllDummies), 2.0f);
                    };
                }
            }

            if (playerHealth != null)
            {
                playerHealth.OnDeath += () =>
                {
                    EffectFactory.PlayRobotDestructionExplosion(playerRobotTransform.position, new Color(0.2f, 0.8f, 1f));
                    if (playerWreckageSmokeObj == null)
                    {
                        playerWreckageSmokeObj = EffectFactory.AttachWreckageSmokeAndSparks(playerRobotTransform);
                    }
                };

                playerHealth.OnRespawn += () =>
                {
                    if (playerWreckageSmokeObj != null)
                    {
                        Destroy(playerWreckageSmokeObj);
                        playerWreckageSmokeObj = null;
                    }
                };
            }
        }

        public void ResetAllDummies()
        {
            if (playerHealth != null)
            {
                playerHealth.Respawn(1000f);
            }

            foreach (var dummy in dummyTargets)
            {
                if (dummy != null)
                {
                    dummy.Respawn(1000f);
                }
            }
        }

        void Update()
        {
            HandlePlayerMovement();
            HandleWeaponInput();
            HandleKeyboardWeaponSwitch();
            HandleTerrainDamage();
        }

        private void HandlePlayerMovement()
        {
            if (playerRobotTransform == null) return;

            // HP 0 (撃破) または スタン中は移動・旋回を完全停止
            if (playerHealth != null && (playerHealth.IsDead || playerHealth.IsStunned))
            {
                return;
            }

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // 1. 移動 (WASD / 矢印キー) - Input System
            Vector2 rawMove = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) rawMove.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) rawMove.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) rawMove.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) rawMove.x += 1f;
            }

            if (rawMove.sqrMagnitude > 0.001f)
            {
                rawMove.Normalize();

                // 地形マネージャーによる移動補正 (森50%減速、泥30%減速等)
                float speedMultiplier = 1.0f;
                if (TerrainManager.Instance != null && currentLegConfig != null)
                {
                    TerrainType t = TerrainManager.Instance.GetTerrainAt(playerRobotTransform.position);
                    speedMultiplier = currentLegConfig.GetSpeedMultiplier(t);
                }

                playerRobotTransform.position += (Vector3)(rawMove * (moveSpeed * speedMultiplier * Time.deltaTime));
            }

            // 2. マウスカーソル方向へ旋回 - Input System
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
                    float dmg = 25f * Time.deltaTime; // 溶岩/ハザード: 毎秒25ダメージ
                    if (currentLegConfig != null)
                    {
                        dmg *= (1.0f - currentLegConfig.lavaDamageReduction);
                    }
                    playerHealth.TakeDamage(dmg, playerRobotTransform.position);
                }
            }
        }

        private void HandleWeaponInput()
        {
            if (activeWeapon == null || currentConfig == null) return;

            // HP 0 (撃破) または スタン中は武器発射を完全遮断
            if (playerHealth != null && (playerHealth.IsDead || playerHealth.IsStunned))
            {
                return;
            }

            var kb = Keyboard.current;
            var mouse = Mouse.current;

            bool isFireInput = false;
            if (currentConfig.isAutomatic)
            {
                bool mouseFire = mouse != null && mouse.leftButton.isPressed;
                bool spaceFire = kb != null && kb.spaceKey.isPressed;
                bool jFire = kb != null && kb.jKey.isPressed;
                isFireInput = mouseFire || spaceFire || jFire;
            }
            else
            {
                bool mouseFire = mouse != null && mouse.leftButton.wasPressedThisFrame;
                bool spaceFire = kb != null && kb.spaceKey.wasPressedThisFrame;
                bool jFire = kb != null && kb.jKey.wasPressedThisFrame;
                isFireInput = mouseFire || spaceFire || jFire;
            }

            if (isFireInput)
            {
                Vector2 forwardDir = playerRobotTransform.right; // 画面から見て時計回りに90度回転
                activeWeapon.TryFire(forwardDir, null);
            }
        }

        private void HandleKeyboardWeaponSwitch()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // マップ切り替え (F5〜F8)
            if (TerrainMapManager.Instance != null)
            {
                if (kb.f5Key.wasPressedThisFrame) TerrainMapManager.Instance.SwitchMap(MapType.Forest);
                if (kb.f6Key.wasPressedThisFrame) TerrainMapManager.Instance.SwitchMap(MapType.Snow);
                if (kb.f7Key.wasPressedThisFrame) TerrainMapManager.Instance.SwitchMap(MapType.City);
                if (kb.f8Key.wasPressedThisFrame) TerrainMapManager.Instance.SwitchMap(MapType.Volcano);
            }

            // HP 0 (撃破) の場合は武器切り替えも不可 (Rキーでのリスポーンのみ許可)
            if (playerHealth != null && playerHealth.IsDead)
            {
                if (kb.rKey.wasPressedThisFrame) ResetAllDummies();
                return;
            }

            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) SetupWeapon(ArmDeviceType.Gatling);
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) SetupWeapon(ArmDeviceType.Sword);
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) SetupWeapon(ArmDeviceType.Cannon);
            if (kb.rKey.wasPressedThisFrame) ResetAllDummies();
        }

        void OnGUI()
        {
            // サンドボックス操作パネル
            GUILayout.BeginArea(new Rect(20, 20, 420, 600), "【バトル・地形マップ テストツール】", GUI.skin.window);
            GUILayout.Space(6);

            // 1. マップ切り替えセクション
            GUILayout.Label("🗺️ <b>地形マップ切替 (F5〜F8)</b>");
            var mapMgr = TerrainMapManager.Instance;
            if (mapMgr != null)
            {
                GUILayout.Label($"現在: <b><color=#00FF88>{mapMgr.GetMapDisplayName(mapMgr.currentMapType)}</color></b>");
                GUILayout.Label($"<color=#AAAAAA><size=10>{mapMgr.GetMapDescription(mapMgr.currentMapType)}</size></color>");

                GUILayout.BeginHorizontal();
                GUI.backgroundColor = mapMgr.currentMapType == MapType.Forest ? Color.green : Color.white;
                if (GUILayout.Button("🌲 森 (F5)", GUILayout.Height(26))) mapMgr.SwitchMap(MapType.Forest);

                GUI.backgroundColor = mapMgr.currentMapType == MapType.Snow ? Color.cyan : Color.white;
                if (GUILayout.Button("❄️ 雪山 (F6)", GUILayout.Height(26))) mapMgr.SwitchMap(MapType.Snow);

                GUI.backgroundColor = mapMgr.currentMapType == MapType.City ? Color.yellow : Color.white;
                if (GUILayout.Button("🏙️ 市街地 (F7)", GUILayout.Height(26))) mapMgr.SwitchMap(MapType.City);

                GUI.backgroundColor = mapMgr.currentMapType == MapType.Volcano ? new Color(1f, 0.4f, 0.2f) : Color.white;
                if (GUILayout.Button("🌋 火山 (F8)", GUILayout.Height(26))) mapMgr.SwitchMap(MapType.Volcano);
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);

            // 2. 現在地の地形ステータス & 脚ユニット切替
            TerrainType currentT = TerrainManager.Instance != null ? TerrainManager.Instance.GetTerrainAt(playerRobotTransform.position) : TerrainType.Normal;
            string tColor = currentT switch
            {
                TerrainType.Forest => "#00FF88",
                TerrainType.Mud => "#FFAA00",
                TerrainType.Ice => "#00FFFF",
                TerrainType.Lava => "#FF4444",
                _ => "#FFFFFF"
            };
            GUILayout.Label($"📍 <b>現在地の地形:</b> <color={tColor}><b>{currentT}</b></color> (速度倍率: {currentLegConfig?.GetSpeedMultiplier(currentT):P0})");

            GUILayout.BeginHorizontal();
            GUILayout.Label("脚切替:", GUILayout.Width(50));
            if (GUILayout.Button("Omni (標準)", GUILayout.Height(22))) SetupLeg(LegDeviceType.Omni);
            if (GUILayout.Button("Tire (最速)", GUILayout.Height(22))) SetupLeg(LegDeviceType.Tire);
            if (GUILayout.Button("Crawler (悪路走破)", GUILayout.Height(22))) SetupLeg(LegDeviceType.Crawler);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // 3. 武器切り替え
            GUILayout.Label($"⚔️ <b>武器:</b> <color=yellow>{currentConfig?.weaponName}</color> (威力:{currentConfig?.damage} 弾速:{currentConfig?.projectileSpeed})");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1. ガトリング", GUILayout.Height(26))) SetupWeapon(ArmDeviceType.Gatling);
            if (GUILayout.Button("2. ソード", GUILayout.Height(26))) SetupWeapon(ArmDeviceType.Sword);
            if (GUILayout.Button("3. キャノン", GUILayout.Height(26))) SetupWeapon(ArmDeviceType.Cannon);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // 4. スタン & HP ステータス
            if (playerHealth != null)
            {
                string stunStatus = playerHealth.IsStunned ? $"<color=red>STUNNED! ({playerHealth.StunRemaining:F1}s)</color>" : "<color=green>NORMAL</color>";
                string invStatus = playerHealth.IsInvincible ? $"<color=cyan>INVINCIBLE ({playerHealth.InvincibleRemaining:F1}s)</color>" : "<color=gray>None</color>";
                GUILayout.Label($"❤️ <b>HP:</b> {playerHealth.currentHp:F0} / {playerHealth.maxHp:F0} | 状態: {stunStatus} | {invStatus}");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("⚡ 自機スタン検証")) playerHealth?.TriggerStun();
            if (GUILayout.Button("💥 ダミー200ダメ"))
            {
                foreach (var dummy in dummyTargets) if (dummy != null) dummy.TakeDamage(200f, dummy.transform.position);
            }
            if (GUILayout.Button("💀 自機HP0撃破")) playerHealth?.TakeDamage(1000f);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            if (GUILayout.Button("🔄 全機体リスポーン & HP全回復 (Rキー)", GUILayout.Height(26)))
            {
                ResetAllDummies();
            }

            GUILayout.EndArea();
        }
    }
}
