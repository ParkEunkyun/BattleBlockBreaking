using System.Collections.Generic;

/// <summary>
/// 노말 모드 한 판의 결과 데이터.
/// 게임 종료 시 NormalManager가 채워서 결과창에 전달합니다.
/// </summary>
public class NormalResultData
{
    // ── 점수 ──────────────────────────────────────
    public int FinalScore;
    public int PrevBestScore;
    public int NewBestScore;
    public BestScoreState ScoreState;

    // ── 콤보 기록 ─────────────────────────────────
    public int MaxCombo;
    public int TotalClearCount;       // 전체 라인 클리어 횟수

    // ── 멀티 라인 기록 ────────────────────────────
    public int SingleCount;
    public int DoubleCount;
    public int TripleCount;
    public int QuadCount;
    public int PerfectCount;          // 5줄+

    // ── 마일스톤 달성 ─────────────────────────────
    public bool Reached10Combo;
    public bool Reached20Combo;
    public bool Reached30Combo;

    // ── 아티팩트 ──────────────────────────────────
    public int ArtifactActivationCount; // 자동+수동 발동 합계

    // ── 드랍 아이템 (결과창에서 일괄 지급) ────────
    public int GoldEarned;
    public int EnhanceStoneBasicCount;   // 강화석 하
    public int EnhanceStoneMidCount;     // 강화석 중
    public int EnhanceStoneHighCount;    // 강화석 상

    /// <summary>드랍 아이템이 하나라도 있는지 확인</summary>
    public bool HasAnyDrop =>
        GoldEarned > 0 ||
        EnhanceStoneBasicCount > 0 ||
        EnhanceStoneMidCount > 0 ||
        EnhanceStoneHighCount > 0;
}

/// <summary>최고기록 대비 이번 점수 상태</summary>
public enum BestScoreState
{
    Below,   // 최고기록보다 낮음
    Equal,   // 동일
    New      // 갱신
}

/// <summary>
/// 드랍 아이템 종류
/// </summary>
public enum DropItemType
{
    Gold,
    EnhanceStoneBasic,
    EnhanceStoneMid,
    EnhanceStoneHigh
}

/// <summary>
/// 보드 위에 스폰된 드랍 아이템 인스턴스
/// </summary>
public class BoardDropItem
{
    public DropItemType ItemType;
    public int BoardX;
    public int BoardY;
    public int RemainingSetCount; // 최대 3세트 후 사라짐
}