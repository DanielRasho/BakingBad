using System.Collections.Generic;
using UnityEngine;

// Marks an object (and its children) as one the camera may fade when it hides the player, like the cell entrances.
// Anything without this marker stays opaque, so regular walls, props and characters never turn see-through.
[DisallowMultipleComponent]
public class OcclusionFadeTarget : MonoBehaviour
{
    private static readonly List<OcclusionFadeTarget> activeTargets = new List<OcclusionFadeTarget>();

    private Renderer[] renderers;

    public static IReadOnlyList<OcclusionFadeTarget> ActiveTargets
    {
        get { return activeTargets; }
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
