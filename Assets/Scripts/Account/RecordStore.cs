using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// ---- persisted data models ----

[Serializable]
public class UserEntry
{
    public string uid;
    public string name;
    public string createdAt;   // ISO-8601 UTC
}

[Serializable]
public class PlayRecord
{
    public string recordId;
    public string uid;
    public string userName;
    public int songId;
    public string songName;
    public int difficulty;     // 0..3 (ADVANCED..LUNATIC)
    public int score;
    public float rate;         // accuracy %
    public string rank;        // grade text (e.g. "SSS+")
    public bool fc;            // full combo
    public bool ap;            // all break
    public string playedAt;    // ISO-8601 UTC
}

// one row of a song+difficulty leaderboard (each user's best, sorted by score)
public class RankEntry
{
    public int position;
    public string uid;
    public string userName;
    public int score;
    public string rank;
    public bool fc;
    public bool ap;
}

/// <summary>
/// Local-only persistence for RFID users and play records (JSON files under persistentDataPath).
/// Every play is appended (history); rankings/best are derived on read. No external server.
/// </summary>
public static class RecordStore
{
    [Serializable] class UserDb { public List<UserEntry> users = new List<UserEntry>(); }
    [Serializable] class RecordDb { public List<PlayRecord> records = new List<PlayRecord>(); }

    static UserDb _users;
    static RecordDb _records;

    static string UsersPath => Path.Combine(Application.persistentDataPath, "users.json");
    static string RecordsPath => Path.Combine(Application.persistentDataPath, "records.json");

    static void EnsureLoaded()
    {
        if (_users == null) _users = LoadOrNew<UserDb>(UsersPath);
        if (_records == null) _records = LoadOrNew<RecordDb>(RecordsPath);
    }

    static T LoadOrNew<T>(string path) where T : new()
    {
        try
        {
            if (File.Exists(path))
            {
                var o = JsonUtility.FromJson<T>(File.ReadAllText(path));
                if (o != null) return o;
            }
        }
        catch (Exception e) { Debug.LogWarning($"[RecordStore] load failed {path}: {e.Message}"); }
        return new T();
    }

    static void SaveUsers()
    {
        try { File.WriteAllText(UsersPath, JsonUtility.ToJson(_users)); }
        catch (Exception e) { Debug.LogWarning($"[RecordStore] save users failed: {e.Message}"); }
    }

    static void SaveRecords()
    {
        try { File.WriteAllText(RecordsPath, JsonUtility.ToJson(_records)); }
        catch (Exception e) { Debug.LogWarning($"[RecordStore] save records failed: {e.Message}"); }
    }

    // ---- users ----

    public static UserEntry GetUser(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return null;
        EnsureLoaded();
        return _users.users.FirstOrDefault(u => u.uid == uid);
    }

    /// <summary>Create (or rename) the user for a card UID and persist.</summary>
    public static UserEntry CreateUser(string uid, string name)
    {
        if (string.IsNullOrEmpty(uid)) return null;
        EnsureLoaded();
        var existing = _users.users.FirstOrDefault(u => u.uid == uid);
        if (existing != null) { existing.name = name; SaveUsers(); return existing; }
        var u = new UserEntry { uid = uid, name = name, createdAt = DateTime.UtcNow.ToString("o") };
        _users.users.Add(u);
        SaveUsers();
        return u;
    }

    // ---- records ----

    public static void AddRecord(PlayRecord r)
    {
        if (r == null) return;
        EnsureLoaded();
        if (string.IsNullOrEmpty(r.recordId)) r.recordId = Guid.NewGuid().ToString("N");
        if (string.IsNullOrEmpty(r.playedAt)) r.playedAt = DateTime.UtcNow.ToString("o");
        _records.records.Add(r);
        SaveRecords();
    }

    /// <summary>A user's best record on a song+difficulty (null if none).</summary>
    public static PlayRecord GetBestRecord(string uid, int songId, int difficulty)
    {
        if (string.IsNullOrEmpty(uid)) return null;
        EnsureLoaded();
        return _records.records
            .Where(x => x.uid == uid && x.songId == songId && x.difficulty == difficulty)
            .OrderByDescending(x => x.score)
            .FirstOrDefault();
    }

    /// <summary>Top-N leaderboard for a song+difficulty: each user's best score, descending.</summary>
    public static List<RankEntry> GetRanking(int songId, int difficulty, int topN)
    {
        EnsureLoaded();
        var best = _records.records
            .Where(x => x.songId == songId && x.difficulty == difficulty)
            .GroupBy(x => x.uid)
            .Select(g => g.OrderByDescending(x => x.score).First())
            .OrderByDescending(x => x.score)
            .Take(Mathf.Max(0, topN))
            .ToList();

        var list = new List<RankEntry>(best.Count);
        for (int i = 0; i < best.Count; i++)
        {
            var b = best[i];
            list.Add(new RankEntry { position = i + 1, uid = b.uid, userName = b.userName, score = b.score, rank = b.rank, fc = b.fc, ap = b.ap });
        }
        return list;
    }

    /// <summary>The user's own position in a song+difficulty leaderboard (null if they have no record).</summary>
    public static RankEntry GetUserRank(int songId, int difficulty, string uid)
    {
        if (string.IsNullOrEmpty(uid)) return null;
        EnsureLoaded();
        var best = _records.records
            .Where(x => x.songId == songId && x.difficulty == difficulty)
            .GroupBy(x => x.uid)
            .Select(g => g.OrderByDescending(x => x.score).First())
            .OrderByDescending(x => x.score)
            .ToList();
        for (int i = 0; i < best.Count; i++)
            if (best[i].uid == uid)
            {
                var b = best[i];
                return new RankEntry { position = i + 1, uid = b.uid, userName = b.userName, score = b.score, rank = b.rank, fc = b.fc, ap = b.ap };
            }
        return null;
    }
}
