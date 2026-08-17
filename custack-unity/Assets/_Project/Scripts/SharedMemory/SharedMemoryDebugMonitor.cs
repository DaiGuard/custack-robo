using System;
using UnityEngine;
using Custack.Equipment;

namespace Custack.SharedMemory
{
    /// <summary>
    /// POSIX 共有メモリ (/dev/shm/custack_robot_poses) の受信データおよび
    /// デバイスIDを Game ビュー上にリアルタイム HUD 表示するデバッグモニター。
    /// </summary>
    public class SharedMemoryDebugMonitor : MonoBehaviour
    {
        [Header("設定")]
        [Tooltip("画面左上にデバッグオーバーレイを表示するかどうか (HostDashboardUI との重なり防止のため default: false)")]
        public bool showOverlay = false;

        [Tooltip("キーボード 'F3' でオーバーレイ表示/非表示を切り替え")]
        public KeyCode toggleKey = KeyCode.F3;

        [Tooltip("共有メモリパス")]
        public string shmPath = "/dev/shm/custack_robot_poses";

        private SharedMemoryReader reader;
        private SharedRobotPoseData latestData;
        private bool hasValidData = false;
        private float updateRateHz = 0f;
        private int frameCount = 0;
        private float lastFpsTime = 0f;

        void Start()
        {
            reader = new SharedMemoryReader(shmPath);
            lastFpsTime = Time.realtimeSinceStartup;
        }

        void Update()
        {
            // F3 キーで個別オーバーレイ表示切替 (F1 は HostDashboardUI 専用)
            bool togglePressed = false;
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                togglePressed = UnityEngine.InputSystem.Keyboard.current.f3Key.wasPressedThisFrame;
            }
            else
            {
                try
                {
                    togglePressed = UnityEngine.Input.GetKeyDown(toggleKey);
                }
                catch
                {
                    // Legacy Input Manager is not enabled
                }
            }

            if (togglePressed)
            {
                showOverlay = !showOverlay;
            }

            if (reader != null && reader.ReadPoses(out latestData))
            {
                hasValidData = true;
                frameCount++;
            }

            float now = Time.realtimeSinceStartup;
            if (now - lastFpsTime >= 1.0f)
            {
                updateRateHz = frameCount / (now - lastFpsTime);
                frameCount = 0;
                lastFpsTime = now;
            }
        }

        void OnDestroy()
        {
            reader?.Dispose();
        }

        private Vector2 scrollPos;

        void OnGUI()
        {
            if (!showOverlay) return;

            GUI.color = Color.white;
            GUI.skin.label.fontSize = 12;
            GUI.skin.box.fontSize = 12;

            // 背景ボックス (固定サイズ)
            GUILayout.BeginArea(new Rect(10, 10, 480, 520), GUI.skin.box);
            GUILayout.BeginVertical();

            GUILayout.Label($"<b><color=#00FFFF>【CuStack-Robo 共有メモリ デバイスID モニター】</color></b> (F3:表示切替)");
            GUILayout.Label($"SHM: <color=#FFFF00>{shmPath}</color> | Rate: <color=#00FF88>{updateRateHz:F1} Hz</color> | 検出中: <color=#00FF88>{latestData.count} 台</color>");
            GUILayout.Space(4);

            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(440));

            // 0〜15 (全16スロット) を固定で表示 (行数・サイズがガタつかない)
            for (int slotId = 0; slotId < 16; slotId++)
            {
                bool isFound = false;
                SharedRobotPose p = default;

                if (hasValidData)
                {
                    for (int k = 0; k < latestData.count && k < SharedRobotPoseData.MaxRobots; k++)
                    {
                        SharedRobotPose candidate = latestData.GetPose(k);
                        if (candidate.id == slotId)
                        {
                            isFound = true;
                            p = candidate;
                            break;
                        }
                    }
                }

                GUILayout.BeginVertical(GUI.skin.box);
                if (isFound)
                {
                    string legStr = GetLegName(p.legId);
                    string rArmStr = GetArmName(p.armRightId);
                    string lArmStr = GetArmName(p.armLeftId);
                    string statusStr = p.status == 1 ? "<color=#00FF88>OK</color>" : "<color=#FFAA00>NoTLM</color>";

                    GUILayout.Label($"<b>🤖 Slot #{slotId:D2} (Tag ID: {p.id})</b> Status: {statusStr}");
                    GUILayout.Label($"  📍 X:{p.x:+0.000;-0.000} Y:{p.y:+0.000;-0.000} θ:{p.theta:+0.00;-0.00} rad ({Mathf.Rad2Deg * p.theta:+0.0;-0.0}°)");
                    GUILayout.Label($"  🦵 Leg: <color=#00FFCC>0x{p.legId:X2}[{legStr}]</color> | ⚔️ R:<color=#FFAA00>0x{p.armRightId:X2}[{rArmStr}]</color> | 🛡️ L:<color=#FF88FF>0x{p.armLeftId:X2}[{lArmStr}]</color>");
                }
                else
                {
                    GUILayout.Label($"<color=#555555><b>🤖 Slot #{slotId:D2}</b> (Tag ID: {slotId}) - [未検出 / 待機中]</color>");
                }
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private static string GetLegName(byte id)
        {
            return id switch
            {
                0x01 => "Omni (オムニ)",
                0x02 => "Tire (二輪差動)",
                0x03 => "Crawler (キャタピラ)",
                _ => "未検出"
            };
        }

        private static string GetArmName(byte id)
        {
            return id switch
            {
                0x01 => "Gatling (ガトリング)",
                0x02 => "Sword (ソード)",
                0x03 => "LaserCannon (キャノン)",
                _ => "未検出"
            };
        }
    }
}
