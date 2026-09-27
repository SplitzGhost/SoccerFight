using System;
using System.Collections;
using UnityEngine;

namespace SoccerFight
{
    public sealed partial class CaptureDriver
    {
        // Prüft die tatsächlichen Balltreffer und verändert ausschließlich das Wegwerfprofil.
        IEnumerator CharacterMenuDesign()
        {
            SeedProfile();
            foreach (var def in Characters.All)
            {
                Profile.GrantCharacter(def.Id);
                Profile.SetLevel("character:" + def.Id, 4);
            }
            Wallet.Add(Currencies.Gems, 240);
            Wallet.Add(Currencies.Coins, 560);
            G.ToMenu();
            yield return Seconds(3f);
            GameInput.AimScreen = G.Menu.ScreenOf(new Vector2(-400f, -420f));
            yield return Shot("c00_hauptmenue");
            foreach (var def in Characters.All)
            {
                Characters.Select(Characters.IndexOf(def));
                yield return Seconds(0.5f);
                yield return Kick("figure");
                yield return Seconds(1.2f);
                Require(G.Menu.Page == MenuPage.CharacterDetails, "Detailseite öffnet für " + def.Id);
                GameInput.AimScreen = G.Menu.ScreenOf(new Vector2(-760f, -400f));
                yield return Shot("c01_" + def.Id);
                yield return Kick("detail_back");
                yield return Seconds(1.1f);
                Require(G.Menu.Page == MenuPage.Main, "Zurück führt aus Figur zum Hauptmenü");
            }

            // Karte auswählen öffnet Details, kostet aber noch keine Kristalle.
            yield return Kick("tab_chars");
            yield return Seconds(1.4f);
            yield return Kick("sport0");
            yield return Seconds(1.2f);
            int wallet = Wallet.Get(Currencies.Gems);
            yield return Kick("card_rio");
            yield return Seconds(1.2f);
            Require(G.Menu.Page == MenuPage.CharacterDetails && Characters.Current.Id == "rio", "Karte öffnet gewählten Charakter");
            Require(Wallet.Get(Currencies.Gems) == wallet, "Karte kauft kein Upgrade");
            int level = CharacterProgression.Level(Characters.Current);
            int price = CharacterProgression.UpgradeCost(level);
            // Zeigen: der Knopf hebt sich, die Zuwächse erscheinen in Grün
            GameInput.AimScreen = G.Menu.TargetScreen("detail_upgrade");
            yield return Seconds(0.8f);
            yield return Shot("c02h_zeigen");
            yield return Kick("detail_upgrade", false);
            yield return Seconds(0.35f);
            yield return Shot("c02w_upgrade_welle");
            yield return Seconds(0.6f);
            Require(CharacterProgression.Level(Characters.Current) == level + 1, "Upgrade erhöht genau eine Stufe");
            Require(Wallet.Get(Currencies.Gems) == wallet - price, "Upgrade bucht genau den Kristallpreis");
            Require(CharacterProgression.Level(Characters.Get("bruno")) == 4, "Upgrade betrifft nur RIO");
            yield return Shot("c02_upgrade");
            yield return Kick("detail_back");
            yield return Seconds(1f);
            Require(G.Menu.Page == MenuPage.Characters, "Zurück führt aus Karte zur Auswahl");
            yield return Kick("card_rio");
            yield return Seconds(1f);

            // Ein zu kleines Guthaben und die Maximalstufe dürfen weder abbuchen noch erhöhen.
            Require(Wallet.TrySpend(new Price(Currencies.Gems, Wallet.Get(Currencies.Gems))), "Testguthaben vollständig ausgegeben");
            level = CharacterProgression.Level(Characters.Current);
            yield return Kick("detail_upgrade");
            yield return Seconds(0.5f);
            Require(CharacterProgression.Level(Characters.Current) == level && Wallet.Get(Currencies.Gems) == 0,
                "Kein Upgrade ohne Kristalle");
            yield return Shot("c03_zu_wenig_kristalle");
            Profile.SetLevel("character:rio", CharacterProgression.MaxLevel);
            Wallet.Add(Currencies.Gems, 500);
            yield return Seconds(0.2f);
            yield return Kick("detail_upgrade");
            yield return Seconds(0.5f);
            Require(CharacterProgression.Level(Characters.Current) == CharacterProgression.MaxLevel && Wallet.Get(Currencies.Gems) == 500,
                "Maximalstufe bleibt ohne Abbuchung");
            yield return Shot("c04_maximum");

            // Basketballer auf den Karten: locker im Takt statt Dribbeln
            yield return Kick("detail_back");
            yield return Seconds(1f);
            yield return Kick("sport1");
            yield return Seconds(1.2f);
            yield return Shot("c05_spieler_basketball");
            yield return Kick("tab_shop");
            yield return Seconds(1.5f);
            yield return Shot("c06_shop");
            Debug.Log("[Capture] Charaktermenü: alle sechs Ansichten und Upgrade-Prüfungen bestanden");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Charaktermenü: " + message);
            Debug.Log("[Capture] geprüft: " + message);
        }
    }
}
