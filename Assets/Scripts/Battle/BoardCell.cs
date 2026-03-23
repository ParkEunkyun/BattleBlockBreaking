using UnityEngine;
using UnityEngine.UI;

public class BoardCell : MonoBehaviour
{
    [SerializeField] private Image baseImage;
    [SerializeField] private Image specialIconImage;

    private void Awake()
    {
        EnsureRefs();
    }

    private void Reset()
    {
        EnsureRefs();
    }

    public void SetVisual(Color baseColor, Sprite iconSprite, bool showIcon)
    {
        EnsureRefs();

        if (baseImage != null)
            baseImage.color = baseColor;

        if (specialIconImage != null)
        {
            specialIconImage.sprite = iconSprite;
            specialIconImage.enabled = showIcon && iconSprite != null;
            specialIconImage.color = Color.white;
            specialIconImage.preserveAspect = true;
        }
    }

    private void EnsureRefs()
    {
        if (baseImage == null)
            baseImage = GetComponent<Image>();

        if (specialIconImage == null)
        {
            Transform icon = transform.Find("SpecialIcon");
            if (icon != null)
                specialIconImage = icon.GetComponent<Image>();
        }

        if (specialIconImage == null)
            specialIconImage = CreateIconImage();
    }

    private Image CreateIconImage()
    {
        GameObject go = new GameObject("SpecialIcon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.enabled = false;
        return img;
    }
}