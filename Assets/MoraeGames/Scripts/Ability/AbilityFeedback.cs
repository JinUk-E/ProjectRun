using UnityEngine;
using MoraeGames.Player;
using MoraeGames.Network;

namespace MoraeGames.Ability
{
    /// <summary>Reusable per-character skill ring; never affects authoritative collisions.</summary>
    [DefaultExecutionOrder(250)]
    public sealed class AbilityFeedback : MonoBehaviour
    {
        private static Material _material;
        private AbilityHolder _holder;
        private NetworkCharacter _network;
        private LineRenderer _ring;
        private float _elapsed;
        private MoraeGames.Game.LocalVisionSubject _vision;
        private void Start() => _vision = GetComponent<MoraeGames.Game.LocalVisionSubject>();
        private void Awake()
        {
            _holder = GetComponent<AbilityHolder>(); _network = GetComponent<NetworkCharacter>();
            var visual = new GameObject("AbilityRange"); visual.transform.SetParent(transform, false);
            _ring = CreateRing(visual, GetComponent<CopController>() != null ? new Color(.1f,.7f,1f) : new Color(1,.55f,.08f));
            _ring.enabled = false;
        }
        public static LineRenderer CreateRing(GameObject owner, Color color)
        {
            if (_material == null) _material = Resources.Load<Material>("AbilityRing");
            var line = owner.AddComponent<LineRenderer>(); line.sharedMaterial = _material;
            line.positionCount = 48; line.loop = true; line.useWorldSpace = true; line.widthMultiplier = .12f;
            line.startColor = line.endColor = color;
            // URP Unlit does not multiply vertex color on every pipeline configuration.
            var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", color); line.SetPropertyBlock(block);
            return line;
        }
        public static void DrawRing(LineRenderer line, Vector3 position, float radius)
        {
            for (int i = 0; i < 48; i++) { float angle = i * Mathf.PI * 2 / 48; line.SetPosition(i, position + new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)) * radius); }
        }
        private void LateUpdate()
        {
            bool active = _holder != null && _holder.IsActive && _holder.VisualEffect == null && (_vision == null || !_vision.Hidden);
            _ring.enabled = active;
            if (!active) { _elapsed = 0; return; }
            _elapsed += Time.deltaTime;
            float radius = _holder.abilityData is AreaScanData scan ? Mathf.Repeat(_elapsed, 1.2f) / 1.2f * scan.scanRadius : 1f + .2f * Mathf.Sin(_elapsed * 12);
            Vector3 position = _network != null && _network.IsSpawned ? _network.PresentedPosition : transform.position;
            DrawRing(_ring, position + Vector3.up * .12f, radius);
        }
    }
}
