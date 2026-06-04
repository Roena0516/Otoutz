using UnityEngine;

/// <summary>
/// Drives the gear's horizontal beat lines so they descend exactly like the notes: spaced one
/// beat apart in world distance and moving at the note fall speed, with a line crossing the
/// judgement line on every beat. Feeds <c>_BeatCount</c> and <c>_ScrollOffset</c> into the gear
/// material each frame (both depend on the live note speed / BPM). Attach to the gear renderer.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class GearBeatScroll : MonoBehaviour
{
    [SerializeField] private float _judgementZ = 10f; // world z of the judgement line
    [SerializeField] private float _timeOffset = 1f;   // notes carry a +1000 ms (1 s) start offset

    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;
    private float _z0, _zRange;

    private static readonly int BeatCountId = Shader.PropertyToID("_BeatCount");
    private static readonly int ScrollOffsetId = Shader.PropertyToID("_ScrollOffset");

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    private void Start()
    {
        // the gear's world-space depth span (UV v 0 = near edge .. 1 = far edge)
        var b = _renderer.bounds;
        _z0 = b.center.z - b.size.z * 0.5f;
        _zRange = b.size.z;
    }

    private void Update()
    {
        var lic = LineInputChecker.Instance;
        var ng = NoteGenerator.Instance;
        if (lic == null || ng == null || ng.BPM <= 0f || ng.speed <= 0f) return;

        float beatsPerSec = ng.BPM / 60f;

        // number of beats spanning the lane = laneDepth / (note distance per beat). With this as
        // _BeatCount, one line = one beat at the exact note spacing, and the scroll offset below
        // advances so the lines move down at the note fall speed.
        float beatCount = (_zRange / ng.speed) * beatsPerSec;

        // a note for beat B sits at z = judgementZ + speed*(B*60/BPM + timeOffset - currentTime);
        // solving for the line positions gives this offset (judgement crossing stays on the beat)
        float offset = ((_z0 - _judgementZ) / ng.speed + (float)lic.currentTime - _timeOffset) * beatsPerSec;

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(BeatCountId, beatCount);
        _mpb.SetFloat(ScrollOffsetId, offset);
        _renderer.SetPropertyBlock(_mpb);
    }
}
