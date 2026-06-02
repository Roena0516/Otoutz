using UnityEngine;
using UnityEngine.UI;

namespace Otoutz
{
    /// <summary>
    /// Reusable Otoutz Midnight background (page gradient + dot tile + soft glows + vignette),
    /// matching the Menu / Result screens. Built procedurally from <see cref="OtoutzSprites"/>.
    ///
    /// In a 3D scene (e.g. InGame) it renders on a Screen Space - Camera canvas pushed to a far
    /// plane distance so all gameplay geometry draws in front of it. With no camera it falls back
    /// to a Screen Space - Overlay canvas (pure-UI scenes).
    /// </summary>
    public class OtoutzBackground : MonoBehaviour
    {
        [Tooltip("Camera to render behind. Auto-resolves to Camera.main / first camera if left empty.")]
        public Camera targetCamera;
        [Tooltip("Canvas sorting order. Keep very low so the background sits behind every other canvas.")]
        public int sortingOrder = -1000;

        const float REF_W = 1920f, REF_H = 1080f;

        void Awake()
        {
            var cam = targetCamera != null ? targetCamera : (Camera.main != null ? Camera.main : FindObjectOfType<Camera>());

            var cgo = new GameObject("OtoutzBackgroundCanvas", typeof(Canvas), typeof(CanvasScaler));
            cgo.layer = LayerMask.NameToLayer("UI");
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.GetComponent<Canvas>();

            if (cam != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                // Sit just inside the far clip so 3D gameplay (always nearer) renders on top.
                float far = cam.farClipPlane, near = cam.nearClipPlane;
                canvas.planeDistance = Mathf.Clamp(far * 0.9f, near + 1f, far - 0.01f);
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            canvas.sortingOrder = sortingOrder;

            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(REF_W, REF_H);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var root = (RectTransform)cgo.transform;

            var bg = OtoutzUI.Image("Background", root, OtoutzSprites.PageGradient(), Color.white);
            OtoutzUI.Stretch(bg.rectTransform);

            var dots = OtoutzUI.Image("Dots", root, OtoutzSprites.DotTile(), OtoutzTheme.A(OtoutzTheme.glow, 0.22f), Image.Type.Tiled);
            OtoutzUI.Stretch(dots.rectTransform);

            var g1 = OtoutzUI.Image("Glow1", root, OtoutzSprites.Glow(), OtoutzTheme.A(OtoutzTheme.glow, 0.18f));
            g1.rectTransform.sizeDelta = new Vector2(1500, 1150);
            g1.rectTransform.anchoredPosition = new Vector2(-520, 220);
            var g2 = OtoutzUI.Image("Glow2", root, OtoutzSprites.Glow(), OtoutzTheme.A(OtoutzTheme.accent, 0.12f));
            g2.rectTransform.sizeDelta = new Vector2(1200, 950);
            g2.rectTransform.anchoredPosition = new Vector2(620, -260);

            var vig = OtoutzUI.Image("Vignette", root, OtoutzSprites.Glow(), OtoutzTheme.A(Color.black, 0.45f));
            vig.rectTransform.sizeDelta = new Vector2(REF_W * 2.2f, REF_H * 1.4f);
            vig.rectTransform.anchoredPosition = new Vector2(0, -REF_H * 0.3f);
        }
    }
}
