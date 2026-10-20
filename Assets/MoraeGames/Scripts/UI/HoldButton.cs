using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MoraeGames.Core;

namespace MoraeGames.UI
{
    /// <summary>
    /// On-screen action button that reports press (and hold, for Interact)
    /// to <see cref="MobileInput"/>. Darkens while pressed for feedback.
    /// </summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public enum ButtonAction
        {
            Ability,   // Q equivalent
            Interact,  // F (rob/rescue) or E (arrest) equivalent
            Ping       // Z equivalent
        }

        public ButtonAction action;

        private Image _image;
        private Color _normalColor;

        private void Awake()
        {
            _image = GetComponent<Image>();
            if (_image != null) _normalColor = _image.color;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            switch (action)
            {
                case ButtonAction.Ability:
                    MobileInput.PressAbility();
                    break;
                case ButtonAction.Interact:
                    MobileInput.PressInteract();
                    MobileInput.SetInteractHeld(true);
                    break;
                case ButtonAction.Ping:
                    MobileInput.PressPing();
                    break;
            }

            if (_image != null)
                _image.color = new Color(_normalColor.r * 0.6f, _normalColor.g * 0.6f, _normalColor.b * 0.6f, _normalColor.a);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        private void OnDisable() => Release();

        private void Release()
        {
            if (action == ButtonAction.Interact)
                MobileInput.SetInteractHeld(false);
            if (_image != null)
                _image.color = _normalColor;
        }
    }
}
