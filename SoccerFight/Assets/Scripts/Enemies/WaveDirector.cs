using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Owns the monster pool: spawns out of the goal portals (the RunDirector decides what and when),
    /// resolves ball and contact collisions through Combat, and handles deaths (splitters, bombers,
    /// kill effects). Area helpers for the blast, the rainbow impact, wind and stage hazards live here.
    /// </summary>
    public sealed class WaveDirector
    {
        readonly List<Monster> monsters = new List<Monster>();
        Transform parent;
        WorldEnvironment env;
        int spawnSide;
        float leftCharge, rightCharge;
        float portalSpin;
        Color portalColor = Palette.MonsterGlow;

        /// <summary>Capture scenes switch the run off and place monsters by hand.</summary>
        public bool Enabled = true;

        public List<Monster> Monsters => monsters;
        public int AliveCount { get; private set; }
        /// <summary>Deaths since the last reset (the run director reads this for wave progress).</summary>
        public int Deaths { get; private set; }
        public Monster Boss { get; private set; }

        public const float RainbowRadius = 2.9f;
        public const float RainbowDamage = 65f;
        public const float RainbowPassDamage = 26f;

        public void Build(Transform root, WorldEnvironment environment)
        {
            parent = new GameObject("Monsters").transform;
            parent.SetParent(root, false);
            env = environment;
        }

        /// <summary>Clears the pitch. The delay is kept for the capture scripts (it no longer schedules anything).</summary>
        public void Restart(float delay = 0f)
        {
            foreach (var m in monsters) m.Deactivate();
            AliveCount = 0;
            Deaths = 0;
            Boss = null;
            EnemyProjectiles.I?.Clear();
        }

        public void SetTheme(StageTheme theme) => portalColor = theme.Glow;

        /// <summary>A pooled monster built for this body (every body has its own set of parts).</summary>
        Monster Get(in Monster.SpawnSpec spec)
        {
            var look = Monster.LookFor(spec);
            foreach (var m in monsters) if (!m.Alive && m.BuiltLook == look) return m;
            var nm = new Monster();
            nm.Build(parent, MonsterArt.Get(spec.Theme ?? StageThemes.All[0], look));
            monsters.Add(nm);
            return nm;
        }

        public Monster Spawn(in Monster.SpawnSpec spec)
        {
            var m = Get(spec);
            m.Spawn(spec);
            AliveCount++;
            if (spec.Rank == Rank.Boss) Boss = m;
            return m;
        }

        /// <summary>Debug/capture helper: drop a basic monster of the current stage somewhere specific.</summary>
        public Monster SpawnAt(Monster.Kind kind, Vector2 pos)
        {
            var run = Game.I.Run;
            return Spawn(new Monster.SpawnSpec
            {
                Type = kind == Monster.Kind.Wisp ? EnemyType.Diver : EnemyType.Hopper,
                At = pos, Level = run.Level, Theme = run.Theme, Rank = Rank.Normal,
            });
        }

        /// <summary>Spits a monster out of the next goal portal, alternating sides.</summary>
        public Monster SpawnFromPortal(EnemyType type, float level, Rank rank = Rank.Normal, int affixes = 0, string name = null, BossDef boss = null)
        {
            bool left = spawnSide++ % 2 == 0;
            Transform portal = left ? env.LeftPortal : env.RightPortal;
            Vector2 at = portal.position;
            float inward = left ? 1f : -1f;
            bool wisp = EnemyDef.Get(type).Body == Monster.Kind.Wisp;
            Vector2 v = wisp ? new Vector2(inward * 3f, 1.5f) : new Vector2(inward * Random.Range(3.5f, 5f), Random.Range(2.5f, 4f));
            if (rank >= Rank.MiniBoss) v *= 0.8f;
            var m = Spawn(new Monster.SpawnSpec
            {
                Type = type, At = at, Vel = v, Level = level, Rank = rank, Affixes = affixes,
                Theme = Game.I.Run.Theme, Name = name, Boss = boss,
            });
            if (left) leftCharge = 1.4f; else rightCharge = 1.4f;
            var fx = FxSystem.I;
            float big = rank >= Rank.MiniBoss ? 1.8f : rank == Rank.Elite ? 1.3f : 1f;
            fx.Flash(at, 2.6f * big, portalColor, 0.25f, 2.6f);
            fx.Ring(FxLayer.Front, at, 0.3f, 1.4f * big, 0.25f, 0.02f, 0.4f, portalColor, portalColor.WithAlpha(0f), 2.2f);
            fx.Sparks(at, new Vector2(inward, 0.3f), 90f, Mathf.RoundToInt(8 * big), 3f, 8f, portalColor, 2.4f, 0.05f, 0.3f);
            if (rank >= Rank.MiniBoss) Game.I.Cam.AddTrauma(0.25f);
            return m;
        }

        /// <summary>Bosses and splitters: a small monster popping out next to its parent.</summary>
        public void SpawnMinion(EnemyType type, Vector2 pos, Monster owner)
        {
            if (AliveCount >= 22) return;
            var m = Spawn(new Monster.SpawnSpec
            {
                Type = type, At = pos, Vel = new Vector2(Random.Range(-3f, 3f), Random.Range(3f, 5.5f)),
                Level = owner != null ? owner.DifficultyLevel : Game.I.Run.Level, Theme = Game.I.Run.Theme, Rank = Rank.Normal,
            });
            FxSystem.I.Ring(FxLayer.Front, m.Center, 0.1f, 0.9f, 0.14f, 0.01f, 0.25f, Color.white, portalColor.WithAlpha(0f), 2f);
            FxSystem.I.Motes(m.Center, Vector2.up, portalColor, 4, 0.2f);
        }

        /// <summary>Every death runs through here, whatever killed the monster.</summary>
        public void OnDied(Monster m)
        {
            AliveCount = Mathf.Max(0, AliveCount - 1);
            Deaths++;
            if (m == Boss) Boss = null;
            Combat.OnKill(m);
            CoinDrops.I?.Drop(m);
            if (m.Type == EnemyType.Splitter)
                for (int i = 0; i < 2; i++) SpawnMinion(EnemyType.Spawnling, m.Center + new Vector2((i - 0.5f) * 0.5f, 0.1f), m);
            m.OnDeathEffects();
            Game.I.Director?.OnMonsterDied(m);
        }

        /// <summary>Wind: a sideways shove for everything alive (bosses barely move).</summary>
        public void Push(float dvx)
        {
            foreach (var m in monsters)
            {
                if (!m.Alive) continue;
                float k = m.Rank == Rank.Boss ? 0.1f : m.Rank == Rank.MiniBoss ? 0.35f : m.K == Monster.Kind.Wisp ? 1f : 0.6f;
                m.Vel.x += dvx * k;
            }
        }

        /// <summary>Lightning strikes and geysers hurt monsters too.</summary>
        public void HazardHit(Vector2 c, float radius, float dmg, bool launch)
        {
            float scaled = dmg * Mathf.Sqrt(Difficulty.HealthMul(Game.I.Run.Level));
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (!m.Alive) continue;
                Vector2 d = m.Center - c;
                if (Mathf.Abs(d.x) > radius + m.Radius || Mathf.Abs(d.y) > 3.5f) continue;
                Combat.Hit(m, scaled, launch ? Vector2.up : new Vector2(Mathf.Sign(d.x), 0.4f), launch ? 12f : 6f, Src.Hazard, big: true);
            }
        }

        public void Update(float dt, Player player, Ball ball)
        {
            var stats = Game.I.Run.Stats;
            int alive = 0;
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (!m.Alive) continue;
                m.Update(dt, player);
                if (!m.Alive) continue;

                // ball vs monster
                if (ball.IsDangerous)
                {
                    Vector2 d = ball.Pos - m.Center;
                    float r = m.Radius + ball.Radius + 0.05f;
                    if (d.sqrMagnitude < r * r && ball.TryRegisterHit(m.Id)) BallHit(ball, m, d, stats);
                }

                // monster vs player
                if (m.Alive && !player.Dead)
                {
                    // the player is a capsule (shins to head), monsters a slightly forgiving circle —
                    // so a well-timed jump really clears a charging boss
                    Vector2 c = m.Center;
                    Vector2 closest = new Vector2(player.Pos.x, Mathf.Clamp(c.y, player.Pos.y + 0.3f, player.Pos.y + 1.45f));
                    float reach = m.Radius * (m.Rank >= Rank.MiniBoss ? 0.84f : 0.92f) + 0.3f;
                    if ((c - closest).sqrMagnitude < reach * reach)
                    {
                        if (player.IsDashing && stats.DashDamageFrac > 0f) player.DashStrike(m);
                        else if (m.Halted) { }   // knocked over or whistled: it cannot hurt anyone right now
                        else if (player.TakeDamage(m.ContactDamage * StageMechanics.EnemyDamageBoost, m.Center))
                            m.Vel.x = Mathf.Sign(m.Center.x - player.Pos.x) * (m.Rank >= Rank.MiniBoss ? 5f : 3.5f);   // bounce off instead of sitting on the player
                    }
                }
                if (m.Alive) alive++;
            }
            AliveCount = alive;
            Separate();

            // portals
            portalSpin += dt * 90f;
            leftCharge = Mathf.Max(0f, leftCharge - dt * 1.6f);
            rightCharge = Mathf.Max(0f, rightCharge - dt * 1.6f);
            UpdatePortal(env.LeftPortal, leftCharge);
            UpdatePortal(env.RightPortal, rightCharge);
        }

        void BallHit(Ball ball, Monster m, Vector2 d, PlayerStats s)
        {
            Vector2 dir = ball.Vel.sqrMagnitude > 0.01f ? ball.Vel.normalized : -d.normalized;
            switch (ball.St)
            {
                case Ball.State.Blast:
                    ball.Explode();   // the blast hits this monster and everything around it
                    break;
                case Ball.State.Pierce:
                    Combat.Hit(m, Player.PowerDamage, dir, 4.5f, Src.Power, big: true);   // keeps flying: no bounce
                    break;
                case Ball.State.Rainbow:
                    Combat.Hit(m, RainbowPassDamage, dir, 5f, Src.RainbowPass, big: true);
                    break;
                case Ball.State.Returning:
                    Combat.Hit(m, Player.ShotDamage * (s.Boomerang ? 1f : 0.6f), dir, 6.5f, Src.Returning);
                    break;
                case Ball.State.Header:
                {
                    Vector2 at = ball.Pos;
                    Combat.Hit(m, Player.HeaderDamage, dir, 11f, Src.Header, big: true);
                    if (m.Alive) m.Stun(Player.HeaderStun + s.HeaderStunBonus);
                    var fx = FxSystem.I;
                    fx.Ring(FxLayer.Front, at, 0.1f, 1.3f, 0.2f, 0.01f, 0.26f, Color.white, Palette.Header.WithAlpha(0f), 2.6f);
                    fx.Sparkles(m.Center + new Vector2(0f, m.Radius), 0.45f, 6, Palette.Header, 2.6f, 0.6f);
                    Game.I.Cam.AddTrauma(0.14f);
                    // Flugkopfball carries on through; otherwise it pops up off the victim's head
                    if (ball.HeaderPierceLeft > 0) ball.HeaderPierceLeft--;
                    else ball.HeaderPop();
                    break;
                }
                default:
                    Combat.Hit(m, Player.ShotDamage * ball.ShotMul, dir, 6.5f, Src.Shot, ball.GoldenShot);
                    ball.GoldenShot = false;
                    if (!ball.TryRicochet(m)) ball.BounceOff(d.normalized);
                    break;
            }
        }

        /// <summary>Soft push-apart so monsters never merge into one blob. Blobs only slide sideways.</summary>
        void Separate()
        {
            for (int i = 0; i < monsters.Count; i++)
            {
                var a = monsters[i];
                if (!a.Alive) continue;
                for (int j = i + 1; j < monsters.Count; j++)
                {
                    var b = monsters[j];
                    if (!b.Alive) continue;
                    Vector2 d = b.Center - a.Center;
                    float min = (a.Radius + b.Radius) * 0.92f;
                    float dist2 = d.sqrMagnitude;
                    if (dist2 >= min * min) continue;
                    float dist = Mathf.Sqrt(dist2);
                    Vector2 n = dist > 1e-4f ? d / dist : new Vector2(a.Id < b.Id ? 1f : -1f, 0f);
                    float push = (min - dist) * 0.5f;
                    // heavier monsters shove lighter ones
                    float wa = a.Radius * a.Radius, wb = b.Radius * b.Radius;
                    float ka = 2f * wb / (wa + wb), kb = 2f * wa / (wa + wb);
                    float sx = Mathf.Abs(n.x) > 1e-3f ? Mathf.Sign(n.x) : (a.Id < b.Id ? 1f : -1f);
                    Vector2 pa = a.K == Monster.Kind.Blob ? new Vector2(sx, 0f) : n;
                    Vector2 pb = b.K == Monster.Kind.Blob ? new Vector2(sx, 0f) : n;
                    a.Pos -= pa * push * ka;
                    b.Pos += pb * push * kb;
                }
            }
        }

        void UpdatePortal(Transform portal, float charge)
        {
            float c = Mathf.Clamp01(charge);
            var glow = portal.GetChild(0).GetComponent<SpriteRenderer>();
            var swirl = portal.GetChild(1).GetComponent<SpriteRenderer>();
            glow.color = portalColor.WithAlpha(0.06f + 0.4f * c);
            swirl.color = portalColor.WithAlpha(0.1f + 0.75f * c);
            swirl.transform.localRotation = Quaternion.Euler(0f, 0f, portalSpin * (1f + c));
            float s = 0.75f + 0.25f * c + 0.03f * Mathf.Sin(portalSpin * 0.05f);
            swirl.transform.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>Glow the portal that the next spawn will come out of.</summary>
        public void ChargeNextPortal(float k)
        {
            if (spawnSide % 2 == 0) leftCharge = Mathf.Max(leftCharge, k); else rightCharge = Mathf.Max(rightCharge, k);
        }

        /// <summary>Explosion: falloff damage and an outward, upward shove.</summary>
        public void Blast(Vector2 p, float radius, float damage, Src src = Src.Blast)
        {
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (!m.Alive) continue;
                Vector2 d = m.Center - p;
                float dist = d.magnitude;
                if (dist > radius + m.Radius) continue;
                float falloff = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(dist / radius));
                Vector2 dir = (dist > 0.01f ? d / dist : Vector2.up) + Vector2.up * 0.6f;
                Combat.Hit(m, damage * falloff, dir, 9f, src, big: true);
            }
        }

        public void RainbowImpact(Vector2 p, float damageMul = 1f)
        {
            var s = Game.I.Run.Stats;
            float radius = RainbowRadius * s.FlickRadiusMul * s.AreaMul;
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (!m.Alive) continue;
                Vector2 d = m.Center - p;
                float dist = d.magnitude;
                if (dist > radius + m.Radius) continue;
                float falloff = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(dist / radius));
                Vector2 dir = new Vector2(Mathf.Sign(d.x == 0f ? 1f : d.x) * 0.8f, 1f);
                Combat.Hit(m, RainbowDamage * falloff * damageMul, dir, 7f, Src.Rainbow, big: true);
            }
        }
    }
}
