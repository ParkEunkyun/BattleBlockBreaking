using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.IO;

public class BattleManager : MonoBehaviour, IDragBlockOwner
{
    public enum ItemKind
    {
        None,
        Attack,
        Support,
        Defense
    }

    public enum AttackItemId
    {
        None,
        Obstacle2,
        SealRandomSlot,
        CurseBlock,
        DisableItemUse,
        DeleteRandomLine,
        Bomb3x3
    }

    public enum SupportItemId
    {
        None,
        RotateRight90,
        RotateLeft90,
        MirrorHorizontal,
        MirrorVertical,
        ResetRemaining
    }

    public enum BattleItemId
    {
        None,

        AttackObstacle2,
        AttackSealRandomSlot,
        AttackCurseBlock,
        AttackDisableItemUse,
        AttackDeleteRandomLine,
        AttackBomb3x3,

        SupportRotateRight90,
        SupportRotateLeft90,
        SupportMirrorHorizontal,
        SupportMirrorVertical,
        SupportResetRemaining,

        DefenseUniversal
    }

    private enum BattlePhase
    {
        Defense,
        Play,
        Resolve
    }

    private enum BlockShapeId
    {
        H3,
        L3,
        H4,
        O4,
        T4,
        L4,
        L4_M,
        Z4,
        Z4_M,
        H5,
        P5,
        T5,
        L5,

        CurseDiag3,
        CurseSplit3,
        CurseDiag4,
        CurseSplit4
    }

    [Serializable]
    private sealed class OwnedItemData
    {
        public int ownedId;
        public BattleItemId itemId;
    }

    private sealed class BlockShape
    {
        public BlockShapeId shapeId;
        public int weight;
        public Color color;
        public List<Vector2Int> baseCells;
        public bool isCurse;
        public Sprite cellSprite;

        public BlockShape(BlockShapeId shapeId, int weight, Color color, List<Vector2Int> baseCells, bool isCurse, Sprite cellSprite)
        {
            this.shapeId = shapeId;
            this.weight = weight;
            this.color = color;
            this.baseCells = baseCells;
            this.isCurse = isCurse;
            this.cellSprite = cellSprite;
        }
    }

    private sealed class BlockInstance
    {
        public BlockShapeId shapeId;
        public int rotation;
        public Color color;
        public List<Vector2Int> cells;
        public bool isCurse;
        public Sprite cellSprite;
    }

    [Header("Prefabs / Sprites")]
    [SerializeField] private GameObject boardCellPrefab;
    [SerializeField] private GameObject previewCellPrefab;

    [SerializeField] private Sprite obstacleSprite;
    [SerializeField] private Sprite sealedSlotOverlaySprite;
    [Serializable]
    private sealed class ItemSpriteEntry
    {
        public BattleItemId itemId;
        public Sprite itemSprite;       // 인벤토리/보드 드랍용
        public Sprite incomingSprite;   // WarningPanel용 (공격 아이템만)
    }

    [SerializeField] private List<ItemSpriteEntry> itemSpriteEntries = new List<ItemSpriteEntry>();
    private readonly Dictionary<BattleItemId, ItemSpriteEntry> _itemSpriteMap = new Dictionary<BattleItemId, ItemSpriteEntry>();

    [Header("Board Size")]
    [SerializeField] private Vector2 myBoardCellSize = new Vector2(64f, 64f);
    [SerializeField] private Vector2 myBoardSpacing = new Vector2(4f, 4f);
    [SerializeField] private Vector2 opponentMiniCellSize = new Vector2(18f, 18f);
    [SerializeField] private Vector2 opponentMiniSpacing = new Vector2(3f, 3f);

    [Header("Preview Size")]
    [SerializeField] private float slotPreviewCellSize = 24f;
    [SerializeField] private float slotPreviewSpacing = 4f;
    [SerializeField] private float dragPreviewCellSize = 30f;
    [SerializeField] private float dragPreviewSpacing = 4f;

    [SerializeField] private Vector2 dragOffset = new Vector2(0f, 180f);

    [Header("Round / Phase")]
    [SerializeField] private int maxRounds = 12;
    [SerializeField] private float defensePhaseSeconds = 3f;
    [SerializeField] private float playPhaseSeconds = 30f;
    [SerializeField] private float resolvePhaseSeconds = 0.2f;

    [Header("Drop Weight")]
    [Range(0f, 1f)][SerializeField] private float attackDropWeight = 0.44f;
    [Range(0f, 1f)][SerializeField] private float supportDropWeight = 0.44f;
    [Range(0f, 1f)][SerializeField] private float defenseDropWeight = 0.12f;
    [SerializeField] private int maxBoardItemCount = 2;
    [SerializeField] private float boardItemSpawnChancePerRound = 0.60f;

    [Header("Loadout")]
    [SerializeField]
    private AttackItemId[] selectedAttackLoadout = new AttackItemId[3]
    {
        AttackItemId.Obstacle2,
        AttackItemId.SealRandomSlot,
        AttackItemId.Bomb3x3
    };

    [SerializeField]
    private SupportItemId[] selectedSupportLoadout = new SupportItemId[2]
    {
        SupportItemId.RotateRight90,
        SupportItemId.ResetRemaining
    };

    [Header("Debug / Test")]
    [SerializeField] private bool loopbackReservedAttackForDebug = false;
    [SerializeField] private Button debugAddAttackItemButton;
    [SerializeField] private Button debugAddDefenseItemButton;
    [SerializeField] private Button debugIncomingObstacleButton;
    [SerializeField] private Button debugIncomingSealButton;

    [Header("End")]
    [SerializeField] private Button roundEndButton;
    [SerializeField] private bool debugAutoOpponentRoundReady = false;

    [Header("Result")]
    [SerializeField] private string lobbySceneName = "Scene_Lobby";
    [SerializeField] private string myNickname = "Me";
    [SerializeField] private string opponentNickname = "Opponent";
    [SerializeField] private int opponentScore = 0;

    [Header("Block Cell Sprites")]
    [SerializeField] private Sprite h3CellSprite;
    [SerializeField] private Sprite l3CellSprite;
    [SerializeField] private Sprite h4CellSprite;
    [SerializeField] private Sprite o4CellSprite;
    [SerializeField] private Sprite t4CellSprite;
    [SerializeField] private Sprite l4CellSprite;
    [SerializeField] private Sprite l4MCellSprite;
    [SerializeField] private Sprite z4CellSprite;
    [SerializeField] private Sprite z4MCellSprite;
    [SerializeField] private Sprite h5CellSprite;
    [SerializeField] private Sprite p5CellSprite;
    [SerializeField] private Sprite t5CellSprite;
    [SerializeField] private Sprite l5CellSprite;
    [SerializeField] private Sprite curseDiag3CellSprite;
    [SerializeField] private Sprite curseSplit3CellSprite;
    [SerializeField] private Sprite curseDiag4CellSprite;
    [SerializeField] private Sprite curseSplit4CellSprite;

    [Header("Loading Overlay")]
    [SerializeField] private GameObject loadingRoot;
    [SerializeField] private TMP_Text loadingText;

    [Header("Attack Telegraph / FX")]
    [SerializeField] private BattleAttackTelegraphView _incomingAttackTelegraphView;
    [SerializeField] private BattleMiniBoardFx _opponentMiniBoardFx;

    private BattleItemId _remotePlannedAttackItem = BattleItemId.None;
    private bool _remoteAttackPlannedVisible = false;

    [Header("Line Clear FX")]
    [SerializeField] private BattleLineClearFx _lineClearFx;
    private bool _isResolvingLineClearFx;

    [SerializeField] private int _lineClearFxPoolSize = 4;

    private readonly Queue<BattleLineClearFx> _lineClearFxPool = new Queue<BattleLineClearFx>();
    private readonly List<BattleLineClearFx> _activeLineClearFxInstances = new List<BattleLineClearFx>();
    private Transform _lineClearFxPoolRoot;

    [Header("Combo FX")]
    [SerializeField] private BattleComboFx _comboFxTemplate;
    [SerializeField] private TMP_FontAsset _comboFxFont;
    [SerializeField] private Vector2 _comboFxOffset = new Vector2(0f, 36f);
    [SerializeField] private float _comboFxRiseDistance = 72f;
    [SerializeField] private float _comboFxDuration = 0.46f;
    [SerializeField] private int _comboFxPoolSize = 4;
    [SerializeField] private Color _comboFxColorLow = new Color(0.45f, 0.90f, 1f, 1f);
    [SerializeField] private Color _comboFxColorMid = new Color(0.80f, 0.45f, 1f, 1f);
    [SerializeField] private Color _comboFxColorHigh = new Color(1f, 0.82f, 0.25f, 1f);

    [Header("Combo Score")]
    [SerializeField] private int[] _comboBonusTable = { 0, 0, 15, 35, 60, 90, 125, 165, 210, 260 };

    private readonly Queue<BattleComboFx> _comboFxPool = new Queue<BattleComboFx>();
    private Transform _comboFxPoolRoot;

    private int _comboCount;
    private int _maxComboCount;
    private RectTransform _comboFxAnchorRoot;

    private bool _comboHadClearThisRound;
    private bool _comboRoundStartedOnce;

    [Header("Defense FX")]
    [SerializeField] private BattleDefenseBoardFx _myBoardDefenseFx;
    private bool _isResolvingDefenseFx;

    [Header("Incoming Attack FX")]
    [SerializeField] private BattleIncomingAttackFx _incomingAttackFx;
    private bool _isResolvingIncomingAttackFx;

    private Coroutine _loadingHideRoutine;

    private GameObject _resultPhaseRoot;
    private GameObject _victoryEmblem;
    private GameObject _drawEmblem;
    private GameObject _defeatEmblem;
    private TMP_Text _myResultText;
    private TMP_Text _opponentResultText;
    private TMP_Text _myNicknameText;
    private TMP_Text _opponentNicknameText;
    private Button _resultLobbyButton;

    private const int BoardSize = 8;
    private const int InventoryCapacity = 8;
    private const string RuntimePrefix = "BBB_RT_";

    private static readonly Color32 EmptySlotColor = new Color32(255, 255, 255, 255);
    private static readonly Color32 AttackSlotColor = new Color32(255, 255, 255, 255); //new Color32(255, 100, 100, 255);
    private static readonly Color32 SupportSlotColor = new Color32(255, 255, 255, 255); //new Color32(255, 203, 100, 255);
    private static readonly Color32 DefenseSlotColor = new Color32(255, 255, 255, 255); //new Color32(100, 105, 255, 255);
    private static readonly Color32 BoardBaseColor = new Color32(255, 255, 255, 255); //new Color32(36, 38, 56, 255);
    private static readonly Color32 ObstacleColor = new Color32(62, 63, 72, 255);
    private static readonly Color32 SealedSlotColor = new Color32(140, 100, 220, 255);

    private static readonly Color32 BlockColor1 = new Color32(230, 117, 127, 255); // E6757F
    private static readonly Color32 BlockColor2 = new Color32(180, 113, 232, 255); // B471E8
    private static readonly Color32 BlockColor3 = new Color32(230, 213, 107, 255); // E6D56B
    private static readonly Color32 BlockColor4 = new Color32(107, 228, 130, 255); // 6BE482
    private static readonly Color32 BlockColor5 = new Color32(112, 154, 231, 255); // 709AE7

    private Canvas _canvas;
    private RectTransform _dragLayer;

    private TMP_Text _roundText;
    private TMP_Text _timerText;
    private TMP_Text _myScoreText;
    private TMP_Text _opponentNameText;

    private RectTransform _myBoardRoot;
    private RectTransform _opponentMiniBoardRoot;
    private RectTransform _ownedItemRoot;

    private readonly DraggableBlockView[] _slotViews = new DraggableBlockView[3];
    private readonly Button[] _ownedItemButtons = new Button[InventoryCapacity];
    private readonly Image[] _ownedItemSlotImages = new Image[InventoryCapacity];
    private readonly Image[] _ownedItemIcons = new Image[InventoryCapacity];
    private readonly GameObject[] _ownedItemBorders = new GameObject[InventoryCapacity];

    private GameObject _defensePhaseRoot;
    private TMP_Text _warningText;
    private Image _incomingAttackIcon;
    private TMP_Text _defenseQuestionText;
    private TMP_Text _defenseCountdownText;
    private Button _useDefenseButton;
    private Button _skipDefenseButton;

    private readonly BoardCell[,] _myBoardCells = new BoardCell[BoardSize, BoardSize];
    private readonly Image[,] _opponentMiniCells = new Image[BoardSize, BoardSize];
    private readonly bool[,] _myOccupied = new bool[BoardSize, BoardSize];
    private readonly bool[,] _myObstacle = new bool[BoardSize, BoardSize];
    private readonly Color[,] _myColors = new Color[BoardSize, BoardSize];
    private readonly Sprite[,] _myBlockSprites = new Sprite[BoardSize, BoardSize];
    private readonly BattleItemId[,] _boardItems = new BattleItemId[BoardSize, BoardSize];

    private readonly bool[,] _opponentOccupied = new bool[BoardSize, BoardSize];

    private readonly List<BlockShape> _normalShapeLibrary = new List<BlockShape>();
    private readonly List<BlockShape> _curseShapeLibrary = new List<BlockShape>();
    private readonly BlockInstance[] _currentBlocks = new BlockInstance[3];

    private readonly List<OwnedItemData> _ownedItems = new List<OwnedItemData>();
    private int _nextOwnedItemId = 1;
    private int _reservedOutgoingOwnedItemId = -1;

    private BattlePhase _phase = BattlePhase.Play;
    private float _phaseTimer;
    private int _round = 1;
    private int _myScore;

    private int _dragSlotIndex = -1;
    private bool _dragCanPlace;
    private bool _dragHasAnchor;
    private Vector2Int _dragAnchor;
    private RectTransform _dragPreviewRoot;

    private BattleItemId _incomingAttackItem = BattleItemId.None;
    private bool _itemUseBlockedThisRound;
    private int _sealedSlotIndex = -1;
    private bool _forceCurseBlockNextRound;

    private int _pendingResolveScore;

    private bool _localRoundReady;
    private bool _opponentRoundReady;
    private bool _waitingForOpponentRoundReady;

    private BattleNetDriver _battleNetDriver;
    private bool _roundReadySendIssued;
    private bool _roundSyncAdvanceQueued;

    [SerializeField] private bool _networkGameStarted;
    [SerializeField] private bool _isHostPlayer;
    [SerializeField] private int _networkSeed;
    private bool IsRankedBattle => BattleMatchSession.Mode == GameMode.Ranked;

    [Header("Disconnect Runtime")]
    [SerializeField] private bool _disconnectPauseActive;
    [SerializeField] private bool _disconnectInputLocked;

    private bool _forcedDisconnectResultActive;
    private bool _forcedDisconnectLocalLose;
    private string _forcedDisconnectReason = string.Empty;

    public int GetMyScoreForNetwork()
    {
        return _myScore;
    }
    public int GetCurrentPhaseForNetwork()
    {
        return (int)_phase;
    }

    public float GetCurrentPhaseTimerForNetwork()
    {
        return _phaseTimer;
    }

    public void ApplyDisconnectSync(bool paused, int phaseValue, float remainingSeconds)
    {
        if (phaseValue >= 0 && phaseValue <= (int)BattlePhase.Resolve)
            _phase = (BattlePhase)phaseValue;

        _phaseTimer = Mathf.Max(0f, remainingSeconds);

        SetDisconnectPauseState(paused);

        if (_phase == BattlePhase.Defense)
        {
            if (_defensePhaseRoot != null)
                _defensePhaseRoot.SetActive(true);

            RefreshDefenseUI();
        }
        else
        {
            HideDefensePhase();
        }

        RefreshOwnedItemUI();
        RefreshBoardVisual();
        RefreshOpponentMiniBoard();
        RefreshTopHud();
        RefreshRoundEndButtonUI();
    }
    public bool IsBattleActiveForDisconnect()
    {
        if (IsRankedBattle && !_networkGameStarted)
            return false;

        if (_resultPhaseRoot != null && _resultPhaseRoot.activeSelf)
            return false;

        return true;
    }

    public bool CanAcceptUserInput()
    {
        if (_disconnectPauseActive)
            return false;

        if (_disconnectInputLocked)
            return false;

        if (_resultPhaseRoot != null && _resultPhaseRoot.activeSelf)
            return false;

        return true;
    }

    public void SetDisconnectPauseState(bool paused)
    {
        _disconnectPauseActive = paused;
        _disconnectInputLocked = paused;

        if (paused)
            CancelDisconnectDrag();

        SetDisconnectButtonInteractable(!paused);
    }

    public void ForceDisconnectResult(bool localPlayerLose, string reason)
    {
        if (_forcedDisconnectResultActive)
            return;

        _forcedDisconnectResultActive = true;
        _forcedDisconnectLocalLose = localPlayerLose;
        _forcedDisconnectReason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason;

        _disconnectPauseActive = false;
        _disconnectInputLocked = true;

        CancelDisconnectDrag();
        SetDisconnectButtonInteractable(false);

        // 기존 EndBattle() 흐름을 그대로 타기 위해 승패만 1점 보정으로 강제
        if (localPlayerLose)
        {
            if (_myScore >= opponentScore)
                opponentScore = _myScore + 1;
        }
        else
        {
            if (_myScore <= opponentScore)
                _myScore = opponentScore + 1;
        }

        RefreshTopHud();
        EndBattle();
    }

    private void SetDisconnectButtonInteractable(bool canInteract)
    {
        if (roundEndButton != null)
            roundEndButton.interactable = canInteract;

        if (_useDefenseButton != null)
            _useDefenseButton.interactable = canInteract;

        if (_skipDefenseButton != null)
            _skipDefenseButton.interactable = canInteract;

        for (int i = 0; i < _ownedItemButtons.Length; i++)
        {
            if (_ownedItemButtons[i] != null)
                _ownedItemButtons[i].interactable = canInteract;
        }
    }

    private void CancelDisconnectDrag()
    {
        _dragSlotIndex = -1;
        _dragCanPlace = false;
        _dragHasAnchor = false;

        if (_dragPreviewRoot != null)
            _dragPreviewRoot.gameObject.SetActive(false);
    }

    [SerializeField] private TMP_Text resultMmrText;
    private void Awake()
    {
        Transform safeArea = FindSafeArea();
        EnsureRuntimeOwnedItemSlots(safeArea);
        EnsureRuntimeDefenseButtons(safeArea);

        CacheHierarchy();
        BuildShapeLibrary();
        SanitizeLoadout();
        ApplyLoadoutFromSession();
        ApplyMatchSession();
        _battleNetDriver = GetComponent<BattleNetDriver>();
        BuildItemSpriteMap();
        BindButtons();
    }

    private void Start()
    {
        ShowLoadingOverlay(IsRankedBattle ? "상대 연결 중..." : "준비 중...");

        BuildBoards();
        WarmupLineClearFxPool();
        WarmupComboFxPool();
        ResetBattleComboState();

        ClearOpponentBoardSnapshot();
        ResetCurrentRoundBlocks();
        HideDefensePhase();
        HideResultPhase();
        RefreshOwnedItemUI();
        RefreshTopHud();

        _reservedOutgoingOwnedItemId = -1;

        if (roundEndButton != null)
            roundEndButton.gameObject.SetActive(false);

        // 랭크전은 네트워크 GAME_START 수신 전까지 라운드를 시작하지 않음
        if (IsRankedBattle)
        {
            _networkGameStarted = false;
            return;
        }

        StartRound();
    }

    private void Update()
    {
        if (IsRankedBattle && !_networkGameStarted)
            return;

        if (_disconnectPauseActive)
            return;

        _phaseTimer -= Time.deltaTime;

        if (_phaseTimer < 0f)
            _phaseTimer = 0f;

        switch (_phase)
        {
            case BattlePhase.Defense:
                UpdateDefensePhase();
                break;

            case BattlePhase.Play:
                UpdatePlayPhase();
                break;

            case BattlePhase.Resolve:
                UpdateResolvePhase();
                break;
        }

        if (IsRankedBattle && _resultPhaseRoot != null && _resultPhaseRoot.activeSelf)
            RefreshRankedResultMmrUi();
    }

    #region Public Debug

    public void DebugGiveAttackItem()
    {
        AddOwnedItem(ToBattleItemId(selectedAttackLoadout[0]));
    }

    public void DebugGiveDefenseItem()
    {
        AddOwnedItem(BattleItemId.DefenseUniversal);
    }

    public void DebugIncomingObstacleAttack()
    {
        QueueIncomingAttack(BattleItemId.AttackObstacle2, true);
    }

    public void DebugIncomingSealAttack()
    {
        QueueIncomingAttack(BattleItemId.AttackSealRandomSlot, true);
    }

    public void QueueIncomingAttack(BattleItemId attackItemId, bool showImmediatelyIfPossible = false)
    {
        Debug.Log($"[BM] QueueIncomingAttack item={attackItemId} phase={_phase}");

        if (GetItemKind(attackItemId) != ItemKind.Attack)
            return;

        _incomingAttackItem = attackItemId;
        RefreshBoardVisual();
        RefreshDefenseUI();

        if (showImmediatelyIfPossible && _phase == BattlePhase.Play)
        {
            ResetCurrentRoundBlocks();
            ShowDefensePhase();
        }
    }

    #endregion

    #region Setup

    private Transform FindSafeArea()
    {
        if (transform.parent != null && transform.parent.name == "SafeArea")
            return transform.parent;

        GameObject safeAreaGo = GameObject.Find("SafeArea");
        return safeAreaGo != null ? safeAreaGo.transform : null;
    }

    private void EnsureRuntimeOwnedItemSlots(Transform safeArea)
    {
        if (safeArea == null)
            return;

        Transform ownedRoot = safeArea.Find("OwnedItemRoot");
        if (ownedRoot == null)
            return;

        Transform template = ownedRoot.Find("ItemSlot1");
        if (template == null)
            return;

        for (int i = 1; i <= InventoryCapacity; i++)
        {
            Transform existing = ownedRoot.Find($"ItemSlot{i}");
            if (existing != null)
                continue;

            GameObject clone = Instantiate(template.gameObject, ownedRoot);
            clone.name = $"ItemSlot{i}";
            clone.SetActive(true);
        }
    }

    private void EnsureRuntimeDefenseButtons(Transform safeArea)
    {
        if (safeArea == null)
            return;

        Transform panel = safeArea.Find("DefensePhaseRoot/DefenseSelectPanel");
        if (panel == null)
            return;

        Transform useButton = panel.Find("UseDefenseButton");
        Transform skipButton = panel.Find("SkipDefenseButton");

        if (useButton == null)
        {
            if (skipButton != null)
            {
                GameObject clone = Instantiate(skipButton.gameObject, panel);
                clone.name = "UseDefenseButton";
                RectTransform rt = clone.transform as RectTransform;
                RectTransform skipRt = skipButton as RectTransform;
                if (rt != null && skipRt != null)
                    rt.anchoredPosition = skipRt.anchoredPosition + new Vector2(0f, 110f);

                SetButtonLabel(clone.transform, "방어 사용");
            }
            else
            {
                Transform slot1 = panel.Find("DefenseItemSlot1");
                if (slot1 != null)
                    slot1.name = "UseDefenseButton";
            }
        }

        for (int i = 1; i <= 3; i++)
        {
            Transform slot = panel.Find($"DefenseItemSlot{i}");
            if (slot != null)
                slot.gameObject.SetActive(false);
        }
    }

    private void CacheHierarchy()
    {
        Transform safeArea = FindSafeArea();
        if (safeArea == null)
        {
            Debug.LogError("SafeArea 못 찾음");
            return;
        }

        _canvas = safeArea.GetComponentInParent<Canvas>();
        _dragLayer = _canvas != null ? _canvas.transform as RectTransform : safeArea as RectTransform;

        _roundText = FindTMP(safeArea, "TopHudRoot/RoundPanel/RoundText");
        _timerText = FindTMP(safeArea, "TopHudRoot/TimerPanel/TimerText");
        _myScoreText = FindTMP(safeArea, "TopHudRoot/MyScorePanel/MyScoreText");
        _opponentNameText = FindTMP(safeArea, "TopHudRoot/OpponentMiniBoardPanel/OpponentNameText");

        _myBoardRoot = FindRect(safeArea, "BoardRoot/MyBoardRoot");
        _comboFxAnchorRoot = _myBoardRoot;
        _opponentMiniBoardRoot = FindRect(safeArea, "TopHudRoot/OpponentMiniBoardPanel/OpponentMiniBoardRoot");
        _ownedItemRoot = FindRect(safeArea, "OwnedItemRoot");

        for (int i = 0; i < 3; i++)
        {
            Transform slot = safeArea.Find($"CurrentBlocksRoot/BlockSlot{i + 1}");
            if (slot == null)
                continue;

            _slotViews[i] = GetOrAdd<DraggableBlockView>(slot.gameObject);
            _slotViews[i].Setup(this, i);
        }

        for (int i = 0; i < InventoryCapacity; i++)
        {
            Transform slot = safeArea.Find($"OwnedItemRoot/ItemSlot{i + 1}");
            if (slot == null)
                continue;

            _ownedItemButtons[i] = GetOrAdd<Button>(slot.gameObject);
            _ownedItemSlotImages[i] = GetOrAdd<Image>(slot.gameObject);
            _ownedItemIcons[i] = FindOrCreateSlotIconImage(slot);
            Transform border = slot.Find("Border");
            _ownedItemBorders[i] = border != null ? border.gameObject : null;
        }

        _defensePhaseRoot = FindGO(safeArea, "DefensePhaseRoot");

        Transform warningTextTr = safeArea.Find("DefensePhaseRoot/WarningPanel/WarningText");
        if (warningTextTr != null)
        {
            _warningText = warningTextTr.GetComponent<TMP_Text>();
        }

        Transform incomingIconTr = safeArea.Find("DefensePhaseRoot/WarningPanel/IncomingAttackIcon");
        if (incomingIconTr != null)
            _incomingAttackIcon = GetOrAdd<Image>(incomingIconTr.gameObject);

        _defenseQuestionText = FindTMP(safeArea, "DefensePhaseRoot/DefenseSelectPanel/DefenseQuestionText");
        _defenseCountdownText = FindTMPEither(
            safeArea,
            "DefensePhaseRoot/DefenseSelectPanel/DefenseCountownText",
            "DefensePhaseRoot/DefenseSelectPanel/DefenseCountdownText");

        Transform useDefenseTr = safeArea.Find("DefensePhaseRoot/DefenseSelectPanel/UseDefenseButton");
        if (useDefenseTr != null)
            _useDefenseButton = GetOrAdd<Button>(useDefenseTr.gameObject);

        Transform skipDefenseTr = safeArea.Find("DefensePhaseRoot/DefenseSelectPanel/SkipDefenseButton");
        if (skipDefenseTr != null)
            _skipDefenseButton = GetOrAdd<Button>(skipDefenseTr.gameObject);

        if (roundEndButton == null)
            roundEndButton = FindButtonDeep(safeArea, "RoundEndButton");

        _resultPhaseRoot = FindGO(safeArea, "ResultPhaseRoot");
        _victoryEmblem = FindGO(safeArea, "ResultPhaseRoot/VictoryEmblem");
        _drawEmblem = FindGO(safeArea, "ResultPhaseRoot/DrawEmblem");
        _defeatEmblem = FindGO(safeArea, "ResultPhaseRoot/DefeatEmblem");
        _myResultText = FindTMP(safeArea, "ResultPhaseRoot/MyScore/MyResultText");
        _opponentResultText = FindTMP(safeArea, "ResultPhaseRoot/OppScore/OpponentResultText");
        _myNicknameText = FindTMP(safeArea, "ResultPhaseRoot/MyScore/MyNickName");
        _opponentNicknameText = FindTMP(safeArea, "ResultPhaseRoot/OppScore/OppNickName");

        Transform resultLobbyTr = safeArea.Find("ResultPhaseRoot/LobbyButton");
        if (resultLobbyTr != null)
            _resultLobbyButton = GetOrAdd<Button>(resultLobbyTr.gameObject);

    }
    private void BuildItemSpriteMap()
    {
        _itemSpriteMap.Clear();

        for (int i = 0; i < itemSpriteEntries.Count; i++)
        {
            ItemSpriteEntry entry = itemSpriteEntries[i];
            if (entry == null)
                continue;

            if (entry.itemId == BattleItemId.None)
                continue;

            _itemSpriteMap[entry.itemId] = entry;
        }
    }

    private Sprite GetItemSprite(BattleItemId itemId)
    {
        if (_itemSpriteMap.TryGetValue(itemId, out ItemSpriteEntry entry))
            return entry.itemSprite;

        return null;
    }

    private Sprite GetIncomingSprite(BattleItemId itemId)
    {
        if (_itemSpriteMap.TryGetValue(itemId, out ItemSpriteEntry entry))
            return entry.incomingSprite != null ? entry.incomingSprite : entry.itemSprite;

        return null;
    }
    private void BindButtons()
    {
        for (int i = 0; i < _ownedItemButtons.Length; i++)
        {
            if (_ownedItemButtons[i] == null)
                continue;

            int index = i;
            _ownedItemButtons[i].onClick.RemoveAllListeners();
            _ownedItemButtons[i].onClick.AddListener(() => OnClickOwnedItem(index));
        }

        if (_useDefenseButton != null)
        {
            _useDefenseButton.onClick.RemoveAllListeners();
            _useDefenseButton.onClick.AddListener(UseDefenseItemNow);
        }

        if (_skipDefenseButton != null)
        {
            _skipDefenseButton.onClick.RemoveAllListeners();
            _skipDefenseButton.onClick.AddListener(SkipDefenseNow);
        }

        if (debugAddAttackItemButton != null)
        {
            debugAddAttackItemButton.onClick.RemoveAllListeners();
            debugAddAttackItemButton.onClick.AddListener(DebugGiveAttackItem);
        }

        if (debugAddDefenseItemButton != null)
        {
            debugAddDefenseItemButton.onClick.RemoveAllListeners();
            debugAddDefenseItemButton.onClick.AddListener(DebugGiveDefenseItem);
        }

        if (debugIncomingObstacleButton != null)
        {
            debugIncomingObstacleButton.onClick.RemoveAllListeners();
            debugIncomingObstacleButton.onClick.AddListener(DebugIncomingObstacleAttack);
        }

        if (debugIncomingSealButton != null)
        {
            debugIncomingSealButton.onClick.RemoveAllListeners();
            debugIncomingSealButton.onClick.AddListener(DebugIncomingSealAttack);
        }

        if (roundEndButton != null)
        {
            roundEndButton.onClick.RemoveAllListeners();
            roundEndButton.onClick.AddListener(RequestRoundEnd);
        }

        if (_resultLobbyButton != null)
        {
            _resultLobbyButton.onClick.RemoveAllListeners();
            _resultLobbyButton.onClick.AddListener(GoToLobbyFromResult);
        }
    }

    private void BuildBoards()
    {
        BuildMyBoard();
        BuildOpponentMiniBoard();
        RefreshBoardVisual();
        RefreshOpponentMiniBoard();
    }
    private void WarmupLineClearFxPool()
    {
        if (_lineClearFx == null)
        {
            Debug.LogWarning("[BattleManager] _lineClearFx 가 비어있어서 LineClearFxPool warmup 을 건너뜀");
            return;
        }

        if (_lineClearFxPoolRoot == null)
        {
            GameObject root = new GameObject(RuntimePrefix + "BattleLineClearFxPool", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            _lineClearFxPoolRoot = root.transform;
        }

        _lineClearFxPool.Clear();
        _activeLineClearFxInstances.Clear();

        // 원본 템플릿은 절대 이동/비활성화하지 않음
        int targetCount = Mathf.Max(1, _lineClearFxPoolSize);

        for (int i = 0; i < targetCount; i++)
        {
            BattleLineClearFx clone = Instantiate(_lineClearFx, _lineClearFxPoolRoot);
            clone.PrepareForReuse();
            clone.SetReturnToPool(ReturnLineClearFxToPool);
            clone.RebuildCache();
            clone.gameObject.SetActive(false);
            _lineClearFxPool.Enqueue(clone);
        }
    }

    private void WarmupComboFxPool()
    {
        if (_comboFxTemplate == null)
        {
            Debug.LogWarning("[BattleManager] _comboFxTemplate 가 비어있어서 ComboFxPool warmup 을 건너뜀");
            return;
        }

        if (_comboFxPoolRoot == null)
        {
            GameObject root = new GameObject(RuntimePrefix + "BattleComboFxPool", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            _comboFxPoolRoot = root.transform;
        }

        _comboFxPool.Clear();

        int count = Mathf.Max(1, _comboFxPoolSize);
        for (int i = 0; i < count; i++)
        {
            BattleComboFx clone = Instantiate(_comboFxTemplate, _comboFxPoolRoot);
            clone.PrepareForReuse();
            clone.OnFinished = ReturnComboFxToPool;
            clone.gameObject.SetActive(false);
            _comboFxPool.Enqueue(clone);
        }

        _comboFxTemplate.gameObject.SetActive(false);
    }

    private BattleComboFx RentComboFx()
    {
        if (_comboFxTemplate == null)
            return null;

        if (_comboFxPool.Count == 0)
        {
            BattleComboFx clone = Instantiate(_comboFxTemplate, _comboFxPoolRoot != null ? _comboFxPoolRoot : transform);
            clone.PrepareForReuse();
            clone.OnFinished = ReturnComboFxToPool;
            clone.gameObject.SetActive(false);
            _comboFxPool.Enqueue(clone);
        }

        BattleComboFx fx = _comboFxPool.Dequeue();
        if (fx == null)
            return null;

        RectTransform parent = _comboFxAnchorRoot != null ? _comboFxAnchorRoot : _myBoardRoot;
        fx.transform.SetParent(parent, false);

        RectTransform rt = fx.transform as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = _comboFxOffset;
        }

        fx.gameObject.SetActive(true);
        return fx;
    }

    private void ReturnComboFxToPool(BattleComboFx fx)
    {
        if (fx == null)
            return;

        fx.PrepareForReuse();
        fx.transform.SetParent(_comboFxPoolRoot != null ? _comboFxPoolRoot : transform, false);
        fx.gameObject.SetActive(false);
        _comboFxPool.Enqueue(fx);
    }

    private void SpawnBattleComboFx(int combo)
    {
        if (combo < 2)
            return;

        BattleComboFx fx = RentComboFx();
        if (fx == null)
            return;

        fx.Play(
            combo,
            _comboFxFont,
            GetBattleComboFxColor(combo),
            _comboFxDuration,
            _comboFxRiseDistance);
    }

    private Color GetBattleComboFxColor(int combo)
    {
        if (combo >= 6)
            return _comboFxColorHigh;

        if (combo >= 4)
            return _comboFxColorMid;

        return _comboFxColorLow;
    }

    private int GetBattleComboBonus(int combo)
    {
        if (_comboBonusTable == null || _comboBonusTable.Length == 0)
            return 0;

        int idx = Mathf.Clamp(combo, 0, _comboBonusTable.Length - 1);
        return Mathf.Max(0, _comboBonusTable[idx]);
    }

    private void ResetBattleComboState()
    {
        _comboCount = 0;
        _maxComboCount = 0;
    }
    private void BeginBattleComboRound()
    {
        // 첫 라운드가 아니라면, 직전 라운드에서 줄 삭제를 한 번도 못 했을 때만 콤보 리셋
        if (_comboRoundStartedOnce && !_comboHadClearThisRound)
            _comboCount = 0;

        _comboRoundStartedOnce = true;
        _comboHadClearThisRound = false;
    }
   
    private BattleLineClearFx RentLineClearFx()
    {
        if (_lineClearFxPool.Count == 0)
        {
            if (_lineClearFx == null)
                return null;

            BattleLineClearFx clone = Instantiate(_lineClearFx, _lineClearFxPoolRoot != null ? _lineClearFxPoolRoot : transform);
            clone.PrepareForReuse();
            clone.SetReturnToPool(ReturnLineClearFxToPool);
            clone.RebuildCache();
            clone.gameObject.SetActive(false);
            _lineClearFxPool.Enqueue(clone);
        }

        BattleLineClearFx fx = _lineClearFxPool.Dequeue();
        if (fx == null)
            return null;

        fx.transform.SetParent(_lineClearFxPoolRoot != null ? _lineClearFxPoolRoot : transform, false);
        fx.PrepareForReuse();
        fx.RebuildCache();
        fx.gameObject.SetActive(true);

        if (!_activeLineClearFxInstances.Contains(fx))
            _activeLineClearFxInstances.Add(fx);

        return fx;
    }

    private void ReturnLineClearFxToPool(BattleLineClearFx fx)
    {
        if (fx == null)
            return;

        _activeLineClearFxInstances.Remove(fx);

        fx.PrepareForReuse();
        fx.transform.SetParent(_lineClearFxPoolRoot != null ? _lineClearFxPoolRoot : transform, false);
        fx.gameObject.SetActive(false);

        _lineClearFxPool.Enqueue(fx);
    }

    private IEnumerator PlayLineClearFxPooled(List<int> completedRows, List<int> completedCols, Action<int, int> onCellCleared)
    {
        bool hasRows = completedRows != null && completedRows.Count > 0;
        bool hasCols = completedCols != null && completedCols.Count > 0;

        if (!hasRows && !hasCols)
            yield break;

        BattleLineClearFx fx = RentLineClearFx();
        if (fx == null)
        {
            if (hasRows)
            {
                for (int i = 0; i < completedRows.Count; i++)
                {
                    int y = completedRows[i];
                    for (int x = 0; x < BoardSize; x++)
                        onCellCleared?.Invoke(x, y);
                }
            }

            if (hasCols)
            {
                for (int i = 0; i < completedCols.Count; i++)
                {
                    int x = completedCols[i];
                    for (int y = 0; y < BoardSize; y++)
                        onCellCleared?.Invoke(x, y);
                }
            }

            yield break;
        }

        _isResolvingLineClearFx = true;
        yield return fx.Play(completedRows, completedCols, onCellCleared);
        _isResolvingLineClearFx = false;

        fx.ReturnToPool();
    }
    private void BuildMyBoard()
    {
        DestroyRuntimeChildren(_myBoardRoot);

        float stepX = myBoardCellSize.x + myBoardSpacing.x;
        float stepY = myBoardCellSize.y + myBoardSpacing.y;
        float startX = -((BoardSize - 1) * stepX) * 0.5f;
        float startY = ((BoardSize - 1) * stepY) * 0.5f;

        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                GameObject go = CreateCellObject(_myBoardRoot, true);
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = myBoardCellSize;
                rt.anchoredPosition = new Vector2(startX + (x * stepX), startY - (y * stepY));

                BoardCell cell = GetOrAdd<BoardCell>(go);
                _myBoardCells[x, y] = cell;

                _myOccupied[x, y] = false;
                _myObstacle[x, y] = false;
                _myColors[x, y] = Color.clear;
                _boardItems[x, y] = BattleItemId.None;
                _myBlockSprites[x, y] = null;
            }
        }
    }

    private void BuildOpponentMiniBoard()
    {
        DestroyRuntimeChildren(_opponentMiniBoardRoot);

        float stepX = opponentMiniCellSize.x + opponentMiniSpacing.x;
        float stepY = opponentMiniCellSize.y + opponentMiniSpacing.y;
        float startX = -((BoardSize - 1) * stepX) * 0.5f;
        float startY = ((BoardSize - 1) * stepY) * 0.5f;

        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                GameObject go = CreateCellObject(_opponentMiniBoardRoot, false);
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = opponentMiniCellSize;
                rt.anchoredPosition = new Vector2(startX + (x * stepX), startY - (y * stepY));

                Image img = GetOrAdd<Image>(go);
                img.color = BoardBaseColor;
                img.raycastTarget = false;
                _opponentMiniCells[x, y] = img;
            }
        }
    }

    private void BuildShapeLibrary()
    {
        _normalShapeLibrary.Clear();
        _curseShapeLibrary.Clear();

        AddShape(_normalShapeLibrary, BlockShapeId.H3, 90, BlockColor1, false, h3CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0));

        AddShape(_normalShapeLibrary, BlockShapeId.L3, 90, BlockColor2, false, l3CellSprite,
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1));

        AddShape(_normalShapeLibrary, BlockShapeId.H4, 90, BlockColor3, false, h4CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0));

        AddShape(_normalShapeLibrary, BlockShapeId.O4, 60, BlockColor4, false, o4CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1));

        AddShape(_normalShapeLibrary, BlockShapeId.T4, 85, BlockColor5, false, t4CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1));

        AddShape(_normalShapeLibrary, BlockShapeId.L4, 85, BlockColor1, false, l4CellSprite,
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2));

        AddShape(_normalShapeLibrary, BlockShapeId.L4_M, 85, BlockColor2, false, l4MCellSprite,
            new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(0, 2));

        AddShape(_normalShapeLibrary, BlockShapeId.Z4, 75, BlockColor3, false, z4CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1));

        AddShape(_normalShapeLibrary, BlockShapeId.Z4_M, 75, BlockColor4, false, z4MCellSprite,
            new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(0, 1), new Vector2Int(1, 1));

        AddShape(_normalShapeLibrary, BlockShapeId.H5, 55, BlockColor5, false, h5CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0), new Vector2Int(4, 0));

        AddShape(_normalShapeLibrary, BlockShapeId.P5, 70, BlockColor1, false, p5CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(0, 2));

        AddShape(_normalShapeLibrary, BlockShapeId.T5, 70, BlockColor2, false, t5CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1), new Vector2Int(1, 2));

        AddShape(_normalShapeLibrary, BlockShapeId.L5, 65, BlockColor3, false, l5CellSprite,
            new Vector2Int(0, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, 2),
            new Vector2Int(1, 0),
            new Vector2Int(2, 0));

        AddShape(_curseShapeLibrary, BlockShapeId.CurseDiag3, 100, new Color32(170, 90, 220, 255), true, curseDiag3CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(2, 2));

        AddShape(_curseShapeLibrary, BlockShapeId.CurseSplit3, 100, new Color32(170, 90, 220, 255), true, curseSplit3CellSprite,
            new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(1, 2));

        AddShape(_curseShapeLibrary, BlockShapeId.CurseDiag4, 100, new Color32(170, 90, 220, 255), true, curseDiag4CellSprite,
            new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(2, 2), new Vector2Int(3, 3));

        AddShape(_curseShapeLibrary, BlockShapeId.CurseSplit4, 100, new Color32(170, 90, 220, 255), true, curseSplit4CellSprite,
            new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(0, 2), new Vector2Int(2, 2));
    }

    private void AddShape(List<BlockShape> target, BlockShapeId shapeId, int weight, Color color, bool isCurse, Sprite cellSprite, params Vector2Int[] cells)
    {
        target.Add(new BlockShape(shapeId, weight, color, new List<Vector2Int>(cells), isCurse, cellSprite));
    }

    private void SanitizeLoadout()
    {
        if (selectedAttackLoadout == null || selectedAttackLoadout.Length != 3)
        {
            selectedAttackLoadout = new AttackItemId[3]
            {
                AttackItemId.Obstacle2,
                AttackItemId.SealRandomSlot,
                AttackItemId.Bomb3x3
            };
        }

        if (selectedSupportLoadout == null || selectedSupportLoadout.Length != 2)
        {
            selectedSupportLoadout = new SupportItemId[2]
            {
                SupportItemId.RotateRight90,
                SupportItemId.ResetRemaining
            };
        }
    }

    private string GetIncomingAttackWarningText(BattleItemId itemId)
    {
        switch (itemId)
        {
            case BattleItemId.AttackObstacle2:
                return "공격 감지 - 장애물 설치";

            case BattleItemId.AttackSealRandomSlot:
                return "공격 감지 - 슬롯 봉인";

            case BattleItemId.AttackCurseBlock:
                return "공격 감지 - 저주 블록";

            case BattleItemId.AttackDisableItemUse:
                return "공격 감지 - 아이템 사용불가";

            case BattleItemId.AttackDeleteRandomLine:
                return "공격 감지 - 행/열 삭제";

            case BattleItemId.AttackBomb3x3:
                return "공격 감지 - 폭탄";

            default:
                return "공격 감지";
        }
    }

    private void RefreshDefenseUI()
    {
        string warning = GetIncomingAttackWarningText(_incomingAttackItem);

        if (_warningText != null)
            _warningText.text = warning;

        if (_incomingAttackIcon != null)
        {
            Sprite sprite = GetIncomingAttackSprite(_incomingAttackItem);
            _incomingAttackIcon.sprite = sprite;
            _incomingAttackIcon.preserveAspect = true;
            _incomingAttackIcon.raycastTarget = false;
            _incomingAttackIcon.color = Color.white;
            _incomingAttackIcon.enabled = sprite != null;
            _incomingAttackIcon.gameObject.SetActive(true);
        }

        int defenseCount = GetDefenseItemCount();

        if (_defenseQuestionText != null)
            _defenseQuestionText.text = $"방어 아이템 사용? {defenseCount}";

        if (_useDefenseButton != null)
        {
            _useDefenseButton.interactable = true;
            SetButtonLabel(_useDefenseButton.transform, defenseCount > 0 ? $"방어 사용 ({defenseCount})" : "방어 없음");

            Image btnImage = _useDefenseButton.GetComponent<Image>();
            if (btnImage != null)
                btnImage.raycastTarget = defenseCount > 0;
        }

        if (_skipDefenseButton != null)
        {
            _skipDefenseButton.interactable = true;
            SetButtonLabel(_skipDefenseButton.transform, "사용 안 함");

            Image btnImage = _skipDefenseButton.GetComponent<Image>();
            if (btnImage != null)
                btnImage.raycastTarget = true;
        }
    }

    #endregion

    #region Round Flow

    private void StartRound()
    {
        Debug.Log($"[BM] StartRound incoming={_incomingAttackItem}");

        BeginBattleComboRound();

        ResetNetworkRoundSyncState();
        RefreshRoundEndButtonUI();
        HideResultPhase();

        _localRoundReady = false;
        _opponentRoundReady = false;
        _waitingForOpponentRoundReady = false;
        _reservedOutgoingOwnedItemId = -1;

        if (_round > maxRounds)
        {
            EndBattle();
            return;
        }

        _pendingResolveScore = 0;

        // ApplyReservedOutgoingAttack();
        ResetCurrentRoundBlocks();
        RefreshOwnedItemUI();
        RefreshBoardVisual();
        RefreshOpponentMiniBoard();
        RefreshTopHud();

        if (_incomingAttackItem != BattleItemId.None)
        {
            _remotePlannedAttackItem = BattleItemId.None;
            RefreshIncomingAttackTelegraph();
            ShowDefensePhase();
        }
        else
        {
            BeginPlayableRound();
        }
    }

    private void BeginPlayableRound()
    {
        ResetNetworkRoundSyncState();

        GiveThreeBlocks();
        TrySpawnRandomBoardItem();
        DrawAllBlockPreviews();

        RefreshOwnedItemUI();
        RefreshBoardVisual();
        RefreshOpponentMiniBoard();

        EnterPlayPhase();
        RefreshRoundEndButtonUI();

        HideLoadingOverlayNextFrame();
    }

    private void AdvanceToNextRound()
    {
        ResetNetworkRoundSyncState();
        ExpireRoundStatuses();
        _round++;
        StartRound();
    }

    private void ExpireRoundStatuses()
    {
        ResetNetworkRoundSyncState();
        _itemUseBlockedThisRound = false;
        _sealedSlotIndex = -1;
        _forceCurseBlockNextRound = false;
        RefreshSlotVisual();
        RefreshOwnedItemUI();
    }

    private void EnterPlayPhase()
    {
        _phase = BattlePhase.Play;
        _phaseTimer = playPhaseSeconds;
        HideDefensePhase();
        RefreshTopHud();
        RefreshRoundEndButtonUI();
    }

    private void EnterResolvePhase()
    {
        ResetNetworkRoundSyncState();
        _phase = BattlePhase.Resolve;
        _phaseTimer = resolvePhaseSeconds;
        HideDefensePhase();
        RefreshTopHud();
    }

    private void ShowDefensePhase()
    {
        _phase = BattlePhase.Defense;
        _phaseTimer = defensePhaseSeconds;

        if (_defensePhaseRoot != null)
            _defensePhaseRoot.SetActive(true);

        RefreshDefenseUI();
        RefreshTopHud();

        HideLoadingOverlayNextFrame();
    }

    private void HideDefensePhase()
    {
        if (_defensePhaseRoot != null)
            _defensePhaseRoot.SetActive(false);
    }

    private void UpdateDefensePhase()
    {
        if (_timerText != null)
            _timerText.text = $"방어{Mathf.CeilToInt(_phaseTimer)}";

        if (_defenseCountdownText != null)
            _defenseCountdownText.text = $"{Mathf.CeilToInt(_phaseTimer)}";

        RefreshDefenseUI();

        if (_phaseTimer <= 0f)
            SkipDefenseNow();
    }

    private void UpdatePlayPhase()
    {
        if (_timerText != null)
            _timerText.text = $"{Mathf.Max(0, Mathf.CeilToInt(_phaseTimer))}";

        RefreshRoundEndButtonUI();

        if (_waitingForOpponentRoundReady || _roundSyncAdvanceQueued)
            return;

        if (_phaseTimer <= 0f || AllBlocksUsed())
        {
            RequestRoundEnd();
            return;
        }
    }

    private void UpdateResolvePhase()
    {
        if (_timerText != null)
            _timerText.text = "정산";

        if (_phaseTimer <= 0f)
            AdvanceToNextRound();
    }

    private void EndBattle()
    {
        SubmitNetworkMatchResultIfNeeded();

        _phase = BattlePhase.Resolve;
        _phaseTimer = 9999f;

        HideDefensePhase();

        if (roundEndButton != null)
            roundEndButton.gameObject.SetActive(false);

        ShowResultPhase();
    }

    #endregion

    #region Defense

    private void RefreshDefenseButtonUI()
    {
        int defenseCount = GetDefenseItemCount();

        if (_defenseQuestionText != null)
            _defenseQuestionText.text = $"방어 아이템 사용?\n보유: {defenseCount}";

        if (_incomingAttackIcon != null)
        {
            Sprite sprite = GetIncomingAttackSprite(_incomingAttackItem);
            _incomingAttackIcon.sprite = sprite;
            _incomingAttackIcon.color = Color.white;
            _incomingAttackIcon.preserveAspect = true;
            _incomingAttackIcon.enabled = sprite != null;
        }

        if (_useDefenseButton != null)
        {
            _useDefenseButton.interactable = defenseCount > 0;
            SetButtonLabel(_useDefenseButton.transform, defenseCount > 0 ? $"방어 사용 ({defenseCount})" : "방어 없음");
        }

        if (_skipDefenseButton != null)
            SetButtonLabel(_skipDefenseButton.transform, "사용 안 함");
    }

    private void UseDefenseItemNow()
    {
        if (!CanAcceptUserInput())
            return;

        if (_phase != BattlePhase.Defense)
            return;

        if (_isResolvingIncomingAttackFx)
            return;

        if (_isResolvingDefenseFx)
            return;

        if (_incomingAttackItem == BattleItemId.None)
        {
            HideDefensePhase();
            BeginPlayableRound();
            return;
        }

        int defenseIndex = FindFirstDefenseOwnedItemIndex();
        if (defenseIndex < 0)
        {
            SkipDefenseNow();
            return;
        }

        StartCoroutine(CoUseDefenseItemNow(defenseIndex));
    }

    private IEnumerator CoUseDefenseItemNow(int defenseIndex)
    {
        _isResolvingDefenseFx = true;

        _ownedItems.RemoveAt(defenseIndex);

        // 상대 화면의 미니보드 방어 FX는 기존대로 유지
        if (IsRankedBattle && _battleNetDriver != null)
            _battleNetDriver.SendAttackOutcome(true);

        _incomingAttackItem = BattleItemId.None;

        RefreshOwnedItemUI();
        RefreshDefenseUI();
        RefreshBoardVisual();
        RefreshOpponentMiniBoard();

        HideDefensePhase();

        if (_myBoardDefenseFx != null)
            yield return StartCoroutine(_myBoardDefenseFx.PlayRoutine());
        else
            yield return new WaitForSeconds(0.2f);

        RefreshOwnedItemUI();
        RefreshDefenseUI();
        RefreshBoardVisual();
        RefreshOpponentMiniBoard();
        RefreshTopHud();

        _isResolvingDefenseFx = false;

        BeginPlayableRound();
    }

    private void SkipDefenseNow()
    {
        if (!CanAcceptUserInput())
            return;

        if (_isResolvingDefenseFx)
            return;

        if (_isResolvingIncomingAttackFx)
            return;

        if (_phase != BattlePhase.Defense)
            return;

        BattleItemId pendingAttack = _incomingAttackItem;
        _incomingAttackItem = BattleItemId.None;

        if (pendingAttack == BattleItemId.None)
        {
            HideDefensePhase();
            BeginPlayableRound();
            return;
        }

        StartCoroutine(CoResolveIncomingAttackAfterDefenseFailed(pendingAttack));
    }
    private IEnumerator CoResolveIncomingAttackAfterDefenseFailed(BattleItemId pendingAttack)
    {
        _isResolvingIncomingAttackFx = true;

        if (IsRankedBattle && _battleNetDriver != null)
            _battleNetDriver.SendAttackOutcome(false);

        HideDefensePhase();
        RefreshDefenseUI();
        RefreshTopHud();

        bool applied = false;

        if (_incomingAttackFx != null)
        {
            yield return StartCoroutine(_incomingAttackFx.PlayRoutine(
                pendingAttack,
                () =>
                {
                    if (applied)
                        return;

                    applied = true;
                    ApplyIncomingAttackToMyBoard(pendingAttack);
                }));
        }
        else
        {
            ApplyIncomingAttackToMyBoard(pendingAttack);
            applied = true;
            yield return new WaitForSeconds(0.18f);
        }

        if (!applied)
            ApplyIncomingAttackToMyBoard(pendingAttack);

        RefreshOwnedItemUI();
        RefreshDefenseUI();
        RefreshBoardVisual();
        RefreshOpponentMiniBoard();
        RefreshTopHud();

        _isResolvingIncomingAttackFx = false;

        BeginPlayableRound();
    }

    #endregion

    #region Blocks

    private void ResetCurrentRoundBlocks()
    {
        for (int i = 0; i < _currentBlocks.Length; i++)
            _currentBlocks[i] = null;

        DrawAllBlockPreviews();
        RefreshSlotVisual();
    }

    private void GiveThreeBlocks()
    {
        int curseIndex = _forceCurseBlockNextRound ? UnityEngine.Random.Range(0, 3) : -1;

        for (int i = 0; i < _currentBlocks.Length; i++)
        {
            if (_forceCurseBlockNextRound && i == curseIndex)
                _currentBlocks[i] = CreateRandomBlock(true);
            else
                _currentBlocks[i] = CreateRandomBlock(false);
        }

        _forceCurseBlockNextRound = false;

        RefreshRoundEndButtonUI();
    }

    private BlockInstance CreateRandomBlock(bool forceCurse)
    {
        List<BlockShape> pool = forceCurse ? _curseShapeLibrary : _normalShapeLibrary;
        BlockShape shape = GetWeightedRandomShape(pool);
        int rotation = UnityEngine.Random.Range(0, 4) * 90;
        List<Vector2Int> rotated = RotateCells(shape.baseCells, rotation);
        NormalizeCells(rotated);

        return new BlockInstance
        {
            shapeId = shape.shapeId,
            rotation = rotation,
            color = shape.color,
            cells = rotated,
            isCurse = shape.isCurse,
            cellSprite = shape.cellSprite
        };
    }

    private BlockShape GetWeightedRandomShape(List<BlockShape> pool)
    {
        int total = 0;
        for (int i = 0; i < pool.Count; i++)
            total += pool[i].weight;

        int roll = UnityEngine.Random.Range(0, total);
        int sum = 0;

        for (int i = 0; i < pool.Count; i++)
        {
            sum += pool[i].weight;
            if (roll < sum)
                return pool[i];
        }

        return pool[pool.Count - 1];
    }

    public void OnBeginDragSlot(int slotIndex, PointerEventData eventData)
    {

        if (!CanAcceptUserInput())
            return;

        if (_isResolvingLineClearFx)
            return;

        if (IsRankedBattle && !_networkGameStarted)
            return;

        if (_localRoundReady)
            return;

        if (_phase != BattlePhase.Play)
            return;

        if (slotIndex < 0 || slotIndex >= _currentBlocks.Length)
            return;

        if (slotIndex == _sealedSlotIndex)
            return;

        if (_currentBlocks[slotIndex] == null)
            return;

        _dragSlotIndex = slotIndex;
        _dragCanPlace = false;
        _dragHasAnchor = false;
        CreateDragPreview(_currentBlocks[slotIndex]);
        UpdateDrag(eventData);
        RefreshBoardVisual();
        RefreshSlotVisual();
    }

    public void OnDragSlot(int slotIndex, PointerEventData eventData)
    {
        if (!CanAcceptUserInput())
            return;

        if (_isResolvingLineClearFx)
            return;

        if (IsRankedBattle && !_networkGameStarted)
            return;

        if (_dragSlotIndex != slotIndex)
            return;

        UpdateDrag(eventData);
        RefreshBoardVisual();
    }

    public void OnEndDragSlot(int slotIndex, PointerEventData eventData)
    {
        if (!CanAcceptUserInput())
            return;

        if (IsRankedBattle && !_networkGameStarted)
            return;

        if (_dragSlotIndex != slotIndex)
            return;

        if (_dragHasAnchor && _dragCanPlace)
            PlaceBlock(slotIndex, _dragAnchor);

        _dragSlotIndex = -1;
        _dragCanPlace = false;
        _dragHasAnchor = false;
        DestroyDragPreview();
        RefreshBoardVisual();
        RefreshSlotVisual();
    }

    private void UpdateDrag(PointerEventData eventData)
    {
        if (_isResolvingLineClearFx)
            return;

        if (_dragPreviewRoot == null)
            return;

        Vector2 adjustedScreenPos = eventData.position + dragOffset;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragLayer, adjustedScreenPos, eventData.pressEventCamera, out Vector2 local))
            _dragPreviewRoot.anchoredPosition = local;

        BlockInstance block = _currentBlocks[_dragSlotIndex];
        _dragHasAnchor = TryGetBoardAnchor(adjustedScreenPos, eventData.pressEventCamera, block, out _dragAnchor);
        _dragCanPlace = _dragHasAnchor && CanPlaceBlock(block, _dragAnchor.x, _dragAnchor.y);
    }

    private bool TryGetBoardAnchor(Vector2 screenPos, Camera cam, BlockInstance block, out Vector2Int anchor)
    {
        anchor = Vector2Int.zero;

        if (_myBoardRoot == null)
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_myBoardRoot, screenPos, cam, out Vector2 local))
            return false;

        float stepX = myBoardCellSize.x + myBoardSpacing.x;
        float stepY = myBoardCellSize.y + myBoardSpacing.y;
        float startX = -((BoardSize - 1) * stepX) * 0.5f;
        float startY = ((BoardSize - 1) * stepY) * 0.5f;

        int width = GetBlockWidth(block);
        int height = GetBlockHeight(block);

        float topLeftCenterX = local.x - (((width - 1) * stepX) * 0.5f);
        float topLeftCenterY = local.y + (((height - 1) * stepY) * 0.5f);

        int x = Mathf.RoundToInt((topLeftCenterX - startX) / stepX);
        int y = Mathf.RoundToInt((startY - topLeftCenterY) / stepY);

        anchor = new Vector2Int(x, y);
        return true;
    }

    private bool CanPlaceBlock(BlockInstance block, int anchorX, int anchorY)
    {
        for (int i = 0; i < block.cells.Count; i++)
        {
            int x = anchorX + block.cells[i].x;
            int y = anchorY + block.cells[i].y;

            if (x < 0 || x >= BoardSize || y < 0 || y >= BoardSize)
                return false;

            if (_myOccupied[x, y])
                return false;

            if (_myObstacle[x, y])
                return false;
        }

        return true;
    }

    private void PlaceBlock(int slotIndex, Vector2Int anchor)
    {
        if (_isResolvingLineClearFx)
            return;

        StartCoroutine(CoPlaceBlockAndResolveLines(slotIndex, anchor));
    }

    private IEnumerator CoPlaceBlockAndResolveLines(int slotIndex, Vector2Int anchor)
    {
        BlockInstance block = _currentBlocks[slotIndex];
        if (block == null)
            yield break;

        if (!CanPlaceBlock(block, anchor.x, anchor.y))
            yield break;

        for (int i = 0; i < block.cells.Count; i++)
        {
            int x = anchor.x + block.cells[i].x;
            int y = anchor.y + block.cells[i].y;

            _myOccupied[x, y] = true;
            _myColors[x, y] = block.color;
            _myBlockSprites[x, y] = block.cellSprite;
        }

        _currentBlocks[slotIndex] = null;

        DrawBlockPreview(slotIndex);
        RefreshBoardVisual();
        RefreshSlotVisual();
        RefreshTopHud();

        List<int> completedRows = new List<int>();
        List<int> completedCols = new List<int>();
        CollectCompletedLinesForFx(completedRows, completedCols);

        bool hadClearThisPlacement = completedRows.Count > 0 || completedCols.Count > 0;

        if (hadClearThisPlacement)
        {
            if (_lineClearFx != null)
            {
                yield return StartCoroutine(PlayLineClearFxPooled(
                    completedRows,
                    completedCols,
                    (x, y) =>
                    {
                        ClearSingleCell(x, y, true);
                        RefreshBoardVisual();
                    }));
            }
            else
            {
                ClearCompletedLinesImmediateForFx(completedRows, completedCols);
            }

            int clearCount = completedRows.Count + completedCols.Count;
            int baseScore = GetScore(clearCount);

            // 같은 배치에서 1줄이든 3줄이든 "줄 삭제 성공 1회"로 취급
            _comboCount += 1;
            _comboHadClearThisRound = true;
            _maxComboCount = Mathf.Max(_maxComboCount, _comboCount);

            int comboBonus = GetBattleComboBonus(_comboCount);
            int gainedScore = baseScore + comboBonus;

            _myScore += gainedScore;
            _pendingResolveScore += gainedScore;

            if (_comboCount >= 2)
                SpawnBattleComboFx(_comboCount);
        }

        RefreshOwnedItemUI();
        RefreshBoardVisual();
        RefreshSlotVisual();
        RefreshTopHud();

        if (IsRankedBattle && _battleNetDriver != null)
        {
            _battleNetDriver.TrySendBoardSnapshot();
            _battleNetDriver.TrySendScoreSync(GetMyScoreForNetwork());
        }

        if (completedRows.Count > 0 || completedCols.Count > 0)
        {
            if (_lineClearFx != null)
            {
                yield return StartCoroutine(PlayLineClearFxPooled(
                    completedRows,
                    completedCols,
                    (x, y) =>
                    {
                        ClearSingleCell(x, y, true);
                        RefreshBoardVisual();
                    }));
            }
            else
            {
                ClearCompletedLinesImmediateForFx(completedRows, completedCols);
            }

            int clearCount = completedRows.Count + completedCols.Count;
            int gainedScore = GetScore(clearCount);

            _myScore += gainedScore;
            _pendingResolveScore += gainedScore;
        }

        RefreshOwnedItemUI();
        RefreshBoardVisual();
        RefreshSlotVisual();
        RefreshTopHud();

        if (IsRankedBattle && _battleNetDriver != null)
        {
            _battleNetDriver.TrySendBoardSnapshot();
            _battleNetDriver.TrySendScoreSync(GetMyScoreForNetwork());
        }
    }
    private void CollectCompletedLinesForFx(List<int> completedRows, List<int> completedCols)
    {
        completedRows.Clear();
        completedCols.Clear();

        for (int y = 0; y < BoardSize; y++)
        {
            bool full = true;

            for (int x = 0; x < BoardSize; x++)
            {
                bool filled = _myOccupied[x, y] || _myObstacle[x, y];

                if (!filled)
                {
                    full = false;
                    break;
                }
            }

            if (full)
                completedRows.Add(y);
        }

        for (int x = 0; x < BoardSize; x++)
        {
            bool full = true;

            for (int y = 0; y < BoardSize; y++)
            {
                bool filled = _myOccupied[x, y] || _myObstacle[x, y];

                if (!filled)
                {
                    full = false;
                    break;
                }
            }

            if (full)
                completedCols.Add(x);
        }
    }

    private void ClearCompletedLinesImmediateForFx(List<int> completedRows, List<int> completedCols)
    {
        HashSet<Vector2Int> cells = new HashSet<Vector2Int>();

        for (int i = 0; i < completedRows.Count; i++)
        {
            int y = completedRows[i];
            for (int x = 0; x < BoardSize; x++)
                cells.Add(new Vector2Int(x, y));
        }

        for (int i = 0; i < completedCols.Count; i++)
        {
            int x = completedCols[i];
            for (int y = 0; y < BoardSize; y++)
                cells.Add(new Vector2Int(x, y));
        }

        foreach (Vector2Int cell in cells)
            ClearSingleCell(cell.x, cell.y, true);
    }
    private int ClearCompletedLinesAndCollectItems()
    {
        bool[] rowFull = new bool[BoardSize];
        bool[] colFull = new bool[BoardSize];
        int clearCount = 0;

        for (int y = 0; y < BoardSize; y++)
        {
            bool full = true;
            for (int x = 0; x < BoardSize; x++)
            {
                if (!_myOccupied[x, y])
                {
                    full = false;
                    break;
                }
            }

            if (full)
            {
                rowFull[y] = true;
                clearCount++;
            }
        }

        for (int x = 0; x < BoardSize; x++)
        {
            bool full = true;
            for (int y = 0; y < BoardSize; y++)
            {
                if (!_myOccupied[x, y])
                {
                    full = false;
                    break;
                }
            }

            if (full)
            {
                colFull[x] = true;
                clearCount++;
            }
        }

        if (clearCount <= 0)
            return 0;

        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                if (!rowFull[y] && !colFull[x])
                    continue;

                ClearSingleCell(x, y, true);
            }
        }

        return clearCount;
    }

    private void ClearSingleCell(int x, int y, bool rewardPickup)
    {
        if (_boardItems[x, y] != BattleItemId.None)
        {
            BattleItemId itemId = _boardItems[x, y];
            _boardItems[x, y] = BattleItemId.None;

            if (rewardPickup)
                CollectBoardItem(x, y, itemId);
        }

        _myOccupied[x, y] = false;
        _myObstacle[x, y] = false;
        _myColors[x, y] = Color.clear;
        _myBlockSprites[x, y] = null;
    }

    private int GetScore(int clearCount)
    {
        switch (clearCount)
        {
            case 1: return 100;
            case 2: return 250;
            case 3: return 450;
            default: return clearCount >= 4 ? 700 : 0;
        }
    }

    private bool AllBlocksUsed()
    {
        for (int i = 0; i < _currentBlocks.Length; i++)
        {
            if (_currentBlocks[i] != null)
                return false;
        }

        return true;
    }

    #endregion

    #region Inventory / Items

    private void OnClickOwnedItem(int slotIndex)
    {
        if (!CanAcceptUserInput())
            return;

        if (_isResolvingLineClearFx)
            return;

        if (_localRoundReady)
            return;

        if (_phase != BattlePhase.Play)
            return;

        if (slotIndex < 0 || slotIndex >= _ownedItems.Count)
            return;

        OwnedItemData owned = _ownedItems[slotIndex];
        ItemKind kind = GetItemKind(owned.itemId);

        if ((kind == ItemKind.Attack || kind == ItemKind.Support) && _itemUseBlockedThisRound)
            return;

        switch (kind)
        {
            case ItemKind.Attack:
                ToggleReserveAttack(owned.ownedId, owned.itemId);
                break;

            case ItemKind.Support:
                UseSupportItemNow(owned.ownedId, owned.itemId);
                break;
        }
    }

    private void ToggleReserveAttack(int ownedId, BattleItemId itemId)
    {
        Debug.Log($"[BM] ToggleReserveAttack ownedId={ownedId} itemId={itemId} reservedBefore={_reservedOutgoingOwnedItemId}");

        if (_reservedOutgoingOwnedItemId == ownedId)
        {
            _reservedOutgoingOwnedItemId = -1;

            if (IsRankedBattle && _battleNetDriver != null)
                _battleNetDriver.SendAttackIntent(BattleItemId.None, false);

            RefreshOwnedItemUI();
            return;
        }

        _reservedOutgoingOwnedItemId = ownedId;

        if (IsRankedBattle && _battleNetDriver != null)
            _battleNetDriver.SendAttackIntent(itemId, true);

        RefreshOwnedItemUI();
    }

    private void UseSupportItemNow(int ownedId, BattleItemId itemId)
    {
        if (!RemoveOwnedItemByOwnedId(ownedId))
            return;

        ApplySupportItemEffect(itemId);
        RefreshOwnedItemUI();
        RefreshBoardVisual();
        DrawAllBlockPreviews();
        RefreshSlotVisual();
        RefreshRoundEndButtonUI();
    }

    private int AddOwnedItem(BattleItemId itemId)
    {
        if (_ownedItems.Count >= InventoryCapacity)
        {
            if (_ownedItems[0].ownedId == _reservedOutgoingOwnedItemId)
                _reservedOutgoingOwnedItemId = -1;

            _ownedItems.RemoveAt(0);
        }

        OwnedItemData owned = new OwnedItemData
        {
            ownedId = _nextOwnedItemId++,
            itemId = itemId
        };

        _ownedItems.Add(owned);
        RefreshOwnedItemUI();
        return owned.ownedId;
    }

    private bool RemoveOwnedItemByOwnedId(int ownedId)
    {
        int index = FindOwnedItemIndexByOwnedId(ownedId);
        if (index < 0)
            return false;

        if (_ownedItems[index].ownedId == _reservedOutgoingOwnedItemId)
            _reservedOutgoingOwnedItemId = -1;

        _ownedItems.RemoveAt(index);
        RefreshOwnedItemUI();
        return true;
    }

    private int FindOwnedItemIndexByOwnedId(int ownedId)
    {
        for (int i = 0; i < _ownedItems.Count; i++)
        {
            if (_ownedItems[i].ownedId == ownedId)
                return i;
        }

        return -1;
    }

    private int FindOwnedSlotIndexByOwnedId(int ownedId)
    {
        for (int i = 0; i < _ownedItems.Count; i++)
        {
            if (_ownedItems[i].ownedId == ownedId)
                return i;
        }

        return -1;
    }

    private int FindFirstOwnedItemIdByType(BattleItemId itemId)
    {
        for (int i = 0; i < _ownedItems.Count; i++)
        {
            if (_ownedItems[i].itemId == itemId)
                return _ownedItems[i].ownedId;
        }

        return -1;
    }

    private int GetDefenseItemCount()
    {
        int count = 0;

        for (int i = 0; i < _ownedItems.Count; i++)
        {
            if (_ownedItems[i].itemId == BattleItemId.DefenseUniversal)
                count++;
        }

        return count;
    }

    private void RefreshOwnedItemUI()
    {
        for (int i = 0; i < InventoryCapacity; i++)
        {
            bool hasItem = i < _ownedItems.Count;

            if (_ownedItemSlotImages[i] != null)
            {
                if (hasItem)
                    _ownedItemSlotImages[i].color = GetSlotColor(_ownedItems[i].itemId);
                else
                    _ownedItemSlotImages[i].color = Color.white;
            }

            if (_ownedItemIcons[i] != null)
            {
                _ownedItemIcons[i].enabled = hasItem;
                _ownedItemIcons[i].sprite = hasItem ? GetGenericItemSprite(_ownedItems[i].itemId) : null;
                _ownedItemIcons[i].color = Color.white;
                _ownedItemIcons[i].preserveAspect = true;
            }

            if (_ownedItemBorders[i] != null)
            {
                bool reserved = hasItem && _ownedItems[i].ownedId == _reservedOutgoingOwnedItemId;
                _ownedItemBorders[i].SetActive(reserved);
            }

            if (_ownedItemButtons[i] != null)
            {
                bool canClick = hasItem;

                if (hasItem)
                {
                    ItemKind kind = GetItemKind(_ownedItems[i].itemId);

                    // 방어템은 인벤토리에서 직접 클릭 안 함
                    if (kind == ItemKind.Defense)
                        canClick = false;

                    // 아이템 사용불가 상태면 공격/지원 클릭 막음
                    if ((kind == ItemKind.Attack || kind == ItemKind.Support) && _itemUseBlockedThisRound)
                        canClick = false;
                }

                // 버튼 알파 안 죽게 interactable은 항상 true 유지
                _ownedItemButtons[i].interactable = true;

                // 실제 클릭 가능 여부는 슬롯 이미지 raycastTarget으로 제어
                if (_ownedItemSlotImages[i] != null)
                    _ownedItemSlotImages[i].raycastTarget = canClick;
            }
        }
    }

    #endregion

    #region Item Effects

    private void ApplyReservedOutgoingAttack()
    {
        if (_reservedOutgoingOwnedItemId < 0)
            return;

        int foundIndex = -1;
        OwnedItemData foundData = null;

        for (int i = 0; i < _ownedItems.Count; i++)
        {
            if (_ownedItems[i].ownedId == _reservedOutgoingOwnedItemId)
            {
                foundIndex = i;
                foundData = _ownedItems[i];
                break;
            }
        }

        if (foundData == null)
        {
            _reservedOutgoingOwnedItemId = -1;

            if (IsRankedBattle && _battleNetDriver != null)
                _battleNetDriver.SendAttackIntent(BattleItemId.None, false);

            RefreshOwnedItemUI();
            return;
        }

        if (GetItemKind(foundData.itemId) != ItemKind.Attack)
        {
            _reservedOutgoingOwnedItemId = -1;

            if (IsRankedBattle && _battleNetDriver != null)
                _battleNetDriver.SendAttackIntent(BattleItemId.None, false);

            RefreshOwnedItemUI();
            return;
        }

        bool success = false;

        if (IsRankedBattle)
        {
            success = (_battleNetDriver != null) && _battleNetDriver.SendItemUseAttack(foundData.itemId);
        }
        else
        {
            success = true;

            if (loopbackReservedAttackForDebug)
                QueueIncomingAttack(foundData.itemId, false);
        }

        if (!success)
            return;

        // 실제 공격이 발사됐으므로 상대의 "공격 예정" 표시는 꺼준다
        if (IsRankedBattle && _battleNetDriver != null)
            _battleNetDriver.SendAttackIntent(BattleItemId.None, false);

        _ownedItems.RemoveAt(foundIndex);
        _reservedOutgoingOwnedItemId = -1;

        RefreshOwnedItemUI();
    }

    private void ApplyIncomingAttackEffect(BattleItemId attackItem)
    {
        switch (attackItem)
        {
            case BattleItemId.AttackObstacle2:
                SpawnObstacleRandomly();
                SpawnObstacleRandomly();
                break;

            case BattleItemId.AttackSealRandomSlot:
                _sealedSlotIndex = UnityEngine.Random.Range(0, 3);
                break;

            case BattleItemId.AttackCurseBlock:
                _forceCurseBlockNextRound = true;
                break;

            case BattleItemId.AttackDisableItemUse:
                _itemUseBlockedThisRound = true;
                break;

            case BattleItemId.AttackDeleteRandomLine:
                DeleteRandomLineWithoutReward();
                break;

            case BattleItemId.AttackBomb3x3:
                BombRandom3x3WithoutReward();
                break;
        }
    }

    private void ApplySupportItemEffect(BattleItemId itemId)
    {
        switch (itemId)
        {
            case BattleItemId.SupportRotateRight90:
                TransformRemainingBlocks(TransformMode.RotateRight90);
                break;

            case BattleItemId.SupportRotateLeft90:
                TransformRemainingBlocks(TransformMode.RotateLeft90);
                break;

            case BattleItemId.SupportMirrorHorizontal:
                TransformRemainingBlocks(TransformMode.MirrorHorizontal);
                break;

            case BattleItemId.SupportMirrorVertical:
                TransformRemainingBlocks(TransformMode.MirrorVertical);
                break;

            case BattleItemId.SupportResetRemaining:
                ResetRemainingBlocks();
                break;
        }
    }

    private enum TransformMode
    {
        RotateRight90,
        RotateLeft90,
        MirrorHorizontal,
        MirrorVertical
    }

    private void TransformRemainingBlocks(TransformMode mode)
    {
        for (int i = 0; i < _currentBlocks.Length; i++)
        {
            if (_currentBlocks[i] == null)
                continue;

            if (i == _sealedSlotIndex)
                continue;

            List<Vector2Int> transformed = new List<Vector2Int>(_currentBlocks[i].cells.Count);

            for (int c = 0; c < _currentBlocks[i].cells.Count; c++)
            {
                Vector2Int p = _currentBlocks[i].cells[c];

                switch (mode)
                {
                    // Unity UI/grid 기준(y 아래 증가)에서 "오른쪽 90도"
                    case TransformMode.RotateRight90:
                        transformed.Add(new Vector2Int(-p.y, p.x));
                        break;

                    // Unity UI/grid 기준(y 아래 증가)에서 "왼쪽 90도"
                    case TransformMode.RotateLeft90:
                        transformed.Add(new Vector2Int(p.y, -p.x));
                        break;

                    case TransformMode.MirrorHorizontal:
                        transformed.Add(new Vector2Int(-p.x, p.y));
                        break;

                    case TransformMode.MirrorVertical:
                        transformed.Add(new Vector2Int(p.x, -p.y));
                        break;
                }
            }

            NormalizeCells(transformed);
            _currentBlocks[i].cells = transformed;

            // rotation 값도 같이 맞춰두기
            switch (mode)
            {
                case TransformMode.RotateRight90:
                    _currentBlocks[i].rotation = (_currentBlocks[i].rotation + 90) % 360;
                    break;

                case TransformMode.RotateLeft90:
                    _currentBlocks[i].rotation = (_currentBlocks[i].rotation + 270) % 360;
                    break;
            }
        }
    }

    private void ResetRemainingBlocks()
    {
        for (int i = 0; i < _currentBlocks.Length; i++)
        {
            if (_currentBlocks[i] == null)
                continue;

            if (i == _sealedSlotIndex)
                continue;

            _currentBlocks[i] = CreateRandomBlock(false);
        }
    }

    private void SpawnObstacleRandomly()
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                if (_myOccupied[x, y])
                    continue;

                if (_boardItems[x, y] != BattleItemId.None)
                    continue;

                candidates.Add(new Vector2Int(x, y));
            }
        }

        if (candidates.Count <= 0)
            return;

        Vector2Int pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        _myOccupied[pick.x, pick.y] = true;
        _myObstacle[pick.x, pick.y] = true;
        _myColors[pick.x, pick.y] = ObstacleColor;
    }

    private void DeleteRandomLineWithoutReward()
    {
        bool isRow = UnityEngine.Random.value < 0.5f;
        int index = UnityEngine.Random.Range(0, BoardSize);

        if (isRow)
        {
            for (int x = 0; x < BoardSize; x++)
                ClearSingleCell(x, index, false);
        }
        else
        {
            for (int y = 0; y < BoardSize; y++)
                ClearSingleCell(index, y, false);
        }
    }

    private void BombRandom3x3WithoutReward()
    {
        int centerX = UnityEngine.Random.Range(0, BoardSize);
        int centerY = UnityEngine.Random.Range(0, BoardSize);

        for (int y = centerY - 1; y <= centerY + 1; y++)
        {
            for (int x = centerX - 1; x <= centerX + 1; x++)
            {
                if (x < 0 || x >= BoardSize || y < 0 || y >= BoardSize)
                    continue;

                ClearSingleCell(x, y, false);
            }
        }
    }

    #endregion

    #region Board Item Spawn / Collect

    private void TrySpawnRandomBoardItem()
    {
        if (CountBoardItems() >= maxBoardItemCount)
            return;

        if (UnityEngine.Random.value > boardItemSpawnChancePerRound)
            return;

        BattleItemId drop = RollBoardDropItem();
        if (drop == BattleItemId.None)
            return;

        SpawnBoardItemRandomly(drop);
    }

    private int CountBoardItems()
    {
        int count = 0;

        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                if (_boardItems[x, y] != BattleItemId.None)
                    count++;
            }
        }

        return count;
    }

    private BattleItemId RollBoardDropItem()
    {
        float total = attackDropWeight + supportDropWeight + defenseDropWeight;
        if (total <= 0.0001f)
            return BattleItemId.None;

        float roll = UnityEngine.Random.value * total;

        if (roll < attackDropWeight)
            return RollAttackDropFromLoadout();

        roll -= attackDropWeight;
        if (roll < supportDropWeight)
            return RollSupportDropFromLoadout();

        return BattleItemId.DefenseUniversal;
    }

    private BattleItemId RollAttackDropFromLoadout()
    {
        int idx = UnityEngine.Random.Range(0, selectedAttackLoadout.Length);
        return ToBattleItemId(selectedAttackLoadout[idx]);
    }

    private BattleItemId RollSupportDropFromLoadout()
    {
        int idx = UnityEngine.Random.Range(0, selectedSupportLoadout.Length);
        return ToBattleItemId(selectedSupportLoadout[idx]);
    }

    private void SpawnBoardItemRandomly(BattleItemId itemId)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                if (_myObstacle[x, y])
                    continue;

                if (_boardItems[x, y] != BattleItemId.None)
                    continue;

                candidates.Add(new Vector2Int(x, y));
            }
        }

        if (candidates.Count <= 0)
            return;

        Vector2Int pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        _boardItems[pick.x, pick.y] = itemId;
        RefreshBoardVisual();
    }

    private void CollectBoardItem(int x, int y, BattleItemId itemId)
    {
        Vector3 startWorld = _myBoardCells[x, y] != null
            ? _myBoardCells[x, y].transform.position
            : _myBoardRoot.position;

        int ownedId = AddOwnedItem(itemId);
        int targetSlotIndex = FindOwnedSlotIndexByOwnedId(ownedId);

        if (targetSlotIndex >= 0)
            StartCoroutine(PlayItemFlyToInventory(startWorld, targetSlotIndex, itemId));
    }

    private IEnumerator PlayItemFlyToInventory(Vector3 worldStart, int targetSlotIndex, BattleItemId itemId)
    {
        if (_dragLayer == null)
            yield break;

        if (targetSlotIndex < 0 || targetSlotIndex >= _ownedItemButtons.Length)
            yield break;

        if (_ownedItemButtons[targetSlotIndex] == null)
            yield break;

        Sprite sprite = GetGenericItemSprite(itemId);
        if (sprite == null)
            yield break;

        GameObject go = new GameObject(RuntimePrefix + "ItemFly", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_dragLayer, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(42f, 42f);

        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.preserveAspect = true;
        img.raycastTarget = false;

        RectTransform targetRt = _ownedItemButtons[targetSlotIndex].transform as RectTransform;

        Vector2 startScreen = RectTransformUtility.WorldToScreenPoint(null, worldStart);
        Vector2 endScreen = RectTransformUtility.WorldToScreenPoint(null, targetRt.position);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragLayer, startScreen, null, out Vector2 startLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragLayer, endScreen, null, out Vector2 endLocal);

        float duration = 0.25f;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);
            t = 1f - Mathf.Pow(1f - t, 3f);

            rt.anchoredPosition = Vector2.Lerp(startLocal, endLocal, t);
            rt.localScale = Vector3.Lerp(Vector3.one * 0.9f, Vector3.one * 0.55f, t);
            yield return null;
        }

        Destroy(go);
    }

    #endregion

    #region Visual

    private void RefreshTopHud()
    {
        if (_roundText != null)
            _roundText.text = $"Round {_round}";

        if (_myScoreText != null)
            _myScoreText.text = _myScore.ToString();

        if (_opponentNameText != null)
            _opponentNameText.text = "Opponent";
    }

    private void RefreshBoardVisual()
    {
        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                if (_myBoardCells[x, y] == null)
                    continue;

                Color baseColor = BoardBaseColor;
                Sprite blockSprite = null;
                bool showBlockSprite = false;
                Sprite overlaySprite = null;
                bool showOverlay = false;

                if (_myOccupied[x, y])
                {
                    baseColor = _myObstacle[x, y] ? ObstacleColor : _myColors[x, y];

                    if (!_myObstacle[x, y])
                    {
                        blockSprite = _myBlockSprites[x, y];
                        showBlockSprite = blockSprite != null;
                    }
                }

                if (_myObstacle[x, y])
                {
                    overlaySprite = obstacleSprite;
                    showOverlay = overlaySprite != null;
                }
                else if (_boardItems[x, y] != BattleItemId.None)
                {
                    overlaySprite = GetGenericItemSprite(_boardItems[x, y]);
                    showOverlay = overlaySprite != null;
                }

                _myBoardCells[x, y].SetVisual(baseColor, blockSprite, showBlockSprite, overlaySprite, showOverlay);
            }
        }

        if (_dragSlotIndex >= 0 && _currentBlocks[_dragSlotIndex] != null && _dragHasAnchor)
        {
            Color previewColor = _dragCanPlace
                ? new Color(0.22f, 0.65f, 0.35f, 1f)
                : new Color(0.82f, 0.25f, 0.25f, 1f);

            for (int i = 0; i < _currentBlocks[_dragSlotIndex].cells.Count; i++)
            {
                int x = _dragAnchor.x + _currentBlocks[_dragSlotIndex].cells[i].x;
                int y = _dragAnchor.y + _currentBlocks[_dragSlotIndex].cells[i].y;

                if (x < 0 || x >= BoardSize || y < 0 || y >= BoardSize)
                    continue;

                if (_myBoardCells[x, y] == null)
                    continue;

                Sprite overlaySprite = null;
                bool showOverlay = false;

                if (_myObstacle[x, y])
                {
                    overlaySprite = obstacleSprite;
                    showOverlay = overlaySprite != null;
                }
                else if (_boardItems[x, y] != BattleItemId.None)
                {
                    overlaySprite = GetGenericItemSprite(_boardItems[x, y]);
                    showOverlay = overlaySprite != null;
                }

                _myBoardCells[x, y].SetVisual(previewColor, null, false, overlaySprite, showOverlay);
            }
        }

        if (_incomingAttackIcon != null)
        {
            Sprite attackSprite = _incomingAttackItem != BattleItemId.None
                ? GetIncomingAttackSprite(_incomingAttackItem)
                : null;

            _incomingAttackIcon.sprite = attackSprite;
            _incomingAttackIcon.color = Color.white;
            _incomingAttackIcon.preserveAspect = true;
            _incomingAttackIcon.enabled = attackSprite != null;
        }

        RefreshSlotVisual();
    }

    private void RefreshOpponentMiniBoard()
    {
        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                Image img = _opponentMiniCells[x, y];
                if (img == null)
                    continue;

                img.color = _opponentOccupied[x, y] ? ObstacleColor : BoardBaseColor;
            }
        }
    }

    private void RefreshSlotVisual()
    {
        Color defaultSlotColor = Color.white;

        for (int i = 0; i < _slotViews.Length; i++)
        {
            if (_slotViews[i] == null)
                continue;

            // 배경색은 sealed 때문에 바꾸지 않음
            if (_dragSlotIndex == i)
                _slotViews[i].SetBackgroundColor(new Color(1f, 0.88f, 0.42f, 1f));
            else if (_currentBlocks[i] != null && _currentBlocks[i].isCurse)
                _slotViews[i].SetBackgroundColor(new Color(0.63f, 0.32f, 0.86f, 1f));
            else
                _slotViews[i].SetBackgroundColor(defaultSlotColor);

            // 봉인은 배경색 대신 체인 오버레이
            bool sealedOn = (i == _sealedSlotIndex);
            _slotViews[i].SetSealOverlay(sealedOn, sealedSlotOverlaySprite);
        }
    }

    private void DrawAllBlockPreviews()
    {
        for (int i = 0; i < _currentBlocks.Length; i++)
            DrawBlockPreview(i);

        RefreshSlotVisual();
    }

    private void DrawBlockPreview(int slotIndex)
    {
        if (_slotViews[slotIndex] == null)
            return;

        RectTransform root = _slotViews[slotIndex].RectTransform;
        DestroyRuntimeChildren(root);

        BlockInstance block = _currentBlocks[slotIndex];
        if (block == null)
            return;

        int width = GetBlockWidth(block);
        int height = GetBlockHeight(block);
        float totalW = (width * slotPreviewCellSize) + ((width - 1) * slotPreviewSpacing);
        float totalH = (height * slotPreviewCellSize) + ((height - 1) * slotPreviewSpacing);
        float startX = -totalW * 0.5f + slotPreviewCellSize * 0.5f;
        float startY = totalH * 0.5f - slotPreviewCellSize * 0.5f;

        for (int i = 0; i < block.cells.Count; i++)
        {
            Vector2Int p = block.cells[i];
            CreatePreviewCell(
                root,
                new Vector2(slotPreviewCellSize, slotPreviewCellSize),
                new Vector2(
                    startX + (p.x * (slotPreviewCellSize + slotPreviewSpacing)),
                    startY - (p.y * (slotPreviewCellSize + slotPreviewSpacing))),
                block.color,
                block.cellSprite);
        }
    }

    #endregion

    #region Drag Preview

    private void CreateDragPreview(BlockInstance block)
    {
        DestroyDragPreview();

        GameObject root = new GameObject(RuntimePrefix + "DragPreview", typeof(RectTransform));
        _dragPreviewRoot = root.GetComponent<RectTransform>();
        _dragPreviewRoot.SetParent(_dragLayer, false);
        _dragPreviewRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _dragPreviewRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _dragPreviewRoot.pivot = new Vector2(0.5f, 0.5f);

        int width = GetBlockWidth(block);
        int height = GetBlockHeight(block);
        float totalW = (width * dragPreviewCellSize) + ((width - 1) * dragPreviewSpacing);
        float totalH = (height * dragPreviewCellSize) + ((height - 1) * dragPreviewSpacing);
        float startX = -totalW * 0.5f + dragPreviewCellSize * 0.5f;
        float startY = totalH * 0.5f - dragPreviewCellSize * 0.5f;

        for (int i = 0; i < block.cells.Count; i++)
        {
            Vector2Int p = block.cells[i];
            CreatePreviewCell(
                _dragPreviewRoot,
                new Vector2(dragPreviewCellSize, dragPreviewCellSize),
                new Vector2(
                    startX + (p.x * (dragPreviewCellSize + dragPreviewSpacing)),
                    startY - (p.y * (dragPreviewCellSize + dragPreviewSpacing))),
                new Color(block.color.r, block.color.g, block.color.b, 0.85f),
                block.cellSprite);
        }
    }

    private void DestroyDragPreview()
    {
        if (_dragPreviewRoot != null)
            Destroy(_dragPreviewRoot.gameObject);

        _dragPreviewRoot = null;
    }

    #endregion

    #region Mapping

    private static BattleItemId ToBattleItemId(AttackItemId itemId)
    {
        switch (itemId)
        {
            case AttackItemId.Obstacle2: return BattleItemId.AttackObstacle2;
            case AttackItemId.SealRandomSlot: return BattleItemId.AttackSealRandomSlot;
            case AttackItemId.CurseBlock: return BattleItemId.AttackCurseBlock;
            case AttackItemId.DisableItemUse: return BattleItemId.AttackDisableItemUse;
            case AttackItemId.DeleteRandomLine: return BattleItemId.AttackDeleteRandomLine;
            case AttackItemId.Bomb3x3: return BattleItemId.AttackBomb3x3;
            default: return BattleItemId.None;
        }
    }

    private static BattleItemId ToBattleItemId(SupportItemId itemId)
    {
        switch (itemId)
        {
            case SupportItemId.RotateRight90: return BattleItemId.SupportRotateRight90;
            case SupportItemId.RotateLeft90: return BattleItemId.SupportRotateLeft90;
            case SupportItemId.MirrorHorizontal: return BattleItemId.SupportMirrorHorizontal;
            case SupportItemId.MirrorVertical: return BattleItemId.SupportMirrorVertical;
            case SupportItemId.ResetRemaining: return BattleItemId.SupportResetRemaining;
            default: return BattleItemId.None;
        }
    }

    private ItemKind GetItemKind(BattleItemId itemId)
    {
        switch (itemId)
        {
            case BattleItemId.AttackObstacle2:
            case BattleItemId.AttackSealRandomSlot:
            case BattleItemId.AttackCurseBlock:
            case BattleItemId.AttackDisableItemUse:
            case BattleItemId.AttackDeleteRandomLine:
            case BattleItemId.AttackBomb3x3:
                return ItemKind.Attack;

            case BattleItemId.SupportRotateRight90:
            case BattleItemId.SupportRotateLeft90:
            case BattleItemId.SupportMirrorHorizontal:
            case BattleItemId.SupportMirrorVertical:
            case BattleItemId.SupportResetRemaining:
                return ItemKind.Support;

            case BattleItemId.DefenseUniversal:
                return ItemKind.Defense;

            default:
                return ItemKind.None;
        }
    }

    private Sprite GetGenericItemSprite(BattleItemId itemId)
    {
        return GetItemSprite(itemId);
    }

    private Sprite GetIncomingAttackSprite(BattleItemId itemId)
    {
        return GetIncomingSprite(itemId);
    }

    private Color GetSlotColor(BattleItemId itemId)
    {
        switch (GetItemKind(itemId))
        {
            case ItemKind.Attack: return AttackSlotColor;
            case ItemKind.Support: return SupportSlotColor;
            case ItemKind.Defense: return DefenseSlotColor;
            default: return EmptySlotColor;
        }
    }

    #endregion

    #region Utility

    private GameObject CreateCellObject(Transform parent, bool ensureBoardCell)
    {
        GameObject go;

        if (boardCellPrefab != null)
        {
            go = Instantiate(boardCellPrefab, parent);
            go.name = RuntimePrefix + "Cell";
        }
        else
        {
            go = new GameObject(RuntimePrefix + "Cell", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
        }

        Image img = GetOrAdd<Image>(go);
        img.raycastTarget = false;

        if (ensureBoardCell)
            GetOrAdd<BoardCell>(go);

        return go;
    }

    private void CreatePreviewCell(RectTransform parent, Vector2 size, Vector2 anchoredPosition, Color color, Sprite sprite)
    {
        GameObject go;

        if (previewCellPrefab != null)
        {
            go = Instantiate(previewCellPrefab, parent);
            go.name = RuntimePrefix + "PreviewCell";
        }
        else
        {
            go = new GameObject(RuntimePrefix + "PreviewCell", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;

        Image img = GetOrAdd<Image>(go);
        img.color = Color.white;
        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = false;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        if (c == null)
            c = go.AddComponent<T>();
        return c;
    }

    private static TMP_Text FindTMP(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private static TMP_Text FindTMPEither(Transform root, string pathA, string pathB)
    {
        TMP_Text a = FindTMP(root, pathA);
        return a != null ? a : FindTMP(root, pathB);
    }

    private static RectTransform FindRect(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t as RectTransform;
    }

    private static GameObject FindGO(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.gameObject : null;
    }

    private static Image FindImage(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<Image>() : null;
    }

    private Image FindOrCreateSlotIconImage(Transform slotRoot)
    {
        Transform iconTr = slotRoot.Find("IconImage");
        if (iconTr != null)
        {
            Image found = GetOrAdd<Image>(iconTr.gameObject);
            found.raycastTarget = false;
            found.preserveAspect = true;
            return found;
        }

        GameObject go = new GameObject("IconImage", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(slotRoot, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;
        img.enabled = false;
        return img;
    }

    private static void DestroyRuntimeChildren(RectTransform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child.name.StartsWith(RuntimePrefix, StringComparison.Ordinal))
                Destroy(child.gameObject);
        }
    }

    private static void NormalizeCells(List<Vector2Int> cells)
    {
        int minX = int.MaxValue;
        int minY = int.MaxValue;

        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].x < minX) minX = cells[i].x;
            if (cells[i].y < minY) minY = cells[i].y;
        }

        for (int i = 0; i < cells.Count; i++)
            cells[i] = new Vector2Int(cells[i].x - minX, cells[i].y - minY);
    }

    private static List<Vector2Int> RotateCells(List<Vector2Int> source, int rotation)
    {
        List<Vector2Int> result = new List<Vector2Int>(source.Count);

        for (int i = 0; i < source.Count; i++)
        {
            Vector2Int p = source[i];
            switch (rotation)
            {
                case 90:
                    result.Add(new Vector2Int(p.y, -p.x));
                    break;
                case 180:
                    result.Add(new Vector2Int(-p.x, -p.y));
                    break;
                case 270:
                    result.Add(new Vector2Int(-p.y, p.x));
                    break;
                default:
                    result.Add(new Vector2Int(p.x, p.y));
                    break;
            }
        }

        return result;
    }

    private static int GetBlockWidth(BlockInstance block)
    {
        int maxX = 0;
        for (int i = 0; i < block.cells.Count; i++)
        {
            if (block.cells[i].x > maxX)
                maxX = block.cells[i].x;
        }
        return maxX + 1;
    }

    private static int GetBlockHeight(BlockInstance block)
    {
        int maxY = 0;
        for (int i = 0; i < block.cells.Count; i++)
        {
            if (block.cells[i].y > maxY)
                maxY = block.cells[i].y;
        }
        return maxY + 1;
    }

    private static void SetButtonLabel(Transform buttonRoot, string value)
    {
        if (buttonRoot == null)
            return;

        TMP_Text tmp = buttonRoot.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
            tmp.text = value;
    }

    #endregion

    #region AddFunction
    private bool HasAnyPlaceableBlock()
    {
        for (int i = 0; i < _currentBlocks.Length; i++)
        {
            if (_currentBlocks[i] == null)
                continue;

            if (i == _sealedSlotIndex)
                continue;

            if (HasPlacementForBlock(_currentBlocks[i]))
                return true;
        }

        return false;
    }

    private bool HasPlacementForBlock(BlockInstance block)
    {
        if (block == null)
            return false;

        int width = GetBlockWidth(block);
        int height = GetBlockHeight(block);

        for (int y = 0; y <= BoardSize - height; y++)
        {
            for (int x = 0; x <= BoardSize - width; x++)
            {
                if (CanPlaceBlock(block, x, y))
                    return true;
            }
        }

        return false;
    }

    private void RequestRoundEnd()
    {
        if (!CanAcceptUserInput())
            return;

        if (_isResolvingLineClearFx)
            return;

        if (IsRankedBattle && !_networkGameStarted)
            return;

        if (_waitingForOpponentRoundReady || _roundSyncAdvanceQueued || _roundReadySendIssued)
            return;

        if (_phase != BattlePhase.Play)
            return;

        if (_localRoundReady)
            return;

        bool noPlacement = !HasAnyPlaceableBlock();
        bool allUsed = AllBlocksUsed();
        bool canEnd = noPlacement || allUsed || _phaseTimer <= 0f;

        if (!canEnd)
            return;

        if (IsRankedBattle && _battleNetDriver != null && !_battleNetDriver.IsRelayReadyForRealtime())
        {
            Debug.LogWarning("[BBB] RequestRoundEnd blocked - relay unavailable");
            SetDisconnectPauseState(true);
            _battleNetDriver.TryReconnectToActiveGame();
            return;
        }

        SendRoundEndReadyToOpponent();
    }



    public void OnOpponentRoundReadyReceived()
    {
        if (_opponentRoundReady)
        {
            Debug.Log("[BBB] Opponent ROUND_READY already received");
            return;
        }

        _opponentRoundReady = true;

        Debug.Log("[BBB] Opponent ROUND_READY received");

        TryResolveRoundEndSync();
    }
    public void OnOpponentScoreSyncReceived(int score)
    {
        opponentScore = score;

        RefreshTopHud();

        Debug.Log($"[BBB] Opponent SCORE_SYNC received / score={score}");
    }

    private void TryEnterResolveWhenBothReady()
    {
        if (!_localRoundReady)
            return;

        if (!_opponentRoundReady)
            return;

        _waitingForOpponentRoundReady = false;
        EnterResolvePhase();
    }

    private void SendRoundEndReadyToOpponent()
    {
        if (_roundReadySendIssued)
            return;

        if (_battleNetDriver == null)
        {
            Debug.LogError("[BBB] BattleNetDriver is null");
            return;
        }

        ApplyReservedOutgoingAttack();

        RefreshOwnedItemUI();
        RefreshDefenseUI();
        RefreshBoardVisual();
        RefreshOpponentMiniBoard();
        RefreshTopHud();

        bool snapOk = _battleNetDriver.TrySendBoardSnapshot();
        bool scoreOk = _battleNetDriver.TrySendScoreSync(GetMyScoreForNetwork());
        bool readyOk = _battleNetDriver.TrySendRoundReady();

        if (!snapOk || !scoreOk || !readyOk)
        {
            Debug.LogError($"[BBB] RoundEnd send failed / snap={snapOk} / score={scoreOk} / ready={readyOk}");

            _roundReadySendIssued = false;
            _localRoundReady = false;
            _waitingForOpponentRoundReady = false;

            RefreshRoundEndButtonUI();
            RefreshOwnedItemUI();
            RefreshSlotVisual();
            RefreshTopHud();

            SetDisconnectPauseState(true);
            _battleNetDriver.TryReconnectToActiveGame();
            return;
        }

        _roundReadySendIssued = true;
        _localRoundReady = true;
        _waitingForOpponentRoundReady = true;

        RefreshRoundEndButtonUI();
        RefreshOwnedItemUI();
        RefreshSlotVisual();

        TryResolveRoundEndSync();
    }

    private IEnumerator CoDebugAutoOpponentRoundReady()
    {
        yield return new WaitForSeconds(0.25f);
        OnOpponentRoundReadyReceived();
    }

    private void RefreshRoundEndButtonUI()
    {
        if (roundEndButton == null)
            return;

        bool noPlacement = !HasAnyPlaceableBlock();
        bool canEnd = _phase == BattlePhase.Play && (noPlacement || AllBlocksUsed() || _phaseTimer <= 0f);

        // 평소엔 숨김, 종료 가능하거나 내가 이미 종료 눌렀으면 표시
        bool shouldShow = canEnd || _localRoundReady;

        if (roundEndButton.gameObject.activeSelf != shouldShow)
            roundEndButton.gameObject.SetActive(shouldShow);

        if (!shouldShow)
            return;

        roundEndButton.interactable = true;

        Image img = roundEndButton.GetComponent<Image>();
        if (img != null)
            img.raycastTarget = canEnd && !_localRoundReady;

        if (_localRoundReady)
        {
            SetButtonLabel(roundEndButton.transform, _opponentRoundReady ? "라운드 종료 완료" : "상대 대기중");
        }
        else
        {
            SetButtonLabel(roundEndButton.transform, "라운드 종료");
        }
    }

    private Button FindButtonDeep(Transform root, string targetName)
    {
        Transform found = FindDeepChild(root, targetName);
        if (found == null)
            return null;

        return found.GetComponent<Button>();
    }

    private Transform FindDeepChild(Transform parent, string targetName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == targetName)
                return child;

            Transform found = FindDeepChild(child, targetName);
            if (found != null)
                return found;
        }

        return null;
    }
    private void ApplyLoadoutFromSession()
    {
        if (!BattleLoadoutSession.HasValidLoadout())
            return;

        for (int i = 0; i < 3; i++)
            selectedAttackLoadout[i] = BattleLoadoutSession.SelectedAttackIds[i];

        for (int i = 0; i < 2; i++)
            selectedSupportLoadout[i] = BattleLoadoutSession.SelectedSupportIds[i];
    }

    private void ShowResultPhase()
    {
        HideLoadingOverlay();

        if (_resultPhaseRoot == null)
            return;

        bool isWin = _myScore > opponentScore;
        bool isDraw = _myScore == opponentScore;
        bool isLose = _myScore < opponentScore;

        if (_victoryEmblem != null)
            _victoryEmblem.SetActive(isWin);

        if (_drawEmblem != null)
            _drawEmblem.SetActive(isDraw);

        if (_defeatEmblem != null)
            _defeatEmblem.SetActive(isLose);

        if (_myResultText != null)
            _myResultText.text = $"{_myScore:N0}";

        if (_opponentResultText != null)
            _opponentResultText.text = $"{opponentScore:N0}";

        if (_myNicknameText != null)
            _myNicknameText.text = $"{myNickname}";

        if (_opponentNicknameText != null)
            _opponentNicknameText.text = $"{opponentNickname}";

        _resultPhaseRoot.SetActive(true);
    }

    private void HideResultPhase()
    {
        if (_resultPhaseRoot != null)
            _resultPhaseRoot.SetActive(false);
    }

    private void GoToLobbyFromResult()
    {
        BattleLoadoutSession.Clear();
        SceneManager.LoadScene(lobbySceneName);
    }

    /// <summary>
    /// 매치 연동용 공개 함수 (닉네임/점수 세팅용)
    /// </summary>
    /// <param name="nickname"></param>
    public void SetMyNickname(string nickname)
    {
        if (!string.IsNullOrWhiteSpace(nickname))
            myNickname = nickname;
    }

    public void SetOpponentResultData(string nickname, int score)
    {
        if (!string.IsNullOrWhiteSpace(nickname))
            opponentNickname = nickname;

        opponentScore = score;
    }

    private void ApplyMatchSession()
    {
        if (BattleMatchSession.Mode == GameMode.None)
            return;

        if (!string.IsNullOrWhiteSpace(BattleMatchSession.MyNickname))
            myNickname = BattleMatchSession.MyNickname;

        if (!string.IsNullOrWhiteSpace(BattleMatchSession.OpponentNickname))
            opponentNickname = BattleMatchSession.OpponentNickname;
    }
    #endregion

    #region Match System
    public bool IsNetworkGameStarted => _networkGameStarted;
    public int NetworkSeed => _networkSeed;
    public bool IsHostPlayer => _isHostPlayer;

    public void SetOpponentNicknameExternal(string nicknameValue)
    {
        if (string.IsNullOrWhiteSpace(nicknameValue))
            return;

        opponentNickname = nicknameValue;

        if (_opponentNameText != null)
            _opponentNameText.text = opponentNickname;
    }

    public void OnNetworkGameStart(int seed, bool isHost)
    {
        ShowLoadingOverlay("블록 준비 중...");

        _networkSeed = seed;
        _isHostPlayer = isHost;
        _networkGameStarted = true;

        ResetNetworkRoundSyncState();

        Debug.Log($"[BattleManager] OnNetworkGameStart / seed={seed} / isHost={isHost}");

        StartRound();

        if (roundEndButton != null)
            roundEndButton.gameObject.SetActive(true);
    }

    private void TryResolveRoundEndSync()
    {
        if (!_localRoundReady || !_opponentRoundReady)
            return;

        if (_roundSyncAdvanceQueued)
            return;

        _waitingForOpponentRoundReady = false;
        _roundSyncAdvanceQueued = true;

        Debug.Log("[BBB] Both ROUND_READY received -> advance phase queued");

        StartCoroutine(CoAdvanceAfterRoundSync());
    }

    private IEnumerator CoAdvanceAfterRoundSync()
    {
        // 같은 프레임에 중복 상태 변경 꼬임 방지
        yield return null;

        bool invoked = false;

        // 네 기존 코드에 있는 라운드 전환 메서드명을 자동으로 찾아서 호출한다.
        // 아래 후보들 중 하나만 실제로 있어도 된다.
        invoked |= InvokeNoArgMethodIfExists("BeginResolvePhase");
        invoked |= InvokeNoArgMethodIfExists("EnterResolvePhase");
        invoked |= InvokeNoArgMethodIfExists("StartResolvePhase");
        invoked |= InvokeNoArgMethodIfExists("AdvanceToResolvePhase");
        invoked |= InvokeNoArgMethodIfExists("AdvanceToNextRound");
        invoked |= InvokeNoArgMethodIfExists("BeginNextRound");
        invoked |= InvokeNoArgMethodIfExists("StartNextRound");
        invoked |= InvokeNoArgMethodIfExists("GoToNextRound");

        if (!invoked)
        {
            Debug.LogError("[BBB] Round sync complete, but no round-advance method was found. Resolve/NextRound 메서드명을 확인해줘.");
        }
    }

    private bool InvokeNoArgMethodIfExists(string methodName)
    {
        var flags = System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic;

        var mi = GetType().GetMethod(methodName, flags);
        if (mi == null)
            return false;

        var ps = mi.GetParameters();
        if (ps != null && ps.Length > 0)
            return false;

        Debug.Log($"[BBB] Invoke round advance method -> {methodName}");
        mi.Invoke(this, null);
        return true;
    }

    public void ResetNetworkRoundSyncState()
    {
        _roundReadySendIssued = false;
        _roundSyncAdvanceQueued = false;

        _localRoundReady = false;
        _opponentRoundReady = false;
        _waitingForOpponentRoundReady = false;

        if (_battleNetDriver != null)
            _battleNetDriver.ResetRoundSyncState();

        Debug.Log("[BBB] ResetNetworkRoundSyncState");
    }
    private void ShowLoadingOverlay(string message)
    {
        if (loadingRoot != null)
            loadingRoot.SetActive(true);

        if (loadingText != null)
            loadingText.text = message;
    }

    private void HideLoadingOverlay()
    {
        if (_loadingHideRoutine != null)
        {
            StopCoroutine(_loadingHideRoutine);
            _loadingHideRoutine = null;
        }

        if (loadingRoot != null)
            loadingRoot.SetActive(false);
    }

    private void HideLoadingOverlayNextFrame()
    {
        if (_loadingHideRoutine != null)
            StopCoroutine(_loadingHideRoutine);

        _loadingHideRoutine = StartCoroutine(CoHideLoadingOverlayNextFrame());
    }

    private IEnumerator CoHideLoadingOverlayNextFrame()
    {
        yield return null;

        if (loadingRoot != null)
            loadingRoot.SetActive(false);

        _loadingHideRoutine = null;
    }

    //미니 보드 스냅샷    
    public byte[] BuildBoardSnapshotPayload()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write((byte)BoardSize);

            for (int y = 0; y < BoardSize; y++)
            {
                for (int x = 0; x < BoardSize; x++)
                {
                    bw.Write(_myOccupied[x, y]);
                }
            }

            return ms.ToArray();
        }
    }

    public void OnOpponentBoardSnapshotReceived(byte[] payload)
    {
        if (payload == null || payload.Length == 0)
            return;

        using (MemoryStream ms = new MemoryStream(payload))
        using (BinaryReader br = new BinaryReader(ms))
        {
            int size = br.ReadByte();

            for (int y = 0; y < BoardSize; y++)
            {
                for (int x = 0; x < BoardSize; x++)
                {
                    _opponentOccupied[x, y] = false;
                }
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool occupied = br.ReadBoolean();

                    if (x < BoardSize && y < BoardSize)
                        _opponentOccupied[x, y] = occupied;
                }
            }
        }

        RefreshOpponentMiniBoard();
    }

    private void ClearOpponentBoardSnapshot()
    {
        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                _opponentOccupied[x, y] = false;
            }
        }

        RefreshOpponentMiniBoard();
    }

    private void SubmitNetworkMatchResultIfNeeded()
    {
        if (BattleMatchSession.Mode != GameMode.Ranked)
            return;

        if (_battleNetDriver == null)
            _battleNetDriver = GetComponent<BattleNetDriver>();

        if (_battleNetDriver == null)
        {
            Debug.LogError("[BBB] SubmitNetworkMatchResultIfNeeded failed - BattleNetDriver is null");
            return;
        }

        _battleNetDriver.SubmitMatchResultByScores(_myScore, opponentScore);
    }

    public void OnRankedRecordRefreshedFromServer()
    {
        RefreshRankedResultMmrUi();
    }

    private void RefreshRankedResultMmrUi()
    {
        if (resultMmrText == null)
            return;

        if (!IsRankedBattle || !RankedRecordCache.HasLoaded)
        {
            resultMmrText.text = "-";
            return;
        }

        resultMmrText.text = RankedRecordCache.GetFormattedMmrWithDelta();
    }    

    private int FindFirstDefenseOwnedItemIndex()
    {
        for (int i = 0; i < _ownedItems.Count; i++)
        {
            if (GetItemKind(_ownedItems[i].itemId) == ItemKind.Defense)
                return i;
        }

        return -1;
    }

    private void ApplyIncomingAttackToMyBoard(BattleItemId itemId)
    {
        switch (itemId)
        {
            case BattleItemId.AttackObstacle2:
                ApplyObstacleAttackToMyBoard(2);
                break;

            case BattleItemId.AttackSealRandomSlot:
                ApplySealRandomSlotAttackToMyBoard();
                break;

            case BattleItemId.AttackCurseBlock:
                ApplyCurseBlockAttackToMyBoard();
                break;

            case BattleItemId.AttackDisableItemUse:
                ApplyDisableItemUseAttackToMyBoard();
                break;

            case BattleItemId.AttackDeleteRandomLine:
                ApplyDeleteRandomLineAttackToMyBoard();
                break;

            case BattleItemId.AttackBomb3x3:
                ApplyBomb3x3AttackToMyBoard();
                break;
        }

        RefreshSlotVisual();
        RefreshOwnedItemUI();
        RefreshBoardVisual();
        RefreshOpponentMiniBoard();
        RefreshTopHud();

        if (IsRankedBattle && _battleNetDriver != null)
            _battleNetDriver.TrySendBoardSnapshot();
    }

    private void ApplyObstacleAttackToMyBoard(int count)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                if (_myOccupied[x, y]) continue;
                if (_myObstacle[x, y]) continue;
                if (_boardItems[x, y] != BattleItemId.None) continue;

                candidates.Add(new Vector2Int(x, y));
            }
        }

        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int pick = UnityEngine.Random.Range(0, candidates.Count);
            Vector2Int cell = candidates[pick];
            candidates.RemoveAt(pick);

            _myObstacle[cell.x, cell.y] = true;
            _myOccupied[cell.x, cell.y] = false;
            _myColors[cell.x, cell.y] = Color.clear;
            _myBlockSprites[cell.x, cell.y] = null;
            _boardItems[cell.x, cell.y] = BattleItemId.None;
        }
    }

    private void ApplySealRandomSlotAttackToMyBoard()
    {
        _sealedSlotIndex = UnityEngine.Random.Range(0, _currentBlocks.Length);
    }

    private void ApplyCurseBlockAttackToMyBoard()
    {
        _forceCurseBlockNextRound = true;
    }

    private void ApplyDisableItemUseAttackToMyBoard()
    {
        _itemUseBlockedThisRound = true;
    }

    private void ApplyDeleteRandomLineAttackToMyBoard()
    {
        List<(bool isRow, int index)> candidates = new List<(bool, int)>();

        for (int y = 0; y < BoardSize; y++)
        {
            bool hasAny = false;
            for (int x = 0; x < BoardSize; x++)
            {
                if (_myOccupied[x, y] || _myObstacle[x, y] || _boardItems[x, y] != BattleItemId.None)
                {
                    hasAny = true;
                    break;
                }
            }

            if (hasAny)
                candidates.Add((true, y));
        }

        for (int x = 0; x < BoardSize; x++)
        {
            bool hasAny = false;
            for (int y = 0; y < BoardSize; y++)
            {
                if (_myOccupied[x, y] || _myObstacle[x, y] || _boardItems[x, y] != BattleItemId.None)
                {
                    hasAny = true;
                    break;
                }
            }

            if (hasAny)
                candidates.Add((false, x));
        }

        if (candidates.Count == 0)
            return;

        int pick = UnityEngine.Random.Range(0, candidates.Count);
        var selected = candidates[pick];

        if (selected.isRow)
        {
            int y = selected.index;
            for (int x = 0; x < BoardSize; x++)
                ClearMyBoardCell(x, y);
        }
        else
        {
            int x = selected.index;
            for (int y = 0; y < BoardSize; y++)
                ClearMyBoardCell(x, y);
        }
    }

    private void ApplyBomb3x3AttackToMyBoard()
    {
        List<Vector2Int> hotCells = new List<Vector2Int>();

        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                if (_myOccupied[x, y] || _myObstacle[x, y] || _boardItems[x, y] != BattleItemId.None)
                    hotCells.Add(new Vector2Int(x, y));
            }
        }

        Vector2Int center = hotCells.Count > 0
            ? hotCells[UnityEngine.Random.Range(0, hotCells.Count)]
            : new Vector2Int(UnityEngine.Random.Range(0, BoardSize), UnityEngine.Random.Range(0, BoardSize));

        for (int y = center.y - 1; y <= center.y + 1; y++)
        {
            for (int x = center.x - 1; x <= center.x + 1; x++)
            {
                if (x < 0 || x >= BoardSize || y < 0 || y >= BoardSize)
                    continue;

                ClearMyBoardCell(x, y);
            }
        }
    }

    private void ClearMyBoardCell(int x, int y)
    {
        _myOccupied[x, y] = false;
        _myObstacle[x, y] = false;
        _myColors[x, y] = Color.clear;
        _myBlockSprites[x, y] = null;
        _boardItems[x, y] = BattleItemId.None;
    }

    public void OnNetworkAttackIntentReceived(BattleItemId itemId, bool isReserved)
    {
        bool validAttack = itemId != BattleItemId.None && GetItemKind(itemId) == ItemKind.Attack;

        _remoteAttackPlannedVisible = isReserved && validAttack;
        _remotePlannedAttackItem = _remoteAttackPlannedVisible ? itemId : BattleItemId.None;

        Debug.Log($"[BM] OnNetworkAttackIntentReceived item={itemId} reserved={isReserved} visible={_remoteAttackPlannedVisible}");

        RefreshIncomingAttackTelegraph();
    }

    public void OnNetworkItemUseReceived(BattleItemId itemId)
    {
        if (GetItemKind(itemId) != ItemKind.Attack)
            return;

        // 이제 예정이 아니라 실제 공격이므로 예정 표시는 끈다
        _remoteAttackPlannedVisible = false;
        _remotePlannedAttackItem = BattleItemId.None;
        RefreshIncomingAttackTelegraph();

        QueueIncomingAttack(itemId, false);
    }

    private void RefreshIncomingAttackTelegraph()
    {
        if (_incomingAttackTelegraphView == null)
            return;

        if (!_remoteAttackPlannedVisible || _remotePlannedAttackItem == BattleItemId.None)
        {
            _incomingAttackTelegraphView.SetPlanned(false, BattleItemId.None);
            return;
        }

        _incomingAttackTelegraphView.SetPlanned(true, _remotePlannedAttackItem);
    }

    public void OnNetworkAttackOutcomeReceived(bool wasBlocked)
    {
        if (_opponentMiniBoardFx == null)
            return;

        if (wasBlocked)
            _opponentMiniBoardFx.PlayAttackBlockedFx();
        else
            _opponentMiniBoardFx.PlayAttackHitFx();
    }

    #endregion
}