using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 노말 아티팩트 런타임 전용 매니저.
/// - 로비에서 선택한 4개 아티팩트를 NormalArtifactSession 으로 받아옴
/// - 장착 효과 / 보유 효과 / 액티브 쿨다운 / 라운드당 1회 제한 관리
/// - 아직 NormalManager 내부 로직에 깊게 물리지 않는 1차 뼈대
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

        // 액티브 재사용 카운트
        public int currentCooldown;

        // 전설/특수 상태
        public bool comboPreserveReady;
        public bool secondChanceReady = true;

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

    /// <summary>
    /// 레벨 데이터가 따로 생기기 전까지는 전부 Lv1로 시작.
    /// 나중에 저장 시스템 붙으면 artifactId 기준으로 _artifactLevels 채우면 됨.
    /// </summary>
    public void BuildFromSession()
    {
        _equipped.Clear();
        _activeUsedThisRound = false;
        _roundIndex = 0;
        _blockPlacedInRun = 0;
        _lineClearedInRun = 0;

        IReadOnlyList<NormalArtifactDefinition> selected = NormalArtifactSession.Selected;
        if (selected != null)
        {
            for (int i = 0; i < selected.Count && i < 4; i++)
            {
                NormalArtifactDefinition def = selected[i];
                if (def == null)
                    continue;

                RuntimeArtifactState state = new RuntimeArtifactState
                {
                    definition = def,
                    level = ResolveArtifactLevel(def),
                    currentCooldown = 0,
                    comboPreserveReady = def.equipEffectType == NormalArtifactEquipEffectType.PreserveComboOnce,
                    secondChanceReady = def.equipEffectType == NormalArtifactEquipEffectType.EmergencySecondChance
                };

                _equipped.Add(state);
            }
        }

        RecalculateOwnedBonuses();
        RaiseChanged();

        if (_verboseLog)
            Debug.Log($"[NormalArtifactRuntimeManager] BuildFromSession / Equipped={_equipped.Count}");
    }

    public void SetArtifactLevel(string artifactId, int level)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
            return;

        _artifactLevels[artifactId] = Mathf.Clamp(level, 1, 10);

        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (state == null || state.definition == null)
                continue;

            if (string.Equals(state.definition.artifactId, artifactId, StringComparison.Ordinal))
            {
                state.level = Mathf.Clamp(level, 1, 10);
            }
        }

        RecalculateOwnedBonuses();
        RaiseChanged();
    }

    public void BeginRound()
    {
        _roundIndex++;
        _activeUsedThisRound = false;

        TickRoundCooldowns();
        RaiseChanged();

        if (_verboseLog)
            Debug.Log($"[NormalArtifactRuntimeManager] BeginRound / Round={_roundIndex}");
    }

    public void EndRound()
    {
        _activeUsedThisRound = false;
        RaiseChanged();
    }

    public void NotifyBlocksPlaced(int count = 1)
    {
        if (count <= 0)
            return;

        _blockPlacedInRun += count;

        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (!state.IsActive) continue;
            if (state.currentCooldown <= 0) continue;
            if (state.definition.cooldownType != NormalArtifactCooldownType.BlockPlaced) continue;

            state.currentCooldown = Mathf.Max(0, state.currentCooldown - count);
        }

        RaiseChanged();
    }

    public void NotifyLinesCleared(int lineCount)
    {
        if (lineCount <= 0)
            return;

        _lineClearedInRun += lineCount;

        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (!state.IsValid) continue;

            // 라인 클리어 쿨다운 감소
            if (state.IsActive &&
                state.currentCooldown > 0 &&
                state.definition.cooldownType == NormalArtifactCooldownType.LineClear)
            {
                state.currentCooldown = Mathf.Max(0, state.currentCooldown - lineCount);
            }

            // 냉각 재순환기
            if (state.definition.equipEffectType == NormalArtifactEquipEffectType.ReduceActiveCooldownOnLineClear)
            {
                int reduce = Mathf.RoundToInt(state.definition.GetEquipValue(state.level));
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
            if (!state.IsValid) continue;
            if (state.definition.equipEffectType != NormalArtifactEquipEffectType.PreserveComboOnce) continue;
            if (!state.comboPreserveReady) continue;

            state.comboPreserveReady = false;

            if (state.definition.cooldownType == NormalArtifactCooldownType.GameOnce)
            {
                state.currentCooldown = int.MaxValue;
            }
            else
            {
                state.currentCooldown = state.CooldownMax;
            }

            RaiseChanged();

            if (_verboseLog)
                Debug.Log("[NormalArtifactRuntimeManager] Combo preserve consumed.");

            return true;
        }

        return false;
    }

    public bool TryConsumeSecondChance()
    {
        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (!state.IsValid) continue;
            if (state.definition.equipEffectType != NormalArtifactEquipEffectType.EmergencySecondChance) continue;
            if (!state.secondChanceReady) continue;

            state.secondChanceReady = false;
            state.currentCooldown = int.MaxValue;
            RaiseChanged();

            if (_verboseLog)
                Debug.Log("[NormalArtifactRuntimeManager] Second chance consumed.");

            return true;
        }

        return false;
    }

    public ActiveUseResult TryUseActive(int equippedIndex)
    {
        if (equippedIndex < 0 || equippedIndex >= _equipped.Count)
            return ActiveUseResult.InvalidIndex;

        RuntimeArtifactState state = _equipped[equippedIndex];
        if (state == null || !state.IsValid)
            return ActiveUseResult.NoArtifact;

        if (!state.IsActive)
            return ActiveUseResult.NotActive;

        if (_activeUsedThisRound && state.definition.activeUsableOncePerRound)
            return ActiveUseResult.AlreadyUsedThisRound;

        if (state.currentCooldown > 0)
            return ActiveUseResult.Cooldown;

        _activeUsedThisRound = true;

        if (state.definition.cooldownType == NormalArtifactCooldownType.GameOnce)
            state.currentCooldown = int.MaxValue;
        else
            state.currentCooldown = state.CooldownMax;

        // 여기서는 실제 보드/블록 변형을 하지 않음.
        // NormalManager 쪽에서 OnActiveUsed 이벤트를 받아 남은 블록 전체에 적용하면 됨.
        OnActiveUsed?.Invoke(equippedIndex, state);
        RaiseChanged();

        if (_verboseLog)
            Debug.Log($"[NormalArtifactRuntimeManager] Active used / Index={equippedIndex} / Effect={state.definition.equipEffectType}");

        return ActiveUseResult.Success;
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

    public float GetBestAmplifyPercent()
    {
        float best = 0f;

        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (!state.IsValid) continue;
            if (state.definition.equipEffectType != NormalArtifactEquipEffectType.AmplifyBestEquippedArtifact) continue;

            best = Mathf.Max(best, state.definition.GetEquipValue(state.level));
        }

        return best;
    }

    public bool HasGravityCollapse(out RuntimeArtifactState state)
    {
        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState s = _equipped[i];
            if (!s.IsValid) continue;

            if (s.definition.equipEffectType == NormalArtifactEquipEffectType.GravityCollapseBoard)
            {
                state = s;
                return true;
            }
        }

        state = null;
        return false;
    }

    private void TickRoundCooldowns()
    {
        for (int i = 0; i < _equipped.Count; i++)
        {
            RuntimeArtifactState state = _equipped[i];
            if (!state.IsValid) continue;
            if (state.currentCooldown <= 0) continue;

            if (state.definition.cooldownType == NormalArtifactCooldownType.Round)
            {
                state.currentCooldown = Mathf.Max(0, state.currentCooldown - 1);
            }

            if (state.definition.equipEffectType == NormalArtifactEquipEffectType.PreserveComboOnce &&
                state.currentCooldown == 0)
            {
                state.comboPreserveReady = true;
            }
        }
    }

    private void ReduceOtherActiveCooldowns(int exceptIndex, int amount)
    {
        if (amount <= 0)
            return;

        for (int i = 0; i < _equipped.Count; i++)
        {
            if (i == exceptIndex) continue;

            RuntimeArtifactState state = _equipped[i];
            if (!state.IsActive) continue;
            if (state.currentCooldown <= 0) continue;
            if (state.currentCooldown == int.MaxValue) continue;

            state.currentCooldown = Mathf.Max(0, state.currentCooldown - amount);
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
            if (!state.IsValid) continue;

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

    private int ResolveArtifactLevel(NormalArtifactDefinition def)
    {
        if (def == null)
            return 1;

        if (_artifactLevels.TryGetValue(def.artifactId, out int level))
            return Mathf.Clamp(level, 1, 10);

        return 1;
    }

    private void RaiseChanged()
    {
        OnRuntimeChanged?.Invoke();
    }
}