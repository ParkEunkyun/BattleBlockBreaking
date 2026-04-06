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

    private void Awake()
    {
        if (battleManager == null)
            battleManager = GetComponent<BattleManager>();

        if (battleNetDriver == null)
            battleNetDriver = GetComponent<BattleNetDriver>();

        TryAutoAssignOverlay();
        HideOverlayImmediate();
    }

    private void Update()
    {
        if (_battleFinished)
            return;

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

        if (battleManager != null)
            battleManager.SetDisconnectPauseState(false);

        Log($"Local reconnect completed / source={source}");
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

        if (_localPauseNotified)
            return;

        _localPauseNotified = true;
        _localPauseUtcTicks = DateTime.UtcNow.Ticks;

        if (battleManager != null)
            battleManager.SetDisconnectPauseState(true);

        if (battleNetDriver != null)
            battleNetDriver.SendDisconnectPause();

        Log($"Local pause notify / source={source}");
    }

    private void NotifyLocalResumed(string source)
    {
        if (_battleFinished)
            return;

        if (!_localPauseNotified)
            return;

        _localPauseNotified = false;

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

        bool reconnectStarted = false;

        if (battleNetDriver != null)
            reconnectStarted = battleNetDriver.TryReconnectToActiveGame();

        if (!reconnectStarted)
        {
            if (battleNetDriver != null)
                battleNetDriver.SendDisconnectResume();

            if (battleManager != null)
                battleManager.SetDisconnectPauseState(false);
        }
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
}