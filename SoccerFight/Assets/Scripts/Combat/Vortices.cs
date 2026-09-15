using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Pulling fields: the step-over cyclone (mint swirl) and the power-shot singularity (a dark core
    /// ringed by light that implodes at the end). Both drag monsters in and tick damage.
    /// </summary>
    public sealed class Vortices
    {
        public static Vortices I { get; private set; }

        sealed class Vortex
        {
            public Vector2 pos;
            public float radius, life, age, dps, implodeDmg, tick;
            public bool implode, active;
            public Color color;
            public Transform root;
            public SpriteRenderer glow, core, ringA, ringB, rim;
        }

        readonly List<Vortex> pool = new List<Vortex>();
        Transform parent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        public void Build(Transform root)
        {
            I = this;
            parent = new GameObject("Vortices").transform;
            parent.SetParent(root, false);
        }

        Vortex Get()
        {
            foreach (var v in pool) if (!v.active) return v;
            var n = new Vortex { root = new GameObject("Vortex").transform };
            n.root.SetParent(parent, false);
            n.glow = Art.MakeSprite("Glow", n.root, Art.SoftGlow, 50, Art.SpriteGlowMat, Color.clear);
            n.ringA = Art.MakeSprite("RingA", n.root, Art.Ring, 51, Art.SpriteGlowMat, Color.clear);
            n.ringB = Art.MakeSprite("RingB", n.root, Art.Ring, 51, Art.SpriteGlowMat, Color.clear);
            n.core = Art.MakeSprite("Core", n.root, Art.Circle, 52, Art.SpriteMat, Color.clear);
            n.rim = Art.MakeSprite("Rim", n.root, Art.Ring, 53, Art.SpriteGlowMat, Color.clear);
            pool.Add(n);
            return n;
        }

        /// <summary>dps and implodeDmg are base damage (the build multiplier is applied by Combat).</summary>
        public void Spawn(Vector2 at, float radius, float life, float dps, float implodeDmg, bool implode, Color c)
        {
            var v = Get();
            v.pos = at; v.radius = radius; v.life = life; v.age = 0f; v.dps = dps; v.implodeDmg = implodeDmg;
            v.implode = implode; v.color = c; v.tick = 0f; v.active = true;
            v.root.gameObject.SetActive(true);
            v.root.position = at;
            v.core.enabled = implode;
            v.rim.enabled = implode;
            FxSystem.I.Flash(at, radius * 1.2f, c, 0.2f, 2.4f);
            FxSystem.I.Ring(FxLayer.Front, at, radius * 1.3f, 0.2f, 0.05f, 0.25f, 0.35f, c.WithAlpha(0f), Color.white, 2.2f);
        }

        public void Clear()
        {
            foreach (var v in pool) { v.active = false; v.root.gameObject.SetActive(false); }
        }

        public void Update(float dt)
        {
            var list = Game.I.Waves.Monsters;
            var fx = FxSystem.I;
            foreach (var v in pool)
            {
                if (!v.active) continue;
                v.age += dt;
                float k = v.age / v.life;
                float grow = MathUtil.EaseOutBack(Mathf.Clamp01(v.age / 0.3f), 1.6f);
                float shrink = 1f - MathUtil.Smooth01((k - 0.85f) / 0.15f);
                float r = v.radius;

                // pull and tick
                v.tick -= dt;
                bool tick = v.tick <= 0f;
                if (tick) v.tick = 0.25f;
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    if (!m.Alive) continue;
                    Vector2 d = v.pos - m.Center;
                    float dist = d.magnitude;
                    if (dist > r * 1.25f + m.Radius) continue;
                    m.PullStrength = Mathf.Max(m.PullStrength, v.implode ? 5f : 3.5f);
                    m.PullTo = v.pos;
                    if (tick && dist < r + m.Radius) Combat.Hit(m, v.dps * 0.25f, d.normalized, 0.5f, Src.Vortex);
                }

                // visuals: two counter-rotating rings, motes spiralling in
                float spin = v.age * (v.implode ? 520f : 380f);
                float s = r * 0.55f * grow * shrink;
                v.glow.color = v.color.WithAlpha(0.35f * shrink);
                v.glow.transform.localScale = Vector3.one * r * 1.1f * grow;
                v.ringA.color = v.color.WithAlpha(0.7f * shrink);
                v.ringA.transform.localScale = new Vector3(s * 1.15f, s * 0.9f, 1f);
                v.ringA.transform.localRotation = Quaternion.Euler(0f, 0f, spin);
                v.ringB.color = Color.Lerp(v.color, Color.white, 0.5f).WithAlpha(0.5f * shrink);
                v.ringB.transform.localScale = new Vector3(s * 0.7f, s * 0.85f, 1f);
                v.ringB.transform.localRotation = Quaternion.Euler(0f, 0f, -spin * 1.4f);
                if (v.implode)
                {
                    float cs = r * 0.16f * grow * shrink * (1f + 0.06f * Mathf.Sin(v.age * 30f));
                    v.core.color = new Color(0.02f, 0.01f, 0.05f, 0.95f * shrink);
                    v.core.transform.localScale = Vector3.one * cs;
                    v.rim.color = Color.Lerp(Color.white, v.color, 0.3f).WithAlpha(0.9f * shrink);
                    v.rim.transform.localScale = Vector3.one * cs * 1.05f;
                }
                if (Random.value < dt * 70f)
                {
                    float a = Random.Range(0f, 360f);
                    Vector2 from = v.pos + MathUtil.Dir(a) * r * Random.Range(0.8f, 1.2f);
                    Vector2 tangent = MathUtil.Dir(a + 90f) * 3f;
                    fx.Streak(FxLayer.Front, from, (v.pos - from) * 2.2f + tangent, 0.35f, 0.03f, 0.06f,
                        Color.Lerp(v.color, Color.white, 0.4f), v.color.WithAlpha(0f), 2.2f, 1f);
                }

                if (v.age >= v.life)
                {
                    v.active = false;
                    v.root.gameObject.SetActive(false);
                    if (v.implode) Implode(v);
                }
            }
        }

        void Implode(Vortex v)
        {
            var fx = FxSystem.I;
            fx.Flash(v.pos, v.radius * 2.2f, v.color, 0.3f, 3f);
            fx.Flash(v.pos, v.radius, Color.white, 0.12f, 3.2f);
            fx.Ring(FxLayer.Front, v.pos, 0.2f, v.radius * 1.4f, 0.5f, 0.02f, 0.45f, Color.white, v.color.WithAlpha(0f), 2.6f);
            fx.Sparks(v.pos, Vector2.up, 360f, 24, 6f, 16f, v.color, 2.6f, 0.05f, 0.4f);
            Game.I.Cam.AddTrauma(0.45f);
            Game.I.Post.Impact(0.7f);
            var list = Game.I.Waves.Monsters;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m.Alive) continue;
                Vector2 d = m.Center - v.pos;
                if (d.magnitude > v.radius * 1.3f + m.Radius) continue;
                Combat.Hit(m, v.implodeDmg, d.normalized + Vector2.up * 0.4f, 10f, Src.Vortex, big: true);
            }
        }
    }

    /// <summary>Legendary companion: a spectral ball orbiting the player that fires at the nearest monster.</summary>
    public sealed class TwinSun
    {
        Transform root;
        SpriteRenderer glow, ball, halo;
        float angle, cooldown, visible;
        Vector2 pos;

        public void Build(Transform parent)
        {
            root = new GameObject("Twin Sun").transform;
            root.SetParent(parent, false);
            glow = Art.MakeSprite("Glow", root, Art.SoftGlow, 124, Art.SpriteGlowMat, Color.clear);
            halo = Art.MakeSprite("Halo", root, Art.Ring, 125, Art.SpriteGlowMat, Color.clear);
            ball = Art.MakeSprite("Ball", root, Art.BallPattern, 126, Art.SpriteAddMat, Color.clear);
            root.gameObject.SetActive(false);
        }

        public void Update(float dt, Player player, bool fighting)
        {
            bool on = Game.I.Run.Stats.TwinSun && !player.Dead;
            visible = Mathf.MoveTowards(visible, on ? 1f : 0f, dt * 3f);
            root.gameObject.SetActive(visible > 0f);
            if (visible <= 0f) return;

            angle += dt * 150f;
            Vector2 center = player.Pos + new Vector2(0f, 1.2f);
            Vector2 target = center + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad) * 1.25f, Mathf.Sin(angle * Mathf.Deg2Rad) * 0.55f);
            pos = Vector2.Lerp(pos == Vector2.zero ? target : pos, target, 1f - Mathf.Exp(-18f * dt));
            root.position = new Vector3(pos.x, pos.y, 0f);
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f);
            glow.color = Palette.Gold.WithAlpha(0.5f * visible * pulse);
            glow.transform.localScale = Vector3.one * 0.9f;
            halo.color = Palette.Gold.WithAlpha(0.35f * visible);
            halo.transform.localScale = Vector3.one * 0.28f * pulse;
            ball.color = Color.Lerp(Palette.Gold, Color.white, 0.5f).WithAlpha(0.8f * visible);
            ball.transform.localScale = Vector3.one * 0.7f;
            ball.transform.localRotation = Quaternion.Euler(0f, 0f, -angle * 3f);
            if (Random.value < dt * 20f) FxSystem.I.Motes(pos, Vector2.up * 0.3f, Palette.Gold, 1, 0.1f);

            if (!on || !fighting) return;
            cooldown -= dt;
            if (cooldown > 0f) return;
            var m = Combat.NearestTo(pos, 11f);
            if (m == null) return;
            cooldown = 1.2f * Game.I.Run.Stats.ShotCooldownMul;
            Vector2 dir = (m.Center - pos).normalized;
            EchoBalls.I.Fire(pos, dir, 24f, Player.ShotDamage, Src.TwinSun, Palette.Gold);
            FxSystem.I.Flash(pos, 0.9f, Palette.Gold, 0.12f, 2.6f);
            FxSystem.I.Ring(FxLayer.Front, pos, 0.05f, 0.5f, 0.08f, 0.01f, 0.16f, Color.white, Palette.Gold.WithAlpha(0f), 2f);
        }
    }
}
