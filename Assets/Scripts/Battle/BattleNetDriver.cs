using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BackEnd;
using BackEnd.Tcp;
using UnityEngine;

public class BattleNetDriver : MonoBehaviour
{
    private enum PacketOp : byte
    {
        Hello = 1,
        Loadout = 2,
        BattleReady = 3,
        GameStart = 4,

        // 다음 단계에서 사용할 예약 패킷
        RoundReady = 20,
        BoardSnapshot = 21,
        ScoreSync = 22,
        ItemUse = 23,
        AttackIntent = 24,
        AttackOutcome = 25,
        Result = 90,
    }

    [Header("Refs")]
    [SerializeField] private BattleManager battleManager;

    [Header("Options")]
    [SerializeField] private bool autoBeginOnStart = true;
    [SerializeField] private bool verboseLog = true;

    private bool _eventsBound;
    private bool _pollEnabled;

    private bool _joiningServer;
    private bool _joinedServer;
    private bool _joiningRoom;
    private bool _joinedRoom;

    private bool _systemGameStarted;
    private bool _localHelloSent;
    private bool _localLoadoutSent;
    private bool _localReadySent;

    private bool _remoteHelloReceived;
    private bool _remoteLoadoutReceived;
    private bool _remoteReadyReceived;

    private bool _gameStartSent;
    private bool _battleStarted;

    private bool _localRoundReadySent;
    private bool _remoteRoundReadyReceived;

    private bool _matchEndSent;
    private void Awake()
    {
        if (battleManager == null)
            battleManager = GetComponent<BattleManager>();
    }

    private void Start()
    {
        if (autoBeginOnStart)
            BeginRankedInGame();
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

    public void BeginRankedInGame()
    {
        if (BattleMatchSession.Mode != GameMode.Ranked)
        {
            Log("Ranked 모드가 아니라서 BattleNetDriver 시작 안 함");
            return;
        }

        if (!BattleMatchSession.HasValidRankedMatch)
        {
            LogError("BattleMatchSession 데이터 부족",
                $"roomToken={BattleMatchSession.InGameRoomToken}, addr={BattleMatchSession.InGameServerAddress}, port={BattleMatchSession.InGameServerPort}");
            return;
        }

        if (_joiningServer || _joinedServer || _joiningRoom || _joinedRoom)
        {
            Log("이미 인게임 접속 흐름 진행중");
            return;
        }

        BindEvents();
        _pollEnabled = true;

        JoinInGameServer();
    }

    private void JoinInGameServer()
    {
        try
        {
            _joiningServer = true;

            ErrorInfo errorInfo;
            bool requested = Backend.Match.JoinGameServer(
                BattleMatchSession.InGameServerAddress,
                BattleMatchSession.InGameServerPort,
                false,
                out errorInfo);

            if (!requested)
            {
                _joiningServer = false;
                LogError("JoinGameServer 요청 실패", FormatErrorInfo(errorInfo));
                return;
            }

            Log($"JoinGameServer 요청 성공 / {BattleMatchSession.InGameServerAddress}:{BattleMatchSession.InGameServerPort}");
        }
        catch (Exception e)
        {
            _joiningServer = false;
            LogError("JoinGameServer 예외", e.Message);
        }
    }

    private void JoinGameRoom()
    {
        if (_joiningRoom || _joinedRoom)
            return;

        try
        {
            _joiningRoom = true;
            Backend.Match.JoinGameRoom(BattleMatchSession.InGameRoomToken);
            Log($"JoinGameRoom 요청 / token={BattleMatchSession.InGameRoomToken}");
        }
        catch (Exception e)
        {
            _joiningRoom = false;
            LogError("JoinGameRoom 예외", e.Message);
        }
    }

    private void BindEvents()
    {
        if (_eventsBound)
            return;

        Backend.Match.OnSessionJoinInServer = args =>
        {
            object errInfo = ReadMember(args, "ErrInfo");
            object sessionInfo = ReadMember(args, "SessionInfo");

            bool success = IsSuccessErrInfo(errInfo);

            string sessionId = ExtractSessionId(sessionInfo);
            SessionId rawSessionId = ExtractSessionIdValue(sessionInfo);

            string reason = ReadMemberAsString(errInfo, "Reason");
            string category = ReadMemberAsString(errInfo, "Category");

            Log($"OnSessionJoinInServer / success={success} / category={category} / reason={reason} / mySession={sessionId}");

            if (!success)
            {
                _joiningServer = false;
                _joinedServer = false;
                LogError("인게임 서버 접속 실패", $"{category} / {reason}");
                return;
            }

            _joiningServer = false;
            _joinedServer = true;

            if (!string.IsNullOrWhiteSpace(sessionId))
                BattleMatchSession.MySessionId = sessionId;

            BattleMatchSession.MySession = rawSessionId;

            JoinGameRoom();
        };

        Backend.Match.OnSessionListInServer = args =>
        {
            _joiningRoom = false;
            _joinedRoom = true;

            // 문서상 OnSessionListInServer는 게임방 입장 성공 시에만 호출됨.
            // 그래서 여기서는 실패 판정하지 말고, GameRecords 파싱에만 집중.
            int count = args.GameRecords != null ? args.GameRecords.Count : 0;

            Log($"OnSessionListInServer / joinedRoom=true / count={count}");

            TryResolveSessionsFromGameRecords(args.GameRecords);

            Log($"SessionResolved / my={BattleMatchSession.MySessionId} / opp={BattleMatchSession.OpponentSessionId} / isHost={BattleMatchSession.IsHost}");
        };

        Backend.Match.OnMatchInGameStart = () =>
        {
            _systemGameStarted = true;
            Log("OnMatchInGameStart 수신");

            SendHello();
            SendLoadout();
            SendBattleReady();
            TrySendGameStart();
        };

        Backend.Match.OnMatchRelay = args =>
        {
            object fromObj = ReadMember(args, "From");
            byte[] data = ReadMember(args, "BinaryUserData") as byte[];

            string fromSessionId = ExtractSessionId(fromObj);

            if (data == null || data.Length == 0)
                return;

            // 자기 자신이 보낸 패킷도 돌아오므로 무시
            if (!string.IsNullOrWhiteSpace(BattleMatchSession.MySessionId) &&
                fromSessionId == BattleMatchSession.MySessionId)
            {
                return;
            }

            HandleRelayPacket(fromSessionId, data);
        };

        Backend.Match.OnSessionOffline = args =>
        {
            string offlineSessionId = ExtractSessionId(ReadMember(args, "SessionInfo"));
            LogError("세션 오프라인 감지", offlineSessionId);
        };

        Backend.Match.OnMatchResult = args =>
        {
            string err = ReadMemberAsString(args, "ErrInfo");
            string reason = ReadMemberAsString(args, "Reason");

            Log($"OnMatchResult / err={err} / reason={reason}");

            RankedRecordService.RefreshMyRankedRecord((ok, message) =>
            {
                if (ok)
                    Log($"RankedRecord refreshed / {message}");
                else
                    LogError("RankedRecord refresh failed", message);

                if (battleManager != null)
                    battleManager.OnRankedRecordRefreshedFromServer();
            });
        };

        _eventsBound = true;
    }

    private void HandleRelayPacket(string fromSessionId, byte[] data)
    {
        try
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader br = new BinaryReader(ms))
            {
                PacketOp op = (PacketOp)br.ReadByte();

                switch (op)
                {
                    case PacketOp.Hello:
                        {
                            string nickname = br.ReadString();

                            _remoteHelloReceived = true;
                            BattleMatchSession.OpponentSessionId = fromSessionId;
                            BattleMatchSession.OpponentNickname = nickname;

                            RefreshHostAuthority();
                            ApplyOpponentNicknameToBattle();

                            Log($"[RECV] HELLO / from={fromSessionId} / nick={nickname} / isHost={BattleMatchSession.IsHost}");

                            TrySendGameStart();
                            break;
                        }

                    case PacketOp.Loadout:
                        {
                            _remoteLoadoutReceived = true;
                            ReadOpponentLoadout(br);

                            Log($"[RECV] LOADOUT / attack={BattleMatchSession.OpponentAttackIds.Count} / support={BattleMatchSession.OpponentSupportIds.Count}");
                            TrySendGameStart();
                            break;
                        }

                    case PacketOp.BattleReady:
                        {
                            _remoteReadyReceived = true;
                            Log("[RECV] BATTLE_READY");
                            TrySendGameStart();
                            break;
                        }

                    case PacketOp.GameStart:
                        {
                            int seed = br.ReadInt32();
                            Log($"[RECV] GAME_START / seed={seed}");
                            ApplyGameStart(seed);
                            break;
                        }

                    case PacketOp.RoundReady:
                        {
                            _remoteRoundReadyReceived = true;

                            if (!string.IsNullOrWhiteSpace(fromSessionId))
                                BattleMatchSession.OpponentSessionId = fromSessionId;

                            Log($"[RECV] ROUND_READY / from={fromSessionId}");

                            if (battleManager != null)
                                battleManager.OnOpponentRoundReadyReceived();

                            break;
                        }

                    case PacketOp.ScoreSync:
                        {
                            int score = br.ReadInt32();

                            Log($"[RECV] SCORE_SYNC / from={fromSessionId} / score={score}");

                            if (battleManager != null)
                                battleManager.OnOpponentScoreSyncReceived(score);

                            break;
                        }

                    case PacketOp.ItemUse:
                        {
                            BattleManager.BattleItemId itemId = (BattleManager.BattleItemId)br.ReadInt32();

                            Log($"[RECV] ITEM_USE / from={fromSessionId} / item={itemId}");

                            if (battleManager != null)
                                battleManager.OnNetworkItemUseReceived(itemId);

                            break;
                        }

                    case PacketOp.BoardSnapshot:
                        {
                            int remaining = (int)(ms.Length - ms.Position);
                            byte[] payload = br.ReadBytes(remaining);

                            Log($"[RECV] BOARD_SNAPSHOT / from={fromSessionId} / bytes={payload.Length}");

                            if (battleManager != null)
                                battleManager.OnOpponentBoardSnapshotReceived(payload);

                            break;
                        }

                    case PacketOp.AttackIntent:
                        {
                            BattleManager.BattleItemId itemId = (BattleManager.BattleItemId)br.ReadInt32();
                            bool isReserved = br.ReadBoolean();

                            Log($"[RECV] ATTACK_INTENT / from={fromSessionId} / item={itemId} / reserved={isReserved}");

                            if (battleManager != null)
                                battleManager.OnNetworkAttackIntentReceived(itemId, isReserved);

                            break;
                        }

                    case PacketOp.AttackOutcome:
                        {
                            bool wasBlocked = br.ReadBoolean();

                            Log($"[RECV] ATTACK_OUTCOME / from={fromSessionId} / blocked={wasBlocked}");

                            if (battleManager != null)
                                battleManager.OnNetworkAttackOutcomeReceived(wasBlocked);

                            break;
                        }
                    default:
                        {
                            Log($"알 수 없는 패킷 수신 / op={(byte)op}");
                            break;
                        }
                }
            }
        }
        catch (Exception e)
        {
            LogError("릴레이 패킷 파싱 예외", e.Message);
        }
    }

    private void ReadOpponentLoadout(BinaryReader br)
    {
        BattleMatchSession.OpponentAttackIds.Clear();
        BattleMatchSession.OpponentSupportIds.Clear();

        br.ReadByte(); // mode

        int attackCount = br.ReadByte();
        for (int i = 0; i < attackCount; i++)
        {
            BattleMatchSession.OpponentAttackIds.Add((BattleManager.AttackItemId)br.ReadInt32());
        }

        int supportCount = br.ReadByte();
        for (int i = 0; i < supportCount; i++)
        {
            BattleMatchSession.OpponentSupportIds.Add((BattleManager.SupportItemId)br.ReadInt32());
        }
    }

    public void SendScoreSync(int score)
    {
        if (!_battleStarted)
        {
            LogError("SCORE_SYNC 전송 실패", "아직 배틀 시작 전");
            return;
        }

        if (!_joinedRoom)
        {
            LogError("SCORE_SYNC 전송 실패", "아직 게임방 입장 전");
            return;
        }

        byte[] packet = BuildScoreSyncPacket(score);
        if (SendPacket(packet))
        {
            Log($"[SEND] SCORE_SYNC / mySession={BattleMatchSession.MySessionId} / score={score}");
        }
    }
    private byte[] BuildScoreSyncPacket(int score)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write((byte)PacketOp.ScoreSync);
            bw.Write(score);
            return ms.ToArray();
        }
    }

    private void SendHello()
    {
        if (_localHelloSent || !_systemGameStarted)
            return;

        string myNick = string.IsNullOrWhiteSpace(BattleMatchSession.MyNickname)
            ? "Player"
            : BattleMatchSession.MyNickname;

        byte[] packet = BuildHelloPacket(myNick);
        if (SendPacket(packet))
        {
            _localHelloSent = true;
            Log($"[SEND] HELLO / nick={myNick}");
        }
    }

    private void SendLoadout()
    {
        if (_localLoadoutSent || !_systemGameStarted)
            return;

        byte[] packet = BuildLoadoutPacket();
        if (SendPacket(packet))
        {
            _localLoadoutSent = true;
            Log($"[SEND] LOADOUT / attack={BattleLoadoutSession.SelectedAttackIds.Count} / support={BattleLoadoutSession.SelectedSupportIds.Count}");
        }
    }

    private void SendBattleReady()
    {
        if (_localReadySent || !_systemGameStarted)
            return;

        byte[] packet = BuildBattleReadyPacket();
        if (SendPacket(packet))
        {
            _localReadySent = true;
            Log("[SEND] BATTLE_READY");
        }
    }

    private void TrySendGameStart()
    {
        if (_battleStarted || _gameStartSent)
            return;

        if (!_systemGameStarted)
            return;

        if (!_localHelloSent || !_localLoadoutSent || !_localReadySent)
            return;

        if (!_remoteHelloReceived || !_remoteLoadoutReceived || !_remoteReadyReceived)
            return;

        RefreshHostAuthority();

        if (!BattleMatchSession.IsHost)
            return;

        int seed = GenerateSeed();
        byte[] packet = BuildGameStartPacket(seed);

        if (SendPacket(packet))
        {
            _gameStartSent = true;
            Log($"[SEND] GAME_START / seed={seed}");
            ApplyGameStart(seed); // 자기 자신은 self echo를 무시하므로 직접 적용
        }
    }

    private void ApplyGameStart(int seed)
    {
        if (_battleStarted)
            return;

        _battleStarted = true;
        _matchEndSent = false;
        ResetRoundSyncState();
        BattleMatchSession.MatchSeed = seed;

        if (battleManager != null)
        {
            battleManager.SetOpponentNicknameExternal(BattleMatchSession.OpponentNickname);
            battleManager.OnNetworkGameStart(seed, BattleMatchSession.IsHost);
        }

        Log($"배틀 시작 확정 / seed={seed} / isHost={BattleMatchSession.IsHost}");
    }

    public void ResetRoundSyncState()
    {
        _localRoundReadySent = false;
        _remoteRoundReadyReceived = false;
    }

    public void SendRoundReady()
    {
        if (!_battleStarted)
        {
            LogError("ROUND_READY 전송 실패", "아직 배틀 시작 전");
            return;
        }

        if (!_joinedRoom)
        {
            LogError("ROUND_READY 전송 실패", "아직 게임방 입장 전");
            return;
        }

        if (_localRoundReadySent)
        {
            Log("[SEND] ROUND_READY already sent");
            return;
        }

        byte[] packet = BuildRoundReadyPacket();
        if (SendPacket(packet))
        {
            _localRoundReadySent = true;
            Log($"[SEND] ROUND_READY / mySession={BattleMatchSession.MySessionId}");
        }
    }

    private byte[] BuildRoundReadyPacket()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write((byte)PacketOp.RoundReady);
            return ms.ToArray();
        }
    }

    private void ApplyOpponentNicknameToBattle()
    {
        if (battleManager != null && !string.IsNullOrWhiteSpace(BattleMatchSession.OpponentNickname))
        {
            battleManager.SetOpponentNicknameExternal(BattleMatchSession.OpponentNickname);
        }
    }

    private void RefreshHostAuthority()
    {
        if (string.IsNullOrWhiteSpace(BattleMatchSession.MySessionId) ||
            string.IsNullOrWhiteSpace(BattleMatchSession.OpponentSessionId))
        {
            return;
        }

        // 세션ID 문자열 사전순으로 1명만 호스트 고정
        BattleMatchSession.IsHost =
            string.CompareOrdinal(BattleMatchSession.MySessionId, BattleMatchSession.OpponentSessionId) < 0;
    }

    private byte[] BuildHelloPacket(string nickname)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write((byte)PacketOp.Hello);
            bw.Write(nickname ?? string.Empty);
            return ms.ToArray();
        }
    }

    private byte[] BuildLoadoutPacket()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write((byte)PacketOp.Loadout);
            bw.Write((byte)BattleLoadoutSession.Mode);

            bw.Write((byte)BattleLoadoutSession.SelectedAttackIds.Count);
            for (int i = 0; i < BattleLoadoutSession.SelectedAttackIds.Count; i++)
            {
                bw.Write((int)BattleLoadoutSession.SelectedAttackIds[i]);
            }

            bw.Write((byte)BattleLoadoutSession.SelectedSupportIds.Count);
            for (int i = 0; i < BattleLoadoutSession.SelectedSupportIds.Count; i++)
            {
                bw.Write((int)BattleLoadoutSession.SelectedSupportIds[i]);
            }

            return ms.ToArray();
        }
    }

    private byte[] BuildBattleReadyPacket()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write((byte)PacketOp.BattleReady);
            return ms.ToArray();
        }
    }

    private byte[] BuildGameStartPacket(int seed)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write((byte)PacketOp.GameStart);
            bw.Write(seed);
            return ms.ToArray();
        }
    }

    private bool SendPacket(byte[] packet)
    {
        if (packet == null || packet.Length == 0)
            return false;

        if (!_joinedRoom)
        {
            LogError("패킷 전송 실패", "아직 게임방 입장 전");
            return false;
        }

        try
        {
            Backend.Match.SendDataToInGameRoom(packet);
            return true;
        }
        catch (Exception e)
        {
            LogError("SendDataToInGameRoom 예외", e.Message);
            return false;
        }
    }

    private int GenerateSeed()
    {
        unchecked
        {
            int a = Environment.TickCount;
            int b = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            int c = DateTime.UtcNow.Millisecond;
            return a ^ b ^ (c << 8);
        }
    }

    private static bool IsSuccessErrInfo(object errInfo)
    {
        if (errInfo == null)
            return false;

        string category = ReadMemberAsString(errInfo, "Category");
        string reason = ReadMemberAsString(errInfo, "Reason");

        return string.Equals(category, "Success", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(reason, "Success", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatErrorInfo(object errInfo)
    {
        if (errInfo == null)
            return "null";

        string category = ReadMemberAsString(errInfo, "Category");
        string reason = ReadMemberAsString(errInfo, "Reason");
        string socket = ReadMemberAsString(errInfo, "SocketError");

        return $"Category={category}, Reason={reason}, Socket={socket}";
    }

    private static string ExtractSessionId(object sessionInfo)
    {
        if (sessionInfo == null)
            return string.Empty;

        string[] candidates =
        {
            "SessionId",
            "m_sessionId",
            "Id",
            "id"
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            string value = ReadMemberAsString(sessionInfo, candidates[i]);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return sessionInfo.ToString();
    }

    private static int GetCountSafe(object obj)
    {
        if (obj == null)
            return 0;

        if (obj is ICollection collection)
            return collection.Count;

        PropertyInfo countProp = obj.GetType().GetProperty("Count",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        if (countProp != null)
        {
            object value = countProp.GetValue(obj, null);
            if (value is int count)
                return count;
        }

        return 0;
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

    private void Log(string msg)
    {
        if (!verboseLog)
            return;

        Debug.Log($"[BattleNetDriver] {msg}");
    }

    private void LogError(string title, string detail = "")
    {
        Debug.LogError($"[BattleNetDriver] {title}" + (string.IsNullOrWhiteSpace(detail) ? "" : $" / {detail}"));
    }

    private void TryResolveSessionsFromGameRecords(System.Collections.IList gameRecords)
    {
        if (gameRecords == null || gameRecords.Count == 0)
            return;

        string myNick = BattleMatchSession.MyNickname;
        string opponentNick = BattleMatchSession.OpponentNickname;

        string mySession = BattleMatchSession.MySessionId;
        string oppSession = BattleMatchSession.OpponentSessionId;

        SessionId mySessionRaw = BattleMatchSession.MySession;
        SessionId oppSessionRaw = BattleMatchSession.OpponentSession;

        for (int i = 0; i < gameRecords.Count; i++)
        {
            object record = gameRecords[i];
            if (record == null)
                continue;

            string sessionId = ExtractSessionIdFromGameRecord(record);
            SessionId sessionRaw = ExtractSessionIdValueFromGameRecord(record);
            string nick = ExtractNicknameFromGameRecord(record);

            if (string.IsNullOrWhiteSpace(sessionId))
                continue;

            if (!string.IsNullOrWhiteSpace(myNick) && nick == myNick)
            {
                mySession = sessionId;
                mySessionRaw = sessionRaw;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(opponentNick) && nick == opponentNick)
            {
                oppSession = sessionId;
                oppSessionRaw = sessionRaw;
                continue;
            }
        }

        // 닉네임 기준으로 못 찾았으면 2인 방 fallback
        if (string.IsNullOrWhiteSpace(mySession) || string.IsNullOrWhiteSpace(oppSession))
        {
            for (int i = 0; i < gameRecords.Count; i++)
            {
                object record = gameRecords[i];
                if (record == null)
                    continue;

                string sessionId = ExtractSessionIdFromGameRecord(record);
                SessionId sessionRaw = ExtractSessionIdValueFromGameRecord(record);

                if (string.IsNullOrWhiteSpace(sessionId))
                    continue;

                if (string.IsNullOrWhiteSpace(mySession))
                {
                    mySession = sessionId;
                    mySessionRaw = sessionRaw;
                }
                else if (sessionId != mySession && string.IsNullOrWhiteSpace(oppSession))
                {
                    oppSession = sessionId;
                    oppSessionRaw = sessionRaw;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(mySession))
            BattleMatchSession.MySessionId = mySession;

        if (!string.IsNullOrWhiteSpace(oppSession))
            BattleMatchSession.OpponentSessionId = oppSession;

        BattleMatchSession.MySession = mySessionRaw;
        BattleMatchSession.OpponentSession = oppSessionRaw;

        RefreshHostAuthority();
    }

    private string ExtractSessionIdFromGameRecord(object record)
    {
        string[] directCandidates =
        {
        "SessionId",
        "m_sessionId",
        "sessionId",
        "id"
    };

        for (int i = 0; i < directCandidates.Length; i++)
        {
            string value = ReadMemberAsString(record, directCandidates[i]);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        object sessionInfo = ReadMember(record, "Session");
        if (sessionInfo == null)
            sessionInfo = ReadMember(record, "SessionInfo");

        if (sessionInfo != null)
            return ExtractSessionId(sessionInfo);

        return string.Empty;
    }

    private string ExtractNicknameFromGameRecord(object record)
    {
        string[] directCandidates =
        {
        "NickName",
        "Nickname",
        "nickName",
        "m_nickName",
        "gamerNickName"
    };

        for (int i = 0; i < directCandidates.Length; i++)
        {
            string value = ReadMemberAsString(record, directCandidates[i]);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        object sessionInfo = ReadMember(record, "Session");
        if (sessionInfo == null)
            sessionInfo = ReadMember(record, "SessionInfo");

        if (sessionInfo != null)
        {
            string[] sessionCandidates =
            {
            "NickName",
            "Nickname",
            "nickName",
            "m_nickName"
        };

            for (int i = 0; i < sessionCandidates.Length; i++)
            {
                string value = ReadMemberAsString(sessionInfo, sessionCandidates[i]);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        return string.Empty;
    }
    public void SendBoardSnapshot()
    {
        if (!_battleStarted)
        {
            LogError("BOARD_SNAPSHOT 전송 실패", "아직 배틀 시작 전");
            return;
        }

        if (!_joinedRoom)
        {
            LogError("BOARD_SNAPSHOT 전송 실패", "아직 게임방 입장 전");
            return;
        }

        if (battleManager == null)
        {
            LogError("BOARD_SNAPSHOT 전송 실패", "battleManager is null");
            return;
        }

        byte[] payload = battleManager.BuildBoardSnapshotPayload();
        if (payload == null || payload.Length == 0)
        {
            LogError("BOARD_SNAPSHOT 전송 실패", "payload is empty");
            return;
        }

        byte[] packet = BuildBoardSnapshotPacket(payload);
        if (SendPacket(packet))
        {
            Log($"[SEND] BOARD_SNAPSHOT / mySession={BattleMatchSession.MySessionId} / bytes={payload.Length}");
        }
    }
    private byte[] BuildBoardSnapshotPacket(byte[] payload)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write((byte)PacketOp.BoardSnapshot);
            bw.Write(payload);
            return ms.ToArray();
        }
    }
    public void SubmitMatchResultByScores(int myScore, int opponentScore)
    {
        if (_matchEndSent)
        {
            Log("MatchEnd already sent");
            return;
        }

        if (string.IsNullOrWhiteSpace(BattleMatchSession.MySessionId) ||
            string.IsNullOrWhiteSpace(BattleMatchSession.OpponentSessionId))
        {
            LogError("MatchEnd 실패",
                $"session missing / my={BattleMatchSession.MySessionId}, opp={BattleMatchSession.OpponentSessionId}");
            return;
        }

        try
        {
            MatchGameResult matchGameResult = new MatchGameResult();
            matchGameResult.m_winners = new List<SessionId>();
            matchGameResult.m_losers = new List<SessionId>();
            matchGameResult.m_draws = new List<SessionId>();

            if (myScore > opponentScore)
            {
                matchGameResult.m_winners.Add(BattleMatchSession.MySession);
                matchGameResult.m_losers.Add(BattleMatchSession.OpponentSession);

                Log($"[SEND] MATCH_END / WIN / my={myScore} / opp={opponentScore}");
            }
            else if (myScore < opponentScore)
            {
                matchGameResult.m_winners.Add(BattleMatchSession.OpponentSession);
                matchGameResult.m_losers.Add(BattleMatchSession.MySession);

                Log($"[SEND] MATCH_END / LOSE / my={myScore} / opp={opponentScore}");
            }
            else
            {
                matchGameResult.m_draws.Add(BattleMatchSession.MySession);
                matchGameResult.m_draws.Add(BattleMatchSession.OpponentSession);

                Log($"[SEND] MATCH_END / DRAW / my={myScore} / opp={opponentScore}");
            }

            Backend.Match.MatchEnd(matchGameResult);
            _matchEndSent = true;
        }
        catch (Exception e)
        {
            LogError("MatchEnd 예외", e.Message);
        }
    }

    private SessionId CreateSessionIdFromString(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("SessionId string is empty.");

        Type t = typeof(SessionId);

        // 1) string 생성자 우선 시도
        ConstructorInfo ctor = t.GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(string) },
            null);

        if (ctor != null)
            return (SessionId)ctor.Invoke(new object[] { raw });

        // 2) Parse(string) 정적 메서드 시도
        MethodInfo parse = t.GetMethod(
            "Parse",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null);

        if (parse != null)
            return (SessionId)parse.Invoke(null, new object[] { raw });

        throw new InvalidOperationException(
            "SessionId를 string에서 생성할 수 없습니다. BattleMatchSession에 SessionId 원본 타입을 저장하도록 바꿔야 합니다.");
    }
    private static SessionId ExtractSessionIdValue(object sessionInfo)
    {
        if (sessionInfo == null)
            return default;

        string[] candidates =
        {
        "SessionId",
        "m_sessionId",
        "Id",
        "id"
    };

        for (int i = 0; i < candidates.Length; i++)
        {
            object value = ReadMember(sessionInfo, candidates[i]);
            if (value is SessionId sid)
                return sid;
        }

        return default;
    }

    private static SessionId ExtractSessionIdValueFromGameRecord(object record)
    {
        if (record == null)
            return default;

        string[] directCandidates =
        {
        "SessionId",
        "m_sessionId",
        "sessionId",
        "id"
    };

        for (int i = 0; i < directCandidates.Length; i++)
        {
            object value = ReadMember(record, directCandidates[i]);
            if (value is SessionId sid)
                return sid;
        }

        object sessionInfo = ReadMember(record, "Session");
        if (sessionInfo == null)
            sessionInfo = ReadMember(record, "SessionInfo");

        if (sessionInfo != null)
            return ExtractSessionIdValue(sessionInfo);

        return default;
    }

    public bool SendItemUseAttack(BattleManager.BattleItemId itemId)
    {
        if (!_battleStarted)
        {
            LogError("ITEM_USE 전송 실패", "아직 배틀 시작 전");
            return false;
        }

        if (!_joinedRoom)
        {
            LogError("ITEM_USE 전송 실패", "아직 게임방 입장 전");
            return false;
        }

        byte[] packet = BuildItemUsePacket(itemId);

        if (!SendPacket(packet))
            return false;

        Log($"[SEND] ITEM_USE / mySession={BattleMatchSession.MySessionId} / item={itemId}");
        return true;
    }

    private byte[] BuildItemUsePacket(BattleManager.BattleItemId itemId)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write((byte)PacketOp.ItemUse);
            bw.Write((int)itemId);
            return ms.ToArray();
        }
    }

    public bool SendAttackIntent(BattleManager.BattleItemId itemId, bool isReserved)
    {
        if (!_battleStarted)
        {
            LogError("ATTACK_INTENT 전송 실패", "아직 배틀 시작 전");
            return false;
        }

        if (!_joinedRoom)
        {
            LogError("ATTACK_INTENT 전송 실패", "아직 게임방 입장 전");
            return false;
        }

        byte[] packet = BuildAttackIntentPacket(itemId, isReserved);

        if (!SendPacket(packet))
            return false;

        Log($"[SEND] ATTACK_INTENT / mySession={BattleMatchSession.MySessionId} / item={itemId} / reserved={isReserved}");
        return true;
    }

    private byte[] BuildAttackIntentPacket(BattleManager.BattleItemId itemId, bool isReserved)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write((byte)PacketOp.AttackIntent);
            bw.Write((int)itemId);
            bw.Write(isReserved);
            return ms.ToArray();
        }
    }

    public bool SendAttackOutcome(bool wasBlocked)
    {
        if (!_battleStarted)
        {
            LogError("ATTACK_OUTCOME 전송 실패", "아직 배틀 시작 전");
            return false;
        }

        if (!_joinedRoom)
        {
            LogError("ATTACK_OUTCOME 전송 실패", "아직 게임방 입장 전");
            return false;
        }

        byte[] packet = BuildAttackOutcomePacket(wasBlocked);

        if (!SendPacket(packet))
            return false;

        Log($"[SEND] ATTACK_OUTCOME / mySession={BattleMatchSession.MySessionId} / blocked={wasBlocked}");
        return true;
    }

    private byte[] BuildAttackOutcomePacket(bool wasBlocked)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write((byte)PacketOp.AttackOutcome);
            bw.Write(wasBlocked);
            return ms.ToArray();
        }
    }
}