using UnityEngine;

namespace SoccerFight
{
    public enum CharacterClass { Striker, Defender, Skiller }

    /// <summary>
    /// Every colour the player art reads. Swapping a kit rebuilds the same body with a different
    /// strip, skin and hair — the skeleton and every animation stay exactly the same.
    /// </summary>
    public struct CharacterKit
    {
        public Color Jersey, JerseyShade, JerseyLight;
        public Color KitWhite, KitWhiteShade;
        public Color Skin, SkinShade, SkinLight;
        public Color Hair, HairLight;
        public Color Boot, BootLight, Neon;
        /// <summary>Length of the hair tuft (1 = the striker's short quiff).</summary>
        public float Tuft;
        public bool Headband;
    }

    public sealed class CharacterDef
    {
        public string Name, Role, Flavour;
        public CharacterClass Class;
        public CharacterKit Kit;
        public Color Accent;
        /// <summary>Card bars, 1..5. Flavour only for now — the classes play identically.</summary>
        public int Attack, Defence, Tech;
    }

    /// <summary>
    /// The three playable characters. They differ in looks only; the class bonuses come later, so
    /// nothing here touches PlayerStats.
    /// </summary>
    public static class Characters
    {
        static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        public static readonly CharacterDef[] All =
        {
            new CharacterDef
            {
                Name = "RIO", Role = "STÜRMER", Class = CharacterClass.Striker,
                Flavour = "Lebt vom Abschluss: sucht die Lücke und zieht ab.",
                Accent = Hex("#FF5A4A"), Attack = 5, Defence = 2, Tech = 3,
                Kit = new CharacterKit
                {
                    Jersey = Hex("#D6443A"), JerseyShade = Hex("#862439"), JerseyLight = Hex("#F07A5C"),
                    KitWhite = Hex("#ECE7DB"), KitWhiteShade = Hex("#98ACB5"),
                    Skin = Hex("#D9A07C"), SkinShade = Hex("#9E6A5C"), SkinLight = Hex("#F2C6A4"),
                    Hair = Hex("#1C1720"), HairLight = Hex("#545066"),
                    Boot = Hex("#151C28"), BootLight = Hex("#35465C"), Neon = Hex("#3DF2FF"),
                    Tuft = 1f, Headband = true
                }
            },
            new CharacterDef
            {
                Name = "BRUNO", Role = "VERTEIDIGER", Class = CharacterClass.Defender,
                Flavour = "Stellt sich dazwischen. Wer durch will, muss an ihm vorbei.",
                Accent = Hex("#5B8CFF"), Attack = 3, Defence = 5, Tech = 2,
                Kit = new CharacterKit
                {
                    Jersey = Hex("#2E4E82"), JerseyShade = Hex("#16274A"), JerseyLight = Hex("#5480BE"),
                    KitWhite = Hex("#DCE4EE"), KitWhiteShade = Hex("#7F91A8"),
                    Skin = Hex("#8C5C3E"), SkinShade = Hex("#5D3A28"), SkinLight = Hex("#B9855C"),
                    Hair = Hex("#17110F"), HairLight = Hex("#4B3B32"),
                    Boot = Hex("#1A1412"), BootLight = Hex("#48382E"), Neon = Hex("#FFB03A"),
                    Tuft = 0.45f, Headband = false
                }
            },
            new CharacterDef
            {
                Name = "MIRA", Role = "SKILLER", Class = CharacterClass.Skiller,
                Flavour = "Tricks statt Kraft: der Ball macht die Arbeit.",
                Accent = Hex("#C77DFF"), Attack = 2, Defence = 3, Tech = 5,
                Kit = new CharacterKit
                {
                    Jersey = Hex("#7B3FBF"), JerseyShade = Hex("#45226C"), JerseyLight = Hex("#AC77EC"),
                    KitWhite = Hex("#E8F2EC"), KitWhiteShade = Hex("#8CB3AB"),
                    Skin = Hex("#EFC6A4"), SkinShade = Hex("#C08C6E"), SkinLight = Hex("#FFE2C6"),
                    Hair = Hex("#2A1B33"), HairLight = Hex("#7A5E8C"),
                    Boot = Hex("#1C1726"), BootLight = Hex("#4A3D60"), Neon = Hex("#FF6FD5"),
                    Tuft = 1.75f, Headband = true
                }
            }
        };

        public static int Index { get; private set; }
        public static CharacterDef Current => All[Mathf.Clamp(Index, 0, All.Length - 1)];

        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { loaded = false; Index = 0; }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            Index = Mathf.Clamp(PlayerPrefs.GetInt("sf_char", 0), 0, All.Length - 1);
        }

        /// <summary>Picks a character and rebuilds the body art for it.</summary>
        public static void Select(int index)
        {
            Load();
            index = Mathf.Clamp(index, 0, All.Length - 1);
            if (index == Index) return;
            Index = index;
            PlayerPrefs.SetInt("sf_char", index);
            PlayerPrefs.Save();
            PlayerArt.Use(index);
            if (Game.I != null && Game.I.Player != null) Game.I.Player.Rig.ApplyLook();
        }
    }
}
