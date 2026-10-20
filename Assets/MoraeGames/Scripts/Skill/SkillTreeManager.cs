using UnityEngine;
using System.Collections.Generic;
using MoraeGames.Core;
using MoraeGames.Player;
using MoraeGames.Ability;
using MoraeGames.Game;

namespace MoraeGames.Skill
{
    /// <summary>
    /// Global skill tree progression. Skill points are earned from match results and
    /// spent to unlock SkillNode assets (cost + prerequisite checks). Progress persists
    /// via PlayerPrefs; skill assets are resolved from Resources/Skills so the
    /// auto-created singleton works without inspector wiring.
    /// </summary>
    public class SkillTreeManager : SingletonBase<SkillTreeManager>
    {
        public List<SkillNode> unlockedSkills = new List<SkillNode>();

        /// <summary>Currency earned from matches, spent on unlocks.</summary>
        public int SkillPoints { get; private set; }

        private const string PointsKey = "SkillTree_Points";
        private const string UnlockedKey = "SkillTree_UnlockedIds";
        private const string SkillResourcePath = "Skills";
        private const int WinPoints = 3;
        private const int LossPoints = 1;

        private SkillNode[] _allSkills;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return; // duplicate instance being destroyed
            LoadProgress();
        }

        /// <summary>All SkillNode assets under any Resources/Skills folder.</summary>
        public SkillNode[] AllSkills
        {
            get
            {
                if (_allSkills == null)
                    _allSkills = Resources.LoadAll<SkillNode>(SkillResourcePath);
                return _allSkills;
            }
        }

        /// <summary>Awards match-end skill points (win pays more) and persists.</summary>
        public void AwardMatchPoints(bool won)
        {
            int amount = won ? WinPoints : LossPoints;
            SkillPoints += amount;
            SaveProgress();
            Debug.Log($"[SkillTree] Awarded {amount} skill points ({(won ? "win" : "loss")}). Total: {SkillPoints}");
        }

        public bool IsSkillUnlocked(SkillNode skill)
        {
            return unlockedSkills.Contains(skill);
        }

        /// <summary>Whether the skill can be unlocked right now (not owned, affordable, prerequisites met).</summary>
        public bool CanUnlock(SkillNode skill)
        {
            if (skill == null || IsSkillUnlocked(skill)) return false;
            if (SkillPoints < skill.cost) return false;

            if (skill.prerequisites != null)
            {
                foreach (var prerequisite in skill.prerequisites)
                {
                    if (prerequisite != null && !IsSkillUnlocked(prerequisite))
                        return false;
                }
            }
            return true;
        }

        /// <summary>Spends points to unlock the skill. Returns false if requirements aren't met.</summary>
        public bool TryUnlock(SkillNode skill)
        {
            if (!CanUnlock(skill)) return false;

            SkillPoints -= skill.cost;
            unlockedSkills.Add(skill);
            SaveProgress();
            Debug.Log($"[SkillTree] Unlocked '{skill.displayName}' ({skill.id}). Points left: {SkillPoints}");
            return true;
        }

        public void ApplySkills(PlayerController player)
        {
            // Local saves are not account-authoritative. Online matches use equal base stats.
            if (MoraeGames.Network.NetworkSessionManager.IsNetworked || player == null) return;
            // Dedupe by id so a duplicate list entry can never double-apply modifiers
            var appliedIds = new HashSet<string>();
            foreach (var skill in unlockedSkills)
            {
                if (skill == null || !appliedIds.Add(skill.id)) continue;
                ApplySkillModifiers(player, skill);
            }
        }

        private void ApplySkillModifiers(PlayerController player, SkillNode skill)
        {
            foreach (var modifier in skill.modifiers)
            {
                switch (modifier.type)
                {
                    case SkillNode.ModifierType.MoveSpeed:
                        player.SetBaseMoveSpeed(player.baseMoveSpeed + modifier.value);
                        break;
                    case SkillNode.ModifierType.Health:
                        player.health += modifier.value;
                        break;
                    case SkillNode.ModifierType.InteractionSpeed:
                        // Applies to robber interaction range and loot/rescue speed
                        var robber = player as RobberController;
                        if (robber != null)
                            robber.interactionRange += modifier.value;
                        break;
                    case SkillNode.ModifierType.ArrestRange:
                        var cop = player as CopController;
                        if (cop != null) cop.arrestRange += modifier.value;
                        break;
                    case SkillNode.ModifierType.DetectionRadius:
                        // Apply to AI cop detection or AI robber detection
                        var aiCop = player.GetComponent<AICopController>();
                        if (aiCop != null) aiCop.detectionRadius += modifier.value;
                        var aiRobber = player.GetComponent<AIRobberController>();
                        if (aiRobber != null) aiRobber.detectionRadius += modifier.value;
                        break;
                    case SkillNode.ModifierType.AbilityCooldownReduction:
                        AbilityHolder abilityHolder = player.GetComponent<AbilityHolder>();
                        if (abilityHolder != null)
                            abilityHolder.ReduceCooldown(modifier.value);
                        break;
                    case SkillNode.ModifierType.LootSpeed:
                        // Scene objects are fresh per match load, so one application per scene is safe
                        var lootSpots = Object.FindObjectsByType<LootSpot>(FindObjectsSortMode.None);
                        foreach (var spot in lootSpots)
                            spot.robberyDuration = Mathf.Max(1f, spot.robberyDuration - modifier.value);
                        break;
                    case SkillNode.ModifierType.RescueSpeed:
                        var prisonZone = Object.FindFirstObjectByType<PrisonZone>();
                        if (prisonZone != null)
                            prisonZone.rescueDuration = Mathf.Max(1f, prisonZone.rescueDuration - modifier.value);
                        break;
                    default:
                        Debug.LogWarning($"Unhandled skill modifier type: {modifier.type}");
                        break;
                }
            }
        }

        private void SaveProgress()
        {
            PlayerPrefs.SetInt(PointsKey, SkillPoints);

            var ids = new List<string>(unlockedSkills.Count);
            foreach (var skill in unlockedSkills)
            {
                if (skill != null && !string.IsNullOrEmpty(skill.id))
                    ids.Add(skill.id);
            }
            PlayerPrefs.SetString(UnlockedKey, string.Join(";", ids));
            PlayerPrefs.Save();
        }

        private void LoadProgress()
        {
            SkillPoints = PlayerPrefs.GetInt(PointsKey, 0);

            unlockedSkills.Clear();
            string joined = PlayerPrefs.GetString(UnlockedKey, "");
            if (string.IsNullOrEmpty(joined)) return;

            var skillsById = new Dictionary<string, SkillNode>();
            foreach (var skill in AllSkills)
            {
                if (skill != null && !string.IsNullOrEmpty(skill.id))
                    skillsById[skill.id] = skill;
            }

            foreach (var id in joined.Split(';'))
            {
                if (skillsById.TryGetValue(id, out SkillNode skill))
                    unlockedSkills.Add(skill);
            }

            Debug.Log($"[SkillTree] Loaded {unlockedSkills.Count} unlocked skills, {SkillPoints} points.");
        }
    }
}
