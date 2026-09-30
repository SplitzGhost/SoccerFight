using System;
using System.Collections;
using UnityEngine;

namespace SoccerFight
{
    public sealed partial class CaptureDriver
    {
        // Spielerauswahl: prüft die tatsächlichen Balltreffer und verändert ausschließlich das Wegwerfprofil.
        IEnumerator CharacterMenuDesign()
        {
            SeedProfile();
            Profile.GrantCharacter("dre");
            foreach (var id in new[] { "rio", "bruno", "mira", "dre" }) Profile.SetLevel("character:" + id, 4);
            Wallet.Add(Currencies.Gems, 240);
            Wallet.Add(Currencies.Coins, 560);
            G.ToMenu();
            yield return Seconds(3f);
            GameInput.AimScreen = G.Menu.ScreenOf(new Vector2(-400f, -420f));
            yield return Shot("p00_hauptmenue");

            // Öffnen: alles fährt gestaffelt ein
            yield return Kick("tab_chars");
            yield return Shot("p01_einfahren_a");
            yield return Seconds(0.2f);
            yield return Shot("p01_einfahren_b");
            yield return Seconds(1.6f);
            Require(G.Menu.Page == MenuPage.Characters && G.Menu.ShownCharacter == Characters.Get("rio"), "Reiter SPIELER zeigt den gewählten Spieler");
            GameInput.AimScreen = G.Menu.ScreenOf(new Vector2(-760f, -200f));
            yield return Seconds(0.6f);
            yield return Shot("p02_rio");
            yield return Seconds(1.1f);
            yield return Shot("p02_rio_atmet");

            // Kacheln: zeigen einen Spieler nur an, gewählt bleibt RIO
            bool first = true;
            foreach (var id in new[] { "mira", "bruno", "dre", "nova", "titan" })
            {
                if (first)
                {
                    GameInput.AimScreen = G.Menu.TargetScreen("tile_" + id);
                    yield return Seconds(0.7f);
                    yield return Shot("p03_kachel_zeigen");
                }
                yield return Kick("tile_" + id, false);
                if (first)
                {
                    yield return Seconds(0.3f);
                    yield return Shot("p03_wechsel_a");
                    yield return Seconds(0.12f);
                    yield return Shot("p03_wechsel_b");
                    yield return Seconds(0.15f);
                    yield return Shot("p03_wechsel_c");
                }
                first = false;
                yield return Seconds(1.5f);
                GameInput.AimScreen = G.Menu.ScreenOf(new Vector2(-760f, -200f));
                yield return Seconds(0.5f);
                Require(G.Menu.Page == MenuPage.Characters && G.Menu.ShownCharacter == Characters.Get(id), "Kachel zeigt " + id);
                Require(Characters.Current.Id == "rio", "Kachel wählt noch nicht aus");
                yield return Shot("p04_" + id);
            }

            // AUSWÄHLEN: bei einem gekauften Spieler
            yield return Kick("tile_mira");
            yield return Seconds(1.2f);
            GameInput.AimScreen = G.Menu.TargetScreen("player_select");
            yield return Seconds(0.8f);
            yield return Shot("p05_auswaehlen_zeigen");
            yield return Kick("player_select", false);
            yield return Seconds(0.3f);
            yield return Shot("p06_auswahl_funken");
            yield return Seconds(1.2f);
            Require(Characters.Current.Id == "mira", "AUSWÄHLEN macht MIRA zur Spielerin");
            yield return Shot("p07_gewaehlt");

            // UPGRADE: Vorschau beim Zeigen, grüne Welle beim Treffer
            int wallet = Wallet.Get(Currencies.Gems);
            int level = CharacterProgression.Level(Characters.Current);
            int price = CharacterProgression.UpgradeCost(level);
            GameInput.AimScreen = G.Menu.TargetScreen("player_upgrade");
            yield return Seconds(0.8f);
            yield return Shot("p08_upgrade_zeigen");
            yield return Kick("player_upgrade", false);
            yield return Seconds(0.18f);
            yield return Shot("p09_upgrade_funken");
            yield return Seconds(0.25f);
            yield return Shot("p09_upgrade_welle");
            yield return Seconds(0.25f);
            yield return Shot("p09_upgrade_werte");
            yield return Seconds(0.6f);
            Require(CharacterProgression.Level(Characters.Current) == level + 1, "Upgrade erhöht genau eine Stufe");
            Require(Wallet.Get(Currencies.Gems) == wallet - price, "Upgrade bucht genau den Kristallpreis");
            Require(CharacterProgression.Level(Characters.Get("bruno")) == 4, "Upgrade betrifft nur MIRA");
            yield return Shot("p10_upgrade");

            // Fähigkeit: hebt sich, ein Treffer nennt die Taste
            GameInput.AimScreen = G.Menu.TargetScreen("player_ability");
            yield return Seconds(0.8f);
            yield return Shot("p11_faehigkeit_zeigen");
            yield return Kick("player_ability");
            yield return Seconds(0.3f);
            yield return Shot("p11_faehigkeit_hinweis");
            yield return Seconds(1.6f);

            // Ein zu kleines Guthaben und die Maximalstufe dürfen weder abbuchen noch erhöhen.
            Require(Wallet.TrySpend(new Price(Currencies.Gems, Wallet.Get(Currencies.Gems))), "Testguthaben vollständig ausgegeben");
            level = CharacterProgression.Level(Characters.Current);
            yield return Kick("player_upgrade");
            yield return Seconds(0.5f);
            Require(CharacterProgression.Level(Characters.Current) == level && Wallet.Get(Currencies.Gems) == 0, "Kein Upgrade ohne Kristalle");
            yield return Shot("p12_zu_wenig_kristalle");
            Profile.SetLevel("character:mira", CharacterProgression.MaxLevel);
            Wallet.Add(Currencies.Gems, 500);
            yield return Seconds(1.8f);
            yield return Kick("player_upgrade");
            yield return Seconds(0.5f);
            Require(CharacterProgression.Level(Characters.Current) == CharacterProgression.MaxLevel && Wallet.Get(Currencies.Gems) == 500,
                "Maximalstufe bleibt ohne Abbuchung");
            yield return Shot("p13_maximum");
            yield return Seconds(1.6f);

            // Noch nicht gekauft: der Knopf zeigt den Preis und führt in den Shop
            yield return Kick("tile_nova");
            yield return Seconds(1.4f);
            GameInput.AimScreen = G.Menu.TargetScreen("player_select");
            yield return Seconds(0.7f);
            yield return Shot("p14_nicht_gekauft");
            yield return Kick("player_select");
            yield return Seconds(1.4f);
            Require(G.Menu.Page == MenuPage.Shop && Characters.Current.Id == "mira", "KAUFEN führt in den Shop, ohne auszuwählen");
            yield return Shot("p15_shop");

            // Figurenleiste schieben, kommende Sportarten
            yield return Kick("tab_chars");
            yield return Seconds(1.8f);
            Require(G.Menu.ShownCharacter == Characters.Current, "Seite öffnet wieder mit dem gewählten Spieler");
            yield return Kick("roster_next");
            yield return Seconds(1f);
            yield return Shot("p16_leiste_weiter");
            yield return Kick("tile_soon_boxen");
            yield return Seconds(0.2f);
            yield return Shot("p17_kommt_bald");
            yield return Seconds(1.8f);
            yield return Kick("roster_prev");
            yield return Seconds(1f);
            yield return Kick("tile_rio");
            yield return Seconds(1.4f);
            Require(G.Menu.ShownCharacter == Characters.Get("rio"), "Leiste zurück, RIO gezeigt");
            yield return Shot("p18_leiste_zurueck");

            // Hauptmenü: die Figur führt zur Spielerauswahl
            yield return Kick("tab_home");
            yield return Seconds(1.2f);
            Require(G.Menu.Page == MenuPage.Main, "Reiter SPIELEN führt zum Hauptmenü");
            yield return Kick("figure");
            yield return Seconds(1.6f);
            Require(G.Menu.Page == MenuPage.Characters, "Figur im Hauptmenü öffnet die Spielerauswahl");
            yield return Shot("p19_ueber_figur");

            // andere Bildschirmformate: 4:3, 21:9, Hochkant
            foreach (var size in new[] { new Vector2Int(1440, 1080), new Vector2Int(2520, 1080), new Vector2Int(1080, 1920) })
            {
                SetSize(size.x, size.y);
                GameInput.AimScreen = G.Menu.ScreenOf(new Vector2(-300f, 100f));
                yield return Seconds(0.8f);
                yield return Shot("p20_" + size.x + "x" + size.y);
            }
            SetSize(1920, 1080);
            Debug.Log("[Capture] Spielerauswahl: alle Ansichten, Auswahl und Upgrade-Prüfungen bestanden");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Spielerauswahl: " + message);
            Debug.Log("[Capture] geprüft: " + message);
        }
    }
}
