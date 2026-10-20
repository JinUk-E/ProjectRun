using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace MoraeGames.UI
{
    public sealed class ArcadeButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private Vector3 _scale;
        private bool _pressed, _hover;
        private float _pulse;
        private void Awake() => _scale = transform.localScale;
        public void OnPointerDown(PointerEventData e) { _pressed = true; }
        public void OnPointerUp(PointerEventData e) { _pressed = false; }
        public void OnPointerEnter(PointerEventData e) { _hover = true; }
        public void OnPointerExit(PointerEventData e) { _hover = false; _pressed = false; }
        public void Pulse() => _pulse = 1;
        private void OnDisable() { _pressed = _hover = false; _pulse = 0; transform.localScale = _scale; }
        private void Update()
        {
            _pulse = Mathf.MoveTowards(_pulse, 0, Time.unscaledDeltaTime * 4);
            float target = (_pressed ? .93f : _hover ? 1.025f : 1) + _pulse * .12f;
            transform.localScale = Vector3.Lerp(transform.localScale, _scale * target, 1 - Mathf.Exp(-22 * Time.unscaledDeltaTime));
        }
    }


}
