using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 로비 씬에서 노말 모드 아티팩트를 선택하고,
/// NormalArtifactSession에 저장한 뒤 노말 씬으로 이동합니다.
///
/// 하이어라키 예시:
/// SafeArea/
///   NormalLobbyRoot/
///     ArtifactListRoot/   ← 전체 아티팩트 카드 목록 (스크롤뷰 Content 등)
///     SelectedSlotRoot/
///       SelectedSlot1~4   ← 선택된 슬롯 (아이콘 + 해제 버튼)
///     StartButton         ← 게임 시작 버튼
/// </summary>
public sealed class LobbyNormalArtifactController : MonoBehaviour
{
    // =========================
    // 내부 타입
    // =========================
    /// <summary>아티팩트 카드 1개 UI 정보</summary>
    private sealed class ArtifactCardUI
    {
        public NormalArtifactDefinition Def;
        public GameObject Root;
        public Image IconImage;
        public TMP_Text NameText;
        public TMP_Text GradeText;
        public Image SelectOverlay; // 선택됐을 때 표시
        public Button Button;
    }

    /// <summary>선택 슬롯 UI 1개</summary>
    private sealed class SelectedSlotUI
    {
        public GameObject Root;
        public Image IconImage;
        public GameObject EmptyIndicator; // 비어있을 때 표시 (예: + 아이콘)
        public Button RemoveButton;
    }

    // =========================
    // 인스펙터
    // =========================
    [Header("아티팩트 데이터 (전체 SO 등록)")]
    [SerializeField] private List<NormalArtifactDefinition> allArtifacts = new List<NormalArtifactDefinition>();

    [Header("카드 생성")]
    [SerializeField] private GameObject artifactCardPrefab; // 카드 프리팹
    [SerializeField] private Transform cardListRoot;        // 카드 목록 부모

    [Header("선택 슬롯 UI")]
    [SerializeField] private List<Transform> selectedSlotRoots = new List<Transform>(); // 슬롯 4개

    [Header("버튼")]
    [SerializeField] private Button startButton;

    [Header("Scene")]
    [SerializeField] private string normalSceneName = "Scene_Normal";

    [Header("등급별 색상 (인스펙터에서 지정)")]
    [SerializeField] private Color colorNormal = new Color(0.8f, 0.8f, 0.8f);
    [SerializeField] private Color colorRare = new Color(0.4f, 0.6f, 1.0f);
    [SerializeField] private Color colorEpic = new Color(0.6f, 0.4f, 1.0f);
    [SerializeField] private Color colorUnique = new Color(1.0f, 0.4f, 0.6f);
    [SerializeField] private Color colorLegend = new Color(1.0f, 0.75f, 0.2f);

    // =========================
    // 런타임
    // =========================
    private readonly List<ArtifactCardUI> _cards = new List<ArtifactCardUI>();
    private readonly List<SelectedSlotUI> _slots = new List<SelectedSlotUI>();
    private readonly List<NormalArtifactDefinition> _selectedDefs = new List<NormalArtifactDefinition>(4);

    private const int MaxSelectCount = 4;
    private bool _initialized;

    // =========================
    // 초기화
    // =========================
    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        BuildSelectedSlots();
        BuildArtifactCards();
        BindStartButton();
        RefreshUI();
    }

    private void BuildSelectedSlots()
    {
        _slots.Clear();

        for (int i = 0; i < selectedSlotRoots.Count; i++)
        {
            Transform tr = selectedSlotRoots[i];
            if (tr == null) continue;

            var slot = new SelectedSlotUI
            {
                Root = tr.gameObject,
                IconImage = tr.Find("Icon")?.GetComponent<Image>(),
                EmptyIndicator = tr.Find("EmptyIndicator")?.gameObject,
                RemoveButton = tr.Find("RemoveButton")?.GetComponent<Button>()
            };

            int captured = i;
            if (slot.RemoveButton != null)
            {
                slot.RemoveButton.onClick.RemoveAllListeners();
                slot.RemoveButton.onClick.AddListener(() => OnClickRemoveSlot(captured));
            }

            _slots.Add(slot);
        }
    }

    private void BuildArtifactCards()
    {
        if (cardListRoot == null || artifactCardPrefab == null) return;

        for (int i = cardListRoot.childCount - 1; i >= 0; i--)
            Destroy(cardListRoot.GetChild(i).gameObject);

        _cards.Clear();

        foreach (var def in allArtifacts)
        {
            if (def == null) continue;

            GameObject go = Instantiate(artifactCardPrefab, cardListRoot);

            var card = new ArtifactCardUI
            {
                Def = def,
                Root = go,
                IconImage = go.transform.Find("Icon")?.GetComponent<Image>(),
                NameText = go.transform.Find("NameText")?.GetComponent<TMP_Text>(),
                GradeText = go.transform.Find("GradeText")?.GetComponent<TMP_Text>(),
                SelectOverlay = go.transform.Find("SelectOverlay")?.GetComponent<Image>(),
                Button = go.GetComponent<Button>() ?? go.AddComponent<Button>()
            };

            if (card.IconImage != null)
                card.IconImage.sprite = def.icon;

            if (card.NameText != null)
                card.NameText.text = def.displayName;

            if (card.GradeText != null)
            {
                card.GradeText.text = GradeToString(def.grade);
                card.GradeText.color = GradeToColor(def.grade);
            }

            var capturedDef = def;
            card.Button.onClick.RemoveAllListeners();
            card.Button.onClick.AddListener(() => OnClickArtifactCard(capturedDef));

            _cards.Add(card);
        }
    }

    private void BindStartButton()
    {
        if (startButton == null) return;

        startButton.onClick.RemoveAllListeners();
        startButton.onClick.AddListener(OnClickStart);
    }

    // =========================
    // LobbyManager 호환 API
    // =========================
    public void OpenPopup()
    {
        SetVisible(true);
    }

    public void ClosePopup()
    {
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);

        if (visible)
            RefreshUI();
    }

    public bool HasValidSelection()
    {
        return _selectedDefs.Count > 0;
    }

    public void ApplyToSession()
    {
        NormalArtifactSession.Set(_selectedDefs);
    }

    // =========================
    // 이벤트 핸들러
    // =========================
    private void OnClickArtifactCard(NormalArtifactDefinition def)
    {
        if (def == null) return;

        // 이미 선택됐으면 해제
        if (_selectedDefs.Contains(def))
        {
            _selectedDefs.Remove(def);
            RefreshUI();
            return;
        }

        // 슬롯이 가득 찼으면 무시
        if (_selectedDefs.Count >= MaxSelectCount)
            return;

        _selectedDefs.Add(def);
        RefreshUI();
    }

    private void OnClickRemoveSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _selectedDefs.Count)
            return;

        _selectedDefs.RemoveAt(slotIndex);
        RefreshUI();
    }

    private void OnClickStart()
    {
        ApplyToSession();
        SceneManager.LoadScene(normalSceneName);
    }

    // =========================
    // UI 갱신
    // =========================
    private void RefreshUI()
    {
        RefreshCardOverlays();
        RefreshSelectedSlots();
        RefreshStartButton();
    }

    private void RefreshCardOverlays()
    {
        foreach (var card in _cards)
        {
            if (card == null) continue;

            bool isSelected = _selectedDefs.Contains(card.Def);

            if (card.SelectOverlay != null)
                card.SelectOverlay.gameObject.SetActive(isSelected);

            bool isFull = _selectedDefs.Count >= MaxSelectCount;

            if (card.Button != null)
                card.Button.interactable = isSelected || !isFull;

            if (card.Root != null)
            {
                var canvasGroup = card.Root.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = card.Root.AddComponent<CanvasGroup>();

                canvasGroup.alpha = (!isSelected && isFull) ? 0.4f : 1f;
            }
        }
    }

    private void RefreshSelectedSlots()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot == null) continue;

            bool hasItem = i < _selectedDefs.Count;

            if (slot.IconImage != null)
            {
                slot.IconImage.gameObject.SetActive(hasItem);

                if (hasItem)
                    slot.IconImage.sprite = _selectedDefs[i].icon;
            }

            if (slot.EmptyIndicator != null)
                slot.EmptyIndicator.SetActive(!hasItem);

            if (slot.RemoveButton != null)
                slot.RemoveButton.gameObject.SetActive(hasItem);
        }
    }

    private void RefreshStartButton()
    {
        // 팝업 내부 StartButton은 0개여도 누를 수 있게 유지
        // 실제 로비 시작 버튼에서는 LobbyManager가 HasValidSelection()으로 체크함
        if (startButton != null)
            startButton.interactable = true;
    }

    // =========================
    // 헬퍼
    // =========================
    private static string GradeToString(ArtifactGrade grade) => grade switch
    {
        ArtifactGrade.Normal => "노말",
        ArtifactGrade.Rare => "레어",
        ArtifactGrade.Epic => "에픽",
        ArtifactGrade.Unique => "유니크",
        ArtifactGrade.Legend => "전설",
        _ => ""
    };

    private Color GradeToColor(ArtifactGrade grade) => grade switch
    {
        ArtifactGrade.Normal => colorNormal,
        ArtifactGrade.Rare => colorRare,
        ArtifactGrade.Epic => colorEpic,
        ArtifactGrade.Unique => colorUnique,
        ArtifactGrade.Legend => colorLegend,
        _ => Color.white
    };
}