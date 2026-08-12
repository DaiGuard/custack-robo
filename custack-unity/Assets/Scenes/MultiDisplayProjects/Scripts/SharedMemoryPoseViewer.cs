using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;

namespace Custack
{
    #region Inspector Display Classes

    [System.Serializable]
    public class ShmStatusInfo
    {
        [Tooltip("共有メモリファイルに接続中か")]
        public bool isConnected = false;

        [Tooltip("共有メモリファイルのパス")]
        public string shmPath = "/dev/shm/custack_robot_poses";

        [Tooltip("共有メモリプロトコルバージョン")]
        public uint protocolVersion = 0;

        [Tooltip("Seqlock シーケンス番号 (偶数: 確定)")]
        public uint sequence = 0;

        [Tooltip("ナノ秒タイムスタンプ")]
        public ulong timestampNs = 0;

        [Tooltip("現在の全検出ロボット数")]
        public uint totalDetectedCount = 0;

        [Tooltip("受信周波数 (Hz)")]
        public float receiveRateHz = 0f;
    }

    [System.Serializable]
    public class RobotPoseDisplay
    {
        [Tooltip("ロボットID (AprilTag ID)")]
        public int targetId = 0;

        [Tooltip("現在検出されているか")]
        public bool isDetected = false;

        [Tooltip("共有メモリ正規化座標 (-1.0 ~ 1.0)")]
        public Vector2 shmPosition = Vector2.zero;

        [Tooltip("共有メモリ回転角 (Radian)")]
        public float shmThetaRad = 0f;

        [Tooltip("共有メモリ回転角 (Degree)")]
        public float shmThetaDeg = 0f;

        [Tooltip("Unity換算ワールド座標 (X, Y)")]
        public Vector3 unityPosition = Vector3.zero;

        [Tooltip("Unity換算 Z軸回転角 (Degree)")]
        public float unityRotationDeg = 0f;

        [Tooltip("最終更新時刻 (Time.time)")]
        public float lastUpdatedTime = 0f;
    }

    #endregion

    /// <summary>
    /// 共有メモリ (/dev/shm/custack_robot_poses) からロボットの位置姿勢データを読み取り、
    /// インスペクター上にリアルタイムで表示・確認するためのデバッグ/モニタリングコンポーネント。
    /// </summary>
    public class SharedMemoryPoseViewer : MonoBehaviour
    {
        #region Serialized Fields (Inspector)

        [Header("共有メモリ設定")]
        [Tooltip("ポーズ共有メモリのパス")]
        public string poseShmPath = "/dev/shm/custack_robot_poses";

        [Tooltip("共有メモリ未検出時に自動で再接続を試行するか")]
        public bool autoReconnect = true;

        [Header("共有メモリ受信ステータス (Inspector Monitor)")]
        [SerializeField]
        private ShmStatusInfo shmStatus = new ShmStatusInfo();

        [Header("ロボット3台分の位置姿勢 (Inspector Monitor: ID 0, 1, 2)")]
        [SerializeField]
        private RobotPoseDisplay[] monitoredRobots = new RobotPoseDisplay[3]
        {
            new RobotPoseDisplay { targetId = 0 },
            new RobotPoseDisplay { targetId = 1 },
            new RobotPoseDisplay { targetId = 2 }
        };

        [Header("全検出ロボット一覧 (リアルタイム)")]
        [SerializeField]
        private List<RobotPoseDisplay> allDetectedRobots = new List<RobotPoseDisplay>();

        [Header("投影設定 / Transform自動反映 (任意)")]
        [Tooltip("投影範囲を計算する平行投影カメラ (未設定時は positionScale を使用)")]
        public Camera projectionCamera;

        [Tooltip("位置スケーリング係数")]
        public Vector2 positionScale = Vector2.one;

        [Tooltip("位置姿勢を反映させたい Transform リスト (要素0->ID 0, 要素1->ID 1, 要素2->ID 2)")]
        public List<Transform> targetTransforms = new List<Transform>();

        [Header("Scene ビュー ギズモ表示")]
        public bool drawGizmos = true;
        public float gizmoRadius = 0.3f;
        public float gizmoArrowLength = 0.8f;
        public Color[] robotColors = new Color[] { Color.red, Color.green, Color.blue };

        #endregion

        #region Private Fields

        private FileStream poseFileStream;
        private byte[] poseBuffer;

        private int frameCount = 0;
        private float statTimer = 0f;
        private const float LostTimeoutSeconds = 0.5f;

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            poseBuffer = new byte[Marshal.SizeOf<SharedRobotPoseData>()];
            shmStatus.shmPath = poseShmPath;

            // monitoredRobots 配列の初期化確認
            if (monitoredRobots == null || monitoredRobots.Length != 3)
            {
                monitoredRobots = new RobotPoseDisplay[3]
                {
                    new RobotPoseDisplay { targetId = 0 },
                    new RobotPoseDisplay { targetId = 1 },
                    new RobotPoseDisplay { targetId = 2 }
                };
            }
        }

        void Start()
        {
            InitSharedMemory();
        }

        void Update()
        {
            // 共有メモリ未接続時の再試行
            if (poseFileStream == null && autoReconnect)
            {
                InitSharedMemory();
            }

            // 共有メモリからデータを読み出し
            ReadRobotPosesFromShm();

            // 受信レート (Hz) の計算
            statTimer += Time.deltaTime;
            if (statTimer >= 1.0f)
            {
                shmStatus.receiveRateHz = frameCount / statTimer;
                frameCount = 0;
                statTimer = 0f;
            }

            // 一定時間検出がないロボットの検出フラグを落とす
            float now = Time.time;
            for (int i = 0; i < monitoredRobots.Length; i++)
            {
                if (monitoredRobots[i].isDetected && (now - monitoredRobots[i].lastUpdatedTime > LostTimeoutSeconds))
                {
                    monitoredRobots[i].isDetected = false;
                }
            }
        }

        void OnDisable()
        {
            CloseSharedMemory();
        }

        void OnDestroy()
        {
            CloseSharedMemory();
        }

        #endregion

        #region Shared Memory Operations

        private void InitSharedMemory()
        {
            try
            {
                if (File.Exists(poseShmPath))
                {
                    poseFileStream = new FileStream(poseShmPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    shmStatus.isConnected = true;
                    Debug.Log($"[SharedMemoryPoseViewer] Connected to SHM: {poseShmPath}");
                }
                else
                {
                    shmStatus.isConnected = false;
                }
            }
            catch (Exception ex)
            {
                shmStatus.isConnected = false;
                Debug.LogWarning($"[SharedMemoryPoseViewer] Cannot open SHM ({poseShmPath}): {ex.Message}");
            }
        }

        private void CloseSharedMemory()
        {
            if (poseFileStream != null)
            {
                poseFileStream.Dispose();
                poseFileStream = null;
            }
            shmStatus.isConnected = false;
        }

        private unsafe void ReadRobotPosesFromShm()
        {
            if (poseFileStream == null) return;

            SharedRobotPoseData data = default;
            bool readSuccess = false;

            // Seqlock ロックフリー読み出し (最大5回リトライ)
            for (int retry = 0; retry < 5; ++retry)
            {
                try
                {
                    poseFileStream.Seek(0, SeekOrigin.Begin);
                    int bytesRead = poseFileStream.Read(poseBuffer, 0, poseBuffer.Length);
                    if (bytesRead < poseBuffer.Length) break;

                    fixed (byte* ptr = poseBuffer)
                    {
                        SharedRobotPoseData* shmData = (SharedRobotPoseData*)ptr;
                        uint seq = shmData->sequence;

                        // 奇数は書き込み中、0は未初期化なのでスキップ
                        if ((seq & 1) != 0 || seq == 0) continue;

                        data = *shmData;

                        // 読み出し前後の sequence が一致していれば整合性あり
                        if (data.sequence == seq)
                        {
                            readSuccess = true;
                            break;
                        }
                    }
                }
                catch
                {
                    break;
                }
            }

            if (!readSuccess) return;

            frameCount++;

            // ステータス情報の更新
            shmStatus.protocolVersion = data.version;
            shmStatus.sequence = data.sequence;
            shmStatus.timestampNs = data.timestampNs;
            shmStatus.totalDetectedCount = data.count;

            // カメラの平行投影スケール計算
            if (projectionCamera != null && projectionCamera.orthographic)
            {
                positionScale.y = projectionCamera.orthographicSize;
                positionScale.x = projectionCamera.orthographicSize * projectionCamera.aspect;
            }

            // 全検出ロボット一覧のクリア＆更新
            allDetectedRobots.Clear();

            // 監視中3台の検出フラグをリセット準備
            bool[] detectedThisFrame = new bool[3];

            float currentTime = Time.time;

            for (int i = 0; i < data.count && i < SharedRobotPoseData.MaxRobots; i++)
            {
                SharedRobotPose pose = data.GetPose(i);
                int id = pose.id;

                // Unity座標系換算
                // ROS/Jetson: X (右+), Y (下+)
                // Unity 2D: X (右+), Y (上+) -> Y反転
                float uX = pose.x * positionScale.x;
                float uY = -pose.y * positionScale.y;
                float uThetaDeg = -pose.theta * Mathf.Rad2Deg;

                RobotPoseDisplay displayItem = new RobotPoseDisplay
                {
                    targetId = id,
                    isDetected = true,
                    shmPosition = new Vector2(pose.x, pose.y),
                    shmThetaRad = pose.theta,
                    shmThetaDeg = pose.theta * Mathf.Rad2Deg,
                    unityPosition = new Vector3(uX, uY, 0f),
                    unityRotationDeg = uThetaDeg,
                    lastUpdatedTime = currentTime
                };

                allDetectedRobots.Add(displayItem);

                // 監視対象ロボット (ID 0, 1, 2) の更新
                for (int m = 0; m < monitoredRobots.Length; m++)
                {
                    if (monitoredRobots[m].targetId == id)
                    {
                        monitoredRobots[m].isDetected = true;
                        monitoredRobots[m].shmPosition = displayItem.shmPosition;
                        monitoredRobots[m].shmThetaRad = displayItem.shmThetaRad;
                        monitoredRobots[m].shmThetaDeg = displayItem.shmThetaDeg;
                        monitoredRobots[m].unityPosition = displayItem.unityPosition;
                        monitoredRobots[m].unityRotationDeg = displayItem.unityRotationDeg;
                        monitoredRobots[m].lastUpdatedTime = currentTime;
                        detectedThisFrame[m] = true;

                        // オプション: Target Transform への反映
                        if (targetTransforms != null && m < targetTransforms.Count && targetTransforms[m] != null)
                        {
                            targetTransforms[m].localPosition = displayItem.unityPosition;
                            targetTransforms[m].localRotation = Quaternion.Euler(0, 0, displayItem.unityRotationDeg);
                        }
                    }
                }
            }

            for (int m = 0; m < monitoredRobots.Length; m++)
            {
                if (!detectedThisFrame[m] && (currentTime - monitoredRobots[m].lastUpdatedTime > LostTimeoutSeconds))
                {
                    monitoredRobots[m].isDetected = false;
                }
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!drawGizmos || monitoredRobots == null) return;

            for (int i = 0; i < monitoredRobots.Length; i++)
            {
                var robot = monitoredRobots[i];
                if (!robot.isDetected) continue;

                Color col = (robotColors != null && i < robotColors.Length) ? robotColors[i] : Color.yellow;
                Gizmos.color = col;

                Vector3 pos = transform.TransformPoint(robot.unityPosition);
                Gizmos.DrawWireSphere(pos, gizmoRadius);

                // 進行方向の矢印
                Vector3 forward = Quaternion.Euler(0, 0, robot.unityRotationDeg) * Vector3.up * gizmoArrowLength;
                Gizmos.DrawLine(pos, pos + forward);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(pos + Vector3.up * (gizmoRadius + 0.2f), $"Robot {robot.targetId}\n({robot.shmPosition.x:F2}, {robot.shmPosition.y:F2})");
#endif
            }
        }

        #endregion
    }
}
