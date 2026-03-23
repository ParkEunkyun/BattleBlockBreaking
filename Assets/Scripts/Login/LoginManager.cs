using System.Collections;
using System.Collections.Generic;
using System.Text;
using BackEnd;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoginManager : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "Scene_Lobby";

    [Header("Root")]
    [SerializeField] private GameObject loginRoot;
    [SerializeField] private GameObject loadingRoot;

    [Header("Login UI")]
    [SerializeField] private Button guestLoginButton;

    [Header("Loading UI")]
    [SerializeField] private TMP_Text loadingStatusText;
    [SerializeField] private TMP_Text loadingPercentText;
    [SerializeField] private Slider loadingSlider;

    [Header("On Device Debug (Optional)")]
    [SerializeField] private TMP_Text debugText;
    [SerializeField] private int debugLineMax = 12;

    [Header("Guest Auto Recovery (Test)")]
    [SerializeField] private bool autoResetGuestOnBadCustomId = true;

    private bool isProcessing = false;
    private readonly Queue<string> debugLines = new Queue<string>();

    private void Awake()
    {
        Application.logMessageReceived += HandleUnityLog;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleUnityLog;
    }

    private void Start()
    {
        SafeSetActive(loginRoot, true);
        SafeSetActive(loadingRoot, false);

        SetLoadingUI(0f, "Ready");
        SetGuestButtonInteractable(true);

        AddDebug("LoginManager Start");
        AddDebug($"Current Scene = {SceneManager.GetActiveScene().name}");
        AddDebug($"Target Lobby Scene = {lobbySceneName}");
        AddDebug($"Local Guest ID = {GetLocalGuestIdSafe()}");
    }

    /// <summary>
    /// 버튼 OnClick 연결용
    /// </summary>
    public void OnGuestLoginSuccess()
    {
        if (isProcessing)
        {
            AddDebug("이미 처리 중입니다.");
            return;
        }

        StartCoroutine(GuestLoginRoutine());
    }

    private IEnumerator GuestLoginRoutine()
    {
        isProcessing = true;
        SetGuestButtonInteractable(false);

        AddDebug("로그인 버튼 클릭됨");
        AddDebug($"로그인 전 Local Guest ID = {GetLocalGuestIdSafe()}");
        AddDebug("게스트 로그인 시도 시작");

        yield return null;

        BackendReturnObject bro = TryGuestLogin();

        if (bro == null)
        {
            AddDebug("GuestLogin 응답이 null 입니다.");
            FailLogin("Login Failed");
            yield break;
        }

        LogBro("첫 로그인 응답", bro);

        if (bro.IsSuccess())
        {
            AddDebug("게스트 로그인 성공");
            yield return StartCoroutine(LoadLobbyRoutine());
            yield break;
        }

        // bad customId 자동 복구
        if (autoResetGuestOnBadCustomId && IsBadCustomId(bro))
        {
            AddDebug("bad customId 감지");
            AddDebug("기기 로컬 게스트 정보 삭제 시도");

            bool deleted = DeleteGuestInfoSafe();

            AddDebug($"DeleteGuestInfo 완료 = {deleted}");
            AddDebug($"삭제 후 Local Guest ID = {GetLocalGuestIdSafe()}");

            yield return null;

            AddDebug("게스트 로그인 재시도 시작");
            BackendReturnObject retryBro = TryGuestLogin();

            if (retryBro == null)
            {
                AddDebug("재시도 응답이 null 입니다.");
                FailLogin("Retry Failed");
                yield break;
            }

            LogBro("재시도 응답", retryBro);

            if (retryBro.IsSuccess())
            {
                AddDebug("재시도 후 게스트 로그인 성공");
                yield return StartCoroutine(LoadLobbyRoutine());
                yield break;
            }

            AddDebug("재시도 후에도 게스트 로그인 실패");
            FailLogin("Login Failed");
            yield break;
        }

        AddDebug("게스트 로그인 실패");
        FailLogin("Login Failed");
    }

    private BackendReturnObject TryGuestLogin()
    {
        try
        {
            return Backend.BMember.GuestLogin("게스트 로그인으로 로그인함");
        }
        catch (System.Exception e)
        {
            AddDebug("GuestLogin 예외 발생");
            AddDebug(e.Message);
            Debug.LogException(e);
            return null;
        }
    }

    private bool DeleteGuestInfoSafe()
    {
        try
        {
            Backend.BMember.DeleteGuestInfo();
            return true;
        }
        catch (System.Exception e)
        {
            AddDebug("DeleteGuestInfo 예외 발생");
            AddDebug(e.Message);
            Debug.LogException(e);
            return false;
        }
    }

    private string GetLocalGuestIdSafe()
    {
        try
        {
            string id = Backend.BMember.GetGuestID();
            return string.IsNullOrEmpty(id) ? "(empty)" : id;
        }
        catch (System.Exception e)
        {
            AddDebug("GetGuestID 예외 발생");
            AddDebug(e.Message);
            Debug.LogException(e);
            return "(error)";
        }
    }

    private bool IsBadCustomId(BackendReturnObject bro)
    {
        if (bro == null)
            return false;

        string errorCode = SafeGetErrorCode(bro);
        string message = SafeGetMessage(bro);

        return errorCode == "BadUnauthorizedException" &&
               !string.IsNullOrEmpty(message) &&
               message.ToLower().Contains("bad customid");
    }

    private string SafeGetStatusCode(BackendReturnObject bro)
    {
        try { return bro.GetStatusCode(); }
        catch { return "(unknown)"; }
    }

    private string SafeGetErrorCode(BackendReturnObject bro)
    {
        try { return bro.GetErrorCode(); }
        catch { return "(unknown)"; }
    }

    private string SafeGetMessage(BackendReturnObject bro)
    {
        try { return bro.GetMessage(); }
        catch { return "(unknown)"; }
    }

    private void LogBro(string title, BackendReturnObject bro)
    {
        AddDebug(title);
        AddDebug($"StatusCode = {SafeGetStatusCode(bro)}");
        AddDebug($"ErrorCode = {SafeGetErrorCode(bro)}");
        AddDebug($"Message = {SafeGetMessage(bro)}");
    }

    private void FailLogin(string uiMessage)
    {
        if (loadingStatusText != null)
            loadingStatusText.text = uiMessage;

        SetGuestButtonInteractable(true);
        isProcessing = false;
    }

    private IEnumerator LoadLobbyRoutine()
    {
        AddDebug("LoadLobbyRoutine 시작");

        SafeSetActive(loginRoot, false);
        SafeSetActive(loadingRoot, true);

        SetLoadingUI(0f, "Now Loading...");

        if (!Application.CanStreamedLevelBeLoaded(lobbySceneName))
        {
            AddDebug($"씬 로드 불가: {lobbySceneName}");
            AddDebug("Build Settings > Scenes In Build 등록 확인");

            SetLoadingUI(0f, "Scene Missing");
            SafeSetActive(loginRoot, true);
            SafeSetActive(loadingRoot, false);

            SetGuestButtonInteractable(true);
            isProcessing = false;
            yield break;
        }

        AddDebug($"씬 로드 시작: {lobbySceneName}");

        AsyncOperation op = null;

        try
        {
            op = SceneManager.LoadSceneAsync(lobbySceneName);
        }
        catch (System.Exception e)
        {
            AddDebug("LoadSceneAsync 예외 발생");
            AddDebug(e.Message);
            Debug.LogException(e);

            SafeSetActive(loginRoot, true);
            SafeSetActive(loadingRoot, false);

            SetGuestButtonInteractable(true);
            isProcessing = false;
            yield break;
        }

        if (op == null)
        {
            AddDebug("LoadSceneAsync 반환값이 null 입니다.");
            SafeSetActive(loginRoot, true);
            SafeSetActive(loadingRoot, false);

            SetGuestButtonInteractable(true);
            isProcessing = false;
            yield break;
        }

        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f);
            SetLoadingUI(progress, "Now Loading...");
            yield return null;
        }

        AddDebug("씬 데이터 로드 완료, 활성화 대기 중");
        SetLoadingUI(1f, "Complete");

        yield return new WaitForSeconds(0.1f);

        AddDebug("씬 활성화");
        op.allowSceneActivation = true;

        while (!op.isDone)
            yield return null;
    }

    private void SetLoadingUI(float value, string message)
    {
        value = Mathf.Clamp01(value);

        if (loadingSlider != null)
            loadingSlider.value = value;

        if (loadingStatusText != null)
            loadingStatusText.text = message;

        if (loadingPercentText != null)
            loadingPercentText.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private void SetGuestButtonInteractable(bool value)
    {
        if (guestLoginButton != null)
            guestLoginButton.interactable = value;
    }

    private void SafeSetActive(GameObject go, bool active)
    {
        if (go != null)
            go.SetActive(active);
    }

    private void AddDebug(string msg)
    {
        string line = $"[{System.DateTime.Now:HH:mm:ss}] {msg}";
        Debug.Log(line);

        if (debugText == null)
            return;

        debugLines.Enqueue(line);

        while (debugLines.Count > Mathf.Max(1, debugLineMax))
            debugLines.Dequeue();

        StringBuilder sb = new StringBuilder();
        foreach (string s in debugLines)
            sb.AppendLine(s);

        debugText.text = sb.ToString();
    }

    private void HandleUnityLog(string condition, string stackTrace, LogType type)
    {
        if (debugText == null)
            return;

        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            string line = $"[ERROR] {condition}";
            debugLines.Enqueue(line);

            while (debugLines.Count > Mathf.Max(1, debugLineMax))
                debugLines.Dequeue();

            StringBuilder sb = new StringBuilder();
            foreach (string s in debugLines)
                sb.AppendLine(s);

            debugText.text = sb.ToString();
        }
    }
}