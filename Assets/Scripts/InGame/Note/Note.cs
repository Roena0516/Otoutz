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
        while (true)
        {
            dropStartTime = (ms - noteGenerator.fallTime) / 1000f;
            double elapsedTime = line.currentTime - dropStartTime;
            float progress = (float)(elapsedTime * speed / (startY - endY));
            progress = Mathf.Clamp01(progress);  // 0 ~ 1 사이로 제한
            float currentY = Mathf.Lerp(startY, endY, progress);
            transform.position = new Vector3(transform.position.x, YPosition, currentY);

            yield return null;
        }
    }

    public IEnumerator MoveLongNote()
    {
        float originScaleZ = gameObject.transform.localScale.z;
        double longNoteEndTimeMs = ms + (60000f / noteGenerator.BPM * noteClass.length);
        double remainingDistance = originScaleZ;

        endY = 10f;

        while (longNoteEndTimeMs - (line.currentTime * 1000f) >= -200f)
        {
            double currentTimeMs = line.currentTime * 1000f;
            double duration = longNoteEndTimeMs - currentTimeMs;
            remainingDistance = speed * (duration / 1000f);

            if (line.currentTime * 1000f <= ms)
            {
                remainingDistance = originScaleZ;
            }

            gameObject.transform.localScale = new Vector3(gameObject.transform.localScale.x, gameObject.transform.localScale.y, (float)remainingDistance);


            dropStartTime = (ms - noteGenerator.fallTime) / 1000f;
            double elapsedTime = line.currentTime - dropStartTime;
            float progress = (float)(elapsedTime * speed / (startY - endY));
            progress = Mathf.Clamp01(progress);  // 0 ~ 1 사이로 제한
            float currentY = Mathf.Lerp(startY, endY, progress);

            currentY += (float)remainingDistance / 2f;
            transform.position = new Vector3(transform.position.x, YPosition, currentY);

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

        while (true)
        {
            dropStartTime = (ms - noteGenerator.fallTime) / 1000f;
            double elapsedTime = line.currentTime - dropStartTime;

            float progress = (float)(elapsedTime * speed / (startY - endY));
            progress = Mathf.Clamp01(progress);

            // 프리뷰의 t = 1 - progress (1=시작, 0=판정선)
            float t = 1f - progress;

            float ang = (noteClass.angle - 90f) * Mathf.Deg2Rad;
            float spd = noteClass.speed > 0 ? noteClass.speed : 1f;
            float te = t * spd;

            // 수직 이동 (speed 배율 적용)
            float currentZ = Mathf.Lerp(endY, startY, Mathf.Clamp01(te));

            // 횡방향 드리프트 (프리뷰: position - cos(ang) * 2 * te, 게임 단위: * gap)
            float driftX = -Mathf.Cos(ang) * 2f * te * gap;

            transform.position = new Vector3(baseX + driftX, YPosition, currentZ);

            yield return null;
        }
    }

    private void Misser()
    {
        // 롱노트와 longNote(null)는 JudgementManager에서 처리하므로 여기서 제외
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
        if (noteClass.type == "hold" && (noteClass.ms - (line.currentTime * 1000f) <= 0 && noteClass.ms - (line.currentTime * 1000f) >= -160))
        {
            if (Math.Abs(judgement.tsumabuki.transform.position.x - gameObject.transform.position.x) <= 3.5f + (1.75f * noteClass.width) + 2.25f)
            {
                line.judgementManager.PerformAction(noteClass, "PerfectP", noteClass.ms);
                line.judgementManager.AddCombo(1);
            }
        }
        if ((noteClass.type == "rbell" || noteClass.type == "avoid") && (noteClass.ms - (line.currentTime * 1000f) <= 0 && noteClass.ms - (line.currentTime * 1000f) >= -160))
        {
            if (Math.Abs(judgement.tsumabuki.transform.position.x - gameObject.transform.position.x) <= 3.5f + (1.75f * noteClass.width) + 2.25f)
            {
                line.judgementManager.PerformAction(noteClass, "Miss", ms);
                line.judgementManager.ClearCombo();
            }
        }
        if (noteClass.type == "leftarrow" && (noteClass.ms - (line.currentTime * 1000f) <= 0 && noteClass.ms - (line.currentTime * 1000f) >= -160))
        {
            if (judgement.tsumabuki.GetComponent<LeverController>().leverDirection == "Left" && Math.Abs(judgement.tsumabuki.transform.position.x - gameObject.transform.position.x) <= 3.5f + (1.75f * noteClass.width) + 2.25f)
            {
                line.judgementManager.PerformAction(noteClass, "PerfectP", noteClass.ms);
                line.judgementManager.AddCombo(1);
            }
        }
        if (noteClass.type == "rightarrow" && (noteClass.ms - (line.currentTime * 1000f) <= 0 && noteClass.ms - (line.currentTime * 1000f) >= -160))
        {
            if (judgement.tsumabuki.GetComponent<LeverController>().leverDirection == "Right" && Math.Abs(judgement.tsumabuki.transform.position.x - gameObject.transform.position.x) <= 3.5f + (1.75f * noteClass.width) + 2.25f)
            {
                line.judgementManager.PerformAction(noteClass, "PerfectP", noteClass.ms);
                line.judgementManager.AddCombo(1);
            }
        }

        if (!noteClass.isInputed && (line.currentTime * 1000f) - ms >= 0f)
        {
            if (noteClass.type == "rbell" || noteClass.type == "avoid")
            {
                judgement.PerformAction(noteClass, "PerfectP", ms);
                isSet = false;
            }
        }
    }

    private void AutoPlayPerformer()
    {
        if (line.isAutoPlay && !noteClass.isInputed && (noteClass.ms - (line.currentTime * 1000f) <= 0))
        {
            line.judgementManager.PerformAction(noteClass, "PerfectP", noteClass.ms);
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
