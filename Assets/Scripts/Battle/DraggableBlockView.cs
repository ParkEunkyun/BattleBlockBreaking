using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggableBlockView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image sealOverlayImage;

    private IDragBlockOwner _owner;
    private int _slotIndex;

    public RectTransform RectTransform => transform as RectTransform;

    private Vector2 _lastDragScreenPos;
    private bool _hasLastDragScreenPos;

    [SerializeField] private float dragUpdateMinDistance = 10f;

    private void Awake()
    {
        EnsureSealOverlay();
    }

    /// <summary>BattleManager / NormalManager °ø¿ë Setup</summary>
    public void Setup(IDragBlockOwner owner, int slotIndex)
    {
        _owner = owner;
        _slotIndex = slotIndex;

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
    }

    public void SetBackgroundColor(Color color)
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (backgroundImage != null)
            backgroundImage.color = color;
    }

    public void SetSealOverlay(bool show, Sprite sprite)
    {
        EnsureSealOverlay();

        if (sealOverlayImage == null)
            return;

        sealOverlayImage.transform.SetAsLastSibling();
        sealOverlayImage.sprite = sprite;
        sealOverlayImage.color = Color.white;
        sealOverlayImage.preserveAspect = true;
        sealOverlayImage.enabled = show && sprite != null;
        sealOverlayImage.gameObject.SetActive(show && sprite != null);
    }

    private void EnsureSealOverlay()
    {
        if (sealOverlayImage != null)
            return;

        Transform found = transform.Find("SealOverlay");
        if (found != null)
        {
            sealOverlayImage = found.GetComponent<Image>();
            if (sealOverlayImage == null)
                sealOverlayImage = found.gameObject.AddComponent<Image>();

            sealOverlayImage.raycastTarget = false;
            sealOverlayImage.enabled = false;
            sealOverlayImage.transform.SetAsLastSibling();
            return;
        }

        GameObject go = new GameObject("SealOverlay", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        sealOverlayImage = go.GetComponent<Image>();
        sealOverlayImage.raycastTarget = false;
        sealOverlayImage.enabled = false;
        sealOverlayImage.transform.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_owner == null || !_owner.CanAcceptUserInput())
            return;

        _hasLastDragScreenPos = false;
        _lastDragScreenPos = eventData.position;
        _owner.OnBeginDragSlot(_slotIndex, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_owner == null || !_owner.CanAcceptUserInput())
            return;

        Vector2 currentPos = eventData.position;

        if (_hasLastDragScreenPos)
        {
            float sqrDist = (currentPos - _lastDragScreenPos).sqrMagnitude;
            float minSqr = dragUpdateMinDistance * dragUpdateMinDistance;

            if (sqrDist < minSqr)
                return;
        }

        _lastDragScreenPos = currentPos;
        _hasLastDragScreenPos = true;

        _owner.OnDragSlot(_slotIndex, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _hasLastDragScreenPos = false;

        if (_owner == null || !_owner.CanAcceptUserInput())
            return;

        _owner.OnEndDragSlot(_slotIndex, eventData);
    }
}