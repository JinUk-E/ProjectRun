using UnityEngine;

namespace MoraeGames.Core
{
    /// <summary>
    /// Bridge between on-screen touch controls (virtual joystick, action buttons)
    /// and gameplay input reads. UI components write; controllers read.
    /// Press events are consume-once so a single tap triggers exactly one action.
    /// </summary>
    public static class MobileInput
    {
        /// <summary>Joystick direction, magnitude 0-1. Zero when not touched.</summary>
        public static Vector2 MoveDirection { get; set; }

        /// <summary>Whether the interact button is currently held (rob/rescue hold).</summary>
        public static bool InteractHeld { get; private set; }

        private static bool _abilityPressed;
        private static bool _interactPressed;
        private static bool _pingPressed;

        public static void PressAbility() => _abilityPressed = true;
        public static void PressInteract() => _interactPressed = true;
        public static void PressPing() => _pingPressed = true;
        public static void SetInteractHeld(bool held) => InteractHeld = held;

        public static bool ConsumeAbilityPressed()
        {
            bool pressed = _abilityPressed;
            _abilityPressed = false;
            return pressed;
        }

        public static bool ConsumeInteractPressed()
        {
            bool pressed = _interactPressed;
            _interactPressed = false;
            return pressed;
        }

        public static bool ConsumePingPressed()
        {
            bool pressed = _pingPressed;
            _pingPressed = false;
            return pressed;
        }

        /// <summary>Clears all input state. Called when the controls UI unloads.</summary>
        public static void ResetAll()
        {
            MoveDirection = Vector2.zero;
            InteractHeld = false;
            _abilityPressed = false;
            _interactPressed = false;
            _pingPressed = false;
        }
    }
}
