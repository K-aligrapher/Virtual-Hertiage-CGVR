using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VirtualHeritage
{
    public enum HeritageTargetKind { Temple, Element, Teleport }

    public sealed class HeritageTarget : MonoBehaviour
    {
        public HeritageTargetKind kind;
        public int index;
        public HeritageApp app;
        Renderer[] renderers;
        MaterialPropertyBlock block;
        bool highlighted;

        public void Configure(HeritageApp owner, HeritageTargetKind targetKind, int targetIndex)
        {
            app = owner; kind = targetKind; index = targetIndex;
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            var interactable = gameObject.AddComponent<XRSimpleInteractable>();
            interactable.hoverEntered.AddListener(_ => Highlight(true));
            interactable.hoverExited.AddListener(_ => Highlight(false));
            interactable.selectEntered.AddListener(_ => Activate());
        }

        public void Highlight(bool value)
        {
            if (highlighted == value || renderers == null) return;
            highlighted = value;
            foreach (var renderer in renderers)
            {
                if (!renderer) continue;
                if (value)
                {
                    block.SetColor("_BaseColor", new Color(.35f,.85f,.7f)); renderer.SetPropertyBlock(block);
                }
                else renderer.SetPropertyBlock(null);
            }
        }

        public void Activate()
        {
            if (!app || app.Transitioning) return;
            switch (kind)
            {
                case HeritageTargetKind.Temple: app.OpenTemple(index); break;
                case HeritageTargetKind.Element: app.SelectElement(index, transform); break;
                case HeritageTargetKind.Teleport: app.rig.Teleport(transform.position); break;
            }
        }
    }
}
