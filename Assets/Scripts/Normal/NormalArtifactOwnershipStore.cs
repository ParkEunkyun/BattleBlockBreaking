using System.Collections.Generic;
using UnityEngine;

public static class NormalArtifactOwnershipStore
{
    private const string OwnedIdsKey = "BBB_NORMAL_ARTIFACT_OWNED_IDS";
    private const string EquippedIdsKey = "BBB_NORMAL_ARTIFACT_EQUIPPED_IDS";

    private const char Separator = '|';
    private const int MaxEquipCount = 4;

    public static bool HasOwned(NormalArtifactDefinition def)
    {
        if (def == null || string.IsNullOrWhiteSpace(def.artifactId))
            return false;

        return HasOwned(def.artifactId);
    }

    public static bool HasOwned(string artifactId)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
            return false;

        HashSet<string> owned = LoadIdSet(OwnedIdsKey);
        return owned.Contains(artifactId);
    }

    public static void AddOwned(NormalArtifactDefinition def, int levelIfNew = 1)
    {
        if (def == null || string.IsNullOrWhiteSpace(def.artifactId))
            return;

        AddOwned(def.artifactId, levelIfNew);
    }

    public static void AddOwned(string artifactId, int levelIfNew = 1)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
            return;

        HashSet<string> owned = LoadIdSet(OwnedIdsKey);

        bool isNew = owned.Add(artifactId);

        if (isNew)
        {
            SaveIdSet(OwnedIdsKey, owned);
            NormalArtifactLevelUtility.SetLevel(artifactId, levelIfNew, false);
            PlayerPrefs.Save();
        }
    }

    public static void RemoveOwned(NormalArtifactDefinition def)
    {
        if (def == null || string.IsNullOrWhiteSpace(def.artifactId))
            return;

        RemoveOwned(def.artifactId);
    }

    public static void RemoveOwned(string artifactId)
    {
        if (string.IsNullOrWhiteSpace(artifactId))
            return;

        HashSet<string> owned = LoadIdSet(OwnedIdsKey);

        if (!owned.Remove(artifactId))
            return;

        SaveIdSet(OwnedIdsKey, owned);

        List<string> equipped = LoadIdList(EquippedIdsKey);
        equipped.RemoveAll(id => id == artifactId);
        SaveIdList(EquippedIdsKey, equipped);

        PlayerPrefs.Save();
    }

    public static List<NormalArtifactDefinition> GetOwnedDefinitions(IReadOnlyList<NormalArtifactDefinition> catalog)
    {
        List<NormalArtifactDefinition> result = new List<NormalArtifactDefinition>();

        if (catalog == null)
            return result;

        HashSet<string> owned = LoadIdSet(OwnedIdsKey);

        for (int i = 0; i < catalog.Count; i++)
        {
            NormalArtifactDefinition def = catalog[i];

            if (def == null || string.IsNullOrWhiteSpace(def.artifactId))
                continue;

            if (!owned.Contains(def.artifactId))
                continue;

            if (result.Contains(def))
                continue;

            result.Add(def);
        }

        return result;
    }

    public static List<NormalArtifactDefinition> LoadEquippedDefinitions(IReadOnlyList<NormalArtifactDefinition> catalog)
    {
        List<NormalArtifactDefinition> result = new List<NormalArtifactDefinition>();

        if (catalog == null)
            return result;

        List<string> ids = LoadIdList(EquippedIdsKey);

        for (int i = 0; i < ids.Count && result.Count < MaxEquipCount; i++)
        {
            string id = ids[i];

            if (string.IsNullOrWhiteSpace(id))
                continue;

            NormalArtifactDefinition def = FindById(catalog, id);

            if (def == null)
            {
                Debug.LogWarning($"[NormalArtifactOwnershipStore] 장착 ID를 Catalog에서 못 찾음: {id}");
                continue;
            }

            if (result.Contains(def))
                continue;

            result.Add(def);
        }

        Debug.Log($"[NormalArtifactOwnershipStore] LoadEquipped / Ids={ids.Count}, Result={result.Count}");

        return result;
    }

    public static void SaveEquippedDefinitions(
    IReadOnlyList<NormalArtifactDefinition> equipped,
    bool allowWrite = false)
    {
        // 중요:
        // 장착 저장은 Confirm 버튼에서만 허용한다.
        // LobbyManager, ApplyToSession, Refresh 계열에서 실수로 호출되면 기존 장착값을 덮어쓰므로 무시.
        if (!allowWrite)
        {
            Debug.LogWarning("[NormalArtifactOwnershipStore] SaveEquipped 차단 / Confirm 외부 저장 시도 무시");
            return;
        }

        List<string> ids = new List<string>();

        if (equipped != null)
        {
            for (int i = 0; i < equipped.Count && ids.Count < MaxEquipCount; i++)
            {
                NormalArtifactDefinition def = equipped[i];

                if (def == null || string.IsNullOrWhiteSpace(def.artifactId))
                    continue;

                if (ids.Contains(def.artifactId))
                    continue;

                ids.Add(def.artifactId);
            }
        }

        if (ids.Count <= 0)
        {
            Debug.LogWarning("[NormalArtifactOwnershipStore] SaveEquipped 무시 / 빈 장착 리스트. 전체 해제는 ClearEquippedDefinitions() 사용");
            return;
        }

        SaveIdList(EquippedIdsKey, ids);
        PlayerPrefs.Save();

        Debug.Log($"[NormalArtifactOwnershipStore] SaveEquipped / Saved={ids.Count}");
    }
    public static void ClearEquippedDefinitions()
    {
        PlayerPrefs.DeleteKey(EquippedIdsKey);
        PlayerPrefs.Save();

        Debug.Log("[NormalArtifactOwnershipStore] ClearEquipped / Saved=0");
    }
    public static void ClearAllOwnedAndEquipped()
    {
        PlayerPrefs.DeleteKey(OwnedIdsKey);
        PlayerPrefs.DeleteKey(EquippedIdsKey);
        PlayerPrefs.Save();
    }

    private static NormalArtifactDefinition FindById(IReadOnlyList<NormalArtifactDefinition> catalog, string artifactId)
    {
        if (catalog == null || string.IsNullOrWhiteSpace(artifactId))
            return null;

        for (int i = 0; i < catalog.Count; i++)
        {
            NormalArtifactDefinition def = catalog[i];

            if (def == null)
                continue;

            if (def.artifactId == artifactId)
                return def;
        }

        return null;
    }

    private static HashSet<string> LoadIdSet(string key)
    {
        HashSet<string> set = new HashSet<string>();
        string raw = PlayerPrefs.GetString(key, string.Empty);

        if (string.IsNullOrWhiteSpace(raw))
            return set;

        string[] parts = raw.Split(Separator);

        for (int i = 0; i < parts.Length; i++)
        {
            string id = parts[i];

            if (string.IsNullOrWhiteSpace(id))
                continue;

            set.Add(id);
        }

        return set;
    }

    private static List<string> LoadIdList(string key)
    {
        List<string> list = new List<string>();
        string raw = PlayerPrefs.GetString(key, string.Empty);

        if (string.IsNullOrWhiteSpace(raw))
            return list;

        string[] parts = raw.Split(Separator);

        for (int i = 0; i < parts.Length; i++)
        {
            string id = parts[i];

            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (list.Contains(id))
                continue;

            list.Add(id);
        }

        return list;
    }

    private static void SaveIdSet(string key, HashSet<string> ids)
    {
        List<string> list = new List<string>();

        if (ids != null)
        {
            foreach (string id in ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                list.Add(id);
            }
        }

        SaveIdList(key, list);
    }

    private static void SaveIdList(string key, List<string> ids)
    {
        if (ids == null || ids.Count <= 0)
        {
            PlayerPrefs.DeleteKey(key);
            return;
        }

        PlayerPrefs.SetString(key, string.Join(Separator.ToString(), ids));
    }
    public static bool HasAnyOwned()
    {
        HashSet<string> owned = LoadIdSet(OwnedIdsKey);
        return owned.Count > 0;
    }

    public static int GetOwnedCount()
    {
        HashSet<string> owned = LoadIdSet(OwnedIdsKey);
        return owned.Count;
    }

    public static void EnsureOwnedDefinitions(IReadOnlyList<NormalArtifactDefinition> defs, int levelIfNew = 1)
    {
        if (defs == null)
            return;

        HashSet<string> owned = LoadIdSet(OwnedIdsKey);
        bool changed = false;

        for (int i = 0; i < defs.Count; i++)
        {
            NormalArtifactDefinition def = defs[i];

            if (def == null || string.IsNullOrWhiteSpace(def.artifactId))
                continue;

            if (owned.Add(def.artifactId))
            {
                NormalArtifactLevelUtility.SetLevel(def.artifactId, levelIfNew, false);
                changed = true;
            }
        }

        if (changed)
        {
            SaveIdSet(OwnedIdsKey, owned);
            PlayerPrefs.Save();
        }
    }
}