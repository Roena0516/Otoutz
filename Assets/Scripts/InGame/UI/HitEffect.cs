using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Spawns the note-hit visual effects:
///  1) a glowing ring burst on the judgement line (lane-coloured), and
///  2) a few stars that pop out at the hit spot and fly toward the player.
/// Everything (textures, materials, meshes) is generated procedurally so no prefabs or
/// scene wiring are needed — JudgementManager creates one of these and calls Play().
/// </summary>
public class HitEffect : MonoBehaviour
{
    // per-lane accent colours (left → right)
    private static readonly Color[] LaneColors =
    {
        new Color(1.0f, 0.35f, 0.42f), // red / pink
        new Color(0.45f, 1.0f, 0.55f), // green
        new Color(0.45f, 0.68f, 1.0f), // blue
        new Color(1.0f, 0.82f, 0.42f), // gold
    };

    private Camera _cam;
    private Mesh _quad;
    private Material _glowMat;  // soft radial glow (ring burst)
    private Material _starMat;  // 5-point star (flying)
    private Material _trailMat; // comet tail behind the star
    private Material _flashMat; // bright sparkle flare at the hit

    private void Awake()
    {
        _cam = Camera.main;
        _quad = BuildQuad();
        // glow under the notes (gear=3001, notes=3005); stars/trails/flash on top of everything
        _glowMat = MakeAdditiveMaterial(MakeGlowTexture(128), 3003);
        _starMat = MakeAdditiveMaterial(MakeStarTexture(96), 3006);
        _trailMat = MakeAdditiveMaterial(null, 3005); // null tex → shader's default white
        _flashMat = MakeAdditiveMaterial(MakeFlareTexture(160), 3007);
    }

    /// <param name="pos">World position of the hit (the lane's judgement point).</param>
    /// <param name="laneIndex">0-3, used for the accent colour.</param>
    public void Play(Vector3 pos, int laneIndex)
    {
        if (_cam == null) _cam = Camera.main;
        Color c = LaneColors[Mathf.Clamp(laneIndex, 0, LaneColors.Length - 1)];

        StartCoroutine(RingBurst(pos, c));
        StartCoroutine(Flash(pos, c));

        // left lanes spray to the left, right lanes to the right
        float outward = pos.x >= 0f ? 1f : -1f;
        int stars = Random.Range(3, 5);
        for (int i = 0; i < stars; i++)
            StartCoroutine(StarFly(pos, c, outward));
    }

    // ── ring burst on the judgement line ──────────────────────────────────────
    private IEnumerator RingBurst(Vector3 pos, Color color)
    {
        var go = NewQuad(_glowMat, "HitGlow");
        var mr = go.GetComponent<MeshRenderer>();
        var mpb = new MaterialPropertyBlock();
        var tr = go.transform;
        tr.position = pos;
        tr.rotation = Quaternion.Euler(90f, 0f, 0f); // lay flat on the lane

        float dur = 0.30f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float s = Mathf.Lerp(2.2f, 8f, 1f - (1f - p) * (1f - p)); // ease-out expand
            tr.localScale = new Vector3(s, s, s);
            float a = Mathf.Lerp(0.95f, 0f, p);
            mpb.SetColor("_BaseColor", new Color(color.r, color.g, color.b, a));
            mr.SetPropertyBlock(mpb);
            yield return null;
        }
        Destroy(go);
    }

    // ── bright sparkle flare at the hit ───────────────────────────────────────
    private IEnumerator Flash(Vector3 pos, Color color)
    {
        var go = NewQuad(_flashMat, "HitFlash");
        var mr = go.GetComponent<MeshRenderer>();
        var mpb = new MaterialPropertyBlock();
        var tr = go.transform;
        tr.position = pos;
        // mostly white with a hint of the lane colour, pushed past 1 so the additive core blows
        // out to a bright white sparkle (and blooms if post-processing is on)
        Color fc = Color.Lerp(color, Color.white, 0.85f);
        const float bright = 2.6f;
        float spin = Random.Range(-25f, 25f);

        float dur = 0.2f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);

            if (_cam != null)
                tr.rotation = Quaternion.LookRotation(pos - _cam.transform.position, Vector3.up)
                              * Quaternion.Euler(0f, 0f, spin);
            float s = Mathf.Lerp(4f, 8.5f, Mathf.Sqrt(p)); // quick pop then ease
            tr.localScale = new Vector3(s, s, s);
            float a = Mathf.Pow(1f - p, 1.6f); // bright at the hit, then fade
            mpb.SetColor("_BaseColor", new Color(fc.r * bright, fc.g * bright, fc.b * bright, a));
            mr.SetPropertyBlock(mpb);
            yield return null;
        }
        Destroy(go);
    }

    // ── stars flying toward the player ────────────────────────────────────────
    private IEnumerator StarFly(Vector3 pos, Color color, float outward)
    {
        var go = NewQuad(_starMat, "HitStar");
        var mr = go.GetComponent<MeshRenderer>();
        var mpb = new MaterialPropertyBlock();
        var tr = go.transform;

        Vector3 p = pos + new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.3f, 0.6f), 0f);
        // shoot up and outward (left lanes → left, right lanes → right) near the camera; gravity
        // bends it into a parabola that sweeps off toward the side, like the reference
        Vector3 vel = new Vector3(
            outward * Random.Range(22f, 30f) + Random.Range(-2f, 2f),
            Random.Range(17f, 23f),
            Random.Range(2f, 6f));
        float dur = 0.7f; // long enough to fly off the screen, then destroyed
        float baseScale = Random.Range(0.9f, 1.4f);
        float spin = Random.Range(-540f, 540f);
        Color sc = Color.Lerp(color, Color.white, 0.45f);

        // fixed size and full brightness — the star leaves the screen instead of fading
        tr.localScale = Vector3.one * baseScale;
        mpb.SetColor("_BaseColor", new Color(sc.r, sc.g, sc.b, 1f));
        mr.SetPropertyBlock(mpb);

        // comet tail following the star
        var trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = _trailMat;
        trail.time = 0.4f;
        trail.startWidth = 0.6f * baseScale;
        trail.endWidth = 0f;
        trail.numCapVertices = 3;
        trail.minVertexDistance = 0.03f;
        trail.alignment = LineAlignment.View; // ribbon faces the camera
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        Color tc = Color.Lerp(color, Color.white, 0.3f);
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(tc, 0f), new GradientColorKey(tc, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = grad;

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            p += vel * Time.deltaTime; // constant velocity — no gravity, no slow-down
            tr.position = p;

            Quaternion face = _cam != null
                ? Quaternion.LookRotation(p - _cam.transform.position, Vector3.up)
                : Quaternion.identity;
            tr.rotation = face * Quaternion.Euler(0f, 0f, spin * t);
            yield return null;
        }

        // hide the head and let the comet tail drain away over its own lifetime instead of
        // popping out all at once when the object is destroyed
        mr.enabled = false;
        float drain = trail.time + 0.1f;
        float dt = 0f;
        while (dt < drain) { dt += Time.deltaTime; yield return null; }
        Destroy(go);
    }

    // ── procedural building blocks ────────────────────────────────────────────
    private GameObject NewQuad(Material mat, string name)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = _quad;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
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

    private static Material MakeAdditiveMaterial(Texture2D tex, int queue)
    {
        // custom additive shader (blend/cull/zwrite are fixed in the shader itself)
        var m = new Material(Shader.Find("PROJECT-O/HitAdditive"));
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.renderQueue = queue;
        return m;
    }

    private static Texture2D MakeGlowTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - r) / r;
            float dy = (y + 0.5f - r) / r;
            float d = Mathf.Sqrt(dx * dx + dy * dy);       // 0 centre .. 1 edge
            float glow = Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f);
            float ring = Mathf.Exp(-40f * (d - 0.72f) * (d - 0.72f)); // bright rim
            float a = Mathf.Clamp01(glow * 0.7f + ring * 0.8f);
            px[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // bright 4-point sparkle flare: glowing core + thin horizontal/vertical spikes + faint diagonals
    private static Texture2D MakeFlareTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f - r) / r;
            float v = (y + 0.5f - r) / r;

            float core = Mathf.Exp(-(u * u + v * v) * 9f);
            float horiz = Mathf.Exp(-(v * v) * 340f) * Mathf.Exp(-(u * u) * 2.2f);
            float vert = Mathf.Exp(-(u * u) * 340f) * Mathf.Exp(-(v * v) * 2.2f);
            float a1 = (u + v) * 0.70710678f, b1 = (u - v) * 0.70710678f;
            float diag = (Mathf.Exp(-(b1 * b1) * 600f) * Mathf.Exp(-(a1 * a1) * 4f)
                        + Mathf.Exp(-(a1 * a1) * 600f) * Mathf.Exp(-(b1 * b1) * 4f)) * 0.4f;

            float a = Mathf.Clamp01(core + horiz + vert + diag);
            px[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static Texture2D MakeStarTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var c = new Vector2(size / 2f, size / 2f);
        float outer = size * 0.46f;
        float inner = outer * 0.45f;
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
            // 2x2 supersample for smoother edges
            float cover = 0f;
            for (int sy = 0; sy < 2; sy++)
            for (int sx = 0; sx < 2; sx++)
                if (PointInPoly(new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f), v)) cover += 0.25f;

            // soft inner brightness toward the centre for a sparkle core
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / outer;
            float core = Mathf.Clamp01(1f - d) * 0.35f;
            float a = Mathf.Clamp01(cover + core * cover);
            px[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static bool PointInPoly(Vector2 p, Vector2[] v)
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
}
