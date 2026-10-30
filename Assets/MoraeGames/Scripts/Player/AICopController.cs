using UnityEngine;
using MoraeGames.Core;
using MoraeGames.Ability;
using MoraeGames.Game;
using UnityEngine.AI;

namespace MoraeGames.Player
{
    /// <summary>
    /// AI-controlled cop that patrols, detects robbers, and arrests them.
    /// </summary>
    public class AICopController : AIController
    {
        public enum PursuitState : byte { Clear, Searching, Chasing }
        private static readonly System.Collections.Generic.List<AICopController> ActiveCops = new();
        private Transform _lastTrackedTarget;

        private void OnEnable() => ActiveCops.Add(this);
        private void OnDisable()
        {
            ActiveCops.Remove(this);
            _currentTarget = _lastTrackedTarget = null;
            _lastSeenAt = -100;
        }

        /// <summary>Reports actual AI attention toward one robber, without revealing police positions.</summary>
        public static PursuitState GetPursuitState(RobberController robber)
        {
            if (robber == null || robber.IsArrested) return PursuitState.Clear;
            var result = PursuitState.Clear;
            foreach (var cop in ActiveCops)
            {
                if (cop.IsLocalPlayer) continue;
                if (cop._currentTarget == robber.transform) return PursuitState.Chasing;
                if (cop._currentTarget == null && cop._lastTrackedTarget == robber.transform &&
                    Time.time - cop._lastSeenAt < cop.searchMemorySeconds) result = PursuitState.Searching;
            }
            return result;
        }
        [Header("Cop Specific AI Stats")]
        public float detectionRadius = GameConstants.CopDetectionRadius;
        public float arrestRange = GameConstants.CopArrestRange;
        [Header("Pursuit")]
        public float searchMemorySeconds = 5f;
        public float predictionSeconds = .35f;

        private Transform _currentTarget;
        private AbilityHolder _ability;
        private float _wanderTimer = 0f;
        private float _decisionTimer;
        private Vector3 _lastKnownPosition;
        private float _lastSeenAt = -100;
        private int _searchStep;
        private int _patrolIndex = -1;
        private NavMeshPath _pursuitPath;

        public bool IsLocalPlayer { get; set; } = false;

        // Reusable buffer for Physics.OverlapSphereNonAlloc (Rule §4: Zero-Allocation)
        private readonly Collider[] _overlapBuffer = new Collider[30];

        protected override void Awake()
        {
            base.Awake();
            _pursuitPath = new NavMeshPath();
            _ability = GetComponent<AbilityHolder>();

            // Cops cannot path through robber-only vent passages (spec §5)
            if (agent != null)
            {
                agent.areaMask &= ~(1 << GameConstants.VentNavMeshArea);
                agent.acceleration = Mathf.Max(agent.acceleration, 24);
                agent.angularSpeed = Mathf.Max(agent.angularSpeed, 720);
                agent.stoppingDistance = .15f;
            }
        }

        protected override void Update()
        {
            // Block AI behavior when assigned as local player
            if (IsLocalPlayer)
            {
                if (agent != null && agent.enabled) agent.enabled = false;
                return;
            }

            base.Update();
            if (!MoraeGames.Network.NetworkSessionManager.HasStateAuthority || InGameManager.IsPaused) return;
            _decisionTimer -= Time.deltaTime;
            if (_decisionTimer > 0) return;
            _decisionTimer = .15f;
            HandleAIBehavior();
        }

        private void HandleAIBehavior()
        {
            if (_currentTarget == null)
            {
                SearchForRobber();
            }
            else
            {
                ChaseAndArrestTarget();
            }
        }

        private void SearchForRobber()
        {
            if (!AgentReady) return;

            _wanderTimer += .15f;

            // Set new destination if arrived or timed out
            var alarm = HeistBuilding.NearestAlarm(transform.position);
            if (Time.time - _lastSeenAt < searchMemorySeconds)
            {
                // Search only the last observed location, never the hidden target's live position.
                Vector3 search = _lastKnownPosition;
                if (!agent.pathPending && agent.hasPath && agent.remainingDistance < .8f && _searchStep < 2)
                    _searchStep++;
                if (_searchStep > 0) search += new Vector3(_searchStep == 1 ? 2.5f : -2.5f, 0, 1.5f);
                if (!TryPursuitPath(search)) TryPursuitPath(_lastKnownPosition);
            }
            else if (alarm != null)
            {
                if (_wanderTimer >= .5f || !agent.hasPath)
                {
                    _wanderTimer = 0;
                    TryPursuitPath(alarm.loot.transform.position);
                }
            }
            else if ((!agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + .1f))
                || (_wanderTimer >= GameConstants.WanderTimeout && agent.velocity.sqrMagnitude < .1f))
            {
                _wanderTimer = 0f;
                if (PatrolObjective()) { DetectRobber(); return; }
                Vector3 randomDirection = Random.insideUnitSphere * GameConstants.CopWanderRadius;
                randomDirection += transform.position;

                UnityEngine.AI.NavMeshHit hit;
                // Sample with the agent's own mask so wander targets never land in vent areas
                if (UnityEngine.AI.NavMesh.SamplePosition(randomDirection, out hit, GameConstants.CopWanderRadius, agent.areaMask))
                {
                    MoveTo(hit.position);
                }
            }

            DetectRobber();
        }

        private void DetectRobber()
        {
            // Find nearest robber using NonAlloc buffer
            int count = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, _overlapBuffer);
            float minDistance = float.MaxValue;
            Transform nearestRobber = null;

            for (int i = 0; i < count; i++)
            {
                Transform robberRoot = _overlapBuffer[i].transform.root;
                if (!robberRoot.CompareTag("Robber")) continue;

                IArrestable arrestable = robberRoot.GetComponent<IArrestable>();
                if (arrestable == null || arrestable.IsArrested || arrestable.IsInvincible || arrestable.IsDisguised) continue;

                // Smoke clouds block detection (Robber smoke bomb ability)
                if (SmokeZoneBehavior.IsConcealed(robberRoot.position)) continue;
                if (!HeistBuilding.HasSight(transform.position, robberRoot.position)) continue;

                float dist = Vector3.Distance(transform.position, robberRoot.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestRobber = robberRoot;
                }
            }

            if (nearestRobber != null)
            {
                _currentTarget = nearestRobber;
                _lastTrackedTarget = nearestRobber;
                _lastKnownPosition = nearestRobber.position;
                _lastSeenAt = Time.time;
                _searchStep = 0;
                Debug.Log($"AI Cop {gameObject.name} spotted target {_currentTarget.name} and begins pursuit!");
            }
        }

        private void ChaseAndArrestTarget()
        {
            if (_currentTarget == null) return;

            // Check if target is still valid in real-time (disguised/concealed targets are "lost")
            IArrestable arrestable = _currentTarget.GetComponent<IArrestable>();
            if (arrestable == null || arrestable.IsArrested || arrestable.IsInvincible)
            {
                _currentTarget = null;
                _lastSeenAt = -100;
                return;
            }
            if (arrestable.IsDisguised
                || SmokeZoneBehavior.IsConcealed(_currentTarget.position)
                || !HeistBuilding.HasSight(transform.position, _currentTarget.position)
                || Vector3.Distance(transform.position, _currentTarget.position) > detectionRadius * GameConstants.ChaseAbandonMultiplier)
            {
                _currentTarget = null;
                _searchStep = 0;
                TryPursuitPath(_lastKnownPosition);
                return;
            }

            float distanceToTarget = Vector3.Distance(transform.position, _currentTarget.position);
            Vector3 observed = _currentTarget.position;
            float elapsed = Time.time - _lastSeenAt;
            Vector3 velocity = elapsed > .01f ? (observed - _lastKnownPosition) / elapsed : Vector3.zero;
            velocity.y = 0;
            Vector3 lead = Vector3.ClampMagnitude(velocity * predictionSeconds, 2f);
            _lastKnownPosition = observed;
            _lastSeenAt = Time.time;
            Vector3 predicted = observed + lead;
            if (!HeistBuilding.HasSight(observed, predicted) || !TryPursuitPath(predicted)) TryPursuitPath(observed);

            // Use speed boost ability when chasing
            if (_ability != null && _ability.IsReady && distanceToTarget < detectionRadius)
            {
                _ability.TryActivate();
            }

            if (distanceToTarget <= arrestRange)
            {
                ArrestTarget();
            }

        }

        /// <summary>Rejects partial paths and unreachable predictions rather than running into a wall.</summary>
        private bool TryPursuitPath(Vector3 destination)
        {
            if (!AgentReady || !NavMesh.SamplePosition(destination, out var hit, 2f, agent.areaMask)) return false;
            if (!agent.CalculatePath(hit.position, _pursuitPath) || _pursuitPath.status != NavMeshPathStatus.PathComplete) return false;
            if (!agent.SetPath(_pursuitPath)) return false;
            agent.isStopped = false;
            return true;
        }

        private bool PatrolObjective()
        {
            var buildings = HeistBuilding.Instances;
            if (buildings.Count == 0) return false;
            if (_patrolIndex < 0) _patrolIndex = Random.Range(0, buildings.Count);
            for (int i = 0; i < buildings.Count; i++)
            {
                _patrolIndex = (_patrolIndex + 1) % buildings.Count;
                var building = buildings[_patrolIndex];
                if (building.loot != null && !building.loot.IsLooted && TryPursuitPath(building.loot.transform.position)) return true;
            }
            return false;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Unity delivers trigger events to disabled components: block arrests
            // during the ready freeze / after game end / when player-controlled.
            if (!enabled || IsLocalPlayer) return;

            // Arrest on touch
            Transform robberRoot = other.transform.root;
            if (!robberRoot.CompareTag("Robber")) return;

            IArrestable arrestable = robberRoot.GetComponent<IArrestable>();
            if (arrestable != null && !arrestable.IsArrested && !arrestable.IsInvincible)
            {
                Debug.Log($"AI Cop {gameObject.name} touched {robberRoot.name} and attempts arrest.");
                _currentTarget = robberRoot;
                ArrestTarget();
            }
        }

        private void ArrestTarget()
        {
            if (_currentTarget == null) return;
            if (!HeistBuilding.HasSight(transform.position, _currentTarget.position)) return;

            IArrestable arrestable = _currentTarget.GetComponent<IArrestable>();
            if (arrestable != null && !arrestable.IsArrested && !arrestable.IsInvincible)
            {
                Debug.Log($"AI Cop {gameObject.name} arrested {_currentTarget.name}!");
                arrestable.OnArrested();
            }
            _currentTarget = null;
            _lastSeenAt = -100;
        }
    }
}
