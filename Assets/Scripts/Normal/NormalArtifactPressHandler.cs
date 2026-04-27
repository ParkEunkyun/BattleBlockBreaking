using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NormalArtifactPressHandler : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    [SerializeField] private float defaultLongPressSeconds = 0.45f;
    [SerializeField] private bool debugLog = false;

    private Action _onShortPress;
    private Action _onLongPressStart;
    private Action _onLongPressEnd;

    private float _longPressSeconds;
    private bool _pressed;
    private bool _longPressed;
    private float _pressStartTime;
    private int _pointerId;

    private RectTransform _rectTransform;
    private Canvas _canvas;

    public void Bind(
        Action onShortPress,
        Action onLongPressStart,
        Action onLongPressEnd,
        float longPressSeconds = -1f)
    {
        _onShortPress = onShortPress;
        _onLongPressStart = onLongPressStart;
        _onLongPressEnd = onLongPressEnd;
        _longPressSeconds = longPressSeconds > 0f ? longPressSeconds : defaultLongPressSeconds;

        Cache();
        EnsureRaycastReceiver();
    }

    private void Awake()
    {
        Cache();
        EnsureRaycastReceiver();
    }

    private void Cache()
    {
        if (_rectTransform == null)
            _rectTransform = transform as RectTransform;

        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        if (!_pressed || _longPressed)
            return;

        if (Time.unscaledTime - _pressStartTime < _longPressSeconds)
            return;

        _longPressed = true;

        if (debugLog)
            Debug.Log($"[NormalArtifactPressHandler] LongPress Start: {name}");

        _onLongPressStart?.Invoke();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pressed = true;
        _longPressed = false;
        _pressStartTime = Time.unscaledTime;
        _pointerId = eventData.pointerId;

        if (debugLog)
            Debug.Log($"[NormalArtifactPressHandler] PointerDown: {name}");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_pressed)
            return;

        if (eventData.pointerId != _pointerId)
            return;

        bool wasLongPressed = _longPressed;

        _pressed = false;
        _longPressed = false;

        if (debugLog)
            Debug.Log($"[NormalArtifactPressHandler] PointerUp: {name} / long={wasLongPressed}");

        if (wasLongPressed)
        {
            _onLongPressEnd?.Invoke();
            return;
        }

        _onShortPress?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_pressed)
            return;

        if (IsPointerInside(eventData))
            return;

        CancelPress();
    }

    private void OnDisable()
    {
        CancelPress();
    }

    private void CancelPress()
    {
        if (!_pressed)
            return;

        bool wasLongPressed = _longPressed;

        _pressed = false;
        _longPressed = false;

        if (debugLog)
            Debug.Log($"[NormalArtifactPressHandler] Cancel: {name} / long={wasLongPressed}");

        if (wasLongPressed)
            _onLongPressEnd?.Invoke();
    }

    private bool IsPointerInside(PointerEventData eventData)
    {
        Cache();

        if (_rectTransform == null)
            return false;

        Camera cam = null;

        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = _canvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(
            _rectTransform,
            eventData.position,
            cam);
    }

    private void EnsureRaycastReceiver()
    {
        Graphic graphic = GetComponent<Graphic>();

        if (graphic == null)
        {
            Image image = gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            return;
        }

        graphic.raycastTarget = true;
    }
}