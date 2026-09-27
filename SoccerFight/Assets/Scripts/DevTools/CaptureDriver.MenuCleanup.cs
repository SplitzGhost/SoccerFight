using System.Collections;
using UnityEngine;

namespace SoccerFight
{
    public sealed partial class CaptureDriver
    {
        // Grafikprüfung des Menüs: Titel (Fußball und Basketball), alle Level, Spielerkarten, Shop,
        // Events, Info und Optionen. Verändert nur das Wegwerfprofil.
        IEnumerator MenuCleanup()
        {
            // erster Start: die Auswahl der Startfigur (gleiche Karten wie die Spielerseite)
            Profile.UseTransient();
            Characters.Reload();
            G.ToMenu();
            yield return Seconds(2.8f);
            yield return Shot("x_start");

            SeedProfile();
            foreach (var id in new[] { "dre", "titan" }) Profile.GrantCharacter(id);
            Profile.SetLevel("character:rio", 3);
            Wallet.Add(Currencies.Coins, 320);
            Wallet.Add(Currencies.Gems, 40);
            Characters.Select(Characters.IndexOf(Characters.Get("rio")));
            ChallengeLevels.Select(1);
            G.ToMenu();
            yield return Seconds(2.6f);
            GameInput.AimScreen = G.Menu.ScreenOf(new Vector2(-300f, 300f));
            yield return Shot("x00_titel");

            // alle Level nacheinander: 2 ist mit Stufe 3 frei, ab 3 gesperrt
            for (int n = 2; n <= ChallengeLevels.All.Length; n++)
            {
                ChallengeLevels.Select(n);
                yield return Seconds(0.3f);
                yield return Shot("x01_level" + n);
            }
            ChallengeLevels.Select(1);

            Characters.Select(Characters.IndexOf(Characters.Get("dre")));
            yield return Seconds(2f);
            yield return Shot("x02_titel_dre");

            yield return Kick("tab_chars");
            yield return Seconds(1.4f);
            yield return Shot("x03_spieler_basketball");
            yield return Kick("sport0");
            yield return Seconds(1.2f);
            yield return Shot("x04_spieler_fussball");

            string[] pages = { "tab_shop", "tab_events", "info", "tab_settings", "tab_ranking" };
            foreach (var id in pages)
            {
                yield return Kick(id);
                yield return Seconds(1.3f);
                yield return Shot("x05_" + id);
            }
            yield return Kick("tab_home");
            yield return Seconds(1f);
        }
    }
}
