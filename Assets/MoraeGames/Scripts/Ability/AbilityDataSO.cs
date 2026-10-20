using UnityEngine;

namespace MoraeGames.Ability
{
    /// <summary>
    /// Base ScriptableObject for ability data and behavior definition.
    /// Contains configuration (cooldown, duration) and virtual methods for ability logic.
    /// All runtime state lives in AbilityHolder, not here — SOs are shared assets.
    /// </summary>
    public abstract class AbilityDataSO : ScriptableObject
    {
        [Header("Ability Info")]
        public string abilityName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("Timing")]
        public float cooldown = 10f;
        public float duration = 3f;

        /// <summary>Called when the ability is activated. Use holder to access the character and store runtime objects.</summary>
        public abstract void OnActivate(AbilityHolder holder);

        /// <summary>Called when the ability expires or is manually deactivated.</summary>
        public abstract void OnDeactivate(AbilityHolder holder);

        /// <summary>Called every frame while the ability is active. Override for continuous effects.</summary>
        public virtual void OnActiveUpdate(AbilityHolder holder) { }
    }
}
