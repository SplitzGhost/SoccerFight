using System.Collections;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Basketball check: each basketball player up close and in the scene, the dribble standing and
    /// running, the throw, the class move, then the three boss skills — as contact sheets, with a few
    /// full frames for the effects. Logs "[Hoops]" lines with the numbers behind the moves.
    /// </summary>
    public sealed partial class CaptureDriver
    {
        /// <summary>Alle Menüfiguren über einen vollständigen Dribbeltakt, ohne den Spielstand anzufassen.</summary>
        IEnumerator MenuBodies()
        {
            foreach (string id in new[] { "rio", "bruno", "mira", "dre", "titan", "nova" })
            {
                var seed = new ProfileData { Character = id };
                seed.Characters.Add(id);
                seed.Flags.Add(Profile.FlagStarter);
                Profile.UseTransient(seed);
                Characters.Reload();
                G.ToMenu();
                GameInput.AimScreen = G.Menu.ScreenOf(new Vector2(-600f, 0f));
                yield return Seconds(1.6f);
                for (int i = 0; i < 12; i++)
                {
                    yield return Shot("body_" + id + "_" + i.ToString("00"));
                    yield return Frames(3);
                }
            }
        }

        /// <summary>Dribbelrhythmus aller sechs Figuren im Stand, Anlaufen und Laufen.</summary>
        IEnumerator Dribble()
        {
            foreach (string id in new[] { "rio", "bruno", "mira", "dre", "titan", "nova" })
            {
                Characters.Preview(Characters.IndexOf(Characters.Get(id)));
                G.Director.DebugJump(1, 1, 0, false);
                P.ApplyStats(true);
                P.Pos = new Vector2(-4f, 0f);
                P.Vel = Vector2.zero;
                P.Rig.ResetPose();
                G.Ball.ResetTo(P.Pos + new Vector2(0.6f, Art.BallRadius));
                Aim(new Vector2(5f, 1.6f));
                Move(0f);
                yield return Seconds(0.7f);
                BeginSheet(6, 1);
                for (int i = 0; i < 6; i++) { yield return SheetCell(new Vector2(0.25f, 1f), 1.4f); yield return Frames(5); }
                EndSheet("d_" + id + "_idle");
                Move(1f);
                BeginSheet(6, 1);
                for (int i = 0; i < 6; i++) { yield return SheetCell(new Vector2(0.25f, 1f), 1.4f); yield return Frames(3); }
                EndSheet("d_" + id + "_start");
                yield return Seconds(0.45f);
                BeginSheet(6, 2);
                for (int i = 0; i < 12; i++) { yield return SheetCell(new Vector2(0.25f, 1f), 1.4f); yield return Frames(2); }
                EndSheet("d_" + id + "_run");
                Move(0f);
                yield return Seconds(0.5f);
            }

            // die Menüfigur: ein Basketballer auf dem Titelbild, mehrere Bilder über einen Dribbel-Takt
            Profile.UseTransient();
            Characters.Reload();
            G.ToMenu();
            GameInput.AimScreen = G.Menu.ScreenOf(new Vector2(0f, -470f));
            yield return Seconds(2.8f);
            yield return Kick("starterSport1");
            yield return Seconds(2.2f);
            yield return Kick("starter_titan");
            yield return Seconds(1f);
            yield return Kick("starterGo");
            yield return Seconds(2.5f);
            for (int i = 0; i < 8; i++) { yield return Shot("d_menu_" + i); yield return Frames(4); }
        }

        IEnumerator Hoops()
        {
            string[] ids = { "dre", "titan", "nova" };
            foreach (var id in ids)
            {
                var def = Characters.Get(id);
                Characters.Preview(Characters.IndexOf(def));
                G.Director.DebugJump(1, 1, 0, false);
                foreach (var a in Abilities.HoopsUnlockable) G.Run.Unlock(a);
                P.ApplyStats(true);
                P.Pos = new Vector2(-3f, 0f);
                P.Vel = Vector2.zero;
                P.DodgeTime = 999f;
                Aim(new Vector2(5f, 1.6f));
                yield return Seconds(1f);
                Debug.Log($"[Hoops] {def.Name}: sport {P.Rig.Sport}, primary {G.Run.Primary}, ball {P.Ball.Kind}, hip {P.Rig.Body.StandHip:0.00}");

                // the body up close, then in the scene
                G.Hud.SetVisible(false);
                G.Cam.SetOverride(P.Pos + new Vector2(0.15f, 1.1f), 1.25f);
                yield return Frames(3);
                yield return Shot("h_" + id + "_portrait");
                G.Cam.ClearOverride();
                G.Hud.SetVisible(true);
                yield return Frames(3);
                yield return Shot("h_" + id + "_scene");

                // the dribble: standing, then running
                BeginSheet(6, 1);
                for (int i = 0; i < 6; i++) { yield return SheetCell(new Vector2(0.2f, 1.05f), 1.35f); yield return Frames(5); }
                EndSheet("h_" + id + "_idle");
                Move(1f);
                yield return Seconds(0.5f);
                BeginSheet(6, 2);
                for (int i = 0; i < 12; i++) { yield return SheetCell(new Vector2(0.3f, 1.05f), 1.35f); yield return Frames(2); }
                EndSheet("h_" + id + "_run");
                Move(0f);
                yield return Seconds(0.7f);

                // the throw
                Aim(new Vector2(6f, 1.4f));
                GameInput.ShootPressed = true;
                BeginSheet(6, 2);
                for (int i = 0; i < 12; i++) { yield return SheetCell(new Vector2(0.4f, 1.1f), 1.4f); yield return Frames(1); }
                EndSheet("h_" + id + "_throw");
                yield return WaitBallHome();
                yield return Seconds(0.3f);

                // the class move on a few blobs
                G.Waves.Restart(999f);
                G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(5.5f, 0f));
                G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(6.6f, 0f));
                G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(4.2f, 0f));
                yield return Frames(10);
                var primary = G.Run.Primary;
                Aim(primary == Ability.Dunk ? new Vector2(5.2f, 0f) : primary == Ability.Three ? new Vector2(6f, 0.3f) : new Vector2(5f, 1.2f));
                GameInput.PowerPressed = true;
                int cells = primary == Ability.Dunk ? 24 : 18;
                BeginSheet(6, cells / 6);
                for (int i = 0; i < cells; i++) { yield return SheetCell(new Vector2(0.4f, 1.2f), 1.8f); yield return Frames(primary == Ability.Crossover ? 2 : 3); }
                EndSheet("h_" + id + "_" + primary.ToString().ToLowerInvariant());
                yield return Shot("h_" + id + "_" + primary.ToString().ToLowerInvariant() + "_after");
                Debug.Log($"[Hoops] {def.Name}: {primary} cd {(primary == Ability.Three ? P.ThreeCooldownTotal : primary == Ability.Dunk ? P.DunkCooldownTotal : P.CrossCooldownTotal):0.0}s, boost {P.CrossBoostLeft:0.0}s, pos {P.Pos}");
                if (primary == Ability.Crossover)
                {
                    // the boost: throws fly through the blobs
                    Aim(new Vector2(8f, 0.6f));
                    GameInput.ShootPressed = true;
                    yield return Frames(14);
                    yield return Shot("h_" + id + "_pierce");
                }
                yield return Seconds(1.2f);
                yield return WaitBallHome();
            }

            // what the cards and the boss offer for a basketball player
            for (int i = 0; i < 3; i++)
            {
                var offer = UpgradeRoller.Offer(G.Run, 3, i == 2);
                Debug.Log("[Hoops] offer: " + string.Join(", ", offer.ConvertAll(u => u.Name + (u.Sport == Sport.Basketball ? "" : " (FUSSBALL!)"))));
            }
            int hoopsCards = 0;
            foreach (var u in UpgradeDb.All) if (u.Sport == Sport.Basketball) hoopsCards++;
            Debug.Log($"[Hoops] cards: {hoopsCards} basketball, {UpgradeDb.All.Count - hoopsCards} soccer; boss pool [{string.Join(",", G.Run.LockedAbilities())}]");

            // the three boss skills (with the last player)
            G.Waves.Restart(999f);
            P.Pos = new Vector2(-3f, 0f);
            yield return WaitBallHome();
            G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(4.5f, 0f));
            yield return Frames(10);
            GameInput.PressAbility(Ability.AlleyOop);
            BeginSheet(6, 3);
            for (int i = 0; i < 18; i++) { yield return SheetCell(new Vector2(1.2f, 2.4f), 2.6f); yield return Frames(4); }
            EndSheet("h_skill_alleyoop");
            yield return Seconds(1f);
            yield return WaitBallHome();

            GameInput.PressAbility(Ability.Block);
            BeginSheet(6, 1);
            for (int i = 0; i < 6; i++) { yield return SheetCell(new Vector2(0f, 1.4f), 1.7f); yield return Frames(4); }
            EndSheet("h_skill_block");
            yield return Seconds(0.8f);

            G.Waves.Restart(999f);
            G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(3f, 0f));
            G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(4.5f, 0f));
            yield return Frames(10);
            Move(1f);
            GameInput.PressAbility(Ability.FastBreak);
            yield return Frames(1);
            Move(0f);
            BeginSheet(6, 2);
            for (int i = 0; i < 12; i++) { yield return SheetCell(new Vector2(0.4f, 1.1f), 1.8f); yield return Frames(2); }
            EndSheet("h_skill_fastbreak");
            yield return Seconds(1f);
            yield return Shot("h_end");

            // ---- the menus: the first launch with both sports, the title with a basketball player, roster and shop
            Profile.UseTransient();
            Characters.Reload();
            G.ToMenu();
            GameInput.AimScreen = G.Menu.ScreenOf(new Vector2(0f, -470f));
            yield return Seconds(2.8f);
            yield return Shot("h_menu_starter_soccer");
            yield return Kick("starterSport1");
            yield return Seconds(2.2f);
            yield return Shot("h_menu_starter_hoops");
            yield return Kick("starter_titan");
            yield return Seconds(1f);
            yield return Kick("starterGo");
            yield return Seconds(2f);
            yield return Shot("h_menu_title_titan");
            yield return Kick("tab_chars");
            yield return Seconds(2.6f);
            yield return Shot("h_menu_roster_hoops");
            yield return Kick("sport0");
            yield return Seconds(2.2f);
            yield return Shot("h_menu_roster_soccer");
            Wallet.Add(Currencies.Coins, 600);
            yield return Kick("tab_shop");
            yield return Seconds(3f);
            yield return Shot("h_menu_shop");
            yield return Kick("shop_char_nova");
            yield return Frames(10);
            yield return Kick("shop_char_nova");
            yield return Seconds(0.8f);
            yield return Kick("shop_char_nova");
            yield return Seconds(1.2f);
            yield return Kick("tab_home");
            yield return Seconds(1.6f);
            yield return Shot("h_menu_title_nova");
            Debug.Log($"[Hoops] menu: owns titan={Profile.OwnsCharacter("titan")} nova={Profile.OwnsCharacter("nova")}, current {Characters.Current.Name}, coins {Wallet.Get(Currencies.Coins)}");
            yield return Kick("play", false);
            yield return Frames(20);
            yield return Shot("h_menu_play");
            yield return Seconds(2f);
            yield return Shot("h_run_start");
        }
    }
}
