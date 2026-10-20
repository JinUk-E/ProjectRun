using UnityEngine;
using TMPro;
using MoraeGames.Core;

namespace MoraeGames.UI
{
    /// <summary>
    /// Displays centered text announcements for game events
    /// (arrest, rescue, countdown, game over, etc.)
    /// </summary>
    public class SystemMessageUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public TMP_Text messageText;
        public GameObject messagePanel;

        private float _displayTimer = 0f;

        /// <summary>Subscribe to game events in OnEnable (Rule §2: Event Lifecycle).</summary>
        private void OnEnable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnSystemMessage += ShowMessage;
            }
        }

        /// <summary>Unsubscribe from game events in OnDisable (Rule §2: Event Lifecycle).</summary>
        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnSystemMessage -= ShowMessage;
            }
        }

        private void Start()
        {
            if (messagePanel != null) messagePanel.SetActive(false);
        }

        private void Update()
        {
            if (_displayTimer > 0f)
            {
                _displayTimer -= Time.deltaTime;

                // Fade out effect
                if (_displayTimer < 0.5f && messageText != null)
                {
                    Color c = messageText.color;
                    c.a = _displayTimer / 0.5f;
                    messageText.color = c;
                }

                if (_displayTimer <= 0f)
                {
                    HideMessage();
                }
            }
        }

        /// <summary>Shows a system message with the given text and color.</summary>
        public void ShowMessage(string message, Color color)
        {
            if (messageText != null)
            {
                messageText.text = message;
                color.a = 1f;
                messageText.color = color;
            }
            if (messagePanel != null) messagePanel.SetActive(true);
            _displayTimer = GameConstants.SystemMessageDuration;
        }

        private void HideMessage()
        {
            if (messagePanel != null) messagePanel.SetActive(false);
        }
    }
}
