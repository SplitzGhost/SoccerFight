using UnityEngine;

namespace SoccerFight
{
    /// <summary>Player abilities. A run starts with Shot + Power Shot (the air-kick recoil always works); the rest are unlocked one per stage.</summary>
    public enum Ability { None, Shot, Power, Flick, Juggle, StepOver, Bicycle, AirKick }

    public static class Abilities
    {
        public static readonly Ability[] Unlockable = { Ability.Flick, Ability.Juggle, Ability.StepOver, Ability.Bicycle };

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
                default: return "";
            }
        }

        public static GameAction Action(Ability a)
        {
            switch (a)
            {
                case Ability.Power: return GameAction.PowerShot;
                case Ability.Flick: return GameAction.Flick;
                case Ability.Juggle: return GameAction.Juggle;
                case Ability.StepOver: return GameAction.StepOver;
                case Ability.Bicycle: return GameAction.Bicycle;
                default: return GameAction.Shoot;   // air kick rides on the normal shot
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
                default: return Palette.ShotCyan;
            }
        }
    }
}
