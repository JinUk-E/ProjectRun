using UnityEngine;
using System.Collections.Generic;
using MoraeGames.Core;
using MoraeGames.Player;

namespace MoraeGames.Game
{
    /// <summary>
    /// Trigger volume that dynamically modifies player movement speed while overlapping.
    /// Used for Boost Pads and Mud Traps.
    /// Uses the PlayerController speed-modifier stack (never raw multiply/divide) so the
    /// effect composes with buffs/debuffs and always reverts, and gates on the component
    /// instead of tags so a disguised (retagged) character cannot break enter/exit symmetry.
    /// </summary>
    public class EnvironmentZone : MonoBehaviour
    {
        public enum ZoneType
        {
            BoostPad,
            MudTrap
        }

        [Header("Zone Settings")]
        public ZoneType zoneType = ZoneType.BoostPad;

        private float SpeedMultiplier =>
            zoneType == ZoneType.BoostPad ? GameConstants.BoostPadMultiplier : GameConstants.MudTrapMultiplier;

        // Characters can have multiple colliders — refcount so the modifier is applied
        // once on first enter and removed only when the last collider exits.
        private readonly Dictionary<PlayerController, int> _overlapCounts = new Dictionary<PlayerController, int>();

        private void OnTriggerEnter(Collider other)
        {
            PlayerController pc = other.transform.root.GetComponent<PlayerController>();
            if (pc == null) return;

            if (_overlapCounts.TryGetValue(pc, out int count))
            {
                _overlapCounts[pc] = count + 1;
            }
            else
            {
                _overlapCounts[pc] = 1;
                pc.ApplySpeedModifier(this, SpeedMultiplier);
                Debug.Log($"[EnvironmentZone] {pc.name} entered {zoneType} zone!");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerController pc = other.transform.root.GetComponent<PlayerController>();
            if (pc == null || !_overlapCounts.TryGetValue(pc, out int count)) return;

            if (count <= 1)
            {
                _overlapCounts.Remove(pc);
                pc.RemoveSpeedModifier(this);
            }
            else
            {
                _overlapCounts[pc] = count - 1;
            }
        }
    }
}
