using UnityEngine;

public enum ArtifactGrade
{
    Normal = 0,
    Rare = 1,
    Epic = 2,
    Unique = 3,
    Legend = 4
}

public enum NormalArtifactType
{
    Passive = 0,
    ConditionalPassive = 1,
    Active = 2,
    LegendaryPassive = 3,
    LegendaryActive = 4
}

public enum NormalArtifactOwnedEffectType
{
    None = 0,
    TotalScoreBonus = 1,
    LineClearScoreBonus = 2,
    ComboScoreBonus = 3
}

public enum NormalArtifactEquipEffectType
{
    None = 0,

    // 기본 점수형
    TotalScoreBonus = 10,
    LineClearScoreBonus = 11,
    ComboScoreBonus = 12,
    Score3CellBlockBonus = 13,
    Score4CellBlockBonus = 14,
    Score5CellBlockBonus = 15,
    HorizontalLineScoreBonus = 16,
    VerticalLineScoreBonus = 17,

    // 조건 패시브형
    LastPlacementClearBonus = 100,
    LowSpaceClearBonus = 101,
    AfterUsingActiveNextClearBonus = 102,
    AfterUsingActiveNextRoundBonus = 103,
    StreakNextRoundBonus = 104,
    PerBlockPlacedNextClearBonus = 105,
    MultiLineNextRoundFirstClearBonus = 106,
    LargeBlockClearBonus = 107,
    IsolatedHoleStackNextClearBonus = 108,
    ReduceActiveCooldownOnLineClear = 109,

    // 액티브형
    RotateRemaining90 = 200,
    RotateRemainingMinus90 = 201,
    RotateRemaining180 = 202,
    MirrorRemainingHorizontal = 203,
    MirrorRemainingVertical = 204,
    RerollRemainingAll = 205,
    RebuildRemainingSafer = 206,
    // 전설형
    PreserveComboOnce = 300,
    EmergencySecondChance = 301,
    AmplifyBestEquippedArtifact = 302,
    GravityCollapseBoard = 303,
    DimensionWarpPlacement = 304,

    // 신규 점수 / 조건형
    LineClearFlatBonus = 400,
    PostClearFlatBonusByRemainingCells = 401,
    ComboBreakFlatBonus = 402,
    ChargeNextClearAfterPlacements = 403,

    // 신규 파밍형
    ItemDropRateMultiplier = 500,
    GoldDropRateMultiplier = 501,
    AllDropRateMultiplier = 502,

    // 신규 쿨다운 / 액티브형
    CooldownReduceChanceOnLineClear = 600,
    ActiveChargeRotate90 = 601,
    TimedTotalScoreBuff = 602,

    // 신규 전설형
    ComboPreserveChance = 700,
    SecondChanceSingleBlocks = 701
}

public enum NormalArtifactTriggerType
{
    None = 0,
    Always = 1,
    OnLastPlacementClear = 2,
    OnLowSpace = 3,
    AfterUsingActive = 4,
    OnConsecutiveRoundsClear = 5,
    OnBlockPlacedCount = 6,
    OnMultiLineClearInRound = 7,
    OnLargeBlockClear = 8,
    OnIsolatedHoleStack = 9,
    ManualButton = 10,
    OnComboBreak = 11,
    OnGameOver = 12
}

public enum NormalArtifactCooldownType
{
    None = 0,
    Round = 1,
    LineClear = 2,
    BlockPlaced = 3,
    GameOnce = 4
}

[CreateAssetMenu(fileName = "NormalArtifactDefinition", menuName = "BBB/Normal/Artifact Definition")]
public sealed class NormalArtifactDefinition : ScriptableObject
{
    private const int MaxLevel = 10;

    [Header("Identity")]
    public string artifactId;
    public string displayName;
    [TextArea(2, 5)] public string description;
    public ArtifactGrade grade = ArtifactGrade.Normal;
    public Sprite icon;

    [Header("Core")]
    public NormalArtifactType artifactType = NormalArtifactType.Passive;
    public NormalArtifactEquipEffectType equipEffectType = NormalArtifactEquipEffectType.None;
    public NormalArtifactOwnedEffectType ownedEffectType = NormalArtifactOwnedEffectType.None;
    public NormalArtifactTriggerType runtimeTriggerType = NormalArtifactTriggerType.Always;
    public NormalArtifactCooldownType cooldownType = NormalArtifactCooldownType.None;

    [Header("Runtime Flags")]
    public bool activeUsableOncePerRound = true;
    public bool applyToAllRemainingBlocks = true;
    public bool autoTriggerOnGameOver = false;

    [Header("Level Tables (Lv1~Lv10)")]
    public float[] equipValues = new float[MaxLevel];
    public float[] ownedValues = new float[MaxLevel];
    public int[] cooldownValues = new int[MaxLevel];

    [Header("Meta / Economy")]
    public int duplicateShardReward = 20;
    public int shardSellGold = 20;
    public int collectionPoint = 10;
    [TextArea(1, 3)] public string ownedEffectDescription;

    [Header("Optional Condition Params")]
    public int paramA;
    public int paramB;
    public float paramFloat;

    public bool IsActiveArtifact =>
        artifactType == NormalArtifactType.Active ||
        artifactType == NormalArtifactType.LegendaryActive;

    public bool IsLegendary =>
        grade == ArtifactGrade.Legend ||
        artifactType == NormalArtifactType.LegendaryPassive ||
        artifactType == NormalArtifactType.LegendaryActive;

    public bool UsesCooldown =>
        cooldownType != NormalArtifactCooldownType.None &&
        cooldownType != NormalArtifactCooldownType.GameOnce;

    public string DisplayNameSafe =>
        string.IsNullOrWhiteSpace(displayName) ? name : displayName;

    public float GetEquipValue(int level)
    {
        return GetFloatByLevel(equipValues, level);
    }

    public float GetOwnedValue(int level)
    {
        return GetFloatByLevel(ownedValues, level);
    }

    public int GetCooldownValue(int level)
    {
        return GetIntByLevel(cooldownValues, level);
    }

    public string GetOwnedEffectSummary(int level)
    {
        if (ownedEffectType == NormalArtifactOwnedEffectType.None)
            return string.Empty;

        float value = GetOwnedValue(level);

        switch (ownedEffectType)
        {
            case NormalArtifactOwnedEffectType.TotalScoreBonus:
                return $"보유 효과: 전체 점수 +{value:0.0}%";
            case NormalArtifactOwnedEffectType.LineClearScoreBonus:
                return $"보유 효과: 줄 클리어 점수 +{value:0.0}%";
            case NormalArtifactOwnedEffectType.ComboScoreBonus:
                return $"보유 효과: 콤보 점수 +{value:0.0}%";
            default:
                return string.Empty;
        }
    }

    public string GetEquipEffectSummary(int level)
    {
        float value = GetEquipValue(level);
        int cd = GetCooldownValue(level);

        switch (equipEffectType)
        {
            case NormalArtifactEquipEffectType.TotalScoreBonus:
                return $"장착 효과: 전체 점수 +{value:0.0}%";
            case NormalArtifactEquipEffectType.LineClearScoreBonus:
                return $"장착 효과: 줄 클리어 점수 +{value:0.0}%";
            case NormalArtifactEquipEffectType.ComboScoreBonus:
                return $"장착 효과: 콤보 점수 +{value:0.0}%";
            case NormalArtifactEquipEffectType.Score3CellBlockBonus:
                return $"장착 효과: 3칸 블록 점수 +{value:0.0}%";
            case NormalArtifactEquipEffectType.Score4CellBlockBonus:
                return $"장착 효과: 4칸 블록 점수 +{value:0.0}%";
            case NormalArtifactEquipEffectType.Score5CellBlockBonus:
                return $"장착 효과: 5칸 블록 점수 +{value:0.0}%";
            case NormalArtifactEquipEffectType.RotateRemaining90:
                return $"장착 효과: 남은 블록 전체 90도 회전 / 쿨다운 {cd}";
            case NormalArtifactEquipEffectType.RotateRemainingMinus90:
                return $"장착 효과: 남은 블록 전체 -90도 회전 / 쿨다운 {cd}";
            case NormalArtifactEquipEffectType.RotateRemaining180:
                return $"장착 효과: 남은 블록 전체 180도 회전 / 쿨다운 {cd}";
            case NormalArtifactEquipEffectType.MirrorRemainingHorizontal:
                return $"장착 효과: 남은 블록 전체 좌우반전 / 쿨다운 {cd}";
            case NormalArtifactEquipEffectType.MirrorRemainingVertical:
                return $"장착 효과: 남은 블록 전체 상하반전 / 쿨다운 {cd}";
            case NormalArtifactEquipEffectType.RerollRemainingAll:
                return $"장착 효과: 남은 블록 전체 재구성 / 쿨다운 {cd}";
            case NormalArtifactEquipEffectType.RebuildRemainingSafer:
                return $"장착 효과: 남은 블록 전체를 더 쉬운 형태로 재구성 / 쿨다운 {cd}";
            case NormalArtifactEquipEffectType.PreserveComboOnce:
                return $"장착 효과: 콤보 끊김 1회 무시 / 재충전 {cd}";
            case NormalArtifactEquipEffectType.EmergencySecondChance:
                return $"장착 효과: 게임 오버 직전 1회 응급 세트 지급 / 첫 클리어 +{value:0.0}%";
            case NormalArtifactEquipEffectType.AmplifyBestEquippedArtifact:
                return $"장착 효과: 가장 강한 다른 아티팩트 효과 {value:0.0}% 증폭";
            case NormalArtifactEquipEffectType.GravityCollapseBoard:
                return $"장착 효과: 보드 전체 낙하 후 줄 즉시 클리어 / 재사용 {cd} / 클리어 보너스 +{value:0.0}%";
            case NormalArtifactEquipEffectType.DimensionWarpPlacement:
                return $"장착 효과: 사용 후 다음 {Mathf.Max(1, paramA)}회 배치가 보드 점유 상태를 무시한다 / 쿨다운 {cd}";
            case NormalArtifactEquipEffectType.LineClearFlatBonus:
                return $"장착 효과: 줄 클리어 시 추가 +{value:0}점";
            case NormalArtifactEquipEffectType.PostClearFlatBonusByRemainingCells:
                return $"장착 효과: 줄 클리어 후 남은 블럭 셀이 {GetAllowedRemainingCells(level)}개 이하이면 추가 +{paramA}점";
            case NormalArtifactEquipEffectType.ComboBreakFlatBonus:
                return $"장착 효과: 콤보가 끊길 때 추가 +{value:0}점";
            case NormalArtifactEquipEffectType.ChargeNextClearAfterPlacements:
                return $"장착 효과: 블록 {Mathf.Max(1, paramA)}회 배치 후 다음 배치로 클리어 성공 시 추가 +{value:0}점";
            case NormalArtifactEquipEffectType.ItemDropRateMultiplier:
                return $"장착 효과: 골드를 제외한 아이템 드롭률 +{value:0}%";
            case NormalArtifactEquipEffectType.GoldDropRateMultiplier:
                return $"장착 효과: 골드 드롭률 +{value:0}%";
            case NormalArtifactEquipEffectType.AllDropRateMultiplier:
                return $"장착 효과: 골드와 아이템 드롭률 +{value:0}%";
            case NormalArtifactEquipEffectType.CooldownReduceChanceOnLineClear:
                return $"장착 효과: 줄 클리어 시 {value:0}% 확률로 액티브 쿨다운 감소";
            case NormalArtifactEquipEffectType.ActiveChargeRotate90:
                return $"장착 효과: 남은 블록 90도 회전 / 최대 {Mathf.Max(1, Mathf.RoundToInt(value))}회 사용 후 쿨다운 {cd}";
            case NormalArtifactEquipEffectType.TimedTotalScoreBuff:
                return $"장착 효과: 사용 시 {Mathf.Max(1, paramA)}라운드 동안 모든 점수 +{value:0}% / 쿨다운 {cd}";
            case NormalArtifactEquipEffectType.ComboPreserveChance:
                return $"장착 효과: {Mathf.Max(1, paramA)}콤보 이상에서 콤보 끊김 방어 {value:0}%";
            case NormalArtifactEquipEffectType.SecondChanceSingleBlocks:
                return "장착 효과: 게임오버 직전 1칸 블록 3개 지급 / 게임당 1회";
            default:
                return string.Empty;
        }
    }

    private int GetAllowedRemainingCells(int level)
    {
        return Mathf.CeilToInt(Mathf.Max(0, level - 1) * 0.5f);
    }

    private static float GetFloatByLevel(float[] values, int level)
    {
        if (values == null || values.Length == 0)
            return 0f;

        int index = Mathf.Clamp(level - 1, 0, values.Length - 1);
        return values[index];
    }

    private static int GetIntByLevel(int[] values, int level)
    {
        if (values == null || values.Length == 0)
            return 0;

        int index = Mathf.Clamp(level - 1, 0, values.Length - 1);
        return values[index];
    }

    private void OnValidate()
    {
        EnsureArraySize(ref equipValues, MaxLevel);
        EnsureArraySize(ref ownedValues, MaxLevel);
        EnsureArraySize(ref cooldownValues, MaxLevel);

        if (string.IsNullOrWhiteSpace(artifactId))
            artifactId = name;
    }

    private static void EnsureArraySize(ref float[] array, int size)
    {
        if (array == null)
        {
            array = new float[size];
            return;
        }

        if (array.Length == size)
            return;

        float[] newArray = new float[size];
        int copyCount = Mathf.Min(array.Length, size);

        for (int i = 0; i < copyCount; i++)
            newArray[i] = array[i];

        array = newArray;
    }

    private static void EnsureArraySize(ref int[] array, int size)
    {
        if (array == null)
        {
            array = new int[size];
            return;
        }

        if (array.Length == size)
            return;

        int[] newArray = new int[size];
        int copyCount = Mathf.Min(array.Length, size);

        for (int i = 0; i < copyCount; i++)
            newArray[i] = array[i];

        array = newArray;
    }
}