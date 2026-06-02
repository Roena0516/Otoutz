using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Otoutz
{
    /// <summary>Factory helpers for building the uGUI tree procedurally.</summary>
    public static class OtoutzUI
    {
        public static TMP_FontAsset Display;  // Baloo 2 stand-in (chunky)
        public static TMP_FontAsset Body;     // Plus Jakarta Sans stand-in

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(100, 100);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        public static void SetAnchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
        }

        public static void Stretch(RectTransform rt, float l = 0, float r = 0, float t = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color, Image.Type type = UnityEngine.UI.Image.Type.Simple)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = type;
            if (type == UnityEngine.UI.Image.Type.Sliced) img.pixelsPerUnitMultiplier = 1f;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Rounded filled panel. Returns the fill Image (its RectTransform is the panel root).</summary>
        public static Image Panel(string name, Transform parent, int radius, Color fill)
        {
            var img = Image(name, parent, OtoutzSprites.RoundedRect(radius), fill, UnityEngine.UI.Image.Type.Sliced);
            return img;
        }

        /// <summary>Adds an inset rounded ring as a stretched child of <paramref name="parent"/>.</summary>
        public static Image Ring(Transform parent, int radius, int thickness, Color color)
        {
            var img = Image("Ring", parent, OtoutzSprites.RoundedBorder(radius, thickness), color, UnityEngine.UI.Image.Type.Sliced);
            Stretch(img.rectTransform);
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string content, float size, Color color,
            bool display = false, FontStyles style = FontStyles.Normal,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = content;
            t.font = display ? Display : Body;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Image Glow(string name, Transform parent, Color color, float alpha)
        {
            var img = Image(name, parent, OtoutzSprites.Glow(), OtoutzTheme.A(color, alpha));
            return img;
        }

        public static UIGradient AddGradient(Image img, Color a, Color b, float angle)
        {
            var g = img.gameObject.AddComponent<UIGradient>();
            g.Set(a, b, angle);
            return g;
        }

        public static PointerRelay MakeClickable(Image img, Action onClick, Action onEnter = null)
        {
            img.raycastTarget = true;
            var pr = img.gameObject.AddComponent<PointerRelay>();
            pr.onClick = onClick;
            pr.onEnter = onEnter;
            return pr;
        }
    }

    /// <summary>Lightweight pointer relay for hover/click without per-widget MonoBehaviours.</summary>
    public class PointerRelay : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
    {
        public Action onClick;
        public Action onEnter;
        public void OnPointerClick(PointerEventData e) { onClick?.Invoke(); }
        public void OnPointerEnter(PointerEventData e) { onEnter?.Invoke(); }
    }
}
