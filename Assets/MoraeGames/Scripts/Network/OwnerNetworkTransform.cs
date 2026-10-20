using UnityEngine;
using Unity.Netcode.Components;

namespace MoraeGames.Network
{
    /// <summary>
    /// Server-authoritative transform. The legacy class name preserves existing prefab references.
    /// Clients send input through NetworkCharacter; they never choose position or speed.
    /// </summary>
    [DisallowMultipleComponent]
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => true;

        public void TeleportOnAuthority(Vector3 position)
        {
            if (IsSpawned && IsServer)
                Teleport(position, transform.rotation, transform.localScale);
        }
    }
}
