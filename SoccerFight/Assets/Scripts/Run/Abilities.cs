using UnityEngine;

namespace SoccerFight
{
    /// <summary>Player abilities. Shot + Power Shot are always there (the air-kick recoil always works); the rest come from the loadout equipped in the menu.</summary>
    public enum Ability { None, Shot, Power, Flick, Juggle, StepOver, Bicycle, AirKick, Tackle, Punt, Wall, Nutmeg, Decoy, Whistle, Header }

    public static class Abilities
    {
        public static readonly Ability[] Unlockable =
        {
            Ability.Flick, Ability.Juggle, Ability.StepOver, Ability.Bicycle,
            Ability.Tackle, Ability.Punt, Ability.Wall, Ability.Nutmeg, Ability.Decoy, Ability.Whistle, Ability.Header
        };

        public static string Name(Ability a)
        {
            switch (a)
            {
                case Ability.Shot: return "SCHUSS";
                case Ability.Power: return "POWER-SCHUSS";
                case Ability.Flick: return "RAINBOW FLICK";
                case Ability.Juggle: return "BALL HOCHHALTEN";
                case Ability.StepOver: return "ÜBERSTEIGER";
                case Ability.Bicycle: return "FALLRÜCKZIEHER";
                case Ability.AirKick: return "LUFT-RÜCKSTOSS";
                case Ability.Tackle: return "GRÄTSCHE";
                case Ability.Punt: return "ABSTOSS";
                case Ability.Wall: return "MAUER";
                case Ability.Nutmeg: return "TUNNEL";
                case Ability.Decoy: return "LOCKVOGEL";
                case Ability.Whistle: return "SCHLUSSPFIFF";
                case Ability.Header: return "KOPFBALL";
                default: return "";
            }
        }

        public static string Description(Ability a)
        {
            switch (a)
            {
                case Ability.Flick: return "Hebt den Ball in einem Regenbogen über die Gegner. Der Aufprall trifft alles in der Nähe.";
                case Ability.Juggle: return "Halte den Ball im Takt hoch. Jede Berührung heilt, perfekte Berührungen heilen doppelt.";
                case Ability.StepOver: return "Täuschung über den Ball, dann ein unverwundbarer Dash mitten durch die Gegner.";
                case Ability.Bicycle: return "Rückwärtssalto in der Luft: Der Ball explodiert beim Aufprall.";
                case Ability.AirKick: return "Schüsse in der Luft stoßen dich in die Gegenrichtung. Nach unten: ein zweiter Sprung.";
                case Ability.Tackle: return "Rutscht flach über den Rasen, wirft Gegner um und duckt sich unter Geschossen weg.";
                case Ability.Punt: return "Drischt den Ball in den Himmel. Er kommt als Meteor genau dort herunter, wohin du gezielt hast.";
                case Ability.Wall: return "Stellt eine Mauer aus drei Geister-Spielern auf: hält Geschosse und Gegner auf.";
                case Ability.Nutmeg: return "Spielt den Ball durch die Beine: Der Getunnelte taumelt und nimmt mehr Schaden.";
                case Ability.Decoy: return "Körpertäuschung zur Seite. Das Nachbild bindet die Gegner und platzt mit einem Stoß.";
                case Ability.Whistle: return "Aufgeladen durch Siege: Ein Pfiff friert alle Gegner ein und stoppt ihre Geschosse.";
                case Ability.Header: return "Lupft den Ball hoch und köpft ihn wuchtig aufs Ziel: betäubt den Getroffenen und springt zurück.";
                default: return "";
            }
        }


        public static Sprite Icon(Ability a)
        {
            switch (a)
            {
                case Ability.Power: return UiArt.IconPower;
                case Ability.Flick: return UiArt.IconFlick;
                case Ability.Juggle: return UiArt.IconJuggle;
                case Ability.StepOver: return UiArt.IconStepOver;
                case Ability.Bicycle: return UiArt.IconBicycle;
                case Ability.AirKick: return UiArt.IconAirKick;
                case Ability.Tackle: return UiArt.IconTackle;
                case Ability.Punt: return UiArt.IconPunt;
                case Ability.Wall: return UiArt.IconWall;
                case Ability.Nutmeg: return UiArt.IconNutmeg;
                case Ability.Decoy: return UiArt.IconDecoy;
                case Ability.Whistle: return UiArt.IconWhistle;
                case Ability.Header: return UiArt.IconHeader;
                default: return UiArt.IconShot;
            }
        }

        public static Color Accent(Ability a)
        {
            switch (a)
            {
                case Ability.Power: return Palette.PowerGold;
                case Ability.Flick: return Color.white;
                case Ability.Juggle: return Palette.Heal;
                case Ability.StepOver: return Palette.DashMint;
                case Ability.Bicycle: return Palette.BlastOrange;
                case Ability.AirKick: return Palette.ShotCyan;
                case Ability.Tackle: return Palette.Turf;
                case Ability.Punt: return Palette.Amber;
                case Ability.Wall: return Palette.Guard;
                case Ability.Nutmeg: return Palette.Showboat;
                case Ability.Decoy: return Palette.Trick;
                case Ability.Whistle: return Palette.Silver;
                case Ability.Header: return Palette.Header;
                default: return Palette.ShotCyan;
            }
        }
    }
}
