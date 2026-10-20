using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using TMPro;
using MoraeGames.Game;

namespace MoraeGames.Editor
{
    /// <summary>Adds accessible interiors to the existing map without regenerating its UI or gimmicks.</summary>
    public static class HeistBuildingSetup
    {
        [MenuItem("MoraeGames/Apply Heist Buildings")]
        public static void Apply()
        {
            const string scenePath = "Assets/MoraeGames/Scenes/InGame.unity";
            Directory.CreateDirectory("SourceAssets/SceneBackups/BeforeHeistBuildings");
            string backup = "SourceAssets/SceneBackups/BeforeHeistBuildings/InGame.unity";
            if (!File.Exists(backup)) File.Copy(scenePath, backup);
            var scene = EditorSceneManager.OpenScene(scenePath);
            var env = GameObject.Find("Environment");
            if (env == null) throw new InvalidOperationException("Missing environment");
            var old = env.transform.Find("HeistBuildings");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("HeistBuildings"); root.transform.SetParent(env.transform, false);
            var spots = UnityEngine.Object.FindObjectsByType<LootSpot>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
            if (spots.Length != 6) throw new InvalidOperationException("Expected six existing loot spots");
            Vector3[] centers = { new Vector3(-32,0,30), new Vector3(-28,0,-30), new Vector3(32,0,-32),
                new Vector3(-38,0,0), new Vector3(30,0,36), new Vector3(-4,0,38) };
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/MoraeGames/Fonts/NanumGothicSDF.asset");
            var blue = Material("HeistWarehouse", new Color(.15f,.42f,.67f));
            var orange = Material("HeistVault", new Color(.72f,.3f,.08f));
            var floor = Material("HeistFloor", new Color(.25f,.32f,.38f));
            var doorway = Material("HeistDoorway", new Color(.15f,.85f,.55f));
            for (int i = 0; i < spots.Length; i++)
            {
                var footprint = new Bounds(centers[i], new Vector3(17,12,17));
                var obstacles = env.transform.Find("Obstacles");
                if (obstacles != null)
                    foreach (var collider in obstacles.GetComponentsInChildren<Collider>(true))
                        if (collider.bounds.Intersects(footprint)) collider.gameObject.SetActive(false);
                var obj = new GameObject("HeistBuilding_" + i); obj.transform.SetParent(root.transform, false); obj.transform.position = centers[i];
                var building = obj.AddComponent<HeistBuilding>();
                building.guarded = i % 2 == 0;
                building.displayName = (building.guarded ? "경보 금고 " : "창고 ") + (i + 1);
                building.loot = spots[i]; spots[i].building = building;
                spots[i].transform.position = centers[i] + new Vector3(-2,.5f,2);
                spots[i].lootValue = building.guarded ? 180 : 100;
                spots[i].robberyDuration = building.guarded ? 7 : 5;
                var walls = new List<BoxCollider>();
                Material wall = building.guarded ? orange : blue;
                walls.Add(Box(obj.transform,"BackWall",new Vector3(0,1.1f,5),new Vector3(10.6f,2.2f,.6f),wall,true).GetComponent<BoxCollider>());
                walls.Add(Box(obj.transform,"LeftWall",new Vector3(-5,1.1f,0),new Vector3(.6f,2.2f,10),wall,true).GetComponent<BoxCollider>());
                walls.Add(Box(obj.transform,"FrontLeft",new Vector3(-3.5f,1.1f,-5),new Vector3(3,2.2f,.6f),wall,true).GetComponent<BoxCollider>());
                walls.Add(Box(obj.transform,"FrontRight",new Vector3(3.5f,1.1f,-5),new Vector3(3,2.2f,.6f),wall,true).GetComponent<BoxCollider>());
                walls.Add(Box(obj.transform,"SideBack",new Vector3(5,1.1f,3.75f),new Vector3(.6f,2.2f,2.5f),wall,true).GetComponent<BoxCollider>());
                walls.Add(Box(obj.transform,"SideFront",new Vector3(5,1.1f,-2.75f),new Vector3(.6f,2.2f,4.5f),wall,true).GetComponent<BoxCollider>());
                building.walls = walls.ToArray();
                Box(obj.transform,"InteriorFloor",new Vector3(0,.025f,0),new Vector3(10,.04f,10),floor,false);
                Box(obj.transform,"Entrance",new Vector3(0,.06f,-5),new Vector3(4,.05f,2),doorway,false);
                Box(obj.transform,"EscapeExit",new Vector3(5,.06f,1),new Vector3(2,.05f,3),doorway,false);
                building.beacon = Box(obj.transform,"AlarmBeacon",new Vector3(-4,3,4),new Vector3(1,1,1),wall,false).GetComponent<Renderer>();
                var label = new GameObject("BuildingLabel",typeof(TextMeshPro)); label.transform.SetParent(obj.transform,false);
                label.transform.localPosition = new Vector3(0,.12f,-7); label.transform.localRotation=Quaternion.Euler(90,0,0);
                var text = label.GetComponent<TextMeshPro>(); text.font=font; text.fontSize=4; text.alignment=TextAlignmentOptions.Center;
                text.rectTransform.sizeDelta=new Vector2(13,3); text.text=building.displayName+"\n<size=70%>"+(building.guarded?"고보상 · 경보 작동":"일반 보관소")+"</size>";
                label.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
            }
            var surface = env.GetComponent<NavMeshSurface>();
            surface.BuildNavMesh();
            if (!AssetDatabase.Contains(surface.navMeshData))
                AssetDatabase.CreateAsset(surface.navMeshData, AssetDatabase.GenerateUniqueAssetPath("Assets/MoraeGames/Scenes/HeistNavMesh.asset"));
            Physics.SyncTransforms();
            foreach (var building in root.GetComponentsInChildren<HeistBuilding>()) VerifyPaths(building);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            File.WriteAllText("TestResults/heist-layout.txt","PASS: six buildings, two doors each; complete entry/escape NavMesh paths; walls block sight; loot inside. Three alarm vaults (7s/180) and three warehouses (5s/100).");
        }

        private static void VerifyPaths(HeistBuilding building)
        {
            Vector3 center=building.transform.position;
            Vector3[] points={center+new Vector3(0,0,-8),building.loot.transform.position,center+new Vector3(8,0,1)};
            for(int i=0;i<points.Length;i++)
            {
                if(!NavMesh.SamplePosition(points[i],out var hit,2,NavMesh.AllAreas)) throw new InvalidOperationException("No walkable point: "+building.name);
                points[i]=hit.position;
            }
            for(int i=0;i<2;i++)
            {
                var path=new NavMeshPath();
                if(!NavMesh.CalculatePath(points[i],points[i+1],NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("Blocked entrance/exit: "+building.name);
            }
            var ray=new Ray(center+Vector3.up,Vector3.left);
            if(!building.walls.Any(w=>w.Raycast(ray,out _,7))) throw new InvalidOperationException("Wall does not block sight");
        }
        private static GameObject Box(Transform parent,string name,Vector3 position,Vector3 scale,Material material,bool solid)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position;obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid) { UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>()); obj.AddComponent<NavMeshModifier>().ignoreFromBuild=true; }
            return obj;
        }
        private static Material Material(string name,Color color)
        {
            if (!AssetDatabase.IsValidFolder("Assets/MoraeGames/Materials")) AssetDatabase.CreateFolder("Assets/MoraeGames", "Materials");
            string path="Assets/MoraeGames/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path); }
            material.SetColor("_BaseColor",color);EditorUtility.SetDirty(material);return material;
        }
        public static void ApplyAndBuild() { Apply(); MovementProbeBuild.Run(); }
    }
}
