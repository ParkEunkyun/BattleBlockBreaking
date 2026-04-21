using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 신규 아티팩트 시스템으로 넘어가기 위한 최소 호환 레이어.
/// 기존 레거시 로직은 제거하고, NormalManager가 컴파일되도록 인터페이스만 유지한다.
/// 실제 발동/쿨다운/보유효과는 NormalArtifactRuntimeManager 쪽으로 이관 예정.
/// </summary>
public class NormalArtifactContext
{
    public int CurrentScore { get; private set; }
    public int CurrentCombo { get; private set; }
    public int CurrentSetIndex { get; private set; }
    public bool[,] Occupied { get; private set; }
    public IReadOnlyList<BattleBlockInstance> CurrentBlocks { get; private set; }

    public System.Action<int> AddScore;
    public System.Action RerollAllBlocks;
    public System.Action<int> RerollOneBlock;
    public System.Action<int> AddComboCount;
    public System.Action<float> SetScoreMultiplierForSets;
    public System.Action<int, int> ClearArea;
    public System.Action<float> ClearBoardRatio;
    public System.Action SpawnDropItem;
    public System.Func<bool> TryConsumeRerollToken;

    public void Init(
        int score,
        int combo,
        int setIndex,
        bool[,] occupied,
        IReadOnlyList<BattleBlockInstance> blocks)
    {
        CurrentScore = score;
        CurrentCombo = combo;
        CurrentSetIndex = setIndex;
        Occupied = occupied;
        CurrentBlocks = blocks;
    }
}

public interface INormalArtifactEffect
{
    void OnBlockPlaced(NormalArtifactContext ctx, int placedCellCount);
    void OnLineClear(NormalArtifactContext ctx, int clearedLineCount);
    void OnSetEnd(NormalArtifactContext ctx, bool hadClearThisSet);
    bool OnGameOverCheck(NormalArtifactContext ctx);
    bool CanActivate(NormalArtifactContext ctx);
    void Activate(NormalArtifactContext ctx);
}

public abstract class NormalArtifactEffectBase : INormalArtifactEffect
{
    protected readonly NormalArtifactDefinition Def;

    protected NormalArtifactEffectBase(NormalArtifactDefinition def)
    {
        Def = def;
    }

    public virtual void OnBlockPlaced(NormalArtifactContext ctx, int placedCellCount) { }
    public virtual void OnLineClear(NormalArtifactContext ctx, int clearedLineCount) { }
    public virtual void OnSetEnd(NormalArtifactContext ctx, bool hadClearThisSet) { }
    public virtual bool OnGameOverCheck(NormalArtifactContext ctx) => false;
    public virtual bool CanActivate(NormalArtifactContext ctx) => false;
    public virtual void Activate(NormalArtifactContext ctx) { }
}

/// <summary>
/// 기본 no-op 효과.
/// </summary>
public sealed class NullArtifactEffect : NormalArtifactEffectBase
{
    public NullArtifactEffect(NormalArtifactDefinition def) : base(def) { }

    public override bool CanActivate(NormalArtifactContext ctx)
    {
        if (Def == null)
            return false;

        return Def.IsActiveArtifact;
    }
}

/// <summary>
/// 기존 NormalManager 타입 체크 호환용 껍데기.
/// 이제 실효과는 하지 않음.
/// </summary>
public sealed class ScoreBoostEffect : NormalArtifactEffectBase
{
    public ScoreBoostEffect(NormalArtifactDefinition def) : base(def) { }
    public float GetLineClearBonusMultiplier() => 1f;
}

public sealed class ComboBoostEffect : NormalArtifactEffectBase
{
    public ComboBoostEffect(NormalArtifactDefinition def) : base(def) { }
    public bool TryExemptComboReset() => false;
    public int GetBonusScorePerCombo() => 0;
    public float GetMilestoneBonusMultiplier() => 1f;
}

public sealed class LuckyBonusEffect : NormalArtifactEffectBase
{
    public LuckyBonusEffect(NormalArtifactDefinition def) : base(def) { }
    public float RollLuckyMultiplier() => 1f;
    public float GetDropRateMultiplier() => 1f;
    public int GetDropItemKeepBonus() => 0;
}

public static class NormalArtifactEffectFactory
{
    public static INormalArtifactEffect Create(NormalArtifactDefinition def)
    {
        if (def == null)
            return null;

        // 1차 정리 단계에서는 전부 no-op.
        // 이후 NormalArtifactRuntimeManager 연동 시 완전 제거 예정.
        return new NullArtifactEffect(def);
    }
}