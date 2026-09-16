using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace SoccerFight
{
    /// <summary>
    /// Everything the title screen draws that the HUD doesn't: chunky toy-like buttons (a lit body,
    /// a dark keyline and a darker lip that sells the depth), outlined icons, the pedestal, stickers,
    /// ribbons and tile patterns, the impact shapes for the kicked ball, and a heavy cartoon text
    /// style (a thick-padded font asset with outline and hard drop shadow).
    /// The logo and the landscape are separate (LogoArt, MenuScenery) because they are generated on
    /// worker threads while the game boots.
    /// </summary>
    public static class MenuArt
    {
        // shots and cursor
        public static Sprite Bracket, Spark, Shock, Burst;
        // chunky widgets
        public static Sprite Vignette, Body, Edge, Gloss, CardBody, Round, RoundEdge, Sticker, Ribbon, Pedestal, Beam, Sparkle, Petal, Badge, Shine;
        // icons (outlined, full colour)
        public static Sprite IconShop, IconTrophy, IconFriends, IconGear, IconInfo, IconEvents, IconCoin, IconGem, IconPower;
        public static Sprite IconBack, IconSwap, IconCheck, IconLock, IconPlay, IconStriker, IconDefender, IconSkiller, IconPlus, IconStar;
        // tileable patterns (white on transparent, tinted by the image)
        public static Texture2D BallPattern, Stripes;
        // text
        public static TMP_FontAsset FontHeavy;
        public static Material TextHeavy, TextHeavySoft, TextPlate;

        /// <summary>Near-black indigo used for every keyline, so all widgets read as one set.</summary>
        public static readonly Color Ink = new Color(0.08f, 0.05f, 0.18f, 1f);

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
                var tex = SdfCanvas.CreateTexture(p.Name, c.Width, c.Height, p.Data, false, true, p.Wrap);
                p.SetTexture?.Invoke(tex);
                if (p.SetSprite == null) continue;
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
            public TextureWrapMode Wrap;
            public System.Action<Sprite> SetSprite;
            public System.Action<Texture2D> SetTexture;
        }

        static List<Pending> pending;
        static Task drawing;

        static void Ui(SdfCanvas c, string name, System.Action<Sprite> set, float border = 0f)
            => Later(c, name, set, null, border > 0f ? new Vector4(border, border, border, border) : default, TextureWrapMode.Clamp);

        static void Later(SdfCanvas c, string name, System.Action<Sprite> sprite, System.Action<Texture2D> texture, Vector4 border, TextureWrapMode wrap)
            => pending.Add(new Pending { Name = name, Canvas = c, Border = border, Wrap = wrap, SetSprite = sprite, SetTexture = texture });

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
                BuildPatterns();
                foreach (var p in pending) p.Data = p.Canvas.Encode(false, false, false);
            });
        }

        static Color Mul(Color c, float f) { c.r *= f; c.g *= f; c.b *= f; return c; }

        // ------------------------------------------------------------------ the kicked ball

        static void BuildShotShapes(float D)
        {
            var br = new SdfCanvas(new Rect(-24, -24, 48, 48), D);
            br.Fill(p => Sdf.Union(Sdf.Box(p, new Vector2(-9f, 19f), new Vector2(11f, 2f), 1.4f),
                                   Sdf.Box(p, new Vector2(-19f, 9f), new Vector2(2f, 11f), 1.4f)), Color.white);
            Ui(br, "MenuBracket", x => Bracket = x);

            var sp = new SdfCanvas(new Rect(-20, -6, 40, 12), D * 2f);
            sp.Fill(p => Sdf.Tapered(p, new Vector2(-18f, 0f), 0.7f, new Vector2(16f, 0f), 3.4f), Color.white);
            Ui(sp, "MenuSpark", x => Spark = x);

            var sh = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            sh.Field(p =>
            {
                float d = (p.magnitude / 64f - 0.84f) / 0.075f;
                return new Color(1f, 1f, 1f, Mathf.Exp(-d * d));
            });
            Ui(sh, "MenuShock", x => Shock = x);

            var bu = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            bu.Field(p =>
            {
                float r = p.magnitude / 64f;
                float core = Mathf.Exp(-r * r * 16f);
                float spikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(Mathf.Atan2(p.y, p.x) * 2f)), 7f) * Mathf.Exp(-r * 3.4f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(core + spikes * 0.95f));
            });
            Ui(bu, "MenuBurst", x => Burst = x);
        }

        // ------------------------------------------------------------------ widgets

        static void BuildWidgets(float D)
        {
            var vg = new SdfCanvas(new Rect(-64, -64, 128, 128), 1f);
            vg.Field(p => new Color(0f, 0f, 0f, Mathf.Pow(Mathf.Clamp01(p.magnitude / 64f), 2.4f)));
            Ui(vg, "MenuVignette", x => Vignette = x);

            // button body: white, lit from the top, with a glossy upper half — tinted per button
            var body = new SdfCanvas(new Rect(-32, -32, 64, 64), D);
            SdfCanvas.SdfFn box = p => Sdf.Box(p, Vector2.zero, new Vector2(31.5f, 31.5f), 18f);
            body.Fill(box, p => Mul(Color.white, Mathf.Lerp(0.8f, 1f, MathUtil.Smooth01((p.y + 30f) / 56f))));
            // inner bottom shade and a crisp top highlight line
            body.Paint(p => Sdf.Box(p, new Vector2(0f, -27f), new Vector2(29f, 5f), 5f), new Color(0.62f, 0.62f, 0.66f, 0.55f), 3f);
            body.Paint(p => Sdf.Box(p, new Vector2(0f, 26.5f), new Vector2(24f, 1.6f), 1.6f), new Color(1f, 1f, 1f, 1f), 0.5f);
            Ui(body, "MenuBody", x => Body = x, 22f * D);

            var edge = new SdfCanvas(new Rect(-32, -32, 64, 64), D);
            edge.Fill(p => Sdf.Box(p, Vector2.zero, new Vector2(31.5f, 31.5f), 22f), Color.white);
            Ui(edge, "MenuEdge", x => Edge = x, 24f * D);

            // glossy band laid over the upper half of a body
            var gloss = new SdfCanvas(new Rect(-32, -16, 64, 32), D);
            gloss.Field(p => new Color(1f, 1f, 1f, MathUtil.Smooth01((p.y + 12f) / 22f) * 0.9f));
            gloss.Clip(p => Sdf.Box(p, new Vector2(0f, 2f), new Vector2(30f, 13f), 13f));
            Ui(gloss, "MenuGloss", x => Gloss = x, 14f * D);

            // card: a squarer body for the character cards and page panels
            var card = new SdfCanvas(new Rect(-32, -32, 64, 64), D);
            card.Fill(p => Sdf.Box(p, Vector2.zero, new Vector2(31.5f, 31.5f), 12f), p => Mul(Color.white, Mathf.Lerp(0.9f, 1f, MathUtil.Smooth01((p.y + 30f) / 60f))));
            Ui(card, "MenuCard", x => CardBody = x, 14f * D);

            var round = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            round.Fill(p => Sdf.Circle(p, Vector2.zero, 62f), p => Mul(Color.white, Mathf.Lerp(0.78f, 1f, MathUtil.Smooth01((p.y + 50f) / 100f))));
            round.Paint(p => Sdf.Ellipse(p, new Vector2(0f, 30f), new Vector2(40f, 22f)), new Color(1f, 1f, 1f, 0.35f), 8f);
            Ui(round, "MenuRound", x => Round = x);
            var roundEdge = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            roundEdge.Fill(p => Sdf.Circle(p, Vector2.zero, 63f), Color.white);
            Ui(roundEdge, "MenuRoundEdge", x => RoundEdge = x);

            // eight-point sticker burst ("NEU", "BALD")
            var st = new SdfCanvas(new Rect(-40, -40, 80, 80), D);
            st.Fill(p =>
            {
                float a = Mathf.Atan2(p.y, p.x);
                float r = 33f + 5.5f * Mathf.Cos(a * 10f);
                return p.magnitude - r;
            }, Color.white);
            Ui(st, "MenuSticker", x => Sticker = x);

            // ribbon with notched ends; the notches sit inside the slice border so they never stretch
            var rb = new SdfCanvas(new Rect(-64, -20, 128, 40), D);
            rb.Fill(p =>
            {
                float d = Sdf.Box(p, Vector2.zero, new Vector2(63f, 18f), 2f);
                float notchL = Sdf.Triangle(p, new Vector2(-64f, 19f), new Vector2(-64f, -19f), new Vector2(-50f, 0f));
                float notchR = Sdf.Triangle(p, new Vector2(64f, 19f), new Vector2(64f, -19f), new Vector2(50f, 0f));
                return Sdf.Subtract(Sdf.Subtract(d, notchL), notchR);
            }, p => Mul(Color.white, Mathf.Lerp(0.82f, 1f, MathUtil.Smooth01((p.y + 16f) / 32f))));
            Later(rb, "MenuRibbon", x => Ribbon = x, null, new Vector4(22f * D, 0f, 22f * D, 0f), TextureWrapMode.Clamp);

            // shield badge (class, rank)
            var bd = new SdfCanvas(new Rect(-32, -36, 64, 72), D * 2f);
            SdfCanvas.SdfFn shield = p =>
            {
                float top = Sdf.Box(p, new Vector2(0f, 12f), new Vector2(26f, 20f), 6f);
                float tip = Sdf.Triangle(p, new Vector2(-26f, 6f), new Vector2(26f, 6f), new Vector2(0f, -33f));
                return Sdf.SmoothUnion(top, tip, 4f);
            };
            bd.Fill(p => shield(p) - 2.5f, Ink);
            bd.Fill(shield, p => Mul(Color.white, Mathf.Lerp(0.72f, 1f, MathUtil.Smooth01((p.y + 20f) / 50f))));
            bd.Paint(p => Sdf.Box(p, new Vector2(-8f, 18f), new Vector2(10f, 9f), 6f), new Color(1f, 1f, 1f, 0.4f), 4f);
            Ui(bd, "MenuBadge", x => Badge = x);

            // pedestal the menu's player stands on: stone top, carved side, gold rune ring
            var pd = new SdfCanvas(new Rect(-220, -80, 440, 160), 1.5f);
            Vector2 topC = new Vector2(0f, 18f), topR = new Vector2(196f, 44f);
            pd.Fill(p => Sdf.Ellipse(p, new Vector2(0f, -22f), new Vector2(210f, 50f)) - 4f, new Color(0f, 0f, 0f, 0.35f), 16f);
            SdfCanvas.SdfFn side = p => Mathf.Min(Sdf.Ellipse(p, new Vector2(0f, -10f), topR), Sdf.Box(p, new Vector2(0f, 4f), new Vector2(196f, 14f)));
            pd.Fill(p => Mathf.Min(side(p), Sdf.Ellipse(p, topC, topR)) - 5f, Ink);
            pd.Fill(side, p =>
            {
                float stone = 0.8f + 0.2f * Noise.Perlin(p.x * 0.08f, p.y * 0.2f);
                float shade = Mathf.Lerp(0.55f, 1f, MathUtil.Smooth01((p.x + 196f) / 392f) * 0.6f + 0.4f);
                return Mul(new Color(0.36f, 0.3f, 0.58f), stone * shade);
            });
            // carved blocks along the side
            for (int i = -6; i <= 6; i++)
            {
                float x = i * 30f + 6f;
                pd.Paint(p => Mathf.Max(Sdf.Box(p, new Vector2(x, -4f), new Vector2(1.2f, 30f)), -side(p) - 0f), new Color(0.16f, 0.12f, 0.3f, 0.8f), 1f);
            }
            pd.Fill(p => Sdf.Ellipse(p, topC, topR), p =>
            {
                float n = 0.85f + 0.15f * Noise.Perlin(p.x * 0.05f + 3f, p.y * 0.12f);
                float lit = MathUtil.Smooth01((p.y - 4f) / 40f);
                return Mul(Color.Lerp(new Color(0.46f, 0.4f, 0.72f), new Color(0.68f, 0.62f, 0.92f), lit), n);
            });
            pd.Fill(p => Sdf.Intersect(Mathf.Abs(Sdf.Ellipse(p, topC, topR - new Vector2(22f, 7f))) - 3f, 0f), new Color(1f, 0.82f, 0.36f, 0.95f));
            pd.Fill(p => Mathf.Abs(Sdf.Ellipse(p, topC, topR - new Vector2(36f, 11f))) - 1.2f, new Color(1f, 0.9f, 0.6f, 0.65f));
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f * Mathf.Deg2Rad;
                Vector2 c = topC + new Vector2(Mathf.Cos(a) * (topR.x - 29f), Mathf.Sin(a) * (topR.y - 9f));
                pd.Fill(p => Sdf.Box(p, c, new Vector2(3.4f, 1.6f), 0.8f), new Color(1f, 0.92f, 0.62f, 0.9f));
            }
            pd.Paint(p => Mathf.Abs(Sdf.Ellipse(p, topC, topR)) - 2f, new Color(0.85f, 0.8f, 1f, 0.8f), 1f);
            Ui(pd, "MenuPedestal", x => Pedestal = x);

            // soft vertical light beam (bottom = source)
            var bm = new SdfCanvas(new Rect(-32, 0, 64, 256), 0.5f);
            bm.Field(p =>
            {
                float across = Mathf.Exp(-(p.x * p.x) / (2f * 13f * 13f));
                float along = Mathf.Pow(1f - p.y / 256f, 1.6f) * MathUtil.Smooth01(p.y / 12f);
                return new Color(1f, 1f, 1f, across * along);
            });
            Ui(bm, "MenuBeam", x => Beam = x);

            var sk = new SdfCanvas(new Rect(-32, -32, 64, 64), D);
            sk.Field(p =>
            {
                Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y));
                float star = Mathf.Exp(-q.x * q.y * 0.08f) * Mathf.Exp(-(q.x + q.y) * 0.06f);
                float core = Mathf.Exp(-p.sqrMagnitude * 0.02f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(star * 1.1f + core));
            });
            Ui(sk, "MenuSparkle", x => Sparkle = x);

            var pe = new SdfCanvas(new Rect(-16, -10, 32, 20), D);
            pe.Fill(p => Sdf.Ellipse(p, Vector2.zero, new Vector2(14f, 7f)), p => Mul(Color.white, 0.8f + 0.2f * MathUtil.Smooth01(p.y / 7f + 0.5f)));
            pe.Paint(p => Sdf.Capsule(p, new Vector2(-10f, 0f), new Vector2(10f, 0f), 0.6f), new Color(0.75f, 0.75f, 0.8f, 0.8f), 0.5f);
            Ui(pe, "MenuPetal", x => Petal = x);

            // diagonal glint that sweeps over buttons (masked by the button itself)
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

        /// <summary>Fills a shape with a dark keyline around it (the keyline is drawn first, underneath).</summary>
        static void Outlined(SdfCanvas c, SdfCanvas.SdfFn f, SdfCanvas.ColorFn col, float line = 5f)
        {
            c.Fill(p => f(p) - line, Ink);
            c.Fill(f, col);
        }

        static void Outlined(SdfCanvas c, SdfCanvas.SdfFn f, Color col, float line = 5f) => Outlined(c, f, p => col, line);

        static SdfCanvas.ColorFn Lit(Color c, float y0, float y1, float dark = 0.72f)
            => p => Mul(c, Mathf.Lerp(dark, 1f, MathUtil.Smooth01((p.y - y0) / (y1 - y0))));

        static void BuildIcons(float D)
        {
            // treasure chest
            var ch = IconCanvas(D);
            Color wood = new Color(0.78f, 0.42f, 0.22f), gold = new Color(1f, 0.8f, 0.24f);
            SdfCanvas.SdfFn chestBody = p => Sdf.Box(p, new Vector2(0f, -22f), new Vector2(44f, 26f), 6f);
            SdfCanvas.SdfFn chestLid = p => Sdf.Union(Sdf.Box(p, new Vector2(0f, 14f), new Vector2(46f, 12f), 4f), Sdf.Intersect(Sdf.Ellipse(p, new Vector2(0f, 16f), new Vector2(46f, 30f)), -(p.y - 16f)));
            ch.Fill(p => Mathf.Min(chestBody(p), chestLid(p)) - 5f, Ink);
            ch.Fill(chestBody, Lit(wood, -48f, 4f, 0.6f));
            ch.Fill(chestLid, Lit(Mul(wood, 1.1f), 2f, 46f, 0.7f));
            foreach (float x in new[] { -30f, 30f })
            {
                ch.Fill(p => Sdf.Box(p, new Vector2(x, -4f), new Vector2(6f, 48f), 1f), Lit(gold, -48f, 44f, 0.7f));
            }
            ch.Fill(p => Sdf.Box(p, new Vector2(0f, 2f), new Vector2(46f, 4f), 1f), Mul(gold, 0.85f));
            ch.Fill(p => Sdf.Box(p, new Vector2(0f, -2f), new Vector2(11f, 13f), 3f) - 3f, Ink);
            ch.Fill(p => Sdf.Box(p, new Vector2(0f, -2f), new Vector2(11f, 13f), 3f), Lit(new Color(1f, 0.9f, 0.5f), -15f, 11f, 0.75f));
            ch.Fill(p => Sdf.Union(Sdf.Circle(p, new Vector2(0f, 0f), 3.4f), Sdf.Box(p, new Vector2(0f, -5f), new Vector2(1.6f, 4f))), Ink);
            ch.Paint(p => Sdf.Box(p, new Vector2(-18f, 30f), new Vector2(14f, 3f), 3f), new Color(1f, 1f, 1f, 0.45f), 2f);
            Ui(ch, "IconShop", x => IconShop = x);

            // trophy
            var tr = IconCanvas(D);
            SdfCanvas.SdfFn cup = p => Sdf.SmoothUnion(Sdf.Intersect(Sdf.Ellipse(p, new Vector2(0f, 30f), new Vector2(32f, 44f)), p.y - 30f), Sdf.Box(p, new Vector2(0f, 38f), new Vector2(34f, 8f), 3f), 3f);
            SdfCanvas.SdfFn handles = p => Mathf.Min(Sdf.Ring(p, new Vector2(-34f, 22f), 14f, 7f), Sdf.Ring(p, new Vector2(34f, 22f), 14f, 7f));
            SdfCanvas.SdfFn stem = p => Sdf.Union(Sdf.Box(p, new Vector2(0f, -22f), new Vector2(7f, 14f), 2f), Sdf.Box(p, new Vector2(0f, -44f), new Vector2(26f, 9f), 4f));
            tr.Fill(p => Mathf.Min(Mathf.Min(cup(p), handles(p)), stem(p)) - 5f, Ink);
            tr.Fill(handles, Mul(gold, 0.85f));
            tr.Fill(stem, Lit(gold, -54f, -8f, 0.65f));
            tr.Fill(p => Sdf.Box(p, new Vector2(0f, -44f), new Vector2(18f, 3f), 1.5f), new Color(0.72f, 0.4f, 0.12f));
            tr.Fill(cup, Lit(gold, -14f, 46f, 0.62f));
            tr.Paint(p => Sdf.Capsule(p, new Vector2(-16f, 36f), new Vector2(-12f, 4f), 5f), new Color(1f, 1f, 0.9f, 0.7f), 2f);
            tr.Fill(p => Sdf.Star4(p, new Vector2(4f, 20f), 11f, 0.6f), new Color(1f, 0.97f, 0.8f));
            Ui(tr, "IconTrophy", x => IconTrophy = x);

            // two friends
            var fr = IconCanvas(D);
            Color green = new Color(0.42f, 0.9f, 0.5f), teal = new Color(0.35f, 0.72f, 1f);
            SdfCanvas.SdfFn backHead = p => Sdf.Circle(p, new Vector2(20f, 20f), 17f);
            SdfCanvas.SdfFn backBody = p => Sdf.Intersect(Sdf.Ellipse(p, new Vector2(22f, -26f), new Vector2(32f, 34f)), p.y + 30f);
            SdfCanvas.SdfFn head = p => Sdf.Circle(p, new Vector2(-14f, 12f), 20f);
            SdfCanvas.SdfFn bodyF = p => Sdf.Intersect(Sdf.Ellipse(p, new Vector2(-14f, -40f), new Vector2(38f, 38f)), p.y + 50f);
            Outlined(fr, p => Mathf.Min(backHead(p), backBody(p)), Lit(teal, -50f, 40f, 0.65f));
            Outlined(fr, p => Mathf.Min(head(p), bodyF(p)), Lit(green, -54f, 34f, 0.6f));
            fr.Paint(p => Sdf.Circle(p, new Vector2(-20f, 20f), 7f), new Color(1f, 1f, 1f, 0.45f), 3f);
            Ui(fr, "IconFriends", x => IconFriends = x);

            // gear
            var gr = IconCanvas(D);
            SdfCanvas.SdfFn gear = p =>
            {
                float d = Sdf.Circle(p, Vector2.zero, 38f);
                for (int i = 0; i < 8; i++) d = Mathf.Min(d, Sdf.Box(p, MathUtil.Dir(i * 45f) * 42f, new Vector2(10f, 8f), 3f, i * 45f));
                return Sdf.Subtract(d, Sdf.Circle(p, Vector2.zero, 15f));
            };
            Outlined(gr, gear, Lit(new Color(0.82f, 0.88f, 1f), -50f, 50f, 0.6f));
            gr.Paint(p => Mathf.Abs(Sdf.Circle(p, Vector2.zero, 26f)) - 2f, new Color(0.5f, 0.56f, 0.75f, 0.8f), 1f);
            Ui(gr, "IconGear", x => IconGear = x);

            // info
            var inf = IconCanvas(D);
            Outlined(inf, p => Sdf.Circle(p, Vector2.zero, 48f), Lit(new Color(0.35f, 0.76f, 1f), -48f, 48f, 0.6f));
            inf.Fill(p => Sdf.Circle(p, new Vector2(0f, 24f), 8f), Color.white);
            inf.Fill(p => Sdf.Union(Sdf.Box(p, new Vector2(1f, -8f), new Vector2(7f, 20f), 2f), Sdf.Box(p, new Vector2(-4f, 9f), new Vector2(10f, 3.5f), 1.5f)), Color.white);
            inf.Fill(p => Sdf.Box(p, new Vector2(1f, -28f), new Vector2(14f, 3.5f), 1.5f), Color.white);
            inf.Paint(p => Sdf.Ellipse(p, new Vector2(-14f, 26f), new Vector2(18f, 10f)), new Color(1f, 1f, 1f, 0.3f), 4f);
            Ui(inf, "IconInfo", x => IconInfo = x);

            // events: calendar with a star
            var ev = IconCanvas(D);
            Color violet = new Color(0.72f, 0.46f, 1f);
            SdfCanvas.SdfFn page = p => Sdf.Box(p, new Vector2(0f, -6f), new Vector2(44f, 42f), 8f);
            Outlined(ev, page, Color.white);
            ev.Fill(p => Sdf.Intersect(page(p), -(p.y - 18f)), Lit(violet, 18f, 36f, 0.8f));
            foreach (float x in new[] { -22f, 22f })
            {
                ev.Fill(p => Sdf.Box(p, new Vector2(x, 38f), new Vector2(5f, 12f), 5f) - 4f, Ink);
                ev.Fill(p => Sdf.Box(p, new Vector2(x, 38f), new Vector2(5f, 12f), 5f), new Color(0.9f, 0.9f, 1f));
            }
            SdfCanvas.SdfFn star = p => Star5(p, new Vector2(0f, -14f), 24f, 11f);
            ev.Fill(p => star(p) - 3.5f, Ink);
            ev.Fill(star, Lit(new Color(1f, 0.8f, 0.25f), -38f, 10f, 0.7f));
            Ui(ev, "IconEvents", x => IconEvents = x);

            // coin
            var cn = IconCanvas(D);
            Outlined(cn, p => Sdf.Circle(p, Vector2.zero, 46f), Lit(gold, -46f, 46f, 0.62f));
            cn.Fill(p => Mathf.Abs(Sdf.Circle(p, Vector2.zero, 34f)) - 2.5f, Mul(gold, 0.72f));
            cn.Fill(p => Star5(p, new Vector2(0f, -2f), 20f, 9f), new Color(1f, 0.95f, 0.7f));
            cn.Paint(p => Sdf.Ellipse(p, new Vector2(-14f, 22f), new Vector2(14f, 8f)), new Color(1f, 1f, 1f, 0.5f), 3f);
            Ui(cn, "IconCoin", x => IconCoin = x);

            // gem
            var gm = IconCanvas(D);
            Color pink = new Color(1f, 0.36f, 0.72f);
            Vector2 g0 = new Vector2(-40f, 16f), g1 = new Vector2(-22f, 38f), g2 = new Vector2(22f, 38f), g3 = new Vector2(40f, 16f), g4 = new Vector2(0f, -42f);
            SdfCanvas.SdfFn gemShape = p => Mathf.Min(Mathf.Min(Sdf.Triangle(p, g0, g3, g4), Sdf.Triangle(p, g0, g1, g3)), Sdf.Triangle(p, g1, g2, g3));
            gm.Fill(p => gemShape(p) - 5f, Ink);
            gm.Fill(gemShape, pink);
            gm.Fill(p => Sdf.Triangle(p, g0, new Vector2(-12f, 16f), g4), Mul(pink, 1.25f));
            gm.Fill(p => Sdf.Triangle(p, new Vector2(12f, 16f), g3, g4), Mul(pink, 0.7f));
            gm.Fill(p => Mathf.Min(Sdf.Triangle(p, g1, new Vector2(-12f, 16f), g0), Sdf.Triangle(p, g1, new Vector2(0f, 38f), new Vector2(-12f, 16f))), new Color(1f, 0.75f, 0.9f));
            gm.Paint(p => Sdf.Capsule(p, new Vector2(-18f, 30f), new Vector2(-26f, 18f), 3f), new Color(1f, 1f, 1f, 0.8f), 1f);
            Ui(gm, "IconGem", x => IconGem = x);

            // power (quit)
            var pw = IconCanvas(D);
            SdfCanvas.SdfFn arc = p => Sdf.Subtract(Sdf.Ring(p, new Vector2(0f, -4f), 34f, 12f), Sdf.Triangle(p, new Vector2(0f, -4f), new Vector2(-40f, 60f), new Vector2(40f, 60f)));
            SdfCanvas.SdfFn bar = p => Sdf.Box(p, new Vector2(0f, 22f), new Vector2(6f, 24f), 6f);
            pw.Fill(p => Mathf.Min(arc(p), bar(p)) - 5f, Ink);
            pw.Fill(arc, Color.white);
            pw.Fill(bar, Color.white);
            Ui(pw, "IconPower", x => IconPower = x);

            // back arrow
            var bk = IconCanvas(D);
            SdfCanvas.SdfFn arrow = p => Sdf.Union(Sdf.Triangle(p, new Vector2(-44f, 0f), new Vector2(-4f, 36f), new Vector2(-4f, -36f)),
                                                    Sdf.Box(p, new Vector2(12f, 0f), new Vector2(30f, 13f), 5f));
            Outlined(bk, arrow, Lit(Color.white, -36f, 36f, 0.8f), 6f);
            Ui(bk, "IconBack", x => IconBack = x);

            // swap (two turning arrows)
            var sw = IconCanvas(D);
            SdfCanvas.SdfFn arcs = p =>
            {
                float a = Sdf.Intersect(Sdf.Ring(p, Vector2.zero, 34f, 11f), p.y - 6f);
                float b = Sdf.Intersect(Sdf.Ring(p, Vector2.zero, 34f, 11f), -(p.y + 6f));
                float h1 = Sdf.Triangle(p, new Vector2(18f, 8f), new Vector2(50f, 8f), new Vector2(34f, -16f));
                float h2 = Sdf.Triangle(p, new Vector2(-18f, -8f), new Vector2(-50f, -8f), new Vector2(-34f, 16f));
                return Mathf.Min(Mathf.Min(a, b), Mathf.Min(h1, h2));
            };
            Outlined(sw, arcs, Color.white, 5f);
            Ui(sw, "IconSwap", x => IconSwap = x);

            var ck = IconCanvas(D);
            SdfCanvas.SdfFn tick = p => Sdf.Union(Sdf.Capsule(p, new Vector2(-30f, 2f), new Vector2(-8f, -22f), 9f), Sdf.Capsule(p, new Vector2(-8f, -22f), new Vector2(34f, 26f), 9f));
            Outlined(ck, tick, Color.white, 6f);
            Ui(ck, "IconCheck", x => IconCheck = x);

            var lk = IconCanvas(D);
            SdfCanvas.SdfFn shackle = p => Sdf.Intersect(Sdf.Ring(p, new Vector2(0f, 10f), 22f, 9f), -(p.y - 6f) + 0f);
            SdfCanvas.SdfFn legs = p => Mathf.Min(Sdf.Box(p, new Vector2(-22f, 4f), new Vector2(4.5f, 8f)), Sdf.Box(p, new Vector2(22f, 4f), new Vector2(4.5f, 8f)));
            SdfCanvas.SdfFn lockBody = p => Sdf.Box(p, new Vector2(0f, -20f), new Vector2(34f, 26f), 7f);
            lk.Fill(p => Mathf.Min(Mathf.Min(shackle(p), legs(p)), lockBody(p)) - 5f, Ink);
            lk.Fill(p => Mathf.Min(shackle(p), legs(p)), new Color(0.78f, 0.82f, 0.92f));
            lk.Fill(lockBody, Lit(new Color(1f, 0.78f, 0.26f), -46f, 6f, 0.65f));
            lk.Fill(p => Sdf.Union(Sdf.Circle(p, new Vector2(0f, -16f), 6f), Sdf.Box(p, new Vector2(0f, -26f), new Vector2(2.6f, 8f), 2f)), Ink);
            Ui(lk, "IconLock", x => IconLock = x);

            var pl = IconCanvas(D);
            SdfCanvas.SdfFn tri = p => Sdf.Triangle(p, new Vector2(-26f, 38f), new Vector2(-26f, -38f), new Vector2(40f, 0f)) + 0f;
            pl.Fill(p => tri(p) - 9f, Ink);
            pl.Fill(p => tri(p) - 3f, Color.white);
            Ui(pl, "IconPlay", x => IconPlay = x);

            var plus = IconCanvas(D);
            SdfCanvas.SdfFn cross = p => Sdf.Union(Sdf.Box(p, Vector2.zero, new Vector2(34f, 11f), 5f), Sdf.Box(p, Vector2.zero, new Vector2(11f, 34f), 5f));
            Outlined(plus, cross, Color.white, 6f);
            Ui(plus, "IconPlus", x => IconPlus = x);

            var sr = IconCanvas(D);
            SdfCanvas.SdfFn bigStar = p => Star5(p, new Vector2(0f, -3f), 50f, 22f);
            sr.Fill(p => bigStar(p) - 5f, Ink);
            sr.Fill(bigStar, Lit(new Color(1f, 0.82f, 0.28f), -50f, 40f, 0.65f));
            sr.Paint(p => Sdf.Ellipse(p, new Vector2(-8f, 14f), new Vector2(10f, 7f)), new Color(1f, 1f, 1f, 0.55f), 3f);
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
            // striker: a ball with a flame tail
            var sk = IconCanvas(D);
            Color flame = new Color(1f, 0.46f, 0.2f), flameHi = new Color(1f, 0.85f, 0.3f);
            SdfCanvas.SdfFn tail = p => Sdf.SmoothUnion(Sdf.Tapered(p, new Vector2(-46f, 30f), 4f, new Vector2(4f, -2f), 26f),
                                                         Sdf.Tapered(p, new Vector2(-40f, -6f), 3f, new Vector2(4f, -8f), 20f), 6f);
            sk.Fill(p => tail(p) - 5f, Ink);
            sk.Fill(tail, p => Color.Lerp(flameHi, flame, MathUtil.Smooth01((-p.x + 10f) / 50f)));
            SdfCanvas.SdfFn ball = p => Sdf.Circle(p, new Vector2(14f, -8f), 28f);
            sk.Fill(p => ball(p) - 5f, Ink);
            sk.Fill(ball, Lit(Color.white, -36f, 20f, 0.78f));
            Vector2 bc = new Vector2(16f, -6f);
            sk.Fill(p => Sdf.Intersect(Sdf.Circle(p, bc, 9f), ball(p)), Ink);
            for (int k = 0; k < 5; k++)
            {
                Vector2 pc = bc + MathUtil.Dir(90f + k * 72f + 10f) * 24f;
                sk.Fill(p => Sdf.Intersect(Sdf.Circle(p, pc, 7.5f), ball(p) + 1f), Ink);
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
            Color blue = new Color(0.36f, 0.56f, 1f);
            Outlined(df, sh, Lit(blue, -50f, 46f, 0.6f), 6f);
            df.Fill(p => Sdf.Intersect(sh(p) + 9f, Mathf.Abs(p.x) - 9f), new Color(0.92f, 0.95f, 1f));
            df.Fill(p => Sdf.Intersect(sh(p) + 9f, Mathf.Abs(p.y - 14f) - 8f), new Color(0.92f, 0.95f, 1f));
            df.Paint(p => Sdf.Box(p, new Vector2(-18f, 30f), new Vector2(12f, 8f), 6f), new Color(1f, 1f, 1f, 0.35f), 3f);
            Ui(df, "IconDefender", x => IconDefender = x);

            // skiller: a big sparkle with two small ones
            var sl = IconCanvas(D);
            Color magic = new Color(0.82f, 0.5f, 1f);
            SdfCanvas.SdfFn sp = p => Sdf.Star4(p, new Vector2(-4f, -4f), 50f, 0.55f);
            sl.Fill(p => sp(p) - 5f, Ink);
            sl.Fill(sp, Lit(magic, -40f, 40f, 0.7f));
            sl.Fill(p => Sdf.Star4(p, new Vector2(-4f, -4f), 22f, 0.55f), new Color(1f, 0.9f, 1f));
            foreach (var c in new[] { new Vector2(34f, 34f), new Vector2(38f, -34f) })
            {
                Vector2 cc = c;
                float rr = cc.y > 0f ? 18f : 13f;
                sl.Fill(p => Sdf.Star4(p, cc, rr, 0.55f) - 4f, Ink);
                sl.Fill(p => Sdf.Star4(p, cc, rr, 0.55f), new Color(1f, 0.85f, 0.45f));
            }
            Ui(sl, "IconSkiller", x => IconSkiller = x);
        }

        public static Sprite ClassIcon(CharacterClass c) => c == CharacterClass.Defender ? IconDefender : c == CharacterClass.Skiller ? IconSkiller : IconStriker;

        // ------------------------------------------------------------------ tiling patterns

        static void BuildPatterns()
        {
            // little footballs and sparkles scattered on a repeating tile
            var bp = new SdfCanvas(new Rect(0, 0, 160, 160), 1f);
            void AddBall(Vector2 c, float r)
            {
                for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                {
                    Vector2 cc = c + new Vector2(ox * 160f, oy * 160f);
                    bp.Fill(p => Sdf.Ring(p, cc, r, 3f), Color.white);
                    bp.Fill(p => Sdf.Circle(p, cc + new Vector2(1f, 1f), r * 0.32f), Color.white);
                }
            }
            void AddSpark(Vector2 c, float r)
            {
                for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                {
                    Vector2 cc = c + new Vector2(ox * 160f, oy * 160f);
                    bp.Fill(p => Sdf.Star4(p, cc, r, 0.55f), Color.white);
                }
            }
            AddBall(new Vector2(40f, 40f), 18f);
            AddBall(new Vector2(120f, 120f), 18f);
            AddSpark(new Vector2(120f, 40f), 12f);
            AddSpark(new Vector2(40f, 120f), 12f);
            Later(bp, "MenuBallPattern", null, t => BallPattern = t, default, TextureWrapMode.Repeat);

            var sp = new SdfCanvas(new Rect(0, 0, 64, 64), 1f);
            sp.Field(p =>
            {
                float v = Mathf.Repeat(p.x + p.y, 32f);
                float a = MathUtil.Smooth01((Mathf.Abs(v - 16f) - 7f) / 1.5f);
                return new Color(1f, 1f, 1f, a);
            });
            Later(sp, "MenuStripes", null, t => Stripes = t, default, TextureWrapMode.Repeat);
        }

        // ------------------------------------------------------------------ text

        /// <summary>
        /// A second SDF asset of the same typeface with a wide padding: thick keylines and a hard
        /// drop shadow only fit into the distance field when there is room around each glyph.
        /// </summary>
        static void BuildFont()
        {
            var font = Resources.Load<Font>("Fonts/Inter-SemiBold");
            FontHeavy = font != null ? TMP_FontAsset.CreateFontAsset(font, 90, 22, GlyphRenderMode.SDFAA, 1024, 1024) : UiArt.FontBold;
            if (FontHeavy == null || FontHeavy.material == null) return;
            FontHeavy.name = "Inter Heavy SDF";

            TextHeavy = new Material(FontHeavy.material) { name = "SF Menu Heavy" };
            TextHeavy.EnableKeyword("OUTLINE_ON");
            TextHeavy.SetColor("_OutlineColor", Ink);
            TextHeavy.SetFloat("_FaceDilate", 0.18f);
            TextHeavy.SetFloat("_OutlineWidth", 0.26f);
            TextHeavy.SetFloat("_OutlineSoftness", 0f);
            TextHeavy.EnableKeyword("UNDERLAY_ON");
            TextHeavy.SetColor("_UnderlayColor", new Color(Ink.r, Ink.g, Ink.b, 0.95f));
            TextHeavy.SetFloat("_UnderlayOffsetX", 0.12f);
            TextHeavy.SetFloat("_UnderlayOffsetY", -0.7f);
            TextHeavy.SetFloat("_UnderlayDilate", 0.44f);
            TextHeavy.SetFloat("_UnderlaySoftness", 0f);

            // lighter version for small print (thin keyline, soft shadow)
            TextHeavySoft = new Material(FontHeavy.material) { name = "SF Menu Heavy Soft" };
            TextHeavySoft.EnableKeyword("OUTLINE_ON");
            TextHeavySoft.SetColor("_OutlineColor", Ink);
            TextHeavySoft.SetFloat("_FaceDilate", 0.1f);
            TextHeavySoft.SetFloat("_OutlineWidth", 0.16f);
            TextHeavySoft.EnableKeyword("UNDERLAY_ON");
            TextHeavySoft.SetColor("_UnderlayColor", new Color(Ink.r, Ink.g, Ink.b, 0.7f));
            TextHeavySoft.SetFloat("_UnderlayOffsetY", -0.45f);
            TextHeavySoft.SetFloat("_UnderlayDilate", 0.2f);
            TextHeavySoft.SetFloat("_UnderlaySoftness", 0.2f);

            // plain dark text printed onto light plates
            TextPlate = new Material(FontHeavy.material) { name = "SF Menu Plate" };
            TextPlate.SetFloat("_FaceDilate", 0.12f);
        }

        /// <summary>Heavy cartoon label: bold face, keyline, drop shadow.</summary>
        public static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box,
            TextAlignmentOptions align = TextAlignmentOptions.Center, float spacing = 2f, Material mat = null)
        {
            var t = UiKit.Label(name, parent, text, size, color, align, pos, box, true, spacing);
            if (FontHeavy != null) t.font = FontHeavy;
            var m = mat != null ? mat : TextHeavy;
            if (m != null) t.fontSharedMaterial = m;
            t.fontStyle = FontStyles.Bold;
            return t;
        }
    }
}
