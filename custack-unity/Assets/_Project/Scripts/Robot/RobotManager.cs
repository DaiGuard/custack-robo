using System.Collections.Generic;
using UnityEngine;
using Custack.Input;
using Custack.SharedMemory;
using Custack.Terrain;

namespace Custack.Robot
{
    /// <summary>
    /// ロボット全機（P1, P2 等）の共有メモリ同期、入力配信、補正後コマンド送信を統括するマネージャー。
    /// </summary>
    public class RobotManager : MonoBehaviour
    {
        public static RobotManager Instance { get; private set; }

        [Header("管理ロボット一覧 (要素0: P1 / AprilTag 0, 要素1: P2 / AprilTag 1)")]
        public List<RobotEntity> robots = new List<RobotEntity>();

        [Header("コンポーネント参照")]
        public ProjectionScaler projectionScaler;
        public TerrainManager terrainManager;

        [Header("共有メモリ設定")]
        public string poseShmPath = "/dev/shm/custack_robot_poses";
        public string cmdShmPath = "/dev/shm/custack_controller_cmd";
        public float sendRateHz = 50f;

        private SharedMemoryReader shmReader;
        private SharedMemoryWriter shmWriter;
        private SingleControllerCommand[] commandOutputBuffer = new SingleControllerCommand[8];
        private float sendTimer = 0f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (projectionScaler == null) projectionScaler = GetComponent<ProjectionScaler>();
            if (projectionScaler == null) projectionScaler = gameObject.AddComponent<ProjectionScaler>();
            if (terrainManager == null) terrainManager = TerrainManager.Instance;
        }

        void Start()
        {
            shmReader = new SharedMemoryReader(poseShmPath);
            shmWriter = new SharedMemoryWriter(cmdShmPath);

            SetupTargetLists();
        }

        private void SetupTargetLists()
        {
            // 各ロボットのホーミングターゲット候補に相手機体を設定
            for (int i = 0; i < robots.Count; i++)
            {
                if (robots[i] == null) continue;
                robots[i].potentialTargets.Clear();
                for (int j = 0; j < robots.Count; j++)
                {
                    if (i != j && robots[j] != null)
                    {
                        robots[i].potentialTargets.Add(robots[j]);
                    }
                }
            }
        }

        void Update()
        {
            // 1. 共有メモリからロボット位置姿勢を読み出し & 反映
            ReadAndApplyPoses();

            // 2. コントローラー入力の取得 & 各ロボットのフレーム処理 (武器発射・地形補正)
            ProcessRobotInputs();

            // 3. 補正後コマンドを共有メモリへ 50Hz 周期で送信
            sendTimer += Time.deltaTime;
            if (sendTimer >= 1.0f / sendRateHz)
            {
                sendTimer = 0f;
                SendCommandsToShm();
            }
        }

        private void ReadAndApplyPoses()
        {
            if (shmReader == null || !shmReader.ReadPoses(out SharedRobotPoseData poseData)) return;

            for (int i = 0; i < poseData.count && i < SharedRobotPoseData.MaxRobots; i++)
            {
                SharedRobotPose pose = poseData.GetPose(i);
                int id = pose.id;

                if (id >= 0 && id < robots.Count && robots[id] != null)
                {
                    Vector3 worldPos = projectionScaler.NormalizedToWorld(pose.x, pose.y);
                    float unityDeg = projectionScaler.NormalizedThetaToUnityDeg(pose.theta);
                    robots[id].ApplyPose(worldPos, unityDeg);
                }
            }
        }

        private void ProcessRobotInputs()
        {
            var inputMgr = ControllerInputManager.Instance;
            if (inputMgr == null) return;

            for (int i = 0; i < robots.Count; i++)
            {
                if (robots[i] == null) continue;

                PlayerInputCommand input = inputMgr.GetPlayerInput(i);
                robots[i].ProcessFrame(input, terrainManager);
            }
        }

        private void SendCommandsToShm()
        {
            if (shmWriter == null) return;

            int activeCount = Mathf.Min(robots.Count, commandOutputBuffer.Length);
            for (int i = 0; i < activeCount; i++)
            {
                if (robots[i] != null)
                {
                    commandOutputBuffer[i] = robots[i].CurrentCommand;
                }
                else
                {
                    commandOutputBuffer[i] = default;
                }
            }

            shmWriter.WriteCommands(commandOutputBuffer, activeCount);
        }

        void OnDestroy()
        {
            shmReader?.Dispose();
            shmWriter?.Dispose();
        }
    }
}
