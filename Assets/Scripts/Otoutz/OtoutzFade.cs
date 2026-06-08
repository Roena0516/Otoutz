using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Otoutz
{
    /// <summary>
    /// Full-screen fade overlay for smooth scene transitions. On scene start it fades in from
    /// black (revealing the scene); <see cref="FadeOutAndLoad"/> fades to black then loads the
    /// next scene. Place one in each scene that should transition (InGame, Result).
    /// Uses unscaled time so it works regardless of Time.timeScale.
    /// </summary>
    public class OtoutzFade : MonoBehaviour
    {
        [Tooltip("How long to hold on solid black after load before the fade-in begins.")]
        public float holdDuration = 0.45f;
        [Tooltip("Duration of the fade-in-from-black played on scene start.")]
        public float enterDuration = 0.45f;
        public Color color = Color.black;

        public static OtoutzFade Instance { get; private set; }

        Image _img;
        Canvas _canvas;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            var cgo = new GameObject("OtoutzFadeCanvas", typeof(Canvas), typeof(GraphicRaycaster));
            cgo.layer = LayerMask.NameToLayer("UI");
            cgo.transform.SetParent(transform, false);
            _canvas = cgo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32760; // above every other canvas

            _img = OtoutzUI.Image("Fade", (RectTransform)cgo.transform, null, color);
            OtoutzUI.Stretch(_img.rectTransform);

            SetAlpha(1f);
            StartCoroutine(EnterRoutine());
        }

        // Fade in from black AFTER the scene's heavy first-frame load (chart parse, note spawn,
        // FMOD) has passed, so the big initial delta-time doesn't make the fade snap to 0 instantly.
        IEnumerator EnterRoutine()
        {
            _canvas.enabled = true; _img.raycastTarget = true;
            SetColorAlpha(1f);
            // let the load hitch happen while the screen is held black
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();

            // hold on black a beat so the blackout reads before the scene reveals
            if (holdDuration > 0f) yield return new WaitForSecondsRealtime(holdDuration);

            float t = 0f;
            while (t < enterDuration)
            {
                // clamp per-step delta so a stray slow frame can't skip the fade
                t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                SetColorAlpha(Mathf.Lerp(1f, 0f, Ease.OutCubic(t / enterDuration)));
                yield return null;
            }
            SetAlpha(0f);
        }

        void SetAlpha(float a)
        {
            var c = color; c.a = a; _img.color = c;
            _img.raycastTarget = a > 0.001f;
            _canvas.enabled = a > 0.001f;
        }

        /// <summary>Fade to black, then load the scene. Overlay stays opaque through the load.</summary>
        public void FadeOutAndLoad(string scene, float dur = 0.5f)
        {
            StartCoroutine(FadeRoutine(_img.color.a, 1f, dur, () => SceneManager.LoadSceneAsync(scene)));
        }

        IEnumerator FadeRoutine(float from, float to, float dur, Action onDone)
        {
            _canvas.enabled = true; _img.raycastTarget = true; // block input while fading
            SetColorAlpha(from);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                SetColorAlpha(Mathf.Lerp(from, to, Ease.OutCubic(t / dur)));
                yield return null;
            }
            SetAlpha(to);
            onDone?.Invoke();
        }

        // like SetAlpha but never disables the canvas mid-fade (avoids a flicker at alpha~0)
        void SetColorAlpha(float a) { var c = color; c.a = a; _img.color = c; }
    }
}
