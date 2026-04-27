using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 노말 아티팩트 런타임 전용 매니저.
/// SO의 equipEffectType / ownedEffectType / param / level table을 해석한다.
/// </summary>
public sealed class NormalArtifactRuntimeManager : MonoBehaviour
{
    public enum ScoreChannel
    {
        Total = 0,
        LineClear = 1,
        Combo = 2
    }

    public enum ActiveUseResult
    {
        Success = 0,
        InvalidIndex = 1,
        NoArtifact = 2,
        NotActive = 3,
        Cooldown = 4,
        AlreadyUsedThisRound = 5
    }

    [Serializable]
    public sealed class RuntimeArtifactState
    {
        public NormalArtifactDefinition definition;
        public int level = 1;

        public int currentCooldown;

        public bool comboPreserveReady;
        public int comboPreserveRemaining;

        public bool secondChanceReady = true;

        public int activeChargesRemaining;

        public int placementChargeCount;
        public bool placementChargeReady;

        public bool IsValid => definition != null;
        public bool IsActive => IsValid && definition.IsActiveArtifact;

        public int CooldownMax
        {
            get
            {
                if (!IsValid) return 0;
                return Mathf.Max(0, definition.GetCooldownValue(level));
            }
        }
    }

    [Header("Debug")]
    [SerializeField] private bool _verboseLog;

    private readonly List<RuntimeArtifactState> _equipped = new List<RuntimeArtifactState>(4);
    private readonly Dictionary<string, int> _artifactLevels = new Dictionary<string, int>();

    private bool _activeUsedThisRound;
    private int _roundIndex;
    private int _blockPlacedInRun;
    private int _lineClearedInRun;
    private int _lastEmptyCellCount;
    private int _lastIsolatedHoleCount;

    private float _timedTotalScoreBuffPercent;
    private int _timedTotalScoreBuffRoundsRemaining;

    private bool _afterActiveNextClearReady;
    private bool _afterActiveRoundBonusPending;
    private float _afterActiveRoundBonusPendingPercent;
    private int _afterActiveRoundBonusRoundsRemaining;
    private float _afterActiveRoundBonusPercent;

    private int _consecutiveClearRounds;
    private bool _streakNextRoundBonusActive;

    private bool _multiLineNextRoundFirstClearBonusActive;
    private bool _firstClearConsumedThisRound;
    private int _clearedLinesThisRound;

    public IReadOnlyList<RuntimeArtifactState> Equipped => _equipped;

    public float OwnedTotalScoreBonus { get; private set; }
    public float OwnedLineClearScoreBonus { get; private set; }
    public float OwnedComboScoreBonus { get; private set; }

    public int RoundIndex => _roundIndex;
    public bool ActiveUsedThisRound => _activeUsedThisRound;

    public event Action<int, RuntimeArtifactState> OnActiveUsed;
    public event Action OnRuntimeChanged;

    private void Awake()
    {
        BuildFromSession();
    }

    public void BuildFromSession()
    {
        _equipped.Clear();
        _activeUsedThisRound = false;
        _roundIndex = 0;
        _blockPlacedInRun = 0;
        _lineClearedInRun = 0;
        _lastEmptyCellCount = 0;
        _lastIsolatedHoleCount = 0;

        _timedTotalScoreBuffPercent = 0f;
        _timedTotalScoreBuffRoundsRemaining = 0;

        _afterActiveNextClearReady = false;
        _afterActiveRoundBonusPending = false;
        _afterActiveRoundBonusPendingPercent = 0f;
        _afterActiveRoundBonusRoundsRemaining = 0;
        _afterActiveRoundBonusPercent = 0f;

        _consecutiveClearRounds = 0;
        _streakNextRoundBonusActive = false;

        _multiLineNextRoundFirstClearBonusActive = false;
        _firstClearConsumedThisRound = false;
        _clearedLinesThisRound = 0;

        IReadOnlyList<NormalArtifactDefinition> selected = NormalArtifactSession.Selected;
        if (selected != null)
        {
            for (int i = 0; i < selected.Count && i < 4; i++)
            {
                NormalArtifactDefinition def = selected[i];
                if (def == null)
                    continue;

                int level = ResolveArtifactLevel(def);

                bool isComboPreserveCharge =
                    def.equipEffectType == NormalArtifactEquipEffectType.ComboPreserveChance;

                bool isLegacyComboPreserve =
                    def.equipEffectType == NormalArtifactEquipEffectType.PreserveComboOnce;

                RuntimeArtifactState state = new RuntimeArtifactState
                {
                    definition = def,
                    level = level,

                    currentCooldown = isComboPreserveCharge
                        ? Mathf.Max(1, def.GetCooldownValue(level))
                        : 0,

                    comboPreserveReady = isLegacyComboPreserve,
                    comboPreserveRemaining = isLegacyComboPreserve
                        ? GetMaxComboPreserveCount(def, level)
                        : 0,

                    secondChanceReady =
                        def.equipEffectType == NormalArtifactEquipEffectType.EmergencySecondChance ||
                        def.equipEffectType == NormalArtifactEquipEffectType.SecondChanceSingleBlocks,

                    activeChargesRemaining = GetMaxActiveCharges(def, level),

                    placementChargeCount = 0,
                    placementChargeReady = false
                };

                _equipped.Add(state);
            }
        }

        RecalculateOwnedBonuses();
        RaiseChanged();

        if (_verboseLog)
            Debug.Log($"[NormalArtifactRuntimeManager] BuildFromSession / Equipped={_equipped.Count}");
    }

    public RuntimeArtifactState GetEquippedState(int index)
    {
        if (index < 0 || index >= _equipped.Count)
            return null;

        return _equipped[index];
    }

    public void SetArtifactLevel(string artifactId, int level)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
            return;

        int clamped = Mathf.Clamp(level, 1, 10);
        _artifactLevels[artifactId] = clamped;

        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || state.definition == null)
                continue;

            if (!string.Equals(state.definition.artifactId, artifactId, StringComparison.Ordinal))
                continue;

            state.level = clamped;

            if (state.definition.equipEffectType == NormalArtifactEquipEffectType.ActiveChargeRotate90)
            {
                state.activeChargesRemaining = Mathf.Clamp(
                    state.activeChargesRemaining,
                    0,
                    GetMaxActiveCharges(state.definition, clamped));
            }

            if (state.definition.equipEffectType == NormalArtifactEquipEffectType.ComboPreserveChance)
            {
                int max = GetMaxComboPreserveCount(state.definition, clamped);
                state.comboPreserveRemaining = Mathf.Clamp(state.comboPreserveRemaining, 0, max);
                state.comboPreserveReady = state.comboPreserveRemaining > 0;

                if (state.comboPreserveRemaining <= 0 && state.currentCooldown <= 0)
                    state.currentCooldown = Mathf.Max(1, state.definition.GetCooldownValue(clamped));
            }
            else if (state.definition.equipEffectType == NormalArtifactEquipEffectType.PreserveComboOnce)
            {
                state.comboPreserveRemaining = GetMaxComboPreserveCount(state.definition, clamped);
                state.comboPreserveReady = state.comboPreserveRemaining > 0;
            }
        }

        RecalculateOwnedBonuses();
        RaiseChanged();
    }

    public void BeginRound()
    {
        _roundIndex++;
        _activeUsedThisRound = false;
        _firstClearConsumedThisRound = false;
        _clearedLinesThisRound = 0;

        TickRoundCooldowns();

        if (_afterActiveRoundBonusPending)
        {
            _afterActiveRoundBonusPercent = _afterActiveRoundBonusPendingPercent;
            _afterActiveRoundBonusRoundsRemaining = 1;
            _afterActiveRoundBonusPending = false;
            _afterActiveRoundBonusPendingPercent = 0f;
        }

        RaiseChanged();

        if (_verboseLog)
            Debug.Log($"[NormalArtifactRuntimeManager] BeginRound / Round={_roundIndex}");
    }

    public void EndRound()
    {
        EndRound(false, 0);
    }

    public void EndRound(bool hadClear, int clearedLinesThisRound)
    {
        _activeUsedThisRound = false;

        if (_timedTotalScoreBuffRoundsRemaining > 0)
        {
            _timedTotalScoreBuffRoundsRemaining--;
            if (_timedTotalScoreBuffRoundsRemaining <= 0)
            {
                _timedTotalScoreBuffRoundsRemaining = 0;
                _timedTotalScoreBuffPercent = 0f;
            }
        }

        if (_afterActiveRoundBonusRoundsRemaining > 0)
        {
            _afterActiveRoundBonusRoundsRemaining--;
            if (_afterActiveRoundBonusRoundsRemaining <= 0)
            {
                _afterActiveRoundBonusRoundsRemaining = 0;
                _afterActiveRoundBonusPercent = 0f;
            }
        }

        _consecutiveClearRounds = hadClear ? _consecutiveClearRounds + 1 : 0;
        _streakNextRoundBonusActive = HasStreakNextRoundBonusReady();
        _multiLineNextRoundFirstClearBonusActive = HasMultiLineNextRoundFirstClearBonusReady(clearedLinesThisRound);

        RaiseChanged();
    }

    public void NotifyBlocksPlaced(int count = 1)
    {
        NotifyBlocksPlaced(count, true);
    }

    public void NotifyBlocksPlaced(int count, bool countPlacementCharge)
    {
        if (count <= 0)
            return;

        _blockPlacedInRun += count;

        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid || state.definition == null)
                continue;

            if (state.definition.equipEffectType == NormalArtifactEquipEffectType.ComboPreserveChance)
            {
                TickComboPreserveCharge(state, count);
                continue;
            }

            if (countPlacementCharge &&
                state.definition.equipEffectType == NormalArtifactEquipEffectType.ChargeNextClearAfterPlacements)
            {
                if (!state.placementChargeReady)
                {
                    int need = Mathf.Max(1, state.definition.paramA);
                    state.placementChargeCount = Mathf.Clamp(state.placementChargeCount + count, 0, need);

                    if (state.placementChargeCount >= need)
                    {
                        state.placementChargeCount = need;
                        state.placementChargeReady = true;
                    }
                }

                continue;
            }

            if (state.IsActive &&
                state.currentCooldown > 0 &&
                state.currentCooldown != int.MaxValue &&
                state.definition.cooldownType == NormalArtifactCooldownType.BlockPlaced)
            {
                state.currentCooldown = Mathf.Max(0, state.currentCooldown - count);
            }
        }

        RaiseChanged();
    }

    public void NotifyBlockPlaced()
    {
        NotifyBlocksPlaced(1, true);
    }

    public void NotifyBoardStateAfterPlacement(int emptyCellCount, int isolatedHoleCount)
    {
        _lastEmptyCellCount = Mathf.Max(0, emptyCellCount);
        _lastIsolatedHoleCount = Mathf.Max(0, isolatedHoleCount);
    }

    public void NotifyLinesCleared(int lineCount)
    {
        if (lineCount <= 0)
            return;

        _lineClearedInRun += lineCount;
        _clearedLinesThisRound += lineCount;

        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid || state.definition == null)
                continue;

            if (state.IsActive &&
                state.currentCooldown > 0 &&
                state.currentCooldown != int.MaxValue &&
                state.definition.cooldownType == NormalArtifactCooldownType.LineClear)
            {
                state.currentCooldown = Mathf.Max(0, state.currentCooldown - lineCount);
            }

            if (state.definition.equipEffectType == NormalArtifactEquipEffectType.ReduceActiveCooldownOnLineClear ||
                state.definition.equipEffectType == NormalArtifactEquipEffectType.CooldownReduceChanceOnLineClear)
            {
                int reduce = RollCooldownReduceAmount(state);
                if (reduce > 0)
                    ReduceOtherActiveCooldowns(i, reduce);
            }
        }

        RaiseChanged();
    }

    public bool TryConsumeComboPreserve()
    {
        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid || state.definition == null)
                continue;

            if (state.definition.equipEffectType != NormalArtifactEquipEffectType.PreserveComboOnce)
                continue;

            if (!state.comboPreserveReady)
                continue;

            state.comboPreserveReady = false;
            state.comboPreserveRemaining = 0;

            if (state.definition.cooldownType == NormalArtifactCooldownType.GameOnce)
                state.currentCooldown = int.MaxValue;
            else
                state.currentCooldown = state.CooldownMax;

            RaiseChanged();

            if (_verboseLog)
                Debug.Log("[NormalArtifactRuntimeManager] Legacy combo preserve consumed.");

            return true;
        }

        return false;
    }

    public bool TryConsumeComboPreserve(int comboBeforeReset)
    {
        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid || state.definition == null)
                continue;

            if (state.definition.equipEffectType != NormalArtifactEquipEffectType.ComboPreserveChance)
                continue;

            int minCombo = state.definition.paramA > 0 ? state.definition.paramA : 5;
            if (comboBeforeReset < minCombo)
                continue;

            if (state.comboPreserveRemaining <= 0)
                continue;

            state.comboPreserveRemaining--;
            state.comboPreserveReady = state.comboPreserveRemaining > 0;

            if (state.comboPreserveRemaining <= 0 && state.currentCooldown <= 0)
                state.currentCooldown = Mathf.Max(1, state.definition.GetCooldownValue(state.level));

            float chance = Mathf.Clamp(state.definition.GetEquipValue(state.level), 0f, 100f);
            bool success = UnityEngine.Random.value <= chance * 0.01f;

            RaiseChanged();

            if (_verboseLog)
            {
                Debug.Log(
                    $"[NormalArtifactRuntimeManager] ComboPreserveChance / combo={comboBeforeReset}, " +
                    $"chance={chance}, success={success}, remain={state.comboPreserveRemaining}, cd={state.currentCooldown}");
            }

            if (success)
                return true;

            break;
        }

        return TryConsumeComboPreserve();
    }

    public bool TryConsumeSecondChance()
    {
        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid || state.definition == null)
                continue;

            if (state.definition.equipEffectType != NormalArtifactEquipEffectType.EmergencySecondChance)
                continue;

            if (!state.secondChanceReady)
                continue;

            state.secondChanceReady = false;
            state.currentCooldown = int.MaxValue;
            RaiseChanged();

            if (_verboseLog)
                Debug.Log("[NormalArtifactRuntimeManager] Second chance consumed.");

            return true;
        }

        return false;
    }

    public bool TryConsumeSecondChanceSingleBlocks()
    {
        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid || state.definition == null)
                continue;

            if (state.definition.equipEffectType != NormalArtifactEquipEffectType.SecondChanceSingleBlocks)
                continue;

            if (!state.secondChanceReady)
                continue;

            state.secondChanceReady = false;
            state.currentCooldown = int.MaxValue;
            RaiseChanged();

            if (_verboseLog)
                Debug.Log("[NormalArtifactRuntimeManager] SecondChanceSingleBlocks consumed.");

            return true;
        }

        return TryConsumeSecondChance();
    }

    public ActiveUseResult TryUseActive(int equippedIndex)
    {
        if (equippedIndex < 0 || equippedIndex >= _equipped.Count)
            return ActiveUseResult.InvalidIndex;

        RuntimeArtifactState state = _equipped[equippedIndex];
        if (state == null || !state.IsValid || state.definition == null)
            return ActiveUseResult.NoArtifact;

        if (!state.IsActive)
            return ActiveUseResult.NotActive;

        if (_activeUsedThisRound && state.definition.activeUsableOncePerRound)
            return ActiveUseResult.AlreadyUsedThisRound;

        if (state.currentCooldown > 0)
            return ActiveUseResult.Cooldown;

        if (state.definition.equipEffectType == NormalArtifactEquipEffectType.ActiveChargeRotate90)
        {
            if (state.activeChargesRemaining <= 0)
                return ActiveUseResult.Cooldown;

            state.activeChargesRemaining--;

            if (state.definition.activeUsableOncePerRound)
                _activeUsedThisRound = true;

            if (state.activeChargesRemaining <= 0)
                state.currentCooldown = state.CooldownMax;

            MarkAfterActiveBonuses();
            OnActiveUsed?.Invoke(equippedIndex, state);
            RaiseChanged();

            if (_verboseLog)
                Debug.Log($"[NormalArtifactRuntimeManager] Active charge used / Index={equippedIndex} / remain={state.activeChargesRemaining}");

            return ActiveUseResult.Success;
        }

        _activeUsedThisRound = true;

        if (state.definition.cooldownType == NormalArtifactCooldownType.GameOnce)
            state.currentCooldown = int.MaxValue;
        else
            state.currentCooldown = state.CooldownMax;

        MarkAfterActiveBonuses();
        OnActiveUsed?.Invoke(equippedIndex, state);
        RaiseChanged();

        if (_verboseLog)
            Debug.Log($"[NormalArtifactRuntimeManager] Active used / Index={equippedIndex} / Effect={state.definition.equipEffectType}");

        return ActiveUseResult.Success;
    }

    public bool HasUsableGameOverRecoveryActive()
    {
        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (CanUseGameOverRecoveryActive(state))
                return true;
        }

        return false;
    }

    private bool CanUseGameOverRecoveryActive(RuntimeArtifactState state)
    {
        if (state == null || !state.IsValid || state.definition == null)
            return false;

        if (!state.IsActive)
            return false;

        if (!IsGameOverRecoveryEffect(state.definition.equipEffectType))
            return false;

        if (_activeUsedThisRound && state.definition.activeUsableOncePerRound)
            return false;

        if (state.currentCooldown > 0)
            return false;

        if (state.definition.equipEffectType == NormalArtifactEquipEffectType.ActiveChargeRotate90)
            return state.activeChargesRemaining > 0;

        return true;
    }

    private static bool IsGameOverRecoveryEffect(NormalArtifactEquipEffectType type)
    {
        switch (type)
        {
            case NormalArtifactEquipEffectType.ActiveChargeRotate90:
            case NormalArtifactEquipEffectType.RotateRemaining90:
            case NormalArtifactEquipEffectType.RotateRemainingMinus90:
            case NormalArtifactEquipEffectType.RotateRemaining180:
            case NormalArtifactEquipEffectType.MirrorRemainingHorizontal:
            case NormalArtifactEquipEffectType.MirrorRemainingVertical:
            case NormalArtifactEquipEffectType.RerollRemainingAll:
            case NormalArtifactEquipEffectType.RebuildRemainingSafer:
            case NormalArtifactEquipEffectType.GravityCollapseBoard:
            case NormalArtifactEquipEffectType.DimensionWarpPlacement:
                return true;

            default:
                return false;
        }
    }

    public float GetOwnedBonus(NormalArtifactOwnedEffectType effectType)
    {
        switch (effectType)
        {
            case NormalArtifactOwnedEffectType.TotalScoreBonus:
                return OwnedTotalScoreBonus;
            case NormalArtifactOwnedEffectType.LineClearScoreBonus:
                return OwnedLineClearScoreBonus;
            case NormalArtifactOwnedEffectType.ComboScoreBonus:
                return OwnedComboScoreBonus;
            default:
                return 0f;
        }
    }

    public float EvaluateOwnedScoreMultiplier(ScoreChannel channel)
    {
        float total = 1f + OwnedTotalScoreBonus * 0.01f;

        switch (channel)
        {
            case ScoreChannel.Total:
                return total;

            case ScoreChannel.LineClear:
                return total + (OwnedLineClearScoreBonus * 0.01f);

            case ScoreChannel.Combo:
                return total + (OwnedComboScoreBonus * 0.01f);

            default:
                return total;
        }
    }

    public float EvaluatePlacementScoreMultiplier(int cellCount)
    {
        float percent = GetEquipPercent(NormalArtifactEquipEffectType.TotalScoreBonus);

        if (cellCount == 3)
            percent += GetEquipPercent(NormalArtifactEquipEffectType.Score3CellBlockBonus);
        else if (cellCount == 4)
            percent += GetEquipPercent(NormalArtifactEquipEffectType.Score4CellBlockBonus);
        else if (cellCount >= 5)
            percent += GetEquipPercent(NormalArtifactEquipEffectType.Score5CellBlockBonus);

        percent += GetTimedTotalScoreBuffPercent();
        percent += GetAfterActiveRoundBonusPercent();

        return Mathf.Max(0f, EvaluateOwnedScoreMultiplier(ScoreChannel.Total) + percent * 0.01f);
    }

    public float EvaluateLineClearScoreMultiplier(
        bool lastPlacementOfSet,
        int emptyCellCount,
        int placedCellCount,
        int horizontalLines,
        int verticalLines)
    {
        float percent = GetEquipPercent(NormalArtifactEquipEffectType.LineClearScoreBonus);

        if (horizontalLines > 0)
            percent += GetEquipPercent(NormalArtifactEquipEffectType.HorizontalLineScoreBonus);

        if (verticalLines > 0)
            percent += GetEquipPercent(NormalArtifactEquipEffectType.VerticalLineScoreBonus);

        if (lastPlacementOfSet)
            percent += GetEquipPercent(NormalArtifactEquipEffectType.LastPlacementClearBonus);

        percent += GetConditionalPercent(NormalArtifactEquipEffectType.LowSpaceClearBonus, state =>
        {
            int threshold = state.definition.paramA > 0 ? state.definition.paramA : 24;
            return emptyCellCount <= threshold;
        });

        percent += GetConditionalPercent(NormalArtifactEquipEffectType.LargeBlockClearBonus, state =>
        {
            int threshold = state.definition.paramA > 0 ? state.definition.paramA : 5;
            return placedCellCount >= threshold;
        });

        percent += GetConditionalPercent(NormalArtifactEquipEffectType.IsolatedHoleStackNextClearBonus, state =>
        {
            int threshold = state.definition.paramA > 0 ? state.definition.paramA : 1;
            return _lastIsolatedHoleCount >= threshold;
        });

        if (_afterActiveNextClearReady)
        {
            percent += GetEquipPercent(NormalArtifactEquipEffectType.AfterUsingActiveNextClearBonus);
            _afterActiveNextClearReady = false;
        }

        if (_streakNextRoundBonusActive)
            percent += GetEquipPercent(NormalArtifactEquipEffectType.StreakNextRoundBonus);

        if (_multiLineNextRoundFirstClearBonusActive && !_firstClearConsumedThisRound)
        {
            percent += GetEquipPercent(NormalArtifactEquipEffectType.MultiLineNextRoundFirstClearBonus);
            _firstClearConsumedThisRound = true;
            _multiLineNextRoundFirstClearBonusActive = false;
        }

        percent += GetTimedTotalScoreBuffPercent();
        percent += GetAfterActiveRoundBonusPercent();

        return Mathf.Max(0f, EvaluateOwnedScoreMultiplier(ScoreChannel.LineClear) + percent * 0.01f);
    }

    public float EvaluateComboScoreMultiplier()
    {
        float percent = GetEquipPercent(NormalArtifactEquipEffectType.ComboScoreBonus);
        percent += GetTimedTotalScoreBuffPercent();
        percent += GetAfterActiveRoundBonusPercent();

        return Mathf.Max(0f, EvaluateOwnedScoreMultiplier(ScoreChannel.Combo) + percent * 0.01f);
    }

    public int GetLineClearFlatBonus()
    {
        return Mathf.RoundToInt(GetEquipPercent(NormalArtifactEquipEffectType.LineClearFlatBonus));
    }

    public int ConsumePostClearFlatBonusByRemainingCells(int remainingOccupiedCells)
    {
        int total = 0;

        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid || state.definition == null)
                continue;

            if (state.definition.equipEffectType != NormalArtifactEquipEffectType.PostClearFlatBonusByRemainingCells)
                continue;

            int allowed = Mathf.CeilToInt(Mathf.Max(0, state.level - 1) * 0.5f);

            if (remainingOccupiedCells <= allowed)
                total += Mathf.Max(0, state.definition.paramA);
        }

        return total;
    }

    public int GetPostClearFlatBonus(int remainingOccupiedCells)
    {
        return ConsumePostClearFlatBonusByRemainingCells(remainingOccupiedCells);
    }

    public int GetComboBreakFlatBonus(int comboBeforeReset)
    {
        if (comboBeforeReset < 2)
            return 0;

        return Mathf.RoundToInt(GetEquipPercent(NormalArtifactEquipEffectType.ComboBreakFlatBonus));
    }

    public bool HasPlacementChargeReady()
    {
        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid || state.definition == null)
                continue;

            if (state.definition.equipEffectType != NormalArtifactEquipEffectType.ChargeNextClearAfterPlacements)
                continue;

            if (state.placementChargeReady)
                return true;
        }

        return false;
    }

    public int ConsumePlacementChargeClearBonus(bool cleared)
    {
        int total = 0;
        bool changed = false;

        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid || state.definition == null)
                continue;

            if (state.definition.equipEffectType != NormalArtifactEquipEffectType.ChargeNextClearAfterPlacements)
                continue;

            if (!state.placementChargeReady)
                continue;

            if (cleared)
                total += Mathf.RoundToInt(state.definition.GetEquipValue(state.level));

            state.placementChargeReady = false;
            state.placementChargeCount = 0;
            changed = true;
        }

        if (changed)
            RaiseChanged();

        return total;
    }

    public float GetGoldDropRateMultiplier()
    {
        float percent = GetEquipPercent(NormalArtifactEquipEffectType.GoldDropRateMultiplier);
        percent += GetEquipPercent(NormalArtifactEquipEffectType.AllDropRateMultiplier);
        return Mathf.Max(0f, 1f + percent * 0.01f);
    }

    public float GetItemDropRateMultiplier()
    {
        float percent = GetEquipPercent(NormalArtifactEquipEffectType.ItemDropRateMultiplier);
        percent += GetEquipPercent(NormalArtifactEquipEffectType.AllDropRateMultiplier);
        return Mathf.Max(0f, 1f + percent * 0.01f);
    }

    public float GetDropRateMultiplier()
    {
        return GetItemDropRateMultiplier();
    }

    public int GetDropKeepBonus()
    {
        return 0;
    }

    public void ActivateTimedTotalScoreBuff(RuntimeArtifactState state)
    {
        if (state == null || !state.IsValid || state.definition == null)
            return;

        if (state.definition.equipEffectType != NormalArtifactEquipEffectType.TimedTotalScoreBuff)
            return;

        _timedTotalScoreBuffPercent = state.definition.GetEquipValue(state.level);
        _timedTotalScoreBuffRoundsRemaining = Mathf.Max(1, state.definition.paramA);
        RaiseChanged();

        if (_verboseLog)
        {
            Debug.Log(
                $"[NormalArtifactRuntimeManager] TimedTotalScoreBuff Activated / " +
                $"percent={_timedTotalScoreBuffPercent}, rounds={_timedTotalScoreBuffRoundsRemaining}");
        }
    }

    public bool TryGetArtifactGaugeStatus(
        int equippedIndex,
        out float fillAmount,
        out bool isReady,
        out string stateText)
    {
        fillAmount = 0f;
        isReady = false;
        stateText = string.Empty;

        RuntimeArtifactState state = GetEquippedState(equippedIndex);
        if (state == null || !state.IsValid || state.definition == null)
            return false;

        NormalArtifactDefinition def = state.definition;

        switch (def.equipEffectType)
        {
            case NormalArtifactEquipEffectType.ActiveChargeRotate90:
                {
                    int max = GetMaxActiveCharges(def, state.level);
                    if (max <= 0)
                        return false;

                    int current = Mathf.Clamp(state.activeChargesRemaining, 0, max);
                    fillAmount = current / (float)max;
                    isReady = current >= max;
                    stateText = isReady ? "READY" : string.Empty;
                    return true;
                }

            case NormalArtifactEquipEffectType.ComboPreserveChance:
                {
                    int max = GetMaxComboPreserveCount(def, state.level);
                    if (max <= 0)
                        return false;

                    int current = Mathf.Clamp(state.comboPreserveRemaining, 0, max);
                    if (current >= max)
                    {
                        fillAmount = 1f;
                        isReady = true;
                        stateText = "READY";
                    }
                    else
                    {
                        int cdMax = Mathf.Max(1, def.GetCooldownValue(state.level));
                        int remain = Mathf.Clamp(state.currentCooldown, 0, cdMax);
                        float chargeProgress = 1f - (remain / (float)cdMax);

                        fillAmount = Mathf.Clamp01((current + chargeProgress) / max);
                        isReady = false;
                        stateText = string.Empty;
                    }

                    return true;
                }

            case NormalArtifactEquipEffectType.ChargeNextClearAfterPlacements:
                {
                    int need = Mathf.Max(1, def.paramA);
                    if (state.placementChargeReady)
                    {
                        fillAmount = 1f;
                        isReady = true;
                        stateText = "READY";
                    }
                    else
                    {
                        int current = Mathf.Clamp(state.placementChargeCount, 0, need);
                        fillAmount = current / (float)need;
                        isReady = false;
                        stateText = string.Empty;
                    }

                    return true;
                }

            case NormalArtifactEquipEffectType.TimedTotalScoreBuff:
                {
                    if (_timedTotalScoreBuffRoundsRemaining <= 0)
                        return false;

                    int duration = Mathf.Max(1, def.paramA);
                    fillAmount = Mathf.Clamp01(_timedTotalScoreBuffRoundsRemaining / (float)duration);
                    isReady = true;
                    stateText = $"{_timedTotalScoreBuffRoundsRemaining}R";
                    return true;
                }

            case NormalArtifactEquipEffectType.AfterUsingActiveNextRoundBonus:
                {
                    if (_afterActiveRoundBonusPending)
                    {
                        fillAmount = 1f;
                        isReady = true;
                        stateText = "NEXT";
                        return true;
                    }

                    if (_afterActiveRoundBonusRoundsRemaining > 0)
                    {
                        fillAmount = 1f;
                        isReady = true;
                        stateText = "READY";
                        return true;
                    }

                    return false;
                }

            case NormalArtifactEquipEffectType.StreakNextRoundBonus:
                {
                    int need = Mathf.Max(1, def.paramA);
                    if (_streakNextRoundBonusActive)
                    {
                        fillAmount = 1f;
                        isReady = true;
                        stateText = "READY";
                    }
                    else
                    {
                        fillAmount = Mathf.Clamp01(_consecutiveClearRounds / (float)need);
                        isReady = false;
                        stateText = string.Empty;
                    }

                    return true;
                }

            case NormalArtifactEquipEffectType.MultiLineNextRoundFirstClearBonus:
                {
                    int need = Mathf.Max(2, def.paramA);
                    if (_multiLineNextRoundFirstClearBonusActive)
                    {
                        fillAmount = 1f;
                        isReady = true;
                        stateText = "READY";
                    }
                    else
                    {
                        fillAmount = Mathf.Clamp01(_clearedLinesThisRound / (float)need);
                        isReady = false;
                        stateText = string.Empty;
                    }

                    return true;
                }
        }

        return false;
    }

    private void TickComboPreserveCharge(RuntimeArtifactState state, int count)
    {
        if (state == null || !state.IsValid || state.definition == null)
            return;

        int maxCharges = GetMaxComboPreserveCount(state.definition, state.level);
        if (maxCharges <= 0 || state.comboPreserveRemaining >= maxCharges)
        {
            state.comboPreserveReady = state.comboPreserveRemaining > 0;
            state.currentCooldown = 0;
            return;
        }

        if (state.currentCooldown <= 0)
            state.currentCooldown = Mathf.Max(1, state.definition.GetCooldownValue(state.level));

        state.currentCooldown -= count;

        while (state.currentCooldown <= 0 &&
               state.comboPreserveRemaining < maxCharges)
        {
            state.comboPreserveRemaining++;

            if (state.comboPreserveRemaining < maxCharges)
            {
                int cd = Mathf.Max(1, state.definition.GetCooldownValue(state.level));
                state.currentCooldown += cd;
            }
            else
            {
                state.currentCooldown = 0;
            }
        }

        state.comboPreserveReady = state.comboPreserveRemaining > 0;
    }

    private void TickRoundCooldowns()
    {
        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid || state.definition == null)
                continue;

            if (state.definition.equipEffectType == NormalArtifactEquipEffectType.ComboPreserveChance)
            {
                state.comboPreserveReady = state.comboPreserveRemaining > 0;
                continue;
            }

            if (state.currentCooldown <= 0)
                continue;

            if (state.definition.cooldownType == NormalArtifactCooldownType.Round)
                state.currentCooldown = Mathf.Max(0, state.currentCooldown - 1);

            if (state.definition.equipEffectType == NormalArtifactEquipEffectType.PreserveComboOnce &&
                state.currentCooldown == 0)
            {
                state.comboPreserveReady = true;
                state.comboPreserveRemaining = GetMaxComboPreserveCount(state.definition, state.level);
            }

            if (state.definition.equipEffectType == NormalArtifactEquipEffectType.ActiveChargeRotate90 &&
                state.currentCooldown == 0)
            {
                state.activeChargesRemaining = GetMaxActiveCharges(state.definition, state.level);
            }
        }
    }

    private void ReduceOtherActiveCooldowns(int exceptIndex, int amount)
    {
        if (amount <= 0)
            return;

        for (int i = 0; i < _equipped.Count; i++)
        {
            if (i == exceptIndex)
                continue;

            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid)
                continue;

            if (!state.IsActive)
                continue;

            if (state.currentCooldown <= 0)
                continue;

            if (state.currentCooldown == int.MaxValue)
                continue;

            state.currentCooldown = Mathf.Max(0, state.currentCooldown - amount);

            if (state.definition.equipEffectType == NormalArtifactEquipEffectType.ActiveChargeRotate90 &&
                state.currentCooldown == 0)
            {
                state.activeChargesRemaining = GetMaxActiveCharges(state.definition, state.level);
            }
        }
    }

    private void RecalculateOwnedBonuses()
    {
        OwnedTotalScoreBonus = 0f;
        OwnedLineClearScoreBonus = 0f;
        OwnedComboScoreBonus = 0f;

        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid || state.definition == null)
                continue;

            float value = state.definition.GetOwnedValue(state.level);

            switch (state.definition.ownedEffectType)
            {
                case NormalArtifactOwnedEffectType.TotalScoreBonus:
                    OwnedTotalScoreBonus += value;
                    break;
                case NormalArtifactOwnedEffectType.LineClearScoreBonus:
                    OwnedLineClearScoreBonus += value;
                    break;
                case NormalArtifactOwnedEffectType.ComboScoreBonus:
                    OwnedComboScoreBonus += value;
                    break;
            }
        }
    }

    private void MarkAfterActiveBonuses()
    {
        if (GetEquipPercent(NormalArtifactEquipEffectType.AfterUsingActiveNextClearBonus) > 0f)
            _afterActiveNextClearReady = true;

        float nextRoundPercent = GetEquipPercent(NormalArtifactEquipEffectType.AfterUsingActiveNextRoundBonus);
        if (nextRoundPercent > 0f)
        {
            _afterActiveRoundBonusPending = true;
            _afterActiveRoundBonusPendingPercent = nextRoundPercent;
        }
    }

    private bool HasStreakNextRoundBonusReady()
    {
        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid || state.definition == null)
                continue;

            if (state.definition.equipEffectType != NormalArtifactEquipEffectType.StreakNextRoundBonus)
                continue;

            int need = Mathf.Max(1, state.definition.paramA);
            if (_consecutiveClearRounds >= need)
                return true;
        }

        return false;
    }

    private bool HasMultiLineNextRoundFirstClearBonusReady(int clearedLinesThisRound)
    {
        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid || state.definition == null)
                continue;

            if (state.definition.equipEffectType != NormalArtifactEquipEffectType.MultiLineNextRoundFirstClearBonus)
                continue;

            int need = Mathf.Max(2, state.definition.paramA);
            if (clearedLinesThisRound >= need)
                return true;
        }

        return false;
    }

    private float GetTimedTotalScoreBuffPercent()
    {
        return _timedTotalScoreBuffRoundsRemaining > 0 ? _timedTotalScoreBuffPercent : 0f;
    }

    private float GetAfterActiveRoundBonusPercent()
    {
        return _afterActiveRoundBonusRoundsRemaining > 0 ? _afterActiveRoundBonusPercent : 0f;
    }

    private float GetEquipPercent(NormalArtifactEquipEffectType type)
    {
        float total = 0f;
        float bestSingle = 0f;

        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid || state.definition == null)
                continue;

            if (state.definition.equipEffectType != type)
                continue;

            float value = state.definition.GetEquipValue(state.level);
            total += value;
            bestSingle = Mathf.Max(bestSingle, value);
        }

        float amplify = GetBestAmplifyPercent();
        if (amplify > 0f && bestSingle > 0f)
            total += bestSingle * amplify * 0.01f;

        return total;
    }

    private float GetConditionalPercent(
        NormalArtifactEquipEffectType type,
        Func<RuntimeArtifactState, bool> predicate)
    {
        float total = 0f;

        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid || state.definition == null)
                continue;

            if (state.definition.equipEffectType != type)
                continue;

            if (predicate == null || predicate(state))
                total += state.definition.GetEquipValue(state.level);
        }

        return total;
    }

    private int RollCooldownReduceAmount(RuntimeArtifactState state)
    {
        if (state == null || !state.IsValid || state.definition == null)
            return 0;

        if (state.definition.equipEffectType == NormalArtifactEquipEffectType.CooldownReduceChanceOnLineClear)
        {
            int level = Mathf.Clamp(state.level, 1, 10);

            if (level >= 10)
                return 2;

            float chance = Mathf.Clamp(state.definition.GetEquipValue(level), 0f, 100f);
            return UnityEngine.Random.value <= chance * 0.01f ? 1 : 0;
        }

        return Mathf.Max(0, Mathf.RoundToInt(state.definition.GetEquipValue(state.level)));
    }

    private int GetMaxComboPreserveCount(NormalArtifactDefinition def, int level)
    {
        if (def == null)
            return 0;

        if (def.equipEffectType == NormalArtifactEquipEffectType.ComboPreserveChance)
            return level >= 10 ? 2 : 1;

        if (def.equipEffectType == NormalArtifactEquipEffectType.PreserveComboOnce)
            return 1;

        return 0;
    }

    private int GetMaxActiveCharges(NormalArtifactDefinition def, int level)
    {
        if (def == null)
            return 0;

        if (def.equipEffectType != NormalArtifactEquipEffectType.ActiveChargeRotate90)
            return 0;

        return Mathf.Max(1, Mathf.RoundToInt(def.GetEquipValue(level)));
    }

    private int ResolveArtifactLevel(NormalArtifactDefinition def)
    {
        if (def == null)
            return 1;

        if (!string.IsNullOrWhiteSpace(def.artifactId) &&
            _artifactLevels.TryGetValue(def.artifactId, out int level))
        {
            return Mathf.Clamp(level, 1, 10);
        }

        return NormalArtifactLevelUtility.GetLevel(def);
    }

    private float GetBestAmplifyPercent()
    {
        float best = 0f;

        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || !state.IsValid || state.definition == null)
                continue;

            if (state.definition.equipEffectType != NormalArtifactEquipEffectType.AmplifyBestEquippedArtifact)
                continue;

            best = Mathf.Max(best, state.definition.GetEquipValue(state.level));
        }

        return best;
    }

    private void RaiseChanged()
    {
        OnRuntimeChanged?.Invoke();
    }
}
