using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SoccerFight
{
    public sealed partial class CaptureDriver
    {
        // Kontrollierte Darstellungszustände; Angriffsauslösung und Balance bleiben unverändert.
        static void MonsterField(Monster monster, string name, float value)
        {
            typeof(Monster).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(monster, value);
        }

        IEnumerator MonsterArtTour()
        {
            Monster.Hold = true;
            try
            {
                for (int stage = 1; stage <= StageThemes.All.Length; stage++)
                {
                    G.Director.DebugJump(stage, 1, 0, false);
                    yield return Frames(2);
                    var theme = G.Run.Theme;
                    var types = new List<EnemyType>();
                    foreach (var entry in theme.Roster) if (!types.Contains(entry.Type)) types.Add(entry.Type);
                    if (types.Contains(EnemyType.Splitter) && !types.Contains(EnemyType.Spawnling)) types.Add(EnemyType.Spawnling);
                    BeginSheet(8, types.Count + 1);
                    for (int row = 0; row <= types.Count; row++)
                    {
                        G.Waves.Restart();
                        FxSystem.I.Clear();
                        bool boss = row == types.Count;
                        var type = boss ? (theme.Boss.Body == Monster.Kind.Wisp ? EnemyType.Lantern : EnemyType.Brute) : types[row];
                        var spec = new Monster.SpawnSpec { Type = type, At = new Vector2(0f, 1f),
                            Theme = theme, Level = 1f, Rank = boss ? Rank.Boss : Rank.Normal, Boss = boss ? theme.Boss : null };
                        var look = MonsterArt.Get(theme, Monster.LookFor(spec));
                        if (!look.Cutout || look.Body == null)
                            throw new InvalidOperationException("Monsterbild fehlt: " + theme.Kit + "/" + look.Look);
                        var monster = G.Waves.Spawn(spec);
                        for (int pose = 0; pose < 8; pose++)
                        {
                            P.Pos = new Vector2(pose == 3 ? -12f : 12f, 1f); P.Vel = Vector2.zero;
                            MonsterField(monster, "windup", pose == 1 ? 0.5f : 0f);
                            MonsterField(monster, "diveTime", pose == 2 ? 1f : 0f);
                            MonsterField(monster, "primeTime", pose == 1 ? 1f : 0f);
                            monster.FreezeTime = pose == 4 ? 2f : 0f;
                            monster.BurnTime = pose == 5 ? 2f : 0f;
                            MonsterField(monster, "blinkTimer", 10f);
                            yield return Frames(16);
                            if (pose == 6) MonsterField(monster, "blink", 0.58f);
                            if (pose == 7) MonsterField(monster, "flash", 1f);
                            G.Hud.SetVisible(false);
                            float size = Mathf.Max(0.8f, monster.Radius * 2.5f);
                            yield return SheetCell(monster.Center - P.Pos, size);
                        }
                        Debug.Log("[MonsterArt] " + theme.Kit + "/" + look.Look + ": acht Zustände, ganzes Bild");
                    }
                    EndSheet("monster_" + stage + "_" + theme.Kit);
                }
            }
            finally { Monster.Hold = false; G.Waves.Restart(); G.Hud.SetVisible(true); }
        }

        /// <summary>
        /// How the painted monsters bend: per stage one row per monster — rest, winding up, rushing forwards,
        /// rising, falling, the landing, the jiggle after it and a hit. The monster stays in place (Hold); only
        /// its velocity is set, which is what drives the bones.
        /// </summary>
        IEnumerator MonsterMotionTour()
        {
            Monster.Hold = true;
            try
            {
                for (int stage = 1; stage <= StageThemes.All.Length; stage++)
                {
                    G.Director.DebugJump(stage, 1, 0, false);
                    yield return Frames(2);
                    var theme = G.Run.Theme;
                    var types = new List<EnemyType>();
                    foreach (var entry in theme.Roster) if (!types.Contains(entry.Type)) types.Add(entry.Type);
                    if (types.Contains(EnemyType.Splitter) && !types.Contains(EnemyType.Spawnling)) types.Add(EnemyType.Spawnling);
                    BeginSheet(8, types.Count + 1);
                    for (int row = 0; row <= types.Count; row++)
                    {
                        G.Waves.Restart();
                        FxSystem.I.Clear();
                        bool boss = row == types.Count;
                        var type = boss ? (theme.Boss.Body == Monster.Kind.Wisp ? EnemyType.Lantern : EnemyType.Brute) : types[row];
                        var spec = new Monster.SpawnSpec { Type = type, At = new Vector2(0f, 1f),
                            Theme = theme, Level = 1f, Rank = boss ? Rank.Boss : Rank.Normal, Boss = boss ? theme.Boss : null };
                        var monster = G.Waves.Spawn(spec);
                        P.Pos = new Vector2(12f, 1f); P.Vel = Vector2.zero;
                        G.Hud.SetVisible(false);
                        float size = Mathf.Max(0.8f, monster.Radius * 2.5f);
                        Vector2[] vel = { Vector2.zero, Vector2.zero, new Vector2(7f, 0f), new Vector2(2f, 7f), new Vector2(2f, -8f), Vector2.zero, Vector2.zero, Vector2.zero };
                        int[] wait = { 60, 30, 16, 14, 14, 3, 7, 9 };
                        for (int pose = 0; pose < 8; pose++)
                        {
                            monster.Vel = vel[pose];
                            MonsterField(monster, "windup", pose == 1 ? 0.5f : 0f);
                            MonsterField(monster, "primeTime", 0f);
                            if (pose == 7) { monster.Hit(0.001f, Vector2.left, 3f, true); monster.Vel = Vector2.zero; }
                            if (pose == 7) { yield return Frames(wait[pose]); MonsterField(monster, "flash", 0.0001f); }
                            yield return Frames(wait[pose]);
                            yield return SheetCell(monster.Center - P.Pos, size);
                        }
                        Debug.Log("[MonsterMotion] " + theme.Kit + "/" + monster.BuiltLook);
                    }
                    EndSheet("motion_" + stage + "_" + theme.Kit);
                }
            }
            finally { Monster.Hold = false; G.Waves.Restart(); G.Hud.SetVisible(true); }
        }
    }
}
