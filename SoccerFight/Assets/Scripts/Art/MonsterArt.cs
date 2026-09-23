using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>Every monster body in the game: one per enemy archetype and one per stage boss.</summary>
    public enum Look
    {
        Hopper, Spawnling, Splitter, Spitter, Brute, Bomber, Diver, Shade, Lantern,
        King, ThornMother, StormLantern, CrystalGuard, MagmaColossus, FrostWyrm, CometOracle, VoidLord
    }

    public enum PartAnim { None, Bob, Sway, Flap, Swing, Pulse, Foot, Orbit, Flicker, Breathe, Jaw }
    public enum PartMat { Normal, Emissive, Glow }

    /// <summary>A sprite attached to the body (or to another part) with its own little motion.</summary>
    public sealed class PartDef
    {
        public string Name;
        public Sprite Sprite;
        public Vector2 Pos;            // on the body, facing right, unscaled
        public int Order = 61;
        public float Rot;
        public Vector2 Scale = Vector2.one;
        public PartMat Mat;
        public Color Tint = Color.white;
        public PartAnim Anim;
        public float Amp, Freq = 1f, Phase;
        /// <summary>Extra motion while the monster winds up an attack (degrees, units or scale by anim).</summary>
        public float Charge;
        public int Parent = -1;
        /// <summary>Uses a sprite built at startup (1 = crown, 2 = soft glow), filled in on the main thread.</summary>
        internal int Shared;

        public PartDef Animate(PartAnim a, float amp, float freq, float phase = 0f, float charge = 0f)
        { Anim = a; Amp = amp; Freq = freq; Phase = phase; Charge = charge; return this; }
        public PartDef Turn(float deg) { Rot = deg; return this; }
        public PartDef Scaled(float x, float y) { Scale = new Vector2(x, y); return this; }
        public PartDef Shine(PartMat m, Color tint) { Mat = m; Tint = tint; return this; }
    }

    public sealed class EyeDef
    {
        public Vector2 Pos;
        public float Size = 1f;
        public Vector2 Stretch = Vector2.one;
        public int Parent = -1;
        public bool Pupil = true;
        public bool Wide;              // white eyeball with a dark pupil (wisps) instead of a glowing eye
        public Color? Color;           // glowing eyes in a colour of their own
    }

    /// <summary>A trailing chain of sprites in world space: tails, tentacles, tendrils, a wyrm's body.</summary>
    public sealed class ChainDef
    {
        public Sprite Sprite;
        public Vector2 Anchor;
        public int Count = 3;
        public float Spacing = 0.16f;
        public Vector2 Hang = new Vector2(0f, -0.06f);
        public float Scale0 = 0.9f, Scale1 = 0.3f;
        public int Order = 57;
        public float Wave = 0.02f, WaveFreq = 5f, Phase;
        public float Stiff = 14f;
        public bool Align;
        public PartMat Mat;
        public Color Tint = Color.white;
    }

    /// <summary>One body in one stage's colours: sprites, parts, eyes, chains and a few anchors.</summary>
    public sealed class LookDef
    {
        public Look Look;
        public StageTheme Theme;
        public bool Wisp;
        public Sprite Body;
        public readonly List<PartDef> Parts = new List<PartDef>();
        public readonly List<EyeDef> Eyes = new List<EyeDef>();
        public readonly List<ChainDef> Chains = new List<ChainDef>();
        public Color Top, Bottom, Glow, Eye;
        public Vector2 GlowPos;
        public float GlowSize = 1.9f, GlowAlpha = 0.16f;
        public Vector2 AuraPos;
        public float AuraSize = 2.4f;
        public float HpY = 1f;
        public float Tilt;
        public int Pending;
        public bool Ready => Pending == 0;
        internal bool SharedDone;
        internal readonly List<Sprite> Owned = new List<Sprite>();
    }

    /// <summary>
    /// Corrupted creatures, each archetype with a body of its own: horned hoppers, seed-like
    /// spawnlings, budding splitter pods, toad spitters with eye stalks, armoured brutes with fists,
    /// fused bombs, bat-winged divers, hooded shades, jelly lanterns — and eight unique bosses (a king
    /// with sceptre and mantle, a thorn bulb, a caged storm lantern, a crystal golem, a magma colossus,
    /// a frost wyrm, a ringed comet eye, a void lord). Soft-shaded with rim light and glowing eyes, in
    /// the stage's colours; generated per stage through the ArtQueue (pure math, off the main thread).
    /// </summary>
    public static class MonsterArt
    {
        public static Sprite Eye, Pupil, EyeGlow, WispEye, Portal, Crown, Orb;

        const float P = 256f, PB = 300f;
        static readonly Dictionary<StageTheme, Dictionary<Look, LookDef>> cache = new Dictionary<StageTheme, Dictionary<Look, LookDef>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); }

        public static void Build()
        {
            BuildEyes();
            BuildPortal();
            BuildCrown();
            BuildOrb();
        }

        public static Look LookOf(EnemyType t)
        {
            switch (t)
            {
                case EnemyType.Spawnling: return Look.Spawnling;
                case EnemyType.Splitter: return Look.Splitter;
                case EnemyType.Spitter: return Look.Spitter;
                case EnemyType.Brute: return Look.Brute;
                case EnemyType.Bomber: return Look.Bomber;
                case EnemyType.Diver: return Look.Diver;
                case EnemyType.Shade: return Look.Shade;
                case EnemyType.Lantern: return Look.Lantern;
                default: return Look.Hopper;
            }
        }

        /// <summary>Queue every body a stage can field: its roster, spawnlings of splitters, the boss and its minions.</summary>
        public static void Prepare(StageTheme theme)
        {
            foreach (var e in theme.Roster)
            {
                Request(theme, LookOf(e.Type));
                if (e.Type == EnemyType.Splitter) Request(theme, Look.Spawnling);
            }
            if (theme.Boss != null)
            {
                Request(theme, theme.Boss.Look);
                Request(theme, LookOf(theme.Boss.Minion));
                if (theme.Boss.Minion == EnemyType.Splitter) Request(theme, Look.Spawnling);
            }
        }

        /// <summary>The finished body (generated right now if nobody asked for it in advance).</summary>
        public static LookDef Get(StageTheme theme, Look look)
        {
            var d = Request(theme, look);
            if (!d.Ready) ArtQueue.Flush();
            if (!d.SharedDone)
            {
                d.SharedDone = true;
                foreach (var p in d.Parts)
                    if (p.Shared == 1) p.Sprite = Crown;
                    else if (p.Shared == 2) p.Sprite = Art.SoftGlow;
            }
            return d;
        }

        static LookDef Request(StageTheme theme, Look look)
        {
            if (!cache.TryGetValue(theme, out var map)) cache[theme] = map = new Dictionary<Look, LookDef>();
            if (map.TryGetValue(look, out var d)) return d;
            d = Create(theme, look);
            map[look] = d;
            return d;
        }

        /// <summary>Free the bodies of themes no longer around (the first stage's always stay for a restart).</summary>
        public static void Trim(StageTheme keepA, StageTheme keepB)
        {
            var drop = new List<StageTheme>();
            foreach (var kv in cache)
                if (kv.Key != keepA && kv.Key != keepB && kv.Key != StageThemes.All[0]) drop.Add(kv.Key);
            foreach (var th in drop)
            {
                foreach (var d in cache[th].Values)
                    foreach (var s in d.Owned)
                    {
                        if (s == null) continue;
                        if (s.texture != null) Object.Destroy(s.texture);
                        Object.Destroy(s);
                    }
                cache.Remove(th);
            }
        }

        static LookDef Create(StageTheme t, Look look)
        {
            switch (look)
            {
                case Look.Spawnling: return Spawnling(t);
                case Look.Splitter: return Splitter(t);
                case Look.Spitter: return Spitter(t);
                case Look.Brute: return Brute(t);
                case Look.Bomber: return Bomber(t);
                case Look.Diver: return Diver(t);
                case Look.Shade: return Shade(t);
                case Look.Lantern: return Lantern(t);
                case Look.King: return King(t);
                case Look.ThornMother: return ThornMother(t);
                case Look.StormLantern: return StormLantern(t);
                case Look.CrystalGuard: return CrystalGuard(t);
                case Look.MagmaColossus: return MagmaColossus(t);
                case Look.FrostWyrm: return FrostWyrm(t);
                case Look.CometOracle: return CometOracle(t);
                case Look.VoidLord: return VoidLord(t);
                default: return Hopper(t);
            }
        }

        // ================================================================== helpers (thread-safe)

        static Color V(Vector2 p, float y0, float y1, Color bottom, Color top)
            => Color.Lerp(bottom, top, MathUtil.Smooth01((p.y - y0) / (y1 - y0)));
        static Color Li(Color c, float k) => Color.Lerp(c, Color.white, k);
        static Color Dk(Color c, float k) => Color.Lerp(c, Color.black, k);
        static Color RimCol(Color top) => Color.Lerp(top, Color.white, 0.55f).WithAlpha(0.75f);
        static float Hash(int n) => MathUtil.Hash(n);
        static Rect Box(float x, float y, float w, float h) => new Rect(x, y, w, h);

        /// <summary>Light along the upper back edge (the monster faces +x).</summary>
        static void Rim(SdfCanvas c, SdfCanvas.SdfFn body, Color col, float w = 0.042f)
            => c.Paint(p => Sdf.Intersect(body(p) + 0.01f, -body(p + new Vector2(-w * 0.8f, w))), col, 0.012f);

        static LookDef New(StageTheme t, Look look, bool wisp)
        {
            var d = new LookDef { Look = look, Theme = t, Wisp = wisp };
            d.Top = wisp ? t.WispTop : t.BlobTop;
            d.Bottom = wisp ? t.WispBottom : t.BlobBottom;
            d.Glow = wisp ? t.WispGlow : t.Glow;
            d.Eye = t.Eye;
            if (wisp) { d.GlowSize = 1.7f; d.GlowAlpha = 0.3f; d.HpY = 0.62f; }
            else { d.GlowPos = new Vector2(0f, 0.35f); d.AuraPos = new Vector2(0f, 0.38f); }
            return d;
        }

        static void Draw(LookDef d, string name, Rect rect, float ppu, Vector2 pivot, System.Action<SdfCanvas> draw, System.Action<Sprite> use)
        {
            d.Pending++;
            ArtQueue.Add(d.Look + " " + name, () => { var c = new SdfCanvas(rect, ppu); draw(c); return c; }, pivot, s =>
            {
                if (s != null) { use(s); d.Owned.Add(s); }
                d.Pending--;
            });
        }

        static void DrawBody(LookDef d, Rect rect, float ppu, System.Action<SdfCanvas> draw)
        {
            Color top = d.Top;
            Draw(d, "Body", rect, ppu, Vector2.zero, c => { draw(c); Toon(c, top, 0.016f); }, s => d.Body = s);
        }

        /// <summary>
        /// The Project Rise finish on every body and part: a sunlit edge on the upper front, a band of
        /// shade on the lower back and a soft dark cartoon outline around the silhouette.
        /// </summary>
        static void Toon(SdfCanvas c, Color top, float outline)
        {
            c.RimLight(new Vector2(0.022f, 0.03f), Color.Lerp(top, new Color(1f, 0.98f, 0.9f), 0.7f).WithAlpha(0.85f), 0.55f);
            c.RimLight(new Vector2(-0.03f, -0.04f), Dk(top, 0.5f), 0.3f);
            c.Outline(Color.Lerp(Dk(top, 0.75f), new Color(0.16f, 0.12f, 0.24f), 0.5f), outline);
        }

        static PartDef Part(LookDef d, string name, float x, float y, int order, PartDef parent = null)
        {
            var p = new PartDef { Name = name, Pos = new Vector2(x, y), Order = order, Parent = parent != null ? d.Parts.IndexOf(parent) : -1 };
            d.Parts.Add(p);
            return p;
        }

        static void Share(LookDef d, string name, Rect rect, float ppu, System.Action<SdfCanvas> draw, params PartDef[] users)
            => Draw(d, name, rect, ppu, Vector2.zero, c => { draw(c); Toon(c, d.Top, 0.011f); }, s => { foreach (var u in users) u.Sprite = s; });

        static EyeDef EyeAt(LookDef d, float x, float y, float size, PartDef parent = null)
        {
            var e = new EyeDef { Pos = new Vector2(x, y), Size = size, Parent = parent != null ? d.Parts.IndexOf(parent) : -1 };
            d.Eyes.Add(e);
            return e;
        }

        static void Feet(LookDef d, Color col, float width, float scale, params Vector2[] at)
        {
            var users = new PartDef[at.Length];
            for (int i = 0; i < at.Length; i++)
                users[i] = Part(d, "Foot" + i, at[i].x, at[i].y, 59).Scaled(scale, scale).Animate(PartAnim.Foot, 1f, 1f, i * 1.7f);
            Share(d, "Foot", Box(-0.14f, -0.07f, 0.28f, 0.14f), P, c =>
            {
                c.Fill(p => Sdf.Ellipse(p, Vector2.zero, new Vector2(0.085f * width, 0.045f)), p => V(p, -0.045f, 0.045f, Dk(col, 0.25f), Li(col, 0.1f)));
                // three toe nicks on the front
                for (int k = 0; k < 2; k++)
                {
                    float tx = 0.02f + k * 0.03f * width;
                    c.Fill(p => Sdf.Capsule(p, new Vector2(tx, -0.04f), new Vector2(tx, -0.015f), 0.004f), Dk(col, 0.45f));
                }
            }, users);
        }

        static bool FeatureGlows(MonsterFeature f) => f == MonsterFeature.Flames || f == MonsterFeature.Lamps || f == MonsterFeature.Stars;
        static float FeatureTilt(MonsterFeature f) => f == MonsterFeature.Lamps || f == MonsterFeature.Stars ? 0.35f : 1f;

        /// <summary>The stage's head feature (horns, thorns, crystals, flames, ice, antennae, void shards).</summary>
        static void Feature(LookDef d, StageTheme t, params PartDef[] users)
        {
            var f = t.Feature;
            Color top = t.BlobTop, glow = t.Glow;
            if (FeatureGlows(f)) foreach (var u in users) u.Mat = PartMat.Emissive;
            Share(d, "Feature", Box(-0.12f, -0.05f, 0.26f, 0.37f), P, c => DrawFeature(c, f, top, glow), users);
        }

        static void DrawFeature(SdfCanvas c, MonsterFeature f, Color top, Color glow)
        {
            Color light = Color.Lerp(top, Color.white, 0.5f);
            switch (f)
            {
                case MonsterFeature.Thorns:
                    c.Fill(p => Sdf.Triangle(p, new Vector2(-0.045f, 0f), new Vector2(0.045f, 0f), new Vector2(0.03f, 0.25f)),
                        p => V(p, 0f, 0.25f, Dk(top, 0.3f), Li(glow, 0.3f)));
                    c.Fill(p => Sdf.Triangle(p, new Vector2(-0.02f, 0.06f), new Vector2(0.04f, 0.1f), new Vector2(0.09f, 0.16f)), Dk(top, 0.15f));
                    break;
                case MonsterFeature.Crystals:
                    c.Fill(p => Sdf.SmoothUnion(Sdf.Box(p, new Vector2(0.01f, 0.1f), new Vector2(0.04f, 0.1f), 0.005f, -8f),
                        Sdf.Triangle(p, new Vector2(-0.03f, 0.19f), new Vector2(0.06f, 0.2f), new Vector2(0.03f, 0.29f)), 0.01f),
                        p => V(p, 0f, 0.29f, Color.Lerp(glow, top, 0.5f), Li(glow, 0.6f)));
                    c.Paint(p => Sdf.Box(p, new Vector2(0.025f, 0.12f), new Vector2(0.012f, 0.09f), 0.004f, -8f), Color.white.WithAlpha(0.6f), 0.01f);
                    break;
                case MonsterFeature.Flames:
                    c.Fill(p => Sdf.SmoothUnion(Sdf.Circle(p, new Vector2(0f, 0.06f), 0.055f),
                        Sdf.Tapered(p, new Vector2(0f, 0.06f), 0.05f, new Vector2(0.03f, 0.28f), 0.004f), 0.03f),
                        p => V(p, 0f, 0.28f, glow, new Color(1f, 0.95f, 0.6f)));
                    c.Paint(p => Sdf.Tapered(p, new Vector2(0f, 0.06f), 0.025f, new Vector2(0.015f, 0.18f), 0.003f), new Color(1f, 1f, 0.85f, 0.9f), 0.015f);
                    break;
                case MonsterFeature.Ice:
                    c.Fill(p => Sdf.Union(Sdf.Triangle(p, new Vector2(-0.04f, 0f), new Vector2(0.05f, 0f), new Vector2(0.02f, 0.3f)),
                        Sdf.Triangle(p, new Vector2(0.01f, 0.02f), new Vector2(0.08f, 0.04f), new Vector2(0.1f, 0.17f))),
                        p => V(p, 0f, 0.3f, new Color(0.62f, 0.82f, 0.95f, 0.95f), new Color(0.95f, 1f, 1f, 0.95f)));
                    c.Paint(p => Sdf.Capsule(p, new Vector2(0f, 0.03f), new Vector2(0.018f, 0.24f), 0.008f), Color.white.WithAlpha(0.7f), 0.008f);
                    break;
                case MonsterFeature.Stars:
                case MonsterFeature.Lamps:
                    c.Fill(p => Sdf.Capsule(p, Vector2.zero, new Vector2(0.01f, 0.2f), 0.012f), Dk(top, 0.2f));
                    if (f == MonsterFeature.Stars) c.Fill(p => Sdf.Star4(p, new Vector2(0.01f, 0.24f), 0.07f, 0.55f), Li(glow, 0.4f));
                    else
                    {
                        c.Fill(p => Sdf.Box(p, new Vector2(0.01f, 0.25f), new Vector2(0.04f, 0.05f), 0.015f), Dk(top, 0.4f));
                        c.Fill(p => Sdf.Box(p, new Vector2(0.01f, 0.25f), new Vector2(0.026f, 0.036f), 0.012f), new Color(1f, 0.9f, 0.6f));
                    }
                    break;
                case MonsterFeature.Void:
                    c.Fill(p => Sdf.Union(Sdf.Triangle(p, new Vector2(-0.05f, 0f), new Vector2(0.04f, 0f), new Vector2(-0.01f, 0.3f)),
                        Sdf.Triangle(p, new Vector2(0f, 0.05f), new Vector2(0.07f, 0.06f), new Vector2(0.09f, 0.2f))), Dk(top, 0.5f));
                    c.Paint(p => Sdf.Intersect(Sdf.Triangle(p, new Vector2(-0.05f, 0f), new Vector2(0.04f, 0f), new Vector2(-0.01f, 0.3f)) + 0.012f,
                        -Sdf.Triangle(p + new Vector2(-0.008f, 0f), new Vector2(-0.05f, 0f), new Vector2(0.04f, 0f), new Vector2(-0.01f, 0.3f))), glow, 0.01f);
                    break;
                default:   // horns
                    c.Fill(p => Sdf.Tapered(p, Vector2.zero, 0.055f, new Vector2(0.06f, 0.22f), 0.008f), p => V(p, 0f, 0.22f, top, light));
                    c.Paint(p => Sdf.Capsule(p, new Vector2(0.02f, 0.05f), new Vector2(0.055f, 0.18f), 0.006f), Li(top, 0.7f).WithAlpha(0.5f), 0.008f);
                    break;
            }
        }

        /// <summary>A soft, glowing blob sprite (cores, sparks, comet tail, smoke).</summary>
        static void SoftBall(SdfCanvas c, float r, Color inner, Color outer)
        {
            c.Field(p =>
            {
                float k = p.magnitude / r;
                if (k >= 1f) return new Color(0f, 0f, 0f, 0f);
                Color col = Color.Lerp(inner, outer, MathUtil.Smooth01(k));
                col.a *= MathUtil.Smooth01((1f - k) / 0.6f);
                return col;
            });
        }

        /// <summary>A bat-like wing: an arm bone with finger struts and a scalloped membrane, pivot at the shoulder.</summary>
        static void Wing(SdfCanvas c, float len, Color membrane, Color bone)
        {
            Vector2 elbow = new Vector2(-0.12f, 0.2f) * len, tip = new Vector2(-0.42f, 0.34f) * len;
            Vector2 f1 = new Vector2(-0.5f, 0.12f) * len, f2 = new Vector2(-0.38f, -0.02f) * len;
            SdfCanvas.SdfFn mem = p =>
            {
                float d = Mathf.Min(Sdf.Triangle(p, Vector2.zero, elbow, f2), Sdf.Triangle(p, elbow, tip, f1));
                d = Mathf.Min(d, Sdf.Triangle(p, elbow, f1, f2));
                // scallops between the finger tips
                d = Mathf.Max(d, -Sdf.Circle(p, (f1 + f2) * 0.5f + new Vector2(0.04f, -0.02f) * len, 0.08f * len));
                d = Mathf.Max(d, -Sdf.Circle(p, (tip + f1) * 0.5f + new Vector2(0.03f, -0.03f) * len, 0.06f * len));
                return d;
            };
            c.Fill(mem, p => V(p, -0.05f * len, 0.35f * len, Dk(membrane, 0.35f), membrane).WithAlpha(0.92f));
            c.Paint(p => Sdf.Intersect(mem(p) + 0.008f, -mem(p + new Vector2(0.02f, 0.02f))), Li(membrane, 0.45f).WithAlpha(0.6f), 0.01f);
            c.Fill(p => Mathf.Min(Mathf.Min(Sdf.Capsule(p, Vector2.zero, elbow, 0.018f * len), Sdf.Capsule(p, elbow, tip, 0.012f * len)),
                Mathf.Min(Sdf.Capsule(p, elbow, f1, 0.009f * len), Sdf.Capsule(p, elbow, f2, 0.009f * len))), bone);
        }

        /// <summary>A claw hand: palm and three long curled fingers, pivot at the wrist.</summary>
        static void Claw(SdfCanvas c, float s, Color dark, Color light)
        {
            SdfCanvas.SdfFn hand = p =>
            {
                float d = Sdf.Ellipse(p, new Vector2(0.02f, 0f) * s, new Vector2(0.06f, 0.05f) * s);
                for (int k = 0; k < 3; k++)
                {
                    float a = -20f + k * 28f;
                    Vector2 b = new Vector2(0.05f, -0.01f + k * 0.02f) * s;
                    Vector2 m = b + MathUtil.Dir(a) * 0.09f * s;
                    Vector2 e = m + MathUtil.Dir(a - 55f) * 0.07f * s;
                    d = Mathf.Min(d, Mathf.Min(Sdf.Tapered(p, b, 0.018f * s, m, 0.012f * s), Sdf.Tapered(p, m, 0.012f * s, e, 0.002f * s)));
                }
                return d;
            };
            c.Fill(hand, p => V(p, -0.1f * s, 0.1f * s, dark, light));
            Rim(c, hand, Li(light, 0.4f).WithAlpha(0.6f), 0.02f * s);
        }

        /// <summary>A heavy arm hanging from the shoulder with a big fist, pivot at the shoulder.</summary>
        static void Arm(SdfCanvas c, float len, float fist, Color dark, Color light, Color knuckle)
        {
            Vector2 wrist = new Vector2(0.06f, -len);
            SdfCanvas.SdfFn arm = p => Sdf.SmoothUnion(Sdf.Tapered(p, Vector2.zero, 0.1f * fist, wrist, 0.075f * fist),
                Sdf.Box(p, wrist + new Vector2(0.03f, -0.07f * fist), new Vector2(0.11f, 0.09f) * fist, 0.05f * fist), 0.04f);
            c.Fill(arm, p => V(p, -len - 0.15f, 0.05f, dark, light));
            // knuckles
            for (int k = 0; k < 3; k++)
            {
                Vector2 kc = wrist + new Vector2(0.1f * fist, (-0.03f - k * 0.05f) * fist);
                c.Paint(p => Sdf.Circle(p, kc, 0.022f * fist), knuckle, 0.01f);
            }
            Rim(c, arm, Li(light, 0.45f).WithAlpha(0.6f), 0.03f);
        }

        static Rect ArmRect(float len, float fist) => Box(-0.16f * fist - 0.05f, -len - 0.24f * fist, 0.4f * fist + 0.12f, len + 0.36f * fist);

        // ================================================================== archetypes

        /// <summary>Düsterling: the classic horned dome that hops after the player.</summary>
        static LookDef Hopper(StageTheme t)
        {
            var d = New(t, Look.Hopper, false);
            Color top = t.BlobTop, bottom = t.BlobBottom, glow = t.Glow;
            var f = t.Feature;
            Feet(d, bottom, 1f, 1f, new Vector2(-0.2f, 0.03f), new Vector2(0.22f, 0.03f));
            float tilt = FeatureTilt(f);
            var hl = Part(d, "HornL", -0.2f, 0.66f, 59).Turn(22f * tilt).Animate(PartAnim.Sway, 3f, 2.4f);
            var hr = Part(d, "HornR", 0.18f, 0.68f, 59).Turn(16f * tilt).Scaled(-1f, 1f).Animate(PartAnim.Sway, 3f, 2.4f, 1f);
            Feature(d, t, hl, hr);
            EyeAt(d, 0.02f, 0.44f, 0.9f);
            EyeAt(d, 0.22f, 0.44f, 1f);
            bool veins = f == MonsterFeature.Crystals || f == MonsterFeature.Flames || f == MonsterFeature.Void;
            DrawBody(d, Box(-0.55f, -0.05f, 1.1f, 0.95f), P, c =>
            {
                SdfCanvas.SdfFn body = p => Sdf.SmoothUnion(Sdf.Ellipse(p, new Vector2(0f, 0.39f), new Vector2(0.45f, 0.39f)),
                    Sdf.Box(p, new Vector2(0f, 0.16f), new Vector2(0.43f, 0.16f), 0.14f), 0.08f);
                c.Fill(body, p => V(p, 0f, 0.78f, bottom, top));
                c.Paint(p => Sdf.Ellipse(p, new Vector2(0.02f, 0.26f), new Vector2(0.24f, 0.16f)), Color.Lerp(top, glow, 0.35f).WithAlpha(0.55f), 0.12f);
                Rim(c, body, RimCol(top));
                if (veins)
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 a = new Vector2(-0.3f + i * 0.18f, 0.18f + Hash(i * 5 + 1) * 0.06f);
                        Vector2 b = a + new Vector2(0.08f + Hash(i * 9 + 3) * 0.06f, 0.22f + Hash(i * 3 + 7) * 0.12f);
                        c.Paint(p => Sdf.Intersect(Sdf.Capsule(p, a, b, 0.012f), body(p) + 0.03f), glow.WithAlpha(0.8f), 0.01f);
                    }
                else
                    for (int i = 0; i < 7; i++)
                    {
                        Vector2 sp = new Vector2(Hash(i * 7 + 1) * 0.3f - 0.1f, 0.35f + Hash(i * 13 + 5) * 0.22f);
                        float sr = 0.018f + 0.012f * Mathf.Abs(Hash(i * 3 + 2));
                        c.Paint(p => Sdf.Circle(p, sp, sr), bottom.WithAlpha(0.45f), 0.006f);
                    }
                // a small grin with two fangs under the eyes
                c.Fill(p => Sdf.Subtract(Sdf.Ellipse(p, new Vector2(0.19f, 0.27f), new Vector2(0.1f, 0.045f)), Sdf.Ellipse(p, new Vector2(0.19f, 0.305f), new Vector2(0.115f, 0.04f))),
                    Dk(bottom, 0.55f));
                foreach (float fx in new[] { 0.15f, 0.24f })
                {
                    float x = fx;
                    c.Fill(p => Sdf.Triangle(p, new Vector2(x - 0.014f, 0.27f), new Vector2(x + 0.014f, 0.27f), new Vector2(x, 0.24f)), new Color(0.95f, 0.93f, 0.88f));
                }
            });
            return d;
        }

        /// <summary>Spawnling: a little seed with one big eye and a sprout that wobbles on its head.</summary>
        static LookDef Spawnling(StageTheme t)
        {
            var d = New(t, Look.Spawnling, false);
            Color top = t.BlobTop, bottom = t.BlobBottom, glow = t.Glow;
            Feet(d, bottom, 0.8f, 0.75f, new Vector2(-0.12f, 0.02f), new Vector2(0.13f, 0.02f));
            var sprout = Part(d, "Sprout", -0.13f, 0.72f, 59).Turn(20f).Animate(PartAnim.Sway, 14f, 3.4f);
            Share(d, "Sprout", Box(-0.16f, -0.03f, 0.32f, 0.3f), P, c =>
            {
                c.Fill(p => Sdf.Tapered(p, Vector2.zero, 0.018f, new Vector2(0.01f, 0.12f), 0.01f), Dk(top, 0.2f));
                SdfCanvas.SdfFn leaf = p => Mathf.Min(Sdf.Ellipse(MathUtil.Rotate(p - new Vector2(0.06f, 0.16f), -35f), Vector2.zero, new Vector2(0.075f, 0.03f)),
                    Sdf.Ellipse(MathUtil.Rotate(p - new Vector2(-0.05f, 0.14f), 30f), Vector2.zero, new Vector2(0.06f, 0.025f)));
                c.Fill(leaf, p => V(p, 0.1f, 0.2f, Color.Lerp(top, glow, 0.4f), Li(glow, 0.35f)));
            }, sprout);
            EyeAt(d, 0.07f, 0.34f, 1.55f);
            d.GlowPos = new Vector2(0f, 0.3f);
            d.HpY = 0.95f;
            DrawBody(d, Box(-0.42f, -0.05f, 0.84f, 0.9f), P, c =>
            {
                SdfCanvas.SdfFn body = p => Sdf.SmoothUnion(Sdf.Circle(p, new Vector2(0f, 0.3f), 0.3f),
                    Sdf.Tapered(p, new Vector2(0f, 0.42f), 0.2f, new Vector2(-0.13f, 0.74f), 0.03f), 0.08f);
                c.Fill(body, p => V(p, 0f, 0.7f, bottom, top));
                // husk stripes curling over the top
                c.Paint(p => Sdf.Intersect(Mathf.Abs(Mathf.Sin((p.y * 0.9f - p.x * 0.4f) * 26f)) * 0.02f - 0.004f, 0.45f - p.y), Dk(top, 0.25f).WithAlpha(0.55f), 0.004f);
                c.Paint(p => Sdf.Ellipse(p, new Vector2(0.03f, 0.2f), new Vector2(0.17f, 0.1f)), Color.Lerp(top, glow, 0.4f).WithAlpha(0.5f), 0.08f);
                Rim(c, body, RimCol(top), 0.035f);
                c.Fill(p => Sdf.Subtract(Sdf.Ellipse(p, new Vector2(0.1f, 0.18f), new Vector2(0.06f, 0.028f)), Sdf.Ellipse(p, new Vector2(0.1f, 0.2f), new Vector2(0.07f, 0.025f))), Dk(bottom, 0.5f));
            });
            return d;
        }

        /// <summary>Knospling: a fat pod with two sleeping seed-buds on its shoulders — the spawnlings it bursts into.</summary>
        static LookDef Splitter(StageTheme t)
        {
            var d = New(t, Look.Splitter, false);
            Color top = t.BlobTop, bottom = t.BlobBottom, glow = t.Glow;
            Feet(d, bottom, 1.1f, 1.05f, new Vector2(-0.28f, 0.03f), new Vector2(0.02f, 0.02f), new Vector2(0.3f, 0.03f));
            var budL = Part(d, "BudL", -0.24f, 0.56f, 59).Turn(28f).Animate(PartAnim.Pulse, 0.05f, 2.6f);
            var budR = Part(d, "BudR", 0.18f, 0.6f, 59).Turn(-14f).Animate(PartAnim.Pulse, 0.05f, 2.6f, 1.9f);
            Share(d, "Bud", Box(-0.2f, -0.05f, 0.4f, 0.42f), P, c =>
            {
                SdfCanvas.SdfFn bud = p => Sdf.SmoothUnion(Sdf.Circle(p, new Vector2(0f, 0.13f), 0.13f),
                    Sdf.Tapered(p, new Vector2(0f, 0.18f), 0.09f, new Vector2(-0.06f, 0.33f), 0.012f), 0.05f);
                c.Fill(bud, p => V(p, 0f, 0.3f, Dk(top, 0.1f), Li(top, 0.18f)));
                c.Paint(p => Sdf.Ellipse(p, new Vector2(0.01f, 0.1f), new Vector2(0.08f, 0.05f)), Color.Lerp(top, glow, 0.5f).WithAlpha(0.6f), 0.05f);
                Rim(c, bud, RimCol(top), 0.025f);
                // a closed, sleeping eye
                c.Fill(p => Sdf.Subtract(Sdf.Ellipse(p, new Vector2(0.03f, 0.15f), new Vector2(0.045f, 0.022f)), Sdf.Ellipse(p, new Vector2(0.03f, 0.168f), new Vector2(0.05f, 0.02f))),
                    Dk(bottom, 0.5f));
            }, budL, budR);
            EyeAt(d, 0.05f, 0.42f, 0.8f);
            EyeAt(d, 0.26f, 0.4f, 0.95f);
            d.HpY = 1.05f;
            DrawBody(d, Box(-0.6f, -0.05f, 1.2f, 0.9f), P, c =>
            {
                SdfCanvas.SdfFn body = p => Sdf.SmoothUnion(Sdf.Ellipse(p, new Vector2(0f, 0.34f), new Vector2(0.5f, 0.35f)),
                    Sdf.Box(p, new Vector2(0f, 0.12f), new Vector2(0.47f, 0.12f), 0.1f), 0.08f);
                c.Fill(body, p => V(p, 0f, 0.7f, bottom, top));
                c.Paint(p => Sdf.Ellipse(p, new Vector2(0.06f, 0.24f), new Vector2(0.3f, 0.15f)), Color.Lerp(top, glow, 0.3f).WithAlpha(0.5f), 0.12f);
                // the seam it will split along, glowing, with veins branching off
                SdfCanvas.SdfFn seam = p =>
                {
                    float x = -0.08f + 0.035f * Mathf.Sin(p.y * 28f);
                    return Mathf.Max(Mathf.Abs(p.x - x) - 0.012f, Mathf.Max(0.06f - p.y, p.y - 0.66f));
                };
                c.Paint(p => Sdf.Intersect(seam(p) - 0.018f, body(p) + 0.02f), Color.Lerp(top, glow, 0.6f).WithAlpha(0.7f), 0.015f);
                c.Paint(p => Sdf.Intersect(seam(p), body(p) + 0.02f), Li(glow, 0.4f), 0.005f);
                for (int i = 0; i < 6; i++)
                {
                    float y0 = 0.14f + i * 0.08f;
                    float dir = i % 2 == 0 ? 1f : -1f;
                    Vector2 a = new Vector2(-0.08f, y0), b = a + new Vector2(dir * (0.12f + 0.05f * Hash(i)), 0.06f);
                    c.Paint(p => Sdf.Intersect(Sdf.Capsule(p, a, b, 0.006f), body(p) + 0.03f), Dk(top, 0.3f).WithAlpha(0.6f), 0.006f);
                }
                Rim(c, body, RimCol(top));
                // jagged grin
                SdfCanvas.SdfFn mouth = p => Sdf.Ellipse(p, new Vector2(0.18f, 0.22f), new Vector2(0.15f, 0.05f));
                c.Fill(mouth, Dk(bottom, 0.6f));
                for (int k = 0; k < 5; k++)
                {
                    float x = 0.07f + k * 0.055f;
                    bool up = k % 2 == 0;
                    c.Fill(p => Sdf.Intersect(Sdf.Triangle(p, new Vector2(x - 0.02f, up ? 0.26f : 0.18f), new Vector2(x + 0.02f, up ? 0.26f : 0.18f), new Vector2(x, up ? 0.215f : 0.225f)), mouth(p)),
                        new Color(0.95f, 0.92f, 0.86f));
                }
            });
            return d;
        }

        /// <summary>Spucker: a squat toad sac with a flared spout, eyes on stalks and a throat that swells before it spits.</summary>
        static LookDef Spitter(StageTheme t)
        {
            var d = New(t, Look.Spitter, false);
            Color top = t.BlobTop, bottom = t.BlobBottom, glow = t.Glow;
            var leg = Part(d, "Haunch", -0.22f, 0.07f, 61).Animate(PartAnim.Foot, 1f, 1f);
            Draw(d, "Haunch", Box(-0.22f, -0.1f, 0.44f, 0.36f), P, Vector2.zero, c =>
            {
                SdfCanvas.SdfFn h = p => Sdf.SmoothUnion(Sdf.Ellipse(p, new Vector2(0f, 0.1f), new Vector2(0.15f, 0.12f)),
                    Sdf.Ellipse(p, new Vector2(0.08f, -0.04f), new Vector2(0.13f, 0.045f)), 0.04f);
                c.Fill(h, p => V(p, -0.08f, 0.22f, Dk(bottom, 0.1f), Color.Lerp(bottom, top, 0.7f)));
                Rim(c, h, RimCol(top), 0.03f);
            }, s => leg.Sprite = s);
            Feet(d, bottom, 1.2f, 1f, new Vector2(0.24f, 0.03f));
            var sac = Part(d, "Throat", 0.25f, 0.38f, 61).Animate(PartAnim.Pulse, 0.035f, 3f, 0f, 0.4f);
            Draw(d, "Throat", Box(-0.2f, -0.17f, 0.4f, 0.34f), P, Vector2.zero, c =>
            {
                SdfCanvas.SdfFn s = p => Sdf.Ellipse(p, new Vector2(0f, -0.02f), new Vector2(0.11f, 0.13f));
                c.Fill(s, p => V(p, -0.12f, 0.12f, Color.Lerp(top, glow, 0.25f), Li(top, 0.35f)));
                c.Paint(p => Sdf.Ellipse(p, new Vector2(-0.04f, 0.05f), new Vector2(0.05f, 0.025f)), Color.white.WithAlpha(0.35f), 0.03f);
                for (int k = 0; k < 3; k++) { float y = -0.06f + k * 0.045f; c.Paint(p => Mathf.Abs(p.y - y) - 0.004f, Dk(top, 0.1f).WithAlpha(0.35f), 0.004f); }
            }, s => sac.Sprite = s);
            var stalkL = Part(d, "StalkL", 0.02f, 0.58f, 59).Turn(24f).Animate(PartAnim.Sway, 9f, 2.2f);
            var stalkR = Part(d, "StalkR", 0.13f, 0.62f, 59).Turn(4f).Animate(PartAnim.Sway, 9f, 2.2f, 1.3f);
            Share(d, "Stalk", Box(-0.06f, -0.03f, 0.12f, 0.3f), P, c =>
                c.Fill(p => Sdf.Tapered(p, Vector2.zero, 0.035f, new Vector2(0f, 0.22f), 0.022f), p => V(p, 0f, 0.22f, top, Li(top, 0.2f))), stalkL, stalkR);
            EyeAt(d, 0f, 0.24f, 0.62f, stalkL);
            EyeAt(d, 0f, 0.24f, 0.7f, stalkR);
            d.HpY = 1.05f;
            DrawBody(d, Box(-0.56f, -0.05f, 1.02f, 0.95f), P, c =>
            {
                SdfCanvas.SdfFn body = p => Sdf.SmoothUnion(Sdf.Ellipse(p, new Vector2(-0.04f, 0.25f), new Vector2(0.46f, 0.26f)),
                    Sdf.Tapered(p, new Vector2(0.06f, 0.36f), 0.2f, new Vector2(0.23f, 0.7f), 0.1f), 0.1f);
                c.Fill(body, p => V(p, 0f, 0.75f, bottom, top));
                // warts on the sac
                for (int i = 0; i < 9; i++)
                {
                    Vector2 w = new Vector2(-0.36f + Hash(i * 5 + 2) * 0.1f + i * 0.07f, 0.2f + Hash(i * 11 + 1) * 0.14f + 0.08f);
                    float r = 0.02f + 0.012f * Mathf.Abs(Hash(i * 7));
                    c.Paint(p => Sdf.Circle(p, w, r), Li(top, 0.25f).WithAlpha(0.6f), 0.008f);
                }
                Rim(c, body, RimCol(top));
                // the flared spout: a lip ring and a glowing throat
                Vector2 m = new Vector2(0.24f, 0.72f);
                c.Fill(p => Sdf.Ellipse(p, m, new Vector2(0.15f, 0.07f)), p => V(p, m.y - 0.07f, m.y + 0.07f, top, Li(top, 0.3f)));
                c.Fill(p => Sdf.Ellipse(p, m + new Vector2(0f, 0.015f), new Vector2(0.105f, 0.042f)), p => Color.Lerp(Li(glow, 0.5f), Dk(bottom, 0.4f), MathUtil.Smooth01((p - m).magnitude / 0.1f)));
            });
            return d;
        }

        /// <summary>Koloss: a hunched, armour-plated brute with two huge fists, tusks and spikes along its back.</summary>
        static LookDef Brute(StageTheme t)
        {
            var d = New(t, Look.Brute, false);
            Color top = t.BlobTop, bottom = t.BlobBottom, glow = t.Glow;
            var armB = Part(d, "ArmBack", -0.04f, 0.5f, 58).Animate(PartAnim.Swing, 16f, 2f, 1.4f, 40f);
            Feet(d, bottom, 1.3f, 1.25f, new Vector2(-0.22f, 0.03f), new Vector2(0.14f, 0.03f));
            float tilt = FeatureTilt(t.Feature);
            var s1 = Part(d, "Spike1", -0.38f, 0.66f, 59).Turn(48f * tilt).Animate(PartAnim.Sway, 2f, 2f);
            var s2 = Part(d, "Spike2", -0.22f, 0.8f, 59).Turn(26f * tilt).Scaled(1.15f, 1.15f).Animate(PartAnim.Sway, 2f, 2f, 0.7f);
            var s3 = Part(d, "Spike3", -0.04f, 0.8f, 59).Turn(6f * tilt).Animate(PartAnim.Sway, 2f, 2f, 1.4f);
            Feature(d, t, s1, s2, s3);
            var armF = Part(d, "ArmFront", 0.18f, 0.48f, 62).Animate(PartAnim.Swing, 16f, 2f, 0f, 40f);
            Share(d, "Arm", ArmRect(0.34f, 1f), P, c => Arm(c, 0.34f, 1f, Dk(bottom, 0.05f), Color.Lerp(bottom, top, 0.85f), Li(top, 0.2f)), armB, armF);
            armB.Tint = new Color(0.72f, 0.72f, 0.78f);
            EyeAt(d, 0.34f, 0.47f, 0.52f);
            EyeAt(d, 0.43f, 0.46f, 0.48f);
            d.HpY = 1.1f;
            DrawBody(d, Box(-0.6f, -0.05f, 1.22f, 1f), P, c =>
            {
                SdfCanvas.SdfFn torso = p => Sdf.SmoothUnion(Sdf.Ellipse(p, new Vector2(-0.06f, 0.38f), new Vector2(0.48f, 0.37f)),
                    Sdf.Ellipse(p, new Vector2(-0.16f, 0.6f), new Vector2(0.34f, 0.25f)), 0.12f);
                SdfCanvas.SdfFn body = p => Sdf.SmoothUnion(torso(p), Sdf.SmoothUnion(Sdf.Ellipse(p, new Vector2(0.3f, 0.42f), new Vector2(0.2f, 0.17f)),
                    Sdf.Box(p, new Vector2(0.33f, 0.3f), new Vector2(0.15f, 0.07f), 0.05f), 0.04f), 0.06f);
                c.Fill(body, p => V(p, 0f, 0.84f, bottom, top));
                // overlapping armour bands across the hump (like a pangolin), each with a lit front edge
                for (int i = 0; i < 4; i++)
                {
                    float bx = -0.44f + i * 0.13f;
                    System.Func<Vector2, float> across = p => p.x - bx - (p.y - 0.55f) * 0.45f;
                    SdfCanvas.SdfFn band = p => Sdf.Intersect(Mathf.Abs(across(p)) - 0.058f, Sdf.Intersect(body(p) + 0.035f, 0.4f - p.y + (p.x - bx) * 0.15f));
                    c.Paint(band, Dk(top, 0.32f), 0.006f);
                    c.Paint(p => Sdf.Intersect(band(p), Mathf.Abs(across(p) - 0.045f) - 0.013f), Li(top, 0.28f).WithAlpha(0.85f), 0.005f);
                    c.Paint(p => Sdf.Intersect(band(p), Mathf.Abs(across(p) + 0.05f) - 0.006f), Dk(top, 0.55f).WithAlpha(0.7f), 0.004f);
                }
                Rim(c, body, RimCol(top), 0.05f);
                // heavy brow, tusks, nostrils
                c.Fill(p => Sdf.Ellipse(p, new Vector2(0.37f, 0.51f), new Vector2(0.13f, 0.035f)), Dk(bottom, 0.3f));
                foreach (float tx in new[] { 0.28f, 0.42f })
                {
                    float x = tx;
                    c.Fill(p => Sdf.Tapered(p, new Vector2(x, 0.3f), 0.025f, new Vector2(x + 0.03f, 0.42f), 0.004f), p => V(p, 0.3f, 0.42f, new Color(0.78f, 0.74f, 0.64f), new Color(0.98f, 0.96f, 0.9f)));
                }
                c.Fill(p => Mathf.Min(Sdf.Circle(p, new Vector2(0.47f, 0.4f), 0.012f), Sdf.Circle(p, new Vector2(0.43f, 0.39f), 0.011f)), Dk(bottom, 0.6f));
                c.Paint(p => Sdf.Ellipse(p, new Vector2(0.05f, 0.24f), new Vector2(0.26f, 0.12f)), Color.Lerp(top, glow, 0.25f).WithAlpha(0.35f), 0.1f);
            });
            return d;
        }

        /// <summary>Bombe: an iron-banded bomb with a sparking fuse and glowing cracks that flare when it primes.</summary>
        static LookDef Bomber(StageTheme t)
        {
            var d = New(t, Look.Bomber, false);
            Color top = t.BlobTop, bottom = t.BlobBottom, glow = t.Glow;
            Feet(d, bottom, 0.9f, 0.85f, new Vector2(-0.16f, 0.02f), new Vector2(0.16f, 0.02f));
            var cracks = Part(d, "Cracks", 0f, 0f, 61).Shine(PartMat.Glow, glow).Animate(PartAnim.Breathe, 0.35f, 3f, 0f, 1f);
            var fuse = Part(d, "Fuse", 0.02f, 0.78f, 59).Animate(PartAnim.Sway, 6f, 3f);
            Draw(d, "Fuse", Box(-0.16f, -0.03f, 0.24f, 0.3f), P, Vector2.zero, c =>
            {
                Vector2 a = Vector2.zero, b = new Vector2(-0.02f, 0.1f), e = new Vector2(-0.09f, 0.2f);
                c.Fill(p => Mathf.Min(Sdf.Capsule(p, a, b, 0.016f), Sdf.Capsule(p, b, e, 0.014f)), new Color(0.42f, 0.36f, 0.28f));
                for (int k = 0; k < 4; k++) { float y = 0.03f + k * 0.045f; c.Paint(p => Mathf.Abs(p.y - y + p.x * 0.6f) - 0.004f, new Color(0.25f, 0.2f, 0.15f), 0.004f); }
            }, s => fuse.Sprite = s);
            var spark = Part(d, "Spark", -0.09f, 0.21f, 64, fuse).Shine(PartMat.Glow, Palette.Gold).Animate(PartAnim.Flicker, 0.35f, 30f, 0f, 1f);
            Draw(d, "Spark", Box(-0.14f, -0.14f, 0.28f, 0.28f), P, Vector2.zero, c =>
            {
                SoftBall(c, 0.13f, new Color(1f, 0.95f, 0.7f, 0.9f), new Color(1f, 0.6f, 0.2f, 0f));
                c.Fill(p => Sdf.Star4(p, Vector2.zero, 0.1f, 0.55f), new Color(1f, 1f, 0.9f));
            }, s => spark.Sprite = s);
            EyeAt(d, 0.06f, 0.42f, 0.72f);
            EyeAt(d, 0.23f, 0.41f, 0.78f);
            d.HpY = 1f;
            SdfCanvas.SdfFn shell = p => Sdf.SmoothUnion(Sdf.Circle(p, new Vector2(0f, 0.37f), 0.36f), Sdf.Box(p, new Vector2(0f, 0.1f), new Vector2(0.28f, 0.1f), 0.08f), 0.06f);
            System.Func<Vector2, float> crackFn = p =>
            {
                float n = Noise.Perlin(p.x * 7f + 3f, p.y * 7f);
                return Mathf.Abs(n - 0.5f) - 0.018f;
            };
            DrawBody(d, Box(-0.48f, -0.05f, 0.96f, 0.9f), P, c =>
            {
                Color iron = Dk(bottom, 0.25f);
                c.Fill(shell, p => V(p, 0f, 0.73f, Dk(iron, 0.2f), Color.Lerp(iron, top, 0.55f)));
                c.Paint(p => Sdf.Intersect(crackFn(p), shell(p) + 0.05f), Color.Lerp(iron, glow, 0.45f), 0.006f);
                // riveted iron band and a big sheen
                c.Paint(p => Mathf.Abs(p.y - 0.34f) - 0.035f, Color.Lerp(iron, Li(top, 0.2f), 0.5f), 0.006f);
                for (int k = 0; k < 6; k++) { float x = -0.3f + k * 0.12f; c.Paint(p => Sdf.Circle(p, new Vector2(x, 0.34f), 0.013f), Li(top, 0.45f), 0.005f); }
                c.Paint(p => Sdf.Intersect(Mathf.Abs(Sdf.Circle(p, new Vector2(0.03f, 0.36f), 0.28f)) - 0.02f, Sdf.HalfPlane(p, new Vector2(-0.05f, 0.45f), new Vector2(0.7f, -1f))),
                    Color.white.WithAlpha(0.28f), 0.02f);
                Rim(c, shell, RimCol(top));
                // nozzle cap on top
                c.Fill(p => Sdf.Box(p, new Vector2(0.02f, 0.75f), new Vector2(0.085f, 0.05f), 0.02f), p => V(p, 0.7f, 0.8f, Dk(iron, 0.3f), Li(iron, 0.3f)));
                // angry brows and gritted teeth
                c.Fill(p => Mathf.Min(Sdf.Capsule(p, new Vector2(-0.01f, 0.52f), new Vector2(0.11f, 0.48f), 0.018f), Sdf.Capsule(p, new Vector2(0.17f, 0.48f), new Vector2(0.3f, 0.52f), 0.018f)), Dk(bottom, 0.6f));
                SdfCanvas.SdfFn mouth = p => Sdf.Box(p, new Vector2(0.15f, 0.25f), new Vector2(0.1f, 0.04f), 0.02f);
                c.Fill(mouth, new Color(0.92f, 0.9f, 0.84f));
                c.Paint(p => Sdf.Intersect(Mathf.Min(Mathf.Abs(Mathf.Repeat(p.x - 0.05f, 0.04f) - 0.02f) - 0.004f, Mathf.Abs(p.y - 0.25f) - 0.004f), mouth(p)), Dk(bottom, 0.4f), 0.003f);
            });
            Draw(d, "Cracks", Box(-0.48f, -0.05f, 0.96f, 0.9f), P, Vector2.zero, c =>
                c.Fill(p => Sdf.Intersect(crackFn(p) + 0.004f, shell(p) + 0.05f), p => new Color(1f, 1f, 1f, 1f), 0.012f), s => cracks.Sprite = s);
            return d;
        }

        /// <summary>Irrlicht: a dart-shaped flyer with bat wings and a whip tail; folds its wings to dive.</summary>
        static LookDef Diver(StageTheme t)
        {
            var d = New(t, Look.Diver, true);
            Color top = t.WispTop, bottom = t.WispBottom, glow = t.WispGlow;
            var wingFar = Part(d, "WingFar", -0.02f, 0.07f, 58).Animate(PartAnim.Flap, 30f, 14f, 0.5f, -60f);
            var wingNear = Part(d, "WingNear", 0f, 0.06f, 61).Scaled(1.1f, 1.1f).Animate(PartAnim.Flap, 30f, 14f, 0f, -60f);
            wingFar.Tint = new Color(0.7f, 0.7f, 0.8f);
            Share(d, "Wing", Box(-0.56f, -0.1f, 0.62f, 0.52f), P, c => Wing(c, 1f, Color.Lerp(top, glow, 0.2f), Dk(bottom, 0.2f)), wingFar, wingNear);
            var tail = new ChainDef { Anchor = new Vector2(-0.26f, -0.01f), Count = 5, Spacing = 0.1f, Hang = new Vector2(-0.08f, -0.012f), Scale0 = 0.8f, Scale1 = 0.3f, Wave = 0.03f, WaveFreq = 7f, Stiff = 16f };
            d.Chains.Add(tail);
            Draw(d, "Tail", Box(-0.12f, -0.12f, 0.24f, 0.24f), P, Vector2.zero, c =>
            {
                SdfCanvas.SdfFn s = p => Sdf.Circle(p, Vector2.zero, 0.1f);
                c.Fill(s, p => V(p, -0.1f, 0.1f, bottom, top));
                c.Paint(p => Sdf.Circle(p, new Vector2(0f, -0.02f), 0.05f), glow.WithAlpha(0.4f), 0.04f);
            }, s => tail.Sprite = s);
            EyeAt(d, 0.15f, 0.045f, 0.7f).Color = Li(glow, 0.3f);
            d.Tilt = 0.6f;
            d.GlowSize = 1.5f;
            d.HpY = 0.55f;
            DrawBody(d, Box(-0.42f, -0.28f, 0.88f, 0.6f), P, c =>
            {
                SdfCanvas.SdfFn body = p => Sdf.SmoothUnion(Sdf.SmoothUnion(Sdf.Ellipse(p, new Vector2(-0.04f, 0f), new Vector2(0.27f, 0.17f)),
                    Sdf.Triangle(p, new Vector2(0.08f, 0.11f), new Vector2(0.08f, -0.11f), new Vector2(0.42f, -0.02f)), 0.07f),
                    Sdf.Triangle(p, new Vector2(-0.18f, 0.1f), new Vector2(0.02f, 0.1f), new Vector2(-0.14f, 0.27f)), 0.04f);
                c.Fill(body, p => V(p, -0.18f, 0.18f, bottom, top));
                c.Paint(p => Sdf.Ellipse(p, new Vector2(0.02f, -0.08f), new Vector2(0.2f, 0.06f)), Li(Color.Lerp(top, glow, 0.4f), 0.2f).WithAlpha(0.6f), 0.05f);
                // a glowing lateral line from nose to tail
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.y + 0.01f + 0.03f * p.x) - 0.008f, body(p) + 0.03f), glow.WithAlpha(0.85f), 0.006f);
                Rim(c, body, RimCol(top), 0.035f);
                c.Fill(p => Sdf.Capsule(p, new Vector2(0.28f, -0.045f), new Vector2(0.37f, -0.03f), 0.006f), Dk(bottom, 0.5f));
            });
            return d;
        }

        /// <summary>Schemen: a hooded wraith with an empty face, two burning eyes and clawed hands drifting beside it.</summary>
        static LookDef Shade(StageTheme t)
        {
            var d = New(t, Look.Shade, true);
            Color top = t.WispTop, bottom = t.WispBottom, glow = t.WispGlow;
            var handFar = Part(d, "HandFar", -0.24f, -0.02f, 58).Scaled(0.85f, 0.85f).Turn(160f).Animate(PartAnim.Bob, 0.05f, 2.6f, 1.3f, 0.08f);
            var handNear = Part(d, "HandNear", 0.28f, -0.08f, 62).Turn(-10f).Animate(PartAnim.Bob, 0.05f, 2.6f, 0f, 0.08f);
            handFar.Tint = new Color(0.7f, 0.7f, 0.8f);
            Share(d, "Hand", Box(-0.06f, -0.14f, 0.26f, 0.28f), P, c => Claw(c, 1f, bottom, Color.Lerp(top, glow, 0.25f)), handFar, handNear);
            var smoke = new ChainDef { Anchor = new Vector2(-0.03f, -0.3f), Count = 4, Spacing = 0.12f, Hang = new Vector2(-0.05f, -0.08f), Scale0 = 0.75f, Scale1 = 0.25f, Wave = 0.04f, WaveFreq = 4f, Stiff = 10f };
            d.Chains.Add(smoke);
            Draw(d, "Smoke", Box(-0.16f, -0.16f, 0.32f, 0.32f), P, Vector2.zero, c => SoftBall(c, 0.15f, Color.Lerp(top, bottom, 0.4f).WithAlpha(0.8f), bottom.WithAlpha(0f)), s => smoke.Sprite = s);
            Color eyeC = Li(glow, 0.45f);
            var e1 = EyeAt(d, 0.05f, 0.14f, 0.42f); e1.Pupil = false; e1.Color = eyeC; e1.Stretch = new Vector2(1.2f, 0.8f);
            var e2 = EyeAt(d, 0.15f, 0.14f, 0.42f); e2.Pupil = false; e2.Color = eyeC; e2.Stretch = new Vector2(1.2f, 0.8f);
            d.GlowSize = 1.6f; d.GlowAlpha = 0.24f;
            d.HpY = 0.6f;
            DrawBody(d, Box(-0.4f, -0.46f, 0.8f, 0.9f), P, c =>
            {
                SdfCanvas.SdfFn body = p =>
                {
                    float dd = Sdf.SmoothUnion(Sdf.Circle(p, new Vector2(0f, 0.14f), 0.25f), Sdf.Tapered(p, new Vector2(0f, 0.08f), 0.27f, new Vector2(-0.07f, -0.34f), 0.13f), 0.08f);
                    // ragged hem
                    float hem = -0.3f - 0.07f * Mathf.Abs(Mathf.Sin(p.x * 21f)) - 0.03f * Noise.Perlin(p.x * 9f, 2f);
                    return Mathf.Max(dd, hem - p.y);
                };
                c.Fill(body, p =>
                {
                    Color col = V(p, -0.4f, 0.36f, bottom, top);
                    col.a = Mathf.Lerp(0.35f, 0.97f, MathUtil.Smooth01((p.y + 0.4f) / 0.4f));
                    return col;
                });
                // folds of the robe
                for (int k = 0; k < 3; k++)
                {
                    float x0 = -0.12f + k * 0.1f;
                    c.Paint(p => Sdf.Intersect(Sdf.Capsule(p, new Vector2(x0 + 0.02f, 0.0f), new Vector2(x0 - 0.04f, -0.3f), 0.012f), body(p) + 0.02f), Dk(bottom, 0.3f).WithAlpha(0.5f), 0.02f);
                }
                Rim(c, body, RimCol(top), 0.035f);
                // the empty face inside the hood
                c.Fill(p => Sdf.Ellipse(p, new Vector2(0.1f, 0.12f), new Vector2(0.13f, 0.15f)), p => Color.Lerp(Dk(bottom, 0.85f), Color.Lerp(bottom, glow, 0.25f), MathUtil.Smooth01(1f - (p - new Vector2(0.1f, 0.1f)).magnitude / 0.15f) * 0.5f));
                c.Paint(p => Sdf.Intersect(Mathf.Abs(Sdf.Ellipse(p, new Vector2(0.1f, 0.12f), new Vector2(0.13f, 0.15f))) - 0.012f, 0.05f - p.y), Li(top, 0.35f).WithAlpha(0.7f), 0.008f);
            });
            return d;
        }

        /// <summary>Laterne: a translucent jelly bell with a glowing eye inside and tentacles trailing beneath.</summary>
        static LookDef Lantern(StageTheme t)
        {
            var d = New(t, Look.Lantern, true);
            Color top = t.WispTop, bottom = t.WispBottom, glow = t.WispGlow;
            var core = Part(d, "Core", 0f, 0.02f, 59).Shine(PartMat.Glow, glow).Animate(PartAnim.Breathe, 0.25f, 2.4f, 0f, 1f);
            Draw(d, "Core", Box(-0.2f, -0.2f, 0.4f, 0.4f), P, Vector2.zero, c => SoftBall(c, 0.19f, new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0f)), s => core.Sprite = s);
            float[] xs = { -0.2f, -0.07f, 0.07f, 0.2f };
            var tent = new ChainDef[xs.Length];
            for (int i = 0; i < xs.Length; i++)
            {
                tent[i] = new ChainDef { Anchor = new Vector2(xs[i], -0.07f - 0.02f * (1 - Mathf.Abs(xs[i]) * 4f)), Count = 4, Spacing = 0.1f, Hang = new Vector2(-0.012f, -0.1f),
                    Scale0 = 1f, Scale1 = 0.55f, Wave = 0.025f, WaveFreq = 4f, Phase = i * 1.3f, Stiff = 12f, Align = true, Order = 57 + (i % 2) };
                d.Chains.Add(tent[i]);
            }
            Draw(d, "Tentacle", Box(-0.04f, -0.13f, 0.08f, 0.15f), P, Vector2.zero, c =>
            {
                c.Fill(p => Sdf.Tapered(p, Vector2.zero, 0.022f, new Vector2(0f, -0.1f), 0.012f), p => V(p, -0.1f, 0f, Color.Lerp(top, glow, 0.35f), Li(top, 0.15f)).WithAlpha(0.9f));
                c.Paint(p => Sdf.Circle(p, new Vector2(0f, -0.1f), 0.012f), glow, 0.006f);
            }, s => { foreach (var ch in tent) ch.Sprite = s; });
            var eye = EyeAt(d, 0.03f, 0.03f, 0.85f);
            eye.Wide = true;
            d.GlowSize = 1.8f;
            d.HpY = 0.55f;
            DrawBody(d, Box(-0.38f, -0.18f, 0.76f, 0.64f), P, c =>
            {
                SdfCanvas.SdfFn bell = p =>
                {
                    float dome = Sdf.Ellipse(p, new Vector2(0f, 0.04f), new Vector2(0.33f, 0.28f));
                    float rim = -0.07f - 0.03f * Mathf.Abs(Mathf.Sin(p.x * 26f));
                    return Mathf.Max(dome, rim - p.y);
                };
                c.Fill(bell, p =>
                {
                    Color col = V(p, -0.1f, 0.32f, bottom, top);
                    // ribs of the bell
                    float rib = Mathf.Abs(Mathf.Sin(Mathf.Atan2(p.x, p.y + 0.3f) * 9f));
                    col = Color.Lerp(col, Li(top, 0.3f), (1f - MathUtil.Smooth01(rib / 0.2f)) * 0.35f);
                    col.a = 0.8f;
                    return col;
                });
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.y + 0.05f) - 0.03f, bell(p)), Dk(bottom, 0.2f).WithAlpha(0.8f), 0.01f);
                for (int k = 0; k < 7; k++) { float x = -0.27f + k * 0.09f; c.Paint(p => Sdf.Circle(p, new Vector2(x, -0.05f), 0.014f), glow, 0.006f); }
                Rim(c, bell, RimCol(top), 0.035f);
                // handle on top, like a lantern
                c.Fill(p => Mathf.Min(Mathf.Abs(Sdf.Circle(p, new Vector2(0f, 0.37f), 0.055f)) - 0.014f, Sdf.Box(p, new Vector2(0f, 0.31f), new Vector2(0.03f, 0.03f), 0.01f)), Dk(bottom, 0.3f));
            });
            return d;
        }

        // ================================================================== bosses

        /// <summary>Düsterkönig: a fat royal dome in an ermine mantle, crowned, grinning, holding a glowing sceptre.</summary>
        static LookDef King(StageTheme t)
        {
            var d = New(t, Look.King, false);
            Color top = t.BlobTop, bottom = t.BlobBottom, glow = t.Glow, accent = t.Accent;
            Feet(d, bottom, 1.3f, 1.2f, new Vector2(-0.24f, 0.03f), new Vector2(0.24f, 0.03f));
            float tilt = FeatureTilt(t.Feature);
            var hl = Part(d, "HornL", -0.32f, 0.66f, 59).Turn(30f * tilt).Scaled(1.25f, 1.25f).Animate(PartAnim.Sway, 2f, 1.6f);
            var hr = Part(d, "HornR", 0.3f, 0.68f, 59).Turn(22f * tilt).Scaled(-1.25f, 1.25f).Animate(PartAnim.Sway, 2f, 1.6f, 1f);
            Feature(d, t, hl, hr);
            var crown = Part(d, "Crown", 0.02f, 0.8f, 64).Shine(PartMat.Emissive, Color.Lerp(Color.white, accent, 0.25f) * 1.2f).Animate(PartAnim.Bob, 0.01f, 3f);
            crown.Shared = 1;
            var sceptre = Part(d, "Sceptre", 0.44f, 0.26f, 62).Turn(-18f).Animate(PartAnim.Swing, 8f, 1.5f, 0f, 45f);
            Draw(d, "Sceptre", Box(-0.08f, -0.34f, 0.16f, 0.9f), PB, Vector2.zero, c =>
            {
                c.Fill(p => Sdf.Capsule(p, new Vector2(0f, -0.3f), new Vector2(0f, 0.4f), 0.022f), p => V(p, -0.3f, 0.4f, new Color(0.5f, 0.35f, 0.15f), new Color(1f, 0.85f, 0.5f)));
                c.Fill(p => Mathf.Min(Sdf.Box(p, new Vector2(0f, 0.39f), new Vector2(0.05f, 0.018f), 0.008f), Sdf.Box(p, new Vector2(0f, -0.02f), new Vector2(0.035f, 0.02f), 0.008f)), new Color(0.95f, 0.78f, 0.4f));
                c.Fill(p => Sdf.Circle(p, new Vector2(0f, 0.47f), 0.065f), p => Color.Lerp(Li(accent, 0.6f), accent, MathUtil.Smooth01((p - new Vector2(-0.02f, 0.49f)).magnitude / 0.07f)));
            }, s => sceptre.Sprite = s);
            var orb = Part(d, "OrbGlow", 0f, 0.47f, 63, sceptre).Shine(PartMat.Glow, accent).Animate(PartAnim.Breathe, 0.3f, 2f, 0f, 1f);
            orb.Shared = 2;
            orb.Scale = new Vector2(0.45f, 0.45f);
            EyeAt(d, 0.04f, 0.53f, 1.15f);
            EyeAt(d, 0.28f, 0.53f, 1.2f);
            d.GlowPos = new Vector2(0f, 0.4f);
            d.HpY = 1.1f;
            DrawBody(d, Box(-0.62f, -0.05f, 1.24f, 1f), PB, c =>
            {
                SdfCanvas.SdfFn body = p => Sdf.SmoothUnion(Sdf.Ellipse(p, new Vector2(0f, 0.42f), new Vector2(0.52f, 0.42f)),
                    Sdf.Box(p, new Vector2(0f, 0.16f), new Vector2(0.5f, 0.16f), 0.14f), 0.08f);
                c.Fill(body, p => V(p, 0f, 0.84f, bottom, top));
                c.Paint(p => Sdf.Ellipse(p, new Vector2(0.04f, 0.46f), new Vector2(0.3f, 0.2f)), Color.Lerp(top, glow, 0.3f).WithAlpha(0.4f), 0.14f);
                Rim(c, body, RimCol(top), 0.05f);
                // mantle: a deep cape over the lower half with an ermine trim along its top
                Color robe = Color.Lerp(Dk(bottom, 0.1f), accent, 0.35f);
                System.Func<Vector2, float> hemY = p => 0.3f + 0.06f * Mathf.Sin(p.x * 7f + 1f);
                c.Paint(p =>
                {
                    Color col = V(p, 0f, 0.35f, Dk(robe, 0.35f), robe);
                    col.a = Mathf.Clamp01(0.5f - Sdf.Intersect(p.y - hemY(p), body(p)) / 0.006f);
                    return col;
                });
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.y - hemY(p)) - 0.045f - 0.012f * Mathf.Sin(p.x * 60f), body(p) + 0.01f), new Color(0.93f, 0.92f, 0.88f), 0.006f);
                for (int k = 0; k < 9; k++)
                {
                    float x = -0.42f + k * 0.105f;
                    float y = 0.3f + 0.06f * Mathf.Sin(x * 7f + 1f);
                    c.Paint(p => Sdf.Ellipse(p, new Vector2(x, y), new Vector2(0.008f, 0.018f)), new Color(0.08f, 0.06f, 0.08f), 0.004f);
                }
                // a wide grin full of fangs
                SdfCanvas.SdfFn mouth = p => Sdf.Ellipse(p, new Vector2(0.18f, 0.39f), new Vector2(0.2f, 0.07f));
                c.Fill(mouth, Dk(bottom, 0.7f));
                c.Paint(p => Sdf.Ellipse(p, new Vector2(0.18f, 0.36f), new Vector2(0.1f, 0.03f)), Color.Lerp(Dk(bottom, 0.5f), glow, 0.35f), 0.02f);
                for (int k = 0; k < 7; k++)
                {
                    float x = 0.04f + k * 0.047f;
                    c.Fill(p => Sdf.Intersect(Sdf.Triangle(p, new Vector2(x - 0.018f, 0.45f), new Vector2(x + 0.018f, 0.45f), new Vector2(x, 0.405f)), mouth(p)), new Color(0.96f, 0.94f, 0.88f));
                    if (k % 2 == 1) c.Fill(p => Sdf.Intersect(Sdf.Triangle(p, new Vector2(x - 0.015f, 0.33f), new Vector2(x + 0.015f, 0.33f), new Vector2(x, 0.365f)), mouth(p)), new Color(0.9f, 0.88f, 0.82f));
                }
            });
            return d;
        }

        /// <summary>Dornenmutter: a striped bulb crowned by a bloom, three eyes, a thorny maw and vines for arms.</summary>
        static LookDef ThornMother(StageTheme t)
        {
            var d = New(t, Look.ThornMother, false);
            Color top = t.BlobTop, bottom = t.BlobBottom, glow = t.Glow, accent = t.Accent;
            Color leafC = Color.Lerp(Dk(bottom, 0.1f), new Color(0.2f, 0.45f, 0.25f), 0.4f);
            var vineFar = Part(d, "VineFar", -0.34f, 0.4f, 58).Scaled(-1f, 1f).Animate(PartAnim.Sway, 14f, 1.8f, 1.1f, 30f);
            var vineNear = Part(d, "VineNear", 0.38f, 0.3f, 62).Turn(-12f).Animate(PartAnim.Sway, 14f, 1.8f, 0f, 30f);
            vineFar.Tint = new Color(0.72f, 0.72f, 0.78f);
            Share(d, "Vine", Box(-0.08f, -0.08f, 0.62f, 0.62f), PB, c =>
            {
                Vector2 a = Vector2.zero, b = new Vector2(0.22f, 0.12f), e = new Vector2(0.36f, 0.34f), tip = new Vector2(0.3f, 0.46f);
                SdfCanvas.SdfFn vine = p => Mathf.Min(Mathf.Min(Sdf.Tapered(p, a, 0.05f, b, 0.036f), Sdf.Tapered(p, b, 0.036f, e, 0.022f)),
                    Mathf.Abs(Sdf.Circle(p, e + new Vector2(-0.06f, 0.03f), 0.065f)) - 0.012f);
                c.Fill(vine, p => V(p, 0f, 0.4f, Dk(leafC, 0.2f), Li(leafC, 0.2f)));
                for (int k = 0; k < 5; k++)
                {
                    float u = 0.15f + k * 0.17f;
                    Vector2 at = u < 0.5f ? Vector2.Lerp(a, b, u * 2f) : Vector2.Lerp(b, e, (u - 0.5f) * 2f);
                    Vector2 dir = k % 2 == 0 ? new Vector2(-0.4f, 1f).normalized : new Vector2(0.7f, -0.6f).normalized;
                    c.Fill(p => Sdf.Triangle(p, at - new Vector2(dir.y, -dir.x) * 0.018f, at + new Vector2(dir.y, -dir.x) * 0.018f, at + dir * 0.07f), Li(top, 0.3f));
                }
                c.Fill(p => Sdf.Ellipse(MathUtil.Rotate(p - (b + new Vector2(0.02f, 0.06f)), 30f), Vector2.zero, new Vector2(0.07f, 0.028f)), Li(leafC, 0.25f));
            }, vineFar, vineNear);
            float tilt = FeatureTilt(t.Feature);
            var tl = Part(d, "ThornL", -0.42f, 0.5f, 59).Turn(55f * tilt).Scaled(1.2f, 1.2f).Animate(PartAnim.Sway, 2f, 1.5f);
            var tr = Part(d, "ThornR", 0.42f, 0.52f, 59).Turn(40f * tilt).Scaled(-1.2f, 1.2f).Animate(PartAnim.Sway, 2f, 1.5f, 1f);
            Feature(d, t, tl, tr);
            var bloom = Part(d, "Bloom", 0.02f, 0.96f, 62).Animate(PartAnim.Pulse, 0.05f, 2.2f, 0f, 0.35f);
            Draw(d, "Bloom", Box(-0.26f, -0.1f, 0.52f, 0.4f), PB, Vector2.zero, c =>
            {
                for (int k = 0; k < 6; k++)
                {
                    float a = 20f + k * 28f;
                    Vector2 dir = MathUtil.Dir(a);
                    c.Fill(p => Sdf.Ellipse(MathUtil.Rotate(p - dir * 0.1f - new Vector2(0f, 0.02f), -a), Vector2.zero, new Vector2(0.11f, 0.045f)),
                        p => Color.Lerp(Dk(accent, 0.25f), Li(accent, 0.35f), MathUtil.Smooth01((p.magnitude - 0.03f) / 0.18f)));
                }
                c.Fill(p => Sdf.Circle(p, new Vector2(0f, 0.03f), 0.06f), p => Color.Lerp(new Color(1f, 0.95f, 0.7f), glow, MathUtil.Smooth01(p.magnitude / 0.07f)));
            }, s => bloom.Sprite = s);
            EyeAt(d, 0.02f, 0.5f, 0.85f);
            EyeAt(d, 0.24f, 0.48f, 0.95f);
            EyeAt(d, 0.13f, 0.66f, 0.7f);
            d.GlowPos = new Vector2(0f, 0.4f);
            d.HpY = 1.25f;
            DrawBody(d, Box(-0.64f, -0.05f, 1.28f, 1.08f), PB, c =>
            {
                SdfCanvas.SdfFn bulb = p => Sdf.SmoothUnion(Sdf.Ellipse(p, new Vector2(0f, 0.38f), new Vector2(0.49f, 0.37f)),
                    Sdf.Tapered(p, new Vector2(0f, 0.52f), 0.3f, new Vector2(0.02f, 0.98f), 0.03f), 0.14f);
                c.Fill(bulb, p =>
                {
                    Color col = V(p, 0f, 0.95f, bottom, top);
                    // pumpkin-like ribs
                    float rib = Mathf.Abs(Mathf.Sin(p.x / Mathf.Max(0.15f, 0.55f - Mathf.Abs(p.y - 0.4f) * 0.4f) * 3.6f));
                    return Color.Lerp(Dk(col, 0.22f), col, MathUtil.Smooth01(rib / 0.35f) * 0.8f + 0.2f);
                });
                c.Paint(p => Sdf.Ellipse(p, new Vector2(0.05f, 0.4f), new Vector2(0.28f, 0.2f)), Color.Lerp(top, glow, 0.3f).WithAlpha(0.35f), 0.14f);
                Rim(c, bulb, RimCol(top), 0.05f);
                // a skirt of broad leaves around the base
                for (int k = 0; k < 6; k++)
                {
                    float x = -0.46f + k * 0.184f;
                    float a = (x < 0f ? 1f : -1f) * (25f + 10f * Hash(k));
                    c.Fill(p => Sdf.Ellipse(MathUtil.Rotate(p - new Vector2(x, 0.12f), a), Vector2.zero, new Vector2(0.17f, 0.075f)),
                        p => V(p, 0f, 0.22f, Dk(leafC, 0.35f), Li(leafC, 0.1f)));
                    c.Paint(p => Sdf.Capsule(MathUtil.Rotate(p - new Vector2(x, 0.12f), a), new Vector2(-0.14f, 0f), new Vector2(0.14f, 0f), 0.006f), Li(leafC, 0.35f).WithAlpha(0.5f), 0.005f);
                }
                // the maw with thorn teeth
                SdfCanvas.SdfFn mouth = p => Sdf.Ellipse(p, new Vector2(0.14f, 0.3f), new Vector2(0.15f, 0.06f));
                c.Fill(mouth, Dk(bottom, 0.75f));
                c.Paint(p => Sdf.Ellipse(p, new Vector2(0.14f, 0.28f), new Vector2(0.08f, 0.025f)), glow.WithAlpha(0.5f), 0.02f);
                for (int k = 0; k < 6; k++)
                {
                    float x = 0.02f + k * 0.047f;
                    bool up = k % 2 == 0;
                    c.Fill(p => Sdf.Intersect(Sdf.Triangle(p, new Vector2(x - 0.016f, up ? 0.36f : 0.24f), new Vector2(x + 0.016f, up ? 0.36f : 0.24f), new Vector2(x, up ? 0.3f : 0.3f)), mouth(p)),
                        Li(top, 0.45f));
                }
            });
            return d;
        }

        /// <summary>Sturmlaterne: a caged iron lantern with a storm eye inside, chains hanging from its base.</summary>
        static LookDef StormLantern(StageTheme t)
        {
            var d = New(t, Look.StormLantern, true);
            Color top = t.WispTop, bottom = t.WispBottom, glow = t.WispGlow;
            Color iron = Color.Lerp(new Color(0.12f, 0.13f, 0.16f), bottom, 0.3f);
            var core = Part(d, "Core", 0f, 0f, 59).Shine(PartMat.Glow, glow).Scaled(1.2f, 1.2f).Animate(PartAnim.Breathe, 0.3f, 3.2f, 0f, 1f);
            Draw(d, "Core", Box(-0.26f, -0.26f, 0.52f, 0.52f), PB, Vector2.zero, c => SoftBall(c, 0.25f, new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0f)), s => core.Sprite = s);
            var eye = EyeAt(d, 0f, 0f, 1.25f);
            eye.Wide = true;
            var chL = new ChainDef { Anchor = new Vector2(-0.22f, -0.33f), Count = 6, Spacing = 0.085f, Hang = new Vector2(0f, -0.085f), Scale0 = 1f, Scale1 = 0.8f, Wave = 0.012f, WaveFreq = 3f, Stiff = 14f, Align = true };
            var chR = new ChainDef { Anchor = new Vector2(0.22f, -0.33f), Count = 6, Spacing = 0.085f, Hang = new Vector2(0f, -0.085f), Scale0 = 1f, Scale1 = 0.8f, Wave = 0.012f, WaveFreq = 3f, Phase = 1.4f, Stiff = 14f, Align = true };
            d.Chains.Add(chL); d.Chains.Add(chR);
            Draw(d, "Link", Box(-0.04f, -0.1f, 0.08f, 0.12f), PB, Vector2.zero, c =>
                c.Fill(p => Mathf.Abs(Sdf.Ellipse(p, new Vector2(0f, -0.04f), new Vector2(0.022f, 0.042f))) - 0.008f, p => V(p, -0.09f, 0f, Dk(iron, 0.2f), Li(iron, 0.35f))),
                s => { chL.Sprite = s; chR.Sprite = s; });
            d.GlowSize = 1.9f;
            d.HpY = 0.55f;
            DrawBody(d, Box(-0.4f, -0.5f, 0.8f, 1.08f), PB, c =>
            {
                // glass panes (translucent), then the iron frame over them
                c.Fill(p => Sdf.Box(p, Vector2.zero, new Vector2(0.25f, 0.26f), 0.03f), p =>
                {
                    Color g = Color.Lerp(top, glow, 0.25f);
                    g.a = 0.32f + 0.12f * Mathf.Sin(p.x * 30f + p.y * 12f);
                    return g;
                });
                SdfCanvas.SdfFn frame = p =>
                {
                    float dd = 9f;
                    foreach (float x in new[] { -0.26f, -0.087f, 0.087f, 0.26f }) dd = Mathf.Min(dd, Sdf.Box(p, new Vector2(x, 0f), new Vector2(0.018f, 0.27f)));
                    dd = Mathf.Min(dd, Sdf.Box(p, new Vector2(0f, 0.27f), new Vector2(0.3f, 0.03f), 0.01f));
                    dd = Mathf.Min(dd, Sdf.Box(p, new Vector2(0f, -0.28f), new Vector2(0.31f, 0.045f), 0.012f));
                    dd = Mathf.Min(dd, Sdf.Triangle(p, new Vector2(-0.31f, 0.29f), new Vector2(0.31f, 0.29f), new Vector2(0f, 0.45f)));
                    dd = Mathf.Min(dd, Mathf.Abs(Sdf.Circle(p, new Vector2(0f, 0.5f), 0.05f)) - 0.014f);
                    dd = Mathf.Min(dd, Sdf.Triangle(p, new Vector2(-0.1f, -0.32f), new Vector2(0.1f, -0.32f), new Vector2(0f, -0.46f)));
                    return dd;
                };
                c.Fill(frame, p => V(p, -0.45f, 0.5f, Dk(iron, 0.2f), Li(iron, 0.4f)));
                c.Paint(p => Sdf.Intersect(frame(p) + 0.006f, -frame(p + new Vector2(-0.012f, 0.014f))), Li(top, 0.5f).WithAlpha(0.6f), 0.006f);
                for (int k = 0; k < 5; k++) { float x = -0.24f + k * 0.12f; c.Paint(p => Sdf.Circle(p, new Vector2(x, -0.28f), 0.012f), Li(iron, 0.6f), 0.004f); }
                // storm streaks on the glass
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.x * 0.4f + p.y - 0.12f) - 0.01f, Sdf.Box(p, Vector2.zero, new Vector2(0.24f, 0.25f))), Color.white.WithAlpha(0.35f), 0.01f);
            });
            return d;
        }

        /// <summary>Kristallwächter: an angular stone golem with a glowing geode chest, crystal shards and heavy fists.</summary>
        static LookDef CrystalGuard(StageTheme t)
        {
            var d = New(t, Look.CrystalGuard, false);
            Color top = t.BlobTop, bottom = t.BlobBottom, glow = t.Glow;
            Color stone = Color.Lerp(Color.Lerp(bottom, top, 0.5f), new Color(0.35f, 0.38f, 0.42f), 0.4f);
            var armB = Part(d, "ArmBack", -0.14f, 0.6f, 58).Animate(PartAnim.Swing, 12f, 1.6f, 1.2f, 45f);
            armB.Tint = new Color(0.72f, 0.72f, 0.78f);
            Feet(d, Dk(stone, 0.2f), 1.4f, 1.3f, new Vector2(-0.22f, 0.03f), new Vector2(0.18f, 0.03f));
            var shards = new[]
            {
                Part(d, "Shard1", -0.3f, 0.66f, 57).Turn(34f).Shine(PartMat.Emissive, Color.white).Animate(PartAnim.Sway, 2f, 1.3f),
                Part(d, "Shard2", -0.1f, 0.8f, 57).Turn(12f).Scaled(1.3f, 1.3f).Shine(PartMat.Emissive, Color.white).Animate(PartAnim.Sway, 2f, 1.3f, 0.8f),
                Part(d, "Shard3", 0.1f, 0.78f, 57).Turn(-14f).Scaled(0.9f, 0.9f).Shine(PartMat.Emissive, Color.white).Animate(PartAnim.Sway, 2f, 1.3f, 1.6f),
            };
            Share(d, "Shard", Box(-0.1f, -0.03f, 0.2f, 0.42f), PB, c =>
            {
                SdfCanvas.SdfFn s = p => Mathf.Min(Sdf.Box(p, new Vector2(0f, 0.12f), new Vector2(0.055f, 0.13f), 0.01f), Sdf.Triangle(p, new Vector2(-0.055f, 0.24f), new Vector2(0.055f, 0.24f), new Vector2(0f, 0.37f)));
                c.Fill(s, p => p.x < -0.015f ? Color.Lerp(Dk(glow, 0.35f), glow, 0.4f) : p.x < 0.02f ? Li(glow, 0.55f) : glow);
                c.Paint(p => Sdf.Capsule(p, new Vector2(0f, 0.04f), new Vector2(0f, 0.3f), 0.006f), Color.white.WithAlpha(0.6f), 0.006f);
            }, shards);
            var core = Part(d, "Core", 0.08f, 0.44f, 61).Shine(PartMat.Glow, glow).Scaled(0.7f, 0.6f).Animate(PartAnim.Breathe, 0.3f, 2.2f, 0f, 1f);
            core.Shared = 2;
            var armF = Part(d, "ArmFront", 0.2f, 0.56f, 62).Animate(PartAnim.Swing, 12f, 1.6f, 0f, 45f);
            Share(d, "Fist", ArmRect(0.36f, 1.15f), PB, c =>
            {
                Arm(c, 0.36f, 1.15f, Dk(stone, 0.35f), Li(stone, 0.15f), Li(glow, 0.2f));
                c.Paint(p => Mathf.Abs(p.y + 0.18f) - 0.01f, Dk(stone, 0.5f), 0.004f);
            }, armB, armF);
            var visor = EyeAt(d, 0.36f, 0.64f, 0.9f);
            visor.Pupil = false; visor.Stretch = new Vector2(1.7f, 0.4f); visor.Color = Li(glow, 0.3f);
            d.GlowPos = new Vector2(0.05f, 0.45f);
            d.HpY = 1.2f;
            DrawBody(d, Box(-0.62f, -0.05f, 1.24f, 1f), PB, c =>
            {
                SdfCanvas.SdfFn body = p => Mathf.Min(Mathf.Min(Sdf.Box(p, new Vector2(-0.02f, 0.4f), new Vector2(0.42f, 0.36f), 0.07f, -4f),
                    Sdf.Box(p, new Vector2(0.3f, 0.62f), new Vector2(0.16f, 0.13f), 0.04f, 8f)), Sdf.Box(p, new Vector2(-0.1f, 0.7f), new Vector2(0.3f, 0.1f), 0.05f, -10f));
                c.Fill(body, p =>
                {
                    Color col = V(p, 0f, 0.85f, Dk(stone, 0.35f), Li(stone, 0.1f));
                    // facets: big planes of different brightness
                    float f = Mathf.Floor((p.x * 1.3f + p.y * 0.8f) * 3f) + Mathf.Floor((p.y - p.x * 0.6f) * 2.5f) * 7f;
                    return Color.Lerp(col, Dk(col, 0.25f), (Hash((int)f) * 0.5f + 0.5f) * 0.5f);
                });
                c.Paint(p => Sdf.Intersect(Mathf.Abs(Noise.Perlin(p.x * 5f, p.y * 5f) - 0.5f) - 0.012f, body(p) + 0.03f), Color.Lerp(stone, glow, 0.6f), 0.006f);
                Rim(c, body, Li(stone, 0.55f).WithAlpha(0.7f), 0.05f);
                // the geode: a cavity lined with bright crystal teeth
                SdfCanvas.SdfFn geode = p => Sdf.Ellipse(p, new Vector2(0.08f, 0.44f), new Vector2(0.15f, 0.13f));
                c.Fill(geode, p => Color.Lerp(Dk(glow, 0.7f), Dk(glow, 0.3f), MathUtil.Smooth01(1f - (p - new Vector2(0.08f, 0.44f)).magnitude / 0.14f)));
                for (int k = 0; k < 9; k++)
                {
                    float a = k * 40f;
                    Vector2 root = new Vector2(0.08f, 0.44f) + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * 0.14f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.12f);
                    Vector2 tip = Vector2.Lerp(root, new Vector2(0.08f, 0.44f), 0.55f);
                    Vector2 side = new Vector2(-(tip - root).y, (tip - root).x).normalized * 0.025f;
                    c.Fill(p => Sdf.Intersect(Sdf.Triangle(p, root + side, root - side, tip), geode(p)), Li(glow, 0.45f));
                }
            });
            return d;
        }

        /// <summary>Magmakoloss: a hulking basalt titan veined with lava, volcanic vents on its shoulders, fists like boulders.</summary>
        static LookDef MagmaColossus(StageTheme t)
        {
            var d = New(t, Look.MagmaColossus, false);
            Color top = t.BlobTop, bottom = t.BlobBottom, glow = t.Glow;
            Color rock = Color.Lerp(bottom, new Color(0.16f, 0.13f, 0.12f), 0.4f);
            var armB = Part(d, "ArmBack", -0.24f, 0.56f, 58).Animate(PartAnim.Swing, 10f, 1.4f, 1.1f, 45f);
            armB.Tint = new Color(0.7f, 0.7f, 0.74f);
            Feet(d, Dk(rock, 0.1f), 1.5f, 1.35f, new Vector2(-0.24f, 0.03f), new Vector2(0.2f, 0.03f));
            System.Func<Vector2, float> lava = p => Mathf.Abs(Noise.Perlin(p.x * 4.2f + 7f, p.y * 4.2f) - 0.5f) - 0.02f;
            SdfCanvas.SdfFn bodyFn = p => Sdf.SmoothUnion(Sdf.SmoothUnion(Sdf.Ellipse(p, new Vector2(0f, 0.4f), new Vector2(0.54f, 0.4f)),
                Sdf.Ellipse(p, new Vector2(-0.05f, 0.64f), new Vector2(0.46f, 0.24f)), 0.1f), Sdf.Ellipse(p, new Vector2(0.33f, 0.52f), new Vector2(0.17f, 0.15f)), 0.05f);
            var cracks = Part(d, "Lava", 0f, 0f, 61).Shine(PartMat.Glow, glow).Animate(PartAnim.Breathe, 0.3f, 1.6f, 0f, 1f);
            var armF = Part(d, "ArmFront", 0.2f, 0.52f, 62).Animate(PartAnim.Swing, 10f, 1.4f, 0f, 45f);
            Share(d, "Fist", ArmRect(0.34f, 1.3f), PB, c =>
            {
                Arm(c, 0.34f, 1.3f, Dk(rock, 0.3f), Li(rock, 0.18f), Color.Lerp(rock, glow, 0.7f));
                c.Paint(p => Mathf.Abs(Noise.Perlin(p.x * 9f, p.y * 9f) - 0.5f) - 0.015f, glow.WithAlpha(0.8f), 0.005f);
            }, armB, armF);
            var e1 = EyeAt(d, 0.35f, 0.58f, 0.5f); e1.Pupil = false; e1.Color = Li(glow, 0.4f);
            var e2 = EyeAt(d, 0.44f, 0.57f, 0.45f); e2.Pupil = false; e2.Color = Li(glow, 0.4f);
            d.GlowPos = new Vector2(0f, 0.45f);
            d.HpY = 1.15f;
            DrawBody(d, Box(-0.64f, -0.05f, 1.28f, 1.08f), PB, c =>
            {
                c.Fill(bodyFn, p =>
                {
                    Color col = V(p, 0f, 0.88f, Dk(rock, 0.3f), Color.Lerp(rock, top, 0.4f));
                    float plate = Noise.Perlin(p.x * 3f + 2f, p.y * 3f);
                    return Color.Lerp(col, Li(col, 0.15f), MathUtil.Smooth01((plate - 0.55f) / 0.05f));
                });
                c.Paint(p => Sdf.Intersect(lava(p), bodyFn(p) + 0.03f), Color.Lerp(rock, glow, 0.55f), 0.006f);
                Rim(c, bodyFn, Li(rock, 0.45f).WithAlpha(0.6f), 0.05f);
                // two smoking vents on the shoulders
                foreach (var v in new[] { new Vector2(-0.32f, 0.82f), new Vector2(0.02f, 0.88f) })
                {
                    Vector2 vc = v;
                    c.Fill(p => Sdf.Triangle(p, vc + new Vector2(-0.12f, -0.06f), vc + new Vector2(0.12f, -0.06f), vc + new Vector2(0.03f, 0.1f)), p => V(p, vc.y - 0.06f, vc.y + 0.1f, rock, Li(rock, 0.2f)));
                    c.Fill(p => Sdf.Ellipse(p, vc + new Vector2(0.025f, 0.08f), new Vector2(0.045f, 0.018f)), Li(glow, 0.3f));
                }
                // glowing maw
                c.Fill(p => Sdf.Ellipse(p, new Vector2(0.37f, 0.44f), new Vector2(0.1f, 0.035f)), p => Color.Lerp(Li(glow, 0.5f), Dk(glow, 0.3f), MathUtil.Smooth01(Mathf.Abs(p.x - 0.37f) / 0.1f)));
                c.Fill(p => Sdf.Ellipse(p, new Vector2(0.4f, 0.59f), new Vector2(0.1f, 0.03f)), Dk(rock, 0.5f));
            });
            Draw(d, "Lava", Box(-0.64f, -0.05f, 1.28f, 1.08f), PB, Vector2.zero, c =>
                c.Fill(p => Sdf.Intersect(lava(p) + 0.006f, bodyFn(p) + 0.035f), Color.white, 0.014f), s => cracks.Sprite = s);
            return d;
        }

        /// <summary>Frostwyrm: a horned serpent head with a jaw that gapes before it strikes, trailing a long spined body.</summary>
        static LookDef FrostWyrm(StageTheme t)
        {
            var d = New(t, Look.FrostWyrm, true);
            Color top = t.WispTop, bottom = t.WispBottom, glow = t.WispGlow;
            Color ice = Li(glow, 0.45f);
            var wingFar = Part(d, "WingFar", -0.14f, 0.08f, 58).Scaled(0.8f, 0.8f).Animate(PartAnim.Flap, 20f, 7f, 0.6f, -30f);
            var wingNear = Part(d, "WingNear", -0.12f, 0.06f, 61).Scaled(0.9f, 0.9f).Animate(PartAnim.Flap, 20f, 7f, 0f, -30f);
            wingFar.Tint = new Color(0.7f, 0.7f, 0.8f);
            Share(d, "Wing", Box(-0.56f, -0.1f, 0.62f, 0.52f), PB, c => Wing(c, 1f, Color.Lerp(top, ice, 0.35f), Li(bottom, 0.1f)), wingFar, wingNear);
            var jaw = Part(d, "Jaw", 0.04f, -0.06f, 59).Animate(PartAnim.Jaw, 3f, 3f, 0f, -26f);
            Draw(d, "Jaw", Box(-0.08f, -0.12f, 0.44f, 0.16f), PB, Vector2.zero, c =>
            {
                SdfCanvas.SdfFn j = p => Sdf.SmoothUnion(Sdf.Triangle(p, new Vector2(-0.04f, 0.02f), new Vector2(0.36f, 0.0f), new Vector2(-0.02f, -0.08f)),
                    Sdf.Circle(p, new Vector2(0f, -0.02f), 0.05f), 0.03f);
                c.Fill(j, p => V(p, -0.08f, 0.02f, Dk(bottom, 0.2f), Color.Lerp(bottom, top, 0.6f)));
                for (int k = 0; k < 5; k++) { float x = 0.05f + k * 0.055f; c.Fill(p => Sdf.Triangle(p, new Vector2(x - 0.014f, 0.01f), new Vector2(x + 0.014f, 0.01f), new Vector2(x + 0.004f, 0.045f)), ice); }
            }, s => jaw.Sprite = s);
            var spine = new ChainDef { Anchor = new Vector2(-0.2f, -0.02f), Count = 11, Spacing = 0.17f, Hang = new Vector2(-0.18f, -0.012f), Scale0 = 1.05f, Scale1 = 0.32f,
                Wave = 0.07f, WaveFreq = 3.2f, Stiff = 10f, Align = true, Order = 56 };
            d.Chains.Add(spine);
            Draw(d, "Segment", Box(-0.2f, -0.22f, 0.4f, 0.44f), PB, Vector2.zero, c =>
            {
                // authored with the spine running along -y and the back on the -x side
                SdfCanvas.SdfFn seg = p => Sdf.Ellipse(p, Vector2.zero, new Vector2(0.13f, 0.15f));
                c.Fill(p => Sdf.Triangle(p, new Vector2(-0.08f, 0.08f), new Vector2(-0.08f, -0.06f), new Vector2(-0.19f, 0.04f)), ice);
                c.Fill(seg, p => Color.Lerp(Color.Lerp(bottom, top, 0.8f), Dk(bottom, 0.1f), MathUtil.Smooth01((p.x + 0.12f) / 0.24f)));
                c.Paint(p => Sdf.Intersect(Mathf.Abs(Mathf.Repeat(p.y, 0.07f) - 0.035f) - 0.006f, p.x - 0.03f), Li(top, 0.25f).WithAlpha(0.6f), 0.004f);
                c.Paint(p => Sdf.Intersect(seg(p) + 0.01f, -seg(p + new Vector2(0.025f, 0f))), Li(top, 0.5f).WithAlpha(0.6f), 0.01f);
            }, s => spine.Sprite = s);
            var eye = EyeAt(d, 0.08f, 0.07f, 0.62f); eye.Color = ice;
            d.Tilt = 0.8f;
            d.GlowSize = 1.9f;
            d.HpY = 0.55f;
            DrawBody(d, Box(-0.46f, -0.2f, 0.92f, 0.52f), PB, c =>
            {
                SdfCanvas.SdfFn head = p => Sdf.SmoothUnion(Sdf.Ellipse(p, new Vector2(-0.06f, 0.02f), new Vector2(0.23f, 0.15f)),
                    Sdf.Box(p, new Vector2(0.2f, -0.01f), new Vector2(0.17f, 0.065f), 0.05f, -5f), 0.06f);
                // horns swept back, frills of ice along the neck
                c.Fill(p => Mathf.Min(Sdf.Tapered(p, new Vector2(-0.1f, 0.12f), 0.04f, new Vector2(-0.42f, 0.26f), 0.006f),
                    Sdf.Tapered(p, new Vector2(-0.02f, 0.14f), 0.03f, new Vector2(-0.28f, 0.29f), 0.005f)), p => V(p, 0.1f, 0.3f, Color.Lerp(top, ice, 0.4f), ice));
                for (int k = 0; k < 3; k++) { float x = -0.3f + k * 0.08f; c.Fill(p => Sdf.Triangle(p, new Vector2(x, 0.05f), new Vector2(x + 0.06f, 0.08f), new Vector2(x - 0.04f, 0.17f)), Color.Lerp(top, ice, 0.6f)); }
                c.Fill(head, p => V(p, -0.14f, 0.16f, bottom, top));
                // scales and a bright brow ridge
                c.Paint(p => Sdf.Intersect(Mathf.Abs(Mathf.Repeat(p.x + Mathf.Abs(Mathf.Repeat(p.y, 0.06f) - 0.03f), 0.06f) - 0.03f) - 0.005f, head(p) + 0.02f), Dk(top, 0.2f).WithAlpha(0.45f), 0.004f);
                c.Fill(p => Sdf.Capsule(p, new Vector2(0.02f, 0.12f), new Vector2(0.16f, 0.1f), 0.018f), Color.Lerp(top, ice, 0.5f));
                Rim(c, head, Li(ice, 0.2f).WithAlpha(0.7f), 0.035f);
                c.Fill(p => Sdf.Circle(p, new Vector2(0.34f, 0.02f), 0.012f), Dk(bottom, 0.6f));
            });
            return d;
        }

        /// <summary>Kometen-Orakel: one enormous eye in a starry orb, girdled by a tilted ring, shards orbiting, a comet's tail behind.</summary>
        static LookDef CometOracle(StageTheme t)
        {
            var d = New(t, Look.CometOracle, true);
            Color top = t.WispTop, bottom = t.WispBottom, glow = t.WispGlow, accent = t.Accent;
            var tail = new ChainDef { Anchor = new Vector2(-0.24f, 0.05f), Count = 6, Spacing = 0.16f, Hang = new Vector2(-0.15f, 0.025f), Scale0 = 1.1f, Scale1 = 0.25f,
                Wave = 0.03f, WaveFreq = 2.5f, Stiff = 8f, Order = 55, Mat = PartMat.Glow, Tint = glow };
            d.Chains.Add(tail);
            Draw(d, "Tail", Box(-0.2f, -0.2f, 0.4f, 0.4f), PB, Vector2.zero, c => SoftBall(c, 0.19f, new Color(1f, 1f, 1f, 0.8f), new Color(1f, 1f, 1f, 0f)), s => tail.Sprite = s);
            var ringBack = Part(d, "RingBack", 0f, 0f, 58).Turn(-18f).Animate(PartAnim.Bob, 0.015f, 1.2f);
            var ringFront = Part(d, "RingFront", 0f, 0f, 62).Turn(-18f).Animate(PartAnim.Bob, 0.015f, 1.2f);
            Color ringC = Color.Lerp(top, accent, 0.45f);
            Draw(d, "RingBack", Box(-0.6f, -0.2f, 1.2f, 0.4f), PB, Vector2.zero, c =>
                c.Fill(p => Sdf.Intersect(Mathf.Abs(Sdf.Ellipse(p, Vector2.zero, new Vector2(0.54f, 0.14f))) - 0.022f, -p.y), p => Dk(ringC, 0.25f).WithAlpha(0.9f)), s => ringBack.Sprite = s);
            Draw(d, "RingFront", Box(-0.6f, -0.2f, 1.2f, 0.4f), PB, Vector2.zero, c =>
            {
                c.Fill(p => Sdf.Intersect(Mathf.Abs(Sdf.Ellipse(p, Vector2.zero, new Vector2(0.54f, 0.14f))) - 0.022f, p.y), p => Color.Lerp(ringC, Li(ringC, 0.4f), MathUtil.Smooth01((p.x + 0.5f) / 1f)).WithAlpha(0.95f));
                c.Fill(p => Sdf.Intersect(Mathf.Abs(Sdf.Ellipse(p, Vector2.zero, new Vector2(0.48f, 0.12f))) - 0.006f, p.y), Li(ringC, 0.5f).WithAlpha(0.7f));
            }, s => ringFront.Sprite = s);
            var shardDefs = new PartDef[3];
            for (int i = 0; i < 3; i++)
                shardDefs[i] = Part(d, "Shard" + i, 0f, 0f, 63).Shine(PartMat.Emissive, i == 1 ? Li(accent, 0.3f) : Li(glow, 0.3f)).Scaled(0.8f + 0.2f * i, 0.8f + 0.2f * i)
                    .Animate(PartAnim.Orbit, 0.6f, 1.3f, i * 2.1f);
            Share(d, "Star", Box(-0.1f, -0.1f, 0.2f, 0.2f), PB, c => c.Fill(p => Sdf.Star4(p, Vector2.zero, 0.09f, 0.5f), Color.white), shardDefs);
            var eye = EyeAt(d, 0.05f, 0.01f, 1.9f);
            eye.Wide = true;
            d.GlowSize = 2f;
            d.HpY = 0.5f;
            DrawBody(d, Box(-0.34f, -0.34f, 0.68f, 0.68f), PB, c =>
            {
                SdfCanvas.SdfFn orb = p => Sdf.Circle(p, Vector2.zero, 0.3f);
                c.Fill(orb, p => V(p, -0.3f, 0.3f, bottom, top));
                // star dust inside the orb
                for (int k = 0; k < 14; k++)
                {
                    Vector2 sp = new Vector2(Hash(k * 7 + 1), Hash(k * 13 + 5)) * 0.24f;
                    float r = 0.006f + 0.006f * Mathf.Abs(Hash(k * 3));
                    c.Paint(p => Sdf.Circle(p, sp, r), Li(glow, 0.6f), 0.004f);
                }
                c.Paint(p => Sdf.Circle(p, new Vector2(0.03f, 0f), 0.22f), Color.Lerp(top, glow, 0.35f).WithAlpha(0.35f), 0.12f);
                Rim(c, orb, RimCol(top), 0.04f);
                // a lid ridge around the great eye
                c.Paint(p => Mathf.Abs(Sdf.Ellipse(p, new Vector2(0.05f, 0.01f), new Vector2(0.18f, 0.16f))) - 0.018f, Dk(bottom, 0.3f), 0.01f);
            });
            return d;
        }

        /// <summary>Leerenfürst: a cloaked lord with a cracked bone mask of three eyes, a crown of void spikes, tendrils and floating claws.</summary>
        static LookDef VoidLord(StageTheme t)
        {
            var d = New(t, Look.VoidLord, false);
            Color top = t.BlobTop, bottom = t.BlobBottom, glow = t.Glow;
            Color cloak = Dk(bottom, 0.25f);
            var tendrils = new ChainDef[3];
            Vector2[] roots = { new Vector2(-0.2f, 0.56f), new Vector2(-0.3f, 0.36f), new Vector2(-0.14f, 0.2f) };
            for (int i = 0; i < 3; i++)
            {
                tendrils[i] = new ChainDef { Anchor = roots[i], Count = 6, Spacing = 0.1f, Hang = new Vector2(-0.09f, -0.02f - i * 0.015f), Scale0 = 1f, Scale1 = 0.35f,
                    Wave = 0.05f, WaveFreq = 3f, Phase = i * 1.7f, Stiff = 9f, Align = true, Order = 56 };
                d.Chains.Add(tendrils[i]);
            }
            Draw(d, "Tendril", Box(-0.05f, -0.12f, 0.1f, 0.14f), PB, Vector2.zero, c =>
            {
                c.Fill(p => Sdf.Tapered(p, Vector2.zero, 0.035f, new Vector2(0f, -0.1f), 0.024f), p => V(p, -0.1f, 0f, Dk(cloak, 0.3f), cloak));
                c.Paint(p => Mathf.Abs(p.x + 0.02f) - 0.006f, glow.WithAlpha(0.6f), 0.006f);
            }, s => { foreach (var ch in tendrils) ch.Sprite = s; });
            var handFar = Part(d, "HandFar", -0.42f, 0.42f, 58).Scaled(1.3f, 1.3f).Turn(170f).Animate(PartAnim.Bob, 0.06f, 1.8f, 1.2f, 0.18f);
            var handNear = Part(d, "HandNear", 0.44f, 0.38f, 62).Scaled(1.4f, 1.4f).Turn(-5f).Animate(PartAnim.Bob, 0.06f, 1.8f, 0f, 0.18f);
            handFar.Tint = new Color(0.72f, 0.72f, 0.78f);
            Share(d, "Hand", Box(-0.06f, -0.14f, 0.26f, 0.28f), PB, c =>
            {
                Claw(c, 1f, Dk(cloak, 0.2f), Color.Lerp(cloak, top, 0.5f));
                c.Paint(p => Sdf.Circle(p, new Vector2(0.02f, 0f), 0.025f), glow, 0.015f);
            }, handFar, handNear);
            Color eyeC = Li(glow, 0.35f);
            var big = EyeAt(d, 0.12f, 0.76f, 0.62f); big.Color = eyeC;
            var s1 = EyeAt(d, 0.04f, 0.82f, 0.34f); s1.Color = eyeC; s1.Pupil = false;
            var s2 = EyeAt(d, 0.2f, 0.82f, 0.34f); s2.Color = eyeC; s2.Pupil = false;
            d.GlowPos = new Vector2(0f, 0.5f);
            d.AuraPos = new Vector2(0f, 0.48f);
            d.HpY = 1.2f;
            DrawBody(d, Box(-0.6f, -0.05f, 1.2f, 1.18f), PB, c =>
            {
                SdfCanvas.SdfFn body = p =>
                {
                    float dd = Sdf.SmoothUnion(Sdf.Tapered(p, new Vector2(0f, 0.14f), 0.46f, new Vector2(0.02f, 0.55f), 0.24f), Sdf.Box(p, new Vector2(0f, 0.08f), new Vector2(0.47f, 0.09f), 0.06f), 0.06f);
                    dd = Sdf.SmoothUnion(dd, Sdf.Ellipse(p, new Vector2(0f, 0.58f), new Vector2(0.34f, 0.12f)), 0.06f);
                    dd = Sdf.SmoothUnion(dd, Sdf.Ellipse(p, new Vector2(0.05f, 0.76f), new Vector2(0.16f, 0.18f)), 0.04f);
                    // shoulder spikes
                    dd = Mathf.Min(dd, Sdf.Triangle(p, new Vector2(-0.34f, 0.56f), new Vector2(-0.2f, 0.66f), new Vector2(-0.46f, 0.78f)));
                    dd = Mathf.Min(dd, Sdf.Triangle(p, new Vector2(0.34f, 0.56f), new Vector2(0.2f, 0.66f), new Vector2(0.44f, 0.8f)));
                    return dd;
                };
                c.Fill(body, p => V(p, 0f, 0.95f, Dk(cloak, 0.4f), Color.Lerp(cloak, top, 0.45f)));
                // void sparkles in the folds and a glowing hem
                for (int k = 0; k < 16; k++)
                {
                    Vector2 sp = new Vector2(Hash(k * 5 + 2) * 0.36f, 0.12f + (Hash(k * 11 + 3) * 0.5f + 0.5f) * 0.4f);
                    float r = 0.005f + 0.005f * Mathf.Abs(Hash(k));
                    c.Paint(p => Sdf.Intersect(Sdf.Circle(p, sp, r), body(p) + 0.02f), Li(glow, 0.4f), 0.004f);
                }
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.y - 0.035f - 0.015f * Mathf.Sin(p.x * 30f)) - 0.014f, body(p) + 0.005f), glow, 0.006f);
                Rim(c, body, Li(top, 0.35f).WithAlpha(0.6f), 0.045f);
                // the bone mask, cracked
                SdfCanvas.SdfFn mask = p => Sdf.Ellipse(p, new Vector2(0.11f, 0.77f), new Vector2(0.11f, 0.13f));
                c.Fill(mask, p => V(p, 0.64f, 0.9f, new Color(0.66f, 0.64f, 0.6f), new Color(0.93f, 0.91f, 0.86f)));
                c.Paint(p => Sdf.Intersect(Sdf.Capsule(p, new Vector2(0.14f, 0.9f), new Vector2(0.09f, 0.68f), 0.005f), mask(p)), new Color(0.2f, 0.18f, 0.2f), 0.003f);
                // a crown of void spikes above the hood
                for (int k = 0; k < 5; k++)
                {
                    float x = -0.09f + k * 0.075f, h = k == 2 ? 0.2f : (k == 1 || k == 3) ? 0.15f : 0.1f;
                    c.Fill(p => Sdf.Triangle(p, new Vector2(x - 0.025f, 0.88f), new Vector2(x + 0.025f, 0.88f), new Vector2(x + 0.01f, 0.9f + h)), p => V(p, 0.88f, 1.1f, Dk(cloak, 0.3f), glow));
                }
            });
            return d;
        }

        // ================================================================== shared sprites

        static void BuildEyes()
        {
            // white so a look can tint it (eyes glow in the theme's eye colour)
            var e = new SdfCanvas(new Rect(-0.08f, -0.1f, 0.16f, 0.2f), 300f);
            e.Fill(p => Sdf.Ellipse(p, Vector2.zero, new Vector2(0.058f, 0.078f)), Color.white);
            Eye = e.ToSprite("Eye", Vector2.zero);

            var pu = new SdfCanvas(new Rect(-0.04f, -0.06f, 0.08f, 0.12f), 300f);
            pu.Fill(p => Sdf.Ellipse(p, Vector2.zero, new Vector2(0.018f, 0.045f)), new Color(0.12f, 0.04f, 0.16f));
            Pupil = pu.ToSprite("Pupil", Vector2.zero);

            var we = new SdfCanvas(new Rect(-0.16f, -0.16f, 0.32f, 0.32f), 300f);
            we.Fill(p => Sdf.Circle(p, Vector2.zero, 0.13f), new Color(1f, 0.98f, 0.9f));
            we.Fill(p => Sdf.Ellipse(p, new Vector2(0.03f, 0f), new Vector2(0.035f, 0.085f)), new Color(0.1f, 0.05f, 0.2f));
            WispEye = we.ToSprite("WispEye", Vector2.zero);

            EyeGlow = Art.SoftGlow;
        }

        static void BuildPortal()
        {
            // Vertical swirl ellipse used as spawn gate.
            var c = new SdfCanvas(new Rect(-0.75f, -1.25f, 1.5f, 2.5f), 160f);
            c.Fill(p =>
            {
                Vector2 q = new Vector2(p.x / 0.6f, p.y / 1.1f);
                return (q.magnitude - 1f) * 0.6f;
            }, p =>
            {
                Vector2 q = new Vector2(p.x / 0.6f, p.y / 1.1f);
                float r = q.magnitude;
                float a = Mathf.Pow(Mathf.Clamp01(r), 3.2f);
                float ang = Mathf.Atan2(q.y, q.x);
                float swirl = 0.5f + 0.5f * Mathf.Sin(ang * 3f + r * 9f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(a * (0.55f + 0.45f * swirl)));
            }, 0.03f);
            Portal = c.ToSprite("Portal", Vector2.zero);
        }

        /// <summary>Spiked crown (white, tinted per theme).</summary>
        static void BuildCrown()
        {
            var c = new SdfCanvas(new Rect(-0.32f, -0.06f, 0.64f, 0.36f), 300f);
            SdfCanvas.SdfFn crown = p =>
            {
                float band = Sdf.Box(p, new Vector2(0f, 0.04f), new Vector2(0.26f, 0.045f), 0.02f);
                float d = band;
                for (int i = 0; i < 5; i++)
                {
                    float x = -0.2f + i * 0.1f, h = i == 2 ? 0.27f : (i == 1 || i == 3) ? 0.2f : 0.15f;
                    d = Sdf.SmoothUnion(d, Sdf.Triangle(p, new Vector2(x - 0.05f, 0.05f), new Vector2(x + 0.05f, 0.05f), new Vector2(x, h)), 0.01f);
                }
                return d;
            };
            c.Fill(p => crown(p) - 0.01f, new Color(0.15f, 0.1f, 0.05f));
            c.Fill(crown, p => V(p, 0f, 0.27f, new Color(0.8f, 0.55f, 0.2f), new Color(1f, 0.93f, 0.6f)));
            for (int i = 0; i < 3; i++)
            {
                float x = -0.1f + i * 0.1f;
                c.Fill(p => Sdf.Circle(p, new Vector2(x, 0.045f), 0.02f), i == 1 ? new Color(1f, 0.3f, 0.4f) : new Color(0.4f, 0.9f, 1f));
            }
            Crown = c.ToSprite("Crown", new Vector2(0f, 0f));
        }

        /// <summary>Glowing projectile core (white, tinted per shooter).</summary>
        static void BuildOrb()
        {
            var c = new SdfCanvas(new Rect(-0.16f, -0.16f, 0.32f, 0.32f), 256f);
            c.Fill(p => Sdf.Circle(p, Vector2.zero, 0.1f), p => Color.Lerp(Color.white, new Color(1f, 1f, 1f, 0.85f), MathUtil.Smooth01(p.magnitude / 0.1f)), 0.03f);
            Orb = c.ToSprite("Orb", Vector2.zero);
        }
    }
}
