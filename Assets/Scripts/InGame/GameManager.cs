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
            SceneManager.LoadSceneAsync("FreePlay");
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

        void LoadResult()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.LoadSceneAsync(isSyncRoom ? "SyncRoomResult" : "Result");
        }

        // _animator is a serialized ref that may be unassigned in the scene; fall back to a
        // scene lookup, and if there's still no fader just transition straight to the result.
        if (_animator == null) _animator = FindObjectOfType<InGameAnimation>();
        if (_animator != null) _animator.FadeIn(1f, LoadResult);
        else LoadResult();
    }
}
