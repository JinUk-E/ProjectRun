using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace MoraeGames.UI
{
    /// <summary>Explicit exit action, separate from keeping the party together.</summary>
    public static class RoomUI
    {
        public static Button CreateExit(Transform parent, TMP_FontAsset font, float x, float y, float xx, float yy, UnityEngine.Events.UnityAction action, string caption = "방 나가기")
        {
            var root=new GameObject("LeaveRoomButton",typeof(RectTransform),typeof(Image),typeof(Button));root.transform.SetParent(parent,false);
            var rect=root.GetComponent<RectTransform>();rect.anchorMin=new Vector2(x,y);rect.anchorMax=new Vector2(xx,yy);rect.offsetMin=rect.offsetMax=Vector2.zero;
            root.GetComponent<Image>().color=new Color(.3f,.12f,.18f,.95f);
            var label=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(root.transform,false);
            var lr=label.GetComponent<RectTransform>();lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;
            var text=label.GetComponent<TMP_Text>();text.font=font;text.fontSize=48;text.enableAutoSizing=true;text.fontSizeMin=30;text.fontSizeMax=48;text.alignment=TextAlignmentOptions.Center;text.text=caption;text.raycastTarget=false;
            var button=root.GetComponent<Button>();button.onClick.AddListener(action);return button;
        }
    }
}
