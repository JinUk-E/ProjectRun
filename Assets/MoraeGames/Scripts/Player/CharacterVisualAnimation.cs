using UnityEngine;
using MoraeGames.Network;

namespace MoraeGames.Player
{
    /// <summary>Animates the visual only, using presented movement on owners and replicated movement on peers.</summary>
    [DefaultExecutionOrder(50)]
    public sealed class CharacterVisualAnimation : MonoBehaviour
    {
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int Arrested = Animator.StringToHash("Arrested");
        private Animator _animator;
        private NetworkCharacter _network;
        private RobberController _robber;
        private Transform _actor;
        private Vector3 _previous;
        private bool _initialized;
        private float _smoothedSpeed;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            _actor = transform.parent;
            if (_actor == null) _actor = transform;
            _network = _actor.GetComponent<NetworkCharacter>();
            _robber = _actor.GetComponent<RobberController>();
            if (_animator != null) _animator.applyRootMotion = false;
        }

        private void OnEnable() { _initialized = false; _smoothedSpeed = 0f; }

        private void LateUpdate()
        {
            if (_animator == null || Time.deltaTime <= 0f) return;
            Vector3 position = _network != null && _network.IsSpawned ? _network.PresentedPosition : _actor.position;
            Vector3 delta = position - _previous;
            delta.y = 0f;
            float speed = _initialized && delta.sqrMagnitude < 9f ? delta.magnitude / Time.deltaTime : 0f;
            _previous = position;
            _initialized = true;
            bool arrested = _robber != null && _robber.IsArrested;
            // Fixed-step movement arrives in bursts between render frames. Smooth the
            // measured speed BEFORE clamping; otherwise high frame rates dilute a run to idle.
            _smoothedSpeed = arrested || delta.sqrMagnitude >= 9f ? 0f :
                Mathf.Lerp(_smoothedSpeed, speed, 1f - Mathf.Exp(-12f * Time.deltaTime));
            _animator.SetFloat(Speed, Mathf.Clamp01(_smoothedSpeed / 5f));
            _animator.SetBool(Arrested, arrested);
        }
    }
}
