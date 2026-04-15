using UnityEngine.EventSystems;

/// <summary>
/// DraggableBlockView가 콜백을 전달할 대상 인터페이스.
/// BattleManager와 NormalManager 모두 이 인터페이스를 구현합니다.
/// </summary>
public interface IDragBlockOwner
{
    bool CanAcceptUserInput();
    void OnBeginDragSlot(int slotIndex, PointerEventData eventData);
    void OnDragSlot(int slotIndex, PointerEventData eventData);
    void OnEndDragSlot(int slotIndex, PointerEventData eventData);
}
