using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// The extra sprites and text materials only the title screen needs: screen vignette and bottom
    /// fade, the lock-on bracket, impact shapes for the kicked ball, and the two logo materials
    /// (a lit face with a cyan halo, and the dark slab used for the extruded copies behind it).
    /// </summary>
    public static class MenuArt
    {
        public static Sprite Vignette, FadeUp, Bracket, Spark, Chevron, Shock, Burst;
        public static Material LogoFace, LogoDeep;

        static bool built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { built = false; }

        public static void Build()
        {
            if (built) return;
            built = true;
            UiArt.Build();
            const float D = 2f;

            // corner darkening, stretched over the whole screen
            var vg = new SdfCanvas(new Rect(-64, -64, 128, 128), 1f);
            vg.Field(p => new Color(0f, 0f, 0f, Mathf.Pow(Mathf.Clamp01(p.magnitude / 64f), 2.6f)));
            Vignette = UiArt.ToUi(vg, "MenuVignette");

            // opaque at the bottom, gone at the top: keeps the footer legible over the live world
            var fade = new SdfCanvas(new Rect(-4, -64, 8, 128), 1f);
            fade.Field(p => new Color(0f, 0f, 0f, Mathf.Pow(Mathf.Clamp01((64f - p.y) / 128f), 1.7f)));
            FadeUp = UiArt.ToUi(fade, "MenuFadeUp");

            // lock-on corner (top-left); the other three are rotated copies
            var br = new SdfCanvas(new Rect(-24, -24, 48, 48), D);
            br.Fill(p => Sdf.Union(Sdf.Box(p, new Vector2(-9f, 19f), new Vector2(11f, 2f), 1.4f),
                                   Sdf.Box(p, new Vector2(-19f, 9f), new Vector2(2f, 11f), 1.4f)), Color.white);
            Bracket = UiArt.ToUi(br, "MenuBracket");

            // impact streak: thick end trails the flight direction
            var sp = new SdfCanvas(new Rect(-20, -6, 40, 12), D * 2f);
            sp.Fill(p => Sdf.Tapered(p, new Vector2(-18f, 0f), 0.7f, new Vector2(16f, 0f), 3.4f), Color.white);
            Spark = UiArt.ToUi(sp, "MenuSpark");

            var cv = new SdfCanvas(new Rect(-16, -16, 32, 32), D * 2f);
            float Chev(Vector2 p, float x) => Sdf.Union(Sdf.Capsule(p, new Vector2(x - 5f, 8f), new Vector2(x + 3f, 0f), 2.2f),
                                                        Sdf.Capsule(p, new Vector2(x + 3f, 0f), new Vector2(x - 5f, -8f), 2.2f));
            cv.Fill(p => Sdf.Union(Chev(p, -4f), Chev(p, 6f)), Color.white);
            Chevron = UiArt.ToUi(cv, "MenuChevron");

            // soft shockwave ring (no hard edges: it scales up a lot)
            var sh = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            sh.Field(p =>
            {
                float d = (p.magnitude / 64f - 0.84f) / 0.075f;
                return new Color(1f, 1f, 1f, Mathf.Exp(-d * d));
            });
            Shock = UiArt.ToUi(sh, "MenuShock");

            // four-point flash for the moment of contact
            var bu = new SdfCanvas(new Rect(-64, -64, 128, 128), D);
            bu.Field(p =>
            {
                float r = p.magnitude / 64f;
                float core = Mathf.Exp(-r * r * 16f);
                float spikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(Mathf.Atan2(p.y, p.x) * 2f)), 7f) * Mathf.Exp(-r * 3.4f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(core + spikes * 0.95f));
            });
            Burst = UiArt.ToUi(bu, "MenuBurst");

            BuildLogoMaterials();
        }

        /// <summary>
        /// TMP distance-field materials. The face carries a dark keyline plus a cyan halo (underlay
        /// with no offset), the slab copies behind it only get the keyline so the stack reads as depth.
        /// </summary>
        static void BuildLogoMaterials()
        {
            var src = UiArt.FontBold != null ? UiArt.FontBold.material : null;
            if (src == null) return;

            LogoFace = new Material(src) { name = "SF Logo Face" };
            LogoFace.EnableKeyword("OUTLINE_ON");
            LogoFace.SetColor("_OutlineColor", new Color(0.015f, 0.055f, 0.1f, 1f));
            LogoFace.SetFloat("_OutlineWidth", 0.11f);
            LogoFace.SetFloat("_OutlineSoftness", 0.015f);
            LogoFace.SetFloat("_FaceDilate", 0.06f);
            LogoFace.EnableKeyword("UNDERLAY_ON");
            LogoFace.SetColor("_UnderlayColor", new Color(0.36f, 0.94f, 1f, 0.45f));
            LogoFace.SetFloat("_UnderlayOffsetX", 0f);
            LogoFace.SetFloat("_UnderlayOffsetY", 0f);
            LogoFace.SetFloat("_UnderlayDilate", 0.35f);
            LogoFace.SetFloat("_UnderlaySoftness", 0.75f);

            LogoDeep = new Material(src) { name = "SF Logo Deep" };
            LogoDeep.EnableKeyword("OUTLINE_ON");
            LogoDeep.SetColor("_OutlineColor", new Color(0.02f, 0.07f, 0.12f, 1f));
            LogoDeep.SetFloat("_OutlineWidth", 0.13f);
            LogoDeep.SetFloat("_OutlineSoftness", 0.02f);
            LogoDeep.SetFloat("_FaceDilate", 0.06f);
        }
    }
}
