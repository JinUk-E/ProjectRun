using UnityEngine;
using MoraeGames.Player;

namespace MoraeGames.Ability
{
    [CreateAssetMenu(menuName = "Abilities/Cop/SpeedBoost")]
    public class SpeedBoostData : AbilityDataSO
    {
        [Header("Speed Boost")]
        public float speedMultiplier = 1.5f;

        public override void OnActivate(AbilityHolder holder)
        {
            // Modifier stack composes safely with zones/debuffs and reverts in any order
            PlayerController pc = holder.GetComponent<PlayerController>();
            if (pc != null)
                pc.ApplySpeedModifier(this, speedMultiplier);
        }

        public override void OnDeactivate(AbilityHolder holder)
        {
            PlayerController pc = holder.GetComponent<PlayerController>();
            if (pc != null)
                pc.RemoveSpeedModifier(this);
        }
    }
}
