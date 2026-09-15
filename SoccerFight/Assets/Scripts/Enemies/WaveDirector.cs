using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>Spawns monster waves out of the goal portals and resolves all combat collisions.</summary>
    public sealed class WaveDirector
    {
        enum Phase { Intermission, Spawning }

        readonly List<Monster> monsters = new List<Monster>();
        Transform parent;
        WorldEnvironment env;
        Phase phase = Phase.Intermission;
        float timer;
        float spawnTimer;
        int toSpawn;
        int spawnSide;
        float leftCharge, rightCharge;
        float portalSpin;
        public bool Enabled = true;

        public int Wave { get; private set; }
        public int AliveCount { get; private set; }
        public int RemainingInWave => AliveCount + toSpawn;

        public const float RainbowRadius = 2.9f;
        public const float RainbowDamage = 55f;
        public const float RainbowPassDamage = 22f;

        public void Build(Transform root, WorldEnvironment environment)
        {
            parent = new GameObject("Monsters").transform;
            parent.SetParent(root, false);
            env = environment;
        }

        public void Restart(float delay = 1.6f)
        {
            foreach (var m in monsters) m.Deactivate();
            Wave = 0;
            toSpawn = 0;
            phase = Phase.Intermission;
            timer = delay;
        }

        Monster Get(Monster.Kind kind)
        {
            foreach (var m in monsters) if (!m.Alive && m.K == kind) return m;
            var nm = new Monster();
            nm.Build(parent, kind);
            monsters.Add(nm);
            return nm;
        }

        /// <summary>Debug/capture helper: drop a monster somewhere specific.</summary>
        public Monster SpawnAt(Monster.Kind kind, Vector2 pos)
        {
            var m = Get(kind);
            m.Spawn(pos, Vector2.zero, 1f);
            return m;
        }

        void SpawnOne()
        {
            bool left = spawnSide++ % 2 == 0;
            Transform portal = left ? env.LeftPortal : env.RightPortal;
            Vector2 at = portal.position;
            float inward = left ? 1f : -1f;
            bool wisp = Wave >= 2 && Random.value < Mathf.Min(0.45f, 0.18f + Wave * 0.04f);
            var m = Get(wisp ? Monster.Kind.Wisp : Monster.Kind.Blob);
            Vector2 v = wisp ? new Vector2(inward * 3f, 1.5f) : new Vector2(inward * Random.Range(3.5f, 5f), Random.Range(2.5f, 4f));
            m.Spawn(at, v, 1f + (Wave - 1) * 0.08f);
            if (left) leftCharge = 1.4f; else rightCharge = 1.4f;
            var fx = FxSystem.I;
            fx.Flash(at, 2.6f, Palette.MonsterGlow, 0.25f, 2.6f);
            fx.Ring(FxLayer.Front, at, 0.3f, 1.4f, 0.25f, 0.02f, 0.4f, Palette.MonsterGlow, Palette.MonsterGlow.WithAlpha(0f), 2.2f);
            fx.Sparks(at, new Vector2(inward, 0.3f), 90f, 8, 3f, 8f, Palette.MonsterGlow, 2.4f, 0.05f, 0.3f);
        }

        public void Update(float dt, Player player, Ball ball)
        {
            if (Enabled && !player.Dead)
            {
                if (phase == Phase.Intermission)
                {
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        Wave++;
                        toSpawn = 3 + Wave * 2;
                        spawnTimer = 0.9f;
                        phase = Phase.Spawning;
                        Game.I.Hud.ShowWaveBanner(Wave);
                    }
                }
                else
                {
                    if (toSpawn > 0)
                    {
                        spawnTimer -= dt;
                        // telegraph: the next portal starts glowing before it spits a monster out
                        bool nextLeft = spawnSide % 2 == 0;
                        if (spawnTimer < 0.6f) { if (nextLeft) leftCharge = Mathf.Max(leftCharge, 1f - spawnTimer / 0.6f); else rightCharge = Mathf.Max(rightCharge, 1f - spawnTimer / 0.6f); }
                        if (spawnTimer <= 0f)
                        {
                            SpawnOne();
                            toSpawn--;
                            spawnTimer = Mathf.Max(0.4f, 1.0f - Wave * 0.06f);
                        }
                    }
                    else if (AliveCount == 0)
                    {
                        phase = Phase.Intermission;
                        timer = 2.8f;
                        Game.I.Hud.OnWaveCleared(Wave);
                    }
                }
            }

            // monsters
            int alive = 0;
            Vector2 playerCenter = player.Pos + new Vector2(0f, 0.8f);
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (!m.Alive) continue;
                m.Update(dt, player);
                if (!m.Alive) continue;
                alive++;

                // ball vs monster
                if (ball.IsDangerous)
                {
                    Vector2 d = ball.Pos - m.Center;
                    float r = m.Radius + Art.BallRadius + 0.05f;
                    if (d.sqrMagnitude < r * r && ball.TryRegisterHit(m.Id))
                    {
                        Vector2 dir = ball.Vel.sqrMagnitude > 0.01f ? ball.Vel.normalized : -d.normalized;
                        if (ball.IsRainbow)
                        {
                            m.Hit(RainbowPassDamage, dir, 5f, true);
                        }
                        else
                        {
                            bool returning = ball.St == Ball.State.Returning;
                            m.Hit(returning ? Player.ShotDamage * 0.6f : Player.ShotDamage, dir, 6.5f, false);
                            if (!returning) ball.BounceOff(d.normalized);
                        }
                    }
                }

                // monster vs player
                if (m.Alive && !player.Dead)
                {
                    float pr = m.Radius + 0.32f;
                    Vector2 pd = m.Center - playerCenter;
                    if (Mathf.Abs(pd.x) < pr && Mathf.Abs(pd.y) < pr + 0.5f) player.TakeDamage(m.ContactDamage, m.Center);
                }
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
                    float sx = Mathf.Abs(n.x) > 1e-3f ? Mathf.Sign(n.x) : (a.Id < b.Id ? 1f : -1f);
                    Vector2 pa = a.K == Monster.Kind.Blob ? new Vector2(sx, 0f) : n;
                    Vector2 pb = b.K == Monster.Kind.Blob ? new Vector2(sx, 0f) : n;
                    a.Pos -= pa * push;
                    b.Pos += pb * push;
                }
            }
        }

        void UpdatePortal(Transform portal, float charge)
        {
            float c = Mathf.Clamp01(charge);
            var glow = portal.GetChild(0).GetComponent<SpriteRenderer>();
            var swirl = portal.GetChild(1).GetComponent<SpriteRenderer>();
            glow.color = Palette.MonsterGlow.WithAlpha(0.06f + 0.4f * c);
            swirl.color = Palette.MonsterGlow.WithAlpha(0.1f + 0.75f * c);
            swirl.transform.localRotation = Quaternion.Euler(0f, 0f, portalSpin * (1f + c));
            float s = 0.75f + 0.25f * c + 0.03f * Mathf.Sin(portalSpin * 0.05f);
            swirl.transform.localScale = new Vector3(s, s, 1f);
        }

        public void RainbowImpact(Vector2 p)
        {
            foreach (var m in monsters)
            {
                if (!m.Alive) continue;
                Vector2 d = m.Center - p;
                float dist = d.magnitude;
                if (dist > RainbowRadius + m.Radius) continue;
                float falloff = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(dist / RainbowRadius));
                Vector2 dir = new Vector2(Mathf.Sign(d.x == 0f ? 1f : d.x) * 0.8f, 1f);
                m.Hit(Mathf.Round(RainbowDamage * falloff), dir, 7f, true);
            }
        }
    }
}
