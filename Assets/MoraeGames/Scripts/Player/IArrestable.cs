namespace MoraeGames.Player
{
    /// <summary>
    /// Interface for characters that can be arrested and rescued.
    /// </summary>
    public interface IArrestable
    {
        /// <summary>Whether this character is currently arrested.</summary>
        bool IsArrested { get; }
        /// <summary>Whether this character has temporary invincibility (post-rescue).</summary>
        bool IsInvincible { get; }
        /// <summary>Whether this character is disguised (hidden from AI detection, still arrestable on touch).</summary>
        bool IsDisguised { get; }
        /// <summary>Called when this character is arrested.</summary>
        void OnArrested();
        /// <summary>Called when this character is rescued from prison.</summary>
        void OnRescued();
    }
}
