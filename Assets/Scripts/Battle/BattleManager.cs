using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class BattleManager : MonoBehaviour
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

        public BlockShape(BlockShapeId shapeId, int weight, Color color, List<Vector2Int> baseCells, bool isCurse)
        {
            this.shapeId = shapeId;
            this.weight = weight;
            this.color = color;
            this.baseCells = baseCells;
            this.isCurse = isCurse;
        }
    }

    private sealed class BlockInstance
    {
        public BlockShapeId shapeId;
        public int rotation;
        public Color color;
        public List<Vector2Int> cells;
        public bool isCurse;
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
    private static readonly Color32 AttackSlotColor = new Color32(255, 100, 100, 255);
    private static readonly Color32 SupportSlotColor = new Color32(255, 203, 100, 255);
    private static readonly Color32 DefenseSlotColor = new Color32(100, 105, 255, 255);
    private static readonly Color32 BoardBaseColor = new Color32(36, 38, 56, 255);
    private static readonly Color32 ObstacleColor = new Color32(62, 63, 72, 255);
    private static readonly Color32 SealedSlotColor = new Color32(140, 100, 220, 255);

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
    private readonly BattleItemId[,] _boardItems = new BattleItemId[BoardSize, BoardSize];

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

    private void Awake()
    {
        Transform safeArea = FindSafeArea();
        EnsureRuntimeOwnedItemSlots(safeArea);
        EnsureRuntimeDefenseButtons(safeArea);

        CacheHierarchy();
        BuildShapeLibrary();
        SanitizeLoadout();
        ApplyLoadoutFromSession();
        BuildItemSpriteMap();
        BindButtons();
    }

    private void Start()
    {
        BuildBoards();
        ResetCurrentRoundBlocks();
        HideDefensePhase();
        HideResultPhase();
        RefreshOwnedItemUI();
        RefreshTopHud();
        StartRound();


        if (roundEndButton != null)
            roundEndButton.gameObject.SetActive(false);
    }

    private void Update()
    {
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

        AddShape(_normalShapeLibrary, BlockShapeId.H3, 90, new Color32(86, 204, 242, 255), false,
    new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0));

        AddShape(_normalShapeLibrary, BlockShapeId.L3, 90, new Color32(111, 230, 185, 255), false,
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1));

        AddShape(_normalShapeLibrary, BlockShapeId.H4, 90, new Color32(255, 196, 87, 255), false,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0));

        AddShape(_normalShapeLibrary, BlockShapeId.O4, 60, new Color32(255, 224, 102, 255), false,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1));

        AddShape(_normalShapeLibrary, BlockShapeId.T4, 85, new Color32(255, 121, 121, 255), false,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1));

        AddShape(_normalShapeLibrary, BlockShapeId.L4, 85, new Color32(255, 159, 67, 255), false,
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2));

        AddShape(_normalShapeLibrary, BlockShapeId.L4_M, 85, new Color32(255, 133, 162, 255), false,
            new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(0, 2));

        AddShape(_normalShapeLibrary, BlockShapeId.Z4, 75, new Color32(163, 230, 53, 255), false,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1));

        AddShape(_normalShapeLibrary, BlockShapeId.Z4_M, 75, new Color32(52, 211, 153, 255), false,
            new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(0, 1), new Vector2Int(1, 1));

        AddShape(_normalShapeLibrary, BlockShapeId.H5, 55, new Color32(129, 140, 248, 255), false,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0), new Vector2Int(4, 0));

        AddShape(_normalShapeLibrary, BlockShapeId.P5, 70, new Color32(192, 132, 252, 255), false,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(0, 2));

        AddShape(_normalShapeLibrary, BlockShapeId.T5, 70, new Color32(244, 114, 182, 255), false,
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1), new Vector2Int(1, 2));

        AddShape(_curseShapeLibrary, BlockShapeId.CurseDiag3, 100, new Color(0.85f, 0.35f, 0.95f), true,
            new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(2, 2));

        AddShape(_curseShapeLibrary, BlockShapeId.CurseSplit3, 100, new Color(0.85f, 0.35f, 0.95f), true,
            new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(1, 2));

        AddShape(_curseShapeLibrary, BlockShapeId.CurseDiag4, 100, new Color(0.85f, 0.35f, 0.95f), true,
            new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(2, 2), new Vector2Int(3, 3));

        AddShape(_curseShapeLibrary, BlockShapeId.CurseSplit4, 100, new Color(0.85f, 0.35f, 0.95f), true,
            new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(0, 2), new Vector2Int(2, 2));
    }

    private void AddShape(List<BlockShape> target, BlockShapeId shapeId, int weight, Color color, bool isCurse, params Vector2Int[] cells)
    {
        target.Add(new BlockShape(shapeId, weight, color, new List<Vector2Int>(cells), isCurse));
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
        RefreshRoundEndButtonUI();
        HideResultPhase();

        _localRoundReady = false;
        _opponentRoundReady = false;
        _waitingForOpponentRoundReady = false;

        if (_round > maxRounds)
        {
            EndBattle();
            return;
        }

        _pendingResolveScore = 0;

        ApplyReservedOutgoingAttack();
        ResetCurrentRoundBlocks();
        RefreshOwnedItemUI();
        RefreshBoardVisual();
        RefreshOpponentMiniBoard();
        RefreshTopHud();

        if (_incomingAttackItem != BattleItemId.None)
            ShowDefensePhase();
        else
            BeginPlayableRound();
    }

    private void BeginPlayableRound()
    {
        GiveThreeBlocks();
        TrySpawnRandomBoardItem();
        DrawAllBlockPreviews();
        RefreshOwnedItemUI();
        RefreshBoardVisual();
        RefreshOpponentMiniBoard();
        EnterPlayPhase();
        RefreshRoundEndButtonUI();
    }

    private void AdvanceToNextRound()
    {
        ExpireRoundStatuses();
        _round++;
        StartRound();
    }

    private void ExpireRoundStatuses()
    {
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
            _timerText.text = $"{Mathf.CeilToInt(_phaseTimer)}";

        if (_phaseTimer <= 0f || AllBlocksUsed())
        {
            RequestRoundEnd();
            return;
        }

        RefreshRoundEndButtonUI();
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
        if (_phase != BattlePhase.Defense)
            return;

        int ownedId = FindFirstOwnedItemIdByType(BattleItemId.DefenseUniversal);
        if (ownedId < 0)
        {
            SkipDefenseNow();
            return;
        }

        RemoveOwnedItemByOwnedId(ownedId);
        _incomingAttackItem = BattleItemId.None;

        RefreshOwnedItemUI();
        RefreshBoardVisual();
        BeginPlayableRound();
        RefreshRoundEndButtonUI();
    }

    private void SkipDefenseNow()
    {
        if (_phase != BattlePhase.Defense)
            return;

        ApplyIncomingAttackEffect(_incomingAttackItem);
        _incomingAttackItem = BattleItemId.None;

        RefreshOwnedItemUI();
        RefreshBoardVisual();
        BeginPlayableRound();
        RefreshRoundEndButtonUI();
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
            isCurse = shape.isCurse
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
        if (_dragSlotIndex != slotIndex)
            return;

        UpdateDrag(eventData);
        RefreshBoardVisual();
    }

    public void OnEndDragSlot(int slotIndex, PointerEventData eventData)
    {
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
        if (_dragPreviewRoot == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragLayer, eventData.position, eventData.pressEventCamera, out Vector2 local))
            _dragPreviewRoot.anchoredPosition = local;

        BlockInstance block = _currentBlocks[_dragSlotIndex];
        _dragHasAnchor = TryGetBoardAnchor(eventData.position, eventData.pressEventCamera, block, out _dragAnchor);
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
        }

        return true;
    }

    private void PlaceBlock(int slotIndex, Vector2Int anchor)
    {
        BlockInstance block = _currentBlocks[slotIndex];
        if (block == null)
            return;

        for (int i = 0; i < block.cells.Count; i++)
        {
            int x = anchor.x + block.cells[i].x;
            int y = anchor.y + block.cells[i].y;

            _myOccupied[x, y] = true;
            _myObstacle[x, y] = false;
            _myColors[x, y] = block.color;
        }

        int clearCount = ClearCompletedLinesAndCollectItems();
        int gainedScore = GetScore(clearCount);
        _myScore += gainedScore;
        _pendingResolveScore += gainedScore;

        _currentBlocks[slotIndex] = null;

        DrawBlockPreview(slotIndex);
        RefreshOwnedItemUI();
        RefreshBoardVisual();
        RefreshSlotVisual();
        RefreshTopHud();

        if (AllBlocksUsed())
            RequestRoundEnd();

        RefreshRoundEndButtonUI();
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
                ToggleReserveAttack(owned.ownedId);
                break;

            case ItemKind.Support:
                UseSupportItemNow(owned.ownedId, owned.itemId);
                break;
        }
    }

    private void ToggleReserveAttack(int ownedId)
    {
        _reservedOutgoingOwnedItemId = _reservedOutgoingOwnedItemId == ownedId ? -1 : ownedId;
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

        int index = FindOwnedItemIndexByOwnedId(_reservedOutgoingOwnedItemId);
        if (index < 0)
        {
            _reservedOutgoingOwnedItemId = -1;
            RefreshOwnedItemUI();
            return;
        }

        BattleItemId attackItem = _ownedItems[index].itemId;
        _ownedItems.RemoveAt(index);
        _reservedOutgoingOwnedItemId = -1;

        if (GetItemKind(attackItem) == ItemKind.Attack && loopbackReservedAttackForDebug)
            _incomingAttackItem = attackItem;

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
                Sprite overlaySprite = null;
                bool showOverlay = false;

                if (_myOccupied[x, y])
                    baseColor = _myObstacle[x, y] ? ObstacleColor : _myColors[x, y];

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

                _myBoardCells[x, y].SetVisual(baseColor, overlaySprite, showOverlay);
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

                _myBoardCells[x, y].SetVisual(previewColor, overlaySprite, showOverlay);
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
        int fillLevel = Mathf.Clamp(8 + _round + (_myScore / 200), 8, 36);

        for (int y = 0; y < BoardSize; y++)
        {
            for (int x = 0; x < BoardSize; x++)
            {
                if (_opponentMiniCells[x, y] == null)
                    continue;

                int hash = (x * 17) + (y * 31) + (_round * 13) + (_myScore / 25);
                bool filled = Mathf.Abs(hash % 64) < fillLevel;
                _opponentMiniCells[x, y].color = filled
                    ? new Color(0.72f, 0.37f, 0.48f, 1f)
                    : BoardBaseColor;
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
                block.color);
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
                new Color(block.color.r, block.color.g, block.color.b, 0.85f));
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

    private void CreatePreviewCell(RectTransform parent, Vector2 size, Vector2 anchoredPosition, Color color)
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
        img.color = color;
        img.raycastTarget = false;
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
        if (_phase != BattlePhase.Play)
            return;

        if (_localRoundReady)
            return;

        bool noPlacement = !HasAnyPlaceableBlock();
        bool allUsed = AllBlocksUsed();
        bool canEnd = noPlacement || allUsed || _phaseTimer <= 0f;

        if (!canEnd)
            return;

        _localRoundReady = true;
        _waitingForOpponentRoundReady = true;

        RefreshRoundEndButtonUI();
        RefreshOwnedItemUI();
        RefreshSlotVisual();

        SendRoundEndReadyToOpponent();
        TryEnterResolveWhenBothReady();
    }



    public void OnOpponentRoundReadyReceived()
    {
        _opponentRoundReady = true;
        RefreshRoundEndButtonUI();
        TryEnterResolveWhenBothReady();
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
        Debug.Log("[BBB] SendRoundEndReadyToOpponent");

        // TODO:
        // 실제 BACKND 대전 연동부에서 여기서 상대에게 "내 라운드 종료" 패킷 전송
        // 상대쪽은 패킷 수신 시 BattleManager.OnOpponentRoundReadyReceived() 호출

        if (debugAutoOpponentRoundReady)
            StartCoroutine(CoDebugAutoOpponentRoundReady());
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

    #endregion

}