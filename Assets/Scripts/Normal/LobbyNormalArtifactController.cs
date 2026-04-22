using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LobbyNormalArtifactController : MonoBehaviour
{
    private sealed class ArtifactCardUI
    {
        public NormalArtifactDefinition Def;
        public GameObject Root;
        public Image IconImage;
        public TMP_Text NameText;
        public TMP_Text GradeText;
        public Image SelectOverlay;
        public Button Button;
        public CanvasGroup CanvasGroup;
    }

    private sealed class SlotUI
    {
        public GameObject Root;
        public Image IconImage;
        public GameObject EmptyIndicator;
        public Button RemoveButton;
    }

    [Header("Artifact Data")]
    [SerializeField] private List<NormalArtifactDefinition> allArtifacts = new List<NormalArtifactDefinition>();

    [Header("Popup Card List")]
    [SerializeField] private GameObject artifactCardPrefab;
    [SerializeField] private Transform cardListRoot; // 팝업 ScrollView/Content

    [Header("Popup Selected Slots")]
    [SerializeField] private List<Transform> selectedSlotRoots = new List<Transform>(); // 팝업 안 4칸

    [Header("Preview Slots")]
    [SerializeField] private List<Transform> previewSlotRoots = new List<Transform>(); // 로비 프리뷰 4칸

    [Header("Popup Buttons")]
    [SerializeField] private Button startButton;        // 팝업 확인 버튼으로 사용
    [SerializeField] private Button closePopupButton;   // 팝업 닫기 버튼

    [Header("Visible Roots")]
    [SerializeField] private GameObject normalModeRoot; // 노말 모드 전용 루트
    [SerializeField] private GameObject popupRoot;      // 아티팩트 팝업 루트
    [SerializeField] private CanvasGroup popupCanvasGroup;

    [Header("Grade Colors")]
    [SerializeField] private Color colorNormal = new Color(0.8f, 0.8f, 0.8f);
    [SerializeField] private Color colorRare = new Color(0.4f, 0.6f, 1.0f);
    [SerializeField] private Color colorEpic = new Color(0.6f, 0.4f, 1.0f);
    [SerializeField] private Color colorUnique = new Color(1.0f, 0.4f, 0.6f);
    [SerializeField] private Color colorLegend = new Color(1.0f, 0.75f, 0.2f);

    private readonly List<ArtifactCardUI> _cards = new List<ArtifactCardUI>();
    private readonly List<SlotUI> _popupSlots = new List<SlotUI>();
    private readonly List<SlotUI> _previewSlots = new List<SlotUI>();

    // 실제 로비 장착 상태
    private readonly List<NormalArtifactDefinition> _equippedDefs = new List<NormalArtifactDefinition>(4);

    // 팝업에서 편집 중인 상태
    private readonly List<NormalArtifactDefinition> _editingDefs = new List<NormalArtifactDefinition>(4);

    private bool _initialized;
    private bool _popupOpen;

    private const int MaxEquipCount = 4;

    private void Awake()
    {
        Initialize();
    }

    // LobbyManager.Awake() 에서 호출
    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        BuildPopupSlots();
        BuildPreviewSlots();
        BuildArtifactCards();
        BindButtons();

        RefreshAllUI();
        SetPopupVisible(false);
        _popupOpen = false;
    }

    // LobbyManager.RefreshModeUI() 에서 호출
    public void SetVisible(bool visible)
    {
        if (normalModeRoot != null)
            normalModeRoot.SetActive(visible);

        if (!visible)
        {
            ClosePopup();
            return;
        }

        RefreshAllUI();
    }

    // LobbyManager.OnClickStartBattle() 에서 선택 없을 때 호출
    public void OpenPopup()
    {
        CopyList(_equippedDefs, _editingDefs);
        _popupOpen = true;
        SetPopupVisible(true);
        RefreshAllUI();
    }

    public void ClosePopup()
    {
        _popupOpen = false;
        CopyList(_equippedDefs, _editingDefs);
        SetPopupVisible(false);
        RefreshAllUI();
    }

    public bool HasValidSelection()
    {
        return _equippedDefs.Count > 0;
    }

    public void ApplyToSession()
    {
        NormalArtifactSession.Set(_equippedDefs);
    }

    private void BuildPopupSlots()
    {
        _popupSlots.Clear();

        for (int i = 0; i < selectedSlotRoots.Count; i++)
        {
            Transform tr = selectedSlotRoots[i];
            if (tr == null) continue;

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
            if (tr == null) continue;

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
        return new SlotUI
        {
            Root = tr.gameObject,
            IconImage = tr.Find("Icon") != null ? tr.Find("Icon").GetComponent<Image>() : null,
            EmptyIndicator = tr.Find("EmptyIndicator") != null ? tr.Find("EmptyIndicator").gameObject : null,
            RemoveButton = tr.Find("RemoveButton") != null ? tr.Find("RemoveButton").GetComponent<Button>() : null
        };
    }

    private void BuildArtifactCards()
    {
        if (cardListRoot == null || artifactCardPrefab == null)
            return;

        for (int i = cardListRoot.childCount - 1; i >= 0; i--)
            Destroy(cardListRoot.GetChild(i).gameObject);

        _cards.Clear();

        foreach (NormalArtifactDefinition def in allArtifacts)
        {
            if (def == null) continue;

            GameObject go = Instantiate(artifactCardPrefab, cardListRoot);

            ArtifactCardUI card = new ArtifactCardUI
            {
                Def = def,
                Root = go,
                IconImage = go.transform.Find("Icon") != null ? go.transform.Find("Icon").GetComponent<Image>() : null,
                NameText = go.transform.Find("NameText") != null ? go.transform.Find("NameText").GetComponent<TMP_Text>() : null,
                GradeText = go.transform.Find("GradeText") != null ? go.transform.Find("GradeText").GetComponent<TMP_Text>() : null,
                SelectOverlay = go.transform.Find("SelectOverlay") != null ? go.transform.Find("SelectOverlay").GetComponent<Image>() : null,
                Button = go.GetComponent<Button>() != null ? go.GetComponent<Button>() : go.AddComponent<Button>(),
                CanvasGroup = go.GetComponent<CanvasGroup>() != null ? go.GetComponent<CanvasGroup>() : go.AddComponent<CanvasGroup>()
            };

            if (card.IconImage != null)
            {
                card.IconImage.sprite = def.icon;
                card.IconImage.enabled = def.icon != null;
                card.IconImage.preserveAspect = true;
            }

            if (card.NameText != null)
                card.NameText.text = def.displayName;

            if (card.GradeText != null)
            {
                card.GradeText.text = GradeToString(def.grade);
                card.GradeText.color = GradeToColor(def.grade);
            }

            NormalArtifactDefinition capturedDef = def;
            card.Button.onClick.RemoveAllListeners();
            card.Button.onClick.AddListener(() => OnClickArtifactCard(capturedDef));

            _cards.Add(card);
        }
    }

    private void BindButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnClickConfirmPopup);
        }

        if (closePopupButton != null)
        {
            closePopupButton.onClick.RemoveAllListeners();
            closePopupButton.onClick.AddListener(ClosePopup);
        }
    }

    private void OnClickArtifactCard(NormalArtifactDefinition def)
    {
        if (!_popupOpen) return;
        if (def == null) return;

        int existingIndex = _editingDefs.IndexOf(def);
        if (existingIndex >= 0)
        {
            _editingDefs.RemoveAt(existingIndex);
            RefreshAllUI();
            return;
        }

        if (_editingDefs.Count >= MaxEquipCount)
            return;

        _editingDefs.Add(def);
        RefreshAllUI();
    }

    private void OnClickRemoveEditingSlot(int slotIndex)
    {
        if (!_popupOpen) return;
        if (slotIndex < 0 || slotIndex >= _editingDefs.Count) return;

        _editingDefs.RemoveAt(slotIndex);
        RefreshAllUI();
    }

    // 팝업 확인 = 장착 확정만
    private void OnClickConfirmPopup()
    {
        CopyList(_editingDefs, _equippedDefs);
        _popupOpen = false;
        SetPopupVisible(false);
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
            if (card == null) continue;

            bool selected = _editingDefs.Contains(card.Def);

            if (card.SelectOverlay != null)
                card.SelectOverlay.gameObject.SetActive(selected);

            if (card.Button != null)
                card.Button.interactable = _popupOpen && (selected || !isFull);

            if (card.CanvasGroup != null)
            {
                if (!_popupOpen)
                    card.CanvasGroup.alpha = 1f;
                else
                    card.CanvasGroup.alpha = (!selected && isFull) ? 0.45f : 1f;
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

    private static void RefreshSingleSlot(SlotUI slot, List<NormalArtifactDefinition> source, int index, bool allowRemove)
    {
        if (slot == null) return;

        bool hasItem = index < source.Count;
        NormalArtifactDefinition def = hasItem ? source[index] : null;

        if (slot.IconImage != null)
        {
            slot.IconImage.gameObject.SetActive(hasItem);
            slot.IconImage.sprite = hasItem ? def.icon : null;
            slot.IconImage.enabled = hasItem && def != null && def.icon != null;
            slot.IconImage.preserveAspect = true;
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

    private static void CopyList(List<NormalArtifactDefinition> src, List<NormalArtifactDefinition> dst)
    {
        dst.Clear();

        for (int i = 0; i < src.Count && i < MaxEquipCount; i++)
        {
            NormalArtifactDefinition def = src[i];
            if (def == null) continue;
            if (dst.Contains(def)) continue;

            dst.Add(def);
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
}