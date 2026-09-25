using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Die Spielwelt, pro Stage aus ihrem Design-Bogen (Inspiration/StagesNewDesigns, Grafik aus <see cref="StageKit"/>):
    /// Himmel, die gemalte Kulisse (mit Leuchten auf gemalten Laternen, Mond, Lava, Polarlicht …), davor eine
    /// dunstige Mittelgrund-Kante aus dem Bodenmaterial mit der Deko der Stage (Torbögen, Kamine, Kristalle,
    /// Schmiedeöfen …), der kachelbare Boden ohne kleine Deko-Objekte, Rahmenstücke an den
    /// Arenaenden, Tropfsteine oder schwebende Brocken, wo die Stage sie hat. Dazu bewegtes Detail: flackernde
    /// Lichter, pulsierende Kristalle, Nebel, Glühwürmchen, Staub im Licht. Beim Stage-Wechsel wird alles
    /// ausgetauscht (<see cref="ApplyStage"/>); Tore und Plattform-Gruppe bleiben.
    /// </summary>
    public sealed class WorldEnvironment
    {
        struct Layer { public Transform t; public Vector2 basePos; public float px, py; }
        struct Fog { public SpriteRenderer sr; public float speed, offset, baseX; }
        struct Firefly { public SpriteRenderer sr; public Vector2 home; public float phase, fx, fy, ax, ay; }
        struct Blink { public SpriteRenderer sr; public Color color; public float baseA, phase, speed, depth; }
        struct Lamp { public SpriteRenderer glow, halo; public Color color; public float seed, strength; }
        struct Bob { public Transform t; public Vector2 home; public float phase, amp; }

        readonly List<Layer> layers = new List<Layer>();
        readonly List<Fog> fogs = new List<Fog>();
        readonly List<Firefly> fireflies = new List<Firefly>();
        readonly List<Blink> blinks = new List<Blink>();
        readonly List<Lamp> lamps = new List<Lamp>();
        readonly List<Bob> bobs = new List<Bob>();

        Transform root, stageRoot;
        CameraRig cam;
        System.Random rng;
        float moteTimer;
        StageKit kit;
        Color fireflyColor = new Color(1f, 0.86f, 0.42f, 1f), moteColor = new Color(0.8f, 0.92f, 1f, 0.45f);

        public Transform Root => root;
        public PlatformViews Platforms { get; private set; }
        public Transform LeftPortal { get; private set; }
        public Transform RightPortal { get; private set; }
        public float Wind { get; private set; }
        public StageKit Kit => kit;

        public const float GoalX = 16.9f;

        static readonly int WindId = Shader.PropertyToID("_SF_Wind");
        static readonly int Push0Id = Shader.PropertyToID("_SF_Push0");
        static readonly int Push1Id = Shader.PropertyToID("_SF_Push1");

        float R() => (float)rng.NextDouble();
        float Range(float a, float b) => a + (b - a) * R();
        T Pick<T>(IList<T> arr) => arr[rng.Next(arr.Count)];

        /// <summary>Night falls off with distance (the title screen's backdrop still uses it).</summary>
        public static Color DepthTint(float depth)
        {
            float b = Mathf.Lerp(1f, 0.42f, depth);
            return new Color(b * Mathf.Lerp(1f, 0.86f, depth), b * Mathf.Lerp(1f, 0.94f, depth), b, 1f);
        }

        Transform Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : stageRoot, false);
            return go.transform;
        }

        /// <summary>px/py: how much of the camera motion the layer follows (1 = infinitely far, 0 = play plane).</summary>
        Transform AddLayer(string name, Vector2 basePos, float px, float py)
        {
            var t = Group(name);
            layers.Add(new Layer { t = t, basePos = basePos, px = px, py = py });
            return t;
        }

        SpriteRenderer Put(Transform parent, StageKit.Piece piece, Vector2 pos, float scale, int order, bool flip = false, Color? tint = null)
        {
            var sr = Art.MakeSprite(piece.Name, parent, piece.Sprite, order, DesignArt.SpriteMat, tint ?? Color.white);
            sr.transform.localPosition = pos;
            sr.transform.localScale = new Vector3(flip ? -scale : scale, scale, 1f);
            return sr;
        }

        /// <summary>Requires EnvironmentArt.Begin/End (goal frame, fog band) to have run. The stage look follows with <see cref="ApplyStage"/>.</summary>
        public void Build(Transform parent, CameraRig cameraRig)
        {
            DesignArt.Load();
            cam = cameraRig;
            root = new GameObject("Environment").transform;
            root.SetParent(parent, false);

            var goals = new GameObject("Goals").transform;
            goals.SetParent(root, false);
            LeftPortal = BuildGoal(goals, -GoalX, 1f);
            RightPortal = BuildGoal(goals, GoalX, -1f);

            Platforms = new PlatformViews();
            Platforms.Build(root);
        }

        /// <summary>Swap the whole scenery for a stage's (behind a covering screen, or at the start).</summary>
        public void ApplyStage(StageTheme theme)
        {
            var next = StageKit.For(theme);
            if (next == kit && stageRoot != null) return;
            if (stageRoot != null) Object.Destroy(stageRoot.gameObject);
            layers.Clear(); fogs.Clear(); fireflies.Clear(); blinks.Clear(); lamps.Clear(); bobs.Clear();
            var old = kit;
            kit = next;
            rng = new System.Random(kit.Id.Length * 7919 + kit.Id[0] * 131);
            stageRoot = new GameObject("Stage " + kit.Id).transform;
            stageRoot.SetParent(root, false);

            moteColor = Color.Lerp(kit.Haze, Color.white, 0.65f).WithAlpha(0.4f);
            BuildSky();
            BuildBackdrop();
            BuildDrift();
            BuildGround();
            BuildFrame();
            BuildExtras();
            Game.I?.Grade?.Adopt(stageRoot);
            if (old != null && old != kit) old.Unload();
        }

        // ------------------------------------------------------------------ Himmel und gemalte Kulisse

        void BuildSky()
        {
            var sky = AddLayer("Sky", new Vector2(0f, 3f), 1f, 1f);
            var fill = Art.MakeSprite("Sky Fill", sky, DesignArt.Block, -1000, DesignArt.SpriteMat, kit.Sky);
            fill.transform.localScale = new Vector3(140f, 50f, 1f);
        }

        // the plate is drawn at the scale of the design sheet; its painted ground line sits just above the pitch
        const float PlateY = 0.3f;

        void BuildBackdrop()
        {
            // far away sideways, but only a third of the camera's rise: when jumping, the scenery sinks a little
            // instead of lifting off the pitch
            var plate = AddLayer("Backdrop", new Vector2(0f, PlateY), 0.88f, 0.35f);
            if (kit.Plate != null) Put(plate, kit.Plate, Vector2.zero, 1f, -950);
            // under the picture: the valley colour, so a high camera never sees the sky below it
            float below = kit.Plate != null ? kit.Plate.BottomH : 2.5f;
            var valley = Art.MakeSprite("Valley Floor", plate, DesignArt.Block, -951, DesignArt.SpriteMat, kit.Valley);
            valley.transform.localPosition = new Vector3(0f, -below - 4f + 0.05f, 0f);
            valley.transform.localScale = new Vector3(60f, 8f, 1f);

            AddFog("Fog Backdrop", -930, new Vector2(0f, PlateY + 0.3f), 0.88f, 0.35f, 1.4f, 0.1f, 0.07f, Color.Lerp(kit.Haze, Color.white, 0.25f));
        }

        /// <summary>Very large pictures (towers, furnaces) are drawn smaller so the rows stay balanced.</summary>
        static float PropScale(StageKit.Piece p) => Mathf.Clamp(2.4f / Mathf.Max(0.5f, p.TopH), 0.6f, 1.15f);

        // ------------------------------------------------------------------ schwebende Brocken (Sternengarten, Eklipse)

        void BuildDrift()
        {
            // nur wo die Vorlage selbst schwebende Brocken zeigt; wenige, klein und im Dunst des Himmels
            var sky = kit.PropsTagged("sky");
            if (sky.Count == 0) return;
            var g = AddLayer("Drifting Rocks", new Vector2(0f, 4.2f), 0.8f, 0.5f);
            Color tint = Color.Lerp(Color.white, kit.Haze * 1.4f, 0.55f) * 0.78f; tint.a = 1f;
            for (float x = -16f; x < 16f; x += Range(6f, 10f))
            {
                var p = Pick(sky);
                var sr = Put(g, p, new Vector2(x, Range(1.5f, 4.5f)), Range(0.22f, 0.36f), -900, R() > 0.5f, tint);
                bobs.Add(new Bob { t = sr.transform, home = sr.transform.localPosition, phase = R() * 10f, amp = Range(0.08f, 0.16f) });
            }
        }

        // ------------------------------------------------------------------ Boden (weltfest)

        void BuildGround()
        {
            var ground = Group("Ground");
            if (kit.Ground != null)
            {
                var wall = Art.MakeSprite("Wall", ground, kit.Ground.Sprite, -110, DesignArt.SpriteMat);
                wall.drawMode = SpriteDrawMode.Tiled;
                wall.size = new Vector2(64f, wall.sprite.bounds.size.y);
                wall.transform.localPosition = new Vector3(-32f, 0f, 0f);
                float wallBottom = -kit.Ground.BottomH;
                var deep = Art.MakeSprite("Deep", ground, DesignArt.Block, -111, DesignArt.SpriteMat, kit.Deep);
                deep.transform.localPosition = new Vector3(0f, wallBottom - 4f + 0.02f, 0f);
                deep.transform.localScale = new Vector3(64f, 8f, 1f);
            }
            if (kit.Id == "mondlicht")
            {
                // kurze Grasbüschel im Wind an der Hinterkante des Rasens
                var back = new FoliageLayer();
                var grass = DesignArt.Plants("grass_b", "grass_d", "grass_e", "grass_f");
                var extra = DesignArt.Plants("fern", "grass_a");
                for (float x = -26f; x < 26f; x += Range(0.9f, 2.2f))
                {
                    bool big = R() < 0.12f;
                    back.Add(big ? extra[rng.Next(extra.Length)] : grass[rng.Next(grass.Length)], new Vector2(x, Range(0.05f, 0.09f)),
                        big ? Range(0.32f, 0.42f) : Range(0.24f, 0.36f), Color.white, 1f, 1f, R() > 0.5f);
                }
                back.Build(ground, "Back Grass", -95, DesignArt.PlantMaterial("SF Design Pitch Grass", 1f));
            }
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

        // ------------------------------------------------------------------ Arenaenden

        void BuildFrame()
        {
            var g = Group("Frame");
            var frame = kit.PropsTagged("frame");
            for (int side = -1; side <= 1; side += 2)
            {
                if (frame.Count > 0)
                {
                    var p = frame[side < 0 ? 0 : frame.Count - 1];
                    float s = 1.05f * PropScale(p);
                    var sr = Put(g, p, new Vector2(side * 19.7f, 0f), s, -106, side < 0);
                    AddPieceLights(g, p, sr.transform.localPosition, s, side < 0, -105, 1f);
                }
                if (kit.End != null) Put(g, kit.End, new Vector2(side * 18.25f, 0f), 1f, -103, side < 0);
            }
        }

        // ------------------------------------------------------------------ Lichter in der gemalten Kulisse

        struct PlateLight { public float x, y, size, alpha, speed; public Color c; public bool lamp; }

        static PlateLight L(float x, float y, float size, string hex, float alpha = 0.3f, bool lamp = false, float speed = 0.5f)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return new PlateLight { x = x, y = y, size = size, c = c, alpha = alpha, lamp = lamp, speed = speed };
        }

        static readonly Dictionary<string, PlateLight[]> plateLights = new Dictionary<string, PlateLight[]>
        {
            ["mondlicht"] = new[] { L(1010, 70, 7f, "#8CB4FF", 0.12f, false, 0.25f), L(1010, 70, 2.6f, "#DDE8FF", 0.14f, false, 0.4f),
                L(1400, 195, 1.1f, "#FFB04A", 1f, true), L(1505, 215, 1.1f, "#FFB04A", 1f, true), L(1370, 350, 1f, "#FFB04A", 1f, true), L(795, 285, 0.9f, "#FFB04A", 1f, true) },
            ["bernstein"] = new[] { L(1190, 110, 9f, "#FFC060", 0.1f, false, 0.2f), L(700, 60, 7f, "#FFB050", 0.08f, false, 0.25f), L(1450, 90, 6f, "#FFD080", 0.08f, false, 0.3f) },
            ["regen"] = new[] { L(1105, 115, 0.9f, "#FFC060", 0.8f, true), L(452, 340, 0.8f, "#FFC060", 0.8f, true), L(1480, 145, 0.8f, "#FFC060", 0.8f, true),
                L(660, 178, 0.6f, "#FFC060", 0.8f, true), L(600, 355, 0.6f, "#FFC060", 0.8f, true), L(222, 160, 0.6f, "#FFC060", 0.8f, true), L(965, 40, 4f, "#9FC8FF", 0.08f, false, 0.9f) },
            ["grotte"] = new[] { L(1100, 60, 6f, "#50FFE0", 0.08f, false, 0.3f), L(855, 145, 1f, "#60FFE0", 0.35f), L(1415, 290, 1.2f, "#60FFE0", 0.35f), L(645, 190, 1f, "#60FFE0", 0.35f),
                L(620, 140, 1.2f, "#B070FF", 0.3f), L(1150, 165, 1.2f, "#B070FF", 0.3f), L(500, 360, 3f, "#40E0D0", 0.14f, false, 0.3f) },
            ["glut"] = new[] { L(455, 110, 2.2f, "#FF7A20", 0.3f, false, 0.6f), L(760, 230, 2.6f, "#FF7A20", 0.3f, false, 0.6f), L(1195, 300, 3.2f, "#FF8A30", 0.35f, false, 0.5f),
                L(880, 240, 1.6f, "#FF7A20", 0.25f, false, 0.7f), L(1200, 180, 3f, "#FF9040", 0.2f, false, 0.4f), L(1000, 90, 1.2f, "#FF7A20", 0.25f, false, 0.8f) },
            ["frost"] = new[] { L(650, 40, 9f, "#50FFB0", 0.07f, false, 0.15f), L(1000, 70, 8f, "#40E8C0", 0.07f, false, 0.12f), L(1300, 40, 8f, "#60FFB0", 0.06f, false, 0.18f) },
            ["stern"] = new[] { L(535, 60, 0.8f, "#FFE8A0", 0.35f, false, 1.2f), L(915, 125, 0.8f, "#FFE8A0", 0.35f, false, 1.4f), L(1485, 60, 0.8f, "#FFE8A0", 0.35f, false, 1.1f),
                L(775, 330, 0.6f, "#FFE8A0", 0.3f, false, 1.6f), L(1245, 95, 1.6f, "#FFD070", 0.3f, true), L(850, 60, 8f, "#B090FF", 0.06f, false, 0.2f) },
            ["eklipse"] = new[] { L(975, 100, 6f, "#FF2040", 0.14f, false, 0.3f), L(975, 100, 3.4f, "#FF4050", 0.12f, false, 0.5f),
                L(340, 45, 1.4f, "#FF7080", 1f, true), L(1395, 40, 1.4f, "#FF7080", 1f, true) },
        };

        void BuildExtras()
        {
            var plate = layers.Find(l => l.t.name == "Backdrop").t;
            if (plateLights.TryGetValue(kit.Id, out var list))
                foreach (var l in list)
                {
                    Vector2 at = kit.PlateLocal(l.x, l.y);
                    // sizes were set for a smaller plate: the picture is now shown at the design sheet's own scale
                    float size = l.size * 1.2f;
                    if (l.lamp) AddLamp(plate, at, size, l.c, 0.8f * l.alpha);
                    else AddBlink(plate, at, size, l.c, l.alpha, -948, l.speed, 0.6f);
                }
            // glow worms and fireflies where the stage has them
            int flies = kit.Id == "mondlicht" ? 26 : kit.Id == "grotte" ? 22 : kit.Id == "bernstein" ? 12 : 0;
            fireflyColor = kit.Id == "grotte" ? new Color(0.45f, 1f, 0.9f) : kit.Id == "bernstein" ? new Color(1f, 0.75f, 0.35f) : new Color(1f, 0.86f, 0.42f);
            for (int i = 0; i < flies; i++)
            {
                var sr = Art.MakeSprite("Firefly", stageRoot, Art.SoftGlow, -300, Art.SpriteGlowMat, fireflyColor.WithAlpha(0f));
                sr.transform.localScale = Vector3.one * Range(0.1f, 0.2f);
                fireflies.Add(new Firefly
                {
                    sr = sr, home = new Vector2(Range(-20f, 20f), Range(0.4f, 5.5f)), phase = R() * 50f,
                    fx = Range(0.12f, 0.3f), fy = Range(0.15f, 0.35f), ax = Range(0.6f, 1.8f), ay = Range(0.3f, 0.9f),
                });
            }

            if (kit.Id == "mondlicht")
            {
                // dunkles Laub ganz außen im Vordergrund
                var fg = AddLayer("Foreground", Vector2.zero, -0.35f, -0.2f);
                var leaves = new FoliageLayer();
                var kinds = DesignArt.Plants("bush_a", "bush_b", "bush_c", "fern");
                foreach (float x in new[] { -25f, 25f })
                    leaves.Add(kinds[rng.Next(kinds.Length)], new Vector2(x + Range(-1.2f, 1.2f), Range(-2.9f, -2.6f)), Range(1.8f, 2.4f),
                        new Color(0.06f, 0.12f, 0.15f), 1f, 0f, R() > 0.5f, Range(-8f, 8f));
                leaves.Build(fg, "Foreground Leaves", 600, DesignArt.PlantMaterial("SF Design Foreground", 0.35f));
            }
        }

        /// <summary>The picture's own light spots (lamp flames, crystals, lava): glow that breathes or flickers.</summary>
        void AddPieceLights(Transform parent, StageKit.Piece p, Vector2 pos, float scale, bool flip, int order, float strength)
        {
            foreach (var l in p.Lights)
            {
                Vector2 at = pos + new Vector2((flip ? -1f : 1f) * l.Pos.x * scale, l.Pos.y * scale);
                AddLamp(parent, at, (l.Radius * 2.2f + 0.35f) * scale / 0.8f, l.Color, strength);
            }
        }

        void AddLamp(Transform parent, Vector2 at, float size, Color color, float strength)
        {
            var halo = Art.MakeSprite("Lamp Halo", parent, Art.SoftGlow, -334, Art.SpriteAddMat, color.WithAlpha(0f));
            halo.transform.localPosition = at;
            halo.transform.localScale = Vector3.one * 3.2f * size;
            var glow = Art.MakeSprite("Lamp Glow", parent, Art.SoftGlow, -322, Art.SpriteGlowMat, color.WithAlpha(0f));
            glow.transform.localPosition = at;
            glow.transform.localScale = Vector3.one * 0.55f * size;
            lamps.Add(new Lamp { glow = glow, halo = halo, color = color, seed = R() * 50f, strength = strength });
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

        void AddBlink(Transform parent, Vector2 pos, float size, Color color, float alpha, int order, float speed, float depth)
        {
            var sr = Art.MakeSprite("Light", parent, Art.SoftGlow, order, Art.SpriteAddMat, color.WithAlpha(alpha));
            sr.transform.localPosition = pos;
            sr.transform.localScale = Vector3.one * size;
            blinks.Add(new Blink { sr = sr, color = color, baseA = alpha, phase = R() * 20f, speed = speed, depth = depth });
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

            // Sterne funkeln, Mond, Lava und Polarlicht atmen
            for (int i = 0; i < blinks.Count; i++)
            {
                var b = blinks[i];
                float f = 1f - b.depth + b.depth * Mathf.PerlinNoise(time * b.speed, b.phase);
                b.sr.color = b.color.WithAlpha(b.baseA * f);
            }

            // Laternen, Fenster, Glut flackern
            for (int i = 0; i < lamps.Count; i++)
            {
                var l = lamps[i];
                float flicker = 0.8f + 0.2f * Mathf.PerlinNoise(time * 3.3f, l.seed) + 0.05f * Mathf.Sin(time * 19f + l.seed);
                l.glow.color = l.color.WithAlpha(0.32f * flicker * l.strength);
                l.halo.color = l.color.WithAlpha(0.16f * flicker * l.strength);
            }

            // hanging buckets sway, drifting rocks bob
            for (int i = 0; i < bobs.Count; i++)
            {
                var b = bobs[i];
                if (b.amp <= 0f) continue;
                b.t.localPosition = b.home + new Vector2(Mathf.Sin(time * 0.4f + b.phase) * b.amp * 0.4f, Mathf.Sin(time * 0.6f + b.phase * 1.7f) * b.amp);
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
                f.sr.color = fireflyColor.WithAlpha(blink * 0.75f);
            }

            // Staub und Pollen im Licht
            var fx = FxSystem.I;
            moteTimer -= dt;
            while (moteTimer <= 0f && fx != null)
            {
                moteTimer += 0.3f;
                Rect view = cam.ViewRect;
                Vector2 p = new Vector2(view.xMin + Random.value * view.width, view.yMin + 1.5f + Random.value * (view.height - 1.5f));
                fx.Spawn(FxLayer.Back, true, Art.CellDot, p,
                    new Vector2(Wind * 0.25f + (Random.value - 0.5f) * 0.12f, 0.05f + Random.value * 0.1f),
                    Random.Range(5f, 9f), Random.Range(0.02f, 0.04f), Random.Range(0.02f, 0.035f), moteColor, moteColor, 1.8f, 0f, -0.01f, 0f, 0f, false, true);
            }
        }
    }
}
