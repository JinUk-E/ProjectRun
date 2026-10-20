using UnityEditor;
using UnityEngine;
using MoraeGames.Skill;

namespace MoraeGames.Editor
{
    /// <summary>
    /// Creates the starter SkillNode assets under Resources/Skills so the
    /// SkillTreeManager can resolve them by id at runtime (including on load).
    /// </summary>
    public class SkillSetupEditor : MonoBehaviour
    {
        private const string SkillFolder = "Assets/MoraeGames/Resources/Skills";

        [MenuItem("MoraeGames/Setup Skill Assets")]
        public static void CreateSkillAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/MoraeGames/Resources"))
                AssetDatabase.CreateFolder("Assets/MoraeGames", "Resources");
            if (!AssetDatabase.IsValidFolder(SkillFolder))
                AssetDatabase.CreateFolder("Assets/MoraeGames/Resources", "Skills");

            // Tier 1 (no prerequisites)
            SkillNode swiftFeet = CreateOrUpdate("swift_feet", "Swift Feet", "이동 속도 +0.5", 1, null,
                Mod(SkillNode.ModifierType.MoveSpeed, 0.5f));
            SkillNode conditioning = CreateOrUpdate("conditioning", "Conditioning", "체력 +25", 1, null,
                Mod(SkillNode.ModifierType.Health, 25f));
            SkillNode quickHands = CreateOrUpdate("quick_hands", "Quick Hands", "약탈 시간 -1초", 2, null,
                Mod(SkillNode.ModifierType.LootSpeed, 1f));
            SkillNode longArm = CreateOrUpdate("long_arm", "Long Arm", "체포 범위 +0.5", 2, null,
                Mod(SkillNode.ModifierType.ArrestRange, 0.5f));

            // Tier 2 (with prerequisites)
            CreateOrUpdate("sprinter", "Sprinter", "이동 속도 +0.5 (추가)", 2, new[] { swiftFeet },
                Mod(SkillNode.ModifierType.MoveSpeed, 0.5f));
            CreateOrUpdate("rescue_expert", "Rescue Expert", "구출 시간 -1초", 2, new[] { quickHands },
                Mod(SkillNode.ModifierType.RescueSpeed, 1f));
            CreateOrUpdate("tactician", "Tactician", "스킬 쿨다운 -2초", 3, new[] { conditioning },
                Mod(SkillNode.ModifierType.AbilityCooldownReduction, 2f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SkillSetup] Skill assets created/updated in {SkillFolder}");
        }

        private static SkillNode.SkillModifier Mod(SkillNode.ModifierType type, float value)
        {
            return new SkillNode.SkillModifier { type = type, value = value };
        }

        private static SkillNode CreateOrUpdate(string id, string displayName, string description,
            int cost, SkillNode[] prerequisites, params SkillNode.SkillModifier[] modifiers)
        {
            string assetPath = $"{SkillFolder}/{id}.asset";
            SkillNode node = AssetDatabase.LoadAssetAtPath<SkillNode>(assetPath);
            if (node == null)
            {
                node = ScriptableObject.CreateInstance<SkillNode>();
                AssetDatabase.CreateAsset(node, assetPath);
            }

            node.id = id;
            node.displayName = displayName;
            node.description = description;
            node.cost = cost;
            node.maxLevel = 1;
            node.prerequisites = prerequisites ?? new SkillNode[0];
            node.modifiers = modifiers;
            EditorUtility.SetDirty(node);
            return node;
        }
    }
}
