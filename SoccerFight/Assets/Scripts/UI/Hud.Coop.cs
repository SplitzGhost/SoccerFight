using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Duo HUD: the partner's name and health float above the partner's head (pinned to the screen
    /// edge when the partner is out of view), a countdown while this player is down, and a quiet
    /// banner while this player has picked and the partner is still choosing.
    /// </summary>
    public sealed partial class Hud
    {
        RectTransform partnerTag;
        CanvasGroup partnerGroup;
        TextMeshProUGUI partnerName, partnerState;
        Image partnerBack, partnerFill;
        float partnerHp = 1f;

        CanvasGroup downGroup;
        TextMeshProUGUI downCount, downSub;

        CanvasGroup waitGroup;
        TextMeshProUGUI waitText;

        TextMeshProUGUI restartHint;

        const float PartnerBarW = 96f;

        void BuildCoop()
        {
            partnerTag = Node("Partner Tag", canvasRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 60f));
            partnerGroup = partnerTag.gameObject.AddComponent<CanvasGroup>();
            partnerGroup.alpha = 0f;
            partnerName = Text("Name", partnerTag, "", 17f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 12f), new Vector2(260f, 24f), true, true, 4f);
            partnerBack = Img("Back", partnerTag, UiArt.Pill, Palette.UiGlass.WithAlpha(0.88f), new Vector2(0f, -8f), new Vector2(PartnerBarW + 6f, 11f), Image.Type.Sliced);
            partnerFill = Img("Fill", partnerTag, UiArt.Pill, Palette.HpA, new Vector2(-PartnerBarW * 0.5f, -8f), new Vector2(PartnerBarW, 6f), Image.Type.Sliced);
            partnerFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            partnerState = Text("State", partnerTag, "", 13f, Palette.Hurt, TextAlignmentOptions.Center, new Vector2(0f, -26f), new Vector2(300f, 20f), true, true, 3f);

            var down = Node("Down", canvasRect, new Vector2(0.5f, 0.5f), new Vector2(0f, -190f), Vector2.zero);
            downGroup = down.gameObject.AddComponent<CanvasGroup>();
            downGroup.alpha = 0f;
            Img("Glow", down, UiArt.Glow, Palette.Hurt.WithAlpha(0.08f), Vector2.zero, new Vector2(900f, 300f));
            Text("Label", down, "AUSGESCHALTET", 15f, Palette.Hurt, TextAlignmentOptions.Center, new Vector2(0f, 62f), new Vector2(700f, 22f), true, true, 14f);
            downCount = Text("Count", down, "", 80f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 8f), new Vector2(400f, 96f), true, true, 4f);
            downSub = Text("Sub", down, "", 16f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, -48f), new Vector2(900f, 24f), true, true, 5f);

            var wait = Node("Waiting", canvasRect, new Vector2(0.5f, 1f), new Vector2(0f, -330f), Vector2.zero);
            waitGroup = wait.gameObject.AddComponent<CanvasGroup>();
            waitGroup.alpha = 0f;
            Img("Plate", wait, UiArt.Pill, Palette.UiGlass.WithAlpha(0.85f), Vector2.zero, new Vector2(620f, 46f), Image.Type.Sliced);
            waitText = Text("Text", wait, "", 17f, Palette.ShotCyan, TextAlignmentOptions.Center, Vector2.zero, new Vector2(600f, 30f), true, true, 5f);

            var restart = deathGroup.transform.Find("Restart");
            if (restart != null) restartHint = restart.GetComponent<TextMeshProUGUI>();
        }

        void UpdateCoop(float dt)
        {
            var s = Coop.S;
            bool on = Coop.Active;
            var remote = Game.I.Remote;
            string partner = PartnerName();

            // ---- the partner's tag
            float tagA = 0f;
            if (on && remote.Present)
            {
                var p = remote.P;
                bool down = s.RemoteDown || p.Dead;
                Vector2 want = WorldToCanvas(p.Pos + new Vector2(0f, down ? 0.6f : 2.55f));
                // off screen: pinned to the edge, pointing the way
                Rect r = canvasRect.rect;
                float hx = r.width * 0.5f - 130f, hy = r.height * 0.5f - 90f;
                string arrow = "";
                if (want.x < -hx) { want.x = -hx; arrow = "« "; }
                else if (want.x > hx) { want.x = hx; arrow = " »"; }
                want.y = Mathf.Clamp(want.y, -hy, hy);
                partnerTag.anchoredPosition = want;
                int ci = Mathf.Clamp(remote.Character, 0, Characters.All.Length - 1);
                Color accent = Characters.All[ci].Accent;
                partnerName.text = arrow == "« " ? arrow + partner : partner + arrow;
                partnerName.color = Color.Lerp(accent, Color.white, 0.45f);
                float frac = Mathf.Clamp01(p.Hp / Mathf.Max(1f, p.MaxHp));
                partnerHp = MathUtil.Damp(partnerHp, frac, 10f, dt);
                partnerFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(6f, PartnerBarW * partnerHp), 6f);
                partnerFill.color = Color.Lerp(Palette.HpA, Palette.HpB, partnerHp);
                partnerBack.enabled = partnerFill.enabled = !down;
                partnerState.text = down ? "AUSGESCHALTET  ·  " + Mathf.CeilToInt(Mathf.Max(0f, remote.DownLeft)) + " S" : "";
                tagA = 1f;
            }
            partnerGroup.alpha = MathUtil.Damp(partnerGroup.alpha, paused ? 0f : tagA, 10f, dt);

            // ---- this player is down
            bool localDown = on && s.LocalDown && Game.I.Director.P != RunDirector.Phase.RunOver;
            downGroup.alpha = MathUtil.Damp(downGroup.alpha, localDown && !paused ? 1f : 0f, 6f, dt);
            if (localDown)
            {
                downCount.text = Mathf.CeilToInt(Mathf.Max(0f, s.DownLeft)).ToString();
                downSub.text = remote.Present && !s.RemoteDown ? "DU SCHAUST " + partner + " ZU  ·  GLEICH GEHT'S WEITER" : "GLEICH GEHT'S WEITER";
            }

            // ---- picked, the partner still chooses
            bool waiting = on && s.WaitingForPartner && !Game.I.Rewards.IsOpen;
            waitGroup.alpha = MathUtil.Damp(waitGroup.alpha, waiting ? 1f : 0f, 8f, dt);
            if (waiting) waitText.text = "WARTE AUF " + partner + "  ·  " + new string('.', 1 + (int)(time * 2.5f) % 3);

            if (restartHint != null) restartHint.text = Coop.IsClient ? "DER HOST STARTET DEN NÄCHSTEN LAUF" : "[ ENTER ]  NEUER LAUF";
        }

        static string PartnerName()
        {
            var s = Coop.S;
            if (s != null && !string.IsNullOrEmpty(s.PartnerName)) return s.PartnerName.ToUpperInvariant();
            return "DEIN MITSPIELER";
        }
    }
}
