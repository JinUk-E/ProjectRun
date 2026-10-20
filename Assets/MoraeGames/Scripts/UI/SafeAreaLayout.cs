using UnityEngine;
namespace MoraeGames.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaLayout : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _last;
        private Vector2Int _screen;
        private void Awake() { _rect = (RectTransform)transform; Apply(); }
        private void Update() { if (_last != Screen.safeArea || _screen.x != Screen.width || _screen.y != Screen.height) Apply(); }
        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            _last = Screen.safeArea; _screen = new Vector2Int(Screen.width, Screen.height);
            _rect.anchorMin = new Vector2(_last.xMin / Screen.width, _last.yMin / Screen.height);
            _rect.anchorMax = new Vector2(_last.xMax / Screen.width, _last.yMax / Screen.height);
            _rect.offsetMin = _rect.offsetMax = Vector2.zero;
        }
    }
}
