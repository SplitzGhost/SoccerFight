using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// What the basketball moves leave in the arena: the dunk's shock waves (rings that run out from
    /// the slam in every direction and throw back whatever they pass), the three's small blast, the
    /// alley-oop's impact, ghost balls on an arc (Dreier-Regen, the alley-oop's second contact) and
    /// burning ground (Splash Zone). Damage only happens on the screen of the player who made the
    /// move; the partner sees the same shapes through CoopFx.
    /// </summary>
    public sealed class Court
    {
        public static Court I { get; private set; }

        sealed class Wave
        {
            public Vector2 at;
            public float maxR, dmg, knock, stun, delay, age, dustT;
            public bool local, started;
            public readonly HashSet<int> hit = new HashSet<int>();
        }

        sealed class Lob
        {
            public Vector2 from, to;
            public float t, dur, h, dmg, radius, spin;
            public bool active;
            public Transform root, spinNode;
            public SpriteRenderer pattern, glow;
            public TrailRenderer trail;
        }

        sealed class Zone
        {
            public Vector2 at;
            public float r, life, age, tick, fxT;
            public SpriteRenderer glow;
        }

        const float WaveSpeed = 15f;

        readonly List<Wave> waves = new List<Wave>();
        readonly List<Lob> lobs = new List<Lob>();
        readonly List<Zone> zones = new List<Zone>();
        Transform parent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        public void Build(Transform root)
        {
            I = this;
            parent = new GameObject("Court").transform;
            parent.SetParent(root, false);
        }

        public void Clear()
        {
            waves.Clear();
            foreach (var l in lobs) { l.active = false; l.root.gameObject.SetActive(false); }
            foreach (var z in zones) Object.Destroy(z.glow.gameObject);
            zones.Clear();
        }

        // ------------------------------------------------------------------ dunk shock waves

        /// <summary>A ring of force running out from at to maxR. local: it hurts (the partner's only shows).</summary>
        public void Shockwave(Vector2 at, float maxR, float dmg, float knock, float stun, float delay, bool local)
        {
            waves.Add(new Wave { at = at, maxR = maxR, dmg = dmg, knock = knock, stun = stun, delay = delay, local = local });
        }

        void UpdateWaves(float dt)
        {
            var fx = FxSystem.I;
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var w = waves[i];
                w.age += dt;
                if (w.age < w.delay) continue;
                float t = w.age - w.delay;
                float r = 0.25f + t * WaveSpeed;
                float life = w.maxR / WaveSpeed;
                if (!w.started)
                {
                    w.started = true;
                    // the ring itself, a softer echo inside it and the air bending around the front
                    fx.Ring(FxLayer.Front, w.at, 0.3f, w.maxR, 0.16f, 0.03f, life, Color.white.WithAlpha(0.8f), Palette.Slam.WithAlpha(0f), 1.8f);
                    fx.Ring(FxLayer.Front, w.at, 0.2f, w.maxR * 0.82f, 0.08f, 0.015f, life * 1.1f, Palette.Slam.WithAlpha(0.5f), Palette.Guard.WithAlpha(0f), 1.6f);
                    fx.Ring(FxLayer.Back, w.at, 0.4f, w.maxR * 1.05f, 0.6f, 0.1f, life * 1.2f, new Color(0.6f, 0.8f, 1f, 0.25f), new Color(0.6f, 0.8f, 1f, 0f), 1.2f, false, false);
                }
                // the front rolls along the ground both ways: a wall of dust and grit
                w.dustT -= dt;
                if (w.dustT <= 0f && r < w.maxR)
                {
                    w.dustT = 0.03f;
                    float floor = w.at.y - 0.15f;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector2 p = new Vector2(w.at.x + s * r, Level.FloorBelow(w.at.x + s * r, floor + 0.5f));
                        if (p.y < floor - 0.6f) continue;   // the front ran off the edge of a platform
                        fx.Dust(p, new Vector2(s * 0.6f, 1f), 1, 2.4f, 0.45f, 0.32f);
                        if (Random.value < 0.5f)
                            fx.Streak(FxLayer.Front, p + new Vector2(0f, Random.Range(0.1f, 0.9f)), new Vector2(s * Random.Range(4f, 7f), 0f), 0.18f, 0.03f, 0.05f,
                                Color.white.WithAlpha(0.7f), Palette.Slam.WithAlpha(0f), 2f, 4f);
                    }
                }
                if (w.local) HitWave(w, r);
                if (r >= w.maxR) waves.RemoveAt(i);
            }
        }

        void HitWave(Wave w, float r)
        {
            var list = Game.I.Waves.Monsters;
            for (int k = 0; k < list.Count; k++)
            {
                var m = list[k];
                if (!m.Alive || w.hit.Contains(m.Id)) continue;
                Vector2 d = m.Center - w.at;
                float dist = d.magnitude;
                if (dist - m.Radius > r) continue;
                w.hit.Add(m.Id);
                float fall = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(dist / w.maxR));
                Vector2 dir = (dist > 0.01f ? d / dist : Vector2.up) + Vector2.up * 0.55f;
                bool killed = Combat.Hit(m, w.dmg * fall, dir, w.knock * fall, Src.Dunk, big: true);
                if (!killed && w.stun > 0f) m.Stun(w.stun);
                if (killed) Game.I.Player.OnDunkKill();
                FxSystem.I.Sparks(m.Center, dir, 50f, 5, 4f, 9f, Palette.Slam, 2.4f, 0.04f, 0.22f);
            }
        }

        // ------------------------------------------------------------------ blasts

        /// <summary>The three lands: a small, bright blast. primary: the real ball (not a ghost), it can score the +3.</summary>
        public static void ThreeBlast(Vector2 p, float radius, float damage, bool primary)
        {
            var game = Game.I;
            int caught = 0;
            foreach (var m in game.Waves.Monsters)
                if (m.Alive && (m.Center - p).magnitude <= radius + m.Radius) caught++;
            game.Waves.Blast(p, radius, damage, Src.Three);
            CoopFx.Send(CoopFx.Kind.Three, p, radius, Palette.HoopFlame);
            BlastFx(p, radius, Palette.HoopFlame, primary ? 1f : 0.7f);
            if (game.Run.Stats.ThreeBurn) I?.FireZone(p, radius * 0.9f, 3f);
            if (primary && caught > 0)
                game.Hud.Popup(p + new Vector2(0f, radius * 0.6f + 0.6f), "+3", Palette.Gold, 40f, true);
        }

        /// <summary>The alley-oop comes down: a heavy golden impact; with Zweiter Kontakt it jumps on to another monster.</summary>
        public static void OopBlast(Vector2 p, float radius, float damage, Monster first)
        {
            var game = Game.I;
            game.Waves.Blast(p, radius, damage, Src.AlleyOop);
            CoopFx.Send(CoopFx.Kind.Oop, p, radius, Palette.Oop);
            BlastFx(p, radius, Palette.Oop, 0.95f);
            game.Hud.Popup(p + new Vector2(0f, radius * 0.6f + 0.7f), "ALLEY-OOP", Palette.Oop, 30f, false);
            if (game.Run.Stats.OopBounce && I != null)
            {
                var next = Combat.NearestTo(p, 12f, first);
                if (next != null && (next.Center - p).magnitude > radius * 0.6f)
                    I.GhostLob(p + new Vector2(0f, 0.3f), next.Center, 0.45f, damage * 0.6f, radius * 0.8f, Palette.Oop);
            }
        }

        /// <summary>The look of a basketball blast (also the partner's, from CoopFx).</summary>
        public static void BlastFx(Vector2 p, float radius, Color c, float power)
        {
            var fx = FxSystem.I;
            var game = Game.I;
            fx.Flash(p, radius * 0.95f * power, c, 0.18f, 2.2f);
            fx.Flash(p, radius * 0.3f, Color.white, 0.07f, 2.4f);
            fx.Ring(FxLayer.Front, p, 0.2f, radius * 1.05f, 0.16f, 0.015f, 0.34f, Color.white.WithAlpha(0.85f), c.WithAlpha(0f), 1.9f);
            fx.Ring(FxLayer.Front, p, 0.1f, radius * 0.6f, 0.12f, 0.01f, 0.22f, Palette.Gold, Palette.BlastOrange.WithAlpha(0f), 2f);
            fx.Sparks(p, Vector2.up, 150f, Mathf.RoundToInt(16 * power), 5f, 13f, c, 2.6f, 0.05f, 0.36f, 10f);
            for (int i = 0; i < Mathf.RoundToInt(10 * power); i++)
            {
                float ang = Random.Range(15f, 165f);
                Color col = Color.Lerp(Palette.Gold, c, Random.value);
                fx.Streak(FxLayer.Front, p, MathUtil.Dir(ang) * Random.Range(5f, 11f), Random.Range(0.2f, 0.4f), 0.06f, 0.04f,
                    Color.Lerp(col, Color.white, 0.35f), col.WithAlpha(0f), 2.6f, 3.5f, 9f);
            }
            fx.Dust(p, Vector2.right, 6, 2.8f, 0.5f, 0.36f);
            fx.Dust(p, Vector2.left, 6, 2.8f, 0.5f, 0.36f);
            fx.Sparkles(p + Vector2.up * 0.3f, radius * 0.5f, 8, Palette.Gold, 2.8f, 0.6f);
            game.Cam.AddTrauma(0.28f * power);
            game.Cam.Kick(new Vector2(0f, -0.18f * power));
            game.Post.Impact(0.25f * power);
            TimeFx.HitStop(0.045f, 0.05f);
        }

        // ------------------------------------------------------------------ ghost balls on an arc

        /// <summary>A spectral basketball that arcs from → to in flight seconds and bursts there.</summary>
        public void GhostLob(Vector2 from, Vector2 to, float flight, float dmg, float radius, Color? color = null)
        {
            Lob l = null;
            foreach (var x in lobs) if (!x.active) { l = x; break; }
            if (l == null)
            {
                l = new Lob { root = new GameObject("GhostLob").transform };
                l.root.SetParent(parent, false);
                l.glow = Art.MakeSprite("Glow", l.root, Art.SoftGlow, 126, Art.SpriteGlowMat, Color.clear);
                l.spinNode = new GameObject("Spin").transform;
                l.spinNode.SetParent(l.root, false);
                l.pattern = Art.MakeSprite("Pattern", l.spinNode, Art.HoopPattern, 127, Art.SpriteAddMat, Color.white);
                var go = new GameObject("Trail");
                go.transform.SetParent(l.root, false);
                l.trail = go.AddComponent<TrailRenderer>();
                l.trail.sharedMaterial = Art.TrailShotMat;
                l.trail.time = 0.22f;
                l.trail.minVertexDistance = 0.03f;
                l.trail.widthMultiplier = Art.BallRadius * 1.6f;
                l.trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
                l.trail.numCapVertices = 3;
                l.trail.sortingOrder = 125;
                lobs.Add(l);
            }
            Color c = color ?? Palette.HoopFlame;
            l.from = from; l.to = to; l.t = 0f; l.dur = Mathf.Max(0.2f, flight);
            l.h = Mathf.Max(1.6f, Mathf.Abs(to.x - from.x) * 0.3f);
            l.dmg = dmg; l.radius = radius; l.active = true;
            l.root.gameObject.SetActive(true);
            l.root.position = from;
            l.trail.Clear();
            l.trail.colorGradient = Ball.TrailGradient(Color.white, c, c * 0.6f);
            l.trail.emitting = true;
            l.glow.color = c.WithAlpha(0.6f);
            l.pattern.color = Color.Lerp(c, Color.white, 0.5f).WithAlpha(0.8f);
        }

        void UpdateLobs(float dt)
        {
            foreach (var l in lobs)
            {
                if (!l.active) continue;
                l.t += dt;
                float k = Mathf.Clamp01(l.t / l.dur);
                Vector2 p = Vector2.Lerp(l.from, l.to, k) + new Vector2(0f, l.h * 4f * k * (1f - k));
                l.root.position = p;
                l.spin -= Mathf.Sign(l.to.x - l.from.x) * 720f * dt;
                l.spinNode.localRotation = Quaternion.Euler(0f, 0f, l.spin);
                l.glow.transform.localScale = Vector3.one * (1.2f + 0.15f * Mathf.Sin(l.t * 30f));
                if (k < 1f) continue;
                l.active = false;
                l.trail.emitting = false;
                l.root.gameObject.SetActive(false);
                ThreeBlast(l.to, l.radius, l.dmg, false);
            }
        }

        // ------------------------------------------------------------------ burning ground

        public void FireZone(Vector2 at, float r, float life)
        {
            var z = new Zone { at = at, r = r, life = life };
            z.glow = Art.MakeSprite("FireZone", parent, Art.SoftGlow, -30, Art.SpriteGlowMat, Palette.BlastOrange.WithAlpha(0f));
            z.glow.transform.position = new Vector3(at.x, at.y - 0.1f, 0f);
            z.glow.transform.localScale = new Vector3(r * 2.6f, r * 0.7f, 1f);
            zones.Add(z);
        }

        void UpdateZones(float dt)
        {
            var fx = FxSystem.I;
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                var z = zones[i];
                z.age += dt;
                float k = z.age / z.life;
                float fade = Mathf.Clamp01(z.age / 0.15f) * (1f - MathUtil.Smooth01((k - 0.75f) / 0.25f));
                z.glow.color = Palette.BlastOrange.WithAlpha(0.35f * fade * (0.85f + 0.15f * Mathf.Sin(z.age * 20f)));
                z.fxT -= dt;
                if (z.fxT <= 0f)
                {
                    z.fxT = 0.03f;
                    Vector2 p = z.at + new Vector2(Random.Range(-z.r, z.r), -0.1f);
                    fx.Streak(FxLayer.Front, p, new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(1.5f, 3.5f)), Random.Range(0.3f, 0.55f), 0.09f, 0.05f,
                        new Color(1f, 0.85f, 0.45f).WithAlpha(fade), Palette.BlastOrange.WithAlpha(0f), 2.6f, 1.2f, 2f);
                }
                z.tick -= dt;
                if (z.tick <= 0f)
                {
                    z.tick = 0.3f;
                    foreach (var m in Game.I.Waves.Monsters)
                    {
                        if (!m.Alive) continue;
                        Vector2 d = m.Center - z.at;
                        if (Mathf.Abs(d.x) < z.r + m.Radius * 0.5f && d.y > -0.8f && d.y < 1.2f + m.Radius) m.Ignite(8f, 3f);
                    }
                }
                if (z.age >= z.life) { Object.Destroy(z.glow.gameObject); zones.RemoveAt(i); }
            }
        }

        public void Update(float dt)
        {
            UpdateWaves(dt);
            UpdateLobs(dt);
            UpdateZones(dt);
        }
    }
}
