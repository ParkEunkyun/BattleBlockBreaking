using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class NormalManager : MonoBehaviour, IDragBlockOwner
{
    // ????????????????????????????????????????????
    //  상수
    // ????????????????????????????????????????????
    private const string RuntimePrefix = "BBB_NRM_";
    private const string HighScoreKey = "BBB_NORMAL_HIGHSCORE";
    private const int ScorePerCell = 1;

    // 멀티 라인 보너스 (index = 줄 수, 5 이상은 마지막 값)
    private static readonly int[] MultiLineBonusTable = { 0, 10, 50, 150, 350, 700 };

    // 콤보 기본 보너스
    private const int ComboScorePerCount = 10;

    // 콤보 마일스톤
    private static readonly (int threshold, int bonus)[] ComboMilestones =
        { (10, 150), (20, 400), (30, 1000) };

    // 드랍 아이템 기본 확률
    private const float BaseDropGold = 0.02f;
    private const float BaseDropStoneBasic = 0.008f;
    private const float BaseDropStoneMid = 0.003f;
    private const float BaseDropStoneHigh = 0.0005f;
    private const int DropMaxSets = 3;
    private const float DropOccupancyLimit = 0.7f;

    // ????????????????????????????????????????????
    //  내부 타입
    // ????????????????????????????????????????????
    [System.Serializable]
    private sealed class SlotRefs
    {
        public RectTransform root;
        public DraggableBlockView dragView;
        public Image background;
        public RectTransform previewRoot;
    }

    private sealed class ArtifactSlot
    {
        public NormalArtifactDefinition Def;
        public INormalArtifactEffect Effect;
        public int CooldownRemaining;
        public bool IsReady => CooldownRemaining <= 0;
        // UI
        public RectTransform Root;
        public Image IconImage;
        public TMP_Text CooldownText;
        public Image ReadyGlow;
    }

    // ????????????????????????????????????????????
    //  인스펙터
    // ????????????????????????????????????????????
    [Header("Prefabs / Sprites")]
    [SerializeField] private GameObject boardCellPrefab;
    [SerializeField] private GameObject previewCellPrefab;
    [SerializeField] private BattleBlockSpriteSet blockSprites;

    [Header("Board")]
    [SerializeField] private Vector2 myBoardCellSize = new Vector2(64f, 64f);
    [SerializeField] private Vector2 myBoardSpacing = new Vector2(4f, 4f);

    [Header("Preview")]
    [SerializeField] private float slotPreviewCellSize = 24f;
    [SerializeField] private float slotPreviewSpacing = 4f;

    [Header("Drag")]
    [SerializeField] private float dragPreviewCellSize = 30f;
    [SerializeField] private float dragPreviewSpacing = 4f;
    [SerializeField] private Vector2 dragOffset = new Vector2(0f, 180f);

    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "Scene_Lobby";

    [Header("Drop Item Sprites")]
    [SerializeField] private Sprite goldSprite;
    [SerializeField] private Sprite stoneBasicSprite;
    [SerializeField] private Sprite stoneMidSprite;
    [SerializeField] private Sprite stoneHighSprite;

    [Header("UI (자동 탐색)")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private GameObject resultPhaseRoot;
    [SerializeField] private TMP_Text resultScoreText;
    [SerializeField] private TMP_Text resultBestScoreText;
    [SerializeField] private GameObject resultStateBelow;   // 최고기록보다 낮을 때
    [SerializeField] private GameObject resultStateEqual;   // 최고기록과 동일할 때
    [SerializeField] private GameObject resultStateNew;     // 최고기록 갱신 시
    [SerializeField] private Transform resultDropRoot;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button resultLobbyButton;

    // ????????????????????????????????????????????
    //  런타임 - 보드
    // ????????????????????????????????????????????
    private Canvas _canvas;
    private RectTransform _dragLayer;
    private RectTransform _myBoardRoot;

    private readonly BoardCell[,] _myBoardCells = new BoardCell[BattleBlockCore.BoardSize, BattleBlockCore.BoardSize];
    private readonly bool[,] _myOccupied = new bool[BattleBlockCore.BoardSize, BattleBlockCore.BoardSize];
    private readonly Color[,] _myColors = new Color[BattleBlockCore.BoardSize, BattleBlockCore.BoardSize];
    private readonly Sprite[,] _myBlockSprites = new Sprite[BattleBlockCore.BoardSize, BattleBlockCore.BoardSize];

    private readonly List<BattleBlockShape> _normalShapes = new List<BattleBlockShape>();
    private readonly List<BattleBlockShape> _curseShapes = new List<BattleBlockShape>();
    private readonly BattleBlockInstance[] _currentBlocks = new BattleBlockInstance[3];
    private readonly SlotRefs[] _slotRefs = new SlotRefs[3];

    // 드래그
    private int _dragSlotIndex = -1;
    private bool _dragCanPlace;
    private bool _dragHasAnchor;
    private Vector2Int _dragAnchor;
    private RectTransform _dragPreviewRoot;

    // ????????????????????????????????????????????
    //  런타임 - 점수 / 콤보
    // ????????????????????????????????????????????
    private int _myScore;
    private int _combo;
    private int _setIndex;
    private int _clearCountThisSet;
    private bool _isGameOver;

    private float _scoreMultiplier = 1f;
    private int _scoreMultiplierRemaining = 0;

    // 기록용
    private int _singleCount, _doubleCount, _tripleCount, _quadCount, _perfectCount;
    private int _maxCombo, _totalClearCount, _artifactActivationCount;

    // ????????????????????????????????????????????
    //  런타임 - 드랍 아이템
    // ????????????????????????????????????????????
    private readonly List<BoardDropItem> _boardDropItems = new List<BoardDropItem>();
    private int _earnedGold, _earnedStoneBasic, _earnedStoneMid, _earnedStoneHigh;

    // ????????????????????????????????????????????
    //  런타임 - 아티팩트
    // ????????????????????????????????????????????
    private readonly ArtifactSlot[] _artifactSlots = new ArtifactSlot[4];

    // ????????????????????????????????????????????
    //  초기화
    // ????????????????????????????????????????????
    private void Awake()
    {
        CacheCanvas();
        CreateDragLayer();
        CacheHierarchy();
        BattleBlockCore.BuildShapeLibrary(_normalShapes, _curseShapes, blockSprites);
        BuildMyBoard();
        BindButtons();
        InitArtifactSlotUI();
        LoadArtifactsFromSession();
        StartNewRun();
    }

    private void CacheCanvas()
    {
        _canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
    }

    private void CreateDragLayer()
    {
        Transform root = _canvas != null ? _canvas.transform : transform;
        var go = new GameObject(RuntimePrefix + "DragLayer", typeof(RectTransform));
        go.transform.SetParent(root, false);
        go.transform.SetAsLastSibling();
        _dragLayer = go.GetComponent<RectTransform>();
        _dragLayer.anchorMin = Vector2.zero;
        _dragLayer.anchorMax = Vector2.one;
        _dragLayer.offsetMin = _dragLayer.offsetMax = Vector2.zero;
    }

    private void CacheHierarchy()
    {
        Transform sa = FindSafeArea();
        if (sa == null) { Debug.LogError("[NormalManager] SafeArea 없음"); return; }

        scoreText = scoreText ?? FindTMP(sa, "TopHudRoot/MyScorePanel/MyScoreText");
        bestScoreText = bestScoreText ?? FindTMP(sa, "TopHudRoot/BestScorePanel/BestScoreText");
        comboText = comboText ?? FindTMP(sa, "TopHudRoot/ComboPanel/ComboText");
        _myBoardRoot = _myBoardRoot ?? FindRect(sa, "BoardRoot/MyBoardRoot");
        resultPhaseRoot = resultPhaseRoot ?? FindGO(sa, "ResultPhaseRoot");
        resultScoreText = resultScoreText ?? FindTMP(sa, "ResultPhaseRoot/ResultScoreText");
        resultBestScoreText = resultBestScoreText ?? FindTMP(sa, "ResultPhaseRoot/ResultBestScoreText");
        resultStateBelow = resultStateBelow ?? FindGO(sa, "ResultPhaseRoot/ResultStateBelow");
        resultStateEqual = resultStateEqual ?? FindGO(sa, "ResultPhaseRoot/ResultStateEqual");
        resultStateNew = resultStateNew ?? FindGO(sa, "ResultPhaseRoot/ResultStateNew");
        resultDropRoot = resultDropRoot ?? FindRect(sa, "ResultPhaseRoot/DropItemRoot");
        retryButton = retryButton ?? FindButton(sa, "ResultPhaseRoot/RetryButton");
        resultLobbyButton = resultLobbyButton ?? FindButton(sa, "ResultPhaseRoot/ResultLobbyButton");

        for (int i = 0; i < 3; i++)
        {
            Transform slot = sa.Find($"CurrentBlocksRoot/BlockSlot{i + 1}");
            if (slot == null) continue;

            var refs = new SlotRefs
            {
                root = slot as RectTransform,
                background = GetOrAdd<Image>(slot.gameObject),
                dragView = GetOrAdd<DraggableBlockView>(slot.gameObject)
            };
            refs.dragView.Setup((IDragBlockOwner)this, i);

            Transform prev = slot.Find("PreviewRoot");
            if (prev == null)
            {
                var pg = new GameObject("PreviewRoot", typeof(RectTransform));
                pg.transform.SetParent(slot, false);
                var rt = pg.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
                prev = rt;
            }
            refs.previewRoot = prev as RectTransform;
            _slotRefs[i] = refs;
        }
    }

    private void BuildMyBoard()
    {
        if (_myBoardRoot == null) { Debug.LogError("[NormalManager] MyBoardRoot 없음"); return; }
        DestroyRuntimeChildren(_myBoardRoot);

        float stepX = myBoardCellSize.x + myBoardSpacing.x;
        float stepY = myBoardCellSize.y + myBoardSpacing.y;
        float startX = -((BattleBlockCore.BoardSize - 1) * stepX) * 0.5f;
        float startY = ((BattleBlockCore.BoardSize - 1) * stepY) * 0.5f;

        for (int y = 0; y < BattleBlockCore.BoardSize; y++)
            for (int x = 0; x < BattleBlockCore.BoardSize; x++)
            {
                var go = CreateCellObject(_myBoardRoot);
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = myBoardCellSize;
                rt.anchoredPosition = new Vector2(startX + x * stepX, startY - y * stepY);
                _myBoardCells[x, y] = GetOrAdd<BoardCell>(go);
            }
    }

    private void BindButtons()
    {
        retryButton?.onClick.AddListener(()
            => SceneManager.LoadScene(SceneManager.GetActiveScene().name));
        resultLobbyButton?.onClick.AddListener(()
            => SceneManager.LoadScene(lobbySceneName));
    }

    private void InitArtifactSlotUI()
    {
        Transform sa = FindSafeArea();
        if (sa == null) return;

        for (int i = 0; i < 4; i++)
        {
            Transform slotTr = sa.Find($"ArtifactRoot/ArtifactSlot{i + 1}");
            if (slotTr == null) continue;

            _artifactSlots[i] = new ArtifactSlot
            {
                Root = slotTr as RectTransform,
                IconImage = FindComp<Image>(slotTr, "Icon"),
                CooldownText = FindTMP(slotTr, "CooldownText"),
                ReadyGlow = FindComp<Image>(slotTr, "ReadyGlow")
            };

            int cap = i;
            GetOrAdd<Button>(slotTr.gameObject).onClick.AddListener(()
                => OnClickArtifactSlot(cap));
        }
    }

    // ── 외부에서 아티팩트 주입 (LobbyController가 호출) ──
    public void SetArtifact(int slotIndex, NormalArtifactDefinition def)
    {
        var slot = _artifactSlots[slotIndex];
        if (slot == null) return;
        slot.Def = def;
        slot.Effect = NormalArtifactEffectFactory.Create(def);
        slot.CooldownRemaining = 0;
        if (slot.IconImage != null) slot.IconImage.sprite = def.icon;
    }

    /// <summary>NormalArtifactSession에서 선택된 아티팩트를 슬롯에 자동 주입</summary>
    private void LoadArtifactsFromSession()
    {
        var selected = NormalArtifactSession.Selected;
        for (int i = 0; i < selected.Count && i < _artifactSlots.Length; i++)
            SetArtifact(i, selected[i]);
    }

    // ????????????????????????????????????????????
    //  새 판 시작
    // ????????????????????????????????????????????
    private void StartNewRun()
    {
        _isGameOver = false;
        _myScore = 0;
        _combo = 0;
        _setIndex = 0;
        _clearCountThisSet = 0;
        _scoreMultiplier = 1f;
        _scoreMultiplierRemaining = 0;
        _dragSlotIndex = -1;

        _maxCombo = _totalClearCount = _artifactActivationCount = 0;
        _singleCount = _doubleCount = _tripleCount = _quadCount = _perfectCount = 0;
        _earnedGold = _earnedStoneBasic = _earnedStoneMid = _earnedStoneHigh = 0;

        _boardDropItems.Clear();

        for (int y = 0; y < BattleBlockCore.BoardSize; y++)
            for (int x = 0; x < BattleBlockCore.BoardSize; x++)
            {
                _myOccupied[x, y] = false;
                _myColors[x, y] = Color.clear;
                _myBlockSprites[x, y] = null;
            }

        if (resultPhaseRoot != null) resultPhaseRoot.SetActive(false);
        ResetCurrentBlocks();
        RefreshAllUI();
    }

    private void ResetCurrentBlocks()
    {
        for (int i = 0; i < _currentBlocks.Length; i++)
            _currentBlocks[i] = BattleBlockCore.CreateRandomNormalBlock(_normalShapes);
    }

    // ????????????????????????????????????????????
    //  IDragBlockOwner
    // ????????????????????????????????????????????
    public bool CanAcceptUserInput()
        => !_isGameOver && (resultPhaseRoot == null || !resultPhaseRoot.activeSelf);

    public void OnBeginDragSlot(int slotIndex, PointerEventData eventData)
    {
        if (!CanAcceptUserInput() || _currentBlocks[slotIndex] == null) return;
        _dragSlotIndex = slotIndex;
        _dragCanPlace = false;
        _dragHasAnchor = false;
        CreateDragPreview(_currentBlocks[slotIndex]);
        UpdateDrag(eventData);
        RefreshBoardVisual();
        RefreshBlockSlots();
    }

    public void OnDragSlot(int slotIndex, PointerEventData eventData)
    {
        if (!CanAcceptUserInput() || _dragSlotIndex != slotIndex) return;
        UpdateDrag(eventData);
        RefreshBoardVisual();
    }

    public void OnEndDragSlot(int slotIndex, PointerEventData eventData)
    {
        if (_dragSlotIndex != slotIndex) return;
        if (_dragHasAnchor && _dragCanPlace) ExecutePlaceBlock(slotIndex, _dragAnchor);
        _dragSlotIndex = -1;
        _dragCanPlace = false;
        _dragHasAnchor = false;
        DestroyDragPreview();
        RefreshBoardVisual();
        RefreshBlockSlots();
    }

    // ????????????????????????????????????????????
    //  드래그 연산
    // ????????????????????????????????????????????
    private void UpdateDrag(PointerEventData ev)
    {
        if (_dragPreviewRoot == null) return;
        Vector2 adj = ev.position + dragOffset;
        Camera cam = ev.pressEventCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _dragLayer, adj, cam, out Vector2 local))
            _dragPreviewRoot.anchoredPosition = local;

        var block = _currentBlocks[_dragSlotIndex];
        _dragHasAnchor = TryGetBoardAnchor(adj, cam, block, out _dragAnchor);
        _dragCanPlace = _dragHasAnchor &&
                         BattleBlockCore.CanPlaceBlock(block, _myOccupied, _dragAnchor.x, _dragAnchor.y);
    }

    private bool TryGetBoardAnchor(Vector2 screenPos, Camera cam,
        BattleBlockInstance block, out Vector2Int anchor)
    {
        anchor = Vector2Int.zero;
        if (_myBoardRoot == null) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _myBoardRoot, screenPos, cam, out Vector2 local)) return false;

        float stepX = myBoardCellSize.x + myBoardSpacing.x;
        float stepY = myBoardCellSize.y + myBoardSpacing.y;
        float ox = -((BattleBlockCore.BoardSize - 1) * stepX) * 0.5f;
        float oy = ((BattleBlockCore.BoardSize - 1) * stepY) * 0.5f;

        int w = GetBlockWidth(block), h = GetBlockHeight(block);
        float tlX = local.x - ((w - 1) * stepX) * 0.5f;
        float tlY = local.y + ((h - 1) * stepY) * 0.5f;

        anchor = new Vector2Int(
            Mathf.RoundToInt((tlX - ox) / stepX),
            Mathf.RoundToInt((oy - tlY) / stepY));
        return true;
    }

    // ????????????????????????????????????????????
    //  블록 배치 메인 로직
    // ????????????????????????????????????????????
    private void ExecutePlaceBlock(int slotIndex, Vector2Int anchor)
    {
        var block = _currentBlocks[slotIndex];
        if (block == null) return;
        if (!BattleBlockCore.CanPlaceBlock(block, _myOccupied, anchor.x, anchor.y)) return;

        // 1. 배치
        BattleBlockCore.PlaceBlock(block, _myOccupied, _myColors, _myBlockSprites, anchor.x, anchor.y);
        CollectDropItemsUnder(block, anchor);

        // 2. 배치 점수 + 아티팩트 훅
        int placedScore = block.CellCount * ScorePerCell;
        NotifyBlockPlaced(block.CellCount);
        AddScore(placedScore);

        // 3. 라인 클리어
        int cleared = BattleBlockCore.ClearCompletedLines(_myOccupied, _myColors, _myBlockSprites);
        if (cleared > 0)
        {
            _clearCountThisSet++;
            _totalClearCount += cleared;
            RecordMultiLine(cleared);
            ProcessClearScore(cleared);
            NotifyLineClear(cleared);
        }

        _currentBlocks[slotIndex] = null;

        // 4. 세트 종료
        if (AreAllBlockSlotsEmpty())
            ProcessSetEnd();
        else
            RefreshAllUI();

        // 5. 게임오버 체크
        CheckGameOver();
    }

    private void ProcessClearScore(int lines)
    {
        int idx = Mathf.Clamp(lines, 0, MultiLineBonusTable.Length - 1);
        int bonus = MultiLineBonusTable[idx];

        // ScoreBoost 패시브 클리어 보너스 배율
        float clearMult = 1f;
        foreach (var s in _artifactSlots)
            if (s?.Effect is ScoreBoostEffect sb)
                clearMult *= sb.GetLineClearBonusMultiplier();

        // LuckyBonus 배율
        float luckyMult = 1f;
        foreach (var s in _artifactSlots)
            if (s?.Effect is LuckyBonusEffect lb)
                luckyMult = Mathf.Max(luckyMult, lb.RollLuckyMultiplier());

        AddScore(Mathf.RoundToInt(bonus * clearMult * luckyMult));
    }

    private void ProcessSetEnd()
    {
        bool hadClear = _clearCountThisSet > 0;

        // ── 콤보 처리 ──────────────────────────
        if (hadClear)
        {
            _combo += _clearCountThisSet;

            // 콤보 보너스
            int comboBonus = _combo * ComboScorePerCount;
            foreach (var s in _artifactSlots)
                if (s?.Effect is ComboBoostEffect cb)
                    comboBonus += _combo * cb.GetBonusScorePerCombo();
            AddScore(comboBonus);

            // 마일스톤
            foreach (var (threshold, bonus) in ComboMilestones)
            {
                if (_combo >= threshold && _combo - _clearCountThisSet < threshold)
                {
                    float mult = 1f;
                    foreach (var s in _artifactSlots)
                        if (s?.Effect is ComboBoostEffect cb) mult *= cb.GetMilestoneBonusMultiplier();
                    AddScore(Mathf.RoundToInt(bonus * mult));
                }
            }

            _maxCombo = Mathf.Max(_maxCombo, _combo);
        }
        else
        {
            // 콤보 리셋 (면제 체크)
            bool exempted = false;
            foreach (var s in _artifactSlots)
                if (s?.Effect is ComboBoostEffect cb && cb.TryExemptComboReset())
                { exempted = true; break; }
            if (!exempted) _combo = 0;
        }

        // ── 아티팩트 SetEnd 훅 + 쿨다운 ────────
        var ctx = BuildContext();
        foreach (var slot in _artifactSlots)
        {
            if (slot?.Effect == null) continue;
            slot.Effect.OnSetEnd(ctx, hadClear);
            if (slot.CooldownRemaining > 0) slot.CooldownRemaining--;

            // 자동 발동 체크
            if (slot.Def?.triggerType == ArtifactTriggerType.AutoActive
                && slot.IsReady && slot.Effect.CanActivate(ctx))
                TryActivateArtifact(slot, ctx);
        }

        // ComboBoost 특수 자동 발동 (N콤보 배수마다)
        foreach (var slot in _artifactSlots)
        {
            if (slot?.Def?.category != ArtifactCategory.ComboBoost) continue;
            if (slot.Def.triggerType == ArtifactTriggerType.PassiveOnly) continue;
            if (!slot.IsReady || slot.Def.autoTriggerComboCount <= 0) continue;
            if (hadClear && _combo > 0 && _combo % slot.Def.autoTriggerComboCount == 0)
                TryActivateArtifact(slot, ctx);
        }

        // ── 드랍 아이템 ─────────────────────────
        TrySpawnDropItems();
        ExpireDropItems();

        // ── 다음 세트 ────────────────────────────
        _setIndex++;
        _clearCountThisSet = 0;
        ResetCurrentBlocks();
        RefreshAllUI();
    }

    // ????????????????????????????????????????????
    //  아티팩트 발동
    // ????????????????????????????????????????????
    private void TryActivateArtifact(ArtifactSlot slot, NormalArtifactContext ctx)
    {
        if (!slot.IsReady || !slot.Effect.CanActivate(ctx)) return;
        slot.Effect.Activate(ctx);
        slot.CooldownRemaining = slot.Def.cooldownSets;
        _artifactActivationCount++;
        RefreshArtifactSlotUI(slot);
    }

    private void OnClickArtifactSlot(int index)
    {
        if (!CanAcceptUserInput()) return;
        var slot = _artifactSlots[index];
        if (slot?.Def == null || slot.Def.triggerType != ArtifactTriggerType.ManualActive) return;
        if (!slot.IsReady) return;
        TryActivateArtifact(slot, BuildContext());
        RefreshAllUI();
    }

    // ????????????????????????????????????????????
    //  드랍 아이템
    // ????????????????????????????????????????????
    private void TrySpawnDropItems()
    {
        if (GetBoardOccupancy() >= DropOccupancyLimit) return;
        float mult = GetDropRateMultiplier();
        TrySpawnDrop(DropItemType.Gold, BaseDropGold * mult);
        TrySpawnDrop(DropItemType.EnhanceStoneBasic, BaseDropStoneBasic * mult);
        TrySpawnDrop(DropItemType.EnhanceStoneMid, BaseDropStoneMid * mult);
        TrySpawnDrop(DropItemType.EnhanceStoneHigh, BaseDropStoneHigh * mult);
    }

    private void TrySpawnDrop(DropItemType type, float chance)
    {
        if (Random.value > chance) return;
        var empty = new List<Vector2Int>();
        for (int y = 0; y < BattleBlockCore.BoardSize; y++)
            for (int x = 0; x < BattleBlockCore.BoardSize; x++)
                if (!_myOccupied[x, y] && GetDropAt(x, y) == null)
                    empty.Add(new Vector2Int(x, y));
        if (empty.Count == 0) return;

        var pos = empty[Random.Range(0, empty.Count)];
        _boardDropItems.Add(new BoardDropItem
        {
            ItemType = type,
            BoardX = pos.x,
            BoardY = pos.y,
            RemainingSetCount = DropMaxSets + GetDropKeepBonus()
        });
    }

    public void SpawnDropItemImmediate() => TrySpawnDrop(DropItemType.Gold, 1f);

    private void CollectDropItemsUnder(BattleBlockInstance block, Vector2Int anchor)
    {
        for (int i = _boardDropItems.Count - 1; i >= 0; i--)
        {
            var drop = _boardDropItems[i];
            foreach (var c in block.cells)
            {
                if (anchor.x + c.x == drop.BoardX && anchor.y + c.y == drop.BoardY)
                {
                    switch (drop.ItemType)
                    {
                        case DropItemType.Gold: _earnedGold++; break;
                        case DropItemType.EnhanceStoneBasic: _earnedStoneBasic++; break;
                        case DropItemType.EnhanceStoneMid: _earnedStoneMid++; break;
                        case DropItemType.EnhanceStoneHigh: _earnedStoneHigh++; break;
                    }
                    _boardDropItems.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private void ExpireDropItems()
    {
        for (int i = _boardDropItems.Count - 1; i >= 0; i--)
        {
            _boardDropItems[i].RemainingSetCount--;
            if (_boardDropItems[i].RemainingSetCount <= 0)
                _boardDropItems.RemoveAt(i);
        }
    }

    private BoardDropItem GetDropAt(int x, int y)
    {
        foreach (var d in _boardDropItems)
            if (d.BoardX == x && d.BoardY == y) return d;
        return null;
    }

    private float GetBoardOccupancy()
    {
        int c = 0;
        for (int y = 0; y < BattleBlockCore.BoardSize; y++)
            for (int x = 0; x < BattleBlockCore.BoardSize; x++)
                if (_myOccupied[x, y]) c++;
        return (float)c / (BattleBlockCore.BoardSize * BattleBlockCore.BoardSize);
    }

    private float GetDropRateMultiplier()
    {
        float m = 1f;
        foreach (var s in _artifactSlots)
            if (s?.Effect is LuckyBonusEffect lb) m *= lb.GetDropRateMultiplier();
        return m;
    }

    private int GetDropKeepBonus()
    {
        int b = 0;
        foreach (var s in _artifactSlots)
            if (s?.Effect is LuckyBonusEffect lb) b += lb.GetDropItemKeepBonus();
        return b;
    }

    // ????????????????????????????????????????????
    //  게임오버
    // ????????????????????????????????????????????
    private void CheckGameOver()
    {
        if (BattleBlockCore.HasAnyPlaceableMove(_currentBlocks, _myOccupied)) return;

        var ctx = BuildContext();
        foreach (var slot in _artifactSlots)
        {
            if (slot?.Effect == null) continue;
            if (slot.Effect.OnGameOverCheck(ctx))
            {
                _artifactActivationCount++;
                RefreshAllUI();
                return;
            }
        }
        ShowGameOver();
    }

    private void ShowGameOver()
    {
        _isGameOver = true;
        DestroyDragPreview();

        int prev = GetBestScore();
        int best = SaveBestScore(_myScore);
        var state = _myScore > prev ? BestScoreState.New
                  : _myScore == prev ? BestScoreState.Equal
                  : BestScoreState.Below;

        if (resultPhaseRoot != null) resultPhaseRoot.SetActive(true);
        if (resultScoreText != null) resultScoreText.text = $"점수 : {_myScore:N0}";
        if (resultBestScoreText != null) resultBestScoreText.text = $"최고 : {best:N0}";
        if (resultStateBelow != null) resultStateBelow.SetActive(state == BestScoreState.Below);
        if (resultStateEqual != null) resultStateEqual.SetActive(state == BestScoreState.Equal);
        if (resultStateNew != null) resultStateNew.SetActive(state == BestScoreState.New);

        BuildResultDropUI();
    }

    private void BuildResultDropUI()
    {
        if (resultDropRoot == null) return;
        DestroyRuntimeChildren(resultDropRoot);

        void Add(Sprite spr, int count, string label)
        {
            if (count <= 0 || spr == null) return;
            var go = new GameObject("DropEntry", typeof(RectTransform));
            go.transform.SetParent(resultDropRoot, false);
            var img = go.AddComponent<Image>();
            img.sprite = spr;
            img.preserveAspect = true;
            var tgo = new GameObject("Count", typeof(RectTransform));
            tgo.transform.SetParent(go.transform, false);
            tgo.AddComponent<TMP_Text>().text = $"{label} × {count}";
        }

        Add(goldSprite, _earnedGold, "골드");
        Add(stoneBasicSprite, _earnedStoneBasic, "강화석(하)");
        Add(stoneMidSprite, _earnedStoneMid, "강화석(중)");
        Add(stoneHighSprite, _earnedStoneHigh, "강화석(상)");
    }

    // ????????????????????????????????????????????
    //  점수 / 배율
    // ????????????????????????????????????????????
    private void AddScore(int amount)
    {
        if (amount <= 0) return;
        float m = _scoreMultiplierRemaining > 0 ? _scoreMultiplier : 1f;
        _myScore += Mathf.RoundToInt(amount * m);
    }

    public void SetScoreMultiplier(float mult, int durationSets)
    {
        _scoreMultiplier = mult;
        _scoreMultiplierRemaining = durationSets;
    }

    public void ClearBoardByRatio(float ratio)
    {
        var occ = new List<Vector2Int>();
        for (int y = 0; y < BattleBlockCore.BoardSize; y++)
            for (int x = 0; x < BattleBlockCore.BoardSize; x++)
                if (_myOccupied[x, y]) occ.Add(new Vector2Int(x, y));

        int count = Mathf.RoundToInt(occ.Count * ratio);
        for (int i = occ.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (occ[i], occ[j]) = (occ[j], occ[i]);
        }
        for (int i = 0; i < Mathf.Min(count, occ.Count); i++)
        {
            int x = occ[i].x, y = occ[i].y;
            _myOccupied[x, y] = false;
            _myColors[x, y] = Color.clear;
            _myBlockSprites[x, y] = null;
        }
    }

    // ????????????????????????????????????????????
    //  아티팩트 컨텍스트
    // ????????????????????????????????????????????
    private NormalArtifactContext BuildContext()
    {
        var ctx = new NormalArtifactContext();
        ctx.Init(_myScore, _combo, _setIndex, _myOccupied, _currentBlocks);
        ctx.AddScore = AddScore;
        ctx.RerollAllBlocks = ResetCurrentBlocks;
        ctx.AddComboCount = n => _combo += n;
        ctx.SetScoreMultiplierForSets = m => SetScoreMultiplier(m, 5); // 기본 5세트
        ctx.ClearBoardRatio = ClearBoardByRatio;
        ctx.SpawnDropItem = SpawnDropItemImmediate;
        ctx.TryConsumeRerollToken = () => true;
        return ctx;
    }

    // ????????????????????????????????????????????
    //  아티팩트 훅 통지
    // ????????????????????????????????????????????
    private void NotifyBlockPlaced(int cellCount)
    {
        var ctx = BuildContext();
        foreach (var s in _artifactSlots) s?.Effect?.OnBlockPlaced(ctx, cellCount);
    }

    private void NotifyLineClear(int lineCount)
    {
        var ctx = BuildContext();
        foreach (var s in _artifactSlots) s?.Effect?.OnLineClear(ctx, lineCount);
    }

    // ????????????????????????????????????????????
    //  기록
    // ????????????????????????????????????????????
    private void RecordMultiLine(int lines)
    {
        if (lines >= 5) _perfectCount++;
        else if (lines == 4) _quadCount++;
        else if (lines == 3) _tripleCount++;
        else if (lines == 2) _doubleCount++;
        else _singleCount++;
    }

    // ????????????????????????????????????????????
    //  UI 갱신
    // ????????????????????????????????????????????
    private void RefreshAllUI()
    {
        RefreshBoardVisual();
        RefreshBlockSlots();
        RefreshScoreUI();
        RefreshArtifactUI();
    }

    private void RefreshBoardVisual()
    {
        for (int y = 0; y < BattleBlockCore.BoardSize; y++)
            for (int x = 0; x < BattleBlockCore.BoardSize; x++)
            {
                var cell = _myBoardCells[x, y];
                if (cell == null) continue;

                bool occ = _myOccupied[x, y];
                Color col = occ ? _myColors[x, y] : BattleBlockCore.BoardBaseColor;
                Sprite spr = occ ? _myBlockSprites[x, y] : null;
                var drop = GetDropAt(x, y);
                cell.SetVisual(col, spr, occ, drop != null ? GetDropSprite(drop.ItemType) : null, drop != null);
            }

        // 드래그 고스트
        if (_dragSlotIndex >= 0 && _currentBlocks[_dragSlotIndex] != null && _dragHasAnchor)
        {
            Color ghost = _dragCanPlace
                ? new Color(0.22f, 0.65f, 0.35f, 1f)
                : new Color(0.82f, 0.25f, 0.25f, 1f);
            foreach (var c in _currentBlocks[_dragSlotIndex].cells)
            {
                int x = _dragAnchor.x + c.x, y = _dragAnchor.y + c.y;
                if (x < 0 || x >= BattleBlockCore.BoardSize) continue;
                if (y < 0 || y >= BattleBlockCore.BoardSize) continue;
                _myBoardCells[x, y]?.SetVisual(ghost, null, false, null, false);
            }
        }
    }

    private void RefreshBlockSlots()
    {
        for (int i = 0; i < _slotRefs.Length; i++)
        {
            var refs = _slotRefs[i];
            if (refs == null) continue;
            bool has = _currentBlocks[i] != null;
            bool drag = i == _dragSlotIndex;
            if (refs.dragView != null) refs.dragView.enabled = has && !_isGameOver;
            if (refs.background != null)
                refs.background.color = drag ? new Color(1f, 0.88f, 0.42f, 1f) : Color.white;
            RebuildSlotPreview(refs.previewRoot, _currentBlocks[i]);
        }
    }

    private void RebuildSlotPreview(RectTransform root, BattleBlockInstance block)
    {
        if (root == null) return;
        DestroyRuntimeChildren(root);
        if (block?.cells == null || block.cells.Count == 0) return;

        int mx = 0, my = 0;
        foreach (var c in block.cells) { if (c.x > mx) mx = c.x; if (c.y > my) my = c.y; }

        float sx = slotPreviewCellSize + slotPreviewSpacing;
        float sy = slotPreviewCellSize + slotPreviewSpacing;
        float ox = -(mx * sx) * 0.5f;
        float oy = (my * sy) * 0.5f;

        foreach (var c in block.cells)
        {
            var go = CreatePreviewCellObject(root);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(slotPreviewCellSize, slotPreviewCellSize);
            rt.anchoredPosition = new Vector2(ox + c.x * sx, oy - c.y * sy);
            var img = GetOrAdd<Image>(go);
            img.color = block.color; img.sprite = block.cellSprite;
            img.type = Image.Type.Simple; img.preserveAspect = false; img.raycastTarget = false;
        }
    }

    private void RefreshScoreUI()
    {
        if (scoreText != null) scoreText.text = $"점수 : {_myScore:N0}";
        if (bestScoreText != null) bestScoreText.text = $"최고 : {GetBestScore():N0}";
        if (comboText != null) comboText.text = _combo > 0 ? $"콤보 {_combo}" : "";
    }

    private void RefreshArtifactUI()
    {
        foreach (var slot in _artifactSlots)
            if (slot != null) RefreshArtifactSlotUI(slot);
    }

    private void RefreshArtifactSlotUI(ArtifactSlot slot)
    {
        bool passive = slot.Def == null ||
                       slot.Def.triggerType == ArtifactTriggerType.PassiveOnly;

        if (slot.CooldownText != null)
            slot.CooldownText.text = passive ? "" :
                                     slot.CooldownRemaining > 0 ? slot.CooldownRemaining.ToString() :
                                     "준비";

        if (slot.ReadyGlow != null)
            slot.ReadyGlow.enabled = !passive && slot.IsReady;
    }

    // ????????????????????????????????????????????
    //  드래그 미리보기
    // ????????????????????????????????????????????
    private void CreateDragPreview(BattleBlockInstance block)
    {
        DestroyDragPreview();
        var go = new GameObject(RuntimePrefix + "DragPreview", typeof(RectTransform));
        _dragPreviewRoot = go.GetComponent<RectTransform>();
        _dragPreviewRoot.SetParent(_dragLayer, false);
        _dragPreviewRoot.anchorMin = _dragPreviewRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _dragPreviewRoot.pivot = new Vector2(0.5f, 0.5f);

        int w = GetBlockWidth(block), h = GetBlockHeight(block);
        float tw = w * dragPreviewCellSize + (w - 1) * dragPreviewSpacing;
        float th = h * dragPreviewCellSize + (h - 1) * dragPreviewSpacing;
        float sx = -tw * 0.5f + dragPreviewCellSize * 0.5f;
        float sy = th * 0.5f - dragPreviewCellSize * 0.5f;

        foreach (var p in block.cells)
            CreatePreviewCell(_dragPreviewRoot,
                new Vector2(dragPreviewCellSize, dragPreviewCellSize),
                new Vector2(sx + p.x * (dragPreviewCellSize + dragPreviewSpacing),
                            sy - p.y * (dragPreviewCellSize + dragPreviewSpacing)),
                new Color(block.color.r, block.color.g, block.color.b, 0.85f),
                block.cellSprite);
    }

    private void DestroyDragPreview()
    {
        if (_dragPreviewRoot != null) Destroy(_dragPreviewRoot.gameObject);
        _dragPreviewRoot = null;
    }

    // ????????????????????????????????????????????
    //  점수 저장
    // ????????????????????????????????????????????
    private static int GetBestScore() => PlayerPrefs.GetInt(HighScoreKey, 0);
    private static int SaveBestScore(int v)
    {
        int best = GetBestScore();
        if (v > best) { PlayerPrefs.SetInt(HighScoreKey, v); PlayerPrefs.Save(); best = v; }
        return best;
    }

    // ????????????????????????????????????????????
    //  헬퍼
    // ????????????????????????????????????????????
    private bool AreAllBlockSlotsEmpty()
    {
        foreach (var b in _currentBlocks) if (b != null) return false;
        return true;
    }

    private static int GetBlockWidth(BattleBlockInstance b)
    {
        if (b?.cells == null || b.cells.Count == 0) return 1;
        int m = 0; foreach (var c in b.cells) if (c.x > m) m = c.x; return m + 1;
    }
    private static int GetBlockHeight(BattleBlockInstance b)
    {
        if (b?.cells == null || b.cells.Count == 0) return 1;
        int m = 0; foreach (var c in b.cells) if (c.y > m) m = c.y; return m + 1;
    }

    private Sprite GetDropSprite(DropItemType t) => t switch
    {
        DropItemType.Gold => goldSprite,
        DropItemType.EnhanceStoneBasic => stoneBasicSprite,
        DropItemType.EnhanceStoneMid => stoneMidSprite,
        DropItemType.EnhanceStoneHigh => stoneHighSprite,
        _ => null
    };

    private void CreatePreviewCell(RectTransform parent, Vector2 size, Vector2 pos, Color col, Sprite spr)
    {
        var go = new GameObject(RuntimePrefix + "DragCell", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos; rt.localScale = Vector3.one;
        var img = go.GetComponent<Image>();
        img.color = col; img.sprite = spr; img.raycastTarget = false;
    }

    private GameObject CreateCellObject(Transform parent)
    {
        var go = boardCellPrefab != null
            ? Instantiate(boardCellPrefab, parent)
            : new GameObject("BoardCell", typeof(RectTransform), typeof(Image), typeof(BoardCell));
        go.name = "BoardCell";
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.SetParent(parent, false); rt.localScale = Vector3.one;
        return go;
    }

    private GameObject CreatePreviewCellObject(Transform parent)
    {
        var go = previewCellPrefab != null
            ? Instantiate(previewCellPrefab, parent)
            : new GameObject("PreviewCell", typeof(RectTransform), typeof(Image));
        go.name = "PreviewCell";
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.SetParent(parent, false); rt.localScale = Vector3.one;
        return go;
    }

    private Transform FindSafeArea()
    {
        if (transform.parent?.name == "SafeArea") return transform.parent;
        return GameObject.Find("SafeArea")?.transform;
    }

    private static RectTransform FindRect(Transform r, string p) { var t = r.Find(p); return t as RectTransform; }
    private static TMP_Text FindTMP(Transform r, string p) { return r.Find(p)?.GetComponent<TMP_Text>(); }
    private static Button FindButton(Transform r, string p) { return r.Find(p)?.GetComponent<Button>(); }
    private static GameObject FindGO(Transform r, string p) { return r.Find(p)?.gameObject; }
    private static T FindComp<T>(Transform r, string p) where T : Component { return r.Find(p)?.GetComponent<T>(); }
    private static T GetOrAdd<T>(GameObject go) where T : Component { return go.GetComponent<T>() ?? go.AddComponent<T>(); }
    private static void DestroyRuntimeChildren(Transform r)
    {
        if (r == null) return;
        for (int i = r.childCount - 1; i >= 0; i--) Destroy(r.GetChild(i).gameObject);
    }
}