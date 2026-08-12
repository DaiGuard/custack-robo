using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Custack
{
    #region Shared Memory Data Structures (Layout must match C++ exactly)

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SharedRobotPose
    {
        public int id;          // AprilTag ID
        public float x;         // 投影面座標 -1.0 ~ 1.0
        public float y;         // 投影面座標 -1.0 ~ 1.0
        public float theta;     // 回転角 (radian)
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct SharedRobotPoseData
    {
        public const int MaxRobots = 32;
        public const uint CurrentVersion = 1;

        public uint version;
        public uint sequence;
        public ulong timestampNs;
        public uint count;
        public fixed byte posesRaw[MaxRobots * 16]; // 32 * sizeof(SharedRobotPose)

        public SharedRobotPose GetPose(int index)
        {
            if (index < 0 || index >= MaxRobots) return default;
            fixed (byte* ptr = posesRaw)
            {
                SharedRobotPose* posesPtr = (SharedRobotPose*)ptr;
                return posesPtr[index];
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SingleControllerCommand
    {
        public short vx;           // 移動速度 Vx (-1000 ~ 1000)
        public short vy;           // 移動速度 Vy (-1000 ~ 1000)
        public short omega;        // 旋回速度 Omega (-1000 ~ 1000)
        public byte armRight;      // 右アーム起動フラグ (0:停止, 1:起動)
        public byte armLeft;       // 左アーム起動フラグ (0:停止, 1:起動)
        public byte active;        // コントローラ有効フラグ (0:無効, 1:有効)
        public byte reserved;      // パディング
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct SharedControllerData
    {
        public const int MaxControllers = 8;
        public const uint CurrentVersion = 1;

        public uint version;
        public uint sequence;
        public ulong timestampMs;
        public uint count;
        public fixed byte controllersRaw[MaxControllers * 8]; // 8 * sizeof(SingleControllerCommand)

        public void SetController(int index, SingleControllerCommand cmd)
        {
            if (index < 0 || index >= MaxControllers) return;
            fixed (byte* ptr = controllersRaw)
            {
                SingleControllerCommand* cmdPtr = (SingleControllerCommand*)ptr;
                cmdPtr[index] = cmd;
            }
        }
    }

    #endregion

    public class SharedMemoryManager : MonoBehaviour
    {
        public static SharedMemoryManager Instance { get; private set; }

        [Header("共有メモリ設定")]
        public string poseShmPath = "/dev/shm/custack_robot_poses";
        public string cmdShmPath = "/dev/shm/custack_controller_cmd";

        [Header("ロボット3台分の位置姿勢モニター (Inspector Debug)")]
        [SerializeField]
        private RobotPoseDisplay[] monitoredRobots = new RobotPoseDisplay[3]
        {
            new RobotPoseDisplay { targetId = 0 },
            new RobotPoseDisplay { targetId = 1 },
            new RobotPoseDisplay { targetId = 2 }
        };

        [Header("プレイヤーオブジェクト (0: ID 0, 1: ID 1, 2: ID 2)")]
        public List<GameObject> playerObjects;

        [Header("外れ値除去フィルタ設定")]
        public int historySize = 5;
        public float zScoreThreshold = 2.0f;

        [Header("投影カメラ設定")]
        public Camera projectionCamera;
        [SerializeField]
        private Vector2 positionScale = Vector2.one;

        [Header("コントローラ感度 & 設定")]
        public float maxSpeed = 1000f;       // Vx, Vy の最大スケール
        public float maxOmega = 1000f;       // 旋回速度の最大スケール
        public float sendRateHz = 50f;       // 送信周期

        // 初期ポーズ保存用
        private List<Vector3> initialPositions = new List<Vector3>();
        private List<Quaternion> initialRotations = new List<Quaternion>();

        // 履歴バッファ
        private List<List<float>> xHistories = new List<List<float>>();
        private List<List<float>> yHistories = new List<List<float>>();
        private List<List<float>> thetaHistories = new List<List<float>>();

        // 共有メモリアクセス用
        private FileStream poseFileStream;
        private FileStream cmdFileStream;
        private byte[] poseBuffer;
        private byte[] cmdBuffer;
        private uint cmdSequence = 0;

        // 周波数計測用
        private int poseFrameCount = 0;
        private float statTimer = 0f;
        private float sendTimer = 0f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            poseBuffer = new byte[Marshal.SizeOf<SharedRobotPoseData>()];
            cmdBuffer = new byte[Marshal.SizeOf<SharedControllerData>()];

            // プレイヤー履歴初期化
            foreach (var player in playerObjects)
            {
                xHistories.Add(new List<float>());
                yHistories.Add(new List<float>());
                thetaHistories.Add(new List<float>());

                if (player != null)
                {
                    initialPositions.Add(player.transform.localPosition);
                    initialRotations.Add(player.transform.localRotation);
                }
                else
                {
                    initialPositions.Add(Vector3.zero);
                    initialRotations.Add(Quaternion.identity);
                }
            }
        }

        void Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 200;

            InitSharedMemory();
        }

        void InitSharedMemory()
        {
            try
            {
                // ポーズ受信用共有メモリを開く
                if (File.Exists(poseShmPath))
                {
                    poseFileStream = new FileStream(poseShmPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    Debug.Log($"[SharedMemoryManager] Successfully opened Pose SHM: {poseShmPath}");
                }
                else
                {
                    Debug.LogWarning($"[SharedMemoryManager] Pose SHM not found at {poseShmPath}. (Will retry in Update)");
                }

                // コマンド送信用共有メモリを開く (なければ作成)
                cmdFileStream = new FileStream(cmdShmPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                if (cmdFileStream.Length < Marshal.SizeOf<SharedControllerData>())
                {
                    cmdFileStream.SetLength(Marshal.SizeOf<SharedControllerData>());
                }
                Debug.Log($"[SharedMemoryManager] Successfully opened Command SHM: {cmdShmPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SharedMemoryManager] Error initializing SHM: {ex.Message}");
            }
        }

        void Update()
        {
            // 共有メモリ未接続時の再試行
            if (poseFileStream == null && File.Exists(poseShmPath))
            {
                try
                {
                    poseFileStream = new FileStream(poseShmPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    Debug.Log($"[SharedMemoryManager] Connected to Pose SHM: {poseShmPath}");
                }
                catch { }
            }

            // --- 1. ロボット位置の読み取り & 反映 ---
            ReadRobotPosesFromShm();

            // --- 2. コントローラ入力の定期送信 (50Hz) ---
            sendTimer += Time.deltaTime;
            if (sendTimer >= 1.0f / sendRateHz)
            {
                sendTimer = 0f;
                WriteControllerCommandsToShm();
            }

            // 周波数統計出力 (1秒ごと)
            statTimer += Time.deltaTime;
            if (statTimer >= 1.0f)
            {
                Debug.Log($"[SHM Frequency] Pose Ingestion: {poseFrameCount} Hz");
                poseFrameCount = 0;
                statTimer -= 1.0f;
            }
        }

        #region Pose Ingestion (Shared Memory -> Unity)

        private unsafe void ReadRobotPosesFromShm()
        {
            if (poseFileStream == null) return;

            SharedRobotPoseData data = default;
            bool readSuccess = false;

            // Seqlock 読み取り
            for (int retry = 0; retry < 5; ++retry)
            {
                poseFileStream.Seek(0, SeekOrigin.Begin);
                int bytesRead = poseFileStream.Read(poseBuffer, 0, poseBuffer.Length);
                if (bytesRead < poseBuffer.Length) break;

                fixed (byte* ptr = poseBuffer)
                {
                    SharedRobotPoseData* shmData = (SharedRobotPoseData*)ptr;
                    uint seq = shmData->sequence;
                    // 奇数は書き込み中なのでスキップ
                    if ((seq & 1) != 0 || seq == 0) continue;

                    data = *shmData;
                    if (data.sequence == seq)
                    {
                        readSuccess = true;
                        break;
                    }
                }
            }

            if (!readSuccess || data.count == 0) return;
            poseFrameCount++;

            // カメラの平行投影スケール計算
            if (projectionCamera != null && projectionCamera.orthographic)
            {
                positionScale.y = projectionCamera.orthographicSize;
                positionScale.x = projectionCamera.orthographicSize * projectionCamera.aspect;
            }

            float currentTime = Time.time;
            bool[] detectedThisFrame = new bool[3];

            // 各ロボットの位置姿勢を更新
            for (int i = 0; i < data.count && i < SharedRobotPoseData.MaxRobots; i++)
            {
                SharedRobotPose pose = data.GetPose(i);
                int id = pose.id;

                // インスペクター用モニター情報の更新
                for (int m = 0; m < monitoredRobots.Length; m++)
                {
                    if (monitoredRobots[m].targetId == id)
                    {
                        monitoredRobots[m].isDetected = true;
                        monitoredRobots[m].shmPosition = new Vector2(pose.x, pose.y);
                        monitoredRobots[m].shmThetaRad = pose.theta;
                        monitoredRobots[m].shmThetaDeg = pose.theta * Mathf.Rad2Deg;
                        monitoredRobots[m].unityPosition = new Vector3(pose.x * positionScale.x, -pose.y * positionScale.y, 0f);
                        monitoredRobots[m].unityRotationDeg = -pose.theta * Mathf.Rad2Deg;
                        monitoredRobots[m].lastUpdatedTime = currentTime;
                        detectedThisFrame[m] = true;
                    }
                }

                if (id >= 0 && id < playerObjects.Count)
                {
                    GameObject player = playerObjects[id];
                    if (player == null) continue;

                    Vector3 initialPos = initialPositions[id];
                    Quaternion initialRot = initialRotations[id];

                    // ROS座標系 -> Unity座標系
                    float targetX = pose.x * positionScale.x;
                    float targetY = -pose.y * positionScale.y; // Y軸反転
                    float targetTheta = -pose.theta * Mathf.Rad2Deg;

                    // 履歴追加 & 外れ値除去
                    AddHistory(xHistories[id], targetX, historySize);
                    AddHistory(yHistories[id], targetY, historySize);
                    AddAngleHistory(thetaHistories[id], targetTheta, historySize);

                    float filteredX = GetFilteredMean(xHistories[id], zScoreThreshold);
                    float filteredY = GetFilteredMean(yHistories[id], zScoreThreshold);
                    float filteredTheta = GetFilteredMean(thetaHistories[id], zScoreThreshold);

                    player.transform.localPosition = initialPos + new Vector3(filteredX, filteredY, 0);
                    player.transform.localRotation = initialRot * Quaternion.Euler(0, 0, filteredTheta);
                }
            }

            for (int m = 0; m < monitoredRobots.Length; m++)
            {
                if (!detectedThisFrame[m] && (currentTime - monitoredRobots[m].lastUpdatedTime > 0.5f))
                {
                    monitoredRobots[m].isDetected = false;
                }
            }
        }

        #endregion

        #region Controller Command Writing (Unity -> Shared Memory)

        private unsafe void WriteControllerCommandsToShm()
        {
            if (cmdFileStream == null) return;

            SharedControllerData cmdData = default;
            cmdData.version = SharedControllerData.CurrentVersion;
            cmdData.timestampMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            cmdData.count = 3; // 3台のコントローラ

            // Gamepad.all から各コントローラの入力を取得
            var gamepads = Gamepad.all;

            for (int i = 0; i < 3; i++)
            {
                SingleControllerCommand cmd = default;

                if (i < gamepads.Count && gamepads[i] != null)
                {
                    var pad = gamepads[i];

                    // 左スティック: 移動 (Vx: 左右, Vy: 前後)
                    Vector2 move = pad.leftStick.ReadValue();
                    // 右スティック: 旋回 (Omega: 左右)
                    Vector2 look = pad.rightStick.ReadValue();

                    cmd.vx = (short)Mathf.Clamp(move.x * maxSpeed, -1000f, 1000f);
                    cmd.vy = (short)Mathf.Clamp(move.y * maxSpeed, -1000f, 1000f);
                    cmd.omega = (short)Mathf.Clamp(look.x * maxOmega, -1000f, 1000f);

                    // ボタン: 右アーム (RB / rightShoulder), 左アーム (LB / leftShoulder)
                    cmd.armRight = (byte)(pad.rightShoulder.isPressed ? 1 : 0);
                    cmd.armLeft = (byte)(pad.leftShoulder.isPressed ? 1 : 0);
                    cmd.active = 1;
                }
                else
                {
                    // 該当コントローラ未接続時は 0 (無効)
                    cmd.active = 0;
                }

                cmdData.SetController(i, cmd);
            }

            // Seqlock シーケンス更新 (偶数維持)
            cmdSequence += 2;
            cmdData.sequence = cmdSequence;

            // 共有メモリへ書き込み
            fixed (byte* ptr = cmdBuffer)
            {
                Marshal.StructureToPtr(cmdData, (IntPtr)ptr, false);
            }

            cmdFileStream.Seek(0, SeekOrigin.Begin);
            cmdFileStream.Write(cmdBuffer, 0, cmdBuffer.Length);
            cmdFileStream.Flush();
        }

        #endregion

        #region Outlier Filter Helpers

        private void AddHistory(List<float> history, float value, int maxSize)
        {
            history.Add(value);
            if (history.Count > maxSize) history.RemoveAt(0);
        }

        private void AddAngleHistory(List<float> history, float newAngle, int maxSize)
        {
            if (history.Count > 0)
            {
                float lastAngle = history[history.Count - 1];
                float diff = Mathf.DeltaAngle(lastAngle, newAngle);
                newAngle = lastAngle + diff;
            }
            AddHistory(history, newAngle, maxSize);
        }

        private float GetFilteredMean(List<float> history, float threshold)
        {
            if (history.Count == 0) return 0f;
            if (history.Count < 3) return history[history.Count - 1];

            List<float> sorted = new List<float>(history);
            sorted.Sort();
            float median = sorted[sorted.Count / 2];
            if (sorted.Count % 2 == 0)
            {
                median = (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2f;
            }

            float sum = 0f;
            foreach (float val in history) sum += val;
            float mean = sum / history.Count;

            float sqDiffSum = 0f;
            foreach (float val in history) sqDiffSum += (val - mean) * (val - mean);
            float standardDeviation = Mathf.Sqrt(sqDiffSum / history.Count);

            if (standardDeviation < 0.0001f) return median;

            float filteredSum = 0f;
            int filteredCount = 0;
            foreach (float val in history)
            {
                if (Mathf.Abs(val - median) <= standardDeviation * threshold)
                {
                    filteredSum += val;
                    filteredCount++;
                }
            }

            if (filteredCount == 0) return median;
            return filteredSum / filteredCount;
        }

        #endregion

        void OnDestroy()
        {
            poseFileStream?.Dispose();
            cmdFileStream?.Dispose();
        }
    }
}
