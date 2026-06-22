using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class LineInputChecker : MonoBehaviour
{
    public double currentTimeMs;
    public double startTime;
    public double currentTime;
    public List<GameObject> Lines;

    public JudgementManager judgementManager;
    public NoteGenerator noteGenerator;
    public GameManager gameManager;
    [SerializeField] private UIManager UIManager;
    private SettingsManager settings;
    private InputThreadDivider divider;

    public MainInputAction action;
    private List<InputAction> LineActions;
    private InputAction speedUp;
    private InputAction speedDown;

    public List<bool> isHolding;

    public List<GameObject> buttons;

    private bool isSpeedHold;
    public bool isAutoPlay;

    private Coroutine repeatCoroutine;

    public List<float> originX;
    public float originY;

    [Header("Key Beam")]
    [SerializeField] private Color beamColor = new Color(0.55f, 0.8f, 1f, 1f);
    [SerializeField] private float beamAlpha = 0.65f;
    [SerializeField] private float beamFadeIn = 0.04f;
    [SerializeField] private float beamFadeOut = 0.18f;
    private Material[] beamMaterials;
    private Coroutine[] beamRoutines;

    public List<Coroutine> currentDownButtonRoutines;
    public List<Coroutine> currentUpButtonRoutines;

    private bool isEnd = false;

    public static LineInputChecker Instance { get; private set; }

    public Thread chartPlayThread;

    private readonly Queue<Action> mainThreadQueue = new Queue<Action>();
    private readonly object queueLock = new object();

    public UnityEvent OnPlay = new UnityEvent();

    private void Awake()
    {
        action = new MainInputAction();
        speedUp = action.Player.SpeedUp;
        speedDown = action.Player.SpeedDown;

        settings = SettingsManager.Instance;

        LineActions = settings.LineActions.ToList();

        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
#if UNITY_STANDALONE_OSX || UNITY_WEBGL
        for (int i = 0; i < 4; i++)
        {
            LineActions[i].Enable();
            LineActions[i].started += Started;
            LineActions[i].performed += Performed;
            LineActions[i].canceled += Canceled;
        }
#endif

        speedUp.Enable();
        speedUp.started += Started;
        speedUp.canceled += Canceled;

        speedDown.Enable();
        speedDown.started += Started;
        speedDown.canceled += Canceled;
    }

    private void OnDisable()
    {
#if UNITY_STANDALONE_OSX || UNITY_WEBGL
        for (int i = 0; i < 4; i++)
        {
            LineActions[i].Disable();
            LineActions[i].started -= Started;
            LineActions[i].performed -= Performed;
            LineActions[i].canceled -= Canceled;
        }
#endif

        speedUp.Disable();
        speedUp.started -= Started;
        speedUp.canceled -= Canceled;

        speedDown.Disable();
        speedDown.started -= Started;
        speedDown.canceled -= Canceled;
    }

    private void OnDestroy()
    {
#if UNITY_STANDALONE_WIN
        isEnd = true;

        if (chartPlayThread != null && chartPlayThread.IsAlive)
        {
            chartPlayThread.Join(100);
        }
#endif
    }

    public void SetSpeed(float duration)
    {
        if (settings.settings.speed + duration >= 1.0 && settings.settings.speed + duration <= 15.0)
        {
            settings.SetSpeed($"{settings.settings.speed += duration}");
            noteGenerator.speed = 12f * settings.settings.speed;
            noteGenerator.fallTime = noteGenerator.distance / noteGenerator.speed * 1000f;
            UIManager.SetSpeedText();
            if (PlayerSession.IsEntered) RecordStore.SetUserSpeed(PlayerSession.Uid, settings.settings.speed);  // per-player
        }
    }

    void Started(InputAction.CallbackContext context)
    {
        string pressed = context.control.name;
        string actionName = context.action.name;

        Debug.Log($"Start {pressed} {actionName}");

        switch (actionName)
        {
            case "Line1Action":
                DownInput(0);
                break;
            case "Line2Action":
                DownInput(1);
                break;
            case "Line3Action":
                DownInput(2);
                break;
            case "Line4Action":
                DownInput(3);
                break;
            case "SpeedUp":
                isSpeedHold = true;
                SetSpeed(0.1f);
                repeatCoroutine = StartCoroutine(RepeatKeyPress(actionName));
                break;
            case "SpeedDown":
                isSpeedHold = true;
                SetSpeed(-0.1f);
                repeatCoroutine = StartCoroutine(RepeatKeyPress(actionName));
                break;
        }
    }

    void Performed(InputAction.CallbackContext context)
    {
        string pressed = context.control.name;
        string actionName = context.action.name;

        Debug.Log($"Perform {pressed} {actionName}");
    }

    void Canceled(InputAction.CallbackContext context)
    {
        string pressed = context.control.name;
        string actionName = context.action.name;

        Debug.Log($"Cancel {pressed} {actionName}");

        isSpeedHold = false;

        switch (actionName)
        {
            case "Line1Action":
                UpInput(0);
                break;
            case "Line2Action":
                UpInput(1);
                break;
            case "Line3Action":
                UpInput(2);
                break;
            case "Line4Action":
                UpInput(3);
                break;
        }

        if (repeatCoroutine != null)
        {
            StopCoroutine(repeatCoroutine);
            repeatCoroutine = null;
        }
    }

    private void Start()
    {
        isHolding = new List<bool>();
        currentDownButtonRoutines = new List<Coroutine>();
        currentUpButtonRoutines = new List<Coroutine>();
        originX = new List<float>();
        originY = buttons[0].transform.position.y;

        for (int i = 0; i < 4; i++)
        {
            isHolding.Add(false);
            currentDownButtonRoutines.Add(null);
            currentUpButtonRoutines.Add(null);
            originX.Add(0);
            originX[i] = buttons[i].transform.position.x;
        }

        SetupBeams();

        Play();
    }

    public void Play()
    {
        currentTimeMs = 0d;
        startTime = Time.time;
        isAutoPlay = settings.isAutoPlay;
        Debug.Log($"Start Time : {startTime}");

        OnPlay.Invoke();

#if UNITY_STANDALONE_WIN
        chartPlayThread = new Thread(ChartPlayWorker);
        chartPlayThread.IsBackground = true;
        chartPlayThread.Start(8000L);
        if (divider == null)
        {
            divider = InputThreadDivider.Instance;
            if (divider == null)
            {
                Debug.LogError("InputThreadDivider.Instance is null");
                return;
            }
        }
#endif
    }

#if UNITY_STANDALONE_WIN
    private void ChartPlayWorker(object param)
    {
        long frequency = (long)param;

        long interval = 10000000 / frequency;
        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        long prevTick = stopwatch.ElapsedTicks;
        long correction = 0;

        while (!isEnd)
        {
            long now = stopwatch.ElapsedTicks;
            long timeDiff = now - prevTick;

            if (timeDiff >= interval - correction)
            {
                double progress = now / 10000000d;
                currentTime = progress;

                divider.OnChartProgressAsync(progress);
                divider.OnChartProgress(progress);

                correction = timeDiff - interval;
                if (correction > interval)
                {
                    correction = interval;
                }

                prevTick = now;
            }
        }

        Debug.Log("ChartPlayWorker ?????? ????");
    }
#endif

    void Update()
    {
#if UNITY_STANDALONE_OSX || UNITY_WEBGL
        currentTime = Time.time - startTime;
#endif
        isEnd = gameManager.isLevelEnd;

        lock (queueLock)
        {
            while (mainThreadQueue.Count > 0)
            {
                var action = mainThreadQueue.Dequeue();
                action?.Invoke();
            }
        }

        var joystick = Joystick.current;
        if (joystick != null)
        {
            for (int i = 0; i < 4; i++)
            {
                // This controller reports physical button 1 as the joystick "trigger" control,
                // not "button1"; lanes 2-4 still map to button2/button3/button4.
                string controlName = i == 0 ? "trigger" : $"button{i + 1}";
                var btn = joystick.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>(controlName);
                if (btn == null) continue;

                bool isPressed = btn.isPressed;

                if (isPressed && !isHolding[i])
                {
                    DownInput(i);
                }
                else if (!isPressed && isHolding[i])
                {
                    UpInput(i);
                }
            }
        }

    }

    public void EnqueueMainThreadAction(Action action)
    {
        lock (queueLock)
        {
            mainThreadQueue.Enqueue(action);
        }
    }

    public void DownInput(int raneNumber, double inputTime = -1)
    {
        if (inputTime < 0)
        {
            inputTime = currentTime;
        }

        currentTimeMs = inputTime * 1000f;

        isHolding[raneNumber] = true;

        judgementManager.Judge(raneNumber, currentTimeMs);

        TriggerBeam(raneNumber, true);

        // if (currentDownButtonRoutines[raneNumber] != null)
        // {
        //     StopCoroutine(currentDownButtonRoutines[raneNumber]);
        // }
        // currentDownButtonRoutines[raneNumber] = StartCoroutine(DownButton(raneNumber));
    }

    public void UpInput(int raneNumber, double inputTime = -1)
    {
        if (inputTime < 0)
        {
            inputTime = currentTime;
        }

        currentTimeMs = inputTime * 1000f;

        isHolding[raneNumber] = false;

        judgementManager.UpJudge(raneNumber, currentTimeMs);

        TriggerBeam(raneNumber, false);

        // if (currentUpButtonRoutines[raneNumber] != null)
        // {
        //     StopCoroutine(currentUpButtonRoutines[raneNumber]);
        // }
        // currentUpButtonRoutines[raneNumber] = StartCoroutine(UpButton(raneNumber));
    }

    private IEnumerator DownButton(int raneNumber)
    {
        Transform T = buttons[raneNumber].transform;

        float elapsedTime = 0f;
        Vector3 startPos = new Vector3(originX[raneNumber], T.position.y, 0f);
        float duration = 0.05f;
        Vector3 targetPos = new Vector3(originX[raneNumber], originY - 0.325f, 0f);
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            float easedT = Mathf.Sin(t * Mathf.PI * 0.5f);

            T.position = Vector3.Lerp(startPos, targetPos, easedT);

            yield return null;
        }

        T.position = targetPos;

        currentDownButtonRoutines[raneNumber] = null;

        yield break;
    }

    // Builds a per-lane material instance for the floor strip so each lane can be lit
    // independently (the asset material is shared across all four lanes). Configured for
    // additive blending so an input "key beam" brightens the lane over the dark stage.
    private void SetupBeams()
    {
        if (Lines == null) return;

        beamMaterials = new Material[Lines.Count];
        beamRoutines = new Coroutine[Lines.Count];

        for (int i = 0; i < Lines.Count; i++)
        {
            if (Lines[i] == null) continue;
            var mr = Lines[i].GetComponent<MeshRenderer>();
            if (mr == null) continue;

            var mat = mr.material; // per-renderer instance (clones the shared material)
            mat.DisableKeyword("_ALPHATEST_ON");
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            // Draw above the gear/playfield (Background "Gear" is queue 3001) but below the
            // notes (queue 3005) so the beam lights the lane without covering the notes.
            mat.renderQueue = 3002;
            mat.SetColor("_BaseColor", new Color(beamColor.r, beamColor.g, beamColor.b, 0f));

            beamMaterials[i] = mat;
        }
    }

    // Fades a lane's key beam in (on press) or out (on release).
    private void TriggerBeam(int lane, bool on)
    {
        if (beamMaterials == null || lane < 0 || lane >= beamMaterials.Length || beamMaterials[lane] == null)
            return;

        if (beamRoutines[lane] != null) StopCoroutine(beamRoutines[lane]);
        beamRoutines[lane] = StartCoroutine(BeamFade(lane, on ? beamAlpha : 0f, on ? beamFadeIn : beamFadeOut));
    }

    private IEnumerator BeamFade(int lane, float targetAlpha, float duration)
    {
        Material mat = beamMaterials[lane];
        float startAlpha = mat.GetColor("_BaseColor").a;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsedTime / duration));
            mat.SetColor("_BaseColor", new Color(beamColor.r, beamColor.g, beamColor.b, alpha));
            yield return null;
        }

        mat.SetColor("_BaseColor", new Color(beamColor.r, beamColor.g, beamColor.b, targetAlpha));
        beamRoutines[lane] = null;
    }

    private IEnumerator UpButton(int raneNumber)
    {
        Transform T = buttons[raneNumber].transform;

        float elapsedTime = 0f;
        Vector3 startPos = new Vector3(originX[raneNumber], T.position.y, 0f);
        float duration = 0.05f;
        Vector3 targetPos = new Vector3(originX[raneNumber], originY, 0f);
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            float easedT = Mathf.Sin(t * Mathf.PI * 0.5f);

            T.position = Vector3.Lerp(startPos, targetPos, easedT);

            yield return null;
        }

        T.position = targetPos;

        currentUpButtonRoutines[raneNumber] = null;

        yield break;
    }

    private IEnumerator RepeatKeyPress(string actionName)
    {
        yield return new WaitForSeconds(0.3f);

        while (isSpeedHold)
        {
            switch (actionName)
            {
                case "SpeedUp":
                    SetSpeed(0.1f);
                    break;
                case "SpeedDown":
                    SetSpeed(-0.1f);
                    break;
            }
            yield return new WaitForSeconds(0.05f);
        }
    }
}
