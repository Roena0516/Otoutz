using UnityEngine;

namespace Otoutz
{
    /// <summary>
    /// Static carrier that hands play results from the InGame scene to the Result scene.
    /// Song display fields are filled by <see cref="OtoutzFlow"/> at launch (it owns the
    /// <see cref="OtoutzSong"/>); the score / judgement fields are filled by JudgementManager
    /// when the chart ends. Mirrors the `result` object shape in the handoff prototype.
    /// </summary>
    public static class OtoutzResultData
    {
        // ---- song display (set at launch) ----
        public static string title = "";
        public static string artist = "";
        public static string genre = "MUSIC";
        public static string levelText = "-";
        public static string glyph = "♪";
        public static float bpm;
        public static Color artA = OtoutzTheme.accent, artB = OtoutzTheme.accent2, artBlob = OtoutzTheme.glow;
        public static int diffIndex = 3;

        // ---- play result (set at level end) ----
        public static int total, perfect, great, good, miss, maxCombo;
        public static int score;
        public static float acc;          // accuracy %, derived from JudgementManager.rate
        public static bool isFC, isAP, isNewRecord;

        /// <summary>True once a real play has populated the result fields this session.</summary>
        public static bool valid;

        /// <summary>Stash the picked song's display data before launching InGame.</summary>
        public static void SetSong(OtoutzSong s, int diff)
        {
            title = s.title;
            artist = s.artist;
            genre = s.genre;
            levelText = s.LevelText(diff);
            glyph = s.glyph;
            bpm = s.bpm;
            artA = s.artA; artB = s.artB; artBlob = s.artBlob;
            diffIndex = diff;
        }

        /// <summary>Snapshot the finished play. clear/fc/ap force maxCombo to total like the spec.</summary>
        public static void SetPlayResult(int perfect, int great, int good, int miss,
            int score, float acc, int maxCombo, bool isFC, bool isAP)
        {
            OtoutzResultData.perfect = perfect;
            OtoutzResultData.great = great;
            OtoutzResultData.good = good;
            OtoutzResultData.miss = miss;
            OtoutzResultData.total = perfect + great + good + miss;
            OtoutzResultData.score = score;
            OtoutzResultData.acc = (isAP && acc < 101f) ? 101f : acc;
            OtoutzResultData.isFC = isFC;
            OtoutzResultData.isAP = isAP;
            OtoutzResultData.maxCombo = (isFC || isAP) ? OtoutzResultData.total : Mathf.Clamp(maxCombo, 0, OtoutzResultData.total);
            OtoutzResultData.isNewRecord = false; // no persisted best-score store yet
            valid = true;
        }

        /// <summary>Fallback demo result so opening the Result scene directly isn't blank.</summary>
        public static void FillDemoIfEmpty()
        {
            if (valid) return;
            title = string.IsNullOrEmpty(title) ? "Sugar Rush" : title;
            artist = string.IsNullOrEmpty(artist) ? "Pop'n Tea" : artist;
            total = 919; perfect = 886; great = 22; good = 10; miss = 1;
            score = 996801; acc = 99.6801f; maxCombo = 917;
            isFC = false; isAP = false; isNewRecord = false;
            valid = true;
        }
    }
}
