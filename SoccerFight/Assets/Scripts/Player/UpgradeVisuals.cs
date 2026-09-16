using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Makes the build readable at a glance. Everything here is derived from the current PlayerStats
    /// every frame, so picking a card changes the look immediately and nothing has to be undone:
    ///  · damage upgrades make the ball a little bigger (up to +30 %)
    ///  · fire / frost / chain spark / explosive shots give the ball an elemental glow, particles and
    ///    a matching trail; faster balls leave a longer trail
    ///  · echo balls circle the ball at the feet, the golden boot shines before its golden kick
    ///  · the captain's shield is a visible bubble, speed upgrades light up the boots and leave
    ///    streaks, jump upgrades puff at take-off, regeneration and frenzy show on the body
    /// </summary>
    public sealed class UpgradeVisuals
    {
        public static readonly Color Fire = new Color(1f, 0.52f, 0.18f);
        public static readonly Color Frost = new Color(0.62f, 0.9f, 1f);
        public static readonly Color Storm = new Color(0.72f, 0.62f, 1f);
        public static readonly Color Boom = new Color(1f, 0.38f, 0.26f);

        Player player;
        Ball ball;
        Transform root;
        SpriteRenderer ballAura, ballAura2;
        SpriteRenderer[] echoes;
        SpriteRenderer shieldGlow, shieldRing, shieldRing2, frenzyGlow;
        Gradient trailBase, trailFire, trailFrost, trailFireFrost, trailStorm, trailBoom;

        float t, size = 1f, sizeVel, shield, frenzy, emberAcc, frostAcc, arcTimer, glintTimer, regenTimer, streakAcc, echoSpin;
        bool wasGrounded = true;

        public void Build(Transform parent, Player p, Ball b)
        {
            player = p;
            ball = b;
            root = new GameObject("Upgrade Visuals").transform;
            root.SetParent(parent, false);

            ballAura = Art.MakeSprite("BallAura", root, Art.SoftGlow, PlayerRig.BallOrderFront - 3, Art.SpriteGlowMat, Color.clear);
            ballAura2 = Art.MakeSprite("BallAura2", root, Art.Ring, PlayerRig.BallOrderFront + 4, Art.SpriteGlowMat, Color.clear);
            echoes = new SpriteRenderer[4];
            for (int i = 0; i < echoes.Length; i++)
                echoes[i] = Art.MakeSprite("Echo" + i, root, Art.BallPattern, PlayerRig.BallOrderFront - 4, Art.SpriteSolidMat, Color.clear);

            shieldGlow = Art.MakeSprite("ShieldGlow", root, Art.SoftGlow, PlayerRig.BaseOrder + 40, Art.SpriteAddMat, Color.clear);
            shieldRing = Art.MakeSprite("ShieldRing", root, Art.Ring, PlayerRig.BaseOrder + 41, Art.SpriteGlowMat, Color.clear);
            shieldRing2 = Art.MakeSprite("ShieldRing2", root, Art.Ring, PlayerRig.BaseOrder + 41, Art.SpriteGlowMat, Color.clear);
            frenzyGlow = Art.MakeSprite("FrenzyGlow", root, Art.SoftGlow, PlayerRig.BaseOrder - 2, Art.SpriteGlowMat, Color.clear);

            var baseKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Palette.ShotCyan, 0.35f), new GradientColorKey(new Color(0.3f, 0.5f, 1f), 1f) };
            trailBase = Grad(baseKeys);
            trailFire = Ball.TrailGradient(new Color(1f, 0.95f, 0.75f), Fire, new Color(0.85f, 0.18f, 0.12f));
            trailFrost = Ball.TrailGradient(Color.white, Frost, new Color(0.45f, 0.62f, 1f));
            trailStorm = Ball.TrailGradient(Color.white, Storm, new Color(0.4f, 0.35f, 1f));
            trailBoom = Ball.TrailGradient(new Color(1f, 0.92f, 0.8f), Boom, new Color(0.7f, 0.12f, 0.2f));
            trailFireFrost = Grad(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Fire, 0.3f), new GradientColorKey(Frost, 0.65f), new GradientColorKey(new Color(0.4f, 0.6f, 1f), 1f) });
        }

        static Gradient Grad(GradientColorKey[] keys)
        {
            var g = new Gradient();
            g.SetKeys(keys, new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        /// <summary>After the ball and player moved (dt = game time, paused = 0).</summary>
        public void Update(float dt)
        {
            if (player == null) return;
            t += dt;
            var s = Game.I.Run.Stats;
            var fx = FxSystem.I;
            UpdateBall(dt, s, fx);
            UpdateBody(dt, s, fx);
        }

        // ------------------------------------------------------------------ ball

        void UpdateBall(float dt, PlayerStats s, FxSystem fx)
        {
            // bigger with damage: a small pop when a card lands, then it settles
            float target = 1f + Mathf.Clamp((s.DamageMul - 1f) * 0.3f, 0f, 0.3f);
            MathUtil.Spring(ref size, ref sizeVel, target, 7f, 0.45f, Mathf.Max(dt, 1e-4f));
            ball.SizeMul = Mathf.Max(0.8f, size);

            bool fire = s.BurnFrac > 0f, frost = s.SlowAmount > 0f, storm = s.ChainTargets > 0, boom = s.Cannoneer || s.KillExplodeFrac > 0f;
            bool golden = player.NextShotGolden && ball.IsHeld;
            ball.SetShotTrail(fire && frost ? trailFireFrost : fire ? trailFire : frost ? trailFrost : storm ? trailStorm : boom ? trailBoom : trailBase,
                0.17f * Mathf.Pow(Mathf.Max(1f, s.BallSpeedMul), 0.9f));

            bool flying = !ball.IsHeld;
            bool visible = !player.Dead && !(ball.St == Ball.State.Meteor);
            Vector2 p = ball.Pos;
            float r = ball.Radius;

            // elemental glow: several elements take turns
            int n = (fire ? 1 : 0) + (frost ? 1 : 0) + (storm ? 1 : 0) + (boom ? 1 : 0);
            Color aura = Color.clear;
            if (n > 0)
            {
                float cycle = Mathf.Repeat(t * 0.5f, n);
                int idx = Mathf.FloorToInt(cycle);
                float blend = MathUtil.Smooth01((cycle - idx - 0.7f) / 0.3f);
                aura = Color.Lerp(Element(idx, fire, frost, storm, boom), Element((idx + 1) % n, fire, frost, storm, boom), blend);
            }
            if (golden) aura = Color.Lerp(aura.a > 0f ? aura : Palette.Gold, Palette.Gold, 0.75f + 0.25f * Mathf.Sin(t * 6f));
            float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(t * 7f, 0.3f);
            float auraA = (n > 0 || golden) && visible ? (flying ? 0.5f : 0.3f) * flicker : 0f;
            ballAura.transform.position = p;
            ballAura.transform.localScale = Vector3.one * r * (flying ? 7.5f : 6f);
            ballAura.color = Color.Lerp(ballAura.color, aura.WithAlpha(auraA), 1f - Mathf.Exp(-10f * dt));
            // a thin halo ring while the ball rests at the feet
            ballAura2.transform.position = p;
            ballAura2.transform.localScale = Vector3.one * r * (2.9f + 0.15f * Mathf.Sin(t * 3f));
            ballAura2.transform.localRotation = Quaternion.Euler(0f, 0f, t * 60f);
            ballAura2.color = Color.Lerp(ballAura2.color, aura.WithAlpha(visible && !flying && (n > 0 || golden) ? 0.28f : 0f), 1f - Mathf.Exp(-10f * dt));

            if (!visible || dt <= 0f) { HideEchoes(); return; }
            float rate = flying ? 2.6f : 1f;

            if (fire)
            {
                emberAcc += dt * 11f * rate;
                while (emberAcc >= 1f)
                {
                    emberAcc -= 1f;
                    Color c = Color.Lerp(Palette.Gold, Fire, Random.value);
                    fx.Spawn(FxLayer.Front, true, Art.CellDot, p + Random.insideUnitCircle * r * 0.8f,
                        new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.7f, 1.6f)) - ball.Vel * 0.04f,
                        Random.Range(0.35f, 0.7f), Random.Range(0.05f, 0.09f), 0f, c, Fire.WithAlpha(0f), 2.8f, 1.2f, -1.4f);
                }
            }
            if (frost)
            {
                frostAcc += dt * 7f * rate;
                while (frostAcc >= 1f)
                {
                    frostAcc -= 1f;
                    fx.Spawn(FxLayer.Front, true, Art.CellSparkle, p + Random.insideUnitCircle * r * 1.1f,
                        new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(-0.2f, 0.3f)) - ball.Vel * 0.03f,
                        Random.Range(0.45f, 0.8f), Random.Range(0.08f, 0.14f), 0f, Color.white, Frost.WithAlpha(0f), 2.2f, 2f, 0.9f,
                        Random.Range(0f, 90f), Random.Range(-60f, 60f));
                    if (Random.value < 0.25f)
                        fx.Spawn(FxLayer.Back, false, Art.CellPuff, p, Random.insideUnitCircle * 0.3f, 0.6f, r * 1.2f, r * 2.6f,
                            Frost.WithAlpha(0.14f), Frost.WithAlpha(0f), 1f, 1.5f, 0.2f, Random.Range(0f, 360f), 20f);
                }
            }
            if (storm && Lightning.I != null && (arcTimer -= dt * rate) <= 0f)
            {
                arcTimer = Random.Range(0.25f, 0.6f);
                Vector2 a = p + MathUtil.Dir(Random.Range(0f, 360f)) * r * 0.9f;
                Vector2 b = p + MathUtil.Dir(Random.Range(0f, 360f)) * r * 1.9f;
                Lightning.I.Bolt(a, b, Storm, 0.022f, 0.09f, 0.06f);
            }
            if (boom && Random.value < dt * 7f * rate)
                fx.Sparks(p + Vector2.up * r * 0.8f, Vector2.up, 70f, 1, 1.2f, 3f, Boom, 2.4f, 0.03f, 0.18f, 2f);
            if ((s.CritChance >= 0.15f || golden) && (glintTimer -= dt) <= 0f)
            {
                glintTimer = golden ? 0.25f : Mathf.Lerp(1.8f, 0.7f, Mathf.Clamp01((s.CritChance - 0.15f) / 0.35f));
                fx.Spawn(FxLayer.Front, true, Art.CellSparkle, p + new Vector2(-0.45f, 0.5f) * r, Vector2.zero, 0.35f, r * 1.4f, 0f,
                    Color.white, Palette.Gold.WithAlpha(0f), 3f, 0f, 0f, 0f, 180f);
            }

            UpdateEchoes(dt, s, p, r);
        }

        static Color Element(int i, bool fire, bool frost, bool storm, bool boom)
        {
            if (fire) { if (i == 0) return Fire; i--; }
            if (frost) { if (i == 0) return Frost; i--; }
            if (storm) { if (i == 0) return Storm; i--; }
            return Boom;
        }

        /// <summary>Echo balls: faint copies that circle the ball while it rests at the feet.</summary>
        void UpdateEchoes(float dt, PlayerStats s, Vector2 p, float r)
        {
            int count = Mathf.Min(s.EchoBalls, echoes.Length);
            bool show = ball.IsHeld;
            echoSpin += dt * 150f;
            for (int i = 0; i < echoes.Length; i++)
            {
                var e = echoes[i];
                float want = show && i < count ? 0.45f : 0f;
                float a = Mathf.MoveTowards(e.color.a, want, dt * 3f);
                if (a <= 0f) { if (e.color.a > 0f) e.color = Color.clear; continue; }
                float ang = echoSpin + i * 360f / Mathf.Max(1, count);
                Vector2 o = MathUtil.Dir(ang) * r * 2.1f;
                o.y *= 0.45f;
                e.transform.position = p + o + Vector2.up * r * 0.25f;
                float sc = r / Art.BallRadius * 0.46f * (Mathf.Sin(ang * Mathf.Deg2Rad) > 0f ? 0.85f : 1f);
                e.transform.localScale = new Vector3(sc, sc, 1f);
                e.transform.localRotation = Quaternion.Euler(0f, 0f, -echoSpin * 2f);
                e.sortingOrder = Mathf.Sin(ang * Mathf.Deg2Rad) > 0f ? PlayerRig.BallOrderFront - 4 : PlayerRig.BallOrderFront + 5;
                e.color = Palette.ShotCyan.WithAlpha(a);
            }
        }

        void HideEchoes()
        {
            foreach (var e in echoes) if (e.color.a > 0f) e.color = Color.clear;
        }

        // ------------------------------------------------------------------ player

        void UpdateBody(float dt, PlayerStats s, FxSystem fx)
        {
            Vector2 body = player.Pos + new Vector2(0f, 0.95f);
            bool alive = !player.Dead;

            // captain's shield: a soft bubble while a charge is ready
            shield = Mathf.MoveTowards(shield, alive && player.Shield > 0 ? 1f : 0f, dt * (player.Shield > 0 ? 3f : 8f));
            float shimmer = 0.8f + 0.2f * Mathf.Sin(t * 5f);
            shieldGlow.transform.position = body;
            shieldGlow.transform.localScale = Vector3.one * 2.9f;
            shieldGlow.color = Palette.ShotCyan.WithAlpha(0.05f * shield * shimmer);
            shieldRing.transform.position = body;
            shieldRing.transform.localScale = Vector3.one * (2.35f + 0.05f * Mathf.Sin(t * 2.2f)) * Mathf.Lerp(0.8f, 1f, shield);
            shieldRing.color = Palette.ShotCyan.WithAlpha(0.12f * shield * shimmer);
            shieldRing2.transform.position = body;
            shieldRing2.transform.localScale = new Vector3(2.2f, 2.35f, 1f) * Mathf.Lerp(0.8f, 1f, shield);
            shieldRing2.transform.localRotation = Quaternion.Euler(0f, 0f, t * 40f);
            shieldRing2.color = Color.white.WithAlpha(0.04f * shield);

            // blood frenzy: the body glows redder with every stack
            float want = alive ? Mathf.Clamp01(Combat.FrenzyStacks / 10f) : 0f;
            frenzy = Mathf.MoveTowards(frenzy, want, dt * 2f);
            frenzyGlow.transform.position = body;
            frenzyGlow.transform.localScale = new Vector3(2.2f, 3f, 1f) * (1f + 0.05f * Mathf.Sin(t * 8f));
            frenzyGlow.color = Palette.Hurt.WithAlpha(0.28f * frenzy);

            // speed: boots burn brighter, streaks peel off when running flat out
            float speedUp = Mathf.Max(0f, s.MoveSpeedMul - 1f);
            bool adrenaline = Combat.AdrenalineMul > 1f;
            player.Rig.GlowBoost = 1f + Mathf.Min(1.4f, speedUp * 3.5f) + (adrenaline ? 0.6f : 0f);
            if (!alive || dt <= 0f) return;
            bool running = player.Grounded && Mathf.Abs(player.Vel.x) > player.MaxSpeedNow * 0.7f;
            if (running && (speedUp > 0f || adrenaline))
            {
                streakAcc += dt * (10f + speedUp * 40f);
                while (streakAcc >= 1f)
                {
                    streakAcc -= 1f;
                    Color c = adrenaline ? Palette.Gold : Palette.Neon;
                    Vector2 at = player.Pos + new Vector2(-Mathf.Sign(player.Vel.x) * 0.25f, Random.Range(0.08f, 1.5f));
                    fx.Streak(FxLayer.Back, at, new Vector2(-Mathf.Sign(player.Vel.x) * Random.Range(4f, 7f), 0f), Random.Range(0.15f, 0.25f),
                        0.02f, 0.06f, c.WithAlpha(0.45f), c.WithAlpha(0f), 1.6f, 2f);
                }
            }

            // stronger legs: a little puff under the feet at take-off
            if (wasGrounded && !player.Grounded && player.Vel.y > 6f && s.JumpMul > 1f)
            {
                fx.Ring(FxLayer.Front, player.Pos + Vector2.up * 0.05f, 0.1f, 0.7f + (s.JumpMul - 1f) * 2f, 0.08f, 0.01f, 0.25f,
                    Color.white, Palette.DashMint.WithAlpha(0f), 2f);
                fx.Sparks(player.Pos, Vector2.down, 120f, 4, 2f, 4f, Palette.DashMint, 2f, 0.03f, 0.2f);
            }
            wasGrounded = player.Grounded;

            // regeneration: small green sparks rise while it heals
            if (s.RegenPerSec > 0f && player.Hp < player.MaxHp && (regenTimer -= dt) <= 0f)
            {
                regenTimer = Mathf.Lerp(0.8f, 0.35f, Mathf.Clamp01(s.RegenPerSec / 2f));
                fx.Spawn(FxLayer.Front, true, Art.CellSparkle, body + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(-0.5f, 0.3f)),
                    new Vector2(0f, Random.Range(0.6f, 1f)), 0.8f, 0.16f, 0f, Palette.Heal, Palette.Heal.WithAlpha(0f), 2.2f, 1f, 0f, 0f, 0f, true, true);
            }
        }
    }
}
