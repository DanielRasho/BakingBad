using System.Collections.Generic;
using UnityEngine;

// Marks an object and its children as surfaces the camera may fade when they hide the player.
[DisallowMultipleComponent]
public class OcclusionFadeTarget : MonoBehaviour
{
    private static readonly List<OcclusionFadeTarget> activeTargets = new List<OcclusionFadeTarget>();

    [Tooltip("Fade only the renderer that blocks the player instead of every renderer below this object.")]
    [SerializeField] private bool fadeRenderersIndividually;

    private Renderer[] renderers;

    public static IReadOnlyList<OcclusionFadeTarget> ActiveTargets
    {
        get { return activeTargets; }
    }

    public bool FadeRenderersIndividually
    {
        get { return fadeRenderersIndividually; }
    }

    // Checked by mesh bounds rather than colliders: the entrance colliders only cover one post of the frame.
    public Renderer[] Renderers
    {
        get
        {
            if (renderers == null)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }

            return renderers;
        }
    }

    private void OnEnable()
    {
        activeTargets.Add(this);
    }

    private void OnDisable()
    {
        activeTargets.Remove(this);
    }
}
