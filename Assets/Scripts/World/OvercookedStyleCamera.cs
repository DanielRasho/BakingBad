using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OvercookedStyleCamera : MonoBehaviour
{
    public enum CameraAngle
    {
        Angle45 = 45,
        Angle60 = 60
    }

    [SerializeField] private Transform target;
    [SerializeField] private CameraAngle angle = CameraAngle.Angle45;

    [Header("Player Occlusion (only objects with OcclusionFadeTarget)")]
    [SerializeField] private bool fadeOccludingObjects = true;
    [SerializeField, Range(0.05f, 1f)] private float occludedAlpha = 0.22f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float sphereCastRadius = 0.18f;
    [SerializeField] private bool fadeNearbyTallObjects = true;
    [SerializeField, Min(0.1f)] private float nearbyFadeRadius = 1.15f;
    [SerializeField, Min(0f)] private float nearbyMinimumHeight = 0.65f;

    private const float CameraDistance = 7f;
    private const float FieldOfView = 40f;
    private static readonly Vector3 LookAtOffset = new Vector3(0f, 0.55f, 0.4f);

    private Camera cachedCamera;
    private Shader occlusionFadeShader;
    private readonly HashSet<Renderer> currentOccluders = new HashSet<Renderer>();
    private readonly Dictionary<Renderer, FadeState> fadeStates = new Dictionary<Renderer, FadeState>();
    private readonly List<Renderer> fadeStateBuffer = new List<Renderer>();

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int FadeAlphaId = Shader.PropertyToID("_FadeAlpha");
    private static readonly int CullId = Shader.PropertyToID("_Cull");

    private sealed class FadeState
    {
        public Material[] OriginalMaterials;
        public Material[] FadeMaterials;
        public float Alpha = 1f;
    }

    public Transform Target
    {
        get { return target; }
        set { target = value; }
    }

    private void Awake()
    {
        cachedCamera = GetComponent<Camera>();
        occlusionFadeShader = Resources.Load<Shader>("Shaders/OcclusionFade");
        ConfigureCamera();
    }

    private void Start()
    {
        SnapToTarget();
    }

    private void LateUpdate()
    {
        ConfigureCamera();

        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = GetDesiredPosition();
        transform.position = desiredPosition;
        LookAtTarget();
        UpdatePlayerOcclusion();
    }

    private void OnDisable()
    {
        RestoreAllOccluders();
    }

    private void OnDestroy()
    {
        RestoreAllOccluders();
    }

    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        transform.position = GetDesiredPosition();
        LookAtTarget();
        ConfigureCamera();
    }

    private Vector3 GetDesiredPosition()
    {
        Quaternion rotation = Quaternion.Euler((float)angle, 0f, 0f);
        Vector3 focusPoint = target.position + LookAtOffset;
        return focusPoint - rotation * Vector3.forward * CameraDistance;
    }

    private void LookAtTarget()
    {
        Vector3 lookTarget = target.position + LookAtOffset;
        Vector3 direction = lookTarget - transform.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }

    private void ConfigureCamera()
    {
        if (cachedCamera == null)
        {
            cachedCamera = GetComponent<Camera>();
        }

        cachedCamera.orthographic = false;
        cachedCamera.usePhysicalProperties = false;
        cachedCamera.fieldOfView = FieldOfView;
        cachedCamera.nearClipPlane = 0.05f;
        cachedCamera.farClipPlane = 100f;
    }

    private void UpdatePlayerOcclusion()
    {
        currentOccluders.Clear();

        if (fadeOccludingObjects && occlusionFadeShader != null && target != null)
        {
            Vector3 focusPoint = target.position + LookAtOffset;
            Vector3 direction = focusPoint - transform.position;
            float distance = direction.magnitude;
            if (distance > 0.01f)
            {
                Ray viewRay = new Ray(transform.position, direction / distance);
                IReadOnlyList<OcclusionFadeTarget> fadeTargets = OcclusionFadeTarget.ActiveTargets;
                for (int i = 0; i < fadeTargets.Count; i++)
                {
                    if (IsHidingPlayer(fadeTargets[i], viewRay, distance))
                    {
                        AddOccludingRenderers(fadeTargets[i]);
                    }
                }
            }
        }

        UpdateFadeStates();
    }

    // Only objects marked with OcclusionFadeTarget (the cell entrances) are checked; everything else stays opaque.
    private bool IsHidingPlayer(OcclusionFadeTarget fadeTarget, Ray viewRay, float distance)
    {
        Vector3 nearbyCenter = target.position + Vector3.up * 0.5f;
        float minimumWorldHeight = target.position.y + nearbyMinimumHeight;
        Renderer[] renderers = fadeTarget.Renderers;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!IsValidOccluder(renderers[i]))
            {
                continue;
            }

            Bounds bounds = renderers[i].bounds;
            if (fadeNearbyTallObjects && bounds.max.y >= minimumWorldHeight && bounds.SqrDistance(nearbyCenter) <= nearbyFadeRadius * nearbyFadeRadius)
            {
                return true;
            }

            bounds.Expand(sphereCastRadius * 2f);
            float hitDistance;
            if (bounds.IntersectRay(viewRay, out hitDistance) && hitDistance <= distance)
            {
                return true;
            }
        }

        return false;
    }

    private void AddOccludingRenderers(OcclusionFadeTarget fadeTarget)
    {
        Renderer[] renderers = fadeTarget.Renderers;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (IsValidOccluder(renderers[i]))
            {
                currentOccluders.Add(renderers[i]);
            }
        }
    }

    private bool IsValidOccluder(Renderer candidate)
    {
        return candidate != null &&
               candidate.enabled &&
               candidate.gameObject.activeInHierarchy &&
               !candidate.transform.IsChildOf(target);
    }

    private void UpdateFadeStates()
    {
        foreach (Renderer renderer in currentOccluders)
        {
            EnsureFadeState(renderer);
        }

        fadeStateBuffer.Clear();
        foreach (KeyValuePair<Renderer, FadeState> pair in fadeStates)
        {
            fadeStateBuffer.Add(pair.Key);
        }

        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration);
        for (int i = 0; i < fadeStateBuffer.Count; i++)
        {
            Renderer renderer = fadeStateBuffer[i];
            FadeState state;
            if (!fadeStates.TryGetValue(renderer, out state) || renderer == null)
            {
                RemoveFadeState(renderer, state, false);
                continue;
            }

            float targetAlpha = currentOccluders.Contains(renderer) ? occludedAlpha : 1f;
            state.Alpha = Mathf.MoveTowards(state.Alpha, targetAlpha, step);
            SetFadeAlpha(state, state.Alpha);

            if (targetAlpha >= 1f && state.Alpha >= 0.999f)
            {
                RemoveFadeState(renderer, state, true);
            }
        }
    }

    private void EnsureFadeState(Renderer renderer)
    {
        if (fadeStates.ContainsKey(renderer))
        {
            return;
        }

        Material[] originals = renderer.sharedMaterials;
        if (originals == null || originals.Length == 0)
        {
            return;
        }

        Material[] faded = new Material[originals.Length];
        for (int i = 0; i < originals.Length; i++)
        {
            faded[i] = CreateFadeMaterial(originals[i], renderer.name, i);
        }

        FadeState state = new FadeState
        {
            OriginalMaterials = originals,
            FadeMaterials = faded
        };

        fadeStates.Add(renderer, state);
        renderer.sharedMaterials = faded;
    }

    private Material CreateFadeMaterial(Material original, string rendererName, int materialIndex)
    {
        Material faded = new Material(occlusionFadeShader)
        {
            name = rendererName + "_OcclusionFade_" + materialIndex,
            hideFlags = HideFlags.HideAndDontSave
        };

        if (original == null)
        {
            return faded;
        }

        Texture texture = null;
        Vector2 scale = Vector2.one;
        Vector2 offset = Vector2.zero;
        if (original.HasProperty(BaseMapId))
        {
            texture = original.GetTexture(BaseMapId);
            scale = original.GetTextureScale("_BaseMap");
            offset = original.GetTextureOffset("_BaseMap");
        }
        else if (original.HasProperty(MainTexId))
        {
            texture = original.GetTexture(MainTexId);
            scale = original.GetTextureScale("_MainTex");
            offset = original.GetTextureOffset("_MainTex");
        }

        if (texture != null)
        {
            faded.SetTexture(BaseMapId, texture);
            faded.SetTextureScale("_BaseMap", scale);
            faded.SetTextureOffset("_BaseMap", offset);
        }

        Color color = Color.white;
        if (original.HasProperty(BaseColorId))
        {
            color = original.GetColor(BaseColorId);
        }
        else if (original.HasProperty(ColorId))
        {
            color = original.GetColor(ColorId);
        }

        faded.SetColor(BaseColorId, color);
        if (original.HasProperty(CullId))
        {
            faded.SetFloat(CullId, original.GetFloat(CullId));
        }

        return faded;
    }

    private static void SetFadeAlpha(FadeState state, float alpha)
    {
        if (state?.FadeMaterials == null)
        {
            return;
        }

        for (int i = 0; i < state.FadeMaterials.Length; i++)
        {
            if (state.FadeMaterials[i] != null)
            {
                state.FadeMaterials[i].SetFloat(FadeAlphaId, alpha);
            }
        }
    }

    private void RemoveFadeState(Renderer renderer, FadeState state, bool restoreMaterials)
    {
        if (state == null)
        {
            if (!ReferenceEquals(renderer, null))
            {
                fadeStates.Remove(renderer);
            }
            return;
        }

        if (restoreMaterials && renderer != null)
        {
            renderer.sharedMaterials = state.OriginalMaterials;
        }

        if (state.FadeMaterials != null)
        {
            for (int i = 0; i < state.FadeMaterials.Length; i++)
            {
                if (state.FadeMaterials[i] != null)
                {
                    Destroy(state.FadeMaterials[i]);
                }
            }
        }

        if (!ReferenceEquals(renderer, null))
        {
            fadeStates.Remove(renderer);
        }
    }

    private void RestoreAllOccluders()
    {
        fadeStateBuffer.Clear();
        foreach (KeyValuePair<Renderer, FadeState> pair in fadeStates)
        {
            fadeStateBuffer.Add(pair.Key);
        }

        for (int i = 0; i < fadeStateBuffer.Count; i++)
        {
            Renderer renderer = fadeStateBuffer[i];
            if (fadeStates.TryGetValue(renderer, out FadeState state))
            {
                RemoveFadeState(renderer, state, true);
            }
        }

        fadeStates.Clear();
        currentOccluders.Clear();
    }

    private void OnValidate()
    {
        cachedCamera = GetComponent<Camera>();
        ConfigureCamera();

        if (target != null && !Application.isPlaying)
        {
            SnapToTarget();
        }
    }
}
