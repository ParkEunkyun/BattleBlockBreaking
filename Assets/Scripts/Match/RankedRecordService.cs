using System;
using BackEnd;
using BackEnd.Tcp;
using LitJson;

public static class RankedRecordService
{
    public static void RefreshMyRankedRecord(Action<bool, string> onDone = null)
    {
        if (string.IsNullOrWhiteSpace(Backend.UserInDate))
        {
            onDone?.Invoke(false, "Backend.UserInDate is empty");
            return;
        }

        if (MatchManager.I == null)
        {
            onDone?.Invoke(false, "MatchManager.I is null");
            return;
        }

        if (!MatchManager.I.TryGetRankedRecordConfig(out MatchType matchType, out MatchModeType modeType, out string matchCardInDate))
        {
            onDone?.Invoke(false, "Ranked record config resolve failed");
            return;
        }

        Backend.Match.GetMatchRecord(
            Backend.UserInDate,
            matchType,
            modeType,
            matchCardInDate,
            callback =>
            {
                if (callback == null)
                {
                    onDone?.Invoke(false, "callback is null");
                    return;
                }

                if (!callback.IsSuccess())
                {
                    onDone?.Invoke(false, callback.ToString());
                    return;
                }

                JsonData rows = callback.FlattenRows();
                if (rows == null || rows.Count <= 0)
                {
                    RankedRecordCache.ApplyNoRecord();
                    onDone?.Invoke(true, "no rows");
                    return;
                }

                JsonData row = rows[0];

                int mmr = GetJsonInt(row, "mmr");
                int victory = GetJsonInt(row, "victory");
                int draw = GetJsonInt(row, "draw");
                int defeat = GetJsonInt(row, "defeat");

                string nickname = GetJsonString(row, "nickname");
                string updatedAt = GetJsonString(row, "updatedAt");

                RankedRecordCache.ApplyRecord(mmr, victory, draw, defeat, nickname, updatedAt);
                onDone?.Invoke(true, "ok");
            });
    }

    private static int GetJsonInt(JsonData row, string key)
    {
        try
        {
            string s = row[key].ToString();
            return int.TryParse(s, out int value) ? value : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string GetJsonString(JsonData row, string key)
    {
        try
        {
            return row[key].ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

}