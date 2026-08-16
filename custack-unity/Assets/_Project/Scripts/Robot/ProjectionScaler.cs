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
            Vector2 scale = GetCurrentScale();
            // ROS/Jetson: X (右+), Y (下+)
            // Unity: X (右+), Y (上+) -> Y反転
            return new Vector3(normX * scale.x, -normY * scale.y, 0f);
        }

        public float NormalizedThetaToUnityDeg(float rad)
        {
            return -rad * Mathf.Rad2Deg;
        }
    }
}
