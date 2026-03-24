using System.Collections.Generic;

public static class BattleMatchSession
{
    public static GameMode Mode = GameMode.None;

    public static string MyNickname = "";
    public static string OpponentNickname = "";

    public static string MySessionId = "";
    public static string OpponentSessionId = "";

    public static string MatchCardInDate = "";
    public static string InGameRoomToken = "";
    public static string InGameServerAddress = "";
    public static ushort InGameServerPort = 0;
    public static bool IsSandbox = false;

    public static bool IsHost = false;
    public static int MatchSeed = 0;

    public static readonly List<BattleManager.AttackItemId> OpponentAttackIds = new List<BattleManager.AttackItemId>();
    public static readonly List<BattleManager.SupportItemId> OpponentSupportIds = new List<BattleManager.SupportItemId>();

    public static bool HasValidRankedMatch =>
        Mode == GameMode.Ranked &&
        !string.IsNullOrWhiteSpace(InGameRoomToken) &&
        !string.IsNullOrWhiteSpace(InGameServerAddress) &&
        InGameServerPort > 0;

    public static void Clear()
    {
        Mode = GameMode.None;

        MyNickname = "";
        OpponentNickname = "";

        MySessionId = "";
        OpponentSessionId = "";

        MatchCardInDate = "";
        InGameRoomToken = "";
        InGameServerAddress = "";
        InGameServerPort = 0;
        IsSandbox = false;

        IsHost = false;
        MatchSeed = 0;

        OpponentAttackIds.Clear();
        OpponentSupportIds.Clear();
    }
}