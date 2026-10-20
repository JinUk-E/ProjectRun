using UnityEngine;

namespace MoraeGames.Skill
{
    [CreateAssetMenu(fileName = "NewSkill", menuName = "SkillTree/SkillNode")]
    public class SkillNode : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea]
        public string description;
        public Sprite icon;

        public int maxLevel = 1;

        [Tooltip("Skill points required to unlock this node.")]
        public int cost = 1;

        public SkillNode[] prerequisites;

        public enum ModifierType
        {
            MoveSpeed,
            Health,
            InteractionSpeed,
            ArrestRange,
            DetectionRadius,
            AbilityCooldownReduction,
            LootSpeed,
            RescueSpeed,
        }

        [System.Serializable]
        public struct SkillModifier
        {
            public ModifierType type;
            public float value;
        }

        public SkillModifier[] modifiers;
    }
}
