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
            // Den fertigen Atlas direkt verwenden: eine Kopie nicht lesbarer Texturen
            // verliert in WebGL ihre Bilddaten. Es wird keine neue Schrift gerastert.
            var atlas = Resources.Load<Texture2D>("UiButtons/Exact/letters");
            var source = Resources.Load<TextAsset>("UiButtons/Exact/letters");
            var shader = Resources.Load<Shader>("UiButtons/Exact/OriginalLetters");
            if (atlas == null || source == null || shader == null || !shader.isSupported)
            {
                Debug.LogWarning("Originalschrift nicht verfügbar; Menü verwendet die Standardschrift.");
                return UiArt.FontBold;
            }
            var layout = JsonUtility.FromJson<Layout>(source.text);
            // RASTER lädt zunächst den im Web-Build entfernten TMP-Bitmap-Shader.
            // Über den vorhandenen SDF-Shader initialisieren, danach das eigene
            // Material einsetzen. Der statische Atlas wird dabei nicht gerastert.
            font = TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("Fonts/Inter-SemiBold"), 64, 0,
                GlyphRenderMode.SDFAA, atlas.width, atlas.height, AtlasPopulationMode.Static);
            if (font == null) return UiArt.FontBold;
            font.name = "Originalschrift der Probebilder";
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.faceInfo = new FaceInfo { familyName = font.name, styleName = "Gemalt", pointSize = 64,
                scale = 1f, lineHeight = 80f, ascentLine = 70f, capLine = 64f, meanLine = 32f, descentLine = -10f };
            // Die nur zur Initialisierung angelegten Ressourcen werden nicht gebraucht.
            UnityEngine.Object.Destroy(font.atlasTextures[0]);
            UnityEngine.Object.Destroy(font.material);
            font.atlasTextures = new[] { atlas };
            font.glyphTable.Clear(); font.characterTable.Clear();
            font.material = new Material(shader);
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
