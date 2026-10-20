using UnityEngine;
using MoraeGames.Core;

namespace MoraeGames.UI
{
    /// <summary>
    /// Root component for the on-screen touch controls group.
    /// Clears all mobile input state when the group unloads so no stale
    /// press/hold survives a scene change.
    /// </summary>
    public class MobileControlsUI : MonoBehaviour
    {
        [Tooltip("Show controls on non-touch platforms too (useful for editor/desktop testing).")]
        public bool showOnDesktop = true;

        private void Awake()
        {
            if (!showOnDesktop && !Application.isMobilePlatform && !Application.isEditor)
                gameObject.SetActive(false);
        }

        private void OnDisable() => MobileInput.ResetAll();
    }
}
