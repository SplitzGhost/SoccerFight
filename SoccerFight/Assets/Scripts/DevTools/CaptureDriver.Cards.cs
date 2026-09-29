using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Szenario „cards“: die Belohnungskarten in allen Seltenheiten, eine Karte im Hover, die Fähigkeitskarten
    /// und zum Schluss eine Übersicht aller Kartenbilder (jedes Upgrade-Relief und jedes Fähigkeits-Medaillon).
    /// </summary>
    public sealed partial class CaptureDriver
    {
        IEnumerator CardsTour()
        {
            G.Director.DebugJump(2, 3, 0, false);
            yield return Seconds(1f);

            // 1 — gewöhnlich, selten, episch; danach legendär neben den früher gemalten Bildern
            yield return ShowCards("c01_seltenheiten", Pick(Rarity.Common, 0), Pick(Rarity.Rare, 0), Pick(Rarity.Epic, 0));
            yield return ShowCards("c02_legendaer", UpgradeByIcon(UpIcon.Damage), Pick(Rarity.Legendary, 0), UpgradeByIcon(UpIcon.Speed));
            yield return ShowCards("c03_mehr", Pick(Rarity.Rare, 3), Pick(Rarity.Epic, 2), Pick(Rarity.Legendary, 1));

            // 2 — Hover auf der mittleren Karte
            G.Rewards.ShowUpgrades(new List<UpgradeDef> { Pick(Rarity.Common, 1), Pick(Rarity.Rare, 1), Pick(Rarity.Epic, 1) }, false, G.Run, u => { });
            yield return Seconds(1.3f);
            G.Rewards.DebugHover(1);
            yield return Seconds(0.6f);
            yield return Shot("c04_hover");
            G.Rewards.Cancel();
            yield return Frames(2);

            // 3 — Fähigkeitskarten
            G.Rewards.ShowAbilities(new List<Ability> { Ability.Punt, Ability.Decoy }, G.Run.Stage, a => { });
            yield return Seconds(1.4f);
            yield return Shot("c05_faehigkeiten");
            G.Rewards.DebugHover(0);
            yield return Seconds(0.6f);
            yield return Shot("c06_faehigkeiten_hover");
            G.Rewards.Cancel();
            G.Rewards.ShowAbilities(new List<Ability> { Ability.AlleyOop, Ability.Block }, G.Run.Stage, a => { });
            yield return Seconds(1.4f);
            yield return Shot("c07_faehigkeiten_basketball");
            G.Rewards.Cancel();
            yield return Frames(2);

            // 4 — alle Kartenbilder auf einem Bogen
            yield return EmblemSheet();
        }

        IEnumerator ShowCards(string shot, params UpgradeDef[] offer)
        {
            G.Rewards.ShowUpgrades(new List<UpgradeDef>(offer), false, G.Run, u => { });
            yield return Seconds(1.4f);
            yield return Shot(shot);
            G.Rewards.Cancel();
            yield return Frames(2);
        }

        static UpgradeDef Pick(Rarity r, int nth)
        {
            UpgradeDef last = null;
            foreach (var u in UpgradeDb.All)
            {
                if (u.Rarity != r || u.Sport != Sport.Soccer || u.IsSynergy) continue;
                last = u;
                if (nth-- <= 0) return u;
            }
            return last ?? UpgradeDb.All[0];
        }

        static UpgradeDef UpgradeByIcon(UpIcon icon)
        {
            foreach (var u in UpgradeDb.All) if (u.Icon == icon) return u;
            return UpgradeDb.All[0];
        }

        IEnumerator EmblemSheet()
        {
            var go = new GameObject("Emblem Sheet", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = G.Cam.Cam;
            canvas.planeDistance = 0.9f;
            canvas.sortingOrder = 3000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)go.transform;
            var back = UiKit.Img("Back", root, null, new Color(0.07f, 0.09f, 0.13f, 1f), Vector2.zero, Vector2.zero);
            back.rectTransform.anchorMin = Vector2.zero;
            back.rectTransform.anchorMax = Vector2.one;

            var icons = (UpIcon[])System.Enum.GetValues(typeof(UpIcon));
            const int cols = 14;
            const float cell = 112f;
            for (int i = 0; i < icons.Length; i++)
            {
                var pos = new Vector2((i % cols - (cols - 1) * 0.5f) * cell * 1.2f, 420f - (i / cols) * cell);
                UiKit.Img(icons[i].ToString(), root, CardEmblems.ForUpgrade(icons[i]), Color.white, pos, Vector2.one * cell).preserveAspect = true;
            }
            var abilities = (Ability[])System.Enum.GetValues(typeof(Ability));
            int n = 0;
            foreach (var a in abilities)
            {
                if (a == Ability.None) continue;
                var pos = new Vector2((n % 11 - 5f) * 150f, -130f - (n / 11) * 160f);
                UiKit.Img(a.ToString(), root, CardEmblems.ForAbility(a, Abilities.Accent(a)), Color.white, pos, Vector2.one * 144f).preserveAspect = true;
                n++;
            }
            G.Hud.SetVisible(false);
            yield return Frames(3);
            yield return Shot("c08_alle_bilder");
            G.Hud.SetVisible(true);
            Destroy(go);
        }
    }
}
