using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Replaces the flat tsumabuki avatar with a glowing star "core": a soft halo, a slowly spinning
/// star, and a short upward light tail — matching the star/comet hit effects. Built procedurally
/// (no art assets) with the PROJECT-O/HitAdditive shader. The LeverController still moves the root
/// on X and the bell judgement still reads transform.position.x, so gameplay is unchanged.
/// </summary>
[DisallowMultipleComponent]
public class TsumabukiStar : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] private Color _coreColor = new Color(1.05f, 1.15f, 1.30f, 1f); // >1 → blooms
    [SerializeField] private Color _glowColor = new Color(0.45f, 0.70f, 1.00f, 1f);
    [SerializeField] private Color _lineColor = new Color(0.50f, 0.95f, 1.90f, 1f); // neon beam (blooms)
    [SerializeField] private float _coreSize = 2.6f;
    [SerializeField] private float _glowSize = 6.5f;
    [SerializeField] private float _spinSpeed = 35f;

    private Camera _cam;
    private Transform _glow, _star;
    private float _seed;

    private void Awake()
    {
        _cam = Camera.main;

        var quad = BuildQuad();
        var spindle = BuildSpindle(24);

        // bright neon-blue material that blooms, matching the star's glow
        var lineMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lineMat.SetColor("_BaseColor", _lineColor);
        if (lineMat.HasProperty("_Cull")) lineMat.SetFloat("_Cull", 0f); // render both sides

        // hide the old flat body quads; reshape the position line into a thin tapered spindle
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.gameObject.name == "Line")
            {
                var lmf = r.GetComponent<MeshFilter>();
                if (lmf != null) lmf.sharedMesh = spindle; // pointed at both ends
                r.sharedMaterial = lineMat;
            }
            else r.enabled = false;
        }

        var glowMat = MakeMat(MakeGlowTex(128), _glowColor, 3004);
        var starMat = MakeMat(MakeStarTex(96), _coreColor, 3006);

        _glow = NewQuad("StarGlow", quad, glowMat);
        _star = NewQuad("StarCore", quad, starMat);
        _seed = Random.value * 10f;
    }

    private void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        Vector3 pos = transform.position;
        Quaternion face = _cam != null
            ? Quaternion.LookRotation(pos - _cam.transform.position, Vector3.up)
            : Quaternion.identity;
        float pulse = 1f + 0.06f * Mathf.Sin((Time.time + _seed) * 3f);

        _glow.position = pos;
        _glow.rotation = face;
        _glow.localScale = Vector3.one * (_glowSize * pulse);

        _star.position = pos;
        _star.rotation = face * Quaternion.Euler(0f, 0f, Time.time * _spinSpeed);
        _star.localScale = Vector3.one * (_coreSize * pulse);
    }

    // ── building blocks ───────────────────────────────────────────────────────
    private Transform NewQuad(string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go.transform;
    }

    private static Mesh BuildQuad()
    {
        var m = new Mesh();
        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
        };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        m.RecalculateBounds();
        return m;
    }

    // thin spindle/needle along local Y: width follows sin so it tapers to a point at both ends
    private static Mesh BuildSpindle(int seg)
    {
        var m = new Mesh();
        var verts = new Vector3[(seg + 1) * 2];
        for (int i = 0; i <= seg; i++)
        {
            float t = (float)i / seg;
            float y = t - 0.5f;
            float w = 0.5f * Mathf.Sin(Mathf.PI * t); // 0 at the ends, widest in the middle
            verts[i * 2] = new Vector3(-w, y, 0f);
            verts[i * 2 + 1] = new Vector3(w, y, 0f);
        }
        var tris = new int[seg * 6];
        int ti = 0;
        for (int i = 0; i < seg; i++)
        {
            int a = i * 2, b = i * 2 + 1, c = i * 2 + 2, d = i * 2 + 3;
            tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
            tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
        }
        m.vertices = verts;
        m.triangles = tris;
        m.RecalculateBounds();
        return m;
    }

    private static Material MakeMat(Texture2D tex, Color color, int queue)
    {
        var m = new Material(Shader.Find("PROJECT-O/HitAdditive"));
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", color);
        m.renderQueue = queue;
        return m;
    }

    private static Texture2D MakeGlowTex(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - r) / r, dy = (y + 0.5f - r) / r;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.0f);
            px[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px); tex.Apply();
        return tex;
    }

    private static Texture2D MakeStarTex(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var c = new Vector2(size / 2f, size / 2f);
        float outer = size * 0.47f, inner = outer * 0.45f;
        var v = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float ang = Mathf.Deg2Rad * (-90f + i * 36f);
            float rad = (i % 2 == 0) ? outer : inner;
            v[i] = c + new Vector2(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad);
        }
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cover = 0f;
            for (int sy = 0; sy < 2; sy++)
            for (int sx = 0; sx < 2; sx++)
                if (PointInPoly(new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f), v)) cover += 0.25f;
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / outer;
            float core = Mathf.Clamp01(1f - d) * 0.4f;
            px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(cover + core * cover));
        }
        tex.SetPixels(px); tex.Apply();
        return tex;
    }

    private static bool PointInPoly(Vector2 p, Vector2[] v)
    {
        bool inside = false;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
            if (((v[i].y > p.y) != (v[j].y > p.y)) &&
                (p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x))
                inside = !inside;
        return inside;
    }
}
