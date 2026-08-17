using UnityEngine;

namespace Custack.Core
{
    /// <summary>
    /// マルチディスプレイ管理コンポーネント。
    /// Display 1: ホスト PC メイン画面 (管理ダッシュボード・信号・コントローラー設定)
    /// Display 2: サブディスプレイ / プロジェクター (床面バトルフィールド 1:1 投影)
    /// </summary>
    public class MultiDisplayManager : MonoBehaviour
    {
        public static MultiDisplayManager Instance { get; private set; }

        [Header("Cameras")]
        [Tooltip("ホスト PC メイン画面用カメラ (Display 1)")]
        public Camera hostDashboardCamera;

        [Tooltip("プロジェクター床面投影用カメラ (Display 2)")]
        public Camera projectionCamera;

        [Header("ディスプレイ状態")]
        public int connectedDisplayCount = 1;
        public bool isSecondaryDisplayActive = false;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeDisplays();
        }

        public void InitializeDisplays()
        {
            connectedDisplayCount = Display.displays.Length;
            Debug.Log($"<color=#00FF88>[MultiDisplayManager]</color> 🖥️ 接続ディスプレイ数: <b>{connectedDisplayCount} 台</b>");

            // Display 2 (プロジェクター) が接続されている場合はアクティブ化
            if (Display.displays.Length > 1)
            {
                Display.displays[1].Activate();
                isSecondaryDisplayActive = true;
                Debug.Log("<color=#00FF88>[MultiDisplayManager]</color> ✅ Display 2 (プロジェクター投影面) をアクティブ化しました。");
            }
            else
            {
                isSecondaryDisplayActive = false;
                Debug.Log("<color=#FFAA00>[MultiDisplayManager]</color> ℹ️ ディスプレイが1台のみ検出されました。Gameビューの Display 切り替えまたは単一画面モードで動作します。");
            }

            // カメラの Target Display を設定
            if (hostDashboardCamera != null)
            {
                hostDashboardCamera.targetDisplay = 0; // Display 1 (メイン画面)
            }

            if (projectionCamera != null)
            {
                // プロジェクターがある場合は Display 2、ない場合もエディタや単一画面で扱えるよう設定
                projectionCamera.targetDisplay = (Display.displays.Length > 1) ? 1 : 0;
            }
        }
    }
}
