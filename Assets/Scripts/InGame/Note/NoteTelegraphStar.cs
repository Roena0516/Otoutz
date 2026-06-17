using System.Collections;
using UnityEngine;

/// <summary>
/// Lever-note entrance telegraph: starting <see cref="beatsAhead"/> beats before the note begins to
/// fall, a big colour-matched star fades in at the note's spawn point, spins, and shrinks down to 0,
/// vanishing exactly as the note starts dropping. The star uses the Custom/TelegraphStar shader
/// (ZTest Always) so it draws OVER the DepthMask "Wall" curtain that otherwise hides anything sitting
/// behind the spawn horizon.
///
/// The star lives on its own root GameObject (not parented to the note) so it is unaffected by
/// Note.MoveNote, which force-toggles every child renderer of the note while it is parked.
/// </summary>
[DisallowMultipleComponent]
public class NoteTelegraphStar : MonoBehaviour
{
    [Tooltip("Star tint; set to the note's own colour by the spawner.")]
    public Color noteColor = Color.white;
    [Tooltip("How many beats before the drop the telegraph appears.")]
    public float beatsAhead = 2f;
    [Tooltip("World size of the star at the start of the telegraph (shrinks to 0).")]
    public float bigScale = 16f;
    [Tooltip("Peak opacity of the star (1 = fully opaque).")]
    public float maxAlpha = 0.45f;
    public float spinSpeed = 140f;   // deg/sec
    public float intensity = 1.4f;   // HDR glow multiplier (feeds scene bloom)
    [Tooltip("World distance from the spawn horizon (Z=87) to the front of the DepthMask curtain. " +
             "The star keeps shrinking until the note crosses this, so it vanishes exactly as the note appears.")]
    public float spawnToCurtain = 2f;

    // Camera-facing orientation (matches the bell note's floating star: note Euler(90,0,0) * star Euler(90,0,0)).
    static readonly Quaternion FaceRot = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(90f, 0f, 0f);

    [Tooltip("World-space upward lift so the star floats above the lane plane.")]
    public float lift = 2.5f;

    static Texture2D _starTex;
    static Shader _shader;

    GameObject _starGO;
    Renderer _rend;
    Material _mat;
    Note _note;
    NoteGenerator _gen;
    LineInputChecker _line;
    float _spin;
    bool _ready;

    void Awake()
    {
        _note = GetComponent<Note>();

        if (_starTex == null) _starTex = BuildStar();
        if (_shader == null) _shader = Shader.Find("Custom/TelegraphStar");

        _starGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var col = _starGO.GetComponent<Collider>(); if (col != null) Destroy(col);
        _starGO.name = "NoteTelegraphStar";
        // The bell note's star ends up camera-facing because it inherits the note's Euler(90,0,0)
        // and adds its own Euler(90,0,0). This star is a root object, so bake both in (world normal
        // -Z, facing the camera) and roll about the normal for the spin.
        _starGO.transform.rotation = FaceRot * Quaternion.Euler(0f, 0f, _spin);
        _starGO.transform.localScale = Vector3.zero;

        _rend = _starGO.GetComponent<Renderer>();
        _mat = _shader != null ? new Material(_shader) : _rend.material;
        if (_shader != null) _rend.material = _mat;
        if (_mat.HasProperty("_MainTex")) _mat.SetTexture("_MainTex", _starTex);
        _rend.enabled = false;

        StartCoroutine(Init());
    }

    IEnumerator Init()
    {
        yield return null; // let NoteSpawner assign noteColor and Note.Start run
        _gen = NoteGenerator.Instance;
        _line = LineInputChecker.Instance;
        ApplyColor(0f);
        _ready = true;
    }

    void Update()
    {
        if (!_ready || _mat == null || _note == null || _starGO == null || _gen == null || _line == null) return;
        if (_note.ms <= 0d) return;

        float fallTime = _gen.fallTime;
        double dropStart = (_note.ms - fallTime) / 1000.0;
        double elapsed = _line.currentTime - dropStart;
        float bpm = _gen.BPM > 0f ? _gen.BPM : 120f;
        float fadeDur = beatsAhead * 60f / bpm;
        // extend the telegraph past the drop until the note emerges from behind the curtain, so the
        // star shrinks to 0 exactly as the note becomes visible (no empty gap)
        float emerge = _gen.speed > 0f ? spawnToCurtain / _gen.speed : 0f;

        if (elapsed >= -fadeDur && elapsed < emerge)
        {
            // follow the note (lane X, descending Z), lifted above the lane plane
            _starGO.transform.position = transform.position + Vector3.up * lift;

            if (!_rend.enabled) _rend.enabled = true;
            float t = Mathf.Clamp01((float)((elapsed + fadeDur) / (fadeDur + emerge))); // 0 -> 1 across the window
            float alpha = Mathf.Clamp01(t / 0.18f) * maxAlpha;               // quick fade-in, capped opacity
            float scale = Mathf.Lerp(bigScale, 0f, t);                       // big -> 0, hits 0 as the note emerges
            _spin += Time.deltaTime * spinSpeed;

            _starGO.transform.localScale = new Vector3(scale, scale, scale);
            _starGO.transform.rotation = FaceRot * Quaternion.Euler(0f, 0f, _spin);
            ApplyColor(alpha);
        }
        else
        {
            if (_rend.enabled) _rend.enabled = false;
            if (elapsed >= emerge) { Destroy(_starGO); Destroy(this); } // note has emerged — telegraph done
        }
    }

    void ApplyColor(float alpha)
    {
        if (_mat == null) return;
        if (_mat.HasProperty("_Color")) _mat.SetColor("_Color", new Color(noteColor.r, noteColor.g, noteColor.b, alpha));
        if (_mat.HasProperty("_Intensity")) _mat.SetFloat("_Intensity", intensity);
    }

    void OnDestroy()
    {
        if (_starGO != null) Destroy(_starGO);
    }

    // Sharp 5-pointed star OUTLINE (neon): a bright thin stroke along the star's edges plus a soft
    // outward glow halo, transparent everywhere else. White RGB (tinted/glowed by the material).
    static Texture2D BuildStar()
    {
        const int S = 256, points = 5;
        const float outer = 0.45f, inner = 0.18f;
        const float stroke = 0.016f;  // half-width of the solid neon line (UV units)
        const float aa = 0.006f;      // edge softening
        const float glowW = 0.06f;    // gaussian glow falloff radius
        var verts = new Vector2[points * 2];
        for (int k = 0; k < points * 2; k++)
        {
            float ang = -Mathf.PI / 2f + k * (Mathf.PI / points); // tip up
            float rad = (k % 2 == 0) ? outer : inner;
            verts[k] = new Vector2(0.5f + Mathf.Cos(ang) * rad, 0.5f + Mathf.Sin(ang) * rad);
        }

        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                var p = new Vector2((x + 0.5f) / S, (y + 0.5f) / S);
                float d = DistToOutline(p, verts);
                float core = 1f - SmoothStep01(stroke, stroke + aa, d);     // solid neon line
                float glow = Mathf.Exp(-(d * d) / (glowW * glowW)) * 0.55f; // soft halo around it
                float a = Mathf.Clamp01(Mathf.Max(core, glow));
                px[y * S + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // Minimum distance from p to the closed polyline through v (the star outline).
    static float DistToOutline(Vector2 p, Vector2[] v)
    {
        float best = float.MaxValue;
        for (int i = 0; i < v.Length; i++)
        {
            Vector2 a = v[i], b = v[(i + 1) % v.Length];
            best = Mathf.Min(best, SegDist(p, a, b));
        }
        return best;
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, Vector2.Dot(ab, ab)));
        return Vector2.Distance(p, a + ab * t);
    }

    static float SmoothStep01(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
