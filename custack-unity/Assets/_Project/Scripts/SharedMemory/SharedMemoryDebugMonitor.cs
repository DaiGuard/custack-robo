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
        [Tooltip("画面左上にデバッグオーバーレイを表示するかどうか")]
        public bool showOverlay = true;

        [Tooltip("キーボード 'F1' でオーバーレイ表示/非表示を切り替え")]
        public KeyCode toggleKey = KeyCode.F1;

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
            // F1 キーでオーバーレイ表示切替 (New Input System / Legacy Input 両対応)
            bool togglePressed = false;
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                togglePressed = UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame;
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

            // 背景ボックス
            GUILayout.BeginArea(new Rect(10, 10, 440, 400), GUI.skin.box);
            GUILayout.BeginVertical();

            GUILayout.Label($"<b><color=#00FFFF>【CuStack-Robo 共有メモリ デバイスID モニター】</color></b> (F1で切替)");
            GUILayout.Label($"SHM Path: <color=#FFFF00>{shmPath}</color> | Rate: <color=#00FF88>{updateRateHz:F1} Hz</color>");
            GUILayout.Label($"Seq: {latestData.sequence} | Active Robots: <color=#00FF88>{latestData.count}</color>");
            GUILayout.Space(2);

            if (!hasValidData)
            {
                GUILayout.Label("<color=#FF4444>⚠️ 共有メモリからデータを取得できていません。\ncustack_router が起動しているか確認してください。</color>");
            }
            else
            {
                int displayCount = Mathf.Min((int)latestData.count, SharedRobotPoseData.MaxRobots);
                if (displayCount == 0)
                {
                    GUILayout.Label("<color=#FFAA00>ロボット未検出 (count: 0)</color>");
                }
                else
                {
                    scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(300));
                    for (int i = 0; i < displayCount; i++)
                    {
                        SharedRobotPose p = latestData.GetPose(i);
                        string legStr = GetLegName(p.legId);
                        string rArmStr = GetArmName(p.armRightId);
                        string lArmStr = GetArmName(p.armLeftId);
                        string statusStr = p.status == 1 ? "<color=#00FF88>OK</color>" : "<color=#FF4444>None</color>";

                        GUILayout.BeginVertical(GUI.skin.box);
                        GUILayout.Label($"<b>🤖 Robot [{p.id}]</b> (Index: {i}) Status: {statusStr}");
                        GUILayout.Label($"  📍 Pose : X={p.x:F3}, Y={p.y:F3}, θ={p.theta:F2} rad ({Mathf.Rad2Deg * p.theta:F1}°)");
                        GUILayout.Label($"  🦵 Leg  : <b><color=#00FFCC>0x{p.legId:X2} [{legStr}]</color></b> | ⚔️ R: <b><color=#FFAA00>0x{p.armRightId:X2} [{rArmStr}]</color></b> | 🛡️ L: <b><color=#FF88FF>0x{p.armLeftId:X2} [{lArmStr}]</color></b>");
                        GUILayout.EndVertical();
                    }
                    GUILayout.EndScrollView();
                }
            }

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
