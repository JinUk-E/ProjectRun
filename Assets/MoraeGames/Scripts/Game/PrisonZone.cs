using UnityEngine;
using MoraeGames.Core;
using MoraeGames.Player;

namespace MoraeGames.Game
{
    /// <summary>
    /// Prison zone where arrested robbers are held. Robbers can rescue teammates
    /// by interacting with this zone.
    /// </summary>
    public class PrisonZone : MonoBehaviour
    {
        [Header("Rescue Settings")]
        public float rescueDuration = GameConstants.RescueDuration;
        [Header("Prison Door")]
        public Transform doorPoint;
        public Transform doorVisual;
        public BoxCollider doorCollider;
        public UnityEngine.AI.NavMeshObstacle doorObstacle;
        public TMPro.TMP_Text doorStatusText;
        public float interactionRange = 2.4f;
        public float doorOpenDuration = 4f;
        public bool DoorOpen { get; private set; }
        public Vector3 InteractionPosition => doorPoint != null ? doorPoint.position + doorPoint.forward * 1.5f : transform.position;
        private float _openRemaining;
        private Vector3 _doorClosedPosition;
        private RobberController _rescuerController;
        private int _lastDoorStatus = -1;
        private int _lastDoorPercent = -1;

        private float _rescueProgress = 0f;
        private bool _isBeingRescued = false;
        private GameObject _rescuer;

        // Cached robber list. Robbers are spawned by InGameManager.Start, whose execution
        // order relative to this Start is not guaranteed — so the cache self-heals whenever
        // it is empty (see HasArrestedRobbers), and InGameManager refreshes it after spawning.
        private RobberController[] _allRobbers;

        private void Awake()
        {
            if (rescueDuration <= 0) rescueDuration = GameConstants.RescueDuration;
            if (doorVisual != null) _doorClosedPosition = doorVisual.localPosition;
        }

        private void Start()
        {
            RefreshRobberCache();
        }

        private void Update()
        {
            if (MoraeGames.Network.NetworkSessionManager.HasStateAuthority)
            {
                if (_isBeingRescued && !CanInteract(_rescuerController)) CancelRescue();
                if (DoorOpen)
                {
                    _openRemaining -= Time.deltaTime;
                    if (_openRemaining <= 0 && !DoorwayOccupied()) DoorOpen = false;
                }
            }
            if (doorCollider != null) doorCollider.enabled = !DoorOpen;
            if (doorObstacle != null) doorObstacle.enabled = !DoorOpen;
            if (doorVisual != null)
                doorVisual.localPosition = Vector3.MoveTowards(doorVisual.localPosition,_doorClosedPosition + Vector3.right * (DoorOpen ? 3.4f : 0),Time.deltaTime*6);
            int state = DoorOpen ? 2 : _isBeingRescued ? 1 : 0;
            int percent = Mathf.RoundToInt(GetRescueProgress()*100);
            if (doorStatusText != null && (state != _lastDoorStatus || percent != _lastDoorPercent))
            {
                _lastDoorStatus=state; _lastDoorPercent=percent;
                if (state==2) doorStatusText.SetText("구출 성공 · 문 열림");
                else if (state==1) doorStatusText.SetText("잠금 해제 중 {0}%",percent);
                else doorStatusText.SetText("문 앞에서 F / 행동 길게 · 동료 구출");
                doorStatusText.color=state==2?Color.green:state==1?Color.yellow:Color.white;
            }
        }

        /// <summary>Only free robbers on the outside face of the door may operate its lock.</summary>
        public bool CanInteract(RobberController robber)
        {
            if (doorPoint == null || robber == null || robber.IsArrested || !robber.isActiveAndEnabled) return false;
            Vector3 offset=robber.transform.position-doorPoint.position;
            if (Mathf.Abs(offset.y)>2.5f) return false;
            offset.y=0;
            return offset.sqrMagnitude<=interactionRange*interactionRange && Vector3.Dot(offset,doorPoint.forward)>=.2f
                && Mathf.Abs(Vector3.Dot(offset,doorPoint.right))<=1.7f;
        }

        private bool DoorwayOccupied()
        {
            // Never close a gate on a free player or trap somebody who walked inside while open.
            foreach(var player in PlayerController.Instances)
            {
                if(player==null || player is RobberController robber && robber.IsArrested) continue;
                Vector3 offset=player.transform.position-transform.position;
                if(Mathf.Abs(offset.x)<5.5f && Mathf.Abs(offset.z)<6.2f) return true;
            }
            return false;
        }

        /// <summary>Refreshes the cached list of all robbers in the scene.</summary>
        public void RefreshRobberCache()
        {
            _allRobbers = FindObjectsByType<RobberController>(FindObjectsSortMode.None);
        }

        /// <summary>Checks if any robbers are currently arrested.</summary>
        public bool HasArrestedRobbers()
        {
            if (_allRobbers == null || _allRobbers.Length == 0)
                RefreshRobberCache();

            foreach (var robber in _allRobbers)
            {
                if (robber != null && robber.IsArrested)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Starts a rescue attempt by the given rescuer.
        /// Fails (returns false) if a rescue is already in progress — a second rescuer
        /// must never reset the accumulated progress — or if nobody is arrested.
        /// </summary>
        public bool TryStartRescue(GameObject rescuerObj)
        {
            // Judgments run only on the state authority (offline or host);
            // becomes a ServerRpc request in NGO Phase 3
            if (!MoraeGames.Network.NetworkSessionManager.HasStateAuthority) return false;

            if (_isBeingRescued) return false;
            if (!HasArrestedRobbers()) return false;
            var robber = rescuerObj != null ? rescuerObj.GetComponent<RobberController>() : null;
            if (!CanInteract(robber)) return false;

            _isBeingRescued = true;
            _rescuer = rescuerObj;
            _rescuerController = robber;
            _rescueProgress = 0f;
            return true;
        }

        /// <summary>Advances rescue progress by the given delta time.</summary>
        public void UpdateRescue(float deltaTime)
        {
            if (!MoraeGames.Network.NetworkSessionManager.HasStateAuthority || !_isBeingRescued) return;

            // Rescuer destroyed mid-rescue: release the lock instead of deadlocking
            if (!CanInteract(_rescuerController))
            {
                CancelRescue();
                return;
            }

            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime<=0) return;
            _rescueProgress += deltaTime;

            if (_rescueProgress >= rescueDuration)
            {
                CompleteRescue();
            }
        }

        /// <summary>Cancels the current rescue attempt and resets progress.</summary>
        public void CancelRescue()
        {
            _isBeingRescued = false;
            _rescuer = null;
            _rescuerController = null;
            _rescueProgress = 0f;
        }

        private void CompleteRescue()
        {
            _isBeingRescued = false;
            DoorOpen = true;
            _openRemaining = doorOpenDuration;
            if(doorCollider!=null) doorCollider.enabled=false;
            if(doorObstacle!=null) doorObstacle.enabled=false;

            // Mass rescue: free ALL arrested robbers at once per game design spec
            if (_allRobbers == null || _allRobbers.Length == 0)
                RefreshRobberCache();

            int rescuedCount = 0;
            foreach (var robber in _allRobbers)
            {
                if (robber == null || !robber.IsArrested) continue;

                // Teleport first so the NavMeshAgent re-enables at the exit position,
                // then flip the state (OnRescued -> HandleRescuedState enables the agent).
                Vector3 rescueExit = doorPoint.position + doorPoint.forward * (3f + rescuedCount / 3 * 1.3f)
                    + doorPoint.right * ((rescuedCount % 3 - 1) * 1.25f);
                if(UnityEngine.AI.NavMesh.SamplePosition(rescueExit,out var hit,1.2f,UnityEngine.AI.NavMesh.AllAreas)) rescueExit=hit.position;

                Rigidbody rb = robber.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // AI robbers stay kinematic — their movement is NavMeshAgent-driven
                    var character = robber.GetComponent<MoraeGames.Network.NetworkCharacter>();
                    rb.isKinematic = MoraeGames.Network.NetworkSessionManager.IsNetworked
                        ? character == null || character.IsAiControlled || !MoraeGames.Network.NetworkSessionManager.HasStateAuthority
                        : !robber.isLocalPlayer;
                    // Restore Continuous CD for the local player (dropped to speculative on arrest)
                    if (!rb.isKinematic)
                        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    rb.position = rescueExit;
                }
                robber.transform.position = rescueExit;
                var networkTransform = robber.GetComponent<MoraeGames.Network.OwnerNetworkTransform>();
                if (networkTransform != null) networkTransform.TeleportOnAuthority(rescueExit);

                robber.OnRescued();
                rescuedCount++;
            }

            Debug.Log($"[PrisonZone] Mass rescue! {rescuedCount} robbers freed by {_rescuer?.name}!");

            if (rescuedCount > 0 && GameManager.Instance != null)
                GameManager.Instance.BroadcastSystemMessage($"구출 성공! 도둑 {rescuedCount}명이 탈출했어요", Color.green);

            _rescuer = null;
            _rescuerController = null;
            _rescueProgress = 0f;
        }

        /// <summary>Returns the current rescue progress as a 0-1 value.</summary>
        public float GetRescueProgress() => _isBeingRescued ? _rescueProgress / rescueDuration : 0f;

        /// <summary>Whether this zone is currently being rescued.</summary>
        public bool IsBeingRescued => _isBeingRescued;

        /// <summary>Copies server progress without running rescue gameplay.</summary>
        public void ApplyRemoteState(bool rescuing, float progress, bool doorOpen)
        {
            if (MoraeGames.Network.NetworkSessionManager.HasStateAuthority) return;
            _isBeingRescued = rescuing;
            _rescueProgress = Mathf.Clamp01(progress) * rescueDuration;
            DoorOpen = doorOpen;
        }
    }
}
