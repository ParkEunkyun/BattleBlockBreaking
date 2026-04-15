using UnityEngine;

/// <summary>아티팩트 카테고리 (6종)</summary>
public enum ArtifactCategory
{
    ScoreBoost,
    ComboBoost,
    ShapeReroll,
    EmergencyClear,
    SecondChance,
    LuckyBonus
}

/// <summary>아티팩트 등급</summary>
public enum ArtifactGrade
{
    Normal = 0,
    Rare = 1,
    Epic = 2,
    Unique = 3,
    Legend = 4
}

/// <summary>
/// 발동 방식.
/// PassiveOnly  : 패시브만 (노말/레어)
/// AutoActive   : 조건 충족 시 자동 발동 (ScoreBoost, ComboBoost, SecondChance, LuckyBonus)
/// ManualActive : 슬롯 탭으로 수동 발동 (ShapeReroll, EmergencyClear)
/// </summary>
public enum ArtifactTriggerType
{
    PassiveOnly,
    AutoActive,
    ManualActive
}

/// <summary>
/// 아티팩트 1개의 모든 데이터를 담는 ScriptableObject.
/// Create → Normal → ArtifactDefinition 으로 생성하세요.
/// </summary>
[CreateAssetMenu(fileName = "ArtifactDef", menuName = "BBB/Normal/ArtifactDefinition")]
public class NormalArtifactDefinition : ScriptableObject
{
    [Header("기본 정보")]
    public ArtifactCategory category;
    public ArtifactGrade grade;
    public string displayName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;

    [Header("발동 방식")]
    public ArtifactTriggerType triggerType;

    // ── 쿨다운 (라운드 단위, PassiveOnly는 0) ──────
    [Header("쿨다운 (세트 단위)")]
    public int cooldownSets;

    // ── 패시브 수치 ────────────────────────────────
    [Header("패시브 - 공통")]
    public int bonusScorePerCell;          // ScoreBoost: 셀당 추가 점수
    public float lineClearBonusMultiplier;   // ScoreBoost: 클리어 보너스 배율 추가 (0.2 = +20%)

    [Header("패시브 - 콤보")]
    public int bonusScorePerCombo;         // ComboBoost: 콤보당 추가 점수
    public float milestoneBonusMultiplier;   // ComboBoost: 마일스톤 보너스 배율 추가
    public int comboFailExemptCount;       // ComboBoost: 콤보 실패 면제 횟수

    [Header("패시브 - 리롤")]
    public int startTokenCount;            // ShapeReroll: 시작 토큰 수
    public int tokenRechargeIntervalSets;  // ShapeReroll: 자동 충전 간격 (0=없음)

    [Header("패시브 - 클리어")]
    public int startChargeCount;           // EmergencyClear: 시작 사용권 수
    public int chargeRechargeIntervalSets; // EmergencyClear: 자동 충전 간격 (0=없음)

    [Header("패시브 - 부활")]
    public int reviveCount;                // SecondChance: 부활 횟수
    public float boardClearRatio;            // SecondChance: 부활 시 보드 클리어 비율 (0~1)
    public float reviveBonusScoreRatio;      // SecondChance: 부활 시 점수 보너스 비율

    [Header("패시브 - 럭키")]
    public float luckyChanceX2;             // LuckyBonus: ×2 확률 (0~1)
    public float luckyChanceX3;             // LuckyBonus: ×3 확률 (0~1)
    public float dropRateMultiplier;        // LuckyBonus: 드랍률 배율 (1.0=기본, 2.0=2배)
    public int dropItemKeepExtraSets;     // LuckyBonus: 미획득 아이템 유지 추가 세트

    // ── 액티브 수치 ────────────────────────────────
    [Header("액티브 효과 수치")]
    public int activeScoreMultiplierDuration; // ScoreBoost: 배율 지속 세트 수
    public float activeScoreMultiplier;         // ScoreBoost: 배율 값 (1.5, 2.0, 3.0)
    public int activeBonusScorePerClear;      // ScoreBoost: 활성 중 클리어마다 추가 점수

    public int activeComboCounterBonus;       // ComboBoost: 즉시 부여 콤보 카운터
    public int activeComboInstantScore;       // ComboBoost: 콤보×N 즉시 점수 (콤보당 배율)
    public int activeComboFixDuration;        // ComboBoost: 배율 고정 세트 수

    public bool activeRerollAll;               // ShapeReroll: 전체 교체 여부
    public bool activeRerollSelective;         // ShapeReroll: 선택 교체 가능 여부
    public bool activeRerollFreeChoice;        // ShapeReroll: 풀에서 직접 선택 (전설)

    public int activeClearAreaSize;           // EmergencyClear: 클리어 영역 크기 (3=3×3, 5=5×5)
    public bool activeClearRowColMode;         // EmergencyClear: 행+열 동시 모드
    public float activeClearBoardRatio;         // EmergencyClear: 보드 비율 클리어 (전설: 0.5)
    public int activeClearBonusScorePerCell;  // EmergencyClear: 제거 셀당 보너스 점수

    public float activePreReviveClearRatio;     // SecondChance: 선제 클리어 비율 (에픽)
    public float activeReviveBonusMultiplierDuration; // SecondChance: 부활 후 배율 지속 세트

    public bool activeLuckyGuarantee;          // LuckyBonus: 다음 클리어 럭키 확정
    public int activeLuckyBoostDuration;      // LuckyBonus: 확률 2배 지속 세트
    public bool activeLuckyStackMax;           // LuckyBonus: 스택 최대치 고정 (전설)
    public int activeLuckyStackMaxDuration;   // LuckyBonus: 스택 고정 지속 세트
    public int activeDropSpawnCount;          // LuckyBonus: 즉시 드랍 아이템 스폰 수

    // ── 자동 발동 트리거 수치 ─────────────────────
    [Header("자동 발동 트리거")]
    public int autoTriggerIntervalSets;       // ScoreBoost: N세트마다 발동
    public int autoTriggerComboCount;         // ComboBoost: N연속 달성 시
    public int autoTriggerNoLuckyStreak;      // LuckyBonus: N세트 연속 실패 시
}