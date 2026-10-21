using UnityEngine;
using MoraeGames.Core;
using MoraeGames.Player;
using MoraeGames.Network;
using System.Collections.Generic;

namespace MoraeGames.Ability
{
    [CreateAssetMenu(menuName = "Abilities/Robber/EscapeTrap")]
    public sealed class EscapeTrapData : AbilityDataSO
    {
        public float radius = 2.5f;
        public float slowMultiplier = .45f;
        public override void OnActivate(AbilityHolder holder)
        {
            var trap = EffectPool.Get("EscapeTrap", () => new GameObject("EscapeTrap", typeof(EscapeTrapZone)));
            trap.transform.position = holder.transform.position;
            trap.GetComponent<EscapeTrapZone>().Initialize(radius, slowMultiplier);
            holder.spawnedObjects.Add(trap);
        }
        public override void OnDeactivate(AbilityHolder holder)
        {
            foreach (var trap in holder.spawnedObjects) EffectPool.Release("EscapeTrap", trap);
            holder.spawnedObjects.Clear();
        }
    }

    public sealed class EscapeTrapZone : MonoBehaviour
    {
        private readonly List<PlayerController> _slowed = new List<PlayerController>();
        private float _radius, _multiplier;
        private LineRenderer _ring;
        public void Initialize(float radius, float multiplier)
        {
            _radius = radius; _multiplier = multiplier;
            if (_ring == null) _ring = AbilityFeedback.CreateRing(gameObject, new Color(1, .55f, .08f));
            AbilityFeedback.DrawRing(_ring, transform.position + Vector3.up * .1f, radius);
            if (GetComponent<MoraeGames.Game.LocalVisionSubject>() == null)
                gameObject.AddComponent<MoraeGames.Game.LocalVisionSubject>();
        }
        private void Update()
        {
            if (!NetworkSessionManager.HasStateAuthority) return;
            foreach (var player in PlayerController.Instances)
            {
                if (!(player is CopController)) continue;
                bool inside = (player.transform.position - transform.position).sqrMagnitude < _radius * _radius;
                if (inside && !_slowed.Contains(player)) { player.ApplySpeedModifier(this, _multiplier); _slowed.Add(player); }
            }
            for (int i = _slowed.Count - 1; i >= 0; i--)
                if (_slowed[i] == null || (_slowed[i].transform.position - transform.position).sqrMagnitude >= _radius * _radius)
                { if (_slowed[i] != null) _slowed[i].RemoveSpeedModifier(this); _slowed.RemoveAt(i); }
        }
        private void OnDisable()
        {
            foreach (var player in _slowed) if (player != null) player.RemoveSpeedModifier(this);
            _slowed.Clear();
        }
    }
}
