using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(Image))]
public sealed class NormalLineClearFx : MonoBehaviour
{
    public enum Axis
    {
        Row = 0,
        Column = 1
    }

    private sealed class SparkleRuntime
    {
        public RectTransform rect;
        public Image image;
        public Vector2 startPos;
        public Vector2 endPos;
        public float delay;
        public float duration;
        public float startScale;
        public float endScale;
        public float startRotation;
        public float rotationSpeed;
        public Color color;
    }

    private static Sprite _whiteSprite;
    private static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite == null)
            {
                _whiteSprite = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    1f);
            }

            return _whiteSprite;
        }
    }

    // ===== FX Core =====
    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;

    private Image _glowImage;
    private RectTransform _coreRect;
    private Image _coreImage;

    private RectTransform _sweepRect;
    private Image _sweepImage;

    private RectTransform _flashRect;
    private Image _flashImage;

    private Coroutine _playRoutine;

    private Vector2 _sweepFrom;
    private Vector2 _sweepTo;

    // ===== Sparkle Settings =====
    private const int SparkleCount = 18;
    private const float SparkleMinDuration = 0.35f;
    private const float SparkleMaxDuration = 0.65f;
    private const float SparkleMaxDelay = 0.12f;
    private const float SparkleMinSize = 18f;
    private const float SparkleMaxSize = 34f;
    private const float SparkleDriftMainMin = 34f;
    private const float SparkleDriftMainMax = 90f;
    private const float SparkleDriftCrossMin = 14f;
    private const float SparkleDriftCrossMax = 34f;

    public Action<NormalLineClearFx> OnFinished;

    private bool _isFinishing;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _glowImage = GetComponent<Image>();

        _glowImage.raycastTarget = false;
        _glowImage.sprite = WhiteSprite;
        _glowImage.type = Image.Type.Simple;
        _glowImage.color = Color.clear;

        _coreRect = CreateChildImage("Core", out _coreImage);
        _sweepRect = CreateChildImage("Sweep", out _sweepImage);
        _flashRect = CreateChildImage("Flash", out _flashImage);

        _canvasGroup.alpha = 0f;
    }

    public void Play(
        Axis axis,
        int lineIndex,
        int boardSize,
        Vector2 cellSize,
        Vector2 spacing,
        Color color,
        float holdDuration,
        float fadeOutDuration,
        float thicknessScale)
    {
        gameObject.SetActive(true);
        _isFinishing = false;

        if (_playRoutine != null)
            StopCoroutine(_playRoutine);

        ApplyLayout(axis, lineIndex, boardSize, cellSize, spacing, thicknessScale);
        ApplyColors(color);

        _playRoutine = StartCoroutine(CoPlay(axis, color, holdDuration, fadeOutDuration));
    }

    private RectTransform CreateChildImage(string childName, out Image image)
    {
        GameObject go = new GameObject(childName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = WhiteSprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;

        return rt;
    }

    private void ApplyColors(Color baseColor)
    {
        Color glow = baseColor;
        glow.a = 0.30f;

        Color core = baseColor;
        core.a = 0.95f;

        Color flash = baseColor;
        flash.a = 0.62f;

        _glowImage.color = glow;
        _coreImage.color = core;
        _flashImage.color = flash;
        _sweepImage.color = new Color(1f, 1f, 1f, 0.95f);
    }

    private void ApplyLayout(
        Axis axis,
        int lineIndex,
        int boardSize,
        Vector2 cellSize,
        Vector2 spacing,
        float thicknessScale)
    {
        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _rectTransform.localRotation = Quaternion.identity;
        _rectTransform.localScale = new Vector3(0.92f, 0.92f, 1f);

        float stepX = cellSize.x + spacing.x;
        float stepY = cellSize.y + spacing.y;

        float boardWidth = boardSize * cellSize.x + (boardSize - 1) * spacing.x;
        float boardHeight = boardSize * cellSize.y + (boardSize - 1) * spacing.y;

        float startX = -((boardSize - 1) * stepX) * 0.5f;
        float startY = ((boardSize - 1) * stepY) * 0.5f;

        float glowThickness;
        float coreThickness;
        float flashThickness;
        float sweepLength;

        if (axis == Axis.Row)
        {
            glowThickness = Mathf.Max(18f, cellSize.y * thicknessScale * 1.95f);
            coreThickness = Mathf.Max(6f, cellSize.y * thicknessScale * 0.82f);
            flashThickness = Mathf.Max(10f, cellSize.y * thicknessScale * 1.22f);
            sweepLength = Mathf.Max(cellSize.x * 1.25f, 96f);

            _rectTransform.sizeDelta = new Vector2(boardWidth, glowThickness);
            _rectTransform.anchoredPosition = new Vector2(0f, startY - lineIndex * stepY);

            _coreRect.sizeDelta = new Vector2(boardWidth, coreThickness);
            _coreRect.anchoredPosition = Vector2.zero;

            _flashRect.sizeDelta = new Vector2(boardWidth, flashThickness);
            _flashRect.anchoredPosition = Vector2.zero;

            _sweepRect.sizeDelta = new Vector2(sweepLength, glowThickness * 1.08f);
            _sweepFrom = new Vector2(-boardWidth * 0.5f - sweepLength * 0.6f, 0f);
            _sweepTo = new Vector2(boardWidth * 0.5f + sweepLength * 0.6f, 0f);
        }
        else
        {
            glowThickness = Mathf.Max(18f, cellSize.x * thicknessScale * 1.95f);
            coreThickness = Mathf.Max(6f, cellSize.x * thicknessScale * 0.82f);
            flashThickness = Mathf.Max(10f, cellSize.x * thicknessScale * 1.22f);
            sweepLength = Mathf.Max(cellSize.y * 1.25f, 96f);

            _rectTransform.sizeDelta = new Vector2(glowThickness, boardHeight);
            _rectTransform.anchoredPosition = new Vector2(startX + lineIndex * stepX, 0f);

            _coreRect.sizeDelta = new Vector2(coreThickness, boardHeight);
            _coreRect.anchoredPosition = Vector2.zero;

            _flashRect.sizeDelta = new Vector2(flashThickness, boardHeight);
            _flashRect.anchoredPosition = Vector2.zero;

            _sweepRect.sizeDelta = new Vector2(glowThickness * 1.08f, sweepLength);
            _sweepFrom = new Vector2(0f, boardHeight * 0.5f + sweepLength * 0.6f);
            _sweepTo = new Vector2(0f, -boardHeight * 0.5f - sweepLength * 0.6f);
        }

        _sweepRect.anchoredPosition = _sweepFrom;
    }

    private IEnumerator CoPlay(Axis axis, Color baseColor, float holdDuration, float fadeOutDuration)
    {
        float fadeInDuration = 0.06f;
        float sweepDuration = Mathf.Max(0.18f, holdDuration + fadeOutDuration + 0.06f);
        float totalDuration = fadeInDuration + holdDuration + fadeOutDuration;

        List<SparkleRuntime> sparkles = SpawnSparkles(axis, baseColor);

        _canvasGroup.alpha = 0f;
        _rectTransform.localScale = new Vector3(0.92f, 0.92f, 1f);

        _coreImage.canvasRenderer.SetAlpha(0f);
        _flashImage.canvasRenderer.SetAlpha(0f);
        _sweepImage.canvasRenderer.SetAlpha(0f);

        _coreImage.CrossFadeAlpha(1f, fadeInDuration, true);
        _flashImage.CrossFadeAlpha(1f, fadeInDuration * 0.8f, true);
        _sweepImage.CrossFadeAlpha(1f, fadeInDuration * 0.5f, true);

        float t = 0f;
        while (t < totalDuration)
        {
            t += Time.unscaledDeltaTime;

            float alpha;
            if (t <= fadeInDuration)
            {
                float p = Mathf.Clamp01(t / fadeInDuration);
                alpha = Mathf.Lerp(0f, 1f, p);
                _rectTransform.localScale = Vector3.Lerp(
                    new Vector3(0.92f, 0.92f, 1f),
                    new Vector3(1.02f, 1.02f, 1f),
                    p);
            }
            else if (t <= fadeInDuration + holdDuration)
            {
                alpha = 1f;
                _rectTransform.localScale = Vector3.Lerp(
                    _rectTransform.localScale,
                    Vector3.one,
                    Time.unscaledDeltaTime * 12f);
            }
            else
            {
                float p = Mathf.Clamp01((t - fadeInDuration - holdDuration) / Mathf.Max(0.0001f, fadeOutDuration));
                alpha = Mathf.Lerp(1f, 0f, p);
                _rectTransform.localScale = Vector3.Lerp(
                    Vector3.one,
                    new Vector3(1.06f, 1.06f, 1f),
                    p);

                _flashImage.color = new Color(_flashImage.color.r, _flashImage.color.g, _flashImage.color.b, Mathf.Lerp(0.62f, 0f, p));
                _coreImage.color = new Color(_coreImage.color.r, _coreImage.color.g, _coreImage.color.b, Mathf.Lerp(0.95f, 0.20f, p));
                _sweepImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.95f, 0f, p));
            }

            _canvasGroup.alpha = alpha;

            float sweepP = Mathf.Clamp01(t / sweepDuration);
            _sweepRect.anchoredPosition = Vector2.Lerp(_sweepFrom, _sweepTo, sweepP);

            float pulse = 1f + Mathf.Sin(t * 26f) * 0.035f;
            if (axis == Axis.Row)
            {
                _coreRect.localScale = new Vector3(1f, pulse, 1f);
                _flashRect.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 18f) * 0.05f, 1f);
            }
            else
            {
                _coreRect.localScale = new Vector3(pulse, 1f, 1f);
                _flashRect.localScale = new Vector3(1f + Mathf.Sin(t * 18f) * 0.05f, 1f, 1f);
            }

            UpdateSparkles(sparkles, t);

            yield return null;
        }

        Finish();

        //_canvasGroup.alpha = 0f;
        //Destroy(gameObject);
    }

    private List<SparkleRuntime> SpawnSparkles(Axis axis, Color baseColor)
    {
        var result = new List<SparkleRuntime>(SparkleCount);

        float width = _rectTransform.sizeDelta.x;
        float height = _rectTransform.sizeDelta.y;

        for (int i = 0; i < SparkleCount; i++)
        {
            GameObject go = new GameObject("Sparkle", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            go.transform.SetAsLastSibling();

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.sprite = WhiteSprite;
            img.type = Image.Type.Simple;

            float size = UnityEngine.Random.Range(SparkleMinSize, SparkleMaxSize);
            rt.sizeDelta = new Vector2(size, size);

            Vector2 startPos;
            Vector2 endPos;

            if (axis == Axis.Row)
            {
                float startX = UnityEngine.Random.Range(-width * 0.46f, width * 0.46f);
                float startY = UnityEngine.Random.Range(-height * 0.15f, height * 0.15f);

                float driftX = UnityEngine.Random.Range(-SparkleDriftCrossMin, SparkleDriftCrossMax) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
                float driftY = UnityEngine.Random.Range(SparkleDriftMainMin, SparkleDriftMainMax) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);

                startPos = new Vector2(startX, startY);
                endPos = startPos + new Vector2(driftX, driftY);
            }
            else
            {
                float startX = UnityEngine.Random.Range(-width * 0.15f, width * 0.15f);
                float startY = UnityEngine.Random.Range(-height * 0.46f, height * 0.46f);

                float driftX = UnityEngine.Random.Range(SparkleDriftMainMin, SparkleDriftMainMax) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
                float driftY = UnityEngine.Random.Range(-SparkleDriftCrossMin, SparkleDriftCrossMax) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);

                startPos = new Vector2(startX, startY);
                endPos = startPos + new Vector2(driftX, driftY);
            }

            Color sparkleColor = Color.Lerp(baseColor, Color.white, UnityEngine.Random.Range(0.70f, 0.95f));
            sparkleColor.a = UnityEngine.Random.Range(0.95f, 1f);
            img.color = new Color(sparkleColor.r, sparkleColor.g, sparkleColor.b, 0f);

            var sparkle = new SparkleRuntime
            {
                rect = rt,
                image = img,
                startPos = startPos,
                endPos = endPos,
                delay = UnityEngine.Random.Range(0f, SparkleMaxDelay),
                duration = UnityEngine.Random.Range(SparkleMinDuration, SparkleMaxDuration),
                startScale = UnityEngine.Random.Range(0.65f, 0.95f),
                endScale = UnityEngine.Random.Range(1.15f, 1.7f),
                startRotation = UnityEngine.Random.Range(0f, 360f),
                rotationSpeed = UnityEngine.Random.Range(-280f, 280f),
                color = sparkleColor
            };

            rt.anchoredPosition = startPos;
            rt.localScale = Vector3.one * sparkle.startScale;
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f + sparkle.startRotation);

            result.Add(sparkle);
        }

        return result;
    }

    private void UpdateSparkles(List<SparkleRuntime> sparkles, float currentTime)
    {
        if (sparkles == null)
            return;

        for (int i = 0; i < sparkles.Count; i++)
        {
            SparkleRuntime s = sparkles[i];
            if (s == null || s.rect == null || s.image == null)
                continue;

            float localTime = currentTime - s.delay;
            if (localTime <= 0f)
            {
                SetSparkleAlpha(s, 0f);
                continue;
            }

            float p = Mathf.Clamp01(localTime / s.duration);
            if (p >= 1f)
            {
                SetSparkleAlpha(s, 0f);
                continue;
            }

            Vector2 pos = Vector2.Lerp(s.startPos, s.endPos, p);

            // »ìÂ¦ È£¸¦ ±×¸®¸é¼­ Æ¢°Ô
            float arc = Mathf.Sin(p * Mathf.PI) * 16f;
            Vector2 dir = (s.endPos - s.startPos).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x);
            pos += normal * arc;

            s.rect.anchoredPosition = pos;
            s.rect.localScale = Vector3.one * Mathf.Lerp(s.startScale, s.endScale, p);
            s.rect.localRotation = Quaternion.Euler(0f, 0f, s.startRotation + s.rotationSpeed * localTime);

            float alpha;
            if (p < 0.2f)
                alpha = Mathf.Lerp(0f, s.color.a, p / 0.2f);
            else
                alpha = Mathf.Lerp(s.color.a, 0f, (p - 0.2f) / 0.8f);

            SetSparkleAlpha(s, alpha);
        }
    }

    private static void SetSparkleAlpha(SparkleRuntime sparkle, float alpha)
    {
        if (sparkle == null || sparkle.image == null)
            return;

        Color c = sparkle.color;
        c.a = alpha;
        sparkle.image.color = c;
    }
    private void OnDisable()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;

        if (_rectTransform != null)
            _rectTransform.localScale = Vector3.one;
    }
    private void Finish()
    {
        if (_isFinishing)
            return;

        _isFinishing = true;
        _playRoutine = null;

        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;

        gameObject.SetActive(false);
        OnFinished?.Invoke(this);
    }
}