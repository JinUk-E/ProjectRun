using UnityEngine;
using MoraeGames.Player;

namespace MoraeGames.Ability
{
    [CreateAssetMenu(menuName = "Abilities/Robber/SpeedBurst")]
    public class SpeedBurstData : AbilityDataSO
    {
        [Header("Speed Burst")]
        public float speedMultiplier = 2f;

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
