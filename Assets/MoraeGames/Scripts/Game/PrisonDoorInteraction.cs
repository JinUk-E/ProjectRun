using UnityEngine;

namespace MoraeGames.Game
{
    /// <summary>The door's interaction trigger; the holding area's trigger cannot rescue prisoners.</summary>
    public sealed class PrisonDoorInteraction : MonoBehaviour
    {
        public PrisonZone prison;
    }
}
