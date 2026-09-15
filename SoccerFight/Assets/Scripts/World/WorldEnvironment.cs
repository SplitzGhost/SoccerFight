using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Assembles the backdrop: parallax layers, hundreds of wind-animated plants per depth layer,
    /// fog, light rays, fireflies and ambient life. Drives the global wind / interaction shader inputs.
    /// </summary>
    public sealed class WorldEnvironment
    {
        struct Layer { public Transform t; public Vector2 basePos; public float px, py; }
        struct Fog { public SpriteRenderer sr; public float speed, offset, baseX; }
        struct Firefly { public SpriteRenderer sr; public Vector2 home; public float phase, fx, fy, ax, ay, parallax; }

        readonly List<Layer> layers = new List<Layer>();
        readonly List<Fog> fogs = new List<Fog>();
        readonly List<Firefly> fireflies = new List<Firefly>();
        readonly List<SpriteRenderer> rays = new List<SpriteRenderer>();
        readonly List<float> rayBaseAlpha = new List<float>();
        readonly List<float> rayBaseRot = new List<float>();
        readonly Ambient ambient = new Ambient();

        Transform root;
        CameraRig cam;
        System.Random rng;
        float moteTimer;

        public Transform LeftPortal { get; private set; }
        public Transform RightPortal { get; private set; }
        public float Wind { get; private set; }

        public const float GoalX = 16.9f;

        static readonly int WindId = Shader.PropertyToID("_SF_Wind");
        static readonly int Push0Id = Shader.PropertyToID("_SF_Push0");
        static readonly int Push1Id = Shader.PropertyToID("_SF_Push1");

        float R() => (float)rng.NextDouble();
        float Range(float a, float b) => a + (b - a) * R();
        T Pick<T>(T[] arr) => arr[rng.Next(arr.Length)];

        Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            return go.transform;
        }

        Transform AddLayer(string name, Vector2 basePos, float px, float py)
        {
            var t = Group(name);
            layers.Add(new Layer { t = t, basePos = basePos, px = px, py = py });
            return t;
        }

        /// <summary>Requires EnvironmentArt.Begin/End to have run.</summary>
        public void Build(Transform parent, CameraRig cameraRig)
        {
            cam = cameraRig;
            rng = new System.Random(1234);
            root = new GameObject("Environment").transform;
            root.SetParent(parent, false);

            BuildSky();
            BuildFar();
            BuildTrees();
            BuildRuins();
            BuildNear();
            BuildGround();
            BuildForeground();
            BuildFireflies();
        }

        // ------------------------------------------------------------------ sky

        void BuildSky()
        {
            var sky = AddLayer("Sky", new Vector2(0f, -1.6f), 1f, 1f);
            Art.MakeSprite("Gradient", sky, EnvironmentArt.Sky, -1000).transform.localScale = new Vector3(240f, 1f, 1f);

            var stars = AddLayer("Stars", new Vector2(0f, 2.6f), 0.97f, 0.9f);
            Art.MakeSprite("Stars", stars, EnvironmentArt.Stars, -995, Art.SpriteAddMat, new Color(1, 1, 1, 0.85f)).transform.localScale = new Vector3(1.6f, 1f, 1f);

            var clouds = AddLayer("Clouds", Vector2.zero, 0.95f, 0.9f);
            var bats = AddLayer("Bats", Vector2.zero, 0.88f, 0.8f);
            ambient.BuildSky(stars, clouds, bats);

            var moon = AddLayer("Moon", new Vector2(5.4f, 6.7f), 0.93f, 0.85f);
            Art.MakeSprite("Halo Wide", moon, Art.SoftGlow, -985, Art.SpriteAddMat, Palette.MoonGlow.WithAlpha(0.13f)).transform.localScale = Vector3.one * 18f;
            Art.MakeSprite("Halo", moon, Art.SoftGlow, -984, Art.SpriteAddMat, Palette.MoonGlow.WithAlpha(0.36f)).transform.localScale = Vector3.one * 6.5f;
            var disc = Art.MakeSprite("Disc", moon, EnvironmentArt.Moon, -983, Art.SpriteEmissiveMat, Color.white);
            disc.sharedMaterial = Art.MakeSpriteMaterial("SF Moon", 1.35f, false);
            disc.transform.localScale = Vector3.one * 1.05f;
            Art.MakeSprite("Rim", moon, Art.Ring, -982, Art.SpriteGlowMat, Palette.MoonGlow.WithAlpha(0.12f)).transform.localScale = Vector3.one * 1.28f;

            float[] rayAngles = { -10f, -19f, -27f, -35f, -43f, -15f, -31f };
            float[] rayWidths = { 1.9f, 3.0f, 1.4f, 2.6f, 1.7f, 0.9f, 0.7f };
            for (int i = 0; i < rayAngles.Length; i++)
            {
                var r = Art.MakeSprite("Ray" + i, moon, Art.LightRay, -760, Art.SpriteAddMat, Palette.MoonGlow.WithAlpha(0f));
                r.transform.localRotation = Quaternion.Euler(0f, 0f, rayAngles[i]);
                r.transform.localScale = new Vector3(rayWidths[i], 1.8f, 1f);
                rays.Add(r);
                rayBaseAlpha.Add(0.075f + 0.035f * (i % 3));
                rayBaseRot.Add(rayAngles[i]);
            }
        }

        // ------------------------------------------------------------------ far mountains + waterfall

        void BuildFar()
        {
            var far = AddLayer("Far", new Vector2(0f, 0.1f), 0.86f, 0.5f);
            Art.MakeSprite("Mountains", far, EnvironmentArt.Far, -900);
            ambient.BuildWaterfall(far);
            AddFog("Fog Far", -880, new Vector2(0f, 0.7f), 0.8f, 0.5f, 2.6f, 0.2f, 0.12f);
        }

        // ------------------------------------------------------------------ trees

        void BuildTrees()
        {
            var mid = AddLayer("Trees", new Vector2(0f, -0.55f), 0.64f, 0.4f);
            Art.MakeSprite("Trunks", mid, EnvironmentArt.Mid, -800);

            var canopy = new FoliageLayer();
            Color tint = new Color(0.92f, 1f, 1f, 1f);
            foreach (var c in EnvironmentArt.Canopies)
            {
                var v = Pick(FoliageArt.Canopies);
                float scale = c.z / Mathf.Max(0.1f, v.Units.width * 0.85f);
                canopy.Add(v, new Vector2(c.x, c.y), scale, tint, 1f, 0f, R() > 0.5f);
            }
            foreach (var a in EnvironmentArt.MossAnchors)
                if (R() > 0.3f) canopy.Add(Pick(FoliageArt.Moss), a, Range(0.7f, 1.2f), tint, 1f, 0f, R() > 0.5f);
            canopy.Build(mid, "Canopies", -799, FoliageLayer.MakeMaterial("SF Foliage Trees", 0.32f, 0.7f));

            AddFog("Fog Mid", -780, new Vector2(0f, 0.0f), 0.56f, 0.35f, 2.2f, 0.2f, -0.18f);
        }

        // ------------------------------------------------------------------ ruins

        void BuildRuins()
        {
            var ruins = AddLayer("Ruins", new Vector2(0f, -0.4f), 0.42f, 0.3f);
            Art.MakeSprite("Arcade", ruins, EnvironmentArt.Ruins, -700);

            var plants = new FoliageLayer();
            Color tint = new Color(0.9f, 0.97f, 1f, 1f);
            foreach (var a in EnvironmentArt.IvyAnchors)
                plants.Add(Pick(FoliageArt.Ivy), a, Range(0.7f, 1.25f), tint, 1f, 0f, R() > 0.5f);
            for (int i = 0; i < EnvironmentArt.PierTops.Count; i++)
            {
                if (i % 3 != 1) continue;
                plants.Add(FoliageArt.Banners[i % FoliageArt.Banners.Length], EnvironmentArt.PierTops[i], Range(0.85f, 1.05f), tint, 1f, 0f, false);
            }
            for (float x = -EnvironmentArt.RuinW * 0.5f + 0.5f; x < EnvironmentArt.RuinW * 0.5f - 0.5f; x += Range(0.35f, 0.9f))
            {
                float roll = R();
                bool bush = roll >= 0.8f;
                var v = roll < 0.45f ? Pick(FoliageArt.Grass) : roll < 0.65f ? Pick(FoliageArt.Ferns) : roll < 0.8f ? Pick(FoliageArt.TallGrass) : Pick(FoliageArt.Bushes);
                float s = bush ? Range(0.5f, 0.8f) : Range(0.7f, 1.1f);
                plants.Add(v, new Vector2(x, Range(0.02f, 0.18f)), s, tint, 1f, 0f, R() > 0.5f);
            }
            plants.Build(ruins, "Ruin Plants", -699, FoliageLayer.MakeMaterial("SF Foliage Ruins", 0.2f, 0.9f));
            ambient.BuildRuins(ruins);

            AddFog("Fog Low", -650, new Vector2(0f, -0.35f), 0.3f, 0.2f, 1.6f, 0.24f, 0.26f);
        }

        // ------------------------------------------------------------------ near vegetation behind the pitch

        void BuildNear()
        {
            var near = AddLayer("Near", new Vector2(0f, -0.4f), 0.16f, 0.12f);
            Art.MakeSprite("Bushes", near, EnvironmentArt.Bushes, -600);

            var back = new FoliageLayer();
            var front = new FoliageLayer();
            float half = EnvironmentArt.BushW * 0.5f - 1f;
            Color deep = new Color(0.72f, 0.82f, 0.86f, 1f), full = Color.white;

            // back row: bushes, reeds and tall grass
            for (float x = -half; x < half; x += Range(0.9f, 1.9f))
                back.Add(Pick(FoliageArt.Bushes), new Vector2(x, Range(0.5f, 0.62f)), Range(0.7f, 1.1f), deep, 1f, 0f, R() > 0.5f);
            for (float x = -half; x < half; x += Range(0.6f, 1.6f))
            {
                var v = R() > 0.7f ? Pick(FoliageArt.Reeds) : Pick(FoliageArt.TallGrass);
                back.Add(v, new Vector2(x, Range(0.5f, 0.6f)), Range(0.8f, 1.2f), deep, 1f, 0f, R() > 0.5f);
            }
            // front row: ferns, flowers, mushrooms, grass
            for (float x = -half; x < half; x += Range(0.25f, 0.7f))
            {
                float roll = R();
                FoliageArt.Variant v;
                float s;
                if (roll < 0.22f) { v = Pick(FoliageArt.Ferns); s = Range(0.7f, 1.05f); }
                else if (roll < 0.36f) { v = Pick(FoliageArt.Flowers); s = Range(0.75f, 1.1f); }
                else if (roll < 0.46f) { v = Pick(FoliageArt.Mushrooms); s = Range(0.8f, 1.3f); }
                else { v = Pick(FoliageArt.Grass); s = Range(0.8f, 1.3f); }
                front.Add(v, new Vector2(x, Range(0.44f, 0.56f)), s, full, 1f, 0f, R() > 0.5f);
            }
            back.Build(near, "Near Back", -595, FoliageLayer.MakeMaterial("SF Foliage Near Back", 0.06f, 0.95f));
            front.Build(near, "Near Front", -590, FoliageLayer.MakeMaterial("SF Foliage Near", 0f, 1f),
                FoliageLayer.MakeMaterial("SF Foliage Near Glow", 0f, 1f, true, 0.9f), -589);
        }

        // ------------------------------------------------------------------ pitch, earth, goals (world locked)

        void BuildGround()
        {
            var ground = Group("Ground");
            var pitch = Art.MakeSprite("Pitch", ground, EnvironmentArt.PitchTile, -100);
            pitch.drawMode = SpriteDrawMode.Tiled;
            pitch.size = new Vector2(52f, EnvironmentArt.PitchH);
            pitch.transform.localPosition = new Vector3(-26f, EnvironmentArt.PitchTop - EnvironmentArt.PitchH, 0f);
            var earth = Art.MakeSprite("Earth", ground, EnvironmentArt.EarthTile, -110);
            earth.drawMode = SpriteDrawMode.Tiled;
            earth.size = new Vector2(52f, EnvironmentArt.EarthH);
            earth.transform.localPosition = new Vector3(-26f, EnvironmentArt.PitchTop - EnvironmentArt.PitchH + 0.13f - EnvironmentArt.EarthH, 0f);

            // interactive grass along the back edge of the pitch (behind the players)
            var backEdge = new FoliageLayer();
            for (float x = -26f; x < 26f; x += Range(0.1f, 0.24f))
            {
                float roll = R();
                bool flower = roll >= 0.95f;
                var v = roll < 0.86f ? Pick(FoliageArt.Grass) : roll < 0.95f ? Pick(FoliageArt.Clover) : Pick(FoliageArt.Flowers);
                float s = flower ? Range(0.45f, 0.6f) : Range(0.55f, 1.0f);
                backEdge.Add(v, new Vector2(x, Range(0.27f, 0.35f)), s, Color.white, 1f, 1f, R() > 0.5f);
            }
            backEdge.Build(ground, "Pitch Back Grass", -95, FoliageLayer.MakeMaterial("SF Foliage Pitch", 0f, 1f),
                FoliageLayer.MakeMaterial("SF Foliage Pitch Glow", 0f, 1f, true, 0.8f), -94);

            // tufts on the front lip (in front of the players, below their feet)
            var lip = new FoliageLayer();
            for (float x = -26f; x < 26f; x += Range(0.14f, 0.36f))
            {
                var v = R() > 0.85f ? Pick(FoliageArt.Clover) : Pick(FoliageArt.Grass);
                lip.Add(v, new Vector2(x, Range(-0.47f, -0.41f)), Range(0.4f, 0.75f), new Color(0.95f, 1f, 1f, 1f), 1f, 0.7f, R() > 0.5f);
            }
            lip.Build(ground, "Pitch Lip Grass", 150, FoliageLayer.MakeMaterial("SF Foliage Lip", 0f, 1f));

            // crystals and stones embedded in the earth wall
            var earthDecor = new FoliageLayer();
            for (int i = 0; i < 26; i++)
                earthDecor.Add(Pick(FoliageArt.Stones), new Vector2(Range(-26f, 26f), Range(-2.9f, -0.75f)), Range(0.8f, 1.6f), new Color(0.75f, 0.85f, 0.88f, 1f));
            for (int i = 0; i < 9; i++)
                earthDecor.Add(Pick(FoliageArt.Crystals), new Vector2(Range(-26f, 26f), Range(-2.4f, -1.0f)), Range(1.0f, 1.5f), Color.white, 0f, 0f, R() > 0.5f, Range(-25f, 25f));
            earthDecor.Build(ground, "Earth Decor", -108, FoliageLayer.MakeMaterial("SF Foliage Earth", 0f, 0f),
                FoliageLayer.MakeMaterial("SF Foliage Earth Glow", 0f, 0f, true, 0.45f), -107);

            // a thin ground mist drifting over the back of the pitch
            var mist = Group("Ground Mist");
            var sr = Art.MakeSprite("Mist", mist, EnvironmentArt.FogBand, -93, Art.SpriteMat, Palette.Fog.WithAlpha(0.07f));
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(64f, 2f);
            sr.transform.localScale = new Vector3(1f, 0.45f, 1f);
            sr.transform.localPosition = new Vector3(-32f, -0.05f, 0f);
            fogs.Add(new Fog { sr = sr, speed = 0.3f, offset = 0f, baseX = -32f });

            LeftPortal = BuildGoal(ground, -GoalX, 1f);
            RightPortal = BuildGoal(ground, GoalX, -1f);
            ambient.BuildLeaves(root);
        }

        Transform BuildGoal(Transform parent, float x, float facing)
        {
            var g = new GameObject(facing > 0 ? "Goal Left" : "Goal Right").transform;
            g.SetParent(parent, false);
            g.localPosition = new Vector3(x, -0.02f, 0f);
            g.localScale = new Vector3(facing, 1f, 1f);
            Art.MakeSprite("Frame", g, EnvironmentArt.Goal, -60);

            var portal = new GameObject("Portal").transform;
            portal.SetParent(g, false);
            portal.localPosition = new Vector3(-0.95f, 1.2f, 0f);
            var glow = Art.MakeSprite("Glow", portal, Art.SoftGlow, -59, Art.SpriteGlowMat, Palette.MonsterGlow.WithAlpha(0f));
            glow.transform.localScale = new Vector3(2.4f, 3.6f, 1f);
            var swirl = Art.MakeSprite("Swirl", portal, MonsterArt.Portal, -58, Art.SpriteGlowMat, Palette.MonsterGlow.WithAlpha(0f));
            swirl.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            return portal;
        }

        // ------------------------------------------------------------------ foreground

        void BuildForeground()
        {
            var fg = AddLayer("Foreground", Vector2.zero, -0.35f, -0.2f);
            var fronds = new FoliageLayer();
            float[] xs = { -23f, -14f, -6.5f, 2.5f, 10.5f, 18f, 26f };
            foreach (float x in xs)
            {
                int n = 2 + rng.Next(2);
                for (int k = 0; k < n; k++)
                    fronds.Add(Pick(FoliageArt.FgLeaves), new Vector2(x + Range(-1.2f, 1.2f), Range(-3.15f, -2.85f)), Range(0.65f, 0.9f),
                        new Color(1f, 1f, 1f, 0.97f), 1f, 0f, R() > 0.5f, Range(-12f, 12f));
            }
            fronds.Build(fg, "Foreground Fronds", 600, FoliageLayer.MakeMaterial("SF Foliage Foreground", 0f, 0.5f));
        }

        // ------------------------------------------------------------------ fireflies & fog

        void BuildFireflies()
        {
            for (int i = 0; i < 42; i++)
            {
                bool front = i < 6;
                var sr = Art.MakeSprite("Firefly", root, Art.SoftGlow, front ? 610 : -640, Art.SpriteGlowMat, Palette.Firefly.WithAlpha(0f));
                float size = front ? Range(0.35f, 0.6f) : Range(0.1f, 0.22f);
                sr.transform.localScale = Vector3.one * size;
                bool nearPitch = i >= 34;
                fireflies.Add(new Firefly
                {
                    sr = sr,
                    home = new Vector2(Range(-24f, 24f), front ? Range(-1.5f, 2.5f) : nearPitch ? Range(0.4f, 1.8f) : Range(0.2f, 5.5f)),
                    phase = R() * 50f,
                    fx = Range(0.12f, 0.3f), fy = Range(0.15f, 0.35f),
                    ax = Range(0.6f, 1.8f), ay = Range(0.3f, 0.9f),
                    parallax = front ? -0.25f : nearPitch ? 0f : 0.3f
                });
            }
        }

        void AddFog(string name, int order, Vector2 basePos, float px, float py, float height, float alpha, float speed)
        {
            var g = AddLayer(name, basePos, px, py);
            var sr = Art.MakeSprite(name, g, EnvironmentArt.FogBand, order, Art.SpriteMat, Palette.Fog.WithAlpha(alpha));
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(64f, 2f);
            sr.transform.localScale = new Vector3(1f, height / 2f, 1f);
            sr.transform.localPosition = new Vector3(-32f, 0f, 0f);
            fogs.Add(new Fog { sr = sr, speed = speed, offset = (float)rng.NextDouble() * 8f, baseX = -32f });
        }

        // ------------------------------------------------------------------ update

        public void Update(float dt, float time, Player player, Ball ball)
        {
            Vector2 c = cam.Position;
            float dy = c.y - cam.BaseY;
            for (int i = 0; i < layers.Count; i++)
            {
                var l = layers[i];
                l.t.localPosition = new Vector3(l.basePos.x + c.x * l.px, l.basePos.y + dy * l.py, 0f);
            }

            // wind: slow lean changes + gusts that roll across the scene (shader does the rest)
            float lean = 0.04f + 0.12f * (Mathf.PerlinNoise(time * 0.06f, 0.3f) - 0.5f);
            float gust = 0.18f + 0.16f * Mathf.PerlinNoise(time * 0.21f, 4.1f);
            Shader.SetGlobalVector(WindId, new Vector4(lean, gust, 0.07f, time));
            Wind = lean * 2f + gust * Mathf.Sin(time * 0.85f) * 0.6f;

            if (player != null)
            {
                float speed01 = Mathf.Clamp01(Mathf.Abs(player.Vel.x) / Player.MaxSpeed);
                float onGround = Mathf.Clamp01(1f - player.Pos.y / 1.2f);
                Shader.SetGlobalVector(Push0Id, new Vector4(player.Pos.x, player.Pos.y + 0.35f, 0.9f, (0.22f + 0.24f * speed01) * onGround));
            }
            if (ball != null)
            {
                float low = Mathf.Clamp01(1f - (ball.Pos.y - Art.BallRadius) / 0.8f);
                Shader.SetGlobalVector(Push1Id, new Vector4(ball.Pos.x, ball.Pos.y, 0.55f, 0.3f * low));
            }

            for (int i = 0; i < fogs.Count; i++)
            {
                var f = fogs[i];
                f.offset = Mathf.Repeat(f.offset + f.speed * dt, 8f);
                var p = f.sr.transform.localPosition;
                f.sr.transform.localPosition = new Vector3(f.baseX + f.offset - 4f, p.y, 0f);
                fogs[i] = f;
            }

            for (int i = 0; i < rays.Count; i++)
            {
                float a = rayBaseAlpha[i] * (0.65f + 0.35f * Mathf.Sin(time * (0.35f + i * 0.07f) + i * 1.7f));
                rays[i].color = Palette.MoonGlow.WithAlpha(a);
                rays[i].transform.localRotation = Quaternion.Euler(0f, 0f, rayBaseRot[i] + Mathf.Sin(time * 0.13f + i) * 1.6f);
            }

            for (int i = 0; i < fireflies.Count; i++)
            {
                var f = fireflies[i];
                float t = time + f.phase;
                Vector2 pos = f.home + new Vector2(Mathf.Sin(t * f.fx * MathUtil.Tau) * f.ax + Mathf.Sin(t * 0.37f) * 0.3f,
                    Mathf.Sin(t * f.fy * MathUtil.Tau + 1.3f) * f.ay);
                pos.x += c.x * f.parallax;
                float blink = Mathf.Clamp01(0.35f + 0.65f * Mathf.Sin(t * 1.1f + f.phase * 3f)) * (0.6f + 0.4f * Mathf.PerlinNoise(t * 2f, f.phase));
                f.sr.transform.localPosition = pos;
                f.sr.color = Palette.Firefly.WithAlpha(blink * (f.parallax < 0f ? 0.35f : 0.9f));
            }

            // floating pollen / dust motes catching the moonlight
            moteTimer -= dt;
            var fx = FxSystem.I;
            while (moteTimer <= 0f && fx != null)
            {
                moteTimer += 0.12f;
                Rect view = cam.ViewRect;
                Vector2 p = new Vector2(view.xMin + Random.value * view.width, view.yMin + 1.5f + Random.value * (view.height - 1.5f));
                Color mc = new Color(0.8f, 0.95f, 1f, 0.55f);
                fx.Spawn(Random.value > 0.8f ? FxLayer.Front : FxLayer.Back, true, Art.CellDot, p,
                    new Vector2(Wind * 0.25f + (Random.value - 0.5f) * 0.12f, 0.05f + Random.value * 0.1f),
                    Random.Range(5f, 9f), Random.Range(0.025f, 0.05f), Random.Range(0.02f, 0.04f), mc, mc, 1.8f, 0f, -0.01f, 0f, 0f, false, true);
            }

            ambient.Update(dt, time, c, Wind);
        }
    }
}
