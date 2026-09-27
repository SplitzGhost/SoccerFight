using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>Freigegebene Originalansicht mit echten Werten und getrenntem Upgrade-Ziel.</summary>
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
        TextMeshProUGUI level, life, damage, speed, cost, gems, coins;
        Image upgradeGlow;
        RectTransform upgrade;
        int shownLevel = -1, shownGems = -1, shownCoins = -1;
        float pulse;
        readonly PlayerStats stats = new PlayerStats();
        static readonly Color Ink = new Color(0.025f, 0.16f, 0.23f);

        static Vector2 At(float x, float y) => new Vector2(x - 836f, 470.5f - y);

        public void Build(RectTransform parent, MenuNav menu)
        {
            nav = menu;
            Page = new SubPage(parent, MenuPage.CharacterDetails, "CHARAKTER", "", MenuArt.Accent, menu.Register, menu.Back, true);
            // Außerhalb von 16:9 bleibt ein ruhiger Rand; die freigegebene Grafik wird nie verzerrt.
            MenuUi.Stretch(UiKit.Img("Rand", Page.Root, null, new Color(0.015f, 0.1f, 0.13f), Vector2.zero, Vector2.zero).rectTransform);
            Page.Content.SetAsLastSibling();
            art = UiKit.Node("Freigegebenes Probebild", Page.Content, Vector2.zero, Page.ArtworkSize).gameObject.AddComponent<RawImage>();
            art.raycastTarget = false;
            loop = new CharacterLoopPlayer(art, parent);
            level = Number("Stufe", 1051f, 176f, 64f, new Vector2(85f, 70f), new Color(0.94f, 0.66f, 0.45f));
            life = Number("Leben", 1054f, 628f, 37f, new Vector2(100f, 42f), Ink);
            damage = Number("Schaden", 1275f, 628f, 37f, new Vector2(100f, 42f), Ink);
            speed = Number("Tempo", 1487f, 628f, 37f, new Vector2(100f, 42f), Ink);
            cost = Number("Kristallpreis", 1285f, 731f, 31f, new Vector2(92f, 38f), Color.white);
            gems = Number("Kristalle", 1448f, 38f, 26f, new Vector2(72f, 35f), Color.white);
            coins = Number("Münzen", 1589f, 38f, 29f, new Vector2(93f, 35f), Color.white);

            var back = UiKit.Node("Zurück", Page.Content, At(77f, 75f), new Vector2(120f, 124f));
            nav.Register(new MenuTarget { Id = "detail_back", Root = back, Size = back.sizeDelta,
                Page = MenuPage.CharacterDetails, Action = () => Return?.Invoke(), Accent = MenuArt.Accent });
            upgrade = UiKit.Node("Upgrade", Page.Content, At(1272f, 823f), new Vector2(260f, 158f));
            upgradeGlow = UiKit.Img("Upgrade-Licht", upgrade, UiArt.Glow, Color.clear, Vector2.zero, new Vector2(220f, 170f));
            nav.Register(new MenuTarget { Id = "detail_upgrade", Root = upgrade, Size = upgrade.sizeDelta,
                Page = MenuPage.CharacterDetails, Action = Upgrade, Accent = MenuArt.Accent,
                Draw = t => upgradeGlow.color = new Color(0.1f, 0.8f, 0.9f, Mathf.Clamp01(t.Hover * 0.09f + pulse * 0.14f)) });
        }

        TextMeshProUGUI Number(string name, float x, float y, float size, Vector2 box, Color color)
        {
            var text = UiKit.Label(name, Page.Content, "", size, color, TextAlignmentOptions.Center, At(x, y), box);
            text.fontSharedMaterial = UiArt.FontBold.material;
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
            shownLevel = shownGems = shownCoins = -1;
            loop.Open(def.Id, texture);
            Refresh();
        }

        void Upgrade()
        {
            if (character == null || !Profile.OwnsCharacter(character.Id)) return;
            int current = CharacterProgression.Level(character);
            if (current >= CharacterProgression.MaxLevel)
            {
                nav.Say("MAXIMALE STUFE ERREICHT", upgrade);
                return;
            }
            int price = CharacterProgression.UpgradeCost(current);
            if (!CharacterProgression.TryUpgrade(character))
            {
                nav.Say("DIR FEHLEN " + Currencies.Format(Mathf.Max(0, price - Wallet.Get(Currencies.Gems))) + " KRISTALLE", upgrade);
                return;
            }
            if (Game.I != null && Game.I.Run != null) Game.I.Run.Rebuild();
            if (Game.I != null && Game.I.Player != null) Game.I.Player.ApplyStats(false);
            pulse = 1f;
            Refresh();
        }

        void Refresh()
        {
            if (character == null) return;
            int n = CharacterProgression.Level(character);
            int g = Wallet.Get(Currencies.Gems), c = Wallet.Get(Currencies.Coins);
            if (n == shownLevel && g == shownGems && c == shownCoins) return;
            shownLevel = n; shownGems = g; shownCoins = c;
            stats.Reset();
            MetaPassives.Apply(stats, character);
            CharacterProgression.Apply(stats, character);
            // Dauerhafte Werte des normalen Schusses/Wurfs, ohne zufällige Krits und Laufkarten.
            float shot = Player.ShotDamage * stats.DamageMul * stats.ShotDamageMul * stats.CategoryMul(SkillCategory.Shot);
            level.text = n.ToString();
            life.text = Mathf.RoundToInt(Player.BaseMaxHp + stats.MaxHpBonus).ToString();
            damage.text = shot.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');
            speed.text = (Player.MaxSpeed * stats.MoveSpeedMul).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
            bool max = n >= CharacterProgression.MaxLevel;
            int price = CharacterProgression.UpgradeCost(n);
            cost.text = max ? "MAX" : price.ToString();
            cost.color = max || g >= price ? Color.white : MetaUi.Danger;
            gems.text = Currencies.Format(g);
            coins.text = Currencies.Format(c);
        }

        public void Update(float dt, bool selected)
        {
            loop.Update(selected || Page.T >= 0.004f);
            if (Page.T < 0.004f)
            {
                return;
            }
            Page.Update(dt);
            pulse = Mathf.MoveTowards(pulse, 0f, dt * 2f);
            Refresh();
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
