using UnityEngine;

namespace Otoutz
{
    /// <summary>
    /// Design tokens ported from the Otoutz handoff (Midnight theme, dark).
    /// All colours and difficulty definitions live here so the screens stay 1:1 with the spec.
    /// </summary>
    public static class OtoutzTheme
    {
        // ---- Midnight palette ----
        public static readonly Color bg0    = Hex("#221a36");
        public static readonly Color bg1    = Hex("#1a1730");
        public static readonly Color bg2    = Hex("#251b3c");
        public static readonly Color glow   = Hex("#7a5cff");
        public static readonly Color accent = Hex("#ff6ec7");
        public static readonly Color accent2= Hex("#56d0ff");
        public static readonly Color ink    = Hex("#f0ebff");
        public static readonly Color sub    = Hex("#a99fce");
        public static readonly Color panel  = Hex("#2c2447");
        public static readonly Color line   = Hex("#3a3158");

        public const float CubicOutBack = 0f; // marker; easing implemented in Ease

        /// <summary>
        /// Difficulty tiers. The project's real charts use ADVANCED / EXPERT / MASTER / LUNATIC
        /// (songDictionary order). We keep the handoff's 4-colour scheme mapped onto those tiers.
        /// </summary>
        public struct Diff
        {
            public string label;
            public Color color;   // base/tint
            public Color deep;    // text / number
            public string desc;   // korean sub description
            public Diff(string l, string c, string d, string desc)
            {
                label = l; color = Hex(c); deep = Hex(d); this.desc = desc;
            }
        }

        public static readonly Diff[] Diffs =
        {
            new Diff("BASIC",    "#3ec98a", "#1f8f5e", "읽기 쉬운 기본 패턴"),
            new Diff("ADVANCED", "#f5b62e", "#c98a0e", "본격 입문 난이도"),
            new Diff("EXPERT",   "#f5577f", "#c92f57", "고밀도 응용 패턴"),
            new Diff("LUNATIC",  "#9b6ef3", "#6f3fd1", "최고 난이도 도전"),
        };

        // data difficulty strings (songList.json / chart filenames), index-aligned with Diffs above
        public static readonly string[] DiffKeys = { "BASIC", "ADVANCED", "EXPERT", "LUNATIC" };

        // ---- helpers ----
        public static Color Hex(string hex)
        {
            hex = hex.Replace("#", "");
            if (hex.Length == 3)
                hex = "" + hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];
            byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
            return new Color32(r, g, b, 255);
        }

        public static Color A(Color c, float a)
        {
            c.a = a; return c;
        }
    }

    public static class Ease
    {
        // cubic-bezier(0.22, 1, 0.36, 1) — the handoff's "enter" easing, approximated.
        public static float OutBack(float x)
        {
            x = Mathf.Clamp01(x);
            float c1 = 1.70158f * 0.55f;
            float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        public static float OutCubic(float x)
        {
            x = Mathf.Clamp01(x);
            return 1f - Mathf.Pow(1f - x, 3f);
        }

        public static float OutQuint(float x)
        {
            x = Mathf.Clamp01(x);
            return 1f - Mathf.Pow(1f - x, 5f);
        }
    }
}
