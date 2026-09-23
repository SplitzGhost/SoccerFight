using UnityEngine;

namespace SoccerFight
{
    /// <summary>The rig parts, so a character swap can re-bind every sprite.</summary>
    public enum PlayerPart { Torso, Pelvis, Neck, Head, HairTuft, Thigh, Shin, Boot, BootGlow, UpperArm, Forearm, Hand, UpperArmNear, ForearmNear }

    /// <summary>Skeleton measurements shared by the art and the procedural rig (world units).</summary>
    public static class PlayerDims
    {
        public const float ThighLen = 0.40f;
        public const float ShinLen = 0.40f;
        public const float UpperArmLen = 0.27f;
        public const float ForearmLen = 0.25f;
        public const float AnkleHeight = 0.10f;   // ankle joint above ground with the boot flat
        public const float TorsoLen = 0.54f;      // hip joint → shoulder joint
        public const float NeckLen = 0.075f;
        public const float HeadR = 0.19f;
        public const float StandHip = AnkleHeight + ThighLen + ShinLen - 0.045f;
        public const float Ppu = 360f;
    }

    /// <summary>One character's finished body sprites, with the build and sport they were drawn for.</summary>
    public sealed class PlayerLook
    {
        public Sprite Torso, Pelvis, Neck, Head, HairTuft;
        public Sprite Thigh, Shin, Boot, BootGlow;
        public Sprite UpperArm, Forearm, Hand;
        /// <summary>The front arm when it differs (a compression sleeve on the throwing arm); null: same as the back arm.</summary>
        public Sprite UpperArmNear, ForearmNear;
        public PlayerBody Body = PlayerBody.Soccer;
        public Sport Sport;
    }

    /// <summary>
    /// Kit, skin and hair for the side-view players. Every part pivots at its joint. Parts carry
    /// form shading, cloth detail and a thin dark contour; the moonlit rim and the grass bounce
    /// come from the character shader at runtime so they follow every pose and the facing. The
    /// colours come from the chosen character's kit and the bones from its build; soccer players
    /// share one body, the basketball players (PlayerArt.Hoops) are drawn with their own.
    /// </summary>
    public static partial class PlayerArt
    {
        public static Sprite Torso, Pelvis, Neck, Head, HairTuft;
        public static Sprite Thigh, Shin, Boot, BootGlow;
        public static Sprite UpperArm, Forearm, Hand;
        public static Sprite UpperArmNear, ForearmNear;
        static PlayerBody body = PlayerBody.Soccer;
        static Sport sport;
        /// <summary>The look of the selected character (what a freshly built rig should wear).</summary>
        public static PlayerLook Current { get; private set; }

        const float P = PlayerDims.Ppu;
        const float LineW = 0.0105f;

        /// <summary>Colours of the character currently being drawn.</summary>
        static CharacterKit K;
        /// <summary>Bones of the character currently being drawn.</summary>
        static PlayerBody Bd => body;
        static readonly PlayerLook[] looks = new PlayerLook[Characters.All.Length];

        // bodies asked for by the menus, drawn a quarter at a time (legs, arms, body, head) so a
        // page full of new characters never freezes a frame for long
        static readonly System.Collections.Generic.List<int> requested = new System.Collections.Generic.List<int>();
        static PlayerLook building;
        static int buildingIndex = -1, buildingStep;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            for (int i = 0; i < looks.Length; i++) looks[i] = null;
            requested.Clear();
            building = null;
            buildingIndex = -1;
            buildingStep = 0;
            Current = null;
        }

        public static bool Ready(int index) => index >= 0 && index < looks.Length && looks[index] != null;

        /// <summary>The look if it is built, else null (and it is queued).</summary>
        public static PlayerLook Peek(int index)
        {
            if (Ready(index)) return looks[index];
            Request(index);
            return null;
        }

        /// <summary>Queue a character's body; Pump draws it over the next frames.</summary>
        public static void Request(int index)
        {
            if (index < 0 || index >= looks.Length || Ready(index) || index == buildingIndex || requested.Contains(index)) return;
            requested.Add(index);
        }

        /// <summary>Points the drawing state (kit, bones, sport) at a character.</summary>
        static void Target(CharacterDef def)
        {
            K = def.Kit;
            body = def.Body ?? PlayerBody.Soccer;
            sport = def.Sport;
        }

        static void BuildStep(int step)
        {
            bool hoops = sport == Sport.Basketball;
            switch (step)
            {
                case 0: if (hoops) HoopsLegs(); else BuildLegs(); break;
                case 1: if (hoops) HoopsArms(); else { BuildArms(); UpperArmNear = ForearmNear = null; } break;
                case 2: if (hoops) HoopsBody(); else BuildBody(); break;
                default: if (hoops) HoopsHead(); else BuildHead(); break;
            }
        }

        /// <summary>Draws one quarter of the next queued body. Call once per frame while a menu waits for bodies.</summary>
        public static void Pump()
        {
            if (buildingIndex < 0)
            {
                while (requested.Count > 0 && Ready(requested[0])) requested.RemoveAt(0);
                if (requested.Count == 0) return;
                buildingIndex = requested[0];
                requested.RemoveAt(0);
                building = new PlayerLook();
                buildingStep = 0;
            }
            var previous = Snapshot();
            var def = Characters.All[buildingIndex];
            Target(def);
            BuildStep(buildingStep);
            switch (buildingStep)
            {
                case 0: building.Thigh = Thigh; building.Shin = Shin; building.Boot = Boot; building.BootGlow = BootGlow; break;
                case 1: building.UpperArm = UpperArm; building.Forearm = Forearm; building.Hand = Hand; building.UpperArmNear = UpperArmNear; building.ForearmNear = ForearmNear; break;
                case 2: building.Torso = Torso; building.Pelvis = Pelvis; building.Neck = Neck; break;
                default: building.Head = Head; building.HairTuft = HairTuft; break;
            }
            building.Body = body;
            building.Sport = sport;
            Restore(previous);
            if (++buildingStep < 4) return;
            looks[buildingIndex] = building;
            building = null;
            buildingIndex = -1;
        }

        /// <summary>Builds the chosen character (the others are drawn when the select screen asks for them).</summary>
        public static void Build()
        {
            Characters.Load();
            Use(Characters.Index);
        }

        /// <summary>
        /// The finished sprite set of a character — built once, then cached. Building writes through
        /// the static fields, so whoever was active before is put back afterwards.
        /// </summary>
        public static PlayerLook Get(int index)
        {
            index = Mathf.Clamp(index, 0, Characters.All.Length - 1);
            if (looks[index] != null) return looks[index];
            if (buildingIndex == index) { building = null; buildingIndex = -1; }   // finish it in one go instead
            requested.Remove(index);

            var previous = Snapshot();
            Target(Characters.All[index]);
            for (int step = 0; step < 4; step++) BuildStep(step);
            looks[index] = Snapshot();
            Restore(previous);
            return looks[index];
        }

        static PlayerLook Snapshot() => new PlayerLook
        {
            Torso = Torso, Pelvis = Pelvis, Neck = Neck, Head = Head, HairTuft = HairTuft,
            Thigh = Thigh, Shin = Shin, Boot = Boot, BootGlow = BootGlow,
            UpperArm = UpperArm, Forearm = Forearm, Hand = Hand, UpperArmNear = UpperArmNear, ForearmNear = ForearmNear,
            Body = body, Sport = sport,
        };

        static void Restore(PlayerLook l)
        {
            Torso = l.Torso; Pelvis = l.Pelvis; Neck = l.Neck; Head = l.Head; HairTuft = l.HairTuft;
            Thigh = l.Thigh; Shin = l.Shin; Boot = l.Boot; BootGlow = l.BootGlow;
            UpperArm = l.UpperArm; Forearm = l.Forearm; Hand = l.Hand; UpperArmNear = l.UpperArmNear; ForearmNear = l.ForearmNear;
            body = l.Body ?? PlayerBody.Soccer; sport = l.Sport;
        }

        /// <summary>Makes a character's sprites the ones the rig draws.</summary>
        public static void Use(int index)
        {
            var l = Get(index);
            Restore(l);
            Current = l;
        }


        /// <summary>Sprite of one rig part for the current look (used when the character changes).</summary>
        public static Sprite SpriteOf(PlayerPart part) => SpriteOf(Snapshot(), part);

        public static Sprite SpriteOf(PlayerLook look, PlayerPart part)
        {
            switch (part)
            {
                case PlayerPart.Torso: return look.Torso;
                case PlayerPart.Pelvis: return look.Pelvis;
                case PlayerPart.Neck: return look.Neck;
                case PlayerPart.Head: return look.Head;
                case PlayerPart.HairTuft: return look.HairTuft;
                case PlayerPart.Thigh: return look.Thigh;
                case PlayerPart.Shin: return look.Shin;
                case PlayerPart.Boot: return look.Boot;
                case PlayerPart.BootGlow: return look.BootGlow;
                case PlayerPart.UpperArm: return look.UpperArm;
                case PlayerPart.Forearm: return look.Forearm;
                case PlayerPart.UpperArmNear: return look.UpperArmNear != null ? look.UpperArmNear : look.UpperArm;
                case PlayerPart.ForearmNear: return look.ForearmNear != null ? look.ForearmNear : look.Forearm;
                default: return look.Hand;
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Thin dark contour behind a shape. It stays open above cutAbove and below cutBelow so the
        /// ends that overlap a neighbouring part join without a seam.</summary>
        static void Contour(SdfCanvas c, SdfCanvas.SdfFn shape, float cutAbove = 99f, float cutBelow = -99f)
            => c.Fill(p => Sdf.Intersect(Sdf.Intersect(shape(p) - LineW, p.y - cutAbove), cutBelow - p.y), Palette.PlayerLine);

        /// <summary>Darken the trailing (back) side of a part for volume.</summary>
        static void ShadeBack(SdfCanvas c, float x0, float x1, float min)
            => c.Shade(p => Mathf.Lerp(min, 1f, MathUtil.Smooth01((p.x - x0) / (x1 - x0))));

        /// <summary>Soft stroke inside existing pixels (folds, seams, highlights), clipped to a region.</summary>
        static void Stroke(SdfCanvas c, SdfCanvas.SdfFn clip, Vector2 a, Vector2 b, float r, Color col, float soft)
            => c.Paint(p => Sdf.Intersect(Sdf.Capsule(p, a, b, r), clip(p)), col, soft);

        /// <summary>Blend a region towards a color with a per-pixel weight (0..1).</summary>
        static void Tint(SdfCanvas c, SdfCanvas.SdfFn region, Color col, System.Func<Vector2, float> weight)
            => c.Paint(p => region(p) < 0f ? col.WithAlpha(Mathf.Clamp01(weight(p))) : Color.clear);

        // ------------------------------------------------------------------ legs

        static void BuildLegs()
        {
            // Thigh with the shorts' leg opening on top.
            {
                const float L = PlayerDims.ThighLen;
                var c = new SdfCanvas(new Rect(-0.16f, -L - 0.1f, 0.32f, L + 0.26f), P);
                SdfCanvas.SdfFn leg = p => Sdf.SmoothUnion(
                    Sdf.Tapered(p, Vector2.zero, 0.09f, new Vector2(0f, -L), 0.066f),
                    Sdf.Ellipse(p, new Vector2(0.02f, -0.19f), new Vector2(0.066f, 0.12f)), 0.04f);
                SdfCanvas.SdfFn shorts = p => Sdf.Intersect(
                    Sdf.Box(p, new Vector2(0.004f, -0.06f), new Vector2(0.12f, 0.155f), 0.075f),
                    Sdf.HalfPlane(p, new Vector2(0f, -0.2f), new Vector2(-0.22f, -1f)));

                // the knee end lies over the shin: no contour there
                Contour(c, p => Sdf.Union(leg(p), shorts(p)), -0.03f, -L + 0.04f);
                c.Fill(leg, K.Skin);
                ShadeBack(c, -0.09f, 0.02f, 0.8f);
                c.Paint(p => Sdf.Ellipse(p, new Vector2(0.042f, -0.26f), new Vector2(0.022f, 0.08f)), K.SkinLight.WithAlpha(0.45f), 0.035f);
                c.Paint(p => Sdf.Circle(p, new Vector2(0.034f, -L + 0.012f), 0.03f), K.SkinLight.WithAlpha(0.35f), 0.03f);

                c.Fill(p => shorts(p) - LineW * 0.8f, Palette.PlayerLine);
                c.Fill(shorts, K.KitWhite);
                Tint(c, shorts, K.KitWhiteShade, p => 0.8f * (1f - MathUtil.Smooth01((p.x + 0.12f) / 0.14f)));
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.x - 0.008f) - 0.017f, shorts(p)), K.Jersey);
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.x - 0.008f) - 0.005f, shorts(p)), K.JerseyShade.WithAlpha(0.5f));
                Stroke(c, shorts, new Vector2(-0.11f, -0.186f), new Vector2(0.11f, -0.216f), 0.012f, K.KitWhiteShade.WithAlpha(0.7f), 0.01f);
                Stroke(c, shorts, new Vector2(0.03f, 0.0f), new Vector2(0.085f, -0.11f), 0.006f, K.KitWhiteShade.WithAlpha(0.45f), 0.012f);
                Thigh = c.ToSprite("Thigh", Vector2.zero);
            }

            // Shin: knee cap, calf, crimson sock with a cream double band, shin-guard bulge in front.
            {
                const float L = PlayerDims.ShinLen;
                var c = new SdfCanvas(new Rect(-0.14f, -L - 0.08f, 0.28f, L + 0.18f), P);
                SdfCanvas.SdfFn shin = p => Sdf.SmoothUnion(
                    Sdf.SmoothUnion(
                        Sdf.Tapered(p, Vector2.zero, 0.068f, new Vector2(0, -L), 0.046f),
                        Sdf.Ellipse(p, new Vector2(-0.026f, -0.13f), new Vector2(0.062f, 0.12f)), 0.03f),
                    Sdf.Ellipse(p, new Vector2(0.024f, -0.19f), new Vector2(0.05f, 0.11f)), 0.02f);

                Contour(c, shin, 0f);
                c.Fill(shin, K.Skin);
                SdfCanvas.SdfFn sock = p => Sdf.Intersect(shin(p), p.y + 0.09f);
                c.Paint(sock, K.Jersey);
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.y + 0.108f) - 0.016f, shin(p)), K.KitWhite);
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.y + 0.157f) - 0.0065f, shin(p)), K.KitWhite);
                ShadeBack(c, -0.085f, 0.015f, 0.76f);
                Tint(c, sock, K.JerseyShade, p => 0.55f * (1f - MathUtil.Smooth01((p.x + 0.08f) / 0.09f)));
                Stroke(c, sock, new Vector2(0.038f, -0.18f), new Vector2(0.032f, -0.31f), 0.011f, K.JerseyLight.WithAlpha(0.6f), 0.014f);
                Shin = c.ToSprite("Shin", Vector2.zero);
            }

            // Boot: dark leather lit from above, cream stripe, laces, toe sheen, neon sole with studs.
            {
                var c = new SdfCanvas(new Rect(-0.15f, -0.15f, 0.4f, 0.24f), P);
                SdfCanvas.SdfFn upper = p =>
                {
                    float ankle = Sdf.Circle(p, new Vector2(-0.008f, -0.005f), 0.054f);
                    float foot = Sdf.Capsule(p, new Vector2(-0.05f, -0.045f), new Vector2(0.15f, -0.052f), 0.043f);
                    float toe = Sdf.Ellipse(p, new Vector2(0.155f, -0.058f), new Vector2(0.062f, 0.036f));
                    float heel = Sdf.Box(p, new Vector2(-0.045f, -0.058f), new Vector2(0.052f, 0.03f), 0.022f);
                    float d = Sdf.SmoothUnion(Sdf.SmoothUnion(ankle, foot, 0.04f), Sdf.Union(toe, heel), 0.03f);
                    return Sdf.Intersect(d, -(p.y + 0.084f));
                };
                SdfCanvas.SdfFn sole = p => Sdf.Box(p, new Vector2(0.052f, -0.09f), new Vector2(0.146f, 0.013f), 0.012f);

                Contour(c, p => Sdf.Union(upper(p), sole(p)), 0.02f);
                c.Fill(upper, p => Color.Lerp(K.Boot, K.BootLight,
                    0.6f * MathUtil.Smooth01((p.y + 0.07f) / 0.08f) * MathUtil.Smooth01((p.x + 0.04f) / 0.14f)));
                c.Paint(p => Sdf.Box(p, new Vector2(-0.07f, -0.056f), new Vector2(0.03f, 0.03f), 0.02f), K.Boot.WithAlpha(0.8f), 0.02f);
                c.Paint(p => Sdf.Capsule(p, new Vector2(-0.035f, -0.042f), new Vector2(0.105f, -0.024f), 0.0105f), K.KitWhite);
                for (int i = 0; i < 3; i++)
                {
                    float lx = 0.035f + i * 0.033f;
                    c.Paint(p => Sdf.Capsule(p, new Vector2(lx, -0.012f), new Vector2(lx + 0.016f, -0.02f), 0.005f), K.KitWhiteShade);
                }
                c.Paint(p => Sdf.Circle(p, new Vector2(0.172f, -0.042f), 0.028f), new Color(1f, 1f, 1f, 0.16f), 0.02f);
                c.Fill(sole, K.Neon);
                c.Paint(p => Sdf.Intersect(sole(p), -(p.y + 0.094f)), Color.Lerp(K.Neon, K.Boot, 0.35f));
                for (int i = 0; i < 3; i++)
                {
                    float sx = -0.05f + i * 0.09f;
                    c.Fill(p => Sdf.Box(p, new Vector2(sx, -0.104f), new Vector2(0.013f, 0.009f), 0.004f), K.Boot);
                }
                Boot = c.ToSprite("Boot", Vector2.zero);

                var g = new SdfCanvas(new Rect(-0.2f, -0.2f, 0.5f, 0.24f), 180f);
                g.Fill(p => Sdf.Capsule(p, new Vector2(-0.07f, -0.09f), new Vector2(0.17f, -0.09f), 0.004f), K.Neon, 0.07f);
                BootGlow = g.ToSprite("BootGlow", Vector2.zero);
            }
        }

        // ------------------------------------------------------------------ arms

        static void BuildArms()
        {
            // Upper arm in a crimson sleeve with a cream cuff.
            {
                const float L = PlayerDims.UpperArmLen;
                var c = new SdfCanvas(new Rect(-0.12f, -L - 0.08f, 0.24f, L + 0.19f), P);
                SdfCanvas.SdfFn arm = p => Sdf.SmoothUnion(
                    Sdf.Tapered(p, Vector2.zero, 0.058f, new Vector2(0, -L), 0.047f),
                    Sdf.Ellipse(p, new Vector2(0.012f, -0.15f), new Vector2(0.05f, 0.075f)), 0.03f);
                SdfCanvas.SdfFn sleeve = p => Sdf.Intersect(
                    Sdf.SmoothUnion(Sdf.Circle(p, new Vector2(0f, 0.005f), 0.08f),
                        Sdf.Tapered(p, Vector2.zero, 0.073f, new Vector2(0, -0.135f), 0.067f), 0.02f),
                    Sdf.HalfPlane(p, new Vector2(0, -0.135f), new Vector2(0.12f, -1f)));

                Contour(c, p => Sdf.Union(arm(p), sleeve(p)), 99f, -L + 0.035f);
                c.Fill(arm, K.Skin);
                ShadeBack(c, -0.06f, 0.012f, 0.8f);
                c.Paint(p => Sdf.Ellipse(p, new Vector2(0.03f, -0.17f), new Vector2(0.016f, 0.045f)), K.SkinLight.WithAlpha(0.4f), 0.025f);
                c.Fill(p => sleeve(p) - LineW * 0.8f, Palette.PlayerLine);
                c.Fill(sleeve, K.Jersey);
                Tint(c, sleeve, K.JerseyShade, p => 0.7f * (1f - MathUtil.Smooth01((p.x + 0.075f) / 0.1f)));
                Tint(c, sleeve, K.JerseyLight, p => 0.45f * MathUtil.Smooth01((p.x - 0.0f) / 0.07f) * MathUtil.Smooth01((p.y + 0.04f) / 0.06f));
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.y + 0.121f - 0.12f * p.x) - 0.012f, sleeve(p)), K.KitWhite);
                UpperArm = c.ToSprite("UpperArm", Vector2.zero);
            }

            // Forearm with a cream sweatband at the wrist.
            {
                const float L = PlayerDims.ForearmLen;
                var c = new SdfCanvas(new Rect(-0.1f, -L - 0.07f, 0.2f, L + 0.15f), P);
                SdfCanvas.SdfFn fore = p => Sdf.SmoothUnion(
                    Sdf.Tapered(p, Vector2.zero, 0.05f, new Vector2(0, -L), 0.04f),
                    Sdf.Ellipse(p, new Vector2(-0.008f, -0.07f), new Vector2(0.046f, 0.075f)), 0.03f);

                Contour(c, fore, 0f, -L + 0.02f);
                c.Fill(fore, K.Skin);
                ShadeBack(c, -0.05f, 0.012f, 0.8f);
                SdfCanvas.SdfFn band = p => Sdf.Intersect(Mathf.Abs(p.y + L - 0.04f) - 0.021f, fore(p) - 0.006f);
                c.Fill(p => band(p) - LineW * 0.7f, Palette.PlayerLine);
                c.Fill(band, K.KitWhite);
                Tint(c, band, K.KitWhiteShade, p => 0.8f * (1f - MathUtil.Smooth01((p.x + 0.045f) / 0.07f)));
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.y + L - 0.04f) - 0.0045f, band(p)), K.Jersey);
                Forearm = c.ToSprite("Forearm", Vector2.zero);
            }

            // Hand: loose fist with a thumb.
            {
                var c = new SdfCanvas(new Rect(-0.095f, -0.145f, 0.19f, 0.185f), P);
                SdfCanvas.SdfFn hand = p => Sdf.SmoothUnion(
                    Sdf.Ellipse(p, new Vector2(0f, -0.048f), new Vector2(0.049f, 0.062f)),
                    Sdf.Ellipse(p, new Vector2(0.036f, -0.03f), new Vector2(0.02f, 0.036f)), 0.015f);
                Contour(c, hand, -0.005f);
                c.Fill(hand, K.Skin);
                ShadeBack(c, -0.05f, 0.02f, 0.8f);
                Stroke(c, hand, new Vector2(-0.028f, -0.088f), new Vector2(0.018f, -0.094f), 0.005f, K.SkinShade.WithAlpha(0.6f), 0.008f);
                Stroke(c, hand, new Vector2(0.03f, -0.012f), new Vector2(0.036f, -0.05f), 0.005f, K.SkinShade.WithAlpha(0.45f), 0.008f);
                Hand = c.ToSprite("Hand", Vector2.zero);
            }
        }

        // ------------------------------------------------------------------ body

        static void BuildBody()
        {
            // Jersey: untucked hem, cream collar, twin pinstripes down the side, crest, soft folds.
            {
                var c = new SdfCanvas(new Rect(-0.25f, -0.13f, 0.5f, 0.77f), P);
                SdfCanvas.SdfFn torso = p =>
                {
                    float body = Sdf.Tapered(p, new Vector2(0f, 0.05f), 0.136f, new Vector2(0.008f, 0.42f), 0.158f);
                    float chest = Sdf.Ellipse(p, new Vector2(0.035f, 0.35f), new Vector2(0.158f, 0.15f));
                    float back = Sdf.Ellipse(p, new Vector2(-0.03f, 0.31f), new Vector2(0.14f, 0.2f));
                    float shoulder = Sdf.Circle(p, new Vector2(0f, 0.46f), 0.12f);
                    float d = Sdf.SmoothUnion(Sdf.SmoothUnion(Sdf.SmoothUnion(body, chest, 0.05f), back, 0.05f), shoulder, 0.04f);
                    return Sdf.Intersect(d, Sdf.HalfPlane(p, new Vector2(0f, -0.07f), new Vector2(0.06f, -1f)));
                };

                Contour(c, torso);
                c.Fill(torso, K.Jersey);
                // form: cool shadow on the back and low on the body, warm light across the chest
                Tint(c, torso, K.JerseyShade, p =>
                    0.75f * (1f - MathUtil.Smooth01((p.x + 0.15f) / 0.14f)) + 0.35f * (1f - MathUtil.Smooth01((p.y + 0.02f) / 0.2f)));
                Tint(c, torso, K.JerseyLight, p =>
                    0.5f * MathUtil.Smooth01((p.x - 0.02f) / 0.1f) * MathUtil.Smooth01((p.y - 0.25f) / 0.12f));
                // twin pinstripes along the side seam
                for (int s = -1; s <= 1; s += 2)
                {
                    float off = s * 0.021f;
                    c.Paint(p => Sdf.Intersect(Mathf.Abs(p.x + 0.012f - 0.07f * (p.y - 0.3f) + off) - 0.0055f, p.y - 0.5f), K.KitWhite.WithAlpha(0.9f));
                }
                // hem band and cloth folds
                c.Paint(p => Sdf.Intersect(torso(p), (p.y + 0.07f - 0.06f * p.x) - 0.022f), K.JerseyShade.WithAlpha(0.45f), 0.006f);
                Stroke(c, torso, new Vector2(-0.07f, 0.1f), new Vector2(0.07f, 0.145f), 0.006f, K.JerseyShade.WithAlpha(0.45f), 0.014f);
                Stroke(c, torso, new Vector2(-0.09f, 0.02f), new Vector2(0.05f, 0.06f), 0.005f, K.JerseyShade.WithAlpha(0.4f), 0.014f);
                Stroke(c, torso, new Vector2(0.03f, 0.2f), new Vector2(0.13f, 0.25f), 0.005f, K.JerseyShade.WithAlpha(0.3f), 0.014f);
                // collar: a cream band just inside the contour where the neck comes out
                c.Paint(p => Sdf.Intersect(Sdf.Intersect(torso(p) + LineW, -(torso(p) + LineW + 0.024f)),
                    Sdf.Intersect(Mathf.Abs(p.x - 0.02f) - 0.095f, 0.47f - p.y)), K.KitWhite, 0.004f);
                // crest on the chest: cream shield with a crimson star point
                SdfCanvas.SdfFn crest = p => Sdf.SmoothUnion(
                    Sdf.Box(p, new Vector2(0.1f, 0.405f), new Vector2(0.024f, 0.018f), 0.006f),
                    Sdf.Triangle(p, new Vector2(0.076f, 0.395f), new Vector2(0.124f, 0.395f), new Vector2(0.1f, 0.358f)), 0.006f);
                c.Paint(crest, K.KitWhite);
                c.Paint(p => Sdf.Circle(p, new Vector2(0.1f, 0.395f), 0.008f), K.JerseyShade);
                Torso = c.ToSprite("Torso", Vector2.zero);
            }

            // Shorts: cream, crimson side stripe, teal-grey shade at the back, crotch fold.
            {
                var c = new SdfCanvas(new Rect(-0.21f, -0.19f, 0.42f, 0.32f), P);
                SdfCanvas.SdfFn shorts = p => Sdf.Box(p, new Vector2(0.005f, -0.03f), new Vector2(0.155f, 0.11f), 0.075f);
                Contour(c, shorts);
                c.Fill(shorts, K.KitWhite);
                Tint(c, shorts, K.KitWhiteShade, p => 0.85f * (1f - MathUtil.Smooth01((p.x + 0.15f) / 0.16f)) + 0.3f * (1f - MathUtil.Smooth01((p.y + 0.12f) / 0.08f)));
                c.Paint(p => Mathf.Abs(p.x + 0.005f) - 0.018f, K.Jersey);
                c.Paint(p => Mathf.Abs(p.x + 0.005f) - 0.005f, K.JerseyShade.WithAlpha(0.5f));
                Stroke(c, shorts, new Vector2(0.06f, -0.03f), new Vector2(0.1f, -0.12f), 0.006f, K.KitWhiteShade.WithAlpha(0.6f), 0.012f);
                Pelvis = c.ToSprite("Pelvis", Vector2.zero);
            }

            // Neck.
            {
                var c = new SdfCanvas(new Rect(-0.09f, -0.07f, 0.18f, 0.24f), P);
                SdfCanvas.SdfFn neck = p => Sdf.Capsule(p, Vector2.zero, new Vector2(0.01f, 0.1f), 0.056f);
                Contour(c, neck);
                c.Fill(neck, K.Skin);
                ShadeBack(c, -0.05f, 0.03f, 0.72f);
                c.Shade(p => Mathf.Lerp(0.78f, 1f, MathUtil.Smooth01((p.y - 0.05f) / -0.08f)));
                Neck = c.ToSprite("Neck", Vector2.zero);
            }
        }

        // ------------------------------------------------------------------ head

        static readonly Vector2 HeadCenter = new Vector2(0f, 0.19f);

        /// <summary>Skull, face, jaw and nose in profile (every character shares the head shape).</summary>
        static float HeadShape(Vector2 p)
        {
            float cranium = Sdf.Circle(p, HeadCenter, PlayerDims.HeadR);
            float face = Sdf.Box(p, new Vector2(0.07f, 0.11f), new Vector2(0.112f, 0.086f), 0.075f);
            float jaw = Sdf.Ellipse(p, new Vector2(0.1f, 0.052f), new Vector2(0.075f, 0.05f));
            float nose = Sdf.Circle(p, new Vector2(0.187f, 0.168f), 0.026f);
            return Sdf.SmoothUnion(Sdf.SmoothUnion(Sdf.SmoothUnion(cranium, face, 0.05f), jaw, 0.04f), nose, 0.02f);
        }

        /// <summary>Skin shading, cheek and brow light of the head (after the contour).</summary>
        static void SkinHead(SdfCanvas c, SdfCanvas.SdfFn head)
        {
            c.Fill(head, K.Skin);
            c.Shade(p => Mathf.Lerp(0.8f, 1f, MathUtil.Smooth01((p.x + 0.12f) / 0.2f)) * Mathf.Lerp(0.88f, 1f, MathUtil.Smooth01((p.y - 0.02f) / 0.12f)));
            c.Paint(p => Sdf.Ellipse(p, new Vector2(0.118f, 0.122f), new Vector2(0.032f, 0.02f)), new Color(0.93f, 0.55f, 0.5f, 0.28f), 0.022f);
            c.Paint(p => Sdf.Ellipse(p, new Vector2(0.14f, 0.28f), new Vector2(0.05f, 0.03f)), K.SkinLight.WithAlpha(0.3f), 0.03f);
        }

        /// <summary>A sweatband around the head: cream with a kit-coloured centre stripe.</summary>
        static void Headband(SdfCanvas c, SdfCanvas.SdfFn headAndHair)
        {
            SdfCanvas.SdfFn band = p => Sdf.Intersect(Mathf.Abs(Vector2.Dot(p - new Vector2(0f, 0.305f), new Vector2(-0.27f, 0.963f))) - 0.022f,
                headAndHair(p) - 0.012f);
            c.Fill(p => band(p) - LineW * 0.7f, Palette.PlayerLine);
            c.Fill(band, K.KitWhite);
            c.Paint(p => Sdf.Intersect(Mathf.Abs(Vector2.Dot(p - new Vector2(0f, 0.305f), new Vector2(-0.27f, 0.963f))) - 0.006f, band(p)), K.Jersey);
            Tint(c, band, K.KitWhiteShade, p => 0.8f * (1f - MathUtil.Smooth01((p.x + 0.16f) / 0.2f)));
        }

        static void Ear(SdfCanvas c)
        {
            c.Fill(p => Sdf.Ellipse(p, new Vector2(-0.018f, 0.165f), new Vector2(0.034f, 0.05f)), K.SkinShade);
            c.Fill(p => Sdf.Ellipse(p, new Vector2(-0.012f, 0.165f), new Vector2(0.015f, 0.026f)), Color.Lerp(K.SkinShade, K.Hair, 0.35f));
            c.Paint(p => Sdf.Ring(p, new Vector2(-0.018f, 0.165f), 0.028f, 0.006f), K.Skin.WithAlpha(0.6f), 0.004f);
        }

        /// <summary>Eye, lid, brow, nose shadow and mouth.</summary>
        static void Face(SdfCanvas c)
        {
            SdfCanvas.SdfFn eye = p => Sdf.Ellipse(p, new Vector2(0.131f, 0.19f), new Vector2(0.021f, 0.026f));
            c.Fill(eye, new Color(0.95f, 0.93f, 0.9f));
            c.Paint(p => Sdf.Intersect(Sdf.Ellipse(p, new Vector2(0.142f, 0.188f), new Vector2(0.013f, 0.021f)), eye(p)), Palette.EyeDark);
            c.Fill(p => Sdf.Circle(p, new Vector2(0.146f, 0.198f), 0.005f), Color.white);
            c.Fill(p => Sdf.Capsule(p, new Vector2(0.108f, 0.213f), new Vector2(0.154f, 0.207f), 0.0058f), Palette.EyeDark);
            c.Fill(p => Sdf.Capsule(p, new Vector2(0.094f, 0.247f), new Vector2(0.162f, 0.236f), 0.0115f), K.Hair);

            c.Paint(p => Sdf.Capsule(p, new Vector2(0.17f, 0.146f), new Vector2(0.19f, 0.142f), 0.008f), K.SkinShade.WithAlpha(0.5f), 0.008f);
            c.Fill(p => Sdf.Capsule(p, new Vector2(0.14f, 0.098f), new Vector2(0.166f, 0.103f), 0.0058f), Color.Lerp(K.SkinShade, Palette.EyeDark, 0.45f));
            c.Paint(p => Sdf.Capsule(p, new Vector2(0.145f, 0.084f), new Vector2(0.163f, 0.087f), 0.005f), K.SkinLight.WithAlpha(0.5f), 0.006f);
        }

        static void BuildHead()
        {
            Vector2 hc = HeadCenter;
            var c = new SdfCanvas(new Rect(-0.27f, -0.06f, 0.54f, 0.54f), P);

            SdfCanvas.SdfFn head = HeadShape;
            SdfCanvas.SdfFn hair = p =>
            {
                float cap = Sdf.Circle(p, hc + new Vector2(-0.012f, 0.016f), 0.205f);
                float line = Sdf.HalfPlane(p, new Vector2(0.07f, 0.25f), new Vector2(0.62f, -0.78f));
                float d = Sdf.Intersect(cap, line);
                float back = Sdf.Intersect(Sdf.Circle(p, hc + new Vector2(-0.02f, 0.0f), 0.202f), p.x + 0.07f);
                d = Sdf.SmoothUnion(d, Sdf.Intersect(back, -(p.y - 0.07f)), 0.02f);
                float quiff = Sdf.Tapered(p, new Vector2(0.03f, 0.335f), 0.072f, new Vector2(0.195f, 0.368f), 0.012f);
                float strandA = Sdf.Tapered(p, new Vector2(0.06f, 0.37f), 0.03f, new Vector2(-0.13f, 0.39f), 0.008f);
                float sideburn = Sdf.Capsule(p, new Vector2(0.022f, 0.205f), new Vector2(0.03f, 0.14f), 0.013f);
                return Sdf.SmoothUnion(Sdf.SmoothUnion(Sdf.SmoothUnion(d, quiff, 0.03f), strandA, 0.02f), sideburn, 0.015f);
            };

            Contour(c, p => Sdf.Union(head(p), hair(p)));
            SkinHead(c, head);
            Ear(c);

            // hair: dark cap with a cool sheen arc and strand lines
            c.Fill(hair, K.Hair);
            c.Paint(p => Sdf.Intersect(Sdf.Ring(p, hc + new Vector2(-0.02f, 0.01f), 0.158f, 0.02f), -(p.y - 0.27f)), K.HairLight.WithAlpha(0.75f), 0.012f);
            Stroke(c, hair, new Vector2(0.12f, 0.35f), new Vector2(-0.06f, 0.372f), 0.004f, K.HairLight.WithAlpha(0.6f), 0.006f);
            Stroke(c, hair, new Vector2(-0.07f, 0.33f), new Vector2(-0.17f, 0.24f), 0.004f, K.HairLight.WithAlpha(0.5f), 0.006f);
            c.Paint(p => Sdf.Intersect(hair(p) + 0.004f, -hair(p + new Vector2(0.01f, 0.028f))), new Color(0.4f, 0.37f, 0.46f, 0.7f), 0.01f);

            // headband: cream with a kit-coloured centre stripe (not every character wears one)
            if (K.Headband) Headband(c, p => Sdf.Union(head(p), hair(p)));

            // eye: almond with sclera, iris looking ahead and a catch-light; lid line and brow; nose shadow, mouth
            Face(c);
            Head = c.ToSprite("Head", Vector2.zero);

            // hair tuft: three spikes that whip with the head. The kit's Tuft scales the whole thing
            // from its root, so the same shape reads as a short quiff, a buzz cut or a long ponytail.
            float s = Mathf.Max(0.2f, K.Tuft);
            Vector2 T(float x, float y) => new Vector2(x * s, y * s);
            var t = new SdfCanvas(new Rect(-0.09f * s, -0.06f * s, 0.3f * s, 0.25f * s), P);
            SdfCanvas.SdfFn tuft = p => Sdf.SmoothUnion(Sdf.SmoothUnion(
                Sdf.Tapered(p, Vector2.zero, 0.05f * s, T(0.16f, 0.1f), 0.006f * s),
                Sdf.Tapered(p, T(-0.03f, -0.005f), 0.042f * s, T(0.07f, 0.145f), 0.005f * s), 0.02f * s),
                Sdf.Tapered(p, T(0.01f, -0.01f), 0.036f * s, T(0.19f, 0.03f), 0.005f * s), 0.02f * s);
            Contour(t, tuft, 0.01f * s);
            t.Fill(tuft, K.Hair);
            t.Paint(p => Sdf.Intersect(Sdf.Capsule(p, T(0f, 0.03f), T(0.11f, 0.085f), 0.008f * s), tuft(p)), K.HairLight.WithAlpha(0.6f), 0.008f);
            HairTuft = t.ToSprite("HairTuft", Vector2.zero);
        }
    }
}
