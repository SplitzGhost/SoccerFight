using UnityEngine;
using UnityEngine.Rendering;

namespace SoccerFight
{
    /// <summary>
    /// Builds every material and generic sprite at startup. The game ships without any imported art:
    /// all visuals are rasterized from SDF shapes, so they stay razor sharp and fully tweakable in code.
    /// </summary>
    public static class Art
    {
        // Sprite materials
        public static Material SpriteMat;        // normal alpha
        public static Material SpriteSolidMat;   // flat fill with renderer color (flashes, afterimages)
        public static Material SpriteAddMat;     // additive, LDR
        public static Material SpriteGlowMat;    // additive, HDR (blooms)
        public static Material CharacterMat;     // rigged characters: moon rim, grass bounce, contact shade
        public static Material SpriteEmissiveMat;// alpha blended but HDR (eyes, neon)

        // Mesh / trail materials
        public static Material ParticleAddMat;
        public static Material ParticleAlphaMat;
        public static Material TrailShotMat;
        public static Material TrailRainbowMat;
        public static Material TrailSoftMat;

        // Generic sprites
        public static Sprite Circle;
        public static Sprite SoftGlow;
        public static Sprite Shadow;
        public static Sprite Ring;
        public static Sprite MarkRing;
        public static Sprite Pill;
        public static Sprite LightRay;

        // Ball
        public const float BallRadius = 0.2f;
        public static Sprite BallPattern;
        public static Sprite BallShade;
        public static Sprite BallHighlight;

        // Particles
        public static Texture2D ParticleAtlas;
        public static Rect[] Cells;
        public const int CellGlow = 0, CellDot = 1, CellStreak = 2, CellSparkle = 3, CellPuff = 4, CellBand = 5, CellRainbow = 6, CellShard = 7;

        static bool built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { built = false; }

        public static void Build()
        {
            if (built) return;
            built = true;

            BuildMaterials();
            BuildGeneric(); BuildTimer.Mark("generic");
            BuildBall(); BuildTimer.Mark("ball");
            BuildParticleAtlas();
            BuildTrails(); BuildTimer.Mark("fx");
            PlayerArt.Build(); BuildTimer.Mark("player");
            MonsterArt.Build(); BuildTimer.Mark("monsters");
        }

        // ------------------------------------------------------------------ materials

        static Shader FindShader(string name)
        {
            var s = Shader.Find(name);
            if (s == null) Debug.LogError($"[SoccerFight] Shader '{name}' not found. Is Assets/Resources/Shaders present?");
            return s;
        }

        public static Material MakeSpriteMaterial(string name, float intensity, bool additive, float solid = 0f)
        {
            var m = new Material(FindShader("SoccerFight/Sprite")) { name = name };
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_Solid", solid);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            return m;
        }

        public static Material MakeMeshMaterial(string name, Texture tex, float intensity, bool additive, bool uvIntensity)
        {
            var m = new Material(FindShader("SoccerFight/Mesh")) { name = name };
            m.mainTexture = tex;
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_UseUvIntensity", uvIntensity ? 1f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            return m;
        }

        static void BuildMaterials()
        {
            SpriteMat = MakeSpriteMaterial("SF Sprite", 1f, false);
            SpriteSolidMat = MakeSpriteMaterial("SF Sprite Solid", 1f, false, 1f);
            SpriteAddMat = MakeSpriteMaterial("SF Sprite Add", 1f, true);
            SpriteGlowMat = MakeSpriteMaterial("SF Sprite Glow", 2.4f, true);
            SpriteEmissiveMat = MakeSpriteMaterial("SF Sprite Emissive", 2.2f, false);
            CharacterMat = new Material(FindShader("SoccerFight/Character")) { name = "SF Character" };
            CharacterMat.SetColor("_RimColor", Palette.MoonRim);
        }

        // ------------------------------------------------------------------ generic

        static void BuildGeneric()
        {
            var c = new SdfCanvas(new Rect(-0.5f, -0.5f, 1f, 1f), 256f);
            c.Fill(p => Sdf.Circle(p, Vector2.zero, 0.49f), Color.white);
            Circle = c.ToSprite("Circle", Vector2.zero);

            var g = new SdfCanvas(new Rect(-0.5f, -0.5f, 1f, 1f), 128f);
            g.Field(p =>
            {
                float r = p.magnitude / 0.5f;
                float a = Mathf.Exp(-r * r * 4.2f) * MathUtil.Smooth01((1f - r) / 0.15f);
                return new Color(1, 1, 1, a);
            });
            SoftGlow = g.ToSprite("SoftGlow", Vector2.zero);

            var s = new SdfCanvas(new Rect(-0.5f, -0.25f, 1f, 0.5f), 128f);
            s.Field(p =>
            {
                float d = new Vector2(p.x / 0.5f, p.y / 0.2f).magnitude;
                float a = Mathf.Clamp01(1f - d);
                return new Color(0, 0, 0, a * a * (3f - 2f * a));
            });
            Shadow = s.ToSprite("Shadow", Vector2.zero);

            var r2 = new SdfCanvas(new Rect(-0.5f, -0.5f, 1f, 1f), 320f);
            r2.Fill(p => Sdf.Ring(p, Vector2.zero, 0.46f, 0.03f), Color.white);
            Ring = r2.ToSprite("Ring", Vector2.zero);

            // hairline ring for ground markers: stays thin even when scaled up a lot
            var r3 = new SdfCanvas(new Rect(-0.5f, -0.5f, 1f, 1f), 320f);
            r3.Fill(p => Sdf.Ring(p, Vector2.zero, 0.47f, 0.008f), Color.white);
            MarkRing = r3.ToSprite("MarkRing", Vector2.zero);

            var pill = new SdfCanvas(new Rect(-0.25f, -0.0625f, 0.5f, 0.125f), 256f);
            pill.Fill(p => Sdf.Box(p, Vector2.zero, new Vector2(0.245f, 0.058f), 0.058f), Color.white);
            float b = 0.0625f * 256f;
            Pill = pill.ToSprite("Pill", Vector2.zero, border: new Vector4(b, b, b, b));

            // Pivot at the top (light source): bright near the source, long soft fade downwards.
            var ray = new SdfCanvas(new Rect(-0.5f, 0f, 1f, 8f), 32f);
            ray.Field(p =>
            {
                float across = Mathf.Exp(-(p.x * p.x) / (2f * 0.16f * 0.16f));
                float fromTop = 8f - p.y;
                float along = MathUtil.Smooth01(fromTop / 0.9f) * Mathf.Pow(p.y / 8f, 1.3f);
                return new Color(1, 1, 1, across * along);
            });
            LightRay = ray.ToSprite("LightRay", new Vector2(0f, 8f));
        }

        // ------------------------------------------------------------------ ball

        static readonly Vector3[] PentagonCenters = BuildIcosa();
        static readonly Vector3[] HexagonCenters = BuildDodeca();

        static Vector3[] BuildIcosa()
        {
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new Vector3[12];
            int i = 0;
            foreach (float s1 in new[] { -1f, 1f })
            foreach (float s2 in new[] { -1f, 1f })
            {
                v[i++] = new Vector3(0, s1, s2 * phi).normalized;
                v[i++] = new Vector3(s1, s2 * phi, 0).normalized;
                v[i++] = new Vector3(s2 * phi, 0, s1).normalized;
            }
            return v;
        }

        static Vector3[] BuildDodeca()
        {
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f, ip = 1f / phi;
            var list = new System.Collections.Generic.List<Vector3>();
            foreach (float a in new[] { -1f, 1f })
            foreach (float b in new[] { -1f, 1f })
            {
                // Dual of the icosahedron (0,±1,±φ) set: face centers are the cyclic permutations of (1/φ, 0, φ).
                foreach (float c in new[] { -1f, 1f }) list.Add(new Vector3(a, b, c).normalized);
                list.Add(new Vector3(a * ip, 0, b * phi).normalized);
                list.Add(new Vector3(0, a * phi, b * ip).normalized);
                list.Add(new Vector3(a * phi, b * ip, 0).normalized);
            }
            return list.ToArray();
        }

        static void BuildBall()
        {
            const float R = BallRadius;
            float ext = R * 1.12f;
            var rect = new Rect(-ext, -ext, ext * 2f, ext * 2f);
            const float ppu = 256f / (BallRadius * 2.24f);

            // Truncated icosahedron: the visible face along a view ray is the face plane hit first,
            // i.e. the one maximizing dot(n, c) / planeDistance. Pentagons sit ~2.65% farther out.
            const float d5 = 2.3274f, d6 = 2.2673f;
            Quaternion tilt = Quaternion.Euler(18f, 32f, 9f);

            var pat = new SdfCanvas(rect, ppu);
            pat.Fill(p => Sdf.Circle(p, Vector2.zero, R), p =>
            {
                Vector2 q = p / R;
                float z2 = 1f - q.sqrMagnitude;
                if (z2 <= 0f) q = q.normalized * 0.9999f;
                Vector3 n = tilt * new Vector3(q.x, q.y, Mathf.Sqrt(Mathf.Max(0f, z2)));

                float best = -9f, second = -9f;
                bool bestIsPent = false;
                for (int i = 0; i < PentagonCenters.Length; i++)
                {
                    float sc = Vector3.Dot(n, PentagonCenters[i]) / d5;
                    if (sc > best) { second = best; best = sc; bestIsPent = true; }
                    else if (sc > second) second = sc;
                }
                for (int i = 0; i < HexagonCenters.Length; i++)
                {
                    float sc = Vector3.Dot(n, HexagonCenters[i]) / d6;
                    if (sc > best) { second = best; best = sc; bestIsPent = false; }
                    else if (sc > second) second = sc;
                }

                Color face = bestIsPent ? Palette.BallPanel : Palette.BallWhite;
                // Seam lines where two faces meet; compensate for foreshortening near the rim.
                float edge = (best - second) / Mathf.Max(0.25f, Mathf.Sqrt(Mathf.Max(0f, z2)));
                float seam = 1f - MathUtil.Smooth01((edge - 0.004f) / 0.01f);
                return Color.Lerp(face, bestIsPent ? Palette.BallPanel : Palette.BallSeam, seam * 0.85f);
            });
            BallPattern = pat.ToSprite("BallPattern", Vector2.zero);

            // Non-rotating shading overlay: the light stays top-left while the pattern spins.
            Vector3 L = new Vector3(-0.45f, 0.62f, 0.64f).normalized;
            var shade = new SdfCanvas(rect, ppu);
            shade.Fill(p => Sdf.Circle(p, Vector2.zero, R + 0.002f), p =>
            {
                Vector2 q = p / R;
                float z = Mathf.Sqrt(Mathf.Max(0f, 1f - q.sqrMagnitude));
                Vector3 n = new Vector3(q.x, q.y, z);
                float lambert = Mathf.Clamp01(Vector3.Dot(n, L));
                float dark = Mathf.Pow(1f - lambert, 1.7f) * 0.72f + Mathf.Pow(1f - z, 4f) * 0.25f;
                return new Color(0.03f, 0.05f, 0.12f, Mathf.Clamp01(dark));
            });
            BallShade = shade.ToSprite("BallShade", Vector2.zero);

            var hi = new SdfCanvas(rect, ppu);
            hi.Fill(p => Sdf.Circle(p, Vector2.zero, R), p =>
            {
                Vector2 q = p / R;
                float z = Mathf.Sqrt(Mathf.Max(0f, 1f - q.sqrMagnitude));
                Vector2 spot = q - new Vector2(-0.38f, 0.46f);
                float spec = Mathf.Exp(-spot.sqrMagnitude / (2f * 0.16f * 0.16f)) * 0.85f;
                float rim = Mathf.Pow(1f - z, 3f) * Mathf.Clamp01(Vector2.Dot(q.normalized, new Vector2(-0.55f, 0.83f))) * 0.9f;
                return new Color(0.85f, 0.97f, 1f, Mathf.Clamp01(spec + rim));
            });
            BallHighlight = hi.ToSprite("BallHighlight", Vector2.zero);
        }

        // ------------------------------------------------------------------ particles

        static void BuildParticleAtlas()
        {
            const int cols = 4, rows = 2;
            var atlas = new SdfCanvas(new Rect(0, 0, cols, rows), 256f);
            Cells = new Rect[cols * rows];
            for (int i = 0; i < Cells.Length; i++)
            {
                int cx = i % cols, cy = i / cols;
                const float inset = 1.5f / 256f;
                Cells[i] = new Rect((cx + inset) / cols, (cy + inset) / rows, (1f - 2f * inset) / cols, (1f - 2f * inset) / rows);
            }

            atlas.Field(p =>
            {
                int cx = Mathf.Clamp(Mathf.FloorToInt(p.x), 0, cols - 1);
                int cy = Mathf.Clamp(Mathf.FloorToInt(p.y), 0, rows - 1);
                int cell = cy * cols + cx;
                Vector2 q = new Vector2(p.x - cx - 0.5f, p.y - cy - 0.5f); // [-0.5, 0.5]
                float r = q.magnitude / 0.5f;
                float edgeFade = MathUtil.Smooth01((0.5f - Mathf.Max(Mathf.Abs(q.x), Mathf.Abs(q.y))) / 0.03f);

                switch (cell)
                {
                    case CellGlow:
                    {
                        float a = Mathf.Exp(-r * r * 4.5f) * MathUtil.Smooth01((1f - r) / 0.12f);
                        return new Color(1, 1, 1, a);
                    }
                    case CellDot:
                    {
                        float a = Mathf.Clamp01((0.8f - r) / 0.12f);
                        return new Color(1, 1, 1, a);
                    }
                    case CellStreak:
                    {
                        float along = Mathf.Abs(q.x) * 2f;
                        float taper = Mathf.Pow(Mathf.Clamp01(1f - along), 0.9f);
                        float w = 0.1f * taper + 0.002f;
                        float a = Mathf.Exp(-(q.y * q.y) / (2f * w * w * 0.25f)) * taper;
                        return new Color(1, 1, 1, Mathf.Clamp01(a) * edgeFade);
                    }
                    case CellSparkle:
                    {
                        float ax = Mathf.Abs(q.x) * 2f, ay = Mathf.Abs(q.y) * 2f;
                        float star = Mathf.Exp(-ay * 26f) * (1f - ax) + Mathf.Exp(-ax * 26f) * (1f - ay);
                        float core = Mathf.Exp(-r * r * 30f);
                        float halo = Mathf.Exp(-r * r * 6f) * 0.35f;
                        return new Color(1, 1, 1, Mathf.Clamp01(star + core + halo) * edgeFade);
                    }
                    case CellPuff:
                    {
                        float ang = Mathf.Atan2(q.y, q.x);
                        float wob = 0.06f * Mathf.Sin(ang * 5f + 1.3f) + 0.04f * Mathf.Sin(ang * 9f + 0.4f);
                        float d = r + wob;
                        float a = Mathf.Clamp01((0.92f - d) / 0.55f);
                        return new Color(1, 1, 1, a * a * 0.9f);
                    }
                    case CellBand:
                    {
                        float v = q.y + 0.5f;
                        float a = Mathf.Pow(Mathf.Sin(Mathf.PI * v), 2.2f);
                        return new Color(1, 1, 1, a);
                    }
                    case CellRainbow:
                    {
                        float v = q.y + 0.5f;
                        Color c = Rainbow(v);
                        c.a = Mathf.Pow(Mathf.Sin(Mathf.PI * v), 0.8f);
                        return c;
                    }
                    default:
                    {
                        float d = Sdf.Triangle(q, new Vector2(0.45f, 0f), new Vector2(-0.35f, 0.22f), new Vector2(-0.3f, -0.2f));
                        float a = Mathf.Clamp01(0.5f - d * 256f);
                        return new Color(1, 1, 1, a);
                    }
                }
            });
            ParticleAtlas = atlas.ToTexture("ParticleAtlas");
            ParticleAddMat = MakeMeshMaterial("SF Particles Add", ParticleAtlas, 1f, true, true);
            ParticleAlphaMat = MakeMeshMaterial("SF Particles Alpha", ParticleAtlas, 1f, false, true);
        }

        /// <summary>Rainbow across a band: v = 0 violet (inside) → v = 1 red (outside).</summary>
        public static Color Rainbow(float v)
        {
            float h = Mathf.Lerp(0.78f, 0.0f, Mathf.Clamp01(v));
            return Color.HSVToRGB(h, 0.78f, 1f);
        }

        // ------------------------------------------------------------------ trails

        static void BuildTrails()
        {
            var shot = new SdfCanvas(new Rect(0, 0, 1, 0.5f), 128f);
            shot.Field(p =>
            {
                float v = p.y / 0.5f;
                float a = Mathf.Pow(Mathf.Sin(Mathf.PI * v), 1.8f);
                float core = Mathf.Pow(Mathf.Sin(Mathf.PI * v), 10f);
                return new Color(Mathf.Lerp(0.55f, 1f, core), Mathf.Lerp(0.9f, 1f, core), 1f, a);
            });
            var shotTex = shot.ToTexture("TrailShot", true, true, TextureWrapMode.Clamp);
            TrailShotMat = MakeMeshMaterial("SF Trail Shot", shotTex, 2.2f, true, false);

            var rb = new SdfCanvas(new Rect(0, 0, 1, 0.5f), 128f);
            rb.Field(p =>
            {
                float v = p.y / 0.5f;
                Color c = Rainbow(v);
                float a = MathUtil.Smooth01(v / 0.16f) * MathUtil.Smooth01((1f - v) / 0.16f);
                c.a = a;
                return c;
            });
            var rbTex = rb.ToTexture("TrailRainbow", true, true, TextureWrapMode.Clamp);
            TrailRainbowMat = MakeMeshMaterial("SF Trail Rainbow", rbTex, 1.9f, true, false);

            var soft = new SdfCanvas(new Rect(0, 0, 1, 0.5f), 64f);
            soft.Field(p =>
            {
                float v = p.y / 0.5f;
                return new Color(1, 1, 1, Mathf.Pow(Mathf.Sin(Mathf.PI * v), 1.5f));
            });
            TrailSoftMat = MakeMeshMaterial("SF Trail Soft", soft.ToTexture("TrailSoft"), 1f, true, false);
        }

        // ------------------------------------------------------------------ helpers

        public static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, int order,
            Material mat = null, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = mat != null ? mat : SpriteMat;
            sr.sortingOrder = order;
            sr.color = color ?? Color.white;
            return sr;
        }
    }
}
