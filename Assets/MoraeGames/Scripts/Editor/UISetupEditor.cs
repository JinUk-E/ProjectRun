using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using MoraeGames.UI;
using MoraeGames.Game;
using MoraeGames.Skill;
using UnityEditor.Events;
using UnityEngine.Events;

namespace MoraeGames.Editor
{
    public class UISetupEditor : MonoBehaviour
    {
        // Korean-capable font applied to every generated text.
        // Created via "MoraeGames/Setup Korean Font Asset" (FontSetupEditor).
        private static TMP_FontAsset _koreanFont;

        private static TMP_FontAsset KoreanFont
        {
            get
            {
                if (_koreanFont == null)
                    _koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSetupEditor.FontAssetPath);
                return _koreanFont;
            }
        }

        private static void ApplyKoreanFont(TMP_Text text)
        {
            if (KoreanFont != null) text.font = KoreanFont;
        }

        /// <summary>Adds a vertex-color gradient to any Image-bearing object.</summary>
        private static void AddGradient(GameObject target, Color top, Color bottom)
        {
            UIGradient gradient = target.AddComponent<UIGradient>();
            gradient.direction = UIGradient.Direction.Vertical;
            gradient.topColor = top;
            gradient.bottomColor = bottom;
        }

        /// <summary>Rounded sprite, glossy gradient, drop shadow, and press/hover transitions.</summary>
        private static void StyleButtonVisual(Button button)
        {
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = UISpriteFactory.RoundedRect;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 0.45f;
            }

            // Top-lit gloss: multiplies with Image.color, so per-button tints still apply
            AddGradient(button.gameObject, Color.white, new Color(0.68f, 0.68f, 0.68f));

            Shadow shadow = button.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(0f, -8f);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        /// <summary>Rounded corners and a subtle vertical shade for background panels.</summary>
        private static void StylePanelVisual(Image panelImage, float cornerScale = 0.25f)
        {
            panelImage.sprite = UISpriteFactory.RoundedRect;
            panelImage.type = Image.Type.Sliced;
            panelImage.pixelsPerUnitMultiplier = cornerScale;
            AddGradient(panelImage.gameObject, Color.white, new Color(0.82f, 0.82f, 0.86f));
        }

        /// <summary>
        /// Anchors a rect purely by screen fractions (Rule §6: anchor-based layout for
        /// all aspect ratios). The rect stretches with its anchors; no pixel offsets.
        /// </summary>
        private static void SetProportionalRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        [MenuItem("MoraeGames/Setup MainMenu UI")]
        public static void SetupMainMenu()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            if (currentScene.name != "MainMenu")
            {
                Debug.Log("Loading MainMenu scene for UI setup...");
                EditorSceneManager.OpenScene("Assets/MoraeGames/Scenes/MainMenu.unity");
                currentScene = SceneManager.GetActiveScene();
            }

            EnsureMainCamera();
            CleanUpOldUI();

            GameObject canvasObj = CreateCanvasSetup();
            MainMenuManager menuManager = canvasObj.AddComponent<MainMenuManager>();

            // Create Title
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(canvasObj.transform, false);
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Cops and Robbers\n<size=80>Online</size>";
            titleText.fontSize = 200;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.raycastTarget = false;
            // Gold vertex gradient for a title-screen look without any art assets
            titleText.enableVertexGradient = true;
            titleText.colorGradient = new VertexGradient(
                new Color(1f, 0.95f, 0.6f), new Color(1f, 0.95f, 0.6f),
                new Color(1f, 0.6f, 0.15f), new Color(1f, 0.6f, 0.15f));
            ApplyKoreanFont(titleText);

            SetProportionalRect(titleObj.GetComponent<RectTransform>(),
                new Vector2(0.20f, 0.70f), new Vector2(0.80f, 0.95f));

            Button startGameBtn = CreateMenuButton("StartButton", "START GAME", Vector2.zero, canvasObj.transform, menuManager.LoadLobbyScene);
            SetProportionalRect(startGameBtn.GetComponent<RectTransform>(),
                new Vector2(0.40f, 0.40f), new Vector2(0.60f, 0.50f));
            Button quitBtn = CreateMenuButton("QuitButton", "QUIT GAME", Vector2.zero, canvasObj.transform, menuManager.QuitGame);
            SetProportionalRect(quitBtn.GetComponent<RectTransform>(),
                new Vector2(0.40f, 0.26f), new Vector2(0.60f, 0.36f));

            CreateBackground(canvasObj.transform);

            EditorSceneManager.MarkSceneDirty(currentScene);
            Debug.Log("MainMenu UI automatically constructed!");
        }

        [MenuItem("MoraeGames/Setup Lobby UI")]
        public static void SetupLobbyUI()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            if (currentScene.name != "Lobby")
            {
                Debug.Log("Loading Lobby scene for UI setup...");
                EditorSceneManager.OpenScene("Assets/MoraeGames/Scenes/Lobby.unity");
                currentScene = SceneManager.GetActiveScene();
            }

            EnsureMainCamera();
            CleanUpOldUI();

            GameObject canvasObj = CreateCanvasSetup();
            LobbyManager lobbyManager = canvasObj.AddComponent<LobbyManager>();

            // Title
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(canvasObj.transform, false);
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Select Your Role";
            titleText.fontSize = 150;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            titleText.raycastTarget = false;
            ApplyKoreanFont(titleText);
            SetProportionalRect(titleObj.GetComponent<RectTransform>(),
                new Vector2(0.25f, 0.87f), new Vector2(0.75f, 0.98f));

            // Role Status Text
            GameObject roleTxtObj = new GameObject("RoleStatusText");
            roleTxtObj.transform.SetParent(canvasObj.transform, false);
            TextMeshProUGUI roleText = roleTxtObj.AddComponent<TextMeshProUGUI>();
            roleText.text = "Role: Not Selected";
            roleText.fontSize = 100;
            roleText.alignment = TextAlignmentOptions.Center;
            roleText.color = Color.yellow;
            roleText.raycastTarget = false;
            ApplyKoreanFont(roleText);
            SetProportionalRect(roleTxtObj.GetComponent<RectTransform>(),
                new Vector2(0.30f, 0.78f), new Vector2(0.70f, 0.86f));

            // Role buttons — proportional, left/right of center, clear of the skill panel
            Button copBtn = CreateMenuButton("CopButton", "COP", Vector2.zero, canvasObj.transform, lobbyManager.SelectCopRole);
            copBtn.GetComponent<Image>().color = new Color(0.2f, 0.4f, 1f); // Blue
            SetProportionalRect(copBtn.GetComponent<RectTransform>(),
                new Vector2(0.28f, 0.66f), new Vector2(0.44f, 0.75f));
            Button robBtn = CreateMenuButton("RobberButton", "ROBBER", Vector2.zero, canvasObj.transform, lobbyManager.SelectRobberRole);
            robBtn.GetComponent<Image>().color = new Color(1f, 0.2f, 0.2f); // Red
            SetProportionalRect(robBtn.GetComponent<RectTransform>(),
                new Vector2(0.50f, 0.66f), new Vector2(0.66f, 0.75f));

            // Character Selection Panel (hidden by default) — left/center band,
            // horizontally clear of the right-edge skill panel
            GameObject charPanel = new GameObject("CharacterSelectPanel");
            charPanel.transform.SetParent(canvasObj.transform, false);
            Image charPanelBg = charPanel.AddComponent<Image>();
            charPanelBg.color = new Color(0.05f, 0.05f, 0.1f, 0.9f);
            StylePanelVisual(charPanelBg);
            SetProportionalRect(charPanel.GetComponent<RectTransform>(),
                new Vector2(0.02f, 0.28f), new Vector2(0.74f, 0.62f));

            // Character Select Title
            TMP_Text charSelectTitle = CreateHUDText(charPanel.transform, "CharSelectTitle",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, "Select Character", 60);
            charSelectTitle.alignment = TextAlignmentOptions.Center;
            charSelectTitle.color = Color.yellow;
            SetProportionalRect(charSelectTitle.rectTransform,
                new Vector2(0.25f, 0.84f), new Vector2(0.75f, 0.99f));

            // Create 5 character card buttons (max for robbers; cops use 3)
            int maxCards = 5;
            Button[] charButtons = new Button[maxCards];
            TMP_Text[] charNameTexts = new TMP_Text[maxCards];
            TMP_Text[] charAbilityTexts = new TMP_Text[maxCards];
            Image[] charButtonImages = new Image[maxCards];

            // Card layout as fractions of the panel: 5 cards, 18% width each, 1.6% gaps
            const float cardWidthFrac = 0.18f;
            const float cardGapFrac = 0.016f;
            const float cardStartFrac = 0.02f;

            UnityAction[] selectActions = {
                lobbyManager.SelectCharacter0,
                lobbyManager.SelectCharacter1,
                lobbyManager.SelectCharacter2,
                lobbyManager.SelectCharacter3,
                lobbyManager.SelectCharacter4
            };

            for (int i = 0; i < maxCards; i++)
            {
                float xMin = cardStartFrac + i * (cardWidthFrac + cardGapFrac);

                // Card button — proportional slot inside the panel
                GameObject cardObj = new GameObject($"CharCard_{i}");
                cardObj.transform.SetParent(charPanel.transform, false);
                RectTransform cardRect = cardObj.AddComponent<RectTransform>();
                SetProportionalRect(cardRect,
                    new Vector2(xMin, 0.06f), new Vector2(xMin + cardWidthFrac, 0.80f));

                Image cardBg = cardObj.AddComponent<Image>();
                cardBg.color = new Color(0.2f, 0.4f, 1f);

                Button cardBtn = cardObj.AddComponent<Button>();
                UnityEventTools.AddPersistentListener(cardBtn.onClick, selectActions[i]);
                StyleButtonVisual(cardBtn);

                // Character name text
                GameObject nameObj = new GameObject("NameText");
                nameObj.transform.SetParent(cardObj.transform, false);
                TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
                nameText.fontSize = 50;
                nameText.fontStyle = FontStyles.Bold;
                nameText.alignment = TextAlignmentOptions.Center;
                nameText.color = Color.white;
                nameText.text = $"Character {i + 1}";
                nameText.raycastTarget = false;
                ApplyKoreanFont(nameText);
                RectTransform nameRect = nameObj.GetComponent<RectTransform>();
                nameRect.anchorMin = new Vector2(0, 0.65f);
                nameRect.anchorMax = new Vector2(1, 1f);
                nameRect.sizeDelta = Vector2.zero;
                nameRect.offsetMin = new Vector2(10, 0);
                nameRect.offsetMax = new Vector2(-10, -10);

                // Ability text
                GameObject abilityObj = new GameObject("AbilityText");
                abilityObj.transform.SetParent(cardObj.transform, false);
                TextMeshProUGUI abilityText = abilityObj.AddComponent<TextMeshProUGUI>();
                abilityText.fontSize = 40;
                abilityText.alignment = TextAlignmentOptions.Center;
                abilityText.color = new Color(0.9f, 0.9f, 0.9f);
                abilityText.text = "Ability";
                abilityText.raycastTarget = false;
                ApplyKoreanFont(abilityText);
                RectTransform abilityRect = abilityObj.GetComponent<RectTransform>();
                abilityRect.anchorMin = new Vector2(0, 0f);
                abilityRect.anchorMax = new Vector2(1, 0.65f);
                abilityRect.sizeDelta = Vector2.zero;
                abilityRect.offsetMin = new Vector2(10, 10);
                abilityRect.offsetMax = new Vector2(-10, 0);

                charButtons[i] = cardBtn;
                charNameTexts[i] = nameText;
                charAbilityTexts[i] = abilityText;
                charButtonImages[i] = cardBg;
            }

            // Selected character text (below panel)
            TMP_Text selectedCharText = CreateHUDText(canvasObj.transform, "SelectedCharText",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, "Selected: None", 60);
            selectedCharText.alignment = TextAlignmentOptions.Center;
            selectedCharText.color = Color.yellow;
            SetProportionalRect(selectedCharText.rectTransform,
                new Vector2(0.25f, 0.20f), new Vector2(0.55f, 0.26f));

            Button startBtn = CreateMenuButton("StartMatchButton", "START MATCH", Vector2.zero, canvasObj.transform, lobbyManager.StartMatch);
            startBtn.GetComponent<Image>().color = new Color(0.2f, 0.8f, 0.2f); // Green
            SetProportionalRect(startBtn.GetComponent<RectTransform>(),
                new Vector2(0.30f, 0.07f), new Vector2(0.50f, 0.17f));

            // Network entry points: Relay host (join code) / join by code
            Button hostBtn = CreateMenuButton("HostMatchButton", "HOST", Vector2.zero, canvasObj.transform, lobbyManager.HostMatch);
            hostBtn.GetComponent<Image>().color = new Color(0.55f, 0.35f, 0.9f);
            SetProportionalRect(hostBtn.GetComponent<RectTransform>(),
                new Vector2(0.03f, 0.07f), new Vector2(0.14f, 0.15f));
            Button joinBtn = CreateMenuButton("JoinMatchButton", "JOIN", Vector2.zero, canvasObj.transform, lobbyManager.JoinMatch);
            joinBtn.GetComponent<Image>().color = new Color(0.35f, 0.55f, 0.9f);
            SetProportionalRect(joinBtn.GetComponent<RectTransform>(),
                new Vector2(0.155f, 0.07f), new Vector2(0.265f, 0.15f));

            // Join-code input (above HOST/JOIN)
            GameObject inputObj = new GameObject("JoinCodeInput");
            inputObj.transform.SetParent(canvasObj.transform, false);
            Image inputBg = inputObj.AddComponent<Image>();
            inputBg.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);
            StylePanelVisual(inputBg, 0.45f);
            SetProportionalRect(inputObj.GetComponent<RectTransform>(),
                new Vector2(0.03f, 0.16f), new Vector2(0.265f, 0.23f));

            TMP_InputField joinCodeInput = inputObj.AddComponent<TMP_InputField>();

            GameObject textArea = new GameObject("TextArea");
            textArea.transform.SetParent(inputObj.transform, false);
            textArea.AddComponent<RectMask2D>();
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(30f, 10f);
            textAreaRect.offsetMax = new Vector2(-30f, -10f);

            TMP_Text inputText = CreateHUDText(textArea.transform, "Text",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, "", 55);
            inputText.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_Text placeholder = CreateHUDText(textArea.transform, "Placeholder",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, "JOIN CODE", 55);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);

            joinCodeInput.textViewport = textAreaRect;
            joinCodeInput.textComponent = inputText;
            joinCodeInput.placeholder = placeholder;
            joinCodeInput.characterLimit = 10;

            // Network status line (code / players / errors)
            TMP_Text networkStatusText = CreateHUDText(canvasObj.transform, "NetworkStatusText",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, "", 45);
            networkStatusText.alignment = TextAlignmentOptions.MidlineLeft;
            networkStatusText.color = new Color(0.6f, 0.9f, 1f);
            SetProportionalRect(networkStatusText.rectTransform,
                new Vector2(0.03f, 0.24f), new Vector2(0.40f, 0.28f));

            // Host-only match start (hidden until hosting)
            Button startNetBtn = CreateMenuButton("StartNetworkMatchButton", "START NET MATCH",
                Vector2.zero, canvasObj.transform, lobbyManager.StartNetworkMatch);
            startNetBtn.GetComponent<Image>().color = new Color(0.9f, 0.55f, 0.2f);
            SetProportionalRect(startNetBtn.GetComponent<RectTransform>(),
                new Vector2(0.03f, 0.29f), new Vector2(0.22f, 0.37f));
            startNetBtn.gameObject.SetActive(false);

            lobbyManager.joinCodeInput = joinCodeInput;
            lobbyManager.networkStatusText = networkStatusText;
            lobbyManager.startNetworkMatchButton = startNetBtn;

            // Skill Tree Panel (right edge) — needs assets from "MoraeGames/Setup Skill Assets"
            string[] skillOrder = { "swift_feet", "sprinter", "conditioning", "tactician", "quick_hands", "rescue_expert", "long_arm" };
            var skillNodeList = new List<SkillNode>();
            foreach (string skillId in skillOrder)
            {
                SkillNode node = AssetDatabase.LoadAssetAtPath<SkillNode>($"Assets/MoraeGames/Resources/Skills/{skillId}.asset");
                if (node != null) skillNodeList.Add(node);
            }

            if (skillNodeList.Count == 0)
            {
                Debug.LogWarning("[UISetup] No skill assets found — run 'MoraeGames/Setup Skill Assets' first, " +
                                 "then re-run this menu to get the lobby skill panel.");
            }
            else
            {
                GameObject skillPanel = new GameObject("SkillPanel");
                skillPanel.transform.SetParent(canvasObj.transform, false);
                Image skillBg = skillPanel.AddComponent<Image>();
                skillBg.color = new Color(0.05f, 0.05f, 0.1f, 0.85f);
                StylePanelVisual(skillBg);
                SetProportionalRect(skillPanel.GetComponent<RectTransform>(),
                    new Vector2(0.77f, 0.05f), new Vector2(0.98f, 0.95f));

                TMP_Text pointsText = CreateHUDText(skillPanel.transform, "SkillPointsText",
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero, "Skill Points: 0", 55);
                pointsText.alignment = TextAlignmentOptions.Center;
                pointsText.color = Color.yellow;
                SetProportionalRect(pointsText.rectTransform,
                    new Vector2(0.05f, 0.93f), new Vector2(0.95f, 0.995f));

                var skillButtons = new Button[skillNodeList.Count];
                var skillButtonTexts = new TMP_Text[skillNodeList.Count];
                for (int i = 0; i < skillNodeList.Count; i++)
                {
                    GameObject skillBtnObj = new GameObject($"SkillButton_{skillNodeList[i].id}");
                    skillBtnObj.transform.SetParent(skillPanel.transform, false);
                    Image skillBtnImg = skillBtnObj.AddComponent<Image>();
                    skillBtnImg.color = new Color(0.35f, 0.35f, 0.4f);
                    // Proportional slot inside the panel: 12% height rows from the top
                    float slotTop = 0.91f - i * 0.125f;
                    SetProportionalRect(skillBtnObj.GetComponent<RectTransform>(),
                        new Vector2(0.04f, slotTop - 0.115f), new Vector2(0.96f, slotTop));

                    Button skillBtn = skillBtnObj.AddComponent<Button>();
                    UnityEventTools.AddIntPersistentListener(skillBtn.onClick, lobbyManager.UnlockSkill, i);
                    StyleButtonVisual(skillBtn);

                    TMP_Text skillLabel = CreateHUDText(skillBtnObj.transform, "Label",
                        Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                        Vector2.zero, Vector2.zero, skillNodeList[i].displayName, 44);
                    skillLabel.alignment = TextAlignmentOptions.Center;

                    skillButtons[i] = skillBtn;
                    skillButtonTexts[i] = skillLabel;
                }

                lobbyManager.skillPointsText = pointsText;
                lobbyManager.skillButtons = skillButtons;
                lobbyManager.skillButtonTexts = skillButtonTexts;
                lobbyManager.skillNodes = skillNodeList.ToArray();
            }

            // Assign references to LobbyManager
            lobbyManager.roleText = roleText;
            lobbyManager.startMatchButton = startBtn;
            lobbyManager.characterSelectPanel = charPanel;
            lobbyManager.characterButtons = charButtons;
            lobbyManager.characterNameTexts = charNameTexts;
            lobbyManager.characterAbilityTexts = charAbilityTexts;
            lobbyManager.characterButtonImages = charButtonImages;
            lobbyManager.selectedCharacterText = selectedCharText;

            charPanel.SetActive(false);

            CreateBackground(canvasObj.transform);

            EditorSceneManager.MarkSceneDirty(currentScene);
            Debug.Log("Lobby UI automatically constructed!");
        }

        [MenuItem("MoraeGames/Setup InGame UI")]
        public static void SetupInGameUI()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            if (currentScene.name != "InGame")
            {
                Debug.Log("Loading InGame scene for UI setup...");
                EditorSceneManager.OpenScene("Assets/MoraeGames/Scenes/InGame.unity");
                currentScene = SceneManager.GetActiveScene();
            }

            EnsureMainCamera();
            CleanUpOldUI();

            GameObject canvasObj = CreateCanvasSetup();
            InGameUIManager uiManager = canvasObj.AddComponent<InGameUIManager>();

            // Timer Panel (Background for timer)
            GameObject timerPanel = new GameObject("TimerPanel");
            timerPanel.transform.SetParent(canvasObj.transform, false);
            Image timerBg = timerPanel.AddComponent<Image>();
            timerBg.color = new Color(0, 0, 0, 0.5f);
            timerBg.raycastTarget = false;
            StylePanelVisual(timerBg);
            SetProportionalRect(timerPanel.GetComponent<RectTransform>(),
                new Vector2(0.42f, 0.90f), new Vector2(0.58f, 0.99f));

            // Timer Text
            GameObject timerObj = new GameObject("TimerText");
            timerObj.transform.SetParent(timerPanel.transform, false);
            TextMeshProUGUI timerText = timerObj.AddComponent<TextMeshProUGUI>();
            timerText.fontSize = 120;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.color = Color.white;
            timerText.text = "02:00";
            timerText.raycastTarget = false;
            ApplyKoreanFont(timerText);
            
            RectTransform timerRect = timerObj.GetComponent<RectTransform>();
            timerRect.anchorMin = Vector2.zero;
            timerRect.anchorMax = Vector2.one;
            timerRect.sizeDelta = Vector2.zero;

            // Mobile touch controls (virtual joystick + action buttons).
            // Created before the Result/Ready overlays so those render on top.
            CreateMobileControls(canvasObj.transform);

            // Result Panel
            GameObject resultPanel = new GameObject("ResultPanel");
            resultPanel.transform.SetParent(canvasObj.transform, false);
            Image panelImg = resultPanel.AddComponent<Image>();
            panelImg.color = new Color(0, 0, 0, 0.9f);
            
            RectTransform panelRect = resultPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;

            // Result Text
            GameObject resultTxtObj = new GameObject("ResultText");
            resultTxtObj.transform.SetParent(resultPanel.transform, false);
            TextMeshProUGUI resultText = resultTxtObj.AddComponent<TextMeshProUGUI>();
            resultText.fontSize = 250;
            resultText.alignment = TextAlignmentOptions.Center;
            resultText.color = Color.yellow;
            resultText.text = "GAME OVER";
            resultText.raycastTarget = false;
            resultText.enableAutoSizing = true;
            resultText.fontSizeMax = 250;
            resultText.fontSizeMin = 80;
            ApplyKoreanFont(resultText);

            SetProportionalRect(resultTxtObj.GetComponent<RectTransform>(),
                new Vector2(0.10f, 0.52f), new Vector2(0.90f, 0.82f));

            // Lobby Button
            Button lobbyBtn = CreateMenuButton("LobbyButton", "BACK TO LOBBY", Vector2.zero, resultPanel.transform, null);
            lobbyBtn.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.8f);
            SetProportionalRect(lobbyBtn.GetComponent<RectTransform>(),
                new Vector2(0.40f, 0.26f), new Vector2(0.60f, 0.36f));

            // HUD: Loot Progress (top-left)
            TMP_Text lootProgressText = CreateHUDText(canvasObj.transform, "LootProgressText",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, "Loot: 0%", 50);
            SetProportionalRect(lootProgressText.rectTransform,
                new Vector2(0.02f, 0.93f), new Vector2(0.20f, 0.99f));

            // HUD: Arrest Count (top-left, below loot)
            TMP_Text arrestCountText = CreateHUDText(canvasObj.transform, "ArrestCountText",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, "Arrested: 0/5", 50);
            SetProportionalRect(arrestCountText.rectTransform,
                new Vector2(0.02f, 0.86f), new Vector2(0.20f, 0.92f));

            // HUD: Ability Cooldown (bottom-center)
            TMP_Text abilityCooldownText = CreateHUDText(canvasObj.transform, "AbilityCooldownText",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, "[Q] READY", 50);
            abilityCooldownText.color = Color.cyan;
            abilityCooldownText.alignment = TextAlignmentOptions.Center;
            SetProportionalRect(abilityCooldownText.rectTransform,
                new Vector2(0.40f, 0.09f), new Vector2(0.60f, 0.16f));

            // HUD: Interaction Prompt (bottom-center, above ability)
            TMP_Text interactionPromptText = CreateHUDText(canvasObj.transform, "InteractionPromptText",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, "", 50);
            interactionPromptText.color = Color.yellow;
            interactionPromptText.alignment = TextAlignmentOptions.Center;
            SetProportionalRect(interactionPromptText.rectTransform,
                new Vector2(0.36f, 0.17f), new Vector2(0.64f, 0.24f));

            // Minimap (top-right corner)
            GameObject minimapPanel = new GameObject("MinimapPanel");
            minimapPanel.transform.SetParent(canvasObj.transform, false);
            Image minimapBg = minimapPanel.AddComponent<Image>();
            minimapBg.color = new Color(0, 0, 0, 0.7f);
            minimapBg.raycastTarget = false;
            StylePanelVisual(minimapBg, 0.35f);
            SetProportionalRect(minimapPanel.GetComponent<RectTransform>(),
                new Vector2(0.86f, 0.76f), new Vector2(0.99f, 0.99f));

            GameObject minimapImgObj = new GameObject("MinimapImage");
            minimapImgObj.transform.SetParent(minimapPanel.transform, false);
            RawImage minimapRawImage = minimapImgObj.AddComponent<RawImage>();
            RectTransform minimapImgRect = minimapImgObj.GetComponent<RectTransform>();
            minimapImgRect.anchorMin = Vector2.zero;
            minimapImgRect.anchorMax = Vector2.one;
            minimapImgRect.sizeDelta = new Vector2(-10, -10); // Padding

            // Ping Indicator (bottom-left, above the virtual joystick)
            TMP_Text pingIndicatorText = CreateHUDText(canvasObj.transform, "PingIndicatorText",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, "", 50);
            pingIndicatorText.color = new Color(0f, 1f, 0.5f);
            SetProportionalRect(pingIndicatorText.rectTransform,
                new Vector2(0.02f, 0.38f), new Vector2(0.28f, 0.44f));

            // System Message (center of screen)
            GameObject sysMsgPanel = new GameObject("SystemMessagePanel");
            sysMsgPanel.transform.SetParent(canvasObj.transform, false);
            Image sysMsgBg = sysMsgPanel.AddComponent<Image>();
            sysMsgBg.color = new Color(0, 0, 0, 0.6f);
            sysMsgBg.raycastTarget = false;
            StylePanelVisual(sysMsgBg);
            SetProportionalRect(sysMsgPanel.GetComponent<RectTransform>(),
                new Vector2(0.30f, 0.56f), new Vector2(0.70f, 0.66f));

            TMP_Text sysMsgText = CreateHUDText(sysMsgPanel.transform, "SystemMessageText",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, "", 100);
            sysMsgText.alignment = TextAlignmentOptions.Center;

            // Ready Countdown (center of screen, large)
            GameObject readyPanel = new GameObject("ReadyPanel");
            readyPanel.transform.SetParent(canvasObj.transform, false);
            Image readyBg = readyPanel.AddComponent<Image>();
            readyBg.color = new Color(0, 0, 0, 0.7f);
            RectTransform readyRect = readyPanel.GetComponent<RectTransform>();
            readyRect.anchorMin = Vector2.zero;
            readyRect.anchorMax = Vector2.one;
            readyRect.sizeDelta = Vector2.zero;

            TMP_Text readyCountdownText = CreateHUDText(readyPanel.transform, "ReadyCountdownText",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, "3", 300);
            readyCountdownText.color = Color.yellow;
            readyCountdownText.alignment = TextAlignmentOptions.Center;
            SetProportionalRect(readyCountdownText.rectTransform,
                new Vector2(0.40f, 0.35f), new Vector2(0.60f, 0.65f));

            // Pause button (top-left, under the arrest counter)
            Button pauseBtn = CreateMenuButton("PauseButton", "II", Vector2.zero, canvasObj.transform, null);
            pauseBtn.GetComponent<Image>().color = new Color(0.25f, 0.28f, 0.38f, 0.9f);
            SetProportionalRect(pauseBtn.GetComponent<RectTransform>(),
                new Vector2(0.02f, 0.76f), new Vector2(0.08f, 0.84f));

            // Pause panel — created LAST so it renders above the HUD, touch controls,
            // and ready overlay. Its background image keeps raycastTarget ON to swallow
            // all touches behind it while paused.
            GameObject pausePanel = new GameObject("PausePanel");
            pausePanel.transform.SetParent(canvasObj.transform, false);
            Image pauseBg = pausePanel.AddComponent<Image>();
            pauseBg.color = new Color(0f, 0f, 0f, 0.78f);
            RectTransform pauseRect = pausePanel.GetComponent<RectTransform>();
            pauseRect.anchorMin = Vector2.zero;
            pauseRect.anchorMax = Vector2.one;
            pauseRect.sizeDelta = Vector2.zero;

            TMP_Text pausedTitle = CreateHUDText(pausePanel.transform, "PausedTitle",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, "PAUSED", 200);
            pausedTitle.alignment = TextAlignmentOptions.Center;
            pausedTitle.fontStyle = FontStyles.Bold;
            SetProportionalRect(pausedTitle.rectTransform,
                new Vector2(0.30f, 0.60f), new Vector2(0.70f, 0.78f));

            Button resumeBtn = CreateMenuButton("ResumeButton", "RESUME", Vector2.zero, pausePanel.transform, null);
            resumeBtn.GetComponent<Image>().color = new Color(0.2f, 0.8f, 0.2f); // Green
            SetProportionalRect(resumeBtn.GetComponent<RectTransform>(),
                new Vector2(0.40f, 0.42f), new Vector2(0.60f, 0.52f));

            Button exitBtn = CreateMenuButton("ExitToLobbyButton", "EXIT TO LOBBY", Vector2.zero, pausePanel.transform, null);
            exitBtn.GetComponent<Image>().color = new Color(0.85f, 0.3f, 0.25f); // Red
            SetProportionalRect(exitBtn.GetComponent<RectTransform>(),
                new Vector2(0.40f, 0.28f), new Vector2(0.60f, 0.38f));

            pausePanel.SetActive(false);

            // Assign to manager
            uiManager.timerText = timerText;
            uiManager.resultPanel = resultPanel;
            uiManager.resultText = resultText;
            uiManager.lobbyButton = lobbyBtn;
            uiManager.lootProgressText = lootProgressText;
            uiManager.arrestCountText = arrestCountText;
            uiManager.abilityCooldownText = abilityCooldownText;
            uiManager.interactionPromptText = interactionPromptText;
            uiManager.minimapImage = minimapRawImage;
            uiManager.pingIndicatorText = pingIndicatorText;
            uiManager.systemMessageText = sysMsgText;
            uiManager.systemMessagePanel = sysMsgPanel;
            uiManager.readyCountdownText = readyCountdownText;
            uiManager.readyPanel = readyPanel;
            uiManager.pauseButton = pauseBtn;
            uiManager.pausePanel = pausePanel;
            uiManager.resumeButton = resumeBtn;
            uiManager.exitToLobbyButton = exitBtn;

            resultPanel.SetActive(false);
            sysMsgPanel.SetActive(false);

            EditorSceneManager.MarkSceneDirty(currentScene);
            Debug.Log("InGame UI successfully constructed with all HUD, minimap, ping, and system message elements!");
        }

        private static void CreateMobileControls(Transform canvasParent)
        {
            GameObject root = new GameObject("MobileControls");
            root.transform.SetParent(canvasParent, false);
            RectTransform rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;
            root.AddComponent<MobileControlsUI>();

            // Procedural anti-aliased circle instead of the built-in placeholder Knob
            Sprite knob = UISpriteFactory.Circle;

            // --- Virtual Joystick (bottom-left) ---
            GameObject joyBg = new GameObject("VirtualJoystick");
            joyBg.transform.SetParent(root.transform, false);
            Image joyBgImg = joyBg.AddComponent<Image>();
            joyBgImg.sprite = knob;
            joyBgImg.color = new Color(1f, 1f, 1f, 0.25f);
            RectTransform joyRect = joyBg.GetComponent<RectTransform>();
            joyRect.anchorMin = Vector2.zero;
            joyRect.anchorMax = Vector2.zero;
            joyRect.pivot = new Vector2(0.5f, 0.5f);
            joyRect.anchoredPosition = new Vector2(500f, 500f);
            joyRect.sizeDelta = new Vector2(550f, 550f);

            GameObject handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(joyBg.transform, false);
            Image handleImg = handleObj.AddComponent<Image>();
            handleImg.sprite = knob;
            handleImg.color = new Color(1f, 1f, 1f, 0.5f);
            handleImg.raycastTarget = false; // Rule §6: non-interactive UI has no raycast
            RectTransform handleRect = handleObj.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(220f, 220f);

            VirtualJoystick joystick = joyBg.AddComponent<VirtualJoystick>();
            joystick.handle = handleRect;

            // --- Action Buttons (bottom-right) ---
            CreateHoldButton(root.transform, "AbilityButton", HoldButton.ButtonAction.Ability, "SKILL",
                new Vector2(-450f, 480f), 360f, knob, new Color(0.2f, 0.7f, 1f, 0.5f));
            CreateHoldButton(root.transform, "InteractButton", HoldButton.ButtonAction.Interact, "ACT",
                new Vector2(-920f, 380f), 320f, knob, new Color(1f, 0.8f, 0.2f, 0.5f));
            CreateHoldButton(root.transform, "PingButton", HoldButton.ButtonAction.Ping, "PING",
                new Vector2(-380f, 920f), 250f, knob, new Color(0.3f, 1f, 0.5f, 0.5f));
        }

        private static void CreateHoldButton(Transform parent, string name, HoldButton.ButtonAction action,
            string label, Vector2 anchoredPos, float size, Sprite sprite, Color color)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            Image img = btnObj.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(size, size);

            HoldButton holdButton = btnObj.AddComponent<HoldButton>();
            holdButton.action = action;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.fontSize = 72;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;
            labelText.text = label;
            labelText.raycastTarget = false; // Rule §6: non-interactive UI has no raycast
            ApplyKoreanFont(labelText);
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
        }

        private static void EnsureMainCamera()
        {
            if (Camera.main == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                Camera cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }
        }

        private static void CleanUpOldUI()
        {
            Canvas oldCanvas = FindFirstObjectByType<Canvas>();
            if (oldCanvas != null) DestroyImmediate(oldCanvas.gameObject);
            UnityEngine.EventSystems.EventSystem oldEventSystem = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (oldEventSystem != null) DestroyImmediate(oldEventSystem.gameObject);
        }

        private static GameObject CreateCanvasSetup()
        {
            if (KoreanFont == null)
                Debug.LogWarning("[UISetup] Korean font asset not found. Run 'MoraeGames/Setup Korean Font Asset' " +
                                 "first, then re-run this menu — otherwise texts fall back to the default font (no Hangul).");

            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3840, 2160); // 4K Resolution
            scaler.matchWidthOrHeight = 0.5f; // Rule §6: balanced match across 16:9 ~ 21:9
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            System.Type inputSystemType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemType != null) eventSystem.AddComponent(inputSystemType);
            else eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            return canvasObj;
        }

        private static void CreateBackground(Transform parent)
        {
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(parent, false);
            bgObj.transform.SetAsFirstSibling();
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = Color.white;
            bgImage.raycastTarget = false;
            // Deep navy → near-black vertical gradient instead of a flat fill
            AddGradient(bgObj, new Color(0.10f, 0.13f, 0.24f), new Color(0.02f, 0.02f, 0.05f));
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
        }

        private static TMP_Text CreateHUDText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size, string defaultText, int fontSize)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.text = defaultText;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false; // Rule §6: non-interactive UI has no raycast
            // Auto-size down when a stretched rect gets tight on narrow aspects
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = Mathf.Max(18f, fontSize * 0.4f);
            ApplyKoreanFont(text);

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            return text;
        }

        private static Button CreateMenuButton(string name, string text, Vector2 anchoredPos, Transform parent, UnityAction onClickAction)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(600, 150);

            Image bgImage = btnObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.6f, 1f, 1f);

            Button button = btnObj.AddComponent<Button>();
            UnityEventTools.AddPersistentListener(button.onClick, onClickAction);
            StyleButtonVisual(button);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI uiText = textObj.AddComponent<TextMeshProUGUI>();
            uiText.text = text;
            uiText.fontSize = 60;
            uiText.fontStyle = FontStyles.Bold;
            uiText.alignment = TextAlignmentOptions.Center;
            uiText.color = Color.white;
            uiText.raycastTarget = false; // the Button's Image handles the raycast
            uiText.enableAutoSizing = true;
            uiText.fontSizeMax = 60;
            uiText.fontSizeMin = 24;
            ApplyKoreanFont(uiText);

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return button;
        }
    }
}
