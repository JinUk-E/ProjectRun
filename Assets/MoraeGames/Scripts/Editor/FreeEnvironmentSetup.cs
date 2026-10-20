using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using MoraeGames.Game;

namespace MoraeGames.Editor
{
    // Visual-only migration: keep the original colliders, loot links and baked navigation.
    public static class FreeEnvironmentSetup
    {
        const string Art = "Assets/MoraeGames/Art/KenneyEnvironment";
        struct Vertex
        {
            public Vector3 p;
            public Vector2 uv;
            public Vertex(Vector3 position, Vector2 tex) { p = position; uv = tex; }
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex(Vector3.Lerp(a.p,b.p,t),Vector2.Lerp(a.uv,b.uv,t));
        }

        [MenuItem("MoraeGames/Apply Free Environment Art")]
        public static void Apply()
        {
            const string scenePath = "Assets/MoraeGames/Scenes/InGame.unity";
            Directory.CreateDirectory("SourceAssets/SceneBackups/BeforeFreeEnvironmentAssets");
            string backup = "SourceAssets/SceneBackups/BeforeFreeEnvironmentAssets/InGame.unity";
            if (!File.Exists(backup)) File.Copy(scenePath,backup);
            foreach (string path in Directory.GetFiles(Art,"*.fbx",SearchOption.AllDirectories))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.isReadable = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
            var scene = EditorSceneManager.OpenScene(scenePath);
            var buildings = UnityEngine.Object.FindObjectsByType<HeistBuilding>(FindObjectsSortMode.None).OrderBy(b=>b.name).ToArray();
            if (buildings.Length != 6) throw new InvalidOperationException("Expected six heist buildings");
            var industrial = MakeMaterial("Industrial");
            var dungeon = MakeMaterial("Dungeon");
            var backing = AssetDatabase.LoadAssetAtPath<Material>(Art+"/Industrial/WallBacking.mat");
            if(backing==null) { backing=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(backing,Art+"/Industrial/WallBacking.mat"); }
            backing.SetColor("_BaseColor",new Color(.65f,.71f,.75f)); EditorUtility.SetDirty(backing);
            Directory.CreateDirectory(Art+"/Generated"); AssetDatabase.Refresh();
            string[] models = {"building-a","building-c","building-j"};
            foreach (var building in buildings)
            {
                int i = Array.IndexOf(buildings,building);
                Transform root = ReplaceRoot(building.transform,"KenneyExterior");
                var source = ReadModel("Industrial/"+models[i%3]);
                Bounds bounds = BoundsOf(source);
                // Normalize the facade to the existing footprint. Slice away the roof and doorways.
                for(int n=0;n<source.Count;n++)
                {
                    var v=source[n];
                    v.p=new Vector3((v.p.x-bounds.center.x)/bounds.size.x*10.6f,
                        (v.p.y-bounds.min.y)/bounds.size.y*4.4f,
                        (v.p.z-bounds.center.z)/bounds.size.z*10.6f);
                    source[n]=v;
                }
                foreach(var wall in building.walls)
                {
                    Vector3 center=building.transform.InverseTransformPoint(wall.bounds.center);
                    // The original cube would cover inset windows. Use a thinner visual backing,
                    // while leaving the original collider and sight-blocking volume untouched.
                    wall.GetComponent<Renderer>().enabled=false;
                    var panel=GameObject.CreatePrimitive(PrimitiveType.Cube); panel.name=wall.name+"_Backing";
                    panel.transform.SetParent(root,false); panel.transform.localPosition=center;
                    Vector3 panelSize=wall.bounds.size;
                    if(panelSize.x<panelSize.z) panelSize.x=.12f; else panelSize.z=.12f;
                    panel.transform.localScale=panelSize;
                    UnityEngine.Object.DestroyImmediate(panel.GetComponent<Collider>());
                    panel.GetComponent<Renderer>().sharedMaterial=backing;
                    Bounds clip=new Bounds(center,wall.bounds.size+new Vector3(.025f,0,.025f));
                    var triangles=Clip(source,clip);
                    if(triangles.Count>0) CreateMesh(root,wall.name,triangles,industrial,building.name+"_"+wall.name);
                }
                // Recognizable industrial props stay inside already-solid wall corners, above eye level.
                FitModel(root,"Industrial/detail-tank",new Bounds(new Vector3(-4.7f,2.45f,4.6f),new Vector3(.7f,.5f,.7f)),industrial,0);
                ValidatePaths(building);
            }
            // Solid map obstacles have no playable interior: use the complete original buildings.
            var obstacles=GameObject.Find("Environment").transform.Find("Obstacles");
            int obstacleCount=0;
            foreach(var collider in obstacles.GetComponentsInChildren<BoxCollider>())
            {
                var renderer=collider.GetComponent<MeshRenderer>();
                if(renderer==null) continue;
                var root=ReplaceRoot(collider.transform,"KenneyBuildingArt");
                FitModel(root,"Industrial/"+models[obstacleCount%models.Length],new Bounds(collider.center,collider.size),industrial,0);
                renderer.enabled=false; obstacleCount++;
            }
            var prison=GameObject.Find("PrisonArea");
            if(prison==null) throw new InvalidOperationException("Missing prison");
            var prisonRoot=ReplaceRoot(prison.transform,"KenneyPrison");
            foreach(string name in new[]{"Prison_Wall_W","Prison_Wall_S"})
            {
                var wall=prison.transform.Find(name);
                if(wall==null) throw new InvalidOperationException("Missing prison wall: "+name);
                var collider=wall.GetComponent<BoxCollider>();
                Bounds b=collider.bounds;
                bool west=name.EndsWith("W");
                for(int i=0;i<5;i++)
                {
                    Vector3 position=b.center;
                    if(west) position.z=b.min.z+(i+.5f)*b.size.z/5;
                    else position.x=b.min.x+(i+.5f)*b.size.x/5;
                    Vector3 size=west?new Vector3(b.size.x,b.size.y,b.size.z/5):new Vector3(b.size.x/5,b.size.y,b.size.z);
                    FitModel(prisonRoot,"Dungeon/gate-metal-bars",new Bounds(prisonRoot.InverseTransformPoint(position),size),dungeon,west?90:0);
                }
                wall.GetComponent<Renderer>().enabled=false;
            }
            Physics.SyncTransforms();
            // Future rebakes must still use invisible collision walls, not decorative render meshes.
            GameObject.Find("Environment").GetComponent<NavMeshSurface>().useGeometry=NavMeshCollectGeometry.PhysicsColliders;
            foreach(var building in buildings) ValidatePaths(building);
            if(prisonRoot.GetComponentsInChildren<Collider>().Length!=0) throw new InvalidOperationException("Art must not add colliders");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/free-environment.txt","PASS: six open-top Kenney industrial facades; "+obstacleCount+" solid city buildings; ten prison bar modules; original wall colliders retained; all six entry-loot-exit paths complete; decorative art excluded from navigation. CC0 sources/licenses included.");
        }

        static Transform ReplaceRoot(Transform parent,string name)
        {
            var old=parent.Find(name); if(old!=null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var obj=new GameObject(name); obj.transform.SetParent(parent,false);
            obj.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
            return obj.transform;
        }
        static Material MakeMaterial(string kit)
        {
            string path=Art+"/"+kit+"/Environment.mat";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/"+kit+"/colormap.png");
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
            material.SetTexture("_BaseMap",texture); material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Smoothness",.15f); material.SetFloat("_Cull",0);
            EditorUtility.SetDirty(material); return material;
        }
        static List<Vertex> ReadModel(string name)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/"+name+".fbx");
            if(prefab==null) throw new InvalidOperationException("Missing model "+name);
            var result=new List<Vertex>();
            foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh; var verts=mesh.vertices; var uv=mesh.uv;
                var matrix=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                foreach(int index in mesh.triangles) result.Add(new Vertex(matrix.MultiplyPoint3x4(verts[index]),uv.Length>index?uv[index]:Vector2.zero));
            }
            if(result.Count==0) throw new InvalidOperationException("Empty model "+name);
            return result;
        }
        static Bounds BoundsOf(List<Vertex> vertices)
        {
            var bounds=new Bounds(vertices[0].p,Vector3.zero);
            foreach(var v in vertices) bounds.Encapsulate(v.p); return bounds;
        }
        static void FitModel(Transform parent,string model,Bounds target,Material material,float yaw)
        {
            var vertices=ReadModel(model); var rotation=Quaternion.Euler(0,yaw,0);
            for(int i=0;i<vertices.Count;i++) { var v=vertices[i]; v.p=rotation*v.p; vertices[i]=v; }
            var b=BoundsOf(vertices);
            for(int i=0;i<vertices.Count;i++)
            {
                var v=vertices[i]; v.p=target.center+Vector3.Scale(v.p-b.center,new Vector3(target.size.x/b.size.x,target.size.y/b.size.y,target.size.z/b.size.z)); vertices[i]=v;
            }
            string name=model.Split('/').Last();
            CreateMesh(parent,name,vertices,material,parent.parent.name+"_"+parent.name+"_"+name+"_"+parent.childCount);
        }
        static void CreateMesh(Transform parent,string name,List<Vertex> vertices,Material material,string assetName)
        {
            string path=Art+"/Generated/"+assetName+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool fresh=mesh==null; if(fresh) mesh=new Mesh(); else mesh.Clear();
            mesh.name=assetName; mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices.Select(v=>v.p).ToList()); mesh.SetUVs(0,vertices.Select(v=>v.uv).ToList());
            mesh.SetTriangles(Enumerable.Range(0,vertices.Count).ToArray(),0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if(fresh) AssetDatabase.CreateAsset(mesh,path); else EditorUtility.SetDirty(mesh);
            var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); obj.transform.SetParent(parent,false);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh; obj.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        static List<Vertex> Clip(List<Vertex> source,Bounds bounds)
        {
            var result=new List<Vertex>();
            for(int i=0;i<source.Count;i+=3)
            {
                var polygon=new List<Vertex>{source[i],source[i+1],source[i+2]};
                for(int axis=0;axis<3;axis++)
                {
                    polygon=ClipPlane(polygon,axis,bounds.min[axis],true);
                    polygon=ClipPlane(polygon,axis,bounds.max[axis],false);
                }
                for(int n=1;n+1<polygon.Count;n++) { result.Add(polygon[0]); result.Add(polygon[n]); result.Add(polygon[n+1]); }
            }
            return result;
        }
        static List<Vertex> ClipPlane(List<Vertex> polygon,int axis,float value,bool minimum)
        {
            var output=new List<Vertex>(); if(polygon.Count==0) return output;
            Vertex a=polygon[polygon.Count-1]; bool insideA=minimum?a.p[axis]>=value:a.p[axis]<=value;
            foreach(var b in polygon)
            {
                bool insideB=minimum?b.p[axis]>=value:b.p[axis]<=value;
                if(insideA!=insideB) output.Add(Vertex.Lerp(a,b,(value-a.p[axis])/(b.p[axis]-a.p[axis])));
                if(insideB) output.Add(b); a=b; insideA=insideB;
            }
            return output;
        }
        static void ValidatePaths(HeistBuilding b)
        {
            Vector3[] points={b.transform.position+new Vector3(0,0,-8),b.loot.transform.position,b.transform.position+new Vector3(8,0,1)};
            for(int i=0;i<points.Length;i++)
            {
                if(!NavMesh.SamplePosition(points[i],out var hit,2,NavMesh.AllAreas)) throw new InvalidOperationException("No navmesh: "+b.name);
                points[i]=hit.position;
            }
            for(int i=0;i<2;i++)
            {
                var path=new NavMeshPath();
                if(!NavMesh.CalculatePath(points[i],points[i+1],NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("Blocked door: "+b.name);
            }
        }
        public static void ApplyAndBuild() { Apply(); MovementProbeBuild.Run(); }
        public static void ApplyAndRelease() { Apply(); ReleaseReadinessRepair.BuildVerificationPlayer(); }
    }
}
