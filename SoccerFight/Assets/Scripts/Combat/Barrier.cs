using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The free-kick wall: three ghost defenders that hold a line for a few seconds. They swallow
    /// enemy projectiles, stop monsters from walking through and bounce the player's own ball back
    /// into the fight. Only one wall stands at a time — a new one replaces the old.
    /// </summary>
    public sealed class Barrier
    {
        public static Barrier I { get; private set; }

        public const float Half = 0.28f;      // half thickness of the slab
        public const float Width = 1.25f;     // half width of the line of defenders
        public const float Height = 2.25f;

        static Sprite figure;

        Transform root;
        SpriteRenderer[] men = new SpriteRenderer[3];
        SpriteRenderer glow, line;
        float x, floor, life, age;
        bool active;

        public bool Active => active;
        public float X => x;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; figure = null; }

        // ------------------------------------------------------------------ build

        public void Build(Transform parent)
        {
            I = this;
            if (figure == null) figure = BuildFigure();

            root = new GameObject("Barrier").transform;
            root.SetParent(parent, false);
            glow = Art.MakeSprite("Glow", root, Art.SoftGlow, 58, Art.SpriteGlowMat, Palette.Guard.WithAlpha(0f));
            glow.transform.localScale = new Vector3(4.6f, 3.4f, 1f);
            for (int i = 0; i < men.Length; i++)
            {
                men[i] = Art.MakeSprite("Defender" + i, root, figure, 59 + i, Art.SpriteMat, Color.clear);
                men[i].transform.localPosition = new Vector3((i - 1) * 0.78f, 0f, 0f);
            }
            line = Art.MakeSprite("Line", root, Art.SoftGlow, 62, Art.SpriteGlowMat, Color.clear);
            line.transform.localScale = new Vector3(3.2f, 0.3f, 1f);
            root.gameObject.SetActive(false);
        }

        /// <summary>A plain standing figure with the arms crossed in front of the face.</summary>
        static Sprite BuildFigure()
        {
            const float ppu = 200f;
            var c = new SdfCanvas(new Rect(-0.42f, -0.04f, 0.84f, 2.3f), ppu);
            SdfCanvas.SdfFn body = p => Sdf.Union(
                Sdf.Union(Sdf.Capsule(p, new Vector2(-0.12f, 0.09f), new Vector2(-0.09f, 0.92f), 0.085f),  // legs, with a gap
                          Sdf.Capsule(p, new Vector2(0.12f, 0.09f), new Vector2(0.09f, 0.92f), 0.085f)),
                Sdf.Union(Sdf.Union(Sdf.Box(p, new Vector2(0f, 1.3f), new Vector2(0.155f, 0.38f), 0.1f),   // torso
                                    Sdf.Box(p, new Vector2(0f, 1.63f), new Vector2(0.235f, 0.075f), 0.07f)), // shoulders
                          Sdf.Union(Sdf.Capsule(p, new Vector2(0f, 1.72f), new Vector2(0f, 1.78f), 0.055f), // neck
                                    Sdf.Circle(p, new Vector2(0f, 1.94f), 0.165f))));                        // head
            // arms folded in front of the chest
            SdfCanvas.SdfFn arms = p => Sdf.Union(Sdf.Capsule(p, new Vector2(-0.245f, 1.5f), new Vector2(0.235f, 1.33f), 0.068f),
                                                  Sdf.Capsule(p, new Vector2(0.245f, 1.52f), new Vector2(-0.235f, 1.35f), 0.068f));
            SdfCanvas.SdfFn all = p => Sdf.Union(body(p), arms(p));
            c.Fill(all, new Color(1f, 1f, 1f, 0.72f));
            // a brighter rim so the silhouette reads against the dark arena
            c.Fill(p => Mathf.Abs(all(p)) - 0.016f, Color.white);
            return c.ToSprite("Defender", new Vector2(0f, 0f));
        }

        // ------------------------------------------------------------------ control

        public void Spawn(float atX, float floorY, float seconds)
        {
            x = atX;
            floor = floorY;
            life = seconds;
            age = 0f;
            active = true;
            root.gameObject.SetActive(true);
            root.position = new Vector3(x, floor, 0f);

            var fx = FxSystem.I;
            for (int i = 0; i < 3; i++)
            {
                Vector2 at = new Vector2(x + (i - 1) * 0.78f, floor);
                fx.Dust(at, Vector2.up, 5, 2.2f, 0.4f, 0.35f);
                fx.Ring(FxLayer.Front, at + new Vector2(0f, 0.1f), 0.1f, 0.9f, 0.12f, 0.01f, 0.26f, Color.white, Palette.Guard.WithAlpha(0f), 2.2f);
            }
            fx.Flash(new Vector2(x, floor + 1.1f), 2.4f, Palette.Guard, 0.18f, 2.6f);
            Game.I.Cam.AddTrauma(0.12f);
        }

        public void Clear()
        {
            active = false;
            if (root != null) root.gameObject.SetActive(false);
        }

        /// <summary>Is this point inside the slab? Used by enemy projectiles.</summary>
        public bool Blocks(Vector2 p)
            => active && Mathf.Abs(p.x - x) < Half + 0.1f && p.y > floor - 0.2f && p.y < floor + Height;

        /// <summary>
        /// The player's ball comes off the wall like a rebound off a defender — with the Abpraller
        /// card it leaves faster than it arrived.
        /// </summary>
        public bool Deflect(Vector2 prev, ref Vector2 pos, ref Vector2 vel, float radius)
        {
            if (!active || pos.y < floor - 0.1f || pos.y > floor + Height) return false;
            float side = prev.x < x ? -1f : 1f;
            float face = x + side * (Half + radius);
            bool crossed = side < 0f ? pos.x > face : pos.x < face;
            if (!crossed || Mathf.Abs(vel.x) < 0.5f) return false;

            bool boost = Game.I.Run.Stats.WallBounce;
            pos.x = face;
            vel.x = -vel.x * (boost ? 1.2f : 0.92f);
            vel.y += boost ? 1.6f : 0.6f;

            var fx = FxSystem.I;
            Vector2 at = new Vector2(face, pos.y);
            fx.Flash(at, boost ? 1.6f : 1.1f, boost ? Color.white : Palette.Guard, 0.12f, 2.8f);
            fx.Ring(FxLayer.Front, at, 0.08f, boost ? 1.2f : 0.8f, 0.12f, 0.01f, 0.2f, Color.white, Palette.Guard.WithAlpha(0f), 2.4f);
            fx.Sparks(at, new Vector2(Mathf.Sign(vel.x), 0.3f), 60f, boost ? 10 : 6, 4f, 10f, Palette.Guard, 2.4f, 0.04f, 0.22f);
            Game.I.Cam.AddTrauma(0.08f);
            return true;
        }

        // ------------------------------------------------------------------ update

        public void Update(float dt)
        {
            if (!active) return;
            age += dt;
            if (age >= life)
            {
                Vector2 at = new Vector2(x, floor + 1f);
                FxSystem.I.Motes(at, Vector2.up * 1.6f, Palette.Guard, 8, 0.8f);
                Clear();
                return;
            }

            float k = Mathf.Min(1f, age / 0.18f);                       // pop in
            float outT = Mathf.Clamp01((life - age) / 0.5f);            // fade out
            float pulse = 0.5f + 0.5f * Mathf.Sin(age * 7f);
            for (int i = 0; i < men.Length; i++)
            {
                float bob = Mathf.Sin(age * 5f + i * 1.7f) * 0.03f;
                float rise = MathUtil.EaseOutBack(Mathf.Clamp01(k - i * 0.06f), 2.4f);
                men[i].transform.localPosition = new Vector3((i - 1) * 0.78f, bob - (1f - rise) * 0.5f, 0f);
                men[i].transform.localScale = new Vector3(1f, Mathf.Lerp(0.6f, 1f, rise), 1f);
                men[i].color = Color.Lerp(Palette.Guard, Color.white, 0.25f + 0.15f * pulse).WithAlpha(0.72f * outT * rise);
            }
            glow.color = Palette.Guard.WithAlpha((0.16f + 0.06f * pulse) * outT);
            glow.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            line.color = Palette.Guard.WithAlpha(0.35f * outT);
            line.transform.localPosition = new Vector3(0f, 0.06f, 0f);

            HoldMonsters(dt, outT);
        }

        /// <summary>Ground monsters pile up in front of the wall instead of walking through it.</summary>
        void HoldMonsters(float dt, float strength)
        {
            var list = Game.I.Waves.Monsters;
            var player = Game.I.Player;
            float side = Mathf.Sign(x - player.Pos.x);
            if (side == 0f) side = 1f;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m.Alive || m.Rank == Rank.Boss) continue;
                if (m.Center.y > floor + Height + m.Radius * 0.5f || m.Center.y < floor - 1f) continue;
                float push = Half + m.Radius * 0.7f;
                float d = m.Pos.x - x;
                if (Mathf.Abs(d) > push) continue;
                float to = x + Mathf.Sign(d == 0f ? side : d) * push;
                m.Pos.x = Mathf.Lerp(m.Pos.x, to, 1f - Mathf.Exp(-22f * dt));
                if ((to - x) * m.Vel.x < 0f) m.Vel.x *= 0.2f;
                if (Random.value < dt * 6f * strength)
                    FxSystem.I.Sparks(new Vector2(to, m.Center.y), new Vector2(Mathf.Sign(to - x), 0.4f), 50f, 1, 2f, 5f, Palette.Guard, 2.2f, 0.04f, 0.22f);
            }
        }
    }
}
