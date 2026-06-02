using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{

    public bool isLevelEnd;
    public bool isSyncRoom;
    public bool isTest;

    [SerializeField] InGameAnimation _animator;

    public static GameManager Instance { get; private set; }

    private LevelEditer levelEditor;

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

    [System.Obsolete]
    private void Start()
    {
        isLevelEnd = false;
        isTest = SceneManager.GetSceneByName("LevelEditor").isLoaded;
        isSyncRoom = SceneManager.GetSceneByName("SyncRoom").isLoaded;

        StartCoroutine(WaitForLevelEnd());

        if (isTest)
        {
            EventSystem[] eventSystems = FindObjectsOfType<EventSystem>();
            if (eventSystems.Length > 1)
            {
                for (int i = 1; i < eventSystems.Length; i++)
                {
                    Destroy(eventSystems[i].gameObject);
                }
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5) && !isTest)
        {
            SceneManager.LoadSceneAsync("InGame");
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isSyncRoom)
            {
                SceneManager.LoadSceneAsync("Menu");
                return;
            }
            if (isTest)
            {
                levelEditor = LevelEditer.Instance;
                levelEditor.canvas.SetActive(true);
                foreach (NoteClass note in levelEditor.saveManager.notes)
                {
                    note.isInputed = false;
                }
                Scene editorScene = SceneManager.GetSceneByName("LevelEditor");
                if (editorScene.IsValid() && editorScene.isLoaded)
                {
                    SceneManager.SetActiveScene(editorScene);
                }
                SceneManager.UnloadSceneAsync("InGame");
                return;
            }
            // Exit mid-game → back to the Otoutz song-select with the previously chosen song.
            Otoutz.OtoutzFlow.OpenOnSelect = true;
            SceneManager.LoadSceneAsync("Menu");
        }
    }

    IEnumerator WaitForLevelEnd()
    {
        yield return new WaitUntil(() => isLevelEnd);
        isLevelEnd = false;
        StartCoroutine(ChangeToResult());
    }

    IEnumerator ChangeToResult()
    {
        yield return new WaitForSeconds(5f);

        if (isTest) yield break;

        string scene = isSyncRoom ? "SyncRoomResult" : "Result";

        // Fade to black, then load the result scene (which fades back in via its own OtoutzFade).
        var fade = Otoutz.OtoutzFade.Instance;
        if (fade != null) fade.FadeOutAndLoad(scene, 0.6f);
        else SceneManager.LoadSceneAsync(scene);
    }
}
