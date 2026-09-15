using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Enemy shots: arcing lobs that splash where they land, straight glowing bolts, and ground
    /// shockwaves that roll along the surface they were born on (jump over them). Pooled sprites.
    /// </summary>
    public sealed class EnemyProjectiles
    {
        public static EnemyProjectiles I { get; private set; }

        enum PKind { Lob, Bolt, Wave }

        sealed class Proj
        {
            public PKind kind;
            public Vector2 pos, vel;
            public float dmg, age, life, floorY, dir;
            public Color color;
            public bool active, hit;
            public Transform root;
            public SpriteRenderer core, glow;
            public TrailRenderer trail;
        }

        readonly List<Proj> pool = new List<Proj>();
        Transform parent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        public void Build(Transform root)
        {
            I = this;
            parent = new GameObject("Enemy Projectiles").transform;
            parent.SetParent(root, false);
        }

        Proj Get()
        {
            foreach (var p in pool) if (!p.active) return p;
            var n = new Proj { root = new GameObject("Proj").transform };
            n.root.SetParent(parent, false);
            n.glow = Art.MakeSprite("Glow", n.root, Art.SoftGlow, 118, Art.SpriteGlowMat, Color.clear);
            n.core = Art.MakeSprite("Core", n.root, MonsterArt.Orb, 119, Art.SpriteEmissiveMat, Color.white);
            var go = new GameObject("Trail");
            go.transform.SetParent(n.root, false);
            n.trail = go.AddComponent<TrailRenderer>();
            n.trail.sharedMaterial = Art.TrailSoftMat;
            n.trail.time = 0.22f;
            n.trail.minVertexDistance = 0.04f;
            n.trail.widthMultiplier = 0.24f;
            n.trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            n.trail.numCapVertices = 3;
            n.trail.sortingOrder = 117;
            n.trail.emitting = false;
            pool.Add(n);
            return n;
        }

        Proj Start(PKind kind, Vector2 pos, Vector2 vel, float dmg, Color c, float life)
        {
            var p = Get();
            p.kind = kind; p.pos = pos; p.vel = vel; p.dmg = dmg * StageMechanics.EnemyDamageBoost; p.color = c;
            p.age = 0f; p.life = life; p.hit = false; p.active = true;
            p.root.gameObject.SetActive(true);
            p.root.position = pos;
            p.trail.Clear();
            p.trail.emitting = kind != PKind.Wave;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.Lerp(c, Color.white, 0.4f), 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            p.trail.colorGradient = g;
            FxSystem.I.Flash(pos, 0.9f, c, 0.12f, 2.4f);
            return p;
        }

        /// <summary>Arc onto a target point (timed so it always lands there).</summary>
        public void Lob(Vector2 from, Vector2 target, float dmg, Color c)
        {
            const float g = 16f;
            float dx = target.x - from.x;
            float flight = Mathf.Clamp(0.55f + Mathf.Abs(dx) * 0.07f, 0.6f, 1.25f);
            float vy = (target.y - from.y + 0.5f * g * flight * flight) / flight;
            Start(PKind.Lob, from, new Vector2(dx / flight, vy), dmg, c, 3f);
        }

        public void Bolt(Vector2 from, Vector2 dir, float speed, float dmg, Color c) => Start(PKind.Bolt, from, dir.normalized * speed, dmg, c, 3.5f);

        /// <summary>A crest that rolls along the surface at groundPoint.y in direction dir (±1).</summary>
        public void Shockwave(Vector2 groundPoint, float dir, float dmg, Color c)
        {
            var p = Start(PKind.Wave, groundPoint, new Vector2(dir * 8.5f, 0f), dmg, c, 1.5f);
            p.floorY = groundPoint.y;
            p.dir = dir;
        }

        public void Clear()
        {
            foreach (var p in pool) { p.active = false; p.root.gameObject.SetActive(false); }
        }

        void Pop(Proj p, float splash, Player player)
        {
            var fx = FxSystem.I;
            fx.Ring(FxLayer.Front, p.pos, 0.1f, 0.6f + splash, 0.14f, 0.01f, 0.24f, Color.white, p.color.WithAlpha(0f), 2.2f);
            fx.Sparks(p.pos, Vector2.up, 160f, 7, 2.5f, 6f, p.color, 2.4f, 0.04f, 0.26f, 8f);
            if (splash > 0f && !p.hit && !player.Dead && (player.Pos + new Vector2(0f, 0.6f) - p.pos).magnitude < splash)
                player.TakeDamage(p.dmg, p.pos);
            p.active = false;
            p.root.gameObject.SetActive(false);
        }

        public void Update(float dt, Player player)
        {
            Vector2 pc = player.Pos + new Vector2(0f, 0.8f);
            foreach (var p in pool)
            {
                if (!p.active) continue;
                p.age += dt;
                float pulse = 0.85f + 0.15f * Mathf.Sin(p.age * 30f);
                switch (p.kind)
                {
                    case PKind.Lob:
                    {
                        Vector2 prev = p.pos;
                        p.vel.y -= 16f * dt;
                        p.pos += p.vel * dt;
                        float floor = Level.FloorBelow(p.pos.x, prev.y + 0.02f);
                        if (!p.hit && (pc - p.pos).magnitude < 0.55f) { p.hit = true; player.TakeDamage(p.dmg, p.pos); Pop(p, 0f, player); continue; }
                        if (p.pos.y <= floor + 0.08f || p.age > p.life) { p.pos.y = Mathf.Max(p.pos.y, floor + 0.08f); Pop(p, 0.95f, player); continue; }
                        break;
                    }
                    case PKind.Bolt:
                    {
                        p.pos += p.vel * dt;
                        if (!p.hit && (pc - p.pos).magnitude < 0.5f) { p.hit = true; player.TakeDamage(p.dmg, p.pos); Pop(p, 0f, player); continue; }
                        if (p.pos.y < Level.FloorBelow(p.pos.x, p.pos.y + 0.3f) + 0.05f || Mathf.Abs(p.pos.x) > Player.ArenaHalf + 2f || p.pos.y > 12f || p.age > p.life)
                        { Pop(p, 0f, player); continue; }
                        break;
                    }
                    case PKind.Wave:
                    {
                        p.pos.x += p.vel.x * dt;
                        // the crest ends where its surface ends (platform edge or arena wall)
                        bool offSurface = Level.FloorBelow(p.pos.x, p.floorY + 0.02f) < p.floorY - 0.05f;
                        if (offSurface || Mathf.Abs(p.pos.x) > Player.ArenaHalf + 0.5f || p.age > p.life) { Pop(p, 0f, player); continue; }
                        if (!p.hit && !player.Dead && Mathf.Abs(player.Pos.x - p.pos.x) < 0.45f && player.Pos.y - p.floorY < 0.75f)
                        { p.hit = true; player.TakeDamage(p.dmg, p.pos); }
                        if (Random.value < dt * 50f)
                            FxSystem.I.Dust(new Vector2(p.pos.x, p.floorY), new Vector2(p.dir * 0.3f, 1f), 1, 2.2f, 0.4f, 0.3f);
                        break;
                    }
                }

                p.root.position = new Vector3(p.pos.x, p.kind == PKind.Wave ? p.floorY + 0.35f : p.pos.y, 0f);
                if (p.kind == PKind.Wave)
                {
                    // tall thin crest leaning forwards
                    p.core.transform.localScale = new Vector3(1.4f, 5.5f, 1f) * (1f - 0.3f * Mathf.Clamp01(p.age / p.life));
                    p.core.transform.localRotation = Quaternion.Euler(0f, 0f, -p.dir * 14f);
                    p.core.color = Color.Lerp(Color.white, p.color, 0.5f) * pulse;
                    p.glow.transform.localScale = new Vector3(1.8f, 2.6f, 1f);
                    p.glow.color = p.color.WithAlpha(0.55f * pulse);
                }
                else
                {
                    float ang = MathUtil.Angle(p.vel);
                    float stretch = p.kind == PKind.Bolt ? 1.6f : 1.15f;
                    p.core.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
                    p.core.transform.localScale = new Vector3(1.6f * stretch, 1.6f, 1f);
                    p.core.color = Color.Lerp(Color.white, p.color, 0.35f) * pulse;
                    p.glow.transform.localScale = Vector3.one * 1.1f;
                    p.glow.color = p.color.WithAlpha(0.6f * pulse);
                }
            }
        }
    }
}
