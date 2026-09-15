using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Color script: cool, misty night forest (teal/blue) so the warm player kit, the white ball
    /// and the magenta monsters pop — the same contrast trick Ori and Hollow Knight use.
    /// </summary>
    public static class Palette
    {
        static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        // Player kit
        public static readonly Color Skin = Hex("#D8976E");
        public static readonly Color SkinShade = Hex("#B5764F");
        public static readonly Color Jersey = Hex("#FF5A3D");
        public static readonly Color JerseyShade = Hex("#D8402A");
        public static readonly Color JerseyLight = Hex("#FF8B6B");
        public static readonly Color KitWhite = Hex("#F3F5F8");
        public static readonly Color KitWhiteShade = Hex("#C4CCD8");
        public static readonly Color Hair = Hex("#221816");
        public static readonly Color Boot = Hex("#1B2233");
        public static readonly Color Neon = Hex("#3DF2FF");
        public static readonly Color EyeDark = Hex("#1A1420");
        public static readonly Color BackLimbTint = new Color(0.60f, 0.64f, 0.78f, 1f);

        // Ball
        public static readonly Color BallWhite = Hex("#F6F8FB");
        public static readonly Color BallPanel = Hex("#1D2436");
        public static readonly Color BallSeam = Hex("#A6AFC0");

        // Effects
        public static readonly Color ShotCyan = Hex("#5CF0FF");
        public static readonly Color ShotCore = Hex("#E8FDFF");
        public static readonly Color Gold = Hex("#FFD66B");
        public static readonly Color HitWhite = Hex("#FFFFFF");
        public static readonly Color Hurt = Hex("#FF3B5C");

        // Monsters
        public static readonly Color MonsterTop = Hex("#6A45A8");
        public static readonly Color MonsterBottom = Hex("#2A1650");
        public static readonly Color MonsterGlow = Hex("#FF4FD8");
        public static readonly Color MonsterEye = Hex("#FFE98A");
        public static readonly Color WispTop = Hex("#3E5AC8");
        public static readonly Color WispBottom = Hex("#1A1F5C");
        public static readonly Color WispGlow = Hex("#7AA8FF");

        // Environment
        public static readonly Color SkyTop = Hex("#040914");
        public static readonly Color SkyMid = Hex("#0A1F33");
        public static readonly Color SkyHorizon = Hex("#1D5264");
        public static readonly Color Moon = Hex("#E4FBFF");
        public static readonly Color MoonGlow = Hex("#6FD2EC");
        public static readonly Color Fog = Hex("#78C9D4");
        public static readonly Color FarTop = Hex("#1B4B5D");
        public static readonly Color FarBottom = Hex("#2A6475");
        public static readonly Color MidTop = Hex("#0D2A3A");
        public static readonly Color MidBottom = Hex("#1A4A5A");
        public static readonly Color RuinTop = Hex("#0A2130");
        public static readonly Color RuinBottom = Hex("#133A4A");
        public static readonly Color RuinRim = Hex("#3E8A98");
        public static readonly Color Bush = Hex("#06151E");
        public static readonly Color Lantern = Hex("#FFC46B");
        public static readonly Color Firefly = Hex("#C8FF8A");
        public static readonly Color GrassEdge = Hex("#86F5CB");
        public static readonly Color PitchA = Hex("#2F8667");
        public static readonly Color PitchB = Hex("#246A51");
        public static readonly Color PitchBack = Hex("#1C5646");
        public static readonly Color EarthTop = Hex("#0C2427");
        public static readonly Color EarthBottom = Hex("#03090C");
        public static readonly Color Foreground = Hex("#02070A");

        // Vegetation (moonlit teal greens + bioluminescent accents)
        public static readonly Color FolDark = Hex("#0A2A2B");
        public static readonly Color FolMid = Hex("#1A5A4C");
        public static readonly Color FolLight = Hex("#3FA07F");
        public static readonly Color FolRim = Hex("#8CF2D2");
        public static readonly Color Fern = Hex("#1B6552");
        public static readonly Color FernLight = Hex("#46AE87");
        public static readonly Color Petal = Hex("#A8F6FF");
        public static readonly Color PetalWarm = Hex("#FFD7A8");
        public static readonly Color MushCap = Hex("#2FC4D8");
        public static readonly Color MushCapDark = Hex("#156E86");
        public static readonly Color MushStem = Hex("#CFE8E2");
        public static readonly Color Ivy = Hex("#18503F");
        public static readonly Color IvyLight = Hex("#35906A");
        public static readonly Color Moss = Hex("#2C6A58");
        public static readonly Color Banner = Hex("#A8473B");
        public static readonly Color BannerLight = Hex("#D26A55");
        public static readonly Color Crystal = Hex("#6FF0FF");
        public static readonly Color Bark = Hex("#10303D");
        public static readonly Color BarkLight = Hex("#2E6A78");
        public static readonly Color Stone = Hex("#16394A");
        public static readonly Color StoneLight = Hex("#2D6576");
        public static readonly Color Mortar = Hex("#0A1F2B");

        // UI
        public static readonly Color UiGlass = Hex("#0C1522");
        public static readonly Color UiRim = Hex("#FFFFFF");
        public static readonly Color UiText = Hex("#F2F6FA");
        public static readonly Color UiMuted = Hex("#8FA3B8");
        public static readonly Color HpA = Hex("#FF3D5E");
        public static readonly Color HpB = Hex("#FF7F50");
    }
}
