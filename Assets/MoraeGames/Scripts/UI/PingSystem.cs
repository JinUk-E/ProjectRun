using UnityEngine;
using TMPro;
using MoraeGames.Player;
using MoraeGames.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MoraeGames.UI
{
    /// <summary>
    /// Smart ping system: players press a key to share their location.
    /// Teammates see directional arrows pointing toward the ping source.
    /// </summary>
    public class PingSystem : MonoBehaviour
    {
        public static PingSystem Instance { get; private set; }
        public int ReceivedPingCount { get; private set; }
        public Vector3 LastPingPosition => _activePingPosition;
        private MoraeGames.Network.NetworkCharacter _networkPlayer;
        private void Awake() => Instance = this;
        private void OnDestroy() { ClearPing(); if (Instance == this) Instance = null; }
        [Header("Ping Settings")]
        public TMP_Text pingIndicatorText;

        // Dirty flags so the indicator string is rebuilt only when displayed values change
        private int _lastPingDistance = -1;
        private int _lastPingSeconds = -1;
        private string _lastArrow;

        private Transform _localPlayer;
        private Vector3 _activePingPosition;
        private float _pingTimer = 0f;
        private bool _hasPing = false;
        private string _pingOwnerName;

        // World-space ping marker
        private GameObject _pingWorldMarker;

        private void Start()
        {
            // Find local player once at startup (Rule §4: no Find in Update)
            FindLocalPlayer();
        }

        private void Update()
        {
            // Retry finding player if not found at Start (spawning may happen later)
            if (_localPlayer == null)
            {
                FindLocalPlayer();
                if (_localPlayer == null) return;
            }

            // Update still runs at timeScale 0 — block ping input while paused
            // (the ping timer below also freezes since deltaTime is 0)
            if (MoraeGames.Game.InGameManager.IsPaused || !MoraeGames.Network.NetworkCharacter.MatchAllowsActions) return;

            // Handle ping input
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame)
                CreatePing();
#else
            if (Input.GetKeyDown(KeyCode.Z))
                CreatePing();
#endif

            // On-screen ping button (mobile)
            if (MobileInput.ConsumePingPressed())
                CreatePing();

            // Update active ping
            if (_hasPing)
            {
                _pingTimer -= Time.deltaTime;
                if (_pingTimer <= 0f)
                {
                    ClearPing();
                    return;
                }
                UpdatePingIndicator();
            }
        }

        private void FindLocalPlayer()
        {
            var players = PlayerController.Instances;
            foreach (var p in players)
            {
                if (p != null && p.IsLocallyControlled)
                {
                    _localPlayer = p.transform;
                    _networkPlayer = p.GetComponent<MoraeGames.Network.NetworkCharacter>();
                    break;
                }
            }
        }

        private const string MarkerPoolKey = "PingMarker";

        public void CreatePing()
        {
            if (_localPlayer == null || !MoraeGames.Network.NetworkCharacter.MatchAllowsActions) return;
            if (MoraeGames.Network.NetworkSessionManager.IsNetworked)
            {
                if (_networkPlayer != null) _networkPlayer.RequestTeamPing();
                return;
            }
            ReceiveTeamPing(_localPlayer.position, "내 위치");
        }

        public void ReceiveTeamPing(Vector3 position, string sender)
        {
            ReceivedPingCount++;
            _lastPingDistance = _lastPingSeconds = -1;
            _activePingPosition = position;
            _pingTimer = GameConstants.PingDuration;
            _hasPing = true;
            _pingOwnerName = sender;

            // Pooled world marker (Rule §4: pooling mandatory for frequent effects)
            if (_pingWorldMarker != null)
                EffectPool.Release(MarkerPoolKey, _pingWorldMarker);
            _pingWorldMarker = EffectPool.Get(MarkerPoolKey, CreateMarker);
            _pingWorldMarker.transform.position = _activePingPosition + Vector3.up * 5f;

            Debug.Log($"[Ping] {_pingOwnerName} pinged at {_activePingPosition}!");
        }

        private static GameObject CreateMarker()
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "PingMarker";
            marker.transform.localScale = new Vector3(0.5f, 3f, 0.5f);
            Destroy(marker.GetComponent<Collider>());
            RendererTint.SetColor(marker.GetComponent<Renderer>(), new Color(0f, 1f, 0.5f, 0.7f));
            return marker;
        }

        private void UpdatePingIndicator()
        {
            if (pingIndicatorText == null || _localPlayer == null) return;

            Vector3 dirToPing = _activePingPosition - _localPlayer.position;
            float distance = dirToPing.magnitude;
            int seconds = Mathf.CeilToInt(_pingTimer);

            if (distance < 3f)
            {
                if (seconds != _lastPingSeconds || _lastPingDistance != 0)
                {
                    _lastPingSeconds = seconds;
                    _lastPingDistance = 0;
                    pingIndicatorText.SetText("팀 신호 · 여기 ({0}초)", seconds);
                }
            }
            else
            {
                float angle = Mathf.Atan2(dirToPing.x, dirToPing.z) * Mathf.Rad2Deg;
                string arrow = GetDirectionArrow(angle);
                int distanceInt = Mathf.RoundToInt(distance);

                // Rebuild only when a displayed value changes (Rule §4: UI Optimization)
                if (distanceInt != _lastPingDistance || seconds != _lastPingSeconds
                    || !ReferenceEquals(arrow, _lastArrow))
                {
                    _lastPingDistance = distanceInt;
                    _lastPingSeconds = seconds;
                    _lastArrow = arrow;
                    // string.Format is fine here: the dirty guard above means this runs
                    // only when a displayed value changes, never every frame
                    pingIndicatorText.text = string.Format("{0} 팀 신호 {1}m ({2}초)", arrow, distanceInt, seconds);
                }
            }
        }

        private string GetDirectionArrow(float angle)
        {
            if (angle < 0) angle += 360f;

            if (angle >= 337.5f || angle < 22.5f) return "^"; // North
            if (angle >= 22.5f && angle < 67.5f) return "/^"; // NE
            if (angle >= 67.5f && angle < 112.5f) return ">"; // East
            if (angle >= 112.5f && angle < 157.5f) return "\\v"; // SE
            if (angle >= 157.5f && angle < 202.5f) return "v"; // South
            if (angle >= 202.5f && angle < 247.5f) return "v/"; // SW
            if (angle >= 247.5f && angle < 292.5f) return "<"; // West
            return "^\\"; // NW
        }

        private void ClearPing()
        {
            _hasPing = false;
            if (pingIndicatorText != null) pingIndicatorText.text = "";
            if (_pingWorldMarker != null)
            {
                EffectPool.Release(MarkerPoolKey, _pingWorldMarker);
                _pingWorldMarker = null;
            }
        }
    }
}
