using System;
using TMPro;
using UnityEngine;

public class BattleDisconnectHandler : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleNetDriver battleNetDriver;

    [Header("Grace Settings")]
    [SerializeField] private float disconnectGraceSeconds = 30f;
    [SerializeField] private bool detectWithApplicationPause = true;
    [SerializeField] private bool detectWithApplicationFocus = true;
    [SerializeField] private bool verboseLog = true;

    [Header("Overlay")]
    [SerializeField] private GameObject disconnectOverlayRoot;
    [SerializeField] private TMP_Text disconnectMessageText;
    [SerializeField] private string disconnectMessageFormat = "상대방의 접속이 원활하지 않습니다. ({0})";

    [Header("Result Text")]
    [SerializeField] private string remoteTimeoutWinReason = "상대가 제한 시간 안에 돌아오지 않아 승리 처리되었습니다.";
    [SerializeField] private string remoteForfeitWinReason = "상대가 배틀에서 이탈하여 승리 처리되었습니다.";
    [SerializeField] private string localTimeoutLoseReason = "앱 이탈 유예 시간을 초과하여 패배 처리되었습니다.";

    private bool _battleFinished;
    private bool _remoteDisconnectActive;
    private float _remoteRemainingSeconds;

    private bool _localPauseNotified;
    private long _localPauseUtcTicks;

    private float _ignoreOfflineUntilUnscaledTime;
    [SerializeField] private float ignoreOfflineCooldownSeconds = 2f;

    [SerializeField] private float localReconnectFallbackSeconds = 4f;
    private bool _localReconnectPending;
    private float _localReconnectDeadlineUnscaled;

    [SerializeField] private int localPauseSignalRetryCount = 1;
    [SerializeField] private float localPauseSignalRetryWindowSeconds = 0.35f;

    private int _localPauseSignalAttemptCount;
    private float _lastLocalPauseSignalUnscaled = -999f;


    private void Awake()
    {
        if (battleManager == null)
            battleManager = GetComponent<BattleManager>();

        if (battleNetDriver == null)
            battleNetDriver = GetComponent<BattleNetDriver>();

        if (Application.isMobilePlatform)
            detectWithApplicationFocus = false;

        TryAutoAssignOverlay();
        HideOverlayImmediate();
    }

    private void Update()
    {
        if (_battleFinished)
            return;

        if (_localReconnectPending && Time.unscaledTime >= _localReconnectDeadlineUnscaled)
        {
            bool reconnectStarted = false;

            if (battleNetDriver != null)
                reconnectStarted = battleNetDriver.TryReconnectToActiveGame();

            _localReconnectDeadlineUnscaled = Time.unscaledTime + Mathf.Max(0.5f, localReconnectFallbackSeconds);
            Log($"Local reconnect retry / started={reconnectStarted} / next={_localReconnectDeadlineUnscaled:0.00}");
        }

        if (!_remoteDisconnectActive)
            return;

        _remoteRemainingSeconds -= Time.unscaledDeltaTime;

        if (_remoteRemainingSeconds < 0f)
            _remoteRemainingSeconds = 0f;

        RefreshOverlayText();

        if (_remoteRemainingSeconds <= 0f)
            HandleRemoteTimeout();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!detectWithApplicationPause)
            return;

        if (pauseStatus)
            NotifyLocalPaused("OnApplicationPause");
        else
            NotifyLocalResumed("OnApplicationPause");
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!detectWithApplicationFocus)
            return;

        if (!hasFocus)
            NotifyLocalPaused("OnApplicationFocus");
        else
            NotifyLocalResumed("OnApplicationFocus");
    }

    public void NotifyRemoteTemporaryLeave(string source = null)
    {
        if (_battleFinished)
            return;

        if (!CanHandleDisconnectNow())
            return;

        if (_remoteDisconnectActive)
            return;

        _remoteDisconnectActive = true;
        _remoteRemainingSeconds = Mathf.Max(1f, disconnectGraceSeconds);

        if (battleManager != null)
            battleManager.SetDisconnectPauseState(true);

        ShowOverlay();
        RefreshOverlayText();

        Log($"Remote temporary leave start / source={source} / grace={disconnectGraceSeconds:0.##}");
    }

    public void NotifyRemoteReturn(string source = null)
    {
        if (_battleFinished)
            return;

        _remoteDisconnectActive = false;
        _remoteRemainingSeconds = 0f;

        IgnoreRemoteOfflineTemporarily(source);

        HideOverlayImmediate();

        if (battleManager != null)
            battleManager.SetDisconnectPauseState(false);

        Log($"Remote return / source={source}");
    }
    public void IgnoreRemoteOfflineTemporarily(string source = null)
    {
        _ignoreOfflineUntilUnscaledTime = Time.unscaledTime + ignoreOfflineCooldownSeconds;
        Log($"Ignore remote offline until={_ignoreOfflineUntilUnscaledTime:0.00} / source={source}");
    }

    public bool ShouldIgnoreRemoteOffline()
    {
        return Time.unscaledTime < _ignoreOfflineUntilUnscaledTime;
    }

    public void ApplyRemoteDisconnectSync(bool paused, int phaseValue, float remainingSeconds, string source = null)
    {
        if (_battleFinished)
            return;

        if (battleManager != null)
            battleManager.ApplyDisconnectSync(paused, phaseValue, remainingSeconds);

        if (paused)
        {
            _remoteDisconnectActive = true;
            _remoteRemainingSeconds = Mathf.Max(1f, disconnectGraceSeconds);
            ShowOverlay();
            RefreshOverlayText();
        }
        else
        {
            _remoteDisconnectActive = false;
            _remoteRemainingSeconds = 0f;
            HideOverlayImmediate();
        }

        Log($"Remote disconnect sync / source={source} / paused={paused} / phase={phaseValue} / remain={remainingSeconds:0.000}");
    }

    public void NotifyLocalReconnectCompleted(string source = null)
    {
        if (_battleFinished)
            return;

        CompleteLocalResume(string.IsNullOrWhiteSpace(source) ? "reconnect-complete" : source);
    }

    public void NotifyLocalReconnectFailed(string source = null)
    {
        if (_battleFinished)
            return;

        _localReconnectPending = true;
        _localReconnectDeadlineUnscaled = Time.unscaledTime + Mathf.Max(0.5f, localReconnectFallbackSeconds);

        if (battleManager != null)
            battleManager.SetDisconnectPauseState(true);

        Log($"Local reconnect failed / source={source} / retryAt={_localReconnectDeadlineUnscaled:0.00}");
    }

    private void CompleteLocalResume(string source)
    {
        _localReconnectPending = false;
        _localReconnectDeadlineUnscaled = 0f;
        _localPauseUtcTicks = 0L;

        ResetLocalPauseSignalState();

        if (battleManager != null)
            battleManager.SetDisconnectPauseState(false);

        Log($"Local resume complete / source={source}");
    }

    public void NotifyRemoteForfeit(string source = null)
    {
        if (_battleFinished)
            return;

        _battleFinished = true;
        _remoteDisconnectActive = false;

        HideOverlayImmediate();

        if (battleNetDriver != null)
            battleNetDriver.MarkMatchEndedByDisconnect();

        if (battleManager != null)
        {
            battleManager.SetDisconnectPauseState(false);
            battleManager.ForceDisconnectResult(false, remoteForfeitWinReason);
        }

        Log($"Remote forfeit / source={source}");
    }

    public void MarkBattleFinished()
    {
        _battleFinished = true;
        _remoteDisconnectActive = false;
        HideOverlayImmediate();
    }

    private void NotifyLocalPaused(string source)
    {
        if (_battleFinished)
            return;

        if (!CanHandleDisconnectNow())
            return;

        if (!_localPauseNotified)
        {
            _localPauseNotified = true;
            _localPauseUtcTicks = DateTime.UtcNow.Ticks;
            _localReconnectPending = false;
            _localReconnectDeadlineUnscaled = 0f;

            ResetLocalPauseSignalState();

            if (battleManager != null)
                battleManager.SetDisconnectPauseState(true);

            TrySendLocalPauseSignals(source);
            Log($"Local pause notify / source={source} / first=true");
            return;
        }

        bool canRetrySignal =
            _localPauseSignalAttemptCount <= Mathf.Max(0, localPauseSignalRetryCount) &&
            (Time.unscaledTime - _lastLocalPauseSignalUnscaled) <= Mathf.Max(0.05f, localPauseSignalRetryWindowSeconds);

        if (!canRetrySignal)
        {
            Log($"Local pause notify ignored / source={source} / retry=false / attempts={_localPauseSignalAttemptCount}");
            return;
        }

        TrySendLocalPauseSignals($"{source}:repeat");
        Log($"Local pause notify / source={source} / first=false / retry=true");
    }

    private void NotifyLocalResumed(string source)
    {
        if (_battleFinished)
            return;

        if (!_localPauseNotified)
            return;

        _localPauseNotified = false;
        ResetLocalPauseSignalState();

        double elapsedSeconds = 0d;

        if (_localPauseUtcTicks > 0L)
            elapsedSeconds = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - _localPauseUtcTicks).TotalSeconds;

        Log($"Local resume notify / source={source} / elapsed={elapsedSeconds:0.00}s");

        if (elapsedSeconds > disconnectGraceSeconds)
        {
            _battleFinished = true;
            _remoteDisconnectActive = false;
            HideOverlayImmediate();

            if (battleNetDriver != null)
            {
                battleNetDriver.SendDisconnectForfeit();
                battleNetDriver.MarkMatchEndedByDisconnect();
            }

            if (battleManager != null)
            {
                battleManager.SetDisconnectPauseState(false);
                battleManager.ForceDisconnectResult(true, localTimeoutLoseReason);
            }

            return;
        }

        bool softResumeSent = false;

        if (battleNetDriver != null)
            softResumeSent = battleNetDriver.TrySendDisconnectResume();

        if (softResumeSent)
        {
            if (battleNetDriver != null && battleNetDriver.IsLocalHostAuthority())
                battleNetDriver.TrySendDisconnectSync(false);

            CompleteLocalResume($"soft-success:{source}");
            return;
        }

        _localReconnectPending = true;
        _localReconnectDeadlineUnscaled = Time.unscaledTime + Mathf.Max(0.5f, localReconnectFallbackSeconds);

        if (battleManager != null)
            battleManager.SetDisconnectPauseState(true);

        bool reconnectStarted = false;

        if (battleNetDriver != null)
            reconnectStarted = battleNetDriver.TryReconnectToActiveGame();

        Log($"Local resume soft-fail / source={source} / reconnectStarted={reconnectStarted} / retryAt={_localReconnectDeadlineUnscaled:0.00}");
    }

    private void HandleRemoteTimeout()
    {
        if (_battleFinished)
            return;

        _battleFinished = true;
        _remoteDisconnectActive = false;
        _remoteRemainingSeconds = 0f;

        HideOverlayImmediate();

        if (battleNetDriver != null)
            battleNetDriver.MarkMatchEndedByDisconnect();

        if (battleManager != null)
        {
            battleManager.SetDisconnectPauseState(false);
            battleManager.ForceDisconnectResult(false, remoteTimeoutWinReason);
        }

        Log("Remote disconnect grace timeout -> local win");
    }

    private bool CanHandleDisconnectNow()
    {
        if (battleManager == null)
            return true;

        return battleManager.IsBattleActiveForDisconnect();
    }

    private void RefreshOverlayText()
    {
        if (disconnectMessageText == null)
            return;

        int remain = Mathf.CeilToInt(_remoteRemainingSeconds);
        disconnectMessageText.text = string.Format(disconnectMessageFormat, remain);
    }

    private void ShowOverlay()
    {
        if (disconnectOverlayRoot != null)
            disconnectOverlayRoot.SetActive(true);
    }

    private void HideOverlayImmediate()
    {
        if (disconnectOverlayRoot != null)
            disconnectOverlayRoot.SetActive(false);
    }

    private void TryAutoAssignOverlay()
    {
        if (disconnectOverlayRoot == null)
        {
            GameObject found = GameObject.Find("DisconnectOverlayRoot");
            if (found != null)
                disconnectOverlayRoot = found;
        }

        if (disconnectMessageText == null && disconnectOverlayRoot != null)
            disconnectMessageText = disconnectOverlayRoot.GetComponentInChildren<TMP_Text>(true);
    }

    private void Log(string msg)
    {
        if (!verboseLog)
            return;

        Debug.Log($"[BattleDisconnectHandler] {msg}");
    }
    private void ResetLocalPauseSignalState()
    {
        _localPauseSignalAttemptCount = 0;
        _lastLocalPauseSignalUnscaled = -999f;
    }

    private bool TrySendLocalPauseSignals(string source)
    {
        bool pauseSent = false;
        bool syncSent = false;

        if (battleNetDriver != null)
        {
            pauseSent = battleNetDriver.TrySendDisconnectPause();
            syncSent = battleNetDriver.TrySendDisconnectSync(true);
        }

        _localPauseSignalAttemptCount++;
        _lastLocalPauseSignalUnscaled = Time.unscaledTime;

        Log($"Local pause signal / source={source} / attempt={_localPauseSignalAttemptCount} / pauseSent={pauseSent} / syncSent={syncSent}");
        return pauseSent || syncSent;
    }

}