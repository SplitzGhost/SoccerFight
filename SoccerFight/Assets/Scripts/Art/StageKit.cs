using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Die Grafik einer Stage aus ihrem Design-Bogen (Inspiration/StagesNewDesigns): Kulisse, kachelbarer Boden,
    /// Plattformen (stehend, schwebend, hängend) und Deko, erzeugt von tools/newdesign/stages.js unter
    /// Resources/Stages/&lt;id&gt;. Die Maße (stage.json) werden sofort gelesen — der Plattform-Generator braucht
    /// sie —, die Bilder erst, wenn die Stage gezeigt wird; beim Wechsel wird die alte Stage wieder freigegeben.
    /// </summary>
    public sealed class StageKit
    {
        [Serializable] internal sealed class LightE { public float x, y, r, cr, cg, cb; }
        [Serializable] internal sealed class LevelE { public float y, x0, x1; }
        [Serializable]
        internal sealed class Entry
        {
            public string name, role, rope;
            public string[] tags;
            public int w, h;
            public float ppu, px, py, walk0, walk1, bottom;
            public LevelE[] levels;
            public float[] anchors;
            public LightE[] lights;
        }
        [Serializable] sealed class Manifest { public string id; public float[] sky, valley, haze, deep; public float plateX0, plateRow0, plateW, platePpu; public Entry[] sprites; }

        public struct LightSpot { public Vector2 Pos; public float Radius; public Color Color; }
        public struct Ledge { public float Y, X0, X1; }

        public enum Role { Plate, Ground, Float, Stand, End, Prop, Rope }

        public sealed class Piece
        {
            public string Name;
            public Role Role;
            public string[] Tags;
            /// <summary>Walkable span relative to the pivot (units, scale 1); platforms only.</summary>
            public float Walk0, Walk1;
            /// <summary>Stand: height of the walk line above the foot (units, scale 1).</summary>
            public float Height;
            /// <summary>Extra walkable ledges below the top (a plank inside a frame).</summary>
            public Ledge[] Ledges = Array.Empty<Ledge>();
            /// <summary>Rope/chain anchors along the top (x relative to the pivot) and the texture they use.</summary>
            public float[] Anchors = Array.Empty<float>();
            public string Rope;
            public LightSpot[] Lights = Array.Empty<LightSpot>();
            public float Width, TopH, BottomH;   // sprite bounds relative to the pivot (units, scale 1)

            internal StageKit Kit;
            internal Entry E;
            Sprite sprite;

            public bool Has(string tag) { foreach (var t in Tags) if (t == tag) return true; return false; }
            public float WalkWidth => Walk1 - Walk0;
            public bool Hangs => Anchors.Length > 0 && !string.IsNullOrEmpty(Rope);

            public Sprite Sprite
            {
                get
                {
                    if (sprite == null)
                    {
                        var tex = Resources.Load<Texture2D>("Stages/" + Kit.Id + "/" + Name);
                        if (tex == null) { Debug.LogError("[SoccerFight] Stage-Bild fehlt: " + Kit.Id + "/" + Name); return DesignArt.Block; }
                        tex.wrapMode = TextureWrapMode.Clamp;
                        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(E.px / E.w, E.py / E.h), E.ppu, 0, SpriteMeshType.FullRect);
                        sprite.name = Kit.Id + "/" + Name;
                        Kit.loaded.Add(this);
                    }
                    return sprite;
                }
            }

            internal void Release()
            {
                if (sprite == null) return;
                var tex = sprite.texture;
                UnityEngine.Object.Destroy(sprite);
                sprite = null;
                if (tex != null) Resources.UnloadAsset(tex);
            }
        }

        public string Id { get; private set; }
        public Color Sky, Valley, Haze, Deep;
        public Piece Plate, Ground, End;
        float plateX0, plateRow0, plateW, platePpu = 44f;
        public readonly List<Piece> Platforms = new List<Piece>();
        public readonly List<Piece> Props = new List<Piece>();
        readonly Dictionary<string, Piece> byName = new Dictionary<string, Piece>();
        readonly List<Piece> loaded = new List<Piece>();

        static readonly Dictionary<string, StageKit> kits = new Dictionary<string, StageKit>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => kits.Clear();

        /// <summary>The kit of a stage theme (manifest only; pictures load on first use).</summary>
        public static StageKit For(StageTheme theme) => Load(theme.Kit);

        /// <summary>Shared pieces (rope, chain).</summary>
        public static StageKit Common => Load("common");

        public static StageKit Load(string id)
        {
            if (kits.TryGetValue(id, out var k)) return k;
            k = new StageKit { Id = id };
            var json = Resources.Load<TextAsset>("Stages/" + id + "/stage");
            if (json == null) Debug.LogError("[SoccerFight] Resources/Stages/" + id + "/stage.json fehlt (tools/newdesign/stages.js)");
            else k.Read(JsonUtility.FromJson<Manifest>(json.text));
            kits[id] = k;
            return k;
        }

        static Color C(float[] a, Color fallback) => a != null && a.Length == 3 ? new Color(a[0], a[1], a[2]) : fallback;

        void Read(Manifest m)
        {
            Sky = C(m.sky, new Color(0.05f, 0.1f, 0.3f));
            Valley = C(m.valley, new Color(0.04f, 0.06f, 0.12f));
            Haze = C(m.haze, new Color(0.2f, 0.25f, 0.4f));
            Deep = C(m.deep, new Color(0.04f, 0.045f, 0.1f));
            plateX0 = m.plateX0; plateRow0 = m.plateRow0; plateW = m.plateW; if (m.platePpu > 0f) platePpu = m.platePpu;
            foreach (var e in m.sprites)
            {
                var p = new Piece { Name = e.name, Tags = e.tags ?? Array.Empty<string>(), Kit = this, E = e, Rope = e.rope };
                switch (e.role)
                {
                    case "plate": p.Role = Role.Plate; Plate = p; break;
                    case "ground": p.Role = Role.Ground; Ground = p; break;
                    case "float": p.Role = Role.Float; break;
                    case "stand": p.Role = Role.Stand; break;
                    case "end": p.Role = Role.End; End = p; break;
                    case "rope": p.Role = Role.Rope; break;
                    default: p.Role = Role.Prop; break;
                }
                p.Walk0 = e.walk0; p.Walk1 = e.walk1;
                p.Height = -e.bottom;
                p.Width = e.w / e.ppu;
                p.TopH = (e.h - e.py) / e.ppu;
                p.BottomH = e.py / e.ppu;
                if (e.levels != null) { p.Ledges = new Ledge[e.levels.Length]; for (int i = 0; i < e.levels.Length; i++) p.Ledges[i] = new Ledge { Y = e.levels[i].y, X0 = e.levels[i].x0, X1 = e.levels[i].x1 }; }
                if (e.anchors != null) p.Anchors = e.anchors;
                if (e.lights != null)
                {
                    p.Lights = new LightSpot[e.lights.Length];
                    for (int i = 0; i < e.lights.Length; i++)
                    {
                        var l = e.lights[i];
                        p.Lights[i] = new LightSpot { Pos = new Vector2(l.x, l.y), Radius = l.r, Color = new Color(l.cr, l.cg, l.cb) };
                    }
                }
                byName[p.Name] = p;
                if (p.Role == Role.Float || p.Role == Role.Stand) Platforms.Add(p);
                else if (p.Role == Role.Prop) Props.Add(p);
            }
        }

        /// <summary>A point of the design sheet's scene in plate units (relative to the plate's pivot).</summary>
        public Vector2 PlateLocal(float sheetX, float sheetY) => new Vector2((sheetX - plateX0 - plateW * 0.5f) / platePpu, (plateRow0 - sheetY) / platePpu);

        public Piece Get(string name) => byName.TryGetValue(name, out var p) ? p : null;

        public List<Piece> PropsTagged(string tag)
        {
            var list = new List<Piece>();
            foreach (var p in Props) if (p.Has(tag)) list.Add(p);
            return list;
        }

        /// <summary>Free the pictures (the manifest stays for layout generation).</summary>
        public void Unload()
        {
            foreach (var p in loaded) p.Release();
            loaded.Clear();
        }
    }
}
