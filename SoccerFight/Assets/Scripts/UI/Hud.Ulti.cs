using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoccerFight
{
    /// <summary>
    /// Ulti im HUD: ein großes Abzeichen links neben den Fähigkeiten mit dem Gesicht des Spielers, um das
    /// sich der Ring füllt, je mehr Schaden er macht. Ist er voll, leuchtet das Abzeichen, ein Lichtpunkt
    /// kreist und „BEREIT“ steht darüber. Beim Auslösen fährt der Name der Ulti groß ins Bild; Dres Buzzer
    /// Beater bekommt dazu eine Wurfuhr (3-2-1-BUZZER).
    /// </summary>
    public sealed partial class Hud
    {
        const float UltiSize = 108f;

        RectTransform ultiRoot;
        Image ultiGlow, ultiBase, ultiFace, ultiShade, ultiRing, ultiFlash, ultiDot, ultiBadge, ultiBadgeRim;
        TextMeshProUGUI ultiKey, ultiLabel;
        float ultiPop, ultiPopVel, ultiFlashT = 99f, ultiShakeT = 99f, ultiShown = -1f;
        int ultiFaceIndex = -2;
        Vector2 ultiHome;
        bool ultiPlaced, ultiWasReady;

        CanvasGroup ultiBannerGroup;
        RectTransform ultiBanner, ultiBannerLineL, ultiBannerLineR;
        TextMeshProUGUI ultiBannerName, ultiBannerWho;
        Image ultiBannerGlow;
        float ultiBannerT = 99f;

        CanvasGroup clockGroup;
        RectTransform clockRt;
        TextMeshProUGUI clockText;
        float clockT = 99f, clockPop;

        void BuildUlti()
        {
            ultiRoot = Node("Ulti", canvasRect, new Vector2(1f, 0f), Vector2.zero, new Vector2(UltiSize, UltiSize));
            Color glass = Palette.UiGlass.WithAlpha(0.9f);
            ultiGlow = Img("Glow", ultiRoot, UiArt.Glow, Color.clear, Vector2.zero, Vector2.one * UltiSize * 2.3f);
            Img("Shadow", ultiRoot, UiArt.Glow, new Color(0f, 0f, 0.02f, 0.5f), new Vector2(0f, -5f), Vector2.one * UltiSize * 1.5f);
            ultiBase = Img("Base", ultiRoot, UiArt.Circle, glass, Vector2.zero, Vector2.one * UltiSize);
            ultiFace = Img("Face", ultiRoot, null, Color.white, new Vector2(0f, -4f), Vector2.one * UltiSize * 0.74f);
            ultiFace.preserveAspect = true;
            ultiFace.material = Art.UiFigureMat;
            ultiFace.enabled = false;
            ultiShade = Img("Charge", ultiRoot, UiArt.Circle, new Color(0.01f, 0.02f, 0.05f, 0.6f), Vector2.zero, Vector2.one * (UltiSize - 2f), Image.Type.Filled);
            ultiShade.fillMethod = Image.FillMethod.Radial360;
            ultiShade.fillOrigin = (int)Image.Origin360.Top;
            ultiShade.fillClockwise = false;
            Img("Rim", ultiRoot, UiArt.RingThin, Color.white.WithAlpha(0.2f), Vector2.zero, Vector2.one * (UltiSize + 12f));
            ultiRing = Img("Ring", ultiRoot, UiArt.RingThick, Color.white, Vector2.zero, Vector2.one * UltiSize, Image.Type.Filled);
            ultiRing.fillMethod = Image.FillMethod.Radial360;
            ultiRing.fillOrigin = (int)Image.Origin360.Top;
            ultiRing.fillClockwise = true;
            ultiDot = Img("Dot", ultiRoot, UiArt.Glow, Color.clear, Vector2.zero, new Vector2(30f, 30f));
            ultiFlash = Img("Flash", ultiRoot, UiArt.RingThick, Color.clear, Vector2.zero, Vector2.one * UltiSize);
            ultiLabel = Text("Label", ultiRoot, "ULTI", 15f, Palette.UiMuted, TextAlignmentOptions.Center, new Vector2(0f, UltiSize * 0.5f + 16f), new Vector2(200f, 22f), true, true, 5f);
            Vector2 badgePos = new Vector2(0f, -UltiSize * 0.5f - 4f);
            ultiBadgeRim = Img("BadgeRim", ultiRoot, UiArt.Pill, Color.white.WithAlpha(0.16f), badgePos, new Vector2(36f, 28f), Image.Type.Sliced);
            ultiBadge = Img("Badge", ultiRoot, UiArt.Pill, Palette.UiGlass.WithAlpha(0.97f), badgePos, new Vector2(34f, 26f), Image.Type.Sliced);
            ultiKey = Text("Key", ultiBadge.rectTransform, "", 15f, Palette.UiText, TextAlignmentOptions.Center, Vector2.zero, new Vector2(120f, 26f), true, false, 1f);
            ultiRoot.gameObject.SetActive(false);

            // der Name der Ulti, groß in der Bildmitte
            ultiBanner = Node("Ulti Banner", canvasRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1400f, 200f));
            ultiBannerGroup = ultiBanner.gameObject.AddComponent<CanvasGroup>();
            ultiBannerGroup.alpha = 0f;
            Img("Band", ultiBanner, UiArt.Glow, new Color(0.01f, 0.01f, 0.03f, 0.7f), new Vector2(0f, 10f), new Vector2(1700f, 300f));
            ultiBannerGlow = Img("Glow", ultiBanner, UiArt.Glow, Color.clear, new Vector2(0f, -6f), new Vector2(1100f, 220f));
            ultiBannerWho = Text("Who", ultiBanner, "", 22f, Palette.UiText, TextAlignmentOptions.Center, new Vector2(0f, 62f), new Vector2(900f, 30f), true, true, 14f);
            ultiBannerName = Text("Name", ultiBanner, "", 86f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, -6f), new Vector2(1400f, 110f), true, true, 10f);
            ultiBannerLineL = Img("LineL", ultiBanner, UiArt.LineFade, Color.white.WithAlpha(0.6f), new Vector2(-330f, 62f), new Vector2(220f, 2f)).rectTransform;
            ultiBannerLineR = Img("LineR", ultiBanner, UiArt.LineFade, Color.white.WithAlpha(0.6f), new Vector2(330f, 62f), new Vector2(220f, 2f)).rectTransform;
            ultiBannerLineR.localScale = new Vector3(-1f, 1f, 1f);

            // Dres Wurfuhr
            clockRt = Node("Shot Clock", canvasRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(600f, 140f));
            clockGroup = clockRt.gameObject.AddComponent<CanvasGroup>();
            clockGroup.alpha = 0f;
            clockText = Text("Clock", clockRt, "", 110f, Palette.HoopFlame, TextAlignmentOptions.Center, Vector2.zero, new Vector2(900f, 140f), true, true, 8f);
        }

        void RefreshUltiBinding()
        {
            if (ultiKey == null) return;
            string label = KeyBindings.ShortName(GameAction.Ulti);
            ultiKey.text = label;
            float w = Mathf.Max(30f, 16f + label.Length * 11f);
            ultiBadge.rectTransform.sizeDelta = new Vector2(w, 26f);
            ultiBadgeRim.rectTransform.sizeDelta = new Vector2(w + 2f, 28f);
        }

        /// <summary>Links neben der letzten Fähigkeit, mit etwas mehr Abstand.</summary>
        void PlaceUlti(float x, float dt)
        {
            bool show = player.Ulti != UltiKind.None;
            if (ultiRoot.gameObject.activeSelf != show) ultiRoot.gameObject.SetActive(show);
            if (!show) { ultiPlaced = false; return; }
            x -= SlotGap * 1.6f + UltiSize * 0.5f;
            var target = new Vector2(x, SlotBottom + UltiSize * 0.5f);
            ultiHome = ultiPlaced ? Vector2.Lerp(ultiHome, target, 1f - Mathf.Exp(-14f * dt)) : target;
            ultiPlaced = true;
        }

        public void OnUltiReady(UltiKind kind)
        {
            ultiFlashT = 0f;
            ultiPopVel += 16f;
            ShowToast("ULTI BEREIT  ·  " + KeyBindings.DisplayName(GameAction.Ulti) + " DRÜCKEN");
        }

        public void OnUltiNotReady()
        {
            ultiShakeT = 0f;
            ShowToast("ULTI LÄDT NOCH  ·  " + Mathf.FloorToInt(player.UltiCharge * 100f) + " %  ·  MACH SCHADEN");
        }

        public void ShowUltiBanner(UltiKind kind, string name, string who)
        {
            ultiBannerT = 0f;
            ultiPopVel -= 12f;
            Color c = UltiDefs.Accent(kind);
            ultiBannerName.text = name;
            ultiBannerName.color = Color.Lerp(c, Color.white, 0.18f);
            ultiBannerWho.text = who + "  ·  ULTI";
            ultiBannerWho.color = Color.Lerp(c, Color.white, 0.55f);
            ultiBannerGlow.color = c.WithAlpha(0.16f);
            bannerT = 99f;
        }

        /// <summary>Die Wurfuhr: 3, 2, 1, dann BUZZER (0).</summary>
        public void ShotClock(int n)
        {
            clockT = 0f;
            clockPop = 1f;
            clockText.text = n > 0 ? n.ToString() : "BUZZER!";
            clockText.color = n > 0 ? Palette.HoopFlame : Palette.Gold;
            clockText.fontSize = n > 0 ? 120f : 96f;
        }

        void ResetUltiHud()
        {
            ultiBannerT = 99f;
            ultiBannerGroup.alpha = 0f;
            clockT = 99f;
            clockGroup.alpha = 0f;
            ultiWasReady = false;
            ultiShown = -1f;
            ultiFaceIndex = -2;
            RefreshUltiBinding();
        }

        void UpdateUlti(float dt)
        {
            UpdateUltiBanner(dt);
            if (!ultiRoot.gameObject.activeSelf) return;

            // das Gesicht des eigenen Spielers
            int face = Characters.Index;
            if (face != ultiFaceIndex)
            {
                ultiFaceIndex = face;
                var look = PlayerArt.Get(face);
                ultiFace.sprite = look != null ? look.Head : null;
                ultiFace.enabled = ultiFace.sprite != null;
            }

            Color c = UltiDefs.Accent(player.Ulti);
            float charge = Mathf.Clamp01(player.UltiCharge);
            ultiShown = ultiShown < 0f ? charge : charge < ultiShown ? charge : MathUtil.Damp(ultiShown, charge, 10f, dt);
            bool ready = player.UltiReady && !player.Dead;
            if (ready && !ultiWasReady) { ultiFlashT = 0f; ultiPopVel += 12f; }
            ultiWasReady = ready;

            ultiShade.fillAmount = 1f - ultiShown;
            ultiRing.fillAmount = ultiShown;
            float breathe = 0.5f + 0.5f * Mathf.Sin(time * 3.2f);
            ultiRing.color = ready ? Color.Lerp(c, Color.white, 0.25f + 0.25f * breathe) : c.WithAlpha(0.9f);
            ultiGlow.color = c.WithAlpha(ready ? 0.14f + 0.1f * breathe : 0.02f * ultiShown);
            ultiFace.color = ready ? Color.white : Color.Lerp(new Color(0.55f, 0.6f, 0.7f), Color.white, ultiShown * 0.6f);
            ultiLabel.text = ready ? "BEREIT" : "ULTI";
            ultiLabel.color = ready ? Color.Lerp(c, Color.white, 0.4f + 0.3f * breathe) : Palette.UiMuted;

            // ein Lichtpunkt läuft an der Spitze des Rings, voll kreist er
            float r = UltiSize * 0.5f - 4f;
            float ang = ready ? 90f - time * 220f : 90f - ultiShown * 360f;
            ultiDot.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) * r;
            ultiDot.color = (ready || ultiShown > 0.01f) ? Color.Lerp(c, Color.white, 0.5f).WithAlpha(ready ? 0.95f : 0.8f) : Color.clear;

            ultiFlashT += dt;
            float ft = ultiFlashT / 0.6f;
            if (ft < 1f)
            {
                float fs = Mathf.Lerp(1f, 1.8f, MathUtil.EaseOutCubic(ft));
                ultiFlash.rectTransform.localScale = new Vector3(fs, fs, 1f);
                ultiFlash.color = Color.Lerp(c, Color.white, 0.4f).WithAlpha(1f - ft);
            }
            else ultiFlash.color = Color.clear;

            MathUtil.Spring(ref ultiPop, ref ultiPopVel, 0f, 3.2f, 0.35f, dt);
            float scale = 1f + ultiPop * 0.05f + (ready ? 0.025f * breathe : 0f);
            ultiRoot.localScale = new Vector3(scale, scale, 1f);
            ultiShakeT += dt;
            float shake = ultiShakeT < 0.4f ? Mathf.Sin(ultiShakeT * 60f) * 8f * (1f - ultiShakeT / 0.4f) : 0f;
            ultiRoot.anchoredPosition = ultiHome + new Vector2(shake, 0f);
        }

        void UpdateUltiBanner(float dt)
        {
            ultiBannerT += dt;
            float t = ultiBannerT;
            const float hold = 0.95f, fade = 0.3f;
            if (t > hold + fade) { ultiBannerGroup.alpha = 0f; }
            else
            {
                float inK = MathUtil.EaseOutCubic(Mathf.Clamp01(t / 0.18f));
                float outK = MathUtil.Smooth01((t - hold) / fade);
                ultiBannerGroup.alpha = inK * (1f - outK);
                float s = Mathf.Lerp(1.35f, 1f, inK) * Mathf.Lerp(1f, 1.04f, t / (hold + fade));
                ultiBannerName.rectTransform.localScale = new Vector3(s, s, 1f);
                ultiBanner.anchoredPosition = new Vector2(Mathf.Lerp(-60f, 0f, inK), 150f + 14f * outK);
                float spread = Mathf.Lerp(150f, 330f, inK);
                ultiBannerLineL.anchoredPosition = new Vector2(-spread, 62f);
                ultiBannerLineR.anchoredPosition = new Vector2(spread, 62f);
            }

            clockT += dt;
            clockPop = Mathf.Max(0f, clockPop - dt * 5f);
            bool buzzer = clockText.text.Length > 2;
            float life = buzzer ? 1.1f : 0.6f;
            clockGroup.alpha = clockT < life ? 1f - MathUtil.Smooth01((clockT - life * 0.6f) / (life * 0.4f)) : 0f;
            float cs = 1f + 0.35f * clockPop * clockPop;
            clockRt.localScale = new Vector3(cs, cs, 1f);
        }
    }
}
