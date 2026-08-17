using System.Collections.Generic;
using UnityEngine;
using Custack.Input;
using Custack.SharedMemory;
using Custack.Terrain;
using Custack.Equipment;
using Custack.Combat;

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
            // 1. 共有メモリからロボット位置姿勢 & デバイスIDを読み出し & 反映
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

                if (id < 0 || id >= SharedRobotPoseData.MaxRobots) continue;

                EnsureRobotSlot(id);

                if (id < robots.Count && robots[id] != null)
                {
                    Vector3 worldPos = projectionScaler.NormalizedToWorld(pose.x, pose.y);
                    float unityDeg = projectionScaler.NormalizedThetaToUnityDeg(pose.theta);
                    robots[id].ApplyPose(worldPos, unityDeg);

                    // 実機ハードウェアから取得したデバイスIDを自動反映
                    if (robots[id].EquipmentComponent != null)
                    {
                        robots[id].EquipmentComponent.SetFromHardware(pose.legId, pose.armRightId, pose.armLeftId);
                    }
                }
            }
        }

        /// <summary>
        /// 指定 ID のロボットスロットが存在することを確認し、なければ動的に生成・登録
        /// </summary>
        public RobotEntity EnsureRobotSlot(int id)
        {
            if (id < 0 || id >= SharedRobotPoseData.MaxRobots) return null;

            // リストのサイズを拡張
            while (robots.Count <= id)
            {
                robots.Add(null);
            }

            if (robots[id] == null)
            {
                // シーン内の既存オブジェクトを検索
                string searchName1 = $"Robot_P{id + 1}";
                string searchName2 = $"Robot_{id}";
                var existingObj = GameObject.Find(searchName1) ?? GameObject.Find(searchName2);
                if (existingObj != null && existingObj.TryGetComponent<RobotEntity>(out var existingEntity))
                {
                    robots[id] = existingEntity;
                }
                else
                {
                    // ランタイムで自動生成
                    robots[id] = CreateRuntimeRobot(id);
                }
                SetupTargetLists();
            }

            return robots[id];
        }

        private RobotEntity CreateRuntimeRobot(int id)
        {
            var robotsRoot = GameObject.Find("--- ROBOTS ---") ?? gameObject;
            var robotObj = new GameObject($"Robot_P{id + 1}");
            robotObj.transform.SetParent(robotsRoot.transform);
            robotObj.transform.position = Vector3.zero;

            var health = robotObj.AddComponent<Health>();
            var equip = robotObj.AddComponent<RobotEquipment>();
            var visual = robotObj.AddComponent<RobotVisual>();
            var entity = robotObj.AddComponent<RobotEntity>();

            entity.robotId = id;
            entity.playerName = $"Player {id + 1}";
            entity.maxSpeed = 1000f;
            entity.maxOmega = 1000f;

            Color robotColor = GetRobotColor(id);
            equip.SetEquipment(LegDeviceType.Omni, ArmDeviceType.Gatling, ArmDeviceType.Sword);

            // 1. 機体本体 (メイン Quad)
            var avatarChild = GameObject.CreatePrimitive(PrimitiveType.Quad);
            avatarChild.name = "Avatar";
            avatarChild.transform.SetParent(robotObj.transform);
            avatarChild.transform.localPosition = Vector3.zero;
            avatarChild.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            var avatarCol = avatarChild.GetComponent<Collider>();
            if (avatarCol != null) Destroy(avatarCol);

            var avatarShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            var mat = new Material(avatarShader) { color = robotColor };
            avatarChild.GetComponent<MeshRenderer>().material = mat;

            // 2. 進行方向インジケーター (上方向矢印)
            var arrowChild = GameObject.CreatePrimitive(PrimitiveType.Quad);
            arrowChild.name = "DirectionArrow";
            arrowChild.transform.SetParent(robotObj.transform);
            arrowChild.transform.localPosition = new Vector3(0, 0.35f, -0.01f);
            arrowChild.transform.localScale = new Vector3(0.35f, 0.35f, 1f);
            arrowChild.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            var arrowCol = arrowChild.GetComponent<Collider>();
            if (arrowCol != null) Destroy(arrowCol);
            arrowChild.GetComponent<MeshRenderer>().material = new Material(avatarShader) { color = Color.white };

            // 3. 頭上 HP バー
            var hpBg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hpBg.name = "HpBar_Bg";
            hpBg.transform.SetParent(robotObj.transform);
            hpBg.transform.localPosition = new Vector3(0, 0.75f, -0.01f);
            hpBg.transform.localScale = new Vector3(1.0f, 0.15f, 1f);
            var hpBgCol = hpBg.GetComponent<Collider>();
            if (hpBgCol != null) Destroy(hpBgCol);
            hpBg.GetComponent<MeshRenderer>().material = new Material(avatarShader) { color = new Color(0.1f, 0.1f, 0.1f, 0.8f) };

            var hpFill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hpFill.name = "HpBar_Fill";
            hpFill.transform.SetParent(hpBg.transform);
            hpFill.transform.localPosition = Vector3.zero;
            hpFill.transform.localScale = new Vector3(0.95f, 0.8f, 1f);
            var hpFillCol = hpFill.GetComponent<Collider>();
            if (hpFillCol != null) Destroy(hpFillCol);
            hpFill.GetComponent<MeshRenderer>().material = new Material(avatarShader) { color = new Color(0.2f, 1.0f, 0.3f, 1f) };

            visual.hpBarFillTransform = hpFill.transform;
            visual.Initialize(id, robotColor);

            // 4. マズルポイント
            var muzzleRObj = new GameObject("Muzzle_R");
            muzzleRObj.transform.SetParent(robotObj.transform);
            muzzleRObj.transform.localPosition = new Vector3(0.5f, 0.5f, 0);
            entity.rightMuzzlePoint = muzzleRObj.transform;

            var muzzleLObj = new GameObject("Muzzle_L");
            muzzleLObj.transform.SetParent(robotObj.transform);
            muzzleLObj.transform.localPosition = new Vector3(-0.5f, 0.5f, 0);
            entity.leftMuzzlePoint = muzzleLObj.transform;

            // 5. 衝突判定
            var col2d = robotObj.AddComponent<CircleCollider2D>();
            col2d.radius = 0.45f;
            col2d.isTrigger = false;

            var rb = robotObj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            Debug.Log($"<color=#00FF88><b>[RobotManager]</b></color> 🤖 ロボット [ID: {id}] ({entity.playerName}) を動的生成・追従開始しました。");
            return entity;
        }

        public static Color GetRobotColor(int id)
        {
            // 視認性の高い 16 色パレット
            Color[] palette = new Color[]
            {
                new Color(0.10f, 0.85f, 1.00f), // 0: P1 (シアン)
                new Color(1.00f, 0.35f, 0.35f), // 1: P2 (レッド/コーラル)
                new Color(0.20f, 1.00f, 0.40f), // 2: エメラルドグリーン
                new Color(1.00f, 0.85f, 0.10f), // 3: イエローゴールド
                new Color(0.80f, 0.40f, 1.00f), // 4: パープル
                new Color(1.00f, 0.55f, 0.10f), // 5: オレンジ
                new Color(0.20f, 0.60f, 1.00f), // 6: ロイヤルブルー
                new Color(1.00f, 0.20f, 0.70f), // 7: マゼンタピンク
                new Color(0.00f, 1.00f, 0.80f), // 8: ターコイズ
                new Color(0.70f, 1.00f, 0.20f), // 9: ライムグリーン
                new Color(1.00f, 0.70f, 0.80f), // 10: ライトピンク
                new Color(0.50f, 0.80f, 1.00f), // 11: スカイブルー
                new Color(0.90f, 0.50f, 0.30f), // 12: テラコッタ
                new Color(0.60f, 0.90f, 0.60f), // 13: セージグリーン
                new Color(0.90f, 0.90f, 0.30f), // 14: レモンイエロー
                new Color(0.90f, 0.60f, 0.90f), // 15: ラベンダー
            };

            if (id >= 0 && id < palette.Length) return palette[id];
            return Color.HSVToRGB((id * 0.618033988749895f) % 1.0f, 0.85f, 1.0f);
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
