using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class NormalScorePopupFx : MonoBehaviour
{
    private RectTransform _rect;
    private CanvasGroup _canvasGroup;
    private HorizontalLayoutGroup _layout;
    private ContentSizeFitter _fitter;

    private TMP_Text _baseText;
    private Image _artifactIcon;
    private TMP_Text _artifactBonusText;

    private Coroutine _playRoutine;
    private Action<NormalScorePopupFx> _onFinished;

    private Image _backgroundImage;
    private void Awake()
    {
        EnsureView();
    }

    private void OnDisable()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        _onFinished = null;
    }

    private void EnsureView()
    {
        _rect = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _layout = GetComponent<HorizontalLayoutGroup>();
        if (_layout == null)
            _layout = gameObject.AddComponent<HorizontalLayoutGroup>();

        _layout.childAlignment = TextAnchor.MiddleCenter;
        _layout.spacing = -5f;
        _layout.childControlWidth = false;
        _layout.childControlHeight = false;
        _layout.childForceExpandWidth = false;
        _layout.childForceExpandHeight = false;

        _fitter = GetComponent<ContentSizeFitter>();
        if (_fitter == null)
            _fitter = gameObject.AddComponent<ContentSizeFitter>();

        _fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        _fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _backgroundImage = GetOrCreateBackground("Background");
        _artifactIcon = GetOrCreateIcon("ArtifactIcon");
        _baseText = GetOrCreateText("BaseText");
        _artifactBonusText = GetOrCreateText("ArtifactBonusText");

        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0.5f, 0.5f);
        _rect.localScale = Vector3.one;
    }

    private TMP_Text GetOrCreateText(string childName)
    {
        Transform found = transform.Find(childName);
        if (found != null)
        {
            TMP_Text existing = found.GetComponent<TMP_Text>();
            if (existing != null)
                return existing;
        }

        GameObject go = new GameObject(childName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(transform, false);

       // LayoutElement le = go.GetComponent<LayoutElement>();
        //le.minWidth = 1f;
       // le.minHeight = 1f;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 48;
        text.enableAutoSizing = false;
        text.fontSizeMin = 32f;
        text.fontSizeMax = 48f;
        text.fontStyle = FontStyles.Bold;
        text.text = string.Empty;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(150f, rt.sizeDelta.y);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        return text;
    }

    private Image GetOrCreateIcon(string childName)
    {
        Transform found = transform.Find(childName);
        if (found != null)
        {
            Image existing = found.GetComponent<Image>();
            if (existing != null)
                return existing;
        }

        GameObject go = new GameObject(childName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(transform, false);

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.preferredWidth = 64f;
        le.preferredHeight = 64f;
        le.minWidth = 64f;
        le.minHeight = 64f;

        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        return img;
    }

    public void Play(
    Vector2 anchoredPosition,
    TMP_FontAsset font,
    int baseScore,
    int artifactBonus,
    Sprite artifactIconSprite,
    Color baseColor,
    Color artifactColor,
    Sprite backgroundSprite,
    Color backgroundColor,
    Vector2 backgroundPadding,
    float riseDistance,
    float duration,
    Action<NormalScorePopupFx> onFinished)
    {
        EnsureView();

        _onFinished = onFinished;

        _rect.anchoredPosition = anchoredPosition;
        _rect.localScale = Vector3.one;
        _canvasGroup.alpha = 1f;

        if (font != null)
        {
            _baseText.font = font;
            _artifactBonusText.font = font;
        }

        _baseText.color = baseColor;
        _artifactBonusText.color = artifactColor;

        _baseText.text = Mathf.Max(0, baseScore).ToString();

        bool showArtifactBonus = artifactBonus > 0;
        _artifactIcon.gameObject.SetActive(showArtifactBonus && artifactIconSprite != null);
        _artifactBonusText.gameObject.SetActive(showArtifactBonus);

        if (showArtifactBonus)
        {
            _artifactIcon.sprite = artifactIconSprite;
            _artifactBonusText.text = "+" + artifactBonus;
        }
        else
        {
            _artifactBonusText.text = string.Empty;
        }

        bool showBackground = backgroundSprite != null;
        _backgroundImage.gameObject.SetActive(showBackground);

        if (showBackground)
        {
            _backgroundImage.sprite = backgroundSprite;
            _backgroundImage.color = backgroundColor;
            _backgroundImage.type = Image.Type.Simple;
            _backgroundImage.preserveAspect = true;
            _backgroundImage.transform.SetAsFirstSibling();

            RectTransform bgRt = _backgroundImage.rectTransform;
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = backgroundPadding;
        }

        if (!showBackground)
            _backgroundImage.gameObject.SetActive(false);

        if (_playRoutine != null)
            StopCoroutine(_playRoutine);

        _playRoutine = StartCoroutine(CoPlay(riseDistance, duration));
    }

    private IEnumerator CoPlay(float riseDistance, float duration)
    {
        Vector2 start = _rect.anchoredPosition;
        Vector2 end = start + Vector2.up * riseDistance;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);

            _rect.anchoredPosition = Vector2.LerpUnclamped(start, end, ease);

            float punch = 1f + Mathf.Sin(t * Mathf.PI) * 0.10f;
            _rect.localScale = new Vector3(punch, punch, 1f);

            if (t < 0.65f)
                _canvasGroup.alpha = 1f;
            else
                _canvasGroup.alpha = 1f - ((t - 0.65f) / 0.35f);

            yield return null;
        }

        _playRoutine = null;
        _onFinished?.Invoke(this);
    }
    private Image GetOrCreateBackground(string childName)
    {
        Transform found = transform.Find(childName);
        if (found != null)
        {
            Image existing = found.GetComponent<Image>();
            if (existing != null)
                return existing;
        }

        GameObject go = new GameObject(childName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling();

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.ignoreLayout = true;

        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.gameObject.SetActive(false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        return img;
    }
}