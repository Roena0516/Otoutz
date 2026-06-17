using FMOD.Studio;
using FMODUnity;
using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class Note : MonoBehaviour
{
    private float speed;

    public float BPM;

    public bool isSet;
    public double ms;

    public bool isEndNote;
    public bool isInputed;

    private LineInputChecker line;
    private JudgementManager judgement;
    private NoteGenerator noteGenerator;

    public NoteClass noteClass;

    private float startY = 87f;
    private float endY = -96f;

    private float YPosition = 0f;

    private double dropStartTime;

    private Coroutine moveNoteRoutine;


    void Start()
    {
        isSet = false;
        isEndNote = false;
        isInputed = false;

        line = LineInputChecker.Instance;
        judgement = JudgementManager.Instance;
        noteGenerator = NoteGenerator.Instance;

        speed = noteGenerator.speed;
        dropStartTime = (ms - noteGenerator.fallTime) / 1000f;
        YPosition = noteClass.type == "hold" || noteClass.type == "rbell" || noteClass.type == "leftarrow" || noteClass.type == "rightarrow" || noteClass.type == "avoid" ? 2f : 0.001f;

        if (noteClass.type == "hold" || noteClass.type == "rbell" || noteClass.type == "leftarrow" || noteClass.type == "rightarrow")
        {
            gameObject.transform.localScale = new Vector3(7f * noteClass.width, 1f, 1f);
        }

        float oneBeatDuration = 60f / BPM * 1000f;

        if (noteClass.type == "null")
        {
            moveNoteRoutine = StartCoroutine(MoveLongNote());
        }
        else if (noteClass.type == "avoid")
        {
            moveNoteRoutine = StartCoroutine(MoveAvoidNote());
        }
        else
        {
            moveNoteRoutine = StartCoroutine(MoveNote());
        }
    }

    public void SetNote()
    {
        dropStartTime = line.currentTime;
        speed = noteGenerator.speed;
        isSet = true;
    }

    private void OnDestroy()
    {
        StopCoroutine(moveNoteRoutine);
    }

    public IEnumerator MoveNote()
    {
        // Notes are all spawned up front and parked at the far horizon until their drop time.
        // Keep them hidden until they actually start falling, so wide bell/arrow bars don't sit
        // visibly at the top of the lane before their turn.
        var rends = GetComponentsInChildren<Renderer>(true);
        while (true)
        {
            dropStartTime = (ms - noteGenerator.fallTime) / 1000f;
            double elapsedTime = line.currentTime - dropStartTime;

            bool visible = elapsedTime >= 0d;
            for (int i = 0; i < rends.Length; i++)
                if (rends[i] != null && rends[i].enabled != visible) rends[i].enabled = visible;

            float progress = (float)(elapsedTime * speed / (startY - endY));
            progress = Mathf.Clamp01(progress);
            float currentY = Mathf.Lerp(startY, endY, progress);
            transform.position = new Vector3(transform.position.x, YPosition, currentY);

            yield return null;
        }
    }

    public IEnumerator MoveLongNote()
    {
        // Re-fit the body from the CURRENT scroll speed every frame (speed can change mid-song), by
        // mapping the note's [start(ms), end(endMs)] time window onto the lane. A note of time T sits
        // at Z = lineZ + speed*(T - now); the leading end pins to the judgement line once the head
        // passes it. No length is baked at spawn, so a mid-song speed change re-sizes it correctly.
        double endMs = ms + (60000.0 / noteGenerator.BPM * noteClass.length);
        float lineZ = startY - noteGenerator.distance;   // judgement line (head reaches it at t = ms)
        float lastLength = float.NaN;

        while (endMs - (line.currentTime * 1000.0) >= -200.0)
        {
            float spd = noteGenerator.speed;             // live speed
            double now = line.currentTime;               // seconds

            // clamp both ends to [lineZ .. startY]: leading end pins to the judgement line after the
            // head passes; both ends cap at the spawn line so the body never extends behind the
            // curtain and has length 0 before the note drops.
            float zFront = Mathf.Min(startY, lineZ + Mathf.Max(0f, (float)(spd * (ms / 1000.0 - now))));
            float zBack  = Mathf.Min(startY, lineZ + (float)(spd * (endMs / 1000.0 - now)));
            float length = Mathf.Max(0f, zBack - zFront);

            // optimisation: only write the scale when the length actually changed
            if (!Mathf.Approximately(length, lastLength))
            {
                Vector3 s = transform.localScale;
                transform.localScale = new Vector3(s.x, length, s.z);
                lastLength = length;
            }

            transform.position = new Vector3(transform.position.x, YPosition, (zFront + zBack) * 0.5f);

            yield return null;
        }

        Destroy(gameObject);
        yield break;
    }

    public IEnumerator MoveAvoidNote()
    {
        float zer0Point = -10.5f;
        float gap = 7f;
        float baseX = zer0Point + gap * (noteClass.position - 1f);

        float ang = (noteClass.angle - 90f) * Mathf.Deg2Rad;
        float spd = noteClass.speed > 0 ? noteClass.speed : 1f;

        while (true)
        {
            float currentSpeed = noteGenerator.speed;
            float currentDistance = noteGenerator.distance;

            double remainingMs = ms - line.currentTime * 1000d;
            float te = (float)(remainingMs * currentSpeed / currentDistance / 1000d) * spd;
            te = Mathf.Max(0f, te);

            float currentZ = Mathf.Min(startY, (startY - currentDistance) + te * currentDistance);
            float driftX = -Mathf.Cos(ang) * 2f * te * gap;

            transform.position = new Vector3(baseX + driftX, YPosition, currentZ);

            yield return null;
        }
    }

    private void Misser()
    {
        if (noteClass.type == "long" || noteClass.type == "null")
            return;

        if (!noteClass.isInputed && (line.currentTime * 1000f) - ms >= 200f)
        {
            judgement.PerformAction(noteClass, "Miss", ms);
            judgement.ClearCombo();
            isSet = false;
        }
    }

    private void BellPerformer()
    {
        if (noteClass.isInputed) return;

        bool inTiming = noteClass.ms - (line.currentTime * 1000f) <= 0
                     && noteClass.ms - (line.currentTime * 1000f) >= -160f;

        if (noteClass.type == "hold" && inTiming)
        {
            if (Math.Abs(judgement.tsumabuki.transform.position.x - gameObject.transform.position.x) <= 3.5f + (1.75f * noteClass.width) + 2.25f)
            {
                line.judgementManager.PerformAction(noteClass, "CriticalBreak", noteClass.ms);
                line.judgementManager.AddCombo(1);
            }
        }

        if (noteClass.type == "rbell" && inTiming)
        {
            if (Math.Abs(judgement.tsumabuki.transform.position.x - gameObject.transform.position.x) <= 3.5f + (1.75f * noteClass.width) + 2.25f)
            {
                line.judgementManager.PerformAction(noteClass, "Miss", ms);
                line.judgementManager.ClearCombo();
                return;
            }
        }

        if (noteClass.type == "avoid")
        {
            if (inTiming)
            {
                bool inRange = Math.Abs(judgement.tsumabuki.transform.position.x - gameObject.transform.position.x)
                             <= 3.5f + (1.75f * noteClass.width) + 2.25f;
                if (inRange)
                {
                    line.judgementManager.PerformAction(noteClass, "Miss", ms);
                    line.judgementManager.ClearCombo();
                    return;
                }
            }

            // 판정 윈도우가 완전히 지나면 피한 것으로 간주
            if ((line.currentTime * 1000f) - ms >= 160f)
            {
                judgement.PerformAction(noteClass, "CriticalBreak", ms);
            }
            return;
        }

        if (noteClass.type == "leftarrow" && inTiming)
        {
            if (judgement.tsumabuki.GetComponent<LeverController>().leverDirection == "Left"
                && Math.Abs(judgement.tsumabuki.transform.position.x - gameObject.transform.position.x) <= 3.5f + (1.75f * noteClass.width) + 2.25f)
            {
                line.judgementManager.PerformAction(noteClass, "CriticalBreak", noteClass.ms);
                line.judgementManager.AddCombo(1);
            }
        }

        if (noteClass.type == "rightarrow" && inTiming)
        {
            if (judgement.tsumabuki.GetComponent<LeverController>().leverDirection == "Right"
                && Math.Abs(judgement.tsumabuki.transform.position.x - gameObject.transform.position.x) <= 3.5f + (1.75f * noteClass.width) + 2.25f)
            {
                line.judgementManager.PerformAction(noteClass, "CriticalBreak", noteClass.ms);
                line.judgementManager.AddCombo(1);
            }
        }

        // rbell/avoid 이외 타입: 시간 지나면 PerfectP (원본 로직 유지)
        if (!noteClass.isInputed && (line.currentTime * 1000f) - ms >= 0f)
        {
            if (noteClass.type == "rbell")
            {
                judgement.PerformAction(noteClass, "CriticalBreak", ms);
                isSet = false;
            }
        }
    }

    private void AutoPlayPerformer()
    {
        if (line.isAutoPlay && !noteClass.isInputed && (noteClass.ms - (line.currentTime * 1000f) <= 0))
        {
            line.judgementManager.PerformAction(noteClass, "CriticalBreak", noteClass.ms);
            line.judgementManager.AddCombo(1);

            Debug.Log($"AutoPlay note.ms: {noteClass.ms}, currentTime: {line.currentTime * 1000f}");
        }
    }

    void Update()
    {
        speed = noteGenerator.speed;

        Misser();

        BellPerformer();

        AutoPlayPerformer();
    }
}