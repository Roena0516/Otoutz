using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
#if (UNITY_STANDALONE || UNITY_EDITOR) && !UNITY_WEBGL
using System.IO.Ports;
#endif

/// <summary>
/// Reads RFID/NFC card UIDs from a serial port (one "{UID}\n" line per tap) on a background thread
/// and raises <see cref="CardScanned"/> on the main thread. Works on standalone (Win/Mac/Linux) and
/// in the editor; no-op on WebGL. Spawned app-wide before the first scene so the intro can subscribe.
/// </summary>
[DisallowMultipleComponent]
public class RfidReader : MonoBehaviour
{
    public static RfidReader Instance { get; private set; }

    /// <summary>Raised on the main thread with the scanned card UID.</summary>
    public static event Action<string> CardScanned;

    [Tooltip("Serial port. Win: COM3 | macOS: /dev/tty.usbserial-xxxx | Linux: /dev/ttyUSB0. Empty = editor simulation only.")]
    public string portName = "";
    public int baudRate = 9600;

    static readonly Queue<string> _queue = new Queue<string>();
    static readonly object _lock = new object();

#if (UNITY_STANDALONE || UNITY_EDITOR) && !UNITY_WEBGL
    SerialPort _port;
    Thread _thread;
    volatile bool _running;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("RfidReader");
        DontDestroyOnLoad(go);
        go.AddComponent<RfidReader>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // port/baud overridable without code (defaults to the Inspector values / empty)
        portName = PlayerPrefs.GetString("rfid_port", portName);
        baudRate = PlayerPrefs.GetInt("rfid_baud", baudRate);
        StartReading();
    }

    void StartReading()
    {
#if (UNITY_STANDALONE || UNITY_EDITOR) && !UNITY_WEBGL
        if (string.IsNullOrEmpty(portName))
        {
            Debug.Log("[RfidReader] no port configured — editor/key simulation only.");
            return;
        }
        try
        {
            _port = new SerialPort(portName, baudRate) { ReadTimeout = 250, NewLine = "\n", DtrEnable = true };
            _port.Open();
            _running = true;
            _thread = new Thread(ReadLoop) { IsBackground = true };
            _thread.Start();
            Debug.Log($"[RfidReader] opened {portName} @ {baudRate}");
        }
        catch (Exception e) { Debug.LogWarning($"[RfidReader] open failed ({portName}): {e.Message}"); }
#else
        Debug.Log("[RfidReader] serial unsupported on this platform.");
#endif
    }

    /// <summary>True while a serial port is open and being read.</summary>
    public bool IsConnected
    {
#if (UNITY_STANDALONE || UNITY_EDITOR) && !UNITY_WEBGL
        get { return _port != null && _port.IsOpen; }
#else
        get { return false; }
#endif
    }

    /// <summary>Real serial ports visible to the OS (empty on WebGL / on error). Filters out the
    /// pseudo-terminal noise (/dev/ttyp*, /dev/ttys*, …) so only actual devices show.</summary>
    public static string[] AvailablePorts()
    {
#if (UNITY_STANDALONE || UNITY_EDITOR) && !UNITY_WEBGL
        try
        {
            var list = new List<string>();
            foreach (var p in SerialPort.GetPortNames())
                if (LooksLikeRealPort(p)) list.Add(p);
            return list.ToArray();
        }
        catch { return new string[0]; }
#else
        return new string[0];
#endif
    }

    // Windows COM*, macOS /dev/tty.* (the dotted real devices, not /dev/ttyp*//dev/ttys* ptys),
    // Linux /dev/ttyUSB*//dev/ttyACM*.
    static bool LooksLikeRealPort(string p)
    {
        if (string.IsNullOrEmpty(p)) return false;
        if (p.StartsWith("COM")) return true;
        if (p.Contains("tty.")) return true;
        if (p.Contains("ttyUSB") || p.Contains("ttyACM")) return true;
        return false;
    }

    /// <summary>Switch port/baud at runtime, persist the choice, and reopen. Returns IsConnected.</summary>
    public bool Reconnect(string port, int baud)
    {
        portName = port ?? "";
        if (baud > 0) baudRate = baud;
        PlayerPrefs.SetString("rfid_port", portName);
        PlayerPrefs.SetInt("rfid_baud", baudRate);
        PlayerPrefs.Save();
        StopReading();
        StartReading();
        return IsConnected;
    }

    void StopReading()
    {
#if (UNITY_STANDALONE || UNITY_EDITOR) && !UNITY_WEBGL
        _running = false;
        try { _thread?.Join(300); } catch { }
        _thread = null;
        try { if (_port != null && _port.IsOpen) _port.Close(); } catch { }
        _port = null;
#endif
    }

#if (UNITY_STANDALONE || UNITY_EDITOR) && !UNITY_WEBGL
    void ReadLoop()
    {
        while (_running)
        {
            try
            {
                string uid = Parse(_port.ReadLine());
                if (!string.IsNullOrEmpty(uid)) { lock (_lock) _queue.Enqueue(uid); }
            }
            catch (TimeoutException) { }
            catch (Exception) { Thread.Sleep(50); }
        }
    }
#endif

    // "{UID}\n" -> UID. Tolerates surrounding braces / whitespace / CR.
    static string Parse(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;
        string s = line.Trim().Trim('\r', '\n', ' ');
        if (s.Length >= 2 && s[0] == '{' && s[s.Length - 1] == '}') s = s.Substring(1, s.Length - 2).Trim();
        return s.Length > 0 ? s : null;
    }

    void Update()
    {
        // drain queued UIDs and raise on the main thread
        while (true)
        {
            string uid = null;
            lock (_lock) { if (_queue.Count > 0) uid = _queue.Dequeue(); }
            if (uid == null) break;
            try { CardScanned?.Invoke(uid); } catch (Exception e) { Debug.LogException(e); }
        }

#if UNITY_EDITOR
        // editor testing without hardware: F9 / F10 emit sample UIDs
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.f9Key.wasPressedThisFrame) Simulate("TEST-UID-001");
            if (kb.f10Key.wasPressedThisFrame) Simulate("TEST-UID-002");
        }
#endif
    }

    /// <summary>Manually enqueue a UID (editor testing / external triggers). Raised next frame on the main thread.</summary>
    public static void Simulate(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return;
        lock (_lock) _queue.Enqueue(uid);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        StopReading();
    }
}
