using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Was die Ultis in der Welt lassen: Rios Feuerkomet, Brunos goldene Kuppel, Miras fünf Zauberbälle,
    /// Dres Buzzer-Würfe mit ihren Fadenkreuzen, Titans Meteor-Marke und Krater. Schaden macht nur die
    /// Seite des Spielers, der die Ulti auslöst (local); der Duo-Partner sieht dieselben Bilder über
    /// <see cref="Remote"/>. Die Ulti-Treffer laufen als Src.Ulti und füllen die Ulti-Leiste nicht.
    /// </summary>
    public sealed class Ultis
    {
        public static Ultis I { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        const float CometSpeed = 36f, CometReach = 0.8f;

        sealed class Fireball
        {
            public bool active, local;
            public Player owner;
            public Vector2 pos, prev, dir;
            public float t, dmg, blastDmg, radius, fxT, spin;
            public readonly HashSet<int> hit = new HashSet<int>();
            public Transform root, spinNode;
            public SpriteRenderer outer, glow, core, pattern;
            public TrailRenderer trail, flame;
        }

        sealed class Shield
        {
            public bool active, local;
            public Player owner;
            public float life, age, r, power, fxT;
            public readonly Dictionary<int, float> pushCd = new Dictionary<int, float>();
            public Transform root;
            public SpriteRenderer shell, sheen, floor, line;
        }

        sealed class Orb
        {
            public bool active, local, flying;
            public Player owner;
            public int index, count;
            public Vector2 pos, vel, to;
            public Monster target;
            public float t, flyT, dmg, spin, fxT;
            public Color color;
            public Transform root, spinNode;
            public SpriteRenderer glow, core, pattern;
            public TrailRenderer trail;
        }

        sealed class Lob
        {
            public bool active, local;
            public Player owner;
            public int index, clock;
            public Vector2 from, to, pos;
            public Monster target;
            public float t, flight, h, dmg, radius, spin, fxT;
            public Transform root, spinNode;
            public SpriteRenderer glow, pattern, core;
            public TrailRenderer trail;
        }

        sealed class Reticle
        {
            public bool active;
            public int index, dup;
            public Monster target;
            public Vector2 at;
            public float t;
            public SpriteRenderer ring, glow;
        }

        sealed class Mark
        {
            public bool active;
            public Vector2 at;
            public float t, life, r, fxT;
            public SpriteRenderer shadow, ring, glow;
        }

        sealed class Crater
        {
            public bool active;
            public Vector2 at;
            public float t, life, r;
            public SpriteRenderer cracks, glow;
        }

        readonly List<Fireball> comets = new List<Fireball>();
        readonly List<Shield> domes = new List<Shield>();
        readonly List<Orb> orbs = new List<Orb>();
        readonly List<Lob> lobs = new List<Lob>();
        readonly List<Reticle> reticles = new List<Reticle>();
        readonly List<Mark> marks = new List<Mark>();
        readonly List<Crater> craters = new List<Crater>();
        Transform parent;

        static Sprite domeSprite, reticleSprite, crackSprite;
        static Gradient cometGradient, flameGradient, lobGradient;

        /// <summary>Ein Stück der Ulti trägt gerade den Ball (Komet, Buzzer-Würfe).</summary>
        public bool HoldsBall
        {
            get
            {
                foreach (var c in comets) if (c.active && c.local) return true;
                foreach (var l in lobs) if (l.active && l.local) return true;
                return false;
            }
        }

        public void Build(Transform root)
        {
            I = this;
            parent = new GameObject("Ultis").transform;
            parent.SetParent(root, false);
            BuildArt();
        }

        public void Clear()
        {
            foreach (var c in comets) { c.active = false; c.trail.emitting = c.flame.emitting = false; c.root.gameObject.SetActive(false); }
            foreach (var d in domes) { d.active = false; d.root.gameObject.SetActive(false); }
            foreach (var o in orbs) { o.active = false; o.trail.emitting = false; o.root.gameObject.SetActive(false); }
            foreach (var l in lobs) { l.active = false; l.trail.emitting = false; l.root.gameObject.SetActive(false); }
            foreach (var r in reticles) { r.active = false; r.ring.gameObject.SetActive(false); r.glow.gameObject.SetActive(false); }
            foreach (var m in marks) { m.active = false; m.shadow.gameObject.SetActive(false); m.ring.gameObject.SetActive(false); m.glow.gameObject.SetActive(false); }
            foreach (var c in craters) { c.active = false; c.cracks.gameObject.SetActive(false); c.glow.gameObject.SetActive(false); }
        }

        public void Update(float dt)
        {
            UpdateComets(dt);
            UpdateDomes(dt);
            UpdateOrbs(dt);
            UpdateLobs(dt);
            UpdateReticles(dt);
            UpdateMarks(dt);
            UpdateCraters(dt);
        }

        // ================================================================== Grafik, einmal beim Start gezeichnet

        static void BuildArt()
        {
            if (domeSprite != null) return;

            // die Kuppel: ein Halbkreis, innen fast durchsichtig, zum Rand hin heller (wie Glas von der Seite),
            // ein feines Sechseck-Netz, eine helle Kante und ein Lichtsaum, wo sie auf dem Boden steht
            var dome = new SdfCanvas(new Rect(-0.5f, 0f, 1f, 0.5f), 256f);
            dome.Field(p =>
            {
                float r = p.magnitude / 0.5f;
                if (r >= 1f) return Color.clear;
                float aa = Mathf.Clamp01((1f - r) * 128f / 1.5f);
                float fres = Mathf.Pow(r, 4f);
                float rim = Mathf.Exp(-Mathf.Pow((0.982f - r) / 0.022f, 2f));
                float hex = 1f - MathUtil.Smooth01(HexEdge(p, 0.052f) / 0.075f);
                float floorBand = Mathf.Exp(-Mathf.Pow(p.y / 0.012f, 2f)) * 0.75f;
                float a = 0.04f + 0.42f * fres + 0.95f * rim + hex * (0.05f + 0.38f * fres) + floorBand;
                return new Color(1f, 1f, 1f, Mathf.Clamp01(a) * aa);
            });
            domeSprite = dome.ToSprite("UltiDome", Vector2.zero);

            // Fadenkreuz: Ring, vier Striche, ein Punkt in der Mitte
            var ret = new SdfCanvas(new Rect(-0.5f, -0.5f, 1f, 1f), 128f);
            const float px = 1f / 128f;
            ret.Field(p =>
            {
                float d = p.magnitude;
                float ring = 1f - MathUtil.Smooth01((Mathf.Abs(d - 0.36f) - 0.022f) / (px * 1.5f));
                float ax = Mathf.Abs(p.x), ay = Mathf.Abs(p.y);
                float tickH = ay < 0.016f && ax > 0.24f && ax < 0.47f ? 1f - MathUtil.Smooth01((ay - 0.012f) / (px * 1.5f)) : 0f;
                float tickV = ax < 0.016f && ay > 0.24f && ay < 0.47f ? 1f - MathUtil.Smooth01((ax - 0.012f) / (px * 1.5f)) : 0f;
                float dot = 1f - MathUtil.Smooth01((d - 0.028f) / (px * 1.5f));
                return new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(Mathf.Max(ring, dot), Mathf.Max(tickH, tickV))));
            });
            reticleSprite = ret.ToSprite("UltiReticle", Vector2.zero);

            // Risse im Boden: gezackte Linien von der Mitte nach außen, glühend
            var segs = new List<Vector4>();
            var rng = new System.Random(7);
            float R01() => (float)rng.NextDouble();
            for (int k = 0; k < 9; k++)
            {
                float ang = k * 40f + (R01() - 0.5f) * 24f;
                Vector2 at = MathUtil.Dir(ang) * 0.05f;
                int steps = 4 + rng.Next(3);
                for (int s = 0; s < steps; s++)
                {
                    ang += (R01() - 0.5f) * 40f;
                    Vector2 next = at + MathUtil.Dir(ang) * (0.06f + R01() * 0.05f);
                    segs.Add(new Vector4(at.x, at.y, next.x, next.y));
                    if (s == 1 && R01() < 0.6f)
                    {
                        Vector2 b = next + MathUtil.Dir(ang + (R01() < 0.5f ? 50f : -50f)) * 0.09f;
                        segs.Add(new Vector4(next.x, next.y, b.x, b.y));
                    }
                    at = next;
                }
            }
            var cr = new SdfCanvas(new Rect(-0.5f, -0.5f, 1f, 1f), 160f);
            cr.Field(p =>
            {
                float rr = p.magnitude / 0.5f;
                if (rr >= 1f) return Color.clear;
                float d = 9f;
                foreach (var s in segs) d = Mathf.Min(d, Sdf.Segment(p, new Vector2(s.x, s.y), new Vector2(s.z, s.w)));
                float along = 1f - MathUtil.Smooth01(rr);
                float core = Mathf.Exp(-Mathf.Pow(d / 0.007f, 2f));
                float glow = Mathf.Exp(-Mathf.Pow(d / 0.03f, 2f)) * 0.4f;
                float hole = Mathf.Exp(-Mathf.Pow(rr / 0.16f, 2f)) * 0.75f;
                return new Color(1f, 1f, 1f, Mathf.Clamp01((core + glow) * (0.3f + 0.7f * along) + hole));
            });
            crackSprite = cr.ToSprite("UltiCracks", Vector2.zero);

            cometGradient = Ball.TrailGradient(Color.white, Palette.Gold, new Color(0.95f, 0.3f, 0.15f));
            flameGradient = Ball.TrailGradient(new Color(1f, 0.7f, 0.35f), Palette.BlastOrange, new Color(0.5f, 0.08f, 0.12f));
            lobGradient = Ball.TrailGradient(new Color(1f, 0.95f, 0.8f), Palette.HoopFlame, new Color(0.9f, 0.35f, 0.15f));
        }

        static Vector2 Mod(Vector2 v, Vector2 r) => new Vector2(v.x - r.x * Mathf.Floor(v.x / r.x), v.y - r.y * Mathf.Floor(v.y / r.y));

        /// <summary>Abstand zur nächsten Sechseck-Kante eines Netzes mit Zellbreite s (0 auf der Kante).</summary>
        static float HexEdge(Vector2 p, float s)
        {
            Vector2 q = p / s;
            Vector2 r = new Vector2(1f, 1.7320508f);
            Vector2 h = r * 0.5f;
            Vector2 a = Mod(q, r) - h;
            Vector2 b = Mod(q - h, r) - h;
            Vector2 g = a.sqrMagnitude < b.sqrMagnitude ? a : b;
            g = new Vector2(Mathf.Abs(g.x), Mathf.Abs(g.y));
            return 0.5f - Mathf.Max(g.x, Vector2.Dot(g, new Vector2(0.5f, 0.8660254f)));
        }

        TrailRenderer MakeTrail(Transform root, string name, Material mat, float time, float width, int order, Gradient g)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = mat;
            tr.time = time;
            tr.widthMultiplier = width;
            tr.minVertexDistance = 0.03f;
            tr.numCapVertices = 4;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.4f, 0.6f), new Keyframe(1f, 0f));
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.sortingOrder = order;
            tr.emitting = false;
            if (g != null) tr.colorGradient = g;
            return tr;
        }

        static Player Partner => Coop.Remote != null && Coop.Remote.Present ? Coop.Remote.P : null;

        // ================================================================== RIO: der Feuerkomet

        public void Comet(Player owner, Vector2 from, Vector2 dir, float dmg, float blastDmg, float radius, bool local)
        {
            Fireball c = null;
            foreach (var x in comets) if (!x.active) { c = x; break; }
            if (c == null)
            {
                c = new Fireball { root = new GameObject("UltiComet").transform };
                c.root.SetParent(parent, false);
                c.outer = Art.MakeSprite("Outer", c.root, Art.SoftGlow, 131, Art.SpriteGlowMat, Color.clear);
                c.glow = Art.MakeSprite("Glow", c.root, Art.SoftGlow, 132, Art.SpriteGlowMat, Color.clear);
                c.spinNode = new GameObject("Spin").transform;
                c.spinNode.SetParent(c.root, false);
                c.pattern = Art.MakeSprite("Pattern", c.spinNode, Art.BallPattern, 133, Art.SpriteMat, new Color(1f, 0.86f, 0.7f));
                c.core = Art.MakeSprite("Core", c.root, Art.SoftGlow, 134, Art.SpriteGlowMat, Color.clear);
                c.flame = MakeTrail(c.root, "Flame", Art.TrailShotMat, 0.55f, 1.5f, 129, flameGradient);
                c.trail = MakeTrail(c.root, "Trail", Art.TrailShotMat, 0.3f, 0.7f, 130, cometGradient);
                comets.Add(c);
            }
            c.active = true;
            c.local = local;
            c.owner = owner;
            c.pos = c.prev = from;
            c.dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.right;
            c.t = c.fxT = 0f;
            c.dmg = dmg;
            c.blastDmg = blastDmg;
            c.radius = radius;
            c.hit.Clear();
            c.root.gameObject.SetActive(true);
            c.root.position = from;
            c.trail.Clear();
            c.flame.Clear();
            c.trail.emitting = c.flame.emitting = true;
        }

        void UpdateComets(float dt)
        {
            var fx = FxSystem.I;
            foreach (var c in comets)
            {
                if (!c.active) continue;
                c.t += dt;
                c.prev = c.pos;
                c.pos += c.dir * CometSpeed * dt;
                c.spin -= Mathf.Sign(c.dir.x == 0f ? 1f : c.dir.x) * 1400f * dt;
                c.root.position = c.pos;
                c.spinNode.localRotation = Quaternion.Euler(0f, 0f, c.spin);
                float flick = 0.85f + 0.15f * Mathf.Sin(c.t * 47f) + 0.08f * Mathf.Sin(c.t * 113f);
                // der Ball bleibt im Feuer erkennbar: Glut außen, kein weißer Kern über ihm
                c.outer.color = new Color(1f, 0.32f, 0.12f, 0.16f * flick);
                c.outer.transform.localScale = Vector3.one * 5f * flick;
                c.glow.color = Palette.BlastOrange.WithAlpha(0.42f);
                c.glow.transform.localScale = Vector3.one * (2.2f + 0.25f * Mathf.Sin(c.t * 38f));
                c.core.color = new Color(1f, 0.8f, 0.5f, 0.22f);
                c.core.transform.localScale = Vector3.one * 0.7f;

                // Flammen, Glut und Rauch hinter dem Kometen
                c.fxT -= dt;
                while (c.fxT <= 0f)
                {
                    c.fxT += 0.012f;
                    Vector2 side = new Vector2(-c.dir.y, c.dir.x);
                    for (int i = 0; i < 2; i++)
                        fx.Streak(FxLayer.Front, c.pos - c.dir * 0.3f + side * Random.Range(-0.25f, 0.25f), -c.dir * Random.Range(3f, 8f) + side * Random.Range(-2f, 2f),
                            Random.Range(0.25f, 0.45f), Random.Range(0.1f, 0.17f), 0.05f, new Color(1f, 0.72f, 0.35f, 0.85f), new Color(0.9f, 0.2f, 0.1f, 0f), 1.9f, 2f, -3f);
                    if (Random.value < 0.6f) fx.Sparks(c.pos, -c.dir, 70f, 1, 2f, 6f, Palette.Gold, 2.4f, 0.04f, 0.45f, 5f);
                    if (Random.value < 0.35f)
                        fx.Spawn(FxLayer.Back, false, Art.CellPuff, c.pos - c.dir * 0.6f + side * Random.Range(-0.3f, 0.3f), -c.dir * 1.2f + Vector2.up * 0.8f,
                            Random.Range(0.7f, 1.1f), 0.3f, 1.2f, new Color(0.16f, 0.12f, 0.14f, 0.42f), new Color(0.1f, 0.1f, 0.12f, 0f), 1f, 1.5f, -0.6f,
                            Random.Range(0f, 360f), Random.Range(-60f, 60f));
                }

                if (c.local)
                {
                    var list = Game.I.Waves.Monsters;
                    for (int mi = 0; mi < list.Count; mi++)
                    {
                        var m = list[mi];
                        if (!m.Alive || c.hit.Contains(m.Id)) continue;
                        float r = m.Radius + CometReach;
                        if ((m.Center - c.pos).sqrMagnitude > r * r) continue;
                        c.hit.Add(m.Id);
                        Vector2 at = m.Center;
                        bool killed = Combat.Hit(m, c.dmg, c.dir + Vector2.up * 0.35f, 16f, Src.Ulti, big: true);
                        if (!killed) m.Ignite(c.dmg * 0.12f, 3f);
                        fx.Flash(at, 1.8f, Palette.BlastOrange, 0.12f, 1.8f);
                        fx.Sparks(at, c.dir, 50f, 14, 8f, 18f, Palette.Gold, 2.4f, 0.06f, 0.3f);
                        TimeFx.HitStop(0.045f, 0.05f);
                        Game.I.Cam.AddTrauma(0.18f);
                    }
                }

                // die Explosion soll man sehen: am Bildrand ist Schluss (spätestens an der Arenawand)
                var view = Game.I.Cam.ViewRect;
                float wall = Mathf.Min(Player.ArenaHalf + 0.4f, c.dir.x > 0f ? view.xMax - 1.2f : -(view.xMin + 1.2f));
                float floor = Level.FloorBelow(c.pos.x, c.prev.y + 0.02f);
                Vector2? end = null;
                if (c.pos.x * Mathf.Sign(c.dir.x) > wall && Mathf.Abs(c.dir.x) > 0.05f) end = new Vector2(Mathf.Sign(c.dir.x) * wall, c.pos.y);
                else if (c.pos.y > Mathf.Min(9.5f, view.yMax - 1f) || c.t > 2.5f) end = c.pos;
                else if (c.dir.y < 0f && c.pos.y < floor + 0.3f) end = new Vector2(c.pos.x, floor + 0.3f);
                if (end == null) continue;

                c.active = false;
                c.trail.emitting = c.flame.emitting = false;
                c.root.gameObject.SetActive(false);
                CometBlast(c, end.Value);
            }
        }

        void CometBlast(Fireball c, Vector2 at)
        {
            var game = Game.I;
            var fx = FxSystem.I;
            float radius = c.radius;
            if (c.local)
            {
                game.Waves.Blast(at, radius, c.blastDmg, Src.Ulti);
                foreach (var m in game.Waves.Monsters)
                    if (m.Alive && (m.Center - at).sqrMagnitude < (radius + m.Radius) * (radius + m.Radius)) m.Ignite(c.blastDmg * 0.1f, 3f);
                float ground = Level.FloorBelow(at.x, at.y + 0.2f);
                if (at.y - ground < 1.2f) Court.I?.FireZone(new Vector2(at.x, ground), radius * 0.75f, 2.6f);
            }

            Color fire = Palette.BlastOrange;
            fx.Flash(at, radius * 1.5f, fire, 0.3f, 2f);
            fx.Flash(at, radius * 0.35f, new Color(1f, 0.85f, 0.6f), 0.1f, 2.2f);
            fx.Spawn(FxLayer.Back, true, Art.CellGlow, at, Vector2.zero, 1f, radius * 2f, radius * 3.2f, fire.WithAlpha(0.3f), fire.WithAlpha(0f), 1.6f);
            fx.Ring(FxLayer.Front, at, 0.4f, radius * 1.35f, 0.8f, 0.03f, 0.55f, Color.white, fire.WithAlpha(0f), 2.8f);
            fx.Ring(FxLayer.Front, at, 0.2f, radius * 0.75f, 0.4f, 0.02f, 0.35f, Palette.Gold, Palette.Hurt.WithAlpha(0f), 2.4f);
            fx.Ring(FxLayer.Back, at, 0.5f, radius * 1.8f, 0.5f, 0.05f, 0.7f, new Color(1f, 0.6f, 0.3f, 0.3f), new Color(1f, 0.6f, 0.3f, 0f), 1.4f, false, false);
            for (int i = 0; i < 34; i++)
            {
                float ang = Random.Range(10f, 170f);
                Color col = Color.Lerp(Palette.Gold, fire, Random.value);
                fx.Streak(FxLayer.Front, at, MathUtil.Dir(ang) * Random.Range(7f, 18f), Random.Range(0.35f, 0.7f), Random.Range(0.1f, 0.16f), 0.045f,
                    Color.Lerp(col, Color.white, 0.35f), col.WithAlpha(0f), 2.8f, 2.5f, 9f);
            }
            fx.Sparks(at, Vector2.up, 220f, 30, 6f, 18f, fire, 2.8f, 0.07f, 0.5f, 12f);
            for (int i = 0; i < 14; i++)
            {
                float ang = Random.Range(20f, 160f);
                fx.Spawn(FxLayer.Front, false, Art.CellShard, at, MathUtil.Dir(ang) * Random.Range(5f, 12f), Random.Range(0.5f, 0.9f),
                    Random.Range(0.1f, 0.2f), 0.04f, Palette.StoneLight, Palette.Stone.WithAlpha(0f), 1f, 0.5f, 22f, Random.Range(0f, 360f), Random.Range(-700f, 700f));
            }
            for (int i = 0; i < 12; i++)
                fx.Spawn(FxLayer.Back, false, Art.CellPuff, at + Random.insideUnitCircle * radius * 0.4f, new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(1.5f, 3f)),
                    Random.Range(1.2f, 1.8f), 0.6f, 2.2f, new Color(0.14f, 0.1f, 0.12f, 0.5f), new Color(0.1f, 0.1f, 0.12f, 0f), 1f, 1f, -0.5f,
                    Random.Range(0f, 360f), Random.Range(-40f, 40f));
            fx.Dust(at, Vector2.right, 12, 5f, 0.8f, 0.45f);
            fx.Dust(at, Vector2.left, 12, 5f, 0.8f, 0.45f);
            fx.Sparkles(at + Vector2.up * 0.5f, radius * 0.5f, 16, Palette.Gold, 3f, 0.9f);

            game.Cam.AddTrauma(c.local ? 0.8f : 0.35f);
            game.Cam.Kick(new Vector2(0f, -0.5f));
            game.Cam.ZoomPunch(0.05f);
            game.Post.Impact(c.local ? 1f : 0.5f);
            if (c.local)
            {
                TimeFx.HitStop(0.1f, 0.03f);
                c.owner?.ReturnUltiBall(at + Vector2.up * 0.6f, new Vector2(-c.dir.x * 2.5f, 9f), false);
            }
        }

        // ================================================================== BRUNO: die goldene Kuppel

        public void Dome(Player owner, float life, float r, float power, bool local)
        {
            Shield d = null;
            foreach (var x in domes) if (!x.active) { d = x; break; }
            if (d == null)
            {
                d = new Shield { root = new GameObject("UltiDome").transform };
                d.root.SetParent(parent, false);
                d.floor = Art.MakeSprite("Floor", parent, Art.SoftGlow, -30, Art.SpriteGlowMat, Color.clear);
                d.shell = Art.MakeSprite("Shell", d.root, domeSprite, 128, Art.SpriteGlowMat, Color.clear);
                d.sheen = Art.MakeSprite("Sheen", d.root, domeSprite, 127, Art.SpriteGlowMat, Color.clear);
                d.line = Art.MakeSprite("Line", d.root, Art.SoftGlow, 129, Art.SpriteGlowMat, Color.clear);
                domes.Add(d);
            }
            // ein Spieler hat nur eine Kuppel: eine neue ersetzt die alte
            foreach (var x in domes) if (x.active && x.owner == owner) x.active = false;
            d.active = true;
            d.local = local;
            d.owner = owner;
            d.life = life;
            d.age = d.fxT = 0f;
            d.r = r;
            d.power = power;
            d.pushCd.Clear();
            d.root.gameObject.SetActive(true);
            d.floor.gameObject.SetActive(true);
        }

        void UpdateDomes(float dt)
        {
            var fx = FxSystem.I;
            Color gold = Palette.Gold;
            foreach (var d in domes)
            {
                if (!d.active) { if (d.root.gameObject.activeSelf) { d.root.gameObject.SetActive(false); d.floor.gameObject.SetActive(false); } continue; }
                d.age += dt;
                var owner = d.owner;
                if (owner == null || owner.Dead || d.age >= d.life) { d.active = false; DomeBurst(d); continue; }
                Vector2 c = new Vector2(owner.Pos.x, owner.Grounded ? owner.Pos.y : Level.FloorBelow(owner.Pos.x, owner.Pos.y + 0.05f));
                d.root.position = c;

                float grow = MathUtil.EaseOutBack(Mathf.Clamp01(d.age / 0.28f), 2.2f);
                float left = d.life - d.age;
                // kurz vor dem Ende flackert sie
                float warn = left < 0.7f ? 0.65f + 0.35f * Mathf.Sign(Mathf.Sin(d.age * 40f)) : 1f;
                float breathe = 0.9f + 0.1f * Mathf.Sin(d.age * 3.4f);
                float s = d.r * 2f * grow;
                d.shell.transform.localScale = new Vector3(s, s, 1f);
                d.shell.color = new Color(1.2f, 1f, 0.5f, 0.48f * breathe * warn);
                float sh = s * (1.03f + 0.02f * Mathf.Sin(d.age * 7f));
                d.sheen.transform.localScale = new Vector3(sh, sh, 1f);
                d.sheen.color = Color.white.WithAlpha((0.1f + 0.08f * Mathf.Sin(d.age * 11f)) * warn);
                d.line.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                d.line.transform.localScale = new Vector3(s * 1.05f, 0.22f, 1f);
                d.line.color = gold.WithAlpha(0.7f * warn);
                d.floor.transform.position = new Vector3(c.x, c.y - 0.05f, 0f);
                d.floor.transform.localScale = new Vector3(s * 1.3f, d.r * 0.5f, 1f);
                d.floor.color = gold.WithAlpha(0.28f * breathe * warn);

                // Lichtpunkte wandern über die Kuppel, Funken steigen innen auf
                d.fxT -= dt;
                if (d.fxT <= 0f)
                {
                    d.fxT = 0.05f;
                    float a = Random.Range(10f, 170f) * Mathf.Deg2Rad;
                    Vector2 on = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d.r * grow * 0.98f;
                    fx.Sparkles(on, 0.05f, 1, Color.Lerp(gold, Color.white, 0.5f), 2.6f, 0.4f);
                    if (Random.value < 0.6f) fx.Motes(c + new Vector2(Random.Range(-d.r, d.r) * 0.7f, 0.1f), Vector2.up * 1.4f, gold, 1, 0.1f);
                }

                if (!d.local) continue;

                // Geschosse prallen ab und fliegen auf die Monster zurück
                float power = d.power;
                EnemyProjectiles.I.Reflect(c, d.r * grow, (at, col) =>
                {
                    var target = Combat.NearestTo(at, 16f);
                    Vector2 dir = target != null ? (target.Center - at).normalized : (at - c).normalized;
                    EchoBalls.I.Fire(at, dir, 26f, 30f * power, Src.Ulti, gold, true);
                    fx.Flash(at, 1.4f, gold, 0.1f, 2.6f);
                    fx.Ring(FxLayer.Front, at, 0.1f, 1f, 0.14f, 0.01f, 0.24f, Color.white, gold.WithAlpha(0f), 2.4f);
                    fx.Sparks(at, dir, 50f, 6, 5f, 11f, gold, 2.4f, 0.04f, 0.2f);
                });

                // Monster werden hinausgedrückt (Bosse nicht), wer an der Wand klebt, bekommt etwas ab
                var monsters = Game.I.Waves.Monsters;
                for (int mi = 0; mi < monsters.Count; mi++)
                {
                    var m = monsters[mi];
                    if (!m.Alive) continue;
                    Vector2 off = m.Center - c;
                    float dist = off.magnitude;
                    float reach = d.r * grow + m.Radius * 0.5f;
                    if (dist > reach || off.y < -0.5f) continue;
                    Vector2 n = dist > 0.01f ? off / dist : Vector2.up;
                    if (m.Rank != Rank.Boss)
                    {
                        float outward = Vector2.Dot(m.Vel, n);
                        if (outward < 6f) m.Vel += n * (6f - outward);
                    }
                    if (!d.pushCd.TryGetValue(m.Id, out float next) || d.age >= next)
                    {
                        d.pushCd[m.Id] = d.age + 0.45f;
                        Combat.Hit(m, 10f * power, n + Vector2.up * 0.5f, 10f, Src.Ulti);
                        Vector2 on = c + n * d.r * grow;
                        fx.Ring(FxLayer.Front, on, 0.1f, 1.1f, 0.16f, 0.01f, 0.28f, Color.white.WithAlpha(0.9f), gold.WithAlpha(0f), 2.4f);
                        fx.Sparks(on, n, 70f, 6, 4f, 9f, gold, 2.4f, 0.04f, 0.22f);
                    }
                }
            }
        }

        void DomeBurst(Shield d)
        {
            d.root.gameObject.SetActive(false);
            d.floor.gameObject.SetActive(false);
            var fx = FxSystem.I;
            var game = Game.I;
            Vector2 c = d.root.position;
            Color gold = Palette.Gold;
            if (d.local) game.Waves.Blast(c + new Vector2(0f, d.r * 0.4f), d.r * 1.7f, 75f * d.power, Src.Ulti);
            fx.Flash(c + new Vector2(0f, d.r * 0.5f), d.r * 2.2f, gold, 0.3f, 1.8f);
            fx.Ring(FxLayer.Front, c + new Vector2(0f, 0.3f), d.r * 0.9f, d.r * 1.9f, 0.5f, 0.02f, 0.45f, Color.white, gold.WithAlpha(0f), 2.8f);
            fx.Ring(FxLayer.Front, c + new Vector2(0f, 0.3f), d.r * 0.5f, d.r * 1.4f, 0.3f, 0.01f, 0.35f, gold, Palette.BlastOrange.WithAlpha(0f), 2.4f);
            // die Kuppel zerspringt in goldene Scherben
            for (int i = 0; i < 36; i++)
            {
                float a = Random.Range(5f, 175f);
                Vector2 n = MathUtil.Dir(a);
                fx.Spawn(FxLayer.Front, true, Art.CellShard, c + n * d.r, n * Random.Range(5f, 12f) + Vector2.up * 2f, Random.Range(0.5f, 0.9f),
                    Random.Range(0.12f, 0.22f), 0.03f, Color.Lerp(gold, Color.white, 0.4f), gold.WithAlpha(0f), 2.4f, 1f, 14f, Random.Range(0f, 360f), Random.Range(-720f, 720f));
            }
            fx.Sparkles(c + new Vector2(0f, d.r * 0.5f), d.r, 18, gold, 3f, 0.8f);
            fx.Dust(c, Vector2.right, 8, 4f, 0.6f, 0.4f);
            fx.Dust(c, Vector2.left, 8, 4f, 0.6f, 0.4f);
            game.Cam.AddTrauma(d.local ? 0.45f : 0.2f);
            game.Post.Impact(d.local ? 0.6f : 0.3f);
            if (d.local)
            {
                TimeFx.HitStop(0.06f, 0.04f);
                game.Hud.Popup(c + new Vector2(0f, d.r + 0.8f), "BOLLWERK", gold, 34f, true);
            }
        }

        /// <summary>Brunos Stampfer: Staubwände, ein Lichtring am Boden, Risse.</summary>
        public static void StompFx(Vector2 at, float r)
        {
            var fx = FxSystem.I;
            Color gold = Palette.Gold;
            fx.Flash(at + new Vector2(0f, 0.4f), r * 0.9f, gold, 0.16f, 1.6f);
            fx.Ring(FxLayer.Front, at + new Vector2(0f, 0.1f), 0.3f, r * 1.6f, 0.3f, 0.02f, 0.4f, Color.white.WithAlpha(0.85f), gold.WithAlpha(0f), 2.2f);
            fx.Dust(at, Vector2.right, 12, 5f, 0.7f, 0.42f);
            fx.Dust(at, Vector2.left, 12, 5f, 0.7f, 0.42f);
            fx.Sparks(at, Vector2.up, 150f, 18, 5f, 12f, gold, 2.4f, 0.05f, 0.3f, 10f);
            for (int i = 0; i < 10; i++)
            {
                float ang = Random.Range(25f, 155f);
                fx.Spawn(FxLayer.Front, false, Art.CellShard, at, MathUtil.Dir(ang) * Random.Range(4f, 9f), Random.Range(0.4f, 0.7f),
                    Random.Range(0.08f, 0.15f), 0.04f, Palette.StoneLight, Palette.Stone.WithAlpha(0f), 1f, 0.5f, 22f, Random.Range(0f, 360f), Random.Range(-600f, 600f));
            }
            I?.AddCrater(at, r * 0.45f, 1.3f, gold);
        }

        // ================================================================== MIRA: die Zauberbälle

        public void StormOrbs(Player owner, int count, Vector2 at, float dmg, bool local)
        {
            for (int i = 0; i < count; i++)
            {
                Orb o = null;
                foreach (var x in orbs) if (!x.active) { o = x; break; }
                if (o == null)
                {
                    o = new Orb { root = new GameObject("UltiOrb").transform };
                    o.root.SetParent(parent, false);
                    o.glow = Art.MakeSprite("Glow", o.root, Art.SoftGlow, 128, Art.SpriteGlowMat, Color.clear);
                    o.spinNode = new GameObject("Spin").transform;
                    o.spinNode.SetParent(o.root, false);
                    o.pattern = Art.MakeSprite("Pattern", o.spinNode, Art.BallPattern, 129, Art.SpriteMat, Color.white);
                    o.core = Art.MakeSprite("Core", o.root, Art.SoftGlow, 130, Art.SpriteGlowMat, Color.clear);
                    o.trail = MakeTrail(o.root, "Trail", Art.TrailRainbowMat, 0.34f, 0.34f, 127, null);
                    var g = new Gradient();
                    g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
                    o.trail.colorGradient = g;
                    orbs.Add(o);
                }
                o.active = true;
                o.local = local;
                o.flying = false;
                o.owner = owner;
                o.index = i;
                o.count = count;
                o.pos = at;
                o.vel = Vector2.zero;
                o.target = null;
                o.t = o.flyT = o.fxT = 0f;
                o.dmg = dmg;
                o.color = Art.Rainbow(i / (float)count);
                o.root.gameObject.SetActive(true);
                o.root.position = at;
                o.trail.Clear();
                o.trail.emitting = true;
            }
        }

        Orb FindOrb(Player owner, int index)
        {
            foreach (var o in orbs) if (o.active && !o.flying && o.owner == owner && o.index == index) return o;
            foreach (var o in orbs) if (o.active && !o.flying && o.owner == owner) return o;
            return null;
        }

        /// <summary>Ein Zauberball löst sich aus dem Kreis und jagt auf sein Ziel. Gibt zurück, wo er startet.</summary>
        public Vector2 LaunchOrb(Player owner, int index, Monster target, Vector2 to)
        {
            var o = FindOrb(owner, index);
            if (o == null) return owner.Pos + new Vector2(0f, 1.2f);
            o.flying = true;
            o.flyT = 0f;
            o.target = target;
            o.to = to;
            // er behält den Schwung aus dem Kreis und biegt dann ein
            o.vel = o.vel * 0.6f + (to - o.pos).normalized * 4f;
            var fx = FxSystem.I;
            fx.Flash(o.pos, 1.6f, o.color, 0.12f, 2.6f);
            fx.Ring(FxLayer.Front, o.pos, 0.1f, 0.9f, 0.12f, 0.01f, 0.2f, Color.white, o.color.WithAlpha(0f), 2.4f);
            return o.pos;
        }

        /// <summary>Die Ulti brach ab: die kreisenden Bälle fliegen auseinander und zerplatzen.</summary>
        public void DropOrbs(Player owner)
        {
            foreach (var o in orbs)
            {
                if (!o.active || o.flying || o.owner != owner) continue;
                o.flying = true;
                o.flyT = 0f;
                o.target = null;
                Vector2 c = owner.Pos + new Vector2(0f, 1.2f);
                o.to = o.pos + (o.pos - c).normalized * 5f;
            }
        }

        void UpdateOrbs(float dt)
        {
            var fx = FxSystem.I;
            foreach (var o in orbs)
            {
                if (!o.active) continue;
                o.t += dt;
                Vector2 prev = o.pos;
                bool front = true;
                if (!o.flying)
                {
                    // im Kreis um Mira, flach wie eine Bahn um sie herum: hinten kleiner und hinter ihr
                    if (o.owner == null) { o.active = false; o.root.gameObject.SetActive(false); continue; }
                    Vector2 center = o.owner.Pos + new Vector2(0f, 1.2f);
                    float turn = 160f * o.t + 560f * Mathf.Max(0f, o.t - 0.3f) * MathUtil.Smooth01(o.t / 0.6f);
                    float a = (turn + o.index * 360f / o.count) * Mathf.Deg2Rad;
                    float rad = 1.55f + 0.12f * Mathf.Sin(o.t * 5f + o.index);
                    Vector2 want = center + new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad * 0.42f + 0.18f * Mathf.Sin(o.t * 3f + o.index * 1.3f));
                    float k = MathUtil.Smooth01(o.t / 0.3f);
                    o.pos = Vector2.Lerp(o.pos, want, 1f - Mathf.Exp(-(6f + 20f * k) * dt));
                    o.vel = (o.pos - prev) / Mathf.Max(dt, 1e-4f);
                    front = Mathf.Sin(a) < 0f;
                }
                else
                {
                    o.flyT += dt;
                    if (o.target != null && o.target.Alive) o.to = o.target.Center;
                    else if (o.target != null && o.local)
                    {
                        o.target = Combat.NearestTo(o.pos, 12f);
                    }
                    float speed = Mathf.Lerp(10f, 38f, MathUtil.Smooth01(o.flyT / 0.3f));
                    Vector2 toward = o.to - o.pos;
                    o.vel = MathUtil.Damp(o.vel, toward.normalized * speed, 10f, dt);
                    o.pos += o.vel * dt;
                    bool hitTarget = o.target != null && o.target.Alive && (o.target.Center - o.pos).sqrMagnitude < (o.target.Radius + 0.35f) * (o.target.Radius + 0.35f);
                    if (hitTarget || toward.sqrMagnitude < 0.2f || o.flyT > 1.5f) { OrbImpact(o); continue; }
                }

                o.root.position = o.pos;
                o.spin -= 900f * dt;
                o.spinNode.localRotation = Quaternion.Euler(0f, 0f, o.spin);
                float depth = front ? 1f : 0.8f;
                o.root.localScale = Vector3.one * depth * 0.8f;
                int baseOrder = front ? 128 : 92;
                o.glow.sortingOrder = baseOrder;
                o.pattern.sortingOrder = baseOrder + 1;
                o.core.sortingOrder = baseOrder + 2;
                o.trail.sortingOrder = baseOrder - 1;
                float pulse = 0.85f + 0.15f * Mathf.Sin(o.t * 20f + o.index);
                o.glow.color = o.color.WithAlpha((front ? 0.6f : 0.4f) * pulse);
                o.glow.transform.localScale = Vector3.one * 1.9f;
                o.pattern.color = Color.Lerp(o.color, Color.white, 0.65f).WithAlpha(front ? 1f : 0.75f);
                o.core.color = Color.white.WithAlpha(0.55f);
                o.core.transform.localScale = Vector3.one * 0.8f;
                o.fxT -= dt;
                if (o.fxT <= 0f)
                {
                    o.fxT = 0.05f;
                    fx.Spawn(FxLayer.Front, true, Art.CellSparkle, o.pos + Random.insideUnitCircle * 0.2f, Random.insideUnitCircle * 0.5f,
                        0.45f, 0.18f, 0f, o.color, o.color.WithAlpha(0f), 2.6f, 1f, 0.4f, Random.Range(0f, 90f), 120f);
                }
            }
        }

        void OrbImpact(Orb o)
        {
            o.active = false;
            o.trail.emitting = false;
            o.root.gameObject.SetActive(false);
            Vector2 at = o.pos;
            if (o.local)
            {
                var area = Game.I.Run.Stats.AreaMul;
                Vector2 dir = o.vel.sqrMagnitude > 0.01f ? o.vel.normalized : Vector2.up;
                if (o.target != null && o.target.Alive) Combat.Hit(o.target, o.dmg, dir + Vector2.up * 0.3f, 10f, Src.Ulti, big: true);
                Game.I.Waves.Blast(at, 1.6f * area, o.dmg * 0.5f, Src.Ulti);
                TimeFx.HitStop(0.04f, 0.05f);
            }
            OrbBurstFx(at, o.color, o.local);
        }

        static void OrbBurstFx(Vector2 at, Color c, bool local)
        {
            var fx = FxSystem.I;
            fx.Flash(at, 1.6f, c, 0.18f, 1.8f);
            fx.Ring(FxLayer.Front, at, 0.15f, 2f, 0.4f, 0.03f, 0.4f, Color.white, Color.white.WithAlpha(0f), 2f, true);
            for (int i = 0; i < 14; i++)
            {
                Color rc = Art.Rainbow(Random.value);
                fx.Streak(FxLayer.Front, at, MathUtil.Dir(Random.Range(0f, 360f)) * Random.Range(5f, 12f), Random.Range(0.25f, 0.45f), 0.07f, 0.04f,
                    Color.Lerp(rc, Color.white, 0.3f), rc.WithAlpha(0f), 2.8f, 3.5f, 6f);
            }
            fx.Sparkles(at, 0.8f, 10, Palette.Gold, 3f, 0.7f);
            Game.I.Cam.AddTrauma(local ? 0.16f : 0.06f);
        }

        // ================================================================== DRE: die Buzzer-Würfe und Fadenkreuze

        public void BuzzerLob(Player owner, int index, Vector2 from, Monster target, Vector2 to, float flight, float dmg, float radius, bool local)
        {
            Lob l = null;
            foreach (var x in lobs) if (!x.active) { l = x; break; }
            if (l == null)
            {
                l = new Lob { root = new GameObject("UltiLob").transform };
                l.root.SetParent(parent, false);
                l.glow = Art.MakeSprite("Glow", l.root, Art.SoftGlow, 126, Art.SpriteGlowMat, Color.clear);
                l.spinNode = new GameObject("Spin").transform;
                l.spinNode.SetParent(l.root, false);
                l.pattern = Art.MakeSprite("Pattern", l.spinNode, Art.HoopPattern, 127, Art.SpriteMat, Color.white);
                l.core = Art.MakeSprite("Core", l.root, Art.SoftGlow, 128, Art.SpriteGlowMat, Color.clear);
                l.trail = MakeTrail(l.root, "Trail", Art.TrailShotMat, 0.3f, Art.BallRadius * 2.2f, 125, lobGradient);
                lobs.Add(l);
            }
            l.active = true;
            l.local = local;
            l.owner = owner;
            l.index = index;
            l.from = l.pos = from;
            l.to = to;
            l.target = target;
            l.t = l.fxT = 0f;
            l.flight = Mathf.Max(0.3f, flight);
            l.h = Mathf.Max(3.4f, Mathf.Abs(to.x - from.x) * 0.32f) + Mathf.Max(0f, to.y - from.y) * 0.3f;
            l.dmg = dmg;
            l.radius = radius;
            l.clock = 3;
            l.root.gameObject.SetActive(true);
            l.root.position = from;
            l.trail.Clear();
            l.trail.emitting = true;
            if (local && index == 0) Game.I.Hud.ShotClock(3);
        }

        void UpdateLobs(float dt)
        {
            var fx = FxSystem.I;
            foreach (var l in lobs)
            {
                if (!l.active) continue;
                l.t += dt;
                float k = Mathf.Clamp01(l.t / l.flight);
                // der Wurf folgt seinem Gegner, wohin der auch läuft
                if (l.target != null && l.target.Alive) l.to = MathUtil.Damp(l.to, l.target.Center, 8f, dt);
                l.pos = Vector2.Lerp(l.from, l.to, k) + new Vector2(0f, l.h * 4f * k * (1f - k));
                l.root.position = l.pos;
                l.spin -= Mathf.Sign(l.to.x - l.from.x + 0.001f) * 720f * dt;
                l.spinNode.localRotation = Quaternion.Euler(0f, 0f, l.spin);
                l.glow.color = Palette.HoopFlame.WithAlpha(0.8f);
                l.glow.transform.localScale = Vector3.one * (1.5f + 0.15f * Mathf.Sin(l.t * 30f + l.index));
                l.core.color = Color.white.WithAlpha(0.45f);
                l.core.transform.localScale = Vector3.one * 0.6f;
                l.fxT -= dt;
                if (l.fxT <= 0f)
                {
                    l.fxT = 0.04f;
                    fx.Sparks(l.pos, Vector2.down, 60f, 1, 1.5f, 4f, Palette.HoopFlame, 2.4f, 0.035f, 0.3f, 4f);
                }
                if (l.local && l.index == 0)
                {
                    int c = k < 1f / 3f ? 3 : k < 2f / 3f ? 2 : 1;
                    if (c != l.clock) { l.clock = c; Game.I.Hud.ShotClock(c); }
                }
                if (k < 1f) continue;

                l.active = false;
                l.trail.emitting = false;
                l.root.gameObject.SetActive(false);
                LobImpact(l);
            }
        }

        void LobImpact(Lob l)
        {
            var game = Game.I;
            Vector2 at = l.to;
            KillReticle(l.index);
            if (l.local)
            {
                game.Waves.Blast(at, l.radius, l.dmg, Src.Ulti);
                game.Hud.Popup(at + new Vector2(0f, l.radius * 0.6f + 0.6f), "+3", Palette.Gold, 40f, true);
            }
            Court.BlastFx(at, l.radius, Palette.HoopFlame, l.local ? 1f : 0.6f);
            if (l.index != 0) return;
            // der Buzzer: alle fünf schlagen in diesem Moment ein
            var fx = FxSystem.I;
            fx.Flash(at + Vector2.up, 4f, Palette.Gold, 0.3f, 1.6f);
            game.Cam.AddTrauma(l.local ? 0.6f : 0.25f);
            game.Post.Impact(l.local ? 1f : 0.4f);
            if (!l.local) return;
            game.Hud.ShotClock(0);
            TimeFx.HitStop(0.1f, 0.03f);
            l.owner?.ReturnUltiBall(at + Vector2.up * 0.4f, new Vector2(Mathf.Sign(l.from.x - at.x) * 2.5f, 9f), false);
        }

        public void Reticles(List<Monster> targets, List<Vector2> spots)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Reticle r = null;
                foreach (var x in reticles) if (!x.active) { r = x; break; }
                if (r == null)
                {
                    r = new Reticle();
                    r.glow = Art.MakeSprite("ReticleGlow", parent, Art.SoftGlow, 139, Art.SpriteGlowMat, Color.clear);
                    r.ring = Art.MakeSprite("Reticle", parent, reticleSprite, 140, Art.SpriteGlowMat, Color.clear);
                    reticles.Add(r);
                }
                r.active = true;
                r.index = i;
                r.target = targets[i];
                r.at = r.target != null ? r.target.Center : spots[i];
                r.dup = 0;
                for (int j = 0; j < i; j++) if (targets[j] == targets[i]) r.dup++;
                r.t = -0.05f * i;   // eins nach dem anderen
                r.ring.gameObject.SetActive(true);
                r.glow.gameObject.SetActive(true);
                r.ring.color = r.glow.color = Color.clear;
            }
        }

        void KillReticle(int index)
        {
            foreach (var r in reticles)
            {
                if (!r.active || r.index != index) continue;
                r.active = false;
                r.ring.gameObject.SetActive(false);
                r.glow.gameObject.SetActive(false);
                FxSystem.I.Ring(FxLayer.Front, r.at, 0.3f, 1.4f, 0.12f, 0.01f, 0.25f, Color.white.WithAlpha(0.8f), Palette.HoopFlame.WithAlpha(0f), 2.2f);
            }
        }

        void UpdateReticles(float dt)
        {
            foreach (var r in reticles)
            {
                if (!r.active) continue;
                r.t += dt;
                if (r.t > 3f) { r.active = false; r.ring.gameObject.SetActive(false); r.glow.gameObject.SetActive(false); continue; }
                if (r.target != null && r.target.Alive) r.at = r.target.Center;
                float show = Mathf.Clamp01(r.t / 0.12f);
                // es fährt zusammen und rastet auf dem Gegner ein, dann pulsiert es
                float size = (r.target != null ? r.target.Radius * 2.6f + 0.7f : 1.3f) * (1f + 0.25f * r.dup);
                float lockK = MathUtil.EaseOutCubic(Mathf.Clamp01(r.t / 0.35f));
                float s = size * Mathf.Lerp(1.8f, 1f, lockK) * (1f + 0.05f * Mathf.Sin(r.t * 14f) * lockK);
                r.ring.transform.position = r.at;
                r.ring.transform.localScale = Vector3.one * s;
                r.ring.transform.localRotation = Quaternion.Euler(0f, 0f, r.t * 90f + r.index * 25f + 45f * (1f - lockK));
                Color c = Color.Lerp(Palette.HoopFlame, Palette.Gold, 0.5f + 0.5f * Mathf.Sin(r.t * 10f));
                r.ring.color = c.WithAlpha(0.95f * show);
                r.glow.transform.position = r.at;
                r.glow.transform.localScale = Vector3.one * s * 1.4f;
                r.glow.color = Palette.HoopFlame.WithAlpha(0.18f * show * lockK);
            }
        }

        // ================================================================== TITAN: Meteor-Marke und Krater

        public void MeteorMark(Vector2 at, float life, float r)
        {
            Mark m = null;
            foreach (var x in marks) if (!x.active) { m = x; break; }
            if (m == null)
            {
                m = new Mark();
                m.glow = Art.MakeSprite("MeteorMarkGlow", parent, Art.SoftGlow, -43, Art.SpriteGlowMat, Color.clear);
                m.shadow = Art.MakeSprite("MeteorShadow", parent, Art.Shadow, -42, Art.SpriteMat, Color.clear);
                m.ring = Art.MakeSprite("MeteorMarkRing", parent, Art.MarkRing, -41, Art.SpriteGlowMat, Color.clear);
                marks.Add(m);
            }
            m.active = true;
            m.at = at;
            m.t = m.fxT = 0f;
            m.life = life;
            m.r = r;
            m.glow.gameObject.SetActive(true);
            m.shadow.gameObject.SetActive(true);
            m.ring.gameObject.SetActive(true);
        }

        void UpdateMarks(float dt)
        {
            foreach (var m in marks)
            {
                if (!m.active) continue;
                m.t += dt;
                float k = Mathf.Clamp01(m.t / m.life);
                if (k >= 1f)
                {
                    m.active = false;
                    m.glow.gameObject.SetActive(false);
                    m.shadow.gameObject.SetActive(false);
                    m.ring.gameObject.SetActive(false);
                    continue;
                }
                Color amber = Palette.Amber;
                float pulse = 0.55f + 0.45f * Mathf.Sin(m.t * (8f + 30f * k));
                Vector3 p = new Vector3(m.at.x, m.at.y + 0.04f, 0f);
                // sein Schatten wächst, je näher er kommt; der Ring zieht sich zusammen
                float sh = Mathf.Lerp(0.5f, m.r * 0.45f, MathUtil.EaseInQuad(k));
                m.shadow.transform.position = p;
                m.shadow.transform.localScale = new Vector3(sh * 2.2f, sh, 1f);
                m.shadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.15f, 0.8f, k));
                float rr = Mathf.Lerp(m.r * 0.9f, m.r * 0.3f, MathUtil.EaseInQuad(k));
                m.ring.transform.position = p;
                m.ring.transform.localScale = new Vector3(rr * 2.15f, rr * 0.8f, 1f);
                m.ring.color = amber.WithAlpha((0.3f + 0.5f * k) * pulse);
                m.glow.transform.position = p;
                m.glow.transform.localScale = new Vector3(rr * 2.4f, rr * 0.9f, 1f);
                m.glow.color = amber.WithAlpha(0.05f + 0.16f * k * pulse);
                m.fxT -= dt;
                if (m.fxT <= 0f)
                {
                    m.fxT = Mathf.Lerp(0.12f, 0.03f, k);
                    FxSystem.I.Sparks(new Vector2(m.at.x + Random.Range(-rr, rr), m.at.y + 0.05f), Vector2.up, 26f, 1, 2f, 6f, amber, 2.4f, 0.04f, 0.3f, 5f);
                }
            }
        }

        /// <summary>Titans Einschlag: Lichtblitz, Staubwände, Gesteinsbrocken, Feuer und ein glühender Krater.</summary>
        public static void MeteorImpactFx(Vector2 at, float r)
        {
            var fx = FxSystem.I;
            Color fire = UltiDefs.Accent(UltiKind.Meteor);
            fx.Flash(at + new Vector2(0f, 0.6f), r * 0.75f, fire, 0.28f, 1.9f);
            fx.Flash(at + new Vector2(0f, 0.4f), r * 0.18f, new Color(1f, 0.85f, 0.6f), 0.1f, 2.2f);
            fx.Spawn(FxLayer.Back, true, Art.CellGlow, at + new Vector2(0f, 0.6f), Vector2.zero, 1.1f, r * 1.4f, r * 2.2f, fire.WithAlpha(0.28f), fire.WithAlpha(0f), 1.6f);
            fx.Ring(FxLayer.Front, at + new Vector2(0f, 0.2f), 0.3f, r, 0.7f, 0.04f, 0.5f, Color.white.WithAlpha(0.85f), Palette.Slam.WithAlpha(0f), 2.4f);
            fx.Ring(FxLayer.Front, at + new Vector2(0f, 0.2f), 0.2f, r * 0.55f, 0.35f, 0.02f, 0.35f, Palette.Gold, fire.WithAlpha(0f), 2.4f);
            fx.Dust(at, Vector2.right, 18, 6.5f, 0.9f, 0.45f);
            fx.Dust(at, Vector2.left, 18, 6.5f, 0.9f, 0.45f);
            for (int i = 0; i < 26; i++)
            {
                float ang = Random.Range(25f, 155f);
                Vector2 from = at + new Vector2(Random.Range(-r, r) * 0.25f, 0.05f);
                fx.Spawn(FxLayer.Front, false, Art.CellShard, from, MathUtil.Dir(ang) * Random.Range(6f, 15f), Random.Range(0.6f, 1.1f),
                    Random.Range(0.1f, 0.24f), 0.05f, Palette.StoneLight, Palette.Stone.WithAlpha(0f), 1f, 0.4f, 24f, Random.Range(0f, 360f), Random.Range(-700f, 700f));
            }
            fx.Sparks(at, Vector2.up, 160f, 30, 6f, 16f, fire, 2.8f, 0.07f, 0.45f, 12f);
            for (int i = 0; i < 20; i++)
            {
                float ang = Random.Range(40f, 140f);
                Color col = Color.Lerp(Palette.Gold, fire, Random.value);
                fx.Streak(FxLayer.Front, at + new Vector2(Random.Range(-0.6f, 0.6f), 0.1f), MathUtil.Dir(ang) * Random.Range(5f, 12f), Random.Range(0.4f, 0.7f),
                    Random.Range(0.1f, 0.16f), 0.05f, Color.Lerp(col, Color.white, 0.3f), col.WithAlpha(0f), 2.8f, 2f, 7f);
            }
            for (int i = 0; i < 10; i++)
                fx.Spawn(FxLayer.Back, false, Art.CellPuff, at + new Vector2(Random.Range(-r, r) * 0.4f, 0.3f), new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(1f, 2.5f)),
                    Random.Range(1.2f, 1.8f), 0.7f, 2.4f, new Color(0.16f, 0.14f, 0.16f, 0.45f), new Color(0.1f, 0.1f, 0.12f, 0f), 1f, 1f, -0.4f,
                    Random.Range(0f, 360f), Random.Range(-40f, 40f));
            fx.Sparkles(at + new Vector2(0f, 0.6f), r * 0.3f, 14, Palette.Gold, 3f, 0.8f);
            I?.AddCrater(at, r * 0.42f, 2.2f, fire);
        }

        void AddCrater(Vector2 at, float r, float life, Color c)
        {
            Crater cr = null;
            foreach (var x in craters) if (!x.active) { cr = x; break; }
            if (cr == null)
            {
                cr = new Crater();
                cr.glow = Art.MakeSprite("CraterGlow", parent, Art.SoftGlow, -31, Art.SpriteGlowMat, Color.clear);
                cr.cracks = Art.MakeSprite("Cracks", parent, crackSprite, -30, Art.SpriteGlowMat, Color.clear);
                craters.Add(cr);
            }
            cr.active = true;
            cr.at = at;
            cr.t = 0f;
            cr.life = life;
            cr.r = r;
            cr.glow.gameObject.SetActive(true);
            cr.cracks.gameObject.SetActive(true);
            cr.cracks.transform.position = new Vector3(at.x, at.y + 0.02f, 0f);
            cr.cracks.transform.localScale = new Vector3(r * 2.2f, r * 0.5f, 1f);
            cr.cracks.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-4f, 4f));
            cr.glow.transform.position = new Vector3(at.x, at.y, 0f);
            cr.glow.transform.localScale = new Vector3(r * 3f, r * 0.7f, 1f);
            cr.cracks.color = c;
            cr.glow.color = c;
        }

        void UpdateCraters(float dt)
        {
            foreach (var c in craters)
            {
                if (!c.active) continue;
                c.t += dt;
                float k = c.t / c.life;
                if (k >= 1f) { c.active = false; c.glow.gameObject.SetActive(false); c.cracks.gameObject.SetActive(false); continue; }
                // die Glut kühlt ab: erst hell und gelb, dann dunkelrot, dann weg
                float heat = 1f - MathUtil.Smooth01(k);
                Color hot = Color.Lerp(new Color(0.8f, 0.15f, 0.08f), new Color(1.3f, 0.85f, 0.45f), heat * heat);
                float flick = 0.9f + 0.1f * Mathf.Sin(c.t * 31f);
                c.cracks.color = hot.WithAlpha(0.95f * heat * flick);
                c.glow.color = hot.WithAlpha(0.35f * heat * flick);
            }
        }

        /// <summary>Novas Pose: ein magentafarbener Schlag geht durch alle, die gerade gefallen sind.</summary>
        public static void AnkleFinaleFx(Vector2 at)
        {
            var fx = FxSystem.I;
            Color c = Palette.Showboat, teal = new Color(0.18f, 0.84f, 0.78f);
            fx.Flash(at, 2.4f, c, 0.2f, 1.7f);
            fx.Ring(FxLayer.Front, at, 0.3f, 4.2f, 0.3f, 0.02f, 0.5f, Color.white.WithAlpha(0.85f), c.WithAlpha(0f), 2.2f);
            fx.Ring(FxLayer.Front, at, 0.2f, 2.6f, 0.2f, 0.01f, 0.4f, teal, teal.WithAlpha(0f), 2.4f);
            for (int i = 0; i < 18; i++)
            {
                Color col = i % 2 == 0 ? c : teal;
                fx.Streak(FxLayer.Front, at, MathUtil.Dir(i * 20f + Random.Range(-6f, 6f)) * Random.Range(6f, 11f), Random.Range(0.3f, 0.5f), 0.07f, 0.06f,
                    Color.Lerp(col, Color.white, 0.4f), col.WithAlpha(0f), 2.6f, 4f);
            }
            fx.Sparkles(at, 1.2f, 20, c, 2.8f, 0.8f);
            Game.I.Cam.AddTrauma(0.25f);
        }

        // ================================================================== Boxer

        /// <summary>KAIs K.O.-Schlag: ein weißer Blitz an der Faust, ein Kranz aus Schlagzacken, die Luft reißt in Schlagrichtung auf.</summary>
        public static void KnockoutFx(Vector2 at, Vector2 dir, float r)
        {
            var fx = FxSystem.I;
            Color c = UltiDefs.Accent(UltiKind.Knockout);
            fx.Flash(at, r * 1.1f, c, 0.2f, 1.9f);
            fx.Flash(at, r * 0.35f, Palette.PunchCore, 0.08f, 2.2f);
            fx.Ring(FxLayer.Front, at, 0.3f, r * 1.6f, 0.36f, 0.02f, 0.45f, Color.white.WithAlpha(0.75f), c.WithAlpha(0f), 2.2f);
            fx.Ring(FxLayer.Front, at, 0.2f, r * 0.9f, 0.3f, 0.01f, 0.3f, Palette.Gold, c.WithAlpha(0f), 2.4f);
            float ang = MathUtil.Angle(dir);
            for (int i = 0; i < 4; i++)
                fx.Spawn(FxLayer.Front, true, Art.CellSparkle, at, Vector2.zero, 0.3f, r * (1.4f - i * 0.2f), r * 0.2f, Palette.PunchCore, c.WithAlpha(0f), 2.4f,
                    0f, 0f, ang + i * 22.5f, 0f, false);
            for (int i = 0; i < 22; i++)
            {
                float a = ang + Random.Range(-35f, 35f);
                fx.Streak(FxLayer.Front, at, MathUtil.Dir(a) * Random.Range(12f, 28f), Random.Range(0.25f, 0.45f), 0.07f, 0.06f,
                    Color.white.WithAlpha(0.9f), c.WithAlpha(0f), 2.6f, 3f);
            }
            fx.Sparks(at, dir, 90f, 26, 8f, 22f, Palette.Gold, 2.8f, 0.06f, 0.36f, 8f);
            fx.Sparks(at, -dir, 120f, 10, 3f, 8f, c, 2.4f, 0.05f, 0.24f);
        }

        /// <summary>LUZ' Schlusspose: grüne und goldene Schwingen aus Licht, Funken wie Flügelstaub.</summary>
        public static void ButterflyFinaleFx(Vector2 at)
        {
            var fx = FxSystem.I;
            Color c = UltiDefs.Accent(UltiKind.Butterfly), gold = Palette.Upper;
            fx.Flash(at, 2.6f, c, 0.22f, 1.8f);
            fx.Ring(FxLayer.Front, at, 0.3f, 4.4f, 0.32f, 0.02f, 0.5f, Color.white.WithAlpha(0.85f), c.WithAlpha(0f), 2.2f);
            fx.Ring(FxLayer.Front, at, 0.2f, 2.6f, 0.2f, 0.01f, 0.4f, gold, gold.WithAlpha(0f), 2.4f);
            // zwei Flügelpaare: Bögen aus Funken links und rechts
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 9; i++)
                {
                    float a = (side > 0 ? 0f : 180f) + side * (Mathf.Lerp(-50f, 70f, i / 8f));
                    Color col = i % 2 == 0 ? c : gold;
                    fx.Streak(FxLayer.Front, at, MathUtil.Dir(a) * Random.Range(6f, 10f), Random.Range(0.35f, 0.55f), 0.07f, 0.06f,
                        Color.Lerp(col, Color.white, 0.4f), col.WithAlpha(0f), 2.6f, 4f);
                }
            fx.Sparkles(at, 1.4f, 22, c, 2.8f, 0.9f);
            fx.Sparkles(at, 1f, 10, gold, 2.8f, 0.8f);
            Game.I.Cam.AddTrauma(0.25f);
        }

        // ================================================================== Duo: die Ulti des Partners auf diesem Bildschirm

        public void Remote(UltiKind kind, int step, Vector2 a, Vector2 b, float f)
        {
            var rp = Partner;
            var game = Game.I;
            var fx = FxSystem.I;
            switch (kind)
            {
                case UltiKind.Volley when step == 1:
                    Comet(rp, a, b, 0f, 0f, f, false);
                    return;
                case UltiKind.Bulwark when step == 1:
                    if (rp != null) Dome(rp, Player.DomeLife, f, 0f, false);
                    StompFx(a, f);
                    return;
                case UltiKind.Storm when step == 1:
                    if (rp != null) StormOrbs(rp, Player.StormOrbs, a, 0f, false);
                    return;
                case UltiKind.Storm when step >= 2:
                {
                    if (rp == null) return;
                    var o = FindOrb(rp, step - 2);
                    if (o != null) { o.flying = true; o.flyT = 0f; o.target = null; o.to = b; o.vel = o.vel * 0.6f + (b - o.pos).normalized * 4f; }
                    return;
                }
                case UltiKind.Buzzer when step >= 1:
                    BuzzerLob(rp, step - 1, a, null, b, f, 0f, 2.1f, false);
                    return;
                case UltiKind.Meteor when step == 1:
                    MeteorMark(a, f, 6.5f);
                    return;
                case UltiKind.Meteor when step == 2:
                    for (int i = 0; i < 3; i++) Court.I?.Shockwave(a + new Vector2(0f, 0.15f), f * (1f + i * 0.3f), 0f, 0f, 0f, i * 0.12f, false);
                    MeteorImpactFx(a, f);
                    game.Cam.AddTrauma(0.35f);
                    return;
                case UltiKind.AnkleBreaker when step == 1:
                    AnkleFinaleFx(a);
                    return;
                case UltiKind.Knockout when step == 1:
                    KnockoutFx(a, b, f);
                    game.Cam.AddTrauma(0.4f);
                    return;
                case UltiKind.Quake when step >= 1:
                    Court.I?.Shockwave(a + new Vector2(0f, 0.15f), f, 0f, 0f, 0f, 0f, false);
                    StompFx(a, f * 0.45f);
                    game.Cam.AddTrauma(0.25f);
                    return;
                case UltiKind.Butterfly when step == 1:
                    ButterflyFinaleFx(a);
                    return;
            }
            if (step != 0) return;
            // der Kinomoment des Partners: ein Lichtkranz um ihn und sein Ulti-Name
            Color c = UltiDefs.Accent(kind);
            Vector2 at = (rp != null ? rp.Pos : a) + new Vector2(0f, 1.05f);
            fx.Spawn(FxLayer.Back, true, Art.CellGlow, at, Vector2.zero, 0.9f, 3f, 6f, c.WithAlpha(0.45f), c.WithAlpha(0f), 2.2f);
            for (int i = 0; i < 12; i++)
            {
                Vector2 d = MathUtil.Dir(i * 30f);
                fx.Streak(FxLayer.Back, at + d * 0.45f, d * Random.Range(6f, 10f), 0.45f, 0.08f, 0.08f, Color.Lerp(c, Color.white, 0.45f).WithAlpha(0.8f), c.WithAlpha(0f), 2.2f, 4f);
            }
            fx.Ring(FxLayer.Front, at, 0.3f, 3f, 0.2f, 0.01f, 0.45f, Color.white.WithAlpha(0.8f), c.WithAlpha(0f), 2.2f);
            game.Hud.ShowToast((Coop.S?.PartnerName ?? "").ToUpperInvariant() + "  ·  " + UltiDefs.Name(kind));
        }
    }
}
