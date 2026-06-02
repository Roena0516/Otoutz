using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Otoutz
{
    /// <summary>
    /// Tints a Graphic's mesh vertices with a directional linear gradient.
    /// Lets us put a smooth accent→accent2 gradient on a rounded-rect sprite (selected rows,
    /// logo marks, slider fills) without baking per-size textures.
    /// </summary>
    [AddComponentMenu("Otoutz/UI Gradient")]
    public class UIGradient : BaseMeshEffect
    {
        public Color colorA = Color.white;
        public Color colorB = Color.white;
        [Tooltip("Gradient direction in degrees (0 = left→right, 90 = bottom→top).")]
        public float angle = 100f;
        [Range(0f, 1f)] public float alpha = 1f;

        public void Set(Color a, Color b, float deg)
        {
            colorA = a; colorB = b; angle = deg;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            var verts = new List<UIVertex>();
            vh.GetUIVertexStream(verts);

            // bounds
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < verts.Count; i++)
            {
                var p = verts[i].position;
                if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
            }
            float w = Mathf.Max(0.0001f, maxX - minX);
            float h = Mathf.Max(0.0001f, maxY - minY);

            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            // project onto dir over the unit box to get a 0..1 factor
            float lenA = Vector2.Dot(new Vector2(0, 0), dir);
            float lenB = Vector2.Dot(new Vector2(dir.x >= 0 ? 1 : 0, dir.y >= 0 ? 1 : 0), dir);
            // simpler: compute min/max projection across 4 corners
            float pmin = float.MaxValue, pmax = float.MinValue;
            Vector2[] corners = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            foreach (var c in corners)
            {
                float pr = Vector2.Dot(c, dir);
                if (pr < pmin) pmin = pr; if (pr > pmax) pmax = pr;
            }
            float span = Mathf.Max(0.0001f, pmax - pmin);

            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                float nx = (v.position.x - minX) / w;
                float ny = (v.position.y - minY) / h;
                float t = (Vector2.Dot(new Vector2(nx, ny), dir) - pmin) / span;
                Color g = Color.Lerp(colorA, colorB, Mathf.Clamp01(t));
                // multiply with existing vertex color (keeps Image.color/alpha)
                g.a *= alpha;
                v.color = g * v.color;
                verts[i] = v;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }
    }
}
