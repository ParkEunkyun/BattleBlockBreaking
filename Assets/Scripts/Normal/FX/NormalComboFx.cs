using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class NormalComboFx : MonoBehaviour
{
    public Action<NormalComboFx> OnFinished;

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

    private RectTransform _root;
    private CanvasGroup _group;

    private TMP_Text _comboText;
    private RectTransform _burstRect;
    private Image _burstImage;

    private RectTransform _scoreRoot;
    private Image _scoreBackground;
    private TMP_Text _baseScoreText;
    private Image _artifactIcon;
    private TMP_Text _artifactBonusText;
    private bool _viewInitialized;

    private readonly List<SparkleRuntime> _sparkles = new List<SparkleRuntime>();

    private Coroutine _playRoutine;

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
        EnsureView();
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        _group.alpha = 0f;
    }

    private void EnsureView()
    {
        if (_viewInitialized)
            return;

        _viewInitialized = true;

        _root = GetComponent<RectTransform>();
        if (_root == null)
            _root = gameObject.AddComponent<RectTransform>();

        _group = GetComponent<CanvasGroup>();
        if (_group == null)
            _group = gameObject.AddComponent<CanvasGroup>();

        _root.anchorMin = new Vector2(0.5f, 0.5f);
        _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = new Vector2(0.5f, 0.5f);

        _burstRect = GetOrCreateRect("Burst", new Vector2(0f, 12f));
        _burstImage = _burstRect.gameObject.AddComponent<Image>();
        _burstImage.raycastTarget = false;
        _burstImage.sprite = WhiteSprite;
        _burstImage.color = new Color(1f, 1f, 1f, 0.28f);

        _comboText = GetOrCreateText("ComboText", new Vector2(0f, 18f), 130f);
        _comboText.lineSpacing = -70f;
        _comboText.characterSpacing = -20f;
        _scoreRoot = GetOrCreateRect("ScoreRoot", new Vector2(0f, -140f));
        _scoreBackground = GetOrCreateImage("ScoreBackground", _scoreRoot, true);
        _baseScoreText = GetOrCreateText("BaseScoreText", Vector2.zero, 64f, _scoreRoot);
        _artifactIcon = GetOrCreateImage("ArtifactIcon", _scoreRoot, false);
        _artifactBonusText = GetOrCreateText("ArtifactBonusText", Vector2.zero, 64f, _scoreRoot);

        _artifactIcon.gameObject.SetActive(false);
        _artifactBonusText.gameObject.SetActive(false);
        _scoreBackground.gameObject.SetActive(false);
        _scoreRoot.gameObject.SetActive(false);

        EnsureSparkles(15);

        _group.alpha = 0f;
    }

    private RectTransform GetOrCreateRect(string name, Vector2 anchoredPos, Transform parent = null)
    {
        Transform root = parent != null ? parent : transform;
        Transform found = root.Find(name);

        RectTransform rt;
        if (found != null)
        {
            rt = found as RectTransform;
            if (rt == null)
                rt = found.gameObject.GetComponent<RectTransform>();
        }
        else
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root, false);
            rt = go.GetComponent<RectTransform>();
        }

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.localScale = Vector3.one;

        return rt;
    }

    private TMP_Text GetOrCreateText(string name, Vector2 anchoredPos, float fontSize, Transform parent = null)
    {
        RectTransform rt = GetOrCreateRect(name, anchoredPos, parent);

        TextMeshProUGUI text = rt.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = rt.gameObject.AddComponent<TextMeshProUGUI>();

        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.lineSpacing = -8f;
        text.text = string.Empty;

        return text;
    }

    private Image GetOrCreateImage(string name, Transform parent, bool sliced)
    {
        RectTransform rt = GetOrCreateRect(name, Vector2.zero, parent);

        Image image = rt.GetComponent<Image>();
        if (image == null)
            image = rt.gameObject.AddComponent<Image>();

        image.raycastTarget = false;
        image.sprite = WhiteSprite;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.preserveAspect = !sliced;

        return image;
    }

    private void EnsureSparkles(int count)
    {
        while (_sparkles.Count < count)
        {
            string sparkleName = $"Sparkle_{_sparkles.Count}";
            RectTransform rt = GetOrCreateRect(sparkleName, Vector2.zero);
            rt.SetAsLastSibling();

            Image img = rt.GetComponent<Image>();
            if (img == null)
                img = rt.gameObject.AddComponent<Image>();

            img.raycastTarget = false;
            img.sprite = WhiteSprite;
            img.color = Color.clear;

            _sparkles.Add(new SparkleRuntime
            {
                rect = rt,
                image = img,
                color = Color.white
            });
        }
    }

    
    public void Play(
        int combo,
        TMP_FontAsset font,
        Color comboColor,
        float duration,
        float riseDistance,
        int baseScore,
        int artifactBonus,
        Sprite artifactIconSprite,
        Sprite scoreBackgroundSprite,
        Color baseScoreColor,
        Color artifactBonusColor,
        Color scoreBackgroundColor,
        Vector2 scoreBackgroundPadding)
    {
        EnsureView();

        _comboText.text = string.Empty;
        _baseScoreText.text = string.Empty;
        _artifactBonusText.text = string.Empty;

        _artifactIcon.gameObject.SetActive(false);
        _artifactBonusText.gameObject.SetActive(false);
        _scoreBackground.gameObject.SetActive(false);
        _scoreRoot.gameObject.SetActive(false);

        if (_playRoutine != null)
            StopCoroutine(_playRoutine);

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (font != null)
        {
            _comboText.font = font;
            _baseScoreText.font = font;
            _artifactBonusText.font = font;
        }

        _comboText.text = $"X{combo} \n COMBO";
        _comboText.color = comboColor;

        _burstRect.sizeDelta = new Vector2(220f, 220f);
        _burstRect.anchoredPosition = new Vector2(0f, 10f);
        _burstImage.color = new Color(comboColor.r, comboColor.g, comboColor.b, 0.32f);

        bool showScore = combo >= 2 && (baseScore > 0 || artifactBonus > 0);
        _scoreRoot.gameObject.SetActive(showScore);

        if (showScore)
        {
            _baseScoreText.text = baseScore > 0 ? $"+{baseScore}" : string.Empty;
            _baseScoreText.color = baseScoreColor;

            bool showArtifact = artifactBonus > 0;
            _artifactIcon.gameObject.SetActive(showArtifact && artifactIconSprite != null);
            _artifactBonusText.gameObject.SetActive(showArtifact);

            if (showArtifact && artifactIconSprite != null)
                _artifactIcon.sprite = artifactIconSprite;

            _artifactBonusText.text = showArtifact ? $"+{artifactBonus}" : string.Empty;
            _artifactBonusText.color = artifactBonusColor;

            _scoreBackground.gameObject.SetActive(scoreBackgroundSprite != null);
            if (scoreBackgroundSprite != null)
            {
                _scoreBackground.sprite = scoreBackgroundSprite;
                _scoreBackground.color = scoreBackgroundColor;
                _scoreBackground.type = Image.Type.Sliced;
            }

            LayoutScoreRow(scoreBackgroundPadding);
        }
        else
        {
            _scoreBackground.gameObject.SetActive(false);
            _artifactIcon.gameObject.SetActive(false);
            _artifactBonusText.gameObject.SetActive(false);
        }

        ResetSparkles(comboColor);

        _root.localScale = Vector3.one;
        _group.alpha = 1f;

        _playRoutine = StartCoroutine(CoPlay(duration, riseDistance, comboColor));
    }

    private void LayoutScoreRow(Vector2 bgPadding)
    {
        _baseScoreText.ForceMeshUpdate();
        _artifactBonusText.ForceMeshUpdate();

        Vector2 baseSize = _baseScoreText.GetPreferredValues(_baseScoreText.text);
        Vector2 bonusSize = _artifactBonusText.gameObject.activeSelf
            ? _artifactBonusText.GetPreferredValues(_artifactBonusText.text)
            : Vector2.zero;

        float gap = 0f;
        float iconSize = _artifactIcon.gameObject.activeSelf ? 18f : 0f;

        float totalWidth = 0f;

        if (_artifactIcon.gameObject.activeSelf)
            totalWidth += iconSize;

        if (!string.IsNullOrEmpty(_baseScoreText.text))
        {
            if (totalWidth > 0f) totalWidth += gap;
            totalWidth += baseSize.x + 2f;
        }

        if (_artifactBonusText.gameObject.activeSelf)
        {
            if (totalWidth > 0f) totalWidth += gap;
            totalWidth += bonusSize.x + 2f;
        }

        float cursor = -totalWidth * 0.5f;

        if (_artifactIcon.gameObject.activeSelf)
        {
            RectTransform iconRt = _artifactIcon.rectTransform;
            iconRt.sizeDelta = new Vector2(iconSize, iconSize);
            iconRt.anchoredPosition = new Vector2(cursor + iconSize * 0.5f, 0f);
            cursor += iconSize;
        }

        RectTransform baseRt = _baseScoreText.rectTransform;
        baseRt.sizeDelta = new Vector2(baseSize.x + 2f, Mathf.Max(40f, baseSize.y));
        if (_artifactIcon.gameObject.activeSelf)
            cursor += gap;
        baseRt.anchoredPosition = new Vector2(cursor + baseRt.sizeDelta.x * 0.5f, 0f);
        cursor += baseRt.sizeDelta.x;

        if (_artifactBonusText.gameObject.activeSelf)
        {
            RectTransform bonusRt = _artifactBonusText.rectTransform;
            bonusRt.sizeDelta = new Vector2(bonusSize.x + 2f, Mathf.Max(40f, bonusSize.y));
            cursor += gap;
            bonusRt.anchoredPosition = new Vector2(cursor + bonusRt.sizeDelta.x * 0.5f, 0f);
            cursor += bonusRt.sizeDelta.x;
        }

        if (_scoreBackground.gameObject.activeSelf)
        {
            RectTransform bgRt = _scoreBackground.rectTransform;
            bgRt.sizeDelta = new Vector2(
                Mathf.Max(120f, totalWidth + bgPadding.x),
                Mathf.Max(52f, 40f + bgPadding.y));
            bgRt.anchoredPosition = Vector2.zero;
        }
    }

    private void ResetSparkles(Color comboColor)
    {
        for (int i = 0; i < _sparkles.Count; i++)
        {
            SparkleRuntime s = _sparkles[i];

            float size = UnityEngine.Random.Range(30f, 70f);
            s.rect.sizeDelta = new Vector2(size, size);

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float radius = UnityEngine.Random.Range(40f, 120f);

            s.startPos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 0.25f;
            s.endPos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            s.delay = UnityEngine.Random.Range(0f, 0.10f);
            s.duration = UnityEngine.Random.Range(0.25f, 0.48f);
            s.startScale = UnityEngine.Random.Range(0.35f, 0.75f);
            s.endScale = UnityEngine.Random.Range(1.0f, 1.7f);
            s.startRotation = UnityEngine.Random.Range(0f, 360f);
            s.rotationSpeed = UnityEngine.Random.Range(-180f, 180f);

            Color sparkleColor = Color.Lerp(comboColor, Color.white, UnityEngine.Random.Range(0.65f, 0.95f));
            sparkleColor.a = UnityEngine.Random.Range(0.85f, 1f);
            s.color = sparkleColor;

            s.rect.anchoredPosition = s.startPos;
            s.rect.localScale = Vector3.one * s.startScale;
            s.rect.localRotation = Quaternion.Euler(0f, 0f, 45f + s.startRotation);
            s.image.color = new Color(s.color.r, s.color.g, s.color.b, 0f);
        }
    }

    private IEnumerator CoPlay(float duration, float riseDistance, Color comboColor)
    {
        Vector2 startPos = _root.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0f, riseDistance);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);

            _root.anchoredPosition = Vector2.Lerp(startPos, endPos, ease);

            float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.08f;
            _root.localScale = new Vector3(pulse, pulse, 1f);

            if (t < 0.72f)
                _group.alpha = 1f;
            else
                _group.alpha = 1f - ((t - 0.72f) / 0.28f);

            float burstPulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.45f;
            _burstRect.localScale = new Vector3(burstPulse, burstPulse, 1f);
            _burstImage.color = new Color(comboColor.r, comboColor.g, comboColor.b, (1f - t) * 0.28f);

            UpdateSparkles(elapsed);

            yield return null;
        }

        _playRoutine = null;
        OnFinished?.Invoke(this);
    }

    private void UpdateSparkles(float currentTime)
    {
        for (int i = 0; i < _sparkles.Count; i++)
        {
            SparkleRuntime s = _sparkles[i];

            float localTime = currentTime - s.delay;
            if (localTime <= 0f)
            {
                s.image.color = new Color(s.color.r, s.color.g, s.color.b, 0f);
                continue;
            }

            float p = Mathf.Clamp01(localTime / Mathf.Max(0.0001f, s.duration));
            if (p >= 1f)
            {
                s.image.color = new Color(s.color.r, s.color.g, s.color.b, 0f);
                continue;
            }

            Vector2 pos = Vector2.Lerp(s.startPos, s.endPos, p);
            float arc = Mathf.Sin(p * Mathf.PI) * 24f;
            Vector2 dir = (s.endPos - s.startPos).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x);
            pos += normal * arc;

            s.rect.anchoredPosition = pos;
            s.rect.localScale = Vector3.one * Mathf.Lerp(s.startScale, s.endScale, p);
            s.rect.localRotation = Quaternion.Euler(0f, 0f, s.startRotation + s.rotationSpeed * localTime);

            float alpha;
            if (p < 0.25f)
                alpha = Mathf.Lerp(0f, s.color.a, p / 0.25f);
            else
                alpha = Mathf.Lerp(s.color.a, 0f, (p - 0.25f) / 0.75f);

            s.image.color = new Color(s.color.r, s.color.g, s.color.b, alpha);
        }
    }
}