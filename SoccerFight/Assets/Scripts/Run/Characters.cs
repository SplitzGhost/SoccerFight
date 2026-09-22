using System.Collections.Generic;
using UnityEngine;

namespace SoccerFight
{
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
        /// <summary>Stable save key — never rename.</summary>
        public string Id;
        public string Name, Flavour;
        public CharacterClass Class;
        public CharacterKit Kit;
        public Color Accent;
        /// <summary>One of the three free starters (one per class).</summary>
        public bool Starter;
        /// <summary>Shop price in coins (a starter costs this once another starter was picked).</summary>
        public int CoinPrice;
        /// <summary>A small personal passive on top of the class trait (null: none).</summary>
        public PassiveDef Perk;
        /// <summary>Card bars, 1..5.</summary>
        public int Attack, Defence, Tech;

        public ClassDef ClassDef => Classes.Of(Class);
        public string Role => ClassDef.Name;
        public Price Cost => Price.Coins(CoinPrice);
    }

    /// <summary>
    /// The playable characters. Order matters only for display (and the sprite cache index), so new
    /// characters are appended. Each belongs to a class (the class trait always applies) and may
    /// carry a small perk of its own; the three starters are free on the first launch.
    /// </summary>
    public static class Characters
    {
        static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        static CharacterKit Kit(string jersey, string jShade, string jLight, string white, string whiteShade,
            string skin, string sShade, string sLight, string hair, string hLight, string boot, string bLight, string neon, float tuft, bool band)
            => new CharacterKit
            {
                Jersey = Hex(jersey), JerseyShade = Hex(jShade), JerseyLight = Hex(jLight),
                KitWhite = Hex(white), KitWhiteShade = Hex(whiteShade),
                Skin = Hex(skin), SkinShade = Hex(sShade), SkinLight = Hex(sLight),
                Hair = Hex(hair), HairLight = Hex(hLight),
                Boot = Hex(boot), BootLight = Hex(bLight), Neon = Hex(neon),
                Tuft = tuft, Headband = band,
            };

        static PassiveDef Perk(string id, string name, string text, System.Action<PlayerStats, int> apply)
            => new PassiveDef { Id = "perk_" + id, Name = name, Text = text, Apply = apply };

        public static readonly CharacterDef[] All =
        {
            // ---- the three starters: one per class, no perk, the first one is free
            new CharacterDef
            {
                Id = "rio", Name = "RIO", Class = CharacterClass.Striker, Starter = true, CoinPrice = 500,
                Flavour = "Lebt vom Abschluss: sucht die Lücke und zieht ab.",
                Accent = Hex("#FF5A4A"), Attack = 5, Defence = 2, Tech = 3,
                Kit = Kit("#D6443A", "#862439", "#F07A5C", "#ECE7DB", "#98ACB5", "#D9A07C", "#9E6A5C", "#F2C6A4",
                          "#1C1720", "#545066", "#151C28", "#35465C", "#3DF2FF", 1f, true),
            },
            new CharacterDef
            {
                Id = "bruno", Name = "BRUNO", Class = CharacterClass.Defender, Starter = true, CoinPrice = 500,
                Flavour = "Stellt sich dazwischen. Wer durch will, muss an ihm vorbei.",
                Accent = Hex("#5B8CFF"), Attack = 3, Defence = 5, Tech = 2,
                Kit = Kit("#2E4E82", "#16274A", "#5480BE", "#DCE4EE", "#7F91A8", "#8C5C3E", "#5D3A28", "#B9855C",
                          "#17110F", "#4B3B32", "#1A1412", "#48382E", "#FFB03A", 0.45f, false),
            },
            new CharacterDef
            {
                Id = "mira", Name = "MIRA", Class = CharacterClass.Skiller, Starter = true, CoinPrice = 500,
                Flavour = "Tricks statt Kraft: der Ball macht die Arbeit.",
                Accent = Hex("#C77DFF"), Attack = 2, Defence = 3, Tech = 5,
                Kit = Kit("#7B3FBF", "#45226C", "#AC77EC", "#E8F2EC", "#8CB3AB", "#EFC6A4", "#C08C6E", "#FFE2C6",
                          "#2A1B33", "#7A5E8C", "#1C1726", "#4A3D60", "#FF6FD5", 1.75f, true),
            },

            // ---- shop characters: two per class, each with a small perk
            new CharacterDef
            {
                Id = "kai", Name = "KAI", Class = CharacterClass.Striker, CoinPrice = 750,
                Flavour = "Kommt aus dem Nichts und trifft, bevor der Gegner reagiert.",
                Accent = Hex("#FF9A3D"), Attack = 5, Defence = 2, Tech = 4,
                Kit = Kit("#E0782A", "#8A3A1C", "#F7A45C", "#2C313B", "#161A21", "#C68A63", "#8C5A43", "#E8B08A",
                          "#2B1A12", "#6B4A38", "#1B1B22", "#43434F", "#FFD23F", 1.3f, false),
                Perk = Perk("kai", "KALTSCHNÄUZIG", "+8 % Krit-Chance.", (s, n) => s.CritChance += 0.08f),
            },
            new CharacterDef
            {
                Id = "zara", Name = "ZARA", Class = CharacterClass.Striker, CoinPrice = 1100,
                Flavour = "Ihr Schuss ist berüchtigt: Wer ihn blockt, spürt ihn tagelang.",
                Accent = Hex("#FFD166"), Attack = 5, Defence = 3, Tech = 2,
                Kit = Kit("#D9A634", "#8C6419", "#F5D06E", "#F4F1E8", "#A8A294", "#7A4B33", "#4E2E20", "#A56E4E",
                          "#120C0A", "#463630", "#2A1E12", "#5C4632", "#FF4F6D", 1.9f, true),
                Perk = Perk("zara", "HAMMERSCHUSS", "Schnellere Bälle, Power-Schuss +15 %.",
                    (s, n) => { s.BallSpeedMul += 0.12f; s.PowerDamageMul += 0.15f; }),
            },
            new CharacterDef
            {
                Id = "ivo", Name = "IVO", Class = CharacterClass.Defender, CoinPrice = 750,
                Flavour = "Ein Fels im Strafraum. Steht einfach immer richtig.",
                Accent = Hex("#57C27E"), Attack = 2, Defence = 5, Tech = 2,
                Kit = Kit("#2F7D4E", "#174A2D", "#56A874", "#EDEFE6", "#9AA79B", "#E3B592", "#A9795F", "#F7D2B4",
                          "#8A5A2B", "#C48A4E", "#18201A", "#3E5044", "#C6FF4A", 0.6f, false),
                Perk = Perk("ivo", "EISENWADE", "+20 maximales Leben.", (s, n) => s.MaxHpBonus += 20f),
            },
            new CharacterDef
            {
                Id = "tala", Name = "TALA", Class = CharacterClass.Defender, CoinPrice = 1100,
                Flavour = "Räumt hinten auf — wer ihr zu nahe kommt, fliegt zurück.",
                Accent = Hex("#3FD0D4"), Attack = 3, Defence = 5, Tech = 3,
                Kit = Kit("#1E8C92", "#0D4C52", "#4CC0C4", "#1F2632", "#10151D", "#B77B55", "#7E4F37", "#D9A07A",
                          "#1A1016", "#4E3844", "#232A36", "#4A566B", "#FF8A3D", 1.5f, true),
                Perk = Perk("tala", "AUSPUTZER", "Getroffen? Eine Schockwelle stößt zurück.",
                    (s, n) => s.CounterStomp += 25f),
            },
            new CharacterDef
            {
                Id = "luna", Name = "LUNA", Class = CharacterClass.Skiller, CoinPrice = 750,
                Flavour = "Tänzelt durch jede Abwehr, als wäre alles nur ein Spiel.",
                Accent = Hex("#FF7EB6"), Attack = 2, Defence = 2, Tech = 5,
                Kit = Kit("#E0568F", "#8E2A5A", "#F58DB8", "#F6EEF2", "#B69CAA", "#F2CFB3", "#C89C82", "#FFE6D2",
                          "#D9B36A", "#FFF0C0", "#2A1D2A", "#5A445A", "#7CF0FF", 1.6f, false),
                Perk = Perk("luna", "LEICHTFUSS", "+8 % Tempo, +6 % Sprunghöhe.",
                    (s, n) => { s.MoveSpeedMul += 0.08f; s.JumpMul += 0.06f; }),
            },
            new CharacterDef
            {
                Id = "nico", Name = "NICO", Class = CharacterClass.Skiller, CoinPrice = 1100,
                Flavour = "Straßenfußballer mit tausend Tricks und null Pause.",
                Accent = Hex("#B7E34F"), Attack = 3, Defence = 2, Tech = 5,
                Kit = Kit("#9CCB3B", "#5A7D1C", "#C4E86E", "#2B3038", "#16191F", "#9A6444", "#683F2A", "#C28A62",
                          "#0F0F14", "#3E3E52", "#101218", "#353A48", "#B06BFF", 1.1f, true),
                Perk = Perk("nico", "ZAUBERFUSS", "Alle Abklingzeiten −10 %.", (s, n) => s.CooldownMul *= 0.9f),
            },
        };

        static readonly Dictionary<string, CharacterDef> byId = new Dictionary<string, CharacterDef>();

        static Characters()
        {
            foreach (var c in All) byId[c.Id] = c;
        }

        public static CharacterDef Get(string id) => id != null && byId.TryGetValue(id, out var c) ? c : null;

        public static int IndexOf(CharacterDef def) => System.Array.IndexOf(All, def);

        /// <summary>The free choices of a new player, one per class.</summary>
        public static IEnumerable<CharacterDef> Starters
        {
            get { foreach (var c in All) if (c.Starter) yield return c; }
        }

        public static IEnumerable<CharacterDef> OfClass(CharacterClass cls)
        {
            foreach (var c in All) if (c.Class == cls) yield return c;
        }

        public static int Index { get; private set; }
        public static CharacterDef Current => All[Mathf.Clamp(Index, 0, All.Length - 1)];

        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { loaded = false; Index = 0; }

        /// <summary>Reads the selected character from the profile (the first starter until one is chosen).</summary>
        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            var def = Get(Profile.SelectedCharacter);
            Index = def != null ? IndexOf(def) : 0;
        }

        /// <summary>Re-reads the selection after the profile was replaced (developer reset).</summary>
        public static void Reload()
        {
            loaded = false;
            int before = Index;
            Load();
            if (Index == before) return;
            PlayerArt.Use(Index);
            if (Game.I != null && Game.I.Player != null) Game.I.Player.Rig.ApplyLook();
        }

        /// <summary>Picks an owned character and rebuilds the body art for it.</summary>
        public static bool Select(int index)
        {
            Load();
            index = Mathf.Clamp(index, 0, All.Length - 1);
            var def = All[index];
            if (!Profile.OwnsCharacter(def.Id)) return false;
            Profile.SelectCharacter(def.Id);
            Profile.Save();
            if (index == Index) return true;
            Index = index;
            PlayerArt.Use(index);
            if (Game.I != null && Game.I.Player != null) Game.I.Player.Rig.ApplyLook();
            return true;
        }

        /// <summary>Shows a character without owning it (onboarding preview, captures).</summary>
        public static void Preview(int index)
        {
            Load();
            index = Mathf.Clamp(index, 0, All.Length - 1);
            if (index == Index) return;
            Index = index;
            PlayerArt.Use(index);
            if (Game.I != null && Game.I.Player != null) Game.I.Player.Rig.ApplyLook();
        }
    }
}
