using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using MoraeGames.Game;
using MoraeGames.Skill;
using MoraeGames.Network;

namespace MoraeGames.UI
{
    public class LobbyManager : MonoBehaviour
    {
        [Header("UI Elements")]
        public TMP_Text roleText;
        public Button startMatchButton;

        [Header("Character Selection")]
        public GameObject characterSelectPanel;
        public Button[] characterButtons;
        public TMP_Text[] characterNameTexts;
        public TMP_Text[] characterAbilityTexts;
        public Image[] characterButtonImages;
        public TMP_Text selectedCharacterText;
        public LobbyHeroView heroView;
        public ArcadeIcon[] abilityIcons;
        public TMP_Text selectedSkillDetail;
        public GameObject growthPanel;
        public void ToggleGrowth() { if (growthPanel != null) growthPanel.SetActive(!growthPanel.activeSelf); }

        [Header("Skill Tree")]
        public TMP_Text skillPointsText;
        public Button[] skillButtons;
        public TMP_Text[] skillButtonTexts;
        public SkillNode[] skillNodes;

        [Header("Network")]
        public TMP_InputField joinCodeInput;
        public TMP_Text networkStatusText;
        public Button startNetworkMatchButton;

        private bool _networkBusy;
        private bool _isLeaving;
        private bool _restoringSelection;
        private Button _leaveRoomButton;
        private TMP_Text _startButtonLabel;
        private int _roomRevision = -1;
        private float _statusRefreshTimer;
        private LanRoomPanel _lanPanel;
        public bool NetworkBusy => _networkBusy;

        private static readonly Color SkillUnlockedColor = new Color(0.2f, 0.7f, 0.3f);
        private static readonly Color SkillAvailableColor = new Color(0.25f, 0.5f, 0.9f);
        private static readonly Color SkillLockedColor = new Color(0.35f, 0.35f, 0.4f);
        private static readonly string[] GrowthNames = { "가벼운 발걸음", "달리기 훈련", "체력 단련", "전술 훈련", "빠른 손놀림", "구출 전문가", "긴 팔" };

        private RoleManager.PlayerRole currentRole = RoleManager.PlayerRole.None;
        private int selectedCharacterIndex = 0;

        // Ability info for display
        private static readonly string[] CopNames = { "추격대장", "수색요원", "포획요원" };
        private static readonly string[] CopAbilities = { "긴급 추격", "수색 파동", "전기 포획탄" };
        private static readonly string[] CopAbilityDescs = {
            "빠르게 접근해 체포",
            "반복 파동으로 위치 확인",
            "자동 조준 · 적중 시 감속"
        };

        private static readonly string[] RobberNames = { "질주꾼", "연막꾼", "위장꾼", "분신꾼", "덫 전문가" };
        private static readonly string[] RobberAbilities = { "전력 질주", "연막 탈출", "위장 잠입", "미끼 분신", "추격 방해 덫" };
        private static readonly string[] RobberAbilityDescs = {
            "갈림길에서 빠르게 탈출",
            "연막 속에서 AI 탐지 회피",
            "AI 추적을 잠시 회피",
            "분신으로 AI를 유인",
            "경찰을 느리게 하는 덫 설치"
        };

        private static readonly Color CopColor = new Color(0.2f, 0.4f, 1f);
        private static readonly Color RobberColor = new Color(1f, 0.2f, 0.2f);
        private static readonly Color SelectedHighlight = new Color(1f, 0.85f, 0.2f);

        private void Start()
        {
            if (startMatchButton != null) _startButtonLabel = startMatchButton.GetComponentInChildren<TMP_Text>();
            if (roleText != null) roleText.text = "도둑 협동 · 경찰은 AI가 맡습니다";
            if (startMatchButton != null) startMatchButton.interactable = false;
            if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
            if (startNetworkMatchButton != null) startNetworkMatchButton.gameObject.SetActive(false);
            RefreshSkillUI();
            _leaveRoomButton = RoomUI.CreateExit(startMatchButton.transform.parent, roleText.font, .025f, .015f, .235f, .09f, ExitToMainMenu, "메인으로");
            _leaveRoomButton.gameObject.AddComponent<ArcadeButtonFeedback>();
            var room = NetworkRoomState.Instance;
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (room != null && manager != null && room.Selections.TryGetValue(manager.LocalClientId, out var selection))
            {
                _restoringSelection = true;
                SelectRobberRole();
                SelectCharacter(selection.Character);
                _restoringSelection = false;
            }
            else SelectRobberRole();
            _lanPanel = gameObject.AddComponent<LanRoomPanel>();
            _lanPanel.Initialize(this);
            // Also cover older scene versions that still have serialized team buttons.
            foreach (var button in startMatchButton.transform.root.GetComponentsInChildren<Button>(true))
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    if (button.onClick.GetPersistentTarget(i) == this && button.onClick.GetPersistentMethodName(i) == nameof(SelectCopRole))
                        button.gameObject.SetActive(false);
        }

        /// <summary>Leaves the room, cancels pending connections, and returns to the main menu.</summary>
        public void ExitToMainMenu()
        {
            if (_isLeaving) return;
            _isLeaving = true;
            NetworkSessionManager.Shutdown();
            MoraeGames.Core.MobileInput.ResetAll();
            Time.timeScale = 1;
            SceneManager.LoadScene("MainMenu");
        }

        private void Update()
        {
            if (_isLeaving) return;
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                if (growthPanel != null && growthPanel.activeSelf) growthPanel.SetActive(false);
                else ExitToMainMenu();
                return;
            }
            if (!NetworkSessionManager.IsNetworked) return;
            if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsConnectedClient) return;
            _statusRefreshTimer -= Time.unscaledDeltaTime;
            if (_statusRefreshTimer > 0) return;
            _statusRefreshTimer = .2f;
            bool host = NetworkSessionManager.HasStateAuthority;
            var room = NetworkRoomState.Instance;
            if (room != null && _roomRevision == room.Revision) return;
            if (room != null) _roomRevision = room.Revision;
            if (_leaveRoomButton != null) _leaveRoomButton.gameObject.SetActive(true);
            if (startNetworkMatchButton != null) startNetworkMatchButton.gameObject.SetActive(false);
            if (startMatchButton != null)
            {
                startMatchButton.interactable = host && room != null && !room.IsLoading;
                if (_startButtonLabel != null) _startButtonLabel.text = host ? "경기 시작 ▶" : "호스트의 시작 대기 중";
            }
            if (networkStatusText != null && room != null)
            {
                string code = NetworkSessionManager.CurrentJoinCode ?? NetworkSessionManager.CurrentLanEndpoint ?? "온라인";
                networkStatusText.text = $"방 {code} · 도둑 {room.Selections.Count}/{NetworkSessionManager.MaxPlayers}명 · 경찰 AI\n{room.Status}";
            }
        }
        /// <summary>Unlocks the skill at the given index (wired to skill buttons).</summary>
        public void UnlockSkill(int index)
        {
            if (SkillTreeManager.Instance == null || skillNodes == null) return;
            if (index < 0 || index >= skillNodes.Length) return;

            SkillTreeManager.Instance.TryUnlock(skillNodes[index]);
            RefreshSkillUI();
        }

        private void RefreshSkillUI()
        {
            SkillTreeManager manager = SkillTreeManager.Instance;
            if (manager == null) return;

            if (skillPointsText != null)
                skillPointsText.SetText("연습 성장 포인트: {0}", manager.SkillPoints);

            if (skillNodes == null || skillButtons == null) return;

            for (int i = 0; i < skillNodes.Length && i < skillButtons.Length; i++)
            {
                SkillNode node = skillNodes[i];
                if (node == null || skillButtons[i] == null) continue;

                bool unlocked = manager.IsSkillUnlocked(node);
                bool canUnlock = manager.CanUnlock(node);
                skillButtons[i].interactable = canUnlock;

                Image buttonImage = skillButtons[i].GetComponent<Image>();
                if (buttonImage != null)
                    buttonImage.color = unlocked ? SkillUnlockedColor
                        : canUnlock ? SkillAvailableColor : SkillLockedColor;

                if (skillButtonTexts != null && i < skillButtonTexts.Length && skillButtonTexts[i] != null)
                {
                    skillButtonTexts[i].text = unlocked
                        ? $"{(i < GrowthNames.Length ? GrowthNames[i] : node.displayName)}\n<size=28>습득 완료</size>"
                        : $"{(i < GrowthNames.Length ? GrowthNames[i] : node.displayName)}\n<size=28>{node.description} · {node.cost} 포인트</size>";
                }
            }
        }

        public void SelectCopRole()
        {
            // Compatibility with old scene callbacks: human players are always robbers.
            SelectRobberRole();
        }

        public void SelectRobberRole()
        {
            Debug.Log("[LobbyManager] SelectRobberRole called.");
            currentRole = RoleManager.PlayerRole.Robber;
            if (RoleManager.Instance != null)
                RoleManager.Instance.AssignRole("Player1", RoleManager.PlayerRole.Robber);

            if (roleText != null) roleText.text = "도둑 협동 · AI 경찰을 피해 탈취하고 동료를 구하세요";
            ShowCharacterSelection();
        }

        private void ShowCharacterSelection()
        {
            if (characterSelectPanel == null) return;
            characterSelectPanel.SetActive(true);

            bool isCop = currentRole == RoleManager.PlayerRole.Cop;
            if (heroView != null) heroView.Select(isCop);
            int count = isCop ? CopNames.Length : RobberNames.Length;
            string[] names = isCop ? CopNames : RobberNames;
            string[] abilities = isCop ? CopAbilities : RobberAbilities;
            string[] descs = isCop ? CopAbilityDescs : RobberAbilityDescs;
            Color baseColor = isCop ? CopColor : RobberColor;

            for (int i = 0; i < characterButtons.Length; i++)
            {
                if (i < count)
                {
                    characterButtons[i].gameObject.SetActive(true);
                    if (characterNameTexts != null && i < characterNameTexts.Length)
                        characterNameTexts[i].text = names[i];
                    if (characterAbilityTexts != null && i < characterAbilityTexts.Length)
                        characterAbilityTexts[i].text = $"{abilities[i]}\n<size=30>{descs[i]}</size>";
                    if (characterButtonImages != null && i < characterButtonImages.Length)
                        characterButtonImages[i].color = baseColor;
                    if (abilityIcons != null && i < abilityIcons.Length && abilityIcons[i] != null)
                        abilityIcons[i].SetSymbol(isCop ? i : i == 0 ? 0 : i + 2);
                }
                else
                {
                    characterButtons[i].gameObject.SetActive(false);
                }
            }

            // Auto-select first character
            SelectCharacter(0);
        }

        public void SelectCharacter(int index)
        {
            index = Mathf.Clamp(index, 0, RobberNames.Length - 1);
            selectedCharacterIndex = index;

            if (RoleManager.Instance != null)
                RoleManager.Instance.SelectCharacter("Player1", index);

            // Update highlight
            bool isCop = currentRole == RoleManager.PlayerRole.Cop;
            int count = isCop ? CopNames.Length : RobberNames.Length;
            Color baseColor = isCop ? CopColor : RobberColor;
            string[] names = isCop ? CopNames : RobberNames;

            for (int i = 0; i < characterButtons.Length && i < count; i++)
            {
                if (characterButtonImages != null && i < characterButtonImages.Length)
                    characterButtonImages[i].color = (i == index) ? SelectedHighlight : baseColor;
            }

            if (selectedCharacterText != null)
                selectedCharacterText.text = $"선택: {names[index]}";
            if (selectedSkillDetail != null)
                selectedSkillDetail.text = (isCop ? CopAbilities[index] : RobberAbilities[index]) + "\n<size=65%>" + (isCop ? CopAbilityDescs[index] : RobberAbilityDescs[index]) + "</size>";

            if (startMatchButton != null) startMatchButton.interactable = !NetworkSessionManager.IsNetworked || NetworkSessionManager.HasStateAuthority;
            if (!_restoringSelection && NetworkSessionManager.IsNetworked && NetworkRoomState.Instance != null)
                NetworkRoomState.Instance.Submit(currentRole, selectedCharacterIndex);

            Debug.Log($"[LobbyManager] Selected character index {index}");
        }

        // Called by dynamically created buttons (index 0-5)
        public void SelectCharacter0() => SelectCharacter(0);
        public void SelectCharacter1() => SelectCharacter(1);
        public void SelectCharacter2() => SelectCharacter(2);
        public void SelectCharacter3() => SelectCharacter(3);
        public void SelectCharacter4() => SelectCharacter(4);
        public void SelectCharacter5() => SelectCharacter(5);

        public void StartMatch()
        {
            if (_isLeaving) return;
            if (NetworkSessionManager.IsNetworked)
            {
                NetworkSessionManager.BeginMatch();
                return;
            }
            if (_networkBusy) return;
            Debug.Log("[LobbyManager] StartMatch called. Loading InGame scene...");
            SceneManager.LoadScene("InGame");
        }

        /// <summary>Creates a Relay session; an outage must not silently become a local-only game.</summary>
        public async void HostMatch()
        {
            if (_lanPanel != null && _lanPanel.HandleHost()) return;
            if (_isLeaving || _networkBusy || NetworkSessionManager.IsNetworked) return;
            _networkBusy = true;
            try
            {
                SetNetworkStatus("Creating relay session...");
                string code = await NetworkSessionManager.StartHostWithRelayAsync(destroyCancellationToken);
                if (this == null || _isLeaving) return;
                if (code == null) { SetNetworkStatus("Could not create room. Check your connection and retry."); return; }
                SetNetworkStatus($"참가 코드: {code} · 도둑 협동 · 스킬 선택을 서버에서 확인합니다");
                if (startNetworkMatchButton != null) startNetworkMatchButton.gameObject.SetActive(true);
            }
            finally { _networkBusy = false; }
        }

        /// <summary>Starts connecting; only IsConnectedClient confirms a completed connection.</summary>
        public async void JoinMatch()
        {
            if (_lanPanel != null && _lanPanel.HandleJoin()) return;
            if (_isLeaving || _networkBusy || NetworkSessionManager.IsNetworked) return;
            string code = joinCodeInput != null ? joinCodeInput.text.Trim() : "";
            if (string.IsNullOrEmpty(code)) { SetNetworkStatus("Enter the host's room code."); return; }
            _networkBusy = true;
            try
            {
                SetNetworkStatus("Joining relay session...");
                bool started = await NetworkSessionManager.StartClientWithRelayAsync(code.ToUpperInvariant(), destroyCancellationToken);
                if (this == null || _isLeaving) return;
                SetNetworkStatus(started ? "Connecting to host..." : "Join failed. Check the room code and connection.");
            }
            finally { _networkBusy = false; }
        }

        /// <summary>Host-only: starts the networked match for everyone connected.</summary>
        public void StartNetworkMatch()
        {
            NetworkSessionManager.BeginMatch();
        }

        private void SetNetworkStatus(string message)
        {
            if (networkStatusText != null) networkStatusText.text = message;
            Debug.Log($"[LobbyManager] {message}");
        }
    }
}


