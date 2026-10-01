using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Player abilities. The shot and the class move on the right mouse button are always there, the
    /// air-kick recoil always works; up to four more are picked after boss fights. Soccer and
    /// basketball share the shot (a kick or a throw) and the air recoil, everything else belongs to
    /// one sport.
    /// </summary>
    public enum Ability
    {
        None, Shot, Power, Flick, Juggle, StepOver, Bicycle, AirKick, Tackle, Punt, Wall, Nutmeg, Decoy, Whistle, Header, Dash,
        // basketball: the three class moves, then the boss skills
        Three, Crossover, Dunk, AlleyOop, Block, FastBreak, StepBack, PumpFake,
        // boxing: the three class moves, then the boss skills
        PowerCross, Guard, Slip, Uppercut, Hooks, Pound, Shadow, Flurry,
    }

    public static class Abilities
    {
        public static readonly Ability[] Unlockable =
        {
            Ability.Flick, Ability.Juggle, Ability.StepOver, Ability.Bicycle,
            Ability.Tackle, Ability.Punt, Ability.Wall, Ability.Nutmeg, Ability.Decoy, Ability.Whistle
        };

        public static readonly Ability[] HoopsUnlockable = { Ability.AlleyOop, Ability.Block, Ability.FastBreak, Ability.StepBack, Ability.PumpFake };

        public static readonly Ability[] BoxUnlockable = { Ability.Uppercut, Ability.Hooks, Ability.Pound, Ability.Shadow, Ability.Flurry };

        public static Ability[] UnlockableFor(Sport s) => s == Sport.Basketball ? HoopsUnlockable : s == Sport.Boxing ? BoxUnlockable : Unlockable;

        /// <summary>The right-mouse-button move of a class (never in one of the four skill slots).</summary>
        public static bool IsClassMove(Ability a) => a == Ability.Power || a == Ability.Dash || a == Ability.Header
                                                     || a == Ability.Three || a == Ability.Crossover || a == Ability.Dunk
                                                     || a == Ability.PowerCross || a == Ability.Guard || a == Ability.Slip;

        /// <summary>The sport of the local player (names of the shared moves follow it).</summary>
        static bool Hoops => Characters.Current.Sport == Sport.Basketball;
        static bool Boxing => Characters.Current.Sport == Sport.Boxing;

        public static string Name(Ability a)
        {
            switch (a)
            {
                case Ability.Shot: return Hoops ? "WURF" : Boxing ? "SCHLAG" : "SCHUSS";
                case Ability.Power: return "POWER-SCHUSS";
                case Ability.Flick: return "RAINBOW FLICK";
                case Ability.Juggle: return "BALL HOCHHALTEN";
                case Ability.StepOver: return "ÜBERSTEIGER";
                case Ability.Bicycle: return "FALLRÜCKZIEHER";
                case Ability.AirKick: return Hoops ? "BODENPASS" : Boxing ? "LUFTSCHLAG" : "LUFT-RÜCKSTOSS";
                case Ability.Tackle: return "GRÄTSCHE";
                case Ability.Punt: return "ABSTOSS";
                case Ability.Wall: return "MAUER";
                case Ability.Nutmeg: return "TUNNEL";
                case Ability.Decoy: return "LOCKVOGEL";
                case Ability.Whistle: return "SCHLUSSPFIFF";
                case Ability.Header: return "KOPFBALL";
                case Ability.Dash: return "ANTRITT";
                case Ability.Three: return "DREIER";
                case Ability.Crossover: return "CROSSOVER";
                case Ability.Dunk: return "DUNK";
                case Ability.AlleyOop: return "ALLEY-OOP";
                case Ability.Block: return "BLOCK";
                case Ability.FastBreak: return "FASTBREAK";
                case Ability.StepBack: return "STEPBACK";
                case Ability.PumpFake: return "PUMP FAKE";
                case Ability.PowerCross: return "KRAFTGERADE";
                case Ability.Guard: return "DECKUNG";
                case Ability.Slip: return "KONTERSCHRITT";
                case Ability.Uppercut: return "UPPERCUT";
                case Ability.Hooks: return "DOPPELHAKEN";
                case Ability.Pound: return "BODENSCHLAG";
                case Ability.Shadow: return "SCHATTENBOXER";
                case Ability.Flurry: return "TROMMELFEUER";
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
                case Ability.Shot: return !Boxing ? "" : "Links, rechts, Haken: Jeder Schlag schickt eine kurze Druckwelle aus dem Handschuh, der dritte in Folge trifft am härtesten.";
                case Ability.AirKick when Boxing: return "Schläge in der Luft stoßen dich in die Gegenrichtung. Nach unten: ein zweiter Sprung.";
                case Ability.AirKick: return Hoops
                    ? "Würfe in der Luft stoßen dich in die Gegenrichtung. Nach unten: ein zweiter Sprung."
                    : "Schüsse in der Luft stoßen dich in die Gegenrichtung. Nach unten: ein zweiter Sprung.";
                case Ability.Tackle: return "Rutscht flach über den Rasen, wirft Gegner um und duckt sich unter Geschossen weg.";
                case Ability.Punt: return "Drischt den Ball in den Himmel. Er kommt als Meteor genau dort herunter, wohin du gezielt hast.";
                case Ability.Wall: return "Stellt eine Mauer aus drei Geister-Spielern auf: hält Geschosse und Gegner auf.";
                case Ability.Nutmeg: return "Spielt den Ball durch die Beine: Der Getunnelte taumelt und nimmt mehr Schaden.";
                case Ability.Decoy: return "Körpertäuschung zur Seite. Das Nachbild bindet die Gegner und platzt mit einem Stoß.";
                case Ability.Whistle: return "Aufgeladen durch Siege: Ein Pfiff friert alle Gegner ein und stoppt ihre Geschosse.";
                case Ability.Header: return "Lupft den Ball hoch und köpft ihn wuchtig aufs Ziel: betäubt den Getroffenen und springt zurück.";
                case Ability.Power: return "Langer Anlauf, dann ein Strahl von einem Schuss: fliegt durch jeden Gegner in seiner Bahn.";
                case Ability.Dash: return "Blitzschneller Antritt in Laufrichtung, auch in der Luft: kurz unverwundbar, der Ball bleibt am Fuß.";
                case Ability.Three: return "Step-back, dann ein Wurf im hohen Bogen genau aufs Fadenkreuz. Dort schlägt er mit einer kleinen Explosion ein.";
                case Ability.Crossover: return "Zweimal blitzschnell durch die Beine: danach kurz schneller, und ein paar Sekunden lang fliegen alle Würfe durch Gegner hindurch.";
                case Ability.Dunk: return "Springt zum Fadenkreuz und hämmert den Ball in den Boden: Druckwellen in alle Richtungen werfen die Gegner zurück.";
                case Ability.AlleyOop: return "Wirft den Ball steil in die Luft. Oben hängt er einen Moment, dann kracht er auf den nächsten Gegner.";
                case Ability.Block: return "Springt mit hochgerissenen Armen: kurz unverwundbar, Gegner weichen zurück, jedes Geschoss fliegt zurück auf die Monster.";
                case Ability.FastBreak: return "Unverwundbarer Sprint nach vorn mit dem Ball: Gegner auf dem Weg werden getroffen und kurz betäubt.";
                case Ability.StepBack: return "Blitzschneller Satz nach hinten, Angriffe gehen ins Leere. Dein nächster Wurf trifft garantiert kritisch.";
                case Ability.PowerCross: return "Ausholen, ein Schritt nach vorn, dann eine Gerade mit allem: Die Druckwelle fliegt durch jeden Gegner in ihrer Bahn.";
                case Ability.Guard: return "Die Fäuste hoch: Solange die Deckung steht, kommt nichts durch. Jeder abgefangene Treffer lädt den Konterschlag am Ende auf.";
                case Ability.Slip: return "Blitzschneller Schritt in Laufrichtung, auch in der Luft: kurz unverwundbar. Der nächste Schlag ist ein sicherer Konter: kritisch und durchschlagend.";
                case Ability.Uppercut: return "Aus der Hocke nach oben: Der Aufwärtshaken schleudert die Gegner vor dir in die Luft, die Druckwelle steigt bis zu den Fliegern.";
                case Ability.Hooks: return "Zwei Haken links und rechts aus der Drehung: Eine Ringwelle trifft alles um dich herum und stößt es weg.";
                case Ability.Pound: return "Beide Fäuste in den Boden: Eine Bebenwelle rollt nach beiden Seiten und betäubt jeden Gegner, den sie erreicht.";
                case Ability.Shadow: return "Ein Schatten von dir bleibt stehen: Die Gegner halten ihn für dich, und er boxt jeden, der ihm zu nahe kommt.";
                case Ability.Flurry: return "Acht blitzschnelle Schläge aufs Fadenkreuz: ein Hagel aus Druckwellen, jede etwas schwächer als ein normaler Schlag.";
                case Ability.PumpFake: return "Wurf angetäuscht: Ein Geisterball fliegt zum Fadenkreuz, die Gegner in der Nähe springen darauf herein, laufen ihm nach und nehmen mehr Schaden.";
                default: return "";
            }
        }


        public static Sprite Icon(Ability a)
        {
            switch (a)
            {
                case Ability.Shot: return Hoops ? UiArt.IconThrow : Boxing ? UiArt.IconPunch : UiArt.IconShot;
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
                case Ability.Dash: return UiArt.IconDash;
                case Ability.Three: return UiArt.IconThree;
                case Ability.Crossover: return UiArt.IconCrossover;
                case Ability.Dunk: return UiArt.IconDunk;
                case Ability.AlleyOop: return UiArt.IconAlleyOop;
                case Ability.Block: return UiArt.IconBlock;
                case Ability.FastBreak: return UiArt.IconFastBreak;
                case Ability.StepBack: return UiArt.IconStepBack;
                case Ability.PumpFake: return UiArt.IconPumpFake;
                case Ability.PowerCross: return UiArt.IconCross;
                case Ability.Guard: return UiArt.IconGuard;
                case Ability.Slip: return UiArt.IconSlip;
                case Ability.Uppercut: return UiArt.IconUppercut;
                case Ability.Hooks: return UiArt.IconHooks;
                case Ability.Pound: return UiArt.IconPound;
                case Ability.Shadow: return UiArt.IconShadow;
                case Ability.Flurry: return UiArt.IconFlurry;
                default: return UiArt.IconShot;
            }
        }

        public static Color Accent(Ability a)
        {
            switch (a)
            {
                case Ability.Shot: return Hoops ? Palette.HoopOrange : Boxing ? Palette.Punch : Palette.ShotCyan;
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
                case Ability.Dash: return Palette.Trick;
                case Ability.Three: return Palette.HoopOrange;
                case Ability.Crossover: return Palette.Trick;
                case Ability.Dunk: return Palette.Slam;
                case Ability.AlleyOop: return Palette.Oop;
                case Ability.Block: return Palette.Guard;
                case Ability.FastBreak: return Palette.DashMint;
                case Ability.StepBack: return Palette.Swish;
                case Ability.PumpFake: return Palette.Showboat;
                case Ability.PowerCross: return Palette.Cross;
                case Ability.Guard: return Palette.Counter;
                case Ability.Slip: return Palette.Slip;
                case Ability.Uppercut: return Palette.Upper;
                case Ability.Hooks: return Palette.Trick;
                case Ability.Pound: return Palette.Slam;
                case Ability.Shadow: return Palette.Shadow;
                case Ability.Flurry: return Palette.Punch;
                default: return Palette.ShotCyan;
            }
        }
    }
}
