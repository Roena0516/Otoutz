using System.Collections.Generic;
using UnityEngine;

namespace Otoutz
{
    /// <summary>
    /// Runtime-generated white sprites (tinted via Image.color) so the UI needs no imported art.
    /// Rounded rects / rings are 9-sliced; everything is cached.
    /// </summary>
    public static class OtoutzSprites
    {
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        static Sprite Make(string key, Texture2D tex, Vector4 border, bool tiled = false)
        {
            tex.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply(false, false);
            var rect = new Rect(0, 0, tex.width, tex.height);
            var s = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            _cache[key] = s;
            return s;
        }

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            return t;
        }

        // ---------- Rounded rectangle (9-slice) ----------
        public static Sprite RoundedRect(int radius)
        {
            // Clamp: radius is the 9-slice corner; values larger than the element's
            // half-height get squished into ellipses. 64 keeps a clean stadium for any pill.
            radius = Mathf.Clamp(radius, 1, 64);
            string key = "rr" + radius;
            if (_cache.TryGetValue(key, out var s)) return s;

            int center = 4;
            int size = radius * 2 + center;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float r = radius;
            float halfX = size * 0.5f, halfY = size * 0.5f;
            float innerX = halfX - r, innerY = halfY - r;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - halfX) - innerX;
                    float dy = Mathf.Abs(y + 0.5f - halfY) - innerY;
                    float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
                    float d = Mathf.Sqrt(ox * ox + oy * oy) - r;
                    float a = Mathf.Clamp01(0.5f - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            return Make(key, tex, new Vector4(radius, radius, radius, radius));
        }

        // ---------- Rounded inset ring / border (9-slice) ----------
        public static Sprite RoundedBorder(int radius, int thickness)
        {
            radius = Mathf.Clamp(radius, 1, 64);
            thickness = Mathf.Max(1, thickness);
            string key = "rb" + radius + "_" + thickness;
            if (_cache.TryGetValue(key, out var s)) return s;

            int center = 4;
            int size = radius * 2 + center;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            float r = radius;
            float halfX = size * 0.5f, halfY = size * 0.5f;
            float innerX = halfX - r, innerY = halfY - r;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - halfX) - innerX;
                    float dy = Mathf.Abs(y + 0.5f - halfY) - innerY;
                    float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
                    float d = Mathf.Sqrt(ox * ox + oy * oy) - r;
                    float outer = Mathf.Clamp01(0.5f - d);
                    float inner = Mathf.Clamp01(0.5f - (d + thickness));
                    float a = Mathf.Clamp01(outer - inner);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            return Make(key, tex, new Vector4(radius, radius, radius, radius));
        }

        // ---------- 5-point star ----------
        public static Sprite Star()
        {
            string key = "star";
            if (_cache.TryGetValue(key, out var s)) return s;
            int size = 96;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            Vector2 c = new Vector2(size * 0.5f, size * 0.52f);
            float outer = size * 0.46f, inner = outer * 0.45f;
            Vector2[] pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.Deg2Rad * (-90f + i * 36f);
                float rad = (i % 2 == 0) ? outer : inner;
                pts[i] = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
            }
            const int ss = 3;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            Vector2 p = new Vector2(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss);
                            if (PointInPoly(p, pts)) hit++;
                        }
                    float a = hit / (float)(ss * ss);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            return Make(key, tex, Vector4.zero);
        }

        // ---------- Right-pointing triangle (play / caret) ----------
        public static Sprite Triangle()
        {
            string key = "tri";
            if (_cache.TryGetValue(key, out var s)) return s;
            int size = 64;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            Vector2[] pts = {
                new Vector2(size * 0.30f, size * 0.18f),
                new Vector2(size * 0.30f, size * 0.82f),
                new Vector2(size * 0.80f, size * 0.50f),
            };
            const int ss = 3;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            Vector2 p = new Vector2(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss);
                            if (PointInPoly(p, pts)) hit++;
                        }
                    float a = hit / (float)(ss * ss);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            return Make(key, tex, Vector4.zero);
        }

        // ---------- Chevron "<" (back arrow / carousel arrow) ----------
        public static Sprite Chevron()
        {
            string key = "chev";
            if (_cache.TryGetValue(key, out var s)) return s;
            int size = 64;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            Vector2 a1 = new Vector2(size * 0.66f, size * 0.16f);
            Vector2 a2 = new Vector2(size * 0.30f, size * 0.50f);
            Vector2 a3 = new Vector2(size * 0.66f, size * 0.84f);
            float thick = size * 0.085f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Mathf.Min(SegDist(p, a1, a2), SegDist(p, a2, a3));
                    float al = Mathf.Clamp01(thick - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(al * 255));
                }
            tex.SetPixels32(px);
            return Make(key, tex, Vector4.zero);
        }

        // ---------- Gear (settings) ----------
        public static Sprite Gear()
        {
            string key = "gear";
            if (_cache.TryGetValue(key, out var s)) return s;
            int size = 80;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            float ringR = size * 0.30f, ringT = size * 0.09f;
            float toothBase = size * 0.30f, toothAmp = size * 0.09f;
            float holeR = size * 0.13f;
            const int ss = 3;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            Vector2 p = new Vector2(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss) - c;
                            float r = p.magnitude;
                            float ang = Mathf.Atan2(p.y, p.x);
                            float toothR = toothBase + toothAmp * Mathf.Sign(Mathf.Cos(ang * 8f) - 0.2f) * 0.5f + toothAmp * 0.5f;
                            bool inGear = r <= toothR && r >= holeR;
                            bool inRing = Mathf.Abs(r - ringR) <= ringT * 0.5f;
                            if ((inGear && (r >= ringR - ringT * 0.5f || r <= holeR + 2)) || inRing || (r <= toothR && r >= ringR - ringT * 0.5f))
                                hit++;
                        }
                    float a = hit / (float)(ss * ss);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            return Make(key, tex, Vector4.zero);
        }

        // ---------- Soft radial glow ----------
        public static Sprite Glow()
        {
            string key = "glow";
            if (_cache.TryGetValue(key, out var s)) return s;
            int size = 128;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            float maxR = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / maxR;
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a; // softer falloff
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            return Make(key, tex, Vector4.zero);
        }

        // ---------- Dot tile (background) ----------
        public static Sprite DotTile()
        {
            string key = "dot";
            if (_cache.TryGetValue(key, out var s)) return s;
            int size = 46;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            Vector2 c = new Vector2(1f, 1f); // dot near corner so tiling spreads evenly
            float dotR = 2.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float a = Mathf.Clamp01(dotR - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            return Make(key, tex, Vector4.zero, tiled: true);
        }

        // ---------- Vertical radial-ish background gradient ----------
        public static Sprite PageGradient()
        {
            string key = "page";
            if (_cache.TryGetValue(key, out var s)) return s;
            int size = 256;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            // radial-gradient(120% 90% at 20% 0%, bg0, bg1 50%, bg2 100%)
            Vector2 center = new Vector2(0.20f, 1.0f); // top-left-ish in UV (y up)
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    float v = y / (float)(size - 1);
                    float dx = (u - center.x) / 1.2f;
                    float dy = (v - center.y) / 0.9f;
                    float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    Color col = t < 0.5f
                        ? Color.Lerp(OtoutzTheme.bg0, OtoutzTheme.bg1, t / 0.5f)
                        : Color.Lerp(OtoutzTheme.bg1, OtoutzTheme.bg2, (t - 0.5f) / 0.5f);
                    px[y * size + x] = col;
                }
            tex.SetPixels32(px);
            return Make(key, tex, Vector4.zero);
        }

        // ---------- Plain soft-edged circle (jacket blobs) ----------
        public static Sprite Circle()
        {
            string key = "circle";
            if (_cache.TryGetValue(key, out var s)) return s;
            int size = 128;
            var tex = NewTex(size, size);
            var px = new Color32[size * size];
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            float maxR = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float a = Mathf.Clamp01((maxR - d));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            return Make(key, tex, Vector4.zero);
        }

        // ---------- math helpers ----------
        static bool PointInPoly(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            int n = poly.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                    (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                    inside = !inside;
            }
            return inside;
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
