using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Szenario „ultis“: jeder der sechs Spieler vor einer kleinen Gegnergruppe, die Leiste füllt sich durch
    /// Schaden, dann die Ulti in einer Bildfolge. Logzeilen „[Ulti]“: Kosten der Leiste, was ein Treffer
    /// bringt, wie viel Schaden die Ulti machte und wie viele Gegner sie erwischte.
    /// </summary>
    public sealed partial class CaptureDriver
    {
        IEnumerator UltiTour()
        {
            string only = Arg("-sfUlti");
            foreach (string id in new[] { "rio", "bruno", "mira", "dre", "titan", "nova" })
            {
                if (!string.IsNullOrEmpty(only) && only != id) continue;
                Characters.Preview(Characters.IndexOf(Characters.Get(id)));
                G.Director.DebugJump(1, 2, 0, false);
                P.ApplyStats(true);
                G.Waves.Restart(999f);
                Monster.Hold = false;
                P.Pos = new Vector2(-4f, 0f);
                P.Vel = Vector2.zero;
                P.Facing = 1;
                P.Rig.ResetPose();
                G.Ball.ResetTo(P.Pos + new Vector2(0.6f, Art.BallRadius));
                G.Hud.SetVisible(true);
                Move(0f);
                Aim(new Vector2(6f, 0.6f));
                yield return Seconds(0.6f);

                var crowd = new List<Monster>();
                foreach (float dx in new[] { 3f, 5.2f, 7.4f, 9.6f, -3.2f })
                    crowd.Add(G.Waves.SpawnAt(Monster.Kind.Blob, P.Pos + new Vector2(dx, 0f)));
                crowd.Add(G.Waves.SpawnAt(Monster.Kind.Wisp, P.Pos + new Vector2(6.2f, 3.4f)));
                yield return Seconds(0.9f);
                Monster.Hold = true;

                // die Leiste: ein Treffer füllt sie um so viel, wie er an Schaden bringt
                P.UltiCharge = 0f;
                float before = P.UltiCharge;
                Combat.Hit(crowd[4], 20f, Vector2.left, 0f, Src.Shot);
                Debug.Log($"[Ulti] {id}: Leiste kostet {P.UltiCost:0} Schaden (Stufe {G.Run.Level:0.0}), ein Treffer brachte {(P.UltiCharge - before) * 100f:0.0} %");
                P.UltiCharge = 0.82f;
                yield return Seconds(0.2f);
                yield return Shot("ulti_" + id + "_00_charging");
                P.UltiCharge = 1f;
                yield return Seconds(0.5f);
                yield return Shot("ulti_" + id + "_01_ready");

                float hp0 = 0f;
                foreach (var m in crowd) hp0 += Mathf.Max(0f, m.Hp);
                Aim(new Vector2(id == "titan" ? 7.4f : 7f, id == "rio" ? 0.9f : 0.6f));
                GameInput.UltiPressed = true;
                yield return null;
                Debug.Log($"[Ulti] {id}: {P.Ulti} gestartet: {P.CurrentAction == Player.Action.Ulti}, Leiste jetzt {P.UltiCharge:0.00}");
                for (int i = 0; i < 16; i++)
                {
                    yield return Shot("ulti_" + id + "_" + (i + 2).ToString("00"));
                    yield return Frames(7);
                }
                yield return Seconds(1.2f);
                yield return Shot("ulti_" + id + "_20_after");
                float hp1 = 0f;
                int alive = 0;
                foreach (var m in crowd) { hp1 += Mathf.Max(0f, m.Alive ? m.Hp : 0f); if (m.Alive) alive++; }
                Debug.Log($"[Ulti] {id}: Schaden {hp0 - hp1:0} von {hp0:0}, noch am Leben {alive}/{crowd.Count}, Aktion {P.CurrentAction}, Ball {G.Ball.St}, Ball weg {P.UltiBallOut}, Leiste {P.UltiCharge:0.00}");
                Monster.Hold = false;
                G.Waves.Restart(999f);
                yield return Seconds(0.8f);
            }
        }
    }
}
