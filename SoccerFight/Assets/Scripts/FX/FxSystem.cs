using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    public enum FxLayer { Back, Front }

    /// <summary>
    /// Lightweight particle renderer: all particles of a layer/blend combination are baked into one
    /// dynamic mesh per frame (one draw call). Supports soft glows, velocity-aligned spark streaks,
    /// sparkles, dust puffs and true ring geometry for shockwaves. HDR intensity travels in uv.z.
    /// </summary>
    public sealed class FxSystem : MonoBehaviour
    {
        public static FxSystem I { get; private set; }

        const byte ModeBillboard = 0, ModeStreak = 1, ModeRing = 2;

        struct Particle
        {
            public Vector2 pos, vel;
            public float age, life;
            public float size0, size1;
            public float thick0, thick1;
            public float rot, rotVel;
            public Color c0, c1;
            public float intensity;
            public float drag, gravity;
            public float stretch;
            public byte cell, mode, sizeEase, alphaBump;
        }

        sealed class Batch
        {
            public Particle[] items = new Particle[2048];
            public int count;
            public Mesh mesh;
            public readonly List<Vector3> verts = new List<Vector3>(8192);
            public readonly List<Color32> cols = new List<Color32>(8192);
            public readonly List<Vector4> uvs = new List<Vector4>(8192);
            public readonly List<int> tris = new List<int>(12288);
        }

        Batch backAdd, backAlpha, frontAdd, frontAlpha;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        public static FxSystem Create(Transform parent)
        {
            var go = new GameObject("FX");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<FxSystem>();
            fx.backAlpha = fx.MakeBatch("Back Alpha", Art.ParticleAlphaMat, -30);
            fx.backAdd = fx.MakeBatch("Back Add", Art.ParticleAddMat, -29);
            fx.frontAlpha = fx.MakeBatch("Front Alpha", Art.ParticleAlphaMat, 400);
            fx.frontAdd = fx.MakeBatch("Front Add", Art.ParticleAddMat, 401);
            I = fx;
            return fx;
        }

        Batch MakeBatch(string name, Material mat, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = order;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            mf.sharedMesh = mesh;
            return new Batch { mesh = mesh };
        }

        Batch Pick(FxLayer layer, bool additive)
            => layer == FxLayer.Front ? (additive ? frontAdd : frontAlpha) : (additive ? backAdd : backAlpha);

        void Push(Batch b, in Particle p)
        {
            if (b.count >= b.items.Length) System.Array.Resize(ref b.items, b.items.Length * 2);
            b.items[b.count++] = p;
        }

        // ------------------------------------------------------------------ primitive emitters

        public void Spawn(FxLayer layer, bool additive, int cell, Vector2 pos, Vector2 vel, float life,
            float size0, float size1, Color c0, Color c1, float intensity = 1f, float drag = 0f,
            float gravity = 0f, float rot = 0f, float rotVel = 0f, bool easeSize = true, bool fadeInOut = false)
        {
            Push(Pick(layer, additive), new Particle
            {
                pos = pos, vel = vel, life = Mathf.Max(0.01f, life), size0 = size0, size1 = size1,
                c0 = c0, c1 = c1, intensity = intensity, drag = drag, gravity = gravity,
                rot = rot, rotVel = rotVel, cell = (byte)cell, mode = ModeBillboard, sizeEase = (byte)(easeSize ? 1 : 0),
                alphaBump = (byte)(fadeInOut ? 1 : 0)
            });
        }

        public void Streak(FxLayer layer, Vector2 pos, Vector2 vel, float life, float width, float stretch,
            Color c0, Color c1, float intensity = 2f, float drag = 3f, float gravity = 0f)
        {
            Push(Pick(layer, true), new Particle
            {
                pos = pos, vel = vel, life = Mathf.Max(0.01f, life), size0 = width, size1 = width * 0.4f,
                c0 = c0, c1 = c1, intensity = intensity, drag = drag, gravity = gravity, stretch = stretch,
                cell = Art.CellStreak, mode = ModeStreak, sizeEase = 0
            });
        }

        public void Ring(FxLayer layer, Vector2 pos, float r0, float r1, float thick0, float thick1, float life,
            Color c0, Color c1, float intensity = 2f, bool rainbow = false, bool additive = true)
        {
            Push(Pick(layer, additive), new Particle
            {
                pos = pos, life = Mathf.Max(0.01f, life), size0 = r0, size1 = r1, thick0 = thick0, thick1 = thick1,
                c0 = c0, c1 = c1, intensity = intensity, cell = rainbow ? (byte)Art.CellRainbow : (byte)Art.CellBand,
                mode = ModeRing, sizeEase = 1
            });
        }

        // ------------------------------------------------------------------ composite effects

        public void Flash(Vector2 pos, float size, Color c, float life = 0.18f, float intensity = 3f)
        {
            Spawn(FxLayer.Front, true, Art.CellGlow, pos, Vector2.zero, life, size * 0.6f, size, c, c.WithAlpha(0f), intensity);
        }

        public void Sparks(Vector2 pos, Vector2 dir, float spreadDeg, int count, float speedMin, float speedMax,
            Color c, float intensity = 2.5f, float width = 0.05f, float life = 0.28f, float gravity = 0f)
        {
            float baseAng = MathUtil.Angle(dir);
            for (int i = 0; i < count; i++)
            {
                float ang = baseAng + Random.Range(-spreadDeg, spreadDeg) * 0.5f;
                float sp = Random.Range(speedMin, speedMax);
                Vector2 v = MathUtil.Dir(ang) * sp;
                Color end = Color.Lerp(c, Palette.HitWhite, 0.2f).WithAlpha(0f);
                Streak(FxLayer.Front, pos, v, life * Random.Range(0.6f, 1.2f), width * Random.Range(0.7f, 1.3f),
                    0.045f, Color.Lerp(Color.white, c, 0.35f), end, intensity, 5f, gravity);
            }
        }

        public void Sparkles(Vector2 pos, float radius, int count, Color c, float intensity = 2.5f, float life = 0.6f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 o = Random.insideUnitCircle * radius;
                Vector2 v = o.normalized * Random.Range(0.3f, 1.4f) + Vector2.up * Random.Range(0.2f, 1.2f);
                float s = Random.Range(0.12f, 0.26f);
                Spawn(FxLayer.Front, true, Art.CellSparkle, pos + o, v, life * Random.Range(0.6f, 1.3f), s, 0f,
                    c, c.WithAlpha(0f), intensity, 2f, -0.6f, Random.Range(0f, 90f), Random.Range(-90f, 90f));
            }
        }

        public void Dust(Vector2 pos, Vector2 dir, int count, float speed = 1.6f, float size = 0.35f, float alpha = 0.35f)
        {
            Color dust = new Color(0.88f, 0.8f, 0.66f, alpha);   // sandy turf dust
            for (int i = 0; i < count; i++)
            {
                Vector2 v = new Vector2(dir.x * Random.Range(0.3f, 1f) + Random.Range(-0.4f, 0.4f),
                    Mathf.Abs(dir.y) * 0.3f + Random.Range(0.1f, 0.6f)) * speed;
                float s = size * Random.Range(0.6f, 1.2f);
                Spawn(FxLayer.Back, false, Art.CellPuff, pos + new Vector2(Random.Range(-0.12f, 0.12f), 0.05f), v,
                    Random.Range(0.35f, 0.6f), s * 0.5f, s * 1.3f, dust, dust.WithAlpha(0f), 1f, 4f, -0.3f,
                    Random.Range(0f, 360f), Random.Range(-40f, 40f));
            }
        }

        public void Motes(Vector2 pos, Vector2 vel, Color c, int count, float spread = 0.2f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 v = vel + Random.insideUnitCircle * 1.2f;
                float s = Random.Range(0.05f, 0.11f);
                Spawn(FxLayer.Front, true, Art.CellDot, pos + Random.insideUnitCircle * spread, v,
                    Random.Range(0.4f, 0.9f), s, 0f, c, c.WithAlpha(0f), 3f, 2.5f, -1.2f);
            }
        }

        /// <summary>Monster pop: glow flash, ring, spark burst, rising souls.</summary>
        public void Burst(Vector2 pos, Color main, Color accent, float scale = 1f)
        {
            Flash(pos, 2.4f * scale, Color.Lerp(main, Color.white, 0.4f), 0.22f, 3f);
            Ring(FxLayer.Front, pos, 0.2f * scale, 1.6f * scale, 0.28f * scale, 0.02f, 0.38f, accent, accent.WithAlpha(0f), 2.6f);
            Sparks(pos, Vector2.up, 360f, Mathf.RoundToInt(22 * scale), 4f, 11f, accent, 2.6f, 0.06f, 0.35f, 4f);
            for (int i = 0; i < 10; i++)
            {
                Vector2 v = Random.insideUnitCircle * 3.2f * scale + Vector2.up * 1.5f;
                float s = Random.Range(0.1f, 0.24f) * scale;
                Spawn(FxLayer.Front, true, Art.CellGlow, pos + Random.insideUnitCircle * 0.2f, v, Random.Range(0.5f, 1.0f),
                    s * 1.4f, 0f, main, main.WithAlpha(0f), 2.8f, 2.4f, -2.2f);
            }
            for (int i = 0; i < 8; i++)
            {
                Vector2 v = Random.insideUnitCircle * 4f * scale + Vector2.up * 2f;
                Spawn(FxLayer.Front, false, Art.CellShard, pos, v, Random.Range(0.45f, 0.8f), 0.16f * scale, 0.02f,
                    Palette.MonsterBottom, Palette.MonsterBottom.WithAlpha(0f), 1f, 1.2f, 12f,
                    Random.Range(0f, 360f), Random.Range(-720f, 720f));
            }
        }

        public void Clear()
        {
            backAdd.count = backAlpha.count = frontAdd.count = frontAlpha.count = 0;
        }

        // ------------------------------------------------------------------ simulation + meshing

        void LateUpdate()
        {
            if (backAdd == null) return; // stale instance after a script reload
            float dt = Time.deltaTime;
            Simulate(backAlpha, dt); Simulate(backAdd, dt); Simulate(frontAlpha, dt); Simulate(frontAdd, dt);
            Build(backAlpha); Build(backAdd); Build(frontAlpha); Build(frontAdd);
        }

        static void Simulate(Batch b, float dt)
        {
            var items = b.items;
            int n = b.count;
            for (int i = 0; i < n; i++)
            {
                ref Particle p = ref items[i];
                p.age += dt;
                if (p.age >= p.life)
                {
                    items[i] = items[--n];
                    i--;
                    continue;
                }
                if (p.drag > 0f) p.vel *= Mathf.Exp(-p.drag * dt);
                p.vel.y -= p.gravity * dt;
                p.pos += p.vel * dt;
                p.rot += p.rotVel * dt;
            }
            b.count = n;
        }

        static Color32 ToLinear32(Color c)
        {
            Color l = c.linear;
            return new Color32(
                (byte)(Mathf.Clamp01(l.r) * 255f), (byte)(Mathf.Clamp01(l.g) * 255f),
                (byte)(Mathf.Clamp01(l.b) * 255f), (byte)(Mathf.Clamp01(c.a) * 255f));
        }

        static void Build(Batch b)
        {
            b.verts.Clear(); b.cols.Clear(); b.uvs.Clear(); b.tris.Clear();
            var cells = Art.Cells;

            for (int i = 0; i < b.count; i++)
            {
                ref Particle p = ref b.items[i];
                float t = p.age / p.life;
                float st = p.sizeEase == 1 ? MathUtil.EaseOutCubic(t) : t;
                float size = Mathf.Lerp(p.size0, p.size1, st);
                Color c = Color.Lerp(p.c0, p.c1, t);
                c.a *= p.alphaBump == 1 ? Mathf.Sin(t * Mathf.PI) : Mathf.Clamp01(p.age / 0.025f);
                if (c.a <= 0.002f) continue;
                Color32 col = ToLinear32(c);
                Rect uv = cells[p.cell];

                if (p.mode == ModeRing)
                {
                    float thick = Mathf.Lerp(p.thick0, p.thick1, st);
                    AddRing(b, p.pos, size, thick, col, uv, p.intensity);
                    continue;
                }

                Vector2 right, up;
                if (p.mode == ModeStreak)
                {
                    float speed = p.vel.magnitude;
                    Vector2 dir = speed > 1e-4f ? p.vel / speed : Vector2.right;
                    float len = size * 2f + speed * p.stretch;
                    right = dir * (len * 0.5f);
                    up = new Vector2(-dir.y, dir.x) * (size * 0.5f);
                }
                else
                {
                    float r = p.rot * Mathf.Deg2Rad;
                    float cs = Mathf.Cos(r), sn = Mathf.Sin(r);
                    right = new Vector2(cs, sn) * (size * 0.5f);
                    up = new Vector2(-sn, cs) * (size * 0.5f);
                }

                int v0 = b.verts.Count;
                Vector2 pos = p.pos;
                b.verts.Add(pos - right - up);
                b.verts.Add(pos - right + up);
                b.verts.Add(pos + right + up);
                b.verts.Add(pos + right - up);
                b.uvs.Add(new Vector4(uv.xMin, uv.yMin, p.intensity, 0));
                b.uvs.Add(new Vector4(uv.xMin, uv.yMax, p.intensity, 0));
                b.uvs.Add(new Vector4(uv.xMax, uv.yMax, p.intensity, 0));
                b.uvs.Add(new Vector4(uv.xMax, uv.yMin, p.intensity, 0));
                b.cols.Add(col); b.cols.Add(col); b.cols.Add(col); b.cols.Add(col);
                b.tris.Add(v0); b.tris.Add(v0 + 1); b.tris.Add(v0 + 2);
                b.tris.Add(v0); b.tris.Add(v0 + 2); b.tris.Add(v0 + 3);
            }

            var mesh = b.mesh;
            mesh.Clear(false);
            if (b.verts.Count == 0) return;
            mesh.SetVertices(b.verts);
            mesh.SetColors(b.cols);
            mesh.SetUVs(0, b.uvs);
            mesh.SetTriangles(b.tris, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(10000f, 10000f, 10f));
        }

        static void AddRing(Batch b, Vector2 center, float radius, float thick, Color32 col, Rect uv, float intensity)
        {
            int seg = Mathf.Clamp(Mathf.RoundToInt(radius * 28f), 24, 96);
            float rin = Mathf.Max(0f, radius - thick * 0.5f), rout = radius + thick * 0.5f;
            float u = uv.center.x;
            int v0 = b.verts.Count;
            for (int s = 0; s <= seg; s++)
            {
                float a = s / (float)seg * MathUtil.Tau;
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                b.verts.Add(center + d * rin);
                b.verts.Add(center + d * rout);
                b.uvs.Add(new Vector4(u, uv.yMin, intensity, 0));
                b.uvs.Add(new Vector4(u, uv.yMax, intensity, 0));
                b.cols.Add(col); b.cols.Add(col);
            }
            for (int s = 0; s < seg; s++)
            {
                int i0 = v0 + s * 2;
                b.tris.Add(i0); b.tris.Add(i0 + 1); b.tris.Add(i0 + 3);
                b.tris.Add(i0); b.tris.Add(i0 + 3); b.tris.Add(i0 + 2);
            }
        }
    }
}
