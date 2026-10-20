using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine.AI;
using Unity.Netcode;
using MoraeGames.Core;
using MoraeGames.Game;
using MoraeGames.Network;

namespace MoraeGames.Editor
{
    public class InGameSetupEditor : MonoBehaviour
    {
        [MenuItem("MoraeGames/Setup InGame Environment")]
        public static void SetupInGame()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            if (currentScene.name != "InGame")
            {
                Debug.Log("Loading InGame scene...");
                EditorSceneManager.OpenScene("Assets/MoraeGames/Scenes/InGame.unity");
                currentScene = SceneManager.GetActiveScene();
            }

            // Environment Root
            GameObject envRoot = GameObject.Find("Environment");
            if (envRoot != null) DestroyImmediate(envRoot);
            envRoot = new GameObject("Environment");
            NavMeshSurface surface = envRoot.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            // surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            // Material Setup (Procedural + Pipeline Agnostic)
            Material groundMat = CreateMaterial("Concrete", new Color(0.6f, 0.6f, 0.6f));
            Material wallMat = CreateMaterial("Brick", new Color(0.8f, 0.4f, 0.3f));
            Material prisonMat = CreateMaterial("Concrete", new Color(0.8f, 0.3f, 0.3f));
            Material obstacleMat = CreateMaterial("Brick", new Color(0.4f, 0.5f, 0.4f));

            // Ground
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(envRoot.transform);
            ground.transform.localScale = new Vector3(10, 1, 10); // 100x100 area
            ground.GetComponent<MeshRenderer>().material = groundMat;
            // Outer Walls
            CreateWall(envRoot.transform, "Wall_North", new Vector3(0, 2.5f, 50f), new Vector3(100, 5, 1), wallMat);
            CreateWall(envRoot.transform, "Wall_South", new Vector3(0, 2.5f, -50f), new Vector3(100, 5, 1), wallMat);
            CreateWall(envRoot.transform, "Wall_East", new Vector3(50f, 2.5f, 0), new Vector3(1, 5, 100), wallMat);
            CreateWall(envRoot.transform, "Wall_West", new Vector3(-50f, 2.5f, 0), new Vector3(1, 5, 100), wallMat);

            // Prison Area (Top Right corner)
            GameObject prisonRoot = new GameObject("PrisonArea");
            prisonRoot.transform.SetParent(envRoot.transform);

            GameObject prisonFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            prisonFloor.name = "PrisonFloor";
            prisonFloor.transform.SetParent(prisonRoot.transform);
            prisonFloor.transform.position = new Vector3(18f, 0.01f, 18f); // Slightly above ground to prevent Z-fighting
            prisonFloor.transform.localScale = new Vector3(1, 1, 1); // 10x10 area
            prisonFloor.GetComponent<MeshRenderer>().material = prisonMat;

            // Prison Walls
            CreateWall(prisonRoot.transform, "Prison_Wall_W", new Vector3(13f, 1.5f, 18f), new Vector3(0.5f, 3, 10), wallMat);
            CreateWall(prisonRoot.transform, "Prison_Wall_S", new Vector3(18f, 1.5f, 13f), new Vector3(10, 3, 0.5f), wallMat);
            // It uses the outer walls for North and East.

            // Obstacles
            GameObject obstacleRoot = new GameObject("Obstacles");
            obstacleRoot.transform.SetParent(envRoot.transform);
            for (int i = 0; i < 80; i++)
            {
                Vector3 pos = new Vector3(Random.Range(-45f, 45f), 1.5f, Random.Range(-45f, 45f));
                // Avoid prison area completely (13~23 area)
                if (pos.x > 8 && pos.x < 28 && pos.z > 8 && pos.z < 28) continue;

                GameObject obs = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obs.name = "Obstacle_" + i;
                obs.transform.SetParent(obstacleRoot.transform);
                obs.transform.position = pos;
                obs.transform.localScale = new Vector3(Random.Range(2f, 5f), Random.Range(2f, 4f), Random.Range(2f, 5f));
                obs.GetComponent<MeshRenderer>().material = obstacleMat;
            }

            // Spawn Points
            GameObject spawnRoot = GameObject.Find("SpawnPoints");
            if (spawnRoot != null) DestroyImmediate(spawnRoot);
            spawnRoot = new GameObject("SpawnPoints");

            // Prison Spawn
            CreateSpawnPoint(spawnRoot.transform, "PrisonSpawn", new Vector3(18f, 0.5f, 18f));

            // Cop Spawns (Center-ish)
            for (int i = 0; i < 3; i++)
            {
                CreateSpawnPoint(spawnRoot.transform, "CopSpawn_" + i, new Vector3(-2f + (i * 2f), 0.5f, 0f));
            }

            // Robber Spawns (Distributed around the map)
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2 / 6;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * 35f, 0.5f, Mathf.Sin(angle) * 35f);
                CreateSpawnPoint(spawnRoot.transform, "RobberSpawn_" + i, pos);
            }

            // Loot Spots (scattered around the map, avoiding prison area)
            GameObject lootRoot = GameObject.Find("LootSpots");
            if (lootRoot != null) DestroyImmediate(lootRoot);
            lootRoot = new GameObject("LootSpots");
            lootRoot.transform.SetParent(envRoot.transform);

            Material lootMat = CreateMaterial("Concrete", new Color(1f, 0.85f, 0.2f)); // Gold color
            Vector3[] lootPositions = new Vector3[]
            {
                new Vector3(-35f, 0.5f, 35f),
                new Vector3(-30f, 0.5f, -35f),
                new Vector3(40f, 0.5f, -40f),
                new Vector3(-45f, 0.5f, 0f),
                new Vector3(30f, 0.5f, 40f),
                new Vector3(-5f, 0.5f, 45f),
            };

            for (int i = 0; i < lootPositions.Length; i++)
            {
                GameObject lootObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                lootObj.name = $"LootSpot_{i}";
                lootObj.transform.SetParent(lootRoot.transform);
                lootObj.transform.position = lootPositions[i];
                lootObj.transform.localScale = new Vector3(1.5f, 0.3f, 1.5f);
                lootObj.GetComponent<MeshRenderer>().material = lootMat;
                lootObj.GetComponent<Collider>().isTrigger = true;
                lootObj.AddComponent<LootSpot>();
            }

            // Prison Zone trigger
            GameObject prisonZoneObj = new GameObject("PrisonZone");
            prisonZoneObj.transform.SetParent(prisonRoot.transform);
            prisonZoneObj.transform.position = new Vector3(18f, 1f, 18f);
            SphereCollider prisonTrigger = prisonZoneObj.AddComponent<SphereCollider>();
            prisonTrigger.isTrigger = true;
            prisonTrigger.radius = GameConstants.PrisonZoneRadius;
            prisonZoneObj.AddComponent<PrisonZone>();

            // -------------------------------------------------------------------
            // Map Gimmicks
            // -------------------------------------------------------------------
            GameObject gimmickRoot = new GameObject("MapGimmicks");
            gimmickRoot.transform.SetParent(envRoot.transform);

            // Co-op Switch + Door
            Material doorMat = CreateMaterial("Brick", new Color(0.6f, 0.3f, 0.1f));
            Material switchMat = CreateMaterial("Concrete", new Color(0.2f, 0.8f, 0.2f));
            Material boostMat = CreateMaterial("Concrete", new Color(0.2f, 0.9f, 1f));
            Material mudMat = CreateMaterial("Concrete", new Color(0.4f, 0.3f, 0.15f));
            Material ventMat = CreateMaterial("Concrete", new Color(0.3f, 0.3f, 0.3f));

            // Door 1 (between obstacles, blocking a shortcut)
            GameObject door1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door1.name = "Door_1";
            door1.transform.SetParent(gimmickRoot.transform);
            door1.transform.position = new Vector3(-8f, 2f, 5f);
            door1.transform.localScale = new Vector3(4f, 4f, 0.5f);
            door1.GetComponent<MeshRenderer>().material = doorMat;

            // Switch for Door 1
            GameObject switch1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            switch1.name = "Switch_1";
            switch1.transform.SetParent(gimmickRoot.transform);
            switch1.transform.position = new Vector3(-12f, 0.15f, 5f);
            switch1.transform.localScale = new Vector3(2f, 0.3f, 2f);
            switch1.GetComponent<MeshRenderer>().material = switchMat;
            switch1.GetComponent<Collider>().isTrigger = true;
            CoopSwitch coopSwitch1 = switch1.AddComponent<CoopSwitch>();
            coopSwitch1.linkedDoor = door1;

            // Boost Pads
            Vector3[] boostPositions = new Vector3[]
            {
                new Vector3(-15f, 0.05f, 0f),
                new Vector3(5f, 0.05f, 15f),
            };
            for (int i = 0; i < boostPositions.Length; i++)
            {
                GameObject boostPad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                boostPad.name = $"BoostPad_{i}";
                boostPad.transform.SetParent(gimmickRoot.transform);
                boostPad.transform.position = boostPositions[i];
                boostPad.transform.localScale = new Vector3(3f, 0.1f, 3f);
                boostPad.GetComponent<MeshRenderer>().material = boostMat;
                boostPad.GetComponent<Collider>().isTrigger = true;
                EnvironmentZone bz = boostPad.AddComponent<EnvironmentZone>();
                bz.zoneType = EnvironmentZone.ZoneType.BoostPad;
            }

            // Mud Traps
            Vector3[] mudPositions = new Vector3[]
            {
                new Vector3(0f, 0.05f, -15f),
                new Vector3(10f, 0.05f, 5f),
            };
            for (int i = 0; i < mudPositions.Length; i++)
            {
                GameObject mudTrap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mudTrap.name = $"MudTrap_{i}";
                mudTrap.transform.SetParent(gimmickRoot.transform);
                mudTrap.transform.position = mudPositions[i];
                mudTrap.transform.localScale = new Vector3(3f, 0.1f, 3f);
                mudTrap.GetComponent<MeshRenderer>().material = mudMat;
                mudTrap.GetComponent<Collider>().isTrigger = true;
                EnvironmentZone mz = mudTrap.AddComponent<EnvironmentZone>();
                mz.zoneType = EnvironmentZone.ZoneType.MudTrap;
            }

            // Vent Passage (robber-only shortcut)
            GameObject vent = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vent.name = "VentPassage_1";
            vent.transform.SetParent(gimmickRoot.transform);
            vent.transform.position = new Vector3(-5f, 1f, -5f);
            vent.transform.localScale = new Vector3(1.5f, 2f, 4f);
            vent.GetComponent<MeshRenderer>().material = ventMat;
            vent.AddComponent<VentPassage>();

            // Keep the vent cube out of the bake so the ground under it stays walkable...
            NavMeshModifier ventModifier = vent.AddComponent<NavMeshModifier>();
            ventModifier.ignoreFromBuild = true;

            // ...and mark that walkable strip as the vent area (robbers only — AI cops
            // strip this area from their areaMask at runtime).
            GameObject ventVolumeObj = new GameObject("VentAreaVolume");
            ventVolumeObj.transform.SetParent(gimmickRoot.transform);
            ventVolumeObj.transform.position = new Vector3(-5f, 1f, -5f);
            NavMeshModifierVolume ventVolume = ventVolumeObj.AddComponent<NavMeshModifierVolume>();
            ventVolume.center = Vector3.zero;
            ventVolume.size = new Vector3(3f, 4f, 6f);
            ventVolume.area = GameConstants.VentNavMeshArea;

            // Name the area in project settings for Navigation-window readability
            SetNavMeshAreaName(GameConstants.VentNavMeshArea, "Vent");

            // Camera and Light
            if (Camera.main == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                Camera cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
                camObj.transform.position = new Vector3(0, 30, -20);
                camObj.transform.rotation = Quaternion.Euler(60, 0, 0); // Top-down angled
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                // Adjust existing camera for top down view
                Camera.main.transform.position = new Vector3(0, 30, -20);
                Camera.main.transform.rotation = Quaternion.Euler(60, 0, 0);
            }
            
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            bool hasDirLight = false;
            foreach(var l in lights) { if (l.type == LightType.Directional) hasDirLight = true; }
            if (!hasDirLight)
            {
                GameObject lightObj = new GameObject("Directional Light");
                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
                lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
            }

            // -------------------------------------------------------------------
            // Setup GameManager / MatchManager
            // -------------------------------------------------------------------
            GameObject managerObj = GameObject.Find("MatchManager");
            if (managerObj == null) managerObj = new GameObject("MatchManager");
            
            InGameManager inGameManager = managerObj.GetComponent<InGameManager>();
            if (inGameManager == null) inGameManager = managerObj.AddComponent<InGameManager>();

            // Add LootManager
            LootManager lootMgr = managerObj.GetComponent<LootManager>();
            if (lootMgr == null) managerObj.AddComponent<LootManager>();

            // Assign Camera Follow
            CameraFollow camFollow = Camera.main.gameObject.GetComponent<CameraFollow>();
            if (camFollow == null) camFollow = Camera.main.gameObject.AddComponent<CameraFollow>();
            inGameManager.mainCameraFollow = camFollow;

            // Match-state sync (scene network object; inert offline, spawned by NGO
            // scene management in networked sessions)
            GameObject matchSyncObj = GameObject.Find("NetworkMatchSync");
            if (matchSyncObj != null) DestroyImmediate(matchSyncObj);
            matchSyncObj = new GameObject("NetworkMatchSync");
            matchSyncObj.AddComponent<NetworkObject>();
            matchSyncObj.AddComponent<NetworkMatchSync>();

            // Load Prefabs
            inGameManager.copPrefabs = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                inGameManager.copPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/MoraeGames/Prefabs/Characters/Cop_{i + 1}.prefab");
            }
            
            // 5 robber prefabs exist (CharacterSetupEditor generates Robber_1..5) —
            // loading a 6th produced a null slot that Instantiate would throw on
            inGameManager.robberPrefabs = new GameObject[5];
            for (int i = 0; i < 5; i++)
            {
                inGameManager.robberPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/MoraeGames/Prefabs/Characters/Robber_{i + 1}.prefab");
            }

            // Assign Spawns
            inGameManager.copSpawns = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject s = GameObject.Find($"SpawnPoints/CopSpawn_{i}");
                if (s != null) inGameManager.copSpawns[i] = s.transform;
            }

            inGameManager.robberSpawns = new Transform[6];
            for (int i = 0; i < 6; i++)
            {
                GameObject s = GameObject.Find($"SpawnPoints/RobberSpawn_{i}");
                if (s != null) inGameManager.robberSpawns[i] = s.transform;
            }

            // Wire the prison spawn so PrisonHelper gets the scene position injected
            GameObject prisonSpawnObj = GameObject.Find("SpawnPoints/PrisonSpawn");
            if (prisonSpawnObj != null) inGameManager.prisonSpawnPoint = prisonSpawnObj.transform;

            // Bake NavMesh
            surface.BuildNavMesh();

            EditorSceneManager.MarkSceneDirty(currentScene);
            Debug.Log("InGame Environment constructed and NavMesh baked successfully!");
        }

        private static void SetNavMeshAreaName(int index, string areaName)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
            if (assets == null || assets.Length == 0) return;

            SerializedObject so = new SerializedObject(assets[0]);
            SerializedProperty areas = so.FindProperty("areas");
            if (areas == null || index >= areas.arraySize) return;

            areas.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue = areaName;
            so.ApplyModifiedProperties();
        }

        [MenuItem("MoraeGames/Bake NavMesh Only")]
        public static void BakeNavMesh()
        {
            GameObject envRoot = GameObject.Find("Environment");
            if (envRoot == null)
            {
                Debug.LogError("Environment root not found. Please setup environment first.");
                return;
            }

            NavMeshSurface surface = envRoot.GetComponent<NavMeshSurface>();
            if (surface == null) surface = envRoot.AddComponent<NavMeshSurface>();
            
            surface.collectObjects = CollectObjects.Children;
            // surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("NavMesh baked successfully!");
        }

        private static void CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            wall.GetComponent<MeshRenderer>().material = mat;
        }

        private static void CreateSpawnPoint(Transform parent, string name, Vector3 pos)
        {
            GameObject spawn = new GameObject(name);
            spawn.transform.SetParent(parent);
            spawn.transform.position = pos;
            
            // Helpful to see in Scene View
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.SetParent(spawn.transform);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = Vector3.one * 0.5f;
            
            Shader shader = GetDefaultShader();
            Material mat = new Material(shader);
            Color c = Color.magenta;
            if (name.Contains("Cop")) c = Color.blue;
            else if (name.Contains("Robber")) c = Color.red;

            if (shader.name.Contains("Universal Render Pipeline"))
                mat.SetColor("_BaseColor", c);
            else
                mat.color = c;
            
            marker.GetComponent<MeshRenderer>().material = mat;
        }

        private static Material CreateMaterial(string type, Color baseColor)
        {
            Shader shader = GetDefaultShader();
            Material mat = new Material(shader);
            Texture2D tex = CreateProceduralTexture(type, baseColor);
            
            if (shader.name.Contains("Universal Render Pipeline"))
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", baseColor);
            }
            else
            {
                mat.mainTexture = tex;
                mat.color = baseColor;
            }
            return mat;
        }

        private static Shader GetDefaultShader()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
                return GraphicsSettings.currentRenderPipeline.defaultShader;
            
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null) return urpLit;
            
            return Shader.Find("Standard");
        }

        private static Texture2D CreateProceduralTexture(string type, Color baseColor)
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color c = baseColor;
                    if (type == "Concrete")
                    {
                        float noise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 0.2f - 0.1f;
                        c = new Color(Mathf.Clamp01(c.r + noise), Mathf.Clamp01(c.g + noise), Mathf.Clamp01(c.b + noise), 1f);
                    }
                    else if (type == "Brick")
                    {
                        int mortarWidth = 4;
                        int brickWidth = 64;
                        int brickHeight = 32;
                        
                        bool isMortarY = (y % brickHeight) < mortarWidth;
                        int offsetX = ((y / brickHeight) % 2 == 0) ? 0 : brickWidth / 2;
                        bool isMortarX = ((x + offsetX) % brickWidth) < mortarWidth;

                        if (isMortarX || isMortarY)
                        {
                            c = new Color(0.8f, 0.8f, 0.8f); 
                        }
                        else
                        {
                            float noise = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.1f - 0.05f;
                            c = new Color(Mathf.Clamp01(c.r + noise), Mathf.Clamp01(c.g + noise), Mathf.Clamp01(c.b + noise), 1f);
                        }
                    }
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
