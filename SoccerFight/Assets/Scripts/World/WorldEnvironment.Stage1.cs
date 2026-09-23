using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SoccerFight
{
    /// <summary>
    /// Die Kulisse von Stage 1 (Mondlicht-Ruinen nach dem Referenzbild): zwölf Ebenen, jede mit eigener
    /// Parallaxe — Himmel, Mond mit Wolkenbank, Berge, Klippen mit Aquädukt und Wasserfall, Waldtal mit
    /// Fluss, nahe Baumgruppen, Rahmenbäume, Fackelruinen, Büsche, Wiese mit Quadermauer und dunkle
    /// Blätter im Vordergrund. Dazu bewegtes Detail: ziehende Wolken, fließendes Wasser, glitzernder
    /// Fluss, pulsierende Kristalle, flackernde Fackeln mit Funken, Pflanzen im Wind.
    /// </summary>
    public sealed partial class WorldEnvironment
    {
        struct Torch { public Transform at; public SpriteRenderer flame, glow, halo; public float seed; }
        struct Drift { public Transform t; public float speed, baseX, sway; }
        struct Glint { public SpriteRenderer sr; public float phase, speed; }

        static readonly Color StageOneFirefly = new Color(1f, 0.82f, 0.42f, 1f);
        static readonly Color CrystalBlue = new Color(0.45f, 0.75f, 1f, 1f);
        static readonly Color TorchColor = new Color(1f, 0.58f, 0.22f, 1f);

        readonly List<Torch> torches = new List<Torch>();
        readonly List<Drift> stageOneClouds = new List<Drift>();
        readonly List<Glint> glints = new List<Glint>();
        readonly List<Renderer> falls = new List<Renderer>();
        Transform stageOneGround;
        bool stageOneShown;
        float emberTimer;

        Material StageOnePlants(string name, float fog, float wind)
        {
            var m = FoliageLayer.MakeMaterial(name, fog, wind);
            m.mainTexture = Stage1Art.PlantAtlas;
            return m;
        }

        void BuildStageOne()
        {
            // ---- Himmel, Mond, Wolken
            var sky = AddLayer("S1 Sky", Vector2.zero, 1f, 1f);
            Art.MakeSprite("Gradient", sky, Stage1Art.Sky, -1000).transform.localScale = new Vector3(240f, 1f, 1f);

            var moon = AddLayer("S1 Moon", Stage1Art.MoonPos, 0.975f, 0.96f);
            Art.MakeSprite("Sky Wash", moon, Art.SoftGlow, -992, Art.SpriteAddMat, new Color(0.3f, 0.45f, 0.85f, 0.2f)).transform.localScale = Vector3.one * 22f;
            Art.MakeSprite("Halo Wide", moon, Art.SoftGlow, -986, Art.SpriteAddMat, new Color(0.55f, 0.68f, 1f, 0.24f)).transform.localScale = Vector3.one * 7.5f;
            Art.MakeSprite("Halo", moon, Art.SoftGlow, -985, Art.SpriteAddMat, new Color(0.85f, 0.9f, 1f, 0.32f)).transform.localScale = Vector3.one * 3.1f;
            var disc = Art.MakeSprite("Disc", moon, Stage1Art.Moon, -984, Art.SpriteEmissiveMat, Color.white);
            disc.sharedMaterial = Art.MakeSpriteMaterial("SF Moon Stage1", 1.2f, false);

            var clouds = AddLayer("S1 Clouds", Vector2.zero, 0.95f, 0.92f);
            (float x, float y, int kind, float s, int order, float speed)[] cloudSpots =
            {
                (-3.9f, 5.62f, 0, 1f, -980, 0f), (-8.9f, 7.35f, 1, 0.9f, -987, 0.05f), (2.9f, 7.05f, 2, 0.85f, -987, 0.04f),
                (8.2f, 6.25f, 1, 1.05f, -987, 0.06f), (-13.5f, 6.4f, 2, 1f, -987, 0.05f), (13.8f, 7.2f, 0, 0.8f, -987, 0.035f)
            };
            foreach (var cs in cloudSpots)
            {
                var sr = Art.MakeSprite("Cloud", clouds, Stage1Art.Clouds[cs.kind], cs.order);
                sr.transform.localPosition = new Vector3(cs.x, cs.y, 0f);
                sr.transform.localScale = new Vector3(cs.s, cs.s, 1f);
                stageOneClouds.Add(new Drift { t = sr.transform, speed = cs.speed, baseX = cs.x, sway = cs.speed == 0f ? 0.25f : 0f });
            }

            // ---- Berge und Klippen
            var mountains = AddLayer("S1 Mountains", Vector2.zero, 0.9f, 0.86f);
            Art.MakeSprite("Range", mountains, Stage1Art.Mountains, -950);
            AddStageOneFog("S1 Fog Mountains", -945, new Vector2(0f, 3.4f), 0.88f, 0.84f, 1.6f, 0.16f, 0.06f, new Color(0.42f, 0.56f, 0.82f));

            var cliffs = AddLayer("S1 Cliffs", Vector2.zero, 0.78f, 0.72f);
            Art.MakeSprite("Cliffs", cliffs, Stage1Art.Cliffs, -900);
            Fall(cliffs, Stage1Art.FallTop, 0.46f, Stage1Art.FallLength, -898, 1f);
            Fall(cliffs, Stage1Art.CascadeTop, 0.13f, Stage1Art.CascadeLength, -898, 0.8f);
            var foam = Art.MakeSprite("Fall Mist", cliffs, Art.SoftGlow, -897, Art.SpriteAddMat, new Color(0.65f, 0.8f, 1f, 0.2f));
            foam.transform.localPosition = new Vector2(Stage1Art.FallTop.x, 1.95f);
            foam.transform.localScale = new Vector3(2.2f, 1f, 1f);
            foreach (var cr in Stage1Art.CliffCrystals) AddBlink(cliffs, new Vector2(cr.x, cr.y), cr.z, CrystalBlue, 0.4f, -896);
            AddStageOneFog("S1 Fog Cliffs", -880, new Vector2(0f, 2.5f), 0.76f, 0.7f, 1.8f, 0.2f, -0.08f, new Color(0.4f, 0.56f, 0.82f));

            // ---- Waldtal mit Fluss und Brücke
            var valley = AddLayer("S1 Valley", Vector2.zero, 0.6f, 0.54f);
            Art.MakeSprite("Valley", valley, Stage1Art.Valley, -850);
            foreach (var g in Stage1Art.RiverGlints)
            {
                var sr = Art.MakeSprite("Glint", valley, Art.SoftGlow, -849, Art.SpriteAddMat, new Color(0.75f, 0.88f, 1f, 0f));
                sr.transform.localPosition = g;
                sr.transform.localScale = new Vector3(Range(0.35f, 0.6f), 0.05f, 1f);
                glints.Add(new Glint { sr = sr, phase = R() * 10f, speed = Range(0.5f, 1.1f) });
            }
            foreach (var cr in Stage1Art.ValleyCrystals) AddBlink(valley, new Vector2(cr.x, cr.y), cr.z, CrystalBlue, 0.42f, -848);
            AddStageOneFog("S1 Fog River", -845, new Vector2(0f, 1.85f), 0.58f, 0.52f, 1.1f, 0.2f, 0.12f, new Color(0.5f, 0.64f, 0.88f));

            // ---- nahe Baumgruppen mit Säulen
            var near = AddLayer("S1 Near", Vector2.zero, 0.4f, 0.36f);
            Art.MakeSprite("Groves", near, Stage1Art.Near, -700);
            foreach (var cr in Stage1Art.NearCrystals) AddBlink(near, new Vector2(cr.x, cr.y), cr.z, CrystalBlue, 0.45f, -699);
            AddStageOneFog("S1 Fog Low", -690, new Vector2(0f, 0.45f), 0.38f, 0.34f, 1.2f, 0.12f, 0.2f, new Color(0.45f, 0.6f, 0.82f));

            // ---- große Rahmenbäume
            var frame = AddLayer("S1 Frame Trees", Vector2.zero, 0.22f, 0.2f);
            var trees = new FoliageLayer();
            (float x, int kind, float s)[] treeSpots = { (-17.4f, 1, 1.05f), (-8.7f, 0, 1f), (8.5f, 1, 1.02f), (17.6f, 0, 0.95f) };
            foreach (var ts in treeSpots)
                trees.Add(Stage1Art.Trees[ts.kind], new Vector2(ts.x, 0.1f), ts.s, Color.white, 1f, 0f, false);
            trees.Build(frame, "Trees", -620, StageOnePlants("SF S1 Trees", 0f, 0.6f));

            // ---- Fackelruinen und Büsche direkt hinter der Wiese
            var props = AddLayer("S1 Ruins", Vector2.zero, 0.1f, 0.08f);
            var banners = new FoliageLayer();
            // zwischen den Plattformen, so dass keine darüber schwebt (sie wandern mit der Parallaxe knapp einen Meter)
            AddRuin(props, new Vector2(-6.35f, 0.3f), 0.78f, false, -330, banners);
            AddRuin(props, new Vector2(6.45f, 0.3f), 0.78f, true, -330, null);
            banners.Build(props, "Banner", -331, StageOnePlants("SF S1 Banner", 0f, 1f));

            var bushLayer = AddLayer("S1 Bushes", Vector2.zero, 0.05f, 0.04f);
            var bushes = new FoliageLayer();
            for (float x = -25f; x < 25f; x += Range(0.7f, 1.5f))
            {
                if (x > -3.8f && x < 2f && R() < 0.8f) continue;   // in der Mitte bleibt der Blick aufs Wasser frei
                float roll = R();
                if (roll < 0.55f) bushes.Add(Pick(Stage1Art.Bushes), new Vector2(x, Range(0.3f, 0.4f)), Range(0.6f, 1f), Color.white, 1f, 0f, R() > 0.5f);
                else if (roll < 0.8f) bushes.Add(Pick(Stage1Art.Ferns), new Vector2(x, Range(0.32f, 0.4f)), Range(0.8f, 1.2f), Color.white, 1f, 0f, R() > 0.5f);
                else bushes.Add(Pick(Stage1Art.TallGrass), new Vector2(x, Range(0.32f, 0.4f)), Range(0.8f, 1.1f), Color.white, 1f, 0f, R() > 0.5f);
            }
            bushes.Build(bushLayer, "Bushes", -300, StageOnePlants("SF S1 Bushes", 0f, 0.8f));

            // ---- Boden (weltfest): Gras an der Hinterkante, Büschel vorn, Ranken über der Mauer, Endruinen
            stageOneGround = new GameObject("Stage 1 Ground").transform;
            stageOneGround.SetParent(root, false);
            var backEdge = new FoliageLayer();
            for (float x = -26f; x < 26f; x += Range(0.09f, 0.22f))
            {
                float roll = R();
                var v = roll < 0.9f ? Pick(Stage1Art.Grass) : Pick(Stage1Art.Flowers);
                float s = roll < 0.9f ? Range(0.6f, 1.05f) : Range(0.6f, 0.85f);
                backEdge.Add(v, new Vector2(x, Range(0.27f, 0.35f)), s, Color.white, 1f, 1f, R() > 0.5f);
            }
            backEdge.Build(stageOneGround, "Back Grass", -95, StageOnePlants("SF S1 Pitch Grass", 0f, 1f));
            var lip = new FoliageLayer();
            for (float x = -26f; x < 26f; x += Range(0.12f, 0.32f))
                lip.Add(Pick(Stage1Art.Grass), new Vector2(x, Range(-0.48f, -0.42f)), Range(0.4f, 0.75f), Color.white, 1f, 0.7f, R() > 0.5f);
            lip.Build(stageOneGround, "Lip Grass", 150, StageOnePlants("SF S1 Lip", 0f, 1f));
            var vines = new FoliageLayer();
            for (float x = -26f; x < 26f; x += Range(0.35f, 1.3f))
                vines.Add(Pick(Stage1Art.Vines), new Vector2(x, Range(-0.5f, -0.44f)), Range(0.8f, 1.4f), Color.white, 1f, 0f, R() > 0.5f);
            vines.Build(stageOneGround, "Wall Vines", -105, StageOnePlants("SF S1 Vines", 0f, 0.7f));
            AddRuin(stageOneGround, new Vector2(-17.4f, 0.3f), 1.15f, false, -320, null);
            AddRuin(stageOneGround, new Vector2(17.4f, 0.3f), 1.15f, true, -320, null);

            // ---- dunkle Blätter im Vordergrund
            var fg = AddLayer("S1 Foreground", Vector2.zero, -0.35f, -0.2f);
            var fronds = new FoliageLayer();
            float[] xs = { -27f, -20.5f, -14f, -8.3f, 8.2f, 14f, 20.5f, 27f };
            foreach (float x in xs)
            {
                int n = 2 + rng.Next(2);
                for (int k = 0; k < n; k++)
                    fronds.Add(Pick(Stage1Art.FgLeaves), new Vector2(x + Range(-1.3f, 1.3f), Range(-2.3f, -2.05f)), Range(1.05f, 1.35f),
                        Color.white, 1f, 0f, R() > 0.5f, Range(-14f, 14f));
            }
            fronds.Build(fg, "Fronds", 600, StageOnePlants("SF S1 Foreground", 0f, 0.5f));
        }

        void ShowStageOneGround(bool on)
        {
            pitchSurface.sprite = on ? Stage1Art.Pitch : EnvironmentArt.PitchTile;
            earthSurface.sprite = on ? Stage1Art.Wall : EnvironmentArt.EarthTile;
        }

        /// <summary>Eine Fackelruine (Sprite gespiegelt für die rechte Seite), mit Flamme, Glut und Lichtschein.</summary>
        void AddRuin(Transform parent, Vector2 pos, float scale, bool flip, int order, FoliageLayer banner)
        {
            var g = new GameObject("Torch Ruin").transform;
            g.SetParent(parent, false);
            g.localPosition = pos;
            g.localScale = new Vector3(flip ? -scale : scale, scale, 1f);
            Art.MakeSprite("Stones", g, Stage1Art.Ruin, order);
            var at = new GameObject("Torch").transform;
            at.SetParent(g, false);
            at.localPosition = Stage1Art.TorchSpot;
            var halo = Art.MakeSprite("Halo", at, Art.SoftGlow, order - 2, Art.SpriteAddMat, TorchColor.WithAlpha(0.2f));
            halo.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            halo.transform.localScale = Vector3.one * 4.2f;
            var flame = Art.MakeSprite("Flame", at, Stage1Art.Flame, order + 2, Art.MakeSpriteMaterial("SF S1 Flame", 1.9f, false), Color.white);
            var glow = Art.MakeSprite("Glow", at, Art.SoftGlow, order + 3, Art.SpriteGlowMat, TorchColor.WithAlpha(0.45f));
            glow.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            glow.transform.localScale = Vector3.one * 0.95f;
            torches.Add(new Torch { at = at, flame = flame, glow = glow, halo = halo, seed = R() * 50f });
            if (banner != null)
                banner.Add(Stage1Art.Banner, pos + Stage1Art.BannerSpot * scale * new Vector2(flip ? -1f : 1f, 1f), scale, Color.white, 1f, 0f, flip);
        }

        void Fall(Transform layer, Vector2 top, float width, float length, int order, float strength)
        {
            var back = Art.MakeMeshMaterial("SF S1 Fall", EnvironmentArt.WaterfallTex, 1f, false, false);
            var front = Art.MakeMeshMaterial("SF S1 Fall Core", EnvironmentArt.WaterfallTex, 1.3f, true, false);
            falls.Add(WaterSheet(layer, "Fall", top, width, length, back, order, new Color(0.62f, 0.76f, 0.98f, 0.55f * strength)));
            falls.Add(WaterSheet(layer, "Fall Core", top, width * 0.65f, length, front, order + 1, new Color(0.75f, 0.86f, 1f, 0.45f * strength)));
        }

        static Renderer WaterSheet(Transform parent, string name, Vector2 top, float width, float length, Material mat, int order, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name };
            float v = length / 4f;
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(top.x - width * 0.6f, top.y - length), new Vector3(top.x + width * 0.6f, top.y - length),
                new Vector3(top.x - width * 0.4f, top.y), new Vector3(top.x + width * 0.4f, top.y)
            });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, v), new Vector2(1f, v) });
            Color l = tint.linear; l.a = tint.a;
            mesh.SetColors(new List<Color> { l, l, l, l });
            mesh.SetTriangles(new List<int> { 0, 2, 3, 0, 3, 1 }, 0);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = order;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return mr;
        }

        void AddStageOneFog(string name, int order, Vector2 basePos, float px, float py, float height, float alpha, float speed, Color color)
        {
            var g = AddLayer(name, basePos, px, py);
            var sr = Art.MakeSprite(name, g, EnvironmentArt.FogBand, order, Art.SpriteMat, color.WithAlpha(alpha));
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(64f, 2f);
            sr.transform.localScale = new Vector3(1f, height / 2f, 1f);
            sr.transform.localPosition = new Vector3(-32f, 0f, 0f);
            fogs.Add(new Fog { sr = sr, speed = speed, offset = R() * 8f, baseX = -32f });
        }

        void UpdateStageOne(float dt, float time, Vector2 camPos)
        {
            // Wolken ziehen; die Bank vor dem Mond wiegt sich nur leicht, damit das Bild so bleibt
            for (int i = 0; i < stageOneClouds.Count; i++)
            {
                var d = stageOneClouds[i];
                Vector3 p = d.t.localPosition;
                if (d.speed > 0f) p.x = Mathf.Repeat(p.x + d.speed * dt + 22f, 44f) - 22f;
                else p.x = d.baseX + Mathf.Sin(time * 0.05f) * d.sway;
                d.t.localPosition = p;
            }

            // Wasser fällt (die Materialien sind nach der Farbanpassung die geklonten)
            for (int i = 0; i < falls.Count; i++)
                falls[i].sharedMaterial.mainTextureOffset = new Vector2(i % 2 == 0 ? 0f : 0.3f, time * (i % 2 == 0 ? 0.5f : 0.85f));

            for (int i = 0; i < glints.Count; i++)
            {
                var g = glints[i];
                float a = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * g.speed + g.phase)), 3f) * 0.35f;
                g.sr.color = new Color(0.75f, 0.88f, 1f, a);
            }

            // Fackeln flackern, Funken steigen auf
            var fx = FxSystem.I;
            emberTimer -= dt;
            bool spark = emberTimer <= 0f;
            if (spark) emberTimer = 0.09f;
            Rect view = cam.ViewRect;
            for (int i = 0; i < torches.Count; i++)
            {
                var t = torches[i];
                float n = Mathf.PerlinNoise(time * 7f, t.seed);
                float n2 = Mathf.PerlinNoise(t.seed, time * 11f);
                t.flame.transform.localScale = new Vector3(0.9f + 0.15f * n2, 0.85f + 0.3f * n, 1f);
                t.flame.transform.localRotation = Quaternion.Euler(0f, 0f, (n2 - 0.5f) * 8f - Wind * 10f);
                t.glow.color = TorchColor.WithAlpha(0.35f + 0.2f * n);
                t.halo.color = TorchColor.WithAlpha(0.16f + 0.07f * n);
                if (!spark || fx == null || !t.at.gameObject.activeInHierarchy) continue;
                Vector2 wp = t.at.position;
                if (!view.Contains(wp) || Random.value > 0.35f) continue;
                Color ec = Color.Lerp(new Color(1f, 0.7f, 0.3f, 0.9f), new Color(1f, 0.9f, 0.5f, 0.9f), Random.value);
                fx.Spawn(FxLayer.Back, true, Art.CellDot, wp + new Vector2((Random.value - 0.5f) * 0.2f, 0.25f),
                    new Vector2((Random.value - 0.5f) * 0.3f + Wind * 0.3f, Random.Range(0.5f, 1.1f)), Random.Range(0.8f, 1.6f),
                    Random.Range(0.025f, 0.045f), 0.005f, ec, ec.WithAlpha(0f), 2.6f, 0.4f, -0.05f, 0f, 0f, false, true);
            }
        }
    }
}
