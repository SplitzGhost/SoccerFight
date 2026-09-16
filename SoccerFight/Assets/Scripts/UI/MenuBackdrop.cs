using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Places the MenuScenery layers and keeps them alive: the stage covers the window at any aspect
    /// ratio, every layer leans with the pointer by its depth, clouds and birds cross the sky, the
    /// islands bob, the waterfall runs, floodlights flicker, fissures breathe, embers, fireflies and
    /// petals drift, and big leaves sway at the screen corners.
    /// </summary>
    public sealed class MenuBackdrop
    {
        sealed class Layer { public RectTransform Rt; public Vector2 Home; public float Depth; }
        sealed class Mote { public RectTransform Rt; public Image Img; public Vector2 Pos, Vel; public float Life, Age, Size, Spin, Phase; public int Kind; public Color Tint; }
        sealed class Drifter { public RectTransform Rt; public float Speed, Y, Phase, Scale; }
        sealed class Glow { public Image Img; public Color Tint; public float Base, Phase, Speed; public bool Flicker; }

        const float CoverW = 2080f, CoverH = 1160f;

        RectTransform root, stage, corners, motes;
        readonly List<Layer> layers = new List<Layer>();
        readonly List<Mote> pool = new List<Mote>();
        readonly List<Drifter> clouds = new List<Drifter>();
        readonly List<Glow> glows = new List<Glow>();
        readonly List<Image> rays = new List<Image>();
        RectTransform islandBig, islandSmall, islandTiny, planet, canopyL, canopyR, fernsL, fernsR;
        RectTransform[] shards;
        RawImage waterfall;
        Image[] birds;
        float[] birdX;
        Vector2 lean;
        float time, fireflyAcc, petalAcc, emberAcc, twinkleAcc, birdTimer;
        public bool Ready { get; private set; }

        public RectTransform Root => root;

        public void Build(RectTransform parent)
        {
            root = UiKit.Node("Backdrop", parent, Vector2.zero, Vector2.zero);
            MenuUi.Stretch(root);
            // a flat base colour in case the art is still missing
            MenuUi.Stretch(UiKit.Img("Base", root, null, new Color(0.24f, 0.13f, 0.36f), Vector2.zero, Vector2.zero).rectTransform);
            stage = UiKit.Node("Stage", root, Vector2.zero, new Vector2(MenuScenery.StageW, MenuScenery.StageH));
            if (MenuScenery.Sky == null) return;
            Ready = true;

            Add(Img("Sky", MenuScenery.Sky, Vector2.zero, new Vector2(MenuScenery.StageW, MenuScenery.StageH)), 0.03f);

            // the sun and its haze sit behind the player
            var sun = UiKit.Node("Sun", stage, MenuScenery.Sun, Vector2.zero);
            Add(sun, 0.08f);
            glows.Add(new Glow { Img = UiKit.Img("Haze", sun, UiArt.Glow, Color.clear, Vector2.zero, new Vector2(1500f, 1000f)), Tint = new Color(1f, 0.78f, 0.55f), Base = 0.35f, Speed = 0.4f });
            glows.Add(new Glow { Img = UiKit.Img("Core", sun, UiArt.Glow, Color.clear, Vector2.zero, new Vector2(520f, 520f)), Tint = new Color(1f, 0.96f, 0.82f), Base = 0.75f, Speed = 0.7f });

            // the cracked planet with its drifting pieces
            planet = UiKit.Node("Planet", stage, MenuScenery.PlanetPos, Vector2.zero);
            Add(planet, 0.1f);
            glows.Add(new Glow { Img = UiKit.Img("Halo", planet, UiArt.Glow, Color.clear, Vector2.zero, new Vector2(900f, 900f)), Tint = new Color(1f, 0.55f, 0.75f), Base = 0.32f, Speed = 0.5f });
            UiKit.Img("Body", planet, MenuScenery.Planet, Color.white, Vector2.zero, new Vector2(520f, 520f));
            glows.Add(new Glow { Img = UiKit.Img("Crack", planet, UiArt.Glow, Color.clear, new Vector2(-60f, -40f), new Vector2(260f, 260f)), Tint = new Color(1f, 0.5f, 0.35f), Base = 0.35f, Speed = 1.4f });
            shards = new RectTransform[MenuScenery.Shards.Length];
            for (int i = 0; i < shards.Length; i++)
                shards[i] = UiKit.Img("Shard" + i, planet, MenuScenery.Shards[i], Color.white, Vector2.zero, new Vector2(96f, 96f)).rectTransform;

            // clouds drift across; some pass in front of the planet
            var cloudLayer = UiKit.Node("Clouds", stage, Vector2.zero, Vector2.zero);
            Add(cloudLayer, 0.14f);
            float[] cy = { 430f, 250f, 540f, 150f, 360f };
            for (int i = 0; i < cy.Length; i++)
            {
                var img = UiKit.Img("Cloud" + i, cloudLayer, MenuScenery.Clouds[i % MenuScenery.Clouds.Length], Color.white.WithAlpha(0.8f - i * 0.08f), new Vector2(-900f + i * 460f, cy[i]), new Vector2(640f, 220f));
                float s = 0.7f + 0.12f * (i % 3);
                clouds.Add(new Drifter { Rt = img.rectTransform, Speed = 9f + i * 3.5f, Y = cy[i], Phase = i * 1.7f, Scale = s });
            }

            // birds
            var birdLayer = UiKit.Node("Birds", stage, Vector2.zero, Vector2.zero);
            Add(birdLayer, 0.18f);
            birds = new Image[4];
            birdX = new float[birds.Length];
            for (int i = 0; i < birds.Length; i++)
            {
                birds[i] = UiKit.Img("Bird" + i, birdLayer, MenuScenery.BirdUp, Color.white, new Vector2(-2000f, 0f), new Vector2(48f, 24f) * (0.8f + 0.1f * i));
                birdX[i] = -2000f;
            }
            birdTimer = 2f;

            Add(Img("Peaks", MenuScenery.Peaks, Vector2.zero, new Vector2(MenuScenery.StageW, 640f)), 0.2f);

            // god rays fanning out of the sun
            var rayRoot = UiKit.Node("Rays", stage, MenuScenery.Sun, Vector2.zero);
            Add(rayRoot, 0.22f);
            float[] angles = { -64f, -38f, -16f, 8f, 30f, 52f, 74f };
            for (int i = 0; i < angles.Length; i++)
            {
                var r = UiKit.Img("Ray" + i, rayRoot, MenuArt.Beam, new Color(1f, 0.92f, 0.75f, 0.1f), Vector2.zero, new Vector2(150f + 60f * (i % 3), 1300f));
                r.rectTransform.pivot = new Vector2(0.5f, 0f);
                r.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
                rays.Add(r);
            }

            Add(Img("Hills", MenuScenery.Hills, new Vector2(0f, -80f), new Vector2(MenuScenery.StageW, 560f)), 0.3f);

            // floating islands; the big one pours a waterfall off its edge
            islandTiny = Island("IslandTiny", MenuScenery.IslandSmall, new Vector2(-1000f, 420f), 0.55f, 0.34f, false);
            islandBig = Island("IslandBig", MenuScenery.IslandBig, new Vector2(-640f, 250f), 1f, 0.4f, true);
            islandSmall = Island("IslandSmall", MenuScenery.IslandSmall, new Vector2(820f, 110f), 1f, 0.46f, false);

            var stadium = Img("Stadium", MenuScenery.Stadium, new Vector2(0f, -10f), new Vector2(MenuScenery.StageW, 640f));
            Add(stadium, 0.5f);
            foreach (var lamp in MenuScenery.Lamps)
            {
                glows.Add(new Glow { Img = UiKit.Img("Lamp", stadium.transform, UiArt.Glow, Color.clear, lamp - new Vector2(0f, -10f), new Vector2(110f, 110f)), Tint = new Color(1f, 0.9f, 0.6f), Base = 0.7f, Speed = 3f + lamp.x * 0.01f, Flicker = true });
            }
            // floodlight cones
            for (int i = 0; i < MenuScenery.Lamps.Count; i += 3)
            {
                var cone = UiKit.Img("Cone", stadium.transform, MenuArt.Beam, new Color(1f, 0.95f, 0.75f, 0.07f), MenuScenery.Lamps[i] - new Vector2(0f, -10f), new Vector2(240f, 700f));
                cone.rectTransform.pivot = new Vector2(0.5f, 0f);
                cone.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 160f + (MenuScenery.Lamps[i].x > 400f ? 25f : -15f));
            }

            var ground = Img("Ground", MenuScenery.Ground, new Vector2(0f, -260f), new Vector2(MenuScenery.StageW, 720f));
            Add(ground, 0.68f);
            foreach (var f in MenuScenery.Fissures)
                glows.Add(new Glow { Img = UiKit.Img("Fissure", ground.transform, UiArt.Glow, Color.clear, f - new Vector2(0f, -260f), new Vector2(120f, 70f)), Tint = new Color(1f, 0.5f, 0.38f), Base = 0.45f, Speed = 1.2f + f.x * 0.002f });
            // crystal clusters on the pitch
            Vector2[] crystals = { new Vector2(-980f, 0f), new Vector2(940f, 10f), new Vector2(1060f, -30f), new Vector2(-1080f, -40f) };
            float[] cs = { 0.9f, 1.05f, 0.7f, 0.65f };
            for (int i = 0; i < crystals.Length; i++)
            {
                Vector2 at = new Vector2(crystals[i].x, MenuScenery.GroundTopAt(crystals[i].x) - 20f + crystals[i].y) - new Vector2(0f, -260f);
                glows.Add(new Glow { Img = UiKit.Img("CrystalGlow", ground.transform, UiArt.Glow, Color.clear, at + new Vector2(0f, 70f * cs[i]), new Vector2(260f, 300f) * cs[i]), Tint = new Color(0.4f, 0.95f, 1f), Base = 0.45f, Speed = 0.9f + i * 0.3f });
                var cr = UiKit.Img("Crystal" + i, ground.transform, MenuScenery.Crystal, Color.white, at, new Vector2(140f, 200f) * cs[i]);
                cr.rectTransform.pivot = new Vector2(0.5f, 0.1f);
                cr.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 6f : -8f));
            }

            // corner foliage lives on the screen corners, not on the stage
            corners = UiKit.Node("Corners", root, Vector2.zero, Vector2.zero);
            MenuUi.Stretch(corners);
            canopyL = Corner("CanopyL", MenuScenery.Canopy, new Vector2(0f, 1f), new Vector2(760f, 560f), new Vector2(0f, 1f), false);
            canopyR = Corner("CanopyR", MenuScenery.Canopy, new Vector2(1f, 1f), new Vector2(760f, 560f), new Vector2(0f, 1f), true);
            fernsL = Corner("FernsL", MenuScenery.Ferns, new Vector2(0f, 0f), new Vector2(720f, 460f), new Vector2(0f, 0f), false);
            fernsR = Corner("FernsR", MenuScenery.Ferns, new Vector2(1f, 0f), new Vector2(720f, 460f), new Vector2(0f, 0f), true);

            motes = UiKit.Node("Motes", root, Vector2.zero, Vector2.zero);
            MenuUi.Stretch(motes);

            // soft corner darkening keeps the widgets readable
            var vg = UiKit.Img("Vignette", root, MenuArt.Vignette, new Color(0.08f, 0.02f, 0.14f, 0.55f), Vector2.zero, Vector2.zero);
            MenuUi.Stretch(vg.rectTransform);
            vg.rectTransform.sizeDelta = new Vector2(260f, 260f);
        }

        Image Img(string name, Sprite sprite, Vector2 pos, Vector2 size) => UiKit.Img(name, stage, sprite, Color.white, pos, size);

        void Add(Image img, float depth) => Add(img.rectTransform, depth);

        void Add(RectTransform rt, float depth) => layers.Add(new Layer { Rt = rt, Home = rt.anchoredPosition, Depth = depth });

        RectTransform Island(string name, Sprite sprite, Vector2 pos, float scale, float depth, bool fall)
        {
            var node = UiKit.Node(name, stage, pos, Vector2.zero);
            node.localScale = Vector3.one * scale;
            Add(node, depth);
            bool big = sprite == MenuScenery.IslandBig;
            float w = big ? 380f : 230f, h = big ? 420f : 260f;
            if (fall)
            {
                Vector2 top = MenuScenery.FallTop;
                waterfall = new GameObject("Waterfall", typeof(RectTransform)).AddComponent<RawImage>();
                var wr = waterfall.rectTransform;
                wr.SetParent(node, false);
                wr.pivot = new Vector2(0.5f, 1f);
                wr.anchoredPosition = top + new Vector2(0f, 4f);
                wr.sizeDelta = new Vector2(34f, 760f);
                waterfall.texture = MenuScenery.Waterfall;
                waterfall.uvRect = new Rect(0f, 0f, 1f, 3f);
                waterfall.color = new Color(1f, 1f, 1f, 0.85f);
                waterfall.raycastTarget = false;
                glows.Add(new Glow { Img = UiKit.Img("Spray", node, UiArt.Glow, Color.clear, top + new Vector2(0f, -8f), new Vector2(90f, 50f)), Tint = new Color(0.85f, 0.95f, 1f), Base = 0.5f, Speed = 4f });
            }
            // centre of the canvas rect relative to the island origin (see MenuScenery.BuildIsland)
            float cyOff = -h * 0.65f + h * 0.5f;
            UiKit.Img("Body", node, sprite, Color.white, new Vector2(0f, cyOff), new Vector2(w, h));
            return node;
        }

        RectTransform Corner(string name, Sprite sprite, Vector2 anchor, Vector2 size, Vector2 pivot, bool mirror)
        {
            var img = UiKit.Img(name, corners, sprite, Color.white, Vector2.zero, size);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            if (mirror) rt.localScale = new Vector3(-1f, 1f, 1f);
            return rt;
        }

        // ------------------------------------------------------------------ update

        /// <param name="aim">pointer, -1..1 on both axes</param>
        /// <param name="zoom">camera push (1 = rest)</param>
        /// <param name="pan">camera pan in stage px (sub pages look a little aside)</param>
        public void Update(float udt, Vector2 aim, float zoom, Vector2 pan)
        {
            if (!Ready) return;
            time += udt;
            Rect r = root.rect;
            float cover = Mathf.Max(r.width / CoverW, r.height / CoverH);
            stage.localScale = Vector3.one * cover * zoom;
            lean = Vector2.Lerp(lean, aim, 1f - Mathf.Exp(-3f * udt));
            Vector2 drift = new Vector2(Mathf.Sin(time * 0.07f) * 14f, Mathf.Sin(time * 0.05f) * 6f);
            foreach (var l in layers)
                l.Rt.anchoredPosition = l.Home + (-lean * new Vector2(60f, 26f) - pan + drift) * l.Depth;

            // islands and planet pieces
            Bob(islandBig, 7f, 0.45f, 0f, 1.2f);
            Bob(islandSmall, 9f, 0.6f, 1.7f, 1.8f);
            Bob(islandTiny, 6f, 0.7f, 3.1f, 2.5f);
            for (int i = 0; i < shards.Length; i++)
            {
                float a = time * (6f + i * 3f) + i * 120f;
                Vector2 c = new Vector2(-250f, -170f) + new Vector2(-40f * i, 30f * i);
                shards[i].anchoredPosition = c + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * 30f, Mathf.Sin(time * 0.8f + i) * 18f);
                shards[i].localRotation = Quaternion.Euler(0f, 0f, time * (14f + i * 9f) * (i % 2 == 0 ? 1f : -1f));
                shards[i].localScale = Vector3.one * (0.9f - i * 0.18f);
            }
            if (waterfall != null)
            {
                var uv = waterfall.uvRect;
                uv.y = Mathf.Repeat(uv.y + udt * 1.6f, 1f);
                waterfall.uvRect = uv;
            }

            // clouds wrap around
            float half = MenuScenery.StageW * 0.5f + 360f;
            foreach (var c in clouds)
            {
                Vector2 p = c.Rt.anchoredPosition;
                p.x -= c.Speed * udt;
                if (p.x < -half) p.x += half * 2f;
                p.y = c.Y + Mathf.Sin(time * 0.2f + c.Phase) * 10f;
                c.Rt.anchoredPosition = p;
                c.Rt.localScale = new Vector3(c.Scale, c.Scale, 1f);
            }

            // light
            for (int i = 0; i < rays.Count; i++)
            {
                float a = 0.06f + 0.05f * Mathf.Sin(time * (0.3f + i * 0.07f) + i * 1.3f);
                rays[i].color = new Color(1f, 0.92f, 0.75f, Mathf.Max(0f, a));
                rays[i].rectTransform.localScale = new Vector3(1f + 0.15f * Mathf.Sin(time * 0.2f + i), 1f, 1f);
            }
            foreach (var g in glows)
            {
                float k = g.Flicker
                    ? (Mathf.PerlinNoise(time * g.Speed, g.Phase + g.Speed) > 0.12f ? 0.85f + 0.15f * Mathf.Sin(time * 20f + g.Speed) : 0.2f)
                    : 0.8f + 0.2f * Mathf.Sin(time * g.Speed + g.Speed * 3f);
                g.Img.color = g.Tint.WithAlpha(g.Base * k);
            }

            // leaves sway from their corners
            Sway(canopyL, 1.4f, 0.45f, 0f);
            Sway(canopyR, 1.2f, 0.4f, 2f);
            Sway(fernsL, 1.8f, 0.6f, 1f);
            Sway(fernsR, 1.6f, 0.55f, 3f);
            float cornerScale = 0.84f * Mathf.Clamp(r.height / 1080f, 0.7f, 1.2f) * Mathf.Clamp(r.width / 1920f, 0.7f, 1.1f);
            canopyL.localScale = new Vector3(cornerScale, cornerScale, 1f);
            canopyR.localScale = new Vector3(-cornerScale, cornerScale, 1f);
            fernsL.localScale = new Vector3(cornerScale, cornerScale, 1f);
            fernsR.localScale = new Vector3(-cornerScale, cornerScale, 1f);

            UpdateBirds(udt);
            UpdateMotes(udt, r, cover * zoom);
        }

        void Bob(RectTransform rt, float amp, float speed, float phase, float tilt)
        {
            rt.anchoredPosition += new Vector2(0f, Mathf.Sin(time * speed + phase) * amp);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * speed * 0.7f + phase) * tilt);
        }

        void Sway(RectTransform rt, float amp, float speed, float phase)
        {
            float gust = 0.6f + 0.4f * Mathf.PerlinNoise(time * 0.3f, phase);
            float s = Mathf.Sign(rt.localScale.x);
            rt.localRotation = Quaternion.Euler(0f, 0f, s * Mathf.Sin(time * speed + phase) * amp * gust);
        }

        void UpdateBirds(float udt)
        {
            birdTimer -= udt;
            if (birdTimer <= 0f)
            {
                birdTimer = Random.Range(7f, 12f);
                float y = Random.Range(180f, 420f);
                for (int i = 0; i < birds.Length; i++)
                {
                    birdX[i] = -1250f - i * 70f;
                    birds[i].rectTransform.anchoredPosition = new Vector2(birdX[i], y + (i % 2 == 0 ? i * 16f : -i * 12f));
                }
            }
            for (int i = 0; i < birds.Length; i++)
            {
                if (birdX[i] > 1300f) continue;
                birdX[i] += udt * (120f + i * 6f);
                var rt = birds[i].rectTransform;
                rt.anchoredPosition = new Vector2(birdX[i], rt.anchoredPosition.y + Mathf.Sin(time * 2f + i) * udt * 8f);
                birds[i].sprite = Mathf.Repeat(time * 4f + i * 0.37f, 1f) < 0.5f ? MenuScenery.BirdUp : MenuScenery.BirdDown;
            }
        }

        // ------------------------------------------------------------------ particles (screen space)

        Mote Take()
        {
            foreach (var m in pool) if (m.Life <= 0f) return m;
            var n = new Mote();
            n.Img = UiKit.Img("Mote", motes, UiArt.Glow, Color.clear, Vector2.zero, Vector2.one * 10f);
            n.Rt = n.Img.rectTransform;
            pool.Add(n);
            return n;
        }

        void UpdateMotes(float udt, Rect r, float stageScale)
        {
            float w = r.width, h = r.height;
            // fireflies near the ground and in the foliage
            fireflyAcc += udt * 3f;
            while (fireflyAcc >= 1f)
            {
                fireflyAcc -= 1f;
                var m = Take();
                m.Kind = 0;
                m.Img.sprite = UiArt.Glow;
                m.Pos = new Vector2(Random.Range(-w * 0.5f, w * 0.5f), Random.Range(-h * 0.5f, -h * 0.05f));
                m.Vel = new Vector2(Random.Range(-12f, 12f), Random.Range(4f, 18f));
                m.Life = Random.Range(4f, 7f); m.Age = 0f; m.Size = Random.Range(14f, 26f); m.Phase = Random.value * 10f;
                m.Tint = Random.value < 0.5f ? new Color(0.85f, 1f, 0.55f) : new Color(1f, 0.85f, 0.5f);
            }
            // petals and leaves blowing across
            petalAcc += udt * 1.2f;
            while (petalAcc >= 1f)
            {
                petalAcc -= 1f;
                var m = Take();
                m.Kind = 1;
                m.Img.sprite = MenuArt.Petal;
                m.Pos = new Vector2(-w * 0.5f - 30f, Random.Range(-h * 0.1f, h * 0.5f));
                m.Vel = new Vector2(Random.Range(60f, 110f), Random.Range(-30f, -8f));
                m.Life = 16f; m.Age = 0f; m.Size = Random.Range(18f, 28f); m.Phase = Random.value * 10f; m.Spin = Random.Range(-120f, 120f);
                m.Tint = Random.value < 0.6f ? new Color(1f, 0.62f, 0.8f) : new Color(0.62f, 0.95f, 0.6f);
            }
            // embers rising out of the fissures
            if (MenuScenery.Fissures.Count > 0)
            {
                emberAcc += udt * 6f;
                while (emberAcc >= 1f)
                {
                    emberAcc -= 1f;
                    var m = Take();
                    m.Kind = 2;
                    m.Img.sprite = UiArt.Glow;
                    Vector2 f = MenuScenery.Fissures[Random.Range(0, MenuScenery.Fissures.Count)];
                    m.Pos = (f + new Vector2(0f, 0f)) * stageScale + new Vector2(Random.Range(-10f, 10f), 0f);
                    m.Vel = new Vector2(Random.Range(-8f, 8f), Random.Range(30f, 70f));
                    m.Life = Random.Range(1.2f, 2.4f); m.Age = 0f; m.Size = Random.Range(8f, 14f); m.Phase = Random.value * 10f;
                    m.Tint = Color.Lerp(new Color(1f, 0.55f, 0.35f), new Color(1f, 0.9f, 0.55f), Random.value);
                }
            }
            // twinkles in the sky
            twinkleAcc += udt * 1.5f;
            while (twinkleAcc >= 1f)
            {
                twinkleAcc -= 1f;
                var m = Take();
                m.Kind = 3;
                m.Img.sprite = MenuArt.Sparkle;
                m.Pos = new Vector2(Random.Range(-w * 0.5f, w * 0.5f), Random.Range(h * 0.15f, h * 0.5f));
                m.Vel = Vector2.zero;
                m.Life = Random.Range(0.8f, 1.6f); m.Age = 0f; m.Size = Random.Range(16f, 30f); m.Phase = 0f; m.Spin = 40f;
                m.Tint = Color.white;
            }

            foreach (var m in pool)
            {
                if (m.Life <= 0f) continue;
                m.Age += udt;
                if (m.Age >= m.Life) { m.Life = 0f; m.Img.color = Color.clear; continue; }
                float u = m.Age / m.Life;
                float a;
                switch (m.Kind)
                {
                    case 0:
                        m.Vel += new Vector2(Mathf.Sin(time * 1.3f + m.Phase) * 10f, Mathf.Cos(time * 0.9f + m.Phase) * 6f) * udt;
                        a = MathUtil.Bump(u) * (0.55f + 0.45f * Mathf.Sin(time * 5f + m.Phase));
                        break;
                    case 1:
                        m.Vel.y += Mathf.Sin(time * 1.5f + m.Phase) * 30f * udt;
                        a = Mathf.Clamp01(m.Age * 2f) * (m.Pos.x > w * 0.5f + 40f ? 0f : 0.9f);
                        if (m.Pos.x > w * 0.5f + 40f) m.Life = 0f;
                        break;
                    case 2:
                        m.Vel.x += Mathf.Sin(time * 3f + m.Phase) * 20f * udt;
                        a = (1f - u) * MathUtil.Smooth01(u * 8f);
                        break;
                    default:
                        a = MathUtil.Bump(u) * 0.85f;
                        break;
                }
                m.Pos += m.Vel * udt;
                m.Rt.anchoredPosition = m.Pos;
                float s = m.Size * (m.Kind == 3 ? MathUtil.Bump(u) : 1f);
                m.Rt.sizeDelta = m.Kind == 1 ? new Vector2(s, s * 0.6f) : new Vector2(s, s);
                m.Rt.localRotation = Quaternion.Euler(0f, 0f, m.Kind == 1 ? m.Age * m.Spin + Mathf.Sin(time * 3f + m.Phase) * 30f : m.Age * m.Spin);
                m.Img.color = m.Tint.WithAlpha(Mathf.Clamp01(a));
            }
        }
    }
}
