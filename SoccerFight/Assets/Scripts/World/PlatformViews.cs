using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The platforms on screen: body and support sprites, grass that bends around whoever walks on
    /// them, hanging roots and ivy, lanterns, crystal and mushroom glow, chains holding the wooden
    /// decks — each platform in its own group so moving ones carry all of it along. Rebuilt whenever
    /// a stage brings a new layout.
    /// </summary>
    public sealed class PlatformViews
    {
        sealed class View
        {
            public Level.Platform P;
            public Transform Root;
            public SpriteRenderer Runes;
            public float Phase;
            public readonly List<SpriteRenderer> Chains = new List<SpriteRenderer>();
            public readonly List<float> ChainX = new List<float>();
        }
        struct Blink { public SpriteRenderer sr; public Color color; public float baseA, phase, speed; }
        struct Pebble { public Transform t; public Vector2 home; public float phase, amp, speed, spin; }
        struct Lamp { public Transform pivot, root; public SpriteRenderer glow, halo; public Transform[] moths; public float seed, chain; }
        struct Speck { public Transform root; public Vector2 local; }

        const float ChainTop = 13.5f;

        readonly List<View> views = new List<View>();
        readonly List<Blink> blinks = new List<Blink>();
        readonly List<Pebble> pebbles = new List<Pebble>();
        readonly List<Lamp> lamps = new List<Lamp>();
        readonly List<Speck> specks = new List<Speck>();
        Transform group;
        PlatformLook[] shown;
        Material grassMat, grassGlowMat, lipMat, hangMat;
        Material ruinGrassMat, ruinLipMat, ruinHangMat;
        System.Random rng = new System.Random(77);
        float speckTimer;

        float R() => (float)rng.NextDouble();
        float Range(float a, float b) => a + (b - a) * R();
        T Pick<T>(T[] arr) => arr[rng.Next(arr.Length)];

        public void Build(Transform parent)
        {
            group = new GameObject("Platforms").transform;
            group.SetParent(parent, false);
            grassMat = FoliageLayer.MakeMaterial("SF Foliage Platform", 0f, 1f);
            grassGlowMat = FoliageLayer.MakeMaterial("SF Foliage Platform Glow", 0f, 1f, true, 0.8f);
            lipMat = FoliageLayer.MakeMaterial("SF Foliage Platform Lip", 0f, 1f);
            hangMat = FoliageLayer.MakeMaterial("SF Foliage Hangings", 0f, 0.8f);
            ruinGrassMat = Stage1Mat("SF S1 Platform Grass", 1f);
            ruinLipMat = Stage1Mat("SF S1 Platform Lip", 1f);
            ruinHangMat = Stage1Mat("SF S1 Platform Vines", 0.8f);
        }

        static Material Stage1Mat(string name, float wind)
        {
            var m = FoliageLayer.MakeMaterial(name, 0f, wind);
            m.mainTexture = Stage1Art.PlantAtlas;
            return m;
        }

        /// <summary>Replace the shown platforms (frees the textures of the previous layout).</summary>
        public void Show(PlatformLook[] looks)
        {
            for (int i = group.childCount - 1; i >= 0; i--) Object.Destroy(group.GetChild(i).gameObject);
            views.Clear(); blinks.Clear(); pebbles.Clear(); lamps.Clear(); specks.Clear();
            if (shown != null && shown != looks) PlatformArt.Release(shown);
            shown = looks;
            rng = new System.Random(looks.Length * 131 + (looks.Length > 0 ? looks[0].P.Seed : 0));
            foreach (var look in looks) BuildOne(look);
            Game.I?.Grade?.Adopt(group);
        }

        void BuildOne(PlatformLook look)
        {
            var p = look.P;
            var v = new View { P = p, Phase = R() * 10f };
            v.Root = new GameObject(p.Kind + " Platform").transform;
            v.Root.SetParent(group, false);
            views.Add(v);

            // supports stand on the pitch (only grounded styles have one, and those never move)
            if (look.Support != null) Art.MakeSprite("Support", group, look.Support, -97).transform.localPosition = look.SupportCenter;
            if (look.Body != null) Art.MakeSprite(p.Kind + " Body", v.Root, look.Body, -82).transform.localPosition = look.BodyCenter;
            if (look.Glow != null)
            {
                v.Runes = Art.MakeSprite("Runes", v.Root, look.Glow, -81, Art.SpriteGlowMat, Palette.Crystal.WithAlpha(0.5f));
                v.Runes.transform.localPosition = look.GlowCenter;
            }

            var back = new FoliageLayer();
            var lip = new FoliageLayer();
            var hang = new FoliageLayer();
            float x0 = p.BaseX0, x1 = p.BaseX1, y = p.BaseY;
            if (look.Ruin)
            {
                // Stage 1: sattgrünes Gras, Blumen und Ranken wie im Referenzbild
                for (float x = x0 + 0.04f; x < x1 - 0.04f; x += Range(0.07f, 0.16f))
                {
                    var fv = R() < 0.9f ? Pick(Stage1Art.Grass) : Pick(Stage1Art.Flowers);
                    back.Add(fv, new Vector2(x, y + Range(0.05f, 0.12f)), Range(0.45f, 0.8f), Color.white, 1f, 1f, R() > 0.5f);
                }
                for (float x = x0 - 0.05f; x < x1 + 0.05f; x += Range(0.14f, 0.3f))
                    lip.Add(Pick(Stage1Art.Grass), new Vector2(x, y - Range(0.1f, 0.15f)), Range(0.3f, 0.5f), Color.white, 1f, 0.7f, R() > 0.5f);
                foreach (var h in look.Hangs)
                    if (R() < 0.8f) hang.Add(Pick(Stage1Art.Vines), h, Range(0.5f, 0.95f), Color.white, 1f, 0.5f, R() > 0.5f);
                if (hang.Count > 0) hang.Build(v.Root, "Vines", -83, ruinHangMat);
                back.Build(v.Root, "Grass", -80, ruinGrassMat);
                lip.Build(v.Root, "Lip Grass", 50, ruinLipMat);
                Place(v);
                return;
            }
            bool rock = p.Kind == Level.Style.Rock;

            switch (p.Kind)
            {
                case Level.Style.Crystal:
                    for (float x = x0 + 0.2f; x < x1 - 0.2f; x += Range(0.5f, 1.1f))
                        back.Add(Pick(FoliageArt.Crystals), new Vector2(x, y + Range(0.06f, 0.1f)), Range(0.3f, 0.48f), Color.white, 0f, 0f, R() > 0.5f, Range(-20f, 20f));
                    for (float x = x0 + 0.1f; x < x1 - 0.1f; x += Range(0.35f, 0.8f))
                        back.Add(Pick(FoliageArt.Grass), new Vector2(x, y + Range(0.05f, 0.1f)), Range(0.35f, 0.55f), new Color(0.85f, 1f, 1f, 1f), 1f, 1f, R() > 0.5f);
                    break;
                case Level.Style.Mushroom:
                    for (float x = x0 + 0.1f; x < x1 - 0.1f; x += Range(0.18f, 0.45f))
                    {
                        var fv = R() < 0.3f ? Pick(FoliageArt.Mushrooms) : R() < 0.7f ? Pick(FoliageArt.Grass) : Pick(FoliageArt.Clover);
                        back.Add(fv, new Vector2(x, y + Range(0.05f, 0.11f)), Range(0.4f, 0.7f), Color.white, 1f, 1f, R() > 0.5f);
                    }
                    // a family of small mushrooms around the stalk's foot on the pitch
                    for (int i = 0; i < 3; i++)
                        back.Add(Pick(FoliageArt.Mushrooms), new Vector2((x0 + x1) * 0.5f + Range(-0.9f, 0.9f), Range(0.24f, 0.3f)),
                            Range(0.6f, 1f), Color.white, 1f, 0.6f, R() > 0.5f);
                    break;
                default:
                {
                    float step0 = rock ? 0.08f : p.Kind == Level.Style.Plank ? 0.3f : 0.14f;
                    float step1 = rock ? 0.2f : p.Kind == Level.Style.Plank ? 0.7f : 0.4f;
                    for (float x = x0 + 0.05f; x < x1 - 0.05f; x += Range(step0, step1))
                    {
                        float roll = R();
                        var fv = roll < 0.82f ? Pick(FoliageArt.Grass) : roll < 0.92f ? Pick(FoliageArt.Clover) : rock ? Pick(FoliageArt.Flowers) : Pick(FoliageArt.Ferns);
                        back.Add(fv, new Vector2(x, y + Range(0.05f, 0.12f)), Range(0.42f, 0.8f), Color.white, 1f, 1f, R() > 0.5f);
                    }
                    if (rock)
                        for (int i = 0; i < 2; i++)
                            back.Add(Pick(FoliageArt.Mushrooms), new Vector2(Range(x0 + 0.3f, x1 - 0.3f), y + 0.1f), Range(0.5f, 0.72f), Color.white, 1f, 0.6f, R() > 0.5f);
                    break;
                }
            }
            if (p.Kind != Level.Style.Crystal)
                for (float x = x0; x < x1; x += Range(0.18f, 0.42f) * (p.Kind == Level.Style.Plank || p.Kind == Level.Style.Mushroom ? 2f : 1f))
                    lip.Add(R() > 0.85f ? Pick(FoliageArt.Clover) : Pick(FoliageArt.Grass), new Vector2(x, y - Range(0.1f, 0.15f)), Range(0.3f, 0.5f),
                        new Color(0.95f, 1f, 1f, 1f), 1f, 0.7f, R() > 0.5f);
            foreach (var h in look.Hangs)
            {
                FoliageArt.Variant hv;
                if (rock || p.Kind == Level.Style.Block) hv = R() > 0.3f ? Pick(FoliageArt.Roots) : Pick(FoliageArt.Moss);
                else hv = R() > 0.45f ? Pick(FoliageArt.Ivy) : Pick(FoliageArt.Moss);
                hang.Add(hv, h, rock ? Range(0.75f, 1.15f) : Range(0.5f, 0.85f), Color.white, 1f, 0.5f, R() > 0.5f);
            }
            if (hang.Count > 0) hang.Build(v.Root, "Hangings", -83, hangMat);
            if (back.Count > 0) back.Build(v.Root, "Grass", -80, grassMat, grassGlowMat, -79);
            if (lip.Count > 0) lip.Build(v.Root, "Lip Grass", 50, lipMat);

            if (look.HasLantern) AddLantern(v.Root, look.Lantern, -81, 0.3f, R() * 50f);

            // glow: crystal tips flicker and shed specks, mushroom spots and runes breathe
            foreach (var c in look.Crystals)
            {
                AddBlink(v.Root, c, p.Kind == Level.Style.Crystal ? 0.3f : 0.42f, Palette.Crystal, p.Kind == Level.Style.Crystal ? 0.22f : 0.32f, -81);
                specks.Add(new Speck { root = v.Root, local = c });
            }
            foreach (var s in look.Spots)
            {
                bool pool = s.z > 0.8f;
                Color sc = p.Kind == Level.Style.Mushroom ? (pool ? Palette.MushCap : Palette.Petal) : Palette.Crystal;
                var sr = Art.MakeSprite(pool ? "Glow Pool" : "Glow Spot", v.Root, Art.SoftGlow, pool ? -84 : -81, Art.SpriteAddMat, sc.WithAlpha(0f));
                sr.transform.localPosition = new Vector2(s.x, s.y);
                sr.transform.localScale = pool ? new Vector3(s.z, s.z * 0.55f, 1f) : Vector3.one * s.z;
                blinks.Add(new Blink { sr = sr, color = sc, baseA = pool ? 0.08f : 0.28f, phase = R() * 20f, speed = pool ? 0.6f : Range(0.8f, 1.6f) });
            }

            if (rock || p.Kind == Level.Style.Block)
            {
                // a soft pool of crystal light under the stone and a few pebbles floating in it
                var pool = Art.MakeSprite("Rock Light", v.Root, Art.SoftGlow, -84, Art.SpriteAddMat, Palette.Crystal.WithAlpha(0.07f));
                pool.transform.localPosition = new Vector3((x0 + x1) * 0.5f, y - 1f, 0f);
                pool.transform.localScale = new Vector3(p.Width * 1.3f, 2.2f, 1f);
                int n = p.Width < 2.4f ? 2 : 3;
                for (int i = 0; i < n; i++)
                {
                    var peb = Art.MakeSprite("Floating Pebble", v.Root, DepthArt.Pebbles[i % DepthArt.Pebbles.Length], -84);
                    float s = Range(0.6f, 1f);
                    peb.transform.localScale = new Vector3(s, s, 1f);
                    pebbles.Add(new Pebble
                    {
                        t = peb.transform, home = new Vector2(Mathf.Lerp(x0 + 0.4f, x1 - 0.4f, (i + 0.5f) / n) + Range(-0.2f, 0.2f), y - Range(1.45f, 1.9f) + (rock ? 0f : 0.35f)),
                        phase = R() * 10f, amp = Range(0.05f, 0.1f), speed = Range(0.7f, 1.2f), spin = Range(-14f, 14f)
                    });
                }
            }

            // wooden decks hang on chains that disappear up into the dark
            foreach (float cx in look.ChainX)
            {
                var ch = Art.MakeSprite("Chain", group, EnvironmentArt.Chain, -85);
                ch.drawMode = SpriteDrawMode.Tiled;
                ch.transform.localScale = new Vector3(1.4f, 1f, 1f);
                v.Chains.Add(ch);
                v.ChainX.Add(cx);
            }
            Place(v);
        }

        void AddBlink(Transform parent, Vector2 pos, float size, Color color, float alpha, int order)
        {
            var sr = Art.MakeSprite("Light", parent, Art.SoftGlow, order, Art.SpriteGlowMat, color.WithAlpha(alpha));
            sr.transform.localPosition = pos;
            sr.transform.localScale = Vector3.one * size;
            blinks.Add(new Blink { sr = sr, color = color, baseA = alpha, phase = R() * 20f, speed = Range(1.5f, 3.5f) });
        }

        void AddLantern(Transform parent, Vector2 spot, int order, float chainLen, float seed)
        {
            var pivot = new GameObject("Lantern").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = spot;
            var chain = Art.MakeSprite("Chain", pivot, EnvironmentArt.Chain, order + 1);
            chain.transform.localScale = new Vector3(1f, chainLen, 1f);
            var body = Art.MakeSprite("Body", pivot, EnvironmentArt.Lantern, order + 2);
            body.transform.localPosition = new Vector3(0f, -chainLen, 0f);
            var halo = Art.MakeSprite("Halo", pivot, Art.SoftGlow, order, Art.SpriteAddMat, Palette.Lantern.WithAlpha(0.22f));
            halo.transform.localPosition = new Vector3(0f, -chainLen - 0.26f, 0f);
            halo.transform.localScale = Vector3.one * 2.6f;
            var glow = Art.MakeSprite("Glow", pivot, Art.SoftGlow, order + 3, Art.SpriteGlowMat, Palette.Lantern.WithAlpha(0.5f));
            glow.transform.localPosition = new Vector3(0f, -chainLen - 0.27f, 0f);
            glow.transform.localScale = Vector3.one * 0.5f;
            var moths = new Transform[2];
            for (int m = 0; m < 2; m++)
            {
                var moth = Art.MakeSprite("Moth", parent, Art.Circle, order + 4, Art.SpriteMat, new Color(0.85f, 0.82f, 0.7f, 0.9f));
                moth.transform.localScale = Vector3.one * 0.028f;
                moths[m] = moth.transform;
            }
            lamps.Add(new Lamp { pivot = pivot, root = parent, glow = glow, halo = halo, moths = moths, seed = seed, chain = chainLen });
        }

        void Place(View v)
        {
            Vector2 off = v.P.Offset;
            v.Root.localPosition = new Vector3(off.x, off.y, 0f);
            for (int i = 0; i < v.Chains.Count; i++)
            {
                // the top stays put: a gliding deck swings on its chains like a pendulum
                Vector2 bottom = new Vector2(v.ChainX[i] + off.x, v.P.Y + 0.14f);
                Vector2 top = new Vector2(v.ChainX[i], ChainTop);
                Vector2 d = bottom - top;
                var ch = v.Chains[i];
                ch.transform.localPosition = top;
                ch.transform.localRotation = Quaternion.Euler(0f, 0f, MathUtil.DownAngle(d));
                ch.size = new Vector2(0.08f, d.magnitude);
            }
        }

        public void Update(float dt, float time, float wind)
        {
            foreach (var v in views)
            {
                Place(v);
                if (v.Runes != null)
                {
                    float pulse = 0.55f + 0.3f * Mathf.Sin(time * 1.3f + v.Phase) + 0.15f * Mathf.PerlinNoise(time * 2.1f, v.Phase);
                    v.Runes.color = Palette.Crystal.WithAlpha(0.55f * pulse);
                }
            }

            for (int i = 0; i < blinks.Count; i++)
            {
                var b = blinks[i];
                float f = 0.72f + 0.28f * Mathf.PerlinNoise(time * b.speed, b.phase);
                b.sr.color = b.color.WithAlpha(b.baseA * f);
            }

            for (int i = 0; i < pebbles.Count; i++)
            {
                var p = pebbles[i];
                float t = time * p.speed + p.phase;
                p.t.localPosition = p.home + new Vector2(Mathf.Sin(t * 0.7f) * 0.05f, Mathf.Sin(t) * p.amp);
                p.t.localRotation = Quaternion.Euler(0f, 0f, time * p.spin + p.phase * 30f);
            }

            for (int i = 0; i < lamps.Count; i++)
            {
                var l = lamps[i];
                float swing = Mathf.Sin(time * 1.25f + l.seed) * 3.2f + Mathf.Sin(time * 0.47f + l.seed * 2f) * 1.2f + wind * 6f;
                l.pivot.localRotation = Quaternion.Euler(0f, 0f, swing);
                float flicker = 0.82f + 0.18f * Mathf.PerlinNoise(time * 3.3f, l.seed) + 0.05f * Mathf.Sin(time * 19f + l.seed);
                l.glow.color = Palette.Lantern.WithAlpha(0.5f * flicker);
                l.halo.color = Palette.Lantern.WithAlpha(0.22f * flicker);
                Vector3 center = l.pivot.localPosition + l.pivot.localRotation * new Vector3(0f, -l.chain - 0.27f, 0f);
                for (int m = 0; m < l.moths.Length; m++)
                {
                    float a = time * (2.4f + m * 0.9f) + l.seed + m * 2.1f;
                    l.moths[m].localPosition = center + new Vector3(Mathf.Cos(a) * (0.22f + m * 0.07f), Mathf.Sin(a * 1.7f) * 0.14f + 0.02f, 0f);
                }
            }

            // specks of light drifting off the crystals
            var fx = FxSystem.I;
            speckTimer -= dt;
            while (speckTimer <= 0f && fx != null && specks.Count > 0)
            {
                speckTimer += 0.22f;
                var s = specks[Random.Range(0, specks.Count)];
                Vector2 at = (Vector2)s.root.position + s.local + Random.insideUnitCircle * 0.12f;
                Color cc = Palette.Crystal.WithAlpha(0.8f);
                fx.Spawn(FxLayer.Back, true, Art.CellDot, at, new Vector2((Random.value - 0.5f) * 0.2f, -0.15f - Random.value * 0.25f),
                    Random.Range(1.4f, 2.4f), Random.Range(0.035f, 0.06f), 0.01f, cc, cc, 2.2f, 0.4f, 0f, 0f, 0f, false, true);
            }
        }

        /// <summary>Dust and sparkles along every platform (a new layout settling in).</summary>
        public void Flourish()
        {
            var fx = FxSystem.I;
            if (fx == null) return;
            foreach (var v in views)
            {
                var p = v.P;
                for (float x = p.X0 + 0.3f; x < p.X1; x += 0.9f)
                    fx.Dust(new Vector2(x, p.Y), new Vector2(Random.value - 0.5f, 0.3f), 2, 1.2f, 0.4f, 0.3f);
                fx.Sparkles(new Vector2(p.Center, p.Y + 0.2f), p.Width * 0.4f, 6, Palette.Crystal, 2.4f, 0.8f);
            }
        }
    }
}
