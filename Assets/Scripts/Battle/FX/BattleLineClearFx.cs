using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleLineClearFx : MonoBehaviour
{
    private enum Axis
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

    private sealed class LineVisual
    {
        public RectTransform root;
        public CanvasGroup group;
        public Canvas canvas;
        public Image glowImage;
        public RectTransform coreRect;
        public Image coreImage;
        public RectTransform sweepRect;
        public Image sweepImage;
        public RectTransform flashRect;
        public Image flashImage;
        public List<SparkleRuntime> sparkles;

        public Axis axis;
        public float startDelay;
        public float holdDuration;
        public float fadeOutDuration;
        public float fadeInDuration;
        public float sweepDuration;
        public float totalDuration;
        public Vector2 sweepFrom;
        public Vector2 sweepTo;
        public bool active;
    }

    private struct ClearEvent
    {
        public float time;
        public int x;
        public int y;
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

    [Header("Board")]
    [SerializeField] private RectTransform boardCellRoot;
    [SerializeField] private int boardSize = 8;
    [SerializeField] private bool topRowIsMaxY = true;

    [Header("Look")]
    [SerializeField] private Color fxColor = new Color(0.55f, 0.90f, 1.00f, 1.00f);
    [SerializeField] private float thicknessScale = 1.00f;
    [SerializeField] private int fxSortingOrder = 600;

    [Header("Timing")]
    [SerializeField] private float lineStartStagger = 0.015f;
    [SerializeField] private float holdDuration = 0.05f;
    [SerializeField] private float fadeOutDuration = 0.12f;
    [SerializeField] private float clearLeadDelay = 0.020f;
    [SerializeField] private float clearStepDelay = 0.008f;
    [SerializeField] private float lineTailWait = 0.025f;

    [Header("Sparkles")]
    [SerializeField] private int sparkleCount = 18;
    [SerializeField] private float sparkleMinDuration = 0.35f;
    [SerializeField] private float sparkleMaxDuration = 0.65f;
    [SerializeField] private float sparkleMaxDelay = 0.12f;
    [SerializeField] private float sparkleMinSize = 18f;
    [SerializeField] private float sparkleMaxSize = 34f;
    [SerializeField] private float sparkleDriftMainMin = 34f;
    [SerializeField] private float sparkleDriftMainMax = 90f;
    [SerializeField] private float sparkleDriftCrossMin = 14f;
    [SerializeField] private float sparkleDriftCrossMax = 34f;

    private RectTransform[,] _cellRects;
    private float[] _rowCenters;
    private float[] _colCenters;
    private bool[,] _clearInvoked;

    private float _cellWidth;
    private float _cellHeight;
    private float _boardWidth;
    private float _boardHeight;

    private readonly List<LineVisual> _lineVisualPool = new List<LineVisual>(16);
    private readonly List<ClearEvent> _clearEvents = new List<ClearEvent>(128);

    private bool _cached;
    private bool _isPlaying;
    private Coroutine _playRoutine;
    private Action<BattleLineClearFx> _returnToPool;

    public void SetReturnToPool(Action<BattleLineClearFx> returnAction)
    {
        _returnToPool = returnAction;
    }

    public void PrepareForReuse()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        HideAllVisuals();
        _isPlaying = false;
    }

    public void ReturnToPool()
    {
        _returnToPool?.Invoke(this);
    }

    public void RebuildCache()
    {
        _cached = false;

        if (boardCellRoot == null)
        {
            Debug.LogWarning("[BattleLineClearFx] boardCellRoot is null.");
            return;
        }

        List<RectTransform> allRects = new List<RectTransform>(boardCellRoot.childCount);

        for (int i = 0; i < boardCellRoot.childCount; i++)
        {
            RectTransform rt = boardCellRoot.GetChild(i) as RectTransform;
            if (rt != null)
                allRects.Add(rt);
        }

        int need = boardSize * boardSize;
        if (allRects.Count < need)
        {
            Debug.LogWarning($"[BattleLineClearFx] child count 부족: {allRects.Count} / need {need}");
            return;
        }

        allRects.Sort((a, b) =>
        {
            Vector2 ap = a.anchoredPosition;
            Vector2 bp = b.anchoredPosition;

            if (!Mathf.Approximately(ap.y, bp.y))
                return bp.y.CompareTo(ap.y);

            return ap.x.CompareTo(bp.x);
        });

        if (allRects.Count > need)
            allRects.RemoveRange(need, allRects.Count - need);

        _cellRects = new RectTransform[boardSize, boardSize];
        _rowCenters = new float[boardSize];
        _colCenters = new float[boardSize];
        _clearInvoked = new bool[boardSize, boardSize];

        for (int visualRow = 0; visualRow < boardSize; visualRow++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                int idx = visualRow * boardSize + x;
                RectTransform rt = allRects[idx];

                int boardY = topRowIsMaxY
                    ? (boardSize - 1 - visualRow)
                    : visualRow;

                _cellRects[x, boardY] = rt;
            }
        }

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        for (int y = 0; y < boardSize; y++)
        {
            RectTransform sample = _cellRects[0, y];
            _rowCenters[y] = sample != null ? sample.anchoredPosition.y : 0f;
        }

        for (int x = 0; x < boardSize; x++)
        {
            RectTransform sample = _cellRects[x, 0];
            _colCenters[x] = sample != null ? sample.anchoredPosition.x : 0f;
        }

        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                RectTransform rt = _cellRects[x, y];
                if (rt == null)
                    continue;

                Vector2 p = rt.anchoredPosition;
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);

                if (_cellWidth <= 0f || _cellHeight <= 0f)
                {
                    Rect rect = rt.rect;
                    _cellWidth = rect.width > 0f ? rect.width : rt.sizeDelta.x;
                    _cellHeight = rect.height > 0f ? rect.height : rt.sizeDelta.y;
                }
            }
        }

        if (_cellWidth <= 0f)
            _cellWidth = 56f;
        if (_cellHeight <= 0f)
            _cellHeight = 56f;

        _boardWidth = (maxX - minX) + _cellWidth;
        _boardHeight = (maxY - minY) + _cellHeight;

        _cached = true;
    }

    public IEnumerator Play(List<int> completedRows, List<int> completedCols, Action<int, int> onCellCleared)
    {
        if (_isPlaying)
            yield break;

        if (!_cached)
            RebuildCache();

        if (!_cached)
        {
            // 캐시 실패 시 즉시 클리어 fallback
            if (completedRows != null)
            {
                for (int i = 0; i < completedRows.Count; i++)
                {
                    int y = completedRows[i];
                    for (int x = 0; x < boardSize; x++)
                        onCellCleared?.Invoke(x, y);
                }
            }

            if (completedCols != null)
            {
                for (int i = 0; i < completedCols.Count; i++)
                {
                    int x = completedCols[i];
                    for (int y = 0; y < boardSize; y++)
                        onCellCleared?.Invoke(x, y);
                }
            }

            yield break;
        }

        PrepareForReuse();
        _isPlaying = true;
        _playRoutine = StartCoroutine(CoPlay(completedRows, completedCols, onCellCleared));
        yield return _playRoutine;
    }

    private IEnumerator CoPlay(List<int> completedRows, List<int> completedCols, Action<int, int> onCellCleared)
    {
        Array.Clear(_clearInvoked, 0, _clearInvoked.Length);
        _clearEvents.Clear();

        List<LineVisual> activeLines = new List<LineVisual>(16);

        int lineOrder = 0;

        if (completedRows != null)
        {
            for (int i = 0; i < completedRows.Count; i++)
            {
                int y = completedRows[i];
                if (y < 0 || y >= boardSize)
                    continue;

                LineVisual line = RentLineVisual();
                ConfigureLineVisual(line, Axis.Row, y, fxColor, lineOrder * lineStartStagger);
                activeLines.Add(line);

                for (int x = 0; x < boardSize; x++)
                {
                    _clearEvents.Add(new ClearEvent
                    {
                        time = line.startDelay + clearLeadDelay + (x * clearStepDelay),
                        x = x,
                        y = y
                    });
                }

                lineOrder++;
            }
        }

        if (completedCols != null)
        {
            for (int i = 0; i < completedCols.Count; i++)
            {
                int x = completedCols[i];
                if (x < 0 || x >= boardSize)
                    continue;

                LineVisual line = RentLineVisual();
                ConfigureLineVisual(line, Axis.Column, x, fxColor, lineOrder * lineStartStagger);
                activeLines.Add(line);

                if (topRowIsMaxY)
                {
                    int step = 0;
                    for (int y = boardSize - 1; y >= 0; y--)
                    {
                        _clearEvents.Add(new ClearEvent
                        {
                            time = line.startDelay + clearLeadDelay + (step * clearStepDelay),
                            x = x,
                            y = y
                        });
                        step++;
                    }
                }
                else
                {
                    for (int y = 0; y < boardSize; y++)
                    {
                        _clearEvents.Add(new ClearEvent
                        {
                            time = line.startDelay + clearLeadDelay + (y * clearStepDelay),
                            x = x,
                            y = y
                        });
                    }
                }

                lineOrder++;
            }
        }

        if (activeLines.Count == 0)
        {
            _isPlaying = false;
            _playRoutine = null;
            yield break;
        }

        _clearEvents.Sort((a, b) => a.time.CompareTo(b.time));

        float maxEndTime = 0f;
        for (int i = 0; i < activeLines.Count; i++)
            maxEndTime = Mathf.Max(maxEndTime, activeLines[i].startDelay + activeLines[i].totalDuration);

        if (_clearEvents.Count > 0)
            maxEndTime = Mathf.Max(maxEndTime, _clearEvents[_clearEvents.Count - 1].time + lineTailWait);

        float elapsed = 0f;
        int clearCursor = 0;

        while (elapsed < maxEndTime)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int i = 0; i < activeLines.Count; i++)
                UpdateLineVisual(activeLines[i], elapsed);

            while (clearCursor < _clearEvents.Count && _clearEvents[clearCursor].time <= elapsed)
            {
                ClearEvent e = _clearEvents[clearCursor];

                if (!_clearInvoked[e.x, e.y])
                {
                    _clearInvoked[e.x, e.y] = true;
                    onCellCleared?.Invoke(e.x, e.y);
                }

                clearCursor++;
            }

            yield return null;
        }

        // 혹시 남은 셀 있으면 마무리
        while (clearCursor < _clearEvents.Count)
        {
            ClearEvent e = _clearEvents[clearCursor];
            if (!_clearInvoked[e.x, e.y])
            {
                _clearInvoked[e.x, e.y] = true;
                onCellCleared?.Invoke(e.x, e.y);
            }

            clearCursor++;
        }

        HideAllVisuals();

        _isPlaying = false;
        _playRoutine = null;
    }

    private LineVisual RentLineVisual()
    {
        for (int i = 0; i < _lineVisualPool.Count; i++)
        {
            if (!_lineVisualPool[i].active)
                return _lineVisualPool[i];
        }

        LineVisual created = CreateLineVisual(_lineVisualPool.Count);
        _lineVisualPool.Add(created);
        return created;
    }

    private LineVisual CreateLineVisual(int index)
    {
        GameObject rootGo = new GameObject($"BattleLineFx_{index}", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Canvas));
        rootGo.transform.SetParent(boardCellRoot, false);

        RectTransform rootRt = rootGo.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);

        CanvasGroup group = rootGo.GetComponent<CanvasGroup>();
        Image glow = rootGo.GetComponent<Image>();
        Canvas canvas = rootGo.GetComponent<Canvas>();

        glow.raycastTarget = false;
        glow.sprite = WhiteSprite;
        glow.type = Image.Type.Simple;
        glow.color = Color.clear;

        canvas.overrideSorting = true;
        canvas.sortingOrder = fxSortingOrder;

        RectTransform coreRect = CreateChildImage(rootRt, "Core", out Image coreImage);
        RectTransform sweepRect = CreateChildImage(rootRt, "Sweep", out Image sweepImage);
        RectTransform flashRect = CreateChildImage(rootRt, "Flash", out Image flashImage);

        List<SparkleRuntime> sparkles = new List<SparkleRuntime>(sparkleCount);

        for (int i = 0; i < sparkleCount; i++)
        {
            GameObject sparkleGo = new GameObject($"Sparkle_{i}", typeof(RectTransform), typeof(Image));
            sparkleGo.transform.SetParent(rootRt, false);
            sparkleGo.transform.SetAsLastSibling();

            RectTransform srt = sparkleGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.5f, 0.5f);
            srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);

            Image simg = sparkleGo.GetComponent<Image>();
            simg.raycastTarget = false;
            simg.sprite = WhiteSprite;
            simg.type = Image.Type.Simple;
            simg.color = Color.clear;

            sparkles.Add(new SparkleRuntime
            {
                rect = srt,
                image = simg,
                color = Color.white
            });
        }

        group.alpha = 0f;
        rootGo.SetActive(false);

        return new LineVisual
        {
            root = rootRt,
            group = group,
            canvas = canvas,
            glowImage = glow,
            coreRect = coreRect,
            coreImage = coreImage,
            sweepRect = sweepRect,
            sweepImage = sweepImage,
            flashRect = flashRect,
            flashImage = flashImage,
            sparkles = sparkles,
            active = false
        };
    }

    private RectTransform CreateChildImage(RectTransform parent, string childName, out Image image)
    {
        GameObject go = new GameObject(childName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

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

    private void ConfigureLineVisual(LineVisual line, Axis axis, int lineIndex, Color color, float startDelay)
    {
        line.active = true;
        line.axis = axis;
        line.startDelay = startDelay;
        line.holdDuration = holdDuration;
        line.fadeOutDuration = fadeOutDuration;
        line.fadeInDuration = 0.06f;
        line.sweepDuration = Mathf.Max(0.18f, holdDuration + fadeOutDuration + 0.06f);
        line.totalDuration = line.fadeInDuration + line.holdDuration + line.fadeOutDuration;

        line.root.gameObject.SetActive(true);
        line.group.alpha = 0f;
        line.root.localScale = new Vector3(0.92f, 0.92f, 1f);
        line.root.localRotation = Quaternion.identity;

        ApplyLineLayout(line, axis, lineIndex);
        ApplyLineColors(line, color);
        ResetSparkles(line, axis, color);
    }

    private void ApplyLineLayout(LineVisual line, Axis axis, int lineIndex)
    {
        line.root.anchorMin = new Vector2(0.5f, 0.5f);
        line.root.anchorMax = new Vector2(0.5f, 0.5f);
        line.root.pivot = new Vector2(0.5f, 0.5f);

        float glowThickness;
        float coreThickness;
        float flashThickness;
        float sweepLength;

        if (axis == Axis.Row)
        {
            glowThickness = Mathf.Max(18f, _cellHeight * thicknessScale * 1.95f);
            coreThickness = Mathf.Max(6f, _cellHeight * thicknessScale * 0.82f);
            flashThickness = Mathf.Max(10f, _cellHeight * thicknessScale * 1.22f);
            sweepLength = Mathf.Max(_cellWidth * 1.25f, 96f);

            line.root.sizeDelta = new Vector2(_boardWidth, glowThickness);
            line.root.anchoredPosition = new Vector2(0f, _rowCenters[lineIndex]);

            line.coreRect.sizeDelta = new Vector2(_boardWidth, coreThickness);
            line.coreRect.anchoredPosition = Vector2.zero;

            line.flashRect.sizeDelta = new Vector2(_boardWidth, flashThickness);
            line.flashRect.anchoredPosition = Vector2.zero;

            line.sweepRect.sizeDelta = new Vector2(sweepLength, glowThickness * 1.08f);
            line.sweepFrom = new Vector2(-_boardWidth * 0.5f - sweepLength * 0.6f, 0f);
            line.sweepTo = new Vector2(_boardWidth * 0.5f + sweepLength * 0.6f, 0f);
        }
        else
        {
            glowThickness = Mathf.Max(18f, _cellWidth * thicknessScale * 1.95f);
            coreThickness = Mathf.Max(6f, _cellWidth * thicknessScale * 0.82f);
            flashThickness = Mathf.Max(10f, _cellWidth * thicknessScale * 1.22f);
            sweepLength = Mathf.Max(_cellHeight * 1.25f, 96f);

            line.root.sizeDelta = new Vector2(glowThickness, _boardHeight);
            line.root.anchoredPosition = new Vector2(_colCenters[lineIndex], 0f);

            line.coreRect.sizeDelta = new Vector2(coreThickness, _boardHeight);
            line.coreRect.anchoredPosition = Vector2.zero;

            line.flashRect.sizeDelta = new Vector2(flashThickness, _boardHeight);
            line.flashRect.anchoredPosition = Vector2.zero;

            line.sweepRect.sizeDelta = new Vector2(glowThickness * 1.08f, sweepLength);
            line.sweepFrom = new Vector2(0f, _boardHeight * 0.5f + sweepLength * 0.6f);
            line.sweepTo = new Vector2(0f, -_boardHeight * 0.5f - sweepLength * 0.6f);
        }

        line.sweepRect.anchoredPosition = line.sweepFrom;
    }

    private void ApplyLineColors(LineVisual line, Color baseColor)
    {
        Color glow = baseColor;
        glow.a = 0.30f;

        Color core = baseColor;
        core.a = 0.95f;

        Color flash = baseColor;
        flash.a = 0.62f;

        line.glowImage.color = glow;
        line.coreImage.color = core;
        line.flashImage.color = flash;
        line.sweepImage.color = new Color(1f, 1f, 1f, 0.95f);
    }

    private void ResetSparkles(LineVisual line, Axis axis, Color baseColor)
    {
        float width = line.root.sizeDelta.x;
        float height = line.root.sizeDelta.y;

        for (int i = 0; i < line.sparkles.Count; i++)
        {
            SparkleRuntime s = line.sparkles[i];
            if (s == null || s.rect == null || s.image == null)
                continue;

            float size = UnityEngine.Random.Range(sparkleMinSize, sparkleMaxSize);
            s.rect.sizeDelta = new Vector2(size, size);

            Vector2 startPos;
            Vector2 endPos;

            if (axis == Axis.Row)
            {
                float startX = UnityEngine.Random.Range(-width * 0.46f, width * 0.46f);
                float startY = UnityEngine.Random.Range(-height * 0.15f, height * 0.15f);

                float driftX = UnityEngine.Random.Range(sparkleDriftCrossMin, sparkleDriftCrossMax) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
                float driftY = UnityEngine.Random.Range(sparkleDriftMainMin, sparkleDriftMainMax) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);

                startPos = new Vector2(startX, startY);
                endPos = startPos + new Vector2(driftX, driftY);
            }
            else
            {
                float startX = UnityEngine.Random.Range(-width * 0.15f, width * 0.15f);
                float startY = UnityEngine.Random.Range(-height * 0.46f, height * 0.46f);

                float driftX = UnityEngine.Random.Range(sparkleDriftMainMin, sparkleDriftMainMax) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
                float driftY = UnityEngine.Random.Range(sparkleDriftCrossMin, sparkleDriftCrossMax) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);

                startPos = new Vector2(startX, startY);
                endPos = startPos + new Vector2(driftX, driftY);
            }

            Color sparkleColor = Color.Lerp(baseColor, Color.white, UnityEngine.Random.Range(0.70f, 0.95f));
            sparkleColor.a = UnityEngine.Random.Range(0.95f, 1f);

            s.startPos = startPos;
            s.endPos = endPos;
            s.delay = UnityEngine.Random.Range(0f, sparkleMaxDelay);
            s.duration = UnityEngine.Random.Range(sparkleMinDuration, sparkleMaxDuration);
            s.startScale = UnityEngine.Random.Range(0.65f, 0.95f);
            s.endScale = UnityEngine.Random.Range(1.15f, 1.70f);
            s.startRotation = UnityEngine.Random.Range(0f, 360f);
            s.rotationSpeed = UnityEngine.Random.Range(-280f, 280f);
            s.color = sparkleColor;

            s.rect.anchoredPosition = startPos;
            s.rect.localScale = Vector3.one * s.startScale;
            s.rect.localRotation = Quaternion.Euler(0f, 0f, 45f + s.startRotation);
            s.image.color = new Color(sparkleColor.r, sparkleColor.g, sparkleColor.b, 0f);
        }
    }

    private void UpdateLineVisual(LineVisual line, float globalTime)
    {
        if (!line.active)
            return;

        float localTime = globalTime - line.startDelay;

        if (localTime <= 0f)
        {
            line.group.alpha = 0f;
            return;
        }

        if (localTime >= line.totalDuration)
        {
            line.group.alpha = 0f;
            return;
        }

        float alpha;

        if (localTime <= line.fadeInDuration)
        {
            float p = Mathf.Clamp01(localTime / Mathf.Max(0.0001f, line.fadeInDuration));
            alpha = Mathf.Lerp(0f, 1f, p);
            line.root.localScale = Vector3.Lerp(
                new Vector3(0.92f, 0.92f, 1f),
                new Vector3(1.02f, 1.02f, 1f),
                p);
        }
        else if (localTime <= line.fadeInDuration + line.holdDuration)
        {
            alpha = 1f;
            line.root.localScale = Vector3.Lerp(
                line.root.localScale,
                Vector3.one,
                Time.unscaledDeltaTime * 12f);
        }
        else
        {
            float p = Mathf.Clamp01((localTime - line.fadeInDuration - line.holdDuration) / Mathf.Max(0.0001f, line.fadeOutDuration));
            alpha = Mathf.Lerp(1f, 0f, p);
            line.root.localScale = Vector3.Lerp(
                Vector3.one,
                new Vector3(1.06f, 1.06f, 1f),
                p);

            line.flashImage.color = new Color(line.flashImage.color.r, line.flashImage.color.g, line.flashImage.color.b, Mathf.Lerp(0.62f, 0f, p));
            line.coreImage.color = new Color(line.coreImage.color.r, line.coreImage.color.g, line.coreImage.color.b, Mathf.Lerp(0.95f, 0.20f, p));
            line.sweepImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.95f, 0f, p));
        }

        line.group.alpha = alpha;

        float sweepP = Mathf.Clamp01(localTime / Mathf.Max(0.0001f, line.sweepDuration));
        line.sweepRect.anchoredPosition = Vector2.Lerp(line.sweepFrom, line.sweepTo, sweepP);

        float pulse = 1f + Mathf.Sin(localTime * 26f) * 0.035f;
        if (line.axis == Axis.Row)
        {
            line.coreRect.localScale = new Vector3(1f, pulse, 1f);
            line.flashRect.localScale = new Vector3(1f, 1f + Mathf.Sin(localTime * 18f) * 0.05f, 1f);
        }
        else
        {
            line.coreRect.localScale = new Vector3(pulse, 1f, 1f);
            line.flashRect.localScale = new Vector3(1f + Mathf.Sin(localTime * 18f) * 0.05f, 1f, 1f);
        }

        UpdateSparkles(line.sparkles, localTime);
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

            float p = Mathf.Clamp01(localTime / Mathf.Max(0.0001f, s.duration));
            if (p >= 1f)
            {
                SetSparkleAlpha(s, 0f);
                continue;
            }

            Vector2 pos = Vector2.Lerp(s.startPos, s.endPos, p);
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

    private void HideAllVisuals()
    {
        for (int i = 0; i < _lineVisualPool.Count; i++)
        {
            LineVisual line = _lineVisualPool[i];
            if (line == null || line.root == null)
                continue;

            line.active = false;
            line.group.alpha = 0f;
            line.root.localScale = Vector3.one;
            line.root.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        PrepareForReuse();
    }
}