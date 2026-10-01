using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Szenario „boxing“: jeder Boxer nah und in der Szene, Boxstand und Laufen, die Kombination (Jab, Gerade, Haken),
    /// der Klassen-Move vor ein paar Gegnern, die fünf Boss-Fähigkeiten und die Ulti – als Kontaktbögen und Einzelbilder.
    /// Danach das Titelbild mit jedem Boxer und die Spielerauswahl. Logzeilen „[Box]“ mit den Zahlen hinter den Moves.
    /// Mit -sfBoxer kai|vera|luz nur einer.
    /// </summary>
    public sealed partial class CaptureDriver
    {
        IEnumerator BoxingTour()
        {
            string only = Arg("-sfBoxer");
            foreach (string id in new[] { "kai", "vera", "luz" })
            {
                if (!string.IsNullOrEmpty(only) && only != id) continue;
                var def = Characters.Get(id);
                Characters.Preview(Characters.IndexOf(def));
                G.Director.DebugJump(1, 1, 0, false);
                G.Waves.Restart(999f);
                P.ApplyStats(true);
                P.Pos = new Vector2(-3f, 0f);
                P.Vel = Vector2.zero;
                P.Facing = 1;
                P.DodgeTime = 999f;
                P.Rig.ResetPose();
                Move(0f);
                Aim(new Vector2(5f, 1.2f));
                yield return Seconds(1f);
                Debug.Log($"[Box] {def.Name}: sport {P.Rig.Sport}, primary {G.Run.Primary}, ball {G.Ball.St}, hip {P.Rig.Body.StandHip:0.00}, tucked {P.Rig.Body.Tucked}, ulti {P.Ulti}");

                // nah und in der Szene
                G.Hud.SetVisible(false);
                G.Cam.SetOverride(P.Pos + new Vector2(0.15f, 1.1f), 1.25f);
                yield return Frames(3);
                yield return Shot("b_" + id + "_portrait");
                G.Cam.ClearOverride();
                G.Hud.SetVisible(true);
                yield return Frames(3);
                yield return Shot("b_" + id + "_scene");

                // Boxstand und Laufen
                BeginSheet(6, 1);
                for (int i = 0; i < 6; i++) { yield return SheetCell(new Vector2(0.15f, 1.05f), 1.35f); yield return Frames(6); }
                EndSheet("b_" + id + "_idle");
                Move(1f);
                yield return Seconds(0.5f);
                BeginSheet(6, 2);
                for (int i = 0; i < 12; i++) { yield return SheetCell(new Vector2(0.3f, 1.05f), 1.35f); yield return Frames(2); }
                EndSheet("b_" + id + "_run");
                Move(0f);
                yield return Seconds(0.7f);

                // die Kombination: Jab, Gerade, Haken
                G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(3.2f, 0f));
                yield return Frames(10);
                Monster.Hold = true;
                Aim(new Vector2(5f, 0.9f));
                BeginSheet(8, 3);
                for (int i = 0; i < 24; i++)
                {
                    if (i == 0 || i == 8 || i == 16) GameInput.ShootPressed = true;
                    yield return SheetCell(new Vector2(0.4f, 1.15f), 1.6f);
                    yield return Frames(1);
                }
                EndSheet("b_" + id + "_combo");
                Debug.Log($"[Box] {def.Name}: combo step {P.PunchStep}, serial {P.PunchSerial}, shot cd {P.ShotCooldownTotal:0.00}s");
                GameInput.ShootPressed = true;
                yield return Frames(6);
                yield return Shot("b_" + id + "_punch_wave");
                Monster.Hold = false;
                yield return Seconds(0.5f);

                // der Klassen-Move vor drei Gegnern
                G.Waves.Restart(999f);
                P.Pos = new Vector2(-3f, 0f);
                var crowd = new List<Monster>
                {
                    G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(3.4f, 0f)),
                    G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(5.4f, 0f)),
                    G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(7.4f, 0f)),
                };
                yield return Frames(12);
                Monster.Hold = true;
                float hp0 = 0f;
                foreach (var m in crowd) hp0 += m.Hp;
                var primary = G.Run.Primary;
                Aim(new Vector2(6f, 0.8f));
                if (primary == Ability.Slip) Move(1f);
                GameInput.PowerPressed = true;
                // die Deckung: ein Gegner läuft hinein (zählt für den Konter)
                if (primary == Ability.Guard) { P.DodgeTime = 0f; P.InvulnTimer = 0f; }
                int cells = primary == Ability.Guard ? 24 : 18;
                BeginSheet(6, cells / 6);
                for (int i = 0; i < cells; i++)
                {
                    if (primary == Ability.Guard && i == 6) P.TakeDamage(10f, P.Pos + new Vector2(1f, 1f));
                    if (primary == Ability.Guard && i == 9) P.TakeDamage(10f, P.Pos + new Vector2(1f, 1f));
                    if (primary == Ability.Slip && i == 10) { Move(0f); GameInput.ShootPressed = true; }
                    yield return SheetCell(new Vector2(0.6f, 1.2f), 1.9f);
                    yield return Frames(primary == Ability.Guard ? 4 : 2);
                }
                Move(0f);
                EndSheet("b_" + id + "_" + primary.ToString().ToLowerInvariant());
                yield return Shot("b_" + id + "_" + primary.ToString().ToLowerInvariant() + "_after");
                float hp1 = 0f;
                foreach (var m in crowd) hp1 += m.Alive ? m.Hp : 0f;
                Debug.Log($"[Box] {def.Name}: {primary} dealt {hp0 - hp1:0} of {hp0:0}, guard hits {P.GuardHits}, counter ready {P.SlipReadyLeft:0.00}, hp {P.Hp:0}");
                Monster.Hold = false;
                P.DodgeTime = 999f;
                yield return Seconds(0.8f);
            }

            // die Boss-Fähigkeiten (mit KAI)
            if (string.IsNullOrEmpty(only) || only == "kai")
            {
                Characters.Preview(Characters.IndexOf(Characters.Get("kai")));
                G.Director.DebugJump(1, 1, 0, false);
                foreach (var a in Abilities.BoxUnlockable) G.Run.Unlock(a);
                P.ApplyStats(true);
                foreach (var a in Abilities.BoxUnlockable)
                {
                    G.Waves.Restart(999f);
                    P.Pos = new Vector2(-3f, 0f);
                    P.Vel = Vector2.zero;
                    P.Facing = 1;
                    P.DodgeTime = 999f;
                    var crowd = new List<Monster>
                    {
                        G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(2.4f, 0f)),
                        G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(-2.6f, 0f)),
                        G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(5.2f, 0f)),
                        G.Waves.SpawnAt(Monster.Kind.Wisp, P.Pos + new Vector2(1.6f, 3f)),
                    };
                    yield return Seconds(0.5f);
                    Monster.Hold = true;
                    float hp0 = 0f;
                    foreach (var m in crowd) hp0 += m.Hp;
                    Aim(new Vector2(6f, 1f));
                    GameInput.PressAbility(a);
                    BeginSheet(6, 3);
                    for (int i = 0; i < 18; i++) { yield return SheetCell(new Vector2(0.3f, 1.4f), 2f); yield return Frames(2); }
                    EndSheet("b_skill_" + a.ToString().ToLowerInvariant());
                    yield return Shot("b_skill_" + a.ToString().ToLowerInvariant() + "_after");
                    float hp1 = 0f;
                    int hit = 0;
                    foreach (var m in crowd) { float h = m.Alive ? m.Hp : 0f; hp1 += h; if (h < m.MaxHp - 0.1f) hit++; }
                    Debug.Log($"[Box] skill {a}: dealt {hp0 - hp1:0}, hit {hit}/{crowd.Count}, action {P.CurrentAction}");
                    Monster.Hold = false;
                    yield return Seconds(1.2f);
                }
            }

            // die Ultis
            foreach (string id in new[] { "kai", "vera", "luz" })
            {
                if (!string.IsNullOrEmpty(only) && only != id) continue;
                Characters.Preview(Characters.IndexOf(Characters.Get(id)));
                G.Director.DebugJump(1, 2, 0, false);
                P.ApplyStats(true);
                G.Waves.Restart(999f);
                P.Pos = new Vector2(-4f, 0f);
                P.Vel = Vector2.zero;
                P.Facing = 1;
                P.Rig.ResetPose();
                Move(0f);
                Aim(new Vector2(7f, 0.6f));
                var crowd = new List<Monster>();
                foreach (float dx in new[] { 3f, 5.2f, 7.4f, 9.6f, -3.2f })
                    crowd.Add(G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(dx, 0f)));
                yield return Seconds(0.9f);
                Monster.Hold = true;
                float hp0 = 0f;
                foreach (var m in crowd) hp0 += m.Hp;
                P.UltiCharge = 1f;
                GameInput.UltiPressed = true;
                yield return null;
                Debug.Log($"[Box] ulti {id}: {P.Ulti} started {P.CurrentAction == Player.Action.Ulti}");
                for (int i = 0; i < 16; i++) { yield return Shot("b_ulti_" + id + "_" + i.ToString("00")); yield return Frames(6); }
                yield return Seconds(1f);
                float hp1 = 0f;
                int alive = 0;
                foreach (var m in crowd) { hp1 += m.Alive ? m.Hp : 0f; if (m.Alive) alive++; }
                Debug.Log($"[Box] ulti {id}: dealt {hp0 - hp1:0} of {hp0:0}, alive {alive}/{crowd.Count}, action {P.CurrentAction}");
                Monster.Hold = false;
                G.Waves.Restart(999f);
                yield return Seconds(0.6f);
            }

            // Belohnungskarten eines Boxers: Upgrades jeder Seltenheit, die Fähigkeiten nach dem Boss, alle Kartenbilder
            if (string.IsNullOrEmpty(only))
            {
                Characters.Preview(Characters.IndexOf(Characters.Get("vera")));
                G.Director.DebugJump(1, 1, 0, false);
                G.Run.Unlock(Ability.Uppercut);
                yield return ShowCards("b_cards_1", UpgradeDb.Get("bx_reach"), UpgradeDb.Get("bx_combo"), UpgradeDb.Get("bx_fan"));
                yield return ShowCards("b_cards_2", UpgradeDb.Get("bx_iron"), UpgradeDb.Get("bx_ko"), UpgradeDb.Get("bx_steel"));
                yield return ShowCards("b_cards_3", UpgradeDb.Get("bx_twinupper"), UpgradeDb.Get("bx_sky"), UpgradeDb.Get("bx_mirror"));
                G.Rewards.ShowAbilities(new List<Ability> { Ability.Hooks, Ability.Shadow, Ability.Flurry }, G.Run.Stage, a => { });
                yield return Seconds(1.4f);
                yield return Shot("b_cards_skills");
                G.Rewards.Cancel();
                yield return Frames(2);
                var offer = UpgradeRoller.Offer(G.Run, 3, false);
                Debug.Log("[Box] offer: " + string.Join(", ", offer.ConvertAll(u => u.Name + (u.Sport == Sport.Boxing ? "" : " (FALSCHE SPORTART!)"))));
                int boxCards = 0;
                foreach (var u in UpgradeDb.All) if (u.Sport == Sport.Boxing) boxCards++;
                Debug.Log($"[Box] cards: {boxCards} boxing; boss pool [{string.Join(",", G.Run.LockedAbilities())}]");
                yield return EmblemSheet();
            }

            // das Titelbild mit jedem Boxer, dann die Spielerauswahl
            foreach (string id in new[] { "kai", "vera", "luz" })
            {
                if (!string.IsNullOrEmpty(only) && only != id) continue;
                var seed = new ProfileData { Character = id };
                seed.Characters.Add(id);
                seed.Flags.Add(Profile.FlagStarter);
                Profile.UseTransient(seed);
                Characters.Reload();
                G.ToMenu();
                GameInput.AimScreen = G.Menu.ScreenOf(new Vector2(-600f, 0f));
                yield return Seconds(1.8f);
                for (int i = 0; i < 6; i++) { yield return Shot("b_menu_" + id + "_" + i); yield return Frames(14); }
                yield return Kick("tab_chars");
                yield return Seconds(2f);
                GameInput.AimScreen = G.Menu.ScreenOf(new Vector2(-760f, -200f));
                yield return Seconds(0.6f);
                yield return Shot("b_page_" + id);
            }
            // der Shop mit den Karten der Boxer
            if (string.IsNullOrEmpty(only))
            {
                yield return Kick("tab_shop");
                yield return Seconds(2f);
                yield return Shot("b_shop");
            }
        }
    }
}
