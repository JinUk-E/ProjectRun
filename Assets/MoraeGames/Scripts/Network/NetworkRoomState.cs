using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Collections;
using MoraeGames.Game;

namespace MoraeGames.Network
{
    /// <summary>Connection-scoped, server-approved loadouts retained across match scene changes.</summary>
    public sealed class NetworkRoomState : MonoBehaviour
    {
        public struct Selection { public RoleManager.PlayerRole Role; public int Character; }
        public static NetworkRoomState Instance { get; private set; }
        public readonly Dictionary<ulong, Selection> Selections = new Dictionary<ulong, Selection>();
        private readonly Dictionary<ulong, string> _errors = new Dictionary<ulong, string>();
        private NetworkManager _manager;
        private CustomMessagingManager _messages;
        private bool _sentInitial;
        public bool IsLoading { get; private set; }
        public string Status { get; private set; } = "선택 전송 대기 중";
        public int Revision { get; private set; }
        private const string SelectMessage="Room.Select.v1", StateMessage="Room.State.v1";
        public static void Initialize(NetworkManager manager)
        {
            Close();
            Instance=manager.gameObject.AddComponent<NetworkRoomState>();
            Instance._manager=manager;Instance._messages=manager.CustomMessagingManager;
            Instance._messages.RegisterNamedMessageHandler(SelectMessage,Instance.OnSelection);
            Instance._messages.RegisterNamedMessageHandler(StateMessage,Instance.OnState);
            manager.OnClientDisconnectCallback+=Instance.OnDisconnected;
            manager.SceneManager.OnLoadEventCompleted+=Instance.OnSceneCompleted;
        }
        public static void Close()
        {
            if(Instance==null)return;
            Instance.Detach();Destroy(Instance);Instance=null;
        }
        private void Detach()
        {
            if(_messages!=null){_messages.UnregisterNamedMessageHandler(SelectMessage);_messages.UnregisterNamedMessageHandler(StateMessage);_messages=null;}
            if(_manager!=null){_manager.OnClientDisconnectCallback-=OnDisconnected;if(_manager.SceneManager!=null)_manager.SceneManager.OnLoadEventCompleted-=OnSceneCompleted;}
        }
        private void OnDestroy(){Detach();if(Instance==this)Instance=null;}
        private void Update()
        {
            if(_sentInitial || _manager==null || !_manager.IsConnectedClient)return;
            _sentInitial=true;
            var roles=RoleManager.Instance;
            Submit(roles!=null?roles.GetRole("Player1"):RoleManager.PlayerRole.None,roles!=null?roles.GetSelectedCharacterIndex("Player1"):0);
        }
        public void Submit(RoleManager.PlayerRole role,int character)
        {
            if(_manager==null || !_manager.IsConnectedClient)return;
            _sentInitial=true;Status="서버에서 선택 확인 중";Revision++;
            if(_manager.IsServer){Accept(_manager.LocalClientId,role,character);return;}
            using(var writer=new FastBufferWriter(8,Allocator.Temp)){writer.WriteValueSafe((int)role);writer.WriteValueSafe(character);_messages.SendNamedMessage(SelectMessage,NetworkManager.ServerClientId,writer);}
        }
        private void OnSelection(ulong sender,FastBufferReader reader)
        {
            // NGO's reader can include an already-consumed message header; validate remaining bytes.
            if(!_manager.IsServer || !reader.TryBeginRead(sizeof(int)*2))return;
            reader.ReadValueSafe(out int role);reader.ReadValueSafe(out int character);Accept(sender,(RoleManager.PlayerRole)role,character);
        }
        private void Accept(ulong sender,RoleManager.PlayerRole role,int character)
        {
            if(!_manager.ConnectedClients.ContainsKey(sender))return;
            if(NetworkSessionManager.MatchStarted || IsLoading){SendState(sender,"경기 중에는 선택을 바꿀 수 없습니다");return;}
            int robbers=0;
            foreach(var pair in Selections){if(pair.Key!=sender)robbers++;}
            if(role==RoleManager.PlayerRole.None)role=RoleManager.PlayerRole.Robber;
            string error=null;
            if(role!=RoleManager.PlayerRole.Robber)error="도둑만 플레이할 수 있습니다. 경찰은 AI가 맡습니다";
            else if(character<0 || character>=5)error="올바른 스킬을 선택하세요";
            else if(robbers>=NetworkSessionManager.MaxPlayers)error="도둑 인원이 가득 찼습니다";
            if(error!=null)_errors[sender]=error;
            else{_errors.Remove(sender);Selections[sender]=new Selection{Role=role,Character=character};}
            Broadcast();
        }
        public bool CanBegin()
        {
            if(_manager==null || !_manager.IsServer || IsLoading)return false;
            foreach(ulong id in _manager.ConnectedClientsIds)
                if(!Selections.ContainsKey(id) || _errors.ContainsKey(id)){Status="모든 도둑의 스킬 선택 확정을 기다리는 중";Revision++;return false;}
            return _manager.ConnectedClientsIds.Count>0;
        }
        public void LoadingStarted(){IsLoading=true;Broadcast();}
        private void OnSceneCompleted(string scene,LoadSceneMode mode,List<ulong> completed,List<ulong> timedOut)
        {
            IsLoading=false;
            NetworkSessionManager.SetMatchStarted(scene=="InGame");
            if(_manager.IsServer)Broadcast();
        }
        private void OnDisconnected(ulong id)
        {
            Selections.Remove(id);_errors.Remove(id);
            if(_manager!=null && _manager.IsServer)Broadcast();
        }
        private string SelectionStatus(ulong id)
        {
            if(_errors.TryGetValue(id,out var error))return error;
            if(Selections.TryGetValue(id,out var s))return $"확정: {(s.Role==RoleManager.PlayerRole.Cop?"경찰":"도둑")} · 스킬 {s.Character+1}";
            return "도둑 스킬을 선택하세요";
        }
        private void Broadcast()
        {
            if(_manager==null || !_manager.IsServer)return;
            foreach(ulong id in _manager.ConnectedClientsIds)SendState(id,SelectionStatus(id));
        }
        private void SendState(ulong id,string status)
        {
            if(id==_manager.LocalClientId && _manager.IsHost){Status=status;Revision++;return;}
            using(var writer=new FastBufferWriter(1024,Allocator.Temp))
            {
                writer.WriteValueSafe(NetworkSessionManager.MatchStarted);writer.WriteValueSafe(IsLoading);
                writer.WriteValueSafe(new FixedString512Bytes(status));writer.WriteValueSafe(Selections.Count);
                foreach(var pair in Selections){writer.WriteValueSafe(pair.Key);writer.WriteValueSafe((int)pair.Value.Role);writer.WriteValueSafe(pair.Value.Character);}
                _messages.SendNamedMessage(StateMessage,id,writer);
            }
        }
        private void OnState(ulong sender,FastBufferReader reader)
        {
            if(_manager.IsServer || sender!=NetworkManager.ServerClientId)return;
            reader.ReadValueSafe(out bool inMatch);reader.ReadValueSafe(out bool loading);reader.ReadValueSafe(out FixedString512Bytes status);reader.ReadValueSafe(out int count);
            if(count<0 || count>NetworkSessionManager.MaxPlayers)return;
            Selections.Clear();
            for(int i=0;i<count;i++){reader.ReadValueSafe(out ulong id);reader.ReadValueSafe(out int role);reader.ReadValueSafe(out int character);Selections[id]=new Selection{Role=(RoleManager.PlayerRole)role,Character=character};}
            NetworkSessionManager.SetMatchStarted(inMatch);IsLoading=loading;Status=status.ToString();Revision++;
        }
    }
}
