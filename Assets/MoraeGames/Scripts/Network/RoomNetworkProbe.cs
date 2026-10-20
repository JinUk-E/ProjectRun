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
            RoleManager.Instance.AssignRole("Player1",RoleManager.PlayerRole.Cop);
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
                var nm=NetworkManager.Singleton;var room=NetworkRoomState.Instance;if(room==null || !nm.IsConnectedClient)return;
                if(_stage==1 && SceneManager.GetActiveScene().name=="Lobby"){_stage=2;_lobbyAt=Time.unscaledTime;return;}
                if(_stage==2 && SceneManager.GetActiveScene().name=="InGame"){_round=2;_stage=3;_playing=-1;_ended=false;_pinged=false;return;}
                if(_stage==0)
                {
                    _id=nm.LocalClientId;
                    if(_peer==2)
                    {
                        if(_validation==0 && room.Selections.ContainsKey(_id)){room.Submit(RoleManager.PlayerRole.Cop,99);_validation=1;return;}
                        if(_validation==1 && room.Status.Contains("올바른 스킬")){room.Submit(RoleManager.PlayerRole.Cop,1);_validation=2;}
                    }
                    if(_peer==3 && _validation==0 && room.Status.Contains("가득")){room.Submit(RoleManager.PlayerRole.Robber,4);_validation=2;}
                    if(_peer==0 && nm.ConnectedClientsIds.Count==4 && room.Selections.Count==4 && Time.unscaledTime-_started>8)NetworkSessionManager.BeginMatch();
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
                        foreach(var ai in FindObjectsByType<AIController>(FindObjectsSortMode.None))ai.enabled=false;
                        foreach(var agent in FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None))if(agent.enabled && agent.isOnNavMesh)agent.isStopped=true;
                        _player=PlayerController.Instances.First(p=>p.IsLocallyControlled);_hud=FindFirstObjectByType<InGameUIManager>();
                        Check(nm.LocalClientId==_id,"Client identity changed");
                        bool robber=_peer==3 || (_round==2 && _peer==0);Check((_player is RobberController)==robber,"Chosen role not spawned");
                        var skill=_player.GetComponent<AbilityHolder>().abilityData;
                        Check(_peer==0?(_round==1?skill is CCProjectileData:skill is DecoyCloneData):_peer==1?skill is SpeedBoostData:_peer==2?skill is AreaScanData:skill is EscapeTrapData,"Chosen skill not spawned");
                        Check(PingSystem.Instance.ReceivedPingCount==0,"Ping state leaked from previous round");
                    }
                    float elapsed=Time.unscaledTime-_playing;
                    if(_peer==0 && elapsed>2 && !_alarmBuilding.AlarmActive)_alarmBuilding.RecordTampering(2);
                    if(!_pinged && elapsed>1 && (_peer==0 || (_round==1 && _peer==1 && elapsed>2)))
                    {
                        if(_peer==0 || _peer==1){PingSystem.Instance.CreatePing();PingSystem.Instance.CreatePing();_pinged=true;}
                    }
                    if(elapsed>4)
                    {
                        Check(_alarmBuilding.AlarmActive && _alarmBuilding.AlarmRemaining>0,"Alarm timer was not replicated");
                        int expected=_round==1?(_peer==3?0:2):(_peer==0 || _peer==3?1:0);
                        Check(PingSystem.Instance.ReceivedPingCount==expected,"Team ping or spam limit mismatch: "+PingSystem.Instance.ReceivedPingCount+" expected="+expected);
                        if(_peer>=2)Check(_validation==2,"Selection rejection was not verified");
                    }
                    if(_round==2 && elapsed>(_peer==0?6:5)){Finish(true,"Confirmed chosen role/skill, invalid selection/team capacity rejection, teammate-only pings + rate limit, same client identity and changed loadout across two rounds; no progression awarded.");return;}
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
