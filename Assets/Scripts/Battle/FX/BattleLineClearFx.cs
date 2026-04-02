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
    [SerializeField] private float preScaleUpDuration = 0.10f;
    [SerializeField] private float preScaleMultiplier = 1.08f;
    [SerializeField] private float cellShrinkDuration = 0.08f;
    [SerializeField] private float intervalBetweenCells = 0.015f;

    [Header("Ease")]
    [SerializeField] private Ease preScaleEase = Ease.OutQuad;
    [SerializeField] private Ease shrinkEase = Ease.InBack;

    [Header("Render Order")]
    [SerializeField] private int fxSortingOrder = 500;

    private RectTransform[,] _cellRects;
    private struct CanvasState
    {
        public Canvas canvas;
        public bool existedBefore;
        public bool prevOverrideSorting;
        public int prevSortingOrder;
    }

    private readonly Dictionary<RectTransform, CanvasState> _promotedCanvasStates = new Dictionary<RectTransform, CanvasState>();

    private Vector3[,] _baseScales;
    private bool _cached;

    private void Awake()
    {
        RebuildCache();
    }

    [ContextMenu("Rebuild Cache")]
    public void RebuildCache()
    {
        _cellRects = new RectTransform[boardSize, boardSize];
        _baseScales = new Vector3[boardSize, boardSize];
        _cached = false;

        if (boardCellRoot == null)
        {
            Debug.LogWarning("[BattleLineClearFx] boardCellRoot is null.");
            return;
        }

        List<RectTransform> allRects = new List<RectTransform>();

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

        // 실제 화면 위치 기준으로 위 -> 아래, 왼 -> 오 정렬
        allRects.Sort((a, b) =>
        {
            Vector2 ap = a.anchoredPosition;
            Vector2 bp = b.anchoredPosition;

            // y 큰 쪽이 위
            if (!Mathf.Approximately(ap.y, bp.y))
                return bp.y.CompareTo(ap.y);

            // 같은 줄이면 x 작은 쪽이 왼쪽
            return ap.x.CompareTo(bp.x);
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
        if (!_cached)
            RebuildCache();

        if (!_cached)
            yield break;

        HashSet<Vector2Int> fullCells = new HashSet<Vector2Int>();

        for (int i = 0; i < completedRows.Count; i++)
        {
            int y = completedRows[i];
            for (int x = 0; x < boardSize; x++)
                fullCells.Add(new Vector2Int(x, y));
        }

        for (int i = 0; i < completedCols.Count; i++)
        {
            int x = completedCols[i];
            for (int y = 0; y < boardSize; y++)
                fullCells.Add(new Vector2Int(x, y));
        }

        PromoteCellsToFront(fullCells);

        // 1) 클리어 대상 셀 전체 살짝 커졌다가 복귀
        foreach (Vector2Int cell in fullCells)
        {
            RectTransform rt = GetCellRect(cell.x, cell.y);
            if (rt == null)
                continue;

            rt.DOKill();
            rt.localScale = _baseScales[cell.x, cell.y];

            Sequence seq = DOTween.Sequence();
            seq.Append(rt.DOScale(_baseScales[cell.x, cell.y] * preScaleMultiplier, preScaleUpDuration * 0.5f).SetEase(preScaleEase));
            seq.Append(rt.DOScale(_baseScales[cell.x, cell.y], preScaleUpDuration * 0.5f).SetEase(preScaleEase));
        }

        yield return new WaitForSeconds(preScaleUpDuration);

        HashSet<Vector2Int> cleared = new HashSet<Vector2Int>();

        // 2) 가로줄: 왼 -> 오
        for (int i = 0; i < completedRows.Count; i++)
        {
            int y = completedRows[i];

            for (int x = 0; x < boardSize; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!cleared.Add(cell))
                    continue;

                yield return AnimateOneCell(cell.x, cell.y, onCellCleared);
            }
        }

        // 3) 세로줄: 위 -> 아래
        for (int i = 0; i < completedCols.Count; i++)
        {
            int x = completedCols[i];

            if (topRowIsMaxY)
            {
                for (int y = boardSize - 1; y >= 0; y--)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (!cleared.Add(cell))
                        continue;

                    yield return AnimateOneCell(cell.x, cell.y, onCellCleared);
                }
            }
            else
            {
                for (int y = 0; y < boardSize; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (!cleared.Add(cell))
                        continue;

                    yield return AnimateOneCell(cell.x, cell.y, onCellCleared);
                }
            }
        }

        PromoteCellsToFront(fullCells);
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

        rt.DOKill();

        Tween t = rt.DOScale(Vector3.zero, cellShrinkDuration).SetEase(shrinkEase);
        yield return t.WaitForCompletion();

        onCellCleared?.Invoke(x, y);

        rt.localScale = _baseScales[x, y];

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
    private void PromoteCellsToFront(HashSet<Vector2Int> cells)
    {
        RestorePromotedCells();

        foreach (Vector2Int cell in cells)
        {
            RectTransform rt = GetCellRect(cell.x, cell.y);
            if (rt == null)
                continue;

            Canvas canvas = rt.GetComponent<Canvas>();
            bool existedBefore = canvas != null;

            if (canvas == null)
                canvas = rt.gameObject.AddComponent<Canvas>();

            CanvasState state = new CanvasState
            {
                canvas = canvas,
                existedBefore = existedBefore,
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

            if (state.existedBefore)
            {
                state.canvas.overrideSorting = state.prevOverrideSorting;
                state.canvas.sortingOrder = state.prevSortingOrder;
            }
            else
            {
                Destroy(state.canvas);
            }
        }

        _promotedCanvasStates.Clear();
    }
}