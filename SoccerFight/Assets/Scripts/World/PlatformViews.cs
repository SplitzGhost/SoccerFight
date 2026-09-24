using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Die Plattformen im neuen Design: schwebende Grasinseln aus Quadersteinen mit Ranken und
    /// hängendem Kristall, Sockel auf Säulen für die Stufen am Boden. Die Grafik wird passend zur
    /// begehbaren Breite skaliert, die Graskante liegt genau auf der Standhöhe. Jede Plattform hat
    /// ihre eigene Gruppe, damit bewegliche alles mitnehmen. Wird pro Stage neu aufgebaut.
    /// </summary>
    public sealed class PlatformViews
    {
        sealed class View
        {
            public Level.Platform P;
            public Transform Root;
            public float Phase;
        }
        struct Blink { public SpriteRenderer sr; public Color color; public float baseA, phase, speed; }
        struct Speck { public Transform root; public Vector2 local; }

        static readonly Color CrystalColor = new Color(0.4f, 0.8f, 1f, 1f);

        // [Sprite, Anteil der Breite, an dem der Kristall hängt (NaN = keiner), Höhe des Kristalls unter der Kante in Sprite-Höhen]
        static readonly (string name, float crystalX, float crystalY)[] Small = { ("plat_small", 0.5f, 0.78f), ("plat_flowers", 0.5f, 0.8f) };
        static readonly (string name, float crystalX, float crystalY)[] Medium = { ("plat_ferns", 0.5f, 0.72f), ("plat_crystal", 0.5f, 0.78f), ("plat_banner", 0.37f, 0.62f) };
        static readonly (string name, float crystalX, float crystalY)[] Wide = { ("plat_wide", float.NaN, 0f), ("plat_big", float.NaN, 0f) };

        readonly List<View> views = new List<View>();
        readonly List<Blink> blinks = new List<Blink>();
        readonly List<Speck> specks = new List<Speck>();
        Transform group;
        Material grassMat;
        System.Random rng = new System.Random(77);
        float speckTimer;

        float R() => (float)rng.NextDouble();
        float Range(float a, float b) => a + (b - a) * R();
        T Pick<T>(T[] arr) => arr[rng.Next(arr.Length)];

        public void Build(Transform parent)
        {
            DesignArt.Load();
            group = new GameObject("Platforms").transform;
            group.SetParent(parent, false);
            grassMat = DesignArt.PlantMaterial("SF Design Platform Grass", 1f);
        }

        /// <summary>Replace the shown platforms.</summary>
        public void Show(PlatformLook[] looks)
        {
            for (int i = group.childCount - 1; i >= 0; i--) Object.Destroy(group.GetChild(i).gameObject);
            views.Clear(); blinks.Clear(); specks.Clear();
            rng = new System.Random(looks.Length * 131 + (looks.Length > 0 ? looks[0].P.Seed : 0));
            foreach (var look in looks) BuildOne(look.P);
            Game.I?.Grade?.Adopt(group);
        }

        void BuildOne(Level.Platform p)
        {
            var v = new View { P = p, Phase = R() * 10f };
            v.Root = new GameObject(p.Kind + " Platform").transform;
            v.Root.SetParent(group, false);
            views.Add(v);

            float w = p.BaseX1 - p.BaseX0, cx = (p.BaseX0 + p.BaseX1) * 0.5f, y = p.BaseY;
            var pick = new System.Random(p.Seed);
            bool pedestal = p.Kind == Level.Style.Capital || p.Kind == Level.Style.Mushroom;

            if (pedestal)
            {
                // Sockel: Grasplatte auf einer Säule, die bis zum Rasen reicht
                var slab = DesignArt.Platform(w < 1.9f ? "ledge_block" : w < 3.4f ? "ledge_pair" : "ledge_long");
                float s = w / slab.Walk, sy = s <= 1f ? s : 1f + (s - 1f) * 0.45f;
                var body = Art.MakeSprite("Slab", v.Root, slab.Sprite, -82, DesignArt.SpriteMat);
                body.transform.localPosition = new Vector3(cx, y, 0f);
                body.transform.localScale = new Vector3(s, sy, 1f);
                var col = DesignArt.Get(pick.Next(2) == 0 ? "column_a" : "column_b");
                float colH = col.bounds.size.y, want = y - 0.3f;
                float cs = Mathf.Clamp(want / colH, 0.55f, 1.3f);
                var support = Art.MakeSprite("Column", v.Root, col, -84, DesignArt.SpriteMat);
                support.transform.localPosition = new Vector3(cx, 0.05f, 0f);
                support.transform.localScale = new Vector3(cs * 1.05f, want / colH, 1f);
            }
            else
            {
                var set = w < 2.4f ? Small : w < 3.5f ? Medium : Wide;
                var choice = set[pick.Next(set.Length)];
                if (choice.name == "plat_banner" && y < 3.2f) choice = Medium[0];   // das Banner hinge sonst bis in die Mauer
                if (w >= 4f) choice = Wide[1];   // die große Insel in hoher Auflösung; die kleinen würden zu klobig
                var ps = DesignArt.Platform(choice.name);
                float s = w / ps.Walk;
                bool flip = choice.name != "plat_banner" && pick.Next(2) == 0;
                var body = Art.MakeSprite(choice.name, v.Root, ps.Sprite, -82, DesignArt.SpriteMat);
                body.transform.localPosition = new Vector3(cx, y, 0f);
                // stark vergrößert wird die Insel nur wenig dicker, sonst hängt sie bis auf den Rasen
                float sy = s <= 1f ? s : 1f + (s - 1f) * 0.45f;
                body.transform.localScale = new Vector3(flip ? -s : s, sy, 1f);

                // hängender Kristall: weicher Schein, pulsiert, verliert Lichtfunken
                if (!float.IsNaN(choice.crystalX))
                {
                    Bounds b = ps.Sprite.bounds;
                    float lx = (b.min.x + b.size.x * choice.crystalX) * s;
                    Vector2 at = new Vector2(cx + (flip ? -lx : lx), y + (b.max.y - b.size.y * choice.crystalY) * sy);
                    AddBlink(v.Root, at, 1.1f * s, CrystalColor, 0.3f, -81);
                    AddBlink(v.Root, at, 0.35f * s, new Color(0.7f, 0.95f, 1f), 0.35f, -80);
                    specks.Add(new Speck { root = v.Root, local = at });
                }
            }

            // ein paar Halme auf der Kante, die sich im Wind und um den Spieler biegen
            var grass = new FoliageLayer();
            var kinds = DesignArt.Plants("grass_b", "grass_d", "grass_e", "grass_f");
            for (float x = p.BaseX0 + 0.15f; x < p.BaseX1 - 0.15f; x += Range(0.35f, 0.8f))
                grass.Add(Pick(kinds), new Vector2(x, y + Range(0.02f, 0.06f)), Range(0.26f, 0.4f), Color.white, 1f, 1f, R() > 0.5f);
            if (grass.Count > 0) grass.Build(v.Root, "Grass", -79, grassMat);
            Place(v);
        }

        void AddBlink(Transform parent, Vector2 pos, float size, Color color, float alpha, int order)
        {
            var sr = Art.MakeSprite("Light", parent, Art.SoftGlow, order, Art.SpriteGlowMat, color.WithAlpha(alpha));
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

            // Lichtfunken, die von den Kristallen abfallen
            var fx = FxSystem.I;
            speckTimer -= dt;
            while (speckTimer <= 0f && fx != null && specks.Count > 0)
            {
                speckTimer += 0.3f;
                var s = specks[Random.Range(0, specks.Count)];
                Vector2 at = (Vector2)s.root.position + s.local + Random.insideUnitCircle * 0.12f;
                Color cc = CrystalColor.WithAlpha(0.8f);
                fx.Spawn(FxLayer.Back, true, Art.CellDot, at, new Vector2((Random.value - 0.5f) * 0.2f, -0.15f - Random.value * 0.25f),
                    Random.Range(1.4f, 2.4f), Random.Range(0.035f, 0.06f), 0.01f, cc, cc, 2.2f, 0.4f, 0f, 0f, 0f, false, true);
            }
        }

        /// <summary>Dust and sparkles along every platform (a new layout settling in).</summary>
        public void Flourish()
        {
            var fx = FxSystem.I;
            if (fx == null) return;
            foreach (var v in views)
            {
                var p = v.P;
                for (float x = p.X0 + 0.3f; x < p.X1; x += 0.9f)
                    fx.Dust(new Vector2(x, p.Y), new Vector2(Random.value - 0.5f, 0.3f), 2, 1.2f, 0.4f, 0.3f);
                fx.Sparkles(new Vector2(p.Center, p.Y + 0.2f), p.Width * 0.4f, 6, CrystalColor, 2.4f, 0.8f);
            }
        }
    }
}
