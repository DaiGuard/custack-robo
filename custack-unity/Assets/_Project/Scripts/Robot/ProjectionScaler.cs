using UnityEngine;

namespace Custack.Robot
{
    /// <summary>
    /// カメラ・プロジェクション画面と正規化座標 (-1.0 ~ 1.0) のスケーリング計算クラス
    /// </summary>
    public class ProjectionScaler : MonoBehaviour
    {
        [Header("カメラ設定")]
        public Camera projectionCamera;

        [Header("手動スケール (カメラ未設定時)")]
        public Vector2 manualScale = new Vector2(5f, 5f);

        [Header("ロボット高さ・カメラ視差補正 (Parallax Correction)")]
        [Tooltip("ロボット天面の高さによる外側浮き上がりを相殺するスケール (1.0: 補正なし, 0.95: 約5%中心へ引き戻し)")]
        [Range(0.75f, 1.05f)]
        public float parallaxCorrectionScale = 0.95f;

        [Tooltip("カメラ光軸中心 (正規化座標 -1.0 ~ 1.0)")]
        public Vector2 cameraOpticalCenterNorm = new Vector2(0f, 0.5f);

        public Vector2 GetCurrentScale()
        {
            if (projectionCamera != null && projectionCamera.orthographic)
            {
                float y = projectionCamera.orthographicSize;
                float x = y * projectionCamera.aspect;
                return new Vector2(x, y);
            }

            return manualScale;
        }

        public Vector3 NormalizedToWorld(float normX, float normY)
        {
            // カメラ光軸中心からの相対オフセット
            float dx = normX - cameraOpticalCenterNorm.x;
            float dy = normY - cameraOpticalCenterNorm.y;

            // ロボットの高さによる外側への浮き上がりを相殺 (中心側へ縮小補正)
            float correctedX = cameraOpticalCenterNorm.x + dx * parallaxCorrectionScale;
            float correctedY = cameraOpticalCenterNorm.y + dy * parallaxCorrectionScale;

            Vector2 scale = GetCurrentScale();
            // ROS/Jetson: X (右+), Y (下+)
            // Unity: X (右+), Y (上+) -> Y反転
            return new Vector3(correctedX * scale.x, -correctedY * scale.y, 0f);
        }

        public float NormalizedThetaToUnityDeg(float rad)
        {
            return -rad * Mathf.Rad2Deg;
        }
    }
}
