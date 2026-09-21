using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace SoccerFight
{
    /// <summary>
    /// Everything the title screen draws on top of its scene, in the language of the in-game UI
    /// (the upgrade cards, the HUD rings): dark glass bodies with a lit top edge, hairline frames
    /// that take the accent colour, clean white glyph icons that get tinted, a crystal gem and a
    /// gold coin, the impact shapes for the kicked ball, and a tracked text style with a soft shadow.
    /// The logo and the scene are separate (LogoArt, MenuScenery) because they generate on worker
    /// threads while the game boots.
    /// </summary>
    public static class MenuArt
    {
        // shots and cursor
        public static Sprite Bracket, Spark, Shock, Burst;
        // widgets
        public static Sprite Vignette, Body, Edge, Frame, Gloss, CardBody, Round, RoundEdge, RoundFrame, Badge, Beam, Sparkle, Shine;
        // icons (white glyphs, tinted per use) and the two currencies in colour
        public static Sprite IconShop, IconTrophy, IconFriends, IconGear, IconInfo, IconEvents, IconCoin, IconGem, IconPower;
        public static Sprite IconBack, IconSwap, IconCheck, IconLock, IconPlay, IconStriker, IconDefender, IconSkiller, IconPlus, IconStar;
        // text
        public static TMP_FontAsset FontHeavy;
        public static Material TextHeavy, TextHeavySoft, TextPlate;

        /// <summary>Near-black night blue for shadows and text on light plates.</summary>
        public static readonly Color Ink = new Color(0.02f, 0.05f, 0.08f, 1f);
        /// <summary>Dark glass of every panel and button (the same family as the upgrade cards).</summary>
        public static readonly Color Glass = new Color(0.05f, 0.09f, 0.14f, 0.94f);
        /// <summary>The menu's own accent: the cyan of the shots and crystals.</summary>
        public static readonly Color Accent = new Color(0.36f, 0.92f, 1f, 1f);

        static bool built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { built = false; pending = null; drawing = null; }

        public static void Build()
        {
            if (built) return;
            UiArt.Build();
            Begin();
            built = true;
            try { drawing.Wait(); }
            catch (System.AggregateException e) { foreach (var ex in e.InnerExceptions) Debug.LogException(ex); }
            foreach (var p in pending)
            {
                if (p.Data == null) continue;
                var c = p.Canvas;
                var tex = SdfCanvas.CreateTexture(p.Name, c.Width, c.Height, p.Data, false, true, TextureWrapMode.Clamp);
                var sprite = Sprite.Create(tex, new Rect(0, 0, c.Width, c.Height), new Vector2(0.5f, 0.5f), c.Ppu * 100f, 0, SpriteMeshType.FullRect, p.Border);
                sprite.name = p.Name;
                p.SetSprite(sprite);
            }
            pending = null;
            drawing = null;
            BuildFont();
            BuildTimer.Mark("menu art");
        }

        // The canvases are drawn on a worker thread (Begin); Build uploads them on the main thread.
        sealed class Pending
        {
            public string Name;
            public SdfCanvas Canvas;
            public Color32[] Data;
            public Vector4 Border;
            public System.Action<Sprite> SetSprite;
        }

        static List<Pending> pending;
        static Task drawing;

        static void Ui(SdfCanvas c, string name, System.Action<Sprite> set, float border = 0f)
            => pending.Add(new Pending { Name = name, Canvas = c, SetSprite = set, Border = border > 0f ? new Vector4(border, border, border, border) : default });

        /// <summary>Starts drawing everything on a worker thread (called early while the game boots).</summary>
        public static void Begin()
        {
            if (built || drawing != null) return;
            pending = new List<Pending>();
            drawing = Par.Run(() =>
            {
                const float D = 2f;
                BuildShotShapes(D);
                BuildWidgets(D);
                BuildIcons(D);
                foreach (var p in pending) p.Data = p.Canvas.Encode(false, false, false);
            });
        }

        static Color Mul(Color c, float f) { c.r *= f; c.g *= f; c.b *= f; return c; }
        static float S01(float v) => MathUtil.Smooth01(v);

        // ------------------------------------------------------------------ the kicked ball

        static void BuildShotShapes(float D)
        {
            var br = new SdfCanvas(new Rect(-24, -24, 48, 48), D);
            br.Fill(p => Sdf.Union(Sdf.Box(p, new Vector2(-9f, 19f), new Vector2(11f, 1.6f), 1.2f),
                                   Sdf.Box(p, new Vector2(-19f, 9f), new Vector2(1.6f, 11f), 1.2f)), Color.white);
            Ui(br, "MenuBracket", x => Bracket = x);

            var sp = new SdfCanvas(new Rect(-20, -6, 40, 12), D * 2f);
            sp.Fill(p => Sdf.Tapered(p, new Vector2(-18f, 0f), 0.6f, new Vector2(16f, 0f), 2.6f), Color.white);
            Ui(sp, "MenuSpark", x => Spark = x);

            var sh = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            sh.Field(p =>
            {
                float d = (p.magnitude / 64f - 0.86f) / 0.06f;
                return new Color(1f, 1f, 1f, Mathf.Exp(-d * d));
            });
            Ui(sh, "MenuShock", x => Shock = x);

            var bu = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            bu.Field(p =>
            {
                float r = p.magnitude / 64f;
                float core = Mathf.Exp(-r * r * 18f);
                float spikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(Mathf.Atan2(p.y, p.x) * 2f)), 9f) * Mathf.Exp(-r * 3.8f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(core + spikes * 0.8f));
            });
            Ui(bu, "MenuBurst", x => Burst = x);
        }

        // ------------------------------------------------------------------ widgets

        const float R = 14f;   // corner radius of buttons and panels (UI px)

        static void BuildWidgets(float D)
        {
            var vg = new SdfCanvas(new Rect(-64, -64, 128, 128), 1f);
            vg.Field(p => new Color(0f, 0f, 0f, Mathf.Pow(Mathf.Clamp01(p.magnitude / 64f), 2.4f)));
            Ui(vg, "MenuVignette", x => Vignette = x);

            // body: lit from the top like the upgrade cards, a hairline of light along the top edge
            var body = new SdfCanvas(new Rect(-32, -32, 64, 64), D);
            SdfCanvas.SdfFn box = p => Sdf.Box(p, Vector2.zero, new Vector2(31.5f, 31.5f), R);
            body.Fill(box, p => Mul(Color.white, Mathf.Lerp(0.74f, 1f, S01((p.y + 30f) / 60f))));
            body.Paint(p => Sdf.Box(p, new Vector2(0f, 29.6f), new Vector2(24f, 0.9f), 0.9f), new Color(1f, 1f, 1f, 1f), 0.8f);
            Ui(body, "MenuBody", x => Body = x, (R + 4f) * D);

            var edge = new SdfCanvas(new Rect(-32, -32, 64, 64), D);
            edge.Fill(p => Sdf.Box(p, Vector2.zero, new Vector2(31.5f, 31.5f), R + 1.5f), Color.white);
            Ui(edge, "MenuEdge", x => Edge = x, (R + 4f) * D);

            // hairline frame (hollow), so a translucent body never shows a rim through itself
            var frame = new SdfCanvas(new Rect(-32, -32, 64, 64), D);
            frame.Fill(p => Mathf.Abs(Sdf.Box(p, Vector2.zero, new Vector2(30.5f, 30.5f), R)) - 1.1f, Color.white);
            Ui(frame, "MenuFrame", x => Frame = x, (R + 4f) * D);

            // soft sheen over the top of a body
            var gloss = new SdfCanvas(new Rect(-32, -16, 64, 32), D);
            gloss.Field(p => new Color(1f, 1f, 1f, S01((p.y + 14f) / 30f) * 0.8f));
            gloss.Clip(p => Sdf.Box(p, Vector2.zero, new Vector2(31f, 15f), R - 2f));
            Ui(gloss, "MenuGloss", x => Gloss = x, (R + 2f) * D);

            var card = new SdfCanvas(new Rect(-32, -32, 64, 64), D);
            card.Fill(p => Sdf.Box(p, Vector2.zero, new Vector2(31.5f, 31.5f), 12f), p => Mul(Color.white, Mathf.Lerp(0.82f, 1f, S01((p.y + 30f) / 60f))));
            Ui(card, "MenuCard", x => CardBody = x, 14f * D);

            var round = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            round.Fill(p => Sdf.Circle(p, Vector2.zero, 62f), p => Mul(Color.white, Mathf.Lerp(0.78f, 1f, S01((p.y + 50f) / 100f))));
            Ui(round, "MenuRound", x => Round = x);
            var roundEdge = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            roundEdge.Fill(p => Sdf.Circle(p, Vector2.zero, 63f), Color.white);
            Ui(roundEdge, "MenuRoundEdge", x => RoundEdge = x);
            var roundFrame = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            roundFrame.Fill(p => Mathf.Abs(Sdf.Circle(p, Vector2.zero, 60f)) - 2.4f, Color.white);
            Ui(roundFrame, "MenuRoundFrame", x => RoundFrame = x);

            // class / rank badge: a cut diamond (the HUD's wave markers are diamonds too)
            var bd = new SdfCanvas(new Rect(-36, -36, 72, 72), D * 2f);
            SdfCanvas.SdfFn diamond = p => Sdf.Box(p, Vector2.zero, new Vector2(22f, 22f), 5f, 45f);
            bd.Fill(diamond, p => Mul(Color.white, Mathf.Lerp(0.72f, 1f, S01((p.y + 26f) / 52f))));
            bd.Paint(p => Mathf.Abs(Sdf.Box(p, Vector2.zero, new Vector2(19f, 19f), 3.5f, 45f)) - 0.9f, new Color(1f, 1f, 1f, 0.9f), 0.5f);
            Ui(bd, "MenuBadge", x => Badge = x);

            // soft vertical light beam (bottom = source)
            var bm = new SdfCanvas(new Rect(-32, 0, 64, 256), 0.5f);
            bm.Field(p =>
            {
                float across = Mathf.Exp(-(p.x * p.x) / (2f * 13f * 13f));
                float along = Mathf.Pow(1f - p.y / 256f, 1.6f) * S01(p.y / 12f);
                return new Color(1f, 1f, 1f, across * along);
            });
            Ui(bm, "MenuBeam", x => Beam = x);

            var sk = new SdfCanvas(new Rect(-32, -32, 64, 64), D);
            sk.Field(p =>
            {
                Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y));
                float star = Mathf.Exp(-q.x * q.y * 0.1f) * Mathf.Exp(-(q.x + q.y) * 0.07f);
                float core = Mathf.Exp(-p.sqrMagnitude * 0.025f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(star * 1.1f + core));
            });
            Ui(sk, "MenuSparkle", x => Sparkle = x);

            // diagonal glint that sweeps over the play button (masked by the button itself)
            var sn = new SdfCanvas(new Rect(-24, -64, 48, 128), D * 0.5f);
            sn.Field(p =>
            {
                float d = Mathf.Abs(p.x - p.y * 0.35f);
                return new Color(1f, 1f, 1f, Mathf.Exp(-d * d / 60f) * 0.9f + Mathf.Exp(-(d - 14f) * (d - 14f) / 8f) * 0.4f);
            });
            Ui(sn, "MenuShine", x => Shine = x);
        }

        // ------------------------------------------------------------------ icons

        static SdfCanvas IconCanvas(float D) => new SdfCanvas(new Rect(-64, -64, 128, 128), D);

        /// <summary>White glyph with a hint of top light; tinted by the image that shows it.</summary>
        static void Glyph(SdfCanvas c, SdfCanvas.SdfFn f) => c.Fill(f, p => Mul(Color.white, Mathf.Lerp(0.8f, 1f, S01((p.y + 50f) / 100f))));

        static SdfCanvas.ColorFn Lit(Color c, float y0, float y1, float dark = 0.72f)
            => p => Mul(c, Mathf.Lerp(dark, 1f, S01((p.y - y0) / (y1 - y0))));

        static void BuildIcons(float D)
        {
            // shop: a kit bag with a ball emblem cut into it
            var sh = IconCanvas(D);
            Glyph(sh, p => Mathf.Min(Sdf.Box(p, new Vector2(0f, -12f), new Vector2(42f, 34f), 8f), Sdf.Intersect(Sdf.Ring(p, new Vector2(0f, 24f), 20f, 7f), -(p.y - 22f))));
            sh.Erase(p => Mathf.Abs(Sdf.Circle(p, new Vector2(0f, -12f), 17f)) - 3f);
            sh.Erase(p => Sdf.Circle(p, new Vector2(0f, -12f), 6f));
            Ui(sh, "IconShop", x => IconShop = x);

            // trophy with a star cut into the cup
            var tr = IconCanvas(D);
            SdfCanvas.SdfFn cup = p => Sdf.SmoothUnion(Sdf.Intersect(Sdf.Ellipse(p, new Vector2(0f, 30f), new Vector2(31f, 44f)), p.y - 30f), Sdf.Box(p, new Vector2(0f, 38f), new Vector2(33f, 7f), 3f), 3f);
            SdfCanvas.SdfFn handles = p => Mathf.Min(Sdf.Ring(p, new Vector2(-34f, 22f), 13f, 5f), Sdf.Ring(p, new Vector2(34f, 22f), 13f, 5f));
            SdfCanvas.SdfFn stem = p => Sdf.Union(Sdf.Box(p, new Vector2(0f, -22f), new Vector2(6f, 14f), 2f), Sdf.Box(p, new Vector2(0f, -44f), new Vector2(26f, 8f), 3f));
            Glyph(tr, p => Mathf.Min(Mathf.Min(cup(p), handles(p)), stem(p)));
            tr.Erase(p => Star5(p, new Vector2(0f, 16f), 15f, 6.5f));
            Ui(tr, "IconTrophy", x => IconTrophy = x);

            // two teammates, the back one separated by a gap
            var fr = IconCanvas(D);
            SdfCanvas.SdfFn back = p => Mathf.Min(Sdf.Circle(p, new Vector2(22f, 22f), 16f), Sdf.Intersect(Sdf.Ellipse(p, new Vector2(24f, -24f), new Vector2(30f, 32f)), p.y + 30f));
            SdfCanvas.SdfFn front = p => Mathf.Min(Sdf.Circle(p, new Vector2(-12f, 12f), 19f), Sdf.Intersect(Sdf.Ellipse(p, new Vector2(-12f, -40f), new Vector2(37f, 37f)), p.y + 50f));
            Glyph(fr, back);
            fr.Erase(p => front(p) - 5f);
            Glyph(fr, front);
            Ui(fr, "IconFriends", x => IconFriends = x);

            // gear
            var gr = IconCanvas(D);
            Glyph(gr, p =>
            {
                float d = Sdf.Circle(p, Vector2.zero, 36f);
                for (int i = 0; i < 8; i++) d = Mathf.Min(d, Sdf.Box(p, MathUtil.Dir(i * 45f) * 40f, new Vector2(9f, 8f), 2.5f, i * 45f));
                return Sdf.Subtract(d, Sdf.Circle(p, Vector2.zero, 15f));
            });
            Ui(gr, "IconGear", x => IconGear = x);

            // info: ring with an i
            var inf = IconCanvas(D);
            Glyph(inf, p => Mathf.Min(Mathf.Abs(Sdf.Circle(p, Vector2.zero, 44f)) - 5f,
                Mathf.Min(Sdf.Circle(p, new Vector2(0f, 20f), 7f), Sdf.Box(p, new Vector2(0f, -10f), new Vector2(6f, 19f), 2f))));
            Ui(inf, "IconInfo", x => IconInfo = x);

            // events: a calendar page with a star
            var ev = IconCanvas(D);
            SdfCanvas.SdfFn page = p => Sdf.Box(p, new Vector2(0f, -6f), new Vector2(44f, 42f), 8f);
            Glyph(ev, p => Mathf.Min(Mathf.Abs(page(p)) - 3.5f, Mathf.Min(Sdf.Intersect(page(p), -(p.y - 18f)),
                Mathf.Min(Sdf.Box(p, new Vector2(-22f, 38f), new Vector2(4.5f, 11f), 4.5f), Sdf.Box(p, new Vector2(22f, 38f), new Vector2(4.5f, 11f), 4.5f)))));
            ev.Erase(p => Mathf.Min(Sdf.Box(p, new Vector2(-22f, 30f), new Vector2(8f, 3f)), Sdf.Box(p, new Vector2(22f, 30f), new Vector2(8f, 3f))));
            Glyph(ev, p => Star5(p, new Vector2(0f, -14f), 21f, 9f));
            Ui(ev, "IconEvents", x => IconEvents = x);

            // coin: lantern gold, struck with a ball
            var cn = IconCanvas(D);
            Color gold = new Color(1f, 0.8f, 0.36f);
            cn.Fill(p => Sdf.Circle(p, Vector2.zero, 46f), Lit(Mul(gold, 0.72f), -46f, 46f, 0.7f));
            cn.Fill(p => Sdf.Circle(p, Vector2.zero, 39f), Lit(gold, -40f, 40f, 0.78f));
            cn.Fill(p => Mathf.Abs(Sdf.Circle(p, Vector2.zero, 30f)) - 1.6f, Mul(gold, 0.7f));
            cn.Fill(p => Mathf.Abs(Sdf.Circle(p, Vector2.zero, 16f)) - 3f, Mul(gold, 0.66f));
            cn.Fill(p => Sdf.Circle(p, Vector2.zero, 5.5f), Mul(gold, 0.66f));
            cn.Paint(p => Sdf.Ellipse(p, new Vector2(-15f, 22f), new Vector2(13f, 7f)), new Color(1f, 1f, 0.92f, 0.55f), 4f);
            Ui(cn, "IconCoin", x => IconCoin = x);

            // gem: a cluster of the same crystals that grow in the ruins
            var gm = IconCanvas(D);
            void Prism(Vector2 root, float ang, float len, float w)
            {
                Vector2 dir = MathUtil.Dir(ang), side = new Vector2(-dir.y, dir.x);
                Vector2 tip = root + dir * len, shoulder = root + dir * (len - w * 1.7f);
                SdfCanvas.SdfFn f = q => Mathf.Min(Sdf.Capsule(q, root, shoulder, w), Sdf.Triangle(q, shoulder + side * w, shoulder - side * w, tip));
                gm.Fill(f, q =>
                {
                    float u = Vector2.Dot(q - root, side) / w;
                    float along = Mathf.Clamp01(Vector2.Dot(q - root, dir) / len);
                    Color col = u < -0.3f ? new Color(0.2f, 0.55f, 0.72f) : u < 0.25f ? new Color(0.7f, 0.98f, 1f) : new Color(0.38f, 0.82f, 0.95f);
                    return Color.Lerp(col, Color.white, S01((along - 0.7f) / 0.3f) * 0.5f);
                });
            }
            Prism(new Vector2(-18f, -40f), 118f, 58f, 11f);
            Prism(new Vector2(18f, -40f), 62f, 54f, 10f);
            Prism(new Vector2(0f, -44f), 90f, 88f, 15f);
            Ui(gm, "IconGem", x => IconGem = x);

            // power (quit)
            var pw = IconCanvas(D);
            Glyph(pw, p => Mathf.Min(Sdf.Subtract(Sdf.Ring(p, new Vector2(0f, -4f), 34f, 10f), Sdf.Triangle(p, new Vector2(0f, -4f), new Vector2(-38f, 60f), new Vector2(38f, 60f))),
                Sdf.Box(p, new Vector2(0f, 22f), new Vector2(5f, 24f), 5f)));
            Ui(pw, "IconPower", x => IconPower = x);

            var bk = IconCanvas(D);
            Glyph(bk, p => Mathf.Min(Sdf.Capsule(p, new Vector2(-34f, 0f), new Vector2(38f, 0f), 7f),
                Mathf.Min(Sdf.Capsule(p, new Vector2(-36f, 0f), new Vector2(-8f, 28f), 7f), Sdf.Capsule(p, new Vector2(-36f, 0f), new Vector2(-8f, -28f), 7f))));
            Ui(bk, "IconBack", x => IconBack = x);

            var sw = IconCanvas(D);
            Glyph(sw, p =>
            {
                float a = Sdf.Intersect(Sdf.Ring(p, Vector2.zero, 32f, 8f), p.y - 6f);
                float b = Sdf.Intersect(Sdf.Ring(p, Vector2.zero, 32f, 8f), -(p.y + 6f));
                float h1 = Sdf.Triangle(p, new Vector2(18f, 8f), new Vector2(48f, 8f), new Vector2(33f, -14f));
                float h2 = Sdf.Triangle(p, new Vector2(-18f, -8f), new Vector2(-48f, -8f), new Vector2(-33f, 14f));
                return Mathf.Min(Mathf.Min(a, b), Mathf.Min(h1, h2));
            });
            Ui(sw, "IconSwap", x => IconSwap = x);

            var ck = IconCanvas(D);
            Glyph(ck, p => Sdf.Union(Sdf.Capsule(p, new Vector2(-30f, 2f), new Vector2(-8f, -22f), 8f), Sdf.Capsule(p, new Vector2(-8f, -22f), new Vector2(34f, 26f), 8f)));
            Ui(ck, "IconCheck", x => IconCheck = x);

            var lk = IconCanvas(D);
            Glyph(lk, p => Mathf.Min(Sdf.Intersect(Sdf.Ring(p, new Vector2(0f, 12f), 22f, 7f), -(p.y - 8f)),
                Mathf.Min(Mathf.Min(Sdf.Box(p, new Vector2(-22f, 6f), new Vector2(3.5f, 8f)), Sdf.Box(p, new Vector2(22f, 6f), new Vector2(3.5f, 8f))),
                    Sdf.Box(p, new Vector2(0f, -20f), new Vector2(33f, 25f), 6f))));
            lk.Erase(p => Sdf.Union(Sdf.Circle(p, new Vector2(0f, -15f), 6f), Sdf.Box(p, new Vector2(0f, -25f), new Vector2(2.6f, 8f), 2f)));
            Ui(lk, "IconLock", x => IconLock = x);

            var pl = IconCanvas(D);
            Glyph(pl, p => Sdf.Triangle(p, new Vector2(-24f, 36f), new Vector2(-24f, -36f), new Vector2(38f, 0f)) - 4f);
            Ui(pl, "IconPlay", x => IconPlay = x);

            var plus = IconCanvas(D);
            Glyph(plus, p => Sdf.Union(Sdf.Box(p, Vector2.zero, new Vector2(32f, 8f), 4f), Sdf.Box(p, Vector2.zero, new Vector2(8f, 32f), 4f)));
            Ui(plus, "IconPlus", x => IconPlus = x);

            var sr = IconCanvas(D);
            Glyph(sr, p => Star5(p, new Vector2(0f, -3f), 50f, 21f));
            Ui(sr, "IconStar", x => IconStar = x);

            BuildClassIcons(D);
        }

        /// <summary>Five-pointed star.</summary>
        static float Star5(Vector2 p, Vector2 c, float outer, float inner)
        {
            Vector2 q = p - c;
            float a = Mathf.Atan2(q.x, q.y);
            float seg = Mathf.PI * 2f / 5f;
            float an = Mathf.Repeat(a + seg * 0.5f, seg) - seg * 0.5f;
            q = new Vector2(Mathf.Abs(Mathf.Sin(an)), Mathf.Cos(an)) * q.magnitude;
            Vector2 tip = new Vector2(0f, outer);
            Vector2 valley = new Vector2(Mathf.Sin(seg * 0.5f), Mathf.Cos(seg * 0.5f)) * inner;
            Vector2 e = valley - tip;
            Vector2 w = q - tip;
            float h = Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
            float dist = (w - e * h).magnitude;
            float side = e.x * w.y - e.y * w.x;
            return side > 0f ? dist : -dist;
        }

        static void BuildClassIcons(float D)
        {
            // striker: a ball with speed lines
            var sk = IconCanvas(D);
            Vector2 bc = new Vector2(14f, -4f);
            Glyph(sk, p => Mathf.Min(Sdf.Circle(p, bc, 30f), Mathf.Min(Sdf.Capsule(p, new Vector2(-50f, 14f), new Vector2(-24f, 14f), 4.5f),
                Mathf.Min(Sdf.Capsule(p, new Vector2(-54f, -4f), new Vector2(-22f, -4f), 4.5f), Sdf.Capsule(p, new Vector2(-46f, -22f), new Vector2(-24f, -22f), 4.5f)))));
            sk.Erase(p => Sdf.Circle(p, bc, 9f));
            for (int k = 0; k < 5; k++)
            {
                Vector2 pc = bc + MathUtil.Dir(90f + k * 72f) * 24f;
                sk.Erase(p => Mathf.Max(Sdf.Circle(p, pc, 7.5f), Sdf.Circle(p, bc, 27f)));
            }
            Ui(sk, "IconStriker", x => IconStriker = x);

            // defender: shield with a band
            var df = IconCanvas(D);
            SdfCanvas.SdfFn sh = p =>
            {
                float top = Sdf.Box(p, new Vector2(0f, 16f), new Vector2(40f, 30f), 8f);
                float tip = Sdf.Triangle(p, new Vector2(-40f, 8f), new Vector2(40f, 8f), new Vector2(0f, -52f));
                return Sdf.SmoothUnion(top, tip, 6f);
            };
            Glyph(df, sh);
            df.Erase(p => Mathf.Abs(sh(p) + 7f) - 2.2f);
            df.Erase(p => Sdf.Intersect(sh(p) + 14f, Mathf.Min(Mathf.Abs(p.x) - 6f, Mathf.Abs(p.y - 14f) - 6f)), 0.55f);
            Ui(df, "IconDefender", x => IconDefender = x);

            // skiller: a big sparkle with two small ones
            var sl = IconCanvas(D);
            Glyph(sl, p => Mathf.Min(Sdf.Star4(p, new Vector2(-6f, -6f), 48f, 0.55f),
                Mathf.Min(Sdf.Star4(p, new Vector2(34f, 34f), 17f, 0.55f), Sdf.Star4(p, new Vector2(38f, -34f), 12f, 0.55f))));
            Ui(sl, "IconSkiller", x => IconSkiller = x);
        }

        public static Sprite ClassIcon(CharacterClass c) => c == CharacterClass.Defender ? IconDefender : c == CharacterClass.Skiller ? IconSkiller : IconStriker;

        // ------------------------------------------------------------------ text

        /// <summary>
        /// A second SDF asset of the same typeface with a wide padding, so the soft shadow under the
        /// letters fits into the distance field.
        /// </summary>
        static void BuildFont()
        {
            var font = Resources.Load<Font>("Fonts/Inter-SemiBold");
            FontHeavy = font != null ? TMP_FontAsset.CreateFontAsset(font, 90, 22, GlyphRenderMode.SDFAA, 1024, 1024) : UiArt.FontBold;
            if (FontHeavy == null || FontHeavy.material == null) return;
            FontHeavy.name = "Inter Menu SDF";

            // headings and button labels: a hairline of night blue and a soft shadow underneath
            TextHeavy = new Material(FontHeavy.material) { name = "SF Menu Heavy" };
            TextHeavy.EnableKeyword("OUTLINE_ON");
            TextHeavy.SetColor("_OutlineColor", Ink);
            TextHeavy.SetFloat("_FaceDilate", 0.06f);
            TextHeavy.SetFloat("_OutlineWidth", 0.08f);
            TextHeavy.SetFloat("_OutlineSoftness", 0f);
            TextHeavy.EnableKeyword("UNDERLAY_ON");
            TextHeavy.SetColor("_UnderlayColor", new Color(0f, 0.02f, 0.04f, 0.65f));
            TextHeavy.SetFloat("_UnderlayOffsetX", 0f);
            TextHeavy.SetFloat("_UnderlayOffsetY", -0.55f);
            TextHeavy.SetFloat("_UnderlayDilate", 0.25f);
            TextHeavy.SetFloat("_UnderlaySoftness", 0.5f);

            // small print: only the soft shadow
            TextHeavySoft = new Material(FontHeavy.material) { name = "SF Menu Soft" };
            TextHeavySoft.SetFloat("_FaceDilate", 0.02f);
            TextHeavySoft.EnableKeyword("UNDERLAY_ON");
            TextHeavySoft.SetColor("_UnderlayColor", new Color(0f, 0.02f, 0.04f, 0.55f));
            TextHeavySoft.SetFloat("_UnderlayOffsetY", -0.4f);
            TextHeavySoft.SetFloat("_UnderlayDilate", 0.15f);
            TextHeavySoft.SetFloat("_UnderlaySoftness", 0.45f);

            // plain dark text printed onto light plates (the gold play button)
            TextPlate = new Material(FontHeavy.material) { name = "SF Menu Plate" };
            TextPlate.SetFloat("_FaceDilate", 0.08f);
        }

        /// <summary>Menu label: semibold, tracked, soft shadow.</summary>
        public static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box,
            TextAlignmentOptions align = TextAlignmentOptions.Center, float spacing = 4f, Material mat = null)
        {
            var t = UiKit.Label(name, parent, text, size, color, align, pos, box, true, spacing);
            if (FontHeavy != null) t.font = FontHeavy;
            var m = mat != null ? mat : TextHeavy;
            if (m != null) t.fontSharedMaterial = m;
            return t;
        }
    }
}
