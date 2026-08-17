using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Custack.Robot;
using Custack.Input;
using Custack.Equipment;
using Custack.Combat;
using Custack.SharedMemory;
using Custack.Terrain;
using Custack.Core;
using Custack.UI;

namespace Custack.Editor
{
    /// <summary>
    /// CuStack-Robo の Unity シーンおよび Prefab をワンクリックで完全セットアップするエディタ拡張。
    /// </summary>
    public static class SceneSetupHelper
    {
        [MenuItem("CuStack/1. Setup Sandbox Scene (共有メモリ & 入力テスト環境構築)", priority = 1)]
        public static void SetupSandboxScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Camera & Lighting (Multi-Display: Display 1 HostDashboard, Display 2 Projection)
            var (dashCam, projCam) = SetupMultiDisplayCameras();

            // 2. Arena Floor
            CreateArenaFloor(10f, 7.5f);

            // 3. Managers
            var managersRoot = new GameObject("--- MANAGERS ---");

            var dispMgrObj = new GameObject("MultiDisplayManager");
            dispMgrObj.transform.SetParent(managersRoot.transform);
            var dispMgr = dispMgrObj.AddComponent<MultiDisplayManager>();
            dispMgr.hostDashboardCamera = dashCam;
            dispMgr.projectionCamera = projCam;

            var dashUIObj = new GameObject("HostDashboardUI");
            dashUIObj.transform.SetParent(managersRoot.transform);
            dashUIObj.AddComponent<HostDashboardUI>();

            var inputMgrObj = new GameObject("ControllerInputManager");
            inputMgrObj.transform.SetParent(managersRoot.transform);
            var inputMgr = inputMgrObj.AddComponent<ControllerInputManager>();
            inputMgr.enableKeyboardFallback = true;

            var terrainMgrObj = new GameObject("TerrainManager");
            terrainMgrObj.transform.SetParent(managersRoot.transform);
            var terrainMgr = terrainMgrObj.AddComponent<TerrainManager>();

            var robotMgrObj = new GameObject("RobotManager");
            robotMgrObj.transform.SetParent(managersRoot.transform);
            var robotMgr = robotMgrObj.AddComponent<RobotManager>();
            var scaler = robotMgrObj.AddComponent<ProjectionScaler>();
            scaler.projectionCamera = projCam;
            robotMgr.projectionScaler = scaler;
            robotMgr.terrainManager = terrainMgr;
            robotMgr.poseShmPath = "/dev/shm/custack_robot_poses";
            robotMgr.cmdShmPath = "/dev/shm/custack_controller_cmd";
            robotMgr.sendRateHz = 50f;

            var monitorObj = new GameObject("SharedMemoryDebugMonitor");
            monitorObj.transform.SetParent(managersRoot.transform);
            var monitor = monitorObj.AddComponent<SharedMemoryDebugMonitor>();
            monitor.shmPath = "/dev/shm/custack_robot_poses";
            monitor.showOverlay = true;

            // 4. Robots (IDs 0 ~ 15)
            var robotsRoot = new GameObject("--- ROBOTS ---");
            var robotEntities = new List<RobotEntity>();

            for (int i = 0; i < 16; i++)
            {
                Vector3 pos;
                if (i == 0) pos = new Vector3(-2.5f, 0, 0);
                else if (i == 1) pos = new Vector3(2.5f, 0, 0);
                else
                {
                    // 待機位置 (グリッド状に配置)
                    int row = (i - 2) / 7;
                    int col = (i - 2) % 7;
                    float px = -4.5f + col * 1.5f;
                    float py = (row == 0) ? -3.0f : 3.0f;
                    pos = new Vector3(px, py, 0);
                }

                Color colVal = RobotManager.GetRobotColor(i);
                var robotObj = CreateRobot(i, $"Robot_P{i + 1}", pos, colVal,
                                           LegDeviceType.Omni, ArmDeviceType.Gatling, ArmDeviceType.Sword);
                robotObj.transform.SetParent(robotsRoot.transform);
                robotEntities.Add(robotObj.GetComponent<RobotEntity>());
            }

            robotMgr.robots = robotEntities;

            // 5. シーン保存
            string scenePath = "Assets/_Project/Scenes/Sandbox/SharedMemorySandbox.unity";
            EnsureDirectory("Assets/_Project/Scenes/Sandbox");
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=#00FF88><b>[CuStack]</b></color> ✅ <b>{scenePath}</b> のセットアップが完了しました！ (16台のロボット P1~P16 を配置)");
        }

        [MenuItem("CuStack/2. Setup Main Battle Scene (本番対戦 & プロジェクション環境構築)", priority = 2)]
        public static void SetupMainBattleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Camera & Lighting (Multi-Display: Display 1 HostDashboard, Display 2 Projection)
            var (dashCam, projCam) = SetupMultiDisplayCameras();

            // 2. Arena Floor
            CreateArenaFloor(12f, 9f);

            // 3. Managers Root
            var managersRoot = new GameObject("--- MANAGERS ---");

            var dispMgrObj = new GameObject("MultiDisplayManager");
            dispMgrObj.transform.SetParent(managersRoot.transform);
            var dispMgr = dispMgrObj.AddComponent<MultiDisplayManager>();
            dispMgr.hostDashboardCamera = dashCam;
            dispMgr.projectionCamera = projCam;

            var dashUIObj = new GameObject("HostDashboardUI");
            dashUIObj.transform.SetParent(managersRoot.transform);
            dashUIObj.AddComponent<HostDashboardUI>();

            var inputMgrObj = new GameObject("ControllerInputManager");
            inputMgrObj.transform.SetParent(managersRoot.transform);
            var inputMgr = inputMgrObj.AddComponent<ControllerInputManager>();

            var terrainMgrObj = new GameObject("TerrainManager");
            terrainMgrObj.transform.SetParent(managersRoot.transform);
            var terrainMgr = terrainMgrObj.AddComponent<TerrainManager>();

            var robotMgrObj = new GameObject("RobotManager");
            robotMgrObj.transform.SetParent(managersRoot.transform);
            var robotMgr = robotMgrObj.AddComponent<RobotManager>();
            var scaler = robotMgrObj.AddComponent<ProjectionScaler>();
            scaler.projectionCamera = projCam;
            robotMgr.projectionScaler = scaler;
            robotMgr.terrainManager = terrainMgr;
            robotMgr.poseShmPath = "/dev/shm/custack_robot_poses";
            robotMgr.cmdShmPath = "/dev/shm/custack_controller_cmd";

            var monitorObj = new GameObject("SharedMemoryDebugMonitor");
            monitorObj.transform.SetParent(managersRoot.transform);
            var monitor = monitorObj.AddComponent<SharedMemoryDebugMonitor>();
            monitor.shmPath = "/dev/shm/custack_robot_poses";

            var gameMgrObj = new GameObject("GameManager");
            gameMgrObj.transform.SetParent(managersRoot.transform);
            var gameMgr = gameMgrObj.AddComponent<GameManager>();

            // 4. Terrains
            var terrainsRoot = new GameObject("--- TERRAINS ---");
            CreateTerrainZone("MudZone", new Vector3(-3.5f, -2.2f, 0), new Vector2(3f, 2.5f), TerrainType.Mud, terrainsRoot.transform);
            CreateTerrainZone("IceZone", new Vector3(3.5f, 2.2f, 0), new Vector2(3f, 2.5f), TerrainType.Ice, terrainsRoot.transform);
            CreateTerrainZone("LavaZone", new Vector3(0f, 0f, 0), new Vector2(2.5f, 2.5f), TerrainType.Lava, terrainsRoot.transform);
            CreateTerrainZone("ForestZone", new Vector3(3.5f, -2.2f, 0), new Vector2(3f, 2.5f), TerrainType.Forest, terrainsRoot.transform);

            // 5. Robots (IDs 0 ~ 15)
            var robotsRoot = new GameObject("--- ROBOTS ---");
            var robotEntities = new List<RobotEntity>();

            for (int i = 0; i < 16; i++)
            {
                Vector3 pos;
                if (i == 1) pos = new Vector3(-3.5f, 0, 0);       // Tag ID 1: P1 (左) -> Bridge 1
                else if (i == 2) pos = new Vector3(3.5f, 0, 0);  // Tag ID 2: P2 (右) -> Bridge 2
                else if (i == 3) pos = new Vector3(0f, 2.5f, 0);  // Tag ID 3: P3 (上中央) -> Bridge 3
                else if (i == 0) pos = new Vector3(0f, -3.8f, 0); // Tag ID 0: 待機
                else
                {
                    // 待機位置 (アリーナ外周グリッド)
                    int idx = (i >= 4) ? (i - 4) : 0;
                    int row = idx / 6;
                    int col = idx % 6;
                    float px = -4.0f + col * 1.6f;
                    float py = (row == 0) ? -3.8f : 3.8f;
                    pos = new Vector3(px, py, 0);
                }

                Color colVal = RobotManager.GetRobotColor(i);
                LegDeviceType defaultLeg = (i == 1) ? LegDeviceType.Omni : (i == 2) ? LegDeviceType.Tire : LegDeviceType.Crawler;
                ArmDeviceType defaultR = (i == 1) ? ArmDeviceType.Gatling : (i == 2) ? ArmDeviceType.Sword : ArmDeviceType.Cannon;
                ArmDeviceType defaultL = (i == 1) ? ArmDeviceType.Sword : (i == 2) ? ArmDeviceType.Gatling : ArmDeviceType.Sword;

                var robotObj = CreateRobot(i, $"Robot_P{i}", pos, colVal, defaultLeg, defaultR, defaultL);
                robotObj.transform.SetParent(robotsRoot.transform);
                robotEntities.Add(robotObj.GetComponent<RobotEntity>());
            }

            robotMgr.robots = robotEntities;
            gameMgr.player1 = robotEntities[1]; // Tag ID 1 (Bridge 1)
            gameMgr.player2 = robotEntities[2]; // Tag ID 2 (Bridge 2)

            // 6. Battle HUD (Canvas & UI)
            CreateBattleHUD(managersRoot.transform);

            // 7. シーン保存
            string scenePath = "Assets/_Project/Scenes/Main/Main.unity";
            EnsureDirectory("Assets/_Project/Scenes/Main");
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=#00FF88><b>[CuStack]</b></color> ✅ <b>{scenePath}</b> のマルチディスプレイセットアップが完了しました！ (Display 1: PC管理画面 / Display 2: プロジェクター床面投影)");
        }

        private static (Camera dashCam, Camera projCam) SetupMultiDisplayCameras()
        {
            // 1. Display 1: ホスト PC 管理ダッシュボード用カメラ
            var dashCamObj = new GameObject("HostDashboardCamera");
            var dashCam = dashCamObj.AddComponent<Camera>();
            dashCam.targetDisplay = 0; // Display 1 (メイン画面)
            dashCam.clearFlags = CameraClearFlags.SolidColor;
            dashCam.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 1f); // スタイリッシュなダークテーマ
            dashCam.depth = 0;
            dashCamObj.AddComponent<AudioListener>();

            // 2. Display 2: プロジェクター床面投影用カメラ (1:1 投影)
            var projCamObj = new GameObject("ProjectionCamera");
            projCamObj.tag = "MainCamera";
            var projCam = projCamObj.AddComponent<Camera>();
            projCam.targetDisplay = 1; // Display 2 (プロジェクター)
            projCam.orthographic = true;
            projCam.orthographicSize = 5.0f;
            projCam.transform.position = new Vector3(0, 0, -10f);
            projCam.backgroundColor = new Color(0.02f, 0.02f, 0.04f, 1f); // プロジェクター用黒背景
            projCam.clearFlags = CameraClearFlags.SolidColor;
            projCam.depth = 1;

            // 3. ライティング
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.98f, 0.95f);
            light.intensity = 1.2f;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0);

            return (dashCam, projCam);
        }

        private static void CreateArenaFloor(float width, float height)
        {
            var floorObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floorObj.name = "ArenaFloor";
            floorObj.transform.position = new Vector3(0, 0, 1f); // ロボットより奥 (Z=1)
            floorObj.transform.localScale = new Vector3(width, height, 1f);

            var col = floorObj.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            var mr = floorObj.GetComponent<MeshRenderer>();
            // 床面は完全な黒 (プロジェクター光を照射せず、AprilTag のコントラストを最大化)
            mr.sharedMaterial = GetOrCreateMaterial("Mat_ArenaFloor", Color.black);
        }

        private static T GetOrAddComponent<T>(GameObject obj) where T : Component
        {
            var comp = obj.GetComponent<T>();
            if (comp == null)
            {
                comp = obj.AddComponent<T>();
            }
            return comp;
        }

        private static GameObject CreateRobot(int id, string name, Vector3 pos, Color color,
                                             LegDeviceType leg, ArmDeviceType rArm, ArmDeviceType lArm)
        {
            var robotObj = new GameObject(name);
            robotObj.transform.position = pos;

            // Health, RobotEquipment, RobotVisual を安全に取得/追加
            var health = GetOrAddComponent<Health>(robotObj);
            var equip = GetOrAddComponent<RobotEquipment>(robotObj);
            var visual = GetOrAddComponent<RobotVisual>(robotObj);
            var entity = GetOrAddComponent<RobotEntity>(robotObj);

            entity.robotId = id;
            entity.playerName = $"Player {id + 1}";
            entity.maxSpeed = 1000f;
            entity.maxOmega = 1000f;

            equip.SetEquipment(leg, rArm, lArm);

            // 1. 外周ドーナツ型リング (ロボット本体・マーカ天面には光を当てず、外側の床面にのみプレイヤーカラー・被弾点滅を投影)
            // 内径 0.35m (直径70cm: ロボット本体を完全クリア), 外径 0.55m (直径110cm)
            var donutChild = new GameObject("DonutRing");
            donutChild.transform.SetParent(robotObj.transform);
            donutChild.transform.localPosition = Vector3.zero;
            var mfDonut = donutChild.AddComponent<MeshFilter>();
            mfDonut.sharedMesh = RobotMeshHelper.GetOrCreateDonutMesh(0.35f, 0.55f);
            var mrDonut = donutChild.AddComponent<MeshRenderer>();
            mrDonut.sharedMaterial = GetOrCreateMaterial($"Mat_Robot_{name}", color);

            visual.centerWhiteRenderer = null;
            visual.donutRenderer = mrDonut;

            // 2. ビジュアル初期化 (余計な矢印や文字を排除し、外周ドーナツリングのみで構成)
            visual.Initialize(id, color);

            // 3. 左右マズルポイント
            var muzzleRObj = new GameObject("Muzzle_R");
            muzzleRObj.transform.SetParent(robotObj.transform);
            muzzleRObj.transform.localPosition = new Vector3(0.5f, 0.5f, 0);
            entity.rightMuzzlePoint = muzzleRObj.transform;

            var muzzleLObj = new GameObject("Muzzle_L");
            muzzleLObj.transform.SetParent(robotObj.transform);
            muzzleLObj.transform.localPosition = new Vector3(-0.5f, 0.5f, 0);
            entity.leftMuzzlePoint = muzzleLObj.transform;

            // 4. 衝突判定
            var col2d = GetOrAddComponent<CircleCollider2D>(robotObj);
            col2d.radius = 0.45f;
            col2d.isTrigger = false;

            var rb = GetOrAddComponent<Rigidbody2D>(robotObj);
            rb.bodyType = RigidbodyType2D.Kinematic;

            return robotObj;
        }

        private static void CreateTerrainZone(string name, Vector3 pos, Vector2 size, TerrainType type, Transform parent)
        {
            var zoneObj = new GameObject(name);
            zoneObj.transform.SetParent(parent);
            zoneObj.transform.position = pos;

            var col = GetOrAddComponent<BoxCollider2D>(zoneObj);
            col.size = size;
            col.isTrigger = true;

            var zone = GetOrAddComponent<TerrainZone>(zoneObj);
            zone.terrainType = type;

            Color zoneColor = type switch
            {
                TerrainType.Mud => new Color(0.45f, 0.25f, 0.08f, 1.0f),      // 泥沼: 落ち着いたダークオレンジ枠線
                TerrainType.Ice => new Color(0.10f, 0.40f, 0.55f, 1.0f),      // 氷上: 落ち着いたシアン枠線
                TerrainType.Lava => new Color(0.55f, 0.12f, 0.05f, 1.0f),     // 溶岩: 落ち着いたダークレッド枠線
                TerrainType.Forest => new Color(0.08f, 0.45f, 0.15f, 1.0f),   // 森林: 落ち着いたダークグリーン枠線
                _ => new Color(0.3f, 0.3f, 0.3f, 1.0f)
            };

            // スタイリッシュな細い外枠境界線 (Line Quads: 幅 2cm) を描画
            var borderRoot = new GameObject("BorderLines");
            borderRoot.transform.SetParent(zoneObj.transform, false);
            borderRoot.transform.localPosition = new Vector3(0, 0, 0.5f);

            float lineWidth = 0.02f; // 幅 2cm の極細枠線 (カメラ干渉を最小化)
            var lineMat = GetOrCreateMaterial($"Mat_Terrain_Border_{type}", zoneColor, false);

            // 上枠
            CreateLineQuad("TopLine", borderRoot.transform, new Vector3(0, size.y / 2f, 0), new Vector3(size.x, lineWidth, 1f), lineMat);
            // 下枠
            CreateLineQuad("BottomLine", borderRoot.transform, new Vector3(0, -size.y / 2f, 0), new Vector3(size.x, lineWidth, 1f), lineMat);
            // 左枠
            CreateLineQuad("LeftLine", borderRoot.transform, new Vector3(-size.x / 2f, 0, 0), new Vector3(lineWidth, size.y, 1f), lineMat);
            // 右枠
            CreateLineQuad("RightLine", borderRoot.transform, new Vector3(size.x / 2f, 0, 0), new Vector3(lineWidth, size.y, 1f), lineMat);
        }

        private static void CreateLineQuad(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPos;
            quad.transform.localScale = scale;
            var col = quad.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void CreateBattleHUD(Transform parent)
        {
            var hudObj = new GameObject("BattleHUD");
            hudObj.transform.SetParent(parent);
            var hud = hudObj.AddComponent<BattleHUD>();

            // Canvas (Display 2: プロジェクター床面投影画面用 HUD)
            var canvasObj = new GameObject("BattleCanvas");
            canvasObj.transform.SetParent(hudObj.transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = 1; // Display 2 (プロジェクター)
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // P1 HP Bar UI (左上)
            var p1Panel = CreateUIPanel("P1_Panel", canvasObj.transform, new Vector2(20, -20), new Vector2(300, 60), new Vector2(0, 1), new Vector2(0, 1));
            var p1HpBg = CreateUIImage("P1_HpBg", p1Panel.transform, new Vector2(0, 10), new Vector2(280, 24), new Color(0.2f, 0.2f, 0.2f, 0.8f));
            var p1HpFill = CreateUIImage("P1_HpFill", p1HpBg.transform, Vector2.zero, new Vector2(276, 20), new Color(0.1f, 0.8f, 1f, 1f));
            p1HpFill.type = Image.Type.Filled;
            p1HpFill.fillMethod = Image.FillMethod.Horizontal;
            var p1HpText = CreateUIText("P1_HpText", p1Panel.transform, new Vector2(0, 10), new Vector2(280, 24), "P1 HP: 100 / 100", 14, Color.white);
            var p1EquipText = CreateUIText("P1_EquipText", p1Panel.transform, new Vector2(0, -18), new Vector2(280, 30), "Leg: Omni | [R]: Gatling [L]: Sword", 12, new Color(0.8f, 0.9f, 1f));

            hud.p1HpBarFill = p1HpFill;
            hud.p1HpText = p1HpText;
            hud.p1EquipmentText = p1EquipText;

            // P2 HP Bar UI (右上)
            var p2Panel = CreateUIPanel("P2_Panel", canvasObj.transform, new Vector2(-20, -20), new Vector2(300, 60), new Vector2(1, 1), new Vector2(1, 1));
            var p2HpBg = CreateUIImage("P2_HpBg", p2Panel.transform, new Vector2(0, 10), new Vector2(280, 24), new Color(0.2f, 0.2f, 0.2f, 0.8f));
            var p2HpFill = CreateUIImage("P2_HpFill", p2HpBg.transform, Vector2.zero, new Vector2(276, 20), new Color(1f, 0.35f, 0.35f, 1f));
            p2HpFill.type = Image.Type.Filled;
            p2HpFill.fillMethod = Image.FillMethod.Horizontal;
            var p2HpText = CreateUIText("P2_HpText", p2Panel.transform, new Vector2(0, 10), new Vector2(280, 24), "P2 HP: 100 / 100", 14, Color.white);
            var p2EquipText = CreateUIText("P2_EquipText", p2Panel.transform, new Vector2(0, -18), new Vector2(280, 30), "Leg: Tire | [R]: Cannon [L]: Gatling", 12, new Color(1f, 0.85f, 0.85f));

            hud.p2HpBarFill = p2HpFill;
            hud.p2HpText = p2HpText;
            hud.p2EquipmentText = p2EquipText;

            // プロジェクター床面への光漏れを防ぐため、P1/P2 パネルは非表示 (ホストPC管理画面で一括確認可能)
            p1Panel.SetActive(false);
            p2Panel.SetActive(false);

            // Winner Banner (中央)
            var winnerBanner = CreateUIPanel("WinnerBanner", canvasObj.transform, Vector2.zero, new Vector2(450, 100), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            CreateUIImage("BannerBg", winnerBanner.transform, Vector2.zero, new Vector2(450, 100), new Color(0, 0, 0, 0.85f));
            var winnerText = CreateUIText("WinnerText", winnerBanner.transform, Vector2.zero, new Vector2(450, 80), "VICTORY!", 32, new Color(1f, 0.85f, 0.1f));
            winnerText.alignment = TextAnchor.MiddleCenter;

            hud.winnerBannerObject = winnerBanner;
            hud.winnerText = winnerText;
            winnerBanner.SetActive(false);
        }

        private static GameObject CreateUIPanel(string name, Transform parent, Vector2 pos, Vector2 size, Vector2 anchorMin, Vector2 anchorMax)
        {
            var panelObj = new GameObject(name);
            panelObj.transform.SetParent(parent, false);
            var rt = panelObj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMin;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return panelObj;
        }

        private static Image CreateUIImage(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var imgObj = new GameObject(name);
            imgObj.transform.SetParent(parent, false);
            var rt = imgObj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = imgObj.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Text CreateUIText(string name, Transform parent, Vector2 pos, Vector2 size, string text, int fontSize, Color color)
        {
            var textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);
            var rt = textObj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var txt = textObj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.alignment = TextAnchor.MiddleLeft;
            return txt;
        }

        private static Material GetOrCreateMaterial(string matName, Color color, bool isTransparent = false)
        {
            EnsureDirectory("Assets/_Project/Materials/Rendering");
            string assetPath = $"Assets/_Project/Materials/Rendering/{matName}.mat";

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Sprites/Default")
                             ?? Shader.Find("Unlit/Color")
                             ?? Shader.Find("Standard");

                mat = new Material(shader);
                mat.name = matName;
                AssetDatabase.CreateAsset(mat, assetPath);
            }

            mat.color = color;
            if (isTransparent)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1);
                if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureDirectory(string dirPath)
        {
            if (!System.IO.Directory.Exists(dirPath))
            {
                System.IO.Directory.CreateDirectory(dirPath);
            }
        }
    }
}
