using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Custack.Input
{
    /// <summary>
    /// 各ロボットに割り当てる入力ソース種別
    /// </summary>
    public enum InputSourceType
    {
        None = 0,
        Gamepad0 = 1,     // 1台目のゲームパッド (Tag ID 1 -> /dev/custack_bridge_1)
        Gamepad1 = 2,     // 2台目のゲームパッド (Tag ID 2 -> /dev/custack_bridge_2)
        Gamepad2 = 3,     // 3台目のゲームパッド (Tag ID 3 -> /dev/custack_bridge_3)
        KeyboardP1 = 10,  // WASD + J/K/U
        KeyboardP2 = 11,  // 矢印キー + Numpad1/2/5 / L/;/P
        KeyboardP3 = 12,  // IJKL + 7/8/9
    }

    /// <summary>
    /// ロボット ID とゲームパッド入力の割り当て設定
    /// </summary>
    [Serializable]
    public class RobotGamepadMapping
    {
        [Tooltip("操作対象のロボットID (AprilTag ID: 1~3)")]
        public int robotId;

        [Tooltip("割り当てる入力ソース (Gamepad0~2, KeyboardP1~P3, None)")]
        public InputSourceType inputSource;

        public RobotGamepadMapping(int id, InputSourceType source)
        {
            robotId = id;
            inputSource = source;
        }
    }

    /// <summary>
    /// 最大3台のゲームパッド・キーボード入力を管理し、
    /// AprilTag ID 1〜3 (/dev/custack_bridge_1〜3) へ専用割り当てを行う入力マネージャー。
    /// </summary>
    public class ControllerInputManager : MonoBehaviour
    {
        public static ControllerInputManager Instance { get; private set; }

        [Header("スティック感度 & デッドゾーン")]
        [Range(0.01f, 0.5f)]
        public float deadzone = 0.15f;

        [Header("キーボードフォールバック設定")]
        [Tooltip("ゲームパッドが未接続の場合にキーボードで操作を代替する")]
        public bool enableKeyboardFallback = true;

        [Header("ロボット・ゲームパッド割り当てマッピング")]
        [Tooltip("各ロボットID (AprilTag ID: 1~3) に対する入力ソース割り当てリスト")]
        public List<RobotGamepadMapping> robotMappings = new List<RobotGamepadMapping>();

        [Header("デバッグ設定")]
        [Tooltip("画面上にコントローラー割り当て変更GUIを表示 (F2キーで切替)")]
        public bool showMappingOverlay = false;

        private PlayerInputCommand[] rawGamepadInputs = new PlayerInputCommand[3];
        private PlayerInputCommand keyboardP1Input;
        private PlayerInputCommand keyboardP2Input;
        private PlayerInputCommand keyboardP3Input;
        private Vector2 scrollPos;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureDefaultMappings();
        }

        public void ResetToDefaultMappings()
        {
            robotMappings = new List<RobotGamepadMapping>
            {
                new RobotGamepadMapping(0, InputSourceType.None),
                new RobotGamepadMapping(1, InputSourceType.Gamepad0), // Tag ID 1 -> Gamepad 0 (/dev/custack_bridge_1)
                new RobotGamepadMapping(2, InputSourceType.Gamepad1), // Tag ID 2 -> Gamepad 1 (/dev/custack_bridge_2)
                new RobotGamepadMapping(3, InputSourceType.Gamepad2), // Tag ID 3 -> Gamepad 2 (/dev/custack_bridge_3)
            };

            for (int i = 4; i < 16; i++)
            {
                robotMappings.Add(new RobotGamepadMapping(i, InputSourceType.None));
            }
            Debug.Log("<color=#00FF88>[ControllerInputManager]</color> ✅ ロボットマッピングをデフォルト(Tag 1:Pad0->Bridge1, Tag 2:Pad1->Bridge2, Tag 3:Pad2->Bridge3)にリセットしました。");
        }

        public void EnsureDefaultMappings()
        {
            if (robotMappings == null || robotMappings.Count < 3)
            {
                ResetToDefaultMappings();
            }
        }

        void Update()
        {
            // F2 キーでコントローラー割り当てオーバーレイ表示切替
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f2Key.wasPressedThisFrame)
            {
                showMappingOverlay = !showMappingOverlay;
            }

            UpdateRawInputs();
        }

        private void UpdateRawInputs()
        {
            var gamepads = Gamepad.all;

            // 1. 各ゲームパッドの入力を取得 (最大3台)
            for (int i = 0; i < 3; i++)
            {
                rawGamepadInputs[i] = ReadGamepadInput(i, gamepads);
            }

            // 2. キーボード入力を取得
            ReadKeyboardInputs();
        }

        private PlayerInputCommand ReadGamepadInput(int padIndex, IReadOnlyList<Gamepad> gamepads)
        {
            PlayerInputCommand cmd = default;

            if (padIndex < gamepads.Count && gamepads[padIndex] != null)
            {
                var pad = gamepads[padIndex];
                cmd.IsConnected = true;

                // 左スティック: 移動 (Vx, Vy)
                Vector2 rawMove = pad.leftStick.ReadValue();
                cmd.Move = ApplyDeadzone(rawMove);

                // 右スティック: 旋回 (Omega)
                Vector2 rawLook = pad.rightStick.ReadValue();
                float omega = rawLook.x;
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

        private void ReadKeyboardInputs()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Keyboard P1 (WASD + J/K/U) -> Tag ID 1
            {
                Vector2 move = Vector2.zero;
                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                keyboardP1Input.Move = move.normalized;

                float omega = 0f;
                if (kb.qKey.isPressed) omega -= 1f;
                if (kb.eKey.isPressed) omega += 1f;
                keyboardP1Input.Omega = omega;

                keyboardP1Input.ArmRightPressed = kb.jKey.wasPressedThisFrame;
                keyboardP1Input.ArmRightHeld = kb.jKey.isPressed;
                keyboardP1Input.ArmLeftPressed = kb.kKey.wasPressedThisFrame;
                keyboardP1Input.ArmLeftHeld = kb.kKey.isPressed;
                keyboardP1Input.TargetSwitchPressed = kb.uKey.wasPressedThisFrame;
                keyboardP1Input.IsConnected = true;
            }

            // Keyboard P2 (矢印 + Numpad1/2/5 または L/;/P) -> Tag ID 2
            {
                Vector2 move = Vector2.zero;
                if (kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.leftArrowKey.isPressed) move.x -= 1f;
                if (kb.rightArrowKey.isPressed) move.x += 1f;
                keyboardP2Input.Move = move.normalized;

                float omega = 0f;
                if (kb.commaKey.isPressed) omega -= 1f;
                if (kb.periodKey.isPressed) omega += 1f;
                keyboardP2Input.Omega = omega;

                keyboardP2Input.ArmRightPressed = kb.numpad1Key.wasPressedThisFrame || kb.lKey.wasPressedThisFrame;
                keyboardP2Input.ArmRightHeld = kb.numpad1Key.isPressed || kb.lKey.isPressed;
                keyboardP2Input.ArmLeftPressed = kb.numpad2Key.wasPressedThisFrame || kb.semicolonKey.wasPressedThisFrame;
                keyboardP2Input.ArmLeftHeld = kb.numpad2Key.isPressed || kb.semicolonKey.isPressed;
                keyboardP2Input.TargetSwitchPressed = kb.numpad5Key.wasPressedThisFrame || kb.pKey.wasPressedThisFrame;
                keyboardP2Input.IsConnected = true;
            }

            // Keyboard P3 (IJKL + 7/8/9) -> Tag ID 3
            {
                Vector2 move = Vector2.zero;
                if (kb.iKey.isPressed) move.y += 1f;
                if (kb.kKey.isPressed) move.y -= 1f;
                if (kb.jKey.isPressed) move.x -= 1f;
                if (kb.lKey.isPressed) move.x += 1f;
                keyboardP3Input.Move = move.normalized;

                float omega = 0f;
                if (kb.uKey.isPressed) omega -= 1f;
                if (kb.oKey.isPressed) omega += 1f;
                keyboardP3Input.Omega = omega;

                keyboardP3Input.ArmRightPressed = kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame;
                keyboardP3Input.ArmRightHeld = kb.digit7Key.isPressed || kb.numpad7Key.isPressed;
                keyboardP3Input.ArmLeftPressed = kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame;
                keyboardP3Input.ArmLeftHeld = kb.digit8Key.isPressed || kb.numpad8Key.isPressed;
                keyboardP3Input.TargetSwitchPressed = kb.digit9Key.wasPressedThisFrame || kb.numpad9Key.wasPressedThisFrame;
                keyboardP3Input.IsConnected = true;
            }
        }

        private Vector2 ApplyDeadzone(Vector2 input)
        {
            if (input.magnitude < deadzone) return Vector2.zero;
            return input.normalized * ((input.magnitude - deadzone) / (1f - deadzone));
        }

        /// <summary>
        /// 指定したロボットID (AprilTag ID) に割り当てられた入力を取得
        /// </summary>
        public PlayerInputCommand GetInputForRobot(int robotId)
        {
            InputSourceType src = GetRobotMapping(robotId);

            switch (src)
            {
                case InputSourceType.Gamepad0:
                    if (rawGamepadInputs[0].IsConnected) return rawGamepadInputs[0];
                    if (enableKeyboardFallback) return keyboardP1Input;
                    return default;

                case InputSourceType.Gamepad1:
                    if (rawGamepadInputs[1].IsConnected) return rawGamepadInputs[1];
                    if (enableKeyboardFallback) return keyboardP2Input;
                    return default;

                case InputSourceType.Gamepad2:
                    if (rawGamepadInputs[2].IsConnected) return rawGamepadInputs[2];
                    if (enableKeyboardFallback) return keyboardP3Input;
                    return default;

                case InputSourceType.KeyboardP1:
                    return keyboardP1Input;

                case InputSourceType.KeyboardP2:
                    return keyboardP2Input;

                case InputSourceType.KeyboardP3:
                    return keyboardP3Input;

                case InputSourceType.None:
                default:
                    return default;
            }
        }

        /// <summary>
        /// ロボットIDに対する現在の割り当て入力ソースを取得
        /// (Tag ID 1 -> Pad 0, Tag ID 2 -> Pad 1, Tag ID 3 -> Pad 2)
        /// </summary>
        public InputSourceType GetRobotMapping(int robotId)
        {
            for (int i = 0; i < robotMappings.Count; i++)
            {
                if (robotMappings[i].robotId == robotId)
                {
                    return robotMappings[i].inputSource;
                }
            }

            // 未登録の場合のデフォルト: (Tag 1 -> Pad 0, Tag 2 -> Pad 1, Tag 3 -> Pad 2)
            if (robotId == 1) return InputSourceType.Gamepad0;
            if (robotId == 2) return InputSourceType.Gamepad1;
            if (robotId == 3) return InputSourceType.Gamepad2;
            return InputSourceType.None;
        }

        /// <summary>
        /// ロボットIDに対する入力ソース割り当てを変更
        /// </summary>
        public void SetRobotMapping(int robotId, InputSourceType source)
        {
            for (int i = 0; i < robotMappings.Count; i++)
            {
                if (robotMappings[i].robotId == robotId)
                {
                    robotMappings[i].inputSource = source;
                    return;
                }
            }
            robotMappings.Add(new RobotGamepadMapping(robotId, source));
        }

        /// <summary>
        /// 互換用旧メソッド
        /// </summary>
        public PlayerInputCommand GetPlayerInput(int playerIndex)
        {
            return GetInputForRobot(playerIndex);
        }

        void OnGUI()
        {
            if (!showMappingOverlay) return;

            GUI.color = Color.white;
            GUI.skin.label.fontSize = 11;
            GUI.skin.button.fontSize = 11;
            GUI.skin.box.fontSize = 11;

            // 画面右上にマッピング設定ウィンドウを表示
            GUILayout.BeginArea(new Rect(Screen.width - 480, 10, 470, 560), GUI.skin.box);
            GUILayout.BeginVertical();

            GUILayout.Label("<b><color=#00FFFF>【🎮 コントローラー & ロボット専用割り当て】</color></b> (F2で切替)");

            // 接続中ゲームパッドの認識状況表示 (最大3台)
            var allPads = Gamepad.all;
            GUILayout.Label($"認識中ゲームパッド数: <color={(allPads.Count > 0 ? "#00FF88" : "#FF6666")}><b>{allPads.Count} 台</b></color> (Bridge 1~3 専用対応)");

            for (int p = 0; p < 3; p++)
            {
                string bridgeName = $"/dev/custack_bridge_{p + 1}";
                if (p < allPads.Count && allPads[p] != null)
                {
                    var pad = allPads[p];
                    Vector2 stick = pad.leftStick.ReadValue();
                    bool btn = pad.rightShoulder.isPressed || pad.buttonEast.isPressed || pad.leftShoulder.isPressed;
                    GUILayout.Label($"  Pad [{p}] ➔ <color=#FFFF00>Tag {p + 1} ({bridgeName})</color>: <color=#00FF88><b>{pad.displayName}</b></color> (Stick: {stick.x:F2}, {stick.y:F2})");
                }
                else
                {
                    string fbName = (p == 0) ? "WASD" : (p == 1 ? "矢印" : "IJKL");
                    GUILayout.Label($"  Pad [{p}] ➔ <color=#888888>Tag {p + 1} ({bridgeName}): 未接続 (KB Fallback: {fbName})</color>");
                }
            }

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 デフォルト設定にリセット", GUILayout.Height(24)))
            {
                ResetToDefaultMappings();
                Robot.RobotManager.Instance?.SetOnlyPrimaryRobotsEnabled();
            }
            if (GUILayout.Button("🎯 1~3のみ表示 (ノイズ遮断)", GUILayout.Height(24)))
            {
                Robot.RobotManager.Instance?.SetOnlyPrimaryRobotsEnabled();
            }
            if (GUILayout.Button("🌐 全機体表示", GUILayout.Height(24)))
            {
                Robot.RobotManager.Instance?.SetAllRobotsEnabled(true);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("<b>▼ 各ロボット (AprilTag ID) の表示 & 入力割り当て:</b>");

            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(360));

            for (int i = 0; i < 16; i++)
            {
                int robotId = i;
                InputSourceType currentSrc = GetRobotMapping(robotId);
                PlayerInputCommand currentInput = GetInputForRobot(robotId);
                bool isRenderOn = Robot.RobotManager.Instance != null && Robot.RobotManager.Instance.IsRobotEnabled(robotId);

                GUILayout.BeginHorizontal(GUI.skin.box);
                Color rColor = Robot.RobotManager.GetRobotColor(robotId);
                GUI.color = isRenderOn ? rColor : new Color(0.4f, 0.4f, 0.4f, 0.6f);
                GUILayout.Label($"<b>🤖 #{robotId}</b>", GUILayout.Width(45));
                GUI.color = Color.white;

                // レンダリング表示 ON / OFF 切り替えトグル
                string renderLabel = isRenderOn ? "<color=#00FF88>👁️ ON</color>" : "<color=#888888>🚫 OFF</color>";
                if (GUILayout.Button(renderLabel, GUILayout.Width(62), GUILayout.Height(22)))
                {
                    if (Robot.RobotManager.Instance != null)
                    {
                        Robot.RobotManager.Instance.SetRobotEnabled(robotId, !isRenderOn);
                    }
                }

                string srcLabel = currentSrc switch
                {
                    InputSourceType.Gamepad0 => "🎮 Pad 0 (Br1)",
                    InputSourceType.Gamepad1 => "🎮 Pad 1 (Br2)",
                    InputSourceType.Gamepad2 => "🎮 Pad 2 (Br3)",
                    InputSourceType.KeyboardP1 => "⌨️ KB (WASD)",
                    InputSourceType.KeyboardP2 => "⌨️ KB (矢印)",
                    InputSourceType.KeyboardP3 => "⌨️ KB (IJKL)",
                    _ => "➖ なし"
                };

                if (GUILayout.Button(srcLabel, GUILayout.Width(105), GUILayout.Height(22)))
                {
                    // クリックで次の入力ソースに切り替え
                    InputSourceType nextSrc = currentSrc switch
                    {
                        InputSourceType.None => InputSourceType.Gamepad0,
                        InputSourceType.Gamepad0 => InputSourceType.Gamepad1,
                        InputSourceType.Gamepad1 => InputSourceType.Gamepad2,
                        InputSourceType.Gamepad2 => InputSourceType.KeyboardP1,
                        InputSourceType.KeyboardP1 => InputSourceType.KeyboardP2,
                        InputSourceType.KeyboardP2 => InputSourceType.KeyboardP3,
                        InputSourceType.KeyboardP3 => InputSourceType.None,
                        _ => InputSourceType.None
                    };
                    SetRobotMapping(robotId, nextSrc);
                }

                // リアルタイム入力状況
                string activeStatus = currentInput.IsConnected ? "<color=#00FF88>●</color>" : "<color=#666666>○</color>";
                string moveInfo = isRenderOn
                    ? $"Vx:{currentInput.Move.y:+0.00;-0.00; 0.00} Vy:{currentInput.Move.x:+0.00;-0.00; 0.00} Ω:{currentInput.Omega:+0.00;-0.00; 0.00}"
                    : "<color=#777777>(非表示・同期停止中)</color>";
                GUILayout.Label($"{activeStatus} {moveInfo}", GUILayout.Width(200));

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
