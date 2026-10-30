#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using MoraeGames.Game;
using MoraeGames.Player;
using MoraeGames.Ability;
using MoraeGames.Network;

namespace MoraeGames.UI
{
    /// <summary>Opt-in local UI/skill integration test; excluded from release. Never awards progression.</summary>
    public sealed class UIFlowProbe : MonoBehaviour
    {
        private int _stage;
        private float _next, _started;
        private InGameUIManager _hud;
        private InGameManager _game;
        private RobberController _player;
        private CopController _cop;
        private float _copSpeed;
        private string _timer;
        private bool _readySeen, _done;
        private bool _vfxMode;
        private int _vfxIndex, _vfxStep;
        private AbilityHolder _vfxHolder;
        private SkillVfx _lastVfx;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains("--ui-flow") && !Environment.GetCommandLineArgs().Contains("--skill-vfx")) return;
            Application.runInBackground = true;
            var bootstrap=FindFirstObjectByType<ServerBootstrap>();if(bootstrap!=null)bootstrap.enabled=false;
            var obj=new GameObject("UIFlowProbe");DontDestroyOnLoad(obj);obj.AddComponent<UIFlowProbe>()._vfxMode=Environment.GetCommandLineArgs().Contains("--skill-vfx");
        }
        private void Start() { _started=Time.unscaledTime;_next=_started+1.5f;Application.logMessageReceived+=Log; }
        private void OnDestroy()=>Application.logMessageReceived-=Log;
        private void Log(string message,string trace,LogType type) { if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert) Finish(false,message); }
        private void Shot(string name)
        {
            // Explicit render also works for a hidden player whose swap chain is not presented.
            var camera = Camera.main;
            foreach (var auxiliary in Camera.allCameras)
                if (auxiliary.targetTexture != null && auxiliary != camera) auxiliary.Render();
            bool temporary = camera == null;
            if (temporary) camera = new GameObject("PreviewCamera", typeof(Camera)).GetComponent<Camera>();
            Vector3 oldPosition=camera.transform.position;
            Quaternion oldRotation=camera.transform.rotation;
            if(name=="heist-alarm")
            {
                var focus=HeistBuilding.NearestAlarm(_player.transform.position).transform.position;
                camera.transform.position=focus+new Vector3(0,26,-18);camera.transform.LookAt(focus);
            }
            if(name.StartsWith("skill-"))
            {
                var focus=_player.transform.position;
                camera.transform.position=focus+new Vector3(0,18,-14);camera.transform.LookAt(focus);
            }
            if(name.StartsWith("prison-"))
            {
                var focus=FindFirstObjectByType<PrisonZone>().transform.position;
                camera.transform.position=focus+new Vector3(0,18,-17);camera.transform.LookAt(focus);
            }
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            foreach (var canvas in canvases) canvas.enabled = false;
            var target = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var worldTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
            worldTexture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0); worldTexture.Apply();
            var worldPixels = worldTexture.GetPixels32();
            Destroy(worldTexture);
            // Overlay HUD is composed AFTER world fog in the real player. Reproduce that
            // order in hidden-player captures instead of putting the HUD behind the mask.
            var overlay = new GameObject("PreviewOverlay", typeof(Camera)).GetComponent<Camera>();
            overlay.CopyFrom(camera); overlay.enabled = false;
            overlay.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
            overlay.clearFlags = CameraClearFlags.SolidColor; overlay.backgroundColor=Color.clear; overlay.cullingMask = 1 << 31;
            var uiObjects = canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>(true)).Distinct().ToArray();
            var layers = uiObjects.Select(t=>t.gameObject.layer).ToArray();
            foreach(var ui in uiObjects) ui.gameObject.layer=31;
            foreach(var canvas in canvases)
            {
                canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=overlay;
                canvas.planeDistance=overlay.nearClipPlane+1; canvas.enabled=true;
            }
            Canvas.ForceUpdateCanvases(); overlay.Render();
            for(int i=0;i<uiObjects.Length;i++) uiObjects[i].gameObject.layer=layers[i];
            Destroy(overlay.gameObject);
            RenderTexture.active = target;
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply();
            var uiPixels=texture.GetPixels32();
            for(int i=0;i<uiPixels.Length;i++)
            {
                float alpha=uiPixels[i].a/255f;
                // Render-target UI is already premultiplied by its blend operation.
                worldPixels[i]=new Color32((byte)Mathf.Min(255,uiPixels[i].r+worldPixels[i].r*(1-alpha)),
                    (byte)Mathf.Min(255,uiPixels[i].g+worldPixels[i].g*(1-alpha)),
                    (byte)Mathf.Min(255,uiPixels[i].b+worldPixels[i].b*(1-alpha)),255);
            }
            texture.SetPixels32(worldPixels);texture.Apply();
            File.WriteAllBytes(Path.GetFullPath("TestResults/ui-"+name+"-"+Screen.width+".png"), texture.EncodeToPNG());
            Destroy(texture);
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
            RenderTexture.ReleaseTemporary(target);
            camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
            if (temporary) Destroy(camera.gameObject);
        }
        private static void Check(bool value,string why) { if(!value) throw new InvalidOperationException(why); }
        private void Advance(float delay=.7f) { _stage++;_next=Time.unscaledTime+delay; }
        private void Update()
        {
            if(_done)return;
            if(Time.unscaledTime-_started>90) { Finish(false,"Timeout stage="+_stage);return; }
            if(Time.unscaledTime<_next)return;
            try
            {
                if(_vfxMode && _stage>=6) { RunVfxChecks(); return; }
                switch(_stage)
                {
                    case 0: Check(FindFirstObjectByType<MainMenuManager>()!=null,"Menu missing"); Shot("main");Advance();break;
                    case 1: FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="StartButton").onClick.Invoke();Advance(1);break;
                    case 2:
                        var lobby=FindFirstObjectByType<LobbyManager>();Check(lobby!=null,"Menu button did not load Lobby");
                        var lan=lobby.GetComponent<LanRoomPanel>();Check(lan!=null && lan.IsLan,"LAN entry missing");
                        foreach(var bad in new[]{"","localhost","999.1.2.3","0.0.0.0","224.0.0.1","192.168.0.1:0","192.168.0.1:65536"})
                            Check(!LanAddress.TryParse(bad,out _,out _),"Invalid LAN endpoint accepted: "+bad);
                        Check(LanAddress.TryParse("192.168.0.10:7777",out _,out var port)&&port==7777,"Valid LAN endpoint rejected");
                        lobby.joinCodeInput.text="bad-ip";
                        Check(!FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="JoinMatchButton").interactable,"Invalid endpoint join enabled");
                        lan.ToggleMode();Check(!lan.IsLan,"Relay mode unavailable");
                        lan.ToggleMode();Check(lan.IsLan,"LAN switch failed");lobby.joinCodeInput.text="";
                        Check(RoleManager.Instance.GetRole("Player1")==RoleManager.PlayerRole.Robber,"Default role must be robber");
                        lobby.SelectCopRole();
                        Check(RoleManager.Instance.GetRole("Player1")==RoleManager.PlayerRole.Robber,"Legacy police selection bypassed robber mode");
                        Check(!FindObjectsByType<Button>(FindObjectsSortMode.None).Any(b=>b.name=="CopButton"),"Police selection is still visible");
                        if (lobby.heroView != null) { Check(lobby.heroView.image.texture != null,"Lobby hero render missing"); Shot("lobby-robber"); }
                        lobby.SelectRobberRole();lobby.SelectCharacter(4);
                        if (lobby.heroView != null) lobby.startMatchButton.GetComponent<ArcadeButtonFeedback>().OnPointerDown(null);
                        Advance();break;
                    case 3:
                        var selectedLobby=FindFirstObjectByType<LobbyManager>();
                        if (selectedLobby.heroView != null)
                        {
                            Check(selectedLobby.startMatchButton.transform.localScale.x < .96f,"Button press feedback missing");
                            selectedLobby.startMatchButton.GetComponent<ArcadeButtonFeedback>().OnPointerUp(null);
                            Check(selectedLobby.abilityIcons[4].symbol==6 && selectedLobby.selectedSkillDetail.text.Contains("덫"),"Selected skill presentation mismatch");
                            selectedLobby.ToggleGrowth();Check(selectedLobby.growthPanel.activeSelf,"Growth panel did not open");Shot("growth");
                            selectedLobby.ToggleGrowth();Check(!selectedLobby.growthPanel.activeSelf,"Growth panel did not close");
                        }
                        Shot("lobby");Advance();break;
                    case 4: FindFirstObjectByType<LobbyManager>().startMatchButton.onClick.Invoke();Advance(.3f);break;
                    case 5:
                        if(SceneManager.GetActiveScene().name!="InGame")return;
                        _hud=FindFirstObjectByType<InGameUIManager>();_game=FindFirstObjectByType<InGameManager>();
                        if(_game.IsReadyPhase) { if(!_readySeen) { Check(_hud.readyPanel.activeSelf,"Ready overlay missing");Shot("ready");_readySeen=true; } return; }
                        Check(_readySeen,"Countdown never displayed");
                        _player=PlayerController.Instances.OfType<RobberController>().First(p=>p.IsLocallyControlled);
                        Check(!PlayerController.Instances.OfType<CopController>().Any(p=>p.IsLocallyControlled),"A human police character was spawned");
                        var vision=FindObjectsByType<FogOfWarCamera>(FindObjectsSortMode.None);
                        Check(vision.Length==1,"Gameplay vision mask missing");
                        Check(FindFirstObjectByType<MinimapController>().minimapImage.material.shader.name=="MoraeGames/MinimapVisionFog","Minimap vision mask missing");
                        Check(vision.All(f=>f.clearRadius==10 && f.hiddenRadius==14),"Vision ranges differ between cameras");
                        foreach(var ai in FindObjectsByType<AIController>(FindObjectsSortMode.None))ai.enabled=false;
                        foreach(var agent in FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None))if(agent.enabled&&agent.isOnNavMesh)agent.isStopped=true;
                        _timer=_hud.timerText.text; Advance(2);break;
                    case 6:
                        Check(LocalVisionSubject.Instances.Any(s=>s.Hidden),"Distant characters not hidden by fog");
                        Check(!_player.GetComponent<LocalVisionSubject>().Hidden,"Local player hidden");
                        foreach(var landmark in HeistBuilding.Instances)
                            Check(!landmark.loot.GetComponent<Renderer>().forceRenderingOff,"Static loot landmark hidden");
                        Check(_hud.timerText.text!=_timer,"Timer UI did not update");
                        Check(_hud.roleText.text.Contains("도둑")&&_hud.DisplayedPhase.Contains("도주"),"Role/phase missing");
                        CheckPursuitGuidance();
                        var ability=_player.GetComponent<AbilityHolder>();Check(ability.abilityData is EscapeTrapData,"New trap was not selected");Check(ability.TryActivate(),"Skill activation failed");
                        _cop=PlayerController.Instances.OfType<CopController>().First();_copSpeed=_cop.moveSpeed;
                        _cop.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled=false;
                        _cop.transform.position=_player.transform.position+Vector3.right;
                        Advance();break;
                    case 7:
                        Check(!_cop.GetComponent<LocalVisionSubject>().Hidden,"Nearby cop did not reappear");
                        Check(_hud.cooldownFill.fillAmount>0&&_hud.skillButtonText.text.Contains("발동"),"Skill HUD did not refresh");
                        if (_hud.compactHud) Check(_hud.skillIcon.symbol==6 && !_hud.skillHintText.gameObject.activeSelf,"Compact skill presentation mismatch");
                        Check(_cop.moveSpeed<_copSpeed*.8f,"Trap did not slow police");Shot("playing");
                        _cop.transform.position+=Vector3.right*10;Advance();break;
                    case 8:
                        Check(Mathf.Abs(_cop.moveSpeed-_copSpeed)<.01f,"Trap slow did not recover after leaving");
                        var loot=FindObjectsByType<LootSpot>(FindObjectsSortMode.None).First(s=>s.building!=null && s.building.guarded);
                        _player.transform.position=loot.building.transform.position+Vector3.left*6;
                        Check(!loot.TryStartRobbery(_player.gameObject),"Theft possible outside building");
                        Check(!HeistBuilding.HasSight(loot.building.transform.position,_player.transform.position),"Wall does not block sight");
                        _player.transform.position=loot.transform.position+Vector3.back;
                        Physics.SyncTransforms();
                        Check(loot.TryStartRobbery(_player.gameObject),"Loot start failed");loot.UpdateRobbery(.9f);
                        Check(!loot.building.AlarmActive,"Alarm fired before warning delay");loot.CancelRobbery();
                        Check(loot.TryStartRobbery(_player.gameObject),"Loot restart failed");loot.UpdateRobbery(.7f);
                        Check(loot.building.AlarmActive,"Repeated tampering bypassed alarm");loot.CancelRobbery();
                        Check(loot.building.AlarmActive && loot.GetProgress()==0,"Cancel cleared alarm or retained loot progress");
                        Check(HeistBuilding.NearestAlarm(loot.transform.position)==loot.building,"Police alarm destination missing");
                        var policeAgent=_cop.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        _cop.transform.position=loot.building.transform.position+Vector3.back*8;
                        policeAgent.enabled=true;
                        Check(policeAgent.Warp(_cop.transform.position),"Police not on building approach mesh");
                        _cop.GetComponent<AICopController>().SendMessage("SearchForRobber");
                        Check(Vector3.Distance(policeAgent.destination,loot.transform.position)<2,"Police did not investigate alarm");
                        PolicePursuitChecks.Run(_cop, _player, loot.building);
                        policeAgent.enabled=false;
                        Shot("heist-alarm");
                        Check(loot.TryStartRobbery(_player.gameObject),"Loot restart after alarm failed");loot.UpdateRobbery(loot.robberyDuration+.1f);
                        _player.OnArrested();Advance();break;
                    case 9:
                        Check(_hud.DisplayedArrested>=1&&_hud.DisplayedPhase.Contains("체포됨"),"Arrest HUD did not update");
                        Check(_hud.lootFill.fillAmount>0,"Loot HUD did not update");Shot("arrested");
                        var prison=FindFirstObjectByType<PrisonZone>();var rescuer=PlayerController.Instances.OfType<RobberController>().First(p=>!p.IsArrested);
                        Shot("prison-locked");
                        rescuer.transform.position=prison.transform.position;
                        Check(!prison.TryStartRescue(rescuer.gameObject),"Rescue from holding area must fail");
                        rescuer.transform.position=prison.doorPoint.position-prison.doorPoint.forward;
                        Check(!prison.TryStartRescue(rescuer.gameObject),"Rescue through inner face must fail");
                        rescuer.transform.position=prison.InteractionPosition;
                        Check(!prison.TryStartRescue(_player.gameObject),"Prisoner must not rescue themselves");
                        Check(prison.TryStartRescue(rescuer.gameObject),"Door rescue start failed");
                        prison.UpdateRescue(prison.rescueDuration*.5f);
                        Check(prison.GetRescueProgress()>.4f,"Door progress missing");
                        rescuer.transform.position=prison.transform.position+Vector3.right*8;
                        prison.UpdateRescue(.1f);
                        Check(!prison.IsBeingRescued && !prison.DoorOpen && prison.GetRescueProgress()==0,"Leaving door did not cancel rescue");
                        rescuer.transform.position=prison.InteractionPosition;
                        Physics.SyncTransforms();
                        rescuer.ServerDriveInteraction(true,true);
                        Check(prison.IsBeingRescued,"Door trigger did not accept action input");prison.UpdateRescue(prison.rescueDuration+.1f);
                        Check(prison.DoorOpen && !prison.doorCollider.enabled,"Rescue did not unlock gate");Advance();break;
                    case 10:
                        Check(!_player.IsArrested&&_hud.DisplayedPhase.Contains("도주"),"Rescue HUD did not update");Shot("rescued");Shot("prison-open");_hud.pauseButton.onClick.Invoke();Advance();break;
                    case 11: Check(_hud.pausePanel.activeSelf,"Pause not visible");Shot("pause");_hud.resumeButton.onClick.Invoke();Advance();break;
                    case 12: _hud.ShowResult(RoleManager.PlayerRole.Robber,"UI preview");Advance();break;
                    case 13: Check(_hud.resultPanel.activeSelf,"Result missing");Shot("result");Advance(15);break;
                    case 14:
                        Check(HeistBuilding.Instances.All(b=>!b.AlarmActive),"Alarm did not expire");
                        SceneManager.LoadScene("Lobby");Advance(1);break;
                    case 15:
                        var exit=FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="LeaveRoomButton");
                        Check(exit.isActiveAndEnabled && exit.interactable,"Offline lobby exit unavailable");
                        Shot("lobby-exit");exit.onClick.Invoke();Advance(1);break;
                    case 16:
                        Check(SceneManager.GetActiveScene().name=="MainMenu" && !NetworkSessionManager.IsNetworked,"Offline exit did not return to menu");
                        FindFirstObjectByType<MainMenuManager>().LoadLobbyScene();Advance(1);break;
                    case 17: FindFirstObjectByType<LobbyManager>().HostMatch();Check(NetworkSessionManager.Mode==NetworkSessionManager.SessionMode.Host,"LAN UI host failed");Advance();break;
                    case 18:
                        Shot("lan-host");
                        string previousClipboard=GUIUtility.systemCopyBuffer;
                        FindFirstObjectByType<LobbyManager>().HostMatch();
                        bool copied=GUIUtility.systemCopyBuffer==NetworkSessionManager.CurrentLanEndpoint;
                        GUIUtility.systemCopyBuffer=previousClipboard;
                        Check(copied,"Host address copy failed");
                        FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="LeaveRoomButton").onClick.Invoke();Advance(1);break;
                    case 19:
                        Check(SceneManager.GetActiveScene().name=="MainMenu" && !NetworkSessionManager.IsNetworked && NetworkRoomState.Instance==null,"Online exit did not clear room");
                        Check(!Unity.Netcode.NetworkManager.Singleton.IsListening,"Transport still listening after exit");
                        FindFirstObjectByType<MainMenuManager>().LoadLobbyScene();Advance(1);break;
                    case 20: Check(NetworkSessionManager.StartHost(),"Host could not restart after leaving");Advance();break;
                    case 21:
                        FindFirstObjectByType<LobbyManager>().JoinMatch();Advance(1);break;
                    case 22:
                        Check(!NetworkSessionManager.IsNetworked && !Unity.Netcode.NetworkManager.Singleton.IsListening,"Leave room did not stop transport");
                        var retryLobby=FindFirstObjectByType<LobbyManager>();
                        retryLobby.joinCodeInput.text="127.0.0.1:59999";retryLobby.JoinMatch();Advance(.2f);break;
                    case 23:
                        Check(!FindFirstObjectByType<LobbyManager>().startMatchButton.interactable,"Practice enabled while connecting");
                        FindFirstObjectByType<LobbyManager>().JoinMatch();Advance(1);break;
                    case 24:
                        Check(!NetworkSessionManager.IsNetworked && !Unity.Netcode.NetworkManager.Singleton.IsListening,"Cancel left transport active");
                        var timeoutLobby=FindFirstObjectByType<LobbyManager>();
                        timeoutLobby.joinCodeInput.text="127.0.0.1:59999";timeoutLobby.JoinMatch();Advance(14);break;
                    case 25:
                        Check(!NetworkSessionManager.IsNetworked && !Unity.Netcode.NetworkManager.Singleton.IsListening,"Failed connection did not recover");
                        var recovered=FindFirstObjectByType<LobbyManager>();
                        Check(recovered.networkStatusText.text.Contains("연결"),"Connection failure explanation missing");
                        Check(recovered.startMatchButton.interactable,"Practice did not recover after failure");
                        recovered.HostMatch();Check(NetworkSessionManager.Mode==NetworkSessionManager.SessionMode.Host,"Host after failed join rejected");
                        Advance();break;
                    case 26:
                        FindFirstObjectByType<LobbyManager>().ExitToMainMenu();
                        Finish(true,"Chase/search/clear HUD and target attribution; LAN/Relay toggle, IP validation, host/address copy, cancel/timeout/retry; UI/skills, prison rescue and room exit/restart. No progression awarded.");break;
                }
            }
            catch(Exception error) { Finish(false,error.ToString()); }
        }
        private void CheckPursuitGuidance()
        {
            var cop = FindFirstObjectByType<AICopController>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var target = typeof(AICopController).GetField("_currentTarget", flags);
            var tracked = typeof(AICopController).GetField("_lastTrackedTarget", flags);
            var seen = typeof(AICopController).GetField("_lastSeenAt", flags);
            var refresh = typeof(InGameUIManager).GetMethod("UpdatePursuitGuidance", flags);
            cop.enabled = true;
            try
            {
                target.SetValue(cop, _player.transform); tracked.SetValue(cop, _player.transform); seen.SetValue(cop, Time.time);
                Check(AICopController.GetPursuitState(_player) == AICopController.PursuitState.Chasing, "Pursuit not attributed to target");
                var teammate = PlayerController.Instances.OfType<RobberController>().First(p => p != _player);
                Check(AICopController.GetPursuitState(teammate) == AICopController.PursuitState.Clear, "Teammate inherited another player's pursuit");
                refresh.Invoke(_hud, null); Check(_hud.DisplayedPhase.Contains("추격 중"), "Chase HUD missing"); Shot("pursuit-chase");
                target.SetValue(cop, null);
                refresh.Invoke(_hud, null); Check(_hud.DisplayedPhase.Contains("수색 중"), "Search HUD missing"); Shot("pursuit-search");
                seen.SetValue(cop, Time.time - cop.searchMemorySeconds - 1);
                refresh.Invoke(_hud, null); Check(_hud.DisplayedPhase.Contains("추격 해제"), "Escape HUD missing"); Shot("pursuit-clear");
            }
            finally { cop.enabled = false; }
        }

        private void RunVfxChecks()
        {
            if(_vfxHolder==null)
            {
                _vfxHolder=_player.GetComponent<AbilityHolder>();
                if(UnityEngine.AI.NavMesh.SamplePosition(Vector3.zero,out var hit,5,UnityEngine.AI.NavMesh.AllAreas)) _player.transform.position=hit.position;
            }
            if(_vfxIndex>=5) { Finish(true,"Five Kenney skill styles render particles; smoke concealment preserved; pooled reuse clears particles and restores visibility; no shader/runtime errors."); return; }
            if(_vfxStep==0)
            {
                _vfxHolder.Deactivate();
                _vfxHolder.abilityData=_game.robberPrefabs[_vfxIndex].GetComponent<AbilityHolder>().abilityData;
                typeof(AbilityHolder).GetField("cooldownTimer",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(_vfxHolder,0f);
                Check(_vfxHolder.TryActivate(),"VFX test activation rejected");
                _lastVfx=_vfxHolder.VisualEffect;
                Check(_lastVfx!=null,"Missing VFX for "+_vfxHolder.abilityData.name);
                _vfxStep=1; _next=Time.unscaledTime+.35f; return;
            }
            if(_vfxStep==1)
            {
                Check(_lastVfx.ParticleCount>0,"No visible particles for "+_lastVfx.Style);
                foreach(var renderer in _lastVfx.GetComponentsInChildren<ParticleSystemRenderer>())
                    Check(renderer.sharedMaterial!=null && renderer.sharedMaterial.shader.isSupported && renderer.sharedMaterial.mainTexture!=null,"Missing particle texture/shader");
                if(_vfxHolder.abilityData is SmokeBombData) Check(SmokeZoneBehavior.IsConcealed(_player.transform.position),"Smoke lost concealment");
                Shot("skill-"+_lastVfx.Style);
                var vision=_lastVfx.GetComponent<LocalVisionSubject>();
                vision.SetHidden(true);
                Check(_lastVfx.GetComponentsInChildren<Renderer>().All(r=>r.forceRenderingOff),"VFX leaks through fog");
                _vfxHolder.Deactivate();
                Check(!_lastVfx.gameObject.activeSelf && _lastVfx.ParticleCount==0,"Pooled VFX did not stop/clear");
                if(_vfxHolder.abilityData is SmokeBombData) Check(!SmokeZoneBehavior.IsConcealed(_player.transform.position),"Smoke concealment leaked after release");
                typeof(AbilityHolder).GetField("cooldownTimer",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(_vfxHolder,0f);
                Check(_vfxHolder.TryActivate(),"Pooled replay rejected");
                Check(_vfxHolder.VisualEffect==_lastVfx,"Effect was not reused from pool");
                _vfxStep=2; _next=Time.unscaledTime+.2f; return;
            }
            Check(_lastVfx.ParticleCount>0 && _lastVfx.GetComponentsInChildren<Renderer>().All(r=>!r.forceRenderingOff),"Pooled VFX did not restore visibility");
            _vfxHolder.Deactivate(); _vfxIndex++; _vfxStep=0; _next=Time.unscaledTime+.1f;
        }
        private void Finish(bool pass,string detail)
        {
            if(_done)return;_done=true;Time.timeScale=1;
            File.WriteAllText("TestResults/"+(_vfxMode?"skill-vfx-":"ui-flow-")+Screen.width+".txt",(pass?"PASS: ":"FAIL: ")+detail);
            Application.Quit(pass?0:1);
        }
    }
}
#endif
