using System.Collections.Generic;
using UnityEngine;
using MoraeGames.Core;
using MoraeGames.Network;

namespace MoraeGames.Game
{
    /// <summary>Interior boundary, persistent burglary alarm and police investigation destination.</summary>
    public sealed class HeistBuilding : MonoBehaviour
    {
        public static readonly List<HeistBuilding> Instances = new List<HeistBuilding>();
        public string displayName;
        public bool guarded;
        public LootSpot loot;
        public BoxCollider[] walls;
        public Renderer beacon;
        public float alarmDelay = 1.5f;
        public float alarmDuration = 14f;
        public float AlarmRemaining { get; private set; }
        public bool AlarmActive => AlarmRemaining > 0;
        private float _tampering;
        private AudioSource _siren;
        private AudioClip _clip;

        private void OnEnable() => Instances.Add(this);
        private void OnDisable() { Instances.Remove(this); if (_siren != null) _siren.Stop(); }
        private void Start()
        {
            if (!guarded) return;
            _siren = gameObject.AddComponent<AudioSource>();
            _siren.playOnAwake = false; _siren.loop = true; _siren.spatialBlend = 1;
            _siren.minDistance = 4; _siren.maxDistance = 38; _siren.volume = .22f;
            _siren.rolloffMode = AudioRolloffMode.Linear;
            const int rate = 22050;
            var samples = new float[rate];
            float phase = 0;
            for (int i = 0; i < rate; i++)
            {
                phase += 2 * Mathf.PI * (i < rate / 2 ? 660 : 880) / rate;
                samples[i] = Mathf.Sin(phase) * .45f;
            }
            _clip = AudioClip.Create("Building siren", rate, 1, rate, false);
            _clip.SetData(samples, 0); _siren.clip = _clip;
        }
        private void OnDestroy() { if (_clip != null) Destroy(_clip); }

        private void Update()
        {
            if (NetworkSessionManager.HasStateAuthority)
                AlarmRemaining = Mathf.Max(0, AlarmRemaining - Time.deltaTime);
            if (beacon != null)
                RendererTint.SetColor(beacon, AlarmActive
                    ? (Mathf.Repeat(Time.time * 3, 1) > .5f ? Color.red : Color.yellow)
                    : guarded ? new Color(.8f, .25f, .08f) : new Color(.15f, .65f, .85f));
            if (_siren != null)
            {
                bool audible = AlarmActive && NetworkCharacter.MatchAllowsActions && !InGameManager.IsPaused;
                if (audible && !_siren.isPlaying) _siren.Play();
                else if (!audible && _siren.isPlaying) _siren.Stop();
            }
        }

        /// <summary>Requires entering the room; touching the outside wall never permits stealing.</summary>
        public bool Contains(Vector3 position)
        {
            Vector3 local = transform.InverseTransformPoint(position);
            return Mathf.Abs(local.x) < 4.65f && Mathf.Abs(local.z) < 4.65f && Mathf.Abs(local.y) < 3;
        }

        /// <summary>Tampering accumulates across aborted attempts. An alarm persists after escape.</summary>
        public void RecordTampering(float seconds)
        {
            if (!guarded || !NetworkSessionManager.HasStateAuthority) return;
            _tampering += seconds;
            if (_tampering < alarmDelay) return;
            bool announce = !AlarmActive;
            AlarmRemaining = alarmDuration;
            if (announce && GameManager.Instance != null)
                GameManager.Instance.BroadcastSystemMessage(displayName + " 경보! 경찰 출동", Color.yellow);
        }

        /// <summary>Mirrors the server timer for light and audio presentation only.</summary>
        public void ApplyRemoteAlarm(float seconds)
        {
            if (!NetworkSessionManager.HasStateAuthority) AlarmRemaining = Mathf.Max(0, seconds);
        }

        /// <summary>Returns the nearest alarm location, never a robber's hidden position.</summary>
        public static HeistBuilding NearestAlarm(Vector3 position)
        {
            HeistBuilding nearest = null;
            float distance = float.MaxValue;
            foreach (var building in Instances)
            {
                if (!building.AlarmActive) continue;
                float candidate = (building.transform.position - position).sqrMagnitude;
                if (candidate < distance) { nearest = building; distance = candidate; }
            }
            return nearest;
        }

        /// <summary>Walls block detection, theft and arrests. Door openings remain transparent.</summary>
        public static bool HasSight(Vector3 from, Vector3 to)
        {
            from.y = to.y = 1;
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .001f) return true;
            var ray = new Ray(from, delta.normalized);
            foreach (var building in Instances)
                foreach (var wall in building.walls)
                    if (wall != null && wall.enabled && wall.Raycast(ray, out _, delta.magnitude)) return false;
            return true;
        }
    }
}
