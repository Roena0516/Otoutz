using FMODUnity;
using FMOD.Studio;
using UnityEngine;
using System.Collections;

public class MusicPlayer : MonoBehaviour
{
    EventInstance eventInstance;

    public float sync;

    public string eventName;

    private SettingsManager settings;
    private LevelEditer levelEditer;
    public GameManager gameManager;
    public LineInputChecker line;

    // Music is non-diegetic: it must not attenuate with the camera's distance. The MusicPlayer sits
    // at the origin but the FMOD listener is on the Main Camera (~21u away), so a 3D music event was
    // being distance-attenuated in-game while the menu preview (listener ~on the source) played full.
    // Anchoring playback to the listener keeps distance ~0, so both play at the intended volume.
    private GameObject _listenerGo;
    private GameObject Listener => (_listenerGo != null) ? _listenerGo
        : (_listenerGo = (Camera.main != null ? Camera.main.gameObject : gameObject));

    public static MusicPlayer Instance { get; private set; }

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

    void Start()
    {
        settings = SettingsManager.Instance;

        // Judge offset no longer shifts the music start; it's applied to each note's input time (ms)
        // in NoteGenerator instead. Music always starts at the fixed base delay.
        sync = 0.8f;
    }

    void OnEnable()
    {
        line.OnPlay.AddListener(OnPlayDetected);
    }

    void OnDisable()
    {
        line.OnPlay.RemoveListener(OnPlayDetected);
    }

    IEnumerator StartSong()
    {
        yield return new WaitForSeconds(sync);

        Debug.Log($"song is started, currentTime: {line.currentTime}");
        eventInstance.start();
    }

    void Update()
    {
        eventInstance.set3DAttributes(RuntimeUtils.To3DAttributes(Listener));
    }

    void OnDestroy()
    {
        eventInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        eventInstance.release();
    }

    void OnPlayDetected()
    {
        // Play�� ����Ǿ��� �� ����

        int timeLinePosition = 0;
        if (!gameManager.isTest)
        {
            eventName = settings.eventName;
        }
        else
        {
            levelEditer = LevelEditer.Instance;
            eventName = levelEditer.eventName;
            timeLinePosition = levelEditer.currentMusicTime;
        }
        eventInstance = RuntimeManager.CreateInstance($"event:/{eventName}");

        Debug.Log($"{eventName}, sync: {sync}, currentTime: {line.currentTime}");

        eventInstance.set3DAttributes(RuntimeUtils.To3DAttributes(Listener));

        eventInstance.setVolume(0.5f * (settings.settings.musicVolume / 10f));
        eventInstance.setTimelinePosition(timeLinePosition);

        StartCoroutine(StartSong());
    }
}
