using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Color script: a bright, saturated cartoon day in the spirit of Project Rise — blue sky, juicy
    /// greens, cream stone, warm sun, lavender shadows. Characters carry the strongest values so they
    /// still pop in front of the light backdrop.
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
        public static readonly Color EyeDark = Hex("#231C2C");
        public static readonly Color PlayerLine = Hex("#2C2640");   // soft cartoon outline, not black
        public static readonly Color MoonRim = Hex("#FFF0C8");   // sun rim on characters
        public static readonly Color BackLimbTint = new Color(0.74f, 0.78f, 0.88f, 1f);
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

        // Environment: a sunny cartoon day. The sun stands upper right (where the moon used to be, so
        // every rim light keeps its direction); shadows lean cool and lavender, far things fade into a
        // pale blue haze instead of into the dark.
        public static readonly Color SkyTop = Hex("#3F95DB");
        public static readonly Color SkyMid = Hex("#7CC3EE");
        public static readonly Color SkyHorizon = Hex("#D9F1F7");
        public static readonly Color Moon = Hex("#FFF8DE");       // the sun disc
        public static readonly Color MoonGlow = Hex("#FFE7A6");   // sun halo and light shafts
        public static readonly Color Haze = Hex("#AFD6F0");       // aerial perspective
        public static readonly Color Fog = Hex("#E4F3FA");        // drifting haze bands
        public static readonly Color CloudLight = Hex("#FFFFFF");
        public static readonly Color CloudShade = Hex("#AEBDE6");
        public static readonly Color FarTop = Hex("#7F9FD4");
        public static readonly Color FarBottom = Hex("#9FC4DE");
        public static readonly Color MidTop = Hex("#3E8A5A");
        public static readonly Color MidBottom = Hex("#8CC08A");
        public static readonly Color RuinTop = Hex("#8B8FAE");
        public static readonly Color RuinBottom = Hex("#A9B79A");
        public static readonly Color RuinRim = Hex("#FFF3D2");
        public static readonly Color Bush = Hex("#2F7D3E");
        public static readonly Color Lantern = Hex("#FFC46B");
        public static readonly Color Firefly = Hex("#FFF4B0");    // pollen and sparkles
        public static readonly Color GrassEdge = Hex("#B6E85C");
        public static readonly Color PitchA = Hex("#66BE3F");
        public static readonly Color PitchB = Hex("#56AD35");
        public static readonly Color PitchBack = Hex("#4E9E3A");
        public static readonly Color EarthTop = Hex("#A56A40");
        public static readonly Color EarthBottom = Hex("#5E3624");
        public static readonly Color Foreground = Hex("#1E5A36");

        // Vegetation: juicy greens with yellow sunlit tops and teal shadows
        public static readonly Color FolDark = Hex("#2A7447");
        public static readonly Color FolMid = Hex("#4DA43E");
        public static readonly Color FolLight = Hex("#8BCB45");
        public static readonly Color FolRim = Hex("#E2F57E");
        public static readonly Color Fern = Hex("#3C9446");
        public static readonly Color FernLight = Hex("#7FC64B");
        public static readonly Color Petal = Hex("#FFFFFF");
        public static readonly Color PetalWarm = Hex("#FFD34E");
        public static readonly Color MushCap = Hex("#E8503A");
        public static readonly Color MushCapDark = Hex("#A8342C");
        public static readonly Color MushStem = Hex("#F6EBD3");
        public static readonly Color Ivy = Hex("#3E8E3E");
        public static readonly Color IvyLight = Hex("#86C84C");
        public static readonly Color Moss = Hex("#74B64A");
        public static readonly Color Banner = Hex("#E0473C");
        public static readonly Color BannerLight = Hex("#FF7E5E");
        public static readonly Color Crystal = Hex("#7FE3FF");
        public static readonly Color Bark = Hex("#7B4D33");
        public static readonly Color BarkLight = Hex("#C08858");
        public static readonly Color Stone = Hex("#9A9DB8");      // Project-Rise style carved stone: cool lavender grey …
        public static readonly Color StoneLight = Hex("#E6DECB"); // … warming to sunlit cream
        public static readonly Color Mortar = Hex("#6C6F8C");
        public static readonly Color Wood = Hex("#9A6038");
        public static readonly Color WoodLight = Hex("#D39A5E");
        public static readonly Color WoodDark = Hex("#5E3622");
        public static readonly Color Slate = Hex("#4E6E82");       // stadium roofs, seats in shadow
        public static readonly Color Seat1 = Hex("#E8513F");
        public static readonly Color Seat2 = Hex("#2F8FE0");
        public static readonly Color Seat3 = Hex("#FFC23D");

        // Skill accents (one hue per ability so the bar reads at a glance)
        public static readonly Color Turf = Hex("#9BE564");        // Grätsche
        public static readonly Color Amber = Hex("#FFB03A");       // Abstoß
        public static readonly Color Guard = Hex("#5B8CFF");       // Mauer
        public static readonly Color Showboat = Hex("#FF6FD5");    // Tunnel
        public static readonly Color Trick = Hex("#C77DFF");       // Lockvogel
        public static readonly Color Silver = Hex("#BFE9FF");      // Schlusspfiff
        public static readonly Color Header = Hex("#7CC4FF");      // Kopfball
        public static readonly Color Coin = Hex("#FFCC5C");        // Münzen

        // Basketball
        public static readonly Color HoopOrange = Hex("#FF8A3A");  // Wurf, Dreier
        public static readonly Color HoopFlame = Hex("#FFB25A");   // Dreier-Explosion
        public static readonly Color Slam = Hex("#9FD8FF");        // Dunk-Druckwellen
        public static readonly Color Oop = Hex("#FFE07A");         // Alley-Oop
        public static readonly Color BallLeather = Hex("#E0712F");
        public static readonly Color BallLeatherDark = Hex("#A94A1C");
        public static readonly Color BallLeatherLight = Hex("#F5A262");
        public static readonly Color BallRib = Hex("#1B1413");

        // UI
        public static readonly Color UiGlass = Hex("#2C4452");   // slate stone of HUD plates
        public static readonly Color UiRim = Hex("#FFFFFF");
        public static readonly Color UiText = Hex("#F2F6FA");
        public static readonly Color UiMuted = Hex("#D4E4EA");
        public static readonly Color HpA = Hex("#FF3D5E");
        public static readonly Color HpB = Hex("#FF7F50");
        public static readonly Color LifeLow = Hex("#F2553A");   // the player's bar: green when healthy, warm red when low
        public static readonly Color Life = Hex("#7CD447");
    }
}
