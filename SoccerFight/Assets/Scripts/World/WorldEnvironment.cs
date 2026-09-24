using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Die Spielwelt im neuen Design (Vorlagen in Inspiration/nnewDesign, Grafik aus <see cref="DesignArt"/>):
    /// Nachthimmel, die gemalte Kulisse mit Mond, Ruinen und Wasserfällen, davor ein dunkler Baumgürtel,
    /// dann die beleuchteten Ruinen, Laternen und Büsche direkt hinter dem Rasen, Rahmenbäume an den
    /// Arenaenden, die Grasdecke über der Quadermauer, Plattformen und dunkles Laub im Vordergrund.
    /// Dazu bewegtes Detail: Pflanzen im Wind, flackernde Laternen, pulsierende Kristalle, Glühwürmchen,
    /// funkelnde Sterne. Alle Stages teilen diese Welt; die Stage-Farbstimmung legt <see cref="ThemeGrade"/> darüber.
    /// </summary>
    public sealed class WorldEnvironment
    {
        struct Layer { public Transform t; public Vector2 basePos; public float px, py; }
        struct Fog { public SpriteRenderer sr; public float speed, offset, baseX; }
        struct Firefly { public SpriteRenderer sr; public Vector2 home; public float phase, fx, fy, ax, ay; }
        struct Blink { public SpriteRenderer sr; public Color color; public float baseA, phase, speed, depth; }
        struct Lamp { public SpriteRenderer glow, halo; public float seed, strength; }

        readonly List<Layer> layers = new List<Layer>();
        readonly List<Fog> fogs = new List<Fog>();
        readonly List<Firefly> fireflies = new List<Firefly>();
        readonly List<Blink> blinks = new List<Blink>();
        readonly List<Lamp> lamps = new List<Lamp>();

        Transform root;
        CameraRig cam;
        System.Random rng;
        float moteTimer;
        Color propTint = Color.white;   // Ruinenreihe: abgedunkelt
        float lampStrength = 1f;

        public Transform Root => root;
        public PlatformViews Platforms { get; private set; }
        public Transform LeftPortal { get; private set; }
        public Transform RightPortal { get; private set; }
        public float Wind { get; private set; }

        public const float GoalX = 16.9f;

        static readonly int WindId = Shader.PropertyToID("_SF_Wind");
        static readonly int Push0Id = Shader.PropertyToID("_SF_Push0");
        static readonly int Push1Id = Shader.PropertyToID("_SF_Push1");

        static readonly Color FireflyColor = new Color(1f, 0.86f, 0.42f, 1f);
        static readonly Color LampColor = new Color(1f, 0.7f, 0.3f, 1f);
        static readonly Color CrystalColor = new Color(0.4f, 0.8f, 1f, 1f);

        float R() => (float)rng.NextDouble();
        float Range(float a, float b) => a + (b - a) * R();
        T Pick<T>(T[] arr) => arr[rng.Next(arr.Length)];

        /// <summary>Night falls off with distance (the title screen's backdrop still uses it).</summary>
        public static Color DepthTint(float depth)
        {
            float b = Mathf.Lerp(1f, 0.42f, depth);
            return new Color(b * Mathf.Lerp(1f, 0.86f, depth), b * Mathf.Lerp(1f, 0.94f, depth), b, 1f);
        }

        Transform Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : root, false);
            return go.transform;
        }

        /// <summary>px/py: how much of the camera motion the layer follows (1 = infinitely far, 0 = play plane).</summary>
        Transform AddLayer(string name, Vector2 basePos, float px, float py)
        {
            var t = Group(name);
            layers.Add(new Layer { t = t, basePos = basePos, px = px, py = py });
            return t;
        }

        SpriteRenderer Put(Transform parent, string sprite, Vector2 pos, float scale, int order, bool flip = false, Color? tint = null)
        {
            var sr = Art.MakeSprite(sprite, parent, DesignArt.Get(sprite), order, DesignArt.SpriteMat, tint ?? propTint);
            sr.transform.localPosition = pos;
            sr.transform.localScale = new Vector3(flip ? -scale : scale, scale, 1f);
            return sr;
        }

        /// <summary>Requires EnvironmentArt.Begin/End (goal frame, fog band) to have run.</summary>
        public void Build(Transform parent, CameraRig cameraRig)
        {
            DesignArt.Load();
            cam = cameraRig;
            rng = new System.Random(1234);
            root = new GameObject("Environment").transform;
            root.SetParent(parent, false);

            BuildSky();
            BuildBackdrop();
            BuildTreeBelt();
            BuildRuins();
            BuildGround();
            BuildPlatforms();
            BuildForeground();
            BuildFireflies();
        }

        // ------------------------------------------------------------------ Himmel und gemalte Kulisse

        void BuildSky()
        {
            var sky = AddLayer("Sky", new Vector2(0f, 3f), 1f, 1f);
            var fill = Art.MakeSprite("Sky Fill", sky, DesignArt.Block, -1000, DesignArt.SpriteMat, DesignArt.SkyTop);
            fill.transform.localScale = new Vector3(120f, 40f, 1f);
        }

        void BuildBackdrop()
        {
            // das Hauptbild ohne Vordergrund; seine Bodenlinie liegt knapp über dem Rasen
            var plate = AddLayer("Backdrop", new Vector2(0f, 0.35f), 0.88f, 0.8f);
            Put(plate, "plate", Vector2.zero, 1f, -950);
            // unter dem Bild dunkles Tal, damit bei hoher Kamera kein Himmel unter der Kulisse durchscheint
            var below = Art.MakeSprite("Valley Floor", plate, DesignArt.Block, -951, DesignArt.SpriteMat, new Color(0.04f, 0.1f, 0.2f));
            below.transform.localPosition = new Vector3(0f, -2.9f, 0f);
            below.transform.localScale = new Vector3(40f, 6f, 1f);

            // Sterne über dem Bildrand (der Himmel geht dort einfarbig weiter) und ein paar funkelnde im Bild
            for (int i = 0; i < 70; i++)
            {
                bool inPicture = i < 22;
                var p = new Vector2(Range(-12f, 12f), inPicture ? Range(4.6f, 6.9f) : Range(7.1f, 14f));
                float s = Range(0.035f, 0.075f) * (R() < 0.15f ? 1.8f : 1f);
                AddBlink(plate, p, s, new Color(0.85f, 0.92f, 1f), inPicture ? 0.55f : 0.85f, -949, Range(0.6f, 2.2f));
            }

            // Mondschein: weiter Hof und ein atmender Schimmer auf der gemalten Scheibe
            Vector2 moon = new Vector2(5.33f, 5.78f);
            var wide = Art.MakeSprite("Moon Halo", plate, Art.SoftGlow, -948, Art.SpriteAddMat, new Color(0.55f, 0.7f, 1f, 0.1f));
            wide.transform.localPosition = moon;
            wide.transform.localScale = Vector3.one * 7f;
            blinks.Add(new Blink { sr = wide, color = new Color(0.55f, 0.7f, 1f), baseA = 0.1f, phase = 3f, speed = 0.25f });
            var near = Art.MakeSprite("Moon Glow", plate, Art.SoftGlow, -947, Art.SpriteAddMat, new Color(0.8f, 0.9f, 1f, 0.12f));
            near.transform.localPosition = moon;
            near.transform.localScale = Vector3.one * 2.6f;
            blinks.Add(new Blink { sr = near, color = new Color(0.8f, 0.9f, 1f), baseA = 0.12f, phase = 7f, speed = 0.4f });

            AddFog("Fog Backdrop", -930, new Vector2(0f, 1.9f), 0.8f, 0.74f, 1.6f, 0.16f, 0.07f, new Color(0.35f, 0.55f, 0.95f));
        }

        // ------------------------------------------------------------------ dunkler Baumgürtel (Mittelgrund)

        void BuildTreeBelt()
        {
            var belt = AddLayer("Tree Belt", new Vector2(0f, -0.1f), 0.55f, 0.4f);
            var floor = Art.MakeSprite("Belt Floor", belt, DesignArt.Block, -712, DesignArt.SpriteMat, new Color(0.05f, 0.12f, 0.17f));
            floor.transform.localPosition = new Vector3(0f, -3.5f, 0f);
            floor.transform.localScale = new Vector3(90f, 7.8f, 1f);

            // hinten Ruinen und Wasserfälle, davor die Baumreihe mit Lücken, durch die die Kulisse scheint
            string[] ruins = { "arch_night", "wall_a_night", "wall_c_night", "column_a_night", "falls_a_night", "falls_b_night" };
            for (float x = -23f; x < 23f; x += Range(3.2f, 5.5f))
                Put(belt, Pick(ruins), new Vector2(x, Range(0.2f, 0.5f)), Range(0.75f, 1f), -710, R() > 0.5f, new Color(0.8f, 0.88f, 1f));
            string[] trees = { "tree_big_night", "tree_a_night", "tree_b_night", "tree_c_night", "tree_d_night" };
            for (float x = -24f; x < 24f; x += Range(1.8f, 3.4f))
            {
                string t = Pick(trees);
                float s = t == "tree_big_night" ? Range(0.62f, 0.8f) : t == "tree_c_night" || t == "tree_d_night" ? Range(1.1f, 1.5f) : Range(0.9f, 1.15f);
                Put(belt, t, new Vector2(x, Range(0f, 0.3f)), s, -705, R() > 0.5f);
            }
            var bushes = new FoliageLayer();
            var bushKinds = DesignArt.Plants("bush_a", "bush_b", "bush_c", "bush_d");
            for (float x = -24f; x < 24f; x += Range(0.8f, 1.6f))
                bushes.Add(Pick(bushKinds), new Vector2(x, Range(0.05f, 0.25f)), Range(0.9f, 1.4f), new Color(0.2f, 0.36f, 0.46f), 1f, 0f, R() > 0.5f);
            // eine zweite, tiefere Reihe: wenn die Kamera steigt, darf unter dem Gürtel keine Lücke aufgehen
            for (float x = -24f; x < 24f; x += Range(0.7f, 1.3f))
                bushes.Add(Pick(bushKinds), new Vector2(x, Range(-0.9f, -0.5f)), Range(1.1f, 1.5f), new Color(0.15f, 0.28f, 0.36f), 1f, 0f, R() > 0.5f);
            bushes.Build(belt, "Belt Bushes", -703, DesignArt.PlantMaterial("SF Design Belt Bushes", 0.4f));

            AddFog("Fog Belt", -700, new Vector2(0f, 0.55f), 0.5f, 0.46f, 1.4f, 0.14f, -0.1f, new Color(0.3f, 0.5f, 0.85f));
        }

        // ------------------------------------------------------------------ beleuchtete Ruinen hinter dem Rasen (weltfest)

        void BuildRuins()
        {
            // Rahmenbäume an beiden Arenaenden (weltfest, markieren das Spielfeldende)
            var trees = Group("Frame Trees");
            Put(trees, "tree_big", new Vector2(-17.7f, -0.05f), 1.6f, -300);
            Put(trees, "tree_big", new Vector2(17.9f, -0.05f), 1.6f, -300, true);

            // Torbögen, Laternen und Mauerreste stehen ein Stück hinter dem Rasen: eigene, langsamer
            // mitlaufende Ebene, im Mondlicht abgedunkelt und kleiner — Kulisse, nichts zum Anfassen
            var g = AddLayer("Ruin Row", new Vector2(0f, 0.3f), 0.22f, 0.18f);
            const float Y = 0f;
            propTint = new Color(0.55f, 0.62f, 0.82f);
            lampStrength = 0.6f;
            LampPost(g, "lantern_post_a", new Vector2(-15.2f, Y), 0.75f, false, new Vector2(0.72f, 0.3f));
            Put(g, "wall_d", new Vector2(-11.8f, Y), 0.65f, -360);
            ArchWithLamp(g, new Vector2(-7.4f, Y), 0.7f, false);
            Put(g, "pillar_broken", new Vector2(-4.2f, Y), 0.6f, -360);
            Crystal(g, "crystal_mid", new Vector2(-3.1f, Y), 0.4f, -358);
            ArchWithLamp(g, new Vector2(6.5f, Y), 0.7f, true);
            Put(g, "block_e", new Vector2(9.4f, Y), 0.6f, -358);
            Put(g, "wall_c", new Vector2(11.8f, Y), 0.65f, -360);
            LampPost(g, "lantern_post_a", new Vector2(15.2f, Y), 0.75f, true, new Vector2(0.72f, 0.3f));
            propTint = Color.white;
            lampStrength = 1f;

            // dunkle Buschreihe am Fuß der Ruinen: verdeckt die Sockel, auch wenn die Kamera steigt
            var bushes = new FoliageLayer();
            var kinds = DesignArt.Plants("bush_a", "bush_b", "bush_c", "bush_d");
            for (float x = -24f; x < 24f; x += Range(0.9f, 1.7f))
                bushes.Add(Pick(kinds), new Vector2(x, Range(-0.35f, -0.1f)), Range(0.8f, 1.15f), new Color(0.26f, 0.4f, 0.48f), 1f, 0f, R() > 0.5f);
            bushes.Build(g, "Ruin Bushes", -320, DesignArt.PlantMaterial("SF Design Ruin Bushes", 0.5f));
        }

        void ArchWithLamp(Transform parent, Vector2 pos, float scale, bool flip)
        {
            var arch = Put(parent, "arch", pos, scale, -332, flip);
            // die Laterne hängt im Bogen: Kette und Laterne aus der Wandlaterne (ohne Halterung)
            var src = DesignArt.Get("lantern_post_b");
            var t = src.texture;
            var rect = new Rect(t.width * 0.33f, 0f, t.width * 0.6f, t.height * 0.78f);
            var lampSprite = Sprite.Create(t, rect, new Vector2(0.48f, 1f), src.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            float archH = arch.sprite.bounds.size.y * scale, lampScale = scale * 0.8f;
            Vector2 hook = pos + new Vector2(0f, archH * 0.66f);
            var lamp = Art.MakeSprite("Arch Lantern", parent, lampSprite, -331, DesignArt.SpriteMat, Color.Lerp(propTint, Color.white, 0.5f));
            lamp.transform.localPosition = hook;
            lamp.transform.localScale = Vector3.one * lampScale;
            AddLamp(parent, hook - new Vector2(0f, t.height * 0.53f / src.pixelsPerUnit * lampScale), 1.25f);
        }

        /// <summary>lampAt: Laterne im Bild, 0..1 von links und von unten.</summary>
        void LampPost(Transform parent, string sprite, Vector2 pos, float scale, bool flip, Vector2 lampAt)
        {
            var sr = Put(parent, sprite, pos, scale, -329, flip);
            Vector2 size = sr.sprite.bounds.size * scale;
            AddLamp(parent, pos + new Vector2((lampAt.x - 0.5f) * size.x * (flip ? -1f : 1f), lampAt.y * size.y), 1f);
        }

        void AddLamp(Transform parent, Vector2 at, float size)
        {
            var halo = Art.MakeSprite("Lamp Halo", parent, Art.SoftGlow, -334, Art.SpriteAddMat, LampColor.WithAlpha(0.2f));
            halo.transform.localPosition = at;
            halo.transform.localScale = Vector3.one * 3.4f * size;
            var glow = Art.MakeSprite("Lamp Glow", parent, Art.SoftGlow, -322, Art.SpriteGlowMat, LampColor.WithAlpha(0.35f));
            glow.transform.localPosition = at;
            glow.transform.localScale = Vector3.one * 0.55f * size;
            lamps.Add(new Lamp { glow = glow, halo = halo, seed = R() * 50f, strength = lampStrength });
        }

        void Crystal(Transform parent, string sprite, Vector2 pos, float scale, int order)
        {
            var sr = Put(parent, sprite, pos, scale, order);
            Vector2 c = pos + new Vector2(0f, sr.sprite.bounds.size.y * scale * 0.55f);
            AddBlink(parent, c, 1.2f * scale + 0.5f, CrystalColor, 0.22f, order + 1, Range(0.8f, 1.4f));
        }

        // ------------------------------------------------------------------ Rasen über der Quadermauer (weltfest)

        void BuildGround()
        {
            var ground = Group("Ground");
            var wall = Art.MakeSprite("Wall", ground, DesignArt.Get("ground"), -110);
            wall.drawMode = SpriteDrawMode.Tiled;
            wall.size = new Vector2(60f, wall.sprite.bounds.size.y);
            wall.transform.localPosition = new Vector3(-30f, 0f, 0f);
            float wallBottom = -wall.sprite.pivot.y / wall.sprite.pixelsPerUnit;
            var deep = Art.MakeSprite("Deep", ground, DesignArt.Block, -111, DesignArt.SpriteMat, DesignArt.GroundDeep);
            deep.transform.localPosition = new Vector3(0f, wallBottom - 4f + 0.02f, 0f);
            deep.transform.localScale = new Vector3(60f, 8f, 1f);

            // nur vereinzelte, kurze Grasbüschel an der Hinterkante: das Spielfeld bleibt ruhig und lesbar
            var back = new FoliageLayer();
            var grass = DesignArt.Plants("grass_b", "grass_d", "grass_e", "grass_f");
            var extra = DesignArt.Plants("fern", "grass_a");
            for (float x = -26f; x < 26f; x += Range(0.9f, 2.2f))
            {
                bool big = R() < 0.12f;
                back.Add(big ? Pick(extra) : Pick(grass), new Vector2(x, Range(0.05f, 0.09f)), big ? Range(0.32f, 0.42f) : Range(0.24f, 0.36f), Color.white, 1f, 1f, R() > 0.5f);
            }
            back.Build(ground, "Back Grass", -95, DesignArt.PlantMaterial("SF Design Pitch Grass", 1f));

            LeftPortal = BuildGoal(ground, -GoalX, 1f);
            RightPortal = BuildGoal(ground, GoalX, -1f);
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

        // ------------------------------------------------------------------ Plattformen (weltfest, pro Stage neu)

        void BuildPlatforms()
        {
            Platforms = new PlatformViews();
            Platforms.Build(root);
        }

        // ------------------------------------------------------------------ dunkles Laub im Vordergrund

        void BuildForeground()
        {
            var fg = AddLayer("Foreground", Vector2.zero, -0.35f, -0.2f);
            var leaves = new FoliageLayer();
            var kinds = DesignArt.Plants("bush_a", "bush_b", "bush_c", "fern");
            float[] xs = { -25f, 25f };   // nur ganz außen, damit nichts vor dem Spielgeschehen hängt
            foreach (float x in xs)
                for (int k = 0; k < 1; k++)
                    leaves.Add(Pick(kinds), new Vector2(x + Range(-1.2f, 1.2f), Range(-2.9f, -2.6f)), Range(1.8f, 2.4f),
                        new Color(0.06f, 0.12f, 0.15f), 1f, 0f, R() > 0.5f, Range(-8f, 8f));
            leaves.Build(fg, "Foreground Leaves", 600, DesignArt.PlantMaterial("SF Design Foreground", 0.35f));
        }

        // ------------------------------------------------------------------ Glühwürmchen, Nebel, Lichter

        void BuildFireflies()
        {
            // nur hinter dem Spielgeschehen, keine großen unscharfen direkt vor der Kamera
            for (int i = 0; i < 26; i++)
            {
                var sr = Art.MakeSprite("Firefly", root, Art.SoftGlow, -300, Art.SpriteGlowMat, FireflyColor.WithAlpha(0f));
                sr.transform.localScale = Vector3.one * Range(0.1f, 0.2f);
                fireflies.Add(new Firefly
                {
                    sr = sr,
                    home = new Vector2(Range(-20f, 20f), Range(0.4f, 5.5f)),
                    phase = R() * 50f,
                    fx = Range(0.12f, 0.3f), fy = Range(0.15f, 0.35f),
                    ax = Range(0.6f, 1.8f), ay = Range(0.3f, 0.9f),
                });
            }
        }

        void AddFog(string name, int order, Vector2 basePos, float px, float py, float height, float alpha, float speed, Color color)
        {
            var g = AddLayer(name, basePos, px, py);
            var sr = Art.MakeSprite(name, g, EnvironmentArt.FogBand, order, DesignArt.SpriteMat, color.WithAlpha(alpha));
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(64f, 2f);
            sr.transform.localScale = new Vector3(1f, height / 2f, 1f);
            sr.transform.localPosition = new Vector3(-32f, 0f, 0f);
            fogs.Add(new Fog { sr = sr, speed = speed, offset = R() * 8f, baseX = -32f });
        }

        void AddBlink(Transform parent, Vector2 pos, float size, Color color, float alpha, int order, float speed)
        {
            var sr = Art.MakeSprite("Light", parent, Art.SoftGlow, order, Art.SpriteAddMat, color.WithAlpha(alpha));
            sr.transform.localPosition = pos;
            sr.transform.localScale = Vector3.one * size;
            blinks.Add(new Blink { sr = sr, color = color, baseA = alpha, phase = R() * 20f, speed = speed, depth = 0.6f });
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

            // Sterne funkeln, Mondschein atmet, Kristalle pulsieren
            for (int i = 0; i < blinks.Count; i++)
            {
                var b = blinks[i];
                float f = 1f - b.depth + b.depth * Mathf.PerlinNoise(time * b.speed, b.phase);
                b.sr.color = b.color.WithAlpha(b.baseA * f);
            }

            // Laternen flackern
            for (int i = 0; i < lamps.Count; i++)
            {
                var l = lamps[i];
                float flicker = 0.8f + 0.2f * Mathf.PerlinNoise(time * 3.3f, l.seed) + 0.05f * Mathf.Sin(time * 19f + l.seed);
                l.glow.color = LampColor.WithAlpha(0.35f * flicker * l.strength);
                l.halo.color = LampColor.WithAlpha(0.2f * flicker * l.strength);
            }

            Platforms.Update(dt, time, Wind);

            for (int i = 0; i < fireflies.Count; i++)
            {
                var f = fireflies[i];
                float t = time + f.phase;
                Vector2 pos = f.home + new Vector2(Mathf.Sin(t * f.fx * MathUtil.Tau) * f.ax + Mathf.Sin(t * 0.37f) * 0.3f,
                    Mathf.Sin(t * f.fy * MathUtil.Tau + 1.3f) * f.ay);
                float blink = Mathf.Clamp01(0.35f + 0.65f * Mathf.Sin(t * 1.1f + f.phase * 3f)) * (0.6f + 0.4f * Mathf.PerlinNoise(t * 2f, f.phase));
                f.sr.transform.localPosition = pos;
                f.sr.color = FireflyColor.WithAlpha(blink * 0.75f);
            }

            // Staub und Pollen im Mondlicht
            var fx = FxSystem.I;
            moteTimer -= dt;
            while (moteTimer <= 0f && fx != null)
            {
                moteTimer += 0.3f;
                Rect view = cam.ViewRect;
                Vector2 p = new Vector2(view.xMin + Random.value * view.width, view.yMin + 1.5f + Random.value * (view.height - 1.5f));
                Color mc = new Color(0.8f, 0.92f, 1f, 0.45f);
                fx.Spawn(FxLayer.Back, true, Art.CellDot, p,
                    new Vector2(Wind * 0.25f + (Random.value - 0.5f) * 0.12f, 0.05f + Random.value * 0.1f),
                    Random.Range(5f, 9f), Random.Range(0.02f, 0.04f), Random.Range(0.02f, 0.035f), mc, mc, 1.8f, 0f, -0.01f, 0f, 0f, false, true);
            }
        }
    }
}
