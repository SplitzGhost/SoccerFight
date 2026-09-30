using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Freigegebene Originalansicht mit echten Werten und getrenntem Upgrade-Ziel. Darüber liegt die normale
    /// Menüleiste (Logo, Reiter, Währung); gemalter Zurück-Knopf und gemalte Währung sind aus dem Bild entfernt
    /// (tools/newdesign/character-menu-clean.js). Der gemalte Upgrade-Knopf hebt sich beim Zeigen an: ein weich
    /// begrenzter Ausschnitt desselben Bildes liegt deckungsgleich darüber und wird vergrößert. Ein Upgrade löst
    /// eine ruhige grüne Welle aus: Ring am Knopf, aufsteigende Funken, dann leuchten Stufe und Werte nacheinander
    /// auf („+8“ steigt auf). Die Szene lebt: Bodennebel und Dunst ziehen im Wind, Lichtpunkte schweben, und das
    /// Bild folgt dem Zeiger mit leichter Parallaxe (vordere Ebenen weiter).
    /// </summary>
    public sealed class CharacterDetailPage
    {
        public SubPage Page { get; private set; }
        public System.Action Return;
        MenuNav nav;
        CharacterDef character;
        RawImage art;
        Texture2D texture;
        CharacterLoopPlayer loop;
        public string LoopId => loop.CharacterId;
        public bool VideoPlaying => loop.IsPlaying;
        public int CompletedLoops => loop.CompletedLoops;

        sealed class Lift
        {
            public RectTransform Mask, Inner;
            public RawImage Img;
            public Image Glow, Shine;
            public Vector2 Size;
        }

        sealed class Stat
        {
            public TextMeshProUGUI Value, Delta, Rise;
            public Image Glow;
            public float Delay, Pop, PopVel;
            public bool Kicked;
        }

        sealed class Spark
        {
            public Image Img;
            public Vector2 Pos, Vel;
            public float Age, Life = -1f, Size, Spin;
        }

        /// <summary>Schwebender Lichtpunkt (Pollen, Glühwürmchen) oder unscharfer Lichtfleck im Vordergrund.</summary>
        sealed class Mote
        {
            public Image Img;
            public Vector2 Pos, Vel;
            public float Age, Life, Size, Phase, Alpha;
            public Color Tint;
        }

        TextMeshProUGUI level, plaque, cost;
        readonly Stat[] stats = new Stat[3];
        Lift upgrade, plaquePatch, skyLeft, skyRight;
        MenuTarget upgradeTarget;
        Image badgeGlow, ring, flash;
        readonly List<Spark> sparks = new List<Spark>();
        RectTransform fxRoot;
        int shownLevel = -1, shownGems = -1;
        float time, fxT = 99f, levelPop, levelPopVel, costShake;
        bool affordable, maxed, levelKicked = true;

        // Atmosphäre: Dunst hinten, Bodennebel vorn, Lichtpunkte und Lichtflecken; alles treibt im selben Wind
        RectTransform hazeLayer, fogLayer, moteLayer, bokehLayer;
        RawImage haze, fogA, fogB;
        readonly List<Mote> motes = new List<Mote>(), bokeh = new List<Mote>();
        Vector2 sway, swayVel, drift;
        float wind;
        static Texture2D fogTex;
        readonly PlayerStats now = new PlayerStats(), next = new PlayerStats();

        static readonly Color Ink = new Color(0.035f, 0.14f, 0.22f);
        static readonly Color Bronze = new Color(0.24f, 0.1f, 0.05f);
        /// <summary>Das Upgrade-Grün: frisch, nicht grell (linearer Farbraum + Bloom).</summary>
        static readonly Color Mint = new Color(0.36f, 1f, 0.58f);
        static Material statMat, levelMat, deltaMat;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { statMat = levelMat = deltaMat = null; fogTex = null; }

        static Vector2 At(float x, float y) => new Vector2(x - 836f, 470.5f - y);

        /// <summary>Kräftige Zahl: verdickte Buchstaben, darunter eine helle (geprägt) oder dunkle Kante.</summary>
        static Material NumberMat(float dilate, Color underlay, float offsetY, Color outline, float outlineW)
        {
            var src = UiArt.FontBold != null ? UiArt.FontBold.material : null;
            if (src == null) return null;
            var m = new Material(src) { name = "Charakterwerte" };
            m.SetFloat("_FaceDilate", dilate);
            if (outlineW > 0f)
            {
                m.EnableKeyword("OUTLINE_ON");
                m.SetColor("_OutlineColor", outline);
                m.SetFloat("_OutlineWidth", outlineW);
            }
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", underlay);
            m.SetFloat("_UnderlayOffsetY", offsetY);
            m.SetFloat("_UnderlayDilate", 0.15f);
            m.SetFloat("_UnderlaySoftness", 0.25f);
            return m;
        }

        public void Build(RectTransform parent, MenuNav menu)
        {
            nav = menu;
            if (statMat == null)
            {
                statMat = NumberMat(0.24f, new Color(1f, 1f, 1f, 0.45f), -0.9f, Color.clear, 0f);
                levelMat = NumberMat(0.2f, new Color(1f, 0.78f, 0.6f, 0.55f), -1.1f, Color.clear, 0f);
                deltaMat = NumberMat(0.2f, new Color(0f, 0.08f, 0.04f, 0.6f), -0.8f, new Color(0.02f, 0.2f, 0.1f, 0.95f), 0.2f);
            }
            Page = new SubPage(parent, MenuPage.CharacterDetails, "CHARAKTER", "", MenuArt.Accent, menu.Register, menu.Back, true);
            // Die freigegebene Grafik wird nie verzerrt oder beschnitten. Außerhalb von 16:9 füllt dasselbe Bild,
            // stark weichgezeichnet und abgedunkelt, den Rest des Bildschirms; die Kanten laufen weich hinein.
            MenuUi.Stretch(UiKit.Img("Rand", Page.Root, null, new Color(0.015f, 0.1f, 0.13f), Vector2.zero, Vector2.zero).rectTransform);
            fill = UiKit.Node("Bildfüllung", Page.Root, Vector2.zero, Page.ArtworkSize).gameObject.AddComponent<RawImage>();
            fill.raycastTarget = false;
            fill.color = new Color(0.5f, 0.58f, 0.64f);
            Page.Content.SetAsLastSibling();
            edge = Page.Content.gameObject.AddComponent<RectMask2D>();
            art = UiKit.Node("Freigegebenes Probebild", Page.Content, Vector2.zero, Page.ArtworkSize).gameObject.AddComponent<RawImage>();
            art.raycastTarget = false;
            loop = new CharacterLoopPlayer(art, parent);
            // Die Videoloops zeigen oben noch den gemalten Zurück-Knopf und die gemalte Währung: dort liegt der
            // bereinigte Himmel des Standbilds darüber (darauf stehen Logo und Währung der Menüleiste).
            skyLeft = MakeLift("Himmel links", 84f, 78f, new Vector2(196f, 168f), 14);
            skyRight = MakeLift("Himmel rechts", 1510f, 44f, new Vector2(344f, 100f), 14);
            BuildAtmosphere();

            // Stufe: tief in die Bronze geprägt; das Schild unten trägt nur noch „STUFE“. Die Videoloops
            // stammen noch aus Bildern mit gemalter „1“ im Schild: dort liegt das bereinigte Standbild darüber.
            plaquePatch = MakeLift("Schild-Abdeckung", 1053f, 226f, new Vector2(70f, 44f), 10);
            badgeGlow = UiKit.Img("Stufen-Licht", Page.Content, UiArt.Glow, Color.clear, At(1052f, 180f), new Vector2(260f, 260f));
            level = Number("Stufe", 1052f, 168f, 60f, new Vector2(84f, 64f), Bronze, levelMat);
            plaque = MenuArt.Label("Schild", Page.Content, "STUFE", 11f, Bronze.WithAlpha(0.85f), At(1053f, 226f), new Vector2(46f, 16f), TextAlignmentOptions.Center, 0.5f, MenuArt.TextHeavySoft);

            float[] xs = { 1051f, 1271f, 1486f };
            for (int i = 0; i < 3; i++)
            {
                var st = new Stat { Delay = 0.22f + i * 0.07f };
                st.Glow = UiKit.Img("Wert-Licht", Page.Content, UiArt.Glow, Color.clear, At(xs[i], 600f), new Vector2(250f, 250f));
                st.Value = Number("Wert", xs[i], 630f, 38f, new Vector2(104f, 42f), Ink, statMat);
                st.Delta = Number("Vorschau", xs[i], 658f, 19f, new Vector2(74f, 22f), Mint, deltaMat);
                st.Rise = Number("Zuwachs", xs[i], 657f, 26f, new Vector2(110f, 30f), Mint, deltaMat);
                stats[i] = st;
            }

            upgrade = MakeLift("Upgrade", 1272f, 800f, new Vector2(290f, 236f), 30);
            cost = Number("Kristallpreis", 1285f, 731f, 30f, new Vector2(92f, 38f), Color.white, CurrencyBar.Numbers, upgrade.Inner, 1272f, 800f);
            upgradeTarget = new MenuTarget { Id = "detail_upgrade", Root = upgrade.Mask, Size = new Vector2(260f, 190f),
                Page = MenuPage.CharacterDetails, Action = Upgrade, Accent = Mint, Draw = DrawUpgrade };
            nav.Register(upgradeTarget);

            // Effekte liegen über allem
            fxRoot = UiKit.Node("Upgrade-Effekte", Page.Content, Vector2.zero, Page.ArtworkSize);
            flash = UiKit.Img("Welle-Licht", fxRoot, UiArt.Glow, Color.clear, At(1272f, 825f), new Vector2(420f, 420f));
            ring = UiKit.Img("Welle", fxRoot, MenuArt.Shock, Color.clear, At(1272f, 825f), new Vector2(100f, 100f));
            for (int i = 0; i < 18; i++)
            {
                var img = UiKit.Img("Funke", fxRoot, MenuArt.Sparkle, Color.clear, Vector2.zero, new Vector2(20f, 20f));
                sparks.Add(new Spark { Img = img });
            }
        }

        // ------------------------------------------------------------------ Atmosphäre

        /// <summary>Weiche, waagrecht nahtlos kachelnde Nebelschwaden (oben und unten auslaufend).</summary>
        static Texture2D FogTexture()
        {
            if (fogTex != null) return fogTex;
            const int W = 256, H = 64;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = (y + 0.5f) / H;
                float profile = Mathf.Pow(Mathf.Sin(v * Mathf.PI), 1.6f);
                for (int x = 0; x < W; x++)
                {
                    float n = 0f, amp = 0.55f, sum = 0f, t = x / (float)W;
                    for (int o = 0; o < 4; o++)
                    {
                        float f = 3f * (1 << o);                  // ganzzahlig viele Zellen über die Breite → nahtlos
                        float cx = t * f, cy = v * f * 0.35f + o * 7.3f;
                        float a = Mathf.PerlinNoise(cx + 11.1f, cy) * (1f - t) + Mathf.PerlinNoise(cx - f + 11.1f, cy) * t;
                        n += a * amp; sum += amp; amp *= 0.5f;
                    }
                    n /= sum;
                    float d = Mathf.Clamp01((n - 0.3f) * 2.2f);
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(255f * d * d * (3f - 2f * d) * profile));
                }
            }
            fogTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "Charakter-Nebel", filterMode = FilterMode.Bilinear };
            fogTex.wrapModeU = TextureWrapMode.Repeat;
            fogTex.wrapModeV = TextureWrapMode.Clamp;
            fogTex.SetPixels32(px);
            fogTex.Apply(false, true);
            return fogTex;
        }

        /// <summary>Eine Nebelebene: rechts weich ausgeblendet, damit sie nie über Werte und Knöpfe zieht.</summary>
        RectTransform FogLayer(string name, Vector2 home, Vector2 size, int softRight)
        {
            var layer = UiKit.Node(name, Page.Content, home, size);
            layer.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(softRight, 0);
            return layer;
        }

        static RawImage Fog(RectTransform layer, Color tint)
        {
            var img = UiKit.Node("Schwaden", layer, Vector2.zero, layer.sizeDelta).gameObject.AddComponent<RawImage>();
            img.texture = FogTexture();
            img.color = tint;
            img.raycastTarget = false;
            return img;
        }

        static readonly Vector2 HazeHome = At(430f, 660f), FogHome = At(420f, 905f);

        void BuildAtmosphere()
        {
            // der ferne Dunst liegt über den Ruinen hinter der Figur, der Bodennebel vor ihren Füßen
            hazeLayer = FogLayer("Dunst", HazeHome, new Vector2(1240f, 260f), 260);
            haze = Fog(hazeLayer, new Color(0.86f, 0.94f, 1f, 0.16f));
            moteLayer = UiKit.Node("Lichtpunkte", Page.Content, Vector2.zero, Page.ArtworkSize);
            for (int i = 0; i < 24; i++)
            {
                var m = new Mote { Img = UiKit.Img("Lichtpunkt", moteLayer, UiArt.Glow, Color.clear, Vector2.zero, Vector2.one * 8f) };
                Respawn(m, true);
                motes.Add(m);
            }
            fogLayer = FogLayer("Bodennebel", FogHome, new Vector2(1320f, 230f), 300);
            fogA = Fog(fogLayer, new Color(0.88f, 0.95f, 1f, 0.22f));
            fogB = Fog(fogLayer, new Color(0.92f, 0.97f, 1f, 0.14f));
            bokehLayer = UiKit.Node("Lichtflecken", Page.Content, Vector2.zero, Page.ArtworkSize);
            for (int i = 0; i < 4; i++)
            {
                var b = new Mote { Img = UiKit.Img("Lichtfleck", bokehLayer, UiArt.Glow, Color.clear, Vector2.zero, Vector2.one) };
                b.Size = Random.Range(90f, 170f);
                b.Pos = At(Random.Range(0f, 1000f), Random.Range(150f, 850f));
                b.Phase = Random.value * 10f;
                b.Alpha = Random.Range(0.025f, 0.045f);
                b.Tint = Color.Lerp(new Color(1f, 0.93f, 0.8f), new Color(0.8f, 0.94f, 1f), Random.value);
                bokeh.Add(b);
            }
        }

        /// <summary>Ein Lichtpunkt beginnt neu irgendwo links der Werte (beim Aufbau schon mitten im Leben).</summary>
        static void Respawn(Mote m, bool anyAge)
        {
            m.Pos = At(Random.Range(-20f, 1000f), Random.Range(160f, 900f));
            m.Vel = new Vector2(Random.Range(-6f, 6f), Random.Range(2f, 12f));
            m.Life = Random.Range(4f, 8f);
            m.Age = anyAge ? Random.Range(0f, m.Life) : 0f;
            m.Size = Random.Range(6f, 13f);
            m.Phase = Random.value * 10f;
            m.Alpha = Random.Range(0.5f, 0.85f);
            m.Tint = Random.value < 0.65f ? new Color(1f, 0.84f, 0.5f) : new Color(0.7f, 0.93f, 1f);
        }

        /// <summary>
        /// Wind aus Grundzug und langsamen Böen; Nebel, Dunst, Lichtpunkte und Flecken treiben darin, jede Ebene
        /// nach ihrer Entfernung. Das Bild folgt dem Zeiger träge (Feder), nähere Ebenen verschieben sich weiter.
        /// </summary>
        void UpdateAtmosphere(float dt, Vector2 aim)
        {
            wind = 16f + 14f * (Mathf.PerlinNoise(time * 0.12f, 3.7f) - 0.5f) * 2f;
            drift.x += wind * dt;

            // Kamera: folgt dem Zeiger mit Trägheit, dazu ein kaum merkliches Atmen
            Vector2 idle = new Vector2(Mathf.PerlinNoise(time * 0.07f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.9f, time * 0.06f) - 0.5f) * 6f;
            MathUtil.Spring(ref sway, ref swayVel, -new Vector2(aim.x * 11f, aim.y * 7f) + idle, 0.8f, 0.72f, dt);
            float k = Page.Content.localScale.x;
            Page.Content.localScale = Vector3.one * (k * 1.025f);   // etwas Überstand: beim Verschieben wird kein Rand sichtbar
            Page.Content.anchoredPosition = sway * k + artShift;
            hazeLayer.anchoredPosition = HazeHome - sway * 0.35f;
            moteLayer.anchoredPosition = sway * 0.6f;
            fogLayer.anchoredPosition = FogHome + sway * 0.9f;
            bokehLayer.anchoredPosition = sway * 2.2f;

            // Schwaden: die Textur zieht mit dem Wind (fern langsamer), leicht auf- und abwallend
            float hw = hazeLayer.sizeDelta.x, fw = fogLayer.sizeDelta.x;
            haze.uvRect = new Rect(-drift.x * 0.3f / hw, 0f, 1f, 1f);
            haze.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Sin(time * 0.21f) * 6f);
            fogA.uvRect = new Rect(-drift.x * 0.8f / fw, 0f, 0.8f, 1f);
            fogB.uvRect = new Rect(0.37f - drift.x * 1.3f * 1.3f / fw, 0f, 1.3f, 1f);
            fogA.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Sin(time * 0.33f) * 5f);
            fogB.rectTransform.anchoredPosition = new Vector2(0f, -14f + Mathf.Sin(time * 0.27f + 1.7f) * 7f);
            fogA.color = fogA.color.WithAlpha(0.2f + 0.04f * Mathf.Sin(time * 0.19f));

            // Lichtpunkte: Auftrieb, Luftwiderstand zum Wind hin, sanfte Wirbel
            foreach (var m in motes)
            {
                m.Age += dt;
                if (m.Age >= m.Life) Respawn(m, false);
                Vector2 air = new Vector2(wind * 0.55f, 6f);
                Vector2 swirl = new Vector2(Mathf.Sin(m.Pos.y * 0.012f + time * 0.6f + m.Phase) * 14f,
                                            Mathf.Cos(m.Pos.x * 0.01f + time * 0.45f + m.Phase) * 10f);
                m.Vel += ((air - m.Vel) * 0.9f + swirl) * dt;
                m.Pos += m.Vel * dt;
                float u = m.Age / m.Life;
                float flicker = 0.65f + 0.35f * Mathf.Sin(time * (1.3f + m.Phase * 0.2f) + m.Phase * 3f);
                var rt = m.Img.rectTransform;
                rt.anchoredPosition = m.Pos;
                rt.sizeDelta = Vector2.one * m.Size;
                m.Img.color = m.Tint.WithAlpha(m.Alpha * flicker * MathUtil.Bump(u));
            }
            float x0 = At(-120f, 0f).x, x1 = At(1060f, 0f).x;
            foreach (var b in bokeh)
            {
                b.Pos.x += wind * 0.9f * dt;
                if (b.Pos.x > x1) b.Pos.x = x0;
                var rt = b.Img.rectTransform;
                rt.anchoredPosition = b.Pos + new Vector2(0f, Mathf.Sin(time * 0.3f + b.Phase) * 12f);
                rt.sizeDelta = Vector2.one * b.Size;
                float fade = Mathf.Clamp01((b.Pos.x - x0) / 160f) * Mathf.Clamp01((x1 - b.Pos.x) / 220f);
                b.Img.color = b.Tint.WithAlpha(b.Alpha * fade * (0.8f + 0.2f * Mathf.Sin(time * 0.5f + b.Phase)));
            }
        }

        /// <summary>Ein Ausschnitt des Bildes, deckungsgleich über dem gemalten Knopf, mit weichem Rand.</summary>
        Lift MakeLift(string name, float x, float y, Vector2 size, int soft)
        {
            var l = new Lift { Size = size };
            l.Mask = UiKit.Node(name, Page.Content, At(x, y), size);
            var mask = l.Mask.gameObject.AddComponent<RectMask2D>();
            mask.softness = new Vector2Int(soft, soft);
            l.Inner = UiKit.Node("Bild", l.Mask, Vector2.zero, size);
            l.Img = l.Inner.gameObject.AddComponent<RawImage>();
            l.Img.raycastTarget = false;
            Vector2 a = Page.ArtworkSize;
            l.Img.uvRect = new Rect((x - size.x * 0.5f) / a.x, 1f - (y + size.y * 0.5f) / a.y, size.x / a.x, size.y / a.y);
            l.Glow = UiKit.Img("Licht", l.Inner, UiArt.Glow, Color.clear, Vector2.zero, size * 0.9f);
            l.Shine = UiKit.Img("Glanz", l.Inner, UiArt.Glow, Color.clear, Vector2.zero, new Vector2(size.y * 0.22f, size.y * 1.3f));
            l.Shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -24f);
            return l;
        }

        TextMeshProUGUI Number(string name, float x, float y, float size, Vector2 box, Color color, Material mat,
            Transform parent = null, float px = 836f, float py = 470.5f)
        {
            // parent: ein Knopf-Ausschnitt (Koordinaten relativ zu seiner Mitte px/py)
            Vector2 pos = parent == null ? At(x, y) : new Vector2(x - px, py - y);
            var text = UiKit.Label(name, parent != null ? parent : Page.Content, "", size, color, TextAlignmentOptions.Center, pos, box);
            if (UiArt.FontBold != null) text.font = UiArt.FontBold;
            if (mat != null) text.fontSharedMaterial = mat;
            text.fontStyle = FontStyles.Bold;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * 0.55f;
            text.fontSizeMax = size;
            return text;
        }

        public void Open(CharacterDef def)
        {
            if (character != def || texture == null)
            {
                ReleaseArt();
                character = def;
                texture = Resources.Load<Texture2D>("Menu/CharacterDetails/" + def.Id);
                art.texture = texture;
            }
            shownLevel = shownGems = -1;
            fxT = 99f;
            loop.Open(def.Id, texture);
            Refresh();
        }

        void Upgrade()
        {
            if (character == null || !Profile.OwnsCharacter(character.Id)) return;
            int current = CharacterProgression.Level(character);
            if (current >= CharacterProgression.MaxLevel)
            {
                nav.Say("MAXIMALE STUFE ERREICHT", upgrade.Mask);
                return;
            }
            int price = CharacterProgression.UpgradeCost(current);
            if (!CharacterProgression.TryUpgrade(character))
            {
                costShake = 1f;
                nav.Say("DIR FEHLEN " + Currencies.Format(Mathf.Max(0, price - Wallet.Get(Currencies.Gems))) + " KRISTALLE", upgrade.Mask);
                return;
            }
            if (Game.I != null && Game.I.Run != null) Game.I.Run.Rebuild();
            if (Game.I != null && Game.I.Player != null) Game.I.Player.ApplyStats(false);
            // die Zuwächse steigen gleich in Grün aus den Rauten auf
            for (int i = 0; i < 3; i++) { stats[i].Rise.text = stats[i].Delta.text; stats[i].Kicked = false; }
            levelKicked = false;
            fxT = 0f;
            for (int i = 0; i < sparks.Count; i++)
            {
                var s = sparks[i];
                float a = Random.Range(-0.9f, 0.9f);
                s.Pos = At(1272f, 830f) + new Vector2(Mathf.Sin(a) * 60f, Random.Range(-20f, 30f));
                s.Vel = new Vector2(Mathf.Sin(a) * Random.Range(40f, 120f), Random.Range(160f, 340f));
                s.Age = -Random.Range(0f, 0.15f);
                s.Life = Random.Range(0.7f, 1.15f);
                s.Size = Random.Range(12f, 26f);
                s.Spin = Random.Range(-200f, 200f);
            }
            Refresh();
        }

        static string Decimal(float v, string format) => v.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',');

        void Refresh()
        {
            if (character == null) return;
            int n = CharacterProgression.Level(character);
            int g = Wallet.Get(Currencies.Gems);
            if (n == shownLevel && g == shownGems) return;
            shownLevel = n; shownGems = g;
            Measure(now, n, out float hp, out float dmg, out float spd);
            level.text = n.ToString();
            stats[0].Value.text = Mathf.RoundToInt(hp).ToString();
            stats[1].Value.text = Decimal(dmg, "0.#");
            stats[2].Value.text = Decimal(spd, "0.0");
            maxed = n >= CharacterProgression.MaxLevel;
            int price = CharacterProgression.UpgradeCost(n);
            affordable = !maxed && g >= price;
            cost.text = maxed ? "MAX" : price.ToString();
            cost.color = maxed || affordable ? Color.white : MetaUi.Danger;
            if (maxed) { foreach (var s in stats) s.Delta.text = ""; return; }
            // Vorschau der nächsten Stufe (erscheint beim Zeigen auf den Upgrade-Knopf)
            Measure(next, n + 1, out float hp1, out float dmg1, out float spd1);
            stats[0].Delta.text = "+" + Mathf.RoundToInt(hp1 - Mathf.RoundToInt(hp));
            stats[1].Delta.text = "+" + Decimal(Mathf.Max(0.1f, dmg1 - dmg), "0.0");
            stats[2].Delta.text = "+" + Decimal(Mathf.Max(0.1f, spd1 - spd), "0.0");
        }

        /// <summary>Dauerhafte Werte des normalen Schusses/Wurfs, ohne zufällige Krits und Laufkarten.</summary>
        void Measure(PlayerStats s, int lvl, out float hp, out float dmg, out float spd)
        {
            s.Reset();
            MetaPassives.Apply(s, character);
            CharacterProgression.ApplyLevel(s, lvl);
            hp = Player.BaseMaxHp + s.MaxHpBonus;
            dmg = Player.ShotDamage * s.DamageMul * s.ShotDamageMul * s.CategoryMul(SkillCategory.Shot);
            spd = Player.MaxSpeed * s.MoveSpeedMul;
        }

        void DrawUpgrade(MenuTarget t)
        {
            float h = Mathf.Clamp01(t.Hover);
            // bezahlbar: der Knopf atmet leise, auch ohne Zeiger
            float breathe = affordable ? 0.5f + 0.5f * Mathf.Sin(time * 2.6f) : 0f;
            float wave = Mathf.Clamp01(1f - fxT / 0.5f);
            float shake = costShake * Mathf.Sin(time * 60f) * 5f;
            float s = 1f + h * 0.045f + breathe * 0.008f + t.Punch * 0.05f + wave * 0.03f;
            upgrade.Inner.localScale = new Vector3(s, s, 1f);
            upgrade.Inner.anchoredPosition = new Vector2(shake, h * 4f);
            Color glow = maxed ? new Color(1f, 0.85f, 0.4f) : affordable ? Mint : new Color(0.4f, 0.85f, 1f);
            upgrade.Glow.color = glow.WithAlpha(h * 0.08f + breathe * 0.03f + wave * 0.18f + t.Hit * 0.1f);
            upgrade.Glow.rectTransform.anchoredPosition = new Vector2(0f, -25f);
            Sweep(upgrade, h);
            // Vorschau der Zuwächse
            float show = maxed ? 0f : h;
            foreach (var st in stats) st.Delta.alpha = show * (affordable ? 1f : 0.55f);
        }

        /// <summary>Ein schmaler Lichtstreif gleitet beim Zeigen über den Knopf.</summary>
        void Sweep(Lift l, float h)
        {
            float u = Mathf.Repeat(time * 0.7f, 1.4f);
            l.Shine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-0.6f, 0.6f, u) * l.Size.x, 0f);
            l.Shine.color = Color.white.WithAlpha(h * 0.09f * (u < 1f ? Mathf.Sin(u * Mathf.PI) : 0f));
        }

        RawImage fill;
        Vector2 artShift;
        RectMask2D edge;
        RenderTexture[] blur;

        /// <summary>Das aktuelle Bild in drei Schritten auf 26 × 15 Punkte verkleinert: vergrößert ergibt das weiche Farbflächen.</summary>
        Texture Blurred(Texture source)
        {
            if (blur == null)
            {
                blur = new RenderTexture[3];
                for (int i = 0; i < 3; i++)
                    blur[i] = new RenderTexture(416 >> (i * 2), 234 >> (i * 2), 0, RenderTextureFormat.ARGB32) { name = "Charakter-Unschärfe " + i, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            }
            for (int i = 0; i < 3; i++) Graphics.Blit(i == 0 ? source : blur[i - 1], blur[i]);
            return blur[2];
        }

        /// <summary>Breiter oder höher als 16:9: das Bild steht ganz in der Mitte, seine vergrößerte Kopie füllt den Rest.</summary>
        void FillScreen()
        {
            Rect r = Page.Root.rect;
            Vector2 size = Page.ArtworkSize;
            float fit = Page.Content.localScale.x;
            float barX = r.width - size.x * fit, barY = r.height - size.y * fit;
            bool bars = barX > 2f || barY > 2f;
            if (fill.enabled != bars) fill.enabled = bars;
            edge.softness = new Vector2Int(barX > 2f ? 110 : 0, barY > 2f ? 110 : 0);
            // ohne Rand bleibt der kleine Überstand der Kamerabewegung unbeschnitten
            // Höher als 16:9: das Bild schließt oben an (die Menüleiste liegt wie gewohnt auf seinem Himmel),
            // der freie Platz liegt darunter; die Oberkante wird dann nicht ausgeblendet.
            artShift = new Vector2(0f, barY > 2f ? barY * 0.5f : 0f);
            edge.padding = bars ? new Vector4(0f, 0f, 0f, barY > 2f ? -260f : 0f) : Vector4.one * -60f;
            if (!bars) return;
            if (art.texture == null) { fill.enabled = false; return; }
            fill.texture = Blurred(art.texture);
            float cover = Mathf.Max(r.width / size.x, r.height / size.y) * 1.04f;
            fill.rectTransform.sizeDelta = size * cover;
        }

        public void Update(float dt, bool selected, Vector2 aim)
        {
            loop.Update(selected || Page.T >= 0.004f);
            if (Page.T < 0.004f)
            {
                return;
            }
            Page.Update(dt);
            FillScreen();
            time += dt;
            costShake = Mathf.MoveTowards(costShake, 0f, dt * 2.5f);
            // die Knopf-Ausschnitte zeigen immer das aktuelle Bild (Standbild oder Videoloop)
            if (upgrade.Img.texture != art.texture) upgrade.Img.texture = art.texture;
            if (plaquePatch.Img.texture != texture) plaquePatch.Img.texture = skyLeft.Img.texture = skyRight.Img.texture = texture;
            Refresh();
            UpdateAtmosphere(dt, aim);
            UpdateFx(dt);
        }

        void UpdateFx(float dt)
        {
            fxT += dt;
            // Ring und Licht am Knopf
            float r = Mathf.Clamp01(fxT / 0.6f);
            ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(90f, 520f, MathUtil.EaseOutCubic(r));
            ring.color = Mint.WithAlpha(r < 1f ? 0.45f * (1f - r) * (1f - r) : 0f);
            flash.color = Mint.WithAlpha(0.22f * Mathf.Clamp01(1f - fxT / 0.45f));
            // Stufe: nach kurzer Pause leuchtet das Schild auf, die Zahl springt
            float lt = fxT - 0.12f;
            if (lt >= 0f && !levelKicked) { levelKicked = true; levelPopVel += 9f; }
            MathUtil.Spring(ref levelPop, ref levelPopVel, 0f, 5f, 0.35f, dt);
            float lf = lt >= 0f ? Mathf.Clamp01(1f - lt / 0.8f) : 0f;
            level.rectTransform.localScale = Vector3.one * (1f + levelPop * 0.12f);
            level.color = Color.Lerp(Bronze, new Color(0.05f, 0.4f, 0.16f), lf);
            badgeGlow.color = Mint.WithAlpha(0.2f * lf);
            // Werte: nacheinander grün, der Zuwachs steigt auf
            foreach (var st in stats)
            {
                float t = fxT - st.Delay;
                if (t >= 0f && !st.Kicked) { st.Kicked = true; st.PopVel += 8f; }
                MathUtil.Spring(ref st.Pop, ref st.PopVel, 0f, 5f, 0.35f, dt);
                float f = t >= 0f ? Mathf.Clamp01(1f - t / 0.9f) : 0f;
                st.Value.rectTransform.localScale = Vector3.one * (1f + st.Pop * 0.1f);
                st.Value.color = Color.Lerp(Ink, new Color(0.04f, 0.42f, 0.18f), f);
                st.Glow.color = Mint.WithAlpha(0.13f * f);
                float rise = t >= 0f ? Mathf.Clamp01(t / 0.9f) : 0f;
                bool on = t >= 0f && rise < 1f;
                st.Rise.alpha = on ? Mathf.Clamp01(rise / 0.12f) * (1f - MathUtil.Smooth01((rise - 0.45f) / 0.55f)) : 0f;
                var home = (Vector2)st.Value.rectTransform.anchoredPosition;
                st.Rise.rectTransform.anchoredPosition = home + new Vector2(0f, 20f + MathUtil.EaseOutCubic(rise) * 55f);
                st.Rise.rectTransform.localScale = Vector3.one * (0.8f + 0.2f * MathUtil.EaseOutBack(Mathf.Clamp01(rise * 4f)));
            }
            // Funken steigen aus dem Knopf
            foreach (var s in sparks)
            {
                if (s.Life < 0f) continue;
                s.Age += dt;
                if (s.Age < 0f) { s.Img.color = Color.clear; continue; }
                float u = s.Age / s.Life;
                if (u >= 1f) { s.Life = -1f; s.Img.color = Color.clear; continue; }
                s.Vel *= 1f - dt * 1.4f;
                s.Pos += s.Vel * dt;
                var rt = s.Img.rectTransform;
                rt.anchoredPosition = s.Pos;
                rt.sizeDelta = Vector2.one * s.Size * (1f - 0.6f * u);
                rt.localRotation = Quaternion.Euler(0f, 0f, s.Spin * s.Age);
                s.Img.color = Color.Lerp(Color.white, Mint, 0.55f + 0.45f * u).WithAlpha(0.8f * Mathf.Clamp01(u / 0.1f) * (1f - u));
            }
        }

        void ReleaseArt()
        {
            loop.Stop();
            if (texture == null) return;
            art.texture = null;
            Resources.UnloadAsset(texture);
            texture = null;
        }
    }
}
