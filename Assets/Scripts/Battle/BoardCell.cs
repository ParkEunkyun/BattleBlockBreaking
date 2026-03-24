using UnityEngine;
using UnityEngine.UI;

public class BoardCell : MonoBehaviour
{
    [SerializeField] private Image baseImage;
    [SerializeField] private Image blockSpriteImage;
    [SerializeField] private Image specialIconImage;

    private void Awake()
    {
        EnsureRefs();
    }

    private void Reset()
    {
        EnsureRefs();
    }

    public void SetVisual(Color baseColor, Sprite blockSprite, bool showBlockSprite, Sprite iconSprite, bool showIcon)
    {
        EnsureRefs();

        if (baseImage != null)
            baseImage.color = baseColor;

        if (blockSpriteImage != null)
        {
            blockSpriteImage.transform.SetSiblingIndex(1);
            blockSpriteImage.sprite = blockSprite;
            blockSpriteImage.enabled = showBlockSprite && blockSprite != null;
            blockSpriteImage.color = Color.white;
            blockSpriteImage.preserveAspect = false;
        }

        if (specialIconImage != null)
        {
            specialIconImage.transform.SetAsLastSibling();
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

        if (blockSpriteImage == null)
        {
            Transform t = transform.Find("BlockSprite");
            if (t != null)
                blockSpriteImage = t.GetComponent<Image>();
        }

        if (specialIconImage == null)
        {
            Transform t = transform.Find("SpecialIcon");
            if (t != null)
                specialIconImage = t.GetComponent<Image>();
        }

        if (blockSpriteImage == null)
            blockSpriteImage = CreateImage("BlockSprite", 1);

        if (specialIconImage == null)
            specialIconImage = CreateImage("SpecialIcon", 2);

        if (baseImage != null)
            baseImage.transform.SetSiblingIndex(0);

        if (blockSpriteImage != null)
            blockSpriteImage.transform.SetSiblingIndex(1);

        if (specialIconImage != null)
            specialIconImage.transform.SetAsLastSibling();
    }

    private Image CreateImage(string name, int siblingIndex)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        go.transform.SetSiblingIndex(siblingIndex);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.enabled = false;

        if (name == "SpecialIcon")
            go.transform.SetAsLastSibling();

        return img;
    }
}