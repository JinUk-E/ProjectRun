using UnityEngine;
using UnityEngine.AI;
using MoraeGames.Player;

namespace MoraeGames.Ability
{
    [CreateAssetMenu(menuName = "Abilities/Robber/DecoyClone")]
    public class DecoyCloneData : AbilityDataSO
    {
        [Header("Decoy Clone")]
        public float decoySpeed = 3f;

        public override void OnActivate(AbilityHolder holder)
        {
            GameObject decoy = new GameObject($"{holder.gameObject.name}_Decoy");
            decoy.transform.position = holder.transform.position;
            decoy.transform.rotation = holder.transform.rotation;
            decoy.tag = "Robber";

            // Clone the owner's whole visual body (capsule + beanie/mask/eyes/nose) so
            // the decoy is indistinguishable from the real robber. Materials are shared
            // (no instantiation) and body children carry no colliders.
            Transform originalBody = holder.transform.Find("Body");
            if (originalBody != null)
            {
                GameObject body = Object.Instantiate(originalBody.gameObject, decoy.transform);
                body.name = "Body";
                body.transform.localPosition = originalBody.localPosition;
                body.transform.localRotation = Quaternion.identity;
            }
            else
            {
                // Fallback: bare capsule if the owner has no Body child
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(decoy.transform);
                body.transform.localPosition = new Vector3(0, 1f, 0);
                Object.Destroy(body.GetComponent<Collider>());
            }

            CapsuleCollider col = decoy.AddComponent<CapsuleCollider>();
            // Instantiated visuals may copy a hidden owner's rendering flag.
            foreach (var renderer in decoy.GetComponentsInChildren<Renderer>(true)) renderer.forceRenderingOff = false;
            decoy.AddComponent<MoraeGames.Game.LocalVisionSubject>();
            col.center = new Vector3(0, 1f, 0);
            col.radius = 0.5f;
            col.height = 2f;
            col.isTrigger = true;

            NavMeshAgent decoyAgent = decoy.AddComponent<NavMeshAgent>();
            decoyAgent.speed = decoySpeed;

            DecoyCloneBehavior behavior = decoy.AddComponent<DecoyCloneBehavior>();
            behavior.lifetime = duration;

            holder.spawnedObjects.Add(decoy);
            Debug.Log($"[DecoyClone] Decoy spawned! Will last {duration}s.");
        }

        public override void OnDeactivate(AbilityHolder holder)
        {
            holder.CleanupSpawnedObjects();
        }
    }

    // Implements IArrestable so AI cops actually chase the decoy — that is its whole
    // purpose. "Arresting" the decoy simply pops it.
    public class DecoyCloneBehavior : MonoBehaviour, IArrestable
    {
        public float lifetime;
        private NavMeshAgent agent;

        public bool IsArrested { get; private set; }
        public bool IsInvincible => false;
        public bool IsDisguised => false;

        public void OnArrested()
        {
            if (IsArrested) return;
            IsArrested = true;
            Debug.Log("[DecoyClone] Decoy was 'arrested' and destroyed!");
            Destroy(gameObject);
        }

        public void OnRescued() { }

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            ChooseRandomDestination();
        }

        private void Update()
        {
            lifetime -= Time.deltaTime;
            if (lifetime <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (agent != null && agent.isOnNavMesh && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
                ChooseRandomDestination();
        }

        private void ChooseRandomDestination()
        {
            if (agent == null || !agent.isOnNavMesh) return;
            Vector3 randomDir = Random.insideUnitSphere * 15f + transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDir, out hit, 15f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }
    }
}

