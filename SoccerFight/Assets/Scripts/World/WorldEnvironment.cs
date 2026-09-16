using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Assembles the world: eight parallax depth layers — far snowy peaks, mountains
    /// with a waterfall, a forest hill with a ruined stadium, big trees, an aqueduct, the arcade ruins,
    /// near columns and trunks, bushes — each moving with the camera by its distance and getting
    /// darker the further back it lies, mist pooling between them, hundreds of wind-animated plants,
    /// ambient life, the marked pitch and the platforms. Drives the global wind / interaction inputs.
    /// </summary>
    public sealed class WorldEnvironment
    {
        struct Layer { public Transform t; public Vector2 basePos; public float px, py; }
        struct Fog { public SpriteRenderer sr; public float speed, offset, baseX; }
        struct Firefly { public SpriteRenderer sr; public Vector2 home; public float phase, fx, fy, ax, ay, parallax; }
        struct Blink { public SpriteRenderer sr; public Color color; public float baseA, phase, speed; }
        struct Puddle { public SpriteRenderer shine; public float x, y, halfW, phase, stir; }

        readonly List<Layer> layers = new List<Layer>();
        readonly List<Fog> fogs = new List<Fog>();
        readonly List<Firefly> fireflies = new List<Firefly>();
        readonly List<SpriteRenderer> rays = new List<SpriteRenderer>();
        readonly List<float> rayBaseAlpha = new List<float>();
        readonly List<float> rayBaseRot = new List<float>();
        readonly List<Blink> blinks = new List<Blink>();
        readonly List<Puddle> puddles = new List<Puddle>();
        readonly Ambient ambient = new Ambient();

        Transform root;
        CameraRig cam;
        System.Random rng;
        float moteTimer, splashTimer;

        public Transform Root => root;
        public PlatformViews Platforms { get; private set; }
        public Transform LeftPortal { get; private set; }
        public Transform RightPortal { get; private set; }
        public float Wind { get; private set; }

        public const float GoalX = 16.9f;

        // depth of each backdrop layer: 0 = the play plane, 1 = the farthest peaks
        const float DNear = 0.1f, DColonnade = 0.2f, DRuins = 0.32f, DAqueduct = 0.46f, DTrees = 0.58f, DForest = 0.72f, DFar = 0.86f, DPeaks = 1f;

        static readonly int WindId = Shader.PropertyToID("_SF_Wind");
        static readonly int Push0Id = Shader.PropertyToID("_SF_Push0");
        static readonly int Push1Id = Shader.PropertyToID("_SF_Push1");

        float R() => (float)rng.NextDouble();
        float Range(float a, float b) => a + (b - a) * R();
        T Pick<T>(T[] arr) => arr[rng.Next(arr.Length)];

        /// <summary>Night falls off with distance: every layer is darker and a little bluer the further back it lies.</summary>
        public static Color DepthTint(float depth)
        {
            float b = Mathf.Lerp(1f, 0.42f, depth);
            return new Color(b * Mathf.Lerp(1f, 0.86f, depth), b * Mathf.Lerp(1f, 0.94f, depth), b, 1f);
        }

        static Color Shade(Color c, Color tint) => new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);

        Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            return go.transform;
        }

        /// <summary>px/py: how much of the camera motion the layer follows (1 = infinitely far, 0 = play plane).</summary>
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
            BuildPeaks();
            BuildFar();
            BuildFarForest();
            BuildTrees();
            BuildAqueduct();
            BuildRuins();
            BuildColonnade();
            BuildNear();
            BuildGround();
            BuildPlatforms();
            BuildForeground();
            BuildFireflies();
        }

        // ------------------------------------------------------------------ sky

        void BuildSky()
        {
            var sky = AddLayer("Sky", new Vector2(0f, -1.6f), 1f, 1f);
            Art.MakeSprite("Gradient", sky, EnvironmentArt.Sky, -1000).transform.localScale = new Vector3(240f, 1f, 1f);

            var stars = AddLayer("Stars", new Vector2(0f, 2.6f), 0.985f, 0.97f);
            Art.MakeSprite("Stars", stars, EnvironmentArt.Stars, -995, Art.SpriteAddMat, new Color(1, 1, 1, 0.85f)).transform.localScale = new Vector3(1.6f, 1f, 1f);

            var clouds = AddLayer("Clouds", Vector2.zero, 0.96f, 0.94f);
            var bats = AddLayer("Bats", Vector2.zero, 0.9f, 0.86f);
            ambient.BuildSky(stars, clouds, bats);

            var moon = AddLayer("Moon", new Vector2(5.4f, 6.6f), 0.975f, 0.96f);
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

        // ------------------------------------------------------------------ far peaks

        // The far layers stand on the horizon (eye level ≈ the camera's base height), each one peeking
        // out above the one in front of it: forest hill, then the mountains, then the snowy peaks.

        void BuildPeaks()
        {
            var l = AddLayer("Peaks", new Vector2(0f, 3.4f), 0.94f, 0.9f);
            Color tint = DepthTint(DPeaks);
            Art.MakeSprite("Range", l, DepthArt.Peaks, -950, Art.SpriteMat, tint);
            AddFog("Fog Peaks", -945, new Vector2(0f, 3.1f), 0.92f, 0.88f, 1.4f, 0.14f, 0.07f, DPeaks);
        }

        // ------------------------------------------------------------------ mountains + waterfall

        void BuildFar()
        {
            var far = AddLayer("Far", new Vector2(0f, 2.7f), 0.87f, 0.8f);
            Color tint = DepthTint(DFar);
            Art.MakeSprite("Mountains", far, EnvironmentArt.Far, -900, Art.SpriteMat, tint);
            ambient.BuildWaterfall(far, tint.b);
            AddStrip(far, Shade(Color.Lerp(Palette.FarBottom, Palette.Fog, 0.16f), tint), 0.1f, -901);
            AddFog("Fog Far", -880, new Vector2(0f, 2.75f), 0.85f, 0.78f, 1.5f, 0.13f, 0.12f, DFar);
        }

        // ------------------------------------------------------------------ forest hill, stadium ruin, bell tower

        void BuildFarForest()
        {
            var l = AddLayer("Far Forest", new Vector2(0f, 2.4f), 0.77f, 0.71f);
            Color tint = DepthTint(DForest);
            Art.MakeSprite("Forest", l, DepthArt.FarForest, -850, Art.SpriteMat, tint);
            foreach (var li in DepthArt.ForestLights)
            {
                bool flood = li.z > 0.8f;
                AddBlink(l, new Vector2(li.x, li.y), flood ? 0.3f : li.z * 0.5f, flood ? new Color(0.78f, 0.93f, 1f) : Palette.Lantern, flood ? 0.3f : 0.5f, -849);
                if (!flood) continue;
                // the last working floodlight still throws a pale cone over the ruined bowl
                var ray = Art.MakeSprite("Flood Beam", l, Art.LightRay, -848, Art.SpriteAddMat, new Color(0.7f, 0.9f, 1f, 0.07f));
                ray.transform.localPosition = new Vector2(li.x, li.y);
                ray.transform.localRotation = Quaternion.Euler(0f, 0f, -52f);
                ray.transform.localScale = new Vector3(0.42f, 0.4f, 1f);
            }
            AddFog("Fog Forest", -830, new Vector2(0f, 2.35f), 0.74f, 0.68f, 1.5f, 0.12f, -0.1f, DForest);
        }

        // ------------------------------------------------------------------ trees

        void BuildTrees()
        {
            // raised so the crowns frame the top of the view and leave the middle open for the far layers
            var mid = AddLayer("Trees", new Vector2(0f, 0.1f), 0.65f, 0.6f);
            Color tint = DepthTint(DTrees);
            Art.MakeSprite("Trunks", mid, EnvironmentArt.Mid, -800, Art.SpriteMat, tint);
            AddStrip(mid, Shade(Palette.MidBottom, tint), 0.35f, -801);

            var canopy = new FoliageLayer();
            Color leaf = new Color(0.92f, 1f, 1f, 1f);
            foreach (var c in EnvironmentArt.Canopies)
            {
                if (c.y < 7f && R() < 0.4f) continue;   // lighter crowns: some lower branches stay bare
                var v = Pick(FoliageArt.Canopies);
                float scale = c.z / Mathf.Max(0.1f, v.Units.width * 0.85f);
                canopy.Add(v, new Vector2(c.x, c.y), scale, leaf, 1f, 0f, R() > 0.5f);
            }
            foreach (var a in EnvironmentArt.MossAnchors)
                if (R() > 0.3f) canopy.Add(Pick(FoliageArt.Moss), a, Range(0.7f, 1.2f), leaf, 1f, 0f, R() > 0.5f);
            canopy.Build(mid, "Canopies", -799, FoliageLayer.MakeMaterial("SF Foliage Trees", 0.28f, 0.7f, false, 1f, tint));

            AddFog("Fog Mid", -780, new Vector2(0f, 0.6f), 0.6f, 0.55f, 2.2f, 0.14f, -0.18f, DTrees);
        }

        // ------------------------------------------------------------------ aqueduct

        void BuildAqueduct()
        {
            var l = AddLayer("Aqueduct", new Vector2(0f, -0.1f), 0.54f, 0.5f);
            Color tint = DepthTint(DAqueduct);
            Art.MakeSprite("Arches", l, DepthArt.Aqueduct, -740, Art.SpriteMat, tint);

            var plants = new FoliageLayer();
            foreach (var a in DepthArt.AqueductIvy)
                plants.Add(R() > 0.35f ? Pick(FoliageArt.Ivy) : Pick(FoliageArt.Moss), a, Range(0.5f, 0.85f), Color.white, 1f, 0f, R() > 0.5f);
            for (float x = -DepthArt.AqW * 0.5f + 0.5f; x < DepthArt.AqW * 0.5f - 0.5f; x += Range(0.4f, 1.1f))
            {
                float roll = R();
                var v = roll < 0.5f ? Pick(FoliageArt.Grass) : roll < 0.75f ? Pick(FoliageArt.TallGrass) : Pick(FoliageArt.Bushes);
                plants.Add(v, new Vector2(x, Range(0.05f, 0.25f)), roll >= 0.75f ? Range(0.45f, 0.7f) : Range(0.6f, 1f), Color.white, 1f, 0f, R() > 0.5f);
            }
            plants.Build(l, "Aqueduct Plants", -739, FoliageLayer.MakeMaterial("SF Foliage Aqueduct", 0.15f, 0.85f, false, 1f, tint));
            ambient.BuildAqueductFall(l, DepthArt.AqueductFallTop, DepthArt.AqueductFallLength, tint.b, -737);
            AddFog("Fog Aqueduct", -730, new Vector2(0f, 0.25f), 0.5f, 0.46f, 1.8f, 0.13f, 0.16f, DAqueduct);
        }

        // ------------------------------------------------------------------ ruins

        void BuildRuins()
        {
            var ruins = AddLayer("Ruins", new Vector2(0f, -0.4f), 0.42f, 0.38f);
            Color tint = DepthTint(DRuins);
            Art.MakeSprite("Arcade", ruins, EnvironmentArt.Ruins, -700, Art.SpriteMat, tint);
            AddStrip(ruins, Shade(Palette.RuinBottom * 1.1f, tint), 0.2f, -701);

            var plants = new FoliageLayer();
            Color leaf = new Color(0.9f, 0.97f, 1f, 1f);
            foreach (var a in EnvironmentArt.IvyAnchors)
                plants.Add(Pick(FoliageArt.Ivy), a, Range(0.7f, 1.25f), leaf, 1f, 0f, R() > 0.5f);
            for (int i = 0; i < EnvironmentArt.PierTops.Count; i++)
            {
                if (i % 3 != 1) continue;
                plants.Add(FoliageArt.Banners[i % FoliageArt.Banners.Length], EnvironmentArt.PierTops[i], Range(0.85f, 1.05f), leaf, 1f, 0f, false);
            }
            for (float x = -EnvironmentArt.RuinW * 0.5f + 0.5f; x < EnvironmentArt.RuinW * 0.5f - 0.5f; x += Range(0.35f, 0.9f))
            {
                float roll = R();
                bool bush = roll >= 0.8f;
                var v = roll < 0.45f ? Pick(FoliageArt.Grass) : roll < 0.65f ? Pick(FoliageArt.Ferns) : roll < 0.8f ? Pick(FoliageArt.TallGrass) : Pick(FoliageArt.Bushes);
                float s = bush ? Range(0.5f, 0.8f) : Range(0.7f, 1.1f);
                plants.Add(v, new Vector2(x, Range(0.02f, 0.18f)), s, leaf, 1f, 0f, R() > 0.5f);
            }
            plants.Build(ruins, "Ruin Plants", -699, FoliageLayer.MakeMaterial("SF Foliage Ruins", 0.16f, 0.9f, false, 1f, tint));
            ambient.BuildRuins(ruins);

            AddFog("Fog Low", -650, new Vector2(0f, -0.35f), 0.36f, 0.33f, 1.6f, 0.2f, 0.26f, DRuins);
        }

        // ------------------------------------------------------------------ near columns and trunks

        void BuildColonnade()
        {
            var l = AddLayer("Colonnade", new Vector2(0f, -0.5f), 0.28f, 0.26f);
            Color tint = DepthTint(DColonnade);
            var hang = new FoliageLayer();
            (float x, int kind, bool flip)[] pieces =
            {
                (-16.4f, 2, false), (-9.9f, 0, false), (-3.1f, 1, true), (4.1f, 2, true), (10.9f, 0, true), (17.4f, 1, false)
            };
            foreach (var (x, kind, flip) in pieces)
            {
                var sprite = kind == 0 ? DepthArt.ColumnTall : kind == 1 ? DepthArt.ColumnBroken : DepthArt.Trunk;
                var sr = Art.MakeSprite(kind == 2 ? "Trunk" : "Column", l, sprite, -620, Art.SpriteMat, tint);
                sr.transform.localPosition = new Vector3(x, 0f, 0f);
                sr.flipX = flip;
                float sx = flip ? -1f : 1f;
                var anchors = kind == 0 ? DepthArt.ColumnIvy : kind == 2 ? DepthArt.TrunkMoss : null;
                if (anchors == null) continue;
                foreach (var a in anchors)
                    hang.Add(kind == 2 || R() > 0.5f ? Pick(FoliageArt.Moss) : Pick(FoliageArt.Ivy), new Vector2(x + a.x * sx, a.y), Range(0.7f, 1.1f), Color.white, 1f, 0f, R() > 0.5f);
            }
            hang.Build(l, "Colonnade Moss", -619, FoliageLayer.MakeMaterial("SF Foliage Colonnade", 0.08f, 0.9f, false, 1f, tint));
        }

        // ------------------------------------------------------------------ near vegetation behind the pitch

        void BuildNear()
        {
            var near = AddLayer("Near", new Vector2(0f, -0.4f), 0.16f, 0.15f);
            Color tint = DepthTint(DNear);
            Art.MakeSprite("Bushes", near, EnvironmentArt.Bushes, -600, Art.SpriteMat, tint);

            var back = new FoliageLayer();
            var front = new FoliageLayer();
            float half = EnvironmentArt.BushW * 0.5f - 1f;
            Color deep = new Color(0.72f, 0.82f, 0.86f, 1f), full = Color.white;

            for (float x = -half; x < half; x += Range(0.9f, 1.9f))
                back.Add(Pick(FoliageArt.Bushes), new Vector2(x, Range(0.5f, 0.62f)), Range(0.7f, 1.1f), deep, 1f, 0f, R() > 0.5f);
            for (float x = -half; x < half; x += Range(0.6f, 1.6f))
            {
                var v = R() > 0.7f ? Pick(FoliageArt.Reeds) : Pick(FoliageArt.TallGrass);
                back.Add(v, new Vector2(x, Range(0.5f, 0.6f)), Range(0.8f, 1.2f), deep, 1f, 0f, R() > 0.5f);
            }
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
            back.Build(near, "Near Back", -595, FoliageLayer.MakeMaterial("SF Foliage Near Back", 0.06f, 0.95f, false, 1f, tint));
            front.Build(near, "Near Front", -590, FoliageLayer.MakeMaterial("SF Foliage Near", 0f, 1f, false, 1f, tint),
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

            // chalk: halfway line, centre circle, both penalty areas with the goal mouths worn to mud
            Art.MakeSprite("Markings Centre", ground, DepthArt.MarkCenter, -99);
            Art.MakeSprite("Markings Left", ground, DepthArt.MarkLeft, -99).transform.localPosition = new Vector3(-DepthArt.MarkBoxCenter, 0f, 0f);
            Art.MakeSprite("Markings Right", ground, DepthArt.MarkRight, -99).transform.localPosition = new Vector3(DepthArt.MarkBoxCenter, 0f, 0f);

            // rain puddles that mirror the moon and splash when someone runs through
            float[] puddleX = { -7.4f, 7.1f, -12.6f };
            for (int i = 0; i < puddleX.Length; i++)
            {
                float y = 0.06f + 0.05f * (i % 2);
                var p = Art.MakeSprite("Puddle", ground, DepthArt.Puddle, -98);
                float s = i == 2 ? 0.75f : 1f;
                p.transform.localPosition = new Vector3(puddleX[i], y, 0f);
                p.transform.localScale = new Vector3(s, 1f, 1f);
                var shine = Art.MakeSprite("Shine", ground, Art.SoftGlow, -97, Art.SpriteAddMat, new Color(0.7f, 0.92f, 1f, 0f));
                shine.transform.localPosition = new Vector3(puddleX[i] + 0.18f * s, y, 0f);
                shine.transform.localScale = new Vector3(0.9f * s, 0.1f, 1f);
                puddles.Add(new Puddle { shine = shine, x = puddleX[i], y = y, halfW = 0.7f * s, phase = R() * 10f });
            }

            // stones and broken drums lying along the far edge of the pitch
            for (int i = 0; i < 12; i++)
            {
                var st = Art.MakeSprite("Stone", ground, DepthArt.Pebbles[i % DepthArt.Pebbles.Length], -96, Art.SpriteMat, new Color(0.85f, 0.92f, 0.95f, 1f));
                st.transform.localPosition = new Vector3(Range(-25f, 25f), Range(0.27f, 0.34f), 0f);
                float s = Range(0.7f, 1.5f);
                st.transform.localScale = new Vector3(R() > 0.5f ? s : -s, s, 1f);
                st.transform.localRotation = Quaternion.Euler(0f, 0f, Range(-15f, 15f));
            }

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

        // ------------------------------------------------------------------ platforms (world locked, walkable; rebuilt per stage)

        void BuildPlatforms()
        {
            Platforms = new PlatformViews();
            Platforms.Build(root);
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

        // ------------------------------------------------------------------ fireflies, fog, distant lights

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

        /// <summary>Mist band between two depth layers; it is dimmer the further back it drifts.</summary>
        void AddFog(string name, int order, Vector2 basePos, float px, float py, float height, float alpha, float speed, float depth)
        {
            var g = AddLayer(name, basePos, px, py);
            Color c = Shade(Palette.Fog, DepthTint(depth * 0.85f)).WithAlpha(alpha);
            var sr = Art.MakeSprite(name, g, EnvironmentArt.FogBand, order, Art.SpriteMat, c);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(64f, 2f);
            sr.transform.localScale = new Vector3(1f, height / 2f, 1f);
            sr.transform.localPosition = new Vector3(-32f, 0f, 0f);
            fogs.Add(new Fog { sr = sr, speed = speed, offset = (float)rng.NextDouble() * 8f, baseX = -32f });
        }

        /// <summary>Solid ground under a layer so vertical parallax never opens a gap below it.</summary>
        void AddStrip(Transform layer, Color color, float top, int order)
        {
            var sr = Art.MakeSprite("Ground Strip", layer, DepthArt.GroundStrip, order, Art.SpriteMat, color);
            sr.transform.localPosition = new Vector3(0f, top, 0f);
            sr.transform.localScale = new Vector3(80f, 1f, 1f);
        }

        void AddBlink(Transform parent, Vector2 pos, float size, Color color, float alpha, int order)
        {
            var sr = Art.MakeSprite("Light", parent, Art.SoftGlow, order, Art.SpriteGlowMat, color.WithAlpha(alpha));
            sr.transform.localPosition = pos;
            sr.transform.localScale = Vector3.one * size;
            blinks.Add(new Blink { sr = sr, color = color, baseA = alpha, phase = R() * 20f, speed = Range(1.5f, 3.5f) });
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

            // plants bend around the player and the ball — on the pitch and on the platforms
            if (player != null)
            {
                float speed01 = Mathf.Clamp01(Mathf.Abs(player.Vel.x) / Player.MaxSpeed);
                float onGround = Mathf.Clamp01(1f - (player.Pos.y - player.GroundY) / 1.2f);
                Shader.SetGlobalVector(Push0Id, new Vector4(player.Pos.x, player.Pos.y + 0.35f, 0.9f, (0.22f + 0.24f * speed01) * onGround));
            }
            if (ball != null)
            {
                float floor = Level.FloorBelow(ball.Pos.x, ball.Pos.y - Art.BallRadius + 0.05f);
                float low = Mathf.Clamp01(1f - (ball.Pos.y - Art.BallRadius - floor) / 0.8f);
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

            // distant windows, the floodlight and the crystals flicker softly
            for (int i = 0; i < blinks.Count; i++)
            {
                var b = blinks[i];
                float f = 0.72f + 0.28f * Mathf.PerlinNoise(time * b.speed, b.phase);
                b.sr.color = b.color.WithAlpha(b.baseA * f);
            }

            Platforms.Update(dt, time, Wind);

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

            var fx = FxSystem.I;
            UpdatePuddles(dt, time, player, fx);

            // floating pollen / dust motes catching the moonlight
            moteTimer -= dt;
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

        void UpdatePuddles(float dt, float time, Player player, FxSystem fx)
        {
            splashTimer -= dt;
            for (int i = 0; i < puddles.Count; i++)
            {
                var p = puddles[i];
                bool wading = player != null && player.Grounded && player.OnPlatform == Level.None
                              && Mathf.Abs(player.Pos.x - p.x) < p.halfW && Mathf.Abs(player.Vel.x) > 1.5f;
                if (wading && splashTimer <= 0f && fx != null)
                {
                    splashTimer = 0.09f;
                    p.stir = 1f;
                    Vector2 at = new Vector2(player.Pos.x, p.y);
                    Color w = new Color(0.62f, 0.86f, 0.95f, 0.85f);
                    fx.Sparks(at, new Vector2(-Mathf.Sign(player.Vel.x) * 0.4f, 1f), 70f, 4, 1.8f, 3.4f, w, 1.4f, 0.025f, 0.32f, 9f);
                }
                p.stir = Mathf.Max(0f, p.stir - dt * 1.5f);
                float a = 0.3f + 0.12f * Mathf.Sin(time * 0.9f + p.phase) + 0.25f * p.stir * (0.5f + 0.5f * Mathf.Sin(time * 22f));
                p.shine.color = new Color(0.7f, 0.92f, 1f, a);
                puddles[i] = p;
            }
        }
    }
}
