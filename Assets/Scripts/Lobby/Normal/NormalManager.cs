using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class NormalManager : MonoBehaviour
{
    private const string HighScoreKey = "BBB_NORMAL_HIGHSCORE";

    [System.Serializable]
    private sealed class SlotRefs
    {
        public RectTransform root;
        public Button button;
        public Image background;
        public RectTransform previewRoot;
        public TMP_Text label;
    }

    [Header("Prefabs / Sprites")]
    [SerializeField] private GameObject boardCellPrefab;
    [SerializeField] private GameObject previewCellPrefab;
    [SerializeField] private BattleBlockSpriteSet blockSprites;

    [Header("Board Size")]
    [SerializeField] private Vector2 myBoardCellSize = new Vector2(64f, 64f);
    [SerializeField] private Vector2 myBoardSpacing = new Vector2(4f, 4f);

    [Header("Preview Size")]
    [SerializeField] private float slotPreviewCellSize = 24f;
    [SerializeField] private float slotPreviewSpacing = 4f;

    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "Scene_Lobby";

    [Header("Score")]
    [SerializeField] private int scorePerPlacedCell = 1;
    [SerializeField] private int scorePerClearedLine = 10;

    [Header("Optional UI (자동 탐색 실패 시 수동 연결)")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private TMP_Text selectedBlockText;
    [SerializeField] private GameObject resultPhaseRoot;
    [SerializeField] private TMP_Text resultScoreText;
    [SerializeField] private TMP_Text resultBestScoreText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button resultLobbyButton;

    private readonly BoardCell[,] _myBoardCells = new BoardCell[BattleBlockCore.BoardSize, BattleBlockCore.BoardSize];
    private readonly bool[,] _myOccupied = new bool[BattleBlockCore.BoardSize, BattleBlockCore.BoardSize];
    private readonly Color[,] _myColors = new Color[BattleBlockCore.BoardSize, BattleBlockCore.BoardSize];
    private readonly Sprite[,] _myBlockSprites = new Sprite[BattleBlockCore.BoardSize, BattleBlockCore.BoardSize];

    private readonly List<BattleBlockShape> _normalShapeLibrary = new List<BattleBlockShape>();
    private readonly List<BattleBlockShape> _curseShapeLibrary = new List<BattleBlockShape>();
    private readonly BattleBlockInstance[] _currentBlocks = new BattleBlockInstance[3];
    private readonly SlotRefs[] _slotRefs = new SlotRefs[3];

    private RectTransform _myBoardRoot;
    private int _selectedSlotIndex = -1;
    private int _myScore;
    private bool _isGameOver;

    private void Awake()
    {
        CacheHierarchy();
        BuildShapeLibrary();
        BuildMyBoard();
        BindButtons();
        StartNewRun();
    }

    private void CacheHierarchy()
    {
        Transform safeArea = FindSafeArea();
        if (safeArea == null)
        {
            Debug.LogError("[NormalManager] SafeArea 못 찾음");
            return;
        }

        if (scoreText == null)
            scoreText = FindTMP(safeArea, "TopHudRoot/MyScorePanel/MyScoreText");

        if (bestScoreText == null)
            bestScoreText = FindTMP(safeArea, "TopHudRoot/BestScorePanel/BestScoreText");

        if (selectedBlockText == null)
            selectedBlockText = FindTMP(safeArea, "TopHudRoot/SelectedBlockPanel/SelectedBlockText");

        if (_myBoardRoot == null)
            _myBoardRoot = FindRect(safeArea, "BoardRoot/MyBoardRoot");

        if (resultPhaseRoot == null)
            resultPhaseRoot = FindGO(safeArea, "ResultPhaseRoot");

        if (resultScoreText == null)
            resultScoreText = FindTMP(safeArea, "ResultPhaseRoot/ResultScoreText");

        if (resultBestScoreText == null)
            resultBestScoreText = FindTMP(safeArea, "ResultPhaseRoot/ResultBestScoreText");

        if (retryButton == null)
            retryButton = FindButton(safeArea, "ResultPhaseRoot/RetryButton");

        if (resultLobbyButton == null)
            resultLobbyButton = FindButton(safeArea, "ResultPhaseRoot/ResultLobbyButton");

        for (int i = 0; i < 3; i++)
        {
            Transform slot = safeArea.Find($"CurrentBlocksRoot/BlockSlot{i + 1}");
            if (slot == null)
                continue;

            SlotRefs refs = new SlotRefs();
            refs.root = slot as RectTransform;
            refs.button = GetOrAdd<Button>(slot.gameObject);
            refs.background = GetOrAdd<Image>(slot.gameObject);

            Transform preview = slot.Find("PreviewRoot");
            if (preview == null)
            {
                GameObject go = new GameObject("PreviewRoot", typeof(RectTransform));
                go.transform.SetParent(slot, false);

                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;

                preview = rt;
            }

            refs.previewRoot = preview as RectTransform;

            Transform labelTr = slot.Find("LabelText");
            if (labelTr == null)
                labelTr = slot.Find("NameText");

            refs.label = labelTr != null ? labelTr.GetComponent<TMP_Text>() : null;

            _slotRefs[i] = refs;
        }
    }

    private void BuildShapeLibrary()
    {
        BattleBlockCore.BuildShapeLibrary(_normalShapeLibrary, _curseShapeLibrary, blockSprites);
    }

    private void BuildMyBoard()
    {
        if (_myBoardRoot == null)
        {
            Debug.LogError("[NormalManager] _myBoardRoot 가 비어있음");
            return;
        }

        DestroyRuntimeChildren(_myBoardRoot);

        float stepX = myBoardCellSize.x + myBoardSpacing.x;
        float stepY = myBoardCellSize.y + myBoardSpacing.y;
        float startX = -((BattleBlockCore.BoardSize - 1) * stepX) * 0.5f;
        float startY = ((BattleBlockCore.BoardSize - 1) * stepY) * 0.5f;

        for (int y = 0; y < BattleBlockCore.BoardSize; y++)
        {
            for (int x = 0; x < BattleBlockCore.BoardSize; x++)
            {
                GameObject go = CreateCellObject(_myBoardRoot);
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = myBoardCellSize;
                rt.anchoredPosition = new Vector2(startX + (x * stepX), startY - (y * stepY));

                BoardCell cell = GetOrAdd<BoardCell>(go);
                Button button = GetOrAdd<Button>(go);

                ColorBlock cb = button.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = Color.white;
                cb.pressedColor = Color.white;
                cb.selectedColor = Color.white;
                cb.disabledColor = Color.white;
                button.colors = cb;

                int capturedX = x;
                int capturedY = y;

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnClickBoardCell(capturedX, capturedY));

                _myBoardCells[x, y] = cell;
                _myOccupied[x, y] = false;
                _myColors[x, y] = Color.clear;
                _myBlockSprites[x, y] = null;
            }
        }

        RefreshBoardVisual();
    }

    private void BindButtons()
    {
        for (int i = 0; i < _slotRefs.Length; i++)
        {
            SlotRefs refs = _slotRefs[i];
            if (refs == null || refs.button == null)
                continue;

            int captured = i;
            refs.button.onClick.RemoveAllListeners();
            refs.button.onClick.AddListener(() => OnClickBlockSlot(captured));
        }

        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(() =>
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            });
        }

        if (resultLobbyButton != null)
        {
            resultLobbyButton.onClick.RemoveAllListeners();
            resultLobbyButton.onClick.AddListener(() =>
            {
                SceneManager.LoadScene(lobbySceneName);
            });
        }
    }

    private void StartNewRun()
    {
        _isGameOver = false;
        _myScore = 0;
        _selectedSlotIndex = -1;

        for (int y = 0; y < BattleBlockCore.BoardSize; y++)
        {
            for (int x = 0; x < BattleBlockCore.BoardSize; x++)
            {
                _myOccupied[x, y] = false;
                _myColors[x, y] = Color.clear;
                _myBlockSprites[x, y] = null;
            }
        }

        if (resultPhaseRoot != null)
            resultPhaseRoot.SetActive(false);

        ResetCurrentBlocks();
        RefreshAllUI();
    }

    private void ResetCurrentBlocks()
    {
        for (int i = 0; i < _currentBlocks.Length; i++)
            _currentBlocks[i] = BattleBlockCore.CreateRandomNormalBlock(_normalShapeLibrary);

        SelectFirstAvailableBlock();
    }

    private void OnClickBlockSlot(int slotIndex)
    {
        if (_isGameOver)
            return;

        if (slotIndex < 0 || slotIndex >= _currentBlocks.Length)
            return;

        if (_currentBlocks[slotIndex] == null)
            return;

        _selectedSlotIndex = slotIndex;
        RefreshBlockSlots();
        RefreshSelectedBlockText();
    }

    private void OnClickBoardCell(int x, int y)
    {
        if (_isGameOver)
            return;

        if (_selectedSlotIndex < 0 || _selectedSlotIndex >= _currentBlocks.Length)
            return;

        BattleBlockInstance block = _currentBlocks[_selectedSlotIndex];
        if (block == null)
            return;

        if (!BattleBlockCore.CanPlaceBlock(block, _myOccupied, x, y))
            return;

        BattleBlockCore.PlaceBlock(block, _myOccupied, _myColors, _myBlockSprites, x, y);
        _myScore += block.CellCount * scorePerPlacedCell;
        _currentBlocks[_selectedSlotIndex] = null;

        int cleared = BattleBlockCore.ClearCompletedLines(_myOccupied, _myColors, _myBlockSprites);
        if (cleared > 0)
            _myScore += cleared * scorePerClearedLine;

        if (AreAllBlockSlotsEmpty())
            ResetCurrentBlocks();
        else
            SelectFirstAvailableBlock();

        RefreshAllUI();

        if (!BattleBlockCore.HasAnyPlaceableMove(_currentBlocks, _myOccupied))
            ShowGameOver();
    }

    private bool AreAllBlockSlotsEmpty()
    {
        for (int i = 0; i < _currentBlocks.Length; i++)
        {
            if (_currentBlocks[i] != null)
                return false;
        }

        return true;
    }

    private void SelectFirstAvailableBlock()
    {
        _selectedSlotIndex = -1;

        for (int i = 0; i < _currentBlocks.Length; i++)
        {
            if (_currentBlocks[i] != null)
            {
                _selectedSlotIndex = i;
                return;
            }
        }
    }

    private void ShowGameOver()
    {
        _isGameOver = true;

        int best = SaveBestScore(_myScore);

        if (resultPhaseRoot != null)
            resultPhaseRoot.SetActive(true);

        if (resultScoreText != null)
            resultScoreText.text = $"점수 : {_myScore}";

        if (resultBestScoreText != null)
            resultBestScoreText.text = $"최고점수 : {best}";
    }

    private void RefreshAllUI()
    {
        RefreshBoardVisual();
        RefreshBlockSlots();
        RefreshScoreUI();
        RefreshSelectedBlockText();
    }

    private void RefreshBoardVisual()
    {
        for (int y = 0; y < BattleBlockCore.BoardSize; y++)
        {
            for (int x = 0; x < BattleBlockCore.BoardSize; x++)
            {
                BoardCell cell = _myBoardCells[x, y];
                if (cell == null)
                    continue;

                bool occupied = _myOccupied[x, y];
                Color baseColor = occupied ? _myColors[x, y] : BattleBlockCore.BoardBaseColor;
                Sprite sprite = occupied ? _myBlockSprites[x, y] : null;

                cell.SetVisual(baseColor, sprite, occupied, null, false);
            }
        }
    }

    private void RefreshBlockSlots()
    {
        for (int i = 0; i < _slotRefs.Length; i++)
        {
            SlotRefs refs = _slotRefs[i];
            if (refs == null)
                continue;

            BattleBlockInstance block = _currentBlocks[i];
            bool hasBlock = block != null;
            bool selected = hasBlock && i == _selectedSlotIndex;

            if (refs.button != null)
                refs.button.interactable = hasBlock && !_isGameOver;

            if (refs.background != null)
                refs.background.color = selected ? new Color32(255, 233, 135, 255) : Color.white;

            if (refs.label != null)
                refs.label.text = hasBlock ? $"{block.shapeId} / {block.rotation * 90}°" : "(빈 슬롯)";

            RebuildSlotPreview(refs.previewRoot, block);
        }
    }

    private void RebuildSlotPreview(RectTransform previewRoot, BattleBlockInstance block)
    {
        if (previewRoot == null)
            return;

        DestroyRuntimeChildren(previewRoot);

        if (block == null || block.cells == null || block.cells.Count == 0)
            return;

        int maxX = 0;
        int maxY = 0;

        for (int i = 0; i < block.cells.Count; i++)
        {
            if (block.cells[i].x > maxX) maxX = block.cells[i].x;
            if (block.cells[i].y > maxY) maxY = block.cells[i].y;
        }

        float stepX = slotPreviewCellSize + slotPreviewSpacing;
        float stepY = slotPreviewCellSize + slotPreviewSpacing;
        float startX = -(maxX * stepX) * 0.5f;
        float startY = (maxY * stepY) * 0.5f;

        for (int i = 0; i < block.cells.Count; i++)
        {
            GameObject go = CreatePreviewCellObject(previewRoot);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(slotPreviewCellSize, slotPreviewCellSize);
            rt.anchoredPosition = new Vector2(
                startX + (block.cells[i].x * stepX),
                startY - (block.cells[i].y * stepY));

            Image img = GetOrAdd<Image>(go);
            img.color = block.color;
            img.sprite = block.cellSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.raycastTarget = false;
        }
    }

    private void RefreshScoreUI()
    {
        if (scoreText != null)
            scoreText.text = $"점수 : {_myScore}";

        if (bestScoreText != null)
            bestScoreText.text = $"최고 : {GetBestScore()}";
    }

    private void RefreshSelectedBlockText()
    {
        if (selectedBlockText == null)
            return;

        if (_isGameOver)
        {
            selectedBlockText.text = "게임 오버";
            return;
        }

        if (_selectedSlotIndex < 0 || _selectedSlotIndex >= _currentBlocks.Length || _currentBlocks[_selectedSlotIndex] == null)
        {
            selectedBlockText.text = "블록을 선택하세요";
            return;
        }

        BattleBlockInstance block = _currentBlocks[_selectedSlotIndex];
        selectedBlockText.text = $"선택 블록 : {block.shapeId} / {block.rotation * 90}°";
    }

    private static int GetBestScore()
    {
        return PlayerPrefs.GetInt(HighScoreKey, 0);
    }

    private static int SaveBestScore(int value)
    {
        int best = GetBestScore();

        if (value > best)
        {
            best = value;
            PlayerPrefs.SetInt(HighScoreKey, best);
            PlayerPrefs.Save();
        }

        return best;
    }

    private Transform FindSafeArea()
    {
        if (transform.parent != null && transform.parent.name == "SafeArea")
            return transform.parent;

        GameObject safeAreaGo = GameObject.Find("SafeArea");
        return safeAreaGo != null ? safeAreaGo.transform : null;
    }

    private static RectTransform FindRect(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t as RectTransform : null;
    }

    private static TMP_Text FindTMP(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private static Button FindButton(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private static GameObject FindGO(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.gameObject : null;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        if (c == null)
            c = go.AddComponent<T>();

        return c;
    }

    private GameObject CreateCellObject(Transform parent)
    {
        GameObject go;

        if (boardCellPrefab != null)
            go = Instantiate(boardCellPrefab, parent);
        else
            go = new GameObject("BoardCell", typeof(RectTransform), typeof(Image), typeof(BoardCell), typeof(Button));

        go.name = "BoardCell";
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null)
            rt = go.AddComponent<RectTransform>();

        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;

        return go;
    }

    private GameObject CreatePreviewCellObject(Transform parent)
    {
        GameObject go;

        if (previewCellPrefab != null)
            go = Instantiate(previewCellPrefab, parent);
        else
            go = new GameObject("PreviewCell", typeof(RectTransform), typeof(Image));

        go.name = "PreviewCell";

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null)
            rt = go.AddComponent<RectTransform>();

        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;

        return go;
    }

    private static void DestroyRuntimeChildren(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
            Object.Destroy(root.GetChild(i).gameObject);
    }
}