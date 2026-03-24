using UnityEngine;
using UnityEngine.EventSystems;

public class MobileDragTuning : MonoBehaviour
{
    [SerializeField] private int mobileDragThreshold = 4;
    [SerializeField] private int desktopDragThreshold = 8;

    private void Awake()
    {
        if (EventSystem.current == null)
            return;

#if UNITY_ANDROID || UNITY_IOS
        EventSystem.current.pixelDragThreshold = mobileDragThreshold;
#else
        EventSystem.current.pixelDragThreshold = desktopDragThreshold;
#endif
    }
}