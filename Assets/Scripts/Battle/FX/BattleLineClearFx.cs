using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class BattleLineClearFx : MonoBehaviour
{
    [Header("Board")]
    [SerializeField] private Transform boardCellRoot;
    [SerializeField] private int boardSize = 8;
    [Tooltip("보드 최상단 줄이 게임 좌표 y = boardSize - 1 이면 체크")]
    [SerializeField] private bool topRowIsMaxY = true;

    [Header("Timing")]
    [SerializeField] private float prePulseDuration = 0.020f;
    [SerializeField] private float prePulseScale = 1.06f;
    [SerializeField] private float impactPunchDuration = 0.035f;
    [SerializeField] private float impactPunchStrength = 0.075f;
    [SerializeField] private float crushDuration = 0.012f;
    [SerializeField] private float crushScaleMultiplier = 0.95f;
    [SerializeField] private float cellShrinkDuration = 0.028f;
    [SerializeField] private float intervalBetweenCells = 0.002f;

    [Header("Ease")]
    [SerializeField] private Ease prePulseEase = Ease.OutQuad;
    [SerializeField] private Ease crushEase = Ease.InQuad;
    [SerializeField] private Ease shrinkEase = Ease.InBack;

    [Header("Render Order")]
    [SerializeField] private int fxSortingOrder = 500;
    [SerializeField] private bool prewarmCellCanvases = true;

    private RectTransform[,] _cellRects;
    private Vector3[,] _baseScales;
    private bool[,] _targetFlags;
    private bool[,] _clearedFlags;
    private readonly List<Vector2Int> _targetCells = new List<Vector2Int>(64);

    private bool _cached;
    private bool _isPlaying;

    private readonly Dictionary<RectTransform, CanvasState> _promotedCanvasStates = new Dictionary<RectTransform, CanvasState>();
    private readonly Dictionary<RectTransform, Canvas> _promoteCanvasCache = new Dictionary<RectTransform, Canvas>();

    private Action<BattleLineClearFx> _returnToPool;

    private struct CanvasState
    {
        public Canvas canvas;
        public bool prevOverrideSorting;
        public int prevSortingOrder;
    }

    private void Awake()
    {
        // 보드 셀 64개는 BattleManager.Start()의 BuildBoards() 이후에 생성됨.
        // 그래서 Awake에서 캐시를 만들면 child count 0 경고가 뜰 수 있음.
        _cached = false;
    }

    private void OnDisable()
    {
        RestoreAllCellsImmediate();
        RestorePromotedCells();
        _isPlaying = false;
    }

    public void SetReturnToPool(Action<BattleLineClearFx> returnAction)
    {
        _returnToPool = returnAction;
    }

    public void PrepareForReuse()
    {
        StopAllCoroutines();
        RestoreAllCellsImmediate();
        RestorePromotedCells();
        _isPlaying = false;
    }

    public void ReturnToPool()
    {
        _returnToPool?.Invoke(this);
    }

    [ContextMenu("Rebuild Cache")]
    public void RebuildCache()
    {
        _cellRects = new RectTransform[boardSize, boardSize];
        _baseScales = new Vector3[boardSize, boardSize];
        _targetFlags = new bool[boardSize, boardSize];
        _clearedFlags = new bool[boardSize, boardSize];
        _targetCells.Clear();
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

        if (allRects.Count < boardSize * boardSize)
        {
            Debug.LogWarning($"[BattleLineClearFx] child count 부족: {allRects.Count} / need {boardSize * boardSize}");
            return;
        }

        allRects.Sort((a, b) =>
        {
            Vector2 ap = a.anchoredPosition;
            Vector2 bp = b.anchoredPosition;

            if (!Mathf.Approximately(ap.y, bp.y))
                return bp.y.CompareTo(ap.y); // y 큰 쪽이 위

            return ap.x.CompareTo(bp.x); // 같은 줄이면 x 작은 쪽이 왼쪽
        });

        int expected = boardSize * boardSize;
        if (allRects.Count > expected)
            allRects.RemoveRange(expected, allRects.Count - expected);

        for (int visualRow = 0; visualRow < boardSize; visualRow++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                int listIndex = visualRow * boardSize + x;
                RectTransform rt = allRects[listIndex];

                int boardY = topRowIsMaxY
                    ? (boardSize - 1 - visualRow)
                    : visualRow;

                _cellRects[x, boardY] = rt;
                _baseScales[x, boardY] = rt.localScale;

                if (prewarmCellCanvases)
                    CachePromoteCanvas(rt);
            }
        }

        _cached = true;
        Debug.Log("[BattleLineClearFx] RebuildCache complete.");
    }

    public IEnumerator Play(
        List<int> completedRows,
        List<int> completedCols,
        Action<int, int> onCellCleared)
    {
        if (_isPlaying)
            yield break;

        if (!_cached)
            RebuildCache();

        if (!_cached)
            yield break;

        _isPlaying = true;

        ClearWorkFlags();
        BuildTargetCells(completedRows, completedCols);

        if (_targetCells.Count == 0)
        {
            _isPlaying = false;
            yield break;
        }

        PromoteCellsToFront(_targetCells);

        // 1) 전체 라인 예열 펄스
        for (int i = 0; i < _targetCells.Count; i++)
        {
            Vector2Int cell = _targetCells[i];
            RectTransform rt = GetCellRect(cell.x, cell.y);
            if (rt == null)
                continue;

            rt.DOKill();
            rt.localScale = _baseScales[cell.x, cell.y];

            rt.DOScale(_baseScales[cell.x, cell.y] * prePulseScale, prePulseDuration)
                .SetEase(prePulseEase);

            rt.DOPunchScale(Vector3.one * impactPunchStrength, impactPunchDuration, 8, 0.75f)
                .SetEase(Ease.OutQuad);
        }

        float preWait = Mathf.Max(prePulseDuration, impactPunchDuration);
        if (preWait > 0f)
            yield return new WaitForSeconds(Mathf.Min(0.018f, preWait * 0.30f));

        // 2) 가로줄: 왼 -> 오
        if (completedRows != null)
        {
            for (int i = 0; i < completedRows.Count; i++)
            {
                int y = completedRows[i];

                for (int x = 0; x < boardSize; x++)
                {
                    if (_clearedFlags[x, y])
                        continue;

                    _clearedFlags[x, y] = true;
                    yield return AnimateOneCell(x, y, onCellCleared);
                }
            }
        }

        // 3) 세로줄: 위 -> 아래
        if (completedCols != null)
        {
            for (int i = 0; i < completedCols.Count; i++)
            {
                int x = completedCols[i];

                if (topRowIsMaxY)
                {
                    for (int y = boardSize - 1; y >= 0; y--)
                    {
                        if (_clearedFlags[x, y])
                            continue;

                        _clearedFlags[x, y] = true;
                        yield return AnimateOneCell(x, y, onCellCleared);
                    }
                }
                else
                {
                    for (int y = 0; y < boardSize; y++)
                    {
                        if (_clearedFlags[x, y])
                            continue;

                        _clearedFlags[x, y] = true;
                        yield return AnimateOneCell(x, y, onCellCleared);
                    }
                }
            }
        }

        RestorePromotedCells();
        _isPlaying = false;
    }

    private void ClearWorkFlags()
    {
        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                _targetFlags[x, y] = false;
                _clearedFlags[x, y] = false;
            }
        }

        _targetCells.Clear();
    }

    private void BuildTargetCells(List<int> completedRows, List<int> completedCols)
    {
        if (completedRows != null)
        {
            for (int i = 0; i < completedRows.Count; i++)
            {
                int y = completedRows[i];
                if (y < 0 || y >= boardSize)
                    continue;

                for (int x = 0; x < boardSize; x++)
                    AddTargetCell(x, y);
            }
        }

        if (completedCols != null)
        {
            for (int i = 0; i < completedCols.Count; i++)
            {
                int x = completedCols[i];
                if (x < 0 || x >= boardSize)
                    continue;

                for (int y = 0; y < boardSize; y++)
                    AddTargetCell(x, y);
            }
        }
    }

    private void AddTargetCell(int x, int y)
    {
        if (_targetFlags[x, y])
            return;

        _targetFlags[x, y] = true;
        _targetCells.Add(new Vector2Int(x, y));
    }

    private IEnumerator AnimateOneCell(int x, int y, Action<int, int> onCellCleared)
    {
        RectTransform rt = GetCellRect(x, y);

        if (rt == null)
        {
            onCellCleared?.Invoke(x, y);
            yield return null;
            yield break;
        }

        Vector3 baseScale = _baseScales[x, y];
        rt.DOKill();
        rt.localScale = baseScale;

        Sequence seq = DOTween.Sequence();
        seq.Append(rt.DOScale(baseScale * crushScaleMultiplier, crushDuration).SetEase(crushEase));
        seq.Append(rt.DOScale(Vector3.zero, cellShrinkDuration).SetEase(shrinkEase));
        seq.OnComplete(() =>
        {
            if (rt != null)
                rt.localScale = baseScale;
        });

        // 노말모드처럼 "훑고 지나가는" 느낌을 주기 위해
        // 트윈 완료를 기다리지 않고, 초반에 바로 클리어를 호출한다.
        float clearDelay = Mathf.Min(0.010f, crushDuration + cellShrinkDuration * 0.18f);
        if (clearDelay > 0f)
            yield return new WaitForSeconds(clearDelay);

        onCellCleared?.Invoke(x, y);

        // 다음 셀은 아주 짧은 간격만 두고 바로 진행
        if (intervalBetweenCells > 0f)
            yield return new WaitForSeconds(intervalBetweenCells);
    }

    private RectTransform GetCellRect(int x, int y)
    {
        if (!_cached)
            return null;

        if (x < 0 || x >= boardSize || y < 0 || y >= boardSize)
            return null;

        return _cellRects[x, y];
    }

    private Canvas CachePromoteCanvas(RectTransform rt)
    {
        if (rt == null)
            return null;

        if (_promoteCanvasCache.TryGetValue(rt, out Canvas cached) && cached != null)
            return cached;

        Canvas canvas = rt.GetComponent<Canvas>();
        if (canvas == null)
            canvas = rt.gameObject.AddComponent<Canvas>();

        _promoteCanvasCache[rt] = canvas;
        return canvas;
    }

    private void PromoteCellsToFront(List<Vector2Int> cells)
    {
        RestorePromotedCells();

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            RectTransform rt = GetCellRect(cell.x, cell.y);
            if (rt == null)
                continue;

            Canvas canvas = CachePromoteCanvas(rt);
            if (canvas == null)
                continue;

            CanvasState state = new CanvasState
            {
                canvas = canvas,
                prevOverrideSorting = canvas.overrideSorting,
                prevSortingOrder = canvas.sortingOrder
            };

            canvas.overrideSorting = true;
            canvas.sortingOrder = fxSortingOrder;
            _promotedCanvasStates[rt] = state;
        }
    }

    private void RestorePromotedCells()
    {
        foreach (KeyValuePair<RectTransform, CanvasState> pair in _promotedCanvasStates)
        {
            RectTransform rt = pair.Key;
            CanvasState state = pair.Value;

            if (rt == null || state.canvas == null)
                continue;

            state.canvas.overrideSorting = state.prevOverrideSorting;
            state.canvas.sortingOrder = state.prevSortingOrder;
        }

        _promotedCanvasStates.Clear();
    }

    private void RestoreAllCellsImmediate()
    {
        if (_cellRects == null || _baseScales == null)
            return;

        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                RectTransform rt = _cellRects[x, y];
                if (rt == null)
                    continue;

                rt.DOKill();
                rt.localScale = _baseScales[x, y];
            }
        }
    }
}