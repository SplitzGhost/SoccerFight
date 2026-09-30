using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// SPIELER: die Spielerauswahl nach den Bildentwürfen (Inspiration/CharacterMenuPreviews), aus Ebenen aufgebaut
    /// (tools/newdesign/playerselect.js → <see cref="PlayerSelectArt"/>). Links steht die große Figur vor ihrem
    /// Steinbild am Himmel, rechts Stufe, Name und Klasse, die Klassenfähigkeit, der Klassenbonus, die drei Werte
    /// und die Knöpfe AUSWÄHLEN und UPGRADE; unten die Figurenleiste. Darüber liegt die normale Menüleiste.
    ///
    /// Eine Kachel der Leiste zeigt einen Spieler nur an; erst AUSWÄHLEN macht ihn zum eigenen Spieler (der Knopf
    /// zeigt dann GEWÄHLT, bei noch nicht gekauften Spielern den Preis und führt in den Shop). Die Leiste lässt sich
    /// mit den Pfeilen schieben; weitere Spieler bekommen einfach eine Kachel mehr.
    ///
    /// Alles lebt: Die Figur atmet, verlagert ihr Gewicht und schaut dem Zeiger nach, Zopf und Flechten schwingen
    /// nach (<see cref="FigureWarp"/>). Dunst und Bodennebel ziehen im Wind, Fackeln flackern, Lichtpunkte treiben,
    /// die Ebenen verschieben sich mit dem Zeiger gegeneinander. Knöpfe und Kacheln heben sich unter dem Zeiger
    /// von ihrem Schatten ab. Beim Wechsel tritt die neue Figur ins Bild und federt in den Stand, Name, Fähigkeit
    /// und Werte folgen gestaffelt; ein Upgrade löst die grüne Welle aus (Ring, Funken, „+8“ steigt aus den Werten).
    /// </summary>
    public sealed class CharacterPage
    {
        /// <summary>What each sport plays like (the line under the tabs of the starter pick).</summary>
        public static string SportLine(Sport s) => s == Sport.Basketball
            ? "BASKETBALL  ·  WÜRFE, CROSSOVER UND DUNKS  ·  DER BALL WIRD GEDRIBBELT UND KOMMT NACH JEDEM WURF ZURÜCK"
            : "FUSSBALL  ·  SCHÜSSE, TRICKS UND KOPFBÄLLE  ·  DER BALL KLEBT AM FUSS UND KOMMT NACH JEDEM SCHUSS ZURÜCK";

        /// <summary>The colour a sport is shown in (tabs, the sport line).</summary>
        public static Color SportAccent(Sport s) => s == Sport.Basketball ? Palette.HoopOrange : MenuArt.Accent;

        // ------------------------------------------------------------------ Bausteine

        /// <summary>Ein gemaltes Teil, das sich unter dem Zeiger von seinem Schatten abhebt.</summary>
        sealed class Knob
        {
            public RectTransform Root, Lift;
            public Image Shadow, Glow, Img, Shine;
            public CanvasGroup Group;
            public Vector2 Home, Size;
            public float Delay;

            public Knob(Transform parent, string name, Vector2 pos, Vector2 size, Sprite sprite, bool shine)
            {
                Home = pos; Size = size;
                Root = UiKit.Node(name, parent, pos, size);
                Group = Root.gameObject.AddComponent<CanvasGroup>();
                Shadow = UiKit.Img("Schatten", Root, UiArt.Glow, Color.clear, Vector2.zero, size);
                Glow = UiKit.Img("Licht", Root, UiArt.Glow, Color.clear, Vector2.zero, size + new Vector2(110f, 90f));
                Lift = UiKit.Node("Heben", Root, Vector2.zero, size);
                Img = UiKit.Img("Bild", Lift, sprite, Color.white, Vector2.zero, size);
                if (!shine) return;
                // ein Lichtstreif, der beim Zeigen über das Teil gleitet (auf seine Form begrenzt)
                var mask = UiKit.Img("Glanzform", Lift, sprite, Color.white, Vector2.zero, size);
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                Shine = UiKit.Img("Glanz", mask.transform, UiArt.Glow, Color.clear, Vector2.zero, new Vector2(size.y * 0.55f, size.y * 1.9f));
                Shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -24f);
            }

            /// <param name="h">Zeiger darüber (0..1)</param>
            /// <param name="glow">Farbe des Scheins; breathe: leises Atmen ohne Zeiger (0..1)</param>
            public void Style(float h, float hit, float punch, float time, Color glow, float breathe, float raise = 6f, float grow = 0.035f)
            {
                float press = Mathf.Clamp01(hit);
                Lift.anchoredPosition = new Vector2(0f, h * raise - press * 3f);
                float s = 1f + h * grow + breathe * 0.006f + punch * 0.04f;
                Lift.localScale = new Vector3(s + punch * 0.012f, s - punch * 0.03f, 1f);
                // der Schatten bleibt liegen: je höher das Teil, desto weiter und blasser
                Shadow.rectTransform.anchoredPosition = new Vector2(0f, -7f - h * raise * 1.3f);
                Shadow.rectTransform.sizeDelta = new Vector2(Size.x * 1.02f + 26f + h * 22f, Size.y * 0.9f + 30f + h * 20f);
                Shadow.color = new Color(0.02f, 0.06f, 0.14f, 0.3f - 0.07f * h);
                Glow.color = glow.WithAlpha(h * 0.09f + breathe * 0.035f + press * 0.12f);
                if (Shine == null) return;
                float u = Mathf.Repeat(time * 0.75f, 1.5f);
                Shine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-0.62f, 0.62f, u) * Size.x, 0f);
                Shine.color = Color.white.WithAlpha((h * 0.16f + breathe * 0.03f) * (u < 1f ? Mathf.Sin(u * Mathf.PI) : 0f));
            }
        }

        sealed class Stat
        {
            public Knob Plate;
            public TextMeshProUGUI Value, Delta, Rise;
            public Image Glow;
            public float Shown = -1f, Target, Delay, Pop, PopVel;
            public bool Kicked = true;
        }

        /// <summary>Eine Kachel der Figurenleiste: ein Spieler oder eine Sportart, die noch kommt.</summary>
        sealed class Tile
        {
            public CharacterDef Def;
            public string Soon;
            public Knob Knob;
            public Image Gold, Lock, Check, CheckPlate, Icon;
            public TextMeshProUGUI Label;
            public MenuTarget Target;
            public float X, On, OnVel;
        }

        sealed class Spark
        {
            public Image Img;
            public Vector2 Pos, Vel;
            public float Age, Life = -1f, Size, Spin;
            public Color Tint;
        }

        /// <summary>Schwebender Lichtpunkt (Pollen, Glühwürmchen).</summary>
        sealed class Mote
        {
            public Image Img;
            public Vector2 Pos, Vel;
            public float Age, Life, Size, Phase, Alpha;
            public Color Tint;
        }

        enum Choice { Select, Chosen, Buy }

        // Maße im Entwurf (Bildpunkte)
        const float TileStep = 172.3f, TileY = 835.5f, FirstTileX = 353f;
        /// <summary>Sichtfenster der Leiste; an beiden Enden läuft sie über ViewSoft weich aus (die nächste Kachel scheint durch).</summary>
        const float ViewLeft = 167f, ViewRight = 1390f, ViewSoft = 90f;
        static readonly Color Navy = new Color(0.035f, 0.075f, 0.2f);
        static readonly Color Ink = new Color(0.13f, 0.07f, 0.015f);
        /// <summary>Das Upgrade-Grün: frisch, nicht grell (linearer Farbraum + Bloom).</summary>
        static readonly Color Mint = new Color(0.36f, 1f, 0.58f);
        static readonly Color Sun = new Color(1f, 0.86f, 0.45f);

        static Material valueMat, levelMat, deltaMat, headMat;
        static Texture2D fogTex;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { valueMat = levelMat = deltaMat = headMat = null; fogTex = null; }

        MenuNav nav;
        SubPage page;
        CharacterDef shown, pendingShow;
        float time, enter, swap = 99f;

        // Welt
        RectTransform world, figRoot, backMask, fxRoot;
        RawImage scene, backOld, backNew, fill;
        RectMask2D edge, backSoft;
        RenderTexture[] blur;
        FigureWarp figure;
        Image figShadow;
        float figIn, figInVel, figOut, land, landVel, breathPhase, lookX, lookXVel, lookY, lookYVel;
        float[] swing, swingVel;
        float lastSway, lastSwayVel;
        bool figSwapped = true;
        Vector2 parallax, parallaxVel, backHome;
        // Atmosphäre
        RectTransform hazeLayer, fogLayer, moteLayer;
        RawImage haze, fogA, fogB;
        readonly List<Mote> motes = new List<Mote>();
        readonly List<(Image img, float phase, float size)> torches = new List<(Image, float, float)>();
        float wind, drift;

        // Tafel
        Knob badge, title, ability, select, upgrade, arrowNext, arrowPrev;
        Image titleImg, selectBlank, selectIcon, levelLock, divider;
        TextMeshProUGUI level, titleName, titleRole, traitTitle, traitText, selectLabel, selectPrice, cost;
        RectTransform trait;
        CanvasGroup traitGroup;
        readonly Stat[] stats = new Stat[3];
        MenuTarget selectTarget, upgradeTarget, abilityTarget, nextTarget, prevTarget;
        float levelPop, levelPopVel, costShake, chosenT, chosenVel, buyT;
        int shownLevel = -1, shownGems = -1;
        bool affordable, maxed, levelKicked = true;
        Choice choice;
        float fxT = 99f, pickT = 99f;
        Image ring, flash, pickRing;
        readonly List<Spark> sparks = new List<Spark>();

        // Figurenleiste
        RectTransform view, row;
        readonly List<Tile> tiles = new List<Tile>();
        float scroll, scrollVel, scrollTarget, scrollMax;

        readonly PlayerStats now = new PlayerStats(), next = new PlayerStats();

        /// <summary>AUSWÄHLEN bei einem Spieler, den man noch nicht hat: das Menü öffnet den Shop auf ihm.</summary>
        public System.Action<ShopItem> ShowInShop;

        public SubPage Page => page;
        /// <summary>Der gerade gezeigte Spieler (muss nicht der gewählte sein).</summary>
        public CharacterDef Shown => shown;

        static Vector2 At(float x, float y) => PlayerSelectArt.At(x, y);

        // ------------------------------------------------------------------ Aufbau

        public void Build(RectTransform parent, MenuNav menu)
        {
            nav = menu;
            if (valueMat == null)
            {
                valueMat = NumberMat(0.22f, new Color(1f, 1f, 1f, 0.5f), -0.9f, Color.clear, 0f);
                levelMat = NumberMat(0.2f, new Color(0.16f, 0.05f, 0.02f, 0.7f), -1.2f, new Color(0.2f, 0.07f, 0.03f, 1f), 0.26f);
                deltaMat = NumberMat(0.2f, new Color(0f, 0.08f, 0.04f, 0.6f), -0.8f, new Color(0.02f, 0.2f, 0.1f, 0.95f), 0.2f);
                headMat = NumberMat(0.34f, Color.clear, 0f, Color.clear, 0f);
            }
            page = new SubPage(parent, MenuPage.Characters, "SPIELER", "", MenuArt.Accent, nav.Register, nav.Back, true);
            page.ArtworkSize = PlayerSelectArt.Size;
            page.Content.sizeDelta = page.ArtworkSize;
            // Außerhalb von 16:9 füllt die Kulisse, stark weichgezeichnet und abgedunkelt, den Rest des Bildschirms.
            MenuUi.Stretch(UiKit.Img("Rand", page.Root, null, new Color(0.03f, 0.12f, 0.2f), Vector2.zero, Vector2.zero).rectTransform);
            fill = UiKit.Node("Bildfüllung", page.Root, Vector2.zero, page.ArtworkSize).gameObject.AddComponent<RawImage>();
            fill.raycastTarget = false;
            fill.color = new Color(0.62f, 0.68f, 0.74f);
            page.Content.SetAsLastSibling();
            edge = page.Content.gameObject.AddComponent<RectMask2D>();

            BuildWorld();
            BuildPanel();
            BuildRoster();
            BuildFx();
        }

        /// <summary>Kräftige Zahl: verdickte Buchstaben, darunter eine helle (geprägt) oder dunkle Kante.</summary>
        static Material NumberMat(float dilate, Color underlay, float offsetY, Color outline, float outlineW)
        {
            var src = UiArt.FontBold != null ? UiArt.FontBold.material : null;
            if (src == null) return null;
            var m = new Material(src) { name = "Spielerwerte" };
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

        TextMeshProUGUI Number(string name, Transform parent, Vector2 pos, float size, Vector2 box, Color color, Material mat,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var text = UiKit.Label(name, parent, "", size, color, align, pos, box);
            if (UiArt.FontBold != null) text.font = UiArt.FontBold;
            if (mat != null) text.fontSharedMaterial = mat;
            text.fontStyle = FontStyles.Bold;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * 0.55f;
            text.fontSizeMax = size;
            return text;
        }

        /// <summary>
        /// Fette Überschrift in der normalen Menüschrift. Die gemalte Schrift der Probebilder hat unsaubere Buchstaben
        /// (K, F, Ä) und keine Ziffern; neben den sauber gemalten Namen und Knöpfen dieser Seite fiele das auf.
        /// </summary>
        static TextMeshProUGUI Head(string name, Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box, TextAlignmentOptions align, Material mat = null)
        {
            var t = UiKit.Label(name, parent, text, size, color, align, pos, box, true, 1f);
            if (mat == null) mat = headMat;
            if (mat != null) t.fontSharedMaterial = mat;
            t.fontStyle = FontStyles.Bold;
            t.enableAutoSizing = true;
            t.fontSizeMin = size * 0.6f;
            t.fontSizeMax = size;
            return t;
        }

        static RawImage Raw(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var img = UiKit.Node(name, parent, pos, size).gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            return img;
        }

        // ------------------------------------------------------------------ Welt: Kulisse, Steinbild, Figur, Luft

        void BuildWorld()
        {
            Vector2 size = page.ArtworkSize;
            world = UiKit.Node("Welt", page.Content, Vector2.zero, size);
            scene = Raw("Kulisse", world, Vector2.zero, size);
            // das Steinbild des Spielers: ein Ausschnitt über der Kulisse, Ränder weich; beim Wechsel blendet der neue über den alten
            var b = PlayerSelectArt.Back;
            backHome = PlayerSelectArt.Center(b);
            backMask = UiKit.Node("Steinbild", world, backHome, new Vector2(b.w, b.h));
            backSoft = backMask.gameObject.AddComponent<RectMask2D>();
            backSoft.softness = new Vector2Int(24, 12);
            backOld = Raw("Vorher", backMask, Vector2.zero, new Vector2(b.w, b.h));
            backNew = Raw("Jetzt", backMask, Vector2.zero, new Vector2(b.w, b.h));
            backOld.enabled = backNew.enabled = false;

            // ferner Dunst über den Ruinen, Fackeln, Lichtpunkte – alles hinter der Figur
            hazeLayer = FogLayer("Dunst", HazeHome, new Vector2(1180f, 230f), 220);
            haze = Fog(hazeLayer, new Color(1f, 1f, 1f, 0.2f));
            (float x, float y, float s)[] flames = { (20f, 512f, 190f), (214f, 590f, 170f), (132f, 470f, 120f), (331f, 610f, 110f), (658f, 668f, 90f), (803f, 662f, 90f) };
            foreach (var f in flames)
                torches.Add((UiKit.Img("Fackelschein", world, UiArt.Glow, Color.clear, At(f.x, f.y), Vector2.one * f.s), Random.value * 10f, f.s));
            moteLayer = UiKit.Node("Lichtpunkte", world, Vector2.zero, size);
            for (int i = 0; i < 22; i++)
            {
                var m = new Mote { Img = UiKit.Img("Lichtpunkt", moteLayer, UiArt.Glow, Color.clear, Vector2.zero, Vector2.one * 8f) };
                Respawn(m, true);
                motes.Add(m);
            }

            figRoot = UiKit.Node("Figur", world, Vector2.zero, size);
            figShadow = UiKit.Img("Bodenschatten", figRoot, UiArt.Glow, Color.clear, Vector2.zero, new Vector2(420f, 46f));
            var go = new GameObject("Bild", typeof(RectTransform));
            go.transform.SetParent(figRoot, false);
            figure = go.AddComponent<FigureWarp>();
            figure.raycastTarget = false;

            // Bodennebel zieht vor den Füßen vorbei (nur links: nie über Werte und Knöpfe)
            fogLayer = FogLayer("Bodennebel", FogHome, new Vector2(1240f, 170f), 280);
            fogA = Fog(fogLayer, new Color(1f, 1f, 1f, 0.2f));
            fogB = Fog(fogLayer, new Color(1f, 1f, 1f, 0.12f));
        }

        static readonly Vector2 HazeHome = PlayerSelectArt.At(430f, 640f), FogHome = PlayerSelectArt.At(430f, 742f);

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
            fogTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "Spieler-Nebel", filterMode = FilterMode.Bilinear };
            fogTex.wrapModeU = TextureWrapMode.Repeat;
            fogTex.wrapModeV = TextureWrapMode.Clamp;
            fogTex.SetPixels32(px);
            fogTex.Apply(false, true);
            return fogTex;
        }

        /// <summary>Eine Nebelebene: an den Seiten weich ausgeblendet.</summary>
        RectTransform FogLayer(string name, Vector2 home, Vector2 size, int soft)
        {
            var layer = UiKit.Node(name, world, home, size);
            layer.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(soft, 0);
            return layer;
        }

        static RawImage Fog(RectTransform layer, Color tint)
        {
            var img = Raw("Schwaden", layer, Vector2.zero, layer.sizeDelta);
            img.texture = FogTexture();
            img.color = tint;
            return img;
        }

        /// <summary>Ein Lichtpunkt beginnt neu irgendwo links der Tafel (beim Aufbau schon mitten im Leben).</summary>
        static void Respawn(Mote m, bool anyAge)
        {
            m.Pos = At(Random.Range(-20f, 960f), Random.Range(200f, 760f));
            m.Vel = new Vector2(Random.Range(-6f, 6f), Random.Range(2f, 12f));
            m.Life = Random.Range(4f, 8f);
            m.Age = anyAge ? Random.Range(0f, m.Life) : 0f;
            m.Size = Random.Range(6f, 12f);
            m.Phase = Random.value * 10f;
            m.Alpha = Random.Range(0.45f, 0.8f);
            m.Tint = Random.value < 0.6f ? new Color(1f, 0.9f, 0.6f) : Color.white;
        }

        // ------------------------------------------------------------------ Tafel rechts

        Knob Part(string name, Transform parent, bool shine, float delay)
        {
            var p = PlayerSelectArt.Get(name);
            var k = new Knob(parent, name, PlayerSelectArt.Center(p), new Vector2(p.w, p.h), PlayerSelectArt.Sprite(name), shine) { Delay = delay };
            return k;
        }

        void BuildPanel()
        {
            var ui = UiKit.Node("Tafel", page.Content, Vector2.zero, page.ArtworkSize);

            // Stufe im Bronze-Abzeichen
            badge = Part("badge", ui, false, 0.04f);
            level = Number("Stufe", badge.Lift, At(1095f, 256f) - badge.Home, 58f, new Vector2(78f, 64f), new Color(1f, 0.8f, 0.62f), levelMat);
            levelLock = UiKit.Img("Schloss", badge.Lift, MenuArt.IconLock, new Color(1f, 0.82f, 0.66f), At(1095f, 256f) - badge.Home, new Vector2(40f, 40f));
            levelLock.preserveAspect = true;

            // Name und Klasse: das gemalte Schriftbild des Spielers; ohne eines (neue Spieler) normale Schrift
            var t = PlayerSelectArt.Get("title_rio");
            title = new Knob(ui, "Name", PlayerSelectArt.Center(t), new Vector2(t.w, t.h), null, false) { Delay = 0.08f };
            titleImg = title.Img;
            titleName = Head("Name", title.Lift, "", 70f, Navy, Vector2.zero, new Vector2(440f, 78f), TextAlignmentOptions.Left);
            titleRole = Head("Klasse", title.Lift, "", 35f, Navy, Vector2.zero, new Vector2(440f, 42f), TextAlignmentOptions.Left);

            // Klassenfähigkeit (Medaillon mit Namen darunter)
            var a = PlayerSelectArt.Get("ability_rio");
            ability = new Knob(ui, "Fähigkeit", PlayerSelectArt.Center(a), new Vector2(a.w, a.h), null, true) { Delay = 0.13f };
            abilityTarget = new MenuTarget { Id = "player_ability", Root = ability.Root, Size = new Vector2(150f, 160f), Page = MenuPage.Characters,
                Action = AbilityInfo, Accent = MenuArt.Accent, Draw = DrawAbility };
            nav.Register(abilityTarget);

            // Klassenbonus: Trennstrich, Überschrift, Text (aus ClassDefs)
            trait = UiKit.Node("Klassenbonus", ui, Vector2.zero, page.ArtworkSize);
            traitGroup = trait.gameObject.AddComponent<CanvasGroup>();
            divider = UiKit.Img("Strich", trait, null, Navy.WithAlpha(0.9f), At(1185.5f, 432f), new Vector2(3.5f, 124f));
            traitTitle = Head("Überschrift", trait, "", 34f, Navy, At(1222f + 215f, 386f), new Vector2(430f, 42f), TextAlignmentOptions.Left);
            traitText = UiKit.Label("Text", trait, "", 25.5f, Navy, TextAlignmentOptions.TopLeft, At(1224f + 212f, 408f + 50f), new Vector2(424f, 100f));
            if (UiArt.FontBold != null) traitText.fontSharedMaterial = UiArt.FontBold.material;
            traitText.textWrappingMode = TextWrappingModes.Normal;
            traitText.lineSpacing = -8f;

            // Werte: Platte mit gemaltem Symbol und Namen, die Zahl schreibt das Spiel
            string[] plates = { "stat_hp", "stat_damage", "stat_speed" };
            for (int i = 0; i < 3; i++)
            {
                var st = new Stat { Delay = 0.22f + i * 0.07f };
                st.Plate = Part(plates[i], ui, false, 0.2f + i * 0.045f);
                var p = PlayerSelectArt.Get(plates[i]);
                // die Platte liegt mit 3 Punkten Rand im Bild: Zahl 129 rechts der Kante, wie im Entwurf
                Vector2 at = At(p.x + 129f, 602f) - st.Plate.Home;
                st.Glow = UiKit.Img("Wert-Licht", st.Plate.Lift, UiArt.Glow, Color.clear, at, new Vector2(210f, 130f));
                st.Value = Number("Wert", st.Plate.Lift, at, 36f, new Vector2(100f, 40f), Navy, valueMat);
                st.Delta = Number("Vorschau", st.Plate.Root, At(p.x + 146f, 537f) - st.Plate.Home, 23f, new Vector2(90f, 28f), Mint, deltaMat);
                st.Rise = Number("Zuwachs", st.Plate.Root, at, 27f, new Vector2(110f, 32f), Mint, deltaMat);
                st.Delta.alpha = st.Rise.alpha = 0f;
                stats[i] = st;
            }

            // AUSWÄHLEN: die gemalte Platte; gewählt oder noch nicht gekauft trägt die leere Platte eigene Schrift
            select = Part("select", ui, true, 0.32f);
            selectBlank = UiKit.Img("Leer", select.Lift, PlayerSelectArt.Sprite("select_blank"), Color.clear, Vector2.zero, select.Size);
            selectBlank.transform.SetSiblingIndex(select.Img.transform.GetSiblingIndex() + 1);
            var words = UiKit.Node("Schrift", select.Lift, new Vector2(0f, -1f), select.Size);
            selectIcon = UiKit.Img("Zeichen", words, MenuArt.IconCheck, Color.clear, Vector2.zero, new Vector2(40f, 40f));
            selectIcon.preserveAspect = true;
            selectLabel = Head("Beschriftung", words, "GEWÄHLT", 44f, Ink, Vector2.zero, new Vector2(230f, 56f), TextAlignmentOptions.Center);
            selectPrice = Head("Preis", words, "", 40f, Ink, Vector2.zero, new Vector2(104f, 50f), TextAlignmentOptions.Left);
            selectTarget = new MenuTarget { Id = "player_select", Root = select.Root, Size = new Vector2(select.Size.x - 14f, select.Size.y - 14f), Page = MenuPage.Characters,
                Action = Choose, Accent = MetaUi.Gold, Draw = DrawSelect };
            nav.Register(selectTarget);

            // UPGRADE mit Kristallpreis
            upgrade = Part("upgrade", ui, true, 0.38f);
            cost = Number("Kristallpreis", upgrade.Lift, At(1578f, 713f) - upgrade.Home, 36f, new Vector2(78f, 42f), Color.white, CurrencyBar.Numbers, TextAlignmentOptions.Left);
            upgradeTarget = new MenuTarget { Id = "player_upgrade", Root = upgrade.Root, Size = new Vector2(upgrade.Size.x - 12f, upgrade.Size.y - 14f), Page = MenuPage.Characters,
                Action = Upgrade, Accent = Mint, Draw = DrawUpgrade };
            nav.Register(upgradeTarget);
        }

        // ------------------------------------------------------------------ Figurenleiste

        void BuildRoster()
        {
            float cx = (ViewLeft + ViewRight) * 0.5f;
            view = UiKit.Node("Figurenleiste", page.Content, At(cx, TileY), new Vector2(ViewRight - ViewLeft, 190f));
            view.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int((int)(ViewSoft * 2f), 0);   // volle Deckkraft ab der halben Weichheit
            row = UiKit.Node("Reihe", view, Vector2.zero, view.sizeDelta);

            int n = 0;
            foreach (var def in RosterOrder())
            {
                var tile = new Tile { Def = def, X = FirstTileX + n * TileStep };
                var blue = PlayerSelectArt.Get("tile_" + def.Id);
                bool painted = blue != null;
                if (!painted) blue = PlayerSelectArt.Get("tile_blank");
                // die Kachel sitzt im Entwurf in ihrem Fach: gleicher Versatz zur Fachmitte in jedem Fach
                int slot = System.Array.IndexOf(PaintedOrder, def.Id);
                Vector2 off = painted ? PlayerSelectArt.Center(blue) - At(FirstTileX + slot * TileStep, TileY) : Vector2.zero;
                tile.Knob = new Knob(row, "Kachel " + def.Name, Vector2.zero, new Vector2(blue.w, blue.h), PlayerSelectArt.Sprite(painted ? "tile_" + def.Id : "tile_blank"), false) { Delay = 0.26f + n * 0.04f };
                tile.Knob.Img.rectTransform.anchoredPosition = off;
                var gold = PlayerSelectArt.Get("tile_" + def.Id + "_on");
                if (painted && gold != null)
                {
                    tile.Gold = UiKit.Img("Gewählt", tile.Knob.Lift, PlayerSelectArt.Sprite("tile_" + def.Id + "_on"), Color.clear,
                        PlayerSelectArt.Center(gold) - At(FirstTileX + slot * TileStep, TileY), new Vector2(gold.w, gold.h));
                }
                else
                {
                    // neuer Spieler ohne gemalte Kachel: leere Kachel mit Namen
                    tile.Label = Head("Name", tile.Knob.Lift, def.Name, 21f, Color.white, new Vector2(0f, -41f), new Vector2(150f, 28f), TextAlignmentOptions.Center, CurrencyBar.Numbers);
                }
                tile.Lock = UiKit.Img("Schloss", tile.Knob.Lift, MenuArt.IconLock, Color.clear, new Vector2(58f, 38f), new Vector2(30f, 30f));
                tile.Lock.preserveAspect = true;
                // der eigene Spieler trägt einen grünen Haken auf dunklem Grund (auch auf der goldenen Kachel gut zu sehen)
                tile.CheckPlate = UiKit.Img("Hakengrund", tile.Knob.Lift, UiArt.Circle, Color.clear, new Vector2(62f, 42f), new Vector2(34f, 34f));
                tile.Check = UiKit.Img("Haken", tile.CheckPlate.transform, MenuArt.IconCheck, Color.clear, Vector2.zero, new Vector2(22f, 22f));
                tile.Check.preserveAspect = true;
                Add(tile, "tile_" + def.Id, () => Show(tile.Def), def.Accent);
                n++;
            }
            // Sportarten, die noch kommen: leere Kacheln hinter dem Pfeil
            (string name, Sprite icon)[] soon = { ("BOXEN", MenuArt.IconBoxing), ("TENNIS", MenuArt.IconTennis) };
            foreach (var s in soon)
            {
                var blank = PlayerSelectArt.Get("tile_blank");
                var tile = new Tile { Soon = s.name + " KOMMT BALD", X = FirstTileX + n * TileStep };
                tile.Knob = new Knob(row, "Kachel " + s.name, Vector2.zero, new Vector2(blank.w, blank.h), PlayerSelectArt.Sprite("tile_blank"), false) { Delay = 0.26f + n * 0.04f };
                tile.Icon = UiKit.Img("Sportart", tile.Knob.Lift, s.icon, Color.white.WithAlpha(0.85f), new Vector2(0f, 19f), new Vector2(62f, 62f));
                tile.Icon.preserveAspect = true;
                tile.Label = Head("Name", tile.Knob.Lift, s.name, 21f, Color.white, new Vector2(0f, -41f), new Vector2(150f, 28f), TextAlignmentOptions.Center, CurrencyBar.Numbers);
                Add(tile, "tile_soon_" + s.name.ToLowerInvariant(), null, MetaUi.Muted);
                n++;
            }
            scrollMax = Mathf.Max(0f, FirstTileX + (n - 1) * TileStep + TileStep * 0.5f + 8f - (ViewRight - ViewSoft));

            // Pfeile: schieben die Leiste, wenn nicht alle Kacheln hineinpassen
            var ar = PlayerSelectArt.Get("arrow");
            arrowNext = new Knob(page.Content, "Weiter", At(ViewRight + 4f, TileY - 4f), new Vector2(ar.w, ar.h), PlayerSelectArt.Sprite("arrow"), false) { Delay = 0.5f };
            arrowPrev = new Knob(page.Content, "Zurück", At(ViewLeft + ViewSoft - 26f, TileY - 4f), new Vector2(ar.w, ar.h), PlayerSelectArt.Sprite("arrow"), false) { Delay = 0.5f };
            arrowPrev.Img.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            foreach (var k in new[] { arrowNext, arrowPrev })
            {
                // dunkle runde Platte hinter dem Zeichen, damit es vor Gras und Pflaster steht
                var plate = UiKit.Img("Platte", k.Lift, UiArt.Circle, new Color(0.02f, 0.07f, 0.15f, 0.55f), Vector2.zero, new Vector2(54f, 54f));
                plate.transform.SetAsFirstSibling();
            }
            nextTarget = new MenuTarget { Id = "roster_next", Root = arrowNext.Root, Size = new Vector2(58f, 66f), Page = MenuPage.Characters,
                Action = () => Scroll(1), Accent = MenuArt.Accent, Draw = t => DrawArrow(arrowNext, t), Visible = () => CanScroll(1) };
            prevTarget = new MenuTarget { Id = "roster_prev", Root = arrowPrev.Root, Size = new Vector2(58f, 66f), Page = MenuPage.Characters,
                Action = () => Scroll(-1), Accent = MenuArt.Accent, Draw = t => DrawArrow(arrowPrev, t), Visible = () => CanScroll(-1) };
            nav.Register(nextTarget);
            nav.Register(prevTarget);
        }

        /// <summary>Reihenfolge der gemalten Kacheln im Entwurf (ihr Fach bestimmt den Versatz des Bildes).</summary>
        static readonly string[] PaintedOrder = { "rio", "mira", "bruno", "dre", "nova", "titan" };

        /// <summary>Die Spieler in der Reihenfolge des Entwurfs, neue dahinter.</summary>
        static IEnumerable<CharacterDef> RosterOrder()
        {
            foreach (var id in PaintedOrder) { var d = Characters.Get(id); if (d != null) yield return d; }
            foreach (var d in Characters.All) if (System.Array.IndexOf(PaintedOrder, d.Id) < 0) yield return d;
        }

        void Add(Tile tile, string id, System.Action action, Color accent)
        {
            tiles.Add(tile);
            tile.Target = new MenuTarget { Id = id, Root = tile.Knob.Root, Size = new Vector2(162f, 122f), Page = MenuPage.Characters,
                Action = action, Soon = tile.Soon, Accent = accent, Draw = t => DrawTile(tile, t), Visible = () => InView(tile) };
            nav.Register(tile.Target);
        }

        /// <summary>Liegt die Kachel (fast) ganz im sichtbaren Teil der Leiste?</summary>
        float InView(Tile tile)
        {
            float x = tile.X - scroll;
            return x > ViewLeft + ViewSoft + 50f && x < ViewRight - ViewSoft - 50f ? 1f : 0f;
        }

        float CanScroll(int dir) => dir > 0 ? (scrollTarget < scrollMax - 1f ? 1f : 0f) : (scrollTarget > 1f ? 1f : 0f);

        void Scroll(int dir) => scrollTarget = Mathf.Clamp(scrollTarget + dir * TileStep * 2f, 0f, scrollMax);

        /// <summary>Die Leiste so weit schieben, dass der gezeigte Spieler ganz zu sehen ist.</summary>
        void Reveal(CharacterDef def, bool snap)
        {
            foreach (var t in tiles)
            {
                if (t.Def != def) continue;
                float left = t.X - TileStep * 0.5f - (ViewLeft + ViewSoft + 8f), right = t.X + TileStep * 0.5f - (ViewRight - ViewSoft + 2f);
                scrollTarget = Mathf.Clamp(Mathf.Clamp(scrollTarget, right, left), 0f, scrollMax);
            }
            if (snap) { scroll = scrollTarget; scrollVel = 0f; }
        }

        // ------------------------------------------------------------------ Effekte über allem

        void BuildFx()
        {
            fxRoot = UiKit.Node("Effekte", page.Content, Vector2.zero, page.ArtworkSize);
            Vector2 at = upgrade.Home;
            flash = UiKit.Img("Welle-Licht", fxRoot, UiArt.Glow, Color.clear, at, new Vector2(420f, 420f));
            ring = UiKit.Img("Welle", fxRoot, MenuArt.Shock, Color.clear, at, new Vector2(100f, 100f));
            pickRing = UiKit.Img("Auswahl-Ring", fxRoot, MenuArt.Shock, Color.clear, Vector2.zero, new Vector2(100f, 100f));
            for (int i = 0; i < 26; i++)
                sparks.Add(new Spark { Img = UiKit.Img("Funke", fxRoot, MenuArt.Sparkle, Color.clear, Vector2.zero, new Vector2(20f, 20f)) });
        }

        void Burst(Vector2 from, Color tint, int count, float spread, float up)
        {
            int made = 0;
            foreach (var s in sparks)
            {
                if (s.Life >= 0f) continue;
                float a = Random.Range(-0.9f, 0.9f);
                s.Pos = from + new Vector2(Mathf.Sin(a) * spread, Random.Range(-16f, 26f));
                s.Vel = new Vector2(Mathf.Sin(a) * Random.Range(40f, 130f), Random.Range(0.45f, 1f) * up);
                s.Age = -Random.Range(0f, 0.15f);
                s.Life = Random.Range(0.7f, 1.15f);
                s.Size = Random.Range(12f, 26f);
                s.Spin = Random.Range(-200f, 200f);
                s.Tint = tint;
                if (++made >= count) break;
            }
        }

        // ------------------------------------------------------------------ Öffnen, Zeigen, Wählen, Upgrade

        /// <summary>Die Seite öffnet: der gewählte Spieler (oder der angegebene) steht da, alles fährt gestaffelt ein.</summary>
        public void Open(CharacterDef def = null)
        {
            enter = 0f;
            fxT = pickT = 99f;
            pendingShow = null;
            Apply(def ?? Characters.Current);
            swap = 99f;
            figOut = 0f;
            figIn = 0f; figInVel = 0f;
            figSwapped = true;
            Reveal(shown, true);
            foreach (var st in stats) st.Shown = -1f;
            shownLevel = shownGems = -1;
            Refresh();
        }

        /// <summary>Einen Spieler anzeigen (Kachel getroffen): die Figur wechselt, Name und Werte folgen.</summary>
        public void Show(CharacterDef def)
        {
            if (def == null) return;
            if (def == shown && pendingShow == null) { land = 0.05f; return; }
            pendingShow = def;
            swap = 0f;
            figSwapped = false;
            Reveal(def, false);
        }

        /// <summary>Texte, Schriftbild und Medaillon auf den Spieler setzen, Figur und Steinbild laden.</summary>
        void Apply(CharacterDef def)
        {
            bool first = shown == null;
            shown = def;
            var cls = def.ClassDef;

            var t = PlayerSelectArt.Get("title_" + def.Id);
            bool painted = t != null;
            titleImg.enabled = painted;
            titleName.gameObject.SetActive(!painted);
            titleRole.gameObject.SetActive(!painted);
            if (painted)
            {
                titleImg.sprite = PlayerSelectArt.Sprite("title_" + def.Id);
                title.Size = new Vector2(t.w, t.h);
                title.Home = PlayerSelectArt.Center(t);
                title.Root.sizeDelta = title.Lift.sizeDelta = titleImg.rectTransform.sizeDelta = title.Size;
            }
            else
            {
                title.Home = At(1172f + 220f, 266f);
                title.Root.sizeDelta = title.Lift.sizeDelta = title.Size = new Vector2(440f, 116f);
                titleName.text = def.Name;
                titleRole.text = def.Role;
                titleName.rectTransform.anchoredPosition = new Vector2(0f, 22f);
                titleRole.rectTransform.anchoredPosition = new Vector2(0f, -38f);
            }

            var a = PlayerSelectArt.Get("ability_" + def.Id);
            if (a != null)
            {
                var sprite = PlayerSelectArt.Sprite("ability_" + def.Id);
                ability.Img.sprite = sprite;
                ability.Lift.GetChild(1).GetComponent<Image>().sprite = sprite;   // Form des Lichtstreifs
                ability.Size = new Vector2(a.w, a.h);
                ability.Home = PlayerSelectArt.Center(a);
                ability.Root.sizeDelta = ability.Lift.sizeDelta = ability.Img.rectTransform.sizeDelta = ability.Size;
                ability.Lift.GetChild(1).GetComponent<RectTransform>().sizeDelta = ability.Size;
            }
            ability.Img.enabled = a != null;

            traitTitle.text = cls.TraitName;
            traitText.text = cls.Bonus;

            // Steinbild: das neue blendet über das alte
            var back = PlayerSelectArt.Texture("back_" + def.Id);
            backOld.texture = first ? back : backNew.texture;
            backOld.enabled = backOld.texture != null;
            backNew.texture = back;
            backNew.enabled = back != null;
            if (scene.texture == null) scene.texture = PlayerSelectArt.Texture("scene");

            var f = PlayerSelectArt.FigureOf(def.Id);
            var tex = f != null ? PlayerSelectArt.Texture("figure_" + def.Id) : null;
            figure.enabled = tex != null;
            if (tex != null)
            {
                figure.Set(tex, f);
                figShadow.rectTransform.anchoredPosition = At(f.x + f.w * 0.5f, f.ground - 2f);
                figShadow.rectTransform.sizeDelta = new Vector2(f.w * 0.9f, 44f);
            }
            swing = new float[figure.SwingCount];
            swingVel = new float[figure.SwingCount];
            breathPhase = Random.value * 6f;
        }

        /// <summary>AUSWÄHLEN getroffen.</summary>
        void Choose()
        {
            var def = pendingShow ?? shown;
            if (def == null) return;
            if (!Profile.OwnsCharacter(def.Id))
            {
                buyT = 1f;
                ShowInShop?.Invoke(Shop.ForCharacter(def));
                return;
            }
            if (Characters.Current == def) { nav.Say(def.Name + " SPIELT SCHON", select.Root); return; }
            Characters.Select(Characters.IndexOf(def));
            if (Game.I != null && Game.I.Run != null) Game.I.Run.Rebuild();
            if (Game.I != null && Game.I.Player != null) Game.I.Player.ApplyStats(false);
            // die Figur freut sich kurz: ein kleiner Hüpfer, goldener Ring am Boden, Funken steigen auf
            pickT = 0f;
            landVel -= 1.1f;
            var f = PlayerSelectArt.FigureOf(def.Id);
            Vector2 feet = f != null ? At(f.x + f.w * 0.5f, f.ground) : At(540f, 742f);
            pickRing.rectTransform.anchoredPosition = feet;
            Burst(feet + new Vector2(0f, 30f), Sun, 14, 150f, 330f);
            nav.Say(def.Name + " IST DEIN SPIELER", select.Root);
        }

        void AbilityInfo()
        {
            if (shown == null) return;
            var lines = shown.ClassDef.Strengths;
            if (lines != null && lines.Length > 0) nav.Say(lines[lines.Length - 1], ability.Root);
        }

        void Upgrade()
        {
            var def = shown;
            if (def == null) return;
            if (!Profile.OwnsCharacter(def.Id)) { costShake = 1f; nav.Say("ERST KAUFEN, DANN VERBESSERN", upgrade.Root); return; }
            int current = CharacterProgression.Level(def);
            if (current >= CharacterProgression.MaxLevel) { nav.Say("MAXIMALE STUFE ERREICHT", upgrade.Root); return; }
            int price = CharacterProgression.UpgradeCost(current);
            if (!CharacterProgression.TryUpgrade(def))
            {
                costShake = 1f;
                nav.Say("DIR FEHLEN " + Currencies.Format(Mathf.Max(0, price - Wallet.Get(Currencies.Gems))) + " KRISTALLE", upgrade.Root);
                return;
            }
            if (Game.I != null && Game.I.Run != null) Game.I.Run.Rebuild();
            if (Game.I != null && Game.I.Player != null) Game.I.Player.ApplyStats(false);
            // die Zuwächse steigen gleich in Grün aus den Werten auf
            foreach (var st in stats) { st.Rise.text = st.Delta.text; st.Kicked = false; }
            levelKicked = false;
            fxT = 0f;
            Burst(upgrade.Home + new Vector2(0f, 6f), Mint, 18, 60f, 340f);
            Refresh();
        }

        static string Decimal(float v, string format) => v.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',');

        static string Format(int i, float v) => i == 0 ? Mathf.RoundToInt(v).ToString() : i == 1 ? Decimal(v, "0.#") : Decimal(v, "0.0");

        /// <summary>Stufe, Preis und Werte des gezeigten Spielers (nur wenn sich etwas geändert hat).</summary>
        void Refresh()
        {
            if (shown == null) return;
            bool own = Profile.OwnsCharacter(shown.Id);
            int n = own ? CharacterProgression.Level(shown) : 0;
            int g = Wallet.Get(Currencies.Gems);
            if (n == shownLevel && g == shownGems) return;
            shownLevel = n; shownGems = g;
            int lvl = Mathf.Max(1, n);
            Measure(now, lvl, out float hp, out float dmg, out float spd);
            level.text = own ? n.ToString() : "";
            levelLock.enabled = !own;
            stats[0].Target = hp; stats[1].Target = dmg; stats[2].Target = spd;
            maxed = own && n >= CharacterProgression.MaxLevel;
            int price = CharacterProgression.UpgradeCost(lvl);
            affordable = own && !maxed && g >= price;
            cost.text = maxed ? "MAX" : price.ToString();
            cost.color = maxed || affordable || !own ? Color.white : MetaUi.Danger;
            if (maxed) { foreach (var s in stats) s.Delta.text = ""; return; }
            // Vorschau der nächsten Stufe (erscheint beim Zeigen auf den Upgrade-Knopf)
            Measure(next, lvl + 1, out float hp1, out float dmg1, out float spd1);
            stats[0].Delta.text = "+" + Mathf.RoundToInt(hp1 - Mathf.RoundToInt(hp));
            stats[1].Delta.text = "+" + Decimal(Mathf.Max(0.1f, dmg1 - dmg), "0.0");
            stats[2].Delta.text = "+" + Decimal(Mathf.Max(0.1f, spd1 - spd), "0.0");
        }

        /// <summary>Dauerhafte Werte des normalen Schusses/Wurfs, ohne zufällige Krits und Laufkarten.</summary>
        void Measure(PlayerStats s, int lvl, out float hp, out float dmg, out float spd)
        {
            s.Reset();
            MetaPassives.Apply(s, shown);
            CharacterProgression.ApplyLevel(s, lvl);
            hp = Player.BaseMaxHp + s.MaxHpBonus;
            dmg = Player.ShotDamage * s.DamageMul * s.ShotDamageMul * s.CategoryMul(SkillCategory.Shot);
            spd = Player.MaxSpeed * s.MoveSpeedMul;
        }

        // ------------------------------------------------------------------ Zeichnen der Ziele

        void DrawAbility(MenuTarget t)
        {
            float h = Mathf.Clamp01(t.Hover);
            ability.Style(h, t.Hit, t.Punch, time, MenuArt.Accent, 0.5f + 0.5f * Mathf.Sin(time * 1.7f), 5f, 0.045f);
        }

        void DrawSelect(MenuTarget t)
        {
            float h = Mathf.Clamp01(t.Hover);
            bool can = choice != Choice.Chosen;
            float breathe = choice == Choice.Select ? 0.5f + 0.5f * Mathf.Sin(time * 2.4f) : 0f;
            select.Style(can ? h : h * 0.45f, t.Hit, t.Punch, time, Sun, breathe, 7f, 0.035f);
        }

        void DrawUpgrade(MenuTarget t)
        {
            float h = Mathf.Clamp01(t.Hover);
            // bezahlbar: der Knopf atmet leise, auch ohne Zeiger
            float breathe = affordable ? 0.5f + 0.5f * Mathf.Sin(time * 2.6f) : 0f;
            float wave = Mathf.Clamp01(1f - fxT / 0.5f);
            Color glow = maxed ? Sun : affordable ? Mint : new Color(0.4f, 0.85f, 1f);
            upgrade.Style(h, t.Hit, t.Punch + wave * 0.6f, time, glow, breathe, 7f, 0.04f);
            upgrade.Lift.anchoredPosition += new Vector2(costShake * Mathf.Sin(time * 60f) * 5f, 0f);
            // Vorschau der Zuwächse über den Platten
            float show = maxed || shown == null || !Profile.OwnsCharacter(shown.Id) ? 0f : h;
            foreach (var st in stats)
            {
                st.Delta.alpha = show * (affordable ? 1f : 0.6f);
                st.Delta.rectTransform.localScale = Vector3.one * (0.85f + 0.15f * show);
            }
        }

        void DrawArrow(Knob k, MenuTarget t)
        {
            float h = Mathf.Clamp01(t.Hover);
            k.Style(h, t.Hit, t.Punch, time, Color.white, 0f, 3f, 0.12f);
            // lädt zum Weiterblättern ein: schwingt sacht in Pfeilrichtung
            float dir = k == arrowNext ? 1f : -1f;
            k.Lift.anchoredPosition += new Vector2(dir * (2.5f * Mathf.Sin(time * 3f) + h * 3f), 0f);
        }

        void DrawTile(Tile tile, MenuTarget t)
        {
            float h = Mathf.Clamp01(t.Hover);
            var def = tile.Def;
            bool on = def != null && def == (pendingShow ?? shown);
            MathUtil.Spring(ref tile.On, ref tile.OnVel, on ? 1f : 0f, 5f, 0.75f, TimeFx.UiDelta);
            float sel = Mathf.Clamp01(tile.On);
            bool soon = def == null;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 2.2f);
            tile.Knob.Style(soon ? h * 0.5f : h, t.Hit, t.Punch, time, on ? Sun : Color.white, sel * pulse * 1.4f, 9f, 0.05f);
            // die gezeigte Kachel steht ein Stück höher und ist golden
            tile.Knob.Lift.anchoredPosition += new Vector2(0f, sel * 5f);
            if (tile.Gold != null) tile.Gold.color = Color.white.WithAlpha(sel);
            bool own = def != null && Profile.OwnsCharacter(def.Id);
            float dim = soon ? 0.62f : own ? 1f : 0.58f;
            tile.Knob.Img.color = new Color(dim, dim, dim, 1f);
            if (tile.Gold != null && !own) tile.Gold.color = new Color(0.78f, 0.78f, 0.78f, sel);
            if (tile.Lock != null) tile.Lock.color = Color.white.WithAlpha(!soon && !own ? 0.95f : 0f);
            bool current = def != null && Characters.Current == def;
            if (tile.Check != null)
            {
                tile.Check.color = Mint.WithAlpha(current ? 1f : 0f);
                tile.CheckPlate.color = new Color(0.02f, 0.1f, 0.08f, current ? 0.85f : 0f);
            }
            if (tile.Icon != null) tile.Icon.rectTransform.localScale = Vector3.one * (1f + 0.05f * h);
            // die angehobene Kachel liegt über ihren Nachbarn
            if ((h > 0.3f || sel > 0.5f && hoveredTiles == 0) && tile.Knob.Root.GetSiblingIndex() != row.childCount - 1) tile.Knob.Root.SetAsLastSibling();
            if (h > 0.3f) hoveredNow++;
        }

        int hoveredTiles, hoveredNow;

        // ------------------------------------------------------------------ Bildschirmformate

        /// <summary>Die Kulisse in drei Schritten auf 26 × 15 Punkte verkleinert: vergrößert ergibt das weiche Farbflächen.</summary>
        Texture Blurred(Texture source)
        {
            if (blur != null) return blur[2];
            blur = new RenderTexture[3];
            for (int i = 0; i < 3; i++)
                blur[i] = new RenderTexture(416 >> (i * 2), 234 >> (i * 2), 0, RenderTextureFormat.ARGB32) { name = "Spieler-Unschärfe " + i, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 3; i++) Graphics.Blit(i == 0 ? source : blur[i - 1], blur[i]);
            return blur[2];
        }

        /// <summary>Breiter oder höher als 16:9: das Bild steht ganz in der Mitte, seine vergrößerte Kopie füllt den Rest.</summary>
        void FillScreen()
        {
            Rect r = page.Root.rect;
            Vector2 size = page.ArtworkSize;
            float fit = page.Content.localScale.x;
            float barX = r.width - size.x * fit, barY = r.height - size.y * fit;
            bool bars = barX > 2f || barY > 2f;
            if (fill.enabled != bars) fill.enabled = bars;
            edge.softness = new Vector2Int(barX > 2f ? 110 : 0, barY > 2f ? 110 : 0);
            // das Steinbild hat seine eigene Maske und reicht bis an den oberen Bildrand: dort genauso weich auslaufen
            backSoft.softness = new Vector2Int(24, barY > 2f ? 110 : 12);
            // ohne Rand bleibt der kleine Überstand der Kamerabewegung unbeschnitten
            edge.padding = bars ? Vector4.zero : Vector4.one * -60f;
            if (!bars) return;
            if (scene.texture == null) { fill.enabled = false; return; }
            if (fill.texture == null) fill.texture = Blurred(scene.texture);
            float cover = Mathf.Max(r.width / size.x, r.height / size.y) * 1.04f;
            fill.rectTransform.sizeDelta = size * cover;
        }

        // ------------------------------------------------------------------ Ablauf pro Bild

        public void Update(float dt, Vector2 aim)
        {
            if (page.T < 0.004f)
            {
                // zu: die großen Bilder wieder freigeben
                if (scene.texture != null)
                {
                    scene.texture = backOld.texture = backNew.texture = null;
                    figure.enabled = false;
                    shown = null;
                    PlayerSelectArt.Release();
                }
                return;
            }
            if (shown == null) Open();
            page.Update(dt);
            FillScreen();
            time += dt;
            enter += dt;
            swap += dt;
            costShake = Mathf.MoveTowards(costShake, 0f, dt * 2.5f);
            buyT = Mathf.MoveTowards(buyT, 0f, dt * 2f);
            hoveredTiles = hoveredNow; hoveredNow = 0;

            // Wechsel: die alte Figur tritt ab, dann erst wird umgeschaltet
            if (pendingShow != null)
            {
                figOut = Mathf.MoveTowards(figOut, 1f, dt / 0.13f);
                if (figOut >= 1f)
                {
                    Apply(pendingShow);
                    pendingShow = null;
                    figSwapped = true;
                    swap = 0f;
                    figIn = 0f; figInVel = 0f;
                    shownLevel = shownGems = -1;
                }
            }
            else figOut = Mathf.MoveTowards(figOut, 0f, dt / 0.1f);
            Refresh();
            UpdateWorld(dt, aim);
            UpdateFigure(dt, aim);
            UpdatePanel(dt);
            UpdateRoster(dt);
            UpdateFx(dt);
        }

        /// <summary>Einfahren eines Teils: 0 → 1 nach seiner Verzögerung.</summary>
        float Enter(float delay, float duration = 0.34f) => MathUtil.EaseOutCubic(Mathf.Clamp01((enter - delay) / duration));

        /// <summary>
        /// Wind aus Grundzug und langsamen Böen; Dunst, Nebel und Lichtpunkte treiben darin, jede Ebene nach ihrer
        /// Entfernung. Die Szene folgt dem Zeiger träge (Feder), nähere Ebenen verschieben sich weiter.
        /// </summary>
        void UpdateWorld(float dt, Vector2 aim)
        {
            wind = 15f + 13f * (Mathf.PerlinNoise(time * 0.12f, 3.7f) - 0.5f) * 2f;
            drift += wind * dt;

            Vector2 idle = new Vector2(Mathf.PerlinNoise(time * 0.07f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.9f, time * 0.06f) - 0.5f) * 5f;
            MathUtil.Spring(ref parallax, ref parallaxVel, -new Vector2(aim.x * 9f, aim.y * 5f) + idle, 0.8f, 0.72f, dt);
            world.localScale = Vector3.one * 1.022f;   // etwas Überstand: beim Verschieben wird kein Rand sichtbar
            world.anchoredPosition = parallax;
            // das Steinbild steht weit hinten am Himmel: es folgt der Kamera weniger als die Ruinen
            backMask.anchoredPosition = backHome - parallax * 0.22f;
            hazeLayer.anchoredPosition = HazeHome - parallax * 0.3f;
            moteLayer.anchoredPosition = parallax * 0.4f;
            fogLayer.anchoredPosition = FogHome + parallax * 0.8f;

            // Steinbild: das neue blendet in 0,35 s über das alte
            float fade = figSwapped ? MathUtil.Smooth01(swap / 0.35f) : 1f;
            backNew.color = Color.white.WithAlpha(backOld.enabled ? fade : 1f);

            float hw = hazeLayer.sizeDelta.x, fw = fogLayer.sizeDelta.x;
            haze.uvRect = new Rect(-drift * 0.3f / hw, 0f, 1f, 1f);
            haze.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Sin(time * 0.21f) * 6f);
            fogA.uvRect = new Rect(-drift * 0.8f / fw, 0f, 0.8f, 1f);
            fogB.uvRect = new Rect(0.37f - drift * 1.7f / fw, 0f, 1.3f, 1f);
            fogA.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Sin(time * 0.33f) * 4f);
            fogB.rectTransform.anchoredPosition = new Vector2(0f, -10f + Mathf.Sin(time * 0.27f + 1.7f) * 6f);
            fogA.color = fogA.color.WithAlpha(0.18f + 0.04f * Mathf.Sin(time * 0.19f));

            // Fackeln: unruhiges, warmes Flackern (zwei überlagerte Schwebungen, jede Fackel für sich)
            foreach (var (img, phase, size) in torches)
            {
                float f = 0.55f + 0.25f * Mathf.Sin(time * 7.3f + phase) + 0.2f * Mathf.Sin(time * 11.9f + phase * 1.7f);
                img.color = new Color(1f, 0.6f, 0.22f, 0.07f + 0.05f * f);
                img.rectTransform.sizeDelta = Vector2.one * size * (0.92f + 0.12f * f);
            }

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
        }

        /// <summary>
        /// Die Figur: tritt von links ins Bild und federt in den Stand; danach atmet sie (einatmen schneller als
        /// ausatmen), verlagert langsam ihr Gewicht, neigt den Kopf zum Zeiger, und die Haare schwingen mit Trägheit nach.
        /// </summary>
        void UpdateFigure(float dt, Vector2 aim)
        {
            if (!figure.enabled) return;
            // Auftritt: gefedert an den Platz (leichtes Überschwingen), beim Ankommen sackt die Figur kurz ein
            float before = figIn;
            MathUtil.Spring(ref figIn, ref figInVel, figSwapped && enter > 0.05f ? 1f : 0f, 3.1f, 0.68f, dt);
            if (before < 0.93f && figIn >= 0.93f) landVel += 0.75f;
            MathUtil.Spring(ref land, ref landVel, 0f, 4.2f, 0.42f, dt);
            float slide = (1f - figIn) * -130f - figOut * 46f;
            float alpha = Mathf.Clamp01(figIn * 2.6f) * (1f - figOut);
            figRoot.anchoredPosition = new Vector2(slide, 0f) + parallax * 0.35f;
            figure.color = Color.white.WithAlpha(alpha);
            figShadow.color = new Color(0.02f, 0.08f, 0.04f, 0.26f * alpha * (1f - Mathf.Clamp01(-land * 4f)));
            figShadow.rectTransform.localScale = new Vector3(1f + land * 0.6f, 1f, 1f);

            // Atmen: etwa 3,6 s, einatmen zügig, ausatmen lang
            breathPhase += dt * (Mathf.PI * 2f / 3.6f);
            float breath = Mathf.Sin(breathPhase) + 0.28f * Mathf.Sin(breathPhase * 2f + 0.7f);
            // Gewicht: zwei langsame, nicht aufgehende Schwingungen – wiederholt sich nie sichtbar
            float sway = 3.1f * Mathf.Sin(time * 0.62f + 0.4f) + 1.7f * Mathf.Sin(time * 0.37f + 2.1f);
            // der Blick folgt dem Zeiger träge: der Kopf neigt sich, der Körper lehnt sich kaum merklich mit
            MathUtil.Spring(ref lookX, ref lookXVel, Mathf.Clamp(aim.x + 0.25f, -1f, 1f), 1.4f, 0.8f, dt);
            MathUtil.Spring(ref lookY, ref lookYVel, aim.y, 1.4f, 0.8f, dt);
            float nod = -lookY * 2.6f + breath * 0.45f - lookX * 1.1f;
            float total = sway + lookX * 2.2f + slide * 0.02f;
            // beim Auftritt neigt sich der Oberkörper gegen die Bewegung und schwingt aus
            float lean = -figInVel * 1.2f;
            figure.Pose(breath, total, nod, land, lean);

            // Haare: ein gedämpftes Pendel, angetrieben von der Beschleunigung des Kopfes und vom Wind
            float swayVel = (total - lastSway) / Mathf.Max(1e-4f, dt);
            float accel = (swayVel - lastSwayVel) / Mathf.Max(1e-4f, dt);
            lastSway = total; lastSwayVel = swayVel;
            float gust = (Mathf.PerlinNoise(time * 0.45f, 7.7f) - 0.5f) * 3.2f;
            for (int i = 0; i < swing.Length; i++)
            {
                float target = gust * (1f + 0.3f * i) - Mathf.Clamp(swayVel * 0.22f + accel * 0.012f, -5f, 5f) - lean * 0.5f;
                MathUtil.Spring(ref swing[i], ref swingVel[i], target, 1.15f, 0.22f, dt);
                figure.Swing(i, Mathf.Clamp(swing[i], -7f, 7f));
            }
        }

        void UpdatePanel(float dt)
        {
            // Zustand des Auswahlknopfs
            var def = pendingShow ?? shown;
            bool own = Profile.OwnsCharacter(def.Id);
            choice = !own ? Choice.Buy : Characters.Current == def ? Choice.Chosen : Choice.Select;
            MathUtil.Spring(ref chosenT, ref chosenVel, choice == Choice.Select ? 0f : 1f, 6f, 0.9f, dt);
            float blank = Mathf.Clamp01(chosenT);
            selectBlank.color = Color.white.WithAlpha(blank);
            // GEWÄHLT mit grünem Haken davor – oder KAUFEN, Münze, Preis (alles zwischen den Schnecken der Platte)
            bool buy = choice == Choice.Buy;
            string word = buy ? "KAUFEN" : "GEWÄHLT";
            if (selectLabel.text != word) selectLabel.text = word;
            selectLabel.color = Ink.WithAlpha(blank);
            selectLabel.fontSizeMax = buy ? 38f : 44f;
            selectLabel.rectTransform.sizeDelta = new Vector2(buy ? 160f : 210f, 56f);
            selectLabel.rectTransform.anchoredPosition = new Vector2(buy ? -70f : 24f, 0f);
            var coin = buy ? ShopArt.Get("coin") : null;
            selectIcon.sprite = buy ? (coin != null ? coin : CoinArt.Ui) : MenuArt.IconCheck;
            selectIcon.rectTransform.anchoredPosition = new Vector2(buy ? 38f : -108f, buy ? 0f : 1f);
            selectIcon.color = Color.white.WithAlpha(blank);
            string priceText = buy ? def.CoinPrice.ToString() : "";
            if (selectPrice.text != priceText) selectPrice.text = priceText;
            selectPrice.color = (Wallet.CanAfford(def.Cost) ? Ink : new Color(0.62f, 0.08f, 0.05f)).WithAlpha(blank);
            selectPrice.rectTransform.anchoredPosition = new Vector2(118f, 0f);

            // Einfahren: jedes Teil nach seiner Verzögerung, von rechts bzw. von unten
            Place(badge, new Vector2(40f, 0f));
            Place(select, new Vector2(0f, -30f));
            Place(upgrade, new Vector2(0f, -30f));
            foreach (var st in stats) Place(st.Plate, new Vector2(0f, -22f));
            // Name, Fähigkeit, Klassenbonus: fahren auch bei jedem Wechsel neu ein
            float change = figSwapped ? swap : 0f;
            float s0 = Mathf.Min(Enter(title.Delay), MathUtil.EaseOutCubic(change / 0.3f));
            float s1 = Enter(ability.Delay) * MathUtil.EaseOutBack(Mathf.Clamp01((change - 0.05f) / 0.34f), 1.4f);
            float s2 = Mathf.Min(Enter(0.17f), MathUtil.EaseOutCubic((change - 0.1f) / 0.3f));
            float outNow = pendingShow != null ? 1f - figOut : 1f;
            title.Root.anchoredPosition = title.Home + new Vector2((1f - s0) * 46f, 0f);
            title.Group.alpha = Mathf.Clamp01(s0 * 1.4f) * outNow;
            ability.Root.anchoredPosition = ability.Home;
            ability.Root.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, s1);
            ability.Group.alpha = Mathf.Clamp01(s1 * 1.6f) * outNow;
            trait.anchoredPosition = new Vector2((1f - s2) * 34f, 0f);
            traitGroup.alpha = Mathf.Clamp01(s2) * outNow;

            // Stufe: springt, wenn sie steigt
            float lt = fxT - 0.12f;
            if (lt >= 0f && !levelKicked) { levelKicked = true; levelPopVel += 9f; }
            MathUtil.Spring(ref levelPop, ref levelPopVel, 0f, 5f, 0.35f, dt);
            float lf = lt >= 0f ? Mathf.Clamp01(1f - lt / 0.8f) : 0f;
            level.rectTransform.localScale = Vector3.one * (1f + levelPop * 0.12f);
            level.color = Color.Lerp(new Color(1f, 0.8f, 0.62f), new Color(0.7f, 1f, 0.78f), lf);
            badge.Style(0f, 0f, levelPop * 0.5f, time, Mint, lf * 4f, 0f, 0f);

            // Werte: zählen zum neuen Stand; nach einem Upgrade leuchten sie nacheinander grün, der Zuwachs steigt auf
            for (int i = 0; i < 3; i++)
            {
                var st = stats[i];
                if (st.Shown < 0f) st.Shown = st.Target;
                st.Shown = Mathf.MoveTowards(st.Shown, st.Target, Mathf.Max(0.5f, Mathf.Abs(st.Target - st.Shown) * 9f) * dt);
                string text = Format(i, st.Shown);
                if (st.Value.text != text) st.Value.text = text;
                float t = fxT - st.Delay;
                if (t >= 0f && !st.Kicked) { st.Kicked = true; st.PopVel += 8f; }
                MathUtil.Spring(ref st.Pop, ref st.PopVel, 0f, 5f, 0.35f, dt);
                float f = t >= 0f ? Mathf.Clamp01(1f - t / 0.9f) : 0f;
                st.Value.rectTransform.localScale = Vector3.one * (1f + st.Pop * 0.1f);
                st.Value.color = Color.Lerp(Navy, new Color(0.04f, 0.42f, 0.18f), f);
                st.Glow.color = Mint.WithAlpha(0.16f * f);
                st.Plate.Style(0f, 0f, st.Pop * 0.35f, time, Mint, 0f, 0f, 0f);
                float rise = t >= 0f ? Mathf.Clamp01(t / 0.9f) : 0f;
                bool on = t >= 0f && rise < 1f;
                st.Rise.alpha = on ? Mathf.Clamp01(rise / 0.12f) * (1f - MathUtil.Smooth01((rise - 0.45f) / 0.55f)) : 0f;
                st.Rise.rectTransform.anchoredPosition = (Vector2)st.Value.rectTransform.anchoredPosition + new Vector2(0f, 24f + MathUtil.EaseOutCubic(rise) * 58f);
                st.Rise.rectTransform.localScale = Vector3.one * (0.8f + 0.2f * MathUtil.EaseOutBack(Mathf.Clamp01(rise * 4f)));
            }
            upgrade.Group.alpha *= own ? 1f : 0.55f;
        }

        void Place(Knob k, Vector2 from)
        {
            float e = Enter(k.Delay);
            k.Root.anchoredPosition = k.Home + from * (1f - e);
            k.Group.alpha = Mathf.Clamp01(e * 1.5f);
        }

        void UpdateRoster(float dt)
        {
            MathUtil.Spring(ref scroll, ref scrollVel, scrollTarget, 3.4f, 0.9f, dt);
            float mid = (ViewLeft + ViewRight) * 0.5f;
            foreach (var t in tiles)
            {
                float e = Enter(t.Knob.Delay);
                t.Knob.Root.anchoredPosition = new Vector2(t.X - scroll - mid, -(1f - e) * 46f);
                t.Knob.Group.alpha = Mathf.Clamp01(e * 1.5f);
            }
            float arrows = Enter(arrowNext.Delay);
            arrowNext.Group.alpha = Mathf.MoveTowards(arrowNext.Group.alpha, CanScroll(1) * arrows, dt * 5f);
            arrowPrev.Group.alpha = Mathf.MoveTowards(arrowPrev.Group.alpha, CanScroll(-1) * arrows, dt * 5f);
        }

        void UpdateFx(float dt)
        {
            fxT += dt;
            pickT += dt;
            // Upgrade: Ring und Licht am Knopf
            float r = Mathf.Clamp01(fxT / 0.6f);
            ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(90f, 520f, MathUtil.EaseOutCubic(r));
            ring.color = Mint.WithAlpha(r < 1f ? 0.45f * (1f - r) * (1f - r) : 0f);
            flash.color = Mint.WithAlpha(0.2f * Mathf.Clamp01(1f - fxT / 0.45f));
            // Auswahl: goldener Ring breitet sich flach am Boden aus
            float p = Mathf.Clamp01(pickT / 0.7f);
            pickRing.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(120f, 760f, MathUtil.EaseOutCubic(p)), Mathf.Lerp(30f, 150f, MathUtil.EaseOutCubic(p)));
            pickRing.color = Sun.WithAlpha(p < 1f ? 0.55f * (1f - p) * (1f - p) : 0f);
            // Funken steigen auf und verglühen
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
                s.Img.color = Color.Lerp(Color.white, s.Tint, 0.55f + 0.45f * u).WithAlpha(0.8f * Mathf.Clamp01(u / 0.1f) * (1f - u));
            }
        }
    }
}
