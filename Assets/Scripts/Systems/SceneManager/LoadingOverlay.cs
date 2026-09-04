using System;
using System.Collections;
using UnityEngine;

public class LoadingOverlay : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeInTime = 1f;
    [SerializeField] private float fadeOutTime = 1f;

    private void Start()
    {
        gameObject.SetActive(true);
    }

    public IEnumerator FadeIn()
    {
        this.gameObject.SetActive(true);
        yield return FadeTo(1f, fadeInTime);
    }

    public IEnumerator FadeOut()
    {
        yield return FadeTo(0f, fadeOutTime);
        this.gameObject.SetActive(false);
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }
        canvasGroup.alpha = targetAlpha;
    }
}
