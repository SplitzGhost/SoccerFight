using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Szenario „screens“: dieselben Menüs und Spielanzeigen in vielen Bildschirmformaten (4:3, 16:10,
    /// 21:9, 32:9, Hochkant …), um zu prüfen, dass überall alles ins Bild passt und es ausfüllt.
    /// Eigene Liste mit „-sfSizes 1440x1080,1080x1920“.
    /// </summary>
    public sealed partial class CaptureDriver
    {
        void SetSize(int w, int h)
        {
            G.Cam.Cam.targetTexture = null;
            rt.Release();
            Destroy(rt);
            Destroy(frame);
            W = w; H = h;
            rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            frame = new Texture2D(W, H, TextureFormat.RGBA32, false);
            G.Cam.Cam.targetTexture = rt;
        }

        static List<Vector2Int> ScreenSizes()
        {
            string arg = Arg("-sfSizes") ?? "1440x1080,1728x1080,2520x1080,3840x1080,1080x1920,1280x720";
            var list = new List<Vector2Int>();
            foreach (var part in arg.Split(','))
            {
                var wh = part.Trim().Split('x');
                if (wh.Length == 2 && int.TryParse(wh[0], out int w) && int.TryParse(wh[1], out int h)) list.Add(new Vector2Int(w, h));
            }
            return list;
        }

        IEnumerator Screens()
        {
            var menu = G.Menu;
            var sizes = ScreenSizes();

            // 1 — erster Start: die Starterwahl
            Profile.UseTransient();
            Characters.Reload();
            G.ToMenu();
            yield return Seconds(2.8f);
            foreach (var s in sizes)
            {
                SetSize(s.x, s.y);
                GameInput.AimScreen = menu.ScreenOf(new Vector2(0f, -300f));
                yield return Seconds(0.6f);
                yield return Shot(Tag(s) + "_00_starter");
            }

            // 2 — alle Menüseiten
            SeedProfile();
            G.ToMenu();
            yield return Seconds(2.4f);
            string[] ids = { "tab_chars", "tab_shop", "tab_events", "tab_ranking", "tab_settings", "friends", "info" };
            foreach (var s in sizes)
            {
                SetSize(s.x, s.y);
                GameInput.AimScreen = menu.ScreenOf(new Vector2(-260f, 160f));
                yield return Seconds(0.8f);
                yield return Shot(Tag(s) + "_01_title");
                yield return Kick("figure");
                yield return Seconds(1.4f);
                yield return Shot(Tag(s) + "_02_details");
                for (int i = 0; i < ids.Length; i++)
                {
                    yield return Kick(ids[i]);
                    yield return Seconds(1.1f);
                    yield return Shot(Tag(s) + "_" + (3 + i).ToString("00") + "_" + ids[i]);
                }
                yield return Kick("tab_home");
                yield return Seconds(1f);
            }

            // 3 — im Lauf: Anzeige, Pause, Optionen, Karten
            G.Restart();
            G.Director.DebugJump(2, 3, 0, false);
            yield return Seconds(1.5f);
            foreach (var s in sizes)
            {
                SetSize(s.x, s.y);
                yield return Seconds(0.5f);
                yield return Shot(Tag(s) + "_10_hud");
                G.Hud.ShowStageCard(2, StageThemes.All[1]);
                yield return Seconds(0.9f);
                yield return Shot(Tag(s) + "_11_stagecard");
                G.Hud.HideStageCard();
                G.Pause.Open();
                yield return Seconds(1f);
                yield return Shot(Tag(s) + "_12_pause");
                G.Pause.OpenSettings();
                yield return Seconds(1f);
                yield return Shot(Tag(s) + "_13_pause_settings");
                G.Pause.Close();
                yield return Frames(30);
                yield return ShowCards(Tag(s) + "_14_cards", Pick(Rarity.Common, 0), Pick(Rarity.Rare, 0), Pick(Rarity.Epic, 0));
                G.Rewards.ShowAbilities(new List<Ability> { Ability.Punt, Ability.Decoy }, G.Run.Stage, a => { });
                yield return Seconds(1.4f);
                yield return Shot(Tag(s) + "_15_abilities");
                G.Rewards.Cancel();
                yield return Frames(2);
            }

            // 4 — Lauf vorbei
            G.Director.EndRun();
            yield return Seconds(3f);
            foreach (var s in sizes)
            {
                SetSize(s.x, s.y);
                yield return Seconds(0.5f);
                yield return Shot(Tag(s) + "_16_runover");
            }
        }

        static string Tag(Vector2Int s) => s.x + "x" + s.y;
    }
}
