using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public sealed class BattleComboFx : MonoBehaviour
{
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
    private bool _isFinishing;

    public Action<BattleComboFx> OnFinished;

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

    public void PrepareForReuse()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }

        _isFinishing = false;

        if (_group != null)
            _group.alpha = 0f;

        if (_root != null)
        {
            _root.localScale = Vector3.one;
            _root.anchoredPosition = Vector2.zero;
        }

        gameObject.SetActive(false);
    }

    public void Play(int combo, TMP_FontAsset fontAsset, Color mainColor, float totalDuration, float riseDistance)
    {
        gameObject.SetActive(true);
        _isFinishing = false;

        if (_co != null)
            StopCoroutine(_co);

        ApplyVisual(combo, fontAsset, mainColor);
        _co = StartCoroutine(CoPlay(totalDuration, riseDistance, mainColor));
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
        if (fontAsset == null)
            fontAsset = TMP_Settings.defaultFontAsset;

        string countText = $"x{combo}";
        string labelText = "COMBO";

        _countFrontText.font = fontAsset;
        _countBackText.font = fontAsset;
        _labelFrontText.font = fontAsset;
        _labelBackText.font = fontAsset;

        _countFrontText.text = countText;
        _countBackText.text = countText;
        _labelFrontText.text = labelText;
        _labelBackText.text = labelText;

        _countFrontText.fontSize = combo >= 10 ? 88f : 128f;
        _countBackText.fontSize = _countFrontText.fontSize;
        _labelFrontText.fontSize = 52f;
        _labelBackText.fontSize = 52f;

        _countFrontText.color = Color.Lerp(Color.white, mainColor, 0.35f);
        _labelFrontText.color = Color.Lerp(Color.white, mainColor, 0.22f);

        Color shadow = mainColor;
        shadow.a = 0.95f;
        _countBackText.color = shadow;
        _labelBackText.color = shadow;

        _burstImage.color = new Color(mainColor.r, mainColor.g, mainColor.b, 0.40f);

        _burstRect.sizeDelta = new Vector2(190f, 190f);
        _burstRect.anchoredPosition = new Vector2(0f, 8f);

        _countFrontRect.anchoredPosition = new Vector2(0f, 28f);
        _countBackRect.anchoredPosition = new Vector2(4f, 24f);

        _labelFrontRect.anchoredPosition = new Vector2(0f, -42f);
        _labelBackRect.anchoredPosition = new Vector2(3f, -46f);

        _countFrontRect.sizeDelta = new Vector2(260f, 100f);
        _countBackRect.sizeDelta = _countFrontRect.sizeDelta;
        _labelFrontRect.sizeDelta = new Vector2(220f, 52f);
        _labelBackRect.sizeDelta = _labelFrontRect.sizeDelta;
    }

    private IEnumerator CoPlay(float totalDuration, float riseDistance, Color mainColor)
    {
        float fadeIn = 0.06f;
        float hold = Mathf.Max(0.10f, totalDuration * 0.34f);
        float fadeOut = Mathf.Max(0.14f, totalDuration - fadeIn - hold);

        _group.alpha = 0f;
        _root.localScale = new Vector3(0.55f, 0.55f, 1f);

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
                    new Vector3(0.55f, 0.55f, 1f),
                    new Vector3(1.12f, 1.12f, 1f),
                    EaseOutBack01(p));
            }
            else if (t <= fadeIn + hold)
            {
                alpha = 1f;
                _root.localScale = Vector3.Lerp(_root.localScale, Vector3.one, Time.unscaledDeltaTime * 14f);
            }
            else
            {
                float p = Mathf.Clamp01((t - fadeIn - hold) / fadeOut);
                alpha = Mathf.Lerp(1f, 0f, p);
                _root.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.05f, 1.05f, 1f), p);
            }

            float moveP = Mathf.Clamp01(t / totalDuration);
            _root.anchoredPosition = Vector2.Lerp(startPos, endPos, EaseOutCubic01(moveP));
            _group.alpha = alpha;

            float burstPulse = 1f + Mathf.Sin(t * 20f) * 0.05f;
            _burstRect.localScale = new Vector3(burstPulse, burstPulse, 1f);
            _burstImage.color = new Color(mainColor.r, mainColor.g, mainColor.b, alpha * 0.28f);

            yield return null;
        }

        Finish();
    }

    private static float EaseOutCubic01(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseOutBack01(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
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