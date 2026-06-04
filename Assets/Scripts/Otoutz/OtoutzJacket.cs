using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Otoutz
{
    /// <summary>Builds the procedural placeholder jacket (gradient + blobs + glyph + inset ring).</summary>
    public static class OtoutzJacket
    {
        /// <param name="holder">A RectTransform sized to the jacket; art is built to fill it.</param>
        public static void Build(RectTransform holder, OtoutzSong song, float size, int radius, bool ring = true)
        {
            // masked container for rounded clipping
            var container = OtoutzUI.Image("JacketClip", holder, OtoutzSprites.RoundedRect(radius), Color.white, Image.Type.Sliced);
            OtoutzUI.Stretch(container.rectTransform);
            var mask = container.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            if (song != null && song.jacketSprite != null)
            {
                // real jacket.png from the chart folder, clipped to the rounded square
                var art = OtoutzUI.Image("JacketArt", container.transform, song.jacketSprite, Color.white);
                OtoutzUI.Stretch(art.rectTransform);
                art.preserveAspect = true;
            }
            else
            {
                // base gradient (a -> b, ~140deg)
                var fill = OtoutzUI.Image("Fill", container.transform, null, Color.white);
                OtoutzUI.Stretch(fill.rectTransform);
                OtoutzUI.AddGradient(fill, song.artA, song.artB, 140f);

                // blobs
                var blob1 = OtoutzUI.Image("Blob1", container.transform, OtoutzSprites.Circle(), OtoutzTheme.A(song.artBlob, 0.85f));
                blob1.rectTransform.sizeDelta = new Vector2(size * 0.85f, size * 0.85f);
                blob1.rectTransform.anchoredPosition = new Vector2(-size * 0.26f, size * 0.30f);

                var blob2 = OtoutzUI.Image("Blob2", container.transform, OtoutzSprites.Circle(), OtoutzTheme.A(song.artA, 0.9f));
                blob2.rectTransform.sizeDelta = new Vector2(size * 0.66f, size * 0.66f);
                blob2.rectTransform.anchoredPosition = new Vector2(size * 0.28f, -size * 0.26f);

                // diagonal sheen
                var sheen = OtoutzUI.Image("Sheen", container.transform, null, Color.white);
                OtoutzUI.Stretch(sheen.rectTransform);
                OtoutzUI.AddGradient(sheen, OtoutzTheme.A(Color.white, 0.4f), OtoutzTheme.A(Color.white, 0f), 125f);

                // glyph
                var glyph = OtoutzUI.Text("Glyph", container.transform, song.glyph, size * 0.4f,
                    OtoutzTheme.A(Color.white, 0.95f), display: true, FontStyles.Bold);
                OtoutzUI.Stretch(glyph.rectTransform);
            }

            // inset white ring (outside the mask so it isn't clipped)
            if (ring)
            {
                var r = OtoutzUI.Image("JacketRing", holder, OtoutzSprites.RoundedBorder(radius, 3), OtoutzTheme.A(Color.white, 0.45f), Image.Type.Sliced);
                OtoutzUI.Stretch(r.rectTransform);
            }
        }
    }
}
