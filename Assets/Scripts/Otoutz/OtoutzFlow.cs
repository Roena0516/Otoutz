using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Otoutz
{
    /// <summary>
    /// Unified Otoutz front-end (Menu → Song Select → Difficulty + Settings) built procedurally
    /// in uGUI to match the design handoff. Wires real songList.json data + SettingsManager + InGame launch.
    /// </summary>
    public class OtoutzFlow : MonoBehaviour
    {
        enum Screen { Menu, Select, Difficulty, Settings }

        [Header("Fonts (TMP). Leave empty to auto-load from Resources/OtoutzFonts)")]
        public TMP_FontAsset displayFont;
        public TMP_FontAsset bodyFont;

        [Header("Carousel tuning (handoff defaults)")]
        [Range(0, 100)] public float focus = 60f;
        [Range(70, 130)] public float cardSize = 125f;

        const float REF_W = 1920f, REF_H = 1080f;

        Canvas _canvas;
        RectTransform _root;
        readonly Dictionary<Screen, RectTransform> _screens = new Dictionary<Screen, RectTransform>();
        readonly Dictionary<Screen, CanvasGroup> _groups = new Dictionary<Screen, CanvasGroup>();

        List<OtoutzSong> _songs = new List<OtoutzSong>();
        Screen _screen = Screen.Menu;
        int _songIndex = 0, _diffIndex = 3, _menuIndex = 0, _rowIndex = 0;
        bool _busy;

        // When returning from gameplay/result, open directly on the song-select screen with the
        // previously chosen song. Set by GameManager (mid-game exit) / OtoutzResult (next/esc).
        public static bool OpenOnSelect;
        static int _lastSongIndex = -1, _lastDiffIndex = 3;

        SettingsManager _sm;

        // local settings model (mirrors the handoff rows; persisted to SettingsManager on back)
        float _setSpeed = 6f; int _setOffset = 0, _setVolume = 80, _setSe = 70;

        // intro / title splash (shown on first entry; any key -> menu, the background stays)
        RectTransform _introRoot; CanvasGroup _introGroup;
        Image _introChar; TextMeshProUGUI _pressText, _pressSub;
        bool _intro;

        // cached Otoutz wordmark sprite (Otoutz_blur), reused for every logo lockup
        Sprite _wordmark;

        // sky layer for occasional shooting stars (behind every screen, above the background art)
        RectTransform _meteorLayer;

        void Awake()
        {
            Application.runInBackground = true;
            LoadFonts();
            _songs = OtoutzData.Load();
            if (_songs.Count == 0) _songs.Add(Placeholder());
            _songIndex = Mathf.Clamp(_songs.Count / 2, 0, _songs.Count - 1);

            Screen start = Screen.Menu;
            if (OpenOnSelect)
            {
                OpenOnSelect = false;
                if (_lastSongIndex >= 0) _songIndex = Mathf.Clamp(_lastSongIndex, 0, _songs.Count - 1);
                _diffIndex = Mathf.Clamp(_lastDiffIndex, 0, 3);
                start = Screen.Select;
            }
            _screen = start;

            _sm = SettingsManager.Instance;
            PullSettings();
            BuildCanvas();
            BuildBackground();
            BuildMenu();
            BuildSelect();
            BuildDifficulty();
            BuildSettings();
            ShowOnly(start, instant: true);
            RefreshMenu(); RefreshSelect(); RefreshDifficulty(); RefreshSettings();
            if (start == Screen.Select)
            {
                PlayPreview();
                // Returning from gameplay/result: play the handoff's "back" enter animation
                // (slide in from the left + fade) so the cross-scene return feels continuous.
                StartCoroutine(EnterAnim(Screen.Select, "back"));
            }
            else if (start == Screen.Menu)
            {
                // First entry: show the title splash (artwork + "press any key"); the menu sits
                // behind it, hidden, and is revealed when the player presses a key.
                BuildIntro();
                _intro = true;
                _screens[Screen.Menu].gameObject.SetActive(false);
            }

            StartCoroutine(MeteorSpawner());
        }

        // Plays a single screen's "enter" animation (no outgoing screen), matching Transition's
        // presets. Used for the initial animated entrance when arriving from another scene.
        IEnumerator EnterAnim(Screen to, string cls)
        {
            _busy = true;
            var rt = _screens[to]; var cg = _groups[to];
            rt.gameObject.SetActive(true);
            rt.SetAsLastSibling();

            float dur = (cls == "zoom" || cls == "zoomout") ? 0.42f : 0.40f;
            Vector2 fromPos = Vector2.zero; float fromScale = 0.99f;
            switch (cls)
            {
                case "fwd": fromPos = new Vector2(64, 0); break;
                case "back": fromPos = new Vector2(-64, 0); break;
                case "zoom": fromScale = 0.93f; break;
                case "zoomout": fromScale = 1.07f; break;
            }

            // Pre-set the start state this frame so the settled state never flashes first.
            cg.alpha = 0.2f; rt.anchoredPosition = fromPos; rt.localScale = Vector3.one * fromScale;

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Ease.OutBack(t / dur);
                cg.alpha = Mathf.Lerp(0.2f, 1f, Ease.OutCubic(t / dur));
                rt.anchoredPosition = Vector2.Lerp(fromPos, Vector2.zero, k);
                rt.localScale = Vector3.one * Mathf.Lerp(fromScale, 1f, k);
                yield return null;
            }
            cg.alpha = 1f; rt.anchoredPosition = Vector2.zero; rt.localScale = Vector3.one;
            _busy = false;
        }

        void LoadFonts()
        {
            OtoutzUI.Display = displayFont != null ? displayFont : Resources.Load<TMP_FontAsset>("OtoutzFonts/Display");
            OtoutzUI.Body = bodyFont != null ? bodyFont : Resources.Load<TMP_FontAsset>("OtoutzFonts/Body");
            if (OtoutzUI.Display == null) OtoutzUI.Display = TMP_Settings.defaultFontAsset;
            if (OtoutzUI.Body == null) OtoutzUI.Body = OtoutzUI.Display;
        }

        OtoutzSong Placeholder()
        {
            var s = new OtoutzSong { title = "No Songs", artist = "-", genre = "MUSIC", bpm = 0 };
            s.artA = OtoutzTheme.accent; s.artB = OtoutzTheme.accent2; s.artBlob = OtoutzTheme.glow; s.glyph = "♪";
            return s;
        }

        void PullSettings()
        {
            if (_sm == null || _sm.settings == null) return;
            _setSpeed = Mathf.Clamp(_sm.settings.speed, 1f, 10f);
            _setOffset = Mathf.Clamp(_sm.settings.sync, -100, 100);
            _setVolume = Mathf.Clamp(_sm.settings.musicVolume * 10, 0, 100);
            _setSe = Mathf.Clamp(_sm.settings.sfxVolume * 10, 0, 100);
        }

        void PushSettings()
        {
            if (_sm == null || _sm.settings == null) return;
            _sm.settings.speed = _setSpeed;
            _sm.settings.sync = _setOffset;
            _sm.settings.musicVolume = Mathf.RoundToInt(_setVolume / 10f);
            _sm.settings.sfxVolume = Mathf.RoundToInt(_setSe / 10f);
            try { _sm.SaveSettings(); } catch { }
        }

        // ============================ Canvas / background ============================
        void BuildCanvas()
        {
            var cgo = new GameObject("OtoutzCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.layer = LayerMask.NameToLayer("UI");
            _canvas = cgo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(REF_W, REF_H);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            cgo.transform.SetParent(transform, false);
            _root = (RectTransform)cgo.transform;

            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }
        }

        void BuildBackground()
        {
            // Starry artwork (149-3) cover-fits the screen and persists across the title splash and
            // every menu screen — so when the title's character fades out, this stays put.
            var sky = LoadArt("149-3");
            if (sky != null)
            {
                CoverImage("Background", _root, sky);
                // gentle dark scrim so the white menu UI keeps contrast over the bright sky
                var scrim = OtoutzUI.Image("Scrim", _root, OtoutzSprites.RoundedRect(2), OtoutzTheme.A(Color.black, 0.20f), Image.Type.Sliced);
                OtoutzUI.Stretch(scrim.rectTransform);
            }
            else
            {
                var bg = OtoutzUI.Image("Background", _root, OtoutzSprites.PageGradient(), Color.white);
                OtoutzUI.Stretch(bg.rectTransform);
                var dots = OtoutzUI.Image("Dots", _root, OtoutzSprites.DotTile(), OtoutzTheme.A(OtoutzTheme.glow, 0.22f), Image.Type.Tiled);
                OtoutzUI.Stretch(dots.rectTransform);
            }

            // bottom vignette
            var vig = OtoutzUI.Image("Vignette", _root, OtoutzSprites.Glow(), OtoutzTheme.A(Color.black, 0.45f));
            var vr = vig.rectTransform;
            vr.anchorMin = new Vector2(0.5f, 0f); vr.anchorMax = new Vector2(0.5f, 0f); vr.pivot = new Vector2(0.5f, 0.5f);
            vr.sizeDelta = new Vector2(REF_W * 2.2f, REF_H * 1.4f);
            vr.anchoredPosition = new Vector2(0, -REF_H * 0.3f);

            // sky meteors render here — above the background art, behind every screen/intro
            _meteorLayer = OtoutzUI.Rect("Meteors", _root);
            OtoutzUI.Stretch(_meteorLayer);
        }

        // Loads a PNG from Resources/OtoutzArt and wraps it in a Sprite (matches OtoutzData.MakeSprite;
        // works regardless of the texture's import type, and Resources is always included in builds).
        static Sprite LoadArt(string name)
        {
            var tex = Resources.Load<Texture2D>("OtoutzArt/" + name);
            if (tex == null) { Debug.LogWarning("[Otoutz] missing art Resources/OtoutzArt/" + name); return null; }
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        // Otoutz wordmark (Otoutz_blur) sized to a target height — replaces the procedural star+text
        // lockup wherever the logo appears.
        Image Wordmark(string name, Transform parent, float height)
        {
            if (_wordmark == null) _wordmark = LoadArt("Otoutz_blur");
            var img = OtoutzUI.Image(name, parent, _wordmark, Color.white);
            float ratio = (_wordmark != null && _wordmark.texture != null)
                ? (float)_wordmark.texture.width / _wordmark.texture.height : 8.715f;
            img.rectTransform.sizeDelta = new Vector2(height * ratio, height);
            return img;
        }

        // Full-screen "cover" fit (fills the parent, cropping overflow) via AspectRatioFitter.
        Image CoverImage(string name, Transform parent, Sprite sprite)
        {
            var img = OtoutzUI.Image(name, parent, sprite, Color.white);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            var arf = img.gameObject.AddComponent<AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            arf.aspectRatio = (sprite != null && sprite.texture != null)
                ? (float)sprite.texture.width / sprite.texture.height : 16f / 9f;
            return img;
        }

        // ============================ Sky meteors (occasional shooting stars) ============================
        IEnumerator MeteorSpawner()
        {
            yield return new WaitForSecondsRealtime(2f);
            while (true)
            {
                if (_meteorLayer != null) StartCoroutine(MeteorRoutine());
                yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(2f, 5f));
            }
        }

        IEnumerator MeteorRoutine()
        {
            // streak diagonally down across the upper sky (mostly down-left)
            bool leftward = UnityEngine.Random.value > 0.3f;
            Vector2 dir = new Vector2(leftward ? -1f : 1f, -0.62f).normalized;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            var m = OtoutzUI.Rect("Meteor", _meteorLayer);
            m.localEulerAngles = new Vector3(0, 0, ang);
            var cg = m.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            // tail: thin streak fading from transparent (far) to white (head)
            float len = UnityEngine.Random.Range(150f, 240f);
            var tail = OtoutzUI.Image("Tail", m, OtoutzSprites.RoundedRect(2), Color.white, Image.Type.Sliced);
            tail.rectTransform.pivot = new Vector2(1f, 0.5f);
            tail.rectTransform.sizeDelta = new Vector2(len, 3.5f);
            tail.rectTransform.anchoredPosition = Vector2.zero;
            OtoutzUI.AddGradient(tail, OtoutzTheme.A(Color.white, 0f), Color.white, 0f);

            // head: soft bright glow at the leading point
            var head = OtoutzUI.Image("Head", m, OtoutzSprites.Glow(), Color.white);
            head.rectTransform.sizeDelta = new Vector2(20f, 20f);

            float startX = leftward ? UnityEngine.Random.Range(120f, 980f) : UnityEngine.Random.Range(-980f, -120f);
            float startY = UnityEngine.Random.Range(220f, 520f);
            Vector2 start = new Vector2(startX, startY);
            Vector2 end = start + dir * UnityEngine.Random.Range(1300f, 1700f);
            float dur = UnityEngine.Random.Range(0.75f, 1.1f);

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                m.anchoredPosition = Vector2.Lerp(start, end, k);
                // fade in quickly, fade out toward the end
                cg.alpha = Mathf.Min(Mathf.Clamp01(k / 0.12f), Mathf.Clamp01((1f - k) / 0.35f));
                yield return null;
            }
            Destroy(m.gameObject);
        }

        // ============================ INTRO / TITLE SPLASH ============================
        void BuildIntro()
        {
            _introRoot = OtoutzUI.Rect("Intro", _root);
            OtoutzUI.Stretch(_introRoot);
            _introGroup = _introRoot.gameObject.AddComponent<CanvasGroup>();
            _introRoot.SetAsLastSibling();

            // character (149-4) shares the exact cover-fit of the background, so it overlays in place
            var charSprite = LoadArt("149-4");
            if (charSprite != null) _introChar = CoverImage("Character", _introRoot, charSprite);

            // Otoutz wordmark (Otoutz_blur) near the top centre
            var logo = Wordmark("Logo", _introRoot, 124);
            logo.rectTransform.anchorMin = logo.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            logo.rectTransform.pivot = new Vector2(0.5f, 1f);
            logo.rectTransform.anchoredPosition = new Vector2(0, -86);

            // blinking "press any key" prompt — raised so it clears the character's legs
            _pressText = OtoutzUI.Text("Press", _introRoot, "PRESS ANY KEY", 34, OtoutzTheme.ink, true, FontStyles.Bold);
            _pressText.characterSpacing = 12;
            _pressText.rectTransform.anchorMin = _pressText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _pressText.rectTransform.pivot = new Vector2(0.5f, 0f);
            _pressText.rectTransform.sizeDelta = new Vector2(800, 48);
            _pressText.rectTransform.anchoredPosition = new Vector2(0, 226);

            _pressSub = OtoutzUI.Text("PressSub", _introRoot, "아무 키나 눌러 시작", 18, OtoutzTheme.sub, false, FontStyles.Bold);
            _pressSub.characterSpacing = 8;
            _pressSub.rectTransform.anchorMin = _pressSub.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _pressSub.rectTransform.pivot = new Vector2(0.5f, 0f);
            _pressSub.rectTransform.sizeDelta = new Vector2(800, 26);
            _pressSub.rectTransform.anchoredPosition = new Vector2(0, 190);
        }

        static bool AnyStart()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
            var m = Mouse.current;
            if (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame)) return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame || gp.startButton.wasPressedThisFrame)) return true;
            return false;
        }

        // Fade the splash (character + wordmark + prompt) out while the menu fades/zooms in.
        // The background image is on _root and untouched, so it stays exactly as on the title.
        IEnumerator ExitIntro()
        {
            if (_busy) yield break;
            _busy = true; _intro = false;

            _screen = Screen.Menu;
            RefreshMenu();
            var menuRt = _screens[Screen.Menu]; var menuCg = _groups[Screen.Menu];
            menuRt.gameObject.SetActive(true);
            menuRt.SetAsLastSibling();
            _introRoot.SetAsLastSibling();   // keep the splash above the menu while it fades out
            menuCg.alpha = 0f;

            const float dur = 0.75f; float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = t / dur;
                // character fades out in place (no upward drift)
                _introGroup.alpha = 1f - Ease.OutCubic(Mathf.Clamp01(k * 1.25f));
                float mk = Mathf.Clamp01((k - 0.25f) / 0.75f);
                menuCg.alpha = Ease.OutCubic(mk);
                menuRt.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, Ease.OutBack(mk));
                yield return null;
            }
            menuCg.alpha = 1f; menuRt.localScale = Vector3.one;
            if (_introRoot != null) Destroy(_introRoot.gameObject);
            _introRoot = null; _introChar = null; _pressText = null; _pressSub = null;
            _busy = false;
        }

        RectTransform NewScreen(Screen s)
        {
            var rt = OtoutzUI.Rect(s + "Screen", _root);
            OtoutzUI.Stretch(rt);
            var cg = rt.gameObject.AddComponent<CanvasGroup>();
            _screens[s] = rt; _groups[s] = cg;
            return rt;
        }

        // ============================ Shared widgets ============================
        void BuildHintBar(Transform parent, (string k, string t)[] items)
        {
            var bar = OtoutzUI.Rect("HintBar", parent);
            bar.anchorMin = new Vector2(0.5f, 0f); bar.anchorMax = new Vector2(0.5f, 0f); bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0, 40);
            bar.sizeDelta = new Vector2(1200, 40);
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 26; layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            layout.childControlWidth = true; layout.childControlHeight = true;

            foreach (var it in items)
            {
                var cell = OtoutzUI.Rect("Hint", bar);
                var hl = cell.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.childAlignment = TextAnchor.MiddleCenter; hl.spacing = 9;
                hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
                hl.childControlWidth = true; hl.childControlHeight = true;
                var fit = cell.gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var chip = OtoutzUI.Panel("Key", cell, 8, OtoutzTheme.A(OtoutzTheme.panel, 0.5f));
                OtoutzUI.Ring(chip.transform, 8, 2, OtoutzTheme.line);
                chip.rectTransform.sizeDelta = new Vector2(0, 30);
                var le = chip.gameObject.AddComponent<LayoutElement>(); le.minWidth = 34; le.minHeight = 30;
                var kt = OtoutzUI.Text("k", chip.transform, it.k, 16, OtoutzTheme.ink, false, FontStyles.Bold);
                OtoutzUI.Stretch(kt.rectTransform, 8, 8, 0, 0);

                var lt = OtoutzUI.Text("t", cell, it.t, 16, OtoutzTheme.sub, false, FontStyles.Bold, TextAlignmentOptions.Left);
                var lle = lt.gameObject.AddComponent<LayoutElement>(); lle.preferredWidth = lt.preferredWidth + 10; lle.minHeight = 30;
            }
        }

        RectTransform BuildTopBar(Transform parent, bool selectLabel, out TextMeshProUGUI counter)
        {
            var bar = OtoutzUI.Rect("TopBar", parent);
            bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1); bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, 120); bar.anchoredPosition = Vector2.zero;

            // left: Otoutz wordmark + section label
            var name = Wordmark("Name", bar, 46);
            name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0, 0.5f);
            name.rectTransform.pivot = new Vector2(0, 0.5f);
            name.rectTransform.anchoredPosition = new Vector2(58, 14);
            var sub = OtoutzUI.Text("Sub", bar, selectLabel ? "MUSIC SELECT" : "DIFFICULTY", 13, OtoutzTheme.sub, false, FontStyles.Bold, TextAlignmentOptions.Left);
            sub.characterSpacing = 14;
            sub.rectTransform.anchorMin = sub.rectTransform.anchorMax = new Vector2(0, 0.5f);
            sub.rectTransform.pivot = new Vector2(0, 0.5f);
            sub.rectTransform.sizeDelta = new Vector2(300, 20);
            sub.rectTransform.anchoredPosition = new Vector2(88, -26);

            // right: sort pill + counter
            var pill = OtoutzUI.Panel("Sort", bar, 20, OtoutzTheme.A(OtoutzTheme.panel, 0.5f));
            OtoutzUI.Ring(pill.transform, 20, 2, OtoutzTheme.line);
            pill.rectTransform.anchorMin = pill.rectTransform.anchorMax = new Vector2(1, 0.5f);
            pill.rectTransform.pivot = new Vector2(1, 0.5f);
            pill.rectTransform.sizeDelta = new Vector2(140, 40);
            pill.rectTransform.anchoredPosition = new Vector2(-180, 0);
            var sortT = OtoutzUI.Text("SortT", pill.transform, "SORT  LEVEL ▾", 13, OtoutzTheme.ink, false, FontStyles.Bold);
            OtoutzUI.Stretch(sortT.rectTransform);

            counter = OtoutzUI.Text("Counter", bar, "01 / 01", 30, OtoutzTheme.accent, true, FontStyles.Bold, TextAlignmentOptions.Right);
            counter.rectTransform.anchorMin = counter.rectTransform.anchorMax = new Vector2(1, 0.5f);
            counter.rectTransform.pivot = new Vector2(1, 0.5f);
            counter.rectTransform.sizeDelta = new Vector2(150, 50);
            counter.rectTransform.anchoredPosition = new Vector2(-70, 0);
            return bar;
        }

        Image BackButton(Transform parent, Action onClick)
        {
            var btn = OtoutzUI.Panel("Back", parent, 16, OtoutzTheme.A(OtoutzTheme.panel, 0.5f));
            OtoutzUI.Ring(btn.transform, 16, 2, OtoutzTheme.line);
            btn.rectTransform.anchorMin = btn.rectTransform.anchorMax = new Vector2(0, 0);
            btn.rectTransform.pivot = new Vector2(0, 0);
            btn.rectTransform.sizeDelta = new Vector2(150, 52);
            btn.rectTransform.anchoredPosition = new Vector2(110, 42);
            var chev = OtoutzUI.Image("Chev", btn.transform, OtoutzSprites.Chevron(), OtoutzTheme.accent);
            chev.rectTransform.anchorMin = chev.rectTransform.anchorMax = new Vector2(0, 0.5f);
            chev.rectTransform.pivot = new Vector2(0, 0.5f);
            chev.rectTransform.sizeDelta = new Vector2(20, 20);
            chev.rectTransform.anchoredPosition = new Vector2(18, 0);
            var t = OtoutzUI.Text("T", btn.transform, "BACK", 16, OtoutzTheme.ink, false, FontStyles.Bold);
            OtoutzUI.Stretch(t.rectTransform, 44, 10, 0, 0);
            OtoutzUI.MakeClickable(btn, onClick);
            return btn;
        }

        // ============================ MENU ============================
        struct MenuRow { public Image bg; public UIGradient grad; public Image ring; public Image iconTile; public Image icon; public TextMeshProUGUI label, sub, arrow; }
        MenuRow[] _menuRows;
        readonly string[] _menuLabels = { "FREE PLAY", "SETTINGS" };
        readonly string[] _menuSubs = { "곡을 선택해 플레이", "게임 환경 설정" };

        void BuildMenu()
        {
            var root = NewScreen(Screen.Menu);

            // title block (centered, raised)
            var titleBlock = OtoutzUI.Rect("Title", root);
            titleBlock.anchoredPosition = new Vector2(0, 200);

            var glow = OtoutzUI.Glow("TitleGlow", titleBlock, OtoutzTheme.accent, 0.45f);
            glow.rectTransform.sizeDelta = new Vector2(900, 360);
            glow.rectTransform.anchoredPosition = new Vector2(0, 0);

            var word = Wordmark("Word", titleBlock, 150);
            word.rectTransform.anchoredPosition = new Vector2(0, 8);

            var subtitle = OtoutzUI.Text("Subtitle", titleBlock, "ARCADE RHYTHM", 19, OtoutzTheme.sub, false, FontStyles.Bold);
            subtitle.characterSpacing = 26;
            subtitle.rectTransform.anchoredPosition = new Vector2(0, -100);
            subtitle.rectTransform.sizeDelta = new Vector2(700, 30);

            // menu rows
            _menuRows = new MenuRow[2];
            for (int i = 0; i < 2; i++)
            {
                var row = OtoutzUI.Panel("Row" + i, root, 22, OtoutzTheme.A(OtoutzTheme.panel, 0.5f));
                row.rectTransform.sizeDelta = new Vector2(540, 88);
                row.rectTransform.anchoredPosition = new Vector2(0, -30 - i * 106);
                var grad = OtoutzUI.AddGradient(row, OtoutzTheme.accent, OtoutzTheme.accent2, 100f); grad.enabled = false;
                var ring = OtoutzUI.Ring(row.transform, 22, 2, OtoutzTheme.line);

                var tile = OtoutzUI.Panel("Icon", row.transform, 16, OtoutzTheme.A(OtoutzTheme.accent, 0.2f));
                tile.rectTransform.anchorMin = tile.rectTransform.anchorMax = new Vector2(0, 0.5f);
                tile.rectTransform.pivot = new Vector2(0, 0.5f);
                tile.rectTransform.sizeDelta = new Vector2(58, 58);
                tile.rectTransform.anchoredPosition = new Vector2(30, 0);
                var icon = OtoutzUI.Image("Glyph", tile.transform, i == 0 ? OtoutzSprites.Triangle() : OtoutzSprites.Gear(), OtoutzTheme.accent);
                icon.rectTransform.sizeDelta = new Vector2(28, 28);

                var label = OtoutzUI.Text("Label", row.transform, _menuLabels[i], 26, OtoutzTheme.ink, false, FontStyles.Bold, TextAlignmentOptions.Left);
                label.characterSpacing = 5;
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0, 0.5f);
                label.rectTransform.pivot = new Vector2(0, 0.5f);
                label.rectTransform.sizeDelta = new Vector2(380, 32);
                label.rectTransform.anchoredPosition = new Vector2(110, 10);
                var sub = OtoutzUI.Text("Sub", row.transform, _menuSubs[i], 14, OtoutzTheme.sub, false, FontStyles.Bold, TextAlignmentOptions.Left);
                sub.rectTransform.anchorMin = sub.rectTransform.anchorMax = new Vector2(0, 0.5f);
                sub.rectTransform.pivot = new Vector2(0, 0.5f);
                sub.rectTransform.sizeDelta = new Vector2(380, 20);
                sub.rectTransform.anchoredPosition = new Vector2(110, -16);
                var arrow = OtoutzUI.Text("Arrow", row.transform, "▸", 26, OtoutzTheme.ink, false, FontStyles.Bold, TextAlignmentOptions.Right);
                arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(1, 0.5f);
                arrow.rectTransform.pivot = new Vector2(1, 0.5f);
                arrow.rectTransform.sizeDelta = new Vector2(40, 40);
                arrow.rectTransform.anchoredPosition = new Vector2(-26, 0);

                int captured = i;
                OtoutzUI.MakeClickable(row, () => { _menuIndex = captured; RefreshMenu(); SelectMenu(captured); }, () => { _menuIndex = captured; RefreshMenu(); });
                _menuRows[i] = new MenuRow { bg = row, grad = grad, ring = ring, iconTile = tile, icon = icon, label = label, sub = sub, arrow = arrow };
            }

            // footer
            var ver = OtoutzUI.Text("Ver", root, "ver 1.0.0", 14, OtoutzTheme.sub, false, FontStyles.Bold, TextAlignmentOptions.Left);
            ver.rectTransform.anchorMin = ver.rectTransform.anchorMax = new Vector2(0, 0); ver.rectTransform.pivot = new Vector2(0, 0);
            ver.rectTransform.anchoredPosition = new Vector2(70, 40); ver.rectTransform.sizeDelta = new Vector2(200, 24);
            var cr = OtoutzUI.Text("Copy", root, "© roenaspace", 14, OtoutzTheme.sub, false, FontStyles.Bold, TextAlignmentOptions.Right);
            cr.rectTransform.anchorMin = cr.rectTransform.anchorMax = new Vector2(1, 0); cr.rectTransform.pivot = new Vector2(1, 0);
            cr.rectTransform.anchoredPosition = new Vector2(-70, 40); cr.rectTransform.sizeDelta = new Vector2(200, 24);

            BuildHintBar(root, new[] { ("↑ ↓", "이동"), ("Enter", "결정") });
        }

        void RefreshMenu()
        {
            if (_menuRows == null) return;
            for (int i = 0; i < _menuRows.Length; i++)
            {
                bool sel = i == _menuIndex;
                var r = _menuRows[i];
                r.grad.enabled = sel;
                r.bg.color = sel ? Color.white : OtoutzTheme.A(OtoutzTheme.panel, 0.5f);
                r.ring.enabled = !sel;
                r.bg.rectTransform.localScale = Vector3.one * (sel ? 1.03f : 1f);
                r.iconTile.color = sel ? OtoutzTheme.A(Color.white, 0.22f) : OtoutzTheme.A(OtoutzTheme.accent, 0.2f);
                r.icon.color = sel ? Color.white : OtoutzTheme.accent;
                r.label.color = sel ? Color.white : OtoutzTheme.ink;
                r.sub.color = sel ? OtoutzTheme.A(Color.white, 0.8f) : OtoutzTheme.sub;
                r.arrow.color = sel ? Color.white : OtoutzTheme.A(Color.white, 0f);
            }
        }

        void SelectMenu(int i)
        {
            if (i == 0) Go(Screen.Select);
            else Go(Screen.Settings);
        }

        // ============================ SELECT (carousel) ============================
        class Card { public RectTransform root; public Image glow; public RectTransform frame; public Image frameBg, frameRing; public RectTransform jacketHolder; public RectTransform genrePill; public TextMeshProUGUI genreText; public Image reflection;
            public Vector2 tPos; public float tScale, tRot, tAlpha; }
        List<Card> _cards = new List<Card>();
        Coroutine _selectAnim;
        RectTransform _carousel;
        TextMeshProUGUI _selCounter, _metaTitle, _metaArtist, _metaBpm;
        TextMeshProUGUI[] _metaChipLevel = new TextMeshProUGUI[4];
        Image[] _metaChipBg = new Image[4];

        void BuildSelect()
        {
            var root = NewScreen(Screen.Select);
            BuildTopBar(root, true, out _selCounter);

            _carousel = OtoutzUI.Rect("Carousel", root);
            _carousel.anchoredPosition = new Vector2(0, 90);
            _carousel.sizeDelta = new Vector2(REF_W, 460);

            float jW = 300f * cardSize / 100f;
            for (int i = 0; i < _songs.Count; i++)
            {
                var song = _songs[i];
                var cardRoot = OtoutzUI.Rect("Card" + i, _carousel);
                cardRoot.sizeDelta = new Vector2(jW, jW);

                var glow = OtoutzUI.Image("Glow", cardRoot, OtoutzSprites.Glow(), OtoutzTheme.A(OtoutzTheme.accent, 0.5f));
                glow.rectTransform.sizeDelta = new Vector2(jW + 80, jW + 80);

                var frame = OtoutzUI.Rect("Frame", cardRoot);
                frame.sizeDelta = new Vector2(jW, jW);
                var frameBg = OtoutzUI.Image("FrameBg", frame, OtoutzSprites.RoundedRect(24), OtoutzTheme.A(Color.white, 0.14f), Image.Type.Sliced);
                OtoutzUI.Stretch(frameBg.rectTransform);
                var frameRing = OtoutzUI.Image("FrameRing", frame, OtoutzSprites.RoundedBorder(24, 2), OtoutzTheme.A(Color.white, 0.6f), Image.Type.Sliced);
                OtoutzUI.Stretch(frameRing.rectTransform);

                var jacketHolder = OtoutzUI.Rect("Jacket", cardRoot);
                jacketHolder.sizeDelta = new Vector2(jW, jW);
                OtoutzJacket.Build(jacketHolder, song, jW, 22, true);

                var pill = OtoutzUI.Panel("Genre", cardRoot, 14, OtoutzTheme.A(Color.white, 0.88f));
                pill.rectTransform.anchorMin = pill.rectTransform.anchorMax = new Vector2(0, 1);
                pill.rectTransform.pivot = new Vector2(0, 1);
                pill.rectTransform.sizeDelta = new Vector2(26 + song.genre.Length * 9.5f, 28);
                pill.rectTransform.anchoredPosition = new Vector2(22, -22);
                var gt = OtoutzUI.Text("GenreT", pill.transform, song.genre, 13, OtoutzTheme.accent, false, FontStyles.Bold);
                gt.characterSpacing = 5;
                OtoutzUI.Stretch(gt.rectTransform);

                var refl = OtoutzUI.Image("Reflection", cardRoot, OtoutzSprites.Glow(), OtoutzTheme.A(OtoutzTheme.accent, 0.4f));
                refl.rectTransform.sizeDelta = new Vector2(jW * 0.7f, 50);
                refl.rectTransform.anchoredPosition = new Vector2(0, -jW * 0.5f - 26);

                int captured = i;
                OtoutzUI.MakeClickable(jacketHolder.gameObject.GetComponent<Image>() ?? AddRaycast(jacketHolder),
                    () => { if (captured == _songIndex) Go(Screen.Difficulty); else { _songIndex = captured; LayoutSelect(true); PlayPreview(); } });

                _cards.Add(new Card { root = cardRoot, glow = glow, frame = frame, frameBg = frameBg, frameRing = frameRing, jacketHolder = jacketHolder, genrePill = pill.rectTransform, genreText = gt, reflection = refl });
            }

            // side arrows
            ArrowButton(root, -1, () => Move(-1));
            ArrowButton(root, +1, () => Move(+1));

            // meta panel
            var meta = OtoutzUI.Rect("Meta", root);
            meta.anchorMin = new Vector2(0.5f, 0); meta.anchorMax = new Vector2(0.5f, 0); meta.pivot = new Vector2(0.5f, 0);
            meta.anchoredPosition = new Vector2(0, 110);
            meta.sizeDelta = new Vector2(1400, 180);
            _metaTitle = OtoutzUI.Text("Title", meta, "", 52, OtoutzTheme.ink, true, FontStyles.Bold);
            _metaTitle.rectTransform.anchorMin = new Vector2(0.5f, 1); _metaTitle.rectTransform.anchorMax = new Vector2(0.5f, 1); _metaTitle.rectTransform.pivot = new Vector2(0.5f, 1);
            _metaTitle.rectTransform.anchoredPosition = new Vector2(0, 0); _metaTitle.rectTransform.sizeDelta = new Vector2(1400, 64);
            _metaArtist = OtoutzUI.Text("Artist", meta, "", 19, OtoutzTheme.sub, false, FontStyles.Bold);
            _metaArtist.rectTransform.anchorMin = new Vector2(0.5f, 1); _metaArtist.rectTransform.anchorMax = new Vector2(0.5f, 1); _metaArtist.rectTransform.pivot = new Vector2(0.5f, 1);
            _metaArtist.rectTransform.anchoredPosition = new Vector2(0, -66); _metaArtist.rectTransform.sizeDelta = new Vector2(1400, 26);

            var chips = OtoutzUI.Rect("Chips", meta);
            chips.anchorMin = new Vector2(0.5f, 0); chips.anchorMax = new Vector2(0.5f, 0); chips.pivot = new Vector2(0.5f, 0);
            chips.anchoredPosition = new Vector2(0, 0); chips.sizeDelta = new Vector2(1000, 44);
            var hl = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter; hl.spacing = 14;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false; hl.childControlWidth = true; hl.childControlHeight = true;

            // BPM pill
            var bpmPill = OtoutzUI.Panel("Bpm", chips, 22, OtoutzTheme.A(OtoutzTheme.panel, 0.5f));
            OtoutzUI.Ring(bpmPill.transform, 22, 2, OtoutzTheme.line);
            var ble = bpmPill.gameObject.AddComponent<LayoutElement>(); ble.minWidth = 130; ble.minHeight = 44;
            var bl = OtoutzUI.Text("L", bpmPill.transform, "BPM", 13, OtoutzTheme.sub, false, FontStyles.Bold, TextAlignmentOptions.Left);
            bl.characterSpacing = 8; bl.rectTransform.anchorMin = new Vector2(0, 0); bl.rectTransform.anchorMax = new Vector2(0, 1); bl.rectTransform.pivot = new Vector2(0, 0.5f);
            bl.rectTransform.sizeDelta = new Vector2(50, 0); bl.rectTransform.anchoredPosition = new Vector2(18, 0);
            _metaBpm = OtoutzUI.Text("V", bpmPill.transform, "0", 18, OtoutzTheme.ink, true, FontStyles.Bold, TextAlignmentOptions.Right);
            _metaBpm.rectTransform.anchorMin = new Vector2(1, 0); _metaBpm.rectTransform.anchorMax = new Vector2(1, 1); _metaBpm.rectTransform.pivot = new Vector2(1, 0.5f);
            _metaBpm.rectTransform.sizeDelta = new Vector2(70, 0); _metaBpm.rectTransform.anchoredPosition = new Vector2(-16, 0);

            for (int i = 0; i < 4; i++)
            {
                var d = OtoutzTheme.Diffs[i];
                var chip = OtoutzUI.Panel("Chip" + i, chips, 22, OtoutzTheme.A(d.color, 0.18f));
                OtoutzUI.Ring(chip.transform, 22, 2, OtoutzTheme.A(d.color, 0.5f));
                var ce = chip.gameObject.AddComponent<LayoutElement>(); ce.minWidth = 150; ce.minHeight = 44;
                var star = OtoutzUI.Image("Star", chip.transform, OtoutzSprites.Star(), d.color);
                star.rectTransform.anchorMin = star.rectTransform.anchorMax = new Vector2(0, 0.5f); star.rectTransform.pivot = new Vector2(0, 0.5f);
                star.rectTransform.sizeDelta = new Vector2(14, 14); star.rectTransform.anchoredPosition = new Vector2(12, 0);
                var lab = OtoutzUI.Text("L", chip.transform, d.label, 12, d.deep, false, FontStyles.Bold, TextAlignmentOptions.Left);
                lab.characterSpacing = 4; lab.rectTransform.anchorMin = new Vector2(0, 0); lab.rectTransform.anchorMax = new Vector2(0, 1); lab.rectTransform.pivot = new Vector2(0, 0.5f);
                lab.rectTransform.sizeDelta = new Vector2(90, 0); lab.rectTransform.anchoredPosition = new Vector2(34, 0);
                var lvl = OtoutzUI.Text("V", chip.transform, "-", 18, d.deep, true, FontStyles.Bold, TextAlignmentOptions.Right);
                lvl.rectTransform.anchorMin = new Vector2(1, 0); lvl.rectTransform.anchorMax = new Vector2(1, 1); lvl.rectTransform.pivot = new Vector2(1, 0.5f);
                lvl.rectTransform.sizeDelta = new Vector2(40, 0); lvl.rectTransform.anchoredPosition = new Vector2(-16, 0);
                _metaChipBg[i] = chip; _metaChipLevel[i] = lvl;
            }

            BuildHintBar(root, new[] { ("← →", "곡 선택"), ("Enter", "난이도 선택"), ("Esc", "메뉴") });
        }

        Image AddRaycast(RectTransform rt)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0); img.raycastTarget = true;
            return img;
        }

        void ArrowButton(Transform parent, int dir, Action onClick)
        {
            var btn = OtoutzUI.Panel("Arrow" + dir, parent, 42, OtoutzTheme.A(OtoutzTheme.panel, 0.55f));
            OtoutzUI.Ring(btn.transform, 42, 2, OtoutzTheme.line);
            btn.rectTransform.anchorMin = btn.rectTransform.anchorMax = new Vector2(dir < 0 ? 0 : 1, 0.5f);
            btn.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            btn.rectTransform.sizeDelta = new Vector2(84, 84);
            btn.rectTransform.anchoredPosition = new Vector2(dir < 0 ? 90 : -90, 90);
            var chev = OtoutzUI.Image("Chev", btn.transform, OtoutzSprites.Chevron(), OtoutzTheme.accent);
            chev.rectTransform.sizeDelta = new Vector2(30, 30);
            chev.rectTransform.localScale = new Vector3(dir < 0 ? 1 : -1, 1, 1);
            OtoutzUI.MakeClickable(btn, onClick);
        }

        void RefreshSelect() { LayoutSelect(false); }

        // Recomputes each card's coverflow target; animates the slide when requested.
        void LayoutSelect(bool animate)
        {
            if (_cards.Count == 0) return;
            if (_selectAnim != null) { StopCoroutine(_selectAnim); _selectAnim = null; }

            float emph = focus / 100f;
            float jW = 300f * cardSize / 100f;
            float spacing = jW * 0.76f;
            int maxSide = 4;

            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
                int off = i - _songIndex;
                int aoff = Mathf.Abs(off);
                bool center = off == 0;
                if (aoff > maxSide) { c.root.gameObject.SetActive(false); continue; }
                c.root.gameObject.SetActive(true);

                c.tPos = new Vector2(off * spacing, 0);
                c.tScale = center ? 1f : (1f - (0.16f + 0.2f * emph)) * Mathf.Pow(0.9f, aoff - 1);
                c.tAlpha = center ? 1f : Mathf.Max(0.16f, 1f - aoff * (0.16f + 0.16f * emph));
                c.tRot = center ? 0f : -Mathf.Sign(off) * (5f + 7f * emph);

                c.root.SetSiblingIndex(center ? _cards.Count : Mathf.Max(0, _cards.Count - aoff));
                c.glow.gameObject.SetActive(center);
                c.frame.gameObject.SetActive(center);
                c.genrePill.gameObject.SetActive(center);
                c.reflection.gameObject.SetActive(center);
            }
            // ensure center is drawn last (front)
            _cards[Mathf.Clamp(_songIndex, 0, _cards.Count - 1)].root.SetAsLastSibling();

            if (animate && isActiveAndEnabled && gameObject.activeInHierarchy)
                _selectAnim = StartCoroutine(AnimateSelect());
            else
                for (int i = 0; i < _cards.Count; i++) ApplyCard(_cards[i], _cards[i].tPos, _cards[i].tScale, _cards[i].tRot, _cards[i].tAlpha);

            RefreshMeta();
            _selCounter.text = $"{(_songIndex + 1):00} / {_songs.Count:00}";
        }

        IEnumerator AnimateSelect()
        {
            // capture current state per active card
            int n = _cards.Count;
            var sPos = new Vector2[n]; var sScale = new float[n]; var sRot = new float[n]; var sAlpha = new float[n];
            for (int i = 0; i < n; i++)
            {
                var c = _cards[i];
                if (!c.root.gameObject.activeSelf) continue;
                sPos[i] = c.root.anchoredPosition;
                sScale[i] = c.root.localScale.x;
                float r = c.root.localEulerAngles.y; if (r > 180f) r -= 360f;
                sRot[i] = r;
                var cg = c.root.GetComponent<CanvasGroup>();
                sAlpha[i] = cg != null ? cg.alpha : 1f;
            }

            const float dur = 0.32f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Ease.OutCubic(t / dur);
                for (int i = 0; i < n; i++)
                {
                    var c = _cards[i];
                    if (!c.root.gameObject.activeSelf) continue;
                    ApplyCard(c,
                        Vector2.LerpUnclamped(sPos[i], c.tPos, k),
                        Mathf.LerpUnclamped(sScale[i], c.tScale, k),
                        Mathf.LerpUnclamped(sRot[i], c.tRot, k),
                        Mathf.LerpUnclamped(sAlpha[i], c.tAlpha, k));
                }
                yield return null;
            }
            for (int i = 0; i < n; i++)
            {
                var c = _cards[i];
                if (c.root.gameObject.activeSelf) ApplyCard(c, c.tPos, c.tScale, c.tRot, c.tAlpha);
            }
            _selectAnim = null;
        }

        void ApplyCard(Card c, Vector2 pos, float scale, float rot, float alpha)
        {
            c.root.anchoredPosition = pos;
            c.root.localScale = Vector3.one * scale;
            c.root.localEulerAngles = new Vector3(0, rot, 0);
            SetCardAlpha(c, alpha);
        }

        void SetCardAlpha(Card c, float a)
        {
            var cg = c.root.GetComponent<CanvasGroup>();
            if (cg == null) cg = c.root.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = a;
        }

        void RefreshMeta()
        {
            var s = _songs[_songIndex];
            _metaTitle.text = s.title;
            _metaArtist.text = s.artist;
            _metaBpm.text = Mathf.RoundToInt(s.bpm).ToString();
            for (int i = 0; i < 4; i++)
            {
                _metaChipLevel[i].text = s.LevelText(i);
                float a = s.HasDiff(i) ? 1f : 0.4f;
                var col = _metaChipBg[i].color; _metaChipBg[i].color = OtoutzTheme.A(OtoutzTheme.Diffs[i].color, s.HasDiff(i) ? 0.18f : 0.08f);
            }
        }

        void Move(int dir)
        {
            int n = Mathf.Clamp(_songIndex + dir, 0, _songs.Count - 1);
            if (n == _songIndex) return;
            _songIndex = n; LayoutSelect(true); PlayPreview();
        }

        // ============================ DIFFICULTY ============================
        struct DiffRow { public Image bg; public UIGradient grad; public Image ring; public Image starTile; public Image star; public TextMeshProUGUI label, desc, lv; public Image startBtn; public TextMeshProUGUI startText; }
        DiffRow[] _diffRows;
        RectTransform _diffJacketHolder;
        TextMeshProUGUI _diffTitle, _diffArtist, _diffBpm;
        Image _diffGlowA, _diffGlowB;

        void BuildDifficulty()
        {
            var root = NewScreen(Screen.Difficulty);
            BuildTopBar(root, false, out _);

            _diffGlowA = OtoutzUI.Image("GlowA", root, OtoutzSprites.Glow(), OtoutzTheme.A(OtoutzTheme.accent, 0.3f));
            _diffGlowA.rectTransform.sizeDelta = new Vector2(1200, 1000);
            _diffGlowA.rectTransform.anchoredPosition = new Vector2(-450, 0);
            _diffGlowB = OtoutzUI.Image("GlowB", root, OtoutzSprites.Glow(), OtoutzTheme.A(OtoutzTheme.accent2, 0.16f));
            _diffGlowB.rectTransform.sizeDelta = new Vector2(900, 700);
            _diffGlowB.rectTransform.anchoredPosition = new Vector2(560, 260);

            // left jacket column
            var left = OtoutzUI.Rect("Left", root);
            left.anchorMin = left.anchorMax = new Vector2(0, 0.5f); left.pivot = new Vector2(0, 0.5f);
            left.anchoredPosition = new Vector2(110, -20); left.sizeDelta = new Vector2(430, 700);

            var frame = OtoutzUI.Panel("Frame", left, 28, OtoutzTheme.A(Color.white, 0.12f));
            frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = new Vector2(0.5f, 1); frame.rectTransform.pivot = new Vector2(0.5f, 1);
            frame.rectTransform.sizeDelta = new Vector2(410, 410);
            frame.rectTransform.anchoredPosition = new Vector2(0, 0);
            OtoutzUI.Ring(frame.transform, 28, 2, OtoutzTheme.A(Color.white, 0.6f));
            _diffJacketHolder = OtoutzUI.Rect("Jacket", frame.transform);
            _diffJacketHolder.sizeDelta = new Vector2(386, 386);

            var genre = OtoutzUI.Panel("Genre", frame.transform, 15, OtoutzTheme.A(Color.white, 0.9f));
            genre.rectTransform.anchorMin = genre.rectTransform.anchorMax = new Vector2(0, 1); genre.rectTransform.pivot = new Vector2(0, 1);
            genre.rectTransform.anchoredPosition = new Vector2(26, -26); genre.rectTransform.sizeDelta = new Vector2(90, 30);
            _diffGenreRect = genre.rectTransform;
            _diffGenreText = OtoutzUI.Text("T", genre.transform, "", 14, OtoutzTheme.accent, false, FontStyles.Bold);
            _diffGenreText.characterSpacing = 5; OtoutzUI.Stretch(_diffGenreText.rectTransform);

            _diffTitle = OtoutzUI.Text("Title", left, "", 42, OtoutzTheme.ink, true, FontStyles.Bold);
            _diffTitle.rectTransform.anchorMin = _diffTitle.rectTransform.anchorMax = new Vector2(0.5f, 1); _diffTitle.rectTransform.pivot = new Vector2(0.5f, 1);
            _diffTitle.rectTransform.anchoredPosition = new Vector2(0, -426); _diffTitle.rectTransform.sizeDelta = new Vector2(430, 54);
            _diffArtist = OtoutzUI.Text("Artist", left, "", 18, OtoutzTheme.sub, false, FontStyles.Bold);
            _diffArtist.rectTransform.anchorMin = _diffArtist.rectTransform.anchorMax = new Vector2(0.5f, 1); _diffArtist.rectTransform.pivot = new Vector2(0.5f, 1);
            _diffArtist.rectTransform.anchoredPosition = new Vector2(0, -482); _diffArtist.rectTransform.sizeDelta = new Vector2(430, 24);
            var bpmPill = OtoutzUI.Panel("Bpm", left, 20, OtoutzTheme.A(OtoutzTheme.panel, 0.5f));
            OtoutzUI.Ring(bpmPill.transform, 20, 2, OtoutzTheme.line);
            bpmPill.rectTransform.anchorMin = bpmPill.rectTransform.anchorMax = new Vector2(0.5f, 1); bpmPill.rectTransform.pivot = new Vector2(0.5f, 1);
            bpmPill.rectTransform.anchoredPosition = new Vector2(0, -516); bpmPill.rectTransform.sizeDelta = new Vector2(140, 40);
            _diffBpm = OtoutzUI.Text("V", bpmPill.transform, "BPM 0", 14, OtoutzTheme.ink, false, FontStyles.Bold);
            OtoutzUI.Stretch(_diffBpm.rectTransform);

            // right: heading + 4 rows
            var head = OtoutzUI.Text("Head", root, "CHOOSE DIFFICULTY", 16, OtoutzTheme.sub, false, FontStyles.Bold, TextAlignmentOptions.Left);
            head.characterSpacing = 18;
            head.rectTransform.anchorMin = head.rectTransform.anchorMax = new Vector2(0, 1); head.rectTransform.pivot = new Vector2(0, 1);
            head.rectTransform.anchoredPosition = new Vector2(605, -300); head.rectTransform.sizeDelta = new Vector2(600, 24);

            _diffRows = new DiffRow[4];
            for (int i = 0; i < 4; i++)
            {
                var d = OtoutzTheme.Diffs[i];
                var row = OtoutzUI.Panel("Row" + i, root, 22, OtoutzTheme.A(OtoutzTheme.panel, 0.45f));
                row.rectTransform.anchorMin = new Vector2(0, 1); row.rectTransform.anchorMax = new Vector2(1, 1); row.rectTransform.pivot = new Vector2(0.5f, 1);
                // horizontal stretch via offsets (left inset 605, right inset 110); top edge at `top`, height 96.
                // NOTE: do not also set anchoredPosition.x — that would re-center the row and break the insets.
                float top = -340 - i * 112;
                row.rectTransform.offsetMin = new Vector2(605, top - 96);
                row.rectTransform.offsetMax = new Vector2(-110, top);
                var grad = OtoutzUI.AddGradient(row, d.color, d.deep, 100f); grad.enabled = false;
                var ring = OtoutzUI.Ring(row.transform, 22, 2, OtoutzTheme.line);

                var tile = OtoutzUI.Panel("StarTile", row.transform, 16, OtoutzTheme.A(d.color, 0.2f));
                tile.rectTransform.anchorMin = tile.rectTransform.anchorMax = new Vector2(0, 0.5f); tile.rectTransform.pivot = new Vector2(0, 0.5f);
                tile.rectTransform.sizeDelta = new Vector2(56, 56); tile.rectTransform.anchoredPosition = new Vector2(30, 0);
                var star = OtoutzUI.Image("Star", tile.transform, OtoutzSprites.Star(), d.color);
                star.rectTransform.sizeDelta = new Vector2(28, 28);

                var label = OtoutzUI.Text("Label", row.transform, d.label, 22, d.deep, false, FontStyles.Bold, TextAlignmentOptions.Left);
                label.characterSpacing = 8;
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0, 0.5f); label.rectTransform.pivot = new Vector2(0, 0.5f);
                label.rectTransform.sizeDelta = new Vector2(300, 28); label.rectTransform.anchoredPosition = new Vector2(110, 12);
                var desc = OtoutzUI.Text("Desc", row.transform, d.desc, 13, OtoutzTheme.sub, false, FontStyles.Bold, TextAlignmentOptions.Left);
                desc.rectTransform.anchorMin = desc.rectTransform.anchorMax = new Vector2(0, 0.5f); desc.rectTransform.pivot = new Vector2(0, 0.5f);
                desc.rectTransform.sizeDelta = new Vector2(300, 20); desc.rectTransform.anchoredPosition = new Vector2(110, -12);

                var lv = OtoutzUI.Text("LV", row.transform, "LV -", 52, d.deep, true, FontStyles.Bold, TextAlignmentOptions.Right);
                lv.rectTransform.anchorMin = lv.rectTransform.anchorMax = new Vector2(1, 0.5f); lv.rectTransform.pivot = new Vector2(1, 0.5f);
                lv.rectTransform.sizeDelta = new Vector2(220, 60); lv.rectTransform.anchoredPosition = new Vector2(-200, 0);

                var startBtn = OtoutzUI.Panel("Start", row.transform, 14, Color.white);
                startBtn.rectTransform.anchorMin = startBtn.rectTransform.anchorMax = new Vector2(1, 0.5f); startBtn.rectTransform.pivot = new Vector2(1, 0.5f);
                startBtn.rectTransform.sizeDelta = new Vector2(140, 52); startBtn.rectTransform.anchoredPosition = new Vector2(-24, 0);
                var startText = OtoutzUI.Text("T", startBtn.transform, "START ▸", 17, d.deep, false, FontStyles.Bold);
                startText.characterSpacing = 4; OtoutzUI.Stretch(startText.rectTransform);
                startBtn.gameObject.SetActive(false);

                int captured = i;
                OtoutzUI.MakeClickable(row, () => { if (_diffIndex == captured) StartGame(); else { _diffIndex = captured; RefreshDifficulty(true); } });
                OtoutzUI.MakeClickable(startBtn, StartGame);
                _diffRows[i] = new DiffRow { bg = row, grad = grad, ring = ring, starTile = tile, star = star, label = label, desc = desc, lv = lv, startBtn = startBtn, startText = startText };
            }

            BackButton(root, () => Go(Screen.Select));
            BuildHintBar(root, new[] { ("↑ ↓", "난이도 선택"), ("Enter", "시작"), ("Esc", "곡 변경") });
        }
        TextMeshProUGUI _diffGenreText;
        RectTransform _diffGenreRect;

        Coroutine _diffAnim;
        void RefreshDifficulty() { RefreshDifficulty(false); }
        void RefreshDifficulty(bool animate)
        {
            if (_diffRows == null) return;
            var s = _songs[_songIndex];
            // clamp diffIndex to an available difficulty
            if (!s.HasDiff(_diffIndex))
            {
                int found = -1;
                for (int i = 3; i >= 0; i--) if (s.HasDiff(i)) { found = i; break; }
                if (found >= 0) _diffIndex = found;
            }

            foreach (Transform c in _diffJacketHolder) Destroy(c.gameObject);
            OtoutzJacket.Build(_diffJacketHolder, s, 386, 20, true);
            _diffGenreText.text = s.genre;
            if (_diffGenreRect != null) _diffGenreRect.sizeDelta = new Vector2(28 + s.genre.Length * 10f, 30);
            _diffTitle.text = s.title;
            _diffArtist.text = s.artist;
            _diffBpm.text = $"BPM {Mathf.RoundToInt(s.bpm)}";

            var deep = OtoutzTheme.Diffs[Mathf.Clamp(_diffIndex, 0, 3)].deep;
            _diffGlowA.color = OtoutzTheme.A(s.artB, 0.3f);
            _diffGlowB.color = OtoutzTheme.A(s.artA, 0.16f);

            for (int i = 0; i < 4; i++)
            {
                var d = OtoutzTheme.Diffs[i];
                var r = _diffRows[i];
                bool sel = i == _diffIndex;
                bool has = s.HasDiff(i);
                r.grad.enabled = sel;
                r.bg.color = sel ? Color.white : OtoutzTheme.A(OtoutzTheme.panel, 0.45f);
                r.ring.enabled = !sel;
                r.starTile.color = sel ? OtoutzTheme.A(Color.white, 0.22f) : OtoutzTheme.A(d.color, 0.2f);
                r.star.color = sel ? Color.white : d.color;
                r.label.color = sel ? Color.white : d.deep;
                r.desc.color = sel ? OtoutzTheme.A(Color.white, 0.8f) : OtoutzTheme.sub;
                r.lv.color = sel ? Color.white : d.deep;
                r.lv.text = $"<size=18>LV</size> {s.LevelText(i)}";
                r.startBtn.gameObject.SetActive(sel && has);
                r.startText.color = d.deep;
                r.bg.color = new Color(r.bg.color.r, r.bg.color.g, r.bg.color.b, has ? r.bg.color.a : r.bg.color.a * 0.6f);
            }

            if (_diffAnim != null) { StopCoroutine(_diffAnim); _diffAnim = null; }
            if (animate && isActiveAndEnabled && gameObject.activeInHierarchy)
                _diffAnim = StartCoroutine(AnimateDiff());
            else
                for (int i = 0; i < 4; i++) _diffRows[i].bg.rectTransform.localScale = Vector3.one * (i == _diffIndex ? 1.025f : 1f);
        }

        IEnumerator AnimateDiff()
        {
            var from = new float[4];
            for (int i = 0; i < 4; i++) from[i] = _diffRows[i].bg.rectTransform.localScale.x;
            const float dur = 0.16f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Ease.OutCubic(t / dur);
                for (int i = 0; i < 4; i++)
                {
                    float target = i == _diffIndex ? 1.025f : 1f;
                    _diffRows[i].bg.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(from[i], target, k);
                }
                yield return null;
            }
            for (int i = 0; i < 4; i++) _diffRows[i].bg.rectTransform.localScale = Vector3.one * (i == _diffIndex ? 1.025f : 1f);
            _diffAnim = null;
        }

        // ============================ SETTINGS ============================
        struct SetRow { public Image bg; public Image ring; public RectTransform fill; public TextMeshProUGUI label, sub, value; }
        SetRow[] _setRows;
        readonly string[] _setLabels = { "NOTE SPEED", "JUDGE OFFSET", "MASTER VOLUME", "SE VOLUME" };
        readonly string[] _setSubs = { "노트 낙하 속도", "판정 타이밍 보정", "전체 음량", "효과음 음량" };
        readonly float[] _setMin = { 1, -100, 0, 0 };
        readonly float[] _setMax = { 10, 100, 100, 100 };
        readonly float[] _setStep = { 0.5f, 5, 5, 5 };

        void BuildSettings()
        {
            var root = NewScreen(Screen.Settings);

            var titleGlow = OtoutzUI.Glow("TitleGlow", root, OtoutzTheme.accent, 0.4f);
            titleGlow.rectTransform.sizeDelta = new Vector2(500, 240);
            titleGlow.rectTransform.anchorMin = titleGlow.rectTransform.anchorMax = new Vector2(0.5f, 1); titleGlow.rectTransform.pivot = new Vector2(0.5f, 1);
            titleGlow.rectTransform.anchoredPosition = new Vector2(0, -120);
            var title = OtoutzUI.Text("Title", root, "SETTINGS", 60, OtoutzTheme.ink, true, FontStyles.Bold);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1); title.rectTransform.pivot = new Vector2(0.5f, 1);
            title.rectTransform.anchoredPosition = new Vector2(0, -120); title.rectTransform.sizeDelta = new Vector2(800, 72);
            var sub = OtoutzUI.Text("Sub", root, "게임 환경 설정", 16, OtoutzTheme.sub, false, FontStyles.Bold);
            sub.characterSpacing = 18;
            sub.rectTransform.anchorMin = sub.rectTransform.anchorMax = new Vector2(0.5f, 1); sub.rectTransform.pivot = new Vector2(0.5f, 1);
            sub.rectTransform.anchoredPosition = new Vector2(0, -196); sub.rectTransform.sizeDelta = new Vector2(800, 24);

            _setRows = new SetRow[4];
            for (int i = 0; i < 4; i++)
            {
                var row = OtoutzUI.Panel("Row" + i, root, 20, OtoutzTheme.A(OtoutzTheme.panel, 0.4f));
                row.rectTransform.anchorMin = row.rectTransform.anchorMax = new Vector2(0.5f, 1); row.rectTransform.pivot = new Vector2(0.5f, 1);
                row.rectTransform.sizeDelta = new Vector2(920, 88);
                row.rectTransform.anchoredPosition = new Vector2(0, -320 - i * 106);
                var ring = OtoutzUI.Ring(row.transform, 20, 2, OtoutzTheme.line);

                var label = OtoutzUI.Text("Label", row.transform, _setLabels[i], 21, OtoutzTheme.ink, false, FontStyles.Bold, TextAlignmentOptions.Left);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0, 0.5f); label.rectTransform.pivot = new Vector2(0, 0.5f);
                label.rectTransform.sizeDelta = new Vector2(280, 28); label.rectTransform.anchoredPosition = new Vector2(30, 12);
                var sub2 = OtoutzUI.Text("Sub", row.transform, _setSubs[i], 13, OtoutzTheme.sub, false, FontStyles.Bold, TextAlignmentOptions.Left);
                sub2.rectTransform.anchorMin = sub2.rectTransform.anchorMax = new Vector2(0, 0.5f); sub2.rectTransform.pivot = new Vector2(0, 0.5f);
                sub2.rectTransform.sizeDelta = new Vector2(280, 20); sub2.rectTransform.anchoredPosition = new Vector2(30, -14);

                // track
                var track = OtoutzUI.Panel("Track", row.transform, 6, OtoutzTheme.A(OtoutzTheme.ink, 0.14f));
                track.rectTransform.anchorMin = new Vector2(0, 0.5f); track.rectTransform.anchorMax = new Vector2(0, 0.5f); track.rectTransform.pivot = new Vector2(0, 0.5f);
                track.rectTransform.sizeDelta = new Vector2(420, 12); track.rectTransform.anchoredPosition = new Vector2(360, 0);
                var fill = OtoutzUI.Panel("Fill", track.transform, 6, Color.white);
                fill.rectTransform.anchorMin = new Vector2(0, 0.5f); fill.rectTransform.anchorMax = new Vector2(0, 0.5f); fill.rectTransform.pivot = new Vector2(0, 0.5f);
                fill.rectTransform.sizeDelta = new Vector2(210, 12); fill.rectTransform.anchoredPosition = Vector2.zero;
                OtoutzUI.AddGradient(fill, OtoutzTheme.accent, OtoutzTheme.accent2, 0f);

                // adjust buttons
                int ci = i;
                var dec = AdjBtn(row.transform, "◂", new Vector2(330, 0), () => Adjust(ci, -1));
                var inc = AdjBtn(row.transform, "▸", new Vector2(790, 0), () => Adjust(ci, +1));

                var value = OtoutzUI.Text("Value", row.transform, "", 30, OtoutzTheme.ink, true, FontStyles.Bold, TextAlignmentOptions.Right);
                value.rectTransform.anchorMin = value.rectTransform.anchorMax = new Vector2(1, 0.5f); value.rectTransform.pivot = new Vector2(1, 0.5f);
                value.rectTransform.sizeDelta = new Vector2(110, 40); value.rectTransform.anchoredPosition = new Vector2(-24, 0);

                int hover = i;
                OtoutzUI.MakeClickable(row, () => { _rowIndex = hover; RefreshSettings(); }, () => { _rowIndex = hover; RefreshSettings(); });
                _setRows[i] = new SetRow { bg = row, ring = ring, fill = fill.rectTransform, label = label, sub = sub2, value = value };
            }

            BackButton(root, () => Go(Screen.Menu));
            BuildHintBar(root, new[] { ("↑ ↓", "항목"), ("← →", "값 조절"), ("Esc", "메뉴") });
        }

        Image AdjBtn(Transform parent, string glyph, Vector2 pos, Action onClick)
        {
            var b = OtoutzUI.Panel("Adj", parent, 12, OtoutzTheme.A(OtoutzTheme.ink, 0.12f));
            b.rectTransform.anchorMin = b.rectTransform.anchorMax = new Vector2(0, 0.5f); b.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            b.rectTransform.sizeDelta = new Vector2(40, 40); b.rectTransform.anchoredPosition = pos;
            var t = OtoutzUI.Text("T", b.transform, glyph, 18, OtoutzTheme.accent, false, FontStyles.Bold);
            OtoutzUI.Stretch(t.rectTransform);
            OtoutzUI.MakeClickable(b, onClick);
            return b;
        }

        float GetSet(int i) { switch (i) { case 0: return _setSpeed; case 1: return _setOffset; case 2: return _setVolume; default: return _setSe; } }
        void SetSet(int i, float v) { switch (i) { case 0: _setSpeed = v; break; case 1: _setOffset = Mathf.RoundToInt(v); break; case 2: _setVolume = Mathf.RoundToInt(v); break; default: _setSe = Mathf.RoundToInt(v); break; } }

        void Adjust(int i, int dir)
        {
            float v = GetSet(i) + dir * _setStep[i];
            v = Mathf.Clamp(Mathf.Round(v / _setStep[i]) * _setStep[i], _setMin[i], _setMax[i]);
            SetSet(i, v);
            RefreshSettings();
        }

        void RefreshSettings()
        {
            if (_setRows == null) return;
            for (int i = 0; i < 4; i++)
            {
                var r = _setRows[i];
                bool sel = i == _rowIndex;
                r.bg.color = OtoutzTheme.A(OtoutzTheme.panel, sel ? 0.62f : 0.4f);
                r.ring.color = sel ? OtoutzTheme.A(OtoutzTheme.accent, 0.55f) : OtoutzTheme.line;
                r.value.color = sel ? OtoutzTheme.accent : OtoutzTheme.ink;

                float v = GetSet(i);
                float pct = (v - _setMin[i]) / (_setMax[i] - _setMin[i]);
                r.fill.sizeDelta = new Vector2(420f * Mathf.Clamp01(pct), 12);

                string disp;
                if (i == 0) disp = (Mathf.Approximately(v, Mathf.Round(v)) ? ((int)v).ToString() : v.ToString("0.0"));
                else if (i == 1) disp = (v > 0 ? "+" : "") + (int)v + "ms";
                else disp = (int)v + "%";
                r.value.text = disp;
            }
        }

        // ============================ Transitions / state ============================
        int Depth(Screen s) { switch (s) { case Screen.Menu: return 0; case Screen.Select: return 1; case Screen.Settings: return 1; default: return 2; } }

        void Go(Screen target)
        {
            if (_busy || target == _screen) return;
            string cls;
            if (target == Screen.Difficulty) cls = "zoom";
            else if (_screen == Screen.Difficulty) cls = "zoomout";
            else cls = Depth(target) >= Depth(_screen) ? "fwd" : "back";

            if (_screen == Screen.Settings && target == Screen.Menu) PushSettings();

            var prev = _screen;
            _screen = target;
            // leaving song-select for anywhere but the difficulty screen stops the song preview
            if (prev == Screen.Select && target != Screen.Difficulty) StopPreview();
            // entering song-select: (re)start the preview, but NOT when coming back from the
            // difficulty screen — the same song's preview kept playing there, so let it continue
            if (target == Screen.Select) { RefreshSelect(); if (prev != Screen.Difficulty) PlayPreview(); }
            if (target == Screen.Difficulty) RefreshDifficulty();
            if (target == Screen.Settings) { _rowIndex = 0; RefreshSettings(); }
            if (target == Screen.Menu) RefreshMenu();

            StartCoroutine(Transition(prev, target, cls));
        }

        void ShowOnly(Screen s, bool instant)
        {
            foreach (var kv in _screens)
            {
                bool on = kv.Key == s;
                kv.Value.gameObject.SetActive(on);
                _groups[kv.Key].alpha = on ? 1f : 0f;
            }
        }

        IEnumerator Transition(Screen from, Screen to, string cls)
        {
            _busy = true;
            if (_screens.TryGetValue(from, out var fr)) fr.gameObject.SetActive(false);
            var rt = _screens[to]; var cg = _groups[to];
            rt.gameObject.SetActive(true);
            rt.SetAsLastSibling();

            float dur = (cls == "zoom" || cls == "zoomout") ? 0.42f : 0.40f;
            Vector2 fromPos = Vector2.zero; float fromScale = 0.99f;
            switch (cls)
            {
                case "fwd": fromPos = new Vector2(64, 0); break;
                case "back": fromPos = new Vector2(-64, 0); break;
                case "zoom": fromScale = 0.93f; break;
                case "zoomout": fromScale = 1.07f; break;
            }

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Ease.OutBack(t / dur);
                cg.alpha = Mathf.Lerp(0.2f, 1f, Ease.OutCubic(t / dur));
                rt.anchoredPosition = Vector2.Lerp(fromPos, Vector2.zero, k);
                rt.localScale = Vector3.one * Mathf.Lerp(fromScale, 1f, k);
                yield return null;
            }
            cg.alpha = 1f; rt.anchoredPosition = Vector2.zero; rt.localScale = Vector3.one;
            _busy = false;
        }

        // ============================ Input ============================
        void Update()
        {
            if (_intro)
            {
                if (_busy) return;
                float a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f));
                if (_pressText != null) _pressText.alpha = a;
                if (_pressSub != null) _pressSub.alpha = a;
                // gentle floating: the character drifts up/down very slowly with a faint sway + tilt
                if (_introChar != null)
                {
                    float ti = Time.unscaledTime;
                    _introChar.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(ti * 0.45f) * 5f, Mathf.Sin(ti * 0.7f) * 9f);
                    _introChar.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(ti * 0.5f) * 0.6f);
                }
                if (AnyStart()) StartCoroutine(ExitIntro());
                return;
            }

            var kb = Keyboard.current;
            if (kb == null || _busy) return;

            switch (_screen)
            {
                case Screen.Menu:
                    if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) { _menuIndex = (_menuIndex + 1) % 2; RefreshMenu(); }
                    else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) { _menuIndex = (_menuIndex + 1) % 2; RefreshMenu(); } // 2 items: toggle
                    else if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) SelectMenu(_menuIndex);
                    break;
                case Screen.Select:
                    if (kb.leftArrowKey.wasPressedThisFrame) Move(-1);
                    else if (kb.rightArrowKey.wasPressedThisFrame) Move(1);
                    else if (kb.enterKey.wasPressedThisFrame) Go(Screen.Difficulty);
                    else if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) Go(Screen.Menu);
                    else WheelScroll();
                    break;
                case Screen.Difficulty:
                    if (kb.upArrowKey.wasPressedThisFrame) MoveDiff(-1);
                    else if (kb.downArrowKey.wasPressedThisFrame) MoveDiff(1);
                    else if (kb.enterKey.wasPressedThisFrame) StartGame();
                    else if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) Go(Screen.Select);
                    break;
                case Screen.Settings:
                    if (kb.upArrowKey.wasPressedThisFrame) { _rowIndex = (_rowIndex + 3) % 4; RefreshSettings(); }
                    else if (kb.downArrowKey.wasPressedThisFrame) { _rowIndex = (_rowIndex + 1) % 4; RefreshSettings(); }
                    else if (kb.leftArrowKey.wasPressedThisFrame) Adjust(_rowIndex, -1);
                    else if (kb.rightArrowKey.wasPressedThisFrame) Adjust(_rowIndex, 1);
                    else if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Go(Screen.Menu);
                    break;
            }
        }

        float _wheelLock;
        void WheelScroll()
        {
            var m = Mouse.current; if (m == null) return;
            if (Time.unscaledTime < _wheelLock) return;
            float dy = m.scroll.ReadValue().y;
            if (Mathf.Abs(dy) < 0.01f) return;
            Move(dy > 0 ? -1 : 1);
            _wheelLock = Time.unscaledTime + 0.18f;
        }

        void MoveDiff(int dir)
        {
            var s = _songs[_songIndex];
            int i = _diffIndex;
            for (int step = 0; step < 4; step++)
            {
                i = Mathf.Clamp(i + dir, 0, 3);
                if (s.HasDiff(i)) { _diffIndex = i; RefreshDifficulty(true); return; }
                if (i == 0 || i == 3) break;
            }
        }

        // ============================ Launch / preview ============================
        void StartGame()
        {
            if (_busy) return;
            var s = _songs[_songIndex];
            if (!s.HasDiff(_diffIndex)) return;
            StartCoroutine(StartRoutine(s, _diffIndex));
        }

        IEnumerator StartRoutine(OtoutzSong s, int diff)
        {
            _busy = true;
            StopPreview();

            // prep the launch data up front so the load is ready the moment we go black
            _lastSongIndex = _songIndex; _lastDiffIndex = diff;
            OtoutzResultData.SetSong(s, diff);
            if (_sm != null)
            {
                _sm.SetFileName(s.filePaths[diff]);
                _sm.SetEventName(s.eventName);
                _sm.SetSongTitle(s.title);
                _sm.SetSongArtist(s.artist);
                _sm.Info = s.infos[diff];
            }

            var overlay = OtoutzUI.Image("Flash", _root, OtoutzSprites.RoundedRect(2), OtoutzTheme.A(Color.white, 0f), Image.Type.Sliced);
            OtoutzUI.Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;
            overlay.transform.SetAsLastSibling();

            // 1) burst of light — a near-instant bright white flash
            float t = 0f; const float flashIn = 0.05f;
            while (t < flashIn)
            {
                t += Time.unscaledDeltaTime;
                overlay.color = OtoutzTheme.A(Color.white, Ease.OutCubic(t / flashIn));
                yield return null;
            }
            overlay.color = Color.white;
            yield return new WaitForSecondsRealtime(0.03f);   // brief hold at peak white

            // 2) blackout — fade the flash down into solid black
            t = 0f; const float toBlack = 0.3f;
            while (t < toBlack)
            {
                t += Time.unscaledDeltaTime;
                overlay.color = Color.Lerp(Color.white, Color.black, Ease.OutCubic(t / toBlack));
                yield return null;
            }
            overlay.color = Color.black;
            yield return null;   // guarantee one fully-black frame before the scene swaps

            // 3) load InGame — its OtoutzFade reveals the scene from black, so black→black→fade-in is seamless
            SceneManager.LoadSceneAsync("InGame");
        }

        // ---- FMOD preview (guarded; harmless if events are missing) ----
        FMOD.Studio.EventInstance _preview;
        bool _previewValid;
        Coroutine _previewCo;

        void PlayPreview()
        {
            if (_screen != Screen.Select) return;
            if (_previewCo != null) StopCoroutine(_previewCo);
            StopPreview();
            _previewCo = StartCoroutine(PreviewRoutine(_songs[_songIndex]));
        }

        IEnumerator PreviewRoutine(OtoutzSong s)
        {
            yield return new WaitForSecondsRealtime(0.35f);
            if (_screen != Screen.Select) yield break;   // left song-select during the delay
            try
            {
                _preview = FMODUnity.RuntimeManager.CreateInstance($"event:/{s.eventName}");
                _preview.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
                float vol = _sm != null ? (_sm.settings.musicVolume / 10f) : 0.6f;
                _preview.setVolume(0.5f * vol);
                _preview.setTimelinePosition(s.previewStart);
                _preview.start();
                _previewValid = true;
            }
            catch { _previewValid = false; }
        }

        void StopPreview()
        {
            if (_previewCo != null) { StopCoroutine(_previewCo); _previewCo = null; }   // cancel a pending (delayed) start too
            if (_previewValid)
            {
                try { _preview.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT); _preview.release(); } catch { }
                _previewValid = false;
            }
        }

        void OnDisable() { StopPreview(); }
    }
}
