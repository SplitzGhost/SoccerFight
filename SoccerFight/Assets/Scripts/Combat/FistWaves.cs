using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Die Druckwellen der Boxer: Jeder Schlag drückt die Luft vor dem Handschuh zu einer hellen Sichel zusammen,
    /// die ein kurzes Stück weiterfliegt (so trifft ein Boxer auch, was nicht direkt vor ihm steht, und Flieger
    /// knapp über ihm). Sie wird beim Fliegen breiter und blasser, verliert also sichtbar Kraft; trifft sie einen
    /// Gegner, platzt sie dort mit einem Schlagstern (außer sie darf durchschlagen). Upgrades: Fächer, Abpraller,
    /// Sprengfaust, Durchschlag. Die Wellen des Duo-Partners werden hier nur gezeichnet.
    /// </summary>
    public sealed class FistWaves
    {
        public static FistWaves I { get; private set; }

        sealed class Wave
        {
            public Vector2 pos, vel;
            public float age, life, dmg, radius, knock, size;
            public Src src;
            public Color color;
            public int pierceLeft, bounces;
            public bool active, partner, crit, explode, heavy;
            public readonly HashSet<int> hit = new HashSet<int>();
            public Transform root;
            public SpriteRenderer body, core, glow;
            public float trailT;
        }

        readonly List<Wave> pool = new List<Wave>();
        Transform parent;
        static Sprite crescent, crescentCore;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; crescent = crescentCore = null; }

        public void Build(Transform root)
        {
            I = this;
            parent = new GameObject("Fist Waves").transform;
            parent.SetParent(root, false);
            if (crescent == null) BuildSprites();
        }

        /// <summary>
        /// Eine Sichel, die nach vorn (+x) gewölbt ist: die Luft vor der Faust. Die Spitzen laufen dünn aus, die
        /// Mitte ist am dicksten; dazu ein schmaler heller Kern an der Vorderkante.
        /// </summary>
        static void BuildSprites()
        {
            const float ppu = 160f;
            var c = new SdfCanvas(new Rect(-0.6f, -0.62f, 1.2f, 1.24f), ppu);
            System.Func<Vector2, float> shape = p =>
                Sdf.Subtract(Sdf.Circle(p, new Vector2(-0.12f, 0f), 0.52f), Sdf.Circle(p, new Vector2(-0.4f, 0f), 0.56f));
            c.Fill(p => shape(p), p =>
            {
                // vorn hell, nach hinten ausblassend; die Spitzen weicher als die Mitte
                float front = Mathf.Clamp01((p.x + 0.1f) / 0.5f);
                float tip = 1f - Mathf.Clamp01(Mathf.Abs(p.y) / 0.55f);
                return new Color(1f, 1f, 1f, Mathf.Lerp(0.25f, 1f, front) * Mathf.Lerp(0.35f, 1f, tip));
            }, 0.05f);
            crescent = c.ToSprite("FistWave", Vector2.zero);

            var k = new SdfCanvas(new Rect(-0.6f, -0.62f, 1.2f, 1.24f), ppu);
            k.Fill(p => Sdf.Subtract(Sdf.Circle(p, new Vector2(-0.12f, 0f), 0.47f), Sdf.Circle(p, new Vector2(-0.3f, 0f), 0.5f)),
                p => new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(Mathf.Abs(p.y) / 0.45f)), 0.03f);
            crescentCore = k.ToSprite("FistWaveCore", Vector2.zero);
        }

        Wave Get()
        {
            foreach (var w in pool) if (!w.active) return w;
            var n = new Wave { root = new GameObject("Wave").transform };
            n.root.SetParent(parent, false);
            n.glow = Art.MakeSprite("Glow", n.root, Art.SoftGlow, 122, Art.SpriteGlowMat, Color.clear);
            n.body = Art.MakeSprite("Body", n.root, crescent, 123, Art.SpriteGlowMat, Color.clear);
            n.core = Art.MakeSprite("Core", n.root, crescentCore, 124, Art.SpriteGlowMat, Color.clear);
            pool.Add(n);
            return n;
        }

        /// <summary>
        /// dmg: Grundschaden (Combat rechnet den Aufbau drauf). range: wie weit die Welle fliegt. radius: ihre Trefferbreite.
        /// pierce: so viele Gegner durchschlägt sie noch, bevor sie platzt. size: Größe des Bildes (1 = ein normaler Schlag).
        /// </summary>
        public void Fire(Vector2 from, Vector2 dir, float speed, float range, float dmg, float radius, float knock, Src src, Color color,
            int pierce = 0, bool crit = false, bool explode = false, int bounces = 0, float size = 1f, bool heavy = false, bool partner = false)
        {
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.right;
            dir.Normalize();
            if (!partner) Coop.SendJolt(from, dir, speed, range, radius, size, color);
            var w = Get();
            w.partner = partner;
            w.pos = from; w.vel = dir * speed;
            w.life = Mathf.Max(0.05f, range / Mathf.Max(1f, speed));
            w.age = 0f; w.dmg = dmg; w.radius = radius; w.knock = knock; w.src = src; w.color = color; w.size = size;
            w.pierceLeft = pierce; w.bounces = bounces; w.crit = crit; w.explode = explode; w.heavy = heavy;
            w.trailT = 0f;
            w.hit.Clear();
            w.active = true;
            w.root.gameObject.SetActive(true);
            Place(w, 0f);
            // die Luft reißt am Handschuh auf
            var fx = FxSystem.I;
            fx.Ring(FxLayer.Front, from, 0.05f, 0.42f * size, 0.08f, 0.01f, 0.12f, Color.white.WithAlpha(0.7f), color.WithAlpha(0f), 1.8f);
            fx.Spawn(FxLayer.Front, true, Art.CellGlow, from, Vector2.zero, 0.1f, 0.5f * size, 0.9f * size, color.WithAlpha(0.6f), color.WithAlpha(0f), 1.6f);
        }

        public void Clear()
        {
            foreach (var w in pool) { w.active = false; w.root.gameObject.SetActive(false); }
        }

        void Kill(Wave w, bool pop)
        {
            if (pop)
            {
                var fx = FxSystem.I;
                fx.Ring(FxLayer.Front, w.pos, 0.1f, 0.7f * w.size, 0.12f, 0.01f, 0.18f, Color.white.WithAlpha(0.8f), w.color.WithAlpha(0f), 2f);
                fx.Sparks(w.pos, w.vel, 70f, 5, 3f, 7f, w.color, 2.2f, 0.035f, 0.16f);
            }
            w.active = false;
            w.root.gameObject.SetActive(false);
        }

        /// <summary>Lage und Aussehen: die Sichel steht quer zur Flugrichtung, wächst und verblasst.</summary>
        void Place(Wave w, float k)
        {
            w.root.position = new Vector3(w.pos.x, w.pos.y, 0f);
            float ang = Mathf.Atan2(w.vel.y, w.vel.x) * Mathf.Rad2Deg;
            w.root.localRotation = Quaternion.Euler(0f, 0f, ang);
            // schnell auf volle Größe, dann langsam breiter (die Luft gibt nach), zum Ende flacher
            float grow = Mathf.Lerp(0.55f, 1f, MathUtil.EaseOutCubic(Mathf.Clamp01(k * 4f))) + 0.25f * k;
            float s = w.size * (w.radius / 0.45f) * grow;
            w.body.transform.localScale = new Vector3(s * Mathf.Lerp(1f, 0.75f, k), s, 1f);
            w.core.transform.localScale = w.body.transform.localScale;
            w.glow.transform.localScale = new Vector3(s * 0.9f, s * 1.3f, 1f);
            w.glow.transform.localPosition = new Vector3(0.12f * s, 0f, 0f);
            float a = Mathf.Clamp01(k * 12f) * (1f - MathUtil.EaseInQuad(k));
            Color hot = Color.Lerp(w.color, Palette.PunchCore, 0.35f);
            // gedämpft: additiv mit Bloom wirkt schon wenig sehr hell
            w.body.color = hot.WithAlpha(0.48f * a);
            w.core.color = Palette.PunchCore.WithAlpha(0.4f * a);
            w.glow.color = w.color.WithAlpha(0.13f * a);
        }

        public void Update(float dt)
        {
            var list = Game.I.Waves.Monsters;
            var fx = FxSystem.I;
            foreach (var w in pool)
            {
                if (!w.active) continue;
                w.age += dt;
                // die Welle bremst ab, je weiter sie kommt
                float k = Mathf.Clamp01(w.age / w.life);
                w.pos += w.vel * dt * Mathf.Lerp(1.15f, 0.7f, k);

                // feine Luftschlieren hinter der Sichel
                w.trailT -= dt;
                if (w.trailT <= 0f)
                {
                    w.trailT = 0.025f;
                    Vector2 d = w.vel.normalized, n = new Vector2(-d.y, d.x);
                    fx.Streak(FxLayer.Front, w.pos - d * 0.15f + n * Random.Range(-0.3f, 0.3f) * w.size, -d * Random.Range(2f, 4f), 0.12f, 0.03f * w.size, 0.05f,
                        Color.white.WithAlpha(0.4f * (1f - k)), w.color.WithAlpha(0f), 1.6f, 6f);
                }

                bool done = false;
                for (int i = 0; i < list.Count && !done; i++)
                {
                    var m = list[i];
                    if (!m.Alive || w.hit.Contains(m.Id)) continue;
                    float r = m.Radius + w.radius * Mathf.Lerp(1f, 1.2f, k);
                    if ((m.Center - w.pos).sqrMagnitude > r * r) continue;
                    w.hit.Add(m.Id);
                    if (!w.partner) Strike(w, m);
                    if (w.pierceLeft > 0) { w.pierceLeft--; continue; }
                    if (w.bounces > 0 && !w.partner)
                    {
                        var next = Combat.NearestTo(w.pos, 7f, m);
                        if (next != null && !w.hit.Contains(next.Id))
                        {
                            w.bounces--;
                            w.vel = (next.Center - w.pos).normalized * Mathf.Max(w.vel.magnitude, 18f);
                            w.age = Mathf.Min(w.age, w.life * 0.3f);
                            continue;
                        }
                    }
                    Kill(w, true);
                    done = true;
                }
                if (done) continue;

                // der Boden schluckt sie; über den Rand der Arena kommt sie nicht
                float floor = Level.FloorBelow(w.pos.x, w.pos.y + 0.25f);
                if (w.pos.y < floor + 0.05f && w.vel.y < 0f) { Kill(w, true); continue; }
                if (w.age >= w.life || Mathf.Abs(w.pos.x) > Player.ArenaHalf + 1.5f) { Kill(w, false); continue; }
                Place(w, k);
            }
        }

        void Strike(Wave w, Monster m)
        {
            Vector2 at = m.Center;
            Vector2 dir = w.vel.normalized;
            var s = Game.I.Run.Stats;
            // K.O.-König: ein angeschlagener normaler Gegner geht sofort zu Boden
            bool ko = s.Knockout && m.Rank == Rank.Normal && m.Hp < m.MaxHp * 0.2f;
            float dmg = ko ? m.Hp + 1f : w.dmg;
            bool killed = Combat.Hit(m, dmg, dir + Vector2.up * 0.25f, w.knock, w.src, forceCrit: w.crit, big: w.heavy || w.crit || ko);
            var fx = FxSystem.I;
            // der Schlagstern: kurze helle Zacke in Schlagrichtung, Funken weiter in derselben Richtung
            float ang = MathUtil.Angle(dir);
            fx.Spawn(FxLayer.Front, true, Art.CellSparkle, at - dir * Mathf.Min(m.Radius * 0.6f, 0.45f), Vector2.zero, 0.16f,
                (w.heavy ? 1.7f : 1.15f) * w.size, 0.3f, Palette.PunchCore, w.color.WithAlpha(0f), 2.2f, 0f, 0f, ang, 0f, false);
            fx.Sparks(at, dir, 50f, w.heavy ? 9 : 5, 5f, 12f, w.color, 2.2f, 0.045f, 0.2f);
            if (ko)
            {
                Game.I.Hud.Popup(at + new Vector2(0f, m.Radius + 0.5f), "K.O.!", Palette.Gold, 34f, true);
                fx.Ring(FxLayer.Front, at, 0.2f, 2f, 0.25f, 0.01f, 0.35f, Color.white, Palette.Gold.WithAlpha(0f), 2.4f);
                TimeFx.HitStop(0.05f, 0.05f);
            }
            if (w.explode) Combat.Explosion(at, 1.4f * s.AreaMul, w.dmg * 0.6f, Palette.BlastOrange);
            TimeFx.HitStop(w.heavy ? 0.045f : 0.018f, 0.06f);
            Game.I.Cam.AddTrauma(w.heavy ? 0.12f : 0.04f);
            if (killed) Game.I.Cam.Kick(dir * 0.05f);
        }
    }
}
