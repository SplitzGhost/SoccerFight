using System.Collections;
using UnityEngine;

namespace SoccerFight
{
    public sealed partial class CaptureDriver
    {
        IEnumerator CharacterMenuLoops()
        {
            SeedProfile();
            foreach (var def in Characters.All) Profile.GrantCharacter(def.Id);
            G.ToMenu();
            yield return Seconds(3f);
            foreach (var def in Characters.All)
            {
                Characters.Select(Characters.IndexOf(def));
                yield return Seconds(0.5f);
                yield return Kick("figure");
                // Video-Decodierung läuft in Echtzeit, die Capture-Zeit ist hingegen fest.
                float timeout = Time.realtimeSinceStartup + 15f;
                while (!G.Menu.CharacterVideoPlaying && Time.realtimeSinceStartup < timeout) yield return null;
                Require(G.Menu.CharacterVideoPlaying && G.Menu.CharacterLoopId == def.Id,"Video läuft für "+def.Id);
                yield return Seconds(1.2f);
                yield return Shot("v_"+def.Id);
                if (def.Id == "rio")
                {
                    timeout = Time.realtimeSinceStartup + 12f;
                    while (G.Menu.CharacterVideoLoops == 0 && Time.realtimeSinceStartup < timeout) yield return null;
                    Require(G.Menu.CharacterVideoLoops > 0,"RIO startet nahtlos die nächste Schleife");
                }
                yield return Kick("detail_back");
                yield return Seconds(1.3f);
                Require(!G.Menu.CharacterVideoPlaying,"Decoder stoppt außerhalb der Detailansicht");
            }
            Debug.Log("[Capture] sechs Videoloops, Wiederholung und Stop beim Verlassen bestanden");
        }
    }
}
