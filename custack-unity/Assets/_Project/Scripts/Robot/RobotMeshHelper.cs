using UnityEngine;

namespace Custack.Robot
{
    /// <summary>
    /// ロボットの視覚モデル（中央白色円形パッチ + 外周ドーナツ型カラーリング）を生成するヘルパークラス。
    /// 天頂カメラの AprilTag 認識精度向上のため、ロボット本体と重なる中心部を白色に保ち、
    /// 外周のドーナツ領域にプレイヤーカラーやエフェクトを描画します。
    /// </summary>
    public static class RobotMeshHelper
    {
        private static Mesh cachedCircleMesh;
        private static Mesh cachedDonutMesh;

        /// <summary>
        /// 中心の白色円形メッシュ（半径 default: 0.28m）
        /// </summary>
        public static Mesh GetOrCreateCircleMesh(float radius = 0.28f, int segments = 36)
        {
            if (cachedCircleMesh != null) return cachedCircleMesh;

            Mesh mesh = new Mesh();
            mesh.name = "Robot_CenterCircleMesh";

            Vector3[] vertices = new Vector3[segments + 1];
            Vector2[] uvs = new Vector2[segments + 1];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices[i + 1] = new Vector3(cos * radius, sin * radius, 0f);
                uvs[i + 1] = new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f);
            }

            for (int i = 0; i < segments; i++)
            {
                // 時計回り (Clockwise: 前面描画)
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1 == segments) ? 1 : i + 2;
                triangles[i * 3 + 2] = i + 1;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            cachedCircleMesh = mesh;
            return mesh;
        }

        /// <summary>
        /// 外周ドーナツ型リングメッシュ（内径 default: 0.30m, 外径 default: 0.52m）
        /// </summary>
        public static Mesh GetOrCreateDonutMesh(float innerRadius = 0.30f, float outerRadius = 0.52f, int segments = 36)
        {
            if (cachedDonutMesh != null) return cachedDonutMesh;

            Mesh mesh = new Mesh();
            mesh.name = "Robot_DonutRingMesh";

            Vector3[] vertices = new Vector3[segments * 2];
            Vector2[] uvs = new Vector2[segments * 2];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // 内周頂点
                vertices[i * 2] = new Vector3(cos * innerRadius, sin * innerRadius, 0f);
                uvs[i * 2] = new Vector2(0f, i / (float)segments);

                // 外周頂点
                vertices[i * 2 + 1] = new Vector3(cos * outerRadius, sin * outerRadius, 0f);
                uvs[i * 2 + 1] = new Vector2(1f, i / (float)segments);
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int i0 = i * 2;         // 現在の内周
                int i1 = i * 2 + 1;     // 現在の外周
                int i2 = next * 2;     // 次の内周
                int i3 = next * 2 + 1; // 次の外周

                // 時計回り (Clockwise: 前面描画)
                // Triangle 1: i0 -> i3 -> i1
                triangles[i * 6] = i0;
                triangles[i * 6 + 1] = i3;
                triangles[i * 6 + 2] = i1;

                // Triangle 2: i0 -> i2 -> i3
                triangles[i * 6 + 3] = i0;
                triangles[i * 6 + 4] = i2;
                triangles[i * 6 + 5] = i3;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            cachedDonutMesh = mesh;
            return mesh;
        }
    }
}
