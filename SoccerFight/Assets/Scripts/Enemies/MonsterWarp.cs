using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace SoccerFight
{
    /// <summary>One bendable limb of a painted monster: a bone chain from root to tip (tools/newdesign/monsters.js).</summary>
    [System.Serializable]
    public sealed class WarpChain
    {
        /// <summary>wing, tail, arm, leg or sway (leaf, horn, fuse, antenna …): only the defaults differ.</summary>
        public string type;
        /// <summary>Joints root → tip in body units (x0, y0, x1, y1 …), facing right, pivot at the sole or centre.</summary>
        public float[] pts;
        /// <summary>Optional outline of the limb (x0, y0 …): everything inside moves with the bones. Without
        /// it the limb is everything within <see cref="w"/> of the chain.</summary>
        public float[] poly;
        /// <summary>Half width of the limb around the chain (fully moved; softly fading out to 1.5×), or with
        /// an outline the soft edge outside it.</summary>
        public float w;
        /// <summary>Own motion: amplitude (degrees), speed (rad/s), delay per bone (rad), phase.</summary>
        public float amp, freq, lag, phase;
        /// <summary>Physics: trailing in the air per unit/s (deg), fling on body acceleration, spring stiffness (rad/s) and damping ratio.</summary>
        public float drag, inertia, stiff, damp;
        /// <summary>Extra root angle while winding up / lunging (degrees); faster beats with speed (wings).</summary>
        public float windup, thrust, speedup;
    }

    /// <summary>A soft swelling (throat sac, jelly bell, bud): scales the picture around a centre.</summary>
    [System.Serializable]
    public sealed class WarpPulse { public float x, y, r, amp, freq, phase, charge; }

    [System.Serializable]
    public sealed class WarpRig
    {
        /// <summary>How much a ground monster's body sways like jelly above its sole (1 = normal, 0 = rigid).</summary>
        public float bend;
        public WarpChain[] chains;
        public WarpPulse[] pulses;
    }

    /// <summary>What the monster is doing this frame, in its own facing frame (x = forwards).</summary>
    public struct WarpDrive
    {
        public float Dt, Time, Charge, Lunge, Speed01;
        public Vector2 Vel, Acc;
        public bool Frozen, Ground;
        /// <summary>Standing on something: feet stay planted instead of dangling.</summary>
        public bool Grounded;
        /// <summary>Off screen: the springs keep going, the mesh is left alone.</summary>
        public bool Hidden;
    }

    /// <summary>
    /// Bends a painted monster without cutting it: the body sprite gets its own copy with a fine grid mesh,
    /// and every frame the grid is pulled by bone chains (wings, tails, tentacles, arms, legs, leaves) and soft
    /// swellings, then the whole body sways like jelly above its sole. Each bone is a damped spring driven by
    /// the body's velocity and acceleration, so limbs trail in the air, fling on take-off and landing and
    /// settle again — plus a small motion of their own (wing beats, tentacle waves, breathing).
    /// </summary>
    public sealed class MonsterWarp
    {
        const int Grid = 32;

        public readonly Sprite Sprite;
        /// <summary>Beat of the first wing (radians), so the body can bob with its wing strokes; -1 without wings.</summary>
        public float WingBeat => wing != null ? wing.Clock + wing.D.phase : -1f;
        ChainRt wing;

        struct Bind { public int V, Chain, SegA, SegB; public float T, W; }
        struct PulseBind { public int V, Pulse; public float W; }

        sealed class ChainRt
        {
            public WarpChain D;
            public Vector2[] P, Q;
            public float[] Angle, AngleVel, Cos, Sin;
            public int Segs;
            public float Clock;
            public bool Leg;
        }

        readonly Vector3[] rest, now;
        NativeArray<Vector3> buf;
        readonly Bind[] binds;
        readonly PulseBind[] pulseBinds;
        readonly ChainRt[] chains;
        readonly WarpPulse[] pulses;
        readonly float[] pulseScale;
        readonly float bendAmount, height;
        float bend, bendVel;

        public MonsterWarp(Sprite src, SecondarySpriteTexture[] secondary, WarpRig rig, bool ground)
        {
            Rect r = src.rect;
            Vector2 pivot = src.pivot;
            float ppu = src.pixelsPerUnit;
            Sprite = Sprite.Create(src.texture, r, new Vector2(pivot.x / r.width, pivot.y / r.height), ppu, 0,
                SpriteMeshType.FullRect, Vector4.zero, false, secondary);
            Sprite.name = src.name + " (bent)";

            int n = (Grid + 1) * (Grid + 1);
            rest = new Vector3[n];
            now = new Vector3[n];
            var uv = new NativeArray<Vector2>(n, Allocator.Temp);
            float tw = src.texture.width, th = src.texture.height;
            for (int j = 0, k = 0; j <= Grid; j++)
                for (int i = 0; i <= Grid; i++, k++)
                {
                    float px = r.width * i / Grid, py = r.height * j / Grid;
                    rest[k] = new Vector3((px - pivot.x) / ppu, (py - pivot.y) / ppu, 0f);
                    uv[k] = new Vector2((r.x + px) / tw, (r.y + py) / th);
                }
            var idx = new NativeArray<ushort>(Grid * Grid * 6, Allocator.Temp);
            for (int j = 0, k = 0; j < Grid; j++)
                for (int i = 0; i < Grid; i++)
                {
                    int a = j * (Grid + 1) + i, b = a + 1, c = a + Grid + 1, d = c + 1;
                    idx[k++] = (ushort)a; idx[k++] = (ushort)c; idx[k++] = (ushort)b;
                    idx[k++] = (ushort)b; idx[k++] = (ushort)c; idx[k++] = (ushort)d;
                }
            buf = new NativeArray<Vector3>(n, Allocator.Persistent);
            buf.CopyFrom(rest);
            Sprite.SetVertexCount(n);
            Sprite.SetVertexAttribute(VertexAttribute.Position, buf);
            Sprite.SetVertexAttribute(VertexAttribute.TexCoord0, uv);
            Sprite.SetIndices(idx);
            uv.Dispose();
            idx.Dispose();

            height = (r.height - pivot.y) / ppu;
            bendAmount = ground ? (rig != null ? rig.bend : 1f) : 0f;

            var chainList = new List<ChainRt>();
            var bindList = new List<Bind>();
            if (rig?.chains != null)
                foreach (var c in rig.chains)
                {
                    if (c.pts == null || c.pts.Length < 4) continue;
                    var rt = new ChainRt { D = c, Segs = c.pts.Length / 2 - 1, Leg = c.type == "leg" };
                    rt.P = new Vector2[rt.Segs + 1];
                    for (int k = 0; k <= rt.Segs; k++) rt.P[k] = new Vector2(c.pts[k * 2], c.pts[k * 2 + 1]);
                    rt.Q = new Vector2[rt.Segs + 1];
                    rt.Angle = new float[rt.Segs]; rt.AngleVel = new float[rt.Segs];
                    rt.Cos = new float[rt.Segs]; rt.Sin = new float[rt.Segs];
                    int ci = chainList.Count;
                    chainList.Add(rt);
                    for (int v = 0; v < n; v++)
                        if (BindVertex(rt, rest[v], out var b)) { b.V = v; b.Chain = ci; bindList.Add(b); }
                }
            chains = chainList.ToArray();
            wing = chainList.Find(c => c.D.type == "wing");
            // where two limbs overlap they share the pull instead of adding it up
            var total = new float[n];
            foreach (var b in bindList) total[b.V] += b.W;
            for (int i = 0; i < bindList.Count; i++)
            {
                var b = bindList[i];
                b.W /= Mathf.Max(1f, total[b.V]);
                bindList[i] = b;
            }
            binds = bindList.ToArray();

            pulses = rig?.pulses ?? new WarpPulse[0];
            pulseScale = new float[pulses.Length];
            var pulseList = new List<PulseBind>();
            for (int p = 0; p < pulses.Length; p++)
                for (int v = 0; v < n; v++)
                {
                    float d = Vector2.Distance(rest[v], new Vector2(pulses[p].x, pulses[p].y));
                    float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pulses[p].r * 0.45f, pulses[p].r, d));
                    if (w > 0.001f) pulseList.Add(new PulseBind { V = v, Pulse = p, W = w });
                }
            pulseBinds = pulseList.ToArray();
        }

        /// <summary>Where a grid point sits along a chain and how strongly the chain pulls it.</summary>
        static bool BindVertex(ChainRt c, Vector2 p, out Bind b)
        {
            b = default;
            float best = float.MaxValue, bestS = 0f;
            for (int k = 0; k < c.Segs; k++)
            {
                Vector2 a = c.P[k], e = c.P[k + 1] - a;
                float len2 = Mathf.Max(1e-8f, e.sqrMagnitude);
                float t = Vector2.Dot(p - a, e) / len2;
                float tc = Mathf.Clamp01(t);
                float d = Vector2.Distance(p, a + e * tc);
                if (d < best)
                {
                    best = d;
                    // behind the root / past the tip keeps its overshoot, so the root can fade out
                    bestS = k + (k == 0 && t < 0f ? t : k == c.Segs - 1 && t > 1f ? 1f : tc);
                }
            }
            float reach = c.D.w;
            float w;
            if (c.D.poly != null && c.D.poly.Length >= 6)
            {
                float edge = PolyDistance(c.D.poly, p, out bool inside);
                w = inside ? 1f : 1f - Mathf.SmoothStep(0f, 1f, edge / Mathf.Max(1e-4f, reach));
            }
            // the whole limb moves fully, the soft edge lies outside it: a half-moved edge would shear into a sawtooth
            else w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(reach, reach * 1.5f, best));
            if (bestS < 0f && c.D.poly == null)
            {
                // the body behind the root stays put
                float behind = -bestS * Vector2.Distance(c.P[0], c.P[1]);
                w *= 1f - Mathf.SmoothStep(0f, 1f, behind / (reach * 0.5f));
            }
            if (w <= 0.001f) return false;
            float s = Mathf.Clamp(bestS, 0f, c.Segs);
            // neighbouring bones blend smoothly across each joint
            if (s <= 0.5f || c.Segs == 1) { b.SegA = b.SegB = s <= 0.5f ? 0 : Mathf.Min(c.Segs - 1, (int)s); b.T = 0f; }
            else if (s >= c.Segs - 0.5f) { b.SegA = b.SegB = c.Segs - 1; b.T = 0f; }
            else
            {
                int j = Mathf.FloorToInt(s - 0.5f);
                b.SegA = j; b.SegB = j + 1; b.T = Mathf.SmoothStep(0f, 1f, s - 0.5f - j);
            }
            b.W = w;
            return true;
        }

        static float PolyDistance(float[] poly, Vector2 p, out bool inside)
        {
            int n = poly.Length / 2;
            inside = false;
            float best = float.MaxValue;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = new Vector2(poly[i * 2], poly[i * 2 + 1]), b = new Vector2(poly[j * 2], poly[j * 2 + 1]);
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                Vector2 e = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, e) / Mathf.Max(1e-8f, e.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(p, a + e * t));
            }
            return best;
        }

        /// <summary>A hit shoves the body along dir (facing frame): limbs and the top of the body lag behind.</summary>
        public void Impulse(Vector2 dir, float strength)
        {
            foreach (var c in chains)
                for (int k = 0; k < c.Segs; k++)
                {
                    Vector2 d = (c.P[k + 1] - c.P[k]).normalized;
                    c.AngleVel[k] -= (d.x * dir.y - d.y * dir.x) * strength * c.D.inertia * 8f;
                }
            bendVel -= dir.x * strength * 0.8f * bendAmount;
        }

        public void Update(in WarpDrive m)
        {
            float dt = Mathf.Min(m.Dt, 0.05f);
            Vector2 acc = Vector2.ClampMagnitude(m.Acc, 250f);
            if (!m.Frozen)
            {
                foreach (var c in chains) Simulate(c, m, acc, dt);
                // jelly: the top lags behind the sole when the body speeds up or turns
                float target = -0.012f * m.Vel.x * bendAmount;
                float k = 16f;
                bendVel += (-k * k * (bend - target) - 2f * 0.35f * k * bendVel - acc.x * 0.1f * bendAmount) * dt;
                bend = Mathf.Clamp(bend + bendVel * dt, -0.16f * height, 0.16f * height);
                for (int p = 0; p < pulses.Length; p++)
                {
                    var q = pulses[p];
                    pulseScale[p] = q.amp * Mathf.Sin(m.Time * q.freq + q.phase) + q.charge * Mathf.Max(m.Charge, m.Lunge);
                }
            }

            if (m.Hidden) return;
            for (int v = 0; v < rest.Length; v++) now[v] = rest[v];
            foreach (var b in binds)
            {
                var c = chains[b.Chain];
                Vector3 p = rest[b.V];
                Vector2 pa = Apply(c, b.SegA, p), pb = b.SegB == b.SegA ? pa : Apply(c, b.SegB, p);
                Vector2 to = Vector2.LerpUnclamped(pa, pb, b.T);
                now[b.V] += (Vector3)((to - (Vector2)p) * b.W);
            }
            foreach (var b in pulseBinds)
            {
                var q = pulses[b.Pulse];
                Vector3 p = rest[b.V];
                now[b.V] += new Vector3(p.x - q.x, p.y - q.y, 0f) * (pulseScale[b.Pulse] * b.W);
            }
            if (bendAmount > 0f && height > 0.01f)
                for (int v = 0; v < now.Length; v++)
                {
                    float h = Mathf.Clamp01(rest[v].y / height);
                    now[v].x += bend * h * Mathf.Sqrt(h);
                }
            buf.CopyFrom(now);
            Sprite.SetVertexAttribute(VertexAttribute.Position, buf);
        }

        static Vector2 Apply(ChainRt c, int k, Vector2 p)
        {
            Vector2 d = p - c.P[k];
            return c.Q[k] + new Vector2(d.x * c.Cos[k] - d.y * c.Sin[k], d.x * c.Sin[k] + d.y * c.Cos[k]);
        }

        static void Simulate(ChainRt c, in WarpDrive m, Vector2 acc, float dt)
        {
            var d = c.D;
            float speed = 1f + d.speedup * m.Speed01;
            // the bends add up along the chain: each bone takes its share, so the whole limb trails by drag · speed
            float share = 1f / c.Segs, limit = 50f * share + 6f;
            float phi = 0f;
            c.Q[0] = c.P[0];
            // own clock: a changing beat speed must not jump the phase
            c.Clock += dt * d.freq * speed;
            for (int k = 0; k < c.Segs; k++)
            {
                Vector2 dir = (c.P[k + 1] - c.P[k]).normalized;
                // the bone as a damped spring: air drag pulls it into the trail, acceleration flings it
                bool planted = c.Leg && m.Grounded;
                float target = planted ? 0f : share * d.drag * -(dir.x * m.Vel.y - dir.y * m.Vel.x);
                float fling = planted ? 0f : share * d.inertia * -(dir.x * acc.y - dir.y * acc.x);
                float stiff = d.stiff * (1f - 0.3f * k / Mathf.Max(1, c.Segs - 1));
                c.AngleVel[k] += (-stiff * stiff * (c.Angle[k] - target) - 2f * d.damp * stiff * c.AngleVel[k] + fling) * dt;
                c.Angle[k] = Mathf.Clamp(c.Angle[k] + c.AngleVel[k] * dt, -limit, limit);

                float own = d.amp * Mathf.Sin(c.Clock + d.phase - k * d.lag);
                if (k == 0) own += d.windup * m.Charge + d.thrust * m.Lunge;
                phi += (own + c.Angle[k]) * Mathf.Deg2Rad;
                c.Cos[k] = Mathf.Cos(phi); c.Sin[k] = Mathf.Sin(phi);
                Vector2 e = c.P[k + 1] - c.P[k];
                c.Q[k + 1] = c.Q[k] + new Vector2(e.x * c.Cos[k] - e.y * c.Sin[k], e.x * c.Sin[k] + e.y * c.Cos[k]);
            }
        }

        public void Dispose()
        {
            if (buf.IsCreated) buf.Dispose();
            if (Sprite != null) Object.Destroy(Sprite);
        }
    }
}
