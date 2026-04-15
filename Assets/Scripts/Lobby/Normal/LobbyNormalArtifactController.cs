using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LobbyNormalArtifactController : MonoBehaviour
{
    [System.Serializable]
    private sealed class ArtifactVisualEntry
    {
        public NormalArtifactId artifactId;
        public string displayName;
        public Sprite iconSprite;
    }

    [System.Serializable]
    private sealed class ArtifactChoiceRefs
    {
        public Button button;
        public Image iconImage;
        public TMP_Text nameText;
        public GameObject selectedFrame;
        public Image background;
    }

    private const string PrefArtifact0 = "BBB_NORMAL_ARTIFACT_0";
    private const string PrefArtifact1 = "BBB_NORMAL_ARTIFACT_1";
    private const string PrefArtifact2 = "BBB_NORMAL_ARTIFACT_2";
    private const string PrefArtifact3 = "BBB_NORMAL_ARTIFACT_3";

    private const int MaxArtifactCount = 4;

    private static readonly Color32 ChoiceSelectedColor = new Color32(255, 255, 255, 255);
    private static readonly Color32 ChoiceUnselectedColor = new Color32(145, 145, 145, 255);

    [Header("Roots")]
    [SerializeField] private GameObject previewRoot;
    [SerializeField] private GameObject popupRoot;

    [Header("Preview")]
    [SerializeField] private Button editArtifactButton;
    [SerializeField] private Image[] previewIcons = new Image[4];

    [Header("Popup")]
    [SerializeField] private TMP_Text popupTitleText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Image[] selectedPreviewIcons = new Image[4];
    [SerializeField] private ArtifactChoiceRefs[] choiceButtons = new ArtifactChoiceRefs[6];

    [Header("Artifact Data")]
    [SerializeField]
    private NormalArtifactId[] defaultArtifacts = new NormalArtifactId[4]
    {
        NormalArtifactId.ScoreBoost,
        NormalArtifactId.ComboBoost,
        NormalArtifactId.ShapeReroll,
        NormalArtifactId.EmergencyClear
    };

    [SerializeField]
    private NormalArtifactId[] allArtifactOptions = new NormalArtifactId[6]
    {
        NormalArtifactId.ScoreBoost,
        NormalArtifactId.ComboBoost,
        NormalArtifactId.ShapeReroll,
        NormalArtifactId.EmergencyClear,
        NormalArtifactId.SecondChance,
        NormalArtifactId.LuckyBonus
    };

    [SerializeField] private ArtifactVisualEntry[] artifactVisualEntries = new ArtifactVisualEntry[6];

    private readonly Dictionary<NormalArtifactId, ArtifactVisualEntry> _visualMap
        = new Dictionary<NormalArtifactId, ArtifactVisualEntry>();

    private readonly List<NormalArtifactId> _selectedArtifacts = new List<NormalArtifactId>(MaxArtifactCount);
    private readonly List<NormalArtifactId> _editingArtifacts = new List<NormalArtifactId>(MaxArtifactCount);

    private bool _initialized;

    public void Initialize()
    {
        if (_initialized)
            return;

        BuildVisualMap();
        LoadSavedArtifactsOrDefault();
        EnsureArtifactFallback();
        BindButtons();
        RefreshPreviewUI();
        ClosePopup();

        _initialized = true;
    }

    public void SetVisible(bool visible)
    {
        if (previewRoot != null)
            previewRoot.SetActive(visible);

        if (!visible)
            ClosePopup();
    }

    public bool HasValidSelection()
    {
        return _selectedArtifacts.Count == MaxArtifactCount;
    }

    public void OpenPopup()
    {
        _editingArtifacts.Clear();

        for (int i = 0; i < _selectedArtifacts.Count; i++)
            _editingArtifacts.Add(_selectedArtifacts[i]);

        RefreshPopupUI();

        if (popupRoot != null)
            popupRoot.SetActive(true);
    }

    public void ClosePopup()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    public void ApplyToSession()
    {
        NormalArtifactSession.SetArtifacts(_selectedArtifacts);
    }

    public List<NormalArtifactId> GetSelectedArtifactsCopy()
    {
        return new List<NormalArtifactId>(_selectedArtifacts);
    }

    private void BuildVisualMap()
    {
        _visualMap.Clear();

        if (artifactVisualEntries == null)
            return;

        for (int i = 0; i < artifactVisualEntries.Length; i++)
        {
            ArtifactVisualEntry entry = artifactVisualEntries[i];
            if (entry == null)
                continue;

            _visualMap[entry.artifactId] = entry;
        }
    }

    private void BindButtons()
    {
        if (editArtifactButton != null)
        {
            editArtifactButton.onClick.RemoveAllListeners();
            editArtifactButton.onClick.AddListener(OpenPopup);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(ConfirmSelection);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ClosePopup);
        }
    }

    private void LoadSavedArtifactsOrDefault()
    {
        _selectedArtifacts.Clear();

        bool hasSaved =
            PlayerPrefs.HasKey(PrefArtifact0) &&
            PlayerPrefs.HasKey(PrefArtifact1) &&
            PlayerPrefs.HasKey(PrefArtifact2) &&
            PlayerPrefs.HasKey(PrefArtifact3);

        if (hasSaved)
        {
            AddArtifactIfValid((NormalArtifactId)PlayerPrefs.GetInt(PrefArtifact0));
            AddArtifactIfValid((NormalArtifactId)PlayerPrefs.GetInt(PrefArtifact1));
            AddArtifactIfValid((NormalArtifactId)PlayerPrefs.GetInt(PrefArtifact2));
            AddArtifactIfValid((NormalArtifactId)PlayerPrefs.GetInt(PrefArtifact3));
            return;
        }

        for (int i = 0; i < defaultArtifacts.Length && i < MaxArtifactCount; i++)
            AddArtifactIfValid(defaultArtifacts[i]);
    }

    private void SaveArtifacts()
    {
        if (_selectedArtifacts.Count < MaxArtifactCount)
            return;

        PlayerPrefs.SetInt(PrefArtifact0, (int)_selectedArtifacts[0]);
        PlayerPrefs.SetInt(PrefArtifact1, (int)_selectedArtifacts[1]);
        PlayerPrefs.SetInt(PrefArtifact2, (int)_selectedArtifacts[2]);
        PlayerPrefs.SetInt(PrefArtifact3, (int)_selectedArtifacts[3]);
        PlayerPrefs.Save();
    }

    private void EnsureArtifactFallback()
    {
        while (_selectedArtifacts.Count < MaxArtifactCount)
        {
            for (int i = 0; i < allArtifactOptions.Length && _selectedArtifacts.Count < MaxArtifactCount; i++)
                AddArtifactIfValid(allArtifactOptions[i]);
        }
    }

    private void AddArtifactIfValid(NormalArtifactId artifactId)
    {
        if (artifactId == NormalArtifactId.None)
            return;

        if (_selectedArtifacts.Contains(artifactId))
            return;

        _selectedArtifacts.Add(artifactId);
    }

    private void ConfirmSelection()
    {
        if (_editingArtifacts.Count != MaxArtifactCount)
            return;

        _selectedArtifacts.Clear();

        for (int i = 0; i < _editingArtifacts.Count; i++)
            _selectedArtifacts.Add(_editingArtifacts[i]);

        SaveArtifacts();
        RefreshPreviewUI();
        ClosePopup();
    }

    private void RefreshPreviewUI()
    {
        for (int i = 0; i < previewIcons.Length; i++)
        {
            NormalArtifactId id = i < _selectedArtifacts.Count
                ? _selectedArtifacts[i]
                : NormalArtifactId.None;

            SetPreviewIcon(previewIcons[i], GetArtifactSprite(id), id != NormalArtifactId.None);
        }
    }

    private void RefreshPopupUI()
    {
        if (popupTitleText != null)
            popupTitleText.text = "아티팩트 선택";

        for (int i = 0; i < selectedPreviewIcons.Length; i++)
        {
            NormalArtifactId id = i < _editingArtifacts.Count
                ? _editingArtifacts[i]
                : NormalArtifactId.None;

            SetPreviewIcon(selectedPreviewIcons[i], GetArtifactSprite(id), id != NormalArtifactId.None);
        }

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            ArtifactChoiceRefs refs = choiceButtons[i];
            NormalArtifactId id = i < allArtifactOptions.Length
                ? allArtifactOptions[i]
                : NormalArtifactId.None;

            bool selected = id != NormalArtifactId.None && _editingArtifacts.Contains(id);

            if (refs != null && refs.button != null)
            {
                refs.button.onClick.RemoveAllListeners();

                NormalArtifactId captured = id;
                refs.button.onClick.AddListener(() => ToggleEditingArtifact(captured));
                refs.button.interactable = id != NormalArtifactId.None;
            }

            RefreshChoiceButtonVisual(
                refs,
                GetArtifactSprite(id),
                GetArtifactDisplayName(id),
                selected
            );
        }

        if (confirmButton != null)
            confirmButton.interactable = _editingArtifacts.Count == MaxArtifactCount;
    }

    private void ToggleEditingArtifact(NormalArtifactId artifactId)
    {
        if (artifactId == NormalArtifactId.None)
            return;

        if (_editingArtifacts.Contains(artifactId))
        {
            _editingArtifacts.Remove(artifactId);
            RefreshPopupUI();
            return;
        }

        if (_editingArtifacts.Count >= MaxArtifactCount)
            return;

        _editingArtifacts.Add(artifactId);
        RefreshPopupUI();
    }

    private Sprite GetArtifactSprite(NormalArtifactId artifactId)
    {
        if (_visualMap.TryGetValue(artifactId, out ArtifactVisualEntry entry))
            return entry.iconSprite;

        return null;
    }

    private string GetArtifactDisplayName(NormalArtifactId artifactId)
    {
        if (_visualMap.TryGetValue(artifactId, out ArtifactVisualEntry entry) &&
            !string.IsNullOrWhiteSpace(entry.displayName))
        {
            return entry.displayName;
        }

        return artifactId.ToString();
    }

    private static void SetPreviewIcon(Image target, Sprite sprite, bool active)
    {
        if (target == null)
            return;

        target.enabled = active;
        target.sprite = sprite;
        target.preserveAspect = true;

        Color c = target.color;
        c.a = active ? 1f : 0f;
        target.color = c;
    }

    private static void RefreshChoiceButtonVisual(ArtifactChoiceRefs refs, Sprite sprite, string displayName, bool selected)
    {
        if (refs == null)
            return;

        if (refs.iconImage != null)
        {
            refs.iconImage.enabled = sprite != null;
            refs.iconImage.sprite = sprite;
            refs.iconImage.preserveAspect = true;
        }

        if (refs.nameText != null)
            refs.nameText.text = displayName;

        if (refs.selectedFrame != null)
            refs.selectedFrame.SetActive(selected);

        if (refs.background != null)
            refs.background.color = selected ? ChoiceSelectedColor : ChoiceUnselectedColor;
    }
}