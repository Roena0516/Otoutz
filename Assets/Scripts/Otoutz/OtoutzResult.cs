using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Otoutz
{
    /// <summary>
    /// Otoutz post-play Result screen, built procedurally in uGUI to match the design handoff
    /// (`screens/05-result.png`). Reads the finished play from <see cref="OtoutzResultData"/>,
    /// plays the staggered reveal + count-up, and wires R / Enter / Esc.
    /// Lives on a single GameObject in the Result scene (no serialized references needed).
    /// </summary>
    public class OtoutzResult : MonoBehaviour
    {
        const float REF_W = 1920f, REF_H = 1080f;

        struct RankTier { public float min; public string rank; public Color a, b, glow;
            public RankTier(float m, string r, string ca, string cb, string cg) { min = m; rank = r; a = OtoutzTheme.Hex(ca); b = OtoutzTheme.Hex(cb); glow = OtoutzTheme.Hex(cg); } }

        static readonly RankTier[] Ranks =
        {
            new RankTier(101f,  "SSS+", "#fff0a8", "#f5a623", "#ffcf5a"),
            new RankTier(100f,  "SSS",  "#ffe48a", "#f5b62e", "#ffcf5a"),
            new RankTier(99.5f, "SS+",  "#bfeaff", "#56d0ff", "#7fd9ff"),
            new RankTier(99f,   "SS",   "#bfeaff", "#56d0ff", "#7fd9ff"),
            new RankTier(98f,   "S+",   "#ffc4e6", "#ff6ec7", "#ff8ed6"),
            new RankTier(97f,   "S",    "#ffc4e6", "#ff6ec7", "#ff8ed6"),
            new RankTier(94f,   "AAA",  "#d6c8ff", "#9b6ef3", "#b69bff"),
            new RankTier(90f,   "AA",   "#cdbfff", "#8a6eff", "#a98aff"),
            new RankTier(80f,   "A",    "#c8d0e0", "#8a93a8", "#aab2c2"),
            new RankTier(0f,    "B",    "#c8d0e0", "#8a93a8", "#aab2c2"),
        };

        static RankTier RankFor(float acc)
        {
            for (int i = 0; i < Ranks.Length; i++) if (acc >= Ranks[i].min) return Ranks[i];
            return Ranks[Ranks.Length - 1];
        }

        // Names + colours match the in-game judgement sprites (CriticalBreak/Break/Hit/Miss),
        // index-aligned with JudgementManager.judgeCount keys.
        struct Judge { public string label; public Color color; public Judge(string l, string c) { label = l; color = OtoutzTheme.Hex(c); } }
        static readonly Judge[] Judges =
        {
            new Judge("CRITICAL BREAK", "#aed136"),
            new Judge("BREAK",          "#d8a32a"),
            new Judge("HIT",            "#8fb4d6"),
            new Judge("MISS",           "#cf4636"),
        };

        Canvas _canvas;
        RectTransform _root;
        bool _busy = true;

        // animated targets
        TextMeshProUGUI _scoreText, _accText, _comboText;
        TextMeshProUGUI[] _judgeCounts = new TextMeshProUGUI[4];
        RectTransform[] _judgeFills = new RectTransform[4];
        float[] _judgeFillPct = new float[4];

        int _score, _total, _maxCombo;
        int[] _judgeVals = new int[4];
        float _acc;

        void Awake()
        {
            Application.runInBackground = true;
            Time.timeScale = 1f;

            // The InGame GameManager DontDestroyOnLoad's itself before loading this scene; its
            // Update() would hijack our Esc/F5. Its job is done, so retire it.
            if (GameManager.Instance != null) Destroy(GameManager.Instance.gameObject);

            OtoutzUI.Display = Resources.Load<TMP_FontAsset>("OtoutzFonts/Display");
            OtoutzUI.Body = Resources.Load<TMP_FontAsset>("OtoutzFonts/Body");
            if (OtoutzUI.Display == null) OtoutzUI.Display = TMP_Settings.defaultFontAsset;
            if (OtoutzUI.Body == null) OtoutzUI.Body = OtoutzUI.Display;

            OtoutzResultData.FillDemoIfEmpty();

            BuildCanvas();
            BuildBackground();
            BuildContent();

            StartCoroutine(Intro());
        }

        // ============================ Canvas / background ============================
        void BuildCanvas()
        {
            var cgo = new GameObject("OtoutzResultCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        void BuildBackground()
        {
            var bg = OtoutzUI.Image("Background", _root, OtoutzSprites.PageGradient(), Color.white);
            OtoutzUI.Stretch(bg.rectTransform);
            var dots = OtoutzUI.Image("Dots", _root, OtoutzSprites.DotTile(), OtoutzTheme.A(OtoutzTheme.glow, 0.22f), Image.Type.Tiled);
            OtoutzUI.Stretch(dots.rectTransform);

            var rank = RankFor(OtoutzResultData.acc);
            // song-tinted ambient glow (lower-left) + rank-tinted glow (upper-right)
            var g1 = OtoutzUI.Image("GlowSong", _root, OtoutzSprites.Glow(), OtoutzTheme.A(OtoutzResultData.artB, 0.26f));
            g1.rectTransform.sizeDelta = new Vector2(1500, 1100);
            g1.rectTransform.anchoredPosition = new Vector2(-540, 40);
            var g2 = OtoutzUI.Image("GlowRank", _root, OtoutzSprites.Glow(), OtoutzTheme.A(rank.glow, 0.2f));
            g2.rectTransform.sizeDelta = new Vector2(1100, 900);
            g2.rectTransform.anchoredPosition = new Vector2(640, 300);

            var vig = OtoutzUI.Image("Vignette", _root, OtoutzSprites.Glow(), OtoutzTheme.A(Color.black, 0.45f));
            vig.rectTransform.sizeDelta = new Vector2(REF_W * 2.2f, REF_H * 1.4f);
            vig.rectTransform.anchoredPosition = new Vector2(0, -REF_H * 0.3f);
        }

        // ============================ Content ============================
        CanvasGroup _leftGroup, _scoreGroup, _judgeGroup, _rankGroup, _clearGroup;
        RectTransform _leftRT, _scoreRT, _judgeRT, _rankRT, _clearRT;

        void BuildContent()
        {
            var d = OtoutzTheme.Diffs[Mathf.Clamp(OtoutzResultData.diffIndex, 0, 3)];
            var rank = RankFor(OtoutzResultData.acc);

            // eyebrow "RESULT"
            var eyebrow = OtoutzUI.Text("Eyebrow", _root, "RESULT", 16, OtoutzTheme.sub, false, FontStyles.Bold);
            eyebrow.characterSpacing = 26;
            eyebrow.rectTransform.anchorMin = eyebrow.rectTransform.anchorMax = new Vector2(0.5f, 1);
            eyebrow.rectTransform.pivot = new Vector2(0.5f, 1);
            eyebrow.rectTransform.sizeDelta = new Vector2(600, 24);
            eyebrow.rectTransform.anchoredPosition = new Vector2(0, -64);

            // content area: inset 92 L/R, 138 top, 150 bottom
            var content = OtoutzUI.Rect("Content", _root);
            OtoutzUI.Stretch(content, 92, 92, 138, 150);

            BuildLeft(content, d);
            BuildRight(content, d, rank);

            BuildButtons();
            BuildHintBar();
        }

        // ---- LEFT: jacket + song meta + clear banner ----
        void BuildLeft(RectTransform content, OtoutzTheme.Diff d)
        {
            var left = OtoutzUI.Rect("Left", content);
            left.anchorMin = left.anchorMax = new Vector2(0, 1); left.pivot = new Vector2(0, 1);
            left.anchoredPosition = new Vector2(0, 0); left.sizeDelta = new Vector2(540, 792);
            _leftRT = left; _leftGroup = left.gameObject.AddComponent<CanvasGroup>();

            // framed jacket (356 jacket + 11 pad => 378 box)
            var frame = OtoutzUI.Panel("Frame", left, 26, OtoutzTheme.A(Color.white, 0.12f));
            frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = new Vector2(0.5f, 1);
            frame.rectTransform.pivot = new Vector2(0.5f, 1);
            frame.rectTransform.sizeDelta = new Vector2(378, 378);
            frame.rectTransform.anchoredPosition = new Vector2(0, 0);
            OtoutzUI.Ring(frame.transform, 26, 2, OtoutzTheme.A(Color.white, 0.6f));
            var glow = OtoutzUI.Image("FrameGlow", left, OtoutzSprites.Glow(), OtoutzTheme.A(OtoutzResultData.artB, 0.4f));
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0.5f, 1);
            glow.rectTransform.pivot = new Vector2(0.5f, 1);
            glow.rectTransform.sizeDelta = new Vector2(460, 460);
            glow.rectTransform.anchoredPosition = new Vector2(0, 40);
            glow.transform.SetAsFirstSibling();

            var jacket = OtoutzUI.Rect("Jacket", frame.transform);
            jacket.sizeDelta = new Vector2(356, 356);
            OtoutzJacket.Build(jacket, DisplaySong(), 356, 18, true);

            var genre = OtoutzUI.Panel("Genre", frame.transform, 14, OtoutzTheme.A(Color.white, 0.9f));
            genre.rectTransform.anchorMin = genre.rectTransform.anchorMax = new Vector2(0, 1);
            genre.rectTransform.pivot = new Vector2(0, 1);
            genre.rectTransform.anchoredPosition = new Vector2(24, -24);
            genre.rectTransform.sizeDelta = new Vector2(28 + OtoutzResultData.genre.Length * 10f, 30);
            var gt = OtoutzUI.Text("T", genre.transform, OtoutzResultData.genre, 13, OtoutzTheme.accent, false, FontStyles.Bold);
            gt.characterSpacing = 5; OtoutzUI.Stretch(gt.rectTransform);

            var title = OtoutzUI.Text("Title", left, OtoutzResultData.title, 40, OtoutzTheme.ink, true, FontStyles.Bold);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1);
            title.rectTransform.pivot = new Vector2(0.5f, 1);
            title.rectTransform.anchoredPosition = new Vector2(0, -402); title.rectTransform.sizeDelta = new Vector2(540, 50);
            var artist = OtoutzUI.Text("Artist", left, OtoutzResultData.artist, 17, OtoutzTheme.sub, false, FontStyles.Bold);
            artist.rectTransform.anchorMin = artist.rectTransform.anchorMax = new Vector2(0.5f, 1);
            artist.rectTransform.pivot = new Vector2(0.5f, 1);
            artist.rectTransform.anchoredPosition = new Vector2(0, -456); artist.rectTransform.sizeDelta = new Vector2(540, 24);

            // diff chip + BPM pill row
            var chips = OtoutzUI.Rect("Chips", left);
            chips.anchorMin = chips.anchorMax = new Vector2(0.5f, 1); chips.pivot = new Vector2(0.5f, 1);
            chips.anchoredPosition = new Vector2(0, -494); chips.sizeDelta = new Vector2(540, 44);
            var hl = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter; hl.spacing = 10;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false; hl.childControlWidth = true; hl.childControlHeight = true;

            var chip = OtoutzUI.Panel("Diff", chips, 22, OtoutzTheme.A(d.color, 0.18f));
            OtoutzUI.Ring(chip.transform, 22, 2, OtoutzTheme.A(d.color, 0.5f));
            var ce = chip.gameObject.AddComponent<LayoutElement>(); ce.minWidth = 168; ce.minHeight = 44;
            var star = OtoutzUI.Image("Star", chip.transform, OtoutzSprites.Star(), d.color);
            star.rectTransform.anchorMin = star.rectTransform.anchorMax = new Vector2(0, 0.5f); star.rectTransform.pivot = new Vector2(0, 0.5f);
            star.rectTransform.sizeDelta = new Vector2(14, 14); star.rectTransform.anchoredPosition = new Vector2(14, 0);
            var clab = OtoutzUI.Text("L", chip.transform, d.label, 13, d.deep, false, FontStyles.Bold, TextAlignmentOptions.Left);
            clab.rectTransform.anchorMin = new Vector2(0, 0); clab.rectTransform.anchorMax = new Vector2(0, 1); clab.rectTransform.pivot = new Vector2(0, 0.5f);
            clab.rectTransform.sizeDelta = new Vector2(95, 0); clab.rectTransform.anchoredPosition = new Vector2(36, 0);
            var clvl = OtoutzUI.Text("V", chip.transform, OtoutzResultData.levelText, 17, d.deep, true, FontStyles.Bold, TextAlignmentOptions.Right);
            clvl.rectTransform.anchorMin = new Vector2(1, 0); clvl.rectTransform.anchorMax = new Vector2(1, 1); clvl.rectTransform.pivot = new Vector2(1, 0.5f);
            clvl.rectTransform.sizeDelta = new Vector2(40, 0); clvl.rectTransform.anchoredPosition = new Vector2(-16, 0);

            var bpm = OtoutzUI.Panel("Bpm", chips, 22, OtoutzTheme.A(OtoutzTheme.panel, 0.5f));
            OtoutzUI.Ring(bpm.transform, 22, 2, OtoutzTheme.line);
            var be = bpm.gameObject.AddComponent<LayoutElement>(); be.minWidth = 130; be.minHeight = 44;
            var bl = OtoutzUI.Text("L", bpm.transform, "BPM", 13, OtoutzTheme.sub, false, FontStyles.Bold, TextAlignmentOptions.Left);
            bl.characterSpacing = 6; bl.rectTransform.anchorMin = new Vector2(0, 0); bl.rectTransform.anchorMax = new Vector2(0, 1); bl.rectTransform.pivot = new Vector2(0, 0.5f);
            bl.rectTransform.sizeDelta = new Vector2(48, 0); bl.rectTransform.anchoredPosition = new Vector2(16, 0);
            var bv = OtoutzUI.Text("V", bpm.transform, Mathf.RoundToInt(OtoutzResultData.bpm).ToString(), 16, OtoutzTheme.ink, true, FontStyles.Bold, TextAlignmentOptions.Right);
            bv.rectTransform.anchorMin = new Vector2(1, 0); bv.rectTransform.anchorMax = new Vector2(1, 1); bv.rectTransform.pivot = new Vector2(1, 0.5f);
            bv.rectTransform.sizeDelta = new Vector2(60, 0); bv.rectTransform.anchoredPosition = new Vector2(-16, 0);

            // clear banner
            bool fancy = OtoutzResultData.isAP || OtoutzResultData.isFC;
            string clearText = OtoutzResultData.isAP ? "ALL PERFECT" : OtoutzResultData.isFC ? "FULL COMBO" : "CLEAR";
            Color cgA, cgB;
            if (OtoutzResultData.isAP) { cgA = OtoutzTheme.Hex("#ffe48a"); cgB = OtoutzTheme.Hex("#f5b62e"); }
            else if (OtoutzResultData.isFC) { cgA = OtoutzTheme.accent; cgB = OtoutzTheme.accent2; }
            else { cgA = OtoutzTheme.A(OtoutzTheme.panel, 0.5f); cgB = cgA; }

            var banner = OtoutzUI.Panel("ClearBanner", left, 18, fancy ? Color.white : OtoutzTheme.A(OtoutzTheme.panel, 0.5f));
            banner.rectTransform.anchorMin = banner.rectTransform.anchorMax = new Vector2(0.5f, 1);
            banner.rectTransform.pivot = new Vector2(0.5f, 1);
            banner.rectTransform.anchoredPosition = new Vector2(0, -560);
            banner.rectTransform.sizeDelta = new Vector2(28 + clearText.Length * 22f, 62);
            if (fancy) OtoutzUI.AddGradient(banner, cgA, cgB, 100f);
            else OtoutzUI.Ring(banner.transform, 18, 2, OtoutzTheme.line);
            if (fancy)
            {
                var bglow = OtoutzUI.Image("Glow", left, OtoutzSprites.Glow(), OtoutzTheme.A(cgB, 0.5f));
                bglow.rectTransform.anchorMin = bglow.rectTransform.anchorMax = new Vector2(0.5f, 1); bglow.rectTransform.pivot = new Vector2(0.5f, 1);
                bglow.rectTransform.sizeDelta = new Vector2(banner.rectTransform.sizeDelta.x + 120, 150);
                bglow.rectTransform.anchoredPosition = new Vector2(0, -560 + 30);
                bglow.transform.SetSiblingIndex(banner.transform.GetSiblingIndex());
            }
            var bt = OtoutzUI.Text("T", banner.transform, clearText, 30, fancy ? Color.white : OtoutzTheme.ink, true, FontStyles.Bold);
            bt.characterSpacing = 6; OtoutzUI.Stretch(bt.rectTransform);
            _clearRT = banner.rectTransform; _clearGroup = banner.gameObject.AddComponent<CanvasGroup>();
        }

        OtoutzSong DisplaySong()
        {
            return new OtoutzSong
            {
                title = OtoutzResultData.title, artist = OtoutzResultData.artist,
                genre = OtoutzResultData.genre, glyph = OtoutzResultData.glyph,
                artA = OtoutzResultData.artA, artB = OtoutzResultData.artB, artBlob = OtoutzResultData.artBlob,
            };
        }

        // ---- RIGHT: score + rank + judgments ----
        void BuildRight(RectTransform content, OtoutzTheme.Diff d, RankTier rank)
        {
            var right = OtoutzUI.Rect("Right", content);
            right.anchorMin = right.anchorMax = new Vector2(0, 1); right.pivot = new Vector2(0, 1);
            right.anchoredPosition = new Vector2(600, 0); right.sizeDelta = new Vector2(1136, 792);

            const float topH = 230f;
            const float rankW = 250f;

            // --- SCORE panel ---
            var score = OtoutzUI.Panel("ScorePanel", right, 24, OtoutzTheme.A(OtoutzTheme.panel, 0.42f));
            score.rectTransform.anchorMin = score.rectTransform.anchorMax = new Vector2(0, 1); score.rectTransform.pivot = new Vector2(0, 1);
            score.rectTransform.anchoredPosition = new Vector2(0, 0);
            score.rectTransform.sizeDelta = new Vector2(1136 - rankW - 30, topH);
            OtoutzUI.Ring(score.transform, 24, 2, OtoutzTheme.line);
            _scoreRT = score.rectTransform; _scoreGroup = score.gameObject.AddComponent<CanvasGroup>();

            var sLabel = OtoutzUI.Text("Label", score.transform, "SCORE", 15, OtoutzTheme.sub, false, FontStyles.Bold, TextAlignmentOptions.Left);
            sLabel.characterSpacing = 22; sLabel.rectTransform.anchorMin = sLabel.rectTransform.anchorMax = new Vector2(0, 1);
            sLabel.rectTransform.pivot = new Vector2(0, 1); sLabel.rectTransform.sizeDelta = new Vector2(400, 20); sLabel.rectTransform.anchoredPosition = new Vector2(32, -22);

            _scoreText = OtoutzUI.Text("Value", score.transform, "0", 88, OtoutzTheme.ink, true, FontStyles.Bold, TextAlignmentOptions.Left);
            _scoreText.rectTransform.anchorMin = _scoreText.rectTransform.anchorMax = new Vector2(0, 1);
            _scoreText.rectTransform.pivot = new Vector2(0, 1); _scoreText.rectTransform.sizeDelta = new Vector2(820, 100); _scoreText.rectTransform.anchoredPosition = new Vector2(30, -46);

            var accLabel = OtoutzUI.Text("AccLabel", score.transform, "ACCURACY", 15, OtoutzTheme.sub, false, FontStyles.Bold, TextAlignmentOptions.Left);
            accLabel.characterSpacing = 18; accLabel.rectTransform.anchorMin = accLabel.rectTransform.anchorMax = new Vector2(0, 1);
            accLabel.rectTransform.pivot = new Vector2(0, 1); accLabel.rectTransform.sizeDelta = new Vector2(130, 22); accLabel.rectTransform.anchoredPosition = new Vector2(32, -172);
            _accText = OtoutzUI.Text("AccValue", score.transform, "0.0000%", 38, rank.b, true, FontStyles.Bold, TextAlignmentOptions.Left);
            _accText.rectTransform.anchorMin = _accText.rectTransform.anchorMax = new Vector2(0, 1);
            _accText.rectTransform.pivot = new Vector2(0, 1); _accText.rectTransform.sizeDelta = new Vector2(360, 46); _accText.rectTransform.anchoredPosition = new Vector2(168, -163);

            // --- RANK badge ---
            var rankGlow = OtoutzUI.Image("RankGlow", right, OtoutzSprites.Glow(), OtoutzTheme.A(rank.glow, 0.55f));
            rankGlow.rectTransform.anchorMin = rankGlow.rectTransform.anchorMax = new Vector2(1, 1); rankGlow.rectTransform.pivot = new Vector2(1, 1);
            rankGlow.rectTransform.sizeDelta = new Vector2(rankW + 120, topH + 120);
            rankGlow.rectTransform.anchoredPosition = new Vector2(60, 50);

            var badge = OtoutzUI.Panel("RankBadge", right, 26, Color.white);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1, 1); badge.rectTransform.pivot = new Vector2(1, 1);
            badge.rectTransform.anchoredPosition = new Vector2(0, 0); badge.rectTransform.sizeDelta = new Vector2(rankW, topH);
            OtoutzUI.AddGradient(badge, rank.a, rank.b, 150f);
            OtoutzUI.Ring(badge.transform, 26, 2, OtoutzTheme.A(Color.white, 0.5f));
            _rankRT = badge.rectTransform; _rankGroup = badge.gameObject.AddComponent<CanvasGroup>();

            var rankEye = OtoutzUI.Text("Eye", badge.transform, "RANK", 13, OtoutzTheme.A(Color.white, 0.85f), false, FontStyles.Bold);
            rankEye.characterSpacing = 16; rankEye.rectTransform.anchorMin = rankEye.rectTransform.anchorMax = new Vector2(0.5f, 1);
            rankEye.rectTransform.pivot = new Vector2(0.5f, 1); rankEye.rectTransform.sizeDelta = new Vector2(200, 20); rankEye.rectTransform.anchoredPosition = new Vector2(0, -14);

            var s1 = OtoutzUI.Image("Star1", badge.transform, OtoutzSprites.Star(), OtoutzTheme.A(Color.white, 0.9f));
            s1.rectTransform.anchorMin = s1.rectTransform.anchorMax = new Vector2(0, 1); s1.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            s1.rectTransform.sizeDelta = new Vector2(26, 26); s1.rectTransform.anchoredPosition = new Vector2(40, -54);
            var s2 = OtoutzUI.Image("Star2", badge.transform, OtoutzSprites.Star(), OtoutzTheme.A(Color.white, 0.8f));
            s2.rectTransform.anchorMin = s2.rectTransform.anchorMax = new Vector2(1, 0); s2.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            s2.rectTransform.sizeDelta = new Vector2(18, 18); s2.rectTransform.anchoredPosition = new Vector2(-44, 48);

            int rlen = rank.rank.Length;
            float rsize = rlen >= 4 ? 96 : rlen == 3 ? 116 : 140;
            var rankText = OtoutzUI.Text("Rank", badge.transform, rank.rank, rsize, Color.white, true, FontStyles.Bold);
            OtoutzUI.Stretch(rankText.rectTransform);

            // --- Judgment panel ---
            var jp = OtoutzUI.Panel("JudgePanel", right, 24, OtoutzTheme.A(OtoutzTheme.panel, 0.42f));
            jp.rectTransform.anchorMin = jp.rectTransform.anchorMax = new Vector2(0, 1); jp.rectTransform.pivot = new Vector2(0, 1);
            jp.rectTransform.anchoredPosition = new Vector2(0, -(topH + 24));
            // Fixed height matching the handoff's tall panel, but bounded so its bottom (~188px
            // from screen bottom) stays clear of the RETRY/NEXT buttons (top ~142px).
            jp.rectTransform.sizeDelta = new Vector2(1136, 500);
            OtoutzUI.Ring(jp.transform, 24, 2, OtoutzTheme.line);
            _judgeRT = jp.rectTransform; _judgeGroup = jp.gameObject.AddComponent<CanvasGroup>();

            int[] vals = { OtoutzResultData.perfect, OtoutzResultData.great, OtoutzResultData.good, OtoutzResultData.miss };
            int maxCount = Mathf.Max(1, vals[0], vals[1], vals[2], vals[3]);
            float[] rowY = { -100, -170, -240, -310 };
            for (int i = 0; i < 4; i++)
            {
                _judgeVals[i] = vals[i];
                _judgeFillPct[i] = vals[i] / (float)maxCount;
                BuildJudgeRow(jp.transform, i, Judges[i], rowY[i]);
            }

            // divider
            var div = OtoutzUI.Image("Divider", jp.transform, OtoutzSprites.RoundedRect(2), OtoutzTheme.A(OtoutzTheme.ink, 0.14f), Image.Type.Sliced);
            div.rectTransform.anchorMin = new Vector2(0, 1); div.rectTransform.anchorMax = new Vector2(1, 1); div.rectTransform.pivot = new Vector2(0.5f, 1);
            div.rectTransform.offsetMin = new Vector2(32, 0); div.rectTransform.offsetMax = new Vector2(-32, 0);
            div.rectTransform.sizeDelta = new Vector2(div.rectTransform.sizeDelta.x, 2);
            div.rectTransform.anchoredPosition = new Vector2(0, -362);

            // MAX COMBO row
            var comboRow = OtoutzUI.Rect("ComboRow", jp.transform);
            comboRow.anchorMin = new Vector2(0, 1); comboRow.anchorMax = new Vector2(1, 1); comboRow.pivot = new Vector2(0.5f, 1);
            comboRow.offsetMin = new Vector2(32, -432); comboRow.offsetMax = new Vector2(-32, -388);
            var cLabel = OtoutzUI.Text("L", comboRow, "MAX COMBO", 19, OtoutzTheme.sub, false, FontStyles.Bold, TextAlignmentOptions.Left);
            cLabel.characterSpacing = 2; cLabel.rectTransform.anchorMin = new Vector2(0, 0.5f); cLabel.rectTransform.anchorMax = new Vector2(0, 0.5f); cLabel.rectTransform.pivot = new Vector2(0, 0.5f);
            cLabel.rectTransform.sizeDelta = new Vector2(280, 28); cLabel.rectTransform.anchoredPosition = Vector2.zero;
            _comboText = OtoutzUI.Text("V", comboRow, "0 / 0", 30, OtoutzTheme.ink, true, FontStyles.Bold, TextAlignmentOptions.Right);
            _comboText.rectTransform.anchorMin = new Vector2(1, 0.5f); _comboText.rectTransform.anchorMax = new Vector2(1, 0.5f); _comboText.rectTransform.pivot = new Vector2(1, 0.5f);
            _comboText.rectTransform.sizeDelta = new Vector2(300, 36); _comboText.rectTransform.anchoredPosition = Vector2.zero;
        }

        void BuildJudgeRow(Transform parent, int i, Judge j, float top)
        {
            var row = OtoutzUI.Rect("Judge" + i, parent);
            row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1); row.pivot = new Vector2(0.5f, 1);
            row.offsetMin = new Vector2(32, top - 26); row.offsetMax = new Vector2(-32, top + 26);

            var star = OtoutzUI.Image("Star", row, OtoutzSprites.Star(), j.color);
            star.rectTransform.anchorMin = star.rectTransform.anchorMax = new Vector2(0, 0.5f); star.rectTransform.pivot = new Vector2(0, 0.5f);
            star.rectTransform.sizeDelta = new Vector2(17, 17); star.rectTransform.anchoredPosition = new Vector2(0, 0);

            var label = OtoutzUI.Text("L", row, j.label, 19, OtoutzTheme.ink, false, FontStyles.Bold, TextAlignmentOptions.Left);
            label.characterSpacing = 2; label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0, 0.5f); label.rectTransform.pivot = new Vector2(0, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(210, 26); label.rectTransform.anchoredPosition = new Vector2(28, 0);

            var count = OtoutzUI.Text("C", row, "0", 30, j.color, true, FontStyles.Bold, TextAlignmentOptions.Right);
            count.rectTransform.anchorMin = count.rectTransform.anchorMax = new Vector2(1, 0.5f); count.rectTransform.pivot = new Vector2(1, 0.5f);
            count.rectTransform.sizeDelta = new Vector2(92, 36); count.rectTransform.anchoredPosition = new Vector2(0, 0);
            _judgeCounts[i] = count;

            var barBg = OtoutzUI.Panel("Bar", row, 6, OtoutzTheme.A(OtoutzTheme.ink, 0.12f));
            barBg.rectTransform.anchorMin = new Vector2(0, 0.5f); barBg.rectTransform.anchorMax = new Vector2(1, 0.5f); barBg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            barBg.rectTransform.offsetMin = new Vector2(250, -6); barBg.rectTransform.offsetMax = new Vector2(-112, 6);

            var fill = OtoutzUI.Panel("Fill", barBg.transform, 6, j.color);
            fill.rectTransform.anchorMin = new Vector2(0, 0); fill.rectTransform.anchorMax = new Vector2(0, 1); fill.rectTransform.pivot = new Vector2(0, 0.5f);
            fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero;
            _judgeFills[i] = fill.rectTransform;
        }

        // ---- buttons + hint bar ----
        void BuildButtons()
        {
            var bar = OtoutzUI.Rect("Buttons", _root);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0); bar.pivot = new Vector2(0.5f, 0);
            bar.anchoredPosition = new Vector2(0, 88); bar.sizeDelta = new Vector2(600, 54);

            var retry = OtoutzUI.Panel("Retry", bar, 16, OtoutzTheme.A(OtoutzTheme.panel, 0.55f));
            OtoutzUI.Ring(retry.transform, 16, 2, OtoutzTheme.line);
            retry.rectTransform.anchorMin = retry.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); retry.rectTransform.pivot = new Vector2(1, 0.5f);
            retry.rectTransform.sizeDelta = new Vector2(190, 54); retry.rectTransform.anchoredPosition = new Vector2(-9, 0);
            var ric = OtoutzUI.Image("Icon", retry.transform, OtoutzSprites.Chevron(), OtoutzTheme.accent); // stand-in refresh glyph
            ric.rectTransform.anchorMin = ric.rectTransform.anchorMax = new Vector2(0, 0.5f); ric.rectTransform.pivot = new Vector2(0, 0.5f);
            ric.rectTransform.sizeDelta = new Vector2(20, 20); ric.rectTransform.anchoredPosition = new Vector2(26, 0);
            var rt = OtoutzUI.Text("T", retry.transform, "RETRY", 17, OtoutzTheme.ink, false, FontStyles.Bold);
            rt.characterSpacing = 2; OtoutzUI.Stretch(rt.rectTransform, 52, 14, 0, 0);
            OtoutzUI.MakeClickable(retry, Retry);

            var next = OtoutzUI.Panel("Next", bar, 16, Color.white);
            OtoutzUI.AddGradient(next, OtoutzTheme.accent, OtoutzTheme.accent2, 100f);
            next.rectTransform.anchorMin = next.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); next.rectTransform.pivot = new Vector2(0, 0.5f);
            next.rectTransform.sizeDelta = new Vector2(200, 54); next.rectTransform.anchoredPosition = new Vector2(9, 0);
            var nglow = OtoutzUI.Image("Glow", bar, OtoutzSprites.Glow(), OtoutzTheme.A(OtoutzTheme.accent, 0.5f));
            nglow.rectTransform.sizeDelta = new Vector2(280, 130); nglow.rectTransform.anchoredPosition = new Vector2(110, -10);
            nglow.transform.SetAsFirstSibling();
            var nt = OtoutzUI.Text("T", next.transform, "NEXT ▸", 17, Color.white, false, FontStyles.Bold);
            nt.characterSpacing = 2; OtoutzUI.Stretch(nt.rectTransform);
            OtoutzUI.MakeClickable(next, Next);
        }

        void BuildHintBar()
        {
            var items = new[] { ("R", "재시도"), ("Enter", "다음"), ("Esc", "곡 선택") };
            var bar = OtoutzUI.Rect("HintBar", _root);
            bar.anchorMin = new Vector2(0.5f, 0); bar.anchorMax = new Vector2(0.5f, 0); bar.pivot = new Vector2(0.5f, 0);
            bar.anchoredPosition = new Vector2(0, 36); bar.sizeDelta = new Vector2(1000, 40);
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter; layout.spacing = 26;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false; layout.childControlWidth = true; layout.childControlHeight = true;

            foreach (var it in items)
            {
                var cell = OtoutzUI.Rect("Hint", bar);
                var hl = cell.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.childAlignment = TextAnchor.MiddleCenter; hl.spacing = 9;
                hl.childForceExpandWidth = false; hl.childForceExpandHeight = false; hl.childControlWidth = true; hl.childControlHeight = true;
                var fit = cell.gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var chip = OtoutzUI.Panel("Key", cell, 8, OtoutzTheme.A(OtoutzTheme.panel, 0.5f));
                OtoutzUI.Ring(chip.transform, 8, 2, OtoutzTheme.line);
                var le = chip.gameObject.AddComponent<LayoutElement>(); le.minWidth = 34; le.minHeight = 30;
                var kt = OtoutzUI.Text("k", chip.transform, it.Item1, 16, OtoutzTheme.ink, false, FontStyles.Bold);
                OtoutzUI.Stretch(kt.rectTransform, 8, 8, 0, 0);
                var lt = OtoutzUI.Text("t", cell, it.Item2, 16, OtoutzTheme.sub, false, FontStyles.Bold, TextAlignmentOptions.Left);
                var lle = lt.gameObject.AddComponent<LayoutElement>(); lle.preferredWidth = lt.preferredWidth + 10; lle.minHeight = 30;
            }
        }

        // ============================ Reveal + count-up ============================
        IEnumerator Intro()
        {
            // initial hidden states
            HideRise(_leftGroup, _leftRT, 26);
            HideRise(_scoreGroup, _scoreRT, 26);
            HideRise(_judgeGroup, _judgeRT, 26);
            HidePop(_rankGroup, _rankRT);
            HidePop(_clearGroup, _clearRT);
            for (int i = 0; i < 4; i++) SetFill(i, 0f);
            SetScore(0); SetAcc(0); for (int i = 0; i < 4; i++) SetJudge(i, 0); SetCombo(0);

            // screen-enter settle (scale .97 -> 1 handled by canvas root); brief input block
            yield return new WaitForSecondsRealtime(0.12f);

            StartCoroutine(RevealRise(_leftGroup, _leftRT, 0.05f, 0.55f));
            StartCoroutine(RevealRise(_scoreGroup, _scoreRT, 0.12f, 0.55f));
            StartCoroutine(RevealRise(_judgeGroup, _judgeRT, 0.20f, 0.55f));
            StartCoroutine(RevealPop(_rankGroup, _rankRT, 0.28f, 0.55f));
            StartCoroutine(RevealPop(_clearGroup, _clearRT, 0.90f, 0.55f));

            StartCoroutine(CountUp(SetScoreF, OtoutzResultData.score, 1.1f, 0.26f));
            StartCoroutine(CountUp(SetAcc, OtoutzResultData.acc, 1.1f, 0.26f));
            StartCoroutine(CountUp(v => SetJudge(0, Mathf.RoundToInt(v)), _judgeVals[0], 0.9f, 0.52f));
            StartCoroutine(CountUp(v => SetJudge(1, Mathf.RoundToInt(v)), _judgeVals[1], 0.9f, 0.60f));
            StartCoroutine(CountUp(v => SetJudge(2, Mathf.RoundToInt(v)), _judgeVals[2], 0.9f, 0.68f));
            StartCoroutine(CountUp(v => SetJudge(3, Mathf.RoundToInt(v)), _judgeVals[3], 0.9f, 0.76f));
            StartCoroutine(CountUp(v => SetCombo(Mathf.RoundToInt(v)), OtoutzResultData.maxCombo, 1.0f, 0.52f));
            for (int i = 0; i < 4; i++) StartCoroutine(FillBar(i, _judgeFillPct[i], 0.9f, 0.5f));

            yield return new WaitForSecondsRealtime(0.35f);
            _busy = false;
        }

        // Only hide (alpha); do NOT offset the position here — RevealRise captures rt.anchoredPosition
        // as its settle target, so pre-offsetting it would make the element rest below its real spot.
        void HideRise(CanvasGroup cg, RectTransform rt, float dy) { cg.alpha = 0f; }
        void HidePop(CanvasGroup cg, RectTransform rt) { cg.alpha = 0f; rt.localScale = Vector3.one * 0.7f; }

        IEnumerator RevealRise(CanvasGroup cg, RectTransform rt, float delay, float dur)
        {
            Vector2 to = rt.anchoredPosition; Vector2 from = to + new Vector2(0, -26);
            yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime; float k = Ease.OutBack(t / dur);
                cg.alpha = Mathf.Clamp01(Ease.OutCubic(t / dur));
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
                yield return null;
            }
            cg.alpha = 1f; rt.anchoredPosition = to;
        }

        IEnumerator RevealPop(CanvasGroup cg, RectTransform rt, float delay, float dur)
        {
            yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime; float k = Ease.OutBack(t / dur);
                cg.alpha = Mathf.Clamp01((t / dur) / 0.6f);
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, k);
                yield return null;
            }
            cg.alpha = 1f; rt.localScale = Vector3.one;
        }

        IEnumerator CountUp(Action<float> set, float target, float dur, float delay)
        {
            set(0f);
            yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime; set(target * Ease.OutCubic(t / dur));
                yield return null;
            }
            set(target);
        }

        IEnumerator FillBar(int i, float pct, float dur, float delay)
        {
            SetFill(i, 0f);
            yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime; SetFill(i, pct * Ease.OutBack(t / dur));
                yield return null;
            }
            SetFill(i, pct);
        }

        void SetFill(int i, float pct)
        {
            var rt = _judgeFills[i]; if (rt == null) return;
            var max = rt.anchorMax; max.x = Mathf.Clamp01(pct); rt.anchorMax = max;
        }

        void SetScoreF(float v) => SetScore(Mathf.RoundToInt(v));
        void SetScore(int v) { _score = v; _scoreText.text = v.ToString("N0", CultureInfo.InvariantCulture); }
        void SetAcc(float v) { _acc = v; _accText.text = v.ToString("0.0000", CultureInfo.InvariantCulture) + "%"; }
        void SetJudge(int i, int v) { _judgeCounts[i].text = v.ToString(); }
        void SetCombo(int v) { _comboText.text = v.ToString() + " <size=19><color=#a99fce>/ " + OtoutzResultData.total + "</color></size>"; }

        // ============================ Input ============================
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _busy) return;
            if (kb.rKey.wasPressedThisFrame) Retry();
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Next();
            else if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) Next();
        }

        void Retry()
        {
            if (_busy && _score == 0) { } // allow even mid-anim once input unblocked
            _busy = true;
            OtoutzResultData.valid = false; // next play repopulates
            SceneManager.LoadSceneAsync("InGame");
        }

        void Next()
        {
            _busy = true;
            OtoutzResultData.valid = false;
            OtoutzFlow.OpenOnSelect = true; // land on the song-select screen, not the main menu
            SceneManager.LoadSceneAsync("Menu");
        }
    }
}
