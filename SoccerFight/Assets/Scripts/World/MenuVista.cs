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
    /// The scene: a big moon rising in a valley between moonlit ranges, the ruined stadium below it,
    /// a stretch of pitch torn open by a chasm full of crystal light, the centre circle floating in
    /// the gap as the player's pedestal (MainMenu keeps it under the player's feet), the last
    /// floodlight leaning over it, floating rocks, a goal with a monster portal flickering in it,
    /// eyes in the dark, trees and lanterns framing the corners, fireflies, mist and leaves.
    /// Every layer leans with the pointer by its depth.
    /// </summary>
    public sealed class MenuVista
    {
        public const int Layer = 25;
        public static int Mask => 1 << Layer;

        /// <summary>The vista is composed for a view this tall (the camera's base size).</summary>
        const float ViewHalfH = 4.9f;
        /// <summary>Content reaches this far to each side before the view has to scale up.</summary>
        const float CoverHalfW = 10.4f;
        static readonly Vector2 MoonPos = new Vector2(0f, -0.45f);
        const float MoonRadius = 1.45f;

        sealed class Part { public Transform T; public Vector2 Home; public float Lean; }
        struct Blink { public SpriteRenderer Sr; public Color Color; public float Base, Phase, Speed, Dead; }
        sealed class Floater { public Transform T; public Vector2 Home; public float Phase, Amp, Speed, Tilt; }
        struct Firefly { public SpriteRenderer Sr; public Vector2 Home; public float Phase, Fx, Fy, Ax, Ay, Alpha; }
        sealed class Speck { public SpriteRenderer Sr; public Vector2 Pos; public float Age, Life, Speed, Size, Phase; }
        struct Fog { public SpriteRenderer Sr; public float Speed, Offset; }
        sealed class Eyes { public Transform T; public Transform L, R; public SpriteRenderer GlowL, GlowR; public int Spot; public float Timer, Open, Target, Blink; }

        readonly List<Part> parts = new List<Part>();
        readonly List<Blink> blinks = new List<Blink>();
        readonly List<Floater> floaters = new List<Floater>();
        readonly List<Firefly> fireflies = new List<Firefly>();
        readonly List<Speck> specks = new List<Speck>();
        readonly List<Fog> fogs = new List<Fog>();
        readonly List<SpriteRenderer> rays = new List<SpriteRenderer>();
        readonly List<float> rayAlpha = new List<float>();
        readonly Ambient ambient = new Ambient();
        readonly Eyes[] eyes = new Eyes[2];

        static readonly Vector2[] EyeSpots =
        {
            new Vector2(-4.55f, -2.05f), new Vector2(-8.05f, -4.35f), new Vector2(8.25f, -4.15f), new Vector2(3.95f, -2.1f),
        };

        Transform root, pedestal, portal, mastHead;
        SpriteRenderer portalGlow, portalSwirl, spot, flicker;
        readonly List<SpriteRenderer> cones = new List<SpriteRenderer>();
        System.Random rng;
        Vector2 lean, drift;
        float time, speckTimer, flickerT;
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

        static Transform Child(string name, Transform parent, Vector2 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
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

        // ------------------------------------------------------------------ build

        public void Build(Transform parent)
        {
            root = new GameObject("Menu Vista").transform;
            root.SetParent(parent, false);
            rng = new System.Random(4242);
            if (MenuScenery.Moon == null || EnvironmentArt.Sky == null) return;

            BuildSky();
            BuildValley();
            BuildIslands();
            BuildMast();
            BuildGround();
            BuildPedestal();
            BuildFrame();
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
            var twinkles = Child("Twinkles", stars, new Vector2(0f, -1.2f));
            var clouds = Group("Clouds", new Vector2(0f, -3.9f), 0.04f);
            var bats = Group("Bats", Vector2.zero, 0.05f);
            ambient.BuildSky(twinkles, clouds, bats);

            // the moon rises in the valley, right behind the player
            var moon = Group("Moon", MoonPos, 0.03f);
            Art.MakeSprite("Halo Wide", moon, Art.SoftGlow, -986, Art.SpriteAddMat, Palette.MoonGlow.WithAlpha(0.16f)).transform.localScale = Vector3.one * 22f;
            Art.MakeSprite("Halo", moon, Art.SoftGlow, -985, Art.SpriteAddMat, Palette.MoonGlow.WithAlpha(0.34f)).transform.localScale = Vector3.one * 9f;
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
                rayAlpha.Add(0.05f + 0.025f * (i % 3));
            }
        }

        void BuildValley()
        {
            // far ranges opening into the valley
            var valley = Group("Valley", new Vector2(0f, -1.25f), 0.08f);
            Art.MakeSprite("Ranges", valley, MenuScenery.Valley, -950, Art.SpriteMat, WorldEnvironment.DepthTint(0.92f));
            AddFog(valley, 0.05f, 1.4f, 0.16f, 0.07f, 0.95f, -945);

            // the mountains with the waterfall, flattened into the distance
            var far = Group("Far", new Vector2(4.3f, -2.05f), 0.12f);
            far.localScale = new Vector3(1f, 0.55f, 1f);
            Color farTint = WorldEnvironment.DepthTint(0.84f);
            Art.MakeSprite("Mountains", far, EnvironmentArt.Far, -900, Art.SpriteMat, farTint);
            ambient.BuildWaterfall(far, farTint.b);
            AddFog(far, 0.35f, 2.4f, 0.13f, 0.12f, 0.84f, -880);

            // the forest hill with the ruined stadium straight below the moon
            var forest = Group("Forest", new Vector2(6.4f, -2.3f), 0.17f);
            Color forestTint = WorldEnvironment.DepthTint(0.7f);
            Art.MakeSprite("Forest", forest, DepthArt.FarForest, -850, Art.SpriteMat, forestTint);
            foreach (var li in DepthArt.ForestLights)
            {
                bool flood = li.z > 0.8f;
                Glow("Light", forest, new Vector2(li.x, li.y), flood ? 0.34f : li.z * 0.5f, flood ? new Color(0.78f, 0.93f, 1f) : Palette.Lantern, flood ? 0.34f : 0.5f, -849, Range(1.5f, 3.5f));
                if (!flood) continue;
                var beam = Art.MakeSprite("Flood Beam", forest, Art.LightRay, -848, Art.SpriteAddMat, new Color(0.7f, 0.9f, 1f, 0.07f));
                beam.transform.localPosition = new Vector2(li.x, li.y);
                beam.transform.localRotation = Quaternion.Euler(0f, 0f, -48f);
                beam.transform.localScale = new Vector3(0.5f, 0.45f, 1f);
            }
            AddFog(forest, 0.05f, 1.6f, 0.13f, -0.1f, 0.7f, -830);
        }

        void BuildIslands()
        {
            Vector2[] homes = { new Vector2(-4.45f, 0.8f), new Vector2(5.55f, 3.1f), new Vector2(-5.85f, 2.55f) };
            float[] scales = { 0.78f, 0.6f, 0.46f };
            float[] depths = { 0.34f, 0.44f, 0.56f };
            for (int i = 0; i < homes.Length && i < MenuScenery.Islands.Length; i++)
            {
                var spec = MenuScenery.IslandSpecs[i];
                var look = MenuScenery.IslandLooks[i];
                var part = Group("Island" + i, homes[i], 0.2f + (1f - depths[i]) * 0.2f);
                var t = Child("Float", part, Vector2.zero);
                t.localScale = Vector3.one * scales[i];
                Color tint = WorldEnvironment.DepthTint(depths[i]);
                int o = -730 + i * -6;
                DressRock(t, MenuScenery.Islands[i], MenuScenery.IslandCenters[i], look, spec.Kind, spec.HalfW, tint, o, i == 0);
                floaters.Add(new Floater { T = t, Home = Vector2.zero, Phase = R() * 10f, Amp = 0.06f + 0.03f * i, Speed = 0.5f + 0.15f * i, Tilt = 1.5f });
            }
        }

        /// <summary>Grass, hanging roots, crystal glow and floating pebbles around a rock body (like the arena's platforms).</summary>
        void DressRock(Transform t, Sprite body, Vector2 center, PlatformLook look, Level.Style kind, float half, Color tint, int order, bool pebbles)
        {
            bool rock = kind == Level.Style.Rock;
            var hang = new FoliageLayer();
            foreach (var h in look.Hangs)
                hang.Add(R() > 0.3f ? Pick(FoliageArt.Roots) : Pick(FoliageArt.Moss), h, Range(0.75f, 1.15f), Color.white, 1f, 0f, R() > 0.5f);
            if (hang.Count > 0) hang.Build(t, "Hangings", order - 2, FoliageLayer.MakeMaterial("SF Menu Hangings", 0f, 0.8f, false, 1f, tint));

            if (body != null) Art.MakeSprite("Body", t, body, order, Art.SpriteMat, tint).transform.localPosition = center;

            var top = new FoliageLayer();
            if (rock)
            {
                for (float x = -half + 0.05f; x < half - 0.05f; x += Range(0.08f, 0.2f))
                {
                    float roll = R();
                    var v = roll < 0.8f ? Pick(FoliageArt.Grass) : roll < 0.9f ? Pick(FoliageArt.Clover) : Pick(FoliageArt.Flowers);
                    top.Add(v, new Vector2(x, Range(0.05f, 0.12f)), Range(0.42f, 0.8f), Color.white, 1f, 0f, R() > 0.5f);
                }
                for (int i = 0; i < 2; i++)
                    top.Add(Pick(FoliageArt.Mushrooms), new Vector2(Range(-half + 0.3f, half - 0.3f), 0.1f), Range(0.5f, 0.72f), Color.white, 1f, 0f, R() > 0.5f);
            }
            else
            {
                for (float x = -half + 0.2f; x < half - 0.2f; x += Range(0.5f, 1.1f))
                    top.Add(Pick(FoliageArt.Crystals), new Vector2(x, Range(0.06f, 0.1f)), Range(0.3f, 0.48f), Color.white, 0f, 0f, R() > 0.5f, Range(-20f, 20f));
                for (float x = -half + 0.1f; x < half - 0.1f; x += Range(0.35f, 0.8f))
                    top.Add(Pick(FoliageArt.Grass), new Vector2(x, Range(0.05f, 0.1f)), Range(0.35f, 0.55f), new Color(0.85f, 1f, 1f, 1f), 1f, 0f, R() > 0.5f);
            }
            if (top.Count > 0)
                top.Build(t, "Top", order + 2, FoliageLayer.MakeMaterial("SF Menu Rock Top", 0f, 1f, false, 1f, tint),
                    FoliageLayer.MakeMaterial("SF Menu Rock Glow", 0f, 1f, true, 0.8f), order + 3);

            foreach (var c in look.Crystals)
                Glow("Crystal", t, c, kind == Level.Style.Crystal ? 0.3f : 0.42f, Palette.Crystal, kind == Level.Style.Crystal ? 0.24f : 0.34f, order + 4, Range(1.5f, 3.5f));
            if (kind == Level.Style.Crystal)
                Glow("Crystal Light", t, new Vector2(0f, -0.9f), half * 3f, Palette.Crystal, 0.08f, order - 3, 0.6f, true);
            if (!rock) return;

            var pool = Art.MakeSprite("Rock Light", t, Art.SoftGlow, order - 3, Art.SpriteAddMat, Palette.Crystal.WithAlpha(0.08f));
            pool.transform.localPosition = new Vector3(0f, -1f, 0f);
            pool.transform.localScale = new Vector3(half * 2.6f, 2.2f, 1f);
            if (!pebbles) return;
            for (int i = 0; i < 3; i++)
            {
                var peb = Art.MakeSprite("Pebble", t, DepthArt.Pebbles[i % DepthArt.Pebbles.Length], order - 1, Art.SpriteMat, tint);
                float s = Range(0.6f, 1f);
                peb.transform.localScale = new Vector3(s, s, 1f);
                var home = new Vector2(Mathf.Lerp(-half + 0.4f, half - 0.4f, (i + 0.5f) / 3f) + Range(-0.2f, 0.2f), -Range(1.45f, 1.9f));
                floaters.Add(new Floater { T = peb.transform, Home = home, Phase = R() * 10f, Amp = Range(0.05f, 0.1f), Speed = Range(0.7f, 1.2f), Tilt = Range(-14f, 14f) * 10f });
            }
        }

        void BuildMast()
        {
            // the last floodlight stands on the right lip and leans out over the gap
            var part = Group("Floodlight", new Vector2(4.8f, MenuScenery.GroundTop - 0.22f), 0.36f);
            Color tint = WorldEnvironment.DepthTint(0.16f);
            var mast = Art.MakeSprite("Mast", part, MenuScenery.Mast, -660, Art.SpriteMat, tint);
            const float leanDeg = 16f;
            mast.transform.localRotation = Quaternion.Euler(0f, 0f, leanDeg);

            mastHead = Child("Head", mast.transform, MenuScenery.MastHead);
            // lamps: the live ones glow, one of them stutters; the beam falls on the centre circle
            foreach (var l in MenuScenery.MastLamps)
            {
                Vector2 at = new Vector2(l.x, l.y) - MenuScenery.MastHead;
                if (l.z > 0.5f)
                {
                    var g = Glow("Lamp", mastHead, at, 0.34f, new Color(0.85f, 0.96f, 1f), 0.75f, -659, Range(2f, 4f));
                    if (flicker == null) flicker = g;
                }
                else Glow("Dead Lamp", mastHead, at, 0.2f, new Color(0.7f, 0.85f, 1f), 0.05f, -659, 6f);
            }
            Glow("Head Halo", mastHead, Vector2.zero, 2.2f, new Color(0.7f, 0.9f, 1f), 0.16f, -661, 1.2f, true);

            // aim the cones from the head (world) at the pedestal
            Vector2 headWorld = (Vector2)part.localPosition + MathUtil.Rotate(MenuScenery.MastHead, leanDeg);
            Vector2 target = new Vector2(0f, MenuScenery.GroundTop + 0.05f);
            Vector2 dir = (target - headWorld).normalized;
            float angle = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
            float dist = Vector2.Distance(target, headWorld);
            float[] widths = { 3.2f, 1.6f };
            float[] alphas = { 0.06f, 0.08f };
            for (int i = 0; i < widths.Length; i++)
            {
                var cone = Art.MakeSprite("Cone" + i, mastHead, Art.LightRay, -655, Art.SpriteAddMat, new Color(0.75f, 0.92f, 1f, alphas[i]));
                cone.transform.localRotation = Quaternion.Euler(0f, 0f, angle - leanDeg);
                cone.transform.localScale = new Vector3(widths[i], dist * 1.25f / 8f, 1f);
                cones.Add(cone);
            }
        }

        void BuildGround()
        {
            var ground = Group("Ground", Vector2.zero, 0.5f);
            Color tint = WorldEnvironment.DepthTint(0.03f);

            // light welling up from deep in the chasm, mist and specks drifting up in it
            Glow("Chasm Light", ground, new Vector2(0f, -5.1f), 7f, Palette.Crystal, 0.3f, -640, 0.5f, true);
            Glow("Chasm Core", ground, new Vector2(0f, -5.6f), 3.4f, new Color(0.7f, 1f, 1f), 0.35f, -639, 0.8f, true);
            var chasmMist = Art.MakeSprite("Chasm Mist", ground, EnvironmentArt.FogBand, -638, Art.SpriteMat, Palette.Fog.WithAlpha(0.12f));
            chasmMist.drawMode = SpriteDrawMode.Tiled;
            chasmMist.size = new Vector2(8f, 2f);
            chasmMist.transform.localPosition = new Vector3(-4f, -4.4f, 0f);
            chasmMist.transform.localScale = new Vector3(1f, 1.2f, 1f);

            var sr = Art.MakeSprite("Torn Pitch", ground, MenuScenery.Ground, -600, Art.SpriteMat, tint);
            sr.transform.localPosition = new Vector3(0f, -4f, 0f);

            foreach (var c in MenuScenery.GroundCrystals) Glow("Wall Crystal", ground, c, 0.5f, Palette.Crystal, 0.36f, -596, Range(1.5f, 3.2f));
            foreach (var c in MenuScenery.GroundCracks) Glow("Crack", ground, new Vector2(c.x, c.y), c.z, Palette.Crystal, 0.12f, -596, 0.9f, true);

            // plants along the back edge of the pitch (they stop where the ground broke away)
            var back = new FoliageLayer();
            var front = new FoliageLayer();
            for (float x = -11.5f; x < 11.5f; x += Range(0.1f, 0.26f))
            {
                float edge = MenuScenery.GapTop + (x > 0f ? 0.06f : 0f);
                if (Mathf.Abs(x) < edge + 0.12f) continue;
                float y = MenuScenery.SurfaceAt(x) + Range(-0.04f, 0.04f);
                float roll = R();
                bool near = Mathf.Abs(x) < edge + 1.2f;
                FoliageArt.Variant v;
                float s;
                if (roll < 0.62f) { v = Pick(FoliageArt.Grass); s = Range(0.55f, 1f); }
                else if (roll < 0.72f) { v = Pick(FoliageArt.Clover); s = Range(0.5f, 0.7f); }
                else if (roll < 0.8f) { v = Pick(FoliageArt.Ferns); s = Range(0.5f, 0.8f); }
                else if (roll < 0.87f) { v = Pick(FoliageArt.Flowers); s = Range(0.45f, 0.65f); }
                else if (roll < 0.93f) { v = Pick(FoliageArt.Mushrooms); s = Range(0.6f, 0.95f); }
                else { v = near ? Pick(FoliageArt.Stones) : Pick(FoliageArt.TallGrass); s = Range(0.6f, 1f); }
                back.Add(v, new Vector2(x, y), s, Color.white, v == FoliageArt.Stones[0] || v == FoliageArt.Stones[FoliageArt.Stones.Length - 1] ? 0f : 1f, 0f, R() > 0.5f);
            }
            // bushes and reeds further out, where the columns of the menu stand
            for (float x = -11f; x < 11f; x += Range(1.1f, 2.2f))
            {
                if (Mathf.Abs(x) < 5.4f) continue;
                back.Add(R() > 0.4f ? Pick(FoliageArt.Bushes) : Pick(FoliageArt.Reeds), new Vector2(x, MenuScenery.GroundTop + Range(0.02f, 0.1f)), Range(0.7f, 1.05f),
                    new Color(0.8f, 0.9f, 0.92f, 1f), 1f, 0f, R() > 0.5f);
            }
            for (float x = -11.5f; x < 11.5f; x += Range(0.16f, 0.38f))
            {
                float y = MenuScenery.SurfaceAt(x) - MenuScenery.PitchBand;
                if (Mathf.Abs(x) < MenuScenery.GapEdge(y, Mathf.Sign(x)) + 0.1f) continue;
                front.Add(R() > 0.85f ? Pick(FoliageArt.Clover) : Pick(FoliageArt.Grass), new Vector2(x, y + Range(-0.02f, 0.03f)), Range(0.4f, 0.72f),
                    new Color(0.95f, 1f, 1f, 1f), 1f, 0f, R() > 0.5f);
            }
            back.Build(ground, "Edge Plants", -598, FoliageLayer.MakeMaterial("SF Menu Edge", 0f, 1f, false, 1f, tint),
                FoliageLayer.MakeMaterial("SF Menu Edge Glow", 0f, 1f, true, 0.85f), -597);
            front.Build(ground, "Lip Plants", -594, FoliageLayer.MakeMaterial("SF Menu Lip", 0f, 1f, false, 1f, tint));

            // crystals and stones in the earth wall
            var earth = new FoliageLayer();
            for (int i = 0; i < 22; i++)
            {
                float x = Range(-11f, 11f);
                float y = Range(-5.2f, -3.6f);
                if (Mathf.Abs(x) < MenuScenery.GapEdge(y, Mathf.Sign(x)) + 0.4f) continue;
                earth.Add(Pick(FoliageArt.Stones), new Vector2(x, y), Range(0.8f, 1.5f), new Color(0.72f, 0.82f, 0.86f, 1f));
            }
            for (int i = 0; i < 8; i++)
            {
                float x = Range(-10f, 10f);
                float y = Range(-5f, -3.8f);
                if (Mathf.Abs(x) < MenuScenery.GapEdge(y, Mathf.Sign(x)) + 0.4f) continue;
                earth.Add(Pick(FoliageArt.Crystals), new Vector2(x, y), Range(1f, 1.4f), Color.white, 0f, 0f, R() > 0.5f, Range(-25f, 25f));
            }
            earth.Build(ground, "Earth Decor", -592, FoliageLayer.MakeMaterial("SF Menu Earth", 0f, 0f),
                FoliageLayer.MakeMaterial("SF Menu Earth Glow", 0f, 0f, true, 0.45f), -591);

            // the left goal: a monster portal flickers between its posts
            var goal = Child("Goal", ground, new Vector2(-2.62f, MenuScenery.GroundTop - 0.22f));
            goal.localScale = new Vector3(0.78f, 0.78f, 1f);
            Art.MakeSprite("Frame", goal, EnvironmentArt.Goal, -586, Art.SpriteMat, new Color(0.82f, 0.9f, 0.93f, 1f));
            portal = Child("Portal", goal, new Vector2(-0.95f, 1.2f));
            portalGlow = Art.MakeSprite("Glow", portal, Art.SoftGlow, -588, Art.SpriteAddMat, Palette.MonsterGlow.WithAlpha(0f));
            portalGlow.transform.localScale = new Vector3(1.8f, 2.8f, 1f);
            portalSwirl = Art.MakeSprite("Swirl", portal, MonsterArt.Portal, -587, Art.SpriteAddMat, Palette.MonsterGlow.WithAlpha(0f));
            portalSwirl.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

            AddFog(ground, MenuScenery.GroundTop - 0.2f, 0.9f, 0.07f, 0.28f, 0.05f, -593);
        }

        void BuildPedestal()
        {
            // positioned every frame by the menu, straight under the player's feet
            pedestal = Child("Pedestal", root, Vector2.zero);
            var look = MenuScenery.PedestalLook;
            float half = MenuScenery.PedestalHalf;
            const int o = -560;

            var hang = new FoliageLayer();
            foreach (var h in look.Hangs)
                hang.Add(R() > 0.3f ? Pick(FoliageArt.Roots) : Pick(FoliageArt.Moss), h, Range(0.8f, 1.2f), Color.white, 1f, 0f, R() > 0.5f);
            hang.Build(pedestal, "Roots", o - 2, FoliageLayer.MakeMaterial("SF Menu Pedestal Roots", 0f, 0.8f));
            Art.MakeSprite("Body", pedestal, MenuScenery.Pedestal, o).transform.localPosition = MenuScenery.PedestalCenter;

            var grass = new FoliageLayer();
            for (float x = -half + 0.05f; x < half - 0.05f; x += Range(0.07f, 0.16f))
            {
                // the middle stays low so the player's boots are not hidden in it
                bool middle = Mathf.Abs(x) < 0.7f;
                float roll = R();
                var v = roll < 0.85f ? Pick(FoliageArt.Grass) : roll < 0.93f ? Pick(FoliageArt.Clover) : Pick(FoliageArt.Flowers);
                grass.Add(v, new Vector2(x, Range(0.08f, 0.13f)), middle ? Range(0.3f, 0.45f) : Range(0.45f, 0.78f), Color.white, 1f, 0f, R() > 0.5f);
            }
            grass.Add(Pick(FoliageArt.Mushrooms), new Vector2(-half + 0.35f, 0.1f), 0.7f, Color.white, 1f, 0f, false);
            grass.Add(Pick(FoliageArt.Mushrooms), new Vector2(half - 0.45f, 0.1f), 0.55f, Color.white, 1f, 0f, true);
            grass.Build(pedestal, "Grass", o + 2, FoliageLayer.MakeMaterial("SF Menu Pedestal Grass", 0f, 1f),
                FoliageLayer.MakeMaterial("SF Menu Pedestal Glow", 0f, 1f, true, 0.8f), o + 3);
            var lip = new FoliageLayer();
            for (float x = -half; x < half; x += Range(0.12f, 0.3f))
                lip.Add(Pick(FoliageArt.Grass), new Vector2(x, -Range(0.1f, 0.15f)), Range(0.3f, 0.5f), new Color(0.95f, 1f, 1f, 1f), 1f, 0f, R() > 0.5f);
            lip.Build(pedestal, "Lip", o + 4, FoliageLayer.MakeMaterial("SF Menu Pedestal Lip", 0f, 1f));

            foreach (var c in look.Crystals) Glow("Crystal", pedestal, c, 0.5f, Palette.Crystal, 0.4f, o + 5, Range(1.5f, 3.5f));
            Glow("Under Light", pedestal, new Vector2(0f, -1.1f), 5.5f, Palette.Crystal, 0.1f, o - 3, 0.6f, true);
            // the floodlight's pool of light on the circle
            spot = Art.MakeSprite("Spot", pedestal, Art.SoftGlow, o + 1, Art.SpriteAddMat, new Color(0.8f, 0.95f, 1f, 0.16f));
            spot.transform.localPosition = new Vector3(0.15f, 0.05f, 0f);
            spot.transform.localScale = new Vector3(4.4f, 0.9f, 1f);
            for (int i = 0; i < 3; i++)
            {
                var peb = Art.MakeSprite("Pebble", pedestal, DepthArt.Pebbles[i % DepthArt.Pebbles.Length], o - 1);
                float s = Range(0.8f, 1.2f);
                peb.transform.localScale = new Vector3(s, s, 1f);
                var home = new Vector2(-1.1f + i * 1.1f + Range(-0.2f, 0.2f), -Range(1.55f, 2.05f));
                floaters.Add(new Floater { T = peb.transform, Home = home, Phase = R() * 10f, Amp = Range(0.05f, 0.1f), Speed = Range(0.7f, 1.2f), Tilt = Range(-140f, 140f) });
            }
        }

        void BuildFrame()
        {
            // trees at the far edges, their crowns and hanging moss framing the top corners
            var frame = Group("Frame", Vector2.zero, 0.75f);
            Color tint = WorldEnvironment.DepthTint(0.08f);
            var trunkL = Art.MakeSprite("Trunk", frame, DepthArt.Trunk, -520, Art.SpriteMat, tint);
            trunkL.transform.localPosition = new Vector3(-9.0f, -3.9f, 0f);
            var trunkR = Art.MakeSprite("Trunk", frame, DepthArt.Trunk, -520, Art.SpriteMat, tint);
            trunkR.transform.localPosition = new Vector3(9.15f, -4.1f, 0f);
            trunkR.flipX = true;

            var crowns = new FoliageLayer();
            Vector3[] spots =
            {
                new Vector3(-8.7f, 4.35f, 4.4f), new Vector3(-6.2f, 4.95f, 3.9f), new Vector3(-4.2f, 5.4f, 3.0f), new Vector3(-10.1f, 2.9f, 3.2f),
                new Vector3(8.8f, 4.3f, 4.4f), new Vector3(6.3f, 5.0f, 3.8f), new Vector3(4.4f, 5.45f, 2.9f), new Vector3(10.2f, 2.8f, 3.2f),
            };
            foreach (var c in spots)
            {
                var v = Pick(FoliageArt.Canopies);
                float scale = c.z / Mathf.Max(0.1f, v.Units.width * 0.85f);
                crowns.Add(v, new Vector2(c.x, c.y), scale, new Color(0.9f, 1f, 1f, 1f), 1f, 0f, R() > 0.5f);
            }
            var hang = new FoliageLayer();
            for (int side = -1; side <= 1; side += 2)
                for (float x = 4.3f; x < 10.5f; x += Range(0.35f, 0.8f))
                {
                    float y = 4.95f + 0.25f * (x - 4.3f) / 6f + Range(-0.2f, 0.25f);
                    var v = R() > 0.45f ? Pick(FoliageArt.Moss) : Pick(FoliageArt.Ivy);
                    hang.Add(v, new Vector2(side * x, y), Range(0.8f, 1.35f), new Color(0.9f, 1f, 1f, 1f), 1f, 0f, R() > 0.5f);
                }
            hang.Build(frame, "Hanging Moss", -517, FoliageLayer.MakeMaterial("SF Menu Moss", 0.05f, 0.9f, false, 1f, tint));
            crowns.Build(frame, "Crowns", -515, FoliageLayer.MakeMaterial("SF Menu Crowns", 0.1f, 0.7f, false, 1f, tint));

            // lanterns hanging into the corners (warm light against the cold night, like in the ruins)
            ambient.AddLantern(frame, new Vector2(-3.9f, 5.2f), -512, 2.05f, 3.3f);
            ambient.AddLantern(frame, new Vector2(6.55f, 5.3f), -512, 2.3f, 11.7f);

            // foreground fronds in the bottom corners
            var front = Group("Front", Vector2.zero, 1f);
            var fronds = new FoliageLayer();
            float[] xs = { -10.2f, -8.6f, -7.2f, 7.4f, 8.8f, 10.3f };
            foreach (float x in xs)
            {
                int n = 2 + rng.Next(2);
                for (int k = 0; k < n; k++)
                    fronds.Add(Pick(FoliageArt.FgLeaves), new Vector2(x + Range(-0.8f, 0.8f), Range(-5.9f, -5.3f)), Range(0.6f, 0.85f),
                        new Color(1f, 1f, 1f, 0.97f), 1f, 0f, R() > 0.5f, Range(-14f, 14f) + (x < 0f ? -8f : 8f));
            }
            fronds.Build(front, "Fronds", 600, FoliageLayer.MakeMaterial("SF Menu Fronds", 0f, 0.5f));

            // something is watching from the dark
            for (int i = 0; i < eyes.Length; i++)
            {
                var e = new Eyes { Spot = i == 0 ? 0 : 2, Timer = 2f + i * 3f, Blink = R() * 3f };
                e.T = Child("Eyes", front, EyeSpots[e.Spot]);
                e.GlowL = Art.MakeSprite("GlowL", e.T, Art.SoftGlow, 598, Art.SpriteGlowMat, Palette.MonsterGlow.WithAlpha(0f));
                e.GlowR = Art.MakeSprite("GlowR", e.T, Art.SoftGlow, 598, Art.SpriteGlowMat, Palette.MonsterGlow.WithAlpha(0f));
                e.GlowL.transform.localPosition = new Vector3(-0.09f, 0f, 0f);
                e.GlowR.transform.localPosition = new Vector3(0.09f, 0f, 0f);
                e.GlowL.transform.localScale = e.GlowR.transform.localScale = Vector3.one * 0.32f;
                e.L = Art.MakeSprite("L", e.T, Art.Circle, 599, Art.SpriteEmissiveMat, Palette.MonsterEye).transform;
                e.R = Art.MakeSprite("R", e.T, Art.Circle, 599, Art.SpriteEmissiveMat, Palette.MonsterEye).transform;
                e.L.localPosition = new Vector3(-0.09f, 0f, 0f);
                e.R.localPosition = new Vector3(0.09f, 0f, 0f);
                eyes[i] = e;
            }
        }

        void BuildLife()
        {
            // fireflies over the ground, the rocks and the corners; a few big ones right at the lens
            for (int i = 0; i < 34; i++)
            {
                bool front = i < 5;
                var sr = Art.MakeSprite("Firefly", root, Art.SoftGlow, front ? 610 : -590, Art.SpriteGlowMat, Palette.Firefly.WithAlpha(0f));
                sr.transform.localScale = Vector3.one * (front ? Range(0.35f, 0.6f) : Range(0.1f, 0.24f));
                Vector2 home = front ? new Vector2(Range(-9f, 9f), Range(-4.5f, 3f))
                    : i < 22 ? new Vector2(Range(-10f, 10f), Range(-2.5f, 1.2f))
                    : new Vector2(Range(-10f, 10f), Range(1f, 4.5f));
                fireflies.Add(new Firefly
                {
                    Sr = sr, Home = home, Phase = R() * 50f,
                    Fx = Range(0.12f, 0.3f), Fy = Range(0.15f, 0.35f), Ax = Range(0.5f, 1.5f), Ay = Range(0.3f, 0.8f),
                    Alpha = front ? 0.35f : 0.9f,
                });
            }
            // crystal specks rising out of the chasm
            for (int i = 0; i < 18; i++)
            {
                var sr = Art.MakeSprite("Speck", root, Art.SoftGlow, -637, Art.SpriteGlowMat, Palette.Crystal.WithAlpha(0f));
                specks.Add(new Speck { Sr = sr, Age = 1f, Life = 0f });
            }
            ambient.BuildLeaves(root);
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
                p.T.localPosition = p.Home - lean * (0.08f + p.Lean * 0.42f) + drift * (0.3f + p.Lean);

            foreach (var f in floaters)
            {
                float t = time * f.Speed + f.Phase;
                f.T.localPosition = f.Home + new Vector2(Mathf.Sin(t * 0.7f) * 0.05f, Mathf.Sin(t) * f.Amp);
                f.T.localRotation = Quaternion.Euler(0f, 0f, Mathf.Abs(f.Tilt) > 10f ? time * f.Tilt * 0.1f + f.Phase * 30f : Mathf.Sin(t * 0.8f) * f.Tilt);
            }

            for (int i = 0; i < blinks.Count; i++)
            {
                var b = blinks[i];
                float k = 0.72f + 0.28f * Mathf.PerlinNoise(time * b.Speed, b.Phase);
                b.Sr.color = b.Color.WithAlpha(b.Base * k);
            }
            UpdateFlicker(dt);

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

            // the portal breathes and turns
            float pulse = 0.6f + 0.4f * Mathf.Sin(time * 1.7f) * Mathf.Sin(time * 0.63f + 1f);
            portalGlow.color = Palette.MonsterGlow.WithAlpha(0.07f + 0.05f * pulse);
            portalSwirl.color = Palette.MonsterGlow.WithAlpha(0.14f + 0.1f * pulse);
            portalSwirl.transform.localRotation = Quaternion.Euler(0f, 0f, -time * 40f);
            portal.localScale = new Vector3(1f + 0.04f * pulse, 1f + 0.04f * pulse, 1f);

            UpdateLife(dt);
            UpdateEyes(dt);
            ambient.Update(dt, time, center, wind);
        }

        /// <summary>The floodlight: one lamp stutters, and with it the beam.</summary>
        void UpdateFlicker(float dt)
        {
            flickerT -= dt;
            float k = 1f;
            if (flickerT < 0f)
            {
                // a short burst of stutter every few seconds
                float burst = -flickerT;
                k = Mathf.PerlinNoise(time * 30f, 7f) > 0.45f ? 1f : 0.15f;
                if (burst > 0.6f) flickerT = Range(3f, 7f);
            }
            if (flicker != null) flicker.color = new Color(0.85f, 0.96f, 1f, 0.75f * k);
            float hum = 0.85f + 0.15f * Mathf.PerlinNoise(time * 2f, 3f);
            for (int i = 0; i < cones.Count; i++)
                cones[i].color = new Color(0.75f, 0.92f, 1f, (i == 0 ? 0.06f : 0.085f) * hum * (0.75f + 0.25f * k));
            spot.color = new Color(0.8f, 0.95f, 1f, 0.16f * hum * (0.8f + 0.2f * k));
        }

        void UpdateLife(float dt)
        {
            for (int i = 0; i < fireflies.Count; i++)
            {
                var f = fireflies[i];
                float t = time + f.Phase;
                Vector2 pos = f.Home + new Vector2(Mathf.Sin(t * f.Fx * MathUtil.Tau) * f.Ax + Mathf.Sin(t * 0.37f) * 0.3f, Mathf.Sin(t * f.Fy * MathUtil.Tau + 1.3f) * f.Ay);
                pos -= lean * (f.Alpha < 0.5f ? 0.6f : 0.25f);
                float blink = Mathf.Clamp01(0.35f + 0.65f * Mathf.Sin(t * 1.1f + f.Phase * 3f)) * (0.6f + 0.4f * Mathf.PerlinNoise(t * 2f, f.Phase));
                f.Sr.transform.localPosition = pos;
                f.Sr.color = Palette.Firefly.WithAlpha(blink * f.Alpha);
            }

            speckTimer -= dt;
            foreach (var s in specks)
            {
                if (s.Age >= s.Life)
                {
                    if (speckTimer > 0f) { s.Sr.color = Palette.Crystal.WithAlpha(0f); continue; }
                    speckTimer = Range(0.2f, 0.5f);
                    s.Pos = new Vector2(Range(-1.8f, 1.8f), Range(-5.6f, -4.2f));
                    s.Age = 0f;
                    s.Life = Range(3f, 5.5f);
                    s.Speed = Range(0.35f, 0.7f);
                    s.Size = Range(0.07f, 0.14f);
                    s.Phase = R() * 10f;
                }
                s.Age += dt;
                s.Pos += new Vector2(Mathf.Sin(time * 1.3f + s.Phase) * 0.15f, s.Speed) * dt;
                float u = s.Age / s.Life;
                s.Sr.transform.localPosition = s.Pos - lean * 0.2f;
                s.Sr.transform.localScale = Vector3.one * s.Size * (1f - u * 0.5f);
                s.Sr.color = Palette.Crystal.WithAlpha(MathUtil.Bump(u) * 0.8f);
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
                        e.T.localPosition = EyeSpots[e.Spot];
                        e.Target = 1f;
                        e.Timer = Range(4f, 9f);
                    }
                }
                e.Open = Mathf.MoveTowards(e.Open, e.Target, dt * (e.Target > e.Open ? 1.2f : 3f));
                e.Blink -= dt;
                float lid = 1f;
                if (e.Blink < 0f)
                {
                    lid = Mathf.Abs(e.Blink + 0.07f) / 0.07f;
                    if (e.Blink < -0.14f) e.Blink = Range(1.5f, 4.5f);
                }
                float open = MathUtil.Smooth01(e.Open);
                var sc = new Vector3(0.07f, 0.045f * Mathf.Clamp01(lid) * open + 0.001f, 1f);
                e.L.localScale = e.R.localScale = sc;
                // they glance around a little
                float look = Mathf.Sin(time * 0.7f + e.Spot) * 0.02f;
                e.L.localPosition = new Vector3(-0.09f + look, 0f, 0f);
                e.R.localPosition = new Vector3(0.09f + look, 0f, 0f);
                var gc = Palette.MonsterGlow.WithAlpha(0.3f * open);
                e.GlowL.color = e.GlowR.color = gc;
            }
        }

        bool SpotTaken(int spot, Eyes self)
        {
            foreach (var e in eyes) if (e != null && e != self && e.Spot == spot) return true;
            return false;
        }

        /// <summary>
        /// Keeps the floating centre circle under the player: world position of the feet, and the
        /// scale the menu's centre stack is drawn at (1 = composed size).
        /// </summary>
        public void PlacePedestal(Vector2 feetWorld, float scale, float alpha)
        {
            if (!built) return;
            Vector3 local = root.InverseTransformPoint(new Vector3(feetWorld.x, feetWorld.y, 0f));
            float s = scale / Mathf.Max(1e-4f, root.localScale.x);
            pedestal.localPosition = new Vector3(local.x, local.y, 0f);
            pedestal.localScale = new Vector3(s, s, 1f);
        }
    }
}
