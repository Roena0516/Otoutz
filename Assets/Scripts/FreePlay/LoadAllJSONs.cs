using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Networking;

public class SongList
{
    public SongInfoClass[] songs;
}

public class LoadAllJSONs : MonoBehaviour
{
    public List<SongInfoClass> songList = new List<SongInfoClass>();
    public Dictionary<string, List<SongInfoClass>> songDictionary = new Dictionary<string, List<SongInfoClass>>();

    public HorizontalSongListShower shower;

#if UNITY_STANDALONE || UNITY_EDITOR
    private void Start()
    {
        GetAll();
#else
    private async void Start()
    {
        await GetAllInWebGL();
#endif
        shower.Shower();
    }

    private void GetAll()
    {
        string[] songList = Directory.GetFiles(Application.streamingAssetsPath, "songList.json");
        string songListJson = File.ReadAllText(songList[0]);
        SongList songListContainer = JsonUtility.FromJson<SongList>(songListJson);

        foreach (var song in songListContainer.songs)
        {
            string[] directory = Directory.GetDirectories(Application.streamingAssetsPath, $"Charts/{song.fileLocation}");

            if (directory.Length == 0)
            {
                Debug.LogWarning($"missing directory: {song.fileLocation}");
                continue;
            }

            // 암호화 제거: .json 파일 직접 읽기
            string[] jsonFiles = Directory.GetFiles(directory[0], song.difficulty + ".json");

            if (jsonFiles.Length == 0)
            {
                Debug.LogWarning($"missing file: {directory[0]} {song.difficulty}");
                continue;
            }

            SongInfoClass info = new();

            info.fileLocation = jsonFiles[0];
            info.id = song.id;
            info.difficulty = song.difficulty;
            info.artist = song.artist;
            info.jpArtist = song.jpArtist;
            info.title = song.title;
            info.jpTitle = song.jpTitle;
            info.category = song.category;
            info.bpm = song.bpm;
            info.eventName = song.eventName;
            info.level = song.level;
            info.previewStart = song.previewStart;
            info.previewEnd = song.previewEnd;

            string key = info.artist + "-" + info.title;
            if (!songDictionary.ContainsKey(key))
            {
                songDictionary[key] = new List<SongInfoClass>();
                SongInfoClass temp = new();

                songDictionary[key].Add(temp);
                songDictionary[key].Add(temp);
                songDictionary[key].Add(temp);
                songDictionary[key].Add(temp);
            }

            if (song.difficulty == "ADVANCED")
            {
                songDictionary[key][0] = info;
            }
            if (song.difficulty == "EXPERT")
            {
                songDictionary[key][1] = info;
            }
            if (song.difficulty == "MASTER")
            {
                songDictionary[key][2] = info;
            }
            if (song.difficulty == "LUNATIC")
            {
                songDictionary[key][3] = info;
            }
        }
    }

    public async Task GetAllInWebGL()
    {
        string songListPath = $"{Application.streamingAssetsPath}/songList.json";

        using (UnityWebRequest songListReq = UnityWebRequest.Get(songListPath))
        {
            var op = songListReq.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (songListReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[WebGL] Failed to load songList.json: {songListReq.error}");
                return;
            }

            string songListJson = songListReq.downloadHandler.text;
            SongList songListContainer = JsonUtility.FromJson<SongList>(songListJson);

            if (songListContainer == null || songListContainer.songs == null)
            {
                Debug.LogError("[WebGL] songListContainer is null or empty.");
                return;
            }

            foreach (var song in songListContainer.songs)
            {
                // 암호화 제거: .json 파일 직접 읽기
                string songFolderUrl = $"{Application.streamingAssetsPath}/Charts/{song.fileLocation}/";
                string songFileUrl = $"{songFolderUrl}{song.difficulty}.json";

                using (UnityWebRequest req = UnityWebRequest.Get(songFileUrl))
                {
                    var subOp = req.SendWebRequest();
                    while (!subOp.isDone)
                        await Task.Yield();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"[WebGL] missing file: {songFileUrl}");
                        continue;
                    }

                    SongInfoClass info = new()
                    {
                        fileLocation = songFileUrl,
                        id = song.id,
                        difficulty = song.difficulty,
                        artist = song.artist,
                        jpArtist = song.jpArtist,
                        title = song.title,
                        jpTitle = song.jpTitle,
                        category = song.category,
                        bpm = song.bpm,
                        eventName = song.eventName,
                        level = song.level
                    };

                    string key = info.artist + "-" + info.title;
                    if (!songDictionary.ContainsKey(key))
                    {
                        songDictionary[key] = new List<SongInfoClass> { new(), new(), new(), new() };
                    }

                    switch (song.difficulty)
                    {
                        case "ADVANCED":
                            songDictionary[key][0] = info;
                            break;
                        case "EXPERT":
                            songDictionary[key][1] = info;
                            break;
                        case "MASTER":
                            songDictionary[key][2] = info;
                            break;
                        case "LUNATIC":
                            songDictionary[key][3] = info;
                            break;
                    }
                    Debug.Log($"[GetAllInWebGL] songFileUrl = {songFileUrl}");
                }
            }
        }
    }

    [System.Serializable]
    private class SongContainer
    {
        public SongInfoClass info;

    }
}
