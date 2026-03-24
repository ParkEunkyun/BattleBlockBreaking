using System;
using System.Reflection;
using BackEnd;
using BackEnd.Tcp;
using LitJson;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MatchManager : MonoBehaviour
{
    public static MatchManager I { get; private set; }

    [Header("Scene")]
    [SerializeField] private string battleSceneName = "Scene_Battle";
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Ranked Match Card")]
    [Tooltip("콘솔의 매치 카드 inDate를 직접 넣어도 되고, 비워두면 아래 옵션으로 자동 탐색.")]
    [SerializeField] private string rankedMatchCardInDate = "";

    [Tooltip("true면 GetMatchList()로 자동 탐색")]
    [SerializeField] private bool autoResolveMatchCardInDate = true;

    [Tooltip("매치 카드 제목에 들어있는 키워드. 비워두면 타입/모드만 맞는 첫 카드 사용")]
    [SerializeField] private string rankedMatchTitleKeyword = "rank";

    [Header("Enum Name (SDK별 표기 차이 대응)")]
    [Tooltip("보통 MMR / Random / Point 중 하나")]
    [SerializeField] private string rankedMatchTypeName = "MMR";

    [Tooltip("보통 OneOnOne")]
    [SerializeField] private string rankedMatchModeTypeName = "OneOnOne";

    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

    private bool _eventsBound;
    private bool _pollEnabled;
    private bool _isStartingMatch;
    private bool _isWaitingForMatch;
    private bool _isLoadingBattleScene;

    private MatchType _cachedMatchType;
    private MatchModeType _cachedMatchModeType;

    private bool _isCancellingMatch;
    private bool _isConnectedToMatchServer;
    private bool _isInMatchRoom;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (I == this)
            I = null;
    }

    private void Update()
    {
        if (!_pollEnabled)
            return;

        try
        {
            Backend.Match.Poll();
        }
        catch (Exception e)
        {
            LogError("Poll 예외", e.Message);
        }
    }

    public void StartRankedMatch()
    {
        if (_isCancellingMatch)
        {
            Log("지금은 매칭 취소 처리중이라 재시작 불가");
            return;
        }

        if (_isStartingMatch || _isWaitingForMatch || _isLoadingBattleScene)
        {
            Log("이미 매칭 진행 중");
            return;
        }

        string myNick = GetBackendNicknameSafe();
        if (string.IsNullOrWhiteSpace(myNick) || myNick == "Me")
        {
            LogError("닉네임 없음", "뒤끝 매치 접속 전 닉네임 생성/변경이 먼저 필요함");
            return;
        }

        if (!BattleLoadoutSession.HasValidLoadout())
        {
            LogError("로드아웃이 유효하지 않음", "공격 3 / 지원 2가 필요함");
            return;
        }

        BattleLoadoutSession.Mode = GameMode.Ranked;

        if (!TryParseMatchEnums(out _cachedMatchType, out _cachedMatchModeType))
            return;

        if (string.IsNullOrWhiteSpace(rankedMatchCardInDate))
        {
            if (!autoResolveMatchCardInDate)
            {
                LogError("matchCardInDate 비어있음", "인스펙터에 직접 넣거나 자동 탐색을 켜줘");
                return;
            }

            if (!TryResolveMatchCardInDateFromConsole())
                return;
        }

        BattleMatchSession.Clear();
        BattleMatchSession.Mode = GameMode.Ranked;
        BattleMatchSession.MyNickname = myNick;

        BindEvents();

        _pollEnabled = true;
        _isStartingMatch = true;
        _isWaitingForMatch = false;
        _isLoadingBattleScene = false;

        // 이미 매칭 서버에 붙어 있으면 재접속하지 말고 바로 방 생성부터
        if (_isConnectedToMatchServer)
        {
            try
            {
                Backend.Match.CreateMatchRoom();
                Log("이미 매칭 서버 연결됨 -> CreateMatchRoom 재요청");
            }
            catch (Exception e)
            {
                ResetMatchFlags();
                LogError("CreateMatchRoom 재요청 예외", e.Message);
            }

            return;
        }

        TryJoinMatchMakingServer();
    }

    public void CancelRankedMatch()
    {
        if (_isCancellingMatch)
        {
            Log("이미 취소 처리중");
            return;
        }

        _isCancellingMatch = true;

        try
        {
            // 매칭 대기중이면 먼저 취소 요청
            if (_isWaitingForMatch)
            {
                Backend.Match.CancelMatchMaking();
                Log("매칭 취소 요청");
                return;
            }

            // 아직 매칭 대기 전(접속중/방생성중 등)이면 바로 서버 연결 종료
            if (_isConnectedToMatchServer)
            {
                Backend.Match.LeaveMatchMakingServer();
                Log("매칭 서버 접속 종료 요청");
                return;
            }

            // 이미 아무 상태도 아니면 즉시 정리
            ForceCleanupAfterCancel();
        }
        catch (Exception e)
        {
            LogError("매칭 취소 예외", e.Message);
            ForceCleanupAfterCancel();
        }
    }

    private void BindEvents()
    {
        if (_eventsBound)
            return;

        Backend.Match.OnJoinMatchMakingServer = args =>
        {
            object errInfo = ReadMember(args, "ErrInfo");

            string errCategory = ReadMemberAsString(errInfo, "Category");
            string errReason = ReadMemberAsString(errInfo, "Reason");
            string errSocket = ReadMemberAsString(errInfo, "SocketError");

            bool isSuccess =
                string.Equals(errCategory, "Success", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errReason, "Success", StringComparison.OrdinalIgnoreCase);

            Log($"OnJoinMatchMakingServer -> Category={errCategory}, Reason={errReason}, Socket={errSocket}");

            if (isSuccess)
            {
                _isConnectedToMatchServer = true;

                try
                {
                    Backend.Match.CreateMatchRoom();
                    Log("CreateMatchRoom 요청");
                }
                catch (Exception e)
                {
                    ResetMatchFlags();
                    LogError("CreateMatchRoom 예외", e.Message);
                }
            }
            else
            {
                _isConnectedToMatchServer = false;
                ResetMatchFlags();
                LogError("매칭 서버 접속 실패",
                    $"Category={errCategory}, Reason={errReason}, Socket={errSocket}");
            }
        };

        Backend.Match.OnMatchMakingRoomCreate = args =>
        {
            string err = ReadMemberAsString(args, "ErrInfo");

            Log($"OnMatchMakingRoomCreate -> {err}");

            if (string.Equals(err, ErrorCode.Success.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    _isInMatchRoom = true;
                    _isStartingMatch = false;
                    _isWaitingForMatch = true;

                    Backend.Match.RequestMatchMaking(
                        _cachedMatchType,
                        _cachedMatchModeType,
                        rankedMatchCardInDate);

                    Log($"RequestMatchMaking 요청 / card={rankedMatchCardInDate}");
                }
                catch (Exception e)
                {
                    ResetMatchFlags();
                    LogError("RequestMatchMaking 예외", e.Message);
                }
            }
            else
            {
                ResetMatchFlags();
                LogError("대기방 생성 실패", ReadMemberAsString(args, "Reason"));
            }
        };

        Backend.Match.OnMatchMakingResponse = args =>
        {
            string err = ReadMemberAsString(args, "ErrInfo");
            string reason = ReadMemberAsString(args, "Reason");

            Log($"OnMatchMakingResponse -> {err} / {reason}");

            if (string.Equals(err, ErrorCode.Match_InProgress.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                _isStartingMatch = false;
                _isWaitingForMatch = true;
                Log("매칭 대기 시작");
                return;
            }

            if (string.Equals(err, ErrorCode.Match_MatchMakingCanceled.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                Log("매칭 취소 완료 -> 매칭 서버 접속 종료 요청");

                _isWaitingForMatch = false;

                try
                {
                    Backend.Match.LeaveMatchMakingServer();
                }
                catch (Exception e)
                {
                    LogError("LeaveMatchMakingServer 예외", e.Message);
                    ForceCleanupAfterCancel();
                }

                return;
            }

            if (string.Equals(err, ErrorCode.Success.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                _isWaitingForMatch = false;
                _isLoadingBattleScene = true;
                _isCancellingMatch = false;

                BattleMatchSession.Mode = GameMode.Ranked;
                BattleMatchSession.MatchCardInDate = ReadMemberAsString(args, "MatchCardIndate");

                object roomInfo = ReadMember(args, "RoomInfo");
                ApplyRoomInfoToSession(roomInfo);

                LoadBattleScene();
                return;
            }

            ResetMatchFlags();
            LogError("매칭 실패", reason);
        };

        Backend.Match.OnLeaveMatchMakingServer = args =>
        {
            object errInfo = ReadMember(args, "ErrInfo");

            string errCategory = ReadMemberAsString(errInfo, "Category");
            string errDetail = ReadMemberAsString(errInfo, "Detail");
            string errReason = ReadMemberAsString(errInfo, "Reason");

            Log($"OnLeaveMatchMakingServer -> Category={errCategory}, Detail={errDetail}, Reason={errReason}");

            _isConnectedToMatchServer = false;
            _isInMatchRoom = false;

            ForceCleanupAfterCancel();
        };

        _eventsBound = true;
    }

    private void TryJoinMatchMakingServer()
    {
        try
        {
            ErrorInfo errorInfo;
            bool requested = Backend.Match.JoinMatchMakingServer(out errorInfo);

            if (!requested)
            {
                ResetMatchFlags();
                LogError("JoinMatchMakingServer 요청 실패", errorInfo != null ? errorInfo.Reason : "Unknown");
                return;
            }

            Log("JoinMatchMakingServer 요청 성공(소켓 연결 시도)");
        }
        catch (Exception e)
        {
            ResetMatchFlags();
            LogError("JoinMatchMakingServer 예외", e.Message);
        }
    }

    private bool TryParseMatchEnums(out MatchType matchType, out MatchModeType modeType)
    {
        matchType = default;
        modeType = default;

        if (!Enum.TryParse(rankedMatchTypeName, true, out matchType))
        {
            LogError("MatchType 파싱 실패", $"인스펙터의 Ranked Match Type Name = {rankedMatchTypeName}");
            return false;
        }

        if (!Enum.TryParse(rankedMatchModeTypeName, true, out modeType))
        {
            LogError("MatchModeType 파싱 실패", $"인스펙터의 Ranked Match Mode Type Name = {rankedMatchModeTypeName}");
            return false;
        }

        return true;
    }

    private bool TryResolveMatchCardInDateFromConsole()
    {
        try
        {
            BackendReturnObject bro = Backend.Match.GetMatchList();

            if (bro == null || !bro.IsSuccess())
            {
                LogError("GetMatchList 실패", bro != null ? bro.ToString() : "null");
                return false;
            }

            JsonData rows = bro.FlattenRows();
            if (rows == null || rows.Count <= 0)
            {
                LogError("GetMatchList 결과 없음", "콘솔에 매치 카드가 없음");
                return false;
            }

            string typeLower = (rankedMatchTypeName ?? "").Trim().ToLowerInvariant();
            string modeLower = (rankedMatchModeTypeName ?? "").Trim().ToLowerInvariant();
            string keywordLower = (rankedMatchTitleKeyword ?? "").Trim().ToLowerInvariant();

            for (int i = 0; i < rows.Count; i++)
            {
                JsonData row = rows[i];

                string rowType = GetJsonStringSafe(row, "matchType").Trim().ToLowerInvariant();
                string rowMode = GetJsonStringSafe(row, "matchModeType").Trim().ToLowerInvariant();
                string rowTitle = GetJsonStringSafe(row, "matchTitle");
                string rowInDate = GetJsonStringSafe(row, "inDate");

                if (rowType != typeLower)
                    continue;

                if (rowMode != modeLower)
                    continue;

                if (!string.IsNullOrWhiteSpace(keywordLower) &&
                    !rowTitle.ToLowerInvariant().Contains(keywordLower))
                    continue;

                rankedMatchCardInDate = rowInDate;
                Log($"자동 탐색된 matchCardInDate = {rankedMatchCardInDate} / title = {rowTitle}");
                return true;
            }

            LogError(
                "자동 매치 카드 탐색 실패",
                $"type={rankedMatchTypeName}, mode={rankedMatchModeTypeName}, keyword={rankedMatchTitleKeyword}");
            return false;
        }
        catch (Exception e)
        {
            LogError("GetMatchList 예외", e.Message);
            return false;
        }
    }

    private void LoadBattleScene()
    {
        try
        {
            // 배틀씬에서는 BattleNetDriver만 Poll 하게 함
            _pollEnabled = false;
            enabled = false;

            Log($"배틀씬 진입 -> {battleSceneName}");
            SceneManager.LoadScene(battleSceneName);
        }
        catch (Exception e)
        {
            ResetMatchFlags();
            LogError("배틀씬 로드 예외", e.Message);
        }
    }

    private void ApplyRoomInfoToSession(object roomInfo)
    {
        if (roomInfo == null)
            return;

        BattleMatchSession.InGameRoomToken = ReadMemberAsString(roomInfo, "m_inGameRoomToken");
        BattleMatchSession.IsSandbox = ReadMemberAsBool(roomInfo, "m_enableSandBox");

        object endpoint = ReadMember(roomInfo, "m_inGameServerEndPoint");
        if (endpoint != null)
        {
            BattleMatchSession.InGameServerAddress = ReadMemberAsString(endpoint, "m_address");
            BattleMatchSession.InGameServerPort = ReadMemberAsUShort(endpoint, "m_port");
        }
    }

    private void ForceCleanupAfterCancel()
    {
        _isCancellingMatch = false;
        _isConnectedToMatchServer = false;
        _isInMatchRoom = false;

        ResetMatchFlags();
        BattleMatchSession.Clear();

        Log("취소 후 상태 정리 완료");
    }

    private void ResetMatchFlags()
    {
        _isStartingMatch = false;
        _isWaitingForMatch = false;
        _isLoadingBattleScene = false;
    }

    private string GetBackendNicknameSafe()
    {
        try
        {
            PropertyInfo prop = typeof(Backend).GetProperty(
                "UserNickName",
                BindingFlags.Public | BindingFlags.Static);

            if (prop != null)
            {
                object value = prop.GetValue(null, null);
                string nick = value != null ? value.ToString() : "";
                if (!string.IsNullOrWhiteSpace(nick))
                    return nick;
            }
        }
        catch
        {
            // ignore
        }

        return "Me";
    }

    private static object ReadMember(object target, string memberName)
    {
        if (target == null || string.IsNullOrWhiteSpace(memberName))
            return null;

        Type type = target.GetType();

        PropertyInfo prop = type.GetProperty(
            memberName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        if (prop != null)
            return prop.GetValue(target, null);

        FieldInfo field = type.GetField(
            memberName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        if (field != null)
            return field.GetValue(target);

        return null;
    }

    private static string ReadMemberAsString(object target, string memberName)
    {
        object value = ReadMember(target, memberName);
        return value != null ? value.ToString() : "";
    }

    private static bool ReadMemberAsBool(object target, string memberName)
    {
        object value = ReadMember(target, memberName);
        if (value == null) return false;

        if (value is bool b) return b;

        bool.TryParse(value.ToString(), out bool parsed);
        return parsed;
    }

    private static ushort ReadMemberAsUShort(object target, string memberName)
    {
        object value = ReadMember(target, memberName);
        if (value == null) return 0;

        if (value is ushort us) return us;

        ushort.TryParse(value.ToString(), out ushort parsed);
        return parsed;
    }

    private static string GetJsonStringSafe(JsonData row, string key)
    {
        try
        {
            return row[key].ToString();
        }
        catch
        {
            return "";
        }
    }

    private void Log(string msg)
    {
        if (!verboseLog) return;
        Debug.Log($"[MatchManager] {msg}");
    }

    private void LogError(string title, string detail = "")
    {
        Debug.LogError($"[MatchManager] {title}" + (string.IsNullOrWhiteSpace(detail) ? "" : $" / {detail}"));
    }


}