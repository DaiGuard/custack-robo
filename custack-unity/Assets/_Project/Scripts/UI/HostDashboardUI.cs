using UnityEngine;
using UnityEngine.InputSystem;
using Custack.Robot;
using Custack.Input;
using Custack.Combat;
using Custack.Core;
using Custack.Terrain;

namespace Custack.UI
{
    /// <summary>
    /// ホスト PC メインディスプレイ (Display 1) に常時表示される管理ダッシュボード。
    /// コントローラー割り当て、シリアルブリッジ信号状態、ロボットテレメトリ、
    /// 機体表示 ON/OFF、バトルステータスを一括可視化・操作します。
    /// </summary>
    public class HostDashboardUI : MonoBehaviour
    {
        public static HostDashboardUI Instance { get; private set; }

        [Header("設定")]
        [Tooltip("ダッシュボードの表示/非表示 (F1キーで最小化・復帰)")]
        public bool showDashboard = true;

        private Vector2 robotScrollPos;
        private float fpsTimer = 0f;
        private float currentFps = 60f;
        private int frameCount = 0;

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
            // F1 キーでダッシュボードの最小化/展開切り替え
            if (Keyboard.current != null)
            {
                if (Keyboard.current.f1Key.wasPressedThisFrame)
                {
                    showDashboard = !showDashboard;
                }

                // F5〜F8 キーで地形マップ切り替え
                if (TerrainMapManager.Instance != null)
                {
                    if (Keyboard.current.f5Key.wasPressedThisFrame) TerrainMapManager.Instance.SwitchMap(MapType.Forest);
                    if (Keyboard.current.f6Key.wasPressedThisFrame) TerrainMapManager.Instance.SwitchMap(MapType.Snow);
                    if (Keyboard.current.f7Key.wasPressedThisFrame) TerrainMapManager.Instance.SwitchMap(MapType.City);
                    if (Keyboard.current.f8Key.wasPressedThisFrame) TerrainMapManager.Instance.SwitchMap(MapType.Volcano);
                }

                // [ / ] キーで視差スケールを微調整
                var scaler = RobotManager.Instance != null ? RobotManager.Instance.projectionScaler : null;
                if (scaler != null)
                {
                    if (Keyboard.current.leftBracketKey.wasPressedThisFrame)
                    {
                        scaler.parallaxCorrectionScale = Mathf.Clamp(scaler.parallaxCorrectionScale - 0.005f, 0.70f, 1.10f);
                    }
                    if (Keyboard.current.rightBracketKey.wasPressedThisFrame)
                    {
                        scaler.parallaxCorrectionScale = Mathf.Clamp(scaler.parallaxCorrectionScale + 0.005f, 0.70f, 1.10f);
                    }
                }
            }

            // FPS 計測
            frameCount++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                currentFps = frameCount / fpsTimer;
                frameCount = 0;
                fpsTimer = 0f;
            }
        }

        void OnGUI()
        {
            // ホスト PC 画面 (Display 1) でのみ描画
            // エディタの Game ビューまたは Display 1 で描画されます

            if (!showDashboard)
            {
                // 最小化時のトグルボタン
                if (GUI.Button(new Rect(10, 10, 180, 28), "<b>🖥️ 管理画面を表示 (F1)</b>"))
                {
                    showDashboard = true;
                }
                return;
            }

            GUI.color = Color.white;
            GUI.skin.label.fontSize = 11;
            GUI.skin.button.fontSize = 11;
            GUI.skin.box.fontSize = 11;

            float screenW = Screen.width;
            float screenH = Screen.height;

            // 全体背景コンテナ (Display 1)
            GUILayout.BeginArea(new Rect(10, 10, screenW - 20, screenH - 20));
            GUILayout.BeginVertical();

            // 1. トップヘッダーバー
            DrawHeaderBar();

            GUILayout.Space(6);

            // 2. メイン 3 カラムレイアウト (左: コントローラー, 中: ロボット信号&テレメトリ, 右: バトル&システム)
            GUILayout.BeginHorizontal();

            // --- 左カラム: コントローラー & シリアルブリッジ ---
            float colWidth = (screenW - 50) / 3f;
            DrawControllerColumn(colWidth);

            GUILayout.Space(8);

            // --- 中央カラム: ロボット機体・信号・テレメトリ ---
            DrawRobotTelemetryColumn(colWidth + 20);

            GUILayout.Space(8);

            // --- 右カラム: バトルステータス & プロジェクター監視 ---
            DrawBattleAndProjectorColumn(colWidth - 20);

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawHeaderBar()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);

            GUILayout.Label("<b><size=14><color=#00FFFF>CuStack-Robo</color> <color=#FFFFFF>Host Master Control Panel</color></size></b>", GUILayout.Width(380));

            // ディスプレイ状態
            var dispMgr = MultiDisplayManager.Instance;
            int dispCount = (dispMgr != null) ? dispMgr.connectedDisplayCount : Display.displays.Length;
            bool isProjActive = (dispMgr != null) ? dispMgr.isSecondaryDisplayActive : (Display.displays.Length > 1);

            string dispStatus = isProjActive
                ? "<color=#00FF88>● Display 2 (プロジェクター 1:1 投影中)</color>"
                : "<color=#FFAA00>○ Display 2 未接続 (単一画面プレビュー)</color>";

            GUILayout.Label($"🖥️ <b>画面構成:</b> Display 1 (PC) | {dispStatus}", GUILayout.Width(350));

            GUILayout.FlexibleSpace();

            // FPS & ステータス
            GUILayout.Label($"⚡ <b>FPS:</b> <color=#00FF88>{currentFps:F1}</color> | <b>Time:</b> {Time.time:F1}s", GUILayout.Width(150));

            if (GUILayout.Button("F1: 最小化", GUILayout.Width(80), GUILayout.Height(22)))
            {
                showDashboard = false;
            }

            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
            if (GUILayout.Button("🚪 終了", GUILayout.Width(70), GUILayout.Height(22)))
            {
                QuitApplication();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndHorizontal();
        }

        private void DrawControllerColumn(float width)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(width));

            GUILayout.Label("<b><color=#00FFFF>【🎮 コントローラー & シリアルブリッジ】</color></b>");
            var allPads = Gamepad.all;
            GUILayout.Label($"認識中ゲームパッド: <color={(allPads.Count > 0 ? "#00FF88" : "#FF6666")}><b>{allPads.Count} 台</b></color> (最大 3 台専用バインド)");

            var inputMgr = ControllerInputManager.Instance;

            for (int p = 0; p < 3; p++)
            {
                string portName = $"/dev/custack_bridge_{p + 1}";
                int targetTagId = p + 1;

                GUILayout.BeginVertical(GUI.skin.box);
                if (p < allPads.Count && allPads[p] != null)
                {
                    var pad = allPads[p];
                    Vector2 stick = pad.leftStick.ReadValue();
                    float omega = pad.rightStick.ReadValue().x;
                    bool btnR = pad.rightShoulder.isPressed || pad.buttonEast.isPressed;
                    bool btnL = pad.leftShoulder.isPressed || pad.buttonWest.isPressed;

                    GUILayout.Label($"<b>🎮 Gamepad [{p}]</b> ➔ <color=#FFFF00>Tag #{targetTagId} ({portName})</color>");
                    GUILayout.Label($"   名前: <color=#00FF88>{pad.displayName}</color>");
                    GUILayout.Label($"   移動: <color=#FFFFFF>Vx: {stick.y:+0.00;-0.00; 0.00}  Vy: {stick.x:+0.00;-0.00; 0.00}</color> | 旋回: <color=#FFFFFF>{omega:+0.00;-0.00; 0.00}</color>");
                    GUILayout.Label($"   武装: 右 [R1/〇]: {(btnR ? "<color=#00FF88>ON</color>" : "OFF")} | 左 [L1/□]: {(btnL ? "<color=#00FF88>ON</color>" : "OFF")}");
                }
                else
                {
                    string fbName = (p == 0) ? "WASD + J/K/U" : (p == 1 ? "矢印 + Numpad / L/;" : "IJKL + 7/8/9");
                    GUILayout.Label($"<b>🎮 Gamepad [{p}]</b> ➔ <color=#FFFF00>Tag #{targetTagId} ({portName})</color>");
                    GUILayout.Label($"   <color=#888888>未接続 (KB代替: <color=#00FF88>{fbName}</color>)</color>");
                }
                GUILayout.EndVertical();
            }

            GUILayout.Space(4);
            if (GUILayout.Button("🔄 コントローラー割り当てを初期化", GUILayout.Height(24)))
            {
                inputMgr?.ResetToDefaultMappings();
            }

            GUILayout.EndVertical();
        }

        private void DrawRobotTelemetryColumn(float width)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(width));

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b><color=#00FFFF>【🤖 ロボット信号・機体テレメトリ】</color></b>");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("🎯 1~3のみ", GUILayout.Width(80), GUILayout.Height(20)))
            {
                RobotManager.Instance?.SetOnlyPrimaryRobotsEnabled();
            }
            if (GUILayout.Button("🌐 全機体", GUILayout.Width(70), GUILayout.Height(20)))
            {
                RobotManager.Instance?.SetAllRobotsEnabled(true);
            }
            GUILayout.EndHorizontal();

            var robotMgr = RobotManager.Instance;
            robotScrollPos = GUILayout.BeginScrollView(robotScrollPos);

            for (int i = 0; i < 16; i++)
            {
                int robotId = i;
                bool isEnabled = (robotMgr != null) && robotMgr.IsRobotEnabled(robotId);
                RobotEntity robot = (robotMgr != null && robotId < robotMgr.robots.Count) ? robotMgr.robots[robotId] : null;

                GUILayout.BeginHorizontal(GUI.skin.box);

                Color rCol = RobotManager.GetRobotColor(robotId);
                GUI.color = isEnabled ? rCol : new Color(0.4f, 0.4f, 0.4f, 0.5f);
                GUILayout.Label($"<b>#{robotId:D2}</b>", GUILayout.Width(35));
                GUI.color = Color.white;

                // 表示 ON / OFF トグル
                string toggleText = isEnabled ? "<color=#00FF88>👁️ON</color>" : "<color=#888888>🚫OFF</color>";
                if (GUILayout.Button(toggleText, GUILayout.Width(50), GUILayout.Height(20)))
                {
                    robotMgr?.SetRobotEnabled(robotId, !isEnabled);
                }

                if (isEnabled && robot != null)
                {
                    Vector3 pos = robot.transform.position;
                    float rot = robot.transform.eulerAngles.z;
                    string posStr = $"X:{pos.x:+0.0;-0.0;0.0} Y:{pos.y:+0.0;-0.0;0.0} ∠{rot:F0}°";

                    var equip = robot.EquipmentComponent;
                    string legStr = (equip != null) ? equip.LegType.ToString() : "Omni";
                    string wpnStr = (equip != null) ? $"{equip.RightArmType}/{equip.LeftArmType}" : "Gatling/Sword";

                    GUILayout.Label($"<color=#FFFFFF>{posStr}</color> | {legStr} | {wpnStr}", GUILayout.Width(220));

                    // HP バー表示
                    var health = robot.HealthComponent;
                    if (health != null)
                    {
                        float hpRatio = health.HealthPercent;
                        string hpColor = (hpRatio > 0.5f) ? "#00FF88" : (hpRatio > 0.2f ? "#FFFF00" : "#FF4444");
                        GUILayout.Label($"HP:<color={hpColor}><b>{health.currentHp:F0}</b></color>", GUILayout.Width(60));
                    }
                }
                else
                {
                    GUILayout.Label("<color=#666666>(表示無効・ノイズ遮断中)</color>", GUILayout.Width(280));
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawBattleAndProjectorColumn(float width)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(width));

            GUILayout.Label("<b><color=#00FFFF>【⚔️ バトル状況 & プロジェクター監視】</color></b>");

            var robotMgr = RobotManager.Instance;

            // P1 (Tag 1), P2 (Tag 2), P3 (Tag 3) のサマリー
            for (int p = 1; p <= 3; p++)
            {
                int tagId = p;
                RobotEntity robot = (robotMgr != null && tagId < robotMgr.robots.Count) ? robotMgr.robots[tagId] : null;

                GUILayout.BeginVertical(GUI.skin.box);
                Color pColor = RobotManager.GetRobotColor(tagId);
                GUI.color = pColor;
                GUILayout.Label($"<b>🤖 Player {p} (Tag #{tagId})</b>");
                GUI.color = Color.white;

                if (robot != null && robot.HealthComponent != null)
                {
                    var h = robot.HealthComponent;
                    float ratio = h.HealthPercent;
                    string bar = GetHpAsciiBar(ratio, 16);
                    string hpCol = (ratio > 0.5f) ? "#00FF88" : (ratio > 0.2f ? "#FFFF00" : "#FF4444");

                    GUILayout.Label($"  HP: <color={hpCol}><b>{h.currentHp:F0} / {h.maxHp:F0}</b></color>");
                    GUILayout.Label($"  <color={hpCol}>[{bar}]</color>");

                    var eq = robot.EquipmentComponent;
                    if (eq != null)
                    {
                        GUILayout.Label($"  脚: <b>{eq.LegType}</b> | 武器: <b>[R]{eq.RightArmType} [L]{eq.LeftArmType}</b>");
                    }
                }
                else
                {
                    GUILayout.Label("  <color=#888888>機体未検出 / 待機中</color>");
                }
                GUILayout.EndVertical();
            }

            GUILayout.Space(6);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b><color=#FFFF00>【📐 カメラ視差・ロボット高さ補正】</color></b>");

            var scaler = robotMgr != null ? robotMgr.projectionScaler : null;
            if (scaler != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>視差スケール:</b> <color=#00FF88><b>{scaler.parallaxCorrectionScale:F3}</b></color>", GUILayout.Width(120));
                if (GUILayout.Button("◀ -0.01", GUILayout.Width(55))) scaler.parallaxCorrectionScale = Mathf.Clamp(scaler.parallaxCorrectionScale - 0.01f, 0.70f, 1.10f);
                if (GUILayout.Button("-0.002", GUILayout.Width(50))) scaler.parallaxCorrectionScale = Mathf.Clamp(scaler.parallaxCorrectionScale - 0.002f, 0.70f, 1.10f);
                if (GUILayout.Button("+0.002", GUILayout.Width(50))) scaler.parallaxCorrectionScale = Mathf.Clamp(scaler.parallaxCorrectionScale + 0.002f, 0.70f, 1.10f);
                if (GUILayout.Button("+0.01 ▶", GUILayout.Width(55))) scaler.parallaxCorrectionScale = Mathf.Clamp(scaler.parallaxCorrectionScale + 0.01f, 0.70f, 1.10f);
                GUILayout.EndHorizontal();

                // スライダー
                scaler.parallaxCorrectionScale = GUILayout.HorizontalSlider(scaler.parallaxCorrectionScale, 0.75f, 1.05f);

                // カメラ光軸中心 Y オフセット
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>光軸中心 Y:</b> {scaler.cameraOpticalCenterNorm.y:+0.000;-0.000;0.000}", GUILayout.Width(110));
                if (GUILayout.Button("▲ 上へ", GUILayout.Width(48))) scaler.cameraOpticalCenterNorm.y = Mathf.Clamp(scaler.cameraOpticalCenterNorm.y - 0.01f, -1.0f, 1.0f);
                if (GUILayout.Button("▼ 下へ", GUILayout.Width(48))) scaler.cameraOpticalCenterNorm.y = Mathf.Clamp(scaler.cameraOpticalCenterNorm.y + 0.01f, -1.0f, 1.0f);
                if (GUILayout.Button("リセット", GUILayout.Width(55))) { scaler.cameraOpticalCenterNorm = new Vector2(0f, 0.5f); scaler.parallaxCorrectionScale = 0.95f; }
                GUILayout.EndHorizontal();

                GUILayout.Label("<color=#888888>ショートカット: [ / ] キーでスケール微調整</color>");
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b><color=#00FFFF>【🗺️ 地形マップ切替 (床面投影)】</color></b>");

            var mapMgr = TerrainMapManager.Instance;
            if (mapMgr != null)
            {
                GUILayout.Label($"現在: <b><color=#00FF88>{mapMgr.GetMapDisplayName(mapMgr.currentMapType)}</color></b>");
                GUILayout.Label($"<color=#AAAAAA><size=10>{mapMgr.GetMapDescription(mapMgr.currentMapType)}</size></color>");

                GUILayout.BeginHorizontal();
                GUI.backgroundColor = mapMgr.currentMapType == MapType.Forest ? Color.green : Color.white;
                if (GUILayout.Button("🌲 森 (F5)", GUILayout.Height(24))) mapMgr.SwitchMap(MapType.Forest);

                GUI.backgroundColor = mapMgr.currentMapType == MapType.Snow ? Color.cyan : Color.white;
                if (GUILayout.Button("❄️ 雪山 (F6)", GUILayout.Height(24))) mapMgr.SwitchMap(MapType.Snow);

                GUI.backgroundColor = mapMgr.currentMapType == MapType.City ? Color.yellow : Color.white;
                if (GUILayout.Button("🏙️ 市街地 (F7)", GUILayout.Height(24))) mapMgr.SwitchMap(MapType.City);

                GUI.backgroundColor = mapMgr.currentMapType == MapType.Volcano ? new Color(1f, 0.4f, 0.2f) : Color.white;
                if (GUILayout.Button("🌋 火山 (F8)", GUILayout.Height(24))) mapMgr.SwitchMap(MapType.Volcano);
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("<color=#888888>TerrainMapManager 未初期化</color>");
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);
            GUILayout.Label("<b>▼ プロジェクター床面投影設定:</b>");
            GUILayout.Label("  投影面: <color=#00FF88>1:1 俯瞰プロジェクションマッピング</color>");
            GUILayout.Label("  解像度比: <color=#00FF88>16:9 / 1080p フルスクリーン</color>");

            GUILayout.Space(4);
            if (GUILayout.Button("🔄 バトル再スタート (全機体 HP 全回復)", GUILayout.Height(26)))
            {
                if (robotMgr != null)
                {
                    for (int i = 0; i < robotMgr.robots.Count; i++)
                    {
                        if (robotMgr.robots[i] != null && robotMgr.robots[i].HealthComponent != null)
                        {
                            robotMgr.robots[i].HealthComponent.Respawn();
                        }
                    }
                }
                BattleHUD.Instance?.HideWinner();
                Debug.Log("<color=#00FF88>[HostDashboardUI]</color> ✅ 全機体の HP を全回復しました。");
            }

            GUILayout.Space(6);
            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
            if (GUILayout.Button("🚪 システム終了 (Quit Application)", GUILayout.Height(28)))
            {
                QuitApplication();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndVertical();
        }

        public void QuitApplication()
        {
            Debug.Log("<color=#FF6666>[HostDashboardUI]</color> 🚪 システムを終了します...");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private string GetHpAsciiBar(float ratio, int length)
        {
            int fill = Mathf.RoundToInt(ratio * length);
            fill = Mathf.Clamp(fill, 0, length);
            return new string('■', fill) + new string('─', length - fill);
        }
    }
}
