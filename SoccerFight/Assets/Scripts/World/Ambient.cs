using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SoccerFight
{
    /// <summary>
    /// Small living details that make the backdrop breathe: drifting clouds, twinkling stars, bats
    /// crossing the moon, a flowing waterfall with mist, tumbling leaves, spirit lights floating in
    /// the arcade and swinging lanterns with moths.
    /// </summary>
    public sealed class Ambient
    {
        struct Cloud { public Transform t; public float speed; }
        struct Twinkle { public SpriteRenderer sr; public float phase, speed, baseA; }
        sealed class Bat { public Transform t; public SpriteRenderer sr; public Vector2 pos; public float vx, phase, bob, scale; public bool active; }
        sealed class FallingLeaf
        {
            public SpriteRenderer sr;
            public Vector2 pos;
            public float fall, phase, f1, f2, f3, age, delay, scale;
            public bool front;
        }
        struct Spirit { public SpriteRenderer glow, core; public Vector2 home; public float phase; }
        struct Lamp { public Transform pivot; public SpriteRenderer glow, halo; public Transform[] moths; public float seed, chain; }
        struct Puff { public SpriteRenderer sr; public float phase; }

        readonly List<Cloud> clouds = new List<Cloud>();
        readonly List<Twinkle> twinkles = new List<Twinkle>();
        readonly List<Bat> bats = new List<Bat>();
        readonly List<FallingLeaf> leaves = new List<FallingLeaf>();
        readonly List<Spirit> spirits = new List<Spirit>();
        readonly List<Lamp> lamps = new List<Lamp>();
        readonly List<Puff> mist = new List<Puff>();

        Transform batGroup;
        Material waterBack, waterFront;
        Vector2 waterBase;
        float waterShade = 1f;     // the far waterfall sits in a darkened depth layer
        float batTimer = 5f;

        // ------------------------------------------------------------------ building

        public void BuildSky(Transform stars, Transform cloudGroup, Transform batParent)
        {
            var r = new System.Random(3);
            float R() => (float)r.NextDouble();
            for (int i = 0; i < 42; i++)
            {
                var sr = Art.MakeSprite("Twinkle", stars, Art.SoftGlow, -993, Art.SpriteGlowMat, new Color(0.85f, 0.95f, 1f, 0f));
                sr.transform.localPosition = new Vector3(R() * 34f - 17f, 1.5f + R() * 7f, 0f);
                sr.transform.localScale = Vector3.one * (0.08f + R() * 0.16f);
                twinkles.Add(new Twinkle { sr = sr, phase = R() * 10f, speed = 0.6f + R() * 2.2f, baseA = 0.25f + R() * 0.5f });
            }

            for (int i = 0; i < 6; i++)
            {
                bool inFront = i % 3 == 2;
                var sr = Art.MakeSprite("Cloud", cloudGroup, EnvironmentArt.Clouds[i % EnvironmentArt.Clouds.Length], inFront ? -981 : -987);
                float s = 0.9f + R() * 0.7f;
                sr.transform.localScale = new Vector3(i % 2 == 0 ? s : -s, s, 1f);
                sr.transform.localPosition = new Vector3(R() * 44f - 22f, 4.6f + R() * 3f, 0f);
                sr.color = new Color(1f, 1f, 1f, inFront ? 0.55f : 0.8f);
                clouds.Add(new Cloud { t = sr.transform, speed = 0.05f + R() * 0.12f });
            }

            batGroup = batParent;
            for (int i = 0; i < 7; i++)
            {
                var sr = Art.MakeSprite("Bat", batGroup, EnvironmentArt.BatFrames[0], -978);
                sr.gameObject.SetActive(false);
                bats.Add(new Bat { t = sr.transform, sr = sr });
            }
        }

        public void BuildWaterfall(Transform far, float shade)
        {
            Vector2 top = EnvironmentArt.WaterfallTop;
            float len = Mathf.Max(0.5f, EnvironmentArt.WaterfallLength);
            waterBase = new Vector2(top.x, top.y - len);
            waterShade = shade;
            waterBack = Art.MakeMeshMaterial("SF Waterfall Back", EnvironmentArt.WaterfallTex, 1f, false, false);
            waterFront = Art.MakeMeshMaterial("SF Waterfall Front", EnvironmentArt.WaterfallTex, 1.3f, true, false);
            WaterQuad(far, "Waterfall Back", top, 0.62f, len, waterBack, -899, Shade(new Color(0.55f, 0.8f, 0.86f, 0.55f), shade));
            WaterQuad(far, "Waterfall Front", top, 0.42f, len, waterFront, -898, Shade(new Color(0.7f, 0.92f, 0.96f, 0.5f), shade));
            for (int i = 0; i < 7; i++)
            {
                var sr = Art.MakeSprite("Mist", far, Art.SoftGlow, -897, Art.SpriteMat, new Color(0.8f, 0.94f, 0.97f, 0f));
                mist.Add(new Puff { sr = sr, phase = i / 7f });
            }
            var pool = Art.MakeSprite("Pool Glow", far, Art.SoftGlow, -897, Art.SpriteAddMat, Shade(new Color(0.6f, 0.9f, 0.95f, 0.25f), shade));
            pool.transform.localPosition = waterBase;
            pool.transform.localScale = new Vector3(2.2f, 0.7f, 1f);
        }

        /// <summary>Water still spilling from the broken aqueduct channel (shares the animated water materials).</summary>
        public void BuildAqueductFall(Transform layer, Vector2 top, float length, float shade, int order)
        {
            if (waterBack == null) return;
            WaterQuad(layer, "Aqueduct Fall", top, 0.2f, length, waterBack, order, Shade(new Color(0.55f, 0.8f, 0.86f, 0.5f), shade));
            WaterQuad(layer, "Aqueduct Fall Core", top, 0.12f, length, waterFront, order + 1, Shade(new Color(0.7f, 0.92f, 0.96f, 0.45f), shade));
            var splash = Art.MakeSprite("Aqueduct Splash", layer, Art.SoftGlow, order + 1, Art.SpriteAddMat, Shade(new Color(0.6f, 0.9f, 0.95f, 0.22f), shade));
            splash.transform.localPosition = top - new Vector2(0f, length);
            splash.transform.localScale = new Vector3(1.1f, 0.4f, 1f);
        }

        static Color Shade(Color c, float s) => new Color(c.r * s, c.g * s, c.b * s, c.a);

        static void WaterQuad(Transform parent, string name, Vector2 top, float width, float length, Material mat, int order, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name };
            float v = length / 4f;
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(top.x - width * 0.5f, top.y - length), new Vector3(top.x + width * 0.5f, top.y - length),
                new Vector3(top.x - width * 0.35f, top.y), new Vector3(top.x + width * 0.35f, top.y)
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
        }

        public void BuildRuins(Transform ruins)
        {
            var r = new System.Random(9);
            float R() => (float)r.NextDouble();
            for (int i = 0; i < EnvironmentArt.ArchSpots.Count; i++)
            {
                if (i % 2 == 1) continue;
                Vector2 home = EnvironmentArt.ArchSpots[i] + new Vector2((R() - 0.5f) * 0.5f, R() * 0.4f);
                var glow = Art.MakeSprite("Spirit Glow", ruins, Art.SoftGlow, -695, Art.SpriteGlowMat, new Color(0.6f, 0.95f, 1f, 0f));
                glow.transform.localScale = Vector3.one * 0.55f;
                var core = Art.MakeSprite("Spirit", ruins, Art.Circle, -694, Art.SpriteEmissiveMat, new Color(0.85f, 1f, 1f, 0f));
                core.transform.localScale = Vector3.one * 0.07f;
                spirits.Add(new Spirit { glow = glow, core = core, home = home, phase = R() * 20f });
            }

            foreach (var spot in EnvironmentArt.LanternSpots) AddLantern(ruins, spot, -698, 0.42f, R() * 50f);
        }

        /// <summary>A swinging lantern with a flickering glow and two moths. order = the halo; the rest stacks above it.</summary>
        public void AddLantern(Transform parent, Vector2 spot, int order, float chainLen, float seed)
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
            lamps.Add(new Lamp { pivot = pivot, glow = glow, halo = halo, moths = moths, seed = seed, chain = chainLen });
        }

        public void BuildLeaves(Transform world)
        {
            var r = new System.Random(15);
            float R() => (float)r.NextDouble();
            for (int i = 0; i < 16; i++)
            {
                bool front = i >= 12;
                var sr = Art.MakeSprite("Leaf", world, EnvironmentArt.Leaves[i % EnvironmentArt.Leaves.Length], front ? 620 : -85);
                sr.color = front ? new Color(0.35f, 0.45f, 0.48f, 0.95f) : new Color(0.75f, 0.85f, 0.88f, 0.9f);
                var leaf = new FallingLeaf
                {
                    sr = sr, front = front, scale = front ? 1.8f + R() * 0.6f : 0.8f + R() * 0.4f,
                    phase = R() * 10f, f1 = 0.6f + R() * 0.5f, f2 = 1.2f + R() * 1.1f, f3 = 1.6f + R() * 1.5f,
                    delay = R() * 8f, fall = 0.35f + R() * 0.3f
                };
                sr.gameObject.SetActive(false);
                leaves.Add(leaf);
            }
        }

        // ------------------------------------------------------------------ update

        public void Update(float dt, float t, Vector2 cam, float wind)
        {
            for (int i = 0; i < twinkles.Count; i++)
            {
                var tw = twinkles[i];
                float s = 0.5f + 0.5f * Mathf.Sin(t * tw.speed + tw.phase);
                tw.sr.color = new Color(0.85f, 0.95f, 1f, tw.baseA * s * s);
            }

            for (int i = 0; i < clouds.Count; i++)
            {
                var c = clouds[i];
                Vector3 p = c.t.localPosition;
                p.x += c.speed * dt;
                if (p.x > 24f) p.x -= 48f;
                c.t.localPosition = p;
            }

            UpdateBats(dt, t, cam);
            UpdateWater(dt, t);
            UpdateLeaves(dt, t, cam, wind);

            for (int i = 0; i < spirits.Count; i++)
            {
                var s = spirits[i];
                float ph = s.phase;
                Vector2 pos = s.home + new Vector2(Mathf.Sin(t * 0.37f + ph) * 0.35f, Mathf.Sin(t * 0.61f + ph * 1.3f) * 0.22f);
                float a = Mathf.Clamp01(0.35f + 0.65f * Mathf.Sin(t * 0.23f + ph));
                a *= a;
                s.glow.transform.localPosition = pos;
                s.core.transform.localPosition = pos;
                s.glow.color = new Color(0.6f, 0.95f, 1f, 0.5f * a);
                s.core.color = new Color(0.85f, 1f, 1f, 0.8f * a);
            }

            for (int i = 0; i < lamps.Count; i++)
            {
                var l = lamps[i];
                float swing = Mathf.Sin(t * 1.25f + l.seed) * 3.2f + Mathf.Sin(t * 0.47f + l.seed * 2f) * 1.2f + wind * 6f;
                l.pivot.localRotation = Quaternion.Euler(0f, 0f, swing);
                float flicker = 0.82f + 0.18f * Mathf.PerlinNoise(t * 3.3f, l.seed) + 0.05f * Mathf.Sin(t * 19f + l.seed);
                l.glow.color = Palette.Lantern.WithAlpha(0.5f * flicker);
                l.halo.color = Palette.Lantern.WithAlpha(0.22f * flicker);
                Vector3 center = l.pivot.localPosition + l.pivot.localRotation * new Vector3(0f, -l.chain - 0.27f, 0f);
                for (int m = 0; m < l.moths.Length; m++)
                {
                    float a = t * (2.4f + m * 0.9f) + l.seed + m * 2.1f;
                    l.moths[m].localPosition = center + new Vector3(Mathf.Cos(a) * (0.22f + m * 0.07f), Mathf.Sin(a * 1.7f) * 0.14f + 0.02f, 0f);
                }
            }
        }

        void UpdateBats(float dt, float t, Vector2 cam)
        {
            batTimer -= dt;
            if (batTimer <= 0f)
            {
                batTimer = 16f + Random.value * 22f;
                int n = 3 + Random.Range(0, 4);
                float dir = Random.value > 0.5f ? 1f : -1f;
                Vector2 start = new Vector2(cam.x - dir * 13f, 5.6f + Random.value * 1.8f);
                int spawned = 0;
                foreach (var b in bats)
                {
                    if (b.active || spawned >= n) continue;
                    spawned++;
                    b.active = true;
                    b.pos = start + new Vector2(-dir * Random.value * 2.5f, (Random.value - 0.5f) * 1.2f);
                    b.vx = dir * (2.4f + Random.value * 1.2f);
                    b.phase = Random.value * 3f;
                    b.bob = 0.1f + Random.value * 0.15f;
                    b.scale = 0.38f + Random.value * 0.25f;
                    b.t.gameObject.SetActive(true);
                }
            }

            Vector3 groupPos = batGroup.position;
            foreach (var b in bats)
            {
                if (!b.active) continue;
                b.pos.x += b.vx * dt;
                b.phase += dt * 9f;
                int f = (int)b.phase % 4;
                b.sr.sprite = EnvironmentArt.BatFrames[f == 3 ? 1 : f];
                Vector2 world = b.pos + new Vector2(0f, Mathf.Sin(b.phase * 0.35f) * b.bob);
                b.t.localPosition = new Vector3(world.x - groupPos.x, world.y - groupPos.y, 0f);
                b.t.localScale = new Vector3(b.scale, b.scale, 1f);
                if (Mathf.Abs(b.pos.x - cam.x) > 16f) { b.active = false; b.t.gameObject.SetActive(false); }
            }
        }

        void UpdateWater(float dt, float t)
        {
            if (waterBack == null) return;
            waterBack.mainTextureOffset = new Vector2(0f, t * 0.45f);
            waterFront.mainTextureOffset = new Vector2(0.3f, t * 0.8f);
            for (int i = 0; i < mist.Count; i++)
            {
                var m = mist[i];
                float u = Mathf.Repeat(t / 2.6f + m.phase, 1f);
                float a = Mathf.Sin(u * Mathf.PI) * 0.28f;
                m.sr.transform.localPosition = waterBase + new Vector2(Mathf.Sin(m.phase * 17f) * 0.35f, 0.05f + u * 0.45f);
                float s = Mathf.Lerp(0.5f, 1.5f, u);
                m.sr.transform.localScale = new Vector3(s * 1.3f, s, 1f);
                m.sr.color = new Color(0.8f * waterShade, 0.94f * waterShade, 0.97f * waterShade, a);
            }
        }

        void UpdateLeaves(float dt, float t, Vector2 cam, float wind)
        {
            for (int i = 0; i < leaves.Count; i++)
            {
                var l = leaves[i];
                if (l.delay > 0f)
                {
                    l.delay -= dt;
                    if (l.delay <= 0f)
                    {
                        l.pos = new Vector2(cam.x + (Random.value - 0.5f) * 22f, cam.y + 5.6f + Random.value);
                        l.age = 0f;
                        l.sr.gameObject.SetActive(true);
                    }
                    continue;
                }
                l.age += dt;
                // tumbling flight: sideways pendulum drift, slower fall while gliding, flipping in 3D
                float swingX = Mathf.Sin(t * l.f1 + l.phase);
                l.pos.x += (wind * 0.9f + swingX * 0.55f) * dt;
                l.pos.y -= l.fall * (0.75f + 0.45f * Mathf.Abs(Mathf.Cos(t * l.f1 + l.phase))) * dt;
                float ground = l.front ? cam.y - 6.5f : -0.3f;
                float fade = Mathf.Clamp01(l.age / 0.6f) * Mathf.Clamp01((l.pos.y - ground) / 0.4f);
                Color c = l.sr.color;
                c.a = (l.front ? 0.95f : 0.9f) * fade;
                l.sr.color = c;
                l.sr.transform.position = new Vector3(l.pos.x, l.pos.y, 0f);
                l.sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * l.f2 + l.phase) * 65f + swingX * 20f);
                l.sr.transform.localScale = new Vector3(Mathf.Cos(t * l.f3 + l.phase) * l.scale, l.scale, 1f);
                if (l.pos.y <= ground || Mathf.Abs(l.pos.x - cam.x) > 16f)
                {
                    l.sr.gameObject.SetActive(false);
                    l.delay = 1.5f + Random.value * 6f;
                }
            }
        }
    }
}
