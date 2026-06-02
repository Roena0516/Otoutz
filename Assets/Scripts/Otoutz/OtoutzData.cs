using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Otoutz
{
    /// <summary>
    /// Loads songList.json + per-chart files and groups them into <see cref="OtoutzSong"/> rows.
    /// Mirrors the grouping logic in the legacy LoadAllJSONs so launch wiring stays compatible.
    /// </summary>
    public static class OtoutzData
    {
        public static List<OtoutzSong> Load()
        {
            string listPath = Path.Combine(Application.streamingAssetsPath, "songList.json");
            if (!File.Exists(listPath))
            {
                Debug.LogError("[OtoutzData] songList.json not found at " + listPath);
                return new List<OtoutzSong>();
            }
            var container = JsonUtility.FromJson<SongList>(File.ReadAllText(listPath));
            var dict = new Dictionary<string, SongInfoClass[]>();
            var order = new List<string>();

            foreach (var song in container.songs)
            {
                string folder = Path.Combine(Application.streamingAssetsPath, "Charts", song.fileLocation);
                string chart = Path.Combine(folder, song.difficulty + ".json");
                if (!File.Exists(chart)) { Debug.LogWarning("[OtoutzData] missing chart " + chart); continue; }

                var info = MakeInfo(song, chart);
                Insert(dict, order, info);
            }
            return Build(dict, order);
        }

        public static async Task<List<OtoutzSong>> LoadWebGL()
        {
            string listPath = $"{Application.streamingAssetsPath}/songList.json";
            SongList container = null;
            using (var req = UnityWebRequest.Get(listPath))
            {
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                if (req.result != UnityWebRequest.Result.Success) { Debug.LogError("[OtoutzData] " + req.error); return new List<OtoutzSong>(); }
                container = JsonUtility.FromJson<SongList>(req.downloadHandler.text);
            }
            var dict = new Dictionary<string, SongInfoClass[]>();
            var order = new List<string>();
            foreach (var song in container.songs)
            {
                string url = $"{Application.streamingAssetsPath}/Charts/{song.fileLocation}/{song.difficulty}.json";
                using (var req = UnityWebRequest.Get(url))
                {
                    var op = req.SendWebRequest();
                    while (!op.isDone) await Task.Yield();
                    if (req.result != UnityWebRequest.Result.Success) { Debug.LogWarning("[OtoutzData] missing " + url); continue; }
                    var info = MakeInfo(song, url);
                    Insert(dict, order, info);
                }
            }
            return Build(dict, order);
        }

        static SongInfoClass MakeInfo(SongInfoClass song, string filePath)
        {
            return new SongInfoClass
            {
                fileLocation = filePath,
                id = song.id,
                difficulty = song.difficulty,
                artist = song.artist,
                jpArtist = song.jpArtist,
                title = song.title,
                jpTitle = song.jpTitle,
                category = song.category,
                bpm = song.bpm,
                eventName = song.eventName,
                level = song.level,
                previewStart = song.previewStart,
                previewEnd = song.previewEnd,
            };
        }

        static void Insert(Dictionary<string, SongInfoClass[]> dict, List<string> order, SongInfoClass info)
        {
            string key = info.artist + "-" + info.title;
            if (!dict.ContainsKey(key)) { dict[key] = new SongInfoClass[4]; order.Add(key); }
            int idx = System.Array.IndexOf(OtoutzTheme.DiffKeys, info.difficulty);
            if (idx >= 0) dict[key][idx] = info;
        }

        static List<OtoutzSong> Build(Dictionary<string, SongInfoClass[]> dict, List<string> order)
        {
            var list = new List<OtoutzSong>();
            foreach (var key in order)
            {
                var slots = dict[key];
                var rep = slots.FirstOrDefault(s => s != null);
                if (rep == null) continue;

                var s = new OtoutzSong
                {
                    title = rep.title,
                    artist = rep.artist,
                    jpTitle = rep.jpTitle,
                    jpArtist = rep.jpArtist,
                    genre = string.IsNullOrEmpty(rep.category) ? "MUSIC" : rep.category.ToUpperInvariant(),
                    bpm = rep.bpm,
                    eventName = rep.eventName,
                    previewStart = rep.previewStart,
                    previewEnd = rep.previewEnd,
                };
                for (int i = 0; i < 4; i++)
                {
                    if (slots[i] != null)
                    {
                        s.infos[i] = slots[i];
                        s.levels[i] = slots[i].level;
                        s.filePaths[i] = slots[i].fileLocation;
                        s.ids[i] = slots[i].id;
                    }
                }
                MakeArt(s, key);
                list.Add(s);
            }
            return list;
        }

        static void MakeArt(OtoutzSong s, string seed)
        {
            int h = 0; foreach (char c in seed) h = h * 31 + c;
            float hue = Mathf.Abs(h % 360) / 360f;
            s.artA = Color.HSVToRGB(hue, 0.30f, 1.00f);
            s.artB = Color.HSVToRGB(Mathf.Repeat(hue + 0.12f, 1f), 0.42f, 0.96f);
            s.artBlob = Color.HSVToRGB(Mathf.Repeat(hue + 0.50f, 1f), 0.45f, 1.00f);
            s.glyph = string.IsNullOrWhiteSpace(s.title) ? "♪" : s.title.Trim().Substring(0, 1);
        }
    }
}
