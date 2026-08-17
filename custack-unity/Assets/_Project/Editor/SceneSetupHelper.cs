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

            // 1. Camera & Lighting
            SetupCameraAndLighting();

            // 2. Arena Floor
            CreateArenaFloor(10f, 7.5f);

            // 3. Managers
            var managersRoot = new GameObject("--- MANAGERS ---");

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

            // 1. Camera & Lighting
            SetupCameraAndLighting();

            // 2. Arena Floor
            CreateArenaFloor(12f, 9f);

            // 3. Managers Root
            var managersRoot = new GameObject("--- MANAGERS ---");

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
                if (i == 0) pos = new Vector3(-3.5f, 0, 0);
                else if (i == 1) pos = new Vector3(3.5f, 0, 0);
                else
                {
                    // 待機位置 (アリーナ上下の待機グリッド)
                    int row = (i - 2) / 7;
                    int col = (i - 2) % 7;
                    float px = -4.5f + col * 1.5f;
                    float py = (row == 0) ? -3.8f : 3.8f;
                    pos = new Vector3(px, py, 0);
                }

                Color colVal = RobotManager.GetRobotColor(i);
                LegDeviceType defaultLeg = (i % 3 == 0) ? LegDeviceType.Omni : (i % 3 == 1) ? LegDeviceType.Tire : LegDeviceType.Crawler;
                ArmDeviceType defaultR = (i % 3 == 0) ? ArmDeviceType.Gatling : (i % 3 == 1) ? ArmDeviceType.Cannon : ArmDeviceType.Sword;
                ArmDeviceType defaultL = (i % 3 == 0) ? ArmDeviceType.Sword : (i % 3 == 1) ? ArmDeviceType.Gatling : ArmDeviceType.Cannon;

                var robotObj = CreateRobot(i, $"Robot_P{i + 1}", pos, colVal, defaultLeg, defaultR, defaultL);
                robotObj.transform.SetParent(robotsRoot.transform);
                robotEntities.Add(robotObj.GetComponent<RobotEntity>());
            }

            robotMgr.robots = robotEntities;
            gameMgr.player1 = robotEntities[0];
            gameMgr.player2 = robotEntities[1];

            // 6. Battle HUD (Canvas & UI)
            CreateBattleHUD(managersRoot.transform);

            // 7. シーン保存
            string scenePath = "Assets/_Project/Scenes/Main/Main.unity";
            EnsureDirectory("Assets/_Project/Scenes/Main");
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=#00FF88><b>[CuStack]</b></color> ✅ <b>{scenePath}</b> のセットアップが完了しました！ (16台のロボット P1~P16 を配置)");
        }

        private static void SetupCameraAndLighting()
        {
            var camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            var cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.0f;
            cam.transform.position = new Vector3(0, 0, -10f);
            cam.backgroundColor = new Color(0.08f, 0.10f, 0.15f, 1f); // 濃紺グレー背景
            cam.clearFlags = CameraClearFlags.SolidColor;
            camObj.AddComponent<AudioListener>();

            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.98f, 0.95f);
            light.intensity = 1.2f;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0);
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
            mr.sharedMaterial = GetOrCreateMaterial("Mat_ArenaFloor", new Color(0.12f, 0.14f, 0.18f, 1f));
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

            // 1. 機体本体 (メイン Quad: 直径 0.9m)
            var avatarChild = GameObject.CreatePrimitive(PrimitiveType.Quad);
            avatarChild.name = "Avatar";
            avatarChild.transform.SetParent(robotObj.transform);
            avatarChild.transform.localPosition = Vector3.zero;
            avatarChild.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

            var avatarCol = avatarChild.GetComponent<Collider>();
            if (avatarCol != null) Object.DestroyImmediate(avatarCol);

            var mr = avatarChild.GetComponent<MeshRenderer>();
            mr.sharedMaterial = GetOrCreateMaterial($"Mat_Robot_{name}", color);

            // 2. 進行方向・マズル向きインジケーター (上方向三角 Quad)
            var arrowChild = GameObject.CreatePrimitive(PrimitiveType.Quad);
            arrowChild.name = "DirectionArrow";
            arrowChild.transform.SetParent(robotObj.transform);
            arrowChild.transform.localPosition = new Vector3(0, 0.35f, -0.01f);
            arrowChild.transform.localScale = new Vector3(0.35f, 0.35f, 1f);
            arrowChild.transform.localRotation = Quaternion.Euler(0, 0, 45f); // ダイヤ型

            var arrowCol = arrowChild.GetComponent<Collider>();
            if (arrowCol != null) Object.DestroyImmediate(arrowCol);

            var arrowMr = arrowChild.GetComponent<MeshRenderer>();
            arrowMr.sharedMaterial = GetOrCreateMaterial("Mat_DirectionArrow", Color.white);

            // 3. 頭上 HP バー (黒背景 + 緑バー)
            var hpBg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hpBg.name = "HpBar_Bg";
            hpBg.transform.SetParent(robotObj.transform);
            hpBg.transform.localPosition = new Vector3(0, 0.75f, -0.01f);
            hpBg.transform.localScale = new Vector3(1.0f, 0.15f, 1f);
            var hpBgCol = hpBg.GetComponent<Collider>();
            if (hpBgCol != null) Object.DestroyImmediate(hpBgCol);
            hpBg.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial("Mat_HpBg", new Color(0.1f, 0.1f, 0.1f, 0.8f));

            var hpFill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            hpFill.name = "HpBar_Fill";
            hpFill.transform.SetParent(hpBg.transform);
            hpFill.transform.localPosition = Vector3.zero;
            hpFill.transform.localScale = new Vector3(0.95f, 0.8f, 1f);
            var hpFillCol = hpFill.GetComponent<Collider>();
            if (hpFillCol != null) Object.DestroyImmediate(hpFillCol);
            hpFill.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial("Mat_HpFill", new Color(0.2f, 1.0f, 0.3f, 1f));

            visual.hpBarFillTransform = hpFill.transform;
            visual.Initialize(id, color);

            // 4. 左右マズルポイント
            var muzzleRObj = new GameObject("Muzzle_R");
            muzzleRObj.transform.SetParent(robotObj.transform);
            muzzleRObj.transform.localPosition = new Vector3(0.5f, 0.5f, 0);
            entity.rightMuzzlePoint = muzzleRObj.transform;

            var muzzleLObj = new GameObject("Muzzle_L");
            muzzleLObj.transform.SetParent(robotObj.transform);
            muzzleLObj.transform.localPosition = new Vector3(-0.5f, 0.5f, 0);
            entity.leftMuzzlePoint = muzzleLObj.transform;

            // 5. 衝突判定
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

            // 視覚化用 Quad (Z=0.5: 床面とロボットの中間)
            var visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            visual.name = "Visual";
            visual.transform.SetParent(zoneObj.transform);
            visual.transform.localPosition = new Vector3(0, 0, 0.5f);
            visual.transform.localScale = new Vector3(size.x, size.y, 1f);

            var quadCol = visual.GetComponent<Collider>();
            if (quadCol != null) Object.DestroyImmediate(quadCol);

            Color zoneColor = type switch
            {
                TerrainType.Mud => new Color(0.50f, 0.32f, 0.15f, 0.6f),
                TerrainType.Ice => new Color(0.35f, 0.75f, 1.0f, 0.6f),
                TerrainType.Lava => new Color(1.0f, 0.25f, 0.08f, 0.7f),
                TerrainType.Forest => new Color(0.12f, 0.65f, 0.22f, 0.6f),
                _ => Color.white
            };

            var mr = visual.GetComponent<MeshRenderer>();
            mr.sharedMaterial = GetOrCreateMaterial($"Mat_Terrain_{type}", zoneColor, true);
        }

        private static void CreateBattleHUD(Transform parent)
        {
            var hudObj = new GameObject("BattleHUD");
            hudObj.transform.SetParent(parent);
            var hud = hudObj.AddComponent<BattleHUD>();

            // Canvas
            var canvasObj = new GameObject("BattleCanvas");
            canvasObj.transform.SetParent(hudObj.transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
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
