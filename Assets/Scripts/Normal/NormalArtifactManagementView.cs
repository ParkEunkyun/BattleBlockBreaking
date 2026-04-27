using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class NormalArtifactManagementView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Main")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text gradeText;
    [SerializeField] private TMP_Text effectText;
    [SerializeField] private TMP_Text materialText;
    [SerializeField] private TMP_Text lockStateText;

    [Header("Buttons")]
    [SerializeField] private Button enhanceButton;
    [SerializeField] private Button dismantleButton;
    [SerializeField] private Button lockButton;
    [SerializeField] private Button backButton;

    private readonly StringBuilder _sb = new StringBuilder(512);

    private NormalArtifactDefinition _currentDef;
    private Action _onBack;

    private void Awake()
    {
        AutoBind();

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(CloseByBackButton);
        }

        if (enhanceButton != null)
        {
            enhanceButton.onClick.RemoveAllListeners();
            enhanceButton.onClick.AddListener(() =>
            {
                // TODO: 강화 재료/골드 소모 로직 붙일 위치
                RefreshCurrent();
            });
        }

        if (dismantleButton != null)
        {
            dismantleButton.onClick.RemoveAllListeners();
            dismantleButton.onClick.AddListener(() =>
            {
                // TODO: 분해 확인 팝업 붙일 위치
                RefreshCurrent();
            });
        }

        if (lockButton != null)
        {
            lockButton.onClick.RemoveAllListeners();
            lockButton.onClick.AddListener(() =>
            {
                // TODO: 잠금 저장 로직 붙일 위치
                RefreshCurrent();
            });
        }

        Hide();
    }

    private void AutoBind()
    {
        if (root == null)
            root = gameObject;

        if (iconImage == null)
            iconImage = FindComp<Image>("Icon");

        if (nameText == null)
            nameText = FindTMP("NameText");

        if (levelText == null)
            levelText = FindTMP("LevelText");

        if (gradeText == null)
            gradeText = FindTMP("GradeText");

        if (effectText == null)
            effectText = FindTMP("EffectText");

        if (materialText == null)
            materialText = FindTMP("MaterialText");

        if (lockStateText == null)
            lockStateText = FindTMP("LockStateText");

        if (enhanceButton == null)
            enhanceButton = FindComp<Button>("EnhanceButton");

        if (dismantleButton == null)
            dismantleButton = FindComp<Button>("DismantleButton");

        if (lockButton == null)
            lockButton = FindComp<Button>("LockButton");

        if (backButton == null)
            backButton = FindComp<Button>("BackButton");
    }

    public void Show(NormalArtifactDefinition def, int level, bool equipped, Action onBack)
    {
        _currentDef = def;
        _onBack = onBack;

        if (root != null)
            root.SetActive(true);

        Refresh(def, level, equipped);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    private void RefreshCurrent()
    {
        if (_currentDef == null)
            return;

        Refresh(_currentDef, NormalArtifactLevelUtility.GetLevel(_currentDef), false);
    }

    private void Refresh(NormalArtifactDefinition def, int level, bool equipped)
    {
        if (def == null)
            return;

        int safeLevel = Mathf.Clamp(level, 1, 10);

        if (iconImage != null)
        {
            iconImage.sprite = def.icon;
            iconImage.enabled = def.icon != null;
            iconImage.preserveAspect = true;
        }

        if (nameText != null)
            nameText.text = def.DisplayNameSafe;

        if (levelText != null)
            levelText.text = $"Lv.{safeLevel}";

        if (gradeText != null)
            gradeText.text = GradeToString(def.grade);

        if (effectText != null)
            effectText.text = BuildEffectText(def, safeLevel);

        if (materialText != null)
            materialText.text = "재료: TODO\n강화/분해 재료 테이블 연결 예정";

        if (lockStateText != null)
            lockStateText.text = equipped ? "장착 중" : "미장착";
    }

    private string BuildEffectText(NormalArtifactDefinition def, int level)
    {
        _sb.Clear();

        string equip = def.GetEquipEffectSummary(level);
        string owned = def.GetOwnedEffectSummary(level);

        if (!string.IsNullOrWhiteSpace(equip))
            _sb.AppendLine(equip);

        if (!string.IsNullOrWhiteSpace(owned))
            _sb.AppendLine(owned);

        if (!string.IsNullOrWhiteSpace(def.description))
        {
            if (_sb.Length > 0)
                _sb.AppendLine();

            _sb.Append(def.description);
        }

        return _sb.ToString().Trim();
    }

    private void CloseByBackButton()
    {
        Hide();
        _onBack?.Invoke();
    }

    private TMP_Text FindTMP(string path)
    {
        Transform tr = transform.Find(path);
        return tr != null ? tr.GetComponent<TMP_Text>() : null;
    }

    private T FindComp<T>(string path) where T : Component
    {
        Transform tr = transform.Find(path);
        return tr != null ? tr.GetComponent<T>() : null;
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
}