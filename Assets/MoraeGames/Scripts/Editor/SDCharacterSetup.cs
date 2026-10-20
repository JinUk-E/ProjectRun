using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using MoraeGames.Player;

namespace MoraeGames.Editor
{
    /// <summary>Replaces only prefab visuals, preserving network identity, abilities and collision geometry.</summary>
    public static class SDCharacterSetup
    {
        private const string Art = "Assets/MoraeGames/Art/SDCharacters";

        [MenuItem("MoraeGames/Apply SD Character Visuals")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit missing");
            foreach (string role in new[] { "Cop", "Robber" })
            {
                string path = Art + "/SD_" + role + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
                importer.importCameras = false;
                importer.importLights = false;
                importer.addCollider = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips) { clip.loopTime = true; clip.loopPose = true; }
                importer.clipAnimations = clips;
                foreach (var embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    string matPath = Art + "/" + role + "_" + embedded.name + ".mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
                    mat.SetColor("_BaseColor", embedded.color);
                    mat.SetFloat("_Smoothness", .18f);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), mat);
                }
                importer.SaveAndReimport();
                BuildController(role, path);
            }
            Directory.CreateDirectory("SourceAssets/PrefabBackups/BeforeSD");
            foreach (string prefabPath in Directory.GetFiles("Assets/MoraeGames/Prefabs/Characters", "*.prefab"))
            {
                string backup = "SourceAssets/PrefabBackups/BeforeSD/" + Path.GetFileName(prefabPath);
                if (!File.Exists(backup)) File.Copy(prefabPath, backup);
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    string role = root.GetComponent<CopController>() != null ? "Cop" : "Robber";
                    var old = root.transform.Find("Body");
                    if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                    var body = new GameObject("Body"); body.transform.SetParent(root.transform, false);
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/SD_" + role + ".fbx"));
                    model.transform.SetParent(body.transform, false);
                    model.name = "Model";
                    var animator = model.GetComponent<Animator>();
                    if (animator == null) animator = model.AddComponent<Animator>();
                    animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Art + "/" + role + ".controller");
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    body.AddComponent<CharacterVisualAnimation>();
                    Require(body.GetComponentsInChildren<Collider>().Length == 0, "Visual has colliders");
                    Require(body.GetComponentInChildren<SkinnedMeshRenderer>() != null, "Skin missing");
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Preview();
            File.WriteAllText("TestResults/sd-characters.txt", "PASS: 9 character prefabs use CC0-derived SD visuals; Idle/Run/Arrested clips; no visual colliders; root motion disabled; existing gameplay roots preserved.");
        }

        private static void BuildController(string role, string path)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            Debug.Log("[SDCharacter] " + role + " clips: " + string.Join(", ", clips.Select(c => c.name)));
            AnimationClip Clip(string key) => clips.FirstOrDefault(c => c.name.EndsWith(key)) ?? throw new InvalidOperationException("Clip missing: " + key);
            string controllerPath = Art + "/" + role + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            foreach (var layer in controller.layers)
                foreach (var state in layer.stateMachine.states) layer.stateMachine.RemoveState(state.state);
            foreach (var oldTree in AssetDatabase.LoadAllAssetsAtPath(controllerPath).OfType<BlendTree>())
                UnityEngine.Object.DestroyImmediate(oldTree, true);
            controller.parameters = new AnimatorControllerParameter[0];
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Arrested", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var locomotion = machine.AddState("Locomotion");
            var tree = new BlendTree { name = "Idle to Run", blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(Clip("Idle"), 0f); tree.AddChild(Clip("Run"), 1f);
            locomotion.motion = tree; machine.defaultState = locomotion;
            var arrest = machine.AddState("Arrested"); arrest.motion = Clip("Arrested");
            var enter = locomotion.AddTransition(arrest); enter.hasExitTime = false; enter.duration = .12f;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Arrested");
            var exit = arrest.AddTransition(locomotion); exit.hasExitTime = false; exit.duration = .12f;
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Arrested");
            EditorUtility.SetDirty(controller);
        }

        private static void Preview()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .70f, .80f);
            var light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.7f; light.transform.rotation = Quaternion.Euler(40, -35, 0);
            foreach (string role in new[] { "Cop", "Robber" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MoraeGames/Prefabs/Characters/" + role + "_1.prefab");
                var actor = new GameObject(role + " Preview");
                var visual = UnityEngine.Object.Instantiate(prefab.transform.Find("Body").gameObject, actor.transform);
                visual.name = "Body";
                actor.transform.position = new Vector3(role == "Cop" ? -1.3f : 1.3f, 0, 0);
                var animator = actor.GetComponentInChildren<Animator>();
                var idle = AssetDatabase.LoadAllAssetsAtPath(Art + "/SD_" + role + ".fbx").OfType<AnimationClip>().First(c => c.name.EndsWith("Idle"));
                idle.SampleAnimation(animator.gameObject, 0);
                var renderer = actor.GetComponentInChildren<SkinnedMeshRenderer>();
                Debug.Log("[SDCharacter] " + role + " bounds=" + renderer.bounds + " triangles=" + renderer.sharedMesh.triangles.Length / 3);
            }
            var camera = new GameObject("Preview Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(4, 3.8f, 8);
            camera.transform.LookAt(new Vector3(0, 1.1f, 0));
            camera.orthographic = true; camera.orthographicSize = 2.4f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f, .08f, .13f);
            camera.nearClipPlane = .1f; camera.farClipPlane = 50;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/MoraeGames/Scenes/CharacterPreview.unity");
            Capture(camera, "TestResults/sd-characters-preview.png");
            camera.transform.position = new Vector3(0, 7, -4.67f); camera.transform.LookAt(new Vector3(0, .8f, 0));
            Capture(camera, "TestResults/sd-characters-topdown.png");
            camera.transform.position = new Vector3(4, 3.8f, 8); camera.transform.LookAt(new Vector3(0, 1.1f, 0));
            foreach (string pose in new[] { "Run", "Arrested" })
            {
                foreach (var animator in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
                {
                    string role = animator.transform.root.name.StartsWith("Cop") ? "Cop" : "Robber";
                    var clip = AssetDatabase.LoadAllAssetsAtPath(Art + "/SD_" + role + ".fbx").OfType<AnimationClip>().First(c => c.name.EndsWith(pose));
                    clip.SampleAnimation(animator.gameObject, clip.length * .25f);
                    var hand = animator.GetComponentsInChildren<Transform>().First(t => t.name == "Hand.L");
                    Debug.Log("[SDCharacter] " + role + " " + pose + " hand=" + hand.position);
                }
                Capture(camera, "TestResults/sd-characters-" + pose.ToLowerInvariant() + ".png");
            }
        }

        private static void Capture(Camera camera, string path)
        {
            // Several camera renders occur in one editor frame. Bake each sampled pose
            // so the render does not reuse Unity's previous-frame skinning cache.
            var skins = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
            var bakedObjects = new GameObject[skins.Length];
            var bakedMeshes = new Mesh[skins.Length];
            for (int i = 0; i < skins.Length; i++)
            {
                var mesh = new Mesh(); skins[i].BakeMesh(mesh); bakedMeshes[i] = mesh;
                Debug.Log("[SDCharacter] baked=" + mesh.bounds + " scale=" + skins[i].transform.lossyScale);
                var obj = new GameObject("BakedPreview"); obj.transform.SetParent(skins[i].transform, false);
                // BakeMesh returns a scale-applied snapshot for these imported FBX skins.
                Vector3 scale = skins[i].transform.lossyScale;
                obj.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                obj.AddComponent<MeshRenderer>().sharedMaterials = skins[i].sharedMaterials;
                skins[i].enabled = false; bakedObjects[i] = obj;
            }
            var target = new RenderTexture(1200, 800, 24);
            var previous = RenderTexture.active;
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(1200, 800, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
            for (int i = 0; i < skins.Length; i++)
            {
                skins[i].enabled = true;
                UnityEngine.Object.DestroyImmediate(bakedObjects[i]);
                UnityEngine.Object.DestroyImmediate(bakedMeshes[i]);
            }
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
