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
                        if (!look.Cutout || look.Body == null || look.Eyes.Count == 0)
                            throw new InvalidOperationException("Monsterbild oder bewegliches Auge fehlt: " + theme.Kit + "/" + look.Look);
                        foreach (var part in look.Parts) if (part.Sprite == null)
                            throw new InvalidOperationException("Monsterteil fehlt: " + look.Look + "/" + part.Name);
                        foreach (var chain in look.Chains) if (chain.Sprite == null)
                            throw new InvalidOperationException("Monsterschweif fehlt: " + look.Look);
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
                        Debug.Log("[MonsterArt] " + theme.Kit + "/" + look.Look + ": acht Zustände, " + look.Parts.Count + " Teile, " + look.Eyes.Count + " Augen");
                    }
                    EndSheet("monster_" + stage + "_" + theme.Kit);
                }
            }
            finally { Monster.Hold = false; G.Waves.Restart(); G.Hud.SetVisible(true); }
        }
    }
}
