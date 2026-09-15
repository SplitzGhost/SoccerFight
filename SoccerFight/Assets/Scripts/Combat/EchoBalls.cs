using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Spectral copies of the ball: multi-shot echoes, trident prongs, twin-sun shots, echo-flip
    /// bombs. Translucent ball with a glowing trail; each hits a monster once, then fades out.
    /// </summary>
    public sealed class EchoBalls
    {
        public static EchoBalls I { get; private set; }

        sealed class Echo
        {
            public Vector2 pos, vel;
            public float dmg, age, life;
            public Src src;
            public bool active, pierce, explosive, ricochet;
            public int bounces;
            public Color color;
            public float spin;
            public readonly HashSet<int> hit = new HashSet<int>();
            public Transform root, spinNode;
            public SpriteRenderer pattern, glow;
            public TrailRenderer trail;
        }

        readonly List<Echo> pool = new List<Echo>();
        Transform parent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        public void Build(Transform root)
        {
            I = this;
            parent = new GameObject("Echo Balls").transform;
            parent.SetParent(root, false);
        }

        Echo Get()
        {
            foreach (var e in pool) if (!e.active) return e;
            var n = new Echo { root = new GameObject("Echo").transform };
            n.root.SetParent(parent, false);
            n.glow = Art.MakeSprite("Glow", n.root, Art.SoftGlow, 126, Art.SpriteGlowMat, Color.clear);
            n.spinNode = new GameObject("Spin").transform;
            n.spinNode.SetParent(n.root, false);
            n.pattern = Art.MakeSprite("Pattern", n.spinNode, Art.BallPattern, 127, Art.SpriteAddMat, Color.white);
            var go = new GameObject("Trail");
            go.transform.SetParent(n.root, false);
            n.trail = go.AddComponent<TrailRenderer>();
            n.trail.sharedMaterial = Art.TrailShotMat;
            n.trail.time = 0.14f;
            n.trail.minVertexDistance = 0.03f;
            n.trail.widthMultiplier = Art.BallRadius * 1.5f;
            n.trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            n.trail.numCapVertices = 3;
            n.trail.sortingOrder = 125;
            pool.Add(n);
            return n;
        }

        /// <summary>dmg is the base (unscaled) damage; Combat applies the build and the echo fraction.</summary>
        public void Fire(Vector2 from, Vector2 dir, float speed, float dmg, Src src, Color color, bool pierce = false, bool explosive = false, bool ricochet = false)
        {
            var e = Get();
            e.pos = from; e.vel = dir.normalized * speed; e.dmg = dmg; e.src = src; e.color = color;
            e.age = 0f; e.life = pierce ? 0.8f : 0.7f; e.pierce = pierce; e.explosive = explosive; e.ricochet = ricochet;
            e.bounces = ricochet ? Mathf.Max(1, Game.I.Run.Stats.Ricochets) : 0;
            e.hit.Clear();
            e.active = true;
            e.root.gameObject.SetActive(true);
            e.root.position = from;
            e.trail.Clear();
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.4f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.35f, 0.5f), new GradientAlphaKey(0f, 1f) });
            e.trail.colorGradient = g;
            e.trail.emitting = true;
            e.spinNode.localScale = Vector3.one * 0.8f;
        }

        public void Clear()
        {
            foreach (var e in pool) { e.active = false; e.root.gameObject.SetActive(false); }
        }

        void Kill(Echo e, bool puff)
        {
            if (puff)
            {
                FxSystem.I.Ring(FxLayer.Front, e.pos, 0.05f, 0.45f, 0.08f, 0.01f, 0.16f, Color.white, e.color.WithAlpha(0f), 2f);
                FxSystem.I.Motes(e.pos, Vector2.up * 0.4f, e.color, 3, 0.1f);
            }
            e.active = false;
            e.root.gameObject.SetActive(false);
        }

        void Detonate(Echo e)
        {
            var s = Game.I.Run.Stats;
            Game.I.Waves.Blast(e.pos, Player.BlastRadius * 0.6f, Player.BlastDamage * 0.6f, Src.Blast);
            FxSystem.I.Flash(e.pos, 2.5f, Palette.BlastOrange, 0.16f, 2.6f);
            Kill(e, false);
        }

        public void Update(float dt)
        {
            var list = Game.I.Waves.Monsters;
            foreach (var e in pool)
            {
                if (!e.active) continue;
                e.age += dt;
                Vector2 prev = e.pos;
                if (!e.pierce) e.vel.y -= (e.explosive ? 18f : 3f) * dt;
                e.pos += e.vel * dt;

                bool done = false;
                for (int i = 0; i < list.Count && !done; i++)
                {
                    var m = list[i];
                    if (!m.Alive || e.hit.Contains(m.Id)) continue;
                    float r = m.Radius + Art.BallRadius * 0.8f;
                    if ((m.Center - e.pos).sqrMagnitude > r * r) continue;
                    e.hit.Add(m.Id);
                    if (e.explosive) { Detonate(e); done = true; break; }
                    Combat.Hit(m, e.dmg, e.vel.normalized, 4.5f, e.src, big: e.src == Src.Power);
                    if (e.pierce) continue;
                    if (e.bounces > 0)
                    {
                        var next = Combat.NearestTo(e.pos, 7f, m);
                        if (next != null && !e.hit.Contains(next.Id))
                        {
                            e.bounces--;
                            e.vel = (next.Center - e.pos).normalized * Mathf.Max(e.vel.magnitude, 18f);
                            e.age = Mathf.Min(e.age, 0.2f);
                            continue;
                        }
                    }
                    Kill(e, true);
                    done = true;
                }
                if (done) continue;

                float floor = Level.FloorBelow(e.pos.x, prev.y - Art.BallRadius + 0.02f);
                if (e.pos.y < floor + Art.BallRadius * 0.5f)
                {
                    if (e.explosive) { e.pos.y = floor + Art.BallRadius; Detonate(e); continue; }
                    Kill(e, true);
                    continue;
                }
                if (e.age > e.life || Mathf.Abs(e.pos.x) > Player.ArenaHalf + 1.5f) { if (e.explosive) Detonate(e); else Kill(e, false); continue; }

                float fade = 1f - Mathf.Clamp01((e.age - e.life + 0.15f) / 0.15f);
                e.root.position = new Vector3(e.pos.x, e.pos.y, 0f);
                e.spin -= Mathf.Sign(e.vel.x) * 900f * dt;
                e.spinNode.localRotation = Quaternion.Euler(0f, 0f, e.spin);
                e.pattern.color = Color.Lerp(e.color, Color.white, 0.5f).WithAlpha(0.75f * fade);
                e.glow.color = e.color.WithAlpha(0.55f * fade);
                e.glow.transform.localScale = Vector3.one * (e.explosive ? 1.3f : 1f);
            }
        }
    }
}
