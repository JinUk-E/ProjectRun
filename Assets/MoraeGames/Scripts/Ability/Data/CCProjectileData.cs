using UnityEngine;
using MoraeGames.Core;
using MoraeGames.Player;

namespace MoraeGames.Ability
{
    [CreateAssetMenu(menuName = "Abilities/Cop/CCProjectile")]
    public class CCProjectileData : AbilityDataSO
    {
        [Header("CC Projectile")]
        public float projectileSpeed = 20f;
        public float slowDuration = 3f;
        public float slowMultiplier = 0.4f;
        public Color projectileColor = new Color(0.3f, 0.8f, 1f);

        private const string PoolKey = "CCProjectile";

        public override void OnActivate(AbilityHolder holder)
        {
            Vector3 direction = holder.transform.forward;

            // Auto-aim toward nearest robber
            Collider[] nearby = Physics.OverlapSphere(holder.transform.position, GameConstants.CopDetectionRadius);
            float minDist = float.MaxValue;
            Transform nearestRobber = null;

            foreach (var hit in nearby)
            {
                Transform root = hit.transform.root;
                if (!root.CompareTag("Robber")) continue;
                IArrestable arrestable = root.GetComponent<IArrestable>();
                if (arrestable == null || arrestable.IsArrested || arrestable.IsDisguised) continue;

                // Smoke clouds block auto-aim targeting too
                if (SmokeZoneBehavior.IsConcealed(root.position)) continue;

                float dist = Vector3.Distance(holder.transform.position, root.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestRobber = root;
                }
            }

            if (nearestRobber != null)
                direction = (nearestRobber.position - holder.transform.position).normalized;

            // Pooled projectile (Rule §4: pooling mandatory for projectiles)
            GameObject projectile = EffectPool.Get(PoolKey, CreateProjectile);
            projectile.transform.position = holder.transform.position + Vector3.up * 1f + direction * 1f;
            RendererTint.SetColor(projectile.GetComponent<Renderer>(), projectileColor);

            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            rb.linearVelocity = direction * projectileSpeed;

            projectile.GetComponent<CCProjectileBehavior>().Launch(holder.gameObject, slowDuration, slowMultiplier);
        }

        public override void OnDeactivate(AbilityHolder holder) { }

        private static GameObject CreateProjectile()
        {
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "CCProjectile";
            projectile.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("AbilityRing");
            projectile.transform.localScale = Vector3.one * 0.4f;

            Object.Destroy(projectile.GetComponent<Collider>());
            SphereCollider col = projectile.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.5f;

            Rigidbody rb = projectile.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            projectile.AddComponent<CCProjectileBehavior>();
            return projectile;
        }
    }

    // Kept as MonoBehaviour since it needs OnTriggerEnter on the projectile GameObject
    public class CCProjectileBehavior : MonoBehaviour
    {
        private const string PoolKey = "CCProjectile";

        private GameObject _owner;
        private float _slowDuration;
        private float _slowMultiplier;
        private float _lifetime;
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        /// <summary>Resets runtime state on (re)use from the pool.</summary>
        public void Launch(GameObject owner, float slowDuration, float slowMultiplier)
        {
            _owner = owner;
            _slowDuration = slowDuration;
            _slowMultiplier = slowMultiplier;
            _lifetime = 3f;
        }

        private void Update()
        {
            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0f) ReturnToPool();
        }

        private void OnTriggerEnter(Collider other)
        {
            Transform root = other.transform.root;
            if (_owner != null && root == _owner.transform) return;
            if (!root.CompareTag("Robber")) { if (!other.isTrigger) ReturnToPool(); return; }
            if (!MoraeGames.Network.NetworkSessionManager.HasStateAuthority) return;

            IArrestable arrestable = root.GetComponent<IArrestable>();
            if (arrestable == null || arrestable.IsArrested) return;

            PlayerController pc = root.GetComponent<PlayerController>();
            if (pc != null)
            {
                // Repeat hits refresh the existing debuff instead of stacking a new one
                SlowDebuff existing = root.GetComponent<SlowDebuff>();
                if (existing != null)
                    existing.Refresh(_slowDuration);
                else
                    root.gameObject.AddComponent<SlowDebuff>().Initialize(pc, _slowDuration, _slowMultiplier);
            }
            Debug.Log($"[CCProjectile] Hit {root.name}! Applying slow.");
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (_rb != null) _rb.linearVelocity = Vector3.zero;
            EffectPool.Release(PoolKey, gameObject);
        }
    }

    public class SlowDebuff : MonoBehaviour
    {
        private PlayerController playerController;
        private float remainingDuration;
        private Renderer bodyRenderer;
        private bool restored;

        public void Initialize(PlayerController pc, float duration, float multiplier)
        {
            playerController = pc;
            remainingDuration = duration;

            // Modifier stack handles both moveSpeed and NavMeshAgent speed,
            // and composes with zones/buffs regardless of expiry order
            pc.ApplySpeedModifier(this, multiplier);

            Transform body = pc.transform.Find("Body");
            if (body != null)
            {
                bodyRenderer = body.GetComponentInChildren<Renderer>();
                RendererTint.SetColor(bodyRenderer, Color.yellow);
            }
        }

        /// <summary>Extends the debuff duration on repeat hits.</summary>
        public void Refresh(float duration)
        {
            remainingDuration = Mathf.Max(remainingDuration, duration);
        }

        private void Update()
        {
            remainingDuration -= Time.deltaTime;
            if (remainingDuration <= 0f)
                Destroy(this);
        }

        // Restore runs on expiry AND when the host object is destroyed mid-debuff
        private void OnDestroy()
        {
            if (restored) return;
            restored = true;

            if (playerController != null)
                playerController.RemoveSpeedModifier(this);
            RendererTint.ClearColor(bodyRenderer);
        }
    }
}

