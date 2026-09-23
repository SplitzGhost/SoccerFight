using UnityEngine;

namespace SoccerFight
{
    public enum StageMechanic { None, Wind, Lightning, Darkness, Geysers, Ice, LowGravity, Eclipse }
    public enum Weather { Fireflies, Leaves, Rain, Spores, Embers, Snow, Stars, Ash }
    public enum MonsterFeature { Horns, Thorns, Crystals, Flames, Ice, Stars, Void, Lamps }

    public struct RosterEntry
    {
        public EnemyType Type;
        public float Weight;
        public int FromWave;
        public string Name;
        public RosterEntry(EnemyType type, float weight, int fromWave, string name) { Type = type; Weight = weight; FromWave = fromWave; Name = name; }
    }

    public sealed class BossDef
    {
        public string Name, Title;
        public Monster.Kind Body;
        public Look Look;
        public EnemyType Minion;
        public BossMove[] Moves;
    }

    /// <summary>
    /// A stage's identity: name, colour script (applied to the world through the environment grade),
    /// weather, special mechanic, monster skins, roster and boss.
    /// </summary>
    public sealed class StageTheme
    {
        public string Name, Tagline, Description, Background, MechanicName, MechanicText, MiniBossName;
        public StageMechanic Mechanic;
        // environment grade
        public float Hue, Saturation = 1f, Brightness = 1f, Contrast = 1f;
        public Color Tint = Color.white;
        public float Vignette;
        // sky, haze and time of day (null = the default sunny sky and haze)
        public Color? SkyTop, SkyHorizon, Haze;
        /// <summary>0 = day, 1 = night: stars, lamps, glowing plants and fireflies.</summary>
        public float Night;
        /// <summary>How much of the sun shows (1 = full, 0 = hidden behind rain clouds or rock).</summary>
        public float Sun = 1f;
        // atmosphere
        public Weather Weather;
        public Color AmbientColor = Color.white;
        public Color Accent = Color.white;
        // monster skin
        public Color BlobTop, BlobBottom, Glow, Eye, WispTop, WispBottom, WispGlow;
        public MonsterFeature Feature;
        /// <summary>Platform styles the stage's generated layouts are built from.</summary>
        public Level.Style[] PlatformStyles;
        public RosterEntry[] Roster;
        public BossDef Boss;
    }

    public static class StageThemes
    {
        static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        public static readonly StageTheme[] All =
        {
            new StageTheme
            {
                Name = "SONNENARENA", Tagline = "Wo alles begann",
                Description = "Ein altes Stadion mitten auf einer sonnigen Wiese. Bunte Tribünen, flatternde Wimpel, frisch gemähter Rasen und Pollen in der Luft.",
                Background = "Blauer Himmel mit dicken Wolken, ferne Berge, Stadion auf dem Hügel, Aquädukt und Säulengang, saftiges Grün, schwebende Felsen als Plattformen.",
                MechanicName = "KEINE", MechanicText = "Der Einstieg: lerne Schuss, Power-Schuss und die Plattformen kennen.",
                Mechanic = StageMechanic.None, Weather = Weather.Fireflies, AmbientColor = H("#C8FF8A"), Accent = H("#5CF0FF"),
                BlobTop = H("#B784FF"), BlobBottom = H("#5E36B8"), Glow = H("#FFB23E"), Eye = H("#FFF6D0"),
                WispTop = H("#FFC454"), WispBottom = H("#E06A1E"), WispGlow = H("#FFE9A0"), Feature = MonsterFeature.Horns, PlatformStyles = new[] { Level.Style.Terrace, Level.Style.Capital, Level.Style.Rock },
                Roster = new[]
                {
                    new RosterEntry(EnemyType.Hopper, 10f, 1, "DÜSTERLING"),
                    new RosterEntry(EnemyType.Diver, 5f, 2, "IRRLICHT"),
                    new RosterEntry(EnemyType.Splitter, 3f, 3, "KNOSPLING"),
                },
                MiniBossName = "RUINENWÄCHTER",
                Boss = new BossDef { Name = "DÜSTERKÖNIG", Title = "Herrscher der Ruinen", Body = Monster.Kind.Blob, Look = Look.King, Minion = EnemyType.Hopper,
                    Moves = new[] { BossMove.LeapSlam, BossMove.Summon, BossMove.Charge } },
            },
            new StageTheme
            {
                Name = "BERNSTEINHAIN", Tagline = "Goldene Stunde",
                Description = "Ein Herbstwald im letzten Abendlicht. Das Laub glüht rot und golden, schräge Lichtstrahlen fallen durch die Kronen.",
                Background = "Bernsteinfarbener Himmel, warmer Dunst zwischen den Stämmen, Laubregen vor der Kamera, goldene Lichtschächte.",
                MechanicName = "WINDBÖEN", MechanicText = "Starke Böen schieben Spieler, Ball und Gegner zur Seite.",
                Mechanic = StageMechanic.Wind, Hue = -18f, Saturation = 1.05f, Brightness = 0.96f, Contrast = 1.05f, Tint = new Color(1.16f, 0.94f, 0.7f),
                SkyTop = H("#5C8FD6"), SkyHorizon = H("#FFD29A"), Haze = H("#F4D9B8"), Night = 0.1f,
                Weather = Weather.Leaves, AmbientColor = H("#FF9A4A"), Accent = H("#FFB347"),
                BlobTop = H("#5FE0B8"), BlobBottom = H("#1E8A7E"), Glow = H("#FFE07A"), Eye = H("#FFF6C8"),
                WispTop = H("#78E4F4"), WispBottom = H("#2A86B4"), WispGlow = H("#D8FAFF"), Feature = MonsterFeature.Thorns, PlatformStyles = new[] { Level.Style.Mushroom, Level.Style.Plank, Level.Style.Rock },
                Roster = new[]
                {
                    new RosterEntry(EnemyType.Hopper, 8f, 1, "DORNLING"),
                    new RosterEntry(EnemyType.Splitter, 6f, 1, "EICHELKOPF"),
                    new RosterEntry(EnemyType.Spitter, 4f, 2, "SPORENSPUCKER"),
                    new RosterEntry(EnemyType.Diver, 4f, 2, "POLLENIRRLICHT"),
                },
                MiniBossName = "BORKENRIESE",
                Boss = new BossDef { Name = "DORNENMUTTER", Title = "Herz des Hains", Body = Monster.Kind.Blob, Look = Look.ThornMother, Minion = EnemyType.Splitter,
                    Moves = new[] { BossMove.Volley, BossMove.Summon, BossMove.LeapSlam } },
            },
            new StageTheme
            {
                Name = "REGENWACHT", Tagline = "Stadt im Dauerregen",
                Description = "Regennasse Türme, eiserne Zäune und flackernde Laternen. Über allem hängt ein Gewitter, das nie weiterzieht.",
                Background = "Kalte Blautöne, schräger Starkregen mit Spritzern am Boden, Blitze erhellen kurz die Silhouetten der Türme.",
                MechanicName = "GEWITTER", MechanicText = "Blitze schlagen an markierten Stellen ein – sie treffen dich und die Gegner.",
                Mechanic = StageMechanic.Lightning, Hue = 12f, Saturation = 0.55f, Brightness = 0.7f, Contrast = 1.05f, Tint = new Color(0.9f, 0.97f, 1.12f),
                SkyTop = H("#4E5D74"), SkyHorizon = H("#AEBACB"), Haze = H("#9FAAB9"), Night = 0.4f, Sun = 0f,
                Weather = Weather.Rain, AmbientColor = H("#B8D8FF"), Accent = H("#7FD4FF"),
                BlobTop = H("#FF8A5E"), BlobBottom = H("#B23C3C"), Glow = H("#FFE3A0"), Eye = H("#FFF6E8"),
                WispTop = H("#FFD27A"), WispBottom = H("#B07A2E"), WispGlow = H("#FFE7A3"), Feature = MonsterFeature.Lamps, PlatformStyles = new[] { Level.Style.Plank, Level.Style.Capital, Level.Style.Block },
                Roster = new[]
                {
                    new RosterEntry(EnemyType.Lantern, 6f, 1, "LATERNENGEIST"),
                    new RosterEntry(EnemyType.Hopper, 6f, 1, "PFÜTZLING"),
                    new RosterEntry(EnemyType.Diver, 4f, 1, "TROPFENIRRLICHT"),
                    new RosterEntry(EnemyType.Brute, 2f, 3, "EISENKÄFER"),
                },
                MiniBossName = "TURMWÄCHTER",
                Boss = new BossDef { Name = "STURMLATERNE", Title = "Auge des Gewitters", Body = Monster.Kind.Wisp, Look = Look.StormLantern, Minion = EnemyType.Lantern,
                    Moves = new[] { BossMove.Volley, BossMove.LightningCall, BossMove.Teleport } },
            },
            new StageTheme
            {
                Name = "GLIMMERGROTTE", Tagline = "Das leuchtende Dunkel",
                Description = "Eine violette Höhle tief unter dem Stadion. Leuchtpilze, Kristalladern und Sporen, die wie Sterne schweben.",
                Background = "Tiefes Indigo, biolumineszente Akzente, kaum Himmel – nur Kristalllicht und der eigene Ball erhellen die Szene.",
                MechanicName = "DUNKELHEIT", MechanicText = "Die Sicht ist eingeschränkt. Dein Ball wird zur Lichtquelle.",
                Mechanic = StageMechanic.Darkness, Hue = 15f, Saturation = 1.1f, Brightness = 0.42f, Contrast = 1.08f, Tint = new Color(0.78f, 0.68f, 1.45f), Vignette = 0.03f,
                SkyTop = H("#120A2A"), SkyHorizon = H("#3E2A70"), Haze = H("#33285E"), Night = 1f, Sun = 0f,
                Weather = Weather.Spores, AmbientColor = H("#7CFFE0"), Accent = H("#B070FF"),
                BlobTop = H("#62F0CF"), BlobBottom = H("#1E8A9C"), Glow = H("#6BF2FF"), Eye = H("#F0FFFB"),
                WispTop = H("#8AF4FF"), WispBottom = H("#2E80C4"), WispGlow = H("#D8FCFF"), Feature = MonsterFeature.Crystals, PlatformStyles = new[] { Level.Style.Crystal, Level.Style.Mushroom, Level.Style.Rock },
                Roster = new[]
                {
                    new RosterEntry(EnemyType.Shade, 6f, 1, "SCHEMEN"),
                    new RosterEntry(EnemyType.Spitter, 5f, 1, "PILZSPUCKER"),
                    new RosterEntry(EnemyType.Hopper, 5f, 1, "KRISTALLKRIECHER"),
                    new RosterEntry(EnemyType.Splitter, 4f, 2, "SPORENKNOLLE"),
                },
                MiniBossName = "GEODENHÜTER",
                Boss = new BossDef { Name = "KRISTALLWÄCHTER", Title = "Hüter der Adern", Body = Monster.Kind.Blob, Look = Look.CrystalGuard, Minion = EnemyType.Shade,
                    Moves = new[] { BossMove.RadialBurst, BossMove.Summon, BossMove.LeapSlam } },
            },
            new StageTheme
            {
                Name = "GLUTSCHMIEDE", Tagline = "Asche und Eisen",
                Description = "Die Ruinen einer Vulkanschmiede. Lava pulsiert unter dem Rasen, Funken steigen auf, die Luft flimmert.",
                Background = "Glühendes Rot-Orange gegen schwarzen Basalt, aufsteigende Glut, Rauchschwaden vor dem Himmel.",
                MechanicName = "LAVAGEYSIRE", MechanicText = "Markierte Bodenstellen brechen aus und schleudern alles in die Luft.",
                Mechanic = StageMechanic.Geysers, Hue = -35f, Saturation = 1.1f, Brightness = 0.58f, Contrast = 1.1f, Tint = new Color(1.35f, 0.82f, 0.62f),
                SkyTop = H("#2A1424"), SkyHorizon = H("#E0643A"), Haze = H("#9C4A3A"), Night = 0.65f,
                Weather = Weather.Embers, AmbientColor = H("#FF8A3A"), Accent = H("#FF6A2A"),
                BlobTop = H("#5A4652"), BlobBottom = H("#1E1418"), Glow = H("#FF8A30"), Eye = H("#FFE36A"),
                WispTop = H("#FFF0A0"), WispBottom = H("#E0A020"), WispGlow = H("#FFF8D0"), Feature = MonsterFeature.Flames, PlatformStyles = new[] { Level.Style.Block, Level.Style.Rock, Level.Style.Capital },
                Roster = new[]
                {
                    new RosterEntry(EnemyType.Bomber, 7f, 1, "GLUTBOMBE"),
                    new RosterEntry(EnemyType.Hopper, 5f, 1, "MAGMALING"),
                    new RosterEntry(EnemyType.Brute, 3f, 2, "OBSIDIANKOLOSS"),
                    new RosterEntry(EnemyType.Diver, 4f, 1, "GLUTIRRLICHT"),
                },
                MiniBossName = "ESSENWÄCHTER",
                Boss = new BossDef { Name = "MAGMAKOLOSS", Title = "Die lebende Esse", Body = Monster.Kind.Blob, Look = Look.MagmaColossus, Minion = EnemyType.Bomber,
                    Moves = new[] { BossMove.LeapSlam, BossMove.GeyserCall, BossMove.Charge } },
            },
            new StageTheme
            {
                Name = "FROSTGIPFEL", Tagline = "Wo die Zeit gefriert",
                Description = "Verschneite Gipfel unter einem Polarlicht. Alles ist still, bleich und glitzernd – und spiegelglatt.",
                Background = "Blasses Blau-Weiß, Schneetreiben, ein grünes Polarlicht über den Bergen, vereiste Säulen.",
                MechanicName = "GLATTEIS", MechanicText = "Beim Bremsen und Wenden rutschst du weiter – plane deine Wege.",
                Mechanic = StageMechanic.Ice, Hue = 25f, Saturation = 0.35f, Brightness = 1.1f, Contrast = 0.95f, Tint = new Color(0.96f, 1f, 1.1f),
                SkyTop = H("#5FA6E6"), SkyHorizon = H("#EEF8FF"), Haze = H("#E6F3FC"), Night = 0f,
                Weather = Weather.Snow, AmbientColor = H("#F2FAFF"), Accent = H("#9BE8FF"),
                BlobTop = H("#7C8CFF"), BlobBottom = H("#3440B0"), Glow = H("#9BE8FF"), Eye = H("#F2F8FF"),
                WispTop = H("#6A86FF"), WispBottom = H("#2A3AB0"), WispGlow = H("#BFD8FF"), Feature = MonsterFeature.Ice, PlatformStyles = new[] { Level.Style.Crystal, Level.Style.Terrace, Level.Style.Rock },
                Roster = new[]
                {
                    new RosterEntry(EnemyType.Hopper, 6f, 1, "FROSTLING"),
                    new RosterEntry(EnemyType.Lantern, 5f, 1, "EISLICHT"),
                    new RosterEntry(EnemyType.Brute, 3f, 2, "SCHNEEKOLOSS"),
                    new RosterEntry(EnemyType.Shade, 3f, 2, "NEBELGEIST"),
                },
                MiniBossName = "GLETSCHERHERZ",
                Boss = new BossDef { Name = "FROSTWYRM", Title = "Atem des Winters", Body = Monster.Kind.Wisp, Look = Look.FrostWyrm, Minion = EnemyType.Lantern,
                    Moves = new[] { BossMove.Volley, BossMove.RadialBurst, BossMove.Teleport } },
            },
            new StageTheme
            {
                Name = "STERNENGARTEN", Tagline = "Über den Wolken",
                Description = "Schwebende Ruinen über einem Wolkenmeer. Das Milchstraßenband zieht über den Himmel, die Luft trägt kaum noch.",
                Background = "Tiefes Indigo mit Sternenstaub, schimmernde Wolkenränder, treibende Funken.",
                MechanicName = "SCHWERELOS", MechanicText = "Geringe Schwerkraft: höhere Sprünge, langsam fallende Bälle.",
                Mechanic = StageMechanic.LowGravity, Hue = 15f, Saturation = 1.15f, Brightness = 0.44f, Contrast = 1.05f, Tint = new Color(1.12f, 0.72f, 1.45f),
                SkyTop = H("#0E0A30"), SkyHorizon = H("#6A3A96"), Haze = H("#4E3A88"), Night = 1f, Sun = 0.55f,
                Weather = Weather.Stars, AmbientColor = H("#FFF3C4"), Accent = H("#FF7AE0"),
                BlobTop = H("#FFD24A"), BlobBottom = H("#C0761A"), Glow = H("#FFF3A0"), Eye = H("#FFFBE0"),
                WispTop = H("#FFF3C4"), WispBottom = H("#B38BE8"), WispGlow = H("#FFE89A"), Feature = MonsterFeature.Stars, PlatformStyles = new[] { Level.Style.Block, Level.Style.Crystal, Level.Style.Rock },
                Roster = new[]
                {
                    new RosterEntry(EnemyType.Shade, 5f, 1, "LEERENKEIM"),
                    new RosterEntry(EnemyType.Lantern, 5f, 1, "STERNSCHNUPPE"),
                    new RosterEntry(EnemyType.Diver, 5f, 1, "KOMETLING"),
                    new RosterEntry(EnemyType.Splitter, 4f, 2, "NEBELKEIM"),
                },
                MiniBossName = "HIMMELSWÄCHTER",
                Boss = new BossDef { Name = "KOMETEN-ORAKEL", Title = "Stimme der Sterne", Body = Monster.Kind.Wisp, Look = Look.CometOracle, Minion = EnemyType.Diver,
                    Moves = new[] { BossMove.Volley, BossMove.RadialBurst, BossMove.Teleport, BossMove.Summon } },
            },
            new StageTheme
            {
                Name = "EKLIPSE", Tagline = "Das Ende des Lichts",
                Description = "Ein roter Mond verschlingt die Sonne über dem zerborstenen Stadion. Alles, was du besiegt hast, kehrt zurück.",
                Background = "Karmesin und Schwarz, Aschefall, Risse aus Leere im Himmel, harte Kontraste.",
                MechanicName = "EKLIPSE", MechanicText = "Pulse der Dunkelheit machen alle Gegner für einige Sekunden schneller und stärker.",
                Mechanic = StageMechanic.Eclipse, Hue = 0f, Saturation = 0.7f, Brightness = 0.38f, Contrast = 1.12f, Tint = new Color(1.4f, 0.72f, 0.8f), Vignette = 0.1f,
                SkyTop = H("#0E040A"), SkyHorizon = H("#5C1422"), Haze = H("#401824"), Night = 0.9f,
                Weather = Weather.Ash, AmbientColor = H("#C9B8BC"), Accent = H("#FF2A4A"),
                BlobTop = H("#EADAE4"), BlobBottom = H("#8A6480"), Glow = H("#FF3A5A"), Eye = H("#FF4A6A"),
                WispTop = H("#FFD0DA"), WispBottom = H("#B06A84"), WispGlow = H("#FF4A6A"), Feature = MonsterFeature.Void, PlatformStyles = new[] { Level.Style.Block, Level.Style.Crystal, Level.Style.Terrace, Level.Style.Plank },
                Roster = new[]
                {
                    new RosterEntry(EnemyType.Hopper, 4f, 1, "LEERENBRUT"),
                    new RosterEntry(EnemyType.Diver, 3f, 1, "SCHATTENIRRLICHT"),
                    new RosterEntry(EnemyType.Splitter, 3f, 1, "ZERFALLSKEIM"),
                    new RosterEntry(EnemyType.Spitter, 3f, 1, "GALLESPUCKER"),
                    new RosterEntry(EnemyType.Brute, 2f, 2, "LEERENKOLOSS"),
                    new RosterEntry(EnemyType.Shade, 3f, 1, "SCHATTEN"),
                    new RosterEntry(EnemyType.Lantern, 3f, 2, "ROTLICHT"),
                    new RosterEntry(EnemyType.Bomber, 3f, 2, "LEERENBOMBE"),
                },
                MiniBossName = "EKLIPSENHERALD",
                Boss = new BossDef { Name = "LEERENFÜRST", Title = "Das letzte Licht", Body = Monster.Kind.Blob, Look = Look.VoidLord, Minion = EnemyType.Shade,
                    Moves = new[] { BossMove.LeapSlam, BossMove.RadialBurst, BossMove.Summon, BossMove.Charge, BossMove.Volley } },
            },
        };

        public static StageTheme For(int stage) => All[(Mathf.Max(1, stage) - 1) % All.Length];

        /// <summary>Cycle suffix after the first loop ("SONNENARENA II").</summary>
        public static string Title(int stage)
        {
            int loop = (Mathf.Max(1, stage) - 1) / All.Length;
            string[] roman = { "", " II", " III", " IV", " V", " VI", " VII", " VIII", " IX", " X" };
            return For(stage).Name + (loop < roman.Length ? roman[loop] : " +" + loop);
        }
    }
}
