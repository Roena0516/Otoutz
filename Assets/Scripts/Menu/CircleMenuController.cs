using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CircleMenuController : MonoBehaviour
{
    [Header("Circle")]
    public RectTransform circleRect;
    public float radius = 350f;
    private float centerAngleDeg = 180f;

    [Header("Items")]
    public List<RectTransform> items = new List<RectTransform>();
    public float stepAngleDeg = 30f;

    [Header("Tween")]
    private float tweenDuration = 0.18f;

    private float _offsetDeg = 0f;
    private Coroutine _moveCo;
    public int currentIndex = 1;

    void Start()
    {
        if (circleRect != null)
            radius = circleRect.rect.width * 0.5f;
        LayoutItemsInstant();
    }

    public void Move(int dir)
    {
        int newIndex = Mathf.Clamp(currentIndex + dir, 0, items.Count - 1);
        if (newIndex == currentIndex) return;
        currentIndex = newIndex;
        float target = (currentIndex - 1) * stepAngleDeg;
        if (_moveCo != null) StopCoroutine(_moveCo);
        _moveCo = StartCoroutine(AnimateOffset(_offsetDeg, target, tweenDuration));
    }

    IEnumerator AnimateOffset(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOutSine(t / duration);
            _offsetDeg = Mathf.Lerp(from, to, k);
            LayoutItemsInstant();
            yield return null;
        }
        _offsetDeg = to;
        LayoutItemsInstant();
        _moveCo = null;
    }

    private void LayoutItemsInstant()
    {
        if (items == null || items.Count == 0) return;

        for (int i = 0; i < items.Count; i++)
        {
            int rel = i - 1;
            float deg = centerAngleDeg + _offsetDeg + rel * stepAngleDeg;
            float rad = deg * Mathf.Deg2Rad;
            Vector2 p = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
            items[i].anchoredPosition = p;
            items[i].localRotation = Quaternion.identity;
            items[i].localScale = Vector3.one;
        }
    }

    private static float EaseOutSine(float x)
    {
        x = Mathf.Clamp01(x);
        return Mathf.Sin(x * Mathf.PI * 0.5f);
    }
}
