using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Keeps a bell / rbell note's existing colour but lifts it out of "flat coloured cube" territory:
/// adds an emissive glow, a bright horizontal line across the front face centre, and a glowing
/// star sprite on the top face centre. Added at spawn time; reads the prefab material's own
/// _BaseColor so each bell keeps its colour.
/// </summary>
[DisallowMultipleComponent]
public class BellNoteVisual : MonoBehaviour
{
    [Tooltip("Emission strength relative to the bell's base colour.")]
    public float emission = 1.35f;

    static Texture2D _glossTex, _starTex;
    Transform _deco;   // holder that cancels the bar's width stretch so the star stays a fixed size

    void Awake()
    {
        var rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;

        var mat = rend.material; // instance, safe to mutate
        Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;

        if (_glossTex == null) _glossTex = BuildGloss();
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _glossTex);

        // glow with the bell's own colour so the scene bloom picks it up
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", baseColor * emission);

        BuildDecorations(baseColor);
    }

    // The bell is a unit cube rotated Euler(90,0,0) then scaled to (7*width, 1, 1). After that
    // rotation: local +X = bar length (world X), local -Z = up (world +Y), and the InGame camera
    // (at y20 z-6, pitched 30° down) sees the local -Y face (world -Z) as the "front". A holder
    // cancels the bell's scale so children sit in world units.
    void BuildDecorations(Color baseColor)
    {
        // --- front-face centre horizontal line: child of the bar so it spans the bar's width ---
        // (the bar is scaled to (7*width,1,1); a fractional localScale.x tracks any width)
        var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var lc = line.GetComponent<Collider>(); if (lc != null) Destroy(lc);
        line.name = "CenterLine";
        line.transform.SetParent(transform, false);
        line.transform.localScale = new Vector3(0.93f, 0.06f, 0.16f); // span X, flat depth, thin height
        line.transform.localPosition = new Vector3(0f, -0.52f, 0f);   // camera-facing front face (local -Y = world -Z)
        Color lineColor = Color.Lerp(baseColor, Color.white, 0.65f);
        SetupMat(line.GetComponent<Renderer>(), lineColor, lineColor * 1.6f, null, false);

        // --- star sprite floating above the bar centre, FIXED size (independent of width) ---
        // The deco holder's scale is reset every frame to cancel the bar's width stretch.
        _deco = new GameObject("BellDeco").transform;
        _deco.SetParent(transform, false);
        _deco.localPosition = Vector3.zero;
        _deco.localRotation = Quaternion.identity;

        if (_starTex == null) _starTex = BuildStar();
        var star = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var sc = star.GetComponent<Collider>(); if (sc != null) Destroy(sc);
        star.name = "TopStar";
        star.transform.SetParent(_deco, false);
        star.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
        star.transform.localPosition = new Vector3(0f, 0f, -1.8f);     // raised above the bar (local -Z = world up)
        star.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);  // face the camera (local -Y = world -Z)
        Color starColor = Color.Lerp(baseColor, Color.white, 0.35f);
        SetupMat(star.GetComponent<Renderer>(), starColor, starColor * 1.7f, _starTex, true);

        StartCoroutine(CancelDecoScaleOnce());
    }

    // Note.Start sets the bar to (7*width,1,1) one frame after our Awake; cancel that stretch once
    // (the bar scale never changes after) so the star keeps a constant world size regardless of width.
    IEnumerator CancelDecoScaleOnce()
    {
        yield return null; // wait for Note.Start to apply the bar scale
        if (_deco == null) yield break;
        Vector3 s = transform.localScale;
        _deco.localScale = new Vector3(
            Mathf.Approximately(s.x, 0f) ? 1f : 1f / s.x,
            Mathf.Approximately(s.y, 0f) ? 1f : 1f / s.y,
            Mathf.Approximately(s.z, 0f) ? 1f : 1f / s.z);
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
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Off); // double-sided so the sprite shows regardless of facing
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (tex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emissive);
        if (tex != null && m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", tex);
    }

    // Grayscale highlight: glossy sheen band through the middle, darkened bevel toward the
    // long edges. Multiplies the bell's base colour, so the colour itself is preserved.
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

    // Sharp 5-pointed star, white on transparent (10-vertex polygon, point-in-poly + 2x2 AA).
    static Texture2D BuildStar()
    {
        const int S = 128, points = 5;
        const float outer = 0.47f, inner = 0.19f;
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
        {
            for (int x = 0; x < S; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                    {
                        var p = new Vector2((x + 0.25f + sx * 0.5f) / S, (y + 0.25f + sy * 0.5f) / S);
                        if (InPoly(p, verts)) hits++;
                    }
                px[y * S + x] = new Color(1f, 1f, 1f, hits / 4f);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static bool InPoly(Vector2 p, Vector2[] v)
    {
        bool inside = false;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
        {
            if (((v[i].y > p.y) != (v[j].y > p.y)) &&
                (p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x))
                inside = !inside;
        }
        return inside;
    }

    static float SmoothStep01(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
