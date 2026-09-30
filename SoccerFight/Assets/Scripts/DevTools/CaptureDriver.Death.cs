using System.Collections;
using UnityEngine;

namespace SoccerFight
{
    /// <summary>Szenario „death“: das Tod-Menü beim Aufgehen, fertig aufgebaut und nach einem Lauf ohne Upgrades.</summary>
    public sealed partial class CaptureDriver
    {
        IEnumerator DeathTour()
        {
            G.Waves.Enabled = true;
            G.Restart();
            Aim(new Vector2(5f, 1.5f));
            yield return Seconds(1f);
            yield return Die(4, 2, 10, 137, 412f, "e01");
            G.Restart();
            yield return Seconds(1f);
            yield return Die(1, 1, 0, 3, 38f, "e02");
        }

        IEnumerator Die(int stage, int wave, int upgrades, int kills, float time, string shot)
        {
            G.Director.DebugJump(stage, wave, upgrades, false);
            G.Run.Kills = kills;
            G.Run.Time = time;
            CoinRewards.Note(kills * 3);
            P.Hp = 1f;
            P.InvulnTimer = 0f;
            P.Shield = 0;
            G.Run.Stats.Revives = 0;
            P.TakeDamage(50f, P.Pos + Vector2.right);
            yield return Seconds(1.25f);
            yield return Shot(shot + "_oeffnet");
            yield return Seconds(2.2f);
            yield return Shot(shot + "_tod_menue");
        }
    }
}
