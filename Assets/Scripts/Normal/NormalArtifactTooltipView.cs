using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class NormalArtifactTooltipView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Texts")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text effectText;

    private readonly StringBuilder _sb = new StringBuilder(256);

    private void Awake()
    {
        AutoBind();
        Hide();
    }

    private void AutoBind()
    {
        if (root == null)
            root = gameObject;

        if (canvasGroup == null)
            canvasGroup = root.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = root.AddComponent<CanvasGroup>();

        if (nameText == null)
            nameText = FindTMP(root.transform, "NameText");

        if (levelText == null)
            levelText = FindTMP(root.transform, "LevelText");

        if (effectText == null)
            effectText = FindTMP(root.transform, "EffectText");

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    public void Show(NormalArtifactDefinition def, int level)
    {
        if (def == null)
        {
            Hide();
            return;
        }

        AutoBind();

        int safeLevel = Mathf.Clamp(level, 1, 10);

        if (nameText != null)
            nameText.text = def.DisplayNameSafe;

        if (levelText != null)
            levelText.text = $"Lv.{safeLevel}";

        if (effectText != null)
            effectText.text = BuildEffectText(def, safeLevel);

        root.SetActive(true);
        canvasGroup.alpha = 1f;
    }

    public void Hide()
    {
        AutoBind();

        canvasGroup.alpha = 0f;

        if (root != null)
            root.SetActive(false);
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

        if (_sb.Length <= 0 && !string.IsNullOrWhiteSpace(def.description))
            _sb.Append(def.description);

        return _sb.ToString().Trim();
    }

    private static TMP_Text FindTMP(Transform root, string path)
    {
        if (root == null || string.IsNullOrWhiteSpace(path))
            return null;

        Transform tr = root.Find(path);
        return tr != null ? tr.GetComponent<TMP_Text>() : null;
    }
}