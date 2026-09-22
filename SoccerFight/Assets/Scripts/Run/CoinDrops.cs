using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>The coin, drawn once: the world sprite that bounces on the pitch and the UI sprite that flies to the counter.</summary>
    public static class CoinArt
    {
        public static Sprite World, Ui;
        /// <summary>Alpha-blended with a touch of HDR, so the gold catches the bloom without burning out.</summary>
        public static Material Mat;

        static bool built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { built = false; World = Ui = null; Mat = null; }

        static Color Mul(Color c, float f) { c.r *= f; c.g *= f; c.b *= f; return c; }

        /// <summary>Lantern gold with a raised rim, a struck ball in the middle and a soft highlight (units: radius 1).</summary>
        static void Draw(SdfCanvas c, float r)
        {
            Color gold = Palette.Coin;
            c.Fill(p => Sdf.Circle(p, Vector2.zero, r), p => Mul(gold, Mathf.Lerp(0.62f, 0.95f, MathUtil.Smooth01((p.y + r) / (2f * r)))));
            c.Fill(p => Sdf.Circle(p, Vector2.zero, r * 0.84f), p => Mul(gold, Mathf.Lerp(1.02f, 0.78f, MathUtil.Smooth01((p.y + r) / (2f * r)))));
            c.Fill(p => Mathf.Abs(Sdf.Circle(p, Vector2.zero, r * 0.66f)) - r * 0.035f, Mul(gold, 0.66f));
            // the ball struck into the face
            c.Fill(p => Sdf.Circle(p, Vector2.zero, r * 0.38f), Mul(gold, 0.7f));
            c.Fill(p => Sdf.Circle(p, new Vector2(-r * 0.06f, r * 0.06f), r * 0.3f), Mul(gold, 1.08f));
            c.Fill(p => Sdf.Circle(p, Vector2.zero, r * 0.11f), Mul(gold, 0.66f));
            for (int k = 0; k < 5; k++)
            {
                Vector2 d = MathUtil.Dir(90f + k * 72f) * r * 0.28f;
                c.Fill(p => Sdf.Circle(p, d, r * 0.07f), Mul(gold, 0.68f));
            }
            c.Paint(p => Sdf.Ellipse(p, new Vector2(-r * 0.34f, r * 0.46f), new Vector2(r * 0.3f, r * 0.15f)), new Color(1f, 1f, 0.9f, 0.6f), r * 0.1f);
        }

        public static void Build()
        {
            if (built) return;
            built = true;
            // world: 0.16 units radius (the ball is 0.2)
            var w = new SdfCanvas(new Rect(-0.17f, -0.17f, 0.34f, 0.34f), 360f);
            Draw(w, 0.16f);
            World = w.ToSprite("Coin", Vector2.zero);
            Mat = Art.MakeSpriteMaterial("SF Coin", 1.3f, false);
            // ui: 64 px across at 2x
            var u = new SdfCanvas(new Rect(-32, -32, 64, 64), 2f);
            Draw(u, 30f);
            Ui = UiArt.ToUi(u, "UiCoin");
        }
    }

    /// <summary>
    /// Coins in the world. A kill throws them out of the monster: they arc, clink onto the turf or a
    /// platform, bounce a little, spin and glint for a moment — then each lifts off and is handed to
    /// the HUD, which flies it into the counter (CoinCounter). The value is only credited when it
    /// arrives there; anything still on its way when a run ends is paid out at once (Flush).
    /// </summary>
    public sealed class CoinDrops
    {
        public static CoinDrops I { get; private set; }

        sealed class Coin
        {
            public Transform T, Spin;
            public SpriteRenderer Face, Glow;
            public Vector2 Pos, Vel;
            public float Angle, SpinRate, Age, Rest, Delay, Scale;
            public int Value, Bounces;
            public bool Live, Landed;
        }

        const int Order = 126;
        const float Gravity = 30f, Radius = 0.16f;
        /// <summary>How long a coin shows itself after landing before it flies to the counter.</summary>
        const float ShowTime = 0.55f;

        readonly List<Coin> coins = new List<Coin>();
        Transform root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        public void Build(Transform parent)
        {
            CoinArt.Build();
            root = new GameObject("Coins").transform;
            root.SetParent(parent, false);
            I = this;
        }

        // ------------------------------------------------------------------ spawning

        /// <summary>A monster died: its coins burst out of it.</summary>
        public void Drop(Monster m)
        {
            int value = CoinRewards.ForKill(m, Game.I.Run.Stage);
            if (value <= 0) return;
            int cap = m.Rank == Rank.Boss ? 16 : m.Rank == Rank.MiniBoss ? 10 : m.Rank == Rank.Elite ? 6 : 3;
            Spawn(m.Center, value, cap, m.Rank >= Rank.MiniBoss ? 1.4f : 1f, false);
        }

        /// <summary>A stage bonus: coins rain down around a point.</summary>
        public void Shower(Vector2 at, int value)
        {
            if (value <= 0) return;
            Spawn(at + new Vector2(0f, 1.5f), value, 18, 1.6f, true);
            FxSystem.I.Sparkles(at + new Vector2(0f, 1.5f), 1.2f, 16, Palette.Coin, 3f, 0.9f);
        }

        void Spawn(Vector2 at, int value, int maxCoins, float spread, bool shower)
        {
            CoinRewards.Note(value);
            int n = Mathf.Clamp(value, 1, maxCoins);
            int each = value / n, extra = value - each * n;
            var fx = FxSystem.I;
            fx.Flash(at, 1f + 0.3f * n / (float)maxCoins, Palette.Coin, 0.14f, 2.4f);
            fx.Sparkles(at, 0.4f * spread, 3 + n / 2, Palette.Coin, 2.8f, 0.6f);
            for (int i = 0; i < n; i++)
            {
                var c = Take();
                c.Value = each + (i < extra ? 1 : 0);
                c.Pos = at + Random.insideUnitCircle * 0.2f;
                float side = Random.Range(-1f, 1f);
                c.Vel = shower
                    ? new Vector2(side * 4.5f * spread, Random.Range(6f, 12f))
                    : new Vector2(side * 3.2f * spread, Random.Range(5.5f, 9.5f) + (spread - 1f) * 3f);
                c.Angle = Random.Range(0f, 360f);
                c.SpinRate = Random.Range(540f, 900f) * (Random.value < 0.5f ? -1f : 1f);
                c.Age = 0f;
                c.Rest = 0f;
                c.Bounces = 0;
                c.Landed = false;
                c.Delay = ShowTime + i * 0.045f + Random.Range(0f, 0.08f);
                c.Scale = c.Value >= 5 ? 1.3f : c.Value >= 3 ? 1.12f : 1f;
            }
        }

        Coin Take()
        {
            foreach (var c in coins) if (!c.Live) { c.Live = true; c.T.gameObject.SetActive(true); return c; }
            var n = new Coin();
            n.T = new GameObject("Coin").transform;
            n.T.SetParent(root, false);
            n.Glow = Art.MakeSprite("Glow", n.T, Art.SoftGlow, Order - 1, Art.SpriteGlowMat, Palette.Coin.WithAlpha(0.25f));
            n.Glow.transform.localScale = Vector3.one * 0.7f;
            n.Spin = new GameObject("Spin").transform;
            n.Spin.SetParent(n.T, false);
            n.Face = Art.MakeSprite("Face", n.Spin, CoinArt.World, Order, CoinArt.Mat);
            n.Live = true;
            coins.Add(n);
            return n;
        }

        // ------------------------------------------------------------------ update

        public void Update(float dt)
        {
            var hud = Game.I.Hud;
            for (int i = 0; i < coins.Count; i++)
            {
                var c = coins[i];
                if (!c.Live) continue;
                c.Age += dt;
                Vector2 prev = c.Pos;
                if (!c.Landed || c.Vel.sqrMagnitude > 0.0001f)
                {
                    c.Vel.y -= Gravity * dt;
                    c.Pos += c.Vel * dt;
                    if (Mathf.Abs(c.Pos.x) > Player.ArenaHalf + 0.3f) { c.Pos.x = Mathf.Sign(c.Pos.x) * (Player.ArenaHalf + 0.3f); c.Vel.x *= -0.5f; }
                    float floor = Level.FloorBelow(c.Pos.x, prev.y - Radius + 0.02f);
                    if (c.Vel.y <= 0f && c.Pos.y <= floor + Radius)
                    {
                        float impact = -c.Vel.y;
                        c.Pos.y = floor + Radius;
                        if (impact > 2.2f && c.Bounces < 3)
                        {
                            // clink: a little hop, a puff of turf and a glint
                            c.Vel.y = impact * 0.42f;
                            c.Vel.x *= 0.65f;
                            c.Bounces++;
                            c.SpinRate *= 0.6f;
                            if (c.Bounces == 1)
                            {
                                Sfx.Play(Sound.CoinDrop, Mathf.Clamp01(impact / 12f) * 0.55f, 1f + Random.Range(-0.06f, 0.1f));
                                FxSystem.I.Dust(new Vector2(c.Pos.x, floor), new Vector2(c.Vel.x * 0.1f, 0.2f), 1, 0.8f, 0.18f, 0.2f);
                                FxSystem.I.Sparkles(c.Pos, 0.12f, 1, Palette.Coin, 2.6f, 0.35f);
                            }
                        }
                        else
                        {
                            c.Vel = Vector2.zero;
                            c.Landed = true;
                        }
                    }
                }
                if (c.Landed) c.Rest += dt;

                // the coin keeps turning; lying still it slows to a gentle wobble and glints now and then
                float spinRate = c.Landed ? Mathf.Lerp(c.SpinRate, 160f * Mathf.Sign(c.SpinRate), 1f - Mathf.Exp(-4f * c.Rest)) : c.SpinRate;
                c.Angle += spinRate * dt;
                float face = Mathf.Abs(Mathf.Cos(c.Angle * Mathf.Deg2Rad));
                float bob = c.Landed ? 0.04f * Mathf.Sin(c.Rest * 7f) : 0f;
                c.T.position = new Vector3(c.Pos.x, c.Pos.y + bob, 0f);
                c.Spin.localScale = new Vector3(Mathf.Max(0.14f, face) * c.Scale, c.Scale, 1f);
                c.Face.color = Color.Lerp(new Color(0.55f, 0.45f, 0.3f), Color.white, 0.35f + 0.65f * face);
                c.Glow.color = Palette.Coin.WithAlpha(0.18f + 0.18f * face);
                if (c.Landed && Random.value < dt * 1.5f) FxSystem.I.Sparkles(c.Pos + new Vector2(0f, 0.05f), 0.1f, 1, Color.white, 2.4f, 0.3f);

                // shown long enough: it lifts off towards the counter
                bool ready = (c.Landed && c.Rest > 0.12f && c.Age > c.Delay) || c.Age > c.Delay + 1.4f;
                if (ready && hud != null)
                {
                    hud.Coins.Launch(c.Pos, c.Value, c.Scale);
                    FxSystem.I.Sparkles(c.Pos, 0.15f, 2, Palette.Coin, 2.8f, 0.35f);
                    Retire(c);
                }
            }
        }

        void Retire(Coin c)
        {
            c.Live = false;
            c.T.gameObject.SetActive(false);
        }

        /// <summary>The run ends or restarts: pay out everything still on the pitch or in the air.</summary>
        public void Flush()
        {
            int total = 0;
            foreach (var c in coins)
            {
                if (!c.Live) continue;
                total += c.Value;
                Retire(c);
            }
            if (total > 0) Wallet.Add(Currencies.Coins, total);
            Game.I.Hud.Coins.Flush();
        }
    }
}
