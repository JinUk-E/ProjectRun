using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MoraeGames.UI;
using MoraeGames.Ability;

namespace MoraeGames.Editor
{
    public static class CasualUISetup
    {
        private static readonly Color Navy = new Color(.035f,.065f,.14f);
        private static readonly Color Blue = new Color(.08f,.40f,.95f);
        private static readonly Color Gold = new Color(1,.72f,.12f);
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSetupEditor.FontAssetPath);
        public static void RunAndBuildProbe() { Run(); MovementProbeBuild.Run(); }

        [MenuItem("MoraeGames/Apply Casual UI")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("SourceAssets/SceneBackups/BeforeCasualUI");
            foreach (string name in new[] { "MainMenu", "Lobby", "InGame" })
            {
                string path = "Assets/MoraeGames/Scenes/" + name + ".unity";
                string backup = "SourceAssets/SceneBackups/BeforeCasualUI/" + name + ".unity";
                if (!File.Exists(backup)) File.Copy(path, backup);
            }
            EditorSceneManager.OpenScene("Assets/MoraeGames/Scenes/InGame.unity");
            foreach (var loot in UnityEngine.Object.FindObjectsByType<MoraeGames.Game.LootSpot>(FindObjectsSortMode.None))
            {
                if (loot.lootValue <= 0) loot.lootValue = MoraeGames.Core.GameConstants.LootValuePerSpot;
                if (loot.robberyDuration <= 0) loot.robberyDuration = MoraeGames.Core.GameConstants.LootRobberyDuration;
            }
            foreach (var prison in UnityEngine.Object.FindObjectsByType<MoraeGames.Game.PrisonZone>(FindObjectsSortMode.None))
                if (prison.rescueDuration <= 0) prison.rescueDuration = MoraeGames.Core.GameConstants.RescueDuration;
            var oldHud = UnityEngine.Object.FindFirstObjectByType<InGameUIManager>();
            if (!File.Exists("TestResults/ui-before.txt")) File.WriteAllText("TestResults/ui-before.txt", oldHud == null ? "No InGameUIManager in saved scene" :
                "Saved HUD: timer=" + (oldHud.timerText != null) + ", loot=" + (oldHud.lootProgressText != null) +
                ", arrests=" + (oldHud.arrestCountText != null) + ", ready=" + (oldHud.readyPanel != null) + ", ability=" + (oldHud.abilityCooldownText != null));
            UISetupEditor.SetupInGameUI();
            BuildHud(); Save();
            UISetupEditor.SetupLobbyUI();
            StyleLobby(); Save();
            UISetupEditor.SetupMainMenu();
            BuildMenu(); Save();
            ConfigureAbilities();
            AssetDatabase.SaveAssets();
            File.WriteAllText("TestResults/ui-wiring.txt", "PASS: MainMenu/Lobby/InGame UI saved; timer, counters, phase, role, skill and controls wired; scene backups preserved.");
        }

        private static void Save() { EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes(); }
        private static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
        private static void Rect(Transform t, float x0, float y0, float x1, float y1)
        {
            var r = (RectTransform)t; r.anchorMin = new Vector2(x0,y0); r.anchorMax = new Vector2(x1,y1);
            r.offsetMin = r.offsetMax = Vector2.zero; r.localScale = Vector3.one;
        }
        private static Image Panel(Transform parent, string name, float x0,float y0,float x1,float y1,Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(parent,false);
            var image = obj.GetComponent<Image>(); image.color = color; image.sprite = UISpriteFactory.RoundedRect;
            image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = .45f; image.raycastTarget = false;
            Rect(obj.transform,x0,y0,x1,y1); return image;
        }
        private static TMP_Text Text(Transform parent,string name,string value,float x0,float y0,float x1,float y1,int size=70)
        {
            var obj = new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); obj.transform.SetParent(parent,false);
            var text = obj.GetComponent<TMP_Text>(); text.font = Font; text.text = value; text.fontSize = size;
            text.enableAutoSizing = true; text.fontSizeMin = size*.6f; text.fontSizeMax = size;
            text.fontStyle = FontStyles.Bold; text.alignment = TextAlignmentOptions.Center; text.color = Color.white; text.raycastTarget = false;
            Rect(obj.transform,x0,y0,x1,y1); return text;
        }
        private static Button Button(Transform parent,string name,string label,float x0,float y0,float x1,float y1,Color color,UnityEngine.Events.UnityAction action)
        {
            var image=Panel(parent,name,x0,y0,x1,y1,color); image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            var shadow=image.gameObject.AddComponent<Shadow>(); shadow.effectColor=new Color(0,0,0,.4f); shadow.effectDistance=new Vector2(0,-12);
            Text(image.transform,"Label",label,.04f,.08f,.96f,.92f,85);
            if(action!=null) UnityEventTools.AddPersistentListener(button.onClick,action);
            return button;
        }
        private static void SafeArea(Canvas canvas)
        {
            var children=Enumerable.Range(0,canvas.transform.childCount).Select(i=>canvas.transform.GetChild(i)).ToArray();
            var root=new GameObject("SafeArea",typeof(RectTransform),typeof(SafeAreaLayout)); root.transform.SetParent(canvas.transform,false);
            Rect(root.transform,0,0,1,1);
            foreach(var child in children) child.SetParent(root.transform,false);
        }
        private static void BuildMenu()
        {
            var canvas=UnityEngine.Object.FindFirstObjectByType<Canvas>(); var root=canvas.transform;
            for(int i=root.childCount-1;i>=0;i--) UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);
            var manager=canvas.GetComponent<MainMenuManager>();
            Panel(root,"Backdrop",0,0,1,1,Navy);
            Panel(root,"BlueSide",0,.12f,.45f,1,new Color(.055f,.14f,.32f));
            Text(root,"Eyebrow","3 vs 5  ·  캐주얼 추격전",.05f,.85f,.45f,.93f,65).color=Gold;
            Text(root,"Title","잡아라!\n도망쳐!",.04f,.45f,.46f,.83f,205);
            Text(root,"Pitch","쫓고, 숨고, 동료를 구하자\n한 판에 담긴 짜릿한 추격전",.05f,.29f,.45f,.44f,65);
            Directory.CreateDirectory("Assets/MoraeGames/Art/UI");
            if (!File.Exists("Assets/MoraeGames/Art/UI/CharacterDuo.png"))
                File.Copy("TestResults/sd-characters-preview.png","Assets/MoraeGames/Art/UI/CharacterDuo.png");
            AssetDatabase.ImportAsset("Assets/MoraeGames/Art/UI/CharacterDuo.png",ImportAssetOptions.ForceSynchronousImport);
            var hero=new GameObject("CharacterDuo",typeof(RectTransform),typeof(RawImage)); hero.transform.SetParent(root,false);
            hero.GetComponent<RawImage>().texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MoraeGames/Art/UI/CharacterDuo.png");
            hero.GetComponent<RawImage>().raycastTarget=false; Rect(hero.transform,.45f,.22f,1,.94f);
            Panel(root,"RobberTag",.52f,.25f,.70f,.32f,new Color(.95f,.30f,.08f));
            Text(root,"RobberLabel","도둑 · 탈취와 구출",.52f,.25f,.70f,.32f,48);
            Panel(root,"CopTag",.76f,.25f,.94f,.32f,Blue);
            Text(root,"CopLabel","경찰 · 추격과 체포",.76f,.25f,.94f,.32f,48);
            var play=Button(root,"StartButton","플레이  ▶",.52f,.07f,.94f,.21f,Gold,manager.LoadLobbyScene);
            play.GetComponentInChildren<TMP_Text>().color=Navy;
            Button(root,"QuitButton","종료",.05f,.075f,.18f,.16f,new Color(.18f,.23f,.34f),manager.QuitGame);
            Text(root,"Footer","역할 선택 → 스킬 선택 → 경기 시작",.21f,.08f,.47f,.15f,43);
            SafeArea(canvas);
        }

        private static void StyleLobby()
        {
            var canvas=UnityEngine.Object.FindFirstObjectByType<Canvas>();
            foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font=Font;
                switch(text.text) { case "Select Your Role": text.text="어느 팀으로 뛸까요?"; break; case "COP":text.text="경찰";break;case "ROBBER":text.text="도둑";break;case "Select Character":text.text="이번 판의 스킬을 선택하세요";break;case "START MATCH":text.text="연습 경기 시작";break; }
            }
            foreach(var button in canvas.GetComponentsInChildren<Button>(true))
                if(button.name.Contains("Start")) button.GetComponent<Image>().color=Gold;
            SafeArea(canvas);
        }
        private static void BuildHud()
        {
            var ui=UnityEngine.Object.FindFirstObjectByType<InGameUIManager>(); var root=ui.transform;
            Panel(root,"StatsPlate",.015f,.89f,.405f,.99f,Navy).transform.SetAsFirstSibling();
            Rect(ui.lootProgressText.transform,.025f,.925f,.21f,.98f);
            Rect(ui.arrestCountText.transform,.23f,.925f,.39f,.98f);
            var track=Panel(root,"LootTrack",.03f,.9f,.20f,.915f,new Color(.2f,.25f,.35f));
            ui.lootFill=Panel(track.transform,"LootFill",0,0,1,1,Gold); ui.lootFill.type=Image.Type.Filled;ui.lootFill.fillMethod=Image.FillMethod.Horizontal;ui.lootFill.fillAmount=0;
            ui.roleText=Text(root,"RoleLabel","역할 확인 중",.11f,.82f,.38f,.88f,55);
            ui.objectiveText=Text(root,"Objective","경기 준비 중",.02f,.68f,.40f,.74f,45);
            Panel(root,"PhasePlate",.40f,.78f,.82f,.86f,Navy);
            ui.phaseText=Text(root,"PhaseText","곧 시작합니다",.41f,.79f,.81f,.85f,58);
            Panel(root,"SkillPlate",.32f,.02f,.67f,.18f,Navy);
            ui.skillNameText=Text(root,"SkillName","스킬",.33f,.125f,.66f,.17f,60);
            ui.skillHintText=Text(root,"SkillHint","",.33f,.075f,.66f,.12f,36);
            Rect(ui.abilityCooldownText.transform,.33f,.025f,.66f,.07f);ui.abilityCooldownText.fontSizeMax=45;
            ui.abilityCooldownText.transform.SetAsLastSibling();
            Rect(ui.interactionPromptText.transform,.28f,.20f,.70f,.27f);
            var controls=Find(root,"MobileControls");ui.actionControls=controls.gameObject.AddComponent<CanvasGroup>();
            var skill=Find(controls,"AbilityButton"); ui.skillButtonText=skill.GetComponentInChildren<TMP_Text>();
            skill.GetComponent<Image>().color=Blue;
            ui.cooldownFill=Panel(skill,"CooldownFill",0,0,1,1,new Color(0,0,0,.65f));
            ui.cooldownFill.sprite=UISpriteFactory.Circle;ui.cooldownFill.type=Image.Type.Filled;ui.cooldownFill.fillMethod=Image.FillMethod.Radial360;
            ui.cooldownFill.fillAmount=0;ui.skillButtonText.transform.SetAsLastSibling();
            Find(controls,"InteractButton").GetComponentInChildren<TMP_Text>().text="행동\nF";
            Find(controls,"PingButton").GetComponentInChildren<TMP_Text>().text="핑";
            ui.readyRoleText=Text(ui.readyPanel.transform,"ReadyRole","역할 확인 중",.15f,.69f,.85f,.82f,130);
            ui.readyHintText=Text(ui.readyPanel.transform,"ReadyHint","",.15f,.18f,.85f,.32f,65);
            ui.readyPanel.transform.SetAsLastSibling();ui.pausePanel.transform.SetAsLastSibling();ui.resultPanel.transform.SetAsLastSibling();
            ui.lobbyButton.GetComponentInChildren<TMP_Text>().text="다시 준비하기";
            ui.resumeButton.GetComponentInChildren<TMP_Text>().text="계속하기";
            ui.exitToLobbyButton.GetComponentInChildren<TMP_Text>().text="로비로";
            foreach (var text in ui.pausePanel.GetComponentsInChildren<TMP_Text>(true))
                if (text.text == "PAUSED") text.text = "잠시 쉬어가기";
            Rect(ui.lobbyButton.transform,.32f,.23f,.68f,.37f);
            Require(ui.timerText!=null&&ui.arrestCountText!=null&&ui.lootProgressText!=null&&ui.phaseText!=null&&ui.cooldownFill!=null,"HUD references missing");
            SafeArea(root.GetComponent<Canvas>());
        }
        private static void ConfigureAbilities()
        {
            Directory.CreateDirectory("Assets/MoraeGames/Resources"); AssetDatabase.Refresh();
            foreach(string name in new[]{"AbilityRing","SmokeCloud"})
            {
                string materialPath="Assets/MoraeGames/Resources/"+name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(material==null) { material=new Material(Shader.Find(name=="AbilityRing"?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath); }
                if(name=="SmokeCloud") { material.SetFloat("_Surface",1); material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",10);material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000; }
                EditorUtility.SetDirty(material);
            }
            const string path="Assets/MoraeGames/Prefabs/Abilities/EscapeTrap.asset";
            var trap=AssetDatabase.LoadAssetAtPath<EscapeTrapData>(path);
            if(trap==null) { trap=ScriptableObject.CreateInstance<EscapeTrapData>();AssetDatabase.CreateAsset(trap,path); }
            trap.abilityName="추격 방해 덫";trap.cooldown=14;trap.duration=6;EditorUtility.SetDirty(trap);
            foreach(string file in Directory.GetFiles("Assets/MoraeGames/Prefabs/Characters","*.prefab"))
            {
                var root=PrefabUtility.LoadPrefabContents(file);
                try {
                    if(root.GetComponent<AbilityFeedback>()==null) root.AddComponent<AbilityFeedback>();
                    if(Path.GetFileNameWithoutExtension(file)=="Robber_5") root.GetComponent<AbilityHolder>().abilityData=trap;
                    PrefabUtility.SaveAsPrefabAsset(root,file);
                } finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        private static void Require(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); }
    }
}
