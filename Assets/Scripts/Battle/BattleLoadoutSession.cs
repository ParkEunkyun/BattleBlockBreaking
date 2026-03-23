using System.Collections.Generic;

public static class BattleLoadoutSession
{
    public static readonly List<BattleManager.AttackItemId> SelectedAttackIds = new List<BattleManager.AttackItemId>();
    public static readonly List<BattleManager.SupportItemId> SelectedSupportIds = new List<BattleManager.SupportItemId>();
    public static bool IsRankedMode = true;

    public static void SetLoadout(
        IList<BattleManager.AttackItemId> attackIds,
        IList<BattleManager.SupportItemId> supportIds,
        bool isRankedMode)
    {
        SelectedAttackIds.Clear();
        SelectedSupportIds.Clear();

        if (attackIds != null)
        {
            for (int i = 0; i < attackIds.Count; i++)
                SelectedAttackIds.Add(attackIds[i]);
        }

        if (supportIds != null)
        {
            for (int i = 0; i < supportIds.Count; i++)
                SelectedSupportIds.Add(supportIds[i]);
        }

        IsRankedMode = isRankedMode;
    }

    public static bool HasValidLoadout()
    {
        return SelectedAttackIds.Count >= 3 && SelectedSupportIds.Count >= 2;
    }

    public static void Clear()
    {
        SelectedAttackIds.Clear();
        SelectedSupportIds.Clear();
        IsRankedMode = true;
    }
}