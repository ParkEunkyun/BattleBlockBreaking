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
    private const int ScorePerCell = 10;

    // 멀티 라인 보너스 (index = 줄 수, 5 이상은 마지막 값)
    private static readonly int[] MultiLineBonusTable = { 0, 100, 500, 1500, 3500, 7000 };

    // 콤보 기본 보너스
    private const int ComboScorePerCount = 50;

    // 콤보 마일스톤
    private static readonly (int threshold, int bonus)[] ComboMilestones =
        { (10, 750), (20, 2000), (30, 5000) };

    // 드랍 아이템 기본 확률
    private const float BaseDropGold = 0.03f;
    private const float BaseDropStoneBasic = 0.03f;
    private const float BaseDropStoneMid = 0.02f;
    private const float BaseDropStoneHigh = 0.01f;
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
        public readonly List<Image> previewCells = new List<Image>();
    }

    private sealed class ArtifactSlot
    {
        public int Index;
        public NormalArtifactDefinition Def;
        public int Level = 1;
        public int CooldownRemaining;

        public bool IsReady => CooldownRemaining <= 0;

        // UI
        public RectTransform Root;
        public Image FrameImage;
        public Image IconImage;
        public TMP_Text NameText;
        public TMP_Text LevelText;
        public TMP_Text CooldownText;
        public Image ReadyGlow;

        // Charge / Stack UI
        public Slider ChargeSlider;
        public TMP_Text ChargeSliderStateText;

        // Touch
        public NormalArtifactPressHandler PressHandler;
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

    [Header("Normal Line Clear FX")]
    [SerializeField] private Color normalLineClearFxColor = new Color(1f, 0.88f, 0.35f, 1f);
    [SerializeField] private float normalLineClearFxHoldDuration = 0.12f;
    [SerializeField] private float normalLineClearFxFadeOutDuration = 0.18f;
    [SerializeField] private float normalLineClearFxThicknessScale = 1.5f;

    [Header("Combo FX")]
    [SerializeField] private TMP_FontAsset comboFxFont;
    [SerializeField] private Vector2 comboFxOffset = new Vector2(0f, 36f);
    [SerializeField] private float comboFxRiseDistance = 78f;
    [SerializeField] private float comboFxDuration = 0.52f;
    [SerializeField] private Color comboFxColorLow = new Color(0.45f, 0.90f, 1f, 1f);
    [SerializeField] private Color comboFxColorMid = new Color(0.80f, 0.45f, 1f, 1f);
    [SerializeField] private Color comboFxColorHigh = new Color(1f, 0.82f, 0.25f, 1f);
    [SerializeField] private Sprite comboScoreBackgroundSprite;
    [SerializeField] private Sprite comboScoreArtifactIcon;
    [SerializeField] private Color comboScoreBaseColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color comboScoreArtifactColor = new Color(0.56f, 1f, 0.45f, 1f);
    [SerializeField] private Color comboScoreBackgroundColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Vector2 comboScoreBackgroundPadding = new Vector2(34f, 16f);

    [Header("Pikup Loot FX")]

    [SerializeField] private int pickupFlyFxPoolSize = 10;
    [SerializeField] private float pickupFlyDuration = 0.42f;
    [SerializeField] private float pickupFlyArcHeight = 90f;
    [SerializeField] private float pickupFlyStartScale = 1.05f;
    [SerializeField] private float pickupFlyEndScale = 0.42f;

    private readonly Queue<NormalPickupFlyFx> _pickupFlyFxPool = new Queue<NormalPickupFlyFx>();

    [Header("FX Pool")]
    [SerializeField] private int normalLineClearFxPoolSize = 8;
    [SerializeField] private int normalComboFxPoolSize = 4;

    private readonly Queue<NormalLineClearFx> _normalLineClearFxPool = new Queue<NormalLineClearFx>();
    private readonly Queue<NormalComboFx> _normalComboFxPool = new Queue<NormalComboFx>();

    [Header("Score Popup FX")]
    [SerializeField] private TMP_FontAsset scorePopupFont;
    [SerializeField] private Sprite artifactScorePopupIcon;
    [SerializeField] private int scorePopupPoolSize = 12;
    [SerializeField] private float scorePopupRiseDistance = 64f;
    [SerializeField] private float scorePopupDuration = 0.62f;
    [SerializeField] private Color scorePopupBaseColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color scorePopupArtifactColor = new Color(0.56f, 1f, 0.45f, 1f);
    [SerializeField] private Color comboScorePopupBaseColor = new Color(1f, 0.84f, 0.36f, 1f);
    [SerializeField] private Vector2 placementScorePopupOffset = new Vector2(0f, 28f);
    [SerializeField] private float lineScoreOutsideOffset = 44f;
    [SerializeField] private Vector2 comboScorePopupOffset = new Vector2(115f, 0f);
    [SerializeField] private Sprite comboScorePopupBackgroundSprite;
    [SerializeField] private Color comboScorePopupBackgroundColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Vector2 comboScorePopupBackgroundPadding = new Vector2(42f, 20f);

    private readonly Queue<NormalScorePopupFx> _scorePopupPool = new Queue<NormalScorePopupFx>();

    [Header("Artifact Slot Frames")]
    [SerializeField] private Sprite normalArtifactSlotSprite;
    [SerializeField] private Sprite rareArtifactSlotSprite;
    [SerializeField] private Sprite epicArtifactSlotSprite;
    [SerializeField] private Sprite uniqueArtifactSlotSprite;
    [SerializeField] private Sprite legendArtifactSlotSprite;
    [SerializeField] private Color artifactDisabledColor = new Color(1f, 1f, 1f, 0.55f);

    [Header("Artifact Charge Slider UI")]
    [SerializeField] private Color artifactChargeReadyColor = new Color(0.45f, 1f, 0.45f, 1f);
    [SerializeField] private Color artifactChargeChargingColor = new Color(1f, 0.82f, 0.28f, 1f);
    [SerializeField] private Color artifactChargeEmptyColor = new Color(0.35f, 0.35f, 0.35f, 0.85f);

    [Header("Artifact Tooltip")]
    [SerializeField] private NormalArtifactTooltipView artifactLongPressTooltip;
    [SerializeField] private float artifactLongPressSeconds = 0.45f;

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
    [SerializeField] private Button pauseButton;
    [SerializeField] private TMP_Text pauseLootCountText;
    [SerializeField] private RectTransform pausePickupTarget;
    [SerializeField] private TMP_Text resultTitleText;

    [Header("Result Drop UI")]
    [SerializeField] private NormalResultDropEntryView resultDropEntryPrefab;
    [SerializeField] private int resultDropEntryPoolSize = 8;

    private readonly Queue<NormalResultDropEntryView> _resultDropEntryPool = new Queue<NormalResultDropEntryView>();
    private readonly List<NormalResultDropEntryView> _activeResultDropEntries = new List<NormalResultDropEntryView>();

    private bool _isPausePopupOpen;
    private bool _runRewardsClaimed;

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

    private readonly List<Image> _dragPreviewCells = new List<Image>();
    private bool _dragPreviewInitialized;

    [SerializeField] private int slotPreviewPoolInitialSize = 6;
    [SerializeField] private int dragPreviewPoolInitialSize = 6;

    // ????????????????????????????????????????????
    //  런타임 - 점수 / 콤보
    // ????????????????????????????????????????????
    private int _myScore;
    private int _combo;
    private int _setIndex;
    private int _clearCountThisSet;
    private int _clearedLinesThisSet;
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
    private NormalArtifactRuntimeManager _artifactRuntime;
    private int _dimensionWarpPlacementRemaining;

    // ????????????????????????????????????????????
    //  초기화
    // ????????????????????????????????????????????
    private struct ClearLineInfo
    {
        public NormalLineClearFx.Axis axis;
        public int index;
    }

    private void Awake()
    {
        CacheCanvas();
        CreateDragLayer();
        CacheHierarchy();
        BattleBlockCore.BuildShapeLibrary(_normalShapes, _curseShapes, blockSprites);
        BuildMyBoard();
        WarmupFxPools();
        WarmupPreviewPools();
        WarmupPickupFlyPool();
        WarmupResultDropEntryPool();
        WarmupScorePopupPool();
        BindButtons();
        InitArtifactSlotUI();
        LoadArtifactsFromSession();
        InitArtifactRuntime();
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
        resultScoreText = resultScoreText ?? FindTMP(sa, "ResultPhaseRoot/ResultScoreText/Score");
        resultBestScoreText = resultBestScoreText ?? FindTMP(sa, "ResultPhaseRoot/ResultBestScoreText/Score");
        resultStateBelow = resultStateBelow ?? FindGO(sa, "ResultPhaseRoot/ResultStateBelow");
        resultStateEqual = resultStateEqual ?? FindGO(sa, "ResultPhaseRoot/ResultStateEqual");
        resultStateNew = resultStateNew ?? FindGO(sa, "ResultPhaseRoot/ResultStateNew");
        resultDropRoot = resultDropRoot ?? FindRect(sa, "ResultPhaseRoot/DropItemRoot");
        retryButton = retryButton ?? FindButton(sa, "ResultPhaseRoot/Buttons/RetryButton");
        resultLobbyButton = resultLobbyButton ?? FindButton(sa, "ResultPhaseRoot/Buttons/ResultLobbyButton");

        pauseButton = pauseButton ?? FindButton(sa, "TopHudRoot/PauseButton");
        pauseLootCountText = pauseLootCountText ?? FindTMP(sa, "TopHudRoot/PauseButton/CountText");
        pausePickupTarget = pausePickupTarget ?? FindRect(sa, "TopHudRoot/PauseButton/PickupTarget");
        resultTitleText = resultTitleText ?? FindTMP(sa, "ResultPhaseRoot/ResultTitleText");

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
        pauseButton?.onClick.RemoveAllListeners();
        pauseButton?.onClick.AddListener(() =>
        {
            if (_isGameOver)
                return;

            OpenPausePopup();
        });

        retryButton?.onClick.RemoveAllListeners();
        retryButton?.onClick.AddListener(() =>
        {
            if (_isGameOver)
                return;

            ClosePausePopup();
        });

        resultLobbyButton?.onClick.RemoveAllListeners();
        resultLobbyButton?.onClick.AddListener(() =>
        {
            ClaimRunRewardsOnce();
            SceneManager.LoadScene(lobbySceneName);
        });
    }

    private void InitArtifactSlotUI()
    {
        Transform sa = FindSafeArea();
        if (sa == null)
            return;

        if (artifactLongPressTooltip == null)
            artifactLongPressTooltip = FindComp<NormalArtifactTooltipView>(sa, "ArtifactLongPressTooltip");

        for (int i = 0; i < 4; i++)
        {
            Transform slotTr = sa.Find($"ArtifactRoot/ArtifactSlot{i + 1}");
            if (slotTr == null)
                continue;

            int cap = i;

            NormalArtifactPressHandler pressHandler = GetOrAdd<NormalArtifactPressHandler>(slotTr.gameObject);
            pressHandler.Bind(
                () => OnClickArtifactSlot(cap),
                () => ShowArtifactTooltip(cap),
                HideArtifactTooltip,
                artifactLongPressSeconds);

            Button button = slotTr.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();

                // 클릭/롱프레스는 PressHandler가 전담.
                // GameObject는 켜둔 상태라 Raycast는 그대로 동작함.
                button.enabled = false;
            }

            Image frameImage = slotTr.GetComponent<Image>();
            if (frameImage != null)
                frameImage.raycastTarget = true;

            ArtifactSlot slot = new ArtifactSlot
            {
                Index = i,
                Root = slotTr as RectTransform,
                FrameImage = frameImage,
                IconImage = FindComp<Image>(slotTr, "Icon"),
                NameText = FindTMP(slotTr, "NameText"),
                LevelText = FindTMP(slotTr, "LevelText"),
                CooldownText = FindTMP(slotTr, "CooldownText"),
                ReadyGlow = FindComp<Image>(slotTr, "ReadyGlow"),
                ChargeSlider = FindComp<Slider>(slotTr, "ChargeSlider"),
                ChargeSliderStateText = FindTMP(slotTr, "ChargeSlider/StateText"),
                PressHandler = pressHandler
            };

            _artifactSlots[i] = slot;
        }

        RefreshArtifactUI();
    }

    // ── 외부에서 아티팩트 주입 (LobbyController가 호출) ──
    public void SetArtifact(int slotIndex, NormalArtifactDefinition def)
    {
        if (slotIndex < 0 || slotIndex >= _artifactSlots.Length)
            return;

        ArtifactSlot slot = _artifactSlots[slotIndex];
        if (slot == null)
            return;

        slot.Def = def;
        slot.Level = NormalArtifactLevelUtility.GetLevel(def);
        slot.CooldownRemaining = 0;

        RefreshArtifactSlotUI(slot);
    }

    /// <summary>NormalArtifactSession에서 선택된 아티팩트를 슬롯에 자동 주입</summary>
    private void LoadArtifactsFromSession()
    {
        for (int i = 0; i < _artifactSlots.Length; i++)
            SetArtifact(i, null);

        var selected = NormalArtifactSession.Selected;
        for (int i = 0; i < selected.Count && i < _artifactSlots.Length; i++)
            SetArtifact(i, selected[i]);

        if (_artifactRuntime != null)
            _artifactRuntime.BuildFromSession();
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
        _dimensionWarpPlacementRemaining = 0;

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

        _isPausePopupOpen = false;
        _runRewardsClaimed = false;

        if (resultPhaseRoot != null)
            resultPhaseRoot.SetActive(false);

        ClearActiveResultDropEntries();

        RefreshPauseLootCount();
        ResetCurrentBlocks();

        _clearedLinesThisSet = 0;

        if (_artifactRuntime != null)
        {
            _artifactRuntime.BuildFromSession();
            _artifactRuntime.BeginRound();
        }

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
        if (_dragPreviewRoot == null)
            return;

        Vector2 adj = ev.position + dragOffset;
        Camera cam = ev.pressEventCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _dragLayer, adj, cam, out Vector2 local))
        {
            _dragPreviewRoot.anchoredPosition = local;
        }

        var block = _currentBlocks[_dragSlotIndex];
        _dragHasAnchor = TryGetBoardAnchor(adj, cam, block, out _dragAnchor);

        if (!_dragHasAnchor || block == null)
        {
            _dragCanPlace = false;
            return;
        }

        if (IsDimensionWarpPlacementActive())
        {
            _dragCanPlace = CanPlaceBlockDimensionWarp(block, _dragAnchor.x, _dragAnchor.y);
        }
        else
        {
            _dragCanPlace = BattleBlockCore.CanPlaceBlock(block, _myOccupied, _dragAnchor.x, _dragAnchor.y);
        }
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
        if (block == null)
            return;

        bool useDimensionWarpPlacement = IsDimensionWarpPlacementActive();

        if (useDimensionWarpPlacement)
        {
            if (!CanPlaceBlockDimensionWarp(block, anchor.x, anchor.y))
                return;
        }
        else
        {
            if (!BattleBlockCore.CanPlaceBlock(block, _myOccupied, anchor.x, anchor.y))
                return;
        }
        // 연쇄 충전기:
        // READY 상태에서 들어온 이번 배치가 "6번째 판정 배치"다.
        // 이 배치는 다시 충전 카운트에 포함하지 않고, 성공/실패 후 스택을 초기화한다.
        bool placementChargeWasReady =
            _artifactRuntime != null && _artifactRuntime.HasPlacementChargeReady();

        // 현재 배치가 이번 세트의 마지막 블록인지 계산
        bool lastPlacementOfSet = true;
        for (int i = 0; i < _currentBlocks.Length; i++)
        {
            if (i == slotIndex)
                continue;

            if (_currentBlocks[i] != null)
            {
                lastPlacementOfSet = false;
                break;
            }
        }

        int placedCellCount = block.CellCount;

        // 1. 배치
        int placeScoreBefore = _myScore;

        CollectDropItemsUnder(block, anchor);

        if (useDimensionWarpPlacement)
        {
            PlaceBlockDimensionWarp(block, anchor);
            ConsumeDimensionWarpPlacement();
        }
        else
        {
            BattleBlockCore.PlaceBlock(block, _myOccupied, _myColors, _myBlockSprites, anchor.x, anchor.y);
        }

        // 빈칸 수 계산 (배치 직후, 클리어 전)
        int emptyCellCount = 0;
        for (int y = 0; y < BattleBlockCore.BoardSize; y++)
        {
            for (int x = 0; x < BattleBlockCore.BoardSize; x++)
            {
                if (!_myOccupied[x, y])
                    emptyCellCount++;
            }
        }

        NotifyBoardStateAfterPlacement(emptyCellCount, CountIsolatedHoles());

        // 2. 배치 점수
        int placedScoreRaw = placedCellCount * ScorePerCell;

        float placementMult = _artifactRuntime != null
            ? _artifactRuntime.EvaluatePlacementScoreMultiplier(placedCellCount)
            : 1f;

        AddScore(Mathf.RoundToInt(placedScoreRaw * placementMult));

        int placedBaseScore = EvaluateActualScoreGain(placedScoreRaw);
        int placedTotalGain = _myScore - placeScoreBefore;
        int placedArtifactGain = Mathf.Max(0, placedTotalGain - placedBaseScore);

        SpawnPlacementScorePopup(block, anchor, placedBaseScore, placedArtifactGain);

        // 3. 라인 클리어
        List<ClearLineInfo> clearInfos = CollectCompletedLines();

        int horizontalLines = 0;
        int verticalLines = 0;
        for (int i = 0; i < clearInfos.Count; i++)
        {
            if (clearInfos[i].axis == NormalLineClearFx.Axis.Row)
                horizontalLines++;
            else
                verticalLines++;
        }

        int cleared = BattleBlockCore.ClearCompletedLines(_myOccupied, _myColors, _myBlockSprites);
        if (cleared > 0)
        {
            SpawnNormalLineClearFx(clearInfos);

            _clearCountThisSet++;
            _clearedLinesThisSet += cleared;
            _totalClearCount += cleared;
            RecordMultiLine(cleared);

            // 콤보는 "줄 제거 이벤트 1회당 +1"
            int prevCombo = _combo;
            _combo += 1;
            _maxCombo = Mathf.Max(_maxCombo, _combo);

            // 라인 기본/아티팩트 점수 계산
            int lineProcessBefore = _myScore;
            ProcessClearScore(
                cleared,
                lastPlacementOfSet,
                emptyCellCount,
                placedCellCount,
                horizontalLines,
                verticalLines);
            int lineProcessGain = _myScore - lineProcessBefore;

            // 콤보 기본/아티팩트 점수 계산
            int comboBaseScore, comboArtifactGain;
            ProcessComboReward(prevCombo, _combo, out comboBaseScore, out comboArtifactGain);

            // 라인클리어 이벤트형 아티팩트 추가 점수
            int lineHookBefore = _myScore;
            NotifyLineClear(cleared);
            int lineHookGain = _myScore - lineHookBefore;

            int clearBaseScore = EvaluateActualScoreGain(GetBaseClearScoreRaw(cleared));
            int clearArtifactGain = Mathf.Max(0, (lineProcessGain + lineHookGain) - clearBaseScore);

            SpawnLineScorePopup(clearInfos, clearBaseScore, clearArtifactGain);

            if (_combo >= 2)
                SpawnNormalComboFx(_combo, comboBaseScore, comboArtifactGain);
        }

        // 연쇄 충전기 처리
        // READY였던 이번 배치가 클리어 실패라면 여기서 즉시 초기화한다.
        // 다만 영점 유지장치/액티브 BlockPlaced 쿨다운 등 다른 배치 기반 효과는 항상 진행되어야 하므로,
        // 연쇄 충전기 카운트만 제외할 수 있게 countPlacementCharge 플래그를 넘긴다.
        if (placementChargeWasReady && cleared <= 0 && _artifactRuntime != null)
            _artifactRuntime.ConsumePlacementChargeClearBonus(false);

        NotifyBlockPlaced(!placementChargeWasReady);

        _currentBlocks[slotIndex] = null;

        // 4. 세트 종료
        if (AreAllBlockSlotsEmpty())
            ProcessSetEnd();
        else
            RefreshAllUI();

        // 5. 게임오버 체크
        CheckGameOver();
    }

    private void ProcessClearScore(
        int lines,
        bool lastPlacementOfSet,
        int emptyCellCount,
        int placedCellCount,
        int horizontalLines,
        int verticalLines)
    {
        int idx = Mathf.Clamp(lines, 0, MultiLineBonusTable.Length - 1);
        int bonus = MultiLineBonusTable[idx];

        float clearMult = _artifactRuntime != null
            ? _artifactRuntime.EvaluateLineClearScoreMultiplier(
                lastPlacementOfSet,
                emptyCellCount,
                placedCellCount,
                horizontalLines,
                verticalLines)
            : 1f;

        int gained = Mathf.RoundToInt(bonus * clearMult);

        if (_artifactRuntime != null)
        {
            gained += _artifactRuntime.GetLineClearFlatBonus();
            gained += _artifactRuntime.ConsumePostClearFlatBonusByRemainingCells(GetOccupiedCellCount());
            gained += _artifactRuntime.ConsumePlacementChargeClearBonus(true);
        }

        AddScore(gained);
    }

    private void ProcessSetEnd()
    {
        bool hadClear = _clearCountThisSet > 0;

        if (!hadClear)
        {
            bool exempt = _artifactRuntime != null && _artifactRuntime.TryConsumeComboPreserve(_combo);

            if (!exempt)
            {
                int comboBreakBonus = _artifactRuntime != null
                    ? _artifactRuntime.GetComboBreakFlatBonus(_combo)
                    : 0;

                if (comboBreakBonus > 0)
                    AddScore(comboBreakBonus);

                _combo = 0;
            }

            if (_artifactRuntime != null)
                _artifactRuntime.ConsumePlacementChargeClearBonus(false);
        }

        if (_artifactRuntime != null)
            _artifactRuntime.EndRound(hadClear, _clearedLinesThisSet);

        TrySpawnDropItems();
        ExpireDropItems();

        _setIndex++;
        _clearCountThisSet = 0;
        _clearedLinesThisSet = 0;
        ResetCurrentBlocks();

        if (_artifactRuntime != null)
            _artifactRuntime.BeginRound();

        RefreshAllUI();
    }

    // ????????????????????????????????????????????
    //  아티팩트 발동
    // ????????????????????????????????????????????
    private void TryActivateArtifact(int slotIndex)
    {
        if (_artifactRuntime == null)
            return;

        var result = _artifactRuntime.TryUseActive(slotIndex);
        if (result == NormalArtifactRuntimeManager.ActiveUseResult.Success)
        {
            ShowArtifactSlotStateText(slotIndex, "USE");
            _artifactActivationCount++;
            RefreshAllUI();
        }
        else
        {
            ShowArtifactSlotStateText(slotIndex, "WAIT");
            RefreshArtifactUI();
        }
    }

    private void OnClickArtifactSlot(int index)
    {
        HideArtifactTooltip();

        if (!CanAcceptUserInput())
            return;

        if (index < 0 || index >= _artifactSlots.Length)
            return;

        ArtifactSlot slot = _artifactSlots[index];

        if (slot?.Def == null)
            return;

        if (!slot.Def.IsActiveArtifact)
            return;

        if (slot.Def.runtimeTriggerType != NormalArtifactTriggerType.ManualButton)
            return;

        TryActivateArtifact(index);
        RefreshAllUI();
    }

    private void ShowArtifactTooltip(int index)
    {
        if (index < 0 || index >= _artifactSlots.Length)
            return;

        ArtifactSlot slot = _artifactSlots[index];
        if (slot == null || slot.Def == null)
            return;

        int level = slot.Level;

        if (_artifactRuntime != null)
        {
            NormalArtifactRuntimeManager.RuntimeArtifactState state = _artifactRuntime.GetEquippedState(index);
            if (state != null && state.definition == slot.Def)
                level = state.level;
        }

        if (artifactLongPressTooltip != null)
            artifactLongPressTooltip.Show(slot.Def, level);
    }

    private void HideArtifactTooltip()
    {
        if (artifactLongPressTooltip != null)
            artifactLongPressTooltip.Hide();
    }

    // ????????????????????????????????????????????
    //  드랍 아이템
    // ????????????????????????????????????????????
    private void TrySpawnDropItems()
    {
        if (GetBoardOccupancy() >= DropOccupancyLimit)
            return;

        float goldMult = _artifactRuntime != null
            ? _artifactRuntime.GetGoldDropRateMultiplier()
            : 1f;

        float itemMult = _artifactRuntime != null
            ? _artifactRuntime.GetItemDropRateMultiplier()
            : 1f;

        TrySpawnDrop(DropItemType.Gold, BaseDropGold * goldMult);
        TrySpawnDrop(DropItemType.EnhanceStoneBasic, BaseDropStoneBasic * itemMult);
        TrySpawnDrop(DropItemType.EnhanceStoneMid, BaseDropStoneMid * itemMult);
        TrySpawnDrop(DropItemType.EnhanceStoneHigh, BaseDropStoneHigh * itemMult);
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
                    Sprite pickupSprite = GetPickupFlySprite(drop.ItemType);
                    SpawnPickupFlyFx(pickupSprite, drop.BoardX, drop.BoardY);

                    switch (drop.ItemType)
                    {
                        case DropItemType.Gold: _earnedGold++; break;
                        case DropItemType.EnhanceStoneBasic: _earnedStoneBasic++; break;
                        case DropItemType.EnhanceStoneMid: _earnedStoneMid++; break;
                        case DropItemType.EnhanceStoneHigh: _earnedStoneHigh++; break;
                    }

                    RefreshPauseLootCount();
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
        return _artifactRuntime != null
            ? _artifactRuntime.GetDropRateMultiplier()
            : 1f;
    }

    private int GetDropKeepBonus()
    {
        return _artifactRuntime != null
            ? _artifactRuntime.GetDropKeepBonus()
            : 0;
    }

    // ????????????????????????????????????????????
    //  게임오버
    // ????????????????????????????????????????????
    private void CheckGameOver()
    {
        if (BattleBlockCore.HasAnyPlaceableMove(_currentBlocks, _myOccupied))
            return;

        // 회전 / 미러 / 리롤 / 중력붕괴처럼
        // 게임오버 직전 판을 살릴 수 있는 액티브가 있으면 바로 게임오버 시키지 않는다.
        if (_artifactRuntime != null && _artifactRuntime.HasUsableGameOverRecoveryActive())
        {
            RefreshArtifactUI();
            return;
        }

        if (_artifactRuntime != null && _artifactRuntime.TryConsumeSecondChanceSingleBlocks())
        {
            GiveSingleCellEmergencyBlocks();
            _artifactActivationCount++;
            RefreshAllUI();
            return;
        }

        if (_artifactRuntime != null && _artifactRuntime.TryConsumeSecondChance())
        {
            ResetCurrentBlocks();
            _artifactActivationCount++;
            RefreshAllUI();
            return;
        }

        ShowGameOver();
    }

    private void ShowGameOver()
    {
        ShowResultPopup(true);
    }

    private void BuildResultDropUI()
    {
        ClearActiveResultDropEntries();

        AddResultDropEntry(goldSprite, "골드", _earnedGold);
        AddResultDropEntry(stoneBasicSprite, "강화석(하)", _earnedStoneBasic);
        AddResultDropEntry(stoneMidSprite, "강화석(중)", _earnedStoneMid);
        AddResultDropEntry(stoneHighSprite, "강화석(상)", _earnedStoneHigh);
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
    private void InitArtifactRuntime()
    {
        _artifactRuntime = GetComponent<NormalArtifactRuntimeManager>();
        if (_artifactRuntime == null)
            _artifactRuntime = gameObject.AddComponent<NormalArtifactRuntimeManager>();

        _artifactRuntime.OnActiveUsed -= HandleArtifactActiveUsed;
        _artifactRuntime.OnActiveUsed += HandleArtifactActiveUsed;

        _artifactRuntime.OnRuntimeChanged -= HandleArtifactRuntimeChanged;
        _artifactRuntime.OnRuntimeChanged += HandleArtifactRuntimeChanged;

        _artifactRuntime.BuildFromSession();
    }

    private void OnDestroy()
    {
        if (_artifactRuntime != null)
        {
            _artifactRuntime.OnActiveUsed -= HandleArtifactActiveUsed;
            _artifactRuntime.OnRuntimeChanged -= HandleArtifactRuntimeChanged;
        }
    }

    private void HandleArtifactRuntimeChanged()
    {
        RefreshArtifactUI();
    }

    private void NotifyBoardStateAfterPlacement(int emptyCellCount, int isolatedHoleCount)
    {
        if (_artifactRuntime != null)
            _artifactRuntime.NotifyBoardStateAfterPlacement(emptyCellCount, isolatedHoleCount);
    }

    private float GetPlacementScoreMultiplier(int cellCount)
    {
        return _artifactRuntime != null
            ? _artifactRuntime.EvaluatePlacementScoreMultiplier(cellCount)
            : 1f;
    }

    private void CountClearAxes(List<ClearLineInfo> infos, out int horizontalLines, out int verticalLines)
    {
        horizontalLines = 0;
        verticalLines = 0;

        if (infos == null)
            return;

        for (int i = 0; i < infos.Count; i++)
        {
            if (infos[i].axis == NormalLineClearFx.Axis.Row)
                horizontalLines++;
            else
                verticalLines++;
        }
    }

    private int CountRemainingBlockSlots()
    {
        int count = 0;
        for (int i = 0; i < _currentBlocks.Length; i++)
            if (_currentBlocks[i] != null)
                count++;

        return count;
    }

    private int CountEmptyCells()
    {
        int count = 0;
        for (int y = 0; y < BattleBlockCore.BoardSize; y++)
            for (int x = 0; x < BattleBlockCore.BoardSize; x++)
                if (!_myOccupied[x, y])
                    count++;

        return count;
    }

    private int CountIsolatedHoles()
    {
        int count = 0;

        for (int y = 0; y < BattleBlockCore.BoardSize; y++)
        {
            for (int x = 0; x < BattleBlockCore.BoardSize; x++)
            {
                if (_myOccupied[x, y])
                    continue;

                bool leftClosed = x == 0 || _myOccupied[x - 1, y];
                bool rightClosed = x == BattleBlockCore.BoardSize - 1 || _myOccupied[x + 1, y];
                bool upClosed = y == 0 || _myOccupied[x, y - 1];
                bool downClosed = y == BattleBlockCore.BoardSize - 1 || _myOccupied[x, y + 1];

                if (leftClosed && rightClosed && upClosed && downClosed)
                    count++;
            }
        }

        return count;
    }

    private void HandleArtifactActiveUsed(int equippedIndex, NormalArtifactRuntimeManager.RuntimeArtifactState state)
    {
        if (state == null || state.definition == null)
            return;

        switch (state.definition.equipEffectType)
        {
            case NormalArtifactEquipEffectType.ActiveChargeRotate90:
            case NormalArtifactEquipEffectType.RotateRemaining90:
                RotateRemainingBlocks(1);
                break;

            case NormalArtifactEquipEffectType.RotateRemainingMinus90:
                RotateRemainingBlocks(3);
                break;

            case NormalArtifactEquipEffectType.RotateRemaining180:
                RotateRemainingBlocks(2);
                break;

            case NormalArtifactEquipEffectType.MirrorRemainingHorizontal:
                MirrorRemainingBlocks(true);
                break;

            case NormalArtifactEquipEffectType.MirrorRemainingVertical:
                MirrorRemainingBlocks(false);
                break;

            case NormalArtifactEquipEffectType.RerollRemainingAll:
                RerollRemainingBlocks(false);
                break;

            case NormalArtifactEquipEffectType.RebuildRemainingSafer:
                RebuildRemainingBlocksSafer(state.definition.GetEquipValue(state.level));
                break;

            case NormalArtifactEquipEffectType.GravityCollapseBoard:
                ApplyGravityCollapse();
                break;

            case NormalArtifactEquipEffectType.DimensionWarpPlacement:
                ArmDimensionWarpPlacement(state);
                break;
      
            case NormalArtifactEquipEffectType.TimedTotalScoreBuff:
                _artifactRuntime?.ActivateTimedTotalScoreBuff(state);
                break;
        }

        _artifactActivationCount++;
        RefreshAllUI();
        CheckGameOver();
    }

    private void RotateRemainingBlocks(int quarterTurns)
    {
        int turns = ((quarterTurns % 4) + 4) % 4;
        if (turns == 0)
            return;

        for (int i = 0; i < _currentBlocks.Length; i++)
        {
            var block = _currentBlocks[i];
            if (block == null || block.cells == null || block.cells.Count == 0)
                continue;

            List<Vector2Int> transformed = new List<Vector2Int>(block.cells.Count);

            for (int c = 0; c < block.cells.Count; c++)
            {
                Vector2Int p = block.cells[c];

                for (int t = 0; t < turns; t++)
                    p = new Vector2Int(-p.y, p.x);

                transformed.Add(p);
            }

            NormalizeCellsToOrigin(transformed);
            block.cells.Clear();
            block.cells.AddRange(transformed);
        }
    }

    private void MirrorRemainingBlocks(bool horizontal)
    {
        for (int i = 0; i < _currentBlocks.Length; i++)
        {
            var block = _currentBlocks[i];
            if (block == null || block.cells == null || block.cells.Count == 0)
                continue;

            List<Vector2Int> transformed = new List<Vector2Int>(block.cells.Count);

            for (int c = 0; c < block.cells.Count; c++)
            {
                Vector2Int p = block.cells[c];
                p = horizontal
                    ? new Vector2Int(-p.x, p.y)
                    : new Vector2Int(p.x, -p.y);

                transformed.Add(p);
            }

            NormalizeCellsToOrigin(transformed);
            block.cells.Clear();
            block.cells.AddRange(transformed);
        }
    }

    private static void NormalizeCellsToOrigin(List<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0)
            return;

        int minX = int.MaxValue;
        int minY = int.MaxValue;

        for (int i = 0; i < cells.Count; i++)
        {
            minX = Mathf.Min(minX, cells[i].x);
            minY = Mathf.Min(minY, cells[i].y);
        }

        for (int i = 0; i < cells.Count; i++)
            cells[i] = new Vector2Int(cells[i].x - minX, cells[i].y - minY);
    }

    private void RerollRemainingBlocks(bool favorable)
    {
        int attempts = favorable ? 10 : 1;

        for (int i = 0; i < _currentBlocks.Length; i++)
        {
            if (_currentBlocks[i] == null)
                continue;

            _currentBlocks[i] = CreateBestCandidateBlock(attempts);
        }
    }

    private void RebuildRemainingBlocksSafer(float qualityPercent)
    {
        int attempts = Mathf.Max(6, 6 + Mathf.RoundToInt(qualityPercent * 0.15f));

        for (int i = 0; i < _currentBlocks.Length; i++)
        {
            if (_currentBlocks[i] == null)
                continue;

            _currentBlocks[i] = CreateBestCandidateBlock(attempts);
        }
    }

    private BattleBlockInstance CreateBestCandidateBlock(int attempts)
    {
        BattleBlockInstance best = null;
        int bestScore = int.MinValue;

        int tryCount = Mathf.Max(1, attempts);
        for (int i = 0; i < tryCount; i++)
        {
            BattleBlockInstance candidate = BattleBlockCore.CreateRandomNormalBlock(_normalShapes);
            int score = EvaluateCandidateBlockScore(candidate);

            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best ?? BattleBlockCore.CreateRandomNormalBlock(_normalShapes);
    }

    private int EvaluateCandidateBlockScore(BattleBlockInstance candidate)
    {
        if (candidate == null)
            return int.MinValue;

        int placeable = CountPlaceableAnchors(candidate);
        return (placeable * 100) - (candidate.CellCount * 4);
    }

    private int CountPlaceableAnchors(BattleBlockInstance block)
    {
        if (block == null)
            return 0;

        int count = 0;
        for (int y = 0; y < BattleBlockCore.BoardSize; y++)
            for (int x = 0; x < BattleBlockCore.BoardSize; x++)
                if (BattleBlockCore.CanPlaceBlock(block, _myOccupied, x, y))
                    count++;

        return count;
    }

    private void ApplyGravityCollapse()
    {
        CollapseBoardDown();

        List<ClearLineInfo> clearInfos = CollectCompletedLines();
        CountClearAxes(clearInfos, out int horizontalLines, out int verticalLines);

        int cleared = BattleBlockCore.ClearCompletedLines(_myOccupied, _myColors, _myBlockSprites);
        if (cleared <= 0)
            return;

        SpawnNormalLineClearFx(clearInfos);

        _clearCountThisSet++;
        _clearedLinesThisSet += cleared;
        _totalClearCount += cleared;
        RecordMultiLine(cleared);

        int prevCombo = _combo;
        _combo += 1;
        _maxCombo = Mathf.Max(_maxCombo, _combo);

        ProcessClearScore(
            cleared,
            false,
            CountEmptyCells(),
            0,
            horizontalLines,
            verticalLines);

        int comboBaseScore, comboArtifactGain;
        ProcessComboReward(prevCombo, _combo, out comboBaseScore, out comboArtifactGain);
        NotifyLineClear(cleared);

        if (_combo >= 2)
            SpawnNormalComboFx(_combo, comboBaseScore, comboArtifactGain);
    }

    private void CollapseBoardDown()
    {
        for (int x = 0; x < BattleBlockCore.BoardSize; x++)
        {
            int writeY = BattleBlockCore.BoardSize - 1;

            for (int y = BattleBlockCore.BoardSize - 1; y >= 0; y--)
            {
                if (!_myOccupied[x, y])
                    continue;

                if (writeY != y)
                {
                    _myOccupied[x, writeY] = true;
                    _myColors[x, writeY] = _myColors[x, y];
                    _myBlockSprites[x, writeY] = _myBlockSprites[x, y];

                    _myOccupied[x, y] = false;
                    _myColors[x, y] = Color.clear;
                    _myBlockSprites[x, y] = null;
                }

                writeY--;
            }

            for (int y = writeY; y >= 0; y--)
            {
                _myOccupied[x, y] = false;
                _myColors[x, y] = Color.clear;
                _myBlockSprites[x, y] = null;
            }
        }
    }

    // ????????????????????????????????????????????
    //  아티팩트 훅 통지
    // ????????????????????????????????????????????
    private void NotifyBlockPlaced(bool countPlacementCharge = true)
    {
        if (_artifactRuntime != null)
            _artifactRuntime.NotifyBlocksPlaced(1, countPlacementCharge);
    }

    private void NotifyLineClear(int lineCount)
    {
        if (_artifactRuntime != null)
            _artifactRuntime.NotifyLinesCleared(lineCount);
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
            Color ghost;

            if (_dragCanPlace && IsDimensionWarpPlacementActive())
            {
                ghost = new Color(0.72f, 0.34f, 1f, 1f);
            }
            else
            {
                ghost = _dragCanPlace
                    ? new Color(0.22f, 0.65f, 0.35f, 1f)
                    : new Color(0.82f, 0.25f, 0.25f, 1f);
            }
            foreach (var c in _currentBlocks[_dragSlotIndex].cells)
            {
                int x = _dragAnchor.x + c.x, y = _dragAnchor.y + c.y;
                if (x < 0 || x >= BattleBlockCore.BoardSize) continue;
                if (y < 0 || y >= BattleBlockCore.BoardSize) continue;

                var dragDrop = GetDropAt(x, y);
                _myBoardCells[x, y]?.SetVisual(
                    ghost,
                    null,
                    false,
                    dragDrop != null ? GetDropSprite(dragDrop.ItemType) : null,
                    dragDrop != null);
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

            if (refs.dragView != null)
                refs.dragView.enabled = has && !_isGameOver;

            if (refs.background != null)
                refs.background.color = drag ? new Color(1f, 0.88f, 0.42f, 1f) : Color.white;

            UpdateSlotPreview(refs, _currentBlocks[i]);
        }
    }
    private void WarmupPreviewPools()
    {
        for (int i = 0; i < _slotRefs.Length; i++)
        {
            EnsureSlotPreviewPool(_slotRefs[i], slotPreviewPoolInitialSize);
            HideUnusedSlotPreviewCells(_slotRefs[i], 0);
        }

        EnsureDragPreviewRoot();
        EnsureDragPreviewPool(dragPreviewPoolInitialSize);
        HideUnusedDragPreviewCells(0);
    }

    private void EnsureSlotPreviewPool(SlotRefs refs, int requiredCount)
    {
        if (refs == null || refs.previewRoot == null)
            return;

        while (refs.previewCells.Count < requiredCount)
        {
            var go = CreatePreviewCellObject(refs.previewRoot);
            var img = GetOrAdd<Image>(go);
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.raycastTarget = false;
            go.SetActive(false);
            refs.previewCells.Add(img);
        }
    }

    private void HideUnusedSlotPreviewCells(SlotRefs refs, int usedCount)
    {
        if (refs == null)
            return;

        for (int i = usedCount; i < refs.previewCells.Count; i++)
        {
            if (refs.previewCells[i] != null)
                refs.previewCells[i].gameObject.SetActive(false);
        }
    }

    private void UpdateSlotPreview(SlotRefs refs, BattleBlockInstance block)
    {
        if (refs == null || refs.previewRoot == null)
            return;

        if (block?.cells == null || block.cells.Count == 0)
        {
            HideUnusedSlotPreviewCells(refs, 0);
            return;
        }

        EnsureSlotPreviewPool(refs, block.cells.Count);

        int mx = 0, my = 0;
        foreach (var c in block.cells)
        {
            if (c.x > mx) mx = c.x;
            if (c.y > my) my = c.y;
        }

        float sx = slotPreviewCellSize + slotPreviewSpacing;
        float sy = slotPreviewCellSize + slotPreviewSpacing;
        float ox = -(mx * sx) * 0.5f;
        float oy = (my * sy) * 0.5f;

        for (int i = 0; i < block.cells.Count; i++)
        {
            var c = block.cells[i];
            Image img = refs.previewCells[i];
            RectTransform rt = img.rectTransform;

            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(slotPreviewCellSize, slotPreviewCellSize);
            rt.anchoredPosition = new Vector2(ox + c.x * sx, oy - c.y * sy);
            rt.localScale = Vector3.one;

            img.color = block.color;
            img.sprite = block.cellSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.raycastTarget = false;
            img.gameObject.SetActive(true);
        }

        HideUnusedSlotPreviewCells(refs, block.cells.Count);
    }
    /*
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
    */
    private void RefreshScoreUI()
    {
        if (scoreText != null) scoreText.text = $"점수 : {_myScore:N0}";
        if (bestScoreText != null) bestScoreText.text = $"최고 : {GetBestScore():N0}";
        if (comboText != null) comboText.text = _combo > 0 ? $"콤보 {_combo}" : "";
    }
    private Sprite GetArtifactSlotFrameSprite(ArtifactGrade grade)
    {
        switch (grade)
        {
            case ArtifactGrade.Normal: return normalArtifactSlotSprite;
            case ArtifactGrade.Rare: return rareArtifactSlotSprite;
            case ArtifactGrade.Epic: return epicArtifactSlotSprite;
            case ArtifactGrade.Unique: return uniqueArtifactSlotSprite;
            case ArtifactGrade.Legend: return legendArtifactSlotSprite;
            default: return normalArtifactSlotSprite;
        }
    }

    private void ShowArtifactSlotStateText(int slotIndex, string text)
    {
        if (slotIndex < 0 || slotIndex >= _artifactSlots.Length)
            return;

        ArtifactSlot slot = _artifactSlots[slotIndex];
        if (slot == null)
            return;

        if (slot.ChargeSliderStateText != null)
        {
            slot.ChargeSliderStateText.gameObject.SetActive(!string.IsNullOrWhiteSpace(text));
            slot.ChargeSliderStateText.text = text;
        }
        else if (slot.CooldownText != null)
        {
            slot.CooldownText.gameObject.SetActive(!string.IsNullOrWhiteSpace(text));
            slot.CooldownText.text = text;
        }
    }
    private void RefreshArtifactUI()
    {
        for (int i = 0; i < _artifactSlots.Length; i++)
            RefreshArtifactSlotUI(_artifactSlots[i]);
    }

    private void RefreshArtifactSlotUI(ArtifactSlot slot)
    {
        if (slot == null)
            return;

        NormalArtifactDefinition def = slot.Def;
        bool hasItem = def != null;

        NormalArtifactRuntimeManager.RuntimeArtifactState state = null;

        if (hasItem && _artifactRuntime != null)
        {
            state = _artifactRuntime.GetEquippedState(slot.Index);

            if (state != null && state.definition == def)
            {
                slot.Level = state.level;
                slot.CooldownRemaining = state.currentCooldown;
            }
            else
            {
                slot.Level = NormalArtifactLevelUtility.GetLevel(def);
                slot.CooldownRemaining = 0;
            }
        }
        else
        {
            slot.Level = NormalArtifactLevelUtility.GetLevel(def);
            slot.CooldownRemaining = 0;
        }

        if (slot.FrameImage != null)
        {
            slot.FrameImage.sprite = hasItem ? GetArtifactSlotFrameSprite(def.grade) : normalArtifactSlotSprite;
            slot.FrameImage.color = hasItem ? Color.white : artifactDisabledColor;
        }

        if (slot.IconImage != null)
        {
            slot.IconImage.gameObject.SetActive(hasItem);
            slot.IconImage.sprite = hasItem ? def.icon : null;
            slot.IconImage.enabled = hasItem && def.icon != null;
            slot.IconImage.preserveAspect = true;
        }

        if (slot.NameText != null)
            slot.NameText.text = hasItem ? def.DisplayNameSafe : string.Empty;

        if (slot.LevelText != null)
        {
            slot.LevelText.gameObject.SetActive(hasItem);
            slot.LevelText.text = hasItem ? $"Lv.{Mathf.Clamp(slot.Level, 1, 10)}" : string.Empty;
        }

        bool manualActive =
            hasItem &&
            def.IsActiveArtifact &&
            def.runtimeTriggerType == NormalArtifactTriggerType.ManualButton;

        bool ready = hasItem && GetArtifactSlotReady(slot, state);

        if (slot.CooldownText != null)
        {
            if (!hasItem)
            {
                slot.CooldownText.text = string.Empty;
                slot.CooldownText.gameObject.SetActive(false);
            }
            else if (!manualActive)
            {
                slot.CooldownText.text = string.Empty;
                slot.CooldownText.gameObject.SetActive(false);
            }
            else
            {
                slot.CooldownText.gameObject.SetActive(true);

                if (slot.CooldownRemaining == int.MaxValue)
                    slot.CooldownText.text = "";
                else if (slot.CooldownRemaining > 0)
                    slot.CooldownText.text = slot.CooldownRemaining.ToString();
                else
                    slot.CooldownText.text = "";
            }
        }

        if (slot.ReadyGlow != null)
            slot.ReadyGlow.gameObject.SetActive(hasItem && ready);

        RefreshArtifactChargeUI(slot, state, manualActive, ready);
    }

    private void RefreshArtifactChargeUI(
    ArtifactSlot slot,
    NormalArtifactRuntimeManager.RuntimeArtifactState state,
    bool manualActive,
    bool ready)
    {
        if (slot == null || slot.ChargeSlider == null)
            return;

        NormalArtifactDefinition def = slot.Def;
        bool hasItem = def != null;

        float fill = 0f;
        bool gaugeReady = false;
        string stateText = string.Empty;
        bool showGauge = false;

        if (hasItem && _artifactRuntime != null)
        {
            showGauge = _artifactRuntime.TryGetArtifactGaugeStatus(
                slot.Index,
                out fill,
                out gaugeReady,
                out stateText);
        }

        if (!showGauge && hasItem && manualActive)
        {
            int cdMax = Mathf.Max(0, def.GetCooldownValue(slot.Level));

            if (cdMax > 0)
            {
                showGauge = true;

                if (slot.CooldownRemaining == int.MaxValue)
                {
                    fill = 0f;
                    gaugeReady = false;
                    stateText = "USED";
                }
                else if (slot.CooldownRemaining > 0)
                {
                    fill = 1f - Mathf.Clamp01(slot.CooldownRemaining / (float)cdMax);
                    gaugeReady = false;
                    stateText = string.Empty;
                }
                else
                {
                    fill = 1f;
                    gaugeReady = true;
                    stateText = "READY";
                }
            }
            else
            {
                showGauge = true;
                fill = ready ? 1f : 0f;
                gaugeReady = ready;
                stateText = ready ? "READY" : string.Empty;
            }
        }

        slot.ChargeSlider.gameObject.SetActive(showGauge && hasItem);
        slot.ChargeSlider.value = Mathf.Clamp01(fill);

        if (slot.ChargeSliderStateText != null)
        {
            slot.ChargeSliderStateText.gameObject.SetActive(showGauge && hasItem && !string.IsNullOrWhiteSpace(stateText));
            slot.ChargeSliderStateText.text = stateText;
        }

        if (!showGauge || !hasItem)
        {
            SetArtifactSliderFillColor(slot.ChargeSlider, artifactChargeEmptyColor);
            return;
        }

        if (gaugeReady || ready)
            SetArtifactSliderFillColor(slot.ChargeSlider, artifactChargeReadyColor);
        else if (fill > 0f)
            SetArtifactSliderFillColor(slot.ChargeSlider, artifactChargeChargingColor);
        else
            SetArtifactSliderFillColor(slot.ChargeSlider, artifactChargeEmptyColor);
    }
    private bool GetArtifactSlotReady(
    ArtifactSlot slot,
    NormalArtifactRuntimeManager.RuntimeArtifactState state)
    {
        if (slot == null || slot.Def == null)
            return false;

        NormalArtifactDefinition def = slot.Def;

        if (state == null)
            return slot.CooldownRemaining <= 0;

        if (state.currentCooldown > 0)
            return false;

        if (def.equipEffectType == NormalArtifactEquipEffectType.ActiveChargeRotate90)
            return state.activeChargesRemaining > 0;

        if (def.equipEffectType == NormalArtifactEquipEffectType.ComboPreserveChance)
            return state.comboPreserveRemaining > 0;

        if (def.equipEffectType == NormalArtifactEquipEffectType.ChargeNextClearAfterPlacements)
            return state.placementChargeReady;

        return true;
    }
    private void SetArtifactSliderFillColor(Slider slider, Color color)
    {
        if (slider == null || slider.fillRect == null)
            return;

        Image fillImage = slider.fillRect.GetComponent<Image>();
        if (fillImage != null)
            fillImage.color = color;
    }

    private void RefreshArtifactChargeSliderUI(int slotIndex, ArtifactSlot slot)
    {
        if (slot == null)
            return;

        bool show = false;
        float fillAmount = 0f;
        bool isReady = false;
        string stateText = string.Empty;

        NormalArtifactRuntimeManager.RuntimeArtifactState state =
            _artifactRuntime != null ? _artifactRuntime.GetEquippedState(slotIndex) : null;

        if (TryGetArtifactChargeSliderStatus(slotIndex, state, out fillAmount, out isReady, out stateText))
            show = true;

        if (slot.ChargeSlider != null)
        {
            slot.ChargeSlider.gameObject.SetActive(show);

            if (show)
            {
                slot.ChargeSlider.interactable = false;
                slot.ChargeSlider.minValue = 0f;
                slot.ChargeSlider.maxValue = 1f;
                slot.ChargeSlider.wholeNumbers = false;
                slot.ChargeSlider.value = Mathf.Clamp01(fillAmount);

                Image fillImage = null;
                if (slot.ChargeSlider.fillRect != null)
                    fillImage = slot.ChargeSlider.fillRect.GetComponent<Image>();

                if (fillImage != null)
                {
                    if (isReady)
                        fillImage.color = artifactChargeReadyColor;
                    else if (fillAmount <= 0.001f)
                        fillImage.color = artifactChargeEmptyColor;
                    else
                        fillImage.color = artifactChargeChargingColor;
                }
            }
        }

        if (slot.ChargeSliderStateText != null)
        {
            slot.ChargeSliderStateText.gameObject.SetActive(show);
            slot.ChargeSliderStateText.text = show ? stateText : string.Empty;
        }
    }

    private bool TryGetArtifactChargeSliderStatus(
    int slotIndex,
    NormalArtifactRuntimeManager.RuntimeArtifactState state,
    out float fillAmount,
    out bool isReady,
    out string stateText)
    {
        fillAmount = 0f;
        isReady = false;
        stateText = string.Empty;

        if (state == null || !state.IsValid || state.definition == null)
            return false;

        NormalArtifactDefinition def = state.definition;

        // 런타임 매니저가 상태를 알고 있는 게이지형 아티팩트
        if (_artifactRuntime != null &&
            _artifactRuntime.TryGetArtifactGaugeStatus(slotIndex, out fillAmount, out isReady, out stateText))
        {
            return true;
        }

        // 마지막 한수: 현재 남은 블록 슬롯 기준으로 표시
        if (def.equipEffectType == NormalArtifactEquipEffectType.LastPlacementClearBonus)
        {
            int remaining = CountRemainingBlockSlots();
            int placed = Mathf.Clamp(3 - remaining, 0, 2);

            if (remaining <= 1)
            {
                fillAmount = 1f;
                isReady = true;
                stateText = "READY";
            }
            else
            {
                fillAmount = placed / 2f;
                isReady = false;
                stateText = string.Empty;
            }

            return true;
        }

        return false;
    }

    private int GetMaxActiveChargesForSlider(NormalArtifactDefinition def, int level)
    {
        if (def == null)
            return 0;

        if (def.equipEffectType != NormalArtifactEquipEffectType.ActiveChargeRotate90)
            return 0;

        return Mathf.Max(1, Mathf.RoundToInt(def.GetEquipValue(level)));
    }

    private int GetMaxComboPreserveCountForSlider(NormalArtifactDefinition def, int level)
    {
        if (def == null)
            return 0;

        if (def.equipEffectType == NormalArtifactEquipEffectType.ComboPreserveChance)
            return level >= 10 ? 2 : 1;

        if (def.equipEffectType == NormalArtifactEquipEffectType.PreserveComboOnce)
            return 1;

        return 0;
    }

    // ????????????????????????????????????????????
    //  드래그 미리보기
    // ????????????????????????????????????????????
    private void CreateDragPreview(BattleBlockInstance block)
    {
        UpdateDragPreview(block);
    }

    private void DestroyDragPreview()
    {
        if (_dragPreviewRoot == null)
            return;

        _dragPreviewRoot.gameObject.SetActive(false);
        HideUnusedDragPreviewCells(0);
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
    /*
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
    */
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
    private List<ClearLineInfo> CollectCompletedLines()
    {
        var result = new List<ClearLineInfo>();

        for (int y = 0; y < BattleBlockCore.BoardSize; y++)
        {
            bool full = true;
            for (int x = 0; x < BattleBlockCore.BoardSize; x++)
            {
                if (!_myOccupied[x, y])
                {
                    full = false;
                    break;
                }
            }

            if (full)
            {
                result.Add(new ClearLineInfo
                {
                    axis = NormalLineClearFx.Axis.Row,
                    index = y
                });
            }
        }

        for (int x = 0; x < BattleBlockCore.BoardSize; x++)
        {
            bool full = true;
            for (int y = 0; y < BattleBlockCore.BoardSize; y++)
            {
                if (!_myOccupied[x, y])
                {
                    full = false;
                    break;
                }
            }

            if (full)
            {
                result.Add(new ClearLineInfo
                {
                    axis = NormalLineClearFx.Axis.Column,
                    index = x
                });
            }
        }

        return result;
    }

    private void SpawnNormalLineClearFx(List<ClearLineInfo> infos)
    {
        if (_myBoardRoot == null || infos == null || infos.Count == 0)
            return;

        foreach (var info in infos)
        {
            NormalLineClearFx fx = RentLineClearFx();
            fx.transform.SetParent(_myBoardRoot, false);
            fx.transform.SetAsLastSibling();

            fx.Play(
                info.axis,
                info.index,
                BattleBlockCore.BoardSize,
                myBoardCellSize,
                myBoardSpacing,
                normalLineClearFxColor,
                normalLineClearFxHoldDuration,
                normalLineClearFxFadeOutDuration,
                normalLineClearFxThicknessScale);
        }
    }
    private void SpawnNormalComboFx(int combo, int baseScore, int artifactBonus)
    {
        if (_myBoardRoot == null || combo < 2)
            return;

        NormalComboFx fx = RentComboFx();
        fx.transform.SetParent(_myBoardRoot, false);
        fx.transform.SetAsLastSibling();

        RectTransform rt = fx.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = comboFxOffset;

        fx.Play(
            combo,
            comboFxFont,
            GetComboFxColor(combo),
            comboFxDuration,
            comboFxRiseDistance,
            baseScore,
            artifactBonus,
            comboScoreArtifactIcon,
            comboScoreBackgroundSprite,
            comboScoreBaseColor,
            comboScoreArtifactColor,
            comboScoreBackgroundColor,
            comboScoreBackgroundPadding);
    }

    private Color GetComboFxColor(int combo)
    {
        if (combo >= 6)
            return comboFxColorHigh;

        if (combo >= 4)
            return comboFxColorMid;

        return comboFxColorLow;
    }
    private void ProcessComboReward(int prevCombo, int newCombo, out int baseApplied, out int artifactApplied)
    {
        baseApplied = 0;
        artifactApplied = 0;

        if (newCombo < 2)
            return;

        int baseComboRaw = newCombo * ComboScorePerCount;
        int baseMilestoneRaw = 0;

        foreach (var (threshold, bonus) in ComboMilestones)
        {
            if (newCombo >= threshold && prevCombo < threshold)
                baseMilestoneRaw += bonus;
        }

        int baseRaw = baseComboRaw + baseMilestoneRaw;

        float comboMult = _artifactRuntime != null
            ? _artifactRuntime.EvaluateComboScoreMultiplier()
            : 1f;

        int totalRaw = Mathf.RoundToInt(baseRaw * comboMult);

        AddScore(totalRaw);

        baseApplied = EvaluateActualScoreGain(baseRaw);
        int totalApplied = EvaluateActualScoreGain(totalRaw);
        artifactApplied = Mathf.Max(0, totalApplied - baseApplied);
    }

    private void WarmupFxPools()
    {
        if (_myBoardRoot == null)
            return;

        for (int i = _normalLineClearFxPool.Count; i < normalLineClearFxPoolSize; i++)
        {
            NormalLineClearFx fx = CreateLineClearFxInstance();
            ReturnLineClearFx(fx);
        }

        for (int i = _normalComboFxPool.Count; i < normalComboFxPoolSize; i++)
        {
            NormalComboFx fx = CreateComboFxInstance();
            ReturnComboFx(fx);
        }
    }

    private NormalLineClearFx CreateLineClearFxInstance()
    {
        var go = new GameObject(
            RuntimePrefix + "NormalLineClearFx",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image),
            typeof(NormalLineClearFx));

        go.transform.SetParent(_myBoardRoot, false);

        var fx = go.GetComponent<NormalLineClearFx>();
        fx.OnFinished = ReturnLineClearFx;
        return fx;
    }

    private NormalComboFx CreateComboFxInstance()
    {
        var go = new GameObject(
            RuntimePrefix + "NormalComboFx",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(NormalComboFx));

        go.transform.SetParent(_myBoardRoot, false);

        var fx = go.GetComponent<NormalComboFx>();
        fx.OnFinished = ReturnComboFx;
        return fx;
    }

    private NormalLineClearFx RentLineClearFx()
    {
        if (_normalLineClearFxPool.Count > 0)
            return _normalLineClearFxPool.Dequeue();

        return CreateLineClearFxInstance();
    }

    private NormalComboFx RentComboFx()
    {
        if (_normalComboFxPool.Count > 0)
            return _normalComboFxPool.Dequeue();

        return CreateComboFxInstance();
    }

    private void ReturnLineClearFx(NormalLineClearFx fx)
    {
        if (fx == null)
            return;

        fx.transform.SetParent(_myBoardRoot, false);
        fx.gameObject.SetActive(false);
        _normalLineClearFxPool.Enqueue(fx);
    }

    private void ReturnComboFx(NormalComboFx fx)
    {
        if (fx == null)
            return;

        fx.transform.SetParent(_myBoardRoot, false);
        fx.gameObject.SetActive(false);
        _normalComboFxPool.Enqueue(fx);
    }
    private void EnsureDragPreviewRoot()
    {
        if (_dragPreviewInitialized && _dragPreviewRoot != null)
            return;

        if (_dragPreviewRoot == null)
        {
            var go = new GameObject(RuntimePrefix + "DragPreview", typeof(RectTransform));
            _dragPreviewRoot = go.GetComponent<RectTransform>();
            _dragPreviewRoot.SetParent(_dragLayer, false);
            _dragPreviewRoot.anchorMin = _dragPreviewRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _dragPreviewRoot.pivot = new Vector2(0.5f, 0.5f);
        }

        _dragPreviewRoot.gameObject.SetActive(false);
        _dragPreviewInitialized = true;
    }

    private void EnsureDragPreviewPool(int requiredCount)
    {
        EnsureDragPreviewRoot();

        while (_dragPreviewCells.Count < requiredCount)
        {
            var go = CreatePreviewCellObject(_dragPreviewRoot);
            var img = GetOrAdd<Image>(go);
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.raycastTarget = false;
            go.SetActive(false);
            _dragPreviewCells.Add(img);
        }
    }

    private void HideUnusedDragPreviewCells(int usedCount)
    {
        for (int i = usedCount; i < _dragPreviewCells.Count; i++)
        {
            if (_dragPreviewCells[i] != null)
                _dragPreviewCells[i].gameObject.SetActive(false);
        }
    }

    private void UpdateDragPreview(BattleBlockInstance block)
    {
        EnsureDragPreviewRoot();

        if (block?.cells == null || block.cells.Count == 0)
        {
            _dragPreviewRoot.gameObject.SetActive(false);
            HideUnusedDragPreviewCells(0);
            return;
        }

        _dragPreviewRoot.gameObject.SetActive(true);
        EnsureDragPreviewPool(block.cells.Count);

        int w = GetBlockWidth(block);
        int h = GetBlockHeight(block);

        float tw = w * dragPreviewCellSize + (w - 1) * dragPreviewSpacing;
        float th = h * dragPreviewCellSize + (h - 1) * dragPreviewSpacing;
        float sx = -tw * 0.5f + dragPreviewCellSize * 0.5f;
        float sy = th * 0.5f - dragPreviewCellSize * 0.5f;

        for (int i = 0; i < block.cells.Count; i++)
        {
            var p = block.cells[i];
            var img = _dragPreviewCells[i];
            var rt = img.rectTransform;

            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(dragPreviewCellSize, dragPreviewCellSize);
            rt.anchoredPosition = new Vector2(
                sx + p.x * (dragPreviewCellSize + dragPreviewSpacing),
                sy - p.y * (dragPreviewCellSize + dragPreviewSpacing));
            rt.localScale = Vector3.one;

            img.color = new Color(block.color.r, block.color.g, block.color.b, 0.85f);
            img.sprite = block.cellSprite;
            img.raycastTarget = false;
            img.gameObject.SetActive(true);
        }

        HideUnusedDragPreviewCells(block.cells.Count);
    }
    private void OpenPausePopup()
    {
        _isPausePopupOpen = true;
        ShowResultPopup(false);
    }

    private void ClosePausePopup()
    {
        _isPausePopupOpen = false;

        if (resultPhaseRoot != null)
            resultPhaseRoot.SetActive(false);
    }

    private void ShowResultPopup(bool gameOver)
    {
        if (gameOver)
            _isGameOver = true;

        DestroyDragPreview();

        int prev = GetBestScore();
        int best = gameOver ? SaveBestScore(_myScore) : Mathf.Max(prev, _myScore);

        var state = _myScore > prev ? BestScoreState.New
                  : _myScore == prev ? BestScoreState.Equal
                  : BestScoreState.Below;

        if (resultPhaseRoot != null)
            resultPhaseRoot.SetActive(true);

        if (resultTitleText != null)
            resultTitleText.text = gameOver ? "Finish" : "Pause";

        if (resultScoreText != null)
            resultScoreText.text = $"{_myScore:N0}";

        if (resultBestScoreText != null)
            resultBestScoreText.text = $"{best:N0}";

        if (resultStateBelow != null)
            resultStateBelow.SetActive(state == BestScoreState.Below);

        if (resultStateEqual != null)
            resultStateEqual.SetActive(state == BestScoreState.Equal);

        if (resultStateNew != null)
            resultStateNew.SetActive(state == BestScoreState.New);

        if (retryButton != null)
            retryButton.gameObject.SetActive(!gameOver);

        SetButtonLabel(retryButton, "Back");
        SetButtonLabel(resultLobbyButton, "EXIT");

        BuildResultDropUI();
        RefreshPauseLootCount();
    }

    private void ClaimRunRewardsOnce()
    {
        if (_runRewardsClaimed)
            return;

        _runRewardsClaimed = true;

        // TODO:
        // 실제 재화 저장 연결
        // EconomyManager.I.AddGold(_earnedGold);
        // EconomyManager.I.AddStoneBasic(_earnedStoneBasic);
        // EconomyManager.I.AddStoneMid(_earnedStoneMid);
        // EconomyManager.I.AddStoneHigh(_earnedStoneHigh);

        Debug.Log($"[NormalManager] 보상 확정 / Gold={_earnedGold}, Basic={_earnedStoneBasic}, Mid={_earnedStoneMid}, High={_earnedStoneHigh}");
    }

    private void RefreshPauseLootCount()
    {
        if (pauseLootCountText == null)
            return;

        int total = _earnedGold + _earnedStoneBasic + _earnedStoneMid + _earnedStoneHigh;
        pauseLootCountText.text = total > 0 ? total.ToString() : string.Empty;
    }

    private void SetButtonLabel(Button btn, string text)
    {
        if (btn == null)
            return;

        TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
            tmp.text = text;
    }

    private Sprite GetPickupFlySprite(DropItemType type)
    {
        switch (type)
        {
            case DropItemType.Gold:
                return goldSprite;
            case DropItemType.EnhanceStoneBasic:
                return stoneBasicSprite;
            case DropItemType.EnhanceStoneMid:
                return stoneMidSprite;
            case DropItemType.EnhanceStoneHigh:
                return stoneHighSprite;
            default:
                return null;
        }
    }

    private void WarmupPickupFlyPool()
    {
        if (_dragLayer == null)
            return;

        while (_pickupFlyFxPool.Count < pickupFlyFxPoolSize)
        {
            var fx = CreatePickupFlyFxInstance();
            ReturnPickupFlyFx(fx);
        }
    }

    private NormalPickupFlyFx CreatePickupFlyFxInstance()
    {
        var go = new GameObject(
            RuntimePrefix + "PickupFlyFx",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image),
            typeof(NormalPickupFlyFx));

        go.transform.SetParent(_dragLayer, false);
        go.transform.SetAsLastSibling();

        var fx = go.GetComponent<NormalPickupFlyFx>();
        fx.OnFinished = ReturnPickupFlyFx;
        return fx;
    }

    private NormalPickupFlyFx RentPickupFlyFx()
    {
        if (_pickupFlyFxPool.Count > 0)
            return _pickupFlyFxPool.Dequeue();

        return CreatePickupFlyFxInstance();
    }

    private void ReturnPickupFlyFx(NormalPickupFlyFx fx)
    {
        if (fx == null)
            return;

        fx.transform.SetParent(_dragLayer, false);
        fx.gameObject.SetActive(false);
        _pickupFlyFxPool.Enqueue(fx);
    }

    private void SpawnPickupFlyFx(Sprite sprite, int boardX, int boardY)
    {
        if (sprite == null || _dragLayer == null || pausePickupTarget == null)
            return;

        if (boardX < 0 || boardX >= BattleBlockCore.BoardSize ||
            boardY < 0 || boardY >= BattleBlockCore.BoardSize)
            return;

        BoardCell cell = _myBoardCells[boardX, boardY];
        if (cell == null)
            return;

        RectTransform cellRt = cell.GetComponent<RectTransform>();
        if (cellRt == null)
            return;

        Camera cam = GetUiCamera();

        Vector2 from = WorldToLocalOnDragLayer(cellRt.TransformPoint(cellRt.rect.center), cam);
        Vector2 to = WorldToLocalOnDragLayer(pausePickupTarget.TransformPoint(pausePickupTarget.rect.center), cam);

        NormalPickupFlyFx fx = RentPickupFlyFx();
        fx.transform.SetParent(_dragLayer, false);
        fx.transform.SetAsLastSibling();

        fx.Play(
            sprite,
            from,
            to,
            pickupFlyDuration,
            pickupFlyArcHeight,
            pickupFlyStartScale,
            pickupFlyEndScale);
    }

    private Vector2 WorldToLocalOnDragLayer(Vector3 worldPos, Camera cam)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _dragLayer,
            RectTransformUtility.WorldToScreenPoint(cam, worldPos),
            cam,
            out Vector2 local);

        return local;
    }

    private Camera GetUiCamera()
    {
        if (_canvas == null)
            return null;

        return _canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _canvas.worldCamera;
    }

    private void WarmupResultDropEntryPool()
    {
        if (resultDropRoot == null || resultDropEntryPrefab == null)
            return;

        while (_resultDropEntryPool.Count < resultDropEntryPoolSize)
        {
            var entry = CreateResultDropEntryInstance();
            ReturnResultDropEntry(entry);
        }
    }

    private NormalResultDropEntryView CreateResultDropEntryInstance()
    {
        var entry = Instantiate(resultDropEntryPrefab, resultDropRoot);
        entry.gameObject.name = RuntimePrefix + "ResultDropEntry";
        entry.gameObject.SetActive(false);
        return entry;
    }

    private NormalResultDropEntryView RentResultDropEntry()
    {
        if (_resultDropEntryPool.Count > 0)
            return _resultDropEntryPool.Dequeue();

        return CreateResultDropEntryInstance();
    }

    private void ReturnResultDropEntry(NormalResultDropEntryView entry)
    {
        if (entry == null)
            return;

        entry.transform.SetParent(resultDropRoot, false);
        entry.SetVisible(false);
        _resultDropEntryPool.Enqueue(entry);
    }

    private void ClearActiveResultDropEntries()
    {
        for (int i = 0; i < _activeResultDropEntries.Count; i++)
            ReturnResultDropEntry(_activeResultDropEntries[i]);

        _activeResultDropEntries.Clear();
    }

    private void AddResultDropEntry(Sprite icon, string label, int count)
    {
        if (resultDropRoot == null || resultDropEntryPrefab == null)
            return;

        if (count <= 0 || icon == null)
            return;

        var entry = RentResultDropEntry();
        entry.transform.SetParent(resultDropRoot, false);
        entry.SetData(icon, label, count);
        entry.SetVisible(true);

        _activeResultDropEntries.Add(entry);
    }

    private int EvaluateActualScoreGain(int rawAmount)
    {
        if (rawAmount <= 0)
            return 0;

        float mult = _scoreMultiplierRemaining > 0 ? _scoreMultiplier : 1f;
        return Mathf.RoundToInt(rawAmount * mult);
    }

    private int GetBaseClearScoreRaw(int lines)
    {
        int idx = Mathf.Clamp(lines, 0, MultiLineBonusTable.Length - 1);
        return MultiLineBonusTable[idx];
    }

    private int GetBaseComboRewardRaw(int prevCombo, int newCombo)
    {
        if (newCombo < 2)
            return 0;

        int total = newCombo * ComboScorePerCount;

        foreach (var (threshold, bonus) in ComboMilestones)
        {
            if (newCombo >= threshold && prevCombo < threshold)
                total += bonus;
        }

        return total;
    }

    private void WarmupScorePopupPool()
    {
        if (_dragLayer == null)
            return;

        for (int i = _scorePopupPool.Count; i < scorePopupPoolSize; i++)
        {
            NormalScorePopupFx fx = CreateScorePopupFxInstance();
            ReturnScorePopupFx(fx);
        }
    }

    private NormalScorePopupFx CreateScorePopupFxInstance()
    {
        GameObject go = new GameObject(
            RuntimePrefix + "ScorePopupFx",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(NormalScorePopupFx));

        go.transform.SetParent(_dragLayer != null ? _dragLayer : transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;

        go.SetActive(false);
        return go.GetComponent<NormalScorePopupFx>();
    }

    private NormalScorePopupFx RentScorePopupFx()
    {
        if (_scorePopupPool.Count > 0)
        {
            NormalScorePopupFx fx = _scorePopupPool.Dequeue();
            if (fx != null)
            {
                fx.gameObject.SetActive(true);
                fx.transform.SetParent(_dragLayer != null ? _dragLayer : transform, false);
                fx.transform.SetAsLastSibling();
                return fx;
            }
        }

        NormalScorePopupFx created = CreateScorePopupFxInstance();
        created.gameObject.SetActive(true);
        created.transform.SetAsLastSibling();
        return created;
    }

    private void ReturnScorePopupFx(NormalScorePopupFx fx)
    {
        if (fx == null)
            return;

        fx.gameObject.SetActive(false);
        fx.transform.SetParent(_dragLayer != null ? _dragLayer : transform, false);
        _scorePopupPool.Enqueue(fx);
    }

    private void SpawnPlacementScorePopup(BattleBlockInstance block, Vector2Int anchor, int baseScore, int artifactBonus)
    {
        if (baseScore <= 0 && artifactBonus <= 0)
            return;

        Vector2 anchoredPos = GetBlockPopupAnchoredPosition(block, anchor);
        SpawnScorePopupAt(anchoredPos, baseScore, artifactBonus, false);
    }

    private void SpawnLineScorePopup(List<ClearLineInfo> infos, int baseScore, int artifactBonus)
    {
        if ((baseScore <= 0 && artifactBonus <= 0) || infos == null || infos.Count == 0)
            return;

        Vector2 anchoredPos;

        if (infos.Count == 1)
        {
            anchoredPos = GetLinePopupAnchoredPosition(infos[0]);
        }
        else
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < infos.Count; i++)
                sum += GetLinePopupAnchoredPosition(infos[i]);

            anchoredPos = sum / infos.Count;
        }

        SpawnScorePopupAt(anchoredPos, baseScore, artifactBonus, false);
    }

    private void SpawnComboScorePopup(int baseScore, int artifactBonus)
    {
        if (baseScore <= 0 && artifactBonus <= 0)
            return;

        if (comboText == null)
            return;

        RectTransform comboRt = comboText.rectTransform;
        Vector3 world = comboRt.TransformPoint(comboRt.rect.center);
        Vector2 anchoredPos = WorldToScorePopupAnchored(world) + comboScorePopupOffset;

        SpawnScorePopupAt(anchoredPos, baseScore, artifactBonus, true);
    }

    private void SpawnScorePopupAt(Vector2 anchoredPos, int baseScore, int artifactBonus, bool comboStyle)
    {
        if (baseScore <= 0 && artifactBonus <= 0)
            return;

        NormalScorePopupFx fx = RentScorePopupFx();
        fx.Play(
            anchoredPos,
            scorePopupFont != null ? scorePopupFont : comboFxFont,
            baseScore,
            artifactBonus,
            artifactScorePopupIcon,
            comboStyle ? comboScorePopupBaseColor : scorePopupBaseColor,
            scorePopupArtifactColor,
            comboStyle ? comboScorePopupBackgroundSprite : null,
            comboStyle ? comboScorePopupBackgroundColor : Color.white,
            comboStyle ? comboScorePopupBackgroundPadding : Vector2.zero,
            scorePopupRiseDistance,
            scorePopupDuration,
            ReturnScorePopupFx);
    }

    private Vector2 GetBlockPopupAnchoredPosition(BattleBlockInstance block, Vector2Int anchor)
    {
        if (_myBoardRoot == null || block == null || block.cells == null || block.cells.Count == 0)
            return Vector2.zero;

        Vector2 sumLocal = Vector2.zero;
        for (int i = 0; i < block.cells.Count; i++)
        {
            int x = anchor.x + block.cells[i].x;
            int y = anchor.y + block.cells[i].y;
            sumLocal += GetBoardCellLocalCenter(x, y);
        }

        Vector2 centerLocal = sumLocal / block.cells.Count;
        centerLocal += placementScorePopupOffset;

        Vector3 world = _myBoardRoot.TransformPoint(centerLocal);
        return WorldToScorePopupAnchored(world);
    }

    private Vector2 GetLinePopupAnchoredPosition(ClearLineInfo info)
    {
        if (_myBoardRoot == null)
            return Vector2.zero;

        Vector2 local;

        if (info.axis == NormalLineClearFx.Axis.Row)
        {
            local = GetBoardCellLocalCenter(BattleBlockCore.BoardSize - 1, info.index);
            local += Vector2.right * lineScoreOutsideOffset;
        }
        else
        {
            local = GetBoardCellLocalCenter(info.index, BattleBlockCore.BoardSize - 1);
            local += Vector2.down * lineScoreOutsideOffset * 0;
        }

        Vector3 world = _myBoardRoot.TransformPoint(local);
        return WorldToScorePopupAnchored(world);
    }

    private Vector2 GetBoardCellLocalCenter(int x, int y)
    {
        float stepX = myBoardCellSize.x + myBoardSpacing.x;
        float stepY = myBoardCellSize.y + myBoardSpacing.y;

        float ox = -((BattleBlockCore.BoardSize - 1) * stepX) * 0.5f;
        float oy = ((BattleBlockCore.BoardSize - 1) * stepY) * 0.5f;

        return new Vector2(
            ox + (x * stepX),
            oy - (y * stepY));
    }

    private Vector2 WorldToScorePopupAnchored(Vector3 worldPos)
    {
        if (_dragLayer == null)
            return Vector2.zero;

        Camera cam = null;
        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = _canvas.worldCamera;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragLayer, screenPoint, cam, out Vector2 localPoint);
        return localPoint;
    }

    private void GiveSingleCellEmergencyBlocks()
    {
        for (int i = 0; i < _currentBlocks.Length; i++)
        {
            BattleBlockInstance block = BattleBlockCore.CreateRandomNormalBlock(_normalShapes);
            if (block != null && block.cells != null)
            {
                block.cells.Clear();
                block.cells.Add(Vector2Int.zero);
            }

            _currentBlocks[i] = block;
        }
    }

    private int GetOccupiedCellCount()
    {
        int count = 0;

        for (int y = 0; y < BattleBlockCore.BoardSize; y++)
        {
            for (int x = 0; x < BattleBlockCore.BoardSize; x++)
            {
                if (_myOccupied[x, y])
                    count++;
            }
        }

        return count;
    }
    private bool IsDimensionWarpPlacementActive()
    {
        return _dimensionWarpPlacementRemaining > 0;
    }

    private void ArmDimensionWarpPlacement(NormalArtifactRuntimeManager.RuntimeArtifactState state)
    {
        int count = 1;

        if (state != null && state.definition != null && state.definition.paramA > 0)
            count = state.definition.paramA;

        _dimensionWarpPlacementRemaining = Mathf.Max(1, count);

        RefreshAllUI();
    }

    private void ConsumeDimensionWarpPlacement()
    {
        if (_dimensionWarpPlacementRemaining <= 0)
            return;

        _dimensionWarpPlacementRemaining--;

        if (_dimensionWarpPlacementRemaining < 0)
            _dimensionWarpPlacementRemaining = 0;
    }

    private bool CanPlaceBlockDimensionWarp(BattleBlockInstance block, int anchorX, int anchorY)
    {
        if (block == null || block.cells == null || block.cells.Count == 0)
            return false;

        for (int i = 0; i < block.cells.Count; i++)
        {
            int x = anchorX + block.cells[i].x;
            int y = anchorY + block.cells[i].y;

            if (x < 0 || x >= BattleBlockCore.BoardSize)
                return false;

            if (y < 0 || y >= BattleBlockCore.BoardSize)
                return false;
        }

        return true;
    }

    private void PlaceBlockDimensionWarp(BattleBlockInstance block, Vector2Int anchor)
    {
        if (block == null || block.cells == null)
            return;

        for (int i = 0; i < block.cells.Count; i++)
        {
            int x = anchor.x + block.cells[i].x;
            int y = anchor.y + block.cells[i].y;

            if (x < 0 || x >= BattleBlockCore.BoardSize)
                continue;

            if (y < 0 || y >= BattleBlockCore.BoardSize)
                continue;

            // 기존 점유 상태를 무시하고 덮어쓴다.
            _myOccupied[x, y] = true;
            _myColors[x, y] = block.color;
            _myBlockSprites[x, y] = block.cellSprite;
        }
    }
}