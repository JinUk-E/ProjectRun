#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using MoraeGames.Game;
using MoraeGames.Core;
using MoraeGames.Player;
using MoraeGames.Ability;
using MoraeGames.UI;
namespace MoraeGames.Network
{
    /// <summary>Opt-in four-process room/ping/rematch integration test. Result states are injected without awarding progress.</summary>
    public sealed class RoomNetworkProbe : MonoBehaviour
    {
        private int _peer,_stage,_round=1,_validation;
        private float _started,_playing=-1,_lobbyAt;
        private bool _done,_pinged,_ended;
        private ulong _id;
        private PlayerController _player;
        private InGameUIManager _hud;
        private string _report;
        private HeistBuilding _alarmBuilding;
        private PrisonZone _prison;
        private bool _doorStarted, _doorCompleted, _doorProgressSeen;
        private bool _lanUi, _connectionRequested;
        private AICopController _pursuitCop;
        private Transform _pursuitTarget;
        private bool _chaseSeen, _searchSeen, _clearSeen;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var arg=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--room-probe="));if(arg==null)return;
            Application.runInBackground=true;
            var bootstrap=FindFirstObjectByType<ServerBootstrap>();if(bootstrap!=null)bootstrap.enabled=false;
            var obj=new GameObject("RoomNetworkProbe");DontDestroyOnLoad(obj);var probe=obj.AddComponent<RoomNetworkProbe>();probe._peer=int.Parse(arg.Split('=')[1]);
        }
        private void Start()
        {
            _started=Time.unscaledTime;_report="TestResults/room-peer-"+_peer+".txt";Application.logMessageReceived+=OnLog;
            _lanUi=Environment.GetCommandLineArgs().Contains("--lan-ui-room");
            if(_lanUi) { SceneManager.LoadScene("Lobby"); return; }
            RoleManager.Instance.AssignRole("Player1",RoleManager.PlayerRole.Robber);
            RoleManager.Instance.SelectCharacter("Player1",_peer==0?2:_peer==1?0:1);
            if(!(_peer==0?NetworkSessionManager.StartHost():NetworkSessionManager.StartClient("127.0.0.1")))Finish(false,"Connect failed");
        }
        private void OnDestroy()=>Application.logMessageReceived-=OnLog;
        private void OnLog(string message,string trace,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)Finish(false,message);}
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private void Update()
        {
            if(_done)return;
            if(Time.unscaledTime-_started>100){Finish(false,"Timeout stage="+_stage+" round="+_round+" status="+NetworkRoomState.Instance?.Status);return;}
            try
            {
                if(_lanUi && !_connectionRequested)
                {
                    var lobby=FindFirstObjectByType<LobbyManager>();
                    if(lobby==null || lobby.GetComponent<LanRoomPanel>()==null || Time.unscaledTime-_started<1) return;
                    lobby.SelectCharacter(_peer==0?2:_peer==1?0:1);
                    if(_peer==0) lobby.HostMatch();
                    else { lobby.joinCodeInput.text="127.0.0.1:7777"; lobby.JoinMatch(); }
                    Check(NetworkSessionManager.IsNetworked,"LAN lobby UI did not start connection");
                    _connectionRequested=true;
                }
                var nm=NetworkManager.Singleton;var room=NetworkRoomState.Instance;if(room==null || !nm.IsConnectedClient)return;
                if(_stage==1 && SceneManager.GetActiveScene().name=="Lobby"){_stage=2;_lobbyAt=Time.unscaledTime;return;}
                if(_stage==2 && SceneManager.GetActiveScene().name=="InGame"){_round=2;_stage=3;_playing=-1;_ended=false;_pinged=false;return;}
                if(_stage==0)
                {
                    _id=nm.LocalClientId;
                    if(_peer==2)
                    {
                        if(_validation==0 && room.Selections.ContainsKey(_id)){room.Submit(RoleManager.PlayerRole.Robber,99);_validation=1;return;}
                        if(_validation==1 && room.Status.Contains("올바른 스킬")){room.Submit(RoleManager.PlayerRole.Robber,1);_validation=2;}
                    }
                    if(_peer==3 && _validation==0 && room.Selections.ContainsKey(_id)){room.Submit(RoleManager.PlayerRole.Cop,0);_validation=1;return;}
                    if(_peer==3 && _validation==1 && room.Status.Contains("도둑만")){room.Submit(RoleManager.PlayerRole.Robber,4);_validation=2;}
                    // Wait for the last peer's rejection/reselection round-trip before starting.
                    if(_peer==0 && nm.ConnectedClientsIds.Count==4 && room.Selections.Count==4 && room.Selections.Values.Any(s=>s.Character==4) && Time.unscaledTime-_started>8)NetworkSessionManager.BeginMatch();
                    if(SceneManager.GetActiveScene().name=="InGame")_stage=1;
                    return;
                }
                if(_stage==1 || _stage==3)
                {
                    if(!NetworkCharacter.MatchAllowsActions && !_ended)return;
                    if(_playing<0)
                    {
                        _playing=Time.unscaledTime;
                        _alarmBuilding=HeistBuilding.Instances.First(b=>b.guarded);
                        Check(!_alarmBuilding.AlarmActive,"Alarm leaked across rounds");
                        _prison=FindFirstObjectByType<PrisonZone>();
                        Check(!_prison.DoorOpen,"Prison gate state leaked across rounds");
                        _doorStarted=_doorCompleted=_doorProgressSeen=false;
                        foreach(var ai in FindObjectsByType<AIController>(FindObjectsSortMode.None))ai.enabled=false;
                        foreach(var agent in FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None))if(agent.enabled && agent.isOnNavMesh)agent.isStopped=true;
                        _player=PlayerController.Instances.First(p=>p.IsLocallyControlled);_hud=FindFirstObjectByType<InGameUIManager>();
                        _chaseSeen = _searchSeen = _clearSeen = false;
                        if (_peer == 0)
                        {
                            _pursuitCop = FindFirstObjectByType<AICopController>(); _pursuitCop.enabled = true;
                            typeof(AICopController).GetField("_decisionTimer", BindingFlags.Instance|BindingFlags.NonPublic).SetValue(_pursuitCop, float.MaxValue);
                            _pursuitTarget = PlayerController.Instances.OfType<RobberController>().First(p => !p.GetComponent<NetworkCharacter>().IsAiControlled && p.GetComponent<AbilityHolder>().abilityData is EscapeTrapData).transform;
                        }
                        Check(nm.LocalClientId==_id,"Client identity changed");
                        Check(_player is RobberController,"Human must spawn as robber");
                        Check(PlayerController.Instances.OfType<CopController>().All(p=>!p.IsLocallyControlled),"Human police spawned");
                        Check(room.Selections.Values.All(s=>s.Role==RoleManager.PlayerRole.Robber),"Server accepted a police player");
                        var skill=_player.GetComponent<AbilityHolder>().abilityData;
                        Check(_peer==0?(_round==1?skill is DisguiseData:skill is DecoyCloneData):_peer==1?skill is SpeedBurstData:_peer==2?skill is SmokeBombData:skill is EscapeTrapData,"Chosen skill not spawned: peer="+_peer+" round="+_round+" skill="+skill?.GetType().Name);
                        Check(PingSystem.Instance.ReceivedPingCount==0,"Ping state leaked from previous round");
                    }
                    float elapsed=Time.unscaledTime-_playing;
                    // Network scene transitions despawn the old player before the new lobby becomes active.
                    if (_player == null) return;
                    if (_peer == 0 && _pursuitCop != null)
                    {
                        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                        typeof(AICopController).GetField("_currentTarget", flags).SetValue(_pursuitCop, elapsed >= .75f && elapsed < 1.8f ? _pursuitTarget : null);
                        typeof(AICopController).GetField("_lastTrackedTarget", flags).SetValue(_pursuitCop, _pursuitTarget);
                        typeof(AICopController).GetField("_lastSeenAt", flags).SetValue(_pursuitCop, elapsed >= .75f && elapsed < 3.5f ? Time.time : -100f);
                    }
                    var pursuit = _player.GetComponent<NetworkCharacter>().PursuitState;
                    if (elapsed > .3f && elapsed < .5f) Check(pursuit == AICopController.PursuitState.Clear, "Pursuit did not reset before the controlled chase");
                    if (_peer == 3)
                    {
                        if (pursuit == AICopController.PursuitState.Chasing) _chaseSeen = true;
                        if (_chaseSeen && pursuit == AICopController.PursuitState.Searching) _searchSeen = true;
                        if (_searchSeen && pursuit == AICopController.PursuitState.Clear) _clearSeen = true;
                        if (elapsed > 4.8f) Check(_chaseSeen && _searchSeen && _clearSeen, "Owner did not receive chase/search/clear sequence");
                    }
                    else Check(pursuit == AICopController.PursuitState.Clear, "Pursuit leaked to another player");
                    if(_peer==0 && elapsed>2 && !_alarmBuilding.AlarmActive)_alarmBuilding.RecordTampering(2);
                    if(_peer==0 && elapsed>2.4f && !_doorStarted)
                    {
                        var prisoner=PlayerController.Instances.OfType<RobberController>().First(p=>p.GetComponent<NetworkCharacter>().IsAiControlled);
                        prisoner.OnArrested();
                        _player.transform.position=_prison.InteractionPosition;
                        _player.GetComponent<Rigidbody>().position=_prison.InteractionPosition;
                        _player.GetComponent<OwnerNetworkTransform>().TeleportOnAuthority(_prison.InteractionPosition);
                        Check(_prison.TryStartRescue(_player.gameObject),"Host door rescue rejected");
                        _prison.UpdateRescue(_prison.rescueDuration*.5f); _doorStarted=true;
                    }
                    if(_prison.GetRescueProgress()>.1f) _doorProgressSeen=true;
                    if(_peer==0 && elapsed>3.6f && !_doorCompleted)
                    {
                        _prison.UpdateRescue(_prison.rescueDuration); _doorCompleted=true;
                    }
                    if(!_pinged && elapsed>1 && (_peer==0 || (_round==1 && _peer==1 && elapsed>2)))
                    {
                        if(_peer==0 || _peer==1){PingSystem.Instance.CreatePing();PingSystem.Instance.CreatePing();_pinged=true;}
                    }
                    if(elapsed>4)
                    {
                        Check(_doorProgressSeen && (elapsed>=6 || _prison.DoorOpen) && !_prison.HasArrestedRobbers(),"Door progress/open/rescue state did not replicate: elapsed="+elapsed+" progressSeen="+_doorProgressSeen+" open="+_prison.DoorOpen);
                        Check(_alarmBuilding.AlarmActive && _alarmBuilding.AlarmRemaining>0,"Alarm timer was not replicated");
                        int expected=_round==1?2:1;
                        Check(PingSystem.Instance.ReceivedPingCount==expected,"Team ping or spam limit mismatch: "+PingSystem.Instance.ReceivedPingCount+" expected="+expected);
                        if(_peer>=2)Check(_validation==2,"Selection rejection was not verified");
                    }
                    if(_round==2 && elapsed>(_peer==0?6:5)){Finish(true,"Four robbers; owner-only chase/search/clear replication and reset; rejected invalid selections; pings and prison door rescue; rematch with changed skill. No progression awarded.");return;}
                    if(_round==1 && elapsed>6 && !_ended)
                    {
                        // Enter a controlled result state without touching user progression saves.
                        var game=FindFirstObjectByType<InGameManager>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                        typeof(InGameManager).GetField("_isGameOver",flags).SetValue(game,true);
                        typeof(InGameManager).GetField("_isPlaying",flags).SetValue(game,false);
                        GameManager.Instance.NotifyGameEnd("Cop");_hud.ShowResult(RoleManager.PlayerRole.Cop);_ended=true;
                        Check(_hud.lobbyButton.interactable==(_peer==0),"Result return authority incorrect");
                    }
                    if(_round==1 && elapsed>8 && _peer==0)_hud.lobbyButton.onClick.Invoke();
                    return;
                }
                if(_stage==2)
                {
                    if(room.IsLoading || Time.unscaledTime-_lobbyAt<1)return;
                    Check(nm.IsConnectedClient && nm.LocalClientId==_id,"Room connection was lost");
                    var lobby=FindFirstObjectByType<LobbyManager>();Check(lobby!=null,"Party did not return to lobby");
                    if(_peer==0 && _validation==0){lobby.SelectRobberRole();lobby.SelectCharacter(3);_validation=1;}
                    if(_peer==0 && Time.unscaledTime-_lobbyAt>3)lobby.startMatchButton.onClick.Invoke();
                }
            }
            catch(Exception error){Finish(false,error.ToString());}
        }
        private void Finish(bool pass,string detail){if(_done)return;_done=true;File.WriteAllText(_report,(pass?"PASS: ":"FAIL: ")+detail);Application.Quit(pass?0:1);}
    }
}
#endif
