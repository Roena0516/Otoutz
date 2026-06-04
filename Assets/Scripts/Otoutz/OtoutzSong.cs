using UnityEngine;

namespace Otoutz
{
    /// <summary>A song row with its 4 difficulty slots, ready for the carousel/difficulty screens.</summary>
    public class OtoutzSong
    {
        public string title;
        public string artist;
        public string jpTitle;
        public string jpArtist;
        public string genre;        // uppercase category
        public float bpm;
        public string eventName;
        public int previewStart;
        public int previewEnd;

        // index order matches OtoutzTheme.DiffKeys = BASIC, ADVANCED, EXPERT, LUNATIC
        public float[] levels = new float[4];
        public string[] filePaths = new string[4];
        public int[] ids = new int[4];
        public SongInfoClass[] infos = new SongInfoClass[4];

        // procedural jacket art (fallback when no jacket.png is present)
        public Color artA, artB, artBlob;
        public string glyph;

        // real jacket loaded from the chart folder's jacket.png (null = use procedural art)
        public Sprite jacketSprite;

        public bool HasDiff(int i) => levels[i] > 0f && infos[i] != null;

        public int FirstDiff()
        {
            for (int i = 0; i < 4; i++) if (HasDiff(i)) return i;
            return 0;
        }

        public string LevelText(int i)
        {
            if (!HasDiff(i)) return "-";
            float v = levels[i];
            return Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.0");
        }
    }
}
