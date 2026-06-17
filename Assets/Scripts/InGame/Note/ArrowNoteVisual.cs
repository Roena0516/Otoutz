using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Arrow note visual: a bell-style bar (glossy slab + glow + centre line) with a row of chevrons
/// floating ABOVE it — exactly the way a bell note floats a star above its bar. The chevrons point
/// and scroll toward the arrow's direction. Matches the design reference.
/// </summary>
[DisallowMultipleComponent]
public class ArrowNoteVisual : MonoBehaviour
{
    [Tooltip("True = chevrons point/flow left (leftarrow); false = right (rightarrow).")]
    public bool pointLeft = true;

    [Tooltip("Warm orange bar colour (matches the reference).")]
    public Color barColor = new Color(0.85f, 0.42f, 0.06f, 1f);
    [Tooltip("Bright chevron colour.")]
    public Color chevronColor = new Color(1.0f, 0.62f, 0.08f, 1f);
    public float emission = 1.4f;
    public float scrollSpeed = 0.22f;   // UV/sec across the strip — gentle
    public float chevronHeight = 1.9f;  // world height of the floating chevron row
    public float chevronRise = 1.8f;    // how far above the bar the chevron row floats

    const int Chevrons = 9;
    static Texture2D _glossTex, _stripLeft, _stripRight;
    Transform _deco, _strip;
    Material _stripMat;

    void Awake()
    {
        var rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;

        // --- bell-style bar: glossy slab + emissive glow ---
        var mat = rend.material;
        if (_glossTex == null) _glossTex = BuildGloss();
        mat.SetColor("_BaseColor", barColor);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _glossTex);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", barColor * emission);

        // --- front-face centre line (child of the bar, so it spans the bar width) ---
        var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var lc = line.GetComponent<Collider>(); if (lc != null) Destroy(lc);
        line.name = "CenterLine";
        line.transform.SetParent(transform, false);
        line.transform.localScale = new Vector3(0.93f, 0.06f, 0.16f);
        line.transform.localPosition = new Vector3(0f, -0.52f, 0f);
        Color lineColor = Color.Lerp(barColor, Color.white, 0.6f);
        SetupMat(line.GetComponent<Renderer>(), lineColor, lineColor * 1.5f, null, false);

        // --- chevron row floating above the bar (like the bell's star), spanning the bar width ---
        // NOTE: AddComponent runs Awake before NoteSpawner assigns pointLeft, so the direction-
        // dependent texture is chosen later in FitOnce (a placeholder is used here).
        _deco = new GameObject("ArrowDeco").transform;   // uniform holder (rotation follows the bar)
        _deco.SetParent(transform, false);
        _deco.localPosition = Vector3.zero;
        _deco.localRotation = Quaternion.identity;

        var strip = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var qc = strip.GetComponent<Collider>(); if (qc != null) Destroy(qc);
        strip.name = "Chevrons";
        strip.transform.SetParent(_deco, false);
        strip.transform.localPosition = new Vector3(0f, 0f, -chevronRise); // raised above the bar (local -Z = world up)
        strip.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);     // stand up, face the camera
        strip.transform.localScale = new Vector3(7f, chevronHeight, 1f);   // X re-fit to the real width after Note.Start
        _strip = strip.transform;
        var sr = strip.GetComponent<Renderer>();
        SetupMat(sr, chevronColor, chevronColor * emission, Strip(true), true); // placeholder; corrected in FitOnce
        _stripMat = sr.material;

        StartCoroutine(FitOnce());
    }

    static Texture2D Strip(bool left)
    {
        if (left) return _stripLeft != null ? _stripLeft : (_stripLeft = BuildChevronStrip(true));
        return _stripRight != null ? _stripRight : (_stripRight = BuildChevronStrip(false));
    }

    // Once Note.Start has applied the bar scale (7*width,1,1): cancel it on the holder (so the
    // chevron strip isn't sheared) and re-fit the strip width to span the bar. The bar scale
    // never changes afterwards, so this only needs to run once.
    IEnumerator FitOnce()
    {
        yield return null;
        Vector3 s = transform.localScale;
        if (_deco != null)
            _deco.localScale = new Vector3(s.x != 0 ? 1f / s.x : 1f, s.y != 0 ? 1f / s.y : 1f, s.z != 0 ? 1f / s.z : 1f);
        if (_strip != null) { var ls = _strip.localScale; ls.x = s.x * 0.96f; _strip.localScale = ls; }
        // pointLeft is now set (NoteSpawner assigned it after Awake) — apply the correct chevron texture
        if (_stripMat != null)
        {
            var tex = Strip(pointLeft);
            if (_stripMat.HasProperty("_BaseMap")) _stripMat.SetTexture("_BaseMap", tex);
            if (_stripMat.HasProperty("_EmissionMap")) _stripMat.SetTexture("_EmissionMap", tex);
        }
    }

    void Update()
    {
        if (_stripMat == null) return;
        var o = _stripMat.GetTextureOffset("_BaseMap");
        o.x += Time.deltaTime * scrollSpeed * (pointLeft ? 1f : -1f);
        _stripMat.SetTextureOffset("_BaseMap", o);
        _stripMat.SetTextureOffset("_EmissionMap", o);
    }

    // ---- textures ----
    // Wide repeating strip of chevrons: white RGB, alpha carries the chevron shape (transparent gaps),
    // so the floating row shows only the arrows. Tinted + glowed by the material colour.
    static Texture2D BuildChevronStrip(bool left)
    {
        const int W = 1024, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        var px = new Color[W * H];
        const float tip = 0.18f, slope = 0.58f, half = 0.16f, aa = 0.05f;
        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;
            float cy = Mathf.Abs(v - 0.5f) * 2f;
            float centre = tip + slope * cy;
            for (int x = 0; x < W; x++)
            {
                float t = Mathf.Repeat((x + 0.5f) / W * Chevrons, 1f);
                float uu = left ? t : 1f - t;
                float d = Mathf.Abs(uu - centre);
                float stroke = 1f - SmoothStep01(half - aa, half + aa, d);
                px[y * W + x] = new Color(1f, 1f, 1f, stroke);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // Grayscale gloss: sheen band + darkened bevel toward the long edges; multiplies the bar colour.
    static Texture2D BuildGloss()
    {
        const int W = 16, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;
            float edge = Mathf.Abs(v - 0.5f) * 2f;
            float bevel = Mathf.Lerp(1.05f, 0.55f, SmoothStep01(0.55f, 1f, edge));
            float sheen = 0.22f * Mathf.Exp(-Mathf.Pow((v - 0.62f) / 0.10f, 2f));
            float val = Mathf.Clamp01(bevel + sheen);
            for (int x = 0; x < W; x++) px[y * W + x] = new Color(val, val, val, 1f);
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static void SetupMat(Renderer rend, Color color, Color emissive, Texture2D tex, bool transparent)
    {
        if (rend == null) return;
        var m = rend.material;
        if (transparent)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.renderQueue = (int)RenderQueue.Transparent + 5;
        }
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Off);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (tex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emissive);
        if (tex != null && m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", tex);
    }

    static float SmoothStep01(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
