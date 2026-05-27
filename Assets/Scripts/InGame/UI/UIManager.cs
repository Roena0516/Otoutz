using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    // UI Components
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshPro _comboText;
    [SerializeField] private TextMeshPro _comboTitleText;
    [SerializeField] private TextMeshProUGUI _rateText;
    [SerializeField] private TextMeshProUGUI _judgeText;
    [SerializeField] private TextMeshProUGUI _plusJudgeText;
    [SerializeField] private TextMeshProUGUI _fastSlow;

    [Header("Judge Count Texts")]
    [SerializeField] private TextMeshPro _perfectpCountText;
    [SerializeField] private TextMeshPro _perfectCountText;
    [SerializeField] private TextMeshPro _greatCountText;
    [SerializeField] private TextMeshPro _goodCountText;
    [SerializeField] private TextMeshPro _missCountText;

    [Header("Song Info Texts")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _artistText;

    [Header("FCAP Text")]
    [SerializeField] private TextMeshProUGUI _FCAPText;
    [SerializeField] private TextMeshPro _speedText;
    [SerializeField] private TextMeshPro _scoreText;
    [SerializeField] private List<TextMeshProUGUI> _fastIndicators;
    [SerializeField] private List<TextMeshProUGUI> _slowIndicators;

    // Classes
    [SerializeField] private LoadManager _loadManager;
    private SettingsManager _settings;

    [Header("Judgement Prefabs")]
    [SerializeField] private Transform _leftJudgementFolder;
    [SerializeField] private Transform _rightJudgementFolder;
    [SerializeField] private GameObject _criticalBreakPrefab;
    [SerializeField] private GameObject _breakPrefab;
    [SerializeField] private GameObject _hitPrefab;
    [SerializeField] private GameObject _missPrefab;

    // Coroutines
    private Coroutine _comboPopInRoutine;
    private Coroutine _currentJudgementRoutine;
    private Coroutine _currentLeftIndicatorRoutine;
    private Coroutine _currentRightIndicatorRoutine;
    private Coroutine _popInRoutine;
    private Coroutine _leftJudgementRoutine;
    private Coroutine _rightJudgementRoutine;
    private GameObject _leftActiveJudgement;
    private GameObject _rightActiveJudgement;

    private void Start()
    {
        if (!_loadManager)
        {
            Debug.LogError("loadManager is not defined");
            return;
        }
        _settings = SettingsManager.Instance;
        if (!_settings)
        {
            Debug.LogError("Settings is not defined");
            return;
        }

        SetUIs();
    }

    private void SetUIs()
    {
        float level = _loadManager.info.level != 0 ? _loadManager.info.level : 0;

        Debug.Log($"level: {level} _loadManager.info.level: {_loadManager.info.level}");

        SetInitialJudgementText();
        SetLevelText(level);
        SetInitialSongInfoText();
        SetSpeedText();
    }

    private void SetInitialJudgementText()
    {
        _judgeText.color = _judgeText.color.SetAlpha(0f);
        _plusJudgeText.color = _plusJudgeText.color.SetAlpha(0f);
        _fastSlow.color = _fastSlow.color.SetAlpha(0f);
        _fastIndicators[0].color = _fastIndicators[0].color.SetAlpha(0f);
        _slowIndicators[0].color = _slowIndicators[0].color.SetAlpha(0f);
        _fastIndicators[1].color = _fastIndicators[1].color.SetAlpha(0f);
        _slowIndicators[1].color = _slowIndicators[1].color.SetAlpha(0f);
    }

    private void SetInitialSongInfoText()
    {
        _titleText.text = _settings.songTitle;
        _artistText.text = _settings.songArtist;
    }

    public void SetSpeedText()
    {
        _speedText.text = $"{_settings.settings.speed:F1}";
    }

    public void SetLevelText(float level)
    {
        _levelText.text = $"{level}";
    }

    public void SetCombo(int combo)
    {
        _comboText.text = $"{combo}";
        _comboTitleText.color = _comboTitleText.color.SetAlpha(1f);
    }

    public void ClearCombo()
    {
        _comboText.text = $"";
        _comboTitleText.color = _comboTitleText.color.SetAlpha(0f);
    }

    public void UpdateJudgeCountText(Dictionary<string, int> judgeCount)
    {
        _perfectpCountText.text = $"{judgeCount["CriticalBreak"]}";
        _perfectCountText.text = $"{judgeCount["Break"]}";
        _greatCountText.text = $"{judgeCount["Hit"]}";
        _goodCountText.text = "";
        _missCountText.text = $"{judgeCount["Miss"]}";
    }

    public void ChangeRate(float rate)
    {
        _rateText.text = $"{rate:F2}%";
    }

    public void SetScore(int score)
    {
        if (_scoreText != null)
            _scoreText.text = $"{score:N0}";
    }

    public void SetFCAPText(string FCAP)
    {
        _FCAPText.text = FCAP;
    }

    public void JudgementTextShower(string judgement, double Ms, float position, string noteType = "normal")
    {
        bool isLeft = position <= 2f;
        Transform folder = isLeft ? _leftJudgementFolder : _rightJudgementFolder;
        GameObject prefab = GetJudgementPrefab(judgement);

        if (prefab == null || folder == null) return;

        bool useNoteX = noteType != "normal" && noteType != "long" && noteType != "null";
        float noteXPos = useNoteX ? -10.5f + 7f * (position - 1f) : folder.position.x;

        if (useNoteX)
        {
            GameObject instance = Instantiate(prefab, folder);
            StartCoroutine(AnimateJudgementPrefab(instance, noteXPos));
        }
        else if (isLeft)
        {
            if (_leftJudgementRoutine != null) StopCoroutine(_leftJudgementRoutine);
            if (_leftActiveJudgement != null) Destroy(_leftActiveJudgement);
            _leftActiveJudgement = Instantiate(prefab, folder);
            _leftJudgementRoutine = StartCoroutine(AnimateJudgementPrefab(_leftActiveJudgement, noteXPos));
        }
        else
        {
            if (_rightJudgementRoutine != null) StopCoroutine(_rightJudgementRoutine);
            if (_rightActiveJudgement != null) Destroy(_rightActiveJudgement);
            _rightActiveJudgement = Instantiate(prefab, folder);
            _rightJudgementRoutine = StartCoroutine(AnimateJudgementPrefab(_rightActiveJudgement, noteXPos));
        }
    }

    private GameObject GetJudgementPrefab(string judgement)
    {
        return judgement switch
        {
            "CriticalBreak" => _criticalBreakPrefab,
            "Break" => _breakPrefab,
            "Hit" => _hitPrefab,
            "Miss" => _missPrefab,
            _ => null
        };
    }

    private IEnumerator AnimateJudgementPrefab(GameObject obj, float xPos)
    {
        Renderer r = obj.GetComponentInChildren<Renderer>();
        if (r == null) { Destroy(obj); yield break; }

        float zPos = obj.transform.position.z;
        Color baseColor = r.material.GetColor("_BaseColor");

        r.material.SetColor("_BaseColor", baseColor.SetAlpha(0f));
        obj.transform.position = new Vector3(xPos, 4f, zPos);

        float fadeInDuration = 0.15f;
        float holdDuration = 0.35f;
        float fadeOutDuration = 0.15f;

        // y=4 → y=5 페이드 인
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeInDuration);
            r.material.SetColor("_BaseColor", baseColor.SetAlpha(p));
            obj.transform.position = new Vector3(xPos, Mathf.Lerp(4f, 5f, p), zPos);
            yield return null;
        }
        r.material.SetColor("_BaseColor", baseColor.SetAlpha(1f));
        obj.transform.position = new Vector3(xPos, 5f, zPos);

        yield return new WaitForSeconds(holdDuration);

        // y=5 → y=6 페이드 아웃
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeOutDuration);
            r.material.SetColor("_BaseColor", baseColor.SetAlpha(1f - p));
            obj.transform.position = new Vector3(xPos, Mathf.Lerp(5f, 6f, p), zPos);
            yield return null;
        }

        Destroy(obj);
    }

    public IEnumerator ShowJudgementTextRoutine(string judgement, double Ms, float position)
    {
        Color tempColor = _judgeText.color;
        int index = Mathf.FloorToInt(position) - 1;
        tempColor.a = 1f;
        _judgeText.color = tempColor;

        // 텍스트 알파값 세팅
        if (judgement == "PerfectP")
        {
            _judgeText.text = "PERFECT";
            _plusJudgeText.color = _plusJudgeText.color.SetAlpha(1f);
        }
        else if (judgement == "Bad")
        {
            _judgeText.text = "MISS";
            _plusJudgeText.color = _plusJudgeText.color.SetAlpha(0f);
        }
        else
        {
            _judgeText.text = $"{judgement.ToUpper()}";
            _plusJudgeText.color = _plusJudgeText.color.SetAlpha(0f);
        }

        // 판정 텍스트 팝인
        if (_popInRoutine != null)
        {
            StopCoroutine(_popInRoutine);
        }
        _popInRoutine = StartCoroutine(PopIn(_judgeText.rectTransform));

        bool isIndicatorLeft = index <= 1;

        _fastSlow.color = _fastSlow.color.SetAlpha(1f);
        _fastSlow.text = string.Empty;

        // FAST / SLOW 처리
        if (Ms > 0)
        {
            if (_settings.settings.fastSlowExp != 0 && _settings.settings.fastSlowExp >= 1)
            {
                if (judgement == "Good" || judgement == "Miss")
                {
                    _fastSlow.color = _fastSlow.color.SetAlpha(1f);
                    _fastSlow.text = $"+{(int)Ms}";

                    if (isIndicatorLeft)
                    {
                        if (_currentLeftIndicatorRoutine != null)
                        {
                            StopCoroutine(_currentLeftIndicatorRoutine);
                        }
                        _currentLeftIndicatorRoutine = StartCoroutine(IndicatorShower(isIndicatorLeft, true));
                    }
                    else
                    {
                        if (_currentRightIndicatorRoutine != null)
                        {
                            StopCoroutine(_currentRightIndicatorRoutine);
                        }
                        _currentRightIndicatorRoutine = StartCoroutine(IndicatorShower(isIndicatorLeft, true));
                    }
                }
            }
            if (_settings.settings.fastSlowExp != 0 && _settings.settings.fastSlowExp >= 2)
            {
                if (judgement == "Great")
                {
                    _fastSlow.color = _fastSlow.color.SetAlpha(1f);
                    _fastSlow.text = $"+{(int)Ms}";

                    if (isIndicatorLeft)
                    {
                        if (_currentLeftIndicatorRoutine != null)
                        {
                            StopCoroutine(_currentLeftIndicatorRoutine);
                        }
                        _currentLeftIndicatorRoutine = StartCoroutine(IndicatorShower(isIndicatorLeft, true));
                    }
                    else
                    {
                        if (_currentRightIndicatorRoutine != null)
                        {
                            StopCoroutine(_currentRightIndicatorRoutine);
                        }
                        _currentRightIndicatorRoutine = StartCoroutine(IndicatorShower(isIndicatorLeft, true));
                    }
                }
            }
            if (_settings.settings.fastSlowExp != 0 && _settings.settings.fastSlowExp >= 3)
            {
                if (judgement == "Perfect")
                {
                    _fastSlow.color = _fastSlow.color.SetAlpha(1f);
                    _fastSlow.text = $"+{(int)Ms}";

                    if (isIndicatorLeft)
                    {
                        if (_currentLeftIndicatorRoutine != null)
                        {
                            StopCoroutine(_currentLeftIndicatorRoutine);
                        }
                        _currentLeftIndicatorRoutine = StartCoroutine(IndicatorShower(isIndicatorLeft, true));
                    }
                    else
                    {
                        if (_currentRightIndicatorRoutine != null)
                        {
                            StopCoroutine(_currentRightIndicatorRoutine);
                        }
                        _currentRightIndicatorRoutine = StartCoroutine(IndicatorShower(isIndicatorLeft, true));
                    }
                }
            }
        }
        else if (Ms == 0)
        {
            _fastSlow.color = _fastSlow.color.SetAlpha(1f);
            _fastSlow.text = string.Empty;
        }
        else // Ms < 0
        {
            if (_settings.settings.fastSlowExp != 0 && _settings.settings.fastSlowExp >= 1)
            {
                if (judgement == "Good" || judgement == "Miss")
                {
                    _fastSlow.color = _fastSlow.color.SetAlpha(1f);
                    _fastSlow.text = $"-{(int)Ms}";

                    if (isIndicatorLeft)
                    {
                        if (_currentLeftIndicatorRoutine != null)
                        {
                            StopCoroutine(_currentLeftIndicatorRoutine);
                        }
                        _currentLeftIndicatorRoutine = StartCoroutine(IndicatorShower(isIndicatorLeft, false));
                    }
                    else
                    {
                        if (_currentRightIndicatorRoutine != null)
                        {
                            StopCoroutine(_currentRightIndicatorRoutine);
                        }
                        _currentRightIndicatorRoutine = StartCoroutine(IndicatorShower(isIndicatorLeft, false));
                    }
                }
            }
            if (_settings.settings.fastSlowExp != 0 && _settings.settings.fastSlowExp >= 2)
            {
                if (judgement == "Great")
                {
                    _fastSlow.color = _fastSlow.color.SetAlpha(1f);
                    _fastSlow.text = $"-{(int)Ms}";

                    if (isIndicatorLeft)
                    {
                        if (_currentLeftIndicatorRoutine != null)
                        {
                            StopCoroutine(_currentLeftIndicatorRoutine);
                        }
                        _currentLeftIndicatorRoutine = StartCoroutine(IndicatorShower(isIndicatorLeft, false));
                    }
                    else
                    {
                        if (_currentRightIndicatorRoutine != null)
                        {
                            StopCoroutine(_currentRightIndicatorRoutine);
                        }
                        _currentRightIndicatorRoutine = StartCoroutine(IndicatorShower(isIndicatorLeft, false));
                    }
                }
            }
            if (_settings.settings.fastSlowExp != 0 && _settings.settings.fastSlowExp >= 3)
            {
                if (judgement == "Perfect")
                {
                    _fastSlow.color = _fastSlow.color.SetAlpha(1f);
                    _fastSlow.text = $"-{(int)Ms}";

                    if (isIndicatorLeft)
                    {
                        if (_currentLeftIndicatorRoutine != null)
                        {
                            StopCoroutine(_currentLeftIndicatorRoutine);
                        }
                        _currentLeftIndicatorRoutine = StartCoroutine(IndicatorShower(isIndicatorLeft, false));
                    }
                    else
                    {
                        if (_currentRightIndicatorRoutine != null)
                        {
                            StopCoroutine(_currentRightIndicatorRoutine);
                        }
                        _currentRightIndicatorRoutine = StartCoroutine(IndicatorShower(isIndicatorLeft, false));
                    }
                }
            }
        }

        // Perfect+
        //if (plusJudgeText.color.a > 0.99f)
        //    StartCoroutine(PopIn(plusJudgeText.rectTransform));

        // 2초 유지
        yield return new WaitForSeconds(2f);

        // 숨기기
        _judgeText.color = _judgeText.color.SetAlpha(0f);
        _plusJudgeText.color = _plusJudgeText.color.SetAlpha(0f);
        _fastSlow.color = _fastSlow.color.SetAlpha(0f);

        _currentJudgementRoutine = null;
    }

    public IEnumerator IndicatorShower(bool isIndicatorLeft, bool isFast)
    {
        int index = 0;
        if (!isIndicatorLeft)
        {
            index = 1;
        }

        if (isFast)
        {
            _fastIndicators[index].color = _fastIndicators[index].color.SetAlpha(0f);
            _slowIndicators[index].color = _slowIndicators[index].color.SetAlpha(0f);

            _fastIndicators[index].color = _fastIndicators[index].color.SetAlpha(1f);
            yield return new WaitForSeconds(1f);
            _fastIndicators[index].color = _fastIndicators[index].color.SetAlpha(0f);
        }
        else
        {
            _fastIndicators[index].color = _fastIndicators[index].color.SetAlpha(0f);
            _slowIndicators[index].color = _slowIndicators[index].color.SetAlpha(0f);

            _slowIndicators[index].color = _slowIndicators[index].color.SetAlpha(1f);
            yield return new WaitForSeconds(1f);
            _slowIndicators[index].color = _slowIndicators[index].color.SetAlpha(0f);
        }

        if (isIndicatorLeft)
        {
            _currentLeftIndicatorRoutine = null;
        }
        else
        {
            _currentRightIndicatorRoutine = null;
        }
    }

    private IEnumerator PopIn(RectTransform target, float fromScale = 0.3f, float toScale = 1f, float duration = 0.07f)
    {
        if (target == null) yield break;

        Vector3 start = Vector3.one * fromScale;
        Vector3 end = Vector3.one * toScale;

        float t = 0f;
        target.localScale = start;

        // EaseOutSine Easing
        float EaseOutSine(float t)
        {
            return Mathf.Sin((t * Mathf.PI) / 2f);
        }

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float e = EaseOutSine(p);
            target.localScale = Vector3.LerpUnclamped(start, end, e);
            yield return null;
        }

        target.localScale = end;
    }
}
