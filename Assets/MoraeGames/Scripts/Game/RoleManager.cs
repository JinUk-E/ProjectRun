using UnityEngine;
using System.Collections.Generic;
using MoraeGames.Core;

namespace MoraeGames.Game
{
    /// <summary>
    /// Manages player role assignments (Cop/Robber) and character selection.
    /// Singleton that persists across scenes.
    /// </summary>
    public class RoleManager : SingletonBase<RoleManager>
    {
        public enum PlayerRole
        {
            None,
            Cop,
            Robber
        }

        private Dictionary<string, PlayerRole> playerRoles = new Dictionary<string, PlayerRole>();
        private Dictionary<string, int> playerCharacterIndex = new Dictionary<string, int>();

        /// <summary>Assigns a role to the specified player.</summary>
        public void AssignRole(string playerId, PlayerRole role)
        {
            playerRoles[playerId] = role;
            // Reset character selection when role changes
            playerCharacterIndex[playerId] = 0;
            Debug.Log($"[RoleManager] Player {playerId} assigned role: {role}");
        }

        /// <summary>Sets the selected character index for the specified player.</summary>
        public void SelectCharacter(string playerId, int characterIndex)
        {
            playerCharacterIndex[playerId] = characterIndex;
            Debug.Log($"[RoleManager] Player {playerId} selected character index: {characterIndex}");
        }

        /// <summary>Returns the selected character index for the specified player.</summary>
        public int GetSelectedCharacterIndex(string playerId)
        {
            if (playerCharacterIndex.TryGetValue(playerId, out int index))
                return index;
            return 0;
        }

        /// <summary>Returns the assigned role for the specified player.</summary>
        public PlayerRole GetRole(string playerId)
        {
            if (playerRoles.TryGetValue(playerId, out PlayerRole role))
            {
                return role;
            }
            return PlayerRole.None;
        }
    }
}
