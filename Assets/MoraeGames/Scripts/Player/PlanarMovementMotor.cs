using UnityEngine;

namespace MoraeGames.Player
{
    /// <summary>Allocation-free capsule sweep and wall slide, shared by authority and prediction.</summary>
    public sealed class PlanarMovementMotor
    {
        private readonly CapsuleCollider _collider;
        private readonly Transform _root;
        private readonly Rigidbody _body;
        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private readonly Collider[] _overlaps = new Collider[32];
        private readonly Vector3 _center;
        private readonly float _radius;
        private readonly float _halfSegment;
        private readonly bool _isRobber;
        private readonly bool _isCop;
        private const float Skin = 0.015f;

        public PlanarMovementMotor(Transform root, Rigidbody body, CapsuleCollider collider)
        {
            _root = root;
            _body = body;
            _collider = collider;
            _isRobber = root.GetComponent<RobberController>() != null;
            _isCop = root.GetComponent<CopController>() != null;
            if (collider == null) return;
            Vector3 scale = collider.transform.lossyScale;
            _center = collider.transform.TransformPoint(collider.center) - root.position;
            _radius = collider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            _halfSegment = Mathf.Max(0f, collider.height * Mathf.Abs(scale.y) * 0.5f - _radius);
        }

        public Vector3 Step(Vector3 position, Vector2 input, float speed, float deltaTime)
        {
            if (_collider == null) return position; // Misconfigured characters must not pass through walls.
            // PhysX permits small contact overlaps. Resolve them before sweeping, otherwise
            // a cast starting inside a wall reports -direction and prevents even escape.
            for (int pass = 0; pass < 3; pass++)
            {
                Vector3 center = position + _center;
                int count = Physics.OverlapCapsuleNonAlloc(center + Vector3.up * _halfSegment,
                    center - Vector3.up * _halfSegment, _radius, _overlaps, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                bool corrected = false;
                for (int i = 0; i < count; i++)
                {
                    Collider other = _overlaps[i];
                    if (Ignore(other)) continue;
                    if (!Physics.ComputePenetration(_collider, _collider.transform.position + position - _root.position,
                        _collider.transform.rotation, other, other.transform.position, other.transform.rotation,
                        out Vector3 direction, out float depth)) continue;
                    direction.y = 0f;
                    if (direction.sqrMagnitude < 0.01f) continue;
                    position += direction.normalized * (depth + Skin);
                    corrected = true;
                }
                if (!corrected) break;
            }
            Vector2 bounded = Vector2.ClampMagnitude(input, 1f);
            Vector3 remaining = new Vector3(bounded.x, 0f, bounded.y) * Mathf.Max(0f, speed) * deltaTime;
            for (int iteration = 0; iteration < 3 && remaining.sqrMagnitude > 0.000001f; iteration++)
            {
                float distance = remaining.magnitude;
                Vector3 direction = remaining / distance;
                Vector3 center = position + _center;
                int count = Physics.CapsuleCastNonAlloc(center + Vector3.up * _halfSegment,
                    center - Vector3.up * _halfSegment, _radius, direction, _hits,
                    distance + Skin, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                if (count == _hits.Length) return position; // Conservative on query overflow.
                float nearest = float.PositiveInfinity;
                Vector3 normal = Vector3.zero;
                for (int i = 0; i < count; i++)
                {
                    Collider other = _hits[i].collider;
                    if (Ignore(other)) continue;
                    Vector3 wallNormal = _hits[i].normal;
                    wallNormal.y = 0f;
                    if (wallNormal.sqrMagnitude < 0.01f) continue; // Ground contact is not a wall.
                    wallNormal.Normalize();
                    if (Vector3.Dot(direction, wallNormal) >= -0.001f || _hits[i].distance >= nearest) continue;
                    nearest = _hits[i].distance;
                    normal = wallNormal;
                }
                if (float.IsPositiveInfinity(nearest)) return position + remaining;
                float travel = Mathf.Clamp(nearest - Skin, 0f, distance);
                position += direction * travel;
                remaining = Vector3.ProjectOnPlane(direction * (distance - travel), normal);
            }
            return position;
        }

        private bool Ignore(Collider other)
        {
            if (other == null || other.attachedRigidbody == _body || other.transform.IsChildOf(_root)) return true;
            if ((_isRobber && MoraeGames.Game.VentPassage.IsPassage(other)) ||
                (_isCop && MoraeGames.Ability.LockdownBlocker.IsBlocker(other)))
            {
                Physics.IgnoreCollision(_collider, other, true);
                return true;
            }
            return Physics.GetIgnoreLayerCollision(_collider.gameObject.layer, other.gameObject.layer) ||
                Physics.GetIgnoreCollision(_collider, other);
        }

        public static Quaternion Turn(Quaternion current, Vector2 input, float degreesPerSecond, float deltaTime)
        {
            if (input.sqrMagnitude < 0.0001f) return current;
            return Quaternion.RotateTowards(current, Quaternion.LookRotation(new Vector3(input.x, 0f, input.y)),
                degreesPerSecond * deltaTime);
        }
    }
}
