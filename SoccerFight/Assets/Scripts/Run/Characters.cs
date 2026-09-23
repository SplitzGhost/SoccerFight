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

        // ---- basketball only
        /// <summary>Second trim colour (side panels, piping, the outline of the number).</summary>
        public Color Stripe;
        public HairStyle Hair2;
        /// <summary>Jersey number (0: none).</summary>
        public int Number;
        /// <summary>Compression sleeve on the throwing (near) arm.</summary>
        public bool Sleeve;
        public bool KneePads;
    }

    /// <summary>The sports of the roster. Every sport has the same three classes with its own moves.</summary>
    public enum Sport { Soccer, Basketball }

    /// <summary>How the head is drawn for the basketball bodies (soccer uses the kit's tuft).</summary>
    public enum HairStyle { Tuft, Fade, Buzz, Braids }

    /// <summary>
    /// Bone lengths of a body (world units). The soccer players share one build; the basketball
    /// players are taller and lankier, each with their own. Rig, art and menu figures all read it.
    /// </summary>
    public sealed class PlayerBody
    {
        public float ThighLen = PlayerDims.ThighLen, ShinLen = PlayerDims.ShinLen;
        public float UpperArmLen = PlayerDims.UpperArmLen, ForearmLen = PlayerDims.ForearmLen;
        /// <summary>Hip joint → shoulder joint along the torso.</summary>
        public float ShoulderY = 0.465f;
        /// <summary>Width of the chest and shoulders (1 = the soccer build).</summary>
        public float Build = 1f;

        public float StandHip => PlayerDims.AnkleHeight + ThighLen + ShinLen - 0.045f;
        public float NeckY => ShoulderY + 0.06f;
        /// <summary>Feet → top of the head, standing (name tags, portraits).</summary>
        public float HeadTop => StandHip + NeckY + 0.065f + 0.38f;

        public static readonly PlayerBody Soccer = new PlayerBody();

        public static PlayerBody Hoops(float thigh, float shin, float upper, float fore, float shoulder, float build)
            => new PlayerBody { ThighLen = thigh, ShinLen = shin, UpperArmLen = upper, ForearmLen = fore, ShoulderY = shoulder, Build = build };
    }

    public sealed class CharacterDef
    {
        /// <summary>Stable save key — never rename.</summary>
        public string Id;
        public string Name, Flavour;
        public CharacterClass Class;
        public Sport Sport = Sport.Soccer;
        public CharacterKit Kit;
        /// <summary>Bone lengths (the soccer build unless the character brings its own).</summary>
        public PlayerBody Body = PlayerBody.Soccer;
        public Color Accent;
        /// <summary>Can be the free first pick (every character of the base roster can).</summary>
        public bool Starter;
        /// <summary>Shop price in coins (a starter costs this once another starter was picked).</summary>
        public int CoinPrice;
        /// <summary>The personal passive on top of the class trait: every character has one, fitting the class.</summary>
        public PassiveDef Perk;
        /// <summary>Card bars, 1..5.</summary>
        public int Attack, Defence, Tech;

        public ClassDef ClassDef => Classes.Of(Class, Sport);
        public string Role => ClassDef.Name;
        public Price Cost => Price.Coins(CoinPrice);
    }

    /// <summary>
    /// The playable characters: one per class and sport. Order matters only for display (and the
    /// sprite cache index), so new ones are appended. Each belongs to a class (the class trait
    /// always applies) and carries a perk of its own that plays into that class; the first one is
    /// free, whichever sport it plays.
    /// </summary>
    public static class Characters
    {
        static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        static CharacterKit Hoops(CharacterKit k, string stripe, HairStyle hair, int number, bool sleeve = false, bool kneePads = false)
        {
            k.Stripe = Hex(stripe);
            k.Hair2 = hair;
            k.Number = number;
            k.Sleeve = sleeve;
            k.KneePads = kneePads;
            return k;
        }

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
            // ---- soccer
            new CharacterDef
            {
                Id = "rio", Name = "RIO", Class = CharacterClass.Striker, Starter = true, CoinPrice = 500,
                Flavour = "Lebt vom Abschluss: sucht die Lücke und zieht ab.",
                Accent = Hex("#FF5A4A"), Attack = 5, Defence = 2, Tech = 3,
                Kit = Kit("#D6443A", "#862439", "#F07A5C", "#ECE7DB", "#98ACB5", "#D9A07C", "#9E6A5C", "#F2C6A4",
                          "#1C1720", "#545066", "#151C28", "#35465C", "#3DF2FF", 1f, true),
                Perk = Perk("rio", "TORRIECHER", "Normale Schüsse: +15 % Schaden, 10 % schneller bereit.",
                    (s, n) => { s.ShotDamageMul += 0.15f; s.ShotCooldownMul *= 0.9f; }),
            },
            new CharacterDef
            {
                Id = "bruno", Name = "BRUNO", Class = CharacterClass.Defender, Starter = true, CoinPrice = 500,
                Flavour = "Stellt sich dazwischen. Wer durch will, muss an ihm vorbei.",
                Accent = Hex("#5B8CFF"), Attack = 3, Defence = 5, Tech = 2,
                Kit = Kit("#2E4E82", "#16274A", "#5480BE", "#DCE4EE", "#7F91A8", "#8C5C3E", "#5D3A28", "#B9855C",
                          "#17110F", "#4B3B32", "#1A1412", "#48382E", "#FFB03A", 0.45f, false),
                Perk = Perk("bruno", "SCHUTZWALL", "Ein Schild blockt einen Treffer und lädt alle 20 s neu.",
                    (s, n) => { s.ShieldCharges = System.Math.Max(s.ShieldCharges, 1); s.ShieldRecharge = 20f; }),
            },
            new CharacterDef
            {
                Id = "mira", Name = "MIRA", Class = CharacterClass.Skiller, Starter = true, CoinPrice = 500,
                Flavour = "Tricks statt Kraft: der Ball macht die Arbeit.",
                Accent = Hex("#C77DFF"), Attack = 2, Defence = 3, Tech = 5,
                Kit = Kit("#7B3FBF", "#45226C", "#AC77EC", "#E8F2EC", "#8CB3AB", "#EFC6A4", "#C08C6E", "#FFE2C6",
                          "#2A1B33", "#7A5E8C", "#1C1726", "#4A3D60", "#FF6FD5", 1.75f, true),
                Perk = Perk("mira", "BALLZAUBER", "Antritt: 25 % weiter, lädt 20 % schneller, trifft Gegner.",
                    (s, n) => { s.DashDistanceMul += 0.25f; s.DashCooldownMul *= 0.8f; s.DashDamageFrac = System.Math.Max(s.DashDamageFrac, 0.4f); }),
            },

            // ---- basketball: the same three classes, taller and lankier bodies, their own moves
            new CharacterDef
            {
                Id = "dre", Name = "DRE", Sport = Sport.Basketball, Class = CharacterClass.Striker, Starter = true, CoinPrice = 500,
                Flavour = "Trifft von überall. Je weiter weg, desto kälter bleibt er.",
                Accent = Hex("#FF6A3D"), Attack = 5, Defence = 2, Tech = 3,
                Kit = Hoops(Kit("#C9352C", "#7A1B24", "#EE6B4E", "#F3EEE4", "#A9A39A", "#8A5A3C", "#5C3826", "#B98460",
                          "#141118", "#46404F", "#1B1A20", "#403E4A", "#FF5A3A", 0.5f, true), "#17151B", HairStyle.Fade, 3, sleeve: true),
                Body = PlayerBody.Hoops(0.46f, 0.47f, 0.31f, 0.29f, 0.5f, 1f),
                Perk = Perk("dre", "SPLASH", "Dreier: +20 % Schaden, lädt 15 % schneller.",
                    (s, n) => { s.ThreeDamageMul += 0.2f; s.ThreeCooldownMul *= 0.85f; }),
            },
            new CharacterDef
            {
                Id = "titan", Name = "TITAN", Sport = Sport.Basketball, Class = CharacterClass.Defender, Starter = true, CoinPrice = 500,
                Flavour = "Unter dem Korb gehört alles ihm. Wer zu nah kommt, fliegt.",
                Accent = Hex("#E7B43A"), Attack = 3, Defence = 5, Tech = 2,
                Kit = Hoops(Kit("#233A70", "#111D3E", "#4263A6", "#F2EBDD", "#A69F92", "#6A4430", "#41291C", "#98684A",
                          "#100C0C", "#3A302C", "#16181F", "#3A4150", "#E9B640", 0.3f, false), "#E9B640", HairStyle.Buzz, 34, kneePads: true),
                Body = PlayerBody.Hoops(0.49f, 0.49f, 0.32f, 0.3f, 0.53f, 1.2f),
                Perk = Perk("titan", "RIM PROTECTOR", "+25 maximales Leben, Dunk-Druckwellen 20 % größer.",
                    (s, n) => { s.MaxHpBonus += 25f; s.DunkWaveMul += 0.2f; }),
            },
            new CharacterDef
            {
                Id = "nova", Name = "NOVA", Sport = Sport.Basketball, Class = CharacterClass.Skiller, Starter = true, CoinPrice = 500,
                Flavour = "Streetball-Legende: Der Ball klebt an ihrer Hand, die Gegner an ihren Fersen.",
                Accent = Hex("#2FD6C8"), Attack = 2, Defence = 3, Tech = 5,
                Kit = Hoops(Kit("#17A39B", "#0A5B5A", "#4FD3C5", "#F6EEF4", "#B7A4B4", "#D9A57E", "#A77558", "#F4CCA6",
                          "#2A1A1E", "#6E4A56", "#D8398F", "#FF86C4", "#2FD6C8", 1.6f, false), "#E0409A", HairStyle.Braids, 11),
                Body = PlayerBody.Hoops(0.45f, 0.45f, 0.3f, 0.28f, 0.49f, 0.94f),
                Perk = Perk("nova", "HANDLES", "Crossover-Boost hält 1 s länger, +8 % Tempo.",
                    (s, n) => { s.CrossTimeBonus += 1f; s.MoveSpeedMul += 0.08f; }),
            },
        };

        static readonly Dictionary<string, CharacterDef> byId = new Dictionary<string, CharacterDef>();

        static Characters()
        {
            foreach (var c in All) byId[c.Id] = c;
        }

        public static CharacterDef Get(string id) => id != null && byId.TryGetValue(id, out var c) ? c : null;

        public static int IndexOf(CharacterDef def) => System.Array.IndexOf(All, def);

        public static IEnumerable<CharacterDef> OfSport(Sport sport)
        {
            foreach (var c in All) if (c.Sport == sport) yield return c;
        }

        /// <summary>The sport's player of a class (null when the sport has none yet).</summary>
        public static CharacterDef Of(Sport sport, CharacterClass cls)
        {
            foreach (var c in All) if (c.Sport == sport && c.Class == cls) return c;
            return null;
        }

        /// <summary>The sports that have players, in display order.</summary>
        public static readonly Sport[] Sports = { Sport.Soccer, Sport.Basketball };

        public static string SportName(Sport s) => s == Sport.Basketball ? "BASKETBALL" : "FUSSBALL";

        /// <summary>Characters sold in the shop before the sport update, with what they cost (refunded once, see Profile).</summary>
        public static readonly Dictionary<string, int> Retired = new Dictionary<string, int>
        {
            { "kai", 750 }, { "zara", 1100 }, { "ivo", 750 }, { "tala", 1100 }, { "luna", 750 }, { "nico", 1100 },
        };

        /// <summary>The free choices of a new player: any one of the base roster.</summary>
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
