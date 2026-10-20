using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;
using MoraeGames.UI;

namespace MoraeGames.Editor
{
    public static class ArcadeUISetup
    {
        private static readonly Color Ink = new Color(.025f,.055f,.16f), Blue = new Color(.04f,.38f,.95f), Gold = new Color(1,.73f,.04f);
        public static void RunAndBuild() { Run(); MovementProbeBuild.Run(); }
        public static void FinalBuild() { RunAndBuild(); ReleaseReadinessRepair.BuildVerificationPlayer(); }
        [MenuItem("MoraeGames/Apply Arcade UI")]
        public static void Run()
        {
            CasualUISetup.Run();
            foreach(string scene in new[]{"MainMenu","Lobby","InGame"})
            {
                EditorSceneManager.OpenScene("Assets/MoraeGames/Scenes/"+scene+".unity");
                var canvas=Object.FindFirstObjectByType<Canvas>();var root=canvas.transform.Find("SafeArea");
                if(scene=="Lobby") Lobby(root); else if(scene=="InGame") Hud(root); else Menu(root);
                foreach(var button in canvas.GetComponentsInChildren<Button>(true)) Style(button.gameObject);
                foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true)) { text.fontStyle=FontStyles.Bold; text.raycastTarget=false; }
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();
            }
            AssetDatabase.SaveAssets();
        }
        private static Transform F(Transform r,string name)=>r.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        private static void R(Transform t,float x,float y,float xx,float yy){var r=(RectTransform)t;r.anchorMin=new Vector2(x,y);r.anchorMax=new Vector2(xx,yy);r.offsetMin=r.offsetMax=Vector2.zero;}
        private static Image P(Transform root,string name,float x,float y,float xx,float yy,Color color)
        {var g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(root,false);var image=g.GetComponent<Image>();image.sprite=UISpriteFactory.RoundedRect;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=false;R(g.transform,x,y,xx,yy);return image;}
        private static TMP_Text T(Transform root,string name,string value,float x,float y,float xx,float yy,int size)
        {var g=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));g.transform.SetParent(root,false);var t=g.GetComponent<TMP_Text>();t.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontSetupEditor.FontAssetPath);t.text=value;t.fontSize=size;t.enableAutoSizing=true;t.fontSizeMax=size;t.fontSizeMin=size*.6f;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;R(g.transform,x,y,xx,yy);return t;}
        private static ArcadeIcon Icon(Transform root,int symbol,float x,float y,float xx,float yy,Color color)
        {var g=new GameObject("Icon",typeof(RectTransform),typeof(ArcadeIcon));g.transform.SetParent(root,false);var icon=g.GetComponent<ArcadeIcon>();icon.symbol=symbol;icon.color=color;icon.raycastTarget=false;R(g.transform,x,y,xx,yy);return icon;}
        private static void Style(GameObject g)
        {
            var image=g.GetComponent<Image>();if(image!=null){var shadow=g.GetComponents<Shadow>().FirstOrDefault(s=>!(s is Outline))??g.AddComponent<Shadow>();shadow.effectColor=Ink;shadow.effectDistance=new Vector2(0,-14);var outline=g.GetComponent<Outline>()??g.AddComponent<Outline>();outline.effectColor=Ink;outline.effectDistance=new Vector2(5,-5);}
            if(g.GetComponent<ArcadeButtonFeedback>()==null)g.AddComponent<ArcadeButtonFeedback>();
        }
        private static void Background(Transform root)
        {
            var bg=P(root,"ArcadeBackdrop",0,0,1,1,new Color(.035f,.22f,.6f));bg.transform.SetAsFirstSibling();
            for(int i=0;i<8;i++){var stripe=P(bg.transform,"Stripe",i*.17f-.25f,-.4f,i*.17f-.15f,1.4f,new Color(.04f,.32f,.78f));stripe.transform.localRotation=Quaternion.Euler(0,0,-18);}
        }
        private static LobbyHeroView Hero(Transform root,float x,float y,float xx,float yy)
        {
            var frame=new GameObject("HeroPreviewFrame",typeof(RectTransform));frame.transform.SetParent(root,false);R(frame.transform,x,y,xx,yy);
            var g=new GameObject("HeroPreview",typeof(RectTransform),typeof(RawImage),typeof(LobbyHeroView),typeof(AspectRatioFitter));g.transform.SetParent(frame.transform,false);R(g.transform,0,0,1,1);
            var fit=g.GetComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=.8f;
            var view=g.GetComponent<LobbyHeroView>();view.image=g.GetComponent<RawImage>();view.image.raycastTarget=false;
            view.copBody=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MoraeGames/Prefabs/Characters/Cop_1.prefab").transform.Find("Body").gameObject;
            view.robberBody=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MoraeGames/Prefabs/Characters/Robber_1.prefab").transform.Find("Body").gameObject;
            return view;
        }
        private static void Menu(Transform root)
        {
            F(root,"Backdrop").gameObject.SetActive(false);F(root,"BlueSide").gameObject.SetActive(false);F(root,"CharacterDuo").gameObject.SetActive(false);
            Background(root); Hero(root,.48f,.23f,.96f,.96f);
            F(root,"Title").GetComponent<TMP_Text>().color=Gold;
            F(root,"RobberTag").gameObject.SetActive(false);F(root,"RobberLabel").gameObject.SetActive(false);F(root,"CopTag").gameObject.SetActive(false);F(root,"CopLabel").gameObject.SetActive(false);
            T(root,"HeroBadge","추격 준비 완료!",.57f,.24f,.89f,.31f,65);
        }
        private static void Lobby(Transform root)
        {
            var ui=Object.FindFirstObjectByType<LobbyManager>();F(root,"Background").gameObject.SetActive(false);Background(root);
            var title=F(root,"TitleText").GetComponent<TMP_Text>();title.text="출동 준비";title.fontSize=110;R(title.transform,.025f,.86f,.25f,.98f);
            R(ui.roleText.transform,.27f,.83f,.70f,.90f);ui.roleText.fontSize=58;
            R(F(root,"CopButton"),.025f,.65f,.235f,.79f);R(F(root,"RobberButton"),.025f,.48f,.235f,.62f);
            T(root,"TeamCaption","01  팀 선택",.025f,.79f,.235f,.85f,45);
            ui.heroView=Hero(root,.27f,.25f,.69f,.79f);
            R(ui.selectedCharacterText.transform,.30f,.75f,.67f,.82f);ui.selectedCharacterText.fontSize=92;ui.selectedCharacterText.transform.SetAsLastSibling();
            R(ui.characterSelectPanel.transform,.72f,.22f,.975f,.85f);ui.characterSelectPanel.GetComponent<Image>().color=Ink;
            F(ui.characterSelectPanel.transform,"CharSelectTitle").GetComponent<TMP_Text>().text="02  스킬 선택";R(F(ui.characterSelectPanel.transform,"CharSelectTitle"),.05f,.89f,.95f,.99f);
            ui.abilityIcons=new ArcadeIcon[5];
            for(int i=0;i<5;i++)
            {
                var button=ui.characterButtons[i];float y=.70f-i*.165f;R(button.transform,.06f,y,.94f,y+.145f);
                R(ui.characterNameTexts[i].transform,.27f,.12f,.97f,.9f);ui.characterNameTexts[i].fontSize=54;
                ui.characterAbilityTexts[i].gameObject.SetActive(false);ui.abilityIcons[i]=Icon(button.transform,i,.035f,.18f,.25f,.82f,Color.white);
            }
            var detail=P(root,"SelectedSkillPlate",.29f,.20f,.69f,.31f,Ink);ui.selectedSkillDetail=T(detail.transform,"SelectedSkillDetail","",.04f,.05f,.96f,.95f,57);
            R(ui.startMatchButton.transform,.72f,.055f,.975f,.18f);ui.startMatchButton.GetComponent<Image>().color=Gold;ui.startMatchButton.GetComponentInChildren<TMP_Text>().text="연습 경기 시작  ▶";
            ui.startMatchButton.GetComponentInChildren<TMP_Text>().color=Ink;
            R(ui.joinCodeInput.transform,.025f,.30f,.235f,.37f);((TMP_Text)ui.joinCodeInput.placeholder).text="참가 코드";
            R(F(root,"HostMatchButton"),.025f,.20f,.125f,.28f);F(root,"HostMatchButton").GetComponentInChildren<TMP_Text>().text="방 만들기";
            R(F(root,"JoinMatchButton"),.14f,.20f,.235f,.28f);F(root,"JoinMatchButton").GetComponentInChildren<TMP_Text>().text="참가";
            R(ui.networkStatusText.transform,.025f,.10f,.68f,.18f);ui.networkStatusText.fontSize=39;
            R(ui.startNetworkMatchButton.transform,.025f,.015f,.235f,.09f);ui.startNetworkMatchButton.GetComponentInChildren<TMP_Text>().text="온라인 시작";
            T(root,"ModeCaption","연습: AI와 대결   /   온라인: 방 만들기·참가",.29f,.055f,.69f,.12f,40);
            var growth=F(root,"SkillPanel");ui.growthPanel=growth.gameObject;growth.GetComponent<Image>().color=Ink;R(growth,.26f,.18f,.70f,.82f);growth.SetAsLastSibling();growth.gameObject.SetActive(false);
            var grow=P(root,"GrowthButton",.80f,.89f,.975f,.96f,new Color(.45f,.16f,.85f));grow.raycastTarget=true;var b=grow.gameObject.AddComponent<Button>();UnityEventTools.AddPersistentListener(b.onClick,ui.ToggleGrowth);T(grow.transform,"Label","연습 성장",0,0,1,1,52);
        }
        private static void Hud(Transform root)
        {
            var ui=Object.FindFirstObjectByType<InGameUIManager>();ui.compactHud=true;
            foreach(string name in new[]{"SkillPlate","SkillHint","Objective","RoleLabel"})F(root,name).gameObject.SetActive(false);
            ui.abilityCooldownText.gameObject.SetActive(false);
            R(F(root,"StatsPlate"),.02f,.895f,.30f,.98f);R(ui.lootProgressText.transform,.065f,.91f,.15f,.965f);R(ui.arrestCountText.transform,.205f,.91f,.285f,.965f);
            Icon(root,8,.028f,.918f,.063f,.965f,Gold);Icon(root,7,.165f,.918f,.20f,.965f,new Color(.3f,.8f,1));
            R(F(root,"LootTrack"),.035f,.90f,.145f,.908f);
            R(F(root,"PhasePlate"),.33f,.82f,.69f,.88f);R(ui.phaseText.transform,.34f,.825f,.68f,.875f);ui.phaseText.fontSizeMax=48;
            R(ui.skillNameText.transform,.78f,.035f,.97f,.08f);ui.skillNameText.fontSizeMax=48;
            var skill=F(root,"AbilityButton");skill.GetComponent<Image>().color=Gold;Style(skill.gameObject);ui.skillFeedback=skill.GetComponent<ArcadeButtonFeedback>();
            ui.skillIcon=Icon(skill,0,.22f,.32f,.78f,.88f,Color.white);R(ui.skillButtonText.transform,.05f,.03f,.95f,.29f);ui.skillButtonText.fontSizeMax=50;
            var interact=F(root,"InteractButton");interact.GetComponent<Image>().color=new Color(.1f,.6f,1);Style(interact.gameObject);Icon(interact,7,.25f,.37f,.75f,.9f,Color.white);R(interact.GetComponentInChildren<TMP_Text>().transform,.05f,.03f,.95f,.32f);interact.GetComponentInChildren<TMP_Text>().text="행동 F";
            var ping=F(root,"PingButton");Style(ping.gameObject);ping.GetComponent<Image>().color=new Color(.25f,.65f,.35f);Icon(ping,9,.23f,.3f,.77f,.90f,Color.white);R(ping.GetComponentInChildren<TMP_Text>().transform,.05f,.03f,.95f,.3f);
            var joystick=F(root,"VirtualJoystick").GetComponent<Image>();joystick.color=new Color(.15f,.5f,1,.3f);
            F(root,"Handle").GetComponent<Image>().color=new Color(.6f,.85f,1,.75f);
        }
    }
}
