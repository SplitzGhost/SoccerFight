using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>Die sechs Ultis, eine pro Spieler.</summary>
    public enum UltiKind : byte { None, Volley, Bulwark, Storm, Buzzer, Meteor, AnkleBreaker }

    /// <summary>Name, Farbe und Beschreibung der Ultis; welcher Spieler welche hat.</summary>
    public static class UltiDefs
    {
        public static UltiKind Of(int characterIndex)
        {
            var all = Characters.All;
            if (characterIndex < 0 || characterIndex >= all.Length) return UltiKind.None;
            switch (all[characterIndex].Id)
            {
                case "rio": return UltiKind.Volley;
                case "bruno": return UltiKind.Bulwark;
                case "mira": return UltiKind.Storm;
                case "dre": return UltiKind.Buzzer;
                case "titan": return UltiKind.Meteor;
                case "nova": return UltiKind.AnkleBreaker;
                default: return UltiKind.None;
            }
        }

        public static string Name(UltiKind k)
        {
            switch (k)
            {
                case UltiKind.Volley: return "JAHRHUNDERT-VOLLEY";
                case UltiKind.Bulwark: return "BOLLWERK";
                case UltiKind.Storm: return "BALLZAUBER-STURM";
                case UltiKind.Buzzer: return "BUZZER BEATER";
                case UltiKind.Meteor: return "METEOR-DUNK";
                case UltiKind.AnkleBreaker: return "ANKLE BREAKER";
                default: return "ULTI";
            }
        }

        public static string Text(UltiKind k)
        {
            switch (k)
            {
                case UltiKind.Volley: return "Lupfen, Volley: ein Feuerkomet quer durch die Arena, große Explosion am Ende.";
                case UltiKind.Bulwark: return "Stampfer und goldene Kuppel: Geschosse prallen zurück, Gegner fliegen raus, am Ende platzt sie.";
                case UltiKind.Storm: return "Der Ball teilt sich in fünf Zauberbälle, die um Mira kreisen und dann auf die Gegner schießen.";
                case UltiKind.Buzzer: return "3-2-1: fünf Würfe im hohen Bogen auf fünf Gegner, alle schlagen beim Buzzer zugleich ein.";
                case UltiKind.Meteor: return "Aus dem Bild springen, dann als Meteor herunterkrachen: der Boden bricht, alles fliegt.";
                case UltiKind.AnkleBreaker: return "Blitzschnelle Crossovers durch alle Gegner in der Nähe: sie fallen um und sind betäubt.";
                default: return "";
            }
        }

        public static Color Accent(UltiKind k)
        {
            switch (k)
            {
                case UltiKind.Volley: return Palette.BlastOrange;
                case UltiKind.Bulwark: return Palette.Gold;
                case UltiKind.Storm: return Palette.Trick;
                case UltiKind.Buzzer: return Palette.HoopFlame;
                case UltiKind.Meteor: return new Color(1f, 0.62f, 0.3f);
                case UltiKind.AnkleBreaker: return Palette.Showboat;
                default: return Color.white;
            }
        }
    }

    /// <summary>
    /// Die Ulti: Eine Leiste füllt sich mit dem Schaden, den der Spieler austeilt (an Bossen zählt er
    /// halb, die Ulti selbst zählt nicht). Ist sie voll, löst die Ulti-Taste (V) die Ulti des Spielers aus.
    /// Jede beginnt mit einem kurzen Kinomoment (Zeitlupe, Bild dunkler, Kamera nah dran, Lichtkranz,
    /// Name groß im Bild) und läuft dann als eigene Bewegung; was davon in der Welt bleibt (Komet,
    /// Kuppel, Zauberbälle, Würfe, Meteor-Marke) lebt in <see cref="Ultis"/>. Währenddessen ist der
    /// Spieler unverwundbar.
    /// </summary>
    public sealed partial class Player
    {
        /// <summary>Schaden für eine volle Leiste je Punkt Wellen-Budget (ein Hüpfer kostet einen Punkt und hat ~30 Leben):
        /// die Leiste kostet damit etwa so viel, wie eine Welle an Leben mitbringt.</summary>
        public const float UltiCostPerBudget = 36f;

        // RIO: lupfen, Volley, Komet
        public const float VolleyLift = 0.22f, VolleyContact = 0.44f, VolleyEnd = 0.8f;
        // BRUNO: Knie hoch, Stampfer, Kuppel
        public const float BulwarkStomp = 0.34f, BulwarkEnd = 0.74f, DomeLife = 5f, DomeRadius = 2.7f;
        // MIRA: hochlupfen, teilen, kreisen lassen, abschießen
        public const float StormSplit = 0.4f, StormLaunch = 1.02f, StormGap = 0.1f, StormEnd = 1.66f;
        public const int StormOrbs = 5;
        // DRE: Sprungwurf, fünf Bälle, Buzzer
        public const float BuzzerJump = 0.08f, BuzzerRelease = 0.44f, BuzzerFlight = 1.2f, BuzzerEnd = 1.35f;
        public const int BuzzerBalls = 5;
        // TITAN: ausholen, aus dem Bild, hängen, herunterkrachen, Landung
        public const float MeteorGather = 0.24f, MeteorRise = 0.3f, MeteorHang = 0.42f, MeteorFall = 0.2f, MeteorRecover = 0.42f;
        public const float MeteorFlight = MeteorRise + MeteorHang + MeteorFall, MeteorHeight = 15f, MeteorRange = 11f;
        // NOVA: ducken, ein Sprung pro Gegner, Pose mit dem Ball auf dem Finger
        public const float AnkleCrouch = 0.16f, AnkleHop = 0.13f, AnklePose = 0.6f, AnkleRange = 11f;
        public const int AnkleMax = 7;

        /// <summary>Ulti-Leiste (0..1).</summary>
        public float UltiCharge;
        /// <summary>Welcher Spieler das ist (-1: der eigene, Characters.Index). Der Duo-Partner setzt seinen.</summary>
        public int CharacterIndex = -1;
        /// <summary>Novas Sprünge in dieser Ulti (das Netz schickt sie mit, die Pose hängt daran).</summary>
        public int UltiSteps;
        /// <summary>Der Ball steckt gerade in der Ulti (Komet, Zauberbälle, Buzzer-Würfe).</summary>
        public bool UltiBallOut;
        /// <summary>Wohin Mira gerade zeigt (lokal, Blickrichtung rechts).</summary>
        public Vector2 UltiPoint = new Vector2(1f, 0.3f);

        public UltiKind Ulti => UltiDefs.Of(CharacterIndex >= 0 ? CharacterIndex : Characters.Index);
        public bool UltiReady => UltiCharge >= 1f;
        /// <summary>So viel Schaden füllt die Leiste gerade einmal (wächst mit Wellengröße und Monster-Leben).</summary>
        public float UltiCost => UltiCostPerBudget * Difficulty.Budget(Run.Level) * Difficulty.HealthMul(Run.Level);
        /// <summary>Der Schaden der Ulti wächst mit gut der Hälfte der Monster-Stärke mit.</summary>
        float UltiPower => Mathf.Lerp(1f, Difficulty.HealthMul(Run.Level), 0.6f);

        float ultiBuffer, ultiReadyFxT, ultiWait, ultiFxT;
        bool ultiFired;
        int ultiStep;
        Vector2 ultiFrom;
        readonly List<Monster> ultiTargets = new List<Monster>();
        readonly List<Vector2> ultiSpots = new List<Vector2>();

        void ResetUlti()
        {
            UltiCharge = 0f;
            ultiBuffer = 0f;
            UltiBallOut = false;
            UltiSteps = 0;
            ultiTargets.Clear();
            ultiSpots.Clear();
        }

        /// <summary>Schaden, der wirklich ankam (Combat.Hit). An Bossen zählt er halb.</summary>
        public void AddUltiDamage(float dealt, bool boss)
        {
            if (Puppet || Dead || dealt <= 0f || Ulti == UltiKind.None) return;
            bool was = UltiReady;
            UltiCharge = Mathf.Min(1f, UltiCharge + dealt * (boss ? 0.5f : 1f) / Mathf.Max(1f, UltiCost));
            if (!was && UltiReady) OnUltiReady();
        }

        void OnUltiReady()
        {
            Color c = UltiDefs.Accent(Ulti);
            var fx = FxSystem.I;
            Vector2 at = Pos + new Vector2(0f, 1f);
            fx.Ring(FxLayer.Front, Pos + new Vector2(0f, 0.08f), 0.2f, 1.9f, 0.12f, 0.01f, 0.4f, Color.white.WithAlpha(0.8f), c.WithAlpha(0f), 2.2f);
            fx.Sparkles(at, 0.9f, 10, Color.Lerp(c, Color.white, 0.3f), 2.6f, 0.7f);
            Game.I.Hud.OnUltiReady(Ulti);
        }

        void PressUlti()
        {
            if (UltiReady) ultiBuffer = 0.3f;
            else Game.I.Hud.OnUltiNotReady();
        }

        /// <summary>Jeden Frame: das Bereit-Leuchten an den Füßen, und der Ball geht in keiner Ulti verloren.</summary>
        void TickUlti(float dt)
        {
            if (Puppet) return;
            if (UltiReady && !Dead && CurrentAction != Action.Ulti)
            {
                ultiReadyFxT -= dt;
                if (ultiReadyFxT <= 0f)
                {
                    ultiReadyFxT = 0.09f;
                    Color c = Color.Lerp(UltiDefs.Accent(Ulti), Color.white, 0.25f);
                    var fx = FxSystem.I;
                    fx.Motes(Pos + new Vector2(Random.Range(-0.45f, 0.45f), 0.1f), new Vector2(0f, Random.Range(1.2f, 2.2f)), c, 1, 0.08f);
                    if (Random.value < 0.25f) fx.Sparkles(Pos + new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.3f, 1.6f)), 0.1f, 1, c, 2.4f, 0.45f);
                }
            }
            if (UltiBallOut && CurrentAction != Action.Ulti && (Ultis.I == null || !Ultis.I.HoldsBall))
                ReturnUltiBall(Pos + new Vector2(Facing * 0.5f, 1.2f), new Vector2(0f, 3f), false);
        }

        /// <summary>Die Ulti hat den Ball wieder freigegeben: er taucht hier auf (oder gleich am Fuß).</summary>
        public void ReturnUltiBall(Vector2 at, Vector2 vel, bool held)
        {
            if (!UltiBallOut) return;
            UltiBallOut = false;
            // ausgeschaltet: der Ball bleibt unsichtbar, bis der Spieler wieder da ist (Respawn/Comeback zeigen ihn)
            if (Dead) { Ball.ResetTo(Pos + new Vector2(0.5f * Facing, Art.BallRadius)); return; }
            if (held) at = Rig.BallHold;
            at.x = Mathf.Clamp(at.x, -ArenaHalf, ArenaHalf);
            at.y = Mathf.Max(at.y, Level.FloorBelow(at.x, at.y + 0.5f) + Art.BallRadius);
            Ball.Unpark(at, vel, held);
            Color c = UltiDefs.Accent(Ulti);
            FxSystem.I.Flash(at, 1.2f, c, 0.14f, 2.4f);
            FxSystem.I.Sparkles(at, 0.3f, 6, Color.Lerp(c, Color.white, 0.4f), 2.6f, 0.5f);
        }

        // ------------------------------------------------------------------ start

        bool StartUlti()
        {
            ultiBuffer = 0f;
            var kind = Ulti;
            if (kind == UltiKind.None || UltiBallOut) return false;
            if (kind == UltiKind.AnkleBreaker && !PlanAnkleBreaker())
            {
                Game.I.Hud.ShowToast("ANKLE BREAKER  ·  KEIN GEGNER IN DER NÄHE");
                return false;
            }
            UltiCharge = 0f;
            CurrentAction = Action.Ulti;
            ActionTime = 0f;
            released = false;
            ultiFired = false;
            ultiStep = 0;
            ultiWait = ultiFxT = 0f;
            ultiFrom = Pos;
            ghostTimer = 0f;
            StepCarry = false;
            DodgeTime = Mathf.Max(DodgeTime, 0.2f);
            float dx = GameInput.AimWorld.x - Pos.x;
            if (Mathf.Abs(dx) > 0.3f && kind != UltiKind.AnkleBreaker) Facing = dx > 0f ? 1 : -1;
            if (kind != UltiKind.Bulwark) GrabUltiBall(kind);

            switch (kind)
            {
                case UltiKind.Volley: StartVolley(); break;
                case UltiKind.Storm: StartStorm(); break;
                case UltiKind.Buzzer: StartBuzzer(); break;
                case UltiKind.Meteor: StartMeteor(); break;
            }
            UltiIntro(kind);
            return true;
        }

        /// <summary>Der Ball gehört zur Ulti: liegt er nicht am Fuß, holt ein Blitz ihn heim.</summary>
        void GrabUltiBall(UltiKind kind)
        {
            if (!Ball.IsHeld)
            {
                if (Ball.IsCatchable(Rig.BallHold, 2.1f * S.CatchRadiusMul)) Ball.ForceCatch(this);
                else
                {
                    Color c = UltiDefs.Accent(kind);
                    var fx = FxSystem.I;
                    fx.Flash(Ball.Pos, 1.3f, c, 0.16f, 2.4f);
                    fx.Sparkles(Ball.Pos, 0.3f, 6, c, 2.6f, 0.5f);
                    Ball.ResetTo(Rig.BallHold);
                    fx.Flash(Rig.BallHold, 1.5f, c, 0.18f, 2.6f);
                }
            }
            KickBallLocal = ToLocal(Ball.Pos);
            Ball.BeginScripted();
        }

        /// <summary>Der Kinomoment: Zeitlupe, das Bild dunkler, die Kamera nah dran, ein Lichtkranz hinter der Figur.</summary>
        void UltiIntro(UltiKind kind)
        {
            var game = Game.I;
            var fx = FxSystem.I;
            Color c = UltiDefs.Accent(kind);
            Color light = Color.Lerp(c, Color.white, 0.45f);
            Vector2 at = Pos + new Vector2(0f, 1.05f);
            TimeFx.SlowMo(0.25f, 0.3f, 0.35f);
            game.Post.UltiFocus(0.55f);
            game.Post.Impact(0.35f);
            game.Cam.SetZoom(0.86f);
            game.Cam.AddTrauma(0.12f);

            // das Licht hinter der Figur: weicher Kranz und Strahlen, die nach außen schießen
            fx.Spawn(FxLayer.Back, true, Art.CellGlow, at, Vector2.zero, 0.95f, 3.2f, 6.8f, c.WithAlpha(0.5f), c.WithAlpha(0f), 2.2f);
            fx.Spawn(FxLayer.Back, true, Art.CellGlow, at, Vector2.zero, 0.55f, 1.2f, 2.6f, light.WithAlpha(0.35f), c.WithAlpha(0f), 2f);
            for (int i = 0; i < 16; i++)
            {
                Vector2 d = MathUtil.Dir(i * 22.5f + Random.Range(-7f, 7f));
                fx.Streak(FxLayer.Back, at + d * 0.45f, d * Random.Range(7f, 12f), Random.Range(0.35f, 0.6f), 0.09f, 0.08f,
                    light.WithAlpha(0.85f), c.WithAlpha(0f), 2.4f, 4f);
            }
            fx.Ring(FxLayer.Front, at, 0.3f, 3.4f, 0.22f, 0.01f, 0.5f, Color.white.WithAlpha(0.9f), c.WithAlpha(0f), 2.4f);
            fx.Ring(FxLayer.Back, Pos + new Vector2(0f, 0.05f), 0.2f, 2.8f, 0.14f, 0.01f, 0.45f, light, c.WithAlpha(0f), 2f);
            fx.Sparkles(at, 1.1f, 14, light, 2.8f, 0.8f);
            fx.Dust(Pos, Vector2.right, 6, 2.6f, 0.45f, 0.35f);
            fx.Dust(Pos, Vector2.left, 6, 2.6f, 0.45f, 0.35f);
            ghosts.Spawn(c, 0.45f, 0.5f);
            Rig.OnUlti();
            game.Hud.ShowUltiBanner(kind, UltiDefs.Name(kind), CharacterIndex >= 0 ? Characters.All[CharacterIndex].Name : Characters.Current.Name);
            Coop.SendUlti(kind, 0, Pos, Vector2.zero, 0f);
        }

        // ------------------------------------------------------------------ while it runs

        float UltiSpeedMul()
        {
            if (Ulti == UltiKind.Buzzer && !Grounded) return 0.25f;
            return 0f;
        }

        float UltiGravityMul()
        {
            float t = ActionTime;
            switch (Ulti)
            {
                case UltiKind.Volley: return t < VolleyContact + 0.12f ? 0.12f : 1f;   // in der Luft: ein Moment Schwebe für den Volley
                case UltiKind.Storm: return 0f;                                         // Mira schwebt (UpdateStorm hält die Höhe)
                case UltiKind.Buzzer: return t > BuzzerRelease - 0.14f && t < BuzzerRelease + 0.2f ? 0.25f : 1f;   // die Hangtime am Scheitel
                case UltiKind.Bulwark: return t >= BulwarkStomp && !released ? 2.5f : 1f;   // in der Luft: hart herunter zum Stampfer
                default: return 1f;
            }
        }

        /// <summary>Titans Flug und Novas Sprünge folgen ihrer eigenen Bahn statt der Physik.</summary>
        bool UltiScripted => CurrentAction == Action.Ulti &&
            ((Ulti == UltiKind.Meteor && ActionTime >= MeteorGather && !ultiFired) ||
             (Ulti == UltiKind.AnkleBreaker && ActionTime >= AnkleCrouch && ultiStep < UltiSteps && ultiSpots.Count >= UltiSteps));

        void UltiMove(float dt)
        {
            Vector2 prev = Pos;
            if (Ulti == UltiKind.Meteor)
            {
                float t = ActionTime - MeteorGather;
                Vector2 top = new Vector2(ultiFrom.x, ultiFrom.y + MeteorHeight);
                Vector2 above = new Vector2(HoopTarget.x, HoopTarget.y + MeteorHeight);
                if (t < MeteorRise) Pos = new Vector2(ultiFrom.x, Mathf.Lerp(ultiFrom.y, top.y, MathUtil.EaseOutCubic(t / MeteorRise)));
                else if (t < MeteorRise + MeteorHang) Pos = Vector2.Lerp(top, above, MathUtil.Smooth01((t - MeteorRise) / MeteorHang));
                else
                {
                    float k = Mathf.Clamp01((t - MeteorRise - MeteorHang) / MeteorFall);
                    Pos = Vector2.Lerp(above, HoopTarget, MathUtil.EaseInQuad(k));
                    if (k >= 1f) { MeteorSlam(); return; }
                }
            }
            else
            {
                // Nova: von Gegner zu Gegner, jeweils knapp an ihm vorbei auf den Boden darunter
                float t = ActionTime - AnkleCrouch;
                int i = Mathf.Clamp(Mathf.FloorToInt(t / AnkleHop), 0, UltiSteps - 1);
                float k = Mathf.Clamp01((t - i * AnkleHop) / AnkleHop);
                Vector2 a = i == 0 ? ultiFrom : ultiSpots[i - 1], b = ultiSpots[i];
                if (Mathf.Abs(b.x - a.x) > 0.05f) Facing = b.x > a.x ? 1 : -1;
                float e = MathUtil.EaseInOutCubic(Mathf.Clamp01(k * 1.15f));
                Pos = Vector2.Lerp(a, b, e) + new Vector2(0f, 0.3f * MathUtil.Bump(e));
            }
            Vel = (Pos - prev) / Mathf.Max(dt, 1e-4f);
            Grounded = false;
            OnPlatform = Level.None;
        }

        void UpdateUlti(float dt)
        {
            // während der Ulti trifft den Spieler nichts
            DodgeTime = Mathf.Max(DodgeTime, 0.12f);
            switch (Ulti)
            {
                case UltiKind.Volley: UpdateVolley(dt); break;
                case UltiKind.Bulwark: UpdateBulwark(dt); break;
                case UltiKind.Storm: UpdateStorm(dt); break;
                case UltiKind.Buzzer: UpdateBuzzer(dt); break;
                case UltiKind.Meteor: UpdateMeteor(dt); break;
                case UltiKind.AnkleBreaker: UpdateAnkle(dt); break;
                default: EndUlti(); break;
            }
        }

        void EndUlti()
        {
            if (CurrentAction != Action.Ulti) return;
            CurrentAction = Action.None;
            Game.I.Cam.SetZoom(1f);
            if (Ball.St == Ball.State.Scripted) Ball.EndScripted();
        }

        /// <summary>Tod (oder ein Treffer, der doch durchkommt): die Ulti bricht ab, der Ball bleibt nicht hängen.</summary>
        void AbortUlti()
        {
            if (CurrentAction != Action.Ulti) return;
            if (Ulti == UltiKind.Storm && UltiBallOut) Ultis.I?.DropOrbs(this);
            EndUlti();
        }

        // ------------------------------------------------------------------ RIO: Jahrhundert-Volley

        void StartVolley()
        {
            // der Volley trifft den Ball auf Hüfthöhe vor dem Körper
            KickBallLocal = new Vector2(0.56f, 0.86f);
            Vector2 dir = AimFrom(ToWorld(KickBallLocal));
            KickAimLocal = new Vector2(dir.x * Facing, dir.y);
        }

        void UpdateVolley(float dt)
        {
            if (!released)
            {
                // in der Luft: Rio bleibt für den Volley fast stehen
                if (!Grounded) Vel.y = MathUtil.Damp(Vel.y, 0f, 9f, dt);
                // der Ball glüht auf, während er hochsteigt und fällt
                ultiFxT -= dt;
                if (ultiFxT <= 0f)
                {
                    ultiFxT = 0.03f;
                    var fx = FxSystem.I;
                    fx.Streak(FxLayer.Front, Ball.Pos + Random.insideUnitCircle * 0.15f, new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(1.5f, 3.5f)),
                        Random.Range(0.2f, 0.35f), 0.07f, 0.05f, new Color(1f, 0.85f, 0.45f), Palette.BlastOrange.WithAlpha(0f), 2.6f, 1.5f, 2f);
                }
                Vector2 dir = AimFrom(Ball.Pos);
                KickAimLocal = new Vector2(dir.x * Facing, dir.y);
                if (ActionTime >= VolleyContact) { released = true; ReleaseVolley(); }
            }
            if (ActionTime >= VolleyEnd) EndUlti();
        }

        void ReleaseVolley()
        {
            Vector2 from = Ball.Pos;
            Vector2 dir = AimFrom(from);
            if (Grounded && OnPlatform == Level.None && dir.y < -0.2f) dir = new Vector2(dir.x, -0.2f).normalized;
            if (Mathf.Abs(dir.x) > 0.05f && dir.x * Facing < 0f) dir.x = Mathf.Abs(dir.x) * Facing;   // ein Volley geht nach vorn
            dir.Normalize();
            KickAimLocal = new Vector2(dir.x * Facing, dir.y);
            float p = UltiPower, area = S.AreaMul;
            Ultis.I.Comet(this, from, dir, 110f * p, 85f * p, 4.2f * area, true);
            Ball.Park();
            UltiBallOut = true;
            Coop.SendUlti(UltiKind.Volley, 1, from, dir, 4.2f * area);

            var fx = FxSystem.I;
            Color fire = Palette.BlastOrange;
            fx.Flash(from, 2.6f, fire, 0.2f, 2.2f);
            fx.Flash(from, 0.8f, new Color(1f, 0.85f, 0.6f), 0.08f, 2.4f);
            fx.Ring(FxLayer.Front, from, 0.2f, 2.6f, 0.4f, 0.01f, 0.32f, Color.white, fire.WithAlpha(0f), 2.8f);
            fx.Ring(FxLayer.Front, from + dir * 0.6f, 0.1f, 1.4f, 0.22f, 0.01f, 0.22f, Palette.Gold, Palette.Hurt.WithAlpha(0f), 2.4f);
            fx.Sparks(from, dir, 30f, 26, 12f, 28f, Palette.Gold, 2.8f, 0.06f, 0.3f);
            fx.Sparks(from, -dir, 110f, 10, 3f, 8f, fire, 2.4f, 0.05f, 0.24f);
            for (int i = 0; i < 10; i++)
            {
                Vector2 side = new Vector2(-dir.y, dir.x) * Random.Range(-0.5f, 0.5f);
                fx.Streak(FxLayer.Front, from + side, dir * Random.Range(18f, 30f), Random.Range(0.16f, 0.28f), 0.05f, 0.06f,
                    Color.white.WithAlpha(0.9f), fire.WithAlpha(0f), 2.6f, 4f);
            }
            if (Grounded)
            {
                fx.Dust(Pos, new Vector2(-dir.x, 0.2f), 10, 3f, 0.5f, 0.4f);
                fx.Ring(FxLayer.Back, Pos + new Vector2(0f, 0.05f), 0.2f, 2.2f, 0.12f, 0.01f, 0.3f, Color.white.WithAlpha(0.7f), fire.WithAlpha(0f), 2f);
            }
            var game = Game.I;
            game.Cam.Kick(-dir * 0.45f);
            game.Cam.AddTrauma(0.55f);
            game.Cam.SetZoom(1f);
            game.Cam.ZoomPunch(0.06f);
            game.Post.Impact(0.9f);
            TimeFx.HitStop(0.09f, 0.03f);
            Vel.x -= dir.x * 5f;
            Rig.OnPowerContact();
        }

        // ------------------------------------------------------------------ BRUNO: Bollwerk

        void UpdateBulwark(float dt)
        {
            if (!released)
            {
                if (ActionTime >= BulwarkStomp)
                {
                    // in der Luft hält er die Pose und kracht erst auf den Boden
                    if (Grounded || ultiWait > 1.2f) { released = true; BulwarkStompNow(); }
                    else
                    {
                        ultiWait += dt;
                        ActionTime = BulwarkStomp;
                        Vel.y = Mathf.Min(Vel.y, -22f);
                    }
                }
            }
            else if (ActionTime >= BulwarkEnd) EndUlti();
        }

        void BulwarkStompNow()
        {
            Vector2 at = Pos;
            float p = UltiPower, area = S.AreaMul;
            Court.I.Shockwave(at + new Vector2(0f, 0.15f), 4.6f * area, 35f * p, 15f, 0.9f, 0f, true, Src.Ulti);
            Ultis.I.Dome(this, DomeLife, DomeRadius * area, p, true);
            Coop.SendUlti(UltiKind.Bulwark, 1, at, Vector2.zero, DomeRadius * area);
            Ultis.StompFx(at, DomeRadius * area);
            var game = Game.I;
            game.Cam.AddTrauma(0.55f);
            game.Cam.Kick(new Vector2(0f, -0.4f));
            game.Cam.SetZoom(1f);
            game.Post.Impact(0.6f);
            TimeFx.HitStop(0.09f, 0.04f);
            Rig.OnLand(22f);
        }

        // ------------------------------------------------------------------ MIRA: Ballzauber-Sturm

        void StartStorm()
        {
            ultiTargets.Clear();
        }

        void UpdateStorm(float dt)
        {
            // Mira steigt ein Stück in die Luft und schwebt dort, solange der Zauber läuft
            float hover = ultiFrom.y + 0.55f * MathUtil.Smooth01(ActionTime / 0.5f);
            Vel.y = (hover - Pos.y) * 8f;

            if (ultiStep == 0 && ActionTime >= StormSplit)
            {
                ultiStep = 1;
                Vector2 at = Ball.Pos;
                Ultis.I.StormOrbs(this, StormOrbs, at, 55f * UltiPower, true);
                Ball.Park();
                UltiBallOut = true;
                Coop.SendUlti(UltiKind.Storm, 1, at, Vector2.zero, 0f);
                var fx = FxSystem.I;
                fx.Flash(at, 1.8f, Palette.Trick, 0.18f, 1.8f);
                fx.Ring(FxLayer.Front, at, 0.1f, 2.4f, 0.3f, 0.02f, 0.4f, Color.white, Color.white.WithAlpha(0f), 2.2f, true);
                for (int i = 0; i < 16; i++)
                {
                    Color rc = Art.Rainbow(i / 16f);
                    fx.Streak(FxLayer.Front, at, MathUtil.Dir(i * 22.5f) * Random.Range(6f, 10f), 0.32f, 0.07f, 0.05f,
                        Color.Lerp(rc, Color.white, 0.3f), rc.WithAlpha(0f), 2.8f, 5f);
                }
                fx.Sparkles(at, 0.6f, 12, Palette.Gold, 3f, 0.7f);
                Game.I.Cam.ZoomPunch(-0.04f);
                Game.I.Cam.AddTrauma(0.2f);
            }
            // die fünf Bälle schießen nacheinander los, jeder auf seinen Gegner
            while (ultiStep >= 1 && ultiStep - 1 < StormOrbs && ActionTime >= StormLaunch + (ultiStep - 1) * StormGap)
            {
                LaunchStormOrb(ultiStep - 1);
                ultiStep++;
            }
            if (ActionTime >= StormEnd)
            {
                EndUlti();
                ReturnUltiBall(Pos, Vector2.zero, true);
            }
        }

        void LaunchStormOrb(int n)
        {
            if (n == 0)
            {
                // die Ziele: die nächsten Gegner zuerst, bei weniger als fünf bekommt der nächste mehrere
                ultiTargets.Clear();
                foreach (var m in Game.I.Waves.Monsters)
                    if (m.Alive && (m.Center - Pos).sqrMagnitude < 17f * 17f) ultiTargets.Add(m);
                Vector2 me = Pos;
                ultiTargets.Sort((a, b) => (a.Center - me).sqrMagnitude.CompareTo((b.Center - me).sqrMagnitude));
                Game.I.Cam.SetZoom(1f);
            }
            Monster target = ultiTargets.Count > 0 ? ultiTargets[n % ultiTargets.Count] : null;
            if (target != null && !target.Alive) target = Combat.NearestTo(Pos, 17f);
            Vector2 to = target != null ? target.Center
                : GameInput.AimWorld + new Vector2((n - 2) * 1.6f, Mathf.Abs(n - 2) * 0.4f);
            Vector2 d = to - (Pos + new Vector2(0f, 1.2f));
            if (d.sqrMagnitude > 0.01f) UltiPoint = new Vector2(d.normalized.x * Facing, d.normalized.y);
            if (Mathf.Abs(d.x) > 0.3f && n == 0) Facing = d.x > 0f ? 1 : -1;
            Vector2 from = Ultis.I.LaunchOrb(this, n, target, to);
            Coop.SendUlti(UltiKind.Storm, (byte)(2 + n), from, to, 0f);
            Game.I.Cam.AddTrauma(0.08f);
        }

        // ------------------------------------------------------------------ DRE: Buzzer Beater

        void StartBuzzer()
        {
            // bis zu fünf Gegner bekommen ein Fadenkreuz, die nächsten zuerst
            ultiTargets.Clear();
            ultiSpots.Clear();
            foreach (var m in Game.I.Waves.Monsters)
                if (m.Alive && (m.Center - Pos).sqrMagnitude < 20f * 20f) ultiTargets.Add(m);
            Vector2 me = Pos;
            ultiTargets.Sort((a, b) => (a.Center - me).sqrMagnitude.CompareTo((b.Center - me).sqrMagnitude));
            if (ultiTargets.Count > BuzzerBalls) ultiTargets.RemoveRange(BuzzerBalls, ultiTargets.Count - BuzzerBalls);
            // ohne Gegner fallen die Würfe fächerförmig um das Fadenkreuz
            Vector2 aim = GameInput.AimWorld;
            for (int i = 0; i < BuzzerBalls; i++)
            {
                float x = Mathf.Clamp(aim.x + (i - 2) * 1.9f, -ArenaHalf + 0.3f, ArenaHalf - 0.3f);
                ultiSpots.Add(new Vector2(x, Level.FloorBelow(x, aim.y + 0.35f) + Art.BallRadius));
            }
            var marks = new List<Monster>();
            for (int i = 0; i < BuzzerBalls; i++) marks.Add(ultiTargets.Count > 0 ? ultiTargets[i % ultiTargets.Count] : null);
            ultiTargets.Clear();
            ultiTargets.AddRange(marks);
            Ultis.I.Reticles(ultiTargets, ultiSpots);
            KickAimLocal = new Vector2(0.55f, 0.85f).normalized;
        }

        void UpdateBuzzer(float dt)
        {
            if (ultiStep == 0 && ActionTime >= BuzzerJump)
            {
                ultiStep = 1;
                if (Grounded)
                {
                    Vel.y = 14.5f * S.JumpMul;
                    Grounded = false;
                    OnPlatform = Level.None;
                    boostRise = true;
                    Rig.OnJump();
                    FxSystem.I.Dust(Pos, Vector2.right, 6, 2.4f, 0.42f, 0.34f);
                    FxSystem.I.Dust(Pos, Vector2.left, 6, 2.4f, 0.42f, 0.34f);
                }
                else Vel.y = Mathf.Max(Vel.y, 9f);
            }
            if (!released && ActionTime >= BuzzerRelease) { released = true; ReleaseBuzzer(); }
            if (released && ((Grounded && ActionTime > BuzzerRelease + 0.12f) || ActionTime >= BuzzerEnd + 0.6f)) EndUlti();
        }

        void ReleaseBuzzer()
        {
            Vector2 from = Ball.Pos;
            float p = UltiPower, area = S.AreaMul;
            for (int i = 0; i < BuzzerBalls; i++)
            {
                var m = i < ultiTargets.Count ? ultiTargets[i] : null;
                Vector2 to = m != null && m.Alive ? m.Center : ultiSpots[i];
                Vector2 start = from + new Vector2(Random.Range(-0.12f, 0.12f), Random.Range(-0.08f, 0.12f));
                Ultis.I.BuzzerLob(this, i, start, m, to, BuzzerFlight, 75f * p, 2.1f * area, true);
                Coop.SendUlti(UltiKind.Buzzer, (byte)(1 + i), start, to, BuzzerFlight);
            }
            Ball.Park();
            UltiBallOut = true;

            var fx = FxSystem.I;
            Color c = Palette.HoopFlame;
            fx.Flash(from, 1.8f, c, 0.18f, 1.9f);
            fx.Ring(FxLayer.Front, from, 0.15f, 2f, 0.28f, 0.01f, 0.3f, Color.white.WithAlpha(0.85f), c.WithAlpha(0f), 2.2f);
            fx.Sparks(from, new Vector2(Facing * 0.5f, 1f), 70f, 20, 7f, 16f, c, 2.6f, 0.05f, 0.3f);
            fx.Sparkles(from, 0.5f, 10, Palette.Gold, 2.8f, 0.6f);
            var game = Game.I;
            game.Cam.Kick(new Vector2(-Facing * 0.15f, -0.2f));
            game.Cam.AddTrauma(0.25f);
            game.Cam.SetZoom(1f);
            game.Post.Impact(0.4f);
            TimeFx.HitStop(0.05f, 0.05f);
            Rig.OnThrowRelease();
        }

        // ------------------------------------------------------------------ TITAN: Meteor-Dunk

        void StartMeteor()
        {
            Vector2 aim = GameInput.AimWorld;
            float tx = Mathf.Clamp(aim.x, Pos.x - MeteorRange, Pos.x + MeteorRange);
            tx = Mathf.Clamp(tx, -ArenaHalf + 0.5f, ArenaHalf - 0.5f);
            // er landet auf der Fläche unter dem Fadenkreuz (nie im Boden)
            float ty = Level.FloorBelow(tx, Mathf.Max(aim.y + 0.35f, Level.FloorBelow(tx, Pos.y + 0.05f) + 0.05f));
            HoopTarget = new Vector2(tx, ty);
            DunkFlight = MeteorFlight;
            if (Mathf.Abs(tx - Pos.x) > 0.2f) Facing = tx > Pos.x ? 1 : -1;
            float until = MeteorGather + MeteorFlight;
            Ultis.I.MeteorMark(HoopTarget, until, 6.5f * S.AreaMul);
            Coop.SendUlti(UltiKind.Meteor, 1, HoopTarget, Vector2.zero, until);
        }

        void UpdateMeteor(float dt)
        {
            var fx = FxSystem.I;
            if (!ultiFired && ultiStep == 0 && ActionTime >= MeteorGather)
            {
                // der Absprung: der Boden bricht unter ihm, er schießt aus dem Bild; die Kamera bleibt am Ziel
                ultiStep = 1;
                Rig.OnJump();
                fx.Dust(Pos, Vector2.right, 10, 3.6f, 0.55f, 0.4f);
                fx.Dust(Pos, Vector2.left, 10, 3.6f, 0.55f, 0.4f);
                fx.Ring(FxLayer.Front, Pos + new Vector2(0f, 0.1f), 0.3f, 2.4f, 0.16f, 0.01f, 0.3f, Color.white.WithAlpha(0.8f), Palette.Slam.WithAlpha(0f), 2f);
                for (int i = 0; i < 8; i++)
                    fx.Streak(FxLayer.Front, Pos + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(0.2f, 1.8f)), new Vector2(0f, Random.Range(14f, 24f)),
                        Random.Range(0.2f, 0.35f), 0.05f, 0.06f, Color.white.WithAlpha(0.85f), Palette.Slam.WithAlpha(0f), 2.4f, 3f);
                var game = Game.I;
                game.Cam.Kick(new Vector2(0f, 0.3f));
                game.Cam.AddTrauma(0.3f);
                game.Cam.SetZoom(1.06f);
                game.Cam.Hold(new Vector2(HoopTarget.x, HoopTarget.y), MeteorFlight + 0.5f);
            }
            if (!ultiFired && ActionTime >= MeteorGather)
            {
                float t = ActionTime - MeteorGather;
                ultiFxT -= dt;
                if (ultiFxT <= 0f)
                {
                    ultiFxT = 0.016f;
                    if (t < MeteorRise) ghosts.Spawn(Palette.Slam, 0.12f, 0.25f);
                    else if (t > MeteorRise + MeteorHang)
                    {
                        // der Fall: er brennt wie ein Meteor
                        ghosts.Spawn(UltiDefs.Accent(UltiKind.Meteor), 0.1f, 0.3f);
                        Vector2 c = Pos + new Vector2(0f, 1f);
                        fx.Flash(c, 2.2f, Palette.BlastOrange, 0.08f, 1.6f);
                        for (int i = 0; i < 3; i++)
                            fx.Streak(FxLayer.Front, c + Random.insideUnitCircle * 0.7f, new Vector2(Random.Range(-1f, 1f), Random.Range(10f, 18f)),
                                Random.Range(0.18f, 0.3f), 0.1f, 0.06f, new Color(1f, 0.85f, 0.5f), Palette.BlastOrange.WithAlpha(0f), 2.8f, 2f);
                        fx.Sparks(c, Vector2.up, 50f, 2, 4f, 10f, Palette.Gold, 2.4f, 0.05f, 0.25f);
                    }
                    else if (Random.value < 0.3f) Game.I.Cam.AddTrauma(0.03f);   // er ist weg, die Erde grollt
                }
            }
            if (ultiFired && ActionTime >= MeteorGather + MeteorFlight + MeteorRecover) EndUlti();
        }

        void MeteorSlam()
        {
            ultiFired = true;
            released = true;
            var s = S;
            Pos = HoopTarget;
            Vel = Vector2.zero;
            Grounded = true;
            Level.FloorBelow(Pos.x, Pos.y + 0.05f, FootHalf, Level.None, out int under);
            OnPlatform = under;
            platformVersion = Level.Version;
            airBoosts = s.AirBoosts;
            airDashes = s.AirDashes;
            ActionTime = MeteorGather + MeteorFlight;

            Vector2 at = new Vector2(Pos.x + Facing * 0.35f, Pos.y);
            Ball.Slam(at);
            float p = UltiPower, area = s.AreaMul;
            float r = 6.5f * area;
            Court.I.Shockwave(at + new Vector2(0f, 0.15f), r, 95f * p, 20f, 1.2f, 0f, true, Src.Ulti);
            Court.I.Shockwave(at + new Vector2(0f, 0.15f), r * 1.3f, 50f * p, 14f, 0.8f, 0.12f, true, Src.Ulti);
            Court.I.Shockwave(at + new Vector2(0f, 0.15f), r * 1.6f, 30f * p, 10f, 0.5f, 0.24f, true, Src.Ulti);
            Ultis.MeteorImpactFx(at, r);
            Coop.SendUlti(UltiKind.Meteor, 2, at, Vector2.zero, r);

            var game = Game.I;
            game.Cam.AddTrauma(1f);
            game.Cam.Kick(new Vector2(0f, -0.7f));
            game.Cam.SetZoom(1f);
            game.Cam.ZoomPunch(0.09f);
            game.Post.Impact(1f);
            TimeFx.HitStop(0.13f, 0.03f);
            Rig.OnLand(26f);
        }

        // ------------------------------------------------------------------ NOVA: Ankle Breaker

        /// <summary>Die Kette: immer der nächste noch stehende Gegner; gelandet wird knapp hinter ihm.</summary>
        bool PlanAnkleBreaker()
        {
            ultiTargets.Clear();
            ultiSpots.Clear();
            var pool = new List<Monster>();
            Vector2 c = Pos + new Vector2(0f, 1f);
            foreach (var m in Game.I.Waves.Monsters)
                if (m.Alive && (m.Center - c).sqrMagnitude < AnkleRange * AnkleRange) pool.Add(m);
            if (pool.Count == 0) return false;
            Vector2 at = Pos;
            while (pool.Count > 0 && ultiTargets.Count < AnkleMax)
            {
                int best = 0;
                float bd = float.MaxValue;
                for (int i = 0; i < pool.Count; i++)
                {
                    Vector2 d = pool[i].Center - at;
                    float score = d.x * d.x + d.y * d.y * 2.5f;   // lieber auf derselben Höhe weiter
                    if (score < bd) { bd = score; best = i; }
                }
                ultiTargets.Add(pool[best]);
                at = pool[best].Center;
                pool.RemoveAt(best);
            }
            Vector2 cur = Pos;
            foreach (var m in ultiTargets)
            {
                float side = Mathf.Sign(m.Center.x - cur.x);
                if (side == 0f) side = Facing;
                float x = Mathf.Clamp(m.Center.x + side * (m.Radius + 0.55f), -ArenaHalf + 0.3f, ArenaHalf - 0.3f);
                var spot = new Vector2(x, Level.FloorBelow(x, m.Center.y + 0.6f));
                ultiSpots.Add(spot);
                cur = spot;
            }
            UltiSteps = ultiTargets.Count;
            return true;
        }

        void UpdateAnkle(float dt)
        {
            var fx = FxSystem.I;
            Color c = Palette.Showboat;
            if (ActionTime >= AnkleCrouch && ultiStep < UltiSteps)
            {
                ghostTimer -= dt;
                if (ghostTimer <= 0f)
                {
                    ghostTimer = 0.018f;
                    ghosts.Spawn(ghostIndex++ % 2 == 0 ? c : Palette.Trick, 0.22f, 0.36f);
                    fx.Streak(FxLayer.Front, Pos + new Vector2(0f, Random.Range(0.2f, 1.6f)), new Vector2(-Facing * Random.Range(6f, 11f), 0f),
                        0.16f, 0.03f, 0.06f, Color.white.WithAlpha(0.75f), c.WithAlpha(0f), 2f, 5f);
                }
            }
            while (ultiStep < UltiSteps && ActionTime >= AnkleCrouch + (ultiStep + 1) * AnkleHop) AnkleHit(ultiStep++);
            float poseAt = AnkleCrouch + UltiSteps * AnkleHop;
            if (!ultiFired && ultiStep >= UltiSteps && ActionTime >= poseAt) { ultiFired = true; AnkleFinale(); }
            if (ActionTime >= poseAt + AnklePose) EndUlti();
        }

        static readonly string[] AnkleWords = { "KNÖCHEL!", "WEG!", "ZU LANGSAM!", "AUTSCH!" };

        void AnkleHit(int i)
        {
            var m = ultiTargets[i];
            Vector2 spot = ultiSpots[i];
            // gelandet: genau auf dem Punkt hinter dem Gegner
            Pos = spot;
            Vel = Vector2.zero;
            Grounded = true;
            Level.FloorBelow(Pos.x, Pos.y + 0.05f, FootHalf, Level.None, out int under);
            OnPlatform = under;
            platformVersion = Level.Version;

            var fx = FxSystem.I;
            Color c = Palette.Showboat;
            if (m.Alive)
            {
                Vector2 at = m.Center;
                Combat.Hit(m, 45f * UltiPower, new Vector2(Facing, 0.6f), 7f, Src.Ulti, big: true);
                if (m.Alive) { m.Stun(2.2f); m.Expose(3.5f); }
                fx.Ring(FxLayer.Front, at, 0.2f, m.Radius * 3f, 0.22f, 0.02f, 0.4f, Color.white, c.WithAlpha(0f), 2.6f);
                fx.Sparkles(at, m.Radius + 0.4f, 10, c, 2.8f, 0.7f);
                fx.Sparks(at, new Vector2(Facing, 0.4f), 70f, 10, 5f, 12f, c, 2.6f, 0.05f, 0.25f);
                Game.I.Hud.Popup(at + new Vector2(0f, m.Radius + 0.6f), AnkleWords[i % AnkleWords.Length], c, 30f, true);
            }
            fx.Dust(Pos, new Vector2(-Facing, 0.3f), 5, 2.2f, 0.36f, 0.3f);
            TimeFx.HitStop(0.035f, 0.05f);
            Game.I.Cam.AddTrauma(0.14f);
            Game.I.Cam.Kick(new Vector2(Facing * 0.12f, 0f));
            Rig.OnDash();
        }

        /// <summary>Die Pose am Ende: der Ball dreht sich auf dem Finger, alle Gefallenen bekommen noch einen mit.</summary>
        void AnkleFinale()
        {
            var fx = FxSystem.I;
            Color c = Palette.Showboat;
            float p = UltiPower;
            foreach (var m in ultiTargets)
            {
                if (!m.Alive) continue;
                Combat.Hit(m, 30f * p, Vector2.up, 4f, Src.Ulti, big: true);
                fx.Sparkles(m.Center + new Vector2(0f, m.Radius), 0.5f, 6, c, 2.6f, 0.6f);
            }
            Ball.JuggleMode = true;
            Ball.OnJuggleTouch(-Facing * 1500f);
            Vector2 at = Pos + new Vector2(0f, 1.2f);
            Ultis.AnkleFinaleFx(at);
            Coop.SendUlti(UltiKind.AnkleBreaker, 1, at, Vector2.zero, 0f);
            Game.I.Hud.Popup(Pos + new Vector2(0f, 3.1f), "ANKLE BREAKER", c, 44f, true);
            Game.I.Cam.SetZoom(1f);
            Game.I.Cam.ZoomPunch(-0.05f);
            Game.I.Post.Impact(0.5f);
        }
    }
}
