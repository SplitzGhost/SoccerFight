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
    /// The scene (art in MenuScenery): a sunny valley in the Project Rise look. The sun up in the
    /// right corner with its shafts of light, big cartoon clouds drifting, gulls crossing; lavender
    /// mountains far back; green hills and the huge stadium standing on them like a landmark, its
    /// pennants flying and its floodlights twinkling; the meadow with the path leading up to the gate;
    /// and the turf plateau the player stands on (MainMenu keeps it under the boots), its grass and
    /// flowers swaying in the wind. Pollen and butterflies float through the air. Every layer leans
    /// with the pointer by its depth.
    /// </summary>
    public sealed class MenuVista
    {
        public const int Layer = 25;
        public static int Mask => 1 << Layer;

        /// <summary>The vista is composed for a view this tall (the camera's base size).</summary>
        const float ViewHalfH = 4.9f;
        /// <summary>Content reaches this far to each side before the view has to scale up.</summary>
        const float CoverHalfW = 10.4f;
        const float SunRadius = 0.62f;

        sealed class Part { public Transform T; public Vector2 Home; public float Lean; }
        struct Blink { public SpriteRenderer Sr; public Color Color; public float Base, Phase, Speed; }
        sealed class Flag { public Transform T; public float Phase, Speed; }
        sealed class Mote { public SpriteRenderer Sr; public Vector2 Pos, Vel, Home; public float Age, Life, Size, Phase; public int Kind; }
        struct Fog { public SpriteRenderer Sr; public float Speed, Offset; }

        readonly List<Part> parts = new List<Part>();
        readonly List<Blink> blinks = new List<Blink>();
        readonly List<Flag> flags = new List<Flag>();
        readonly List<Mote> pollen = new List<Mote>();
        readonly List<Mote> butterflies = new List<Mote>();
        readonly List<Fog> fogs = new List<Fog>();
        readonly List<SpriteRenderer> rays = new List<SpriteRenderer>();
        readonly List<float> rayAlpha = new List<float>();
        readonly Ambient ambient = new Ambient();

        Transform root, ground;
        System.Random rng;
        Vector2 lean, drift;
        float time, groundY = -3.56f;
        bool built;

        public bool Ready => built;

        float R() => (float)rng.NextDouble();
        float Range(float a, float b) => a + (b - a) * R();
        T Pick<T>(T[] arr) => arr[rng.Next(arr.Length)];

        Transform Group(string name, Vector2 home, float lean)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = home;
            parts.Add(new Part { T = t, Home = home, Lean = lean });
            return t;
        }

        SpriteRenderer Glow(string name, Transform parent, Vector2 pos, float size, Color color, float alpha, int order, float speed = 2f)
        {
            var sr = Art.MakeSprite(name, parent, Art.SoftGlow, order, Art.SpriteAddMat, color.WithAlpha(alpha));
            sr.transform.localPosition = pos;
            sr.transform.localScale = Vector3.one * size;
            blinks.Add(new Blink { Sr = sr, Color = color, Base = alpha, Phase = R() * 20f, Speed = speed });
            return sr;
        }

        void AddFog(Transform parent, float y, float height, float alpha, float speed, int order)
        {
            var sr = Art.MakeSprite("Haze", parent, EnvironmentArt.FogBand, order, Art.SpriteMat, Palette.Fog.WithAlpha(alpha));
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
            if (MenuScenery.Sun == null || MenuScenery.Hills == null || EnvironmentArt.Sky == null) return;

            BuildSky();
            BuildPeaks();
            BuildHills();
            BuildMeadow();
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
            var sky = Group("Sky", new Vector2(0f, -1.6f), 0.01f);
            Art.MakeSprite("Gradient", sky, EnvironmentArt.Sky, -1000).transform.localScale = new Vector3(110f, 1f, 1f);

            // clouds and gulls from the game's own sky (the star twinkles stay hidden by day)
            var twinkles = Group("Twinkles", new Vector2(0f, -1.4f), 0.02f);
            var clouds = Group("Clouds", new Vector2(0f, -4.4f), 0.04f);
            var birds = Group("Birds", Vector2.zero, 0.05f);
            ambient.BuildSky(twinkles, clouds, birds);

            // the sun up in the right corner, its shafts falling into the valley
            var sun = Group("Sun", MenuScenery.SunPos, 0.03f);
            Art.MakeSprite("Halo Wide", sun, Art.SoftGlow, -986, Art.SpriteAddMat, Palette.MoonGlow.WithAlpha(0.12f)).transform.localScale = Vector3.one * 12f;
            Art.MakeSprite("Halo", sun, Art.SoftGlow, -985, Art.SpriteAddMat, Palette.MoonGlow.WithAlpha(0.28f)).transform.localScale = Vector3.one * 3.6f;
            float disc = SunRadius / 0.55f;
            var body = Art.MakeSprite("Disc", sun, MenuScenery.Sun, -983, Art.SpriteEmissiveMat, Color.white);
            body.sharedMaterial = Art.MakeSpriteMaterial("SF Menu Sun", 1f, false);
            body.transform.localScale = Vector3.one * disc;

            float[] angles = { -58f, -46f, -34f, -24f, -14f, -4f, 8f };
            float[] widths = { 1.4f, 2.6f, 1.1f, 2.2f, 1.6f, 2.8f, 1.2f };
            for (int i = 0; i < angles.Length; i++)
            {
                var r = Art.MakeSprite("Ray" + i, sun, Art.LightRay, -760, Art.SpriteAddMat, Palette.MoonGlow.WithAlpha(0f));
                r.transform.localPosition = MathUtil.Dir(angles[i] - 90f) * SunRadius * 0.5f;
                r.transform.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
                r.transform.localScale = new Vector3(widths[i], 1.6f, 1f);
                rays.Add(r);
                rayAlpha.Add(0.06f + 0.025f * (i % 3));
            }
        }

        void BuildPeaks()
        {
            var peaks = Group("Peaks", new Vector2(0f, MenuScenery.PeaksY), 0.08f);
            Art.MakeSprite("Mountains", peaks, MenuScenery.Peaks, -950, Art.HazeMat(0.18f), WorldEnvironment.DepthTint(0.9f));
            AddFog(peaks, -0.2f, 1.4f, 0.14f, 0.06f, -945);
        }

        void BuildHills()
        {
            var hills = Group("Hills", new Vector2(0f, MenuScenery.HillsY), 0.16f);
            Art.MakeSprite("Hills", hills, MenuScenery.Hills, -900, Art.HazeMat(0.12f), WorldEnvironment.DepthTint(0.5f));
            // pennants flying from the stadium's rim, in the colours of the stands
            Color[] cols = { Palette.Seat1, Palette.Seat2, Palette.Seat3 };
            for (int i = 0; i < MenuScenery.StadiumFlags.Count; i++)
            {
                var f = Art.MakeSprite("Pennant", hills, MenuScenery.Pennant, -899, Art.SpriteMat, Color.Lerp(cols[i % cols.Length], Palette.Haze, 0.12f));
                f.transform.localPosition = MenuScenery.StadiumFlags[i];
                flags.Add(new Flag { T = f.transform, Phase = R() * 10f, Speed = Range(2.4f, 3.4f) });
            }
            // the floodlights twinkle faintly even by day
            foreach (var l in MenuScenery.Floodlights)
                Glow("Floodlight", hills, new Vector2(l.x, l.y), l.z, new Color(1f, 0.98f, 0.85f), 0.22f, -898, 3f);
            AddFog(hills, -0.5f, 1.2f, 0.1f, -0.09f, -880);
        }

        void BuildMeadow()
        {
            var meadow = Group("Meadow", new Vector2(0f, MenuScenery.MeadowY), 0.26f);
            Art.MakeSprite("Meadow", meadow, MenuScenery.Meadow, -850, Art.HazeMat(0.03f), Color.white);
            // grass swaying along the far edge of the meadow
            var edge = new FoliageLayer();
            for (float x = -12.5f; x < 12.5f; x += Range(0.15f, 0.4f))
                edge.Add(Pick(FoliageArt.Grass), new Vector2(x, Range(-0.05f, 0.02f)), Range(0.3f, 0.5f), new Color(0.92f, 1f, 0.92f, 1f), 1f, 0f, R() > 0.5f);
            edge.Build(meadow, "Meadow Grass", -849, FoliageLayer.MakeMaterial("SF Menu Meadow Grass", 0.05f, 0.9f));
        }

        void BuildGround()
        {
            // pinned under the player's boots every frame (PlaceGround); it does not lean
            ground = new GameObject("Plateau").transform;
            ground.SetParent(root, false);
            ground.localPosition = new Vector3(0f, groundY, 0f);
            Art.MakeSprite("Plateau", ground, MenuScenery.Ground, -600, Art.SpriteMat, Color.white);

            // grass, clover and flowers along the turf; the middle stays clear where the player stands
            var plants = new FoliageLayer();
            for (float x = -12f; x < 12f; x += Range(0.12f, 0.35f))
            {
                if (Mathf.Abs(x) < 1.6f) continue;
                float v = Range(0.02f, MenuScenery.GroundBand - 0.06f);
                float y = MenuScenery.GroundTop(x) - v;
                float roll = R();
                float grow = 0.7f + 0.6f * v / MenuScenery.GroundBand;
                if (roll < 0.62f) plants.Add(Pick(FoliageArt.Grass), new Vector2(x, y), Range(0.4f, 0.7f) * grow, Color.white, 1f, 0f, R() > 0.5f);
                else if (roll < 0.82f) plants.Add(Pick(FoliageArt.Clover), new Vector2(x, y), Range(0.6f, 0.9f) * grow, Color.white, 1f, 0f, R() > 0.5f);
                else plants.Add(Pick(FoliageArt.Flowers), new Vector2(x, y), Range(0.5f, 0.75f) * grow, Color.white, 1f, 0f, R() > 0.5f);
            }
            plants.Build(ground, "Plants", -594, FoliageLayer.MakeMaterial("SF Menu Plants", 0f, 1f));
        }

        void BuildLife()
        {
            // pollen and dandelion fluff drifting through the sunshine, some right in front of the lens
            for (int i = 0; i < 44; i++)
            {
                bool front = i < 7;
                var sr = Art.MakeSprite("Pollen", root, Art.SoftGlow, front ? 611 : -570, Art.SpriteMat, Palette.Firefly.WithAlpha(0f));
                var m = new Mote { Sr = sr, Kind = front ? 1 : 0, Phase = R() * 10f };
                Respawn(m, true);
                pollen.Add(m);
            }
            // butterflies fluttering over the meadow
            Color[] wings = { new Color(1f, 0.82f, 0.3f), new Color(1f, 0.55f, 0.7f), new Color(0.55f, 0.8f, 1f), Color.white };
            for (int i = 0; i < 5; i++)
            {
                var sr = Art.MakeSprite("Butterfly", root, MenuScenery.Butterfly, -580, Art.SpriteMat, wings[i % wings.Length]);
                butterflies.Add(new Mote { Sr = sr, Home = new Vector2(Range(-9f, 9f), Range(-2.9f, -1.2f)), Phase = R() * 20f, Size = Range(0.9f, 1.3f) });
            }
        }

        void Respawn(Mote m, bool anywhere)
        {
            bool front = m.Kind == 1;
            m.Pos = new Vector2(anywhere ? Range(-11f, 11f) : Range(-12f, -10.5f), Range(-4.5f, 4f));
            m.Vel = new Vector2(Range(0.15f, 0.4f), Range(-0.05f, 0.08f)) * (front ? 2.2f : 1f);
            m.Size = front ? Range(0.08f, 0.14f) : Range(0.03f, 0.06f);
            m.Age = 0f;
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

            for (int i = 0; i < blinks.Count; i++)
            {
                var b = blinks[i];
                float k = 0.6f + 0.4f * Mathf.PerlinNoise(time * b.Speed, b.Phase);
                b.Sr.color = b.Color.WithAlpha(b.Base * k);
            }
            for (int i = 0; i < rays.Count; i++)
            {
                float a = rayAlpha[i] * (0.6f + 0.4f * Mathf.Sin(time * (0.3f + i * 0.07f) + i * 1.7f));
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
            // pennants: they flap along their length and swing a little on the pole
            foreach (var f in flags)
            {
                float t = time * f.Speed + f.Phase;
                f.T.localScale = new Vector3(0.85f + 0.15f * Mathf.Sin(t * 1.7f), 1f + 0.18f * Mathf.Sin(t * 2.3f), 1f);
                f.T.localRotation = Quaternion.Euler(0f, 0f, -6f + 8f * Mathf.Sin(t) + wind * 20f);
            }
            UpdateLife(dt, wind);
            ambient.Update(dt, time, center, wind);
        }

        void UpdateLife(float dt, float wind)
        {
            foreach (var m in pollen)
            {
                m.Pos += (m.Vel + new Vector2(wind * 0.25f, Mathf.Sin(time * 1.3f + m.Phase) * 0.12f)) * dt;
                if (m.Pos.x > 11.5f || m.Pos.y < -5.4f || m.Pos.y > 5.4f) Respawn(m, false);
                m.Sr.transform.localPosition = m.Pos - lean * (m.Kind == 1 ? 0.8f : 0.3f);
                m.Sr.transform.localScale = Vector3.one * m.Size;
                float tw = 0.6f + 0.4f * Mathf.Sin(time * 2.1f + m.Phase * 3f);
                m.Sr.color = Palette.Firefly.WithAlpha((m.Kind == 1 ? 0.4f : 0.75f) * tw);
            }
            foreach (var b in butterflies)
            {
                float t = time + b.Phase;
                // lazy loops over the meadow, a little bob with every wing beat
                Vector2 pos = b.Home + new Vector2(Mathf.Sin(t * 0.21f) * 2.4f + Mathf.Sin(t * 0.53f) * 0.8f, Mathf.Sin(t * 0.37f) * 0.45f + Mathf.Abs(Mathf.Sin(t * 9f)) * 0.06f);
                b.Sr.transform.localPosition = pos - lean * 0.35f;
                float flap = Mathf.Abs(Mathf.Cos(t * 11f));
                b.Sr.transform.localScale = new Vector3(0.35f + 0.65f * flap, 1f, 1f) * b.Size;
                b.Sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Cos(t * 0.21f) * 25f);
            }
        }

        /// <summary>Keeps the plateau's surface right under the player's boots (world position of the feet).</summary>
        public void PlaceGround(Vector2 feetWorld)
        {
            if (!built) return;
            Vector3 local = root.InverseTransformPoint(new Vector3(feetWorld.x, feetWorld.y, 0f));
            // the boots stand in the middle of the chalk circle
            groundY = local.y + MenuScenery.GroundBand * 0.5f;
            ground.localPosition = new Vector3(0f, groundY, 0f);
        }
    }
}
