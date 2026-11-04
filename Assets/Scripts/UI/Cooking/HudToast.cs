using UnityEngine;

// A HUD banner that pops in, stays for a moment and fades out. Whoever owns it fills in its content before calling Show.
// It fades instead of deactivating so its Update keeps running, and uses unscaled time so a pause can't freeze it half visible.
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class HudToast : MonoBehaviour
{
    private const float PopInSeconds = 0.22f;
    private const float FadeOutSeconds = 0.4f;
    private const float StartScale = 0.7f;
    private const float PopOvershoot = 1.7f;

    private CanvasGroup canvasGroup;
    private float shownAt;
    private float visibleSeconds;
    private bool isShowing;

    public bool IsShowing
    {
        get { return isShowing; }
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    // Cuts the remaining time so the next toast gets its turn, but never before minSeconds so it can still be read.
    public void HurryUp(float minSeconds)
    {
        if (!isShowing)
        {
            return;
        }

        float elapsed = Time.unscaledTime - shownAt;
        visibleSeconds = Mathf.Min(visibleSeconds, Mathf.Max(minSeconds, elapsed + FadeOutSeconds));
    }

    public void Show(float seconds)
    {
        EnsureInitialized();
        shownAt = Time.unscaledTime;
        visibleSeconds = Mathf.Max(PopInSeconds + FadeOutSeconds, seconds);
        isShowing = true;
        transform.SetAsLastSibling();
        Animate(0f);
    }

    private void Update()
    {
        if (isShowing)
        {
            Animate(Time.unscaledTime - shownAt);
        }
    }

    private void Animate(float elapsed)
    {
        if (elapsed >= visibleSeconds)
        {
            canvasGroup.alpha = 0f;
            isShowing = false;
            return;
        }

        float popIn = Mathf.Clamp01(elapsed / PopInSeconds);
        float back = popIn - 1f;
        float eased = 1f + back * back * ((PopOvershoot + 1f) * back + PopOvershoot);
        transform.localScale = Vector3.one * Mathf.LerpUnclamped(StartScale, 1f, eased);

        float fadeIn = popIn;
        float fadeOut = Mathf.Clamp01((visibleSeconds - elapsed) / FadeOutSeconds);
        canvasGroup.alpha = Mathf.Min(fadeIn, fadeOut);
    }

    private void EnsureInitialized()
    {
        if (canvasGroup != null)
        {
            return;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
