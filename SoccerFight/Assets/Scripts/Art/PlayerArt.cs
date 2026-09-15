using UnityEngine;

namespace SoccerFight
{
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

    /// <summary>Kit, skin and hair for the side-view soccer player. Every part pivots at its joint.</summary>
    public static class PlayerArt
    {
        public static Sprite Torso, Pelvis, Neck, Head, HairTuft;
        public static Sprite Thigh, Shin, Boot, BootGlow;
        public static Sprite UpperArm, Forearm, Hand;

        const float P = PlayerDims.Ppu;

        public static void Build()
        {
            BuildLegs();
            BuildArms();
            BuildBody();
            BuildHead();
        }

        static void ShadeBack(SdfCanvas c, float x0, float x1, float min)
        {
            // Darken the trailing edge of a limb slightly for volume.
            c.Shade(p => Mathf.Lerp(min, 1f, MathUtil.Smooth01((p.x - x0) / (x1 - x0))));
        }

        static void BuildLegs()
        {
            // Thigh with the shorts' leg opening baked on top.
            {
                const float L = PlayerDims.ThighLen;
                var c = new SdfCanvas(new Rect(-0.15f, -L - 0.1f, 0.3f, L + 0.24f), P);
                c.Fill(p => Sdf.Tapered(p, Vector2.zero, 0.088f, new Vector2(0, -L), 0.068f), Palette.Skin);
                ShadeBack(c, -0.09f, 0.0f, 0.86f);
                c.Fill(p => Sdf.Intersect(
                        Sdf.Box(p, new Vector2(0.005f, -0.06f), new Vector2(0.112f, 0.15f), 0.07f),
                        Sdf.HalfPlane(p, new Vector2(0f, -0.195f), new Vector2(-0.18f, -1f))),
                    Palette.KitWhite);
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.x + 0.004f) - 0.017f, -(p.y + 0.2f)), Palette.Jersey);
                c.Shade(p => p.x < -0.04f && p.y > -0.21f ? Mathf.Lerp(0.9f, 1f, MathUtil.Smooth01((p.x + 0.11f) / 0.07f)) : 1f);
                Thigh = c.ToSprite("Thigh", Vector2.zero);
            }

            // Shin: skin knee, calf bulge, sock with bands, shin-guard bulge in front.
            {
                const float L = PlayerDims.ShinLen;
                var c = new SdfCanvas(new Rect(-0.13f, -L - 0.08f, 0.26f, L + 0.17f), P);
                SdfCanvas.SdfFn shin = p => Sdf.SmoothUnion(
                    Sdf.SmoothUnion(
                        Sdf.Tapered(p, Vector2.zero, 0.068f, new Vector2(0, -L), 0.046f),
                        Sdf.Ellipse(p, new Vector2(-0.024f, -0.13f), new Vector2(0.06f, 0.12f)), 0.03f),
                    Sdf.Ellipse(p, new Vector2(0.022f, -0.19f), new Vector2(0.05f, 0.11f)), 0.02f);
                c.Fill(shin, Palette.Skin);
                c.Paint(p => p.y + 0.09f, Palette.Jersey);
                c.Paint(p => Mathf.Abs(p.y + 0.105f) - 0.017f, Palette.KitWhite);
                c.Paint(p => Mathf.Abs(p.y + 0.16f) - 0.007f, Palette.KitWhite);
                ShadeBack(c, -0.08f, 0.01f, 0.84f);
                c.Paint(p => Sdf.Capsule(p, new Vector2(0.035f, -0.14f), new Vector2(0.03f, -0.3f), 0.01f), Palette.JerseyLight.WithAlpha(0.55f), 0.012f);
                Shin = c.ToSprite("Shin", Vector2.zero);
            }

            // Boot: dark leather, white swoosh, neon sole + studs.
            {
                var c = new SdfCanvas(new Rect(-0.14f, -0.14f, 0.38f, 0.22f), P);
                SdfCanvas.SdfFn upper = p =>
                {
                    float ankle = Sdf.Circle(p, new Vector2(-0.008f, -0.005f), 0.054f);
                    float foot = Sdf.Capsule(p, new Vector2(-0.05f, -0.045f), new Vector2(0.15f, -0.052f), 0.043f);
                    float toe = Sdf.Ellipse(p, new Vector2(0.155f, -0.058f), new Vector2(0.062f, 0.036f));
                    float heel = Sdf.Box(p, new Vector2(-0.045f, -0.058f), new Vector2(0.052f, 0.03f), 0.022f);
                    float d = Sdf.SmoothUnion(Sdf.SmoothUnion(ankle, foot, 0.04f), Sdf.Union(toe, heel), 0.03f);
                    return Sdf.Intersect(d, -(p.y + 0.084f));
                };
                c.Fill(upper, Palette.Boot);
                c.Paint(p => Sdf.Capsule(p, new Vector2(-0.03f, -0.036f), new Vector2(0.11f, -0.022f), 0.011f), Palette.KitWhite);
                c.Paint(p => Sdf.Circle(p, new Vector2(0.17f, -0.045f), 0.03f), new Color(1, 1, 1, 0.10f), 0.02f);
                c.Fill(p => Sdf.Box(p, new Vector2(0.052f, -0.088f), new Vector2(0.142f, 0.012f), 0.011f), Palette.Neon);
                for (int i = 0; i < 3; i++)
                {
                    float sx = -0.05f + i * 0.09f;
                    c.Fill(p => Sdf.Box(p, new Vector2(sx, -0.103f), new Vector2(0.013f, 0.009f), 0.004f), Palette.Boot);
                }
                Boot = c.ToSprite("Boot", Vector2.zero);

                var g = new SdfCanvas(new Rect(-0.2f, -0.2f, 0.5f, 0.24f), 180f);
                g.Fill(p => Sdf.Capsule(p, new Vector2(-0.07f, -0.09f), new Vector2(0.17f, -0.09f), 0.004f), Palette.Neon, 0.07f);
                BootGlow = g.ToSprite("BootGlow", Vector2.zero);
            }
        }

        static void BuildArms()
        {
            {
                const float L = PlayerDims.UpperArmLen;
                var c = new SdfCanvas(new Rect(-0.11f, -L - 0.07f, 0.22f, L + 0.17f), P);
                c.Fill(p => Sdf.Tapered(p, Vector2.zero, 0.058f, new Vector2(0, -L), 0.048f), Palette.Skin);
                ShadeBack(c, -0.06f, 0.01f, 0.86f);
                SdfCanvas.SdfFn sleeve = p => Sdf.Intersect(
                    Sdf.SmoothUnion(Sdf.Circle(p, new Vector2(0f, 0.005f), 0.079f),
                        Sdf.Tapered(p, new Vector2(0, 0f), 0.072f, new Vector2(0, -0.13f), 0.066f), 0.02f),
                    Sdf.HalfPlane(p, new Vector2(0, -0.135f), new Vector2(0.12f, -1f)));
                c.Fill(sleeve, Palette.Jersey);
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.y + 0.122f - 0.12f * p.x) - 0.011f, sleeve(p)), Palette.KitWhite);
                c.Shade(p => sleeve(p) < 0f ? Mathf.Lerp(0.88f, 1f, MathUtil.Smooth01((p.x + 0.07f) / 0.08f)) : 1f);
                UpperArm = c.ToSprite("UpperArm", Vector2.zero);
            }
            {
                const float L = PlayerDims.ForearmLen;
                var c = new SdfCanvas(new Rect(-0.09f, -L - 0.06f, 0.18f, L + 0.13f), P);
                c.Fill(p => Sdf.Tapered(p, Vector2.zero, 0.05f, new Vector2(0, -L), 0.04f), Palette.Skin);
                ShadeBack(c, -0.05f, 0.01f, 0.86f);
                Forearm = c.ToSprite("Forearm", Vector2.zero);
            }
            {
                var c = new SdfCanvas(new Rect(-0.09f, -0.13f, 0.18f, 0.17f), P);
                c.Fill(p => Sdf.SmoothUnion(
                    Sdf.Ellipse(p, new Vector2(0f, -0.045f), new Vector2(0.05f, 0.06f)),
                    Sdf.Ellipse(p, new Vector2(0.035f, -0.03f), new Vector2(0.02f, 0.035f)), 0.015f), Palette.Skin);
                ShadeBack(c, -0.05f, 0.02f, 0.86f);
                Hand = c.ToSprite("Hand", Vector2.zero);
            }
        }

        static void BuildBody()
        {
            {
                var c = new SdfCanvas(new Rect(-0.24f, -0.08f, 0.48f, 0.72f), P);
                SdfCanvas.SdfFn torso = p =>
                {
                    float body = Sdf.Tapered(p, new Vector2(0f, 0.08f), 0.132f, new Vector2(0.008f, 0.42f), 0.158f);
                    float chest = Sdf.Ellipse(p, new Vector2(0.035f, 0.35f), new Vector2(0.158f, 0.15f));
                    float back = Sdf.Ellipse(p, new Vector2(-0.03f, 0.31f), new Vector2(0.14f, 0.2f));
                    float shoulder = Sdf.Circle(p, new Vector2(0f, 0.46f), 0.12f);
                    return Sdf.SmoothUnion(Sdf.SmoothUnion(Sdf.SmoothUnion(body, chest, 0.05f), back, 0.05f), shoulder, 0.04f);
                };
                c.Fill(torso, Palette.Jersey);
                // side panel stripe
                c.Paint(p => Sdf.Intersect(Mathf.Abs(p.x + 0.012f - 0.07f * (p.y - 0.3f)) - 0.022f, p.y - 0.5f), Palette.KitWhite);
                // collar
                c.Paint(p => Sdf.Intersect(Sdf.Ring(p, new Vector2(0.035f, 0.57f), 0.09f, 0.032f), -(p.y - 0.535f)), Palette.KitWhite);
                // volume: darker back, light chest rim
                c.Shade(p => Mathf.Lerp(0.8f, 1f, MathUtil.Smooth01((p.x + 0.16f) / 0.12f)));
                c.Paint(p => Sdf.Intersect(torso(p) + 0.006f, -torso(p + new Vector2(0.03f, 0.01f))), Palette.JerseyLight.WithAlpha(0.7f), 0.01f);
                Torso = c.ToSprite("Torso", Vector2.zero);
            }
            {
                var c = new SdfCanvas(new Rect(-0.2f, -0.18f, 0.4f, 0.3f), P);
                SdfCanvas.SdfFn shorts = p => Sdf.Box(p, new Vector2(0.005f, -0.03f), new Vector2(0.152f, 0.108f), 0.075f);
                c.Fill(shorts, Palette.KitWhite);
                c.Paint(p => Mathf.Abs(p.y - 0.045f) - 0.013f, Palette.KitWhiteShade);
                c.Paint(p => Mathf.Abs(p.x + 0.005f) - 0.018f, Palette.Jersey);
                c.Shade(p => Mathf.Lerp(0.84f, 1f, MathUtil.Smooth01((p.x + 0.15f) / 0.12f)));
                Pelvis = c.ToSprite("Pelvis", Vector2.zero);
            }
            {
                var c = new SdfCanvas(new Rect(-0.08f, -0.06f, 0.16f, 0.22f), P);
                c.Fill(p => Sdf.Capsule(p, Vector2.zero, new Vector2(0.01f, 0.1f), 0.056f), Palette.SkinShade);
                Neck = c.ToSprite("Neck", Vector2.zero);
            }
        }

        static void BuildHead()
        {
            Vector2 hc = new Vector2(0f, 0.19f);
            var c = new SdfCanvas(new Rect(-0.26f, -0.05f, 0.52f, 0.52f), P);

            SdfCanvas.SdfFn head = p =>
            {
                float cranium = Sdf.Circle(p, hc, PlayerDims.HeadR);
                float face = Sdf.Box(p, new Vector2(0.07f, 0.11f), new Vector2(0.11f, 0.085f), 0.075f);
                float nose = Sdf.Circle(p, new Vector2(0.186f, 0.168f), 0.027f);
                return Sdf.SmoothUnion(Sdf.SmoothUnion(cranium, face, 0.05f), nose, 0.02f);
            };
            c.Fill(head, Palette.Skin);
            c.Shade(p => Mathf.Lerp(0.84f, 1.0f, MathUtil.Smooth01((p.x + 0.12f) / 0.2f)) * Mathf.Lerp(0.92f, 1f, MathUtil.Smooth01((p.y - 0.02f) / 0.12f)));

            // ear
            c.Fill(p => Sdf.Ellipse(p, new Vector2(-0.018f, 0.165f), new Vector2(0.034f, 0.05f)), Palette.SkinShade);
            c.Fill(p => Sdf.Ellipse(p, new Vector2(-0.012f, 0.165f), new Vector2(0.015f, 0.026f)), Color.Lerp(Palette.SkinShade, Palette.Hair, 0.35f));

            // hair: covers top and back of the head, clean curved hairline + a quiff
            SdfCanvas.SdfFn hair = p =>
            {
                float cap = Sdf.Circle(p, hc + new Vector2(-0.012f, 0.014f), 0.203f);
                float line = Sdf.HalfPlane(p, new Vector2(0.07f, 0.25f), new Vector2(0.62f, -0.78f));
                float d = Sdf.Intersect(cap, line);
                float back = Sdf.Intersect(Sdf.Circle(p, hc + new Vector2(-0.02f, 0.0f), 0.2f), p.x + 0.07f);
                d = Sdf.SmoothUnion(d, Sdf.Intersect(back, -(p.y - 0.07f)), 0.02f);
                float quiff = Sdf.Tapered(p, new Vector2(0.03f, 0.33f), 0.07f, new Vector2(0.19f, 0.36f), 0.012f);
                return Sdf.SmoothUnion(d, quiff, 0.03f);
            };
            c.Fill(hair, Palette.Hair);
            c.Paint(p => Sdf.Intersect(hair(p) + 0.004f, -hair(p + new Vector2(0.01f, 0.028f))), new Color(0.38f, 0.33f, 0.36f, 0.8f), 0.01f);

            // headband
            c.Fill(p => Sdf.Intersect(Mathf.Abs(Vector2.Dot(p - new Vector2(0f, 0.305f), new Vector2(-0.27f, 0.963f))) - 0.021f,
                head(p) - 0.012f), Palette.KitWhite);

            // eye, brow, mouth
            c.Fill(p => Sdf.Ellipse(p, new Vector2(0.125f, 0.192f), new Vector2(0.02f, 0.032f)), Palette.EyeDark);
            c.Fill(p => Sdf.Circle(p, new Vector2(0.131f, 0.203f), 0.0075f), Color.white);
            c.Fill(p => Sdf.Capsule(p, new Vector2(0.092f, 0.244f), new Vector2(0.158f, 0.236f), 0.012f), Palette.Hair);
            c.Fill(p => Sdf.Capsule(p, new Vector2(0.138f, 0.098f), new Vector2(0.163f, 0.104f), 0.0065f), Color.Lerp(Palette.SkinShade, Palette.EyeDark, 0.4f));
            Head = c.ToSprite("Head", Vector2.zero);

            var t = new SdfCanvas(new Rect(-0.08f, -0.05f, 0.26f, 0.22f), P);
            t.Fill(p => Sdf.SmoothUnion(
                Sdf.Tapered(p, Vector2.zero, 0.048f, new Vector2(0.14f, 0.1f), 0.006f),
                Sdf.Tapered(p, new Vector2(-0.03f, -0.005f), 0.04f, new Vector2(0.07f, 0.13f), 0.005f), 0.02f), Palette.Hair);
            HairTuft = t.ToSprite("HairTuft", Vector2.zero);
        }
    }
}
