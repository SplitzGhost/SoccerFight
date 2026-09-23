using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>Where a hit came from. Primary sources scale with the build; derived ones (chains, explosions, burn) carry already-scaled damage.</summary>
    public enum Src { Shot, Returning, Echo, TwinSun, Power, Rainbow, RainbowPass, Blast, Header, Three, Dunk, AlleyOop, FastBreak, Block, Dash, Tackle, Nutmeg, Punt, Decoy, Whistle, Nova, Stomp, Vortex, Chain, Explosion, Burn, Hazard }

    /// <summary>
    /// Every player hit on a monster goes through here: damage multipliers, crits, then the build's
    /// on-hit effects (burn, frost, chain sparks, cannon blasts, nova) and on-kill effects (life steal,
    /// frenzy, chain-reaction explosions, wildfire). A depth guard keeps chain reactions finite.
    /// </summary>
    public static class Combat
    {
        static int depth;
        static int novaHits, frenzyStacks;
        static float frenzyTime, adrenalineT, bulletTimeCd;
        static readonly HashSet<int> chained = new HashSet<int>();

        public static float AdrenalineMul => adrenalineT > 0f ? 1.2f : 1f;
        public static int FrenzyStacks => frenzyStacks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Reset();

        public static void Reset()
        {
            depth = 0; novaHits = 0; frenzyStacks = 0;
            frenzyTime = adrenalineT = bulletTimeCd = 0f;
        }

        static PlayerStats S => Game.I.Run.Stats;

        static bool Primary(Src s) => s <= Src.Vortex;
        static bool Direct(Src s) => s <= Src.Dash;

        static float SourceMul(Src src, PlayerStats s)
        {
            switch (src)
            {
                case Src.Shot: case Src.Returning: return s.ShotDamageMul;
                case Src.Echo: case Src.TwinSun: return s.ShotDamageMul * s.EchoDamageFrac;
                case Src.Power: return s.PowerDamageMul;
                case Src.Rainbow: case Src.RainbowPass: return s.FlickDamageMul;
                case Src.Blast: return s.BlastDamageMul;
                case Src.Three: return s.ThreeDamageMul;
                case Src.AlleyOop: return s.OopDamageMul;
                default: return 1f;
            }
        }

        public static void Update(float dt)
        {
            if (frenzyTime > 0f) { frenzyTime -= dt; if (frenzyTime <= 0f) frenzyStacks = 0; }
            adrenalineT = Mathf.Max(0f, adrenalineT - dt);
            bulletTimeCd = Mathf.Max(0f, bulletTimeCd - Time.unscaledDeltaTime);
        }

        /// <summary>Damage a monster. Returns true if it died.</summary>
        public static bool Hit(Monster m, float baseDmg, Vector2 dir, float knock, Src src, bool forceCrit = false, bool big = false)
        {
            if (m == null || !m.Alive) return false;
            var s = S;
            float d = baseDmg;
            bool boosted = false;
            if (Primary(src))
            {
                d *= s.DamageMul * SourceMul(src, s) * (1f + 0.06f * frenzyStacks);
                // class traits: the striker's shots, the skiller's tricks, the defender's header
                var cat = SkillCatalog.CategoryOf(src);
                if (cat != null)
                {
                    float cm = s.CategoryMul(cat.Value);
                    d *= cm;
                    boosted = cm > 1.01f && cat.Value == SkillCategory.Shot && s.ShotImpactFx;
                }
            }
            // Heisse Hand: while the ball burns, throws hit harder and set fire
            bool hot = (src == Src.Shot || src == Src.Returning || src == Src.Echo) && Game.I.Player.HotHandLeft > 0f;
            if (hot) d *= 1.4f;
            d *= m.DamageTakenMul;
            if (m.Slowed && s.FrostVuln > 0f && src != Src.Burn) d *= 1f + s.FrostVuln;

            bool canCrit = Primary(src) || (src == Src.Chain && s.ChainCanCrit);
            bool crit = forceCrit || (canCrit && Random.value < s.CritChance);
            if (crit)
            {
                d *= s.CritMul + s.SharpshooterBonus;
                knock *= s.SharpshooterBonus > 0f ? 2.2f : 1.25f;
            }
            knock *= s.KnockbackMul;

            if (DevMode.OneHit) d = Mathf.Max(d, m.Hp + 1f);

            bool direct = Direct(src);
            bool shock = direct && s.ThermalShock && m.Burning && m.Slowed;
            Vector2 at = m.Center;
            bool killed = m.Hit(d, dir, knock, big || crit, crit, boosted);
            if (hot && !killed) m.Ignite(d * 0.3f, 3f);
            if (boosted) PowerStar(at, dir, m.Radius, crit || big);
            // heavier kicks (knockback upgrades) land with a visible punch ring
            if (direct && s.KnockbackMul > 1f)
                FxSystem.I.Ring(FxLayer.Front, at, 0.1f, 0.45f + 0.55f * (s.KnockbackMul - 1f), 0.1f, 0.01f, 0.18f,
                    Color.white.WithAlpha(0.8f), Palette.ShotCyan.WithAlpha(0f), 2f);

            if (crit) OnCrit(at);
            if (direct)
            {
                if (!killed)
                {
                    if (s.BurnFrac > 0f) m.Ignite(d * s.BurnFrac * (1f + s.BurnBoost), s.BurnTime);
                    if (s.SlowAmount > 0f) m.Chill(s.SlowAmount, s.SlowTime);
                    if (s.FreezeOnThird && m.Slowed && m.HitCount % 3 == 0) m.Freeze(1f);
                }
                if (s.ChainTargets > 0 || forceCrit && s.GoldenBoot) Chain(m, d, forceCrit && s.GoldenBoot);
                if ((s.Cannoneer || (forceCrit && s.GoldenBoot)) && (src == Src.Shot || src == Src.Echo || src == Src.TwinSun))
                    Explosion(at, 1.3f * s.AreaMul, d * 0.5f, Palette.ShotCyan);
                if (s.NovaEvery > 0 && ++novaHits >= s.NovaEvery) { novaHits = 0; Nova(Game.I.Player); }
                if (shock)
                {
                    m.BurnTime = 0f; m.SlowTime = 0f;
                    Explosion(at, 1.8f * s.AreaMul, d * 1.5f, new Color(0.65f, 0.9f, 1f));
                }
            }
            return killed;
        }

        /// <summary>
        /// The striker's shot lands with extra weight: a hot four-point star at the impact, a tight
        /// ring and a spray of sparks carried on in the direction of the shot.
        /// </summary>
        static void PowerStar(Vector2 at, Vector2 dir, float radius, bool heavy)
        {
            var fx = FxSystem.I;
            // saturated reds and ambers, never white: the monster's own hit flash is already white
            Color hot = Classes.Striker.Accent;
            Color core = Palette.Gold;
            float k = heavy ? 1.3f : 1f;
            Vector2 n = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.right;
            Vector2 p = at - n * Mathf.Min(radius * 0.6f, 0.5f);
            float ang = MathUtil.Angle(n);
            // an eight-point star: a big four-point one along the shot, a smaller one turned 45°
            fx.Spawn(FxLayer.Front, true, Art.CellSparkle, p, Vector2.zero, 0.24f, 2.1f * k, 0.5f * k, core, hot.WithAlpha(0f), 2.4f,
                0f, 0f, ang, 0f, false);
            fx.Spawn(FxLayer.Front, true, Art.CellSparkle, p, Vector2.zero, 0.2f, 1.3f * k, 0.3f * k, hot, hot.WithAlpha(0f), 2.2f,
                0f, 0f, ang + 45f, 0f, false);
            fx.Flash(p, 1.5f * k, hot, 0.12f, 2.2f);
            fx.Ring(FxLayer.Front, p, 0.15f, 1.15f * k, 0.18f, 0.01f, 0.24f, core, hot.WithAlpha(0f), 2.2f);
            fx.Sparks(p, n, 40f, heavy ? 9 : 6, 8f, 18f, hot, 2.4f, 0.06f, 0.22f);
        }

        static void OnCrit(Vector2 at)
        {
            var s = S;
            if (s.BulletTime && bulletTimeCd <= 0f) { bulletTimeCd = 1.4f; TimeFx.SlowMo(0.35f, 0.1f, 0.25f); }
            if (s.Perpetual) Game.I.Player.ReduceCooldowns(0.2f);
            // the crit-damage upgrades make the sparkle bigger
            float boost = Mathf.Max(0f, s.CritMul + s.SharpshooterBonus - 1.75f);
            FxSystem.I.Sparkles(at, 0.35f + boost * 0.2f, 4 + Mathf.RoundToInt(boost * 6f), Palette.Gold, 2.6f, 0.4f + boost * 0.1f);
        }

        /// <summary>Called for every monster death (any cause).</summary>
        public static void OnKill(Monster m)
        {
            var s = S;
            var run = Game.I.Run;
            run.Kills++;
            if (s.LifeOnKill > 0f)
            {
                var player = Game.I.Player;
                player.Heal(s.LifeOnKill);
                // a small green orb flies from the kill to the player
                Vector2 to = player.Pos + new Vector2(0f, 1f) - m.Center;
                FxSystem.I.Spawn(FxLayer.Front, true, Art.CellGlow, m.Center, to / 0.4f, 0.4f, 0.3f, 0.12f, Palette.Heal, Palette.Heal.WithAlpha(0.2f), 2.8f);
            }
            if (s.BloodFrenzy) { frenzyStacks = Mathf.Min(10, frenzyStacks + 1); frenzyTime = 4f; }
            if (s.AdrenalineTime > 0f) adrenalineT = s.AdrenalineTime;
            if (s.Perpetual) Game.I.Player.ReduceCooldowns(0.5f);
            // the whistle fills with every kill — elites and bosses are worth a lot more
            Game.I.Player.AddUltimate(m.Rank == Rank.Boss ? 0.5f : m.Rank == Rank.MiniBoss ? 0.25f : m.Rank == Rank.Elite ? 0.12f : 0.05f);
            if (s.BurnSpread && m.Burning)
            {
                foreach (var o in Game.I.Waves.Monsters)
                    if (o.Alive && o != m && (o.Center - m.Center).sqrMagnitude < 2.4f * 2.4f) o.Ignite(Mathf.Max(m.BurnDps, 4f), s.BurnTime);
                FxSystem.I.Ring(FxLayer.Front, m.Center, 0.2f, 2.4f, 0.2f, 0.01f, 0.3f, new Color(1f, 0.8f, 0.4f), Palette.BlastOrange.WithAlpha(0f), 2.2f);
            }
            if (s.KillExplodeFrac > 0f && depth < 4)
                Explosion(m.Center, s.KillExplodeRadius * s.AreaMul * Mathf.Sqrt(Mathf.Max(1f, m.Radius / 0.42f)), m.MaxHp * s.KillExplodeFrac, Palette.MonsterGlow);
        }

        /// <summary>Area burst: falloff damage, outward shove, no further on-hit effects (kills can still chain).</summary>
        public static void Explosion(Vector2 at, float radius, float dmg, Color c, Src src = Src.Explosion)
        {
            CoopFx.Send(CoopFx.Kind.Explosion, at, radius, c);
            var fx = FxSystem.I;
            fx.Flash(at, radius * 1.4f, c, 0.16f, 2.6f);
            fx.Ring(FxLayer.Front, at, 0.1f, radius, 0.25f, 0.01f, 0.28f, Color.white, c.WithAlpha(0f), 2.4f);
            fx.Sparks(at, Vector2.up, 200f, 10, 4f, 10f, c, 2.4f, 0.045f, 0.28f, 6f);
            Game.I.Cam.AddTrauma(0.12f);
            depth++;
            var list = Game.I.Waves.Monsters;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m.Alive) continue;
                Vector2 d = m.Center - at;
                float dist = d.magnitude;
                if (dist > radius + m.Radius) continue;
                float fall = Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(dist / radius));
                Hit(m, dmg * fall, (dist > 0.01f ? d / dist : Vector2.up) + Vector2.up * 0.4f, 5f, src);
            }
            depth--;
        }

        static Monster Nearest(Vector2 from, float range, HashSet<int> skip)
        {
            Monster best = null;
            float bd = range * range;
            foreach (var m in Game.I.Waves.Monsters)
            {
                if (!m.Alive || skip.Contains(m.Id)) continue;
                float d = (m.Center - from).sqrMagnitude;
                if (d < bd) { bd = d; best = m; }
            }
            return best;
        }

        public static Monster NearestTo(Vector2 from, float range, Monster except = null)
        {
            chained.Clear();
            if (except != null) chained.Add(except.Id);
            return Nearest(from, range, chained);
        }

        /// <summary>Sparks jump from a struck monster to its neighbours, each arc hopping onwards.</summary>
        static void Chain(Monster origin, float dmg, bool golden)
        {
            if (depth > 2) return;   // sparks from sparks from sparks: stop the cascade
            var s = S;
            int arcs = Mathf.Max(s.ChainTargets, golden ? 2 : 0);
            int jumps = Mathf.Max(1, s.ChainJumps);
            float frac = Mathf.Max(s.ChainFrac, golden ? 0.5f : 0f);
            Color c = golden ? Palette.Gold : new Color(0.6f, 0.9f, 1f);
            chained.Clear();
            chained.Add(origin.Id);
            depth++;
            for (int a = 0; a < arcs; a++)
            {
                Vector2 from = origin.Center;
                float cd = dmg * frac;
                for (int j = 0; j < jumps; j++)
                {
                    var n = Nearest(from, 5.5f, chained);
                    if (n == null) break;
                    chained.Add(n.Id);
                    Lightning.I.Bolt(from, n.Center, c, 0.05f, 0.18f, 0.25f);
                    Vector2 at = n.Center;
                    Hit(n, cd, (at - from).normalized, 2f, Src.Chain);
                    if (s.ChainIgnites && n.Alive) n.Ignite(Mathf.Max(cd * 0.4f, 3f), s.BurnTime);
                    from = at;
                    cd *= 0.85f;
                }
            }
            depth--;
        }

        public static void Nova(Player p)
        {
            var s = S;
            Vector2 at = p.Pos + new Vector2(0f, 0.9f);
            float r = 3f * s.AreaMul;
            CoopFx.Send(CoopFx.Kind.Nova, at, r, Palette.ShotCyan);
            var fx = FxSystem.I;
            fx.Flash(at, r * 1.2f, Palette.ShotCyan, 0.2f, 2.8f);
            fx.Ring(FxLayer.Front, at, 0.3f, r, 0.35f, 0.02f, 0.35f, Color.white, Palette.ShotCyan.WithAlpha(0f), 2.6f);
            fx.Ring(FxLayer.Front, at, 0.2f, r * 0.7f, 0.2f, 0.01f, 0.25f, Palette.ShotCore, Palette.ShotCyan.WithAlpha(0f), 2.4f, true);
            Game.I.Cam.AddTrauma(0.2f);
            var list = Game.I.Waves.Monsters;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m.Alive) continue;
                Vector2 d = m.Center - at;
                if (d.magnitude > r + m.Radius) continue;
                Hit(m, 60f, d.normalized + Vector2.up * 0.3f, 7f, Src.Nova, big: true);
            }
        }

        /// <summary>Counter stomp: a ring of force around the player after taking a hit.</summary>
        public static void Stomp(Player p, float dmg)
        {
            Vector2 at = p.Pos + new Vector2(0f, 0.6f);
            float r = 2.6f * S.AreaMul;
            var fx = FxSystem.I;
            fx.Ring(FxLayer.Front, at, 0.3f, r, 0.4f, 0.02f, 0.35f, Color.white, Palette.Gold.WithAlpha(0f), 2.4f);
            fx.Dust(p.Pos, Vector2.right, 6, 3f, 0.5f, 0.35f);
            fx.Dust(p.Pos, Vector2.left, 6, 3f, 0.5f, 0.35f);
            var list = Game.I.Waves.Monsters;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m.Alive) continue;
                Vector2 d = m.Center - at;
                if (d.magnitude > r + m.Radius) continue;
                Hit(m, dmg, d.normalized + Vector2.up * 0.5f, 11f, Src.Stomp, big: true);
            }
        }

        /// <summary>Maestro: every ability grants a second of invulnerability and an echo volley at nearby foes.</summary>
        public static void Maestro(Player p)
        {
            if (!S.Maestro) return;
            p.DodgeTime = Mathf.Max(p.DodgeTime, 1f);
            Vector2 from = p.Pos + new Vector2(0f, 1f);
            chained.Clear();
            for (int i = 0; i < 3; i++)
            {
                var m = Nearest(from, 10f, chained);
                Vector2 dir = m != null ? (m.Center - from).normalized : MathUtil.Dir(60f + i * 30f);
                if (m != null) chained.Add(m.Id);
                EchoBalls.I.Fire(from, dir, 22f, Player.ShotDamage, Src.Echo, Palette.Gold);
            }
        }

        /// <summary>
        /// The power shot reached the point the player aimed at: with Singularity a black hole opens
        /// exactly there (kept inside the arena and above the ground so it can swallow something).
        /// </summary>
        public static void OnPierceTarget(Vector2 at)
        {
            var s = S;
            if (!s.Singularity) return;
            at.x = Mathf.Clamp(at.x, -Player.ArenaHalf, Player.ArenaHalf);
            at.y = Mathf.Clamp(at.y, Level.FloorBelow(at.x, at.y + 0.5f) + 0.7f, 9f);
            // base numbers: Combat applies the global damage multiplier to vortex hits
            float power = Player.PowerDamage * s.PowerDamageMul;
            Vortices.I.Spawn(at, 3.4f * s.AreaMul, 2.5f, power * 2f, power * 6f, true, new Color(0.7f, 0.45f, 1f));
        }
    }
}
