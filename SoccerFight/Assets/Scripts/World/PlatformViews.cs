using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Die Plattformen einer Stage aus ihrem Design-Bogen (<see cref="StageKit"/>): stehende Stücke (Sockel,
    /// Gestelle, Stümpfe) reichen bis auf den Rasen, schwebende Inseln tragen Leuchtkristalle, hängende Bretter
    /// und Balken hängen an Seilen oder Ketten, die bis über den Bildrand reichen. Die Bilder werden nur
    /// gleichmäßig skaliert (Größe und Höhe legt der Generator in <see cref="Level"/> passend fest). Unter
    /// manchen schwebenden Stücken hängt eine Laterne, ein Banner oder ein Stern der Stage. Jede Plattform hat
    /// ihre eigene Gruppe, damit bewegliche alles mitnehmen. Wird pro Stage neu aufgebaut.
    /// </summary>
    public sealed class PlatformViews
    {
        sealed class View
        {
            public Level.Platform P;
            public Transform Root;
        }
        struct Blink { public SpriteRenderer sr; public Color color; public float baseA, phase, speed; }
        struct Speck { public Transform root; public Vector2 local; public Color color; }
        struct Swing { public Transform t; public float phase, amp; }

        readonly List<View> views = new List<View>();
        readonly List<Blink> blinks = new List<Blink>();
        readonly List<Speck> specks = new List<Speck>();
        readonly List<Swing> swings = new List<Swing>();
        Transform group;
        StageKit kit;
        System.Random rng = new System.Random(77);
        float speckTimer;

        float R() => (float)rng.NextDouble();
        float Range(float a, float b) => a + (b - a) * R();

        public void Build(Transform parent)
        {
            DesignArt.Load();
            group = new GameObject("Platforms").transform;
            group.SetParent(parent, false);
        }

        /// <summary>Replace the shown platforms (the layout was generated from this kit).</summary>
        public void Show(PlatformLook[] looks, StageKit stageKit)
        {
            for (int i = group.childCount - 1; i >= 0; i--) Object.Destroy(group.GetChild(i).gameObject);
            views.Clear(); blinks.Clear(); specks.Clear(); swings.Clear();
            kit = stageKit;
            rng = new System.Random(looks.Length * 131 + (looks.Length > 0 ? looks[0].P.Seed : 0));
            int n = 0;
            foreach (var look in looks) if (look.P.Owner == null) BuildOne(look.P, n++);
            Game.I?.Grade?.Adopt(group);
        }

        void BuildOne(Level.Platform p, int index)
        {
            if (kit == null || p.Piece < 0 || p.Piece >= kit.Platforms.Count) return;
            var pc = kit.Platforms[p.Piece];
            var v = new View { P = p };
            v.Root = new GameObject(pc.Name + " Platform").transform;
            v.Root.SetParent(group, false);
            views.Add(v);

            float s = p.Scale, cx = (p.BaseX0 + p.BaseX1) * 0.5f, y = p.BaseY;
            float fx = p.Flip ? -1f : 1f;
            // stands draw behind floats; neighbours never share an order (no flicker where they overlap)
            int order = (pc.Role == StageKit.Role.Stand ? -86 : -80) - (index % 3);
            var body = Art.MakeSprite(pc.Name, v.Root, pc.Sprite, order, DesignArt.SpriteMat);
            body.transform.localPosition = new Vector3(cx, y, 0f);
            body.transform.localScale = new Vector3(fx * s, s, 1f);

            // ropes or chains up out of the picture
            if (pc.Hangs)
            {
                var ropeKit = StageKit.Common.Get(pc.Rope);
                if (ropeKit != null)
                {
                    foreach (float ax in pc.Anchors)
                    {
                        var rope = Art.MakeSprite("Rope", v.Root, ropeKit.Sprite, order - 1, DesignArt.SpriteMat);
                        rope.drawMode = SpriteDrawMode.Tiled;
                        rope.size = new Vector2(ropeKit.Width, 16f / s);
                        rope.transform.localPosition = new Vector3(cx + fx * ax * s, y - 0.12f * s, 0f);
                        rope.transform.localScale = new Vector3(s, s, 1f);
                    }
                }
            }

            // light spots in the picture: a soft glow that breathes, sparks drifting off
            foreach (var l in pc.Lights)
            {
                Vector2 at = new Vector2(cx + fx * l.Pos.x * s, y + l.Pos.y * s);
                AddBlink(v.Root, at, (l.Radius * 5f + 0.5f) * s, l.Color, 0.22f, order + 1, Art.SpriteAddMat);
                AddBlink(v.Root, at, (l.Radius * 1.8f + 0.2f) * s, Color.Lerp(l.Color, Color.white, 0.4f), 0.28f, order + 2, Art.SpriteGlowMat);
                specks.Add(new Speck { root = v.Root, local = at, color = l.Color });
            }

            // sometimes a lantern, banner or star hangs under a higher floating piece (never down into the pitch)
            if (pc.Role == StageKit.Role.Float && !pc.Hangs && pc.Lights.Length == 0 && p.BaseY > 3.2f && R() < 0.5f)
            {
                var hangs = kit.PropsTagged("hang");
                if (hangs.Count > 0)
                {
                    var h = hangs[rng.Next(hangs.Count)];
                    float side = R() < 0.5f ? -1f : 1f;
                    float hx = cx + side * pc.WalkWidth * s * Range(0.22f, 0.36f);
                    float hs = s * Range(0.4f, 0.5f);
                    var sr = Art.MakeSprite(h.Name, v.Root, h.Sprite, order - 2, DesignArt.SpriteMat);
                    var pivot = new GameObject("Hook").transform;
                    pivot.SetParent(v.Root, false);
                    pivot.localPosition = new Vector3(hx, y - 0.18f * s, 0f);
                    sr.transform.SetParent(pivot, false);
                    sr.transform.localScale = new Vector3(hs, hs, 1f);
                    swings.Add(new Swing { t = pivot, phase = R() * 10f, amp = Range(2f, 4f) });
                    foreach (var l in h.Lights)
                    {
                        var g = Art.MakeSprite("Light", pivot, Art.SoftGlow, order + 1, Art.SpriteAddMat, l.Color.WithAlpha(0.25f));
                        g.transform.localPosition = l.Pos * hs;
                        g.transform.localScale = Vector3.one * (l.Radius * 6f + 0.6f) * hs;
                        blinks.Add(new Blink { sr = g, color = l.Color, baseA = 0.25f, phase = R() * 20f, speed = Range(2f, 3.5f) });
                    }
                }
            }

            Place(v);
        }

        void AddBlink(Transform parent, Vector2 pos, float size, Color color, float alpha, int order, Material mat)
        {
            var sr = Art.MakeSprite("Light", parent, Art.SoftGlow, order, mat, color.WithAlpha(alpha));
            sr.transform.localPosition = pos;
            sr.transform.localScale = Vector3.one * size;
            blinks.Add(new Blink { sr = sr, color = color, baseA = alpha, phase = R() * 20f, speed = Range(0.8f, 1.6f) });
        }

        void Place(View v)
        {
            Vector2 off = v.P.Offset;
            v.Root.localPosition = new Vector3(off.x, off.y, 0f);
        }

        public void Update(float dt, float time, float wind)
        {
            foreach (var v in views) Place(v);

            for (int i = 0; i < blinks.Count; i++)
            {
                var b = blinks[i];
                float f = 0.6f + 0.4f * Mathf.PerlinNoise(time * b.speed, b.phase);
                b.sr.color = b.color.WithAlpha(b.baseA * f);
            }
            // hanging lanterns and banners sway in the wind
            foreach (var s in swings)
                s.t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.3f + s.phase) * s.amp + wind * 3f);

            // light sparks falling off crystals, embers, lamps
            var fx = FxSystem.I;
            speckTimer -= dt;
            while (speckTimer <= 0f && fx != null && specks.Count > 0)
            {
                speckTimer += 0.3f;
                var s = specks[Random.Range(0, specks.Count)];
                Vector2 at = (Vector2)s.root.position + s.local + Random.insideUnitCircle * 0.12f;
                Color cc = s.color.WithAlpha(0.8f);
                fx.Spawn(FxLayer.Back, true, Art.CellDot, at, new Vector2((Random.value - 0.5f) * 0.2f, -0.15f - Random.value * 0.25f),
                    Random.Range(1.4f, 2.4f), Random.Range(0.035f, 0.06f), 0.01f, cc, cc, 2.2f, 0.4f, 0f, 0f, 0f, false, true);
            }
        }

        /// <summary>Dust and sparkles along every platform (a new layout settling in).</summary>
        public void Flourish()
        {
            var fx = FxSystem.I;
            if (fx == null) return;
            Color c = kit != null ? Color.Lerp(kit.Haze, Color.white, 0.6f) : Color.white;
            foreach (var v in views)
            {
                var p = v.P;
                for (float x = p.X0 + 0.3f; x < p.X1; x += 0.9f)
                    fx.Dust(new Vector2(x, p.Y), new Vector2(Random.value - 0.5f, 0.3f), 2, 1.2f, 0.4f, 0.3f);
                fx.Sparkles(new Vector2(p.Center, p.Y + 0.2f), p.Width * 0.4f, 6, c, 2.4f, 0.8f);
            }
        }
    }
}
