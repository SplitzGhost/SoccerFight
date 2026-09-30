using System.Collections;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Szenario „shop“: der Shop mit den gemalten Karten – Übersicht, eine Karte unter dem Zeiger (angehoben),
    /// Kauf mit Bestätigung, zu wenig Münzen, Auswählen, und die Seite in 4:3 und 21:9.
    /// </summary>
    public sealed partial class CaptureDriver
    {
        IEnumerator ShopTour()
        {
            var menu = G.Menu;
            SeedProfile();
            Wallet.Add(Currencies.Coins, 700);
            G.ToMenu();
            yield return Seconds(2.4f);
            yield return Kick("tab_shop");
            GameInput.AimScreen = menu.ScreenOf(new Vector2(0f, -500f));
            yield return Seconds(2.2f);
            yield return Shot("s01_shop");

            // Zeiger auf einer hohen und einer kleinen Karte
            GameInput.AimScreen = menu.TargetScreen("shop_char_dre");
            yield return Seconds(0.9f);
            yield return Shot("s02_hover_hoch");
            GameInput.AimScreen = menu.TargetScreen("shop_char_bruno");
            yield return Seconds(0.9f);
            yield return Shot("s03_hover_klein");

            // kaufen: erster Treffer fragt, zweiter kauft, dritter wählt aus
            yield return Kick("shop_char_titan");
            yield return Seconds(0.5f);
            yield return Shot("s04_kaufen_frage");
            yield return Kick("shop_char_titan");
            yield return Seconds(0.9f);
            yield return Shot("s05_gekauft");
            yield return Kick("shop_char_titan");
            yield return Seconds(0.9f);
            yield return Shot("s06_gewaehlt");
            Require(Profile.OwnsCharacter("titan") && Characters.Current.Id == "titan", "Titan gekauft und gewählt");

            // zu wenig Münzen
            yield return Kick("shop_char_nova");
            yield return Seconds(0.3f);
            yield return Shot("s07_zu_teuer");
            GameInput.AimScreen = menu.ScreenOf(new Vector2(0f, -500f));
            yield return Seconds(1.2f);

            foreach (var s in new[] { new Vector2Int(1440, 1080), new Vector2Int(2520, 1080) })
            {
                SetSize(s.x, s.y);
                GameInput.AimScreen = menu.ScreenOf(new Vector2(0f, -500f));
                yield return Seconds(0.8f);
                yield return Shot("s08_" + Tag(s));
            }
        }
    }
}
