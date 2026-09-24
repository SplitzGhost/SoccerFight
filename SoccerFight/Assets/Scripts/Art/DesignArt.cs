using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Gemeinsames für die Design-Grafik: das Sprite-Material, ein weißer Block für Farbflächen und der
    /// Pflanzen-Atlas (Gras, Farn, Büsche mit Wind) aus Resources/NewDesign (tools/newdesign/build.js).
    /// Die Stage-Welten selbst liefert <see cref="StageKit"/>. Einmal beim Start geladen.
    /// </summary>
    public static class DesignArt
    {
        [Serializable]
        sealed class Entry
        {
            public string name;
            public int w, h;
            public float ppu, px, py, sx0, sx1;
            public bool tile;
        }

        [Serializable]
        sealed class PlantEntry
        {
            public string name, mode;
            public int x, y, w, h;
            public float px, py, ppu;
        }

        [Serializable]
        sealed class Manifest
        {
            public Entry[] sprites;
            public PlantEntry[] plants;
            public float[] skyTop, groundDeep;
            public int[] plantsSize;
        }

        /// <summary>Eine Plattform-Grafik: Sprite (Pivot auf der Standlinie) und ihre begehbare Breite.</summary>
        public sealed class PlatformSprite
        {
            public Sprite Sprite;
            /// <summary>Begehbare Breite in Einheiten (bei Maßstab 1).</summary>
            public float Walk;
        }

        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, PlatformSprite> platforms = new Dictionary<string, PlatformSprite>();
        static readonly Dictionary<string, FoliageArt.Variant> plants = new Dictionary<string, FoliageArt.Variant>();
        static bool loaded;

        /// <summary>Etwas heller als normale Sprites: gleicht Tonemapping und Vignette aus, damit die Farben den Vorlagen entsprechen.</summary>
        public const float Brightness = 1.15f;
        public static Material SpriteMat { get; private set; }
        public static Texture2D PlantAtlas { get; private set; }
        public static Color SkyTop { get; private set; } = new Color(0.05f, 0.1f, 0.3f);
        /// <summary>Farbe, in die die Mauer nach unten ausläuft.</summary>
        public static Color GroundDeep { get; private set; } = new Color(0.04f, 0.045f, 0.1f);
        /// <summary>Weißes Quadrat (1 × 1 Einheit, Pivot Mitte) für Flächen in Himmels- oder Erdfarbe.</summary>
        public static Sprite Block { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { loaded = false; SpriteMat = null; sprites.Clear(); platforms.Clear(); plants.Clear(); }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            SpriteMat = Art.MakeSpriteMaterial("SF Design Sprite", Brightness, false);
            var json = Resources.Load<TextAsset>("NewDesign/design");
            if (json == null) { Debug.LogError("[SoccerFight] Resources/NewDesign/design.json fehlt (tools/newdesign/build.js)"); return; }
            var m = JsonUtility.FromJson<Manifest>(json.text);
            if (m.skyTop != null && m.skyTop.Length == 3) SkyTop = new Color(m.skyTop[0], m.skyTop[1], m.skyTop[2]);
            if (m.groundDeep != null && m.groundDeep.Length == 3) GroundDeep = new Color(m.groundDeep[0], m.groundDeep[1], m.groundDeep[2]);

            foreach (var e in m.sprites)
            {
                var tex = Resources.Load<Texture2D>("NewDesign/" + e.name);
                if (tex == null) { Debug.LogError("[SoccerFight] Design-Bild fehlt: " + e.name); continue; }
                tex.wrapMode = TextureWrapMode.Clamp;
                var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(e.px / e.w, e.py / e.h), e.ppu, 0, SpriteMeshType.FullRect);
                s.name = e.name;
                sprites[e.name] = s;
                if (e.sx1 > e.sx0) platforms[e.name] = new PlatformSprite { Sprite = s, Walk = (e.sx1 - e.sx0) / e.ppu };
            }

            PlantAtlas = Resources.Load<Texture2D>("NewDesign/plants");
            if (PlantAtlas != null && m.plants != null)
            {
                float aw = PlantAtlas.width, ah = PlantAtlas.height;
                foreach (var p in m.plants)
                {
                    var mode = p.mode == "hanging" ? FoliageArt.Mode.Hanging : FoliageArt.Mode.Rooted;
                    var v = new FoliageArt.Variant { Name = p.name, Mode = mode, Sway = mode == FoliageArt.Mode.Hanging ? 0.8f : 1f };
                    v.Entry = new AtlasBuilder.Entry
                    {
                        Name = p.name,
                        Uv = new Rect(p.x / aw, p.y / ah, p.w / aw, p.h / ah),
                        Units = new Rect(-p.px / p.ppu, -p.py / p.ppu, p.w / p.ppu, p.h / p.ppu),
                    };
                    plants[p.name] = v;
                }
            }

            var white = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "SF Design Block", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            white.SetPixels32(px);
            white.Apply(false, true);
            Block = Sprite.Create(white, new Rect(1, 1, 2, 2), new Vector2(0.5f, 0.5f), 2f, 0, SpriteMeshType.FullRect);
        }

        public static Sprite Get(string name)
        {
            if (sprites.TryGetValue(name, out var s)) return s;
            Debug.LogError("[SoccerFight] unbekanntes Design-Bild: " + name);
            return Block;
        }

        public static PlatformSprite Platform(string name) => platforms.TryGetValue(name, out var p) ? p : null;

        public static FoliageArt.Variant Plant(string name) => plants.TryGetValue(name, out var v) ? v : null;

        public static FoliageArt.Variant[] Plants(params string[] names)
        {
            var list = new List<FoliageArt.Variant>();
            foreach (var n in names) { var v = Plant(n); if (v != null) list.Add(v); }
            return list.ToArray();
        }

        /// <summary>Wind-Material für Pflanzen aus dem Design-Atlas.</summary>
        public static Material PlantMaterial(string name, float wind, Color? tint = null)
        {
            var m = FoliageLayer.MakeMaterial(name, 0f, wind, false, Brightness, tint);
            m.mainTexture = PlantAtlas;
            return m;
        }
    }
}
