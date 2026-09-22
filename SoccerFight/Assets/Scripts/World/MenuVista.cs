using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The title screen's backdrop is a place in the game's own world, not a picture: the game
    /// camera films it with the game's materials, wind, bloom and grading, so it looks exactly like
    /// the arenas. It lives on its own layer — while the menu covers the screen the camera draws only
    /// this layer (and the menu), otherwise never.
    ///
    /// The scene (art in MenuScenery): a mountain valley after the catastrophe. The moon hangs over
    /// the valley's low saddle; far back a massif with its summit split open, crystal light welling
    /// out of the cleft, and a peak sheared flat; burnt ridges in front of it with embers still
    /// smoking in a blast crater; a wrecked plain with a glowing rift, smouldering craters, tilted
    /// slabs of bedrock and dead trees; and the cracked plateau the player stands on (MainMenu keeps
    /// it under the boots). Alive with smoke columns,
    /// embers, falling ash, crystal motes, drifting mist, sheet lightning far behind the peaks and
    /// eyes in the dark. Every layer leans with the pointer by its depth.
    /// </summary>
    public sealed class MenuVista
    {
        public const int Layer = 25;
        public static int Mask => 1 << Layer;

        /// <summary>The vista is composed for a view this tall (the camera's base size).</summary>
        const float ViewHalfH = 4.9f;
        /// <summary>Content reaches this far to each side before the view has to scale up.</summary>
        const float CoverHalfW = 10.4f;
        const float MoonRadius = 1.0f;

        sealed class Part { public Transform T; public Vector2 Home; public float Lean; }
        struct Blink { public SpriteRenderer Sr; public Color Color; public float Base, Phase, Speed; }
        sealed class Puff { public SpriteRenderer Sr; public float Age, Life, Size, Spin, Drift; public Vector2 Pos; }
        sealed class Plume { public Transform Parent; public Vector2 Source; public float Scale, Rate, Timer, Light; public int Order; public readonly List<Puff> Puffs = new List<Puff>(); }
        sealed class Mote { public SpriteRenderer Sr; public Vector2 Pos, Vel; public float Age, Life, Size, Phase; public int Kind; }
        struct Fog { public SpriteRenderer Sr; public float Speed, Offset; }
        sealed class Eyes { public Transform T; public Transform L, R; public SpriteRenderer GlowL, GlowR; public int Spot; public float Timer, Open, Target, Blink; }

        readonly List<Part> parts = new List<Part>();
        readonly List<Blink> blinks = new List<Blink>();
        readonly List<Plume> plumes = new List<Plume>();
        readonly List<Mote> embers = new List<Mote>();
        readonly List<Mote> ash = new List<Mote>();
        readonly List<Mote> motes = new List<Mote>();
        readonly List<Fog> fogs = new List<Fog>();
        readonly List<SpriteRenderer> rays = new List<SpriteRenderer>();
        readonly List<float> rayAlpha = new List<float>();
        readonly List<Vector2> emberSources = new List<Vector2>();   // vista units, where embers rise from
        readonly List<Vector2> moteSources = new List<Vector2>();
        readonly List<(Transform t, float speed)> hazes = new List<(Transform, float)>();
        readonly Ambient ambient = new Ambient();
        readonly Eyes[] eyes = new Eyes[2];

        /// <summary>Dark places on the plain where eyes can turn up (vista units).</summary>
        static readonly Vector2[] EyeSpots =
        {
            new Vector2(-6.1f, -2.62f), new Vector2(2.45f, -2.12f), new Vector2(-2.35f, -1.88f), new Vector2(-7.7f, -2.95f),
        };

        Transform root, ground, peaksT;
        SpriteRenderer flashSky, flashPeaks;
        System.Random rng;
        Vector2 lean, drift;
        float time, groundY = -3.56f, flashT = 5f, flashA;
        bool built;

        public bool Ready => built;

        float R() => (float)rng.NextDouble();
        float Range(float a, float b) => a + (b - a) * R();
        T Pick<T>(T[] arr) => arr[rng.Next(arr.Length)];

        static Color Shade(Color c, Color tint) => new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);

        Transform Group(string name, Vector2 home, float lean)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = home;
            parts.Add(new Part { T = t, Home = home, Lean = lean });
            return t;
        }

        SpriteRenderer Glow(string name, Transform parent, Vector2 pos, float size, Color color, float alpha, int order, float speed = 2f, bool additive = false)
        {
            var sr = Art.MakeSprite(name, parent, Art.SoftGlow, order, additive ? Art.SpriteAddMat : Art.SpriteGlowMat, color.WithAlpha(alpha));
            sr.transform.localPosition = pos;
            sr.transform.localScale = Vector3.one * size;
            blinks.Add(new Blink { Sr = sr, Color = color, Base = alpha, Phase = R() * 20f, Speed = speed });
            return sr;
        }

        void AddFog(Transform parent, float y, float height, float alpha, float speed, float depth, int order)
        {
            Color c = Shade(Palette.Fog, WorldEnvironment.DepthTint(depth * 0.85f)).WithAlpha(alpha);
            var sr = Art.MakeSprite("Fog", parent, EnvironmentArt.FogBand, order, Art.SpriteMat, c);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(64f, 2f);
            sr.transform.localScale = new Vector3(1f, height / 2f, 1f);
            sr.transform.localPosition = new Vector3(-32f, y, 0f);
            fogs.Add(new Fog { Sr = sr, Speed = speed, Offset = R() * 8f });
        }

        /// <summary>A column of smoke rising from a point of a layer (layer-local), puffs cycling through it.</summary>
        void AddPlume(Transform parent, Vector2 source, float scale, float light, int order, int count, float rate)
        {
            var p = new Plume { Parent = parent, Source = source, Scale = scale, Rate = rate, Light = light, Order = order, Timer = 0f };
            for (int i = 0; i < count; i++)
            {
                var sr = Art.MakeSprite("Smoke", parent, MenuScenery.Smoke, order, Art.SpriteMat, Color.clear);
                // spread over the column's life so it is already standing when the menu opens
                var puff = new Puff { Sr = sr, Life = Range(5f, 7.5f), Spin = Range(-12f, 12f), Drift = Range(0.8f, 1.2f) };
                puff.Age = puff.Life * i / count;
                puff.Pos = source;
                puff.Size = scale * Range(0.45f, 0.6f);
                p.Puffs.Add(puff);
            }
            plumes.Add(p);
        }

        // ------------------------------------------------------------------ build

        public void Build(Transform parent)
        {
            root = new GameObject("Menu Vista").transform;
            root.SetParent(parent, false);
            rng = new System.Random(4242);
            if (MenuScenery.Moon == null || MenuScenery.Peaks == null || EnvironmentArt.Sky == null) return;

            BuildSky();
            BuildPeaks();
            BuildRidge();
            BuildWaste();
            BuildGround();
            BuildLife();

            SetLayer(root, Layer);
            root.gameObject.SetActive(false);
            built = true;
        }

        public static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }

        void BuildSky()
        {
            var sky = Group("Sky", new Vector2(0f, -3.4f), 0.01f);
            Art.MakeSprite("Gradient", sky, EnvironmentArt.Sky, -1000).transform.localScale = new Vector3(110f, 1f, 1f);

            var stars = Group("Stars", new Vector2(0f, -1.4f), 0.02f);
            Art.MakeSprite("Stars", stars, EnvironmentArt.Stars, -995, Art.SpriteAddMat, new Color(1f, 1f, 1f, 0.85f)).transform.localScale = new Vector3(1.15f, 1f, 1f);
            var twinkles = new GameObject("Twinkles").transform;
            twinkles.SetParent(stars, false);
            twinkles.localPosition = new Vector3(0f, -1.2f, 0f);
            var clouds = Group("Clouds", new Vector2(0f, -3.9f), 0.04f);
            var bats = Group("Bats", Vector2.zero, 0.05f);
            ambient.BuildSky(twinkles, clouds, bats);

            // the smoke of the catastrophe hangs over the valley in long dark banks, drifting slowly
            var haze = Group("Haze", Vector2.zero, 0.05f);
            for (int i = 0; i < 7; i++)
            {
                var sr = Art.MakeSprite("Haze", haze, MenuScenery.Smoke, -981, Art.SpriteMat, new Color(0.1f, 0.15f, 0.19f, 0.26f));
                float s = Range(3.5f, 6f);
                sr.transform.localScale = new Vector3(s * 1.8f, s * 0.55f, 1f);
                sr.transform.localPosition = new Vector3(Range(-12f, 12f), Range(1.2f, 3.6f), 0f);
                hazes.Add((sr.transform, Range(0.04f, 0.1f)));
            }

            // sheet lightning far behind the mountains: the sky lights up for a moment
            var storm = Group("Storm", new Vector2(-5.5f, 1.2f), 0.04f);
            flashSky = Art.MakeSprite("Sky Flash", storm, Art.SoftGlow, -990, Art.SpriteAddMat, new Color(0.6f, 0.8f, 1f, 0f));
            flashSky.transform.localScale = new Vector3(16f, 7f, 1f);

            // the moon over the valley's saddle, up to the right of the player
            var moon = Group("Moon", MenuScenery.MoonPos, 0.03f);
            Art.MakeSprite("Halo Wide", moon, Art.SoftGlow, -986, Art.SpriteAddMat, Palette.MoonGlow.WithAlpha(0.15f)).transform.localScale = Vector3.one * 17f;
            Art.MakeSprite("Halo", moon, Art.SoftGlow, -985, Art.SpriteAddMat, Palette.MoonGlow.WithAlpha(0.32f)).transform.localScale = Vector3.one * 6.2f;
            float disc = MoonRadius / 0.55f;
            var body = Art.MakeSprite("Disc", moon, MenuScenery.Moon, -983, Art.SpriteEmissiveMat, Color.white);
            body.sharedMaterial = Art.MakeSpriteMaterial("SF Menu Moon", 1.3f, false);
            body.transform.localScale = Vector3.one * disc;
            Art.MakeSprite("Rim", moon, Art.Ring, -982, Art.SpriteGlowMat, Palette.MoonGlow.WithAlpha(0.14f)).transform.localScale = Vector3.one * disc * 1.22f;

            float[] angles = { -44f, -29f, -15f, -4f, 9f, 22f, 36f, 50f };
            float[] widths = { 1.6f, 2.8f, 1.2f, 2.2f, 1.5f, 3.0f, 1.3f, 2.0f };
            for (int i = 0; i < angles.Length; i++)
            {
                var r = Art.MakeSprite("Ray" + i, moon, Art.LightRay, -760, Art.SpriteAddMat, Palette.MoonGlow.WithAlpha(0f));
                r.transform.localPosition = MathUtil.Dir(angles[i] - 90f) * MoonRadius * 0.6f;
                r.transform.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
                r.transform.localScale = new Vector3(widths[i], 1.5f, 1f);
                rays.Add(r);
                rayAlpha.Add(0.045f + 0.02f * (i % 3));
            }
        }

        void BuildPeaks()
        {
            peaksT = Group("Peaks", new Vector2(0f, MenuScenery.PeaksY), 0.08f);
            Color tint = WorldEnvironment.DepthTint(0.74f);
            Art.MakeSprite("Massif", peaksT, MenuScenery.Peaks, -950, Art.SpriteMat, tint);
            // the flash lights the crests from behind
            flashPeaks = Art.MakeSprite("Flash", peaksT, Art.SoftGlow, -951, Art.SpriteAddMat, new Color(0.65f, 0.85f, 1f, 0f));
            flashPeaks.transform.localPosition = new Vector3(-5.5f, 2.4f, 0f);
            flashPeaks.transform.localScale = new Vector3(9f, 4f, 1f);
            // crystal light out of the split summit
            foreach (var g in MenuScenery.CleftGlows)
                Glow("Cleft", peaksT, new Vector2(g.x, g.y), g.z, Palette.Crystal, g.z > 0.5f ? 0.22f : 0.2f, -948, Range(0.8f, 2f));
            var beam = Art.MakeSprite("Cleft Beam", peaksT, Art.LightRay, -949, Art.SpriteAddMat, Palette.Crystal.WithAlpha(0.06f));
            beam.transform.localPosition = new Vector3(6.1f, 1.75f, 0f);
            beam.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            beam.transform.localScale = new Vector3(0.9f, 0.55f, 1f);
            // thin smoke from far fires behind the front range
            AddPlume(peaksT, new Vector2(-2.2f, 0.75f), 0.9f, 0.6f, -947, 6, 1f);
            AddPlume(peaksT, new Vector2(8.3f, 1.35f), 0.8f, 0.6f, -947, 6, 1f);
            AddFog(peaksT, 0.1f, 1.5f, 0.17f, 0.07f, 0.95f, -945);
        }

        void BuildRidge()
        {
            var ridge = Group("Ridge", new Vector2(0f, MenuScenery.RidgeY), 0.15f);
            Color tint = WorldEnvironment.DepthTint(0.6f);
            Art.MakeSprite("Ridge", ridge, MenuScenery.Ridge, -900, Art.SpriteMat, tint);
            foreach (var f in MenuScenery.RidgeFires)
            {
                Vector2 at = new Vector2(f.x, f.y);
                Glow("Embers", ridge, at, 0.5f * f.z, Palette.BlastOrange, 0.3f, -899, 3f);
                Glow("Ember Core", ridge, at + new Vector2(0f, -0.02f), 0.18f * f.z, Palette.Lantern, 0.5f, -898, 5f);
                AddPlume(ridge, at + new Vector2(0f, 0.05f), 1.5f * f.z, 0.45f, -897, 9, 1f);
                emberSources.Add(new Vector2(f.x, f.y + MenuScenery.RidgeY));
            }
            AddFog(ridge, -0.55f, 1.2f, 0.15f, -0.1f, 0.6f, -880);
        }

        void BuildWaste()
        {
            var waste = Group("Plain", new Vector2(0f, MenuScenery.WasteY), 0.26f);
            Color tint = WorldEnvironment.DepthTint(0.3f);
            Art.MakeSprite("Plain", waste, MenuScenery.Waste, -850, Art.SpriteMat, tint);
            foreach (var g in MenuScenery.RiftGlows)
            {
                Glow("Rift", waste, new Vector2(g.x, g.y), g.z * 0.55f, Palette.Crystal, 0.22f, -848, Range(1f, 2.5f));
                moteSources.Add(new Vector2(g.x, g.y + MenuScenery.WasteY));
            }
            foreach (var cr in MenuScenery.Craters)
            {
                Vector2 at = new Vector2(cr.x, cr.y);
                Glow("Crater Glow", waste, at, cr.z * 0.8f, Palette.BlastOrange, 0.16f, -847, 2.5f);
                AddPlume(waste, at + new Vector2(0f, 0.04f), 1.1f + cr.z * 0.9f, 0.3f, -846, 9, 1f);
                emberSources.Add(new Vector2(cr.x, cr.y + MenuScenery.WasteY));
            }
            AddFog(waste, 0.05f, 0.9f, 0.14f, 0.16f, 0.35f, -840);
            AddFog(waste, -0.8f, 1.1f, 0.08f, -0.12f, 0.25f, -839);

            // something watches from the dark between the slabs
            for (int i = 0; i < eyes.Length; i++)
            {
                var e = new Eyes { Spot = i == 0 ? 0 : 1, Timer = 2f + i * 3.5f, Blink = R() * 3f };
                e.T = new GameObject("Eyes").transform;
                e.T.SetParent(root, false);
                e.T.localPosition = EyeSpots[e.Spot];
                e.GlowL = Art.MakeSprite("GlowL", e.T, Art.SoftGlow, -843, Art.SpriteGlowMat, Palette.MonsterGlow.WithAlpha(0f));
                e.GlowR = Art.MakeSprite("GlowR", e.T, Art.SoftGlow, -843, Art.SpriteGlowMat, Palette.MonsterGlow.WithAlpha(0f));
                e.GlowL.transform.localPosition = new Vector3(-0.06f, 0f, 0f);
                e.GlowR.transform.localPosition = new Vector3(0.06f, 0f, 0f);
                e.GlowL.transform.localScale = e.GlowR.transform.localScale = Vector3.one * 0.22f;
                e.L = Art.MakeSprite("L", e.T, Art.Circle, -842, Art.SpriteEmissiveMat, Palette.MonsterEye).transform;
                e.R = Art.MakeSprite("R", e.T, Art.Circle, -842, Art.SpriteEmissiveMat, Palette.MonsterEye).transform;
                eyes[i] = e;
            }
        }

        void BuildGround()
        {
            // pinned under the player's boots every frame (PlaceGround); it does not lean
            ground = new GameObject("Plateau").transform;
            ground.SetParent(root, false);
            ground.localPosition = new Vector3(0f, groundY, 0f);
            Color tint = WorldEnvironment.DepthTint(0.02f);
            Art.MakeSprite("Plateau", ground, MenuScenery.Ground, -600, Art.SpriteMat, tint);
            foreach (var g in MenuScenery.GroundGlows)
            {
                Glow("Crack", ground, new Vector2(g.x, g.y), g.z, Palette.Crystal, 0.2f, -596, Range(0.8f, 2f), true);
                moteSources.Add(new Vector2(g.x, g.y));   // ground-local: offset by the ground's height when used
            }

            // dry, sparse grass and a few crystal shards along the plateau (they sway in the wind)
            var tufts = new FoliageLayer();
            var shards = new FoliageLayer();
            for (float x = -12f; x < 12f; x += Range(0.25f, 0.7f))
            {
                if (Mathf.Abs(x) < 1.5f) continue;
                if (Noise.Perlin(x * 0.7f, 5.5f) < 0.38f) continue;
                float v = Range(0.02f, MenuScenery.GroundBand - 0.08f);
                float y = MenuScenery.GroundTop(x) - v;
                float roll = R();
                if (roll < 0.8f) tufts.Add(Pick(FoliageArt.Grass), new Vector2(x, y), Range(0.35f, 0.7f) * (0.7f + 0.6f * v / MenuScenery.GroundBand),
                    new Color(0.62f, 0.72f, 0.7f, 1f), 1f, 0f, R() > 0.5f);
                else if (roll < 0.9f) tufts.Add(Pick(FoliageArt.TallGrass), new Vector2(x, y), Range(0.45f, 0.7f), new Color(0.55f, 0.65f, 0.64f, 1f), 1f, 0f, R() > 0.5f);
                else shards.Add(Pick(FoliageArt.Crystals), new Vector2(x, y), Range(0.45f, 0.8f), Color.white, 0f, 0f, R() > 0.5f, Range(-25f, 25f));
            }
            tufts.Build(ground, "Tufts", -594, FoliageLayer.MakeMaterial("SF Menu Tufts", 0f, 1f, false, 1f, tint));
            shards.Build(ground, "Shards", -593, FoliageLayer.MakeMaterial("SF Menu Shards", 0f, 0f), FoliageLayer.MakeMaterial("SF Menu Shards Glow", 0f, 0f, true, 0.8f), -592);
            AddFog(ground, -0.1f, 0.7f, 0.06f, 0.26f, 0.05f, -590);
        }

        void BuildLife()
        {
            // embers rising out of the fires and craters, and drifting across the whole scene
            for (int i = 0; i < 46; i++)
            {
                var sr = Art.MakeSprite("Ember", root, Art.SoftGlow, i < 8 ? 610 : -560, Art.SpriteGlowMat, Palette.BlastOrange.WithAlpha(0f));
                embers.Add(new Mote { Sr = sr, Life = 0f, Age = Range(0f, 4f), Kind = i < 8 ? 1 : 0 });
            }
            // ash falling slowly, some of it right in front of the lens
            for (int i = 0; i < 60; i++)
            {
                bool front = i < 8;
                var sr = Art.MakeSprite("Ash", root, Art.SoftGlow, front ? 611 : -570, Art.SpriteMat, new Color(0.7f, 0.8f, 0.85f, 0f));
                var m = new Mote { Sr = sr, Kind = front ? 1 : 0, Phase = R() * 10f };
                Respawn(m, true);
                ash.Add(m);
            }
            // crystal motes rising out of the rift and the cracks
            for (int i = 0; i < 26; i++)
            {
                var sr = Art.MakeSprite("Mote", root, Art.SoftGlow, -589, Art.SpriteGlowMat, Palette.Crystal.WithAlpha(0f));
                motes.Add(new Mote { Sr = sr, Life = 0f, Age = Range(0f, 3f) });
            }
        }

        void Respawn(Mote m, bool anywhere)
        {
            bool front = m.Kind == 1;
            m.Pos = new Vector2(Range(-11f, 11f), anywhere ? Range(-5f, 5f) : Range(5f, 5.8f));
            m.Vel = new Vector2(Range(0.12f, 0.35f), -Range(0.12f, 0.3f)) * (front ? 2.2f : 1f);
            m.Size = front ? Range(0.07f, 0.13f) : Range(0.025f, 0.055f);
            m.Age = 0f;
            m.Life = 99f;
        }

        // ------------------------------------------------------------------ update

        public void SetVisible(bool on)
        {
            if (!built || root.gameObject.activeSelf == on) return;
            root.gameObject.SetActive(on);
        }

        /// <param name="aim">pointer, -1..1 on both axes</param>
        /// <param name="zoom">1 = composed framing; above 1 the view pushes in</param>
        /// <param name="shift">extra offset of the whole scene (sub pages lift it a little)</param>
        public void Update(float dt, Camera cam, Vector2 center, Vector2 aim, float zoom, Vector2 shift, float wind)
        {
            if (!built || !root.gameObject.activeSelf) return;
            time += dt;

            float size = cam.orthographicSize;
            float cover = Mathf.Max(1f, size * cam.aspect / CoverHalfW);
            float scale = size / ViewHalfH * cover * zoom;
            root.localPosition = new Vector3(center.x, center.y, 0f);
            root.localScale = new Vector3(scale, scale, 1f);

            lean = Vector2.Lerp(lean, aim, Mathf.Clamp01(dt * 2.2f));
            drift = Vector2.Lerp(drift, shift, Mathf.Clamp01(dt * 4f));
            foreach (var p in parts)
            {
                p.T.localPosition = p.Home - lean * (0.08f + p.Lean * 0.42f) + drift * (0.3f + p.Lean);
            }

            for (int i = 0; i < blinks.Count; i++)
            {
                var b = blinks[i];
                float k = 0.7f + 0.3f * Mathf.PerlinNoise(time * b.Speed, b.Phase);
                b.Sr.color = b.Color.WithAlpha(b.Base * k * (1f + flashA * 0.6f));
            }
            for (int i = 0; i < rays.Count; i++)
            {
                float a = rayAlpha[i] * (0.65f + 0.35f * Mathf.Sin(time * (0.35f + i * 0.07f) + i * 1.7f));
                rays[i].color = Palette.MoonGlow.WithAlpha(a);
            }
            for (int i = 0; i < fogs.Count; i++)
            {
                var f = fogs[i];
                f.Offset = Mathf.Repeat(f.Offset + f.Speed * dt, 8f);
                var p = f.Sr.transform.localPosition;
                f.Sr.transform.localPosition = new Vector3(-32f + f.Offset - 4f, p.y, 0f);
                fogs[i] = f;
            }

            foreach (var (t, speed) in hazes)
            {
                var hp = t.localPosition;
                hp.x += speed * dt;
                if (hp.x > 13f) hp.x -= 26f;
                t.localPosition = hp;
            }
            UpdateLightning(dt);
            UpdatePlumes(dt, wind);
            UpdateParticles(dt, wind);
            UpdateEyes(dt);
            ambient.Update(dt, time, center, wind);
        }

        /// <summary>Now and then a flash far behind the peaks, sometimes a double one.</summary>
        void UpdateLightning(float dt)
        {
            flashT -= dt;
            if (flashT <= 0f)
            {
                flashA = Range(0.55f, 1f);
                flashT = R() < 0.35f ? Range(0.12f, 0.22f) : Range(7f, 14f);
                flashPeaks.transform.localPosition = new Vector3(Range(-9f, 1f), 2.4f, 0f);
                // the storm group sits at x = -5.5: move the sky glow inside it to the same spot
                flashSky.transform.localPosition = new Vector3(flashPeaks.transform.localPosition.x + 5.5f, 0f, 0f);
            }
            flashA = Mathf.MoveTowards(flashA, 0f, dt * 4.5f);
            float a = flashA * flashA;
            flashSky.color = new Color(0.6f, 0.8f, 1f, 0.14f * a);
            flashPeaks.color = new Color(0.65f, 0.85f, 1f, 0.3f * a);
        }

        void UpdatePlumes(float dt, float wind)
        {
            foreach (var p in plumes)
            {
                foreach (var f in p.Puffs)
                {
                    f.Age += dt;
                    if (f.Age >= f.Life)
                    {
                        f.Age -= f.Life;
                        f.Life = Range(5f, 7.5f);
                        f.Size = p.Scale * Range(0.45f, 0.6f);
                        f.Drift = Range(0.8f, 1.2f);
                    }
                    float u = f.Age / f.Life;
                    // rises, slows, spreads and bends away with the wind
                    float rise = p.Scale * (1.6f * u - 0.45f * u * u);
                    float bend = p.Scale * (0.9f + wind * 0.8f) * u * u * f.Drift;
                    Vector2 pos = p.Source + new Vector2(bend + Mathf.Sin(u * 5f + f.Spin) * 0.06f * p.Scale, rise);
                    f.Sr.transform.localPosition = pos;
                    float s = f.Size * (0.5f + 1.9f * u);
                    f.Sr.transform.localScale = new Vector3(s, s, 1f);
                    f.Sr.transform.localRotation = Quaternion.Euler(0f, 0f, f.Spin * time + f.Spin * 10f);
                    float a = MathUtil.Bump(Mathf.Clamp01(u * 1.25f)) * 0.5f;
                    // lit warm by the fire at its foot, a dark column against the moonlit rock higher up
                    Color col = Color.Lerp(new Color(0.45f, 0.26f, 0.16f), new Color(0.1f, 0.13f, 0.16f), S01(u * 2.5f));
                    f.Sr.color = new Color(col.r * (0.8f + 0.8f * p.Light), col.g * (0.8f + 0.8f * p.Light), col.b * (0.8f + 0.8f * p.Light), a);
                }
            }
        }

        static float S01(float v) => MathUtil.Smooth01(v);

        void UpdateParticles(float dt, float wind)
        {
            // embers: born at a fire, rising in a wobbling line, winking out
            foreach (var m in embers)
            {
                m.Age += dt;
                if (m.Age >= m.Life)
                {
                    bool fromFire = emberSources.Count > 0 && R() < 0.7f;
                    Vector2 src = fromFire ? emberSources[rng.Next(emberSources.Count)] : new Vector2(Range(-10f, 10f), Range(-4.6f, -3f));
                    m.Pos = src + new Vector2(Range(-0.25f, 0.25f), Range(0f, 0.1f));
                    m.Vel = new Vector2(Range(0.05f, 0.3f), Range(0.25f, 0.6f)) * (m.Kind == 1 ? 1.8f : 1f);
                    m.Life = Range(2.5f, 5f);
                    m.Age = 0f;
                    m.Size = m.Kind == 1 ? Range(0.07f, 0.12f) : Range(0.025f, 0.05f);
                    m.Phase = R() * 10f;
                    if (m.Kind == 1) m.Pos = new Vector2(Range(-9f, 9f), Range(-5.2f, -4.2f));
                }
                m.Vel.x += (wind * 0.3f + Mathf.Sin(time * 2.3f + m.Phase) * 0.35f) * dt;
                m.Pos += m.Vel * dt;
                float u = m.Age / m.Life;
                float flick = 0.65f + 0.35f * Mathf.Sin(time * 13f + m.Phase * 7f);
                m.Sr.transform.localPosition = m.Pos - lean * (m.Kind == 1 ? 0.7f : 0.2f);
                m.Sr.transform.localScale = Vector3.one * m.Size * (1f - 0.5f * u);
                m.Sr.color = Color.Lerp(Palette.Lantern, Palette.BlastOrange, u).WithAlpha(MathUtil.Bump(u) * flick * (m.Kind == 1 ? 0.45f : 0.9f));
            }

            // ash: drifting down and sideways, tumbling
            foreach (var m in ash)
            {
                m.Pos += (m.Vel + new Vector2(wind * 0.2f + Mathf.Sin(time * 0.9f + m.Phase) * 0.08f, Mathf.Sin(time * 1.7f + m.Phase) * 0.05f)) * dt;
                if (m.Pos.y < -5.4f || m.Pos.x > 11.5f) Respawn(m, false);
                m.Sr.transform.localPosition = m.Pos - lean * (m.Kind == 1 ? 0.8f : 0.3f);
                float flat = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(time * 2f + m.Phase));
                m.Sr.transform.localScale = new Vector3(m.Size, m.Size * flat, 1f);
                m.Sr.color = new Color(0.7f, 0.8f, 0.85f, m.Kind == 1 ? 0.22f : 0.45f);
            }

            // crystal motes rising out of the rift and the cracks in the plateau
            int waste = MenuScenery.RiftGlows.Count;
            foreach (var m in motes)
            {
                m.Age += dt;
                if (m.Age >= m.Life)
                {
                    if (moteSources.Count == 0) continue;
                    int k = rng.Next(moteSources.Count);
                    Vector2 src = moteSources[k];
                    if (k >= waste) src.y += groundY;   // plateau cracks are ground-local
                    m.Pos = src + new Vector2(Range(-0.2f, 0.2f), 0f);
                    m.Vel = new Vector2(Range(-0.05f, 0.05f), Range(0.2f, 0.45f));
                    m.Life = Range(2.5f, 4.5f);
                    m.Age = 0f;
                    m.Size = Range(0.04f, 0.09f) * (k >= waste ? 1.4f : 1f);
                    m.Phase = R() * 10f;
                }
                m.Pos += new Vector2(m.Vel.x + Mathf.Sin(time * 1.3f + m.Phase) * 0.12f, m.Vel.y) * dt;
                float u = m.Age / m.Life;
                m.Sr.transform.localPosition = m.Pos - lean * 0.25f;
                m.Sr.transform.localScale = Vector3.one * m.Size * (1f - u * 0.5f);
                m.Sr.color = Palette.Crystal.WithAlpha(MathUtil.Bump(u) * 0.75f);
            }
        }

        void UpdateEyes(float dt)
        {
            foreach (var e in eyes)
            {
                e.Timer -= dt;
                if (e.Timer <= 0f)
                {
                    if (e.Target > 0.5f)
                    {
                        // duck away for a while
                        e.Target = 0f;
                        e.Timer = Range(3f, 7f);
                    }
                    else
                    {
                        // and turn up somewhere else
                        int next = e.Spot;
                        for (int k = 0; k < 4 && (next == e.Spot || SpotTaken(next, e)); k++) next = rng.Next(EyeSpots.Length);
                        if (!SpotTaken(next, e)) e.Spot = next;
                        e.Target = 1f;
                        e.Timer = Range(4f, 9f);
                    }
                }
                e.T.localPosition = EyeSpots[e.Spot] - lean * 0.2f;
                e.Open = Mathf.MoveTowards(e.Open, e.Target, dt * (e.Target > e.Open ? 1.2f : 3f));
                e.Blink -= dt;
                float lid = 1f;
                if (e.Blink < 0f)
                {
                    lid = Mathf.Abs(e.Blink + 0.07f) / 0.07f;
                    if (e.Blink < -0.14f) e.Blink = Range(1.5f, 4.5f);
                }
                float open = MathUtil.Smooth01(e.Open);
                var sc = new Vector3(0.05f, 0.032f * Mathf.Clamp01(lid) * open + 0.001f, 1f);
                e.L.localScale = e.R.localScale = sc;
                // they glance around a little
                float look = Mathf.Sin(time * 0.7f + e.Spot) * 0.015f;
                e.L.localPosition = new Vector3(-0.06f + look, 0f, 0f);
                e.R.localPosition = new Vector3(0.06f + look, 0f, 0f);
                e.GlowL.color = e.GlowR.color = Palette.MonsterGlow.WithAlpha(0.3f * open);
            }
        }

        bool SpotTaken(int spot, Eyes self)
        {
            foreach (var e in eyes) if (e != null && e != self && e.Spot == spot) return true;
            return false;
        }

        /// <summary>Keeps the plateau's surface right under the player's boots (world position of the feet).</summary>
        public void PlaceGround(Vector2 feetWorld)
        {
            if (!built) return;
            Vector3 local = root.InverseTransformPoint(new Vector3(feetWorld.x, feetWorld.y, 0f));
            // the boots stand a little in front of the plateau's back edge
            groundY = local.y + MenuScenery.GroundBand * 0.3f;
            ground.localPosition = new Vector3(0f, groundY, 0f);
        }
    }
}
