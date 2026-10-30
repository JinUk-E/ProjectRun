using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MoraeGames.Network;

namespace MoraeGames.UI
{
    /// <summary>LAN and Relay entry UI using the existing lobby layout and connection lifecycle.</summary>
    public sealed class LanRoomPanel : MonoBehaviour
    {
        private LobbyManager _lobby;
        private Button _modeButton, _hostButton, _joinButton;
        private TMP_Text _modeLabel, _hostLabel, _joinLabel;
        private bool _lan = true, _wasActive, _wasBusy;
        private float _connectingAt = -1;
        private string _lanInput = "", _relayInput = "";
        public bool IsLan => _lan;

        public void Initialize(LobbyManager lobby)
        {
            _lobby = lobby;
            var root = lobby.startMatchButton.transform.parent;
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.name == "HostMatchButton") _hostButton = button;
                if (button.name == "JoinMatchButton") _joinButton = button;
            }
            _hostLabel = _hostButton.GetComponentInChildren<TMP_Text>();
            _joinLabel = _joinButton.GetComponentInChildren<TMP_Text>();
            _modeButton = RoomUI.CreateExit(root, lobby.roleText.font, .025f, .39f, .235f, .45f, ToggleMode, "LAN  ↔  인터넷");
            _modeButton.name = "ConnectionModeButton";
            _modeButton.GetComponent<Image>().color = new Color(.12f, .35f, .6f);
            _modeLabel = _modeButton.GetComponentInChildren<TMP_Text>();
            _lan = NetworkSessionManager.SupportsDirectLan && NetworkSessionManager.CurrentJoinCode == null;
            lobby.joinCodeInput.contentType = TMP_InputField.ContentType.Standard;
            lobby.joinCodeInput.lineType = TMP_InputField.LineType.SingleLine;
            lobby.joinCodeInput.onValueChanged.AddListener(OnInputChanged);
            Refresh();
            Status(NetworkSessionManager.LastConnectionMessage ?? (NetworkSessionManager.SupportsDirectLan ? "같은 공유기 / Wi-Fi에서 함께 플레이 · 기본 포트 7777\n호스트는 LAN 방 만들기, 친구는 호스트 IPv4 주소로 참가" : "친구와 함께: 방 만들기 → 참가 코드 공유\n혼자 즐기기: 스킬 선택 → 연습 경기 시작"));
        }

        private void OnDestroy()
        {
            if (_lobby != null && _lobby.joinCodeInput != null)
                _lobby.joinCodeInput.onValueChanged.RemoveListener(OnInputChanged);
        }

        private void OnInputChanged(string value) => RefreshButtons();

        public void ToggleMode()
        {
            if (!NetworkSessionManager.SupportsDirectLan) return;
            if (NetworkSessionManager.IsNetworked || _lobby.NetworkBusy) return;
            if (_lan) _lanInput = _lobby.joinCodeInput.text; else _relayInput = _lobby.joinCodeInput.text;
            _lan = !_lan;
            _lobby.joinCodeInput.SetTextWithoutNotify(_lan ? _lanInput : _relayInput);
            Refresh();
            Status(_lan ? "같은 공유기 / Wi-Fi · 호스트 IPv4 또는 IPv4:포트 입력"
                : "인터넷 플레이 · 방을 만든 친구의 Relay 참가 코드 입력");
        }

        /// <summary>Returns true when the LAN UI handled the shared host action.</summary>
        public bool HandleHost()
        {
            if (NetworkSessionManager.IsNetworked)
            {
                string value = NetworkSessionManager.CurrentJoinCode ?? NetworkSessionManager.CurrentLanEndpoint;
                if (!string.IsNullOrEmpty(value)) WebPlatform.CopyText(value);
                return true;
            }
            if (!_lan) return false;
            if (_lobby.NetworkBusy) return true;
            if (!NetworkSessionManager.StartHost())
            {
                Status("방을 열지 못했습니다. 다른 게임이 포트 7777을 사용 중인지 확인해 주세요.");
                return true;
            }
            Refresh();
            Status("LAN 방을 열었습니다. 표시된 주소를 친구에게 전달하세요.\n사용 가능한 IPv4: " + string.Join(" / ", LanAddress.GetLocalAddresses()));
            return true;
        }

        /// <summary>Uses the join action for connection cancellation or explicit room exit while active.</summary>
        public bool HandleJoin()
        {
            if (NetworkSessionManager.IsNetworked)
            {
                NetworkSessionManager.Shutdown();
                SceneManager.LoadScene("Lobby");
                return true;
            }
            if (!_lan) return false;
            if (_lobby.NetworkBusy) return true;
            if (!LanAddress.TryParse(_lobby.joinCodeInput.text, out _, out _))
            {
                Status("IPv4 주소를 입력하세요. 예: 192.168.0.10 또는 192.168.0.10:7777");
                return true;
            }
            if (NetworkSessionManager.StartClient(_lobby.joinCodeInput.text))
            {
                _connectingAt = Time.unscaledTime;
                Status("호스트에 연결 중… · 참가 버튼으로 취소할 수 있습니다");
                Refresh();
            }
            else Status("연결을 시작하지 못했습니다. 잠시 후 다시 시도해 주세요.");
            return true;
        }

        private void Update()
        {
            if (_lobby == null) return;
            bool active = NetworkSessionManager.IsNetworked;
            bool busy = _lobby.NetworkBusy;
            if (active != _wasActive || busy != _wasBusy) Refresh();
            var nm = NetworkManager.Singleton;
            if (active && NetworkSessionManager.Mode == NetworkSessionManager.SessionMode.Client && nm != null && !nm.IsConnectedClient)
            {
                if (_connectingAt < 0) _connectingAt = Time.unscaledTime;
                _lobby.startMatchButton.interactable = false;
                if (Time.unscaledTime - _connectingAt > 12)
                {
                    NetworkSessionManager.Shutdown();
                    _connectingAt = -1;
                    Refresh();
                    Status(_lan ? "연결 시간이 초과됐습니다. 호스트 주소·같은 네트워크·UDP 방화벽을 확인하고 재시도하세요." : "연결 시간이 초과됐습니다. 참가 코드와 호스트 접속 상태를 확인하고 다시 시도하세요.");
                }
            }
            else _connectingAt = -1;
        }

        private void Refresh()
        {
            _wasActive = NetworkSessionManager.IsNetworked;
            _wasBusy = _lobby.NetworkBusy;
            _modeButton.interactable = NetworkSessionManager.SupportsDirectLan && !_wasActive && !_wasBusy;
            _modeLabel.text = !NetworkSessionManager.SupportsDirectLan ? "온라인 · 참가 코드로 함께하기" : _lan ? "LAN 선택됨 · 인터넷으로 ↔" : "인터넷 선택됨 · LAN으로 ↔";
            var input = _lobby.joinCodeInput;
            input.characterLimit = _lan ? 21 : 10;
            input.readOnly = _wasActive || _wasBusy;
            if (input.placeholder is TMP_Text placeholder) placeholder.text = _lan ? "호스트 IPv4:7777" : "Relay 참가 코드";
            if (_wasActive) input.SetTextWithoutNotify(NetworkSessionManager.CurrentJoinCode ?? NetworkSessionManager.CurrentLanEndpoint ?? "");
            _hostLabel.text = _wasActive ? (_lan ? "주소 복사" : "코드 복사") : "방 만들기";
            _joinLabel.text = _wasActive ? "연결 종료" : "참가";
            if (!_wasActive && !_wasBusy)
            {
                _lobby.startMatchButton.interactable = true;
                _lobby.startMatchButton.GetComponentInChildren<TMP_Text>().text = "연습 경기 시작 ▶";
            }
            else if (_wasBusy) _lobby.startMatchButton.interactable = false;
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            _hostButton.interactable = !_lobby.NetworkBusy;
            _joinButton.interactable = !_lobby.NetworkBusy && (NetworkSessionManager.IsNetworked ||
                (_lan ? LanAddress.TryParse(_lobby.joinCodeInput.text, out _, out _) : !string.IsNullOrWhiteSpace(_lobby.joinCodeInput.text)));
        }

        private void Status(string text) => _lobby.networkStatusText.text = text;
    }
}
