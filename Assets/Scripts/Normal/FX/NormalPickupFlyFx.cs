using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(Image))]
public sealed class NormalPickupFlyFx : MonoBehaviour
{
    public Action<NormalPickupFlyFx> OnFinished;

    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Image _image;
    private Coroutine _co;
    private bool _isFinishing;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _image = GetComponent<Image>();

        _image.raycastTarget = false;
        _canvasGroup.alpha = 0f;
    }

    public void Play(
        Sprite sprite,
        Vector2 from,
        Vector2 to,
        float duration,
        float arcHeight,
        float startScale,
        float endScale)
    {
        gameObject.SetActive(true);
        _isFinishing = false;

        if (_co != null)
            StopCoroutine(_co);

        _image.sprite = sprite;
        _image.enabled = sprite != null;
        _image.color = Color.white;
        _image.preserveAspect = true;

        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _rectTransform.anchoredPosition = from;
        _rectTransform.localScale = Vector3.one * startScale;
        _rectTransform.localRotation = Quaternion.identity;

        _co = StartCoroutine(CoPlay(from, to, duration, arcHeight, startScale, endScale));
    }

    private IEnumerator CoPlay(
        Vector2 from,
        Vector2 to,
        float duration,
        float arcHeight,
        float startScale,
        float endScale)
    {
        duration = Mathf.Max(0.12f, duration);
        _canvasGroup.alpha = 1f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);

            Vector2 pos = Vector2.Lerp(from, to, EaseInOutCubic(p));
            pos.y += Mathf.Sin(p * Mathf.PI) * arcHeight;

            float scale = Mathf.Lerp(startScale, endScale, EaseOutCubic(p));
            float alpha = p < 0.78f ? 1f : Mathf.Lerp(1f, 0f, (p - 0.78f) / 0.22f);

            _rectTransform.anchoredPosition = pos;
            _rectTransform.localScale = Vector3.one * scale;
            _rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 20f, p));
            _canvasGroup.alpha = alpha;

            yield return null;
        }

        Finish();
    }

    private void OnDisable()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }

        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;
    }

    private void Finish()
    {
        if (_isFinishing)
            return;

        _isFinishing = true;
        _co = null;

        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;

        gameObject.SetActive(false);
        OnFinished?.Invoke(this);
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }
}