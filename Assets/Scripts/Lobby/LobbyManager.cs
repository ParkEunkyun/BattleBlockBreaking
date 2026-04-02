using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    [Serializable]
    private sealed class AttackVisualEntry
    {
        public BattleManager.AttackItemId itemId;
        public string displayName;
        public Sprite iconSprite;
    }

    [Serializable]
    private sealed class SupportVisualEntry
    {
        public BattleManager.SupportItemId itemId;
        public string displayName;
        public Sprite iconSprite;
    }

    private sealed class ChoiceButtonRefs
    {
        public Button button;
        public Image background;
        public Image iconImage;
        public TMP_Text nameText;
        public GameObject selectedFrame;
    }

    [Header("Scene")]
    [SerializeField] private string battleSceneName = "Scene_Battle";
    [SerializeField] private string normalSceneName = "Scene_Normal";

    [Header("Profile / Lobby Text")]
    [SerializeField] private string nickname = "";
    [SerializeField] private string tierLabel = "";
    [SerializeField] private int gold = 12450;
    [SerializeField] private string seasonLabel = "시즌1: 블록 쇼다운";
    [SerializeField] private string defaultMissionText = "오늘의 미션: 3승 달성!";
    [SerializeField] private string defaultNoticeText = "BBB 준비중";

    [Header("Default Loadout")]
    [SerializeField]
    private BattleManager.AttackItemId[] defaultAttackLoadout = new BattleManager.AttackItemId[3]
    {
        BattleManager.AttackItemId.Obstacle2,
        BattleManager.AttackItemId.SealRandomSlot,
        BattleManager.AttackItemId.Bomb3x3
    };

    [SerializeField]
    private BattleManager.SupportItemId[] defaultSupportLoadout = new BattleManager.SupportItemId[2]
    {
        BattleManager.SupportItemId.RotateRight90,
        BattleManager.SupportItemId.ResetRemaining
    };

    [Header("All Selectable Items")]
    [SerializeField]
    private BattleManager.AttackItemId[] allAttackOptions = new BattleManager.AttackItemId[6]
    {
        BattleManager.AttackItemId.Obstacle2,
        BattleManager.AttackItemId.SealRandomSlot,
        BattleManager.AttackItemId.CurseBlock,
        BattleManager.AttackItemId.DisableItemUse,
        BattleManager.AttackItemId.DeleteRandomLine,
        BattleManager.AttackItemId.Bomb3x3
    };

    [SerializeField]
    private BattleManager.SupportItemId[] allSupportOptions = new BattleManager.SupportItemId[5]
    {
        BattleManager.SupportItemId.RotateRight90,
        BattleManager.SupportItemId.RotateLeft90,
        BattleManager.SupportItemId.MirrorHorizontal,
        BattleManager.SupportItemId.MirrorVertical,
        BattleManager.SupportItemId.ResetRemaining
    };

    [Header("Visual Data")]
    [SerializeField] private List<AttackVisualEntry> attackVisualEntries = new List<AttackVisualEntry>();
    [SerializeField] private List<SupportVisualEntry> supportVisualEntries = new List<SupportVisualEntry>();

    [Serializable]
    private sealed class TierVisualEntry
    {
        public string tierName;
        public int minMmr;
        public int maxMmr = 999999;
        public Sprite iconSprite;
    }

    [Header("Mode")]
    [SerializeField] private GameMode startGameMode = GameMode.Ranked;

    private const string PrefAttack0 = "BBB_LOBBY_ATTACK_0";
    private const string PrefAttack1 = "BBB_LOBBY_ATTACK_1";
    private const string PrefAttack2 = "BBB_LOBBY_ATTACK_2";
    private const string PrefSupport0 = "BBB_LOBBY_SUPPORT_0";
    private const string PrefSupport1 = "BBB_LOBBY_SUPPORT_1";
    private const string PrefMode = "BBB_LOBBY_MODE";

    private readonly Dictionary<BattleManager.AttackItemId, AttackVisualEntry> _attackVisualMap = new Dictionary<BattleManager.AttackItemId, AttackVisualEntry>();
    private readonly Dictionary<BattleManager.SupportItemId, SupportVisualEntry> _supportVisualMap = new Dictionary<BattleManager.SupportItemId, SupportVisualEntry>();

    private readonly List<BattleManager.AttackItemId> _selectedAttackLoadout = new List<BattleManager.AttackItemId>(3);
    private readonly List<BattleManager.SupportItemId> _selectedSupportLoadout = new List<BattleManager.SupportItemId>(2);

    private readonly List<BattleManager.AttackItemId> _editingAttackLoadout = new List<BattleManager.AttackItemId>(3);
    private readonly List<BattleManager.SupportItemId> _editingSupportLoadout = new List<BattleManager.SupportItemId>(2);

    private GameMode _gameMode = GameMode.Ranked;
    private Coroutine _matchRoutine;

    [SerializeField] private TMP_Text _nicknameText;
    [SerializeField] private TMP_Text _tierText;
    [SerializeField] private TMP_Text _goldText;
    [SerializeField] private TMP_Text _seasonText;
    [SerializeField] private TMP_Text _missionText;
    [SerializeField] private TMP_Text _noticeText;
    [SerializeField] private TMP_Text _matchStatusText;
    [SerializeField] private TMP_Text _popupTitleText;

    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _startBattleButton;
    [SerializeField] private Button _rankedModeButton;
    [SerializeField] private Button _normalModeButton;
    [SerializeField] private Button _editAttackLoadoutButton;
    [SerializeField] private Button _editSupportLoadoutButton;
    [SerializeField] private Button _confirmLoadoutButton;
    [SerializeField] private Button _closeLoadoutButton;
    [SerializeField] private Button _cancelMatchButton;
    [SerializeField] private Button _loadoutTabButton;
    [SerializeField] private Button _homeTabButton;
    [SerializeField] private Button _shopTabButton;
    [SerializeField] private Button _recordTabButton;
    [SerializeField] private Button _claimRewardButton;

    private GameObject _loadoutPopupRoot;
    private GameObject _matchPopupRoot;

    private readonly Image[] _attackPreviewIcons = new Image[3];
    private readonly Image[] _supportPreviewIcons = new Image[2];
    private readonly Image[] _selectedAttackPreviewIcons = new Image[3];
    private readonly Image[] _selectedSupportPreviewIcons = new Image[2];

    private readonly ChoiceButtonRefs[] _attackChoiceButtons = new ChoiceButtonRefs[6];
    private readonly ChoiceButtonRefs[] _supportChoiceButtons = new ChoiceButtonRefs[5];

    private static readonly Color32 ChoiceSelectedColor = new Color32(255, 214, 120, 255);
    private static readonly Color32 ChoiceUnselectedColor = new Color32(255, 255, 255, 255);

    [SerializeField] private Sprite selectSprite;
    [SerializeField] private Sprite normalSprite;

    private bool IsRankedMode => _gameMode == GameMode.Ranked;
    private bool IsNormalMode => _gameMode == GameMode.Normal;

    [Header("Ranked Record UI")]
    [SerializeField] private TMP_Text mmrText;
    [SerializeField] private Image tierIconImage;
    [SerializeField] private TMP_Text detailVictoryText;
    [SerializeField] private TMP_Text detailDrawText;
    [SerializeField] private TMP_Text detailDefeatText;
    [SerializeField] private List<TierVisualEntry> tierVisualEntries = new List<TierVisualEntry>();

    private void Awake()
    {
        BuildVisualMaps();
        CacheHierarchy();
        BindButtons();
        LoadSavedLoadoutOrDefault();
        RefreshNicknameFromBackend();
        ApplyProfileTexts();
        RefreshModeUI();
        RefreshPreviewUI();
        HideLoadoutPopup();
        HideMatchPopup();
        ApplyRankedRecordUi();
    }
    private void Start()
    {
        RankedRecordCache.Changed += HandleRankedRecordCacheChanged;

        ApplyRankedRecordUi();
        RankedRecordService.RefreshMyRankedRecord();

        AdMobManager.Instance.ShowBannerBottom();
    }
    private void OnDestroy()
    {
        RankedRecordCache.Changed -= HandleRankedRecordCacheChanged;
    }

    private void BuildVisualMaps()
    {
        _attackVisualMap.Clear();
        _supportVisualMap.Clear();

        for (int i = 0; i < attackVisualEntries.Count; i++)
        {
            AttackVisualEntry entry = attackVisualEntries[i];
            if (entry == null)
                continue;

            _attackVisualMap[entry.itemId] = entry;
        }

        for (int i = 0; i < supportVisualEntries.Count; i++)
        {
            SupportVisualEntry entry = supportVisualEntries[i];
            if (entry == null)
                continue;

            _supportVisualMap[entry.itemId] = entry;
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

        _nicknameText = FindTMP(safeArea, "TopBarRoot/ProfilePanel/NicknameText");
        _tierText = FindTMP(safeArea, "TopBarRoot/ProfilePanel/TierBadgeRoot/TierText");
        _goldText = FindTMP(safeArea, "TopBarRoot/CurrencyPanel/GoldText");
        _seasonText = FindTMP(safeArea, "MainContentRoot/LogoRoot/SeasonBannerPanel/SeasonText");
        _missionText = FindTMP(safeArea, "MainContentRoot/MissionRoot/DailyMissionPanel/MissionText");
        _noticeText = FindTMP(safeArea, "MainContentRoot/NoticeRoot/NoticeText");
        _matchStatusText = FindTMP(safeArea, "MatchPopupRoot/MatchPopupPanel/MatchStatusText");
        _popupTitleText = FindTMP(safeArea, "LoadoutPopupRoot/LoadoutPopupPanel/PopupTitleText");

        _settingsButton = FindButton(safeArea, "TopBarRoot/SettingsButton");
        _startBattleButton = FindButton(safeArea, "MainContentRoot/MatchStartRoot/StartBattleButton");
        _rankedModeButton = FindButton(safeArea, "MainContentRoot/MatchStartRoot/MatchModePanel/RankedModeButton");
        _normalModeButton = FindButton(safeArea, "MainContentRoot/MatchStartRoot/MatchModePanel/NormalModeButton");
        _editAttackLoadoutButton = FindButton(safeArea, "MainContentRoot/LoadoutPreviewRoot/AttackLoadoutPanel/EditAttackLoadoutButton");
        _editSupportLoadoutButton = FindButton(safeArea, "MainContentRoot/LoadoutPreviewRoot/SupportLoadoutPanel/EditSupportLoadoutButton");
        _claimRewardButton = FindButton(safeArea, "MainContentRoot/MissionRoot/DailyMissionPanel/ClaimRewardButton");

        _homeTabButton = FindButton(safeArea, "BottomTabRoot/HomeTabButton");
        _loadoutTabButton = FindButton(safeArea, "BottomTabRoot/LoadoutTabButton");
        _shopTabButton = FindButton(safeArea, "BottomTabRoot/ShopTabButton");
        _recordTabButton = FindButton(safeArea, "BottomTabRoot/RecordTabButton");

        _loadoutPopupRoot = FindGO(safeArea, "LoadoutPopupRoot");
        _matchPopupRoot = FindGO(safeArea, "MatchPopupRoot");

        _confirmLoadoutButton = FindButton(safeArea, "LoadoutPopupRoot/LoadoutPopupPanel/ConfirmLoadoutButton");
        _closeLoadoutButton = FindButton(safeArea, "LoadoutPopupRoot/LoadoutPopupPanel/CloseLoadoutButton");
        _cancelMatchButton = FindButton(safeArea, "MatchPopupRoot/MatchPopupPanel/CancelMatchButton");

        for (int i = 0; i < 3; i++)
        {
            Transform slot = safeArea.Find($"MainContentRoot/LoadoutPreviewRoot/AttackLoadoutPanel/AttackSlot{i + 1}");
            _attackPreviewIcons[i] = EnsureSlotIcon(slot);
        }

        for (int i = 0; i < 2; i++)
        {
            Transform slot = safeArea.Find($"MainContentRoot/LoadoutPreviewRoot/SupportLoadoutPanel/SupportSlot{i + 1}");
            _supportPreviewIcons[i] = EnsureSlotIcon(slot);
        }

        for (int i = 0; i < 3; i++)
        {
            Transform slot = safeArea.Find($"LoadoutPopupRoot/LoadoutPopupPanel/SelectedAttackPreviewRoot/SelectedAttackSlot{i + 1}");
            _selectedAttackPreviewIcons[i] = EnsureSlotIcon(slot);
        }

        for (int i = 0; i < 2; i++)
        {
            Transform slot = safeArea.Find($"LoadoutPopupRoot/LoadoutPopupPanel/SelectedSupportPreviewRoot/SelectedSupportSlot{i + 1}");
            _selectedSupportPreviewIcons[i] = EnsureSlotIcon(slot);
        }

        for (int i = 0; i < _attackChoiceButtons.Length; i++)
        {
            Transform tr = safeArea.Find($"LoadoutPopupRoot/LoadoutPopupPanel/AttackSelectRoot/AttackItemButton{i + 1}");
            _attackChoiceButtons[i] = CacheChoiceButton(tr, true);
        }

        for (int i = 0; i < _supportChoiceButtons.Length; i++)
        {
            Transform tr = safeArea.Find($"LoadoutPopupRoot/LoadoutPopupPanel/SupportSelectRoot/SupportItemButton{i + 1}");
            _supportChoiceButtons[i] = CacheChoiceButton(tr, false);
        }
    }

    private void BindButtons()
    {
        if (_settingsButton != null)
        {
            _settingsButton.onClick.RemoveAllListeners();
            _settingsButton.onClick.AddListener(() => Debug.Log("[BBB] SettingsButton"));
        }

        if (_startBattleButton != null)
        {
            _startBattleButton.onClick.RemoveAllListeners();
            _startBattleButton.onClick.AddListener(OnClickStartBattle);
        }

        if (_rankedModeButton != null)
        {
            _rankedModeButton.onClick.RemoveAllListeners();
            _rankedModeButton.onClick.AddListener(() =>
            {
                _gameMode = GameMode.Ranked;
                RefreshModeUI();
            });
        }

        if (_normalModeButton != null)
        {
            _normalModeButton.onClick.RemoveAllListeners();
            _normalModeButton.onClick.AddListener(() =>
            {
                _gameMode = GameMode.Normal;
                RefreshModeUI();
            });
        }

        if (_editAttackLoadoutButton != null)
        {
            _editAttackLoadoutButton.onClick.RemoveAllListeners();
            _editAttackLoadoutButton.onClick.AddListener(OpenLoadoutPopup);
        }

        if (_editSupportLoadoutButton != null)
        {
            _editSupportLoadoutButton.onClick.RemoveAllListeners();
            _editSupportLoadoutButton.onClick.AddListener(OpenLoadoutPopup);
        }

        if (_confirmLoadoutButton != null)
        {
            _confirmLoadoutButton.onClick.RemoveAllListeners();
            _confirmLoadoutButton.onClick.AddListener(ConfirmLoadoutSelection);
        }

        if (_closeLoadoutButton != null)
        {
            _closeLoadoutButton.onClick.RemoveAllListeners();
            _closeLoadoutButton.onClick.AddListener(CloseLoadoutPopup);
        }

        if (_cancelMatchButton != null)
        {
            _cancelMatchButton.onClick.RemoveAllListeners();
            _cancelMatchButton.onClick.AddListener(CancelMatching);
        }

        if (_loadoutTabButton != null)
        {
            _loadoutTabButton.onClick.RemoveAllListeners();
            _loadoutTabButton.onClick.AddListener(OpenLoadoutPopup);
        }

        if (_homeTabButton != null)
        {
            _homeTabButton.onClick.RemoveAllListeners();
            _homeTabButton.onClick.AddListener(() => Debug.Log("[BBB] HomeTabButton"));
        }

        if (_shopTabButton != null)
        {
            _shopTabButton.onClick.RemoveAllListeners();
            _shopTabButton.onClick.AddListener(() => Debug.Log("[BBB] ShopTabButton"));
        }

        if (_recordTabButton != null)
        {
            _recordTabButton.onClick.RemoveAllListeners();
            _recordTabButton.onClick.AddListener(() => Debug.Log("[BBB] RecordTabButton"));
        }

        if (_claimRewardButton != null)
        {
            _claimRewardButton.onClick.RemoveAllListeners();
            _claimRewardButton.onClick.AddListener(() => Debug.Log("[BBB] ClaimRewardButton"));
        }

        for (int i = 0; i < _attackChoiceButtons.Length; i++)
        {
            if (_attackChoiceButtons[i] == null || _attackChoiceButtons[i].button == null)
                continue;

            int index = i;
            _attackChoiceButtons[i].button.onClick.RemoveAllListeners();
            _attackChoiceButtons[i].button.onClick.AddListener(() => ToggleAttackChoice(index));
        }

        for (int i = 0; i < _supportChoiceButtons.Length; i++)
        {
            if (_supportChoiceButtons[i] == null || _supportChoiceButtons[i].button == null)
                continue;

            int index = i;
            _supportChoiceButtons[i].button.onClick.RemoveAllListeners();
            _supportChoiceButtons[i].button.onClick.AddListener(() => ToggleSupportChoice(index));
        }
    }

    private void LoadSavedLoadoutOrDefault()
    {
        _selectedAttackLoadout.Clear();
        _selectedSupportLoadout.Clear();

        bool hasSavedAttack =
            PlayerPrefs.HasKey(PrefAttack0) &&
            PlayerPrefs.HasKey(PrefAttack1) &&
            PlayerPrefs.HasKey(PrefAttack2);

        bool hasSavedSupport =
            PlayerPrefs.HasKey(PrefSupport0) &&
            PlayerPrefs.HasKey(PrefSupport1);

        if (hasSavedAttack)
        {
            _selectedAttackLoadout.Add((BattleManager.AttackItemId)PlayerPrefs.GetInt(PrefAttack0));
            _selectedAttackLoadout.Add((BattleManager.AttackItemId)PlayerPrefs.GetInt(PrefAttack1));
            _selectedAttackLoadout.Add((BattleManager.AttackItemId)PlayerPrefs.GetInt(PrefAttack2));
        }
        else
        {
            for (int i = 0; i < defaultAttackLoadout.Length && i < 3; i++)
                _selectedAttackLoadout.Add(defaultAttackLoadout[i]);
        }

        if (hasSavedSupport)
        {
            _selectedSupportLoadout.Add((BattleManager.SupportItemId)PlayerPrefs.GetInt(PrefSupport0));
            _selectedSupportLoadout.Add((BattleManager.SupportItemId)PlayerPrefs.GetInt(PrefSupport1));
        }
        else
        {
            for (int i = 0; i < defaultSupportLoadout.Length && i < 2; i++)
                _selectedSupportLoadout.Add(defaultSupportLoadout[i]);
        }

        if (PlayerPrefs.HasKey(PrefMode))
        {
            _gameMode = (GameMode)PlayerPrefs.GetInt(PrefMode);
        }
        else
        {
            _gameMode = startGameMode;
        }

        if (_gameMode != GameMode.Ranked && _gameMode != GameMode.Normal)
            _gameMode = startGameMode;

        EnsureLoadoutValidFallback();
    }

    private void EnsureLoadoutValidFallback()
    {
        while (_selectedAttackLoadout.Count < 3)
        {
            for (int i = 0; i < allAttackOptions.Length && _selectedAttackLoadout.Count < 3; i++)
            {
                if (!_selectedAttackLoadout.Contains(allAttackOptions[i]))
                    _selectedAttackLoadout.Add(allAttackOptions[i]);
            }
        }

        while (_selectedSupportLoadout.Count < 2)
        {
            for (int i = 0; i < allSupportOptions.Length && _selectedSupportLoadout.Count < 2; i++)
            {
                if (!_selectedSupportLoadout.Contains(allSupportOptions[i]))
                    _selectedSupportLoadout.Add(allSupportOptions[i]);
            }
        }
    }

    private void SaveLoadout()
    {
        if (_selectedAttackLoadout.Count >= 3)
        {
            PlayerPrefs.SetInt(PrefAttack0, (int)_selectedAttackLoadout[0]);
            PlayerPrefs.SetInt(PrefAttack1, (int)_selectedAttackLoadout[1]);
            PlayerPrefs.SetInt(PrefAttack2, (int)_selectedAttackLoadout[2]);
        }

        if (_selectedSupportLoadout.Count >= 2)
        {
            PlayerPrefs.SetInt(PrefSupport0, (int)_selectedSupportLoadout[0]);
            PlayerPrefs.SetInt(PrefSupport1, (int)_selectedSupportLoadout[1]);
        }

        PlayerPrefs.SetInt(PrefMode, (int)_gameMode);
        PlayerPrefs.Save();
    }

    private void ApplyProfileTexts()
    {
        SetTMP(_nicknameText, nickname);
        SetTMP(_tierText, tierLabel);
        SetTMP(_goldText, gold.ToString("N0"));
        SetTMP(_seasonText, seasonLabel);
        SetTMP(_missionText, defaultMissionText);
        SetTMP(_noticeText, defaultNoticeText);
        SetTMP(_popupTitleText, "로드아웃 편집");
    }

    private void RefreshModeUI()
    {
        SetModeButtonVisual(_rankedModeButton, IsRankedMode);
        SetModeButtonVisual(_normalModeButton, IsNormalMode);
    }

    private void SetModeButtonVisual(Button button, bool selected)
    {
        if (button == null)
            return;

        Image bg = button.GetComponent<Image>();
        if (bg != null)
            bg.sprite = selected ? selectSprite : normalSprite;
    }

    private void RefreshPreviewUI()
    {
        for (int i = 0; i < _attackPreviewIcons.Length; i++)
        {
            BattleManager.AttackItemId itemId = i < _selectedAttackLoadout.Count
                ? _selectedAttackLoadout[i]
                : BattleManager.AttackItemId.None;

            SetPreviewIcon(_attackPreviewIcons[i], GetAttackSprite(itemId), itemId != BattleManager.AttackItemId.None);
        }

        for (int i = 0; i < _supportPreviewIcons.Length; i++)
        {
            BattleManager.SupportItemId itemId = i < _selectedSupportLoadout.Count
                ? _selectedSupportLoadout[i]
                : BattleManager.SupportItemId.None;

            SetPreviewIcon(_supportPreviewIcons[i], GetSupportSprite(itemId), itemId != BattleManager.SupportItemId.None);
        }
    }

    private void OpenLoadoutPopup()
    {
        _editingAttackLoadout.Clear();
        _editingSupportLoadout.Clear();

        for (int i = 0; i < _selectedAttackLoadout.Count; i++)
            _editingAttackLoadout.Add(_selectedAttackLoadout[i]);

        for (int i = 0; i < _selectedSupportLoadout.Count; i++)
            _editingSupportLoadout.Add(_selectedSupportLoadout[i]);

        RefreshLoadoutPopupUI();

        if (_loadoutPopupRoot != null)
            _loadoutPopupRoot.SetActive(true);
    }

    private void CloseLoadoutPopup()
    {
        if (_loadoutPopupRoot != null)
            _loadoutPopupRoot.SetActive(false);
    }

    private void HideLoadoutPopup()
    {
        if (_loadoutPopupRoot != null)
            _loadoutPopupRoot.SetActive(false);
    }

    private void ShowMatchPopup(string status)
    {
        if (_matchPopupRoot != null)
            _matchPopupRoot.SetActive(true);

        SetTMP(_matchStatusText, status);
    }

    private void HideMatchPopup()
    {
        if (_matchPopupRoot != null)
            _matchPopupRoot.SetActive(false);
    }

    private void ToggleAttackChoice(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= allAttackOptions.Length)
            return;

        BattleManager.AttackItemId itemId = allAttackOptions[optionIndex];
        int existingIndex = _editingAttackLoadout.IndexOf(itemId);

        if (existingIndex >= 0)
        {
            _editingAttackLoadout.RemoveAt(existingIndex);
        }
        else
        {
            if (_editingAttackLoadout.Count >= 3)
                return;

            _editingAttackLoadout.Add(itemId);
        }

        RefreshLoadoutPopupUI();
    }

    private void ToggleSupportChoice(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= allSupportOptions.Length)
            return;

        BattleManager.SupportItemId itemId = allSupportOptions[optionIndex];
        int existingIndex = _editingSupportLoadout.IndexOf(itemId);

        if (existingIndex >= 0)
        {
            _editingSupportLoadout.RemoveAt(existingIndex);
        }
        else
        {
            if (_editingSupportLoadout.Count >= 2)
                return;

            _editingSupportLoadout.Add(itemId);
        }

        RefreshLoadoutPopupUI();
    }

    private void RefreshLoadoutPopupUI()
    {
        for (int i = 0; i < _attackChoiceButtons.Length; i++)
        {
            if (_attackChoiceButtons[i] == null)
                continue;

            BattleManager.AttackItemId itemId = i < allAttackOptions.Length
                ? allAttackOptions[i]
                : BattleManager.AttackItemId.None;

            bool selected = _editingAttackLoadout.Contains(itemId);
            RefreshChoiceButtonVisual(_attackChoiceButtons[i], GetAttackSprite(itemId), GetAttackDisplayName(itemId), selected);
        }

        for (int i = 0; i < _supportChoiceButtons.Length; i++)
        {
            if (_supportChoiceButtons[i] == null)
                continue;

            BattleManager.SupportItemId itemId = i < allSupportOptions.Length
                ? allSupportOptions[i]
                : BattleManager.SupportItemId.None;

            bool selected = _editingSupportLoadout.Contains(itemId);
            RefreshChoiceButtonVisual(_supportChoiceButtons[i], GetSupportSprite(itemId), GetSupportDisplayName(itemId), selected);
        }

        for (int i = 0; i < _selectedAttackPreviewIcons.Length; i++)
        {
            BattleManager.AttackItemId itemId = i < _editingAttackLoadout.Count
                ? _editingAttackLoadout[i]
                : BattleManager.AttackItemId.None;

            SetPreviewIcon(_selectedAttackPreviewIcons[i], GetAttackSprite(itemId), itemId != BattleManager.AttackItemId.None);
        }

        for (int i = 0; i < _selectedSupportPreviewIcons.Length; i++)
        {
            BattleManager.SupportItemId itemId = i < _editingSupportLoadout.Count
                ? _editingSupportLoadout[i]
                : BattleManager.SupportItemId.None;

            SetPreviewIcon(_selectedSupportPreviewIcons[i], GetSupportSprite(itemId), itemId != BattleManager.SupportItemId.None);
        }

        bool canConfirm = _editingAttackLoadout.Count == 3 && _editingSupportLoadout.Count == 2;
        if (_confirmLoadoutButton != null)
            _confirmLoadoutButton.interactable = canConfirm;
    }

    private void RefreshChoiceButtonVisual(ChoiceButtonRefs refs, Sprite icon, string displayName, bool selected)
    {
        if (refs == null)
            return;

        if (refs.iconImage != null)
        {
            refs.iconImage.sprite = icon;
            refs.iconImage.enabled = icon != null;
            refs.iconImage.color = Color.white;
            refs.iconImage.preserveAspect = true;
        }

        if (refs.nameText != null)
            refs.nameText.text = displayName;

        if (refs.selectedFrame != null)
            refs.selectedFrame.SetActive(selected);

        if (refs.background != null)
            refs.background.color = selected ? ChoiceSelectedColor : ChoiceUnselectedColor;
    }

    private void ConfirmLoadoutSelection()
    {
        if (_editingAttackLoadout.Count != 3 || _editingSupportLoadout.Count != 2)
            return;

        _selectedAttackLoadout.Clear();
        _selectedSupportLoadout.Clear();

        for (int i = 0; i < _editingAttackLoadout.Count; i++)
            _selectedAttackLoadout.Add(_editingAttackLoadout[i]);

        for (int i = 0; i < _editingSupportLoadout.Count; i++)
            _selectedSupportLoadout.Add(_editingSupportLoadout[i]);

        SaveLoadout();
        RefreshPreviewUI();
        CloseLoadoutPopup();
    }

    private void OnClickStartBattle()
    {
        if (_selectedAttackLoadout.Count != 3 || _selectedSupportLoadout.Count != 2)
        {
            OpenLoadoutPopup();
            return;
        }

        BattleLoadoutSession.SetLoadout(_selectedAttackLoadout, _selectedSupportLoadout, _gameMode);
        SaveLoadout();

        if (_matchRoutine != null)
        {
            StopCoroutine(_matchRoutine);
            _matchRoutine = null;
        }

        switch (_gameMode)
        {
            case GameMode.Ranked:
                {
                    ShowMatchPopup("랭크전 매칭중...");

                    if (MatchManager.I == null)
                    {
                        Debug.LogError("[LobbyManager] MatchManager가 씬에 없습니다.");
                        HideMatchPopup();
                        return;
                    }

                    RankedRecordCache.MarkPreMatchFromCurrent();
                    MatchManager.I.StartRankedMatch();
                    break;
                }

            case GameMode.Normal:
                {
                    ShowMatchPopup("노말 모드 준비중...");
                    _matchRoutine = StartCoroutine(CoEnterScene(normalSceneName));
                    break;
                }

            default:
                {
                    Debug.LogError("[LobbyManager] 지원하지 않는 GameMode 입니다.");
                    HideMatchPopup();
                    break;
                }
        }
    }

    private IEnumerator CoEnterScene(string sceneName)
    {
        yield return new WaitForSecondsRealtime(0.35f);

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[LobbyManager] sceneName 이 비어있음");
            HideMatchPopup();
            yield break;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void CancelMatching()
    {
        if (_matchRoutine != null)
        {
            StopCoroutine(_matchRoutine);
            _matchRoutine = null;
        }

        if (IsRankedMode && MatchManager.I != null)
        {
            MatchManager.I.CancelRankedMatch();
        }

        HideMatchPopup();
    }

    private Sprite GetAttackSprite(BattleManager.AttackItemId itemId)
    {
        if (_attackVisualMap.TryGetValue(itemId, out AttackVisualEntry entry))
            return entry.iconSprite;

        return null;
    }

    private Sprite GetSupportSprite(BattleManager.SupportItemId itemId)
    {
        if (_supportVisualMap.TryGetValue(itemId, out SupportVisualEntry entry))
            return entry.iconSprite;

        return null;
    }

    private string GetAttackDisplayName(BattleManager.AttackItemId itemId)
    {
        if (_attackVisualMap.TryGetValue(itemId, out AttackVisualEntry entry) && !string.IsNullOrWhiteSpace(entry.displayName))
            return entry.displayName;

        return itemId.ToString();
    }

    private string GetSupportDisplayName(BattleManager.SupportItemId itemId)
    {
        if (_supportVisualMap.TryGetValue(itemId, out SupportVisualEntry entry) && !string.IsNullOrWhiteSpace(entry.displayName))
            return entry.displayName;

        return itemId.ToString();
    }

    private static void SetPreviewIcon(Image iconImage, Sprite sprite, bool show)
    {
        if (iconImage == null)
            return;

        iconImage.sprite = sprite;
        iconImage.enabled = show && sprite != null;
        iconImage.color = Color.white;
        iconImage.preserveAspect = true;
    }

    private Transform FindSafeArea()
    {
        if (transform.parent != null && transform.parent.name == "SafeArea")
            return transform.parent;

        GameObject safeAreaGo = GameObject.Find("SafeArea");
        return safeAreaGo != null ? safeAreaGo.transform : null;
    }

    private static TMP_Text FindTMP(Transform root, string path)
    {
        Transform t = root.Find(path);
        if (t == null)
            return null;

        TMP_Text tmp = t.GetComponent<TMP_Text>();
        if (tmp != null)
            return tmp;

        return t.GetComponentInChildren<TMP_Text>(true);
    }

    private static Button FindButton(Transform root, string path)
    {
        Transform t = root.Find(path);
        if (t == null)
            return null;

        return t.GetComponent<Button>();
    }

    private static GameObject FindGO(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.gameObject : null;
    }

    private static void SetTMP(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private static Image EnsureSlotIcon(Transform slotRoot)
    {
        if (slotRoot == null)
            return null;

        Transform iconTr = slotRoot.Find("IconImage");
        if (iconTr != null)
        {
            Image existing = iconTr.GetComponent<Image>();
            if (existing == null)
                existing = iconTr.gameObject.AddComponent<Image>();

            existing.raycastTarget = false;
            existing.preserveAspect = true;
            return existing;
        }

        GameObject go = new GameObject("IconImage", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(slotRoot, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(12f, 12f);
        rt.offsetMax = new Vector2(-12f, -12f);
        rt.localScale = Vector3.one;

        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;
        img.enabled = false;
        return img;
    }

    private ChoiceButtonRefs CacheChoiceButton(Transform buttonRoot, bool attackButton)
    {
        if (buttonRoot == null)
            return null;

        ChoiceButtonRefs refs = new ChoiceButtonRefs();
        refs.button = buttonRoot.GetComponent<Button>();
        refs.background = buttonRoot.GetComponent<Image>();

        Transform iconTr = buttonRoot.Find("IconImage");
        if (iconTr != null)
            refs.iconImage = iconTr.GetComponent<Image>();

        Transform nameTr = buttonRoot.Find("NameText");
        if (nameTr != null)
            refs.nameText = nameTr.GetComponent<TMP_Text>();

        Transform frameTr = buttonRoot.Find("SelectedFrame");
        if (frameTr != null)
            refs.selectedFrame = frameTr.gameObject;

        if (refs.iconImage == null)
            refs.iconImage = CreateChoiceIcon(buttonRoot);

        if (refs.nameText == null)
            refs.nameText = CreateChoiceNameText(buttonRoot);

        if (refs.selectedFrame == null)
            refs.selectedFrame = CreateChoiceSelectedFrame(buttonRoot);

        if (refs.selectedFrame != null)
            refs.selectedFrame.SetActive(false);

        return refs;
    }

    private static Image CreateChoiceIcon(Transform root)
    {
        GameObject go = new GameObject("IconImage", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(14f, 0f);
        rt.sizeDelta = new Vector2(52f, 52f);

        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;
        return img;
    }

    private static TMP_Text CreateChoiceNameText(Transform root)
    {
        GameObject go = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(root, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(80f, 10f);
        rt.offsetMax = new Vector2(-16f, -10f);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.text = string.Empty;
        tmp.color = Color.black;
        return tmp;
    }

    private static GameObject CreateChoiceSelectedFrame(Transform root)
    {
        GameObject go = new GameObject("SelectedFrame", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = new Color(1f, 0.85f, 0.2f, 0.22f);

        return go;
    }

    // 로비 티어 표시
    private void HandleRankedRecordCacheChanged()
    {
        ApplyRankedRecordUi();
    }

    private void ApplyRankedRecordUi()
    {
        if (!RankedRecordCache.HasLoaded)
        {
            if (mmrText != null)
                mmrText.text = "-";

            if (detailVictoryText != null)
                detailVictoryText.text = "0";

            if (detailDrawText != null)
                detailDrawText.text = "0";

            if (detailDefeatText != null)
                detailDefeatText.text = "0";

            return;
        }

        TierVisualEntry tier = GetTierVisualByMmr(RankedRecordCache.CurrentMmr);

        if (_tierText != null)
            _tierText.text = tier != null && !string.IsNullOrWhiteSpace(tier.tierName)
                ? tier.tierName
                : tierLabel;

        if (mmrText != null)
            mmrText.text = RankedRecordCache.CurrentMmr.ToString();

        if (tierIconImage != null)
        {
            tierIconImage.sprite = tier != null ? tier.iconSprite : null;
            tierIconImage.enabled = tierIconImage.sprite != null;
        }

        if (detailVictoryText != null)
            detailVictoryText.text = RankedRecordCache.Victory.ToString();

        if (detailDrawText != null)
            detailDrawText.text = RankedRecordCache.Draw.ToString();

        if (detailDefeatText != null)
            detailDefeatText.text = RankedRecordCache.Defeat.ToString();
    }

    private TierVisualEntry GetTierVisualByMmr(int mmr)
    {
        for (int i = 0; i < tierVisualEntries.Count; i++)
        {
            TierVisualEntry entry = tierVisualEntries[i];
            if (entry == null)
                continue;

            if (mmr >= entry.minMmr && mmr <= entry.maxMmr)
                return entry;
        }

        return null;
    }

    private void RefreshNicknameFromBackend()
    {
        string resolved = ResolveBackendNicknameSafe();

        if (!string.IsNullOrWhiteSpace(resolved))
            nickname = resolved;
    }

    private string ResolveBackendNicknameSafe()
    {
        try
        {
            // 1순위: Backend.UserNickName 프로퍼티 직접 시도
            var prop = typeof(BackEnd.Backend).GetProperty(
                "UserNickName",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static);

            if (prop != null)
            {
                object value = prop.GetValue(null, null);
                string nick = value != null ? value.ToString() : string.Empty;

                if (!string.IsNullOrWhiteSpace(nick))
                    return nick;
            }
        }
        catch
        {
            // ignore
        }

        return nickname; // fallback
    }
}