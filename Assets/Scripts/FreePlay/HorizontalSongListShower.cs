using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using System.IO;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using FMODUnity;
using FMOD.Studio;

/// <summary>
/// 가로 무한 스크롤 곡 선택 UI
/// </summary>
public class HorizontalSongListShower : MonoBehaviour
{
    private SettingsManager settings;

    public LoadAllJSONs loader;
    private List<SongInfoClass> allOfInfos;
    private List<SongListInfoSetter> songObjects = new List<SongListInfoSetter>();

    public GameObject contentFolder;
    public GameObject songPrefab;
    public GameObject canvas;
    public GameObject difficultyIndicator;

    [Header("Horizontal List Settings")]
    public float cardSpacing = 600f; // 카드 간격
    public int visibleCards = 5; // 화면에 보이는 카드 수 (홀수 권장)

    // 재킷 이미지는 카드 안에만 표시하므로 제거
    // [SerializeField] private Image _jacketImage;

    public TextMeshProUGUI speedText;
    public TextMeshProUGUI bgnText;
    public TextMeshProUGUI midText;
    public TextMeshProUGUI endText;
    public TextMeshProUGUI sndText;

    // 곡 정보는 카드 안에만 표시하므로 제거
    // public TextMeshProUGUI info_titleText;
    // public TextMeshProUGUI info_artistText;
    // public TextMeshProUGUI info_bpmText;

    // 플레이 기록만 별도 표시 (레이팅 제거)
    public TextMeshProUGUI info_rateText;
    public TextMeshProUGUI info_comboText;
    // public TextMeshProUGUI info_ratingText;

    // Approach 관련 기능 제거
    // [SerializeField] private Image _approachJacketImage;
    // [SerializeField] private TextMeshProUGUI _approachSongTitle;
    // [SerializeField] private TextMeshProUGUI _approachSongArtist;
    // [SerializeField] private TextMeshProUGUI _approachLevel;

    // 드롭다운 제거
    // public TMP_Dropdown dropdown;

    private EventInstance _preview;

    private float originX;
    private float indicatorOriginX;
    private float originY;

    public int currentIndex = 0; // 현재 선택된 곡 인덱스 (무한 스크롤)
    public int selectedDifficulty;

    private bool isHold;

    private Coroutine currentSetSongRoutine;
    private Coroutine currentSetDifficultyRoutine;
    private Coroutine repeatCoroutine;
    private Coroutine _currentStopPreviewRoutine;
    private Coroutine _currentStartPreviewRoutine;

    public SongInfoClass selectedSongInfo;
    [SerializeField] private FreePlayAnimation _animator;

    private List<Result> results;

    // private string baseUrl = "https://prod.windeath44.wiki/api"; // API 기능 제거
    // private string accessToken; // API 기능 제거

    private void Start()
    {
        canvas.transform.localScale = Vector3.one;
        settings = SettingsManager.Instance;

        UIInit();
        isHold = false;

        FMODInit();
    }

    private void UIInit()
    {
        originX = contentFolder.transform.position.x;
        indicatorOriginX = difficultyIndicator.transform.position.x;
        originY = contentFolder.transform.position.y;

        currentIndex = 0;
        selectedDifficulty = 1;

        speedText.text = $"{settings.settings.speed:F1}";

        // 드롭다운 제거
        /*
        dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        Debug.Log(settings.settings.effectOption);
        if (settings.settings.effectOption == "None")
        {
            dropdown.value = 0;
        }
        if (settings.settings.effectOption == "Random")
        {
            dropdown.value = 1;
        }
        if (settings.settings.effectOption == "Half Random")
        {
            dropdown.value = 2;
        }
        if (settings.settings.effectOption == "L. Quater Random")
        {
            dropdown.value = 3;
        }
        if (settings.settings.effectOption == "R. Quater Random")
        {
            dropdown.value = 4;
        }
        Debug.Log($"value : {dropdown.value}");
        */

        bgnText.color = bgnText.color.SetAlpha(0f);
        midText.color = midText.color.SetAlpha(0f);
        endText.color = endText.color.SetAlpha(0f);
        sndText.color = sndText.color.SetAlpha(0f);
    }

    private void FMODInit()
    {
        _preview = RuntimeManager.CreateInstance("event:");
        _preview.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
        _preview.setVolume(0.5f);
    }

    public void Shower()
    {
        // 오프라인 모드: API 호출 제거, 빈 결과 리스트 초기화
        results = new();
        Debug.Log("[Shower] Offline mode: No API call, starting with empty results");

        foreach (var pair in loader.songDictionary)
        {
            string key = pair.Key;
            SongInfoClass info = null;

            if (loader.songDictionary.TryGetValue(key, out var songDifficulty))
            {
                info = songDifficulty
                    .Take(4)
                    .FirstOrDefault(s => s.level != 0);
            }

            if (selectedSongInfo.artist == "")
            {
                SetSelectedSongInfo(info);
            }

            Debug.Log($"{info.id} {info.artist} {info.title} {info.bpm}");

            // 노래 리스트에 노래 추가
            GameObject song = Instantiate(songPrefab, contentFolder.transform);
            Transform title = song.transform.Find("Title");
            Transform bottom = song.transform.Find("Bottom");
            Transform artist = bottom.Find("Artist");

            title.gameObject.GetComponent<TextMeshProUGUI>().text = $"{info.title}";
            artist.gameObject.GetComponent<TextMeshProUGUI>().text = $"{info.artist}";

            SongListInfoSetter setter = song.GetComponent<SongListInfoSetter>();

            // 빈 기록
            Result empty = new()
            {
                userId = "1",
                musicId = info.id,
                rate = 0,
                rating = 0,
                combo = 0,
                perfectPlus = 0,
                perfect = 0,
                great = 0,
                good = 0,
                miss = 0,
            };

            // 난이도 별 기록 리스트
            if (setter.results.Count == 0)
            {
                setter.results.Add(empty);
                setter.results.Add(empty);
                setter.results.Add(empty);
                setter.results.Add(empty);
            }

            // 난이도 별 채보 파일 경로 리스트
            if (setter.filePath.Count == 0)
            {
                setter.filePath.Add("");
                setter.filePath.Add("");
                setter.filePath.Add("");
                setter.filePath.Add("");
            }

            // 난이도 별 노래 id 리스트
            if (setter.ids.Count == 0)
            {
                setter.ids.Add(0);
                setter.ids.Add(0);
                setter.ids.Add(0);
                setter.ids.Add(0);
            }

            setter.artist = info.artist;
            setter.jpArtist = info.jpArtist;
            setter.title = info.title;
            setter.jpTitle = info.jpTitle;
            setter.BPM = info.bpm;
            setter.eventName = info.eventName;
            setter.previewStart = info.previewStart;
            setter.previewEnd = info.previewEnd;

            // 난이도 따른 파일 경로 및 기록 지정
            List<SongInfoClass> songList = loader.songDictionary[key];
            foreach (SongInfoClass infos in songList)
            {
                if (allOfInfos == null)
                {
                    allOfInfos = new();
                }
                allOfInfos.Add(infos);

                if (infos.difficulty == "ADVANCED")
                {
                    setter.filePath[0] = infos.fileLocation;

                    Result found = null;
                    if (results != null)
                    {
                        found = results.FirstOrDefault(r => r.musicId == infos.id);
                    }
                    if (found != null)
                    {
                        setter.results[0] = found;
                        Debug.Log($"[Shower] MEMORY: Matched musicId={infos.id}, rate={found.rate}%");
                    }
                    else
                    {
                        Debug.Log($"[Shower] MEMORY: No match for musicId={infos.id}");
                    }

                    setter.ids[0] = infos.id;
                }
                if (infos.difficulty == "EXPERT")
                {
                    setter.filePath[1] = infos.fileLocation;

                    Result found = null;
                    if (results != null)
                    {
                        found = results.FirstOrDefault(r => r.musicId == infos.id);
                    }
                    if (found != null)
                    {
                        setter.results[1] = found;
                    }

                    setter.ids[1] = infos.id;
                }
                if (infos.difficulty == "MASTER")
                {
                    setter.filePath[2] = infos.fileLocation;

                    Result found = null;
                    if (results != null)
                    {
                        found = results.FirstOrDefault(r => r.musicId == infos.id);
                    }
                    if (found != null)
                    {
                        setter.results[2] = found;
                    }

                    setter.ids[2] = infos.id;
                }
                if (infos.difficulty == "LUNATIC")
                {
                    setter.filePath[3] = infos.fileLocation;

                    Result found = null;
                    if (results != null)
                    {
                        found = results.FirstOrDefault(r => r.musicId == infos.id);
                    }
                    if (found != null)
                    {
                        setter.results[3] = found;
                    }

                    setter.ids[3] = infos.id;
                }
            }

            songObjects.Add(setter);
        }

        // 가로 배치 초기화
        InitializeHorizontalLayout();

        // 첫 곡 선택
        UpdateCurrentSong();

        _animator.FadeOut();
        _animator.ShowPanels();
    }

    /// <summary>
    /// 곡 카드들을 가로로 배치
    /// </summary>
    private void InitializeHorizontalLayout()
    {
        for (int i = 0; i < songObjects.Count; i++)
        {
            RectTransform rect = songObjects[i].GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(i * cardSpacing, 0);
        }
    }

    private void SetSelectedSongInfo(SongInfoClass info)
    {
        selectedSongInfo = info;
        DifficultySetter(info.artist + "-" + info.title);
    }

    public MainInputAction action;
    private InputAction listLeft;
    private InputAction listRight;
    private InputAction DifficultyUp;
    private InputAction DifficultyDown;
    private InputAction scrollList;
    private InputAction songSelect;
    private InputAction exitSongList;
    private InputAction speedUp;
    private InputAction speedDown;

    private void Awake()
    {
        action = new MainInputAction();
        listLeft = action.FreePlay.ListUp; // 좌측 이동
        listRight = action.FreePlay.ListDown; // 우측 이동
        DifficultyUp = action.FreePlay.DifficultyUp;
        DifficultyDown = action.FreePlay.DifficultyDown;
        scrollList = action.FreePlay.ScrollList;
        songSelect = action.FreePlay.SongSelect;
        exitSongList = action.FreePlay.ExitSongList;
        speedUp = action.FreePlay.SpeedUp;
        speedDown = action.FreePlay.SpeedDown;
    }

    [System.Obsolete]
    private void OnEnable()
    {
        listLeft.Enable();
        listLeft.started += Started;
        listLeft.canceled += Canceled;

        listRight.Enable();
        listRight.started += Started;
        listRight.canceled += Canceled;

        DifficultyUp.Enable();
        DifficultyUp.started += Started;
        DifficultyUp.canceled += Canceled;

        DifficultyDown.Enable();
        DifficultyDown.started += Started;
        DifficultyDown.canceled += Canceled;

        scrollList.Enable();
        scrollList.performed += OnScroll;

        songSelect.Enable();
        songSelect.started += Started;
        songSelect.canceled += Canceled;

        exitSongList.Enable();
        exitSongList.started += Started;
        exitSongList.canceled += Canceled;

        speedUp.Enable();
        speedUp.started += Started;
        speedUp.canceled += Canceled;

        speedDown.Enable();
        speedDown.started += Started;
        speedDown.canceled += Canceled;
    }

    [System.Obsolete]
    private void OnDisable()
    {
        listLeft.Disable();
        listLeft.started -= Started;
        listLeft.canceled -= Canceled;

        listRight.Disable();
        listRight.started -= Started;
        listRight.canceled -= Canceled;

        DifficultyUp.Disable();
        DifficultyUp.started -= Started;
        DifficultyUp.canceled -= Canceled;

        DifficultyDown.Disable();
        DifficultyDown.started -= Started;
        DifficultyDown.canceled -= Canceled;

        scrollList.Disable();
        scrollList.performed -= OnScroll;

        songSelect.Disable();
        songSelect.started -= Started;
        songSelect.canceled -= Canceled;

        exitSongList.Disable();
        exitSongList.started -= Started;
        exitSongList.canceled -= Canceled;

        speedUp.Disable();
        speedUp.started -= Started;
        speedUp.canceled -= Canceled;

        speedDown.Disable();
        speedDown.started -= Started;
        speedDown.canceled -= Canceled;
    }

    [System.Obsolete]
    private void OnScroll(InputAction.CallbackContext context)
    {
        Vector2 scrollDelta = context.ReadValue<Vector2>();

        if (scrollDelta.y > 0)
        {
            MoveSong(-1); // 왼쪽
        }
        if (scrollDelta.y < 0)
        {
            MoveSong(1); // 오른쪽
        }
    }

    [System.Obsolete]
    void Started(InputAction.CallbackContext context)
    {
        string actionName = context.action.name;

        if (!isHold)
        {
            isHold = true;

            switch (actionName)
            {
                case "ListUp": // 왼쪽
                    MoveSong(-1);
                    repeatCoroutine = StartCoroutine(RepeatKeyPress(actionName));
                    break;
                case "ListDown": // 오른쪽
                    MoveSong(1);
                    repeatCoroutine = StartCoroutine(RepeatKeyPress(actionName));
                    break;
                case "DifficultyUp":
                    SetDifficulty(selectedDifficulty + 1, 1);
                    repeatCoroutine = StartCoroutine(RepeatKeyPress(actionName));
                    break;
                case "DifficultyDown":
                    SetDifficulty(selectedDifficulty - 1, -1);
                    repeatCoroutine = StartCoroutine(RepeatKeyPress(actionName));
                    break;
                case "SongSelect":
                    SelectSong();
                    break;
                case "ExitSongList":
                    _animator.FadeIn(onComplete: () =>
                    {
                        _preview.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                        SceneManager.LoadScene("Menu");
                    });
                    break;
                case "SpeedUp":
                    SpeedOneUp();
                    repeatCoroutine = StartCoroutine(RepeatKeyPress(actionName));
                    break;
                case "SpeedDown":
                    SpeedOneDown();
                    repeatCoroutine = StartCoroutine(RepeatKeyPress(actionName));
                    break;
            }
        }
    }

    void Canceled(InputAction.CallbackContext context)
    {
        isHold = false;

        if (repeatCoroutine != null)
        {
            StopCoroutine(repeatCoroutine);
            repeatCoroutine = null;
        }
    }

    public void SpeedOneUp()
    {
        settings.SetSpeed($"{(settings.settings.speed + 0.1f):F1}");
        speedText.text = $"{settings.settings.speed:F1}";
        settings.SaveSettings();
    }
    public void SpeedOneDown()
    {
        settings.SetSpeed($"{(settings.settings.speed - 0.1f):F1}");
        speedText.text = $"{settings.settings.speed:F1}";
        settings.SaveSettings();
    }

    /// <summary>
    /// 곡 이동 (무한 스크롤)
    /// </summary>
    /// <param name="direction">-1: 왼쪽, 1: 오른쪽</param>
    private void MoveSong(int direction)
    {
        if (songObjects.Count == 0) return;

        // 무한 스크롤: 양 끝에서 반대편으로 순환
        currentIndex += direction;
        if (currentIndex < 0)
        {
            currentIndex = songObjects.Count - 1;
        }
        else if (currentIndex >= songObjects.Count)
        {
            currentIndex = 0;
        }

        if (currentSetSongRoutine != null)
        {
            StopCoroutine(currentSetSongRoutine);
        }
        currentSetSongRoutine = StartCoroutine(AnimateToCurrentSong());

        UpdateCurrentSong();

        // SFXLoader.Instance.PlaySFX("list_scroll.ogg"); // SFXLoader 클래스가 존재하지 않아 주석 처리
    }

    /// <summary>
    /// 현재 선택된 곡으로 애니메이션
    /// </summary>
    private IEnumerator AnimateToCurrentSong()
    {
        Transform T = contentFolder.transform;

        float elapsedTime = 0f;
        Vector3 startPos = T.localPosition;
        float duration = 0.25f;
        Vector3 targetPos = new Vector3(-currentIndex * cardSpacing, 0, 0);

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            float easedT = Mathf.Sin(t * Mathf.PI * 0.5f); // EaseOutSine

            T.localPosition = Vector3.Lerp(startPos, targetPos, easedT);

            yield return null;
        }

        T.localPosition = targetPos;
        currentSetSongRoutine = null;

        yield break;
    }

    /// <summary>
    /// 현재 곡 정보 업데이트
    /// </summary>
    private void UpdateCurrentSong()
    {
        if (songObjects.Count == 0) return;

        SongListInfoSetter setter = songObjects[currentIndex];

        DifficultySetter(setter.artist + "-" + setter.title);
        // 곡 정보는 카드 안에만 표시하므로 제거
        // SetInfoBoard(setter);
        SetDifficulty(selectedDifficulty, 1);

        if (_currentStartPreviewRoutine != null)
        {
            StopCoroutine(_currentStartPreviewRoutine);
            _preview.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _currentStartPreviewRoutine = null;
        }
        _currentStartPreviewRoutine = StartCoroutine(SetPreview($"event:/{setter.eventName}", setter.previewStart, setter.previewEnd));

        Result found = GetResult(setter.ids[selectedDifficulty - 1]);
        if (found != null)
        {
            SetResult(found);
        }

        // 카드 크기 및 투명도 조절
        UpdateCardAppearance();
    }

    /// <summary>
    /// 선택된 카드 강조
    /// </summary>
    private void UpdateCardAppearance()
    {
        for (int i = 0; i < songObjects.Count; i++)
        {
            Image img = songObjects[i].GetComponent<Image>();
            RectTransform rect = songObjects[i].GetComponent<RectTransform>();

            if (i == currentIndex)
            {
                // 선택된 카드: 크고 불투명
                img.color = img.color.SetAlpha(1f);
                rect.localScale = Vector3.one * 1.1f;
            }
            else
            {
                // 선택되지 않은 카드: 작고 반투명
                img.color = img.color.SetAlpha(0.5f);
                rect.localScale = Vector3.one * 0.9f;
            }
        }
    }

    // id로 Result 가져오기 (오프라인 모드: 항상 빈 결과 반환)
    private Result GetResult(int musicId)
    {
        // 오프라인 모드: 빈 결과 반환
        Result empty = new()
        {
            userId = "local",
            musicId = musicId,
            rate = 0,
            rating = 0,
            combo = 0,
            perfectPlus = 0,
            perfect = 0,
            great = 0,
            good = 0,
            miss = 0,
        };

        return empty;
    }

    // Result UI 변경 (레이팅 제거)
    private void SetResult(Result result)
    {
        info_rateText.text = $"{result.rate:F2}%";
        info_comboText.text = $"{result.combo}";
    }

    private IEnumerator SetPreview(string eventName, int startTime, int endTime)
    {
        if (_currentStopPreviewRoutine != null)
        {
            StopCoroutine(_currentStopPreviewRoutine);
            _preview.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _currentStopPreviewRoutine = null;
        }

        yield return new WaitForSeconds(0.5f);

        _preview.release();
        _preview = RuntimeManager.CreateInstance(eventName);
        _preview.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
        _preview.setVolume(0.5f * (settings.settings.musicVolume / 10f));
        _preview.setTimelinePosition(startTime);
        _preview.start();

        Debug.Log($"Preview started: {eventName}, from {startTime}ms to {endTime}ms");

        _currentStopPreviewRoutine = StartCoroutine(StopPreview(endTime - startTime));

        _currentStartPreviewRoutine = null;

        yield break;
    }

    private IEnumerator StopPreview(int duration)
    {
        yield return new WaitForSeconds(duration / 1000f);

        _preview.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        _currentStopPreviewRoutine = null;

        yield break;
    }

    // 곡 정보는 카드 안에만 표시하므로 제거
    /*
    private void SetInfoBoard(SongListInfoSetter setter)
    {
        info_titleText.text = $"{setter.title}";
        info_artistText.text = $"{setter.artist}";
        info_bpmText.text = $"{setter.BPM} BPM";
    }
    */

    private void DifficultySetter(string key)
    {
        List<SongInfoClass> songList = loader.songDictionary[key];

        bgnText.color = bgnText.color.SetAlpha(0f);
        bgnText.text = $"0";
        midText.color = midText.color.SetAlpha(0f);
        midText.text = $"0";
        endText.color = endText.color.SetAlpha(0f);
        endText.text = $"0";
        sndText.color = sndText.color.SetAlpha(0f);
        sndText.text = $"0";

        foreach (SongInfoClass infos in songList)
        {
            if (infos.difficulty == "ADVANCED")
            {
                bgnText.color = bgnText.color.SetAlpha(1f);
                bgnText.text = $"{infos.level}";
            }
            if (infos.difficulty == "EXPERT")
            {
                midText.color = midText.color.SetAlpha(1f);
                midText.text = $"{infos.level}";
            }
            if (infos.difficulty == "MASTER")
            {
                endText.color = endText.color.SetAlpha(1f);
                endText.text = $"{infos.level}";
            }
            if (infos.difficulty == "LUNATIC")
            {
                sndText.color = sndText.color.SetAlpha(1f);
                sndText.text = $"{infos.level}";
            }
            if (infos.difficulty == null)
            {
                sndText.color = sndText.color.SetAlpha(0f);
                sndText.text = $"{infos.level}";
            }
        }
    }

    private void SetDifficulty(int toIndex, int index)
    {
        if (toIndex > 0 && toIndex <= 4)
        {
            if (toIndex == 1)
            {
                if (bgnText.color.a == 0)
                {
                    SetDifficulty(toIndex + index, index);
                    return;
                }
            }
            if (toIndex == 2)
            {
                if (midText.color.a == 0)
                {
                    SetDifficulty(toIndex + index, index);
                    return;
                }
            }
            if (toIndex == 3)
            {
                if (endText.color.a == 0)
                {
                    SetDifficulty(toIndex + index, index);
                    return;
                }
            }
            if (toIndex == 4)
            {
                if (sndText.color.a == 0)
                {
                    return;
                }
            }
            selectedDifficulty = toIndex;

            SongListInfoSetter setter = songObjects[currentIndex];
            Result found = GetResult(setter.ids[selectedDifficulty - 1]);
            if (found != null)
            {
                SetResult(found);
            }

            SongInfoClass foundInfoClass = allOfInfos.FirstOrDefault(info => info.id == setter.ids[selectedDifficulty - 1]);
            if (foundInfoClass == null)
            {
                Debug.LogError($"info is not found: {setter.ids[selectedDifficulty - 1]} id");
            }
            SetSelectedSongInfo(foundInfoClass);
            // 재킷 이미지는 카드 안에만 표시하므로 제거
            // SetJacketImage($"Images/Jackets/{foundInfoClass.eventName}/{foundInfoClass.difficulty}");

            if (currentSetDifficultyRoutine != null)
            {
                StopCoroutine(currentSetDifficultyRoutine);
            }
            currentSetDifficultyRoutine = StartCoroutine(SetDifficultyIndicator(toIndex - 1));
        }
    }

    // 재킷 이미지는 카드 안에만 표시하므로 제거
    /*
    private void SetJacketImage(string path)
    {
        _jacketImage.sprite = Resources.Load<Sprite>(path);
    }
    */

    private IEnumerator SetDifficultyIndicator(int index)
    {
        canvas.transform.localScale = Vector3.one;

        Transform T = difficultyIndicator.transform;

        float elapsedTime = 0f;
        Vector3 startPos = new(T.position.x, T.position.y, 0f);
        float duration = 0.15f;
        Vector3 targetPos = new(indicatorOriginX + 109.75f * index, T.position.y, 0f);

        while (elapsedTime < duration)
        {
            canvas.transform.localScale = Vector3.one;

            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            float easedT = Mathf.Sin(t * Mathf.PI * 0.5f);

            T.position = Vector3.Lerp(startPos, targetPos, easedT);

            yield return null;
        }

        canvas.transform.localScale = Vector3.one;
        T.position = targetPos;

        currentSetDifficultyRoutine = null;

        yield break;
    }

    [System.Obsolete]
    private IEnumerator RepeatKeyPress(string actionName)
    {
        yield return new WaitForSeconds(0.3f);

        while (isHold)
        {
            switch (actionName)
            {
                case "ListUp": // 왼쪽
                    MoveSong(-1);
                    break;
                case "ListDown": // 오른쪽
                    MoveSong(1);
                    break;
                case "DifficultyUp":
                    SetDifficulty(selectedDifficulty + 1, 1);
                    break;
                case "DifficultyDown":
                    SetDifficulty(selectedDifficulty - 1, -1);
                    break;
                case "SpeedUp":
                    SpeedOneUp();
                    break;
                case "SpeedDown":
                    SpeedOneDown();
                    break;

            }
            yield return new WaitForSeconds(0.05f);
        }
    }

    public void SelectSong()
    {
        SongListInfoSetter setter = songObjects[currentIndex];

        settings.SetFileName($"{setter.filePath[selectedDifficulty - 1]}");
        settings.SetSongTitle(setter.title);
        settings.SetSongArtist(setter.artist);
        settings.SetEventName(setter.eventName);
        settings.Info = selectedSongInfo;

        Debug.Log($"[SelectSong] Selected: {settings.fileName}");

        // Approach 애니메이션 제거: 바로 씬 전환
        _preview.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        SceneManager.LoadSceneAsync("InGame");
    }

    public void SetSpeed(string inputed)
    {
        settings.SetSpeed(inputed);
    }

    // 드롭다운 제거
    /*
    private void OnDropdownValueChanged(int index)
    {
        string selectedOption = dropdown.options[index].text;
        DropdownHandler(selectedOption);
    }

    public void DropdownHandler(string option)
    {
        settings.settings.effectOption = option;
    }
    */
}
