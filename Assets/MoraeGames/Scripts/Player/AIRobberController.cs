using UnityEngine;
using MoraeGames.Core;
using MoraeGames.Game;
using MoraeGames.Ability;

namespace MoraeGames.Player
{
    /// <summary>
    /// AI-controlled robber that steals loot, rescues teammates, and flees from cops.
    /// Priority: Flee > Loot > Rescue > Wander.
    /// Arrest/Invincible/Disguised state is owned by the sibling RobberController;
    /// this component delegates IArrestable to it and only handles AI/NavMesh side effects.
    /// </summary>
    public class AIRobberController : AIController, IArrestable
    {
        [Header("Robber Specific AI Stats")]
        public float detectionRadius = GameConstants.RobberDetectionRadius;
        public float safeDistance = GameConstants.RobberSafeDistance;

        public bool IsLocalPlayer { get; set; } = false;

        // Fallback state, only used if no RobberController is attached (should not happen in practice)
        private bool _isArrestedFallback = false;

        public bool IsArrested => _robberController != null ? _robberController.IsArrested : _isArrestedFallback;
        public bool IsInvincible => _robberController != null && _robberController.IsInvincible;
        public bool IsDisguised => _robberController != null && _robberController.IsDisguised;

        private Transform _threat;
        private LootSpot _targetLootSpot;
        private bool _isRobbingLoot = false;
        private PrisonZone _targetPrison;
        private bool _isRescuing = false;
        private bool _movingToPrison = false;
        private float _rescueDecisionTimer = 0f;
        private float _wanderTimer = 0f;

        // Cached references (Rule §2: Dependency — cache in Awake)
        private AbilityHolder _ability;
        private RobberController _robberController;
        private Rigidbody _rb;
        private LootManager _lootManager;
        private PrisonZone _prisonZone;

        // Reusable buffer for Physics.OverlapSphereNonAlloc (Rule §4: Zero-Allocation)
        private readonly Collider[] _overlapBuffer = new Collider[30];

        protected override void Awake()
        {
            base.Awake();
            _ability = GetComponent<AbilityHolder>();
            _robberController = GetComponent<RobberController>();
            _rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            // Cache scene-level references once (Rule §4: no Find in Update)
            _lootManager = Object.FindFirstObjectByType<LootManager>();
            _prisonZone = Object.FindFirstObjectByType<PrisonZone>();
        }

        protected override void Update()
        {
            // Block AI behavior when assigned as local player
            if (IsLocalPlayer)
            {
                if (agent != null && agent.enabled) agent.enabled = false;
                return;
            }

            if (IsArrested) return;

            base.Update();
            HandleAIBehavior();
        }

        private void HandleAIBehavior()
        {
            // Priority: Flee > Loot > Rescue > Wander
            if (_threat != null)
            {
                AbortInteraction();
                FleeFromThreat();
                return;
            }

            CheckForThreats();
            if (_threat != null)
            {
                AbortInteraction();
                FleeFromThreat();
                return;
            }

            // Continue ongoing robbery
            if (_isRobbingLoot && _targetLootSpot != null)
            {
                ContinueLootRobbery();
                return;
            }

            // Continue ongoing rescue
            if (_isRescuing && _targetPrison != null)
            {
                ContinueRescue();
                return;
            }

            // Committed to traveling toward the prison for a rescue
            if (_movingToPrison)
            {
                HandleTravelToPrison();
                return;
            }

            // Look for loot or rescue opportunities
            TryFindMission();
        }

        private void AbortInteraction()
        {
            if (_isRobbingLoot && _targetLootSpot != null)
            {
                _targetLootSpot.CancelRobbery();
            }
            _targetLootSpot = null;
            _isRobbingLoot = false;

            if (_isRescuing && _targetPrison != null)
            {
                _targetPrison.CancelRescue();
            }
            _targetPrison = null;
            _isRescuing = false;
            _movingToPrison = false;
        }

        private void TryFindMission()
        {
            // Try to find loot first (using cached LootManager)
            if (_lootManager != null)
            {
                LootSpot spot = _lootManager.GetNearestUnlootedSpot(transform.position);
                if (spot != null)
                {
                    float dist = Vector3.Distance(transform.position, spot.transform.position);
                    if (dist <= GameConstants.RobberInteractionRange && spot.CanReach(gameObject))
                    {
                        if (spot.TryStartRobbery(gameObject))
                        {
                            _targetLootSpot = spot;
                            _isRobbingLoot = true;
                            StopMoving();
                            return;
                        }
                    }
                    else
                    {
                        MoveTo(spot.transform.position);
                        return;
                    }
                }
            }

            // Try to rescue teammates (using cached PrisonZone)
            if (_prisonZone != null && _prisonZone.HasArrestedRobbers() && !_prisonZone.IsBeingRescued)
            {
                float dist = Vector3.Distance(transform.position, _prisonZone.transform.position);
                if (dist <= GameConstants.PrisonZoneRadius)
                {
                    if (_prisonZone.TryStartRescue(gameObject))
                    {
                        _targetPrison = _prisonZone;
                        _isRescuing = true;
                        StopMoving();
                        return;
                    }
                }
                else
                {
                    // Periodic decision instead of a per-frame roll (a per-frame 30% roll
                    // reaches ~99% within a fraction of a second)
                    _rescueDecisionTimer -= Time.deltaTime;
                    if (_rescueDecisionTimer <= 0f)
                    {
                        _rescueDecisionTimer = GameConstants.RobberRescueDecisionInterval;
                        if (Random.value < GameConstants.RobberRescueAttemptChance)
                        {
                            _movingToPrison = true;
                            MoveTo(_prisonZone.transform.position);
                            return;
                        }
                    }
                }
            }

            // Default: wander
            WanderOrDoMission();
        }

        private void HandleTravelToPrison()
        {
            if (_prisonZone == null || !_prisonZone.HasArrestedRobbers() || _prisonZone.IsBeingRescued)
            {
                _movingToPrison = false;
                return;
            }

            float dist = Vector3.Distance(transform.position, _prisonZone.transform.position);
            if (dist <= GameConstants.PrisonZoneRadius)
            {
                _movingToPrison = false;
                if (_prisonZone.TryStartRescue(gameObject))
                {
                    _targetPrison = _prisonZone;
                    _isRescuing = true;
                    StopMoving();
                }
            }
            else
            {
                MoveTo(_prisonZone.transform.position);
            }
        }

        private void ContinueLootRobbery()
        {
            if (_targetLootSpot == null || _targetLootSpot.IsLooted)
            {
                _isRobbingLoot = false;
                _targetLootSpot = null;
                return;
            }

            // Never rob remotely (e.g. after being teleported away)
            float dist = Vector3.Distance(transform.position, _targetLootSpot.transform.position);
            if (dist > GameConstants.RobberInteractionRange * 1.5f)
            {
                _targetLootSpot.CancelRobbery();
                _isRobbingLoot = false;
                _targetLootSpot = null;
                return;
            }

            _targetLootSpot.UpdateRobbery(Time.deltaTime);
            if (_targetLootSpot.IsLooted)
            {
                _isRobbingLoot = false;
                _targetLootSpot = null;
            }
        }

        private void ContinueRescue()
        {
            if (_targetPrison == null)
            {
                _isRescuing = false;
                return;
            }

            float dist = Vector3.Distance(transform.position, _targetPrison.transform.position);
            if (dist > GameConstants.PrisonZoneRadius * 1.5f)
            {
                _targetPrison.CancelRescue();
                _isRescuing = false;
                _targetPrison = null;
                return;
            }

            _targetPrison.UpdateRescue(Time.deltaTime);
            if (!_targetPrison.IsBeingRescued)
            {
                _isRescuing = false;
                _targetPrison = null;
            }
        }

        private void CheckForThreats()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, _overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                Transform root = _overlapBuffer[i].transform.root;
                if (root.CompareTag("Cop") && HeistBuilding.HasSight(transform.position, root.position))
                {
                    _threat = root;
                    break;
                }
            }
        }

        private void FleeFromThreat()
        {
            float distanceToThreat = Vector3.Distance(transform.position, _threat.position);

            if (distanceToThreat >= safeDistance || !HeistBuilding.HasSight(transform.position, _threat.position))
            {
                _threat = null;
                StopMoving();
                return;
            }

            // Use ability when fleeing (speed burst or smoke bomb)
            if (_ability != null && _ability.IsReady)
            {
                _ability.TryActivate();
            }

            Vector3 fleeDirection = (transform.position - _threat.position).normalized;
            Vector3 fleeDestination = transform.position + (fleeDirection * GameConstants.RobberFleeDistance);
            MoveTo(fleeDestination);
        }

        private void WanderOrDoMission()
        {
            if (!AgentReady) return;

            _wanderTimer += Time.deltaTime;

            if (agent.remainingDistance <= agent.stoppingDistance + 0.1f || _wanderTimer >= GameConstants.WanderTimeout)
            {
                _wanderTimer = 0f;
                Vector3 randomDirection = Random.insideUnitSphere * GameConstants.RobberWanderRadius;
                randomDirection += transform.position;

                UnityEngine.AI.NavMeshHit navHit;
                if (UnityEngine.AI.NavMesh.SamplePosition(randomDirection, out navHit, GameConstants.RobberWanderRadius, UnityEngine.AI.NavMesh.AllAreas))
                {
                    MoveTo(navHit.position);
                }
            }
        }

        /// <summary>
        /// IArrestable entry point. Forwards to the state owner (RobberController).
        /// </summary>
        public void OnArrested()
        {
            if (_robberController != null)
            {
                _robberController.OnArrested();
                return;
            }

            // Fallback: standalone AI robber without a RobberController
            if (_isArrestedFallback) return;
            _isArrestedFallback = true;
            HandleArrestedState();
            PrisonHelper.TeleportToPrison(transform, _rb);
        }

        /// <summary>
        /// IArrestable entry point. Forwards to the state owner (RobberController).
        /// </summary>
        public void OnRescued()
        {
            if (_robberController != null)
            {
                _robberController.OnRescued();
                return;
            }

            if (!_isArrestedFallback) return;
            _isArrestedFallback = false;
            HandleRescuedState();
        }

        /// <summary>
        /// AI/NavMesh side effects of arrest. Called by RobberController (single state owner).
        /// Aborts any in-progress interaction so shared objects don't stay locked.
        /// </summary>
        public void HandleArrestedState()
        {
            AbortInteraction();
            _threat = null;
            StopMoving();
            if (agent != null) agent.enabled = false;
        }

        /// <summary>
        /// AI/NavMesh side effects of rescue. Called by RobberController (single state owner).
        /// Must run after the character has been teleported to the rescue exit.
        /// </summary>
        public void HandleRescuedState()
        {
            if (!IsLocalPlayer && agent != null)
                agent.enabled = true;
        }
    }
}
