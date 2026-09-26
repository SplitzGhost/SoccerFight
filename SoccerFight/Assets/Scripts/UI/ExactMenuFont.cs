using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace SoccerFight
{
    /// <summary>Gemalte Originalbuchstaben für veränderliche Menübeschriftungen.</summary>
    public static class ExactMenuFont
    {
        [Serializable] sealed class Layout { public Letter[] items; }
        [Serializable] sealed class Letter { public uint unicode; public int x, y, width, height; }
        static TMP_FontAsset font;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => font = null;

        public static TMP_FontAsset Get()
        {
            if (font != null) return font;
            var atlas = UnityEngine.Object.Instantiate(Resources.Load<Texture2D>("UiButtons/Exact/letters"));
            var layout = JsonUtility.FromJson<Layout>(Resources.Load<TextAsset>("UiButtons/Exact/letters").text);
            font = TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("Fonts/Inter-SemiBold"), 64, 0,
                GlyphRenderMode.RASTER, atlas.width, atlas.height, AtlasPopulationMode.Static);
            font.name = "Originalschrift der Probebilder";
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.faceInfo = new FaceInfo { familyName = font.name, styleName = "Gemalt", pointSize = 64,
                scale = 1f, lineHeight = 80f, ascentLine = 70f, capLine = 64f, meanLine = 32f, descentLine = -10f };
            font.atlasTextures = new[] { atlas };
            font.glyphTable.Clear(); font.characterTable.Clear();
            font.material = new Material(Resources.Load<Shader>("UiButtons/Exact/OriginalLetters"));
            font.material.mainTexture = atlas;
            foreach (var e in layout.items)
            {
                float k = 64f / e.height;
                var glyph = new Glyph(e.unicode, new GlyphMetrics(e.width * k, 64f, 0f, 64f, e.width * k + 3f),
                    new GlyphRect(e.x, e.y, e.width, e.height), 1f, 0);
                font.glyphTable.Add(glyph);
                font.characterTable.Add(new TMP_Character(e.unicode, font, glyph));
            }
            font.fallbackFontAssetTable = new List<TMP_FontAsset> { UiArt.FontBold };
            font.ReadFontAssetDefinition();
            return font;
        }
    }
}
