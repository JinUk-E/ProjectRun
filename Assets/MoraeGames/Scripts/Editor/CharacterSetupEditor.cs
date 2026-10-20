using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AI;
using Unity.Netcode;
using MoraeGames.Core;
using MoraeGames.Player;
using MoraeGames.Ability;
using MoraeGames.Network;

namespace MoraeGames.Editor
{
    public class CharacterSetupEditor : MonoBehaviour
    {
        [MenuItem("MoraeGames/Setup Character Prefabs")]
        public static void GenerateCharacters()
        {
            string folderPath = "Assets/MoraeGames/Prefabs/Characters";

            if (!AssetDatabase.IsValidFolder("Assets/MoraeGames/Prefabs"))
                AssetDatabase.CreateFolder("Assets/MoraeGames", "Prefabs");
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder("Assets/MoraeGames/Prefabs", "Characters");

            // Ensure Tags exist
            AddTag("Cop");
            AddTag("Robber");
            AddTag("SmokeZone");

            string matFolderPath = folderPath + "/Materials";
            if (!AssetDatabase.IsValidFolder(matFolderPath))
                AssetDatabase.CreateFolder(folderPath, "Materials");

            // Create ability SO assets
            string abilityFolderPath = "Assets/MoraeGames/Prefabs/Abilities";
            if (!AssetDatabase.IsValidFolder(abilityFolderPath))
            {
                if (!AssetDatabase.IsValidFolder("Assets/MoraeGames/Prefabs"))
                    AssetDatabase.CreateFolder("Assets/MoraeGames", "Prefabs");
                AssetDatabase.CreateFolder("Assets/MoraeGames/Prefabs", "Abilities");
            }
            CreateAbilityAssets(abilityFolderPath);

            // Base Materials (Not directly applied, just for reference color)
            Material copBaseMat = CreateMaterial("CopBase_Ref", Color.blue, matFolderPath);
            Material robberBaseMat = CreateMaterial("RobberBase_Ref", Color.red, matFolderPath);
            Material cyanNoseMat = CreateMaterial("Nose_Cyan", Color.cyan, matFolderPath);
            Material blackNoseMat = CreateMaterial("Nose_Black", Color.black, matFolderPath);

            // Generate 3 Cops
            for (int i = 1; i <= 3; i++)
            {
                GenerateCharacterPrefab($"Cop_{i}", true, copBaseMat, cyanNoseMat, i, folderPath, matFolderPath);
            }

            // Generate 5 Robbers
            for (int i = 1; i <= 5; i++)
            {
                GenerateCharacterPrefab($"Robber_{i}", false, robberBaseMat, blackNoseMat, i, folderPath, matFolderPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Successfully generated 3 Cop and 5 Robber prefabs in {folderPath}");
        }

        private static void GenerateCharacterPrefab(string name, bool isCop, Material baseMat, Material noseMat, int index, string path, string matPath)
        {
            // Create root
            GameObject go = new GameObject(name);
            
            // Physical representation (Capsule is standard)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform);
            body.transform.localPosition = new Vector3(0, 1f, 0); // Stand on ground

            // Differentiate visually
            Material instanceMat = new Material(baseMat);
            float shade = 1f - (index * 0.15f); // Different shades
            if (instanceMat.HasProperty("_BaseColor"))
                instanceMat.SetColor("_BaseColor", instanceMat.GetColor("_BaseColor") * shade);
            else
                instanceMat.color *= shade;

            // Slight gloss so characters don't look like flat default primitives
            if (instanceMat.HasProperty("_Smoothness")) instanceMat.SetFloat("_Smoothness", 0.55f);
            else if (instanceMat.HasProperty("_Glossiness")) instanceMat.SetFloat("_Glossiness", 0.55f);

            // Save the instance material as an asset to ensure the prefab retains the reference!
            string savedMatPath = $"{matPath}/{name}_Mat.mat";
            AssetDatabase.CreateAsset(instanceMat, savedMatPath);

            body.GetComponent<MeshRenderer>().material = instanceMat;

            // Direction indicator (Nose/Glasses)
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "DirectionIndicator";
            nose.transform.SetParent(body.transform);
            nose.transform.localPosition = new Vector3(0, 0.42f, 0.42f);
            nose.transform.localScale = new Vector3(0.35f, 0.15f, 0.35f);
            DestroyImmediate(nose.GetComponent<Collider>());
            nose.GetComponent<MeshRenderer>().material = noseMat;

            // Role-specific details so characters read at a glance instead of
            // looking like bare default capsules
            AddCharacterDetails(body, isCop, matPath);

            // Components
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation; // Prevent falling over
            rb.useGravity = false; // NavMeshAgent handles height
            rb.isKinematic = true; // AI agents use NavMesh

            // Add Collider to Root for better detection and trigger events
            CapsuleCollider col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 1f, 0);
            col.radius = 0.5f;
            col.height = 2f;
            col.isTrigger = true; // Use trigger for "touch" detection

            // Solid collision volume (child, same rigidbody = compound collider) so
            // locally-controlled characters physically collide with walls/doors.
            // Slightly smaller than the root trigger so touch detection fires first.
            GameObject collision = new GameObject("Collision");
            collision.transform.SetParent(go.transform);
            collision.transform.localPosition = Vector3.zero;
            CapsuleCollider solidCol = collision.AddComponent<CapsuleCollider>();
            solidCol.center = new Vector3(0, 1f, 0);
            solidCol.radius = 0.45f;
            solidCol.height = 2f;

            // Remove collider from body to avoid double collision or issues
            DestroyImmediate(body.GetComponent<Collider>());

            NavMeshAgent agent = go.AddComponent<NavMeshAgent>();
            agent.speed = isCop ? GameConstants.CopNavSpeed : GameConstants.RobberNavSpeed;
            
            // Add specific scripts and AbilityHolder with SO reference
            string abilityFolder = "Assets/MoraeGames/Prefabs/Abilities";
            AbilityHolder holder = go.AddComponent<AbilityHolder>();

            if (isCop)
            {
                go.AddComponent<CopController>();
                go.AddComponent<AICopController>();
                // Cycle through cop ability SOs
                string[] copAbilities = { "SpeedBoost", "AreaScan", "CCProjectile" };
                string abilityName = copAbilities[(index - 1) % copAbilities.Length];
                holder.abilityData = AssetDatabase.LoadAssetAtPath<AbilityDataSO>($"{abilityFolder}/{abilityName}.asset");
            }
            else
            {
                go.AddComponent<RobberController>();
                go.AddComponent<AIRobberController>();
                // Cycle through robber ability SOs
                string[] robberAbilities = { "SpeedBurst", "SmokeBomb", "Disguise", "DecoyClone", "EscapeTrap" };
                string abilityName = robberAbilities[(index - 1) % robberAbilities.Length];
                holder.abilityData = AssetDatabase.LoadAssetAtPath<AbilityDataSO>($"{abilityFolder}/{abilityName}.asset");
            }

            go.tag = isCop ? "Cop" : "Robber";

            // Network components: inert in offline play (never spawned), active once
            // a session runs. NGO's DefaultNetworkPrefabs list auto-collects prefabs
            // carrying NetworkObject, so no manual registration is needed.
            go.AddComponent<NetworkObject>();
            go.AddComponent<OwnerNetworkTransform>();
            go.AddComponent<NetworkCharacter>();
            // Robbers carry replicated arrest/invincible/disguise state
            if (!isCop) go.AddComponent<NetworkRobberSync>();

            // Save as Prefab
            string localPath = $"{path}/{name}.prefab";
            // Make sure any existing prefab is replaced
            PrefabUtility.SaveAsPrefabAsset(go, localPath, out bool success);
            
            if (success)
                Debug.Log($"Created prefab: {localPath}");
            else
                Debug.LogError($"Failed to create prefab: {localPath}");

            // Cleanup scene object
            DestroyImmediate(go);
        }

        private static void AddCharacterDetails(GameObject body, bool isCop, string matPath)
        {
            Material dark = GetOrCreateMaterial("Accent_Dark", new Color(0.10f, 0.10f, 0.14f), matPath);
            Material navy = GetOrCreateMaterial("Accent_Navy", new Color(0.12f, 0.20f, 0.45f), matPath);
            Material gold = GetOrCreateMaterial("Accent_Gold", new Color(1f, 0.80f, 0.25f), matPath);
            Material white = GetOrCreateMaterial("Accent_White", new Color(0.95f, 0.95f, 0.95f), matPath);
            Material grey = GetOrCreateMaterial("Accent_Grey", new Color(0.22f, 0.22f, 0.26f), matPath);

            if (isCop)
            {
                // Police cap: brim + crown, with a gold badge on the chest
                CreateDetail(PrimitiveType.Cylinder, "Hat_Brim", body,
                    new Vector3(0f, 0.95f, 0.08f), new Vector3(0.80f, 0.03f, 0.80f), navy);
                CreateDetail(PrimitiveType.Cylinder, "Hat_Crown", body,
                    new Vector3(0f, 1.05f, 0f), new Vector3(0.55f, 0.12f, 0.55f), navy);
                CreateDetail(PrimitiveType.Sphere, "Badge", body,
                    new Vector3(0f, 0.15f, 0.48f), Vector3.one * 0.12f, gold);

                CreateDetail(PrimitiveType.Sphere, "Eye_L", body,
                    new Vector3(-0.15f, 0.62f, 0.45f), Vector3.one * 0.11f, dark);
                CreateDetail(PrimitiveType.Sphere, "Eye_R", body,
                    new Vector3(0.15f, 0.62f, 0.45f), Vector3.one * 0.11f, dark);
            }
            else
            {
                // Robber beanie + classic eye-mask band with white eyes
                CreateDetail(PrimitiveType.Sphere, "Beanie", body,
                    new Vector3(0f, 0.92f, 0f), new Vector3(0.72f, 0.42f, 0.72f), grey);
                CreateDetail(PrimitiveType.Cube, "EyeMask", body,
                    new Vector3(0f, 0.62f, 0.30f), new Vector3(0.62f, 0.16f, 0.32f), dark);
                CreateDetail(PrimitiveType.Sphere, "Eye_L", body,
                    new Vector3(-0.15f, 0.62f, 0.47f), Vector3.one * 0.11f, white);
                CreateDetail(PrimitiveType.Sphere, "Eye_R", body,
                    new Vector3(0.15f, 0.62f, 0.47f), Vector3.one * 0.11f, white);
            }
        }

        private static void CreateDetail(PrimitiveType type, string name, GameObject body,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject detail = GameObject.CreatePrimitive(type);
            detail.name = name;
            detail.transform.SetParent(body.transform);
            detail.transform.localPosition = localPosition;
            detail.transform.localScale = localScale;
            DestroyImmediate(detail.GetComponent<Collider>());
            detail.GetComponent<MeshRenderer>().material = material;
        }

        private static Material GetOrCreateMaterial(string name, Color color, string matPath)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>($"{matPath}/{name}.mat");
            if (existing != null) return existing;
            return CreateMaterial(name, color, matPath);
        }

        private static void CreateAbilityAssets(string folderPath)
        {
            // Cop abilities
            CreateOrUpdateAbilitySO<SpeedBoostData>(folderPath, "SpeedBoost", "Speed Boost",
                "Increases movement speed temporarily.", so => {
                    so.cooldown = GameConstants.CopSpeedBoostCooldown;
                    so.duration = GameConstants.CopSpeedBoostDuration;
                    so.speedMultiplier = GameConstants.CopSpeedBoostMultiplier;
                });
            CreateOrUpdateAbilitySO<AreaScanData>(folderPath, "AreaScan", "Area Scan",
                "Reveals nearby robbers with markers.", so => {
                    so.cooldown = GameConstants.CopAreaScanCooldown;
                    so.duration = GameConstants.CopAreaScanDuration;
                    so.scanRadius = GameConstants.CopAreaScanRadius;
                });
            CreateOrUpdateAbilitySO<CCProjectileData>(folderPath, "CCProjectile", "CC Projectile",
                "Fires a projectile that slows the target.", so => {
                    so.cooldown = GameConstants.CCProjectileCooldown;
                    so.duration = 0.1f;
                    so.projectileSpeed = GameConstants.CCProjectileSpeed;
                    so.slowDuration = GameConstants.CCSlowDuration;
                    so.slowMultiplier = GameConstants.CCSlowMultiplier;
                });
            CreateOrUpdateAbilitySO<PrisonLockdownData>(folderPath, "PrisonLockdown", "Prison Lockdown",
                "Creates a forcefield around the prison.", so => {
                    so.cooldown = GameConstants.PrisonLockdownCooldown;
                    so.duration = GameConstants.PrisonLockdownDuration;
                    so.lockdownRadius = GameConstants.PrisonLockdownRadius;
                });

            // Robber abilities
            CreateOrUpdateAbilitySO<EscapeTrapData>(folderPath, "EscapeTrap", "추격 방해 덫",
                "경찰을 느리게 하는 덫을 설치합니다.", so => { so.cooldown = 14; so.duration = 6; so.radius = 2.5f; so.slowMultiplier = .45f; });
            CreateOrUpdateAbilitySO<SpeedBurstData>(folderPath, "SpeedBurst", "Speed Burst",
                "Doubles movement speed briefly.", so => {
                    so.cooldown = GameConstants.RobberSpeedBurstCooldown;
                    so.duration = GameConstants.RobberSpeedBurstDuration;
                    so.speedMultiplier = GameConstants.RobberSpeedBurstMultiplier;
                });
            CreateOrUpdateAbilitySO<SmokeBombData>(folderPath, "SmokeBomb", "Smoke Bomb",
                "Creates a smoke cloud that blocks detection.", so => {
                    so.cooldown = GameConstants.RobberSmokeBombCooldown;
                    so.duration = GameConstants.RobberSmokeBombDuration;
                    so.smokeRadius = GameConstants.RobberSmokeBombRadius;
                });
            CreateOrUpdateAbilitySO<DisguiseData>(folderPath, "Disguise", "Disguise",
                "Disguises as a cop to avoid detection.", so => {
                    so.cooldown = GameConstants.RobberDisguiseCooldown;
                    so.duration = GameConstants.RobberDisguiseDuration;
                });
            CreateOrUpdateAbilitySO<DecoyCloneData>(folderPath, "DecoyClone", "Decoy Clone",
                "Spawns a decoy that moves randomly.", so => {
                    so.cooldown = GameConstants.DecoyCloneCooldown;
                    so.duration = GameConstants.DecoyCloneDuration;
                    so.decoySpeed = GameConstants.DecoyCloneSpeed;
                });

            Debug.Log($"Ability SO assets created/updated in {folderPath}");
        }

        private static void CreateOrUpdateAbilitySO<T>(string folder, string fileName, string displayName, string desc, System.Action<T> configure) where T : AbilityDataSO
        {
            string assetPath = $"{folder}/{fileName}.asset";
            T existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(existing, assetPath);
            }
            existing.abilityName = displayName;
            existing.description = desc;
            configure(existing);
            EditorUtility.SetDirty(existing);
        }

        private static Material CreateMaterial(string name, Color baseColor, string targetPath)
        {
            Shader shader = null;
            if (GraphicsSettings.currentRenderPipeline != null)
                shader = GraphicsSettings.currentRenderPipeline.defaultShader;
            else
                shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material mat = new Material(shader);
            
            if (shader.name.Contains("Universal Render Pipeline"))
                mat.SetColor("_BaseColor", baseColor);
            else
                mat.color = baseColor;
                
            AssetDatabase.CreateAsset(mat, $"{targetPath}/{name}.mat");
            return mat;
        }

        private static void AddTag(string tag)
        {
            UnityEngine.Object[] asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if ((asset != null) && (asset.Length > 0))
            {
                SerializedObject so = new SerializedObject(asset[0]);
                SerializedProperty tags = so.FindProperty("tags");

                for (int i = 0; i < tags.arraySize; ++i)
                {
                    if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                    {
                        return; // Tag already exists
                    }
                }

                tags.InsertArrayElementAtIndex(0);
                tags.GetArrayElementAtIndex(0).stringValue = tag;
                so.ApplyModifiedProperties();
                so.Update();
            }
        }
    }
}
