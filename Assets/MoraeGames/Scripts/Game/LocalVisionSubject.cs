using System.Collections.Generic;
using UnityEngine;

namespace MoraeGames.Game
{
    /// <summary>Local presentation only: hides actors without disabling their simulation or colliders.</summary>
    public sealed class LocalVisionSubject : MonoBehaviour
    {
        public static readonly List<LocalVisionSubject> Instances = new List<LocalVisionSubject>();
        private Renderer[] _renderers;
        private bool[] _previous;
        private bool _hidden;
        public bool Hidden => _hidden;
        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _previous = new bool[_renderers.Length];
        }
        private void OnEnable() => Instances.Add(this);
        private void OnDisable() { SetHidden(false); Instances.Remove(this); }
        public void SetHidden(bool hidden)
        {
            if (_hidden == hidden) return;
            _hidden = hidden;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null) continue;
                if (hidden) { _previous[i] = renderer.forceRenderingOff; renderer.forceRenderingOff = true; }
                else renderer.forceRenderingOff = _previous[i];
            }
        }
    }
}
