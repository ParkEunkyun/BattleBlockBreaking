using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public sealed class NormalComboFx : MonoBehaviour
{
    private sealed class Spark
    {
        public RectTransform rect;
        public Image image;
        public Vector2 from;
        public Vector2 to;
        public float delay;
        public float duration;
        public float rotationSpeed;
        public float sizeFrom;
        public float sizeTo;
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

    private RectTransform _root;
    private CanvasGroup _group;

    private RectTransform _burstRect;
    private Image _burstImage;

    private RectTransform _countBackRect;
    private TextMeshProUGUI _countBackText;
    private RectTransform _countFrontRect;
    private TextMeshProUGUI _countFrontText;

    private RectTransform _labelBackRect;
    private TextMeshProUGUI _labelBackText;
    private RectTransform _labelFrontRect;
    private TextMeshProUGUI _labelFrontText;

    private Coroutine _co;

    public Action<NormalComboFx> OnFinished;

    private bool _isFinishing;

    private void Awake()
    {
        _root = GetComponent<RectTransform>();
        _group = GetComponent<CanvasGroup>();

        _root.anchorMin = new Vector2(0.5f, 0.5f);
        _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = new Vector2(0.5f, 0.5f);
        _group.alpha = 0f;

        _burstRect = CreateImageChild("Burst", out _burstImage);
        _countBackRect = CreateTextChild("CountBack", out _countBackText);
        _countFrontRect = CreateTextChild("CountFront", out _countFrontText);
        _labelBackRect = CreateTextChild("LabelBack", out _labelBackText);
        _labelFrontRect = CreateTextChild("LabelFront", out _labelFrontText);
    }

    public void Play(
        int combo,
        TMP_FontAsset fontAsset,
        Color mainColor,
        float totalDuration,
        float riseDistance)
    {
        gameObject.SetActive(true);
        _isFinishing = false;

        if (_co != null)
            StopCoroutine(_co);

        ApplyVisual(combo, fontAsset, mainColor);
        _co = StartCoroutine(CoPlay(combo, totalDuration, riseDistance, mainColor));
    }

    private RectTransform CreateImageChild(string name, out Image image)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = WhiteSprite;
        image.type = Image.Type.Simple;

        return rt;
    }

    private RectTransform CreateTextChild(string name, out TextMeshProUGUI text)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        text = go.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.fontSize = 64f;
        text.fontStyle = FontStyles.Bold;

        return rt;
    }

    private void ApplyVisual(int combo, TMP_FontAsset fontAsset, Color mainColor)
    {
        string countText = $"x{combo}";
        string labelText = "COMBO";

        if (fontAsset == null)
            fontAsset = TMP_Settings.defaultFontAsset;

        _countFrontText.font = fontAsset;
        _countBackText.font = fontAsset;
        _labelFrontText.font = fontAsset;
        _labelBackText.font = fontAsset;

        _countFrontText.text = countText;
        _countBackText.text = countText;
        _labelFrontText.text = labelText;
        _labelBackText.text = labelText;

        _countFrontText.fontSize = combo >= 10 ? 100f : 150f;
        _countBackText.fontSize = _countFrontText.fontSize;
        _labelFrontText.fontSize = 100;
        _labelBackText.fontSize = 100f;

        _countFrontText.color = Color.Lerp(Color.white, mainColor, 0.35f);
        _labelFrontText.color = Color.Lerp(Color.white, mainColor, 0.25f);

        Color shadow = mainColor;
        shadow.a = 0.95f;
        _countBackText.color = shadow;
        _labelBackText.color = shadow;

        _burstImage.color = new Color(mainColor.r, mainColor.g, mainColor.b, 0.44f);

        _burstRect.sizeDelta = new Vector2(220f, 220f);
        _burstRect.anchoredPosition = new Vector2(0f, 16f);

        _countFrontRect.anchoredPosition = new Vector2(0f, 50f);
        _countBackRect.anchoredPosition = new Vector2(4f, 46f);

        _labelFrontRect.anchoredPosition = new Vector2(0f, -42f);
        _labelBackRect.anchoredPosition = new Vector2(3f, -46f);

        _countFrontRect.sizeDelta = new Vector2(280f, 110f);
        _countBackRect.sizeDelta = _countFrontRect.sizeDelta;
        _labelFrontRect.sizeDelta = new Vector2(260f, 60f);
        _labelBackRect.sizeDelta = _labelFrontRect.sizeDelta;
    }

    private IEnumerator CoPlay(int combo, float totalDuration, float riseDistance, Color mainColor)
    {
        float fadeIn = 0.08f;
        float hold = Mathf.Max(0.10f, totalDuration * 0.34f);
        float fadeOut = Mathf.Max(0.18f, totalDuration - fadeIn - hold);

        List<Spark> sparks = BuildSparks(mainColor, combo);

        _group.alpha = 0f;
        _root.localScale = new Vector3(0.45f, 0.45f, 1f);

        Vector2 startPos = _root.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0f, riseDistance);

        float t = 0f;
        while (t < totalDuration)
        {
            t += Time.unscaledDeltaTime;

            float alpha;
            if (t <= fadeIn)
            {
                float p = Mathf.Clamp01(t / fadeIn);
                alpha = Mathf.Lerp(0f, 1f, p);
                _root.localScale = Vector3.Lerp(
                    new Vector3(0.45f, 0.45f, 1f),
                    new Vector3(1.18f, 1.18f, 1f),
                    EaseOutBack01(p));
            }
            else if (t <= fadeIn + hold)
            {
                alpha = 1f;
                _root.localScale = Vector3.Lerp(_root.localScale, Vector3.one, Time.unscaledDeltaTime * 12f);
            }
            else
            {
                float p = Mathf.Clamp01((t - fadeIn - hold) / fadeOut);
                alpha = Mathf.Lerp(1f, 0f, p);
                _root.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.08f, 1.08f, 1f), p);
            }

            float moveP = Mathf.Clamp01(t / totalDuration);
            _root.anchoredPosition = Vector2.Lerp(startPos, endPos, EaseOutCubic01(moveP));

            _group.alpha = alpha;

            float burstPulse = 1f + Mathf.Sin(t * 18f) * 0.08f;
            _burstRect.localScale = new Vector3(burstPulse, burstPulse, 1f);
            _burstImage.color = new Color(_burstImage.color.r, _burstImage.color.g, _burstImage.color.b, alpha * 0.32f);

            UpdateSparks(sparks, t);

            yield return null;
        }

        Finish();
        //Destroy(gameObject);
    }

    private List<Spark> BuildSparks(Color baseColor, int combo)
    {
        int count = Mathf.Clamp(6 + combo, 10, 18);
        var result = new List<Spark>(count);

        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling();

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.sprite = WhiteSprite;
            img.type = Image.Type.Simple;

            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float startRadius = UnityEngine.Random.Range(16f, 36f);
            float endRadius = UnityEngine.Random.Range(58f, 120f);

            Vector2 from = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * startRadius;
            Vector2 to = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * endRadius;

            Color c = Color.Lerp(baseColor, Color.white, UnityEngine.Random.Range(0.65f, 0.95f));
            c.a = 0f;

            float sizeFrom = UnityEngine.Random.Range(8f, 16f);
            float sizeTo = UnityEngine.Random.Range(14f, 26f);

            rt.sizeDelta = new Vector2(sizeFrom, sizeFrom);
            rt.anchoredPosition = from;
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f + UnityEngine.Random.Range(-25f, 25f));

            img.color = c;

            result.Add(new Spark
            {
                rect = rt,
                image = img,
                from = from,
                to = to,
                delay = UnityEngine.Random.Range(0f, 0.08f),
                duration = UnityEngine.Random.Range(0.22f, 0.46f),
                rotationSpeed = UnityEngine.Random.Range(-360f, 360f),
                sizeFrom = sizeFrom,
                sizeTo = sizeTo,
                color = c
            });
        }

        return result;
    }

    private void UpdateSparks(List<Spark> sparks, float t)
    {
        if (sparks == null)
            return;

        for (int i = 0; i < sparks.Count; i++)
        {
            Spark s = sparks[i];
            if (s == null || s.rect == null || s.image == null)
                continue;

            float local = t - s.delay;
            if (local <= 0f)
            {
                SetSparkAlpha(s, 0f);
                continue;
            }

            float p = Mathf.Clamp01(local / s.duration);
            if (p >= 1f)
            {
                SetSparkAlpha(s, 0f);
                continue;
            }

            float eased = EaseOutCubic01(p);
            Vector2 pos = Vector2.Lerp(s.from, s.to, eased);
            float arc = Mathf.Sin(p * Mathf.PI) * 12f;
            Vector2 dir = (s.to - s.from).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x);
            pos += normal * arc;

            s.rect.anchoredPosition = pos;

            float size = Mathf.Lerp(s.sizeFrom, s.sizeTo, eased);
            s.rect.sizeDelta = new Vector2(size, size);
            s.rect.localRotation = Quaternion.Euler(0f, 0f, s.rect.localRotation.eulerAngles.z + s.rotationSpeed * Time.unscaledDeltaTime);

            float alpha;
            if (p < 0.18f)
                alpha = Mathf.Lerp(0f, 1f, p / 0.18f);
            else
                alpha = Mathf.Lerp(1f, 0f, (p - 0.18f) / 0.82f);

            SetSparkAlpha(s, alpha);
        }
    }

    private static void SetSparkAlpha(Spark spark, float alpha)
    {
        Color c = spark.color;
        c.a = alpha;
        spark.image.color = c;
    }

    private static float EaseOutCubic01(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseOutBack01(float t)
    {
        t = Mathf.Clamp01(t);
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
    private void OnDisable()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }

        if (_group != null)
            _group.alpha = 0f;

        if (_root != null)
            _root.localScale = Vector3.one;
    }
    private void Finish()
    {
        if (_isFinishing)
            return;

        _isFinishing = true;
        _co = null;

        if (_group != null)
            _group.alpha = 0f;

        gameObject.SetActive(false);
        OnFinished?.Invoke(this);
    }
}