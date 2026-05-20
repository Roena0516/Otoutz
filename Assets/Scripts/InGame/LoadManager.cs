using UnityEngine;
using System.IO;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using UnityEngine.Networking;

[System.Serializable]
public class SongInfoClass
{
    public int id;
    public string title;
    public string jpTitle;
    public string artist;
    public string jpArtist;
    public string category;
    public float bpm;
    public string eventName;
    public string fileLocation;
    public float level = 0;
    public string difficulty;
    public int previewStart = 0;
    public int previewEnd = 20000;
}

[System.Serializable]
public class NoteClass
{
    public string id;
    public float beat;
    public float ms;
    public float width = 1f;
    public float length = 4f;
    public float tick;
    public float position;
    public string type;
    public float angle = 0f;
    public float speed = 1f;

    public bool isInputed = false;
    public bool isEndNote = false;

    public bool isSyncRoom;

    public GameObject noteObject;
    public GameObject longObject;

    // 롱노트 전용 변수
    public float pressedTime = 0f;        // 누른 누적 시간 (ms)
    public bool isLongNotePressing = false; // 현재 누르고 있는지
    public float lastTickBeat = 0f;       // 마지막으로 표시한 tick 비트
    public string startJudgement = "";    // 시작 판정 (최대 판정)
    public bool longNoteStarted = false;  // 롱노트 시작 판정을 받았는지
}

public class LoadManager : MonoBehaviour
{
    public SongInfoClass info;
    public List<NoteClass> notes;

    private SettingsManager settings;
    private LevelEditer levelEditer;
    public GameManager gameManager;

    public static LoadManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

#if UNITY_STANDALONE || UNITY_EDITOR
    private void Start()
#else
    private async void Start()
#endif
    {
        settings = SettingsManager.Instance;
        levelEditer = LevelEditer.Instance;

        if (!SceneManager.GetSceneByName("LevelEditor").isLoaded)
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            LoadFromJson(settings.fileName);
#endif
        }
        else
        {
            // 깊은 복사: JsonUtility를 사용하여 새로운 객체 생성
            info = JsonUtility.FromJson<SongInfoClass>(JsonUtility.ToJson(levelEditer.saveManager.info));

            // notes도 깊은 복사
            notes = new List<NoteClass>();
            foreach (var note in levelEditer.saveManager.notes)
            {
                NoteClass copiedNote = JsonUtility.FromJson<NoteClass>(JsonUtility.ToJson(note));
                notes.Add(copiedNote);
            }

            settings.eventName = info.eventName;

            Debug.Log($"1{levelEditer.eventName}");

            Debug.Log("Chart loaded successfully!");
            Debug.Log($"{info.artist}");
        }
    }

    public void LoadFromJson(string filePath)
    {
        if (File.Exists(filePath))
        {
            // 암호화 제거: .json 파일 직접 읽기
            string json = File.ReadAllText(filePath);

            NotesContainer container = JsonUtility.FromJson<NotesContainer>(json);

            info = settings.Info;
            notes = container.notes;

            Debug.Log($"Chart loaded successfully! eventName: {settings.eventName}, id: {info.id}");
        }
        else
        {
            Debug.LogError("File not found at: " + filePath);
        }
    }

    public async Task LoadFromJsonInWebGL(string filePath)
    {
        string json = null;
        using (UnityWebRequest req = UnityWebRequest.Get(filePath))
        {
            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (req.result == UnityWebRequest.Result.ConnectionError ||
                req.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[WebGL] Failed to load chart file: {filePath}\nError: {req.error}");
                return;
            }

            json = req.downloadHandler.text;
        }

        try
        {
            // 암호화 제거: .json 파일 직접 읽기
            NotesContainer container = JsonUtility.FromJson<NotesContainer>(json);

            info = settings.Info;
            notes = container.notes;

            Debug.Log($"Chart loaded successfully! eventName: {settings.eventName}, id: {info.id}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"JSON Parse or Decrypt Error: {e.Message}");
        }
    }

    [System.Serializable]
    private class NotesContainer
    {
        public SongInfoClass info;
        public List<NoteClass> notes;
    }
}
