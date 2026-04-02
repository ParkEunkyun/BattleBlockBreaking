using TMPro;
using UnityEngine;

public class BattleAttackTelegraphView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject rootObject;

    [Header("Optional")]
    [SerializeField] private TMP_Text attackNameText;

    private void Awake()
    {
        SetPlanned(false, BattleManager.BattleItemId.None);
    }

    public void SetPlanned(bool visible, BattleManager.BattleItemId itemId)
    {
        if (rootObject != null)
            rootObject.SetActive(visible);

        if (!visible)
        {
            if (attackNameText != null)
                attackNameText.text = string.Empty;
            return;
        }

        if (attackNameText != null)
            attackNameText.text = GetAttackDisplayName(itemId);
    }

    private string GetAttackDisplayName(BattleManager.BattleItemId itemId)
    {
        switch (itemId)
        {
            case BattleManager.BattleItemId.AttackObstacle2:
                return "장애물 공격 예정";
            case BattleManager.BattleItemId.AttackSealRandomSlot:
                return "봉인 공격 예정";
            case BattleManager.BattleItemId.AttackCurseBlock:
                return "저주 블록 공격 예정";
            case BattleManager.BattleItemId.AttackDisableItemUse:
                return "아이템 금지 공격 예정";
            case BattleManager.BattleItemId.AttackDeleteRandomLine:
                return "줄 삭제 공격 예정";
            case BattleManager.BattleItemId.AttackBomb3x3:
                return "폭탄 공격 예정";
            default:
                return "공격 예정";
        }
    }
}