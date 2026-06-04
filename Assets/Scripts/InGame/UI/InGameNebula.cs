using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using FMODUnity;

/// <summary>
/// Builds an animated nebula backdrop for the in-game scene (drifting clouds + particles) and
/// shows the live audio spectrum as a subtle waveform behind the playfield. An FFT DSP is added
/// to FMOD's master channel group; its spectrum is downsampled into a 1-D texture each frame and
/// fed to the <c>PROJECT-O/Nebula</c> shader (<c>_SpecTex</c>). The full-screen RawImage lives on a
/// far-plane Screen Space - Camera canvas so all gameplay renders in front of it.
/// </summary>
public class InGameNebula : MonoBehaviour
{
    [Tooltip("Canvas sorting order — keep just above OtoutzBackground (-1000) but below gameplay UI.")]
    public int sortingOrder = -999;

    [Header("Spectrum")]
    [SerializeField] private int _bands = 256;
    [SerializeField] private float _gain = 90f;
    [SerializeField, Range(0f, 1f)] private float _attack = 0.6f; // rise smoothing
    [SerializeField, Range(0f, 1f)] private float _decay = 0.06f;  // fall smoothing

    private Material _mat;
    private Texture2D _specTex;
    private Color[] _pixels;
    private float[] _smoothed;
    private float[] _mono;
    private float[] _chBuf;

    private FMOD.ChannelGroup _master;
    private FMOD.DSP _fft;
    private bool _fftReady;

    private static readonly int SpecTexId = Shader.PropertyToID("_SpecTex");

    private void Awake()
    {
        var cam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();

        var cgo = new GameObject("InGameNebulaCanvas", typeof(Canvas), typeof(CanvasScaler));
        cgo.layer = LayerMask.NameToLayer("UI");
        cgo.transform.SetParent(transform, false);

        var canvas = cgo.GetComponent<Canvas>();
        if (cam != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = Mathf.Clamp(cam.farClipPlane * 0.9f, cam.nearClipPlane + 1f, cam.farClipPlane - 0.01f);
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        canvas.sortingOrder = sortingOrder;

        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _mat = new Material(Shader.Find("PROJECT-O/Nebula"));

        _specTex = new Texture2D(_bands, 1, TextureFormat.RFloat, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        _pixels = new Color[_bands];
        _smoothed = new float[_bands];
        _mat.SetTexture(SpecTexId, _specTex);

        var img = new GameObject("Nebula", typeof(RawImage)).GetComponent<RawImage>();
        img.transform.SetParent(cgo.transform, false);
        img.texture = Texture2D.whiteTexture;
        img.material = _mat;
        img.raycastTarget = false;
        var rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void Start()
    {
        try
        {
            RuntimeManager.CoreSystem.getMasterChannelGroup(out _master);
            RuntimeManager.CoreSystem.createDSPByType(FMOD.DSP_TYPE.FFT, out _fft);
            _fft.setParameterInt((int)FMOD.DSP_FFT.WINDOWSIZE, 1024);
            _fft.setParameterInt((int)FMOD.DSP_FFT.WINDOWTYPE, (int)FMOD.DSP_FFT_WINDOW.HANNING);
            _master.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, _fft);
            _fftReady = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[InGameNebula] FFT setup failed: " + e.Message);
        }
    }

    private void Update()
    {
        if (!_fftReady || _mat == null) return;

        if (_fft.getParameterData((int)FMOD.DSP_FFT.SPECTRUMDATA, out IntPtr data, out uint _) != FMOD.RESULT.OK)
            return;

        var fft = (FMOD.DSP_PARAMETER_FFT)Marshal.PtrToStructure(data, typeof(FMOD.DSP_PARAMETER_FFT));
        if (fft.numchannels < 1 || fft.length < 1) { Fade(); return; }

        int bins = fft.length;
        if (_mono == null || _mono.Length != bins) { _mono = new float[bins]; _chBuf = new float[bins]; }

        // average the channels into a mono spectrum (reusing buffers to avoid per-frame GC)
        Array.Clear(_mono, 0, bins);
        for (int ch = 0; ch < fft.numchannels; ch++)
        {
            fft.getSpectrum(ch, ref _chBuf);
            for (int b = 0; b < bins; b++) _mono[b] += _chBuf[b];
        }
        float inv = 1f / fft.numchannels;

        for (int c = 0; c < _bands; c++)
        {
            // log-ish mapping so bass doesn't dominate the whole width
            int b0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Pow((float)c / _bands, 2f) * bins), 0, bins - 1);
            int b1 = Mathf.Clamp(Mathf.Max(b0 + 1, Mathf.FloorToInt(Mathf.Pow((c + 1f) / _bands, 2f) * bins)), b0 + 1, bins);

            float sum = 0f;
            for (int b = b0; b < b1; b++) sum += _mono[b] * inv;
            float val = Mathf.Clamp01(Mathf.Sqrt(sum / (b1 - b0) * _gain));

            float prev = _smoothed[c];
            _smoothed[c] = val > prev ? Mathf.Lerp(prev, val, _attack) : Mathf.Lerp(prev, val, _decay);
            _pixels[c] = new Color(_smoothed[c], 0f, 0f, 1f);
        }
        _specTex.SetPixels(_pixels);
        _specTex.Apply(false);
    }

    private void Fade()
    {
        for (int c = 0; c < _bands; c++)
        {
            _smoothed[c] = Mathf.Lerp(_smoothed[c], 0f, _decay);
            _pixels[c] = new Color(_smoothed[c], 0f, 0f, 1f);
        }
        _specTex.SetPixels(_pixels);
        _specTex.Apply(false);
    }

    private void OnDestroy()
    {
        if (!_fftReady) return;
        try { _master.removeDSP(_fft); _fft.release(); } catch { /* shutting down */ }
    }
}
