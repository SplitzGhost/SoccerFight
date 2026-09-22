using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// The coin counter in the top-right corner of the HUD and the coins that fly into it. A coin
    /// handed over from the pitch pops up where it lay, then sweeps along a curved path into the
    /// counter, trailing sparkles and speeding up towards the end. On arrival the value is credited,
    /// the counter pops and counts up, a ring and a few sparks burst from the icon.
    /// </summary>
    public sealed class CoinCounter
    {
        sealed class Flyer
        {
            public RectTransform Rt, Spin;
            public Image Face, Glow;
            public Vector2 From, C1, C2;
            public float T, Dur, Scale, Phase, TrailT;
            public int Value;
            public bool Live;
        }

        sealed class Bit
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Pos, Vel;
            public float Age, Life, Size0, Size1, Spin;
            public Color Tint;
            public bool Live;
        }

        RectTransform canvas, root, flyRoot, icon, numberRt, pill;
        Image iconImg, ring, glow, rim;
        TextMeshProUGUI number, runText;
        readonly List<Flyer> flyers = new List<Flyer>();
        readonly List<Bit> bits = new List<Bit>();
        System.Func<Vector2, Vector2> worldToCanvas;

        float shown, pop, popVel, iconPunch, iconPunchVel, ringT = 99f, flashT = 99f, runT = 99f;
        float time;

        public void Build(RectTransform canvasRect, System.Func<Vector2, Vector2> toCanvas)
        {
            canvas = canvasRect;
            worldToCanvas = toCanvas;
            CoinArt.Build();

            root = UiKit.Node("Coins", canvasRect, Vector2.zero, new Vector2(200f, 56f));
            root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-44f - 100f, -46f);
            pill = UiKit.Node("Pill", root, Vector2.zero, new Vector2(200f, 56f));
            glow = UiKit.Img("Glow", pill, UiArt.Glow, Palette.Coin.WithAlpha(0f), new Vector2(-66f, 0f), new Vector2(160f, 160f));
            rim = UiKit.Img("Rim", pill, UiArt.Pill, Color.white.WithAlpha(0.16f), Vector2.zero, new Vector2(202f, 58f), Image.Type.Sliced);
            UiKit.Img("Body", pill, UiArt.Pill, Palette.UiGlass.WithAlpha(0.88f), Vector2.zero, new Vector2(200f, 56f), Image.Type.Sliced);
            ring = UiKit.Img("Ring", pill, UiArt.RingThin, Palette.Coin.WithAlpha(0f), new Vector2(-66f, 0f), new Vector2(60f, 60f));
            iconImg = UiKit.Img("Icon", pill, CoinArt.Ui, Color.white, new Vector2(-66f, 0f), new Vector2(46f, 46f));
            icon = iconImg.rectTransform;
            number = UiKit.Label("Amount", pill, "0", 26f, Palette.UiText, TextAlignmentOptions.Right, new Vector2(22f, 1f), new Vector2(120f, 40f), true, 1f);
            numberRt = number.rectTransform;
            runText = UiKit.Label("Run", root, "", 13f, Palette.Coin, TextAlignmentOptions.Right, new Vector2(-20f, -42f), new Vector2(240f, 20f), true, 3f);
            runText.alpha = 0f;

            flyRoot = UiKit.Node("Flying Coins", canvasRect, Vector2.zero, Vector2.zero);
            shown = Wallet.Get(Currencies.Coins);
        }

        /// <summary>Canvas position (centre origin) of the counter's coin icon.</summary>
        Vector2 Target => canvas.InverseTransformPoint(icon.TransformPoint(Vector3.zero));

        public void Reset()
        {
            foreach (var f in flyers) Retire(f);
            foreach (var b in bits) { b.Live = false; b.Rt.gameObject.SetActive(false); }
            shown = Wallet.Get(Currencies.Coins);
            runT = 99f;
            runText.alpha = 0f;
        }

        // ------------------------------------------------------------------ flights

        /// <summary>A coin leaves the pitch at this world position.</summary>
        public void Launch(Vector2 world, int value, float scale)
        {
            var f = TakeFlyer();
            f.From = worldToCanvas(world);
            f.Value = value;
            f.T = 0f;
            f.TrailT = 0f;
            f.Scale = scale;
            f.Phase = Random.value * 10f;
            Vector2 to = Target;
            float dist = Vector2.Distance(f.From, to);
            f.Dur = Mathf.Clamp(0.5f + dist / 3200f, 0.55f, 0.95f) * Random.Range(0.92f, 1.08f);
            // a swinging arc: up and out first, then curling into the counter from below-left
            float side = f.From.x < to.x ? -1f : 1f;
            f.C1 = f.From + new Vector2(side * Random.Range(60f, 160f), Random.Range(220f, 340f));
            f.C2 = to + new Vector2(-Random.Range(160f, 260f), -Random.Range(120f, 220f));
            f.Rt.anchoredPosition = f.From;
        }

        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        /// <summary>Everything still flying is credited at once (the run ended).</summary>
        public void Flush()
        {
            int total = 0;
            foreach (var f in flyers) if (f.Live) { total += f.Value; Retire(f); }
            if (total > 0) Wallet.Add(Currencies.Coins, total);
            shown = Wallet.Get(Currencies.Coins);
        }

        public void Update(float udt)
        {
            time += udt;
            Vector2 to = Target;
            foreach (var f in flyers)
            {
                if (!f.Live) continue;
                f.T += udt;
                float u = Mathf.Clamp01(f.T / f.Dur);
                // a little pop where it lay, then it accelerates into the counter
                float e = MathUtil.EaseInCubic(u) * 0.75f + u * 0.25f;
                Vector2 p = Bezier(f.From, f.C1, f.C2, to, e);
                f.Rt.anchoredPosition = p;
                float popIn = 1f + 0.3f * MathUtil.Bump(Mathf.Clamp01(f.T / 0.18f));
                float s = f.Scale * popIn * Mathf.Lerp(0.85f, 0.6f, MathUtil.EaseInQuad(u));
                f.Rt.localScale = new Vector3(s, s, 1f);
                // it tumbles: the face narrows and widens like a spinning coin
                float face = Mathf.Abs(Mathf.Cos((f.Phase + f.T * 11f)));
                f.Spin.localScale = new Vector3(Mathf.Max(0.18f, face), 1f, 1f);
                f.Face.color = Color.Lerp(new Color(0.7f, 0.58f, 0.4f), Color.white, 0.4f + 0.6f * face);
                f.Glow.color = Palette.Coin.WithAlpha(0.2f + 0.25f * u);

                // sparkle trail
                f.TrailT -= udt;
                if (f.TrailT <= 0f)
                {
                    f.TrailT = 0.022f;
                    var b = TakeBit();
                    b.Pos = p + Random.insideUnitCircle * 8f;
                    b.Vel = Random.insideUnitCircle * 30f;
                    b.Life = Random.Range(0.25f, 0.45f);
                    b.Size0 = Random.Range(14f, 24f) * f.Scale;
                    b.Size1 = 0f;
                    b.Spin = Random.Range(-200f, 200f);
                    b.Tint = Random.value < 0.35f ? Color.white : Palette.Coin;
                    b.Img.sprite = MenuArt.Sparkle != null ? MenuArt.Sparkle : UiArt.Glow;
                }
                if (u >= 1f) Arrive(f);
            }

            UpdateBits(udt);

            // the number rolls up to the wallet
            int target = Wallet.Get(Currencies.Coins);
            if (shown < target) shown = Mathf.Min(target, shown + Mathf.Max(18f, (target - shown) * 9f) * udt);
            else shown = target;
            number.text = Currencies.Format(Mathf.FloorToInt(shown));

            MathUtil.Spring(ref pop, ref popVel, 0f, 5f, 0.3f, udt);
            MathUtil.Spring(ref iconPunch, ref iconPunchVel, 0f, 6f, 0.28f, udt);
            float ps = 1f + pop * 0.22f;
            numberRt.localScale = new Vector3(ps, ps, 1f);
            float isc = 1f + iconPunch * 0.35f;
            icon.localScale = new Vector3(isc, isc, 1f);
            icon.localRotation = Quaternion.Euler(0f, 0f, iconPunch * 12f);
            pill.localScale = Vector3.one * (1f + pop * 0.04f);

            flashT += udt;
            float flash = Mathf.Clamp01(1f - flashT / 0.35f);
            number.color = Color.Lerp(Palette.UiText, Palette.Coin, flash);
            glow.color = Palette.Coin.WithAlpha(0.08f + 0.35f * flash + 0.04f * Mathf.Sin(time * 2f));
            rim.color = Color.Lerp(Color.white.WithAlpha(0.16f), Palette.Coin.WithAlpha(0.7f), flash);

            ringT += udt;
            float rk = Mathf.Clamp01(ringT / 0.4f);
            ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(46f, 120f, MathUtil.EaseOutCubic(rk));
            ring.color = Palette.Coin.WithAlpha(0.9f * (1f - rk) * (ringT < 0.4f ? 1f : 0f));

            // "+N in this run" under the counter while coins come in
            runT += udt;
            float ra = runT < 2.4f ? Mathf.Clamp01(runT * 6f) * (1f - MathUtil.Smooth01((runT - 1.8f) / 0.6f)) : 0f;
            runText.alpha = ra;
        }

        void Arrive(Flyer f)
        {
            Retire(f);
            Wallet.Add(Currencies.Coins, f.Value);
            popVel += 9f;
            iconPunchVel += 11f;
            flashT = 0f;
            ringT = 0f;
            runT = Mathf.Min(runT, 0.2f);
            runText.text = "+" + Currencies.Format(CoinRewards.Earned) + " IN DIESEM LAUF";

            Vector2 at = Target;
            for (int i = 0; i < 6; i++)
            {
                var b = TakeBit();
                b.Img.sprite = i % 2 == 0 && MenuArt.Sparkle != null ? MenuArt.Sparkle : UiArt.Glow;
                b.Pos = at;
                b.Vel = MathUtil.Dir(Random.Range(0f, 360f)) * Random.Range(90f, 220f);
                b.Life = Random.Range(0.3f, 0.5f);
                b.Size0 = Random.Range(16f, 26f);
                b.Size1 = 0f;
                b.Spin = Random.Range(-300f, 300f);
                b.Tint = i % 3 == 0 ? Color.white : Palette.Coin;
            }
        }

        void UpdateBits(float udt)
        {
            foreach (var b in bits)
            {
                if (!b.Live) continue;
                b.Age += udt;
                float u = b.Age / b.Life;
                if (u >= 1f) { b.Live = false; b.Rt.gameObject.SetActive(false); continue; }
                b.Vel *= Mathf.Exp(-3f * udt);
                b.Pos += b.Vel * udt;
                b.Rt.anchoredPosition = b.Pos;
                float sz = Mathf.Lerp(b.Size0, b.Size1, u);
                b.Rt.sizeDelta = new Vector2(sz, sz);
                b.Rt.localRotation = Quaternion.Euler(0f, 0f, b.Spin * b.Age);
                b.Img.color = b.Tint.WithAlpha(b.Tint.a * (1f - u));
            }
        }

        // ------------------------------------------------------------------ pools

        Flyer TakeFlyer()
        {
            foreach (var f in flyers) if (!f.Live) { f.Live = true; f.Rt.gameObject.SetActive(true); return f; }
            var n = new Flyer();
            n.Rt = UiKit.Node("Coin", flyRoot, Vector2.zero, new Vector2(40f, 40f));
            n.Glow = UiKit.Img("Glow", n.Rt, UiArt.Glow, Palette.Coin.WithAlpha(0.4f), Vector2.zero, new Vector2(110f, 110f));
            n.Spin = UiKit.Node("Spin", n.Rt, Vector2.zero, new Vector2(40f, 40f));
            n.Face = UiKit.Img("Face", n.Spin, CoinArt.Ui, Color.white, Vector2.zero, new Vector2(40f, 40f));
            n.Live = true;
            flyers.Add(n);
            return n;
        }

        void Retire(Flyer f)
        {
            f.Live = false;
            f.Rt.gameObject.SetActive(false);
        }

        Bit TakeBit()
        {
            foreach (var b in bits) if (!b.Live) { b.Live = true; b.Age = 0f; b.Rt.gameObject.SetActive(true); return b; }
            var n = new Bit();
            n.Img = UiKit.Img("Spark", flyRoot, UiArt.Glow, Color.white, Vector2.zero, new Vector2(20f, 20f));
            n.Rt = n.Img.rectTransform;
            n.Live = true;
            bits.Add(n);
            return n;
        }
    }
}
