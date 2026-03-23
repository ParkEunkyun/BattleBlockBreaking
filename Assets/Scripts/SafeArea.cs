using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    void Start()
    {
        RectTransform rt = GetComponent<RectTransform>();
        Rect safe = Screen.safeArea;

        // safeArea에서 아래쪽(Height) 조정하지 않음
        Vector2 anchorMin = safe.position;
        Vector2 anchorMax = safe.position + safe.size;

        // 아래쪽은 고정 (0)
        anchorMin.y = 0;

        anchorMin.x /= Screen.width;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
    }
}
