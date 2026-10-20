using UnityEngine;
using UnityEngine.EventSystems;
using MoraeGames.Core;

namespace MoraeGames.UI
{
    /// <summary>
    /// On-screen virtual joystick. Writes its direction (magnitude 0-1) to
    /// <see cref="MobileInput.MoveDirection"/> while touched; resets on release.
    /// Attach to the joystick background image; assign the handle RectTransform.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("References")]
        public RectTransform handle;

        [Tooltip("Handle travel radius in local units. 0 = auto (40% of background width).")]
        public float handleRange = 0f;
        [Range(0f, 0.3f)] public float deadZone = 0.08f;

        private RectTransform _rect;
        private float _range;
        private int _pointerId = int.MinValue;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _range = Mathf.Max(1f, handleRange > 0f ? handleRange : _rect.rect.width * 0.4f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId != int.MinValue) return;
            _pointerId = eventData.pointerId;
            UpdateInput(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId) UpdateInput(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId) ResetInput();
        }

        private void OnDisable() => ResetInput();
        private void OnApplicationFocus(bool focused) { if (!focused) ResetInput(); }
        private void OnApplicationPause(bool paused) { if (paused) ResetInput(); }

        public static Vector2 FilterInput(Vector2 input, float deadZone)
        {
            input = Vector2.ClampMagnitude(input, 1f);
            float magnitude = input.magnitude;
            float threshold = Mathf.Clamp(deadZone, 0f, 0.3f);
            return magnitude <= threshold ? Vector2.zero : input / magnitude * ((magnitude - threshold) / (1f - threshold));
        }

        private void UpdateInput(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                Vector2 input = Vector2.ClampMagnitude(localPoint / _range, 1f);
                if (handle != null) handle.anchoredPosition = input * _range;
                MobileInput.MoveDirection = FilterInput(input, deadZone);
            }
        }

        private void ResetInput()
        {
            _pointerId = int.MinValue;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
            MobileInput.MoveDirection = Vector2.zero;
        }
    }
}
