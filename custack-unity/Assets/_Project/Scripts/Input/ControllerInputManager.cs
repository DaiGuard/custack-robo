using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Custack.Input
{
    /// <summary>
    /// 最大2台の PS5 / Gamepad コントローラーを個別検知・管理し、
    /// 各プレイヤーに割り当てて入力を提供するマネージャー。
    /// （コントローラーがない場合はキーボード操作もサポート）
    /// </summary>
    public class ControllerInputManager : MonoBehaviour
    {
        public static ControllerInputManager Instance { get; private set; }

        [Header("スティック感度 & デッドゾーン")]
        [Range(0.01f, 0.5f)]
        public float deadzone = 0.15f;

        [Header("キーボードフォールバック設定")]
        public bool enableKeyboardFallback = true;

        [Header("デバッグモニター")]
        [SerializeField]
        private PlayerInputCommand[] playerInputs = new PlayerInputCommand[2];

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Update()
        {
            UpdateInputs();
        }

        private void UpdateInputs()
        {
            var gamepads = Gamepad.all;

            // Player 1 (Index 0) の入力更新
            playerInputs[0] = ReadGamepadInput(0, gamepads);
            // Player 2 (Index 1) の入力更新
            playerInputs[1] = ReadGamepadInput(1, gamepads);

            // キーボードフォールバック
            if (enableKeyboardFallback)
            {
                ApplyKeyboardFallback();
            }
        }

        private PlayerInputCommand ReadGamepadInput(int playerIndex, IReadOnlyList<Gamepad> gamepads)
        {
            PlayerInputCommand cmd = default;

            if (playerIndex < gamepads.Count && gamepads[playerIndex] != null)
            {
                var pad = gamepads[playerIndex];
                cmd.IsConnected = true;

                // 左スティック: 移動 (Vx, Vy)
                Vector2 rawMove = pad.leftStick.ReadValue();
                cmd.Move = ApplyDeadzone(rawMove);

                // 右スティック: 旋回 (Omega)
                Vector2 rawLook = pad.rightStick.ReadValue();
                float omega = rawLook.x;
                // L2/R2 による旋回補助 (L2: 左旋回, R2: 右旋回)
                if (pad.leftTrigger.isPressed || pad.rightTrigger.isPressed)
                {
                    omega += pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                }
                cmd.Omega = Mathf.Abs(omega) > deadzone ? Mathf.Clamp(omega, -1f, 1f) : 0f;

                // 右武器 (R1 / rightShoulder または 〇 / buttonEast)
                cmd.ArmRightPressed = pad.rightShoulder.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame;
                cmd.ArmRightHeld = pad.rightShoulder.isPressed || pad.buttonEast.isPressed;

                // 左武器 (L1 / leftShoulder または □ / buttonWest)
                cmd.ArmLeftPressed = pad.leftShoulder.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame;
                cmd.ArmLeftHeld = pad.leftShoulder.isPressed || pad.buttonWest.isPressed;

                // ホーミングターゲット切り替え: △ボタン (buttonNorth / Triangle)
                cmd.TargetSwitchPressed = pad.buttonNorth.wasPressedThisFrame;
            }
            else
            {
                cmd.IsConnected = false;
            }

            return cmd;
        }

        private void ApplyKeyboardFallback()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // コントローラー1が未接続の場合、WASD + J/K/U を Player 1 に割り当て
            if (!playerInputs[0].IsConnected)
            {
                Vector2 move = Vector2.zero;
                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                playerInputs[0].Move = move.normalized;

                float omega = 0f;
                if (kb.qKey.isPressed) omega -= 1f;
                if (kb.eKey.isPressed) omega += 1f;
                playerInputs[0].Omega = omega;

                playerInputs[0].ArmRightPressed = kb.jKey.wasPressedThisFrame;
                playerInputs[0].ArmRightHeld = kb.jKey.isPressed;
                playerInputs[0].ArmLeftPressed = kb.kKey.wasPressedThisFrame;
                playerInputs[0].ArmLeftHeld = kb.kKey.isPressed;
                playerInputs[0].TargetSwitchPressed = kb.uKey.wasPressedThisFrame; // △相当
                playerInputs[0].IsConnected = true;
            }

            // コントローラー2が未接続の場合、矢印キー + テンキー 1/2/5 を Player 2 に割り当て
            if (!playerInputs[1].IsConnected)
            {
                Vector2 move = Vector2.zero;
                if (kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.leftArrowKey.isPressed) move.x -= 1f;
                if (kb.rightArrowKey.isPressed) move.x += 1f;
                playerInputs[1].Move = move.normalized;

                float omega = 0f;
                if (kb.commaKey.isPressed) omega -= 1f;
                if (kb.periodKey.isPressed) omega += 1f;
                playerInputs[1].Omega = omega;

                playerInputs[1].ArmRightPressed = kb.numpad1Key.wasPressedThisFrame || kb.lKey.wasPressedThisFrame;
                playerInputs[1].ArmRightHeld = kb.numpad1Key.isPressed || kb.lKey.isPressed;
                playerInputs[1].ArmLeftPressed = kb.numpad2Key.wasPressedThisFrame || kb.semicolonKey.wasPressedThisFrame;
                playerInputs[1].ArmLeftHeld = kb.numpad2Key.isPressed || kb.semicolonKey.isPressed;
                playerInputs[1].TargetSwitchPressed = kb.numpad5Key.wasPressedThisFrame || kb.pKey.wasPressedThisFrame;
                playerInputs[1].IsConnected = true;
            }
        }

        private Vector2 ApplyDeadzone(Vector2 input)
        {
            if (input.magnitude < deadzone) return Vector2.zero;
            return input.normalized * ((input.magnitude - deadzone) / (1f - deadzone));
        }

        public PlayerInputCommand GetPlayerInput(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < playerInputs.Length)
            {
                return playerInputs[playerIndex];
            }
            return default;
        }
    }
}
