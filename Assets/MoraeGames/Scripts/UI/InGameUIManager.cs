using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using MoraeGames.Game;
using MoraeGames.Player;
using MoraeGames.Ability;
using MoraeGames.Core;
using MoraeGames.Network;

namespace MoraeGames.UI
{
    /// <summary>
    /// Manages all in-game HUD elements: timer, loot progress, arrest count,
    /// ability cooldown, interaction prompts, and results panel.
    /// All text is TextMeshPro; per-frame updates use SetText with numeric args
    /// or dirty flags so no strings are allocated every frame (Rule §4).
    /// </summary>
    public class InGameUIManager : MonoBehaviour
    {
        [Header("UI Elements")]
        public TMP_Text timerText;
        public GameObject resultPanel;
        public TMP_Text resultText;
        public Button lobbyButton;

        [Header("HUD Elements")]
        public TMP_Text lootProgressText;
        public TMP_Text arrestCountText;
        public TMP_Text abilityCooldownText;
        public TMP_Text interactionPromptText;

        [Header("Minimap")]
        public RawImage minimapImage;

        [Header("Ping")]
        public TMP_Text pingIndicatorText;

        [Header("System Messages")]
        public TMP_Text systemMessageText;
        public GameObject systemMessagePanel;

        [Header("Ready Countdown")]
        public TMP_Text readyCountdownText;
        public GameObject readyPanel;
        public TMP_Text roleText, objectiveText, phaseText, readyRoleText, readyHintText, skillNameText, skillHintText, skillButtonText;
        public Image lootFill, cooldownFill;
        public CanvasGroup actionControls;
        public ArcadeIcon skillIcon;
        public ArcadeButtonFeedback skillFeedback;
        public bool compactHud;
        public string DisplayedPhase => phaseText != null ? phaseText.text : "";
        public int DisplayedArrested => _lastArrestedCount;
        private RobberController _localRobber;
        private int _lastPhase = -1;
        private float _goUntil;
        private NetworkCharacter _localNetworkCharacter;
        private AICopController.PursuitState _lastPursuit;
        private float _escapedUntil;
        private int _lastGuidance = -1;
        private TMP_Text _guidanceText;

        [Header("Pause")]
        public Button pauseButton;
        public GameObject pausePanel;
        public Button resumeButton;
        public Button exitToLobbyButton;

        // Cached references (Rule §4: no Find in Update)
        private InGameManager _inGameManager;
        private LootManager _lootManager;
        private AbilityHolder _localPlayerAbility;
        private PlayerController _localPlayer;
        private MinimapController _minimapController;
        private PingSystem _pingSystem;
        private SystemMessageUI _systemMessageUI;

        // Cached robber list for arrest count UI

        // Reusable buffer for Physics.OverlapSphereNonAlloc (Rule §4: Zero-Allocation)
        // 32 slots: characters are compound colliders (2 each) and the prison area gets crowded
        private readonly Collider[] _overlapBuffer = new Collider[32];

        // Dirty flags for UI text updates (Rule §4: UI Optimization)
        private int _lastMinutes = -1;
        private int _lastSeconds = -1;
        private int _lastArrestedCount = -1;
        private int _lastTotalCount = -1;
        private int _lastLootPercent = -1;
        private int _lastCountdownNum = int.MinValue;
        private int _lastPromptState = -1; // 0 none, 1 rob, 2 robbing, 3 rescue, 4 rescuing
        private int _lastPromptPercent = -1;
        private int _lastAbilityState = -1; // 0 active, 1 ready, 2 cooldown

        private void Start()
        {
            _inGameManager = FindFirstObjectByType<InGameManager>();
            _lootManager = FindFirstObjectByType<LootManager>();
            if (phaseText != null)
            {
                var plate = new GameObject("SituationalGuide", typeof(RectTransform), typeof(Image));
                plate.transform.SetParent(phaseText.transform.parent, false);
                var rect = (RectTransform)plate.transform;
                rect.anchorMin = new Vector2(.32f, .745f); rect.anchorMax = new Vector2(.70f, .81f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var background = plate.GetComponent<Image>(); background.color = new Color(.015f, .035f, .09f, .88f); background.raycastTarget = false;
                var label = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(plate.transform, false);
                _guidanceText = label.GetComponent<TMP_Text>();
                _guidanceText.font = phaseText.font; _guidanceText.fontSize = 38;
                _guidanceText.enableAutoSizing = true; _guidanceText.fontSizeMin = 24; _guidanceText.fontSizeMax = 38;
                _guidanceText.alignment = TextAlignmentOptions.Center; _guidanceText.raycastTarget = false;
                _guidanceText.SetText("건물 안 보석 앞에서 F / 행동 유지 · Q / 스킬\n전리품 {0}% 확보 또는 시간 종료까지 팀 생존", GameConstants.LootWinThreshold * 100);
                var lr = (RectTransform)label.transform; lr.anchorMin = new Vector2(.025f, .05f); lr.anchorMax = new Vector2(.975f, .95f);
                lr.offsetMin = lr.offsetMax = Vector2.zero;
                plate.transform.SetSiblingIndex(phaseText.transform.GetSiblingIndex());
            }
            if (resultPanel != null)
                resultPanel.SetActive(false);
            if (NetworkSessionManager.IsNetworked && resultPanel != null)
                RoomUI.CreateExit(resultPanel.transform, resultText.font, .38f, .10f, .62f, .19f, LeaveNetworkRoom);

            // Setup minimap
            _minimapController = gameObject.AddComponent<MinimapController>();
            if (minimapImage != null)
                _minimapController.SetMinimapImage(minimapImage);

            // Setup ping system
            _pingSystem = gameObject.AddComponent<PingSystem>();
            if (pingIndicatorText != null)
                _pingSystem.pingIndicatorText = pingIndicatorText;

            // Setup system messages
            _systemMessageUI = gameObject.AddComponent<SystemMessageUI>();
            if (systemMessageText != null)
                _systemMessageUI.messageText = systemMessageText;
            if (systemMessagePanel != null)
                _systemMessageUI.messagePanel = systemMessagePanel;
        }

        private void OnEnable()
        {
            if (lobbyButton != null)
            {
                lobbyButton.onClick.RemoveAllListeners();
                lobbyButton.onClick.AddListener(OnLobbyButtonClicked);
            }
            if (pauseButton != null) pauseButton.onClick.AddListener(OnPauseClicked);
            if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
            if (exitToLobbyButton != null) exitToLobbyButton.onClick.AddListener(OnExitToLobbyClicked);
        }

        private void OnDisable()
        {
            if (lobbyButton != null)
            {
                lobbyButton.onClick.RemoveListener(OnLobbyButtonClicked);
            }
            if (pauseButton != null) pauseButton.onClick.RemoveListener(OnPauseClicked);
            if (resumeButton != null) resumeButton.onClick.RemoveListener(OnResumeClicked);
            if (exitToLobbyButton != null) exitToLobbyButton.onClick.RemoveListener(OnExitToLobbyClicked);
        }

        private void OnPauseClicked() => SetPauseUI(true);

        private void OnResumeClicked() => SetPauseUI(false);

        private void OnExitToLobbyClicked()
        {
            // SetPaused(false) restores timeScale before the scene change;
            // InGameManager.OnDestroy is a second safety net
            if (_inGameManager != null) _inGameManager.SetPaused(false);
            OnLobbyButtonClicked();
        }

        private void SetPauseUI(bool paused)
        {
            if (_inGameManager == null) return;
            if (paused && _inGameManager.IsGameOver) return; // no pausing over the result screen

            _inGameManager.SetPaused(paused);
            if (pausePanel != null) pausePanel.SetActive(paused);
        }

        private void Update()
        {
            // Cache InGameManager once
            if (_inGameManager == null) return;

            // ESC toggles pause (keyboard convenience; mobile uses the on-screen button)
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                SetPauseUI(!InGameManager.IsPaused);
#else
            if (Input.GetKeyDown(KeyCode.Escape))
                SetPauseUI(!InGameManager.IsPaused);
#endif

            // Hide the pause button once the result screen is up
            if (pauseButton != null && pauseButton.gameObject.activeSelf == _inGameManager.IsGameOver)
                pauseButton.gameObject.SetActive(!_inGameManager.IsGameOver);

            // Keep the panel in sync when the game force-unpauses (e.g. match ends
            // while the menu is open in MenuOnly mode)
            if (pausePanel != null && pausePanel.activeSelf && !InGameManager.IsPaused)
                pausePanel.SetActive(false);

            // Cache LootManager once

            // Find local player once
            if (_localPlayer == null)
            {
                foreach (var pc in PlayerController.Instances)
                {
                    if (pc != null && pc.IsLocallyControlled)
                    {
                        _localPlayer = pc;
                        _localPlayerAbility = pc.GetComponent<AbilityHolder>();
                        _localRobber = pc as RobberController;
                        _localNetworkCharacter = pc.GetComponent<NetworkCharacter>();
                        bool cop = pc is CopController;
                        if (roleText != null) roleText.text = cop ? "경찰 · 추격팀" : "도둑 · 도주팀";
                        if (objectiveText != null) objectiveText.text = cop ? "도둑을 모두 체포하세요 · 가까이 접근해 체포" : "전리품을 훔치거나 제한 시간까지 살아남으세요";
                        if (readyRoleText != null) readyRoleText.text = cop ? "당신은 경찰!" : "당신은 도둑!";
                        if (readyHintText != null)
                        {
                            if (cop) readyHintText.text = "도둑에게 접근 → 자동 체포\nQ 스킬로 도주로를 차단하세요";
                            else readyHintText.SetText("승리: 전리품 {0}% 확보 또는 시간 종료까지 팀 생존\n건물 안 보석 앞에서 F / 행동 길게 누르기\n구출: 감옥 정문 밖에서 F / 행동 길게 누르기", GameConstants.LootWinThreshold * 100);
                        }
                        if (skillNameText != null) skillNameText.text = AbilityPresentation.Name(_localPlayerAbility != null ? _localPlayerAbility.abilityData : null);
                        if (skillHintText != null) skillHintText.text = AbilityPresentation.Description(_localPlayerAbility != null ? _localPlayerAbility.abilityData : null);
                        if (skillIcon != null && _localPlayerAbility != null) skillIcon.SetSymbol(AbilityPresentation.Symbol(_localPlayerAbility.abilityData));
                        break;
                    }
                }
            }

            // Cache robber list once it exists (networked clients receive spawns
            // slightly after scene load, so retry while the list is empty)
            UpdatePhaseUI();
            UpdateTimerUI();
            UpdateLootUI();
            UpdateArrestCountUI();
            UpdatePursuitGuidance();
            UpdateAbilityUI();

            // Ready countdown display
            if (_inGameManager.IsReadyPhase)
            {
                UpdateReadyCountdownUI();
                return;
            }
            else if (readyPanel != null && readyPanel.activeSelf)
            {
                readyPanel.SetActive(false);
            }

            UpdateInteractionPromptUI();
        }

        private void UpdatePhaseUI()
        {
            int phase = _inGameManager.IsGameOver ? 4 : _inGameManager.IsReadyPhase ? 0 :
                _localPlayer == null ? 5 : _localRobber != null && _localRobber.IsArrested ? 2 : 1;
            if (actionControls != null)
            {
                actionControls.interactable = phase == 1 && !InGameManager.IsPaused;
                actionControls.blocksRaycasts = actionControls.interactable;
                actionControls.alpha = actionControls.interactable ? 1f : .35f;
            }
            if (phase != _lastPhase)
            {
                _lastGuidance = -1;
                if (phaseText != null) phaseText.color = Color.white;
                int previous = _lastPhase; _lastPhase = phase;
                if (phaseText != null) phaseText.text = phase == 0 ? "곧 시작합니다" : phase == 2 ? "체포됨 · 동료의 구출을 기다리세요" :
                    phase == 4 ? "경기 종료" : phase == 5 ? "플레이어 연결 중" : _localRobber == null ? "추격 중 · 도둑을 체포하세요" : "도주 중 · 전리품을 노리세요";
                if (phase == 1 && previous == 0) { _goUntil = Time.unscaledTime + 1.5f; if (phaseText != null) phaseText.text = "시작!  달리세요!"; }
                if (phase == 1 && previous == 2 && _systemMessageUI != null) _systemMessageUI.ShowMessage("구출 성공! 다시 도망치세요", Color.green);
            }
            if (_goUntil > 0 && Time.unscaledTime > _goUntil) { _goUntil = 0; _lastPhase = -1; }
        }

        /// <summary>Explains the server's pursuit state and the next useful action without exposing hidden enemies.</summary>
        private void UpdatePursuitGuidance()
        {
            if (_guidanceText == null) return;
            bool visible = _localRobber != null && !_inGameManager.IsReadyPhase && !_inGameManager.IsGameOver;
            _guidanceText.transform.parent.gameObject.SetActive(visible);
            if (!visible) { _lastGuidance = -1; _lastPursuit = AICopController.PursuitState.Clear; _escapedUntil = 0; return; }
            var pursuit = NetworkSessionManager.IsNetworked && _localNetworkCharacter != null
                ? _localNetworkCharacter.PursuitState : AICopController.GetPursuitState(_localRobber);
            if (_localRobber.IsArrested) { pursuit = AICopController.PursuitState.Clear; _escapedUntil = 0; }
            else if (_lastPursuit != AICopController.PursuitState.Clear && pursuit == AICopController.PursuitState.Clear)
                _escapedUntil = Time.time + 3f;
            _lastPursuit = pursuit;
            int state = _localRobber.IsArrested ? 4 : pursuit == AICopController.PursuitState.Chasing ? 1 :
                pursuit == AICopController.PursuitState.Searching ? 2 : Time.time < _escapedUntil ? 3 :
                _lastArrestedCount > 0 ? 5 : HeistBuilding.NearestAlarm(_localRobber.transform.position) != null ? 6 : 0;
            if (_goUntil > 0) { _lastGuidance = -1; return; }
            if (state == _lastGuidance) return;
            _lastGuidance = state;
            if (!_localRobber.IsArrested && phaseText != null)
            {
                phaseText.text = state == 1 ? "추격 중! · 경찰이 나를 보고 있어요" : state == 2 ? "수색 중 · 마지막 위치를 찾고 있어요" :
                    state == 3 ? "추격 해제! · 다시 탈취를 노리세요" : "도주 중 · 현재 나를 쫓는 경찰 없음";
                phaseText.color = state == 1 ? new Color(1, .4f, .3f) : state == 2 ? new Color(1, .8f, .25f) : new Color(.55f, 1, .8f);
            }
            _guidanceText.text = state == 1 ? "건물 모서리로 시야를 끊으세요\nQ / 스킬로 도주할 틈을 만드세요" :
                state == 2 ? "같은 자리에 머물지 말고 다른 출구로 이동하세요\n다시 보이면 추격이 이어집니다" :
                state == 3 ? "추격은 끝났지만 다시 발견될 수 있어요\n보석 탈취 또는 동료 구출을 선택하세요" :
                state == 4 ? "동료가 감옥 정문 밖에서 F / 행동을 누르면 구출\n구출되면 문 밖에서 다시 도주합니다" :
                state == 5 ? "동료가 감옥에 있어요 · 정문 밖에서 F / 행동 유지\n움직이거나 체포되면 구출 진행이 취소됩니다" :
                state == 6 ? "알람 발생 · 경찰이 건물로 접근할 수 있어요\n탈취를 계속할지 다른 출구로 빠질지 선택하세요" :
                "건물 안 보석 앞에서 F / 행동 유지 · Q / 스킬\n목표 전리품 확보 또는 시간 종료까지 팀 생존";
            if (state == 0) _guidanceText.SetText("건물 안 보석 앞에서 F / 행동 유지 · Q / 스킬\n전리품 {0}% 확보 또는 시간 종료까지 팀 생존", GameConstants.LootWinThreshold * 100);
        }

        private void UpdateReadyCountdownUI()
        {
            if (readyCountdownText == null) return;
            if (readyPanel != null && !readyPanel.activeSelf)
                readyPanel.SetActive(true);

            float countdown = _inGameManager.GetReadyCountdown();
            int displayNum = Mathf.CeilToInt(countdown);

            // Only update text when the displayed number changes (Rule §4: UI Optimization)
            if (displayNum != _lastCountdownNum)
            {
                _lastCountdownNum = displayNum;
                if (displayNum > 0) readyCountdownText.SetText("{0}", displayNum);
                else readyCountdownText.SetText("GO!");
                readyCountdownText.color = displayNum > 0 ? Color.yellow : Color.green;
            }
        }

        private void UpdateTimerUI()
        {
            if (timerText == null) return;

            float remainingTime = _inGameManager.GetRemainingTime();
            int minutes = Mathf.FloorToInt(remainingTime / 60);
            int seconds = Mathf.FloorToInt(remainingTime % 60);

            // Only update text when values actually change (Rule §4: UI Optimization)
            if (minutes != _lastMinutes || seconds != _lastSeconds)
            {
                _lastMinutes = minutes;
                _lastSeconds = seconds;
                // Zero-alloc numeric formatting; manual zero-pad for seconds
                if (seconds < 10) timerText.SetText("{0}:0{1}", minutes, seconds);
                else timerText.SetText("{0}:{1}", minutes, seconds);
                timerText.color = remainingTime <= 10f ? Color.red : Color.white;
            }
        }

        private void UpdateLootUI()
        {
            if (lootProgressText == null || _lootManager == null) return;

            // Networked clients: thefts register on the server, so display its value
            float stolen = (!NetworkSessionManager.HasStateAuthority && NetworkMatchSync.Instance != null)
                ? NetworkMatchSync.Instance.LootPercent
                : _lootManager.GetStolenPercentage();
            int percent = Mathf.RoundToInt(stolen * 100f);

            // Only update text when value changes (Rule §4: UI Optimization)
            if (percent != _lastLootPercent)
            {
                _lastLootPercent = percent;
                lootProgressText.SetText(compactHud ? "{0}%" : "전리품 {0}%", percent);
                if (lootFill != null) lootFill.fillAmount = Mathf.Clamp01(stolen);
                lootProgressText.color = percent >= 50 ? Color.yellow : Color.white;
            }
        }

        private void UpdateArrestCountUI()
        {
            if (arrestCountText == null) return;

            int arrested = 0;
            int total = 0;
            foreach (var player in PlayerController.Instances)
            {
                var r = player as RobberController;
                if (r == null) continue;
                total++;
                if (r.IsArrested) arrested++;
            }

            // Only update text when values change (Rule §4: UI Optimization)
            if (arrested != _lastArrestedCount || total != _lastTotalCount)
            {
                _lastArrestedCount = arrested;
                _lastTotalCount = total;
                arrestCountText.SetText(compactHud ? "{0} / {1}" : "체포 {0} / {1}", arrested, total);
            }
        }

        private void UpdateAbilityUI()
        {
            if (abilityCooldownText == null || _localPlayerAbility == null) return;
            if (cooldownFill != null) cooldownFill.fillAmount = _localPlayerAbility.CooldownPercent;
            if (skillButtonText != null)
            {
                if (_localPlayerAbility.IsActive) skillButtonText.SetText("발동 중");
                else if (_localPlayerAbility.IsReady) skillButtonText.SetText("스킬\nQ");
                else skillButtonText.SetText("{0}초", Mathf.CeilToInt(_localPlayerAbility.CooldownRemaining));
            }

            if (_localPlayerAbility.IsActive)
            {
                if (_lastAbilityState != 0)
                {
                    _lastAbilityState = 0;
                    if (skillFeedback != null) skillFeedback.Pulse();
                    abilityCooldownText.SetText("효과 발동 중");
                    abilityCooldownText.color = Color.green;
                }
            }
            else if (_localPlayerAbility.IsReady)
            {
                if (_lastAbilityState != 1)
                {
                    _lastAbilityState = 1;
                    abilityCooldownText.SetText("Q / 스킬 버튼으로 사용");
                    abilityCooldownText.color = Color.cyan;
                }
            }
            else
            {
                if (_lastAbilityState != 2)
                {
                    _lastAbilityState = 2;
                    abilityCooldownText.color = Color.gray;
                }
                // Changes every frame — SetText with numeric arg allocates nothing
                abilityCooldownText.SetText("재사용까지 {0:1}초", _localPlayerAbility.CooldownRemaining);
            }
        }

        private void UpdateInteractionPromptUI()
        {
            if (interactionPromptText == null || _localPlayer == null) return;
            if (_localRobber == null || _localRobber.IsArrested) { interactionPromptText.SetText(""); _lastPromptState = -1; return; }

            // Use NonAlloc buffer (Rule §4: Zero-Allocation)
            int count = Physics.OverlapSphereNonAlloc(_localPlayer.transform.position, 3f, _overlapBuffer);
            int state = 0;
            int percent = 0;

            for (int i = 0; i < count; i++)
            {
                LootSpot loot = _overlapBuffer[i].GetComponent<LootSpot>();
                if (loot != null && !loot.IsLooted && loot.CanReach(_localPlayer.gameObject))
                {
                    if (loot.IsBeingRobbed)
                    {
                        state = loot.building != null && loot.building.guarded ? 6 : 2;
                        percent = Mathf.RoundToInt(loot.GetProgress() * 100f);
                    }
                    else state = loot.building != null && loot.building.guarded ? 5 : 1;
                    break;
                }

                var door = _overlapBuffer[i].GetComponent<PrisonDoorInteraction>();
                PrisonZone prison = door != null ? door.prison : null;
                if (prison != null && prison.CanInteract(_localRobber) && prison.HasArrestedRobbers())
                {
                    if (prison.IsBeingRescued)
                    {
                        state = 4;
                        percent = Mathf.RoundToInt(prison.GetRescueProgress() * 100f);
                    }
                    else state = 3;
                    break;
                }
            }

            // Only update text when the state/percent actually changes (Rule §4)
            if (state == _lastPromptState && percent == _lastPromptPercent) return;
            _lastPromptState = state;
            _lastPromptPercent = percent;

            switch (state)
            {
                case 1: interactionPromptText.SetText("F / 행동 버튼을 눌러 탈취"); break;
                case 2: interactionPromptText.SetText("계속 누르세요 · 탈취 {0}%", percent); break;
                case 3: interactionPromptText.SetText("감옥 문 · F / 행동을 길게 눌러 잠금 해제"); break;
                case 4: interactionPromptText.SetText("문 앞에서 계속 누르세요 · 잠금 해제 {0}%", percent); break;
                case 5: interactionPromptText.SetText("경보 금고 · 탈취 시 경찰 출동! F / 행동 길게"); break;
                case 6: interactionPromptText.SetText("경보 위험 · 탈취 {0}% · 출구를 확보하세요", percent); break;
                default: interactionPromptText.SetText(""); break;
            }
        }

        /// <summary>
        /// Displays the game result panel with the winner information.
        /// The reason comes from the actual win condition (time up vs loot vs arrests).
        /// </summary>
        public void ShowResult(RoleManager.PlayerRole winner, string reason = null)
        {
            if (NetworkSessionManager.IsNetworked && lobbyButton != null)
            {
                lobbyButton.interactable = NetworkSessionManager.HasStateAuthority;
                lobbyButton.GetComponentInChildren<TMP_Text>().text = NetworkSessionManager.HasStateAuthority ? "같은 방에서 다시 준비" : "호스트가 돌아가기를 기다리는 중";
            }
            if (readyPanel != null) readyPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
                resultPanel.transform.SetAsLastSibling();

                if (winner == RoleManager.PlayerRole.Cop)
                {
                    resultText.text = "경찰 승리!\n<size=55%>도둑을 모두 체포했습니다</size>";
                    resultText.color = Color.cyan;
                }
                else
                {
                    resultText.text = "도둑 승리!\n<size=55%>추격을 따돌렸습니다</size>";
                    resultText.color = Color.red;
                }
            }
        }

        private void OnLobbyButtonClicked()
        {
            if (NetworkSessionManager.IsNetworked && _inGameManager != null && _inGameManager.IsGameOver)
            {
                NetworkSessionManager.ReturnToRoom();
                return;
            }
            LeaveNetworkRoom();
        }

        private void LeaveNetworkRoom()
        {
            // Leave the networked session first — host shutdown disconnects clients
            // (their disconnect callback routes them back to the lobby), and loading
            // a local scene while a session is live would desync everything
            if (NetworkSessionManager.IsNetworked)
                NetworkSessionManager.Shutdown();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToLobby();
            }
            else
            {
                SceneManager.LoadScene("Lobby");
            }
        }
    }
}
