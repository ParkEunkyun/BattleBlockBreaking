using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class NormalResultDropEntryView : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text countText;

    public void SetData(Sprite icon, string label, int count)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
        }

        if (nameText != null)
            nameText.text = label;

        if (countText != null)
            countText.text = $"x{count}";
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}