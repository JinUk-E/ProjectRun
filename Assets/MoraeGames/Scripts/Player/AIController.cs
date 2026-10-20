using UnityEngine;
using UnityEngine.AI;

namespace MoraeGames.Player
{
    /// <summary>
    /// Base class for AI-controlled characters. Requires NavMeshAgent for pathfinding.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class AIController : MonoBehaviour
    {
        [Header("AI Base Stats")]
        public float health = 100f;

        protected NavMeshAgent agent;

        /// <summary>Whether the NavMeshAgent can safely receive commands right now.</summary>
        protected bool AgentReady => agent != null && agent.enabled && agent.isOnNavMesh;

        protected virtual void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        protected virtual void Update()
        {
            // Base update logic for AI (e.g., state machines, sensory checks)
        }

        /// <summary>Applies damage to this AI. Calls Die() if health reaches zero.</summary>
        public virtual void TakeDamage(float amount)
        {
            health -= amount;
            if (health <= 0)
            {
                Die();
            }
        }

        protected virtual void Die()
        {
            Debug.Log($"AI {gameObject.name} died.");
            StopMoving();
        }

        /// <summary>Commands this AI to move to the given destination. No-op if the agent is disabled/off-mesh.</summary>
        public void MoveTo(Vector3 destination)
        {
            if (!AgentReady) return;
            agent.SetDestination(destination);
            agent.isStopped = false;
        }

        /// <summary>Stops this AI's movement. No-op if the agent is disabled/off-mesh.</summary>
        public void StopMoving()
        {
            if (!AgentReady) return;
            agent.isStopped = true;
        }
    }
}
