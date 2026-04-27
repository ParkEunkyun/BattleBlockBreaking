using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LobbyNormalArtifactController : MonoBehaviour
{
    private enum ArtifactSortMode
    {
        GradeDesc = 0,
        GradeAsc = 1,
        LevelDesc = 2,
        LevelAsc = 3,
        NameAsc = 4
    }

    private sealed class ArtifactCardUI
    {
        public NormalArtifactDefinition Def;
        public GameObject Root;
        public Image GradeFrame;
        public Image IconImage;
        public TMP_Text NameText;
        public TMP_Text GradeText;
        public TMP_Text LevelText;
        public Image SelectOverlay;
        public GameObject EquippedMark;
        public Button Button;
        public Button ManageButton;
        public CanvasGroup CanvasGroup;
        public NormalArtifactPressHandler PressHandler;
    }

    private sealed class SlotUI
    {
        public GameObject Root;
        public Image FrameImage;
        public Image IconImage;
        public TMP_Text LevelText;
        public GameObject EmptyIndicator;
        public Button RemoveButton;
    }

    [Header("Artifact Catalog")]
    [SerializeField] private List<NormalArtifactDefinition> artifactCatalog = new List<NormalArtifactDefinition>();

    [Header("Popup Card List")]
    [SerializeField] private GameObject artifactCardPrefab;
    [SerializeField] private Transform cardListRoot;

    [Header("Sort")]
    [SerializeField] private TMP_Dropdown sortDropdown;
    [SerializeField] private ArtifactSortMode defaultSortMode = ArtifactSortMode.GradeDesc;

    [Header("Popup Selected Slots")]
    [SerializeField] private List<Transform> selectedSlotRoots = new List<Transform>();

    [Header("Preview Slots")]
    [SerializeField] private List<Transform> previewSlotRoots = new List<Transform>();

    [Header("Popup Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private Button closePopupButton;

    [Header("Visible Roots")]
    [SerializeField] private GameObject normalModeRoot;
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private CanvasGroup popupCanvasGroup;

    [Header("Popup Tooltip")]
    [SerializeField] private NormalArtifactTooltipView popupTooltipView;

    [Header("Management Screen")]
    [SerializeField] private GameObject managementRoot;
    [SerializeField] private NormalArtifactManagementView managementView;
    [SerializeField] private Button closeManagementButton;

    [Header("Press")]
    [SerializeField] private float longPressSeconds = 0.45f;

    [Header("Grade Colors")]
    [SerializeField] private Color colorNormal = new Color(0.72f, 0.72f, 0.72f, 1f);
    [SerializeField] private Color colorRare = new Color(0.30f, 0.64f, 1.00f, 1f);
    [SerializeField] private Color colorEpic = new Color(0.61f, 0.36f, 1.00f, 1f);
    [SerializeField] private Color colorUnique = new Color(1.00f, 0.36f, 0.54f, 1f);
    [SerializeField] private Color colorLegend = new Color(1.00f, 0.82f, 0.30f, 1f);

    private readonly List<ArtifactCardUI> _cards = new List<ArtifactCardUI>();
    private readonly List<SlotUI> _popupSlots = new List<SlotUI>();
    private readonly List<SlotUI> _previewSlots = new List<SlotUI>();

    private readonly List<NormalArtifactDefinition> _ownedDefs = new List<NormalArtifactDefinition>(32);
    private readonly List<NormalArtifactDefinition> _equippedDefs = new List<NormalArtifactDefinition>(4);
    private readonly List<NormalArtifactDefinition> _editingDefs = new List<NormalArtifactDefinition>(4);

    private bool _initialized;
    private bool _popupOpen;

    private NormalArtifactDefinition _manageButtonTarget;
    private ArtifactSortMode _currentSortMode;

    private const int MaxEquipCount = 4;

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        _currentSortMode = defaultSortMode;

        BuildPopupSlots();
        BuildPreviewSlots();
        BindSortDropdown();
        BindButtons();

        LoadOwnedAndEquippedFromStore();

        RefreshAllUI();
        SetPopupVisible(false);
        SetManagementVisible(false);

        _popupOpen = false;
    }

    public void SetVisible(bool visible)
    {
        if (normalModeRoot != null)
            normalModeRoot.SetActive(visible);

        if (!visible)
        {
            ClosePopup();
            CloseManagementScreen();
            return;
        }

        LoadOwnedAndEquippedFromStore();
        RefreshAllUI();
    }

    public void OpenPopup()
    {
        Initialize();

        LoadOwnedAndEquippedFromStore();
        CopyList(_equippedDefs, _editingDefs);

        _popupOpen = true;
        _manageButtonTarget = null;

        HidePopupTooltip();

        SetManagementVisible(false);
        SetPopupVisible(true);

        RebuildArtifactCards();
        RefreshAllUI();

        Debug.Log($"[LobbyNormalArtifactController] OpenPopup / Owned={_ownedDefs.Count}, Equipped={_equippedDefs.Count}");
    }

    public void ClosePopup()
    {
        _popupOpen = false;
        _manageButtonTarget = null;

        HidePopupTooltip();

        CopyList(_equippedDefs, _editingDefs);

        SetPopupVisible(false);
        SetManagementVisible(false);
        RefreshAllUI();
    }

    public bool HasValidSelection()
    {
        LoadOwnedAndEquippedFromStore();
        return _equippedDefs.Count > 0;
    }

    public void ApplyToSession()
    {
        LoadOwnedAndEquippedFromStore();
        NormalArtifactSession.Set(_equippedDefs);

        Debug.Log(
            $"[LobbyNormalArtifactController] ApplyToSession / " +
            $"Catalog={artifactCatalog.Count}, Owned={NormalArtifactOwnershipStore.GetOwnedCount()}, Equipped={_equippedDefs.Count}"
        );
    }
    public void SetCatalog(IReadOnlyList<NormalArtifactDefinition> catalog)
    {
        // 핵심:
        // Controller 인스펙터에 이미 Artifact Catalog가 들어있으면
        // LobbyManager에서 넘어온 catalog로 덮어쓰지 않는다.
        // 덮어쓰면 저장된 Equipped ID를 Definition으로 복구 못 해서 Equipped=0이 됨.
        if (artifactCatalog.Count > 0)
        {
            LoadOwnedAndEquippedFromStore();

            if (_popupOpen)
                RebuildArtifactCards();

            RefreshAllUI();

            Debug.Log(
                $"[LobbyNormalArtifactController] SetCatalog 무시 / 기존 Catalog 사용 / " +
                $"Catalog={artifactCatalog.Count}, Owned={NormalArtifactOwnershipStore.GetOwnedCount()}, Equipped={_equippedDefs.Count}"
            );

            return;
        }

        if (catalog == null || catalog.Count <= 0)
        {
            Debug.LogWarning("[LobbyNormalArtifactController] SetCatalog 실패 / 기존 Catalog도 없고 전달된 catalog도 비어 있음");
            return;
        }

        artifactCatalog.Clear();

        for (int i = 0; i < catalog.Count; i++)
        {
            NormalArtifactDefinition def = catalog[i];

            if (def == null)
                continue;

            if (string.IsNullOrWhiteSpace(def.artifactId))
            {
                Debug.LogWarning($"[LobbyNormalArtifactController] artifactId 비어 있음: {def.name}");
                continue;
            }

            if (artifactCatalog.Contains(def))
                continue;

            artifactCatalog.Add(def);
        }

        LoadOwnedAndEquippedFromStore();

        if (_popupOpen)
            RebuildArtifactCards();

        RefreshAllUI();

        Debug.Log(
            $"[LobbyNormalArtifactController] SetCatalog 완료 / " +
            $"Catalog={artifactCatalog.Count}, Owned={NormalArtifactOwnershipStore.GetOwnedCount()}, Equipped={_equippedDefs.Count}"
        );
    }

    public void ForceReloadFromStoreAndRefresh()
    {
        LoadOwnedAndEquippedFromStore();

        if (_popupOpen)
            RebuildArtifactCards();

        RefreshAllUI();
    }
    public void RefreshPreviewOnly()
    {
        RefreshPreviewSlots();
    }
    public int EquippedCount => _equippedDefs.Count;

    public void RefreshOwnedAndPreview()
    {
        LoadOwnedAndEquippedFromStore();

        if (_popupOpen)
            RebuildArtifactCards();

        RefreshAllUI();
    }

    public void GrantOwnedFromGacha(NormalArtifactDefinition def)
    {
        if (def == null)
            return;

        NormalArtifactOwnershipStore.AddOwned(def);
        RefreshOwnedAndPreview();
    }

    private void LoadOwnedAndEquippedFromStore()
    {
        _ownedDefs.Clear();

        List<NormalArtifactDefinition> owned = NormalArtifactOwnershipStore.GetOwnedDefinitions(artifactCatalog);

        for (int i = 0; i < owned.Count; i++)
        {
            NormalArtifactDefinition def = owned[i];

            if (def == null)
                continue;

            if (_ownedDefs.Contains(def))
                continue;

            _ownedDefs.Add(def);
        }

        _equippedDefs.Clear();

        List<NormalArtifactDefinition> equipped = NormalArtifactOwnershipStore.LoadEquippedDefinitions(artifactCatalog);

        for (int i = 0; i < equipped.Count && _equippedDefs.Count < MaxEquipCount; i++)
        {
            NormalArtifactDefinition def = equipped[i];

            if (def == null)
                continue;

            if (!_ownedDefs.Contains(def))
                continue;

            if (_equippedDefs.Contains(def))
                continue;

            _equippedDefs.Add(def);
        }

        NormalArtifactSession.Set(_equippedDefs);
    }

    private void BuildPopupSlots()
    {
        _popupSlots.Clear();

        for (int i = 0; i < selectedSlotRoots.Count; i++)
        {
            Transform tr = selectedSlotRoots[i];
            if (tr == null)
                continue;

            SlotUI slot = CreateSlotUI(tr);
            int capturedIndex = i;

            if (slot.RemoveButton != null)
            {
                slot.RemoveButton.onClick.RemoveAllListeners();
                slot.RemoveButton.onClick.AddListener(() => OnClickRemoveEditingSlot(capturedIndex));
            }

            _popupSlots.Add(slot);
        }
    }

    private void BuildPreviewSlots()
    {
        _previewSlots.Clear();

        for (int i = 0; i < previewSlotRoots.Count; i++)
        {
            Transform tr = previewSlotRoots[i];
            if (tr == null)
                continue;

            SlotUI slot = CreateSlotUI(tr);

            if (slot.RemoveButton != null)
            {
                slot.RemoveButton.onClick.RemoveAllListeners();
                slot.RemoveButton.gameObject.SetActive(false);
            }

            _previewSlots.Add(slot);
        }
    }

    private static SlotUI CreateSlotUI(Transform tr)
    {
        if (tr == null)
            return null;

        Image frameImage = null;

        Transform gradeFrameTr = tr.Find("GradeFrame");
        if (gradeFrameTr != null)
            frameImage = gradeFrameTr.GetComponent<Image>();

        if (frameImage == null)
            frameImage = tr.GetComponent<Image>();

        Image iconImage = FindComp<Image>(tr, "Icon");

        if (iconImage == null)
            iconImage = FindComp<Image>(tr, "IconImage");

        TMP_Text levelText = FindTMP(tr, "LevelText");

        GameObject emptyIndicator = null;
        Transform emptyTr = tr.Find("EmptyIndicator");

        if (emptyTr != null)
            emptyIndicator = emptyTr.gameObject;

        Button removeButton = FindComp<Button>(tr, "RemoveButton");

        return new SlotUI
        {
            Root = tr.gameObject,
            FrameImage = frameImage,
            IconImage = iconImage,
            LevelText = levelText,
            EmptyIndicator = emptyIndicator,
            RemoveButton = removeButton
        };
    }

    private void BindSortDropdown()
    {
        if (sortDropdown == null)
            return;

        sortDropdown.onValueChanged.RemoveAllListeners();

        sortDropdown.ClearOptions();
        sortDropdown.AddOptions(new List<string>
        {
            "등급 높은순",
            "등급 낮은순",
            "레벨 높은순",
            "레벨 낮은순",
            "이름순"
        });

        sortDropdown.value = (int)_currentSortMode;
        sortDropdown.RefreshShownValue();

        sortDropdown.onValueChanged.AddListener(OnChangeSortMode);
    }

    private void OnChangeSortMode(int value)
    {
        _currentSortMode = (ArtifactSortMode)Mathf.Clamp(value, 0, 4);
        _manageButtonTarget = null;

        if (_popupOpen)
            RebuildArtifactCards();

        RefreshAllUI();
    }

    private void RebuildArtifactCards()
    {
        if (cardListRoot == null || artifactCardPrefab == null)
            return;

        for (int i = cardListRoot.childCount - 1; i >= 0; i--)
            Destroy(cardListRoot.GetChild(i).gameObject);

        _cards.Clear();

        // 소유한 아티팩트만 정렬
        SortArtifacts(_ownedDefs);

        // 장착/선택 중인 아티팩트는 맨 앞에 표시
        for (int i = 0; i < _ownedDefs.Count; i++)
        {
            NormalArtifactDefinition def = _ownedDefs[i];

            if (def == null)
                continue;

            if (!_editingDefs.Contains(def))
                continue;

            CreateArtifactCard(def);
        }

        // 나머지 소유 아티팩트 표시
        for (int i = 0; i < _ownedDefs.Count; i++)
        {
            NormalArtifactDefinition def = _ownedDefs[i];

            if (def == null)
                continue;

            if (_editingDefs.Contains(def))
                continue;

            CreateArtifactCard(def);
        }

        RefreshCardOverlays();

        Debug.Log($"[LobbyNormalArtifactController] Rebuild Cards / Owned={_ownedDefs.Count}, Equipped={_equippedDefs.Count}, Editing={_editingDefs.Count}");
    }

    private void SortArtifacts(List<NormalArtifactDefinition> list)
    {
        if (list == null)
            return;

        list.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int levelA = NormalArtifactLevelUtility.GetLevel(a);
            int levelB = NormalArtifactLevelUtility.GetLevel(b);

            int gradeA = (int)a.grade;
            int gradeB = (int)b.grade;

            switch (_currentSortMode)
            {
                case ArtifactSortMode.GradeDesc:
                    {
                        int gradeCompare = gradeB.CompareTo(gradeA);
                        if (gradeCompare != 0) return gradeCompare;

                        int levelCompare = levelB.CompareTo(levelA);
                        if (levelCompare != 0) return levelCompare;

                        return string.Compare(a.DisplayNameSafe, b.DisplayNameSafe, System.StringComparison.Ordinal);
                    }

                case ArtifactSortMode.GradeAsc:
                    {
                        int gradeCompare = gradeA.CompareTo(gradeB);
                        if (gradeCompare != 0) return gradeCompare;

                        int levelCompare = levelB.CompareTo(levelA);
                        if (levelCompare != 0) return levelCompare;

                        return string.Compare(a.DisplayNameSafe, b.DisplayNameSafe, System.StringComparison.Ordinal);
                    }

                case ArtifactSortMode.LevelDesc:
                    {
                        int levelCompare = levelB.CompareTo(levelA);
                        if (levelCompare != 0) return levelCompare;

                        int gradeCompare = gradeB.CompareTo(gradeA);
                        if (gradeCompare != 0) return gradeCompare;

                        return string.Compare(a.DisplayNameSafe, b.DisplayNameSafe, System.StringComparison.Ordinal);
                    }

                case ArtifactSortMode.LevelAsc:
                    {
                        int levelCompare = levelA.CompareTo(levelB);
                        if (levelCompare != 0) return levelCompare;

                        int gradeCompare = gradeB.CompareTo(gradeA);
                        if (gradeCompare != 0) return gradeCompare;

                        return string.Compare(a.DisplayNameSafe, b.DisplayNameSafe, System.StringComparison.Ordinal);
                    }

                case ArtifactSortMode.NameAsc:
                default:
                    return string.Compare(a.DisplayNameSafe, b.DisplayNameSafe, System.StringComparison.Ordinal);
            }
        });
    }

    private void CreateArtifactCard(NormalArtifactDefinition def)
    {
        GameObject go = Instantiate(artifactCardPrefab, cardListRoot);
        go.name = $"ArtifactCard_{def.artifactId}";

        Button button = go.GetComponent<Button>();
        if (button == null)
            button = go.AddComponent<Button>();

        button.onClick.RemoveAllListeners();
        button.enabled = false;

        Image rootImage = go.GetComponent<Image>();
        if (rootImage == null)
            rootImage = go.AddComponent<Image>();

        rootImage.raycastTarget = true;

        CanvasGroup canvasGroup = go.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = go.AddComponent<CanvasGroup>();

        NormalArtifactPressHandler pressHandler = go.GetComponent<NormalArtifactPressHandler>();
        if (pressHandler == null)
            pressHandler = go.AddComponent<NormalArtifactPressHandler>();

        ArtifactCardUI card = new ArtifactCardUI
        {
            Def = def,
            Root = go,
            GradeFrame = FindComp<Image>(go.transform, "GradeFrame"),
            IconImage = FindComp<Image>(go.transform, "IconImage"),
            LevelText = FindTMP(go.transform, "LevelText"),
            SelectOverlay = FindComp<Image>(go.transform, "SelectedFrame"),
            EquippedMark = FindChildObject(go.transform, "EquippedMark"),
            Button = button,
            ManageButton = FindComp<Button>(go.transform, "ManageButton"),
            CanvasGroup = canvasGroup,
            PressHandler = pressHandler
        };

        SetRaycastTargetRecursive(go.transform, false);
        rootImage.raycastTarget = true;

        if (card.GradeFrame != null)
        {
            card.GradeFrame.color = GradeToColor(def.grade);
            card.GradeFrame.raycastTarget = false;
        }

        if (card.IconImage != null)
        {
            card.IconImage.sprite = def.icon;
            card.IconImage.enabled = def.icon != null;
            card.IconImage.preserveAspect = true;
            card.IconImage.raycastTarget = false;
        }

        if (card.LevelText != null)
        {
            card.LevelText.text = $"Lv.{NormalArtifactLevelUtility.GetLevel(def)}";
            card.LevelText.raycastTarget = false;
        }

        if (card.SelectOverlay != null)
        {
            card.SelectOverlay.gameObject.SetActive(false);
            card.SelectOverlay.raycastTarget = false;
        }

        if (card.EquippedMark != null)
            card.EquippedMark.SetActive(false);

        NormalArtifactDefinition capturedDef = def;

        pressHandler.Bind(
    () => OnClickArtifactCard(capturedDef),
    () => ShowPopupTooltip(capturedDef),
    HidePopupTooltip,
    longPressSeconds);

        // 카드 내부 ManageButton은 더 이상 사용하지 않음.
        // 프리팹에 남아 있어도 무조건 숨김 처리.
        if (card.ManageButton != null)
        {
            card.ManageButton.onClick.RemoveAllListeners();
            card.ManageButton.gameObject.SetActive(false);
        }

        _cards.Add(card);
    }

    private void BindButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnClickConfirmPopup);
        }

        if (settingButton != null)
        {
            settingButton.onClick.RemoveAllListeners();
            settingButton.onClick.AddListener(OnClickSettingButton);
            settingButton.interactable = true;
        }

        if (closePopupButton != null)
        {
            closePopupButton.onClick.RemoveAllListeners();
            closePopupButton.onClick.AddListener(ClosePopup);
        }

        if (closeManagementButton != null)
        {
            closeManagementButton.onClick.RemoveAllListeners();
            closeManagementButton.onClick.AddListener(CloseManagementScreen);
        }
    }

    private void OnClickArtifactCard(NormalArtifactDefinition def)
    {
        if (!_popupOpen)
            return;

        if (def == null)
            return;

        HidePopupTooltip();

        _manageButtonTarget = null;

        int existingIndex = _editingDefs.IndexOf(def);

        if (existingIndex >= 0)
        {
            _editingDefs.RemoveAt(existingIndex);
            RefreshAllUI();
            return;
        }

        if (_editingDefs.Count >= MaxEquipCount)
        {
            RefreshAllUI();
            return;
        }

        _editingDefs.Add(def);
        RefreshAllUI();
    }

    private void OnLongPressArtifactCard(NormalArtifactDefinition def)
    {
        ShowPopupTooltip(def);
    }

    private void ShowPopupTooltip(NormalArtifactDefinition def)
    {
        if (!_popupOpen)
            return;

        if (def == null)
            return;

        if (popupTooltipView == null && popupRoot != null)
            popupTooltipView = popupRoot.GetComponentInChildren<NormalArtifactTooltipView>(true);

        if (popupTooltipView == null)
        {
            Debug.LogWarning("[LobbyNormalArtifactController] Popup Tooltip View 연결 안 됨");
            return;
        }

        int level = NormalArtifactLevelUtility.GetLevel(def);
        popupTooltipView.Show(def, level);
    }

    private void HidePopupTooltip()
    {
        if (popupTooltipView == null && popupRoot != null)
            popupTooltipView = popupRoot.GetComponentInChildren<NormalArtifactTooltipView>(true);

        if (popupTooltipView != null)
            popupTooltipView.Hide();
    }

    private void OnClickSettingButton()
    {
        HidePopupTooltip();

        _manageButtonTarget = null;

        // Setting은 특정 카드 포커스와 무관하게 관리 화면으로 이동
        SetPopupVisible(false);
        SetManagementVisible(true);

        // 현재 ManagementView가 "특정 아티팩트 상세" 구조라면,
        // 일단 소유 목록의 첫 번째 아티팩트를 기본 상세로 보여준다.
        // 이후 관리화면에 목록 UI를 붙이면 여기만 관리화면 목록 오픈으로 바꾸면 됨.
        if (managementView != null && _ownedDefs.Count > 0)
        {
            NormalArtifactDefinition firstDef = _ownedDefs[0];

            managementView.Show(
                firstDef,
                NormalArtifactLevelUtility.GetLevel(firstDef),
                _editingDefs.Contains(firstDef),
                CloseManagementScreen);
        }

        RefreshAllUI();
    }
    private void OnClickRemoveEditingSlot(int slotIndex)
    {
        if (!_popupOpen)
            return;

        if (slotIndex < 0 || slotIndex >= _editingDefs.Count)
            return;

        _editingDefs.RemoveAt(slotIndex);
        RefreshAllUI();
    }

    private void OnClickConfirmPopup()
    {
        CopyList(_editingDefs, _equippedDefs);

        if (_equippedDefs.Count <= 0)
            NormalArtifactOwnershipStore.ClearEquippedDefinitions();
        else
            NormalArtifactOwnershipStore.SaveEquippedDefinitions(_equippedDefs, true);

        // 저장 직후 세션 즉시 반영
        NormalArtifactSession.Set(_equippedDefs);

        // 저장소 기준으로 다시 동기화
        LoadOwnedAndEquippedFromStore();

        _popupOpen = false;
        _manageButtonTarget = null;

        // 핵심: 팝업 닫기 전에 프리뷰/카드 상태 먼저 즉시 갱신
        RefreshPreviewSlots();
        RefreshPopupSlots();
        RefreshCardOverlays();

        SetPopupVisible(false);
        SetManagementVisible(false);

        // 마지막 전체 갱신
        RefreshAllUI();

        Debug.Log($"[LobbyNormalArtifactController] 장착 저장 완료 / Owned={NormalArtifactOwnershipStore.GetOwnedCount()}, Equipped={_equippedDefs.Count}");
    }

    private void OpenManagementScreen(NormalArtifactDefinition def)
    {
        if (def == null)
            return;

        _manageButtonTarget = null;

        SetPopupVisible(false);
        SetManagementVisible(true);

        if (managementView != null)
        {
            managementView.Show(
                def,
                NormalArtifactLevelUtility.GetLevel(def),
                _editingDefs.Contains(def),
                CloseManagementScreen);
        }

        RefreshAllUI();
    }

    private void CloseManagementScreen()
    {
        SetManagementVisible(false);

        if (_popupOpen)
            SetPopupVisible(true);

        RefreshAllUI();
    }

    private void RefreshAllUI()
    {
        RefreshCardOverlays();
        RefreshPopupSlots();
        RefreshPreviewSlots();
        RefreshButtons();
    }

    private void RefreshCardOverlays()
    {
        bool isFull = _editingDefs.Count >= MaxEquipCount;

        for (int i = 0; i < _cards.Count; i++)
        {
            ArtifactCardUI card = _cards[i];
            if (card == null)
                continue;

            bool selected = _editingDefs.Contains(card.Def);

            if (card.SelectOverlay != null)
                card.SelectOverlay.gameObject.SetActive(selected);

            if (card.EquippedMark != null)
                card.EquippedMark.SetActive(selected);

            if (card.GradeFrame != null && card.Def != null)
                card.GradeFrame.color = GradeToColor(card.Def.grade);

            if (card.LevelText != null)
                card.LevelText.text = $"Lv.{NormalArtifactLevelUtility.GetLevel(card.Def)}";

            if (card.Button != null)
                card.Button.interactable = _popupOpen && (selected || !isFull);

            // 카드 내부 ManageButton은 더 이상 사용하지 않음
            if (card.ManageButton != null)
                card.ManageButton.gameObject.SetActive(false);

            if (card.CanvasGroup != null)
            {
                if (!_popupOpen)
                    card.CanvasGroup.alpha = 1f;
                else
                    card.CanvasGroup.alpha = (!selected && isFull) ? 0.45f : 1f;

                card.CanvasGroup.blocksRaycasts = true;
                card.CanvasGroup.interactable = true;
            }
        }
    }

    private void RefreshPopupSlots()
    {
        for (int i = 0; i < _popupSlots.Count; i++)
            RefreshSingleSlot(_popupSlots[i], _editingDefs, i, true);
    }

    private void RefreshPreviewSlots()
    {
        for (int i = 0; i < _previewSlots.Count; i++)
            RefreshSingleSlot(_previewSlots[i], _equippedDefs, i, false);
    }

    private void RefreshSingleSlot(
    SlotUI slot,
    List<NormalArtifactDefinition> source,
    int index,
    bool allowRemove)
    {
        if (slot == null)
            return;

        bool hasItem = source != null && index < source.Count;
        NormalArtifactDefinition def = hasItem ? source[index] : null;

        if (slot.FrameImage != null)
        {
            // 등급 표현은 Sprite 교체 금지, Color만 변경
            slot.FrameImage.color = GetArtifactGradeColor(def);
        }

        if (slot.IconImage != null)
        {
            slot.IconImage.gameObject.SetActive(hasItem);

            if (hasItem && def != null)
            {
                slot.IconImage.sprite = def.icon;
                slot.IconImage.enabled = def.icon != null;
                slot.IconImage.preserveAspect = true;
                slot.IconImage.color = Color.white;
            }
            else
            {
                slot.IconImage.sprite = null;
                slot.IconImage.enabled = false;
            }
        }

        if (slot.LevelText != null)
        {
            slot.LevelText.gameObject.SetActive(hasItem);
            slot.LevelText.text = hasItem && def != null
                ? $"Lv.{NormalArtifactLevelUtility.GetLevel(def)}"
                : string.Empty;
        }

        if (slot.EmptyIndicator != null)
            slot.EmptyIndicator.SetActive(!hasItem);

        if (slot.RemoveButton != null)
            slot.RemoveButton.gameObject.SetActive(allowRemove && hasItem);
    }

    private void RefreshButtons()
    {
        if (startButton != null)
            startButton.interactable = true;

        if (settingButton != null)
            settingButton.interactable = true;

        if (closePopupButton != null)
            closePopupButton.interactable = true;
    }

    private void SetPopupVisible(bool visible)
    {
        if (popupRoot != null)
            popupRoot.SetActive(visible);

        if (popupCanvasGroup != null)
        {
            popupCanvasGroup.alpha = visible ? 1f : 0f;
            popupCanvasGroup.interactable = visible;
            popupCanvasGroup.blocksRaycasts = visible;
        }
    }

    private void SetManagementVisible(bool visible)
    {
        if (managementRoot != null)
            managementRoot.SetActive(visible);

        if (!visible && managementView != null)
            managementView.Hide();
    }

    private static void CopyList(List<NormalArtifactDefinition> src, List<NormalArtifactDefinition> dst)
    {
        dst.Clear();

        for (int i = 0; i < src.Count && i < MaxEquipCount; i++)
        {
            NormalArtifactDefinition def = src[i];

            if (def == null)
                continue;

            if (dst.Contains(def))
                continue;

            dst.Add(def);
        }
    }

    private static void SetRaycastTargetRecursive(Transform root, bool value)
    {
        if (root == null)
            return;

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = value;
    }

    private static void SetButtonRaycast(Button button, bool value)
    {
        if (button == null)
            return;

        Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        Graphic targetGraphic = button.targetGraphic;

        if (targetGraphic != null)
            targetGraphic.raycastTarget = value;
        else
        {
            Image image = button.GetComponent<Image>();
            if (image != null)
                image.raycastTarget = value;
        }

        button.enabled = true;
        button.interactable = true;
    }

    private static GameObject FindChildObject(Transform root, string path)
    {
        Transform tr = root != null ? root.Find(path) : null;
        return tr != null ? tr.gameObject : null;
    }

    private static TMP_Text FindTMP(Transform root, string path)
    {
        Transform tr = root != null ? root.Find(path) : null;
        return tr != null ? tr.GetComponent<TMP_Text>() : null;
    }

    private static T FindComp<T>(Transform root, string path) where T : Component
    {
        Transform tr = root != null ? root.Find(path) : null;
        return tr != null ? tr.GetComponent<T>() : null;
    }

    private Color GradeToColor(ArtifactGrade grade)
    {
        switch (grade)
        {
            case ArtifactGrade.Normal: return colorNormal;
            case ArtifactGrade.Rare: return colorRare;
            case ArtifactGrade.Epic: return colorEpic;
            case ArtifactGrade.Unique: return colorUnique;
            case ArtifactGrade.Legend: return colorLegend;
            default: return Color.white;
        }
    }
    private static string GradeToString(ArtifactGrade grade)
    {
        switch (grade)
        {
            case ArtifactGrade.Normal: return "노말";
            case ArtifactGrade.Rare: return "레어";
            case ArtifactGrade.Epic: return "에픽";
            case ArtifactGrade.Unique: return "유니크";
            case ArtifactGrade.Legend: return "전설";
            default: return string.Empty;
        }
    }
    private Color GetArtifactGradeColor(NormalArtifactDefinition def)
    {
        if (def == null)
            return Color.white;

        return GradeToColor(def.grade);
    }
}