using UnityEngine;
using Custack.Combat;
using Custack.Robot;
using Custack.UI;

namespace Custack.Core
{
    /// <summary>
    /// 対戦の進行管理、勝敗判定、リセットを処理するゲームマネージャー。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("プレイヤー参照")]
        public RobotEntity player1;
        public RobotEntity player2;

        [Header("対戦状態")]
        public bool isGameOver = false;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            InitializeGame();
        }

        public void InitializeGame()
        {
            isGameOver = false;
            if (BattleHUD.Instance != null)
            {
                BattleHUD.Instance.HideWinner();
            }

            if (player1 != null && player1.HealthComponent != null)
            {
                player1.HealthComponent.OnDeath += OnPlayer1Death;
                player1.HealthComponent.OnHealthChanged += (cur, max) => BattleHUD.Instance?.UpdatePlayerHp(0, cur, max);
                UpdateEquipmentUI(0, player1);
            }

            if (player2 != null && player2.HealthComponent != null)
            {
                player2.HealthComponent.OnDeath += OnPlayer2Death;
                player2.HealthComponent.OnHealthChanged += (cur, max) => BattleHUD.Instance?.UpdatePlayerHp(1, cur, max);
                UpdateEquipmentUI(1, player2);
            }
        }

        private void UpdateEquipmentUI(int index, RobotEntity robot)
        {
            if (robot == null || robot.EquipmentComponent == null || BattleHUD.Instance == null) return;
            var eq = robot.EquipmentComponent;
            BattleHUD.Instance.UpdatePlayerEquipment(index, eq.CurrentLegConfig.legName, eq.CurrentRightArmConfig.weaponName, eq.CurrentLeftArmConfig.weaponName);
        }

        private void OnPlayer1Death()
        {
            if (isGameOver) return;
            isGameOver = true;
            Debug.Log("[GameManager] Player 2 Wins!");
            BattleHUD.Instance?.ShowWinner("Player 2");
        }

        private void OnPlayer2Death()
        {
            if (isGameOver) return;
            isGameOver = true;
            Debug.Log("[GameManager] Player 1 Wins!");
            BattleHUD.Instance?.ShowWinner("Player 1");
        }

        /// <summary>
        /// ゲームのリセット / 再戦
        /// </summary>
        public void RestartGame()
        {
            isGameOver = false;
            BattleHUD.Instance?.HideWinner();

            if (player1 != null && player1.HealthComponent != null) player1.HealthComponent.Respawn();
            if (player2 != null && player2.HealthComponent != null) player2.HealthComponent.Respawn();

            // RobotManager の全管理機体をリスポーン
            if (RobotManager.Instance != null && RobotManager.Instance.robots != null)
            {
                foreach (var robot in RobotManager.Instance.robots)
                {
                    if (robot != null && robot.HealthComponent != null)
                    {
                        robot.HealthComponent.Respawn();
                    }
                }
            }

            Debug.Log("<color=#00FF88>[GameManager]</color> 🔄 ゲームを再戦リセットしました！ (全機体 HP 1000 回復)");
        }

        void Update()
        {
            if (!isGameOver) return;

            // Rキー または ゲームパッドの △ / Y ボタンでリスタート
            bool keyboardRestart = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame;
            bool gamepadRestart = false;

            if (UnityEngine.InputSystem.Gamepad.current != null)
            {
                gamepadRestart = UnityEngine.InputSystem.Gamepad.current.buttonNorth.wasPressedThisFrame || UnityEngine.InputSystem.Gamepad.current.startButton.wasPressedThisFrame;
            }

            if (keyboardRestart || gamepadRestart)
            {
                RestartGame();
            }
        }
    }
}
