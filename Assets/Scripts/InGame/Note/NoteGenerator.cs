using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using System.IO;
using System.Linq;

public class NoteGenerator : MonoBehaviour
{
    [SerializeField] private GameObject notePrefab;
    [SerializeField] private GameObject bellPrefab;
    [SerializeField] private GameObject longPrefab;
    [SerializeField] private GameObject rBellPrefab;
    [SerializeField] private GameObject avoidPrefab;
    [SerializeField] private GameObject leftArrowPrefab;
    [SerializeField] private GameObject rightArrowPrefab;

    public float BPM;

    public float distance;
    public float fallTime;
    public float speed;

    private int noteCount;

    public List<GameObject> Lines;

    public Dictionary<string, int> noteTypeCounts = new Dictionary<string, int>();

    private Vector3 spawnPosition1;
    private Vector3 spawnPosition2;
    private Vector3 spawnPosition3;
    private Vector3 spawnPosition4;

    Quaternion spawnRotation;

    public LoadManager loadManager;
    public LineInputChecker checker;
    public JudgementManager judgement;
    public MusicPlayer musicPlayer;
    public GameManager gameManager;
    private SettingsManager settings;
    private LevelEditer levelEditor;

    public List<NoteClass> notes;
    public SongInfoClass info;

    public List<int> randomRane;

    public GameObject notesFolder;
    public GameObject bellsFolder;

    public static NoteGenerator Instance { get; private set; }

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

        randomRane.Add(1);
        randomRane.Add(2);
        randomRane.Add(3);
        randomRane.Add(4);
    }


#if UNITY_STANDALONE || UNITY_EDITOR
    private void Start()
#else
    private async void Start()
#endif
    {
        speed = 12f;
        distance = 77f; //- (-2.63f);
        spawnPosition1 = new Vector3(Lines[0].transform.position.x, transform.position.y, 0);
        spawnPosition2 = new Vector3(Lines[1].transform.position.x, transform.position.y, 0);
        spawnPosition3 = new Vector3(Lines[2].transform.position.x, transform.position.y, 0);
        spawnPosition4 = new Vector3(Lines[3].transform.position.x, transform.position.y, 0);
        spawnRotation = Quaternion.Euler(90f, 0f, 0f);

        settings = SettingsManager.Instance;

        speed *= settings.settings.speed;

        fallTime = distance / speed * 1000f;

        noteTypeCounts["normal"] = 0;
        noteTypeCounts["hold"] = 0;
        noteTypeCounts["long"] = 0;
        noteTypeCounts["bell"] = 0;
        noteTypeCounts["rbell"] = 0;
        noteTypeCounts["avoid"] = 0;
        noteTypeCounts["leftarrow"] = 0;
        noteTypeCounts["rightarrow"] = 0;

        if (settings.settings.effectOption == "Random")
        {
            randomRane.Shuffle();
        }
        if (settings.settings.effectOption == "Half Random")
        {
            randomRane.ShuffleBySplit(2);
        }
        if (settings.settings.effectOption == "L. Quater Random")
        {
            randomRane.ShuffleBySplit(1);
        }
        if (settings.settings.effectOption == "R. Quater Random")
        {
            randomRane.ShuffleBySplit(3);
        }

#if UNITY_STANDALONE || UNITY_EDITOR
#else
        Debug.Log(settings.fileName);
        await loadManager.LoadFromJsonInWebGL(Path.Combine(settings.fileName));
#endif

        StartCoroutine(NoteGenerate());
    }

    private IEnumerator NoteGenerate()
    {
        yield return new WaitForSeconds(0.1f);

        notes = loadManager.notes;
        info = loadManager.info;
        if (!gameManager.isTest)
        {
            BPM = info.bpm;
        }
        else
        {
            levelEditor = LevelEditer.Instance;
            BPM = levelEditor.BPM;
        }

        noteCount = notes.Count;
        Debug.Log($"Count : {noteCount}");


        if (!gameManager.isTest)
        {
            StartCoroutine(NoteSpawnerSpawner());
        }
        else
        {
            StartCoroutine(TestNoteSpawnerSpawner());
        }



        foreach (NoteClass note in notes)
        {
            if (note.type != "null" && note.type != "")
            {
                noteTypeCounts[note.type]++;

                if (note.type == "bell" || note.type == "rbell" || note.type == "leftarrow" || note.type == "rightarrow" || note.type == "avoid")
                {
                    noteTypeCounts["hold"]++;
                }
            }
            note.isEndNote = false;
        }

        notes[noteCount - 1].isEndNote = true;
        Debug.Log($"isEndNote is {notes[noteCount - 1].beat}");

        judgement.CalcRate();

        yield break;
    }

    private IEnumerator NoteSpawnerSpawner()
    {
        foreach (NoteClass note in notes)
        {
            //Debug.Log(randomRane[note.position - 1]);
            // note.position = randomRane[note.position - 1];
            NoteSpawner(note, note.position, note.type, note.beat, spawnRotation);
            yield return new WaitForSeconds(0.03625f);
        }
        yield break;
    }

    private IEnumerator TestNoteSpawnerSpawner()
    {
        foreach (NoteClass note in notes)
        {
            NoteSpawner(note, note.position, note.type, note.beat, spawnRotation);
        }
        yield break;
    }


    public void NoteSpawner(NoteClass noteClass, float position, string type, float beat, Quaternion R)
    {
        Vector3 ranePosition = spawnPosition1;
        float oneBeatDuration;
        float beatDuration;
        GameObject note = null;
        GameObject longNote = null;
        if (position == 1f)
        {
            ranePosition = spawnPosition1;
        }
        if (position == 2f)
        {
            ranePosition = spawnPosition2;
        }
        if (position == 3f)
        {
            ranePosition = spawnPosition3;
        }
        if (position == 4f)
        {
            ranePosition = spawnPosition4;
        }

        oneBeatDuration = 60f / BPM * 1000f;
        beatDuration = oneBeatDuration * beat;

        if (type == "normal")
        {
            note = Instantiate(notePrefab, ranePosition, R, notesFolder.transform);
        }
        else if (type == "long")
        {
            note = Instantiate(notePrefab, ranePosition, R, notesFolder.transform);

            float oneBeatDistance = speed * (oneBeatDuration / 1000f);
            float longNoteLength = oneBeatDistance * noteClass.length;

            Vector3 longNotePosition = ranePosition;
            longNote = Instantiate(longPrefab, longNotePosition, R, notesFolder.transform);

            NoteClass longNoteClass = longNote.GetComponent<Note>().noteClass;

            longNote.transform.localScale = new Vector3(6f, 0f, longNoteLength);
            longNotePosition.z += longNoteLength / 2f;
            longNote.transform.position = longNotePosition;

            longNoteClass.type = "null";
            longNoteClass.noteObject = longNote;
            longNoteClass.position = noteClass.position;
            longNoteClass.beat = noteClass.beat;
            longNoteClass.length = noteClass.length;

            StartCoroutine(NoteSetter(longNoteClass, longNote, beatDuration));
        }
        else if (type == "hold" || type == "bell")
        {
            noteClass.type = "hold";
            float zer0Point = -10.5f;
            float gap = 7f;
            ranePosition = new Vector3(zer0Point + gap * (position - 1), spawnPosition1.y, 0);

            note = Instantiate(bellPrefab, ranePosition, R, bellsFolder.transform);
        }
        else if (type == "rbell")
        {
            float zer0Point = -10.5f;
            float gap = 7f;
            ranePosition = new Vector3(zer0Point + gap * (position - 1), spawnPosition1.y, 0);

            note = Instantiate(rBellPrefab, ranePosition, R, bellsFolder.transform);
        }
        else if (type == "avoid")
        {
            float zer0Point = -10.5f;
            float gap = 7f;
            ranePosition = new Vector3(zer0Point + gap * (position - 1), spawnPosition1.y, 0);

            note = Instantiate(avoidPrefab, ranePosition, R, bellsFolder.transform);
        }
        else if (type == "leftarrow")
        {
            float zer0Point = -10.5f;
            float gap = 7f;
            ranePosition = new Vector3(zer0Point + gap * (position - 1), spawnPosition1.y, 0);

            note = Instantiate(leftArrowPrefab, ranePosition, R, bellsFolder.transform);
        }
        else if (type == "rightarrow")
        {
            float zer0Point = -10.5f;
            float gap = 7f;
            ranePosition = new Vector3(zer0Point + gap * (position - 1), spawnPosition1.y, 0);

            note = Instantiate(rightArrowPrefab, ranePosition, R, bellsFolder.transform);
        }
        else
        {
            Debug.LogWarning($"Unknown note type: {type}");
            return;
        }

        if (noteClass.isEndNote == true)
        {
            note.GetComponent<Note>().isEndNote = true;
        }
        noteClass.id = $"{position}_{beat}";
        noteClass.noteObject = note;
        noteClass.longObject = longNote;
        //noteClass.noteObject.GetComponent<Note>().SetSpeed(speed);


        StartCoroutine(NoteSetter(noteClass, note, beatDuration));
    }

    IEnumerator NoteSetter(NoteClass noteClass, GameObject note, float beatDuration)
    {
        Note noteScript = note.GetComponent<Note>();

        float ms = beatDuration + 1000f;

        if (gameManager.isTest)
        {
            levelEditor = LevelEditer.Instance;
            ms -= levelEditor.currentMusicTime;
        }

        noteClass.ms = ms;

        noteScript.noteClass = noteClass;
        noteScript.ms = ms;
        noteScript.BPM = BPM;

        yield break;
    }
}
