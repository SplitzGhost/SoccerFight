using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Duo: the partner's big moments seen from this side. Damage already travels as hits; this
    /// sends just the look of an explosion, a rainbow landing, a meteor, the whistle — so the
    /// monsters around the partner don't fall to nothing visible.
    /// </summary>
    public static class CoopFx
    {
        public enum Kind : byte { Explosion, Blast, Rainbow, Meteor, Whistle, Nova, Slam, Three, Oop }

        public static void Send(Kind kind, Vector2 at, float radius, Color c)
        {
            if (!Coop.Active) return;
            var w = Coop.S.Event(Ev.Fx);
            w.Byte((byte)kind); w.Vec(at); w.Float(radius); w.Color(c);
        }

        public static void Play(Kind kind, Vector2 at, float radius, Color c)
        {
            var fx = FxSystem.I;
            var cam = Game.I.Cam;
            switch (kind)
            {
                case Kind.Explosion:
                    fx.Flash(at, radius * 1.4f, c, 0.16f, 2.6f);
                    fx.Ring(FxLayer.Front, at, 0.1f, radius, 0.25f, 0.01f, 0.28f, Color.white, c.WithAlpha(0f), 2.4f);
                    fx.Sparks(at, Vector2.up, 200f, 8, 4f, 10f, c, 2.4f, 0.045f, 0.28f, 6f);
                    cam.AddTrauma(0.05f);
                    break;
                case Kind.Blast:
                    fx.Flash(at, 5f, Palette.BlastOrange, 0.24f, 2.6f);
                    fx.Flash(at, 2.4f, Color.white, 0.1f, 3.2f);
                    fx.Ring(FxLayer.Front, at, 0.3f, radius * 1.1f, 0.55f, 0.03f, 0.45f, Color.white, Palette.BlastOrange.WithAlpha(0f), 2.6f);
                    fx.Sparks(at, Vector2.up, 200f, 20, 6f, 16f, Palette.BlastOrange, 2.8f, 0.06f, 0.42f, 14f);
                    fx.Dust(at, Vector2.right, 7, 3.6f, 0.65f, 0.42f);
                    fx.Dust(at, Vector2.left, 7, 3.6f, 0.65f, 0.42f);
                    cam.AddTrauma(0.2f);
                    break;
                case Kind.Rainbow:
                    fx.Flash(at + Vector2.up * 0.3f, 3.2f, Color.white, 0.2f, 2.4f);
                    fx.Ring(FxLayer.Front, at, 0.2f, radius, 0.6f, 0.04f, 0.5f, Color.white, Color.white.WithAlpha(0f), 2.2f, true);
                    for (int i = 0; i < 18; i++)
                    {
                        Color rc = Art.Rainbow(Random.value);
                        fx.Streak(FxLayer.Front, at, MathUtil.Dir(Random.Range(15f, 165f)) * Random.Range(6f, 15f), Random.Range(0.3f, 0.55f), 0.07f, 0.04f,
                            Color.Lerp(rc, Color.white, 0.3f), rc.WithAlpha(0f), 2.8f, 3.5f, 9f);
                    }
                    fx.Sparkles(at + Vector2.up * 0.4f, 1.2f, 12, Palette.Gold, 3f, 0.9f);
                    cam.AddTrauma(0.18f);
                    break;
                case Kind.Meteor:
                    fx.Flash(at, 6f, Palette.Amber, 0.26f, 2.8f);
                    fx.Flash(at, 2.6f, Color.white, 0.1f, 3.4f);
                    fx.Ring(FxLayer.Front, at, 0.3f, radius * 1.25f, 0.6f, 0.03f, 0.5f, Color.white, Palette.Amber.WithAlpha(0f), 2.8f);
                    fx.Sparks(at, Vector2.up, 150f, 20, 7f, 18f, Palette.Amber, 2.8f, 0.06f, 0.45f, 13f);
                    fx.Dust(at, Vector2.right, 8, 4f, 0.7f, 0.45f);
                    fx.Dust(at, Vector2.left, 8, 4f, 0.7f, 0.45f);
                    cam.AddTrauma(0.3f);
                    break;
                case Kind.Whistle:
                    for (int i = 0; i < 3; i++)
                        fx.Ring(FxLayer.Front, at, 0.4f + i * 0.5f, radius + i * 4f, 0.26f, 0.015f, 0.5f + i * 0.12f, i == 0 ? Color.white : Palette.Silver, Palette.Silver.WithAlpha(0f), 2.2f);
                    fx.Flash(at, 3.4f, Palette.Silver, 0.2f, 3f);
                    cam.AddTrauma(0.15f);
                    Game.I.Hud.ShowToast("ABPFIFF VON " + (Coop.S?.PartnerName ?? "").ToUpperInvariant());
                    break;
                case Kind.Nova:
                    fx.Flash(at, radius * 1.2f, Palette.ShotCyan, 0.2f, 2.8f);
                    fx.Ring(FxLayer.Front, at, 0.3f, radius, 0.35f, 0.02f, 0.35f, Color.white, Palette.ShotCyan.WithAlpha(0f), 2.6f);
                    cam.AddTrauma(0.08f);
                    break;
                case Kind.Slam:
                    // the partner's dunk: the same waves, without the damage (that happened over there)
                    for (int i = 0; i < 2; i++) Court.I?.Shockwave(at + new Vector2(0f, 0.15f), radius * (1f + i * 0.28f), 0f, 0f, 0f, i * 0.11f, false);
                    fx.Flash(at + new Vector2(0f, 0.3f), 3.2f, c, 0.16f, 2.8f);
                    fx.Dust(at, Vector2.right, 8, 4.5f, 0.6f, 0.4f);
                    fx.Dust(at, Vector2.left, 8, 4.5f, 0.6f, 0.4f);
                    cam.AddTrauma(0.25f);
                    break;
                case Kind.Three:
                case Kind.Oop:
                    Court.BlastFx(at, radius, c, 0.6f);
                    break;
            }
        }
    }
}
