using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Custack.Equipment;
using Custack.Robot;

namespace Custack.Combat
{
    /// <summary>
    /// 実機・共有メモリ不要で、武器エフェクトおよびダメージ計算・スタン・無敵時間・移動停止を
    /// 単体で視覚確認・テストするためのサンドボックスコントローラー。
    /// </summary>
    public class WeaponSandboxController : MonoBehaviour
    {
        [Header("プレイヤーテスト機体")]
        public Transform playerRobotTransform;
        public float moveSpeed = 5.0f;
        public float rotateSpeed = 360f;

        [Header("現在装備中の武器")]
        public ArmDeviceType currentWeaponType = ArmDeviceType.Gatling;
        private WeaponBase activeWeapon;
        private ArmWeaponConfig currentConfig;
        private Health playerHealth;

        [Header("ダミーターゲット")]
        public List<Health> dummyTargets = new List<Health>();

        private Camera mainCam;

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

        private GameObject playerWreckageSmokeObj;

        private void SetupDummyTargets()
        {
            foreach (var dummy in dummyTargets)
            {
                if (dummy != null)
                {
                    dummy.OnDeath += () =>
                    {
                        // 撃破時に大爆発エフェクトを再生し、2.0秒後にリスポーン
                        EffectFactory.PlayRobotDestructionExplosion(dummy.transform.position, Color.red);
                        Invoke(nameof(ResetAllDummies), 2.0f);
                    };
                }
            }

            if (playerHealth != null)
            {
                playerHealth.OnDeath += () =>
                {
                    // 自機撃破時: 大爆発 + 黒煙アタッチ
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
            Vector2 move = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
            }

            if (move.sqrMagnitude > 0.001f)
            {
                move.Normalize();
                playerRobotTransform.position += (Vector3)(move * (moveSpeed * Time.deltaTime));
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
                Vector2 forwardDir = playerRobotTransform.up;
                activeWeapon.TryFire(forwardDir, null);
            }
        }

        private void HandleKeyboardWeaponSwitch()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

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
            GUILayout.BeginArea(new Rect(20, 20, 380, 520), "【バトル・エフェクト テストツール】", GUI.skin.window);
            GUILayout.Space(8);

            GUILayout.Label("🎮 <b>操作方法</b>");
            GUILayout.Label("・移動: <b>WASD / 矢印キー</b>");
            GUILayout.Label("・照準: <b>マウスカーソル</b>");
            GUILayout.Label("・発射: <b>左クリック / Space / J</b>");
            GUILayout.Label("・切替: <b>[1] ガトリング / [2] ソード / [3] キャノン</b>");
            GUILayout.Space(8);

            GUILayout.Label($"<b>現在の武器:</b> <color=yellow>{currentConfig?.weaponName ?? "None"}</color>");
            GUILayout.Label($"<b>基礎威力:</b> {currentConfig?.damage ?? 0} (ガトリング8/ソード40/キャノン30) | <b>弾速:</b> {currentConfig?.projectileSpeed ?? 0}");
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1. ガトリング", GUILayout.Height(30))) SetupWeapon(ArmDeviceType.Gatling);
            if (GUILayout.Button("2. ソード", GUILayout.Height(30))) SetupWeapon(ArmDeviceType.Sword);
            if (GUILayout.Button("3. キャノン", GUILayout.Height(30))) SetupWeapon(ArmDeviceType.Cannon);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("⚡ <b>スタン & 無敵時間ステータス</b>");
            if (playerHealth != null)
            {
                string stunStatus = playerHealth.IsStunned ? $"<color=red>STUNNED! (残 {playerHealth.StunRemaining:F1}s)</color>" : "<color=green>NORMAL</color>";
                string invStatus = playerHealth.IsInvincible ? $"<color=cyan>INVINCIBLE (残 {playerHealth.InvincibleRemaining:F1}s)</color>" : "<color=gray>None</color>";
                GUILayout.Label($"プレイヤーHP: <b>{playerHealth.currentHp:F0} / {playerHealth.maxHp:F0}</b>");
                GUILayout.Label($"状態: {stunStatus} | {invStatus}");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("⚡ 自機スタン検証 (1.5秒停止+無敵)"))
            {
                playerHealth?.TriggerStun();
            }
            if (GUILayout.Button("💥 ダミーへ100ダメ (スタン)"))
            {
                foreach (var dummy in dummyTargets)
                {
                    if (dummy != null) dummy.TakeDamage(100f, dummy.transform.position);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("💀 自機HP 0 撃破テスト (大破爆発+黒煙+操作不能)"))
            {
                playerHealth?.TakeDamage(1000f);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("✨ <b>エフェクト直接プレビュー</b>");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("火花スパーク")) EffectFactory.PlayHitSparks(playerRobotTransform.position + playerRobotTransform.up * 1.5f, playerRobotTransform.up, Color.yellow);
            if (GUILayout.Button("切断光")) EffectFactory.PlaySlashImpact(playerRobotTransform.position + playerRobotTransform.up * 1.5f, playerRobotTransform.up, new Color(0.2f, 1f, 0.4f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("大爆発 (青白)")) EffectFactory.PlayLaserImpact(playerRobotTransform.position + playerRobotTransform.up * 2f, new Color(0.3f, 0.7f, 1f));
            if (GUILayout.Button("衝撃波リング")) EffectFactory.CreateShockwave(playerRobotTransform.position, Color.cyan, 0.2f, 2.0f, 0.35f);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            if (GUILayout.Button("🔄 ダミーターゲット全リセット (Rキー)", GUILayout.Height(28)))
            {
                ResetAllDummies();
            }

            GUILayout.EndArea();
        }
    }
}
