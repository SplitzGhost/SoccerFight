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

        // Player kit: deep crimson + warm cream, shadows shifted towards the scene's teal so the
        // character reads clearly against the night without looking pasted on
        public static readonly Color Skin = Hex("#D9A07C");
        public static readonly Color SkinShade = Hex("#9E6A5C");
        public static readonly Color SkinLight = Hex("#F2C6A4");
        public static readonly Color Jersey = Hex("#D6443A");
        public static readonly Color JerseyShade = Hex("#862439");
        public static readonly Color JerseyLight = Hex("#F07A5C");
        public static readonly Color KitWhite = Hex("#ECE7DB");
        public static readonly Color KitWhiteShade = Hex("#98ACB5");
        public static readonly Color Hair = Hex("#1C1720");
        public static readonly Color HairLight = Hex("#545066");
        public static readonly Color Boot = Hex("#151C28");
        public static readonly Color BootLight = Hex("#35465C");
        public static readonly Color Neon = Hex("#3DF2FF");
        public static readonly Color EyeDark = Hex("#1A1420");
        public static readonly Color PlayerLine = Hex("#0B1620");
        public static readonly Color MoonRim = Hex("#BDF3FF");
        public static readonly Color BackLimbTint = new Color(0.56f, 0.65f, 0.73f, 1f);
        public static readonly Color Heal = Hex("#7CFFB8");

        // Ball
        public static readonly Color BallWhite = Hex("#F6F8FB");
        public static readonly Color BallPanel = Hex("#1D2436");
        public static readonly Color BallSeam = Hex("#A6AFC0");

        // Effects
        public static readonly Color ShotCyan = Hex("#5CF0FF");
        public static readonly Color ShotCore = Hex("#E8FDFF");
        public static readonly Color Gold = Hex("#FFD66B");
        public static readonly Color PowerGold = Hex("#FFC24A");
        public static readonly Color DashMint = Hex("#8CFFD9");
        public static readonly Color BlastOrange = Hex("#FF8440");
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

        // Skill accents (one hue per ability so the bar reads at a glance)
        public static readonly Color Turf = Hex("#9BE564");        // Grätsche
        public static readonly Color Amber = Hex("#FFB03A");       // Abstoß
        public static readonly Color Guard = Hex("#5B8CFF");       // Mauer
        public static readonly Color Showboat = Hex("#FF6FD5");    // Tunnel
        public static readonly Color Trick = Hex("#C77DFF");       // Lockvogel
        public static readonly Color Silver = Hex("#BFE9FF");      // Schlusspfiff
        public static readonly Color Header = Hex("#7CC4FF");      // Kopfball
        public static readonly Color Coin = Hex("#FFCC5C");        // Münzen

        // UI
        public static readonly Color UiGlass = Hex("#0C1522");
        public static readonly Color UiRim = Hex("#FFFFFF");
        public static readonly Color UiText = Hex("#F2F6FA");
        public static readonly Color UiMuted = Hex("#8FA3B8");
        public static readonly Color HpA = Hex("#FF3D5E");
        public static readonly Color HpB = Hex("#FF7F50");
    }
}
