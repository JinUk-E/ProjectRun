using UnityEngine;
using MoraeGames.Core;
using MoraeGames.Player;

namespace MoraeGames.Ability
{
    [CreateAssetMenu(menuName = "Abilities/Robber/Disguise")]
    public class DisguiseData : AbilityDataSO
    {
        [Header("Disguise")]
        public Color disguiseColor = new Color(0.3f, 0.3f, 0.9f);

        public override void OnActivate(AbilityHolder holder)
        {
            // Flag-based concealment: hides from AI detection without touching the tag.
            // (Swapping the tag broke trigger-zone enter/exit symmetry and made the
            // robber unarrestable on touch, which the spec does not intend.)
            RobberController robber = holder.GetComponent<RobberController>();
            if (robber != null) robber.IsDisguised = true;

            // Visual change (MPB tint — no material instantiation, no color snapshot needed)
            Transform body = holder.transform.Find("Body");
            if (body != null)
                RendererTint.SetColor(body.GetComponentInChildren<Renderer>(), disguiseColor);

            Debug.Log($"[Disguise] {holder.gameObject.name} is now disguised!");
        }

        public override void OnDeactivate(AbilityHolder holder)
        {
            RobberController robber = holder.GetComponent<RobberController>();
            if (robber != null) robber.IsDisguised = false;

            // Restore visual: clearing the tint reveals the shared material's own color
            Transform body = holder.transform.Find("Body");
            if (body != null)
                RendererTint.ClearColor(body.GetComponentInChildren<Renderer>());

            Debug.Log($"[Disguise] {holder.gameObject.name} disguise expired!");
        }
    }
}

