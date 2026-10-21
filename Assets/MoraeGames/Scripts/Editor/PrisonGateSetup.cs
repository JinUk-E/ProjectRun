using System;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using TMPro;
using MoraeGames.Game;

namespace MoraeGames.Editor
{
    /// <summary>Rebuilds only the prison enclosure and rebakes walkable approaches.</summary>
    public static class PrisonGateSetup
    {
        static Material stone,metal,dark,gold,floor,bed;
        static TMP_FontAsset font;
        [MenuItem("MoraeGames/Apply Prison Gate Rescue")]
        public static void Apply()
        {
            const string path="Assets/MoraeGames/Scenes/InGame.unity";
            Directory.CreateDirectory("SourceAssets/SceneBackups/BeforePrisonGate");
            string backup="SourceAssets/SceneBackups/BeforePrisonGate/InGame.unity";
            if(!File.Exists(backup)) File.Copy(path,backup);
            var scene=EditorSceneManager.OpenScene(path);
            var prison=UnityEngine.Object.FindFirstObjectByType<PrisonZone>();
            var game=UnityEngine.Object.FindFirstObjectByType<InGameManager>();
            var area=GameObject.Find("PrisonArea");
            var old=area.transform.Find("PrisonArchitecture");
            if(old!=null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach(var renderer in area.GetComponentsInChildren<Renderer>(true)) renderer.enabled=false;
            foreach(var collider in area.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
            // The holding area is no longer an interaction target.
            foreach(var collider in prison.GetComponents<Collider>()) collider.enabled=false;
            var root=new GameObject("PrisonArchitecture").transform;
            root.SetParent(area.transform,false); root.position=new Vector3(18,0,18);
            stone=Mat("PrisonStone",new Color(.31f,.37f,.45f));
            metal=Mat("PrisonSteel",new Color(.58f,.67f,.74f),.65f);
            dark=Mat("PrisonFrame",new Color(.08f,.12f,.18f));
            gold=Mat("PrisonLock",new Color(1,.68f,.08f));
            floor=Mat("PrisonConcrete",new Color(.37f,.42f,.46f));
            bed=Mat("PrisonBed",new Color(.19f,.4f,.48f));
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/MoraeGames/Fonts/NanumGothicSDF.asset");
            Box(root,"ConcreteFloor",new Vector3(0,.025f,0),new Vector3(10.5f,.04f,10.5f),floor);
            Wall(root,"NorthFence",new Vector3(0,0,5),10,0);
            Wall(root,"WestFence",new Vector3(-5,0,0),10,90);
            Wall(root,"EastFence",new Vector3(5,0,0),10,90);
            Wall(root,"FrontLeftFence",new Vector3(-3.3f,0,-5),3.4f,0);
            Wall(root,"FrontRightFence",new Vector3(3.3f,0,-5),3.4f,0);
            foreach(float x in new[]{-5f,5f}) foreach(float z in new[]{-5f,5f})
            {
                Box(root,"WatchPillar",new Vector3(x,2,z),new Vector3(.7f,4,.7f),stone,true);
                Box(root,"PillarCap",new Vector3(x,4.1f,z),new Vector3(.95f,.25f,.95f),dark);
                Box(root,"WarningLamp",new Vector3(x,4.35f,z),new Vector3(.32f,.25f,.32f),gold);
            }
            foreach(float x in new[]{-3.3f,3.3f})
            {
                Box(root,"BedFrame",new Vector3(x,.35f,2.7f),new Vector3(2.1f,.4f,1.1f),dark,true);
                Box(root,"Mattress",new Vector3(x,.62f,2.7f),new Vector3(2,.18f,1),bed);
                Box(root,"Pillow",new Vector3(x-.65f,.76f,2.7f),new Vector3(.5f,.12f,.75f),floor);
            }
            foreach(float x in new[]{-1.85f,1.85f}) Box(root,"GatePost",new Vector3(x,2.1f,-5),new Vector3(.4f,4.2f,.65f),dark,true);
            Box(root,"GateHeader",new Vector3(0,4.15f,-5),new Vector3(4.3f,.5f,.8f),dark);
            Box(root,"PrisonSign",new Vector3(0,4.65f,-5.3f),new Vector3(4.3f,.9f,.15f),dark);
            Text(root,"PrisonTitle","감 옥",new Vector3(0,4.65f,-5.4f),new Vector2(4.1f,.85f),7,false);
            var gate=new GameObject("SlidingGate").transform; gate.SetParent(root,false); gate.localPosition=new Vector3(0,0,-5);
            Bars(gate,3.2f);
            Box(gate,"LockBody",new Vector3(0,1.6f,-.24f),new Vector3(.6f,.55f,.22f),gold);
            Box(gate,"LockLeft",new Vector3(-.19f,1.98f,-.24f),new Vector3(.09f,.28f,.12f),metal);
            Box(gate,"LockRight",new Vector3(.19f,1.98f,-.24f),new Vector3(.09f,.28f,.12f),metal);
            Box(gate,"LockTop",new Vector3(0,2.12f,-.24f),new Vector3(.45f,.09f,.12f),metal);
            prison.doorVisual=gate;
            var block=Box(root,"GateCollision",new Vector3(0,1.8f,-5),new Vector3(3.2f,3.6f,.4f),dark,true);
            block.GetComponent<Renderer>().enabled=false;
            block.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
            prison.doorCollider=block.GetComponent<BoxCollider>();
            var obstacle=block.AddComponent<NavMeshObstacle>(); obstacle.shape=NavMeshObstacleShape.Box; obstacle.size=Vector3.one; obstacle.carving=true;
            obstacle.carveOnlyStationary=false; obstacle.enabled=false; prison.doorObstacle=obstacle;
            var door=new GameObject("PrisonDoor",typeof(PrisonDoorInteraction),typeof(BoxCollider)).transform;
            door.SetParent(root,false); door.localPosition=new Vector3(0,0,-5); door.localRotation=Quaternion.Euler(0,180,0);
            door.GetComponent<PrisonDoorInteraction>().prison=prison;
            var trigger=door.GetComponent<BoxCollider>(); trigger.isTrigger=true; trigger.center=new Vector3(0,1,1); trigger.size=new Vector3(3.4f,2,2);
            door.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
            prison.doorPoint=door; prison.interactionRange=2.4f;
            Box(root,"RescuePad",new Vector3(0,.055f,-6.5f),new Vector3(3.4f,.04f,2),gold);
            prison.doorStatusText=Text(root,"DoorHint","문 앞에서 F / 행동 길게 · 동료 구출",new Vector3(0,.1f,-8),new Vector2(13,1.3f),3.2f,true);
            if(game.prisonSpawnPoint!=null) game.prisonSpawnPoint.position=new Vector3(18,0,18);
            var environment=GameObject.Find("Environment");
            var obstacles=environment.transform.Find("Obstacles");
            var footprint=new Bounds(new Vector3(18,2,17),new Vector3(13,8,16));
            foreach(var collider in obstacles.GetComponentsInChildren<Collider>())
                if(collider.bounds.Intersects(footprint)) collider.gameObject.SetActive(false);
            var surface=environment.GetComponent<NavMeshSurface>(); surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            if(!AssetDatabase.Contains(surface.navMeshData)) AssetDatabase.CreateAsset(surface.navMeshData,AssetDatabase.GenerateUniqueAssetPath("Assets/MoraeGames/Scenes/PrisonGateNavMesh.asset"));
            Physics.SyncTransforms();
            Vector3 approach=prison.InteractionPosition;
            if(!NavMesh.SamplePosition(approach,out var approachHit,.6f,NavMesh.AllAreas)) throw new InvalidOperationException("Prison door approach not walkable");
            foreach(Vector3 point in new[]{new Vector3(18,0,7),new Vector3(10,0,18),new Vector3(26,0,18)})
            {
                if(!NavMesh.SamplePosition(point,out var from,2,NavMesh.AllAreas)) throw new InvalidOperationException("Missing approach mesh");
                var route=new NavMeshPath();
                if(!NavMesh.CalculatePath(from.position,approachHit.position,NavMesh.AllAreas,route)||route.status!=NavMeshPathStatus.PathComplete) throw new InvalidOperationException("Prison door route blocked");
            }
            obstacle.enabled=true;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            File.WriteAllText("TestResults/prison-gate-layout.txt","PASS: enclosed prison; door-only trigger; walkable front and side approaches; dynamic gate carving; clear rescue exits.");
        }
        static Material Mat(string name,Color color,float metallic=0)
        {
            string path="Assets/MoraeGames/Materials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color); mat.SetFloat("_Metallic",metallic); mat.SetFloat("_Smoothness",.35f); EditorUtility.SetDirty(mat); return mat;
        }
        static GameObject Box(Transform parent,string name,Vector3 position,Vector3 size,Material material,bool solid=false)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name; obj.transform.SetParent(parent,false); obj.transform.localPosition=position; obj.transform.localScale=size;
            obj.GetComponent<Renderer>().sharedMaterial=material; if(!solid) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>()); return obj;
        }
        static void Bars(Transform parent,float width)
        {
            Box(parent,"LowerRail",new Vector3(0,.3f,0),new Vector3(width,.2f,.22f),dark);
            Box(parent,"MiddleRail",new Vector3(0,1.8f,0),new Vector3(width,.12f,.18f),metal);
            Box(parent,"UpperRail",new Vector3(0,3.6f,0),new Vector3(width,.2f,.22f),dark);
            int count=Mathf.CeilToInt(width/.55f);
            for(int i=0;i<=count;i++) Box(parent,"SteelBar",new Vector3(-width/2+width*i/count,1.95f,0),new Vector3(.11f,3.3f,.11f),metal);
        }
        static void Wall(Transform parent,string name,Vector3 position,float width,float yaw)
        {
            var root=new GameObject(name).transform; root.SetParent(parent,false); root.localPosition=position; root.localRotation=Quaternion.Euler(0,yaw,0);
            var collision=Box(root,"FenceCollision",new Vector3(0,1.8f,0),new Vector3(width,3.6f,.4f),stone,true); collision.GetComponent<Renderer>().enabled=false;
            Box(root,"ConcreteBase",new Vector3(0,.2f,0),new Vector3(width,.4f,.6f),stone);
            Bars(root,width);
        }
        static TMP_Text Text(Transform parent,string name,string value,Vector3 position,Vector2 size,float fontSize,bool ground)
        {
            var obj=new GameObject(name,typeof(TextMeshPro)); obj.transform.SetParent(parent,false);obj.transform.localPosition=position;
            if(ground) obj.transform.localRotation=Quaternion.Euler(90,0,0);
            var text=obj.GetComponent<TextMeshPro>(); text.font=font; text.fontSize=fontSize; text.text=value; text.alignment=TextAlignmentOptions.Center; text.rectTransform.sizeDelta=size; return text;
        }
        public static void ApplyAndBuild(){Apply();MovementProbeBuild.Run();}
    }
}
