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
        // atmosphere
        public Weather Weather;
        public Color AmbientColor = Color.white;
        public Color Accent = Color.white;
        // monster skin
        public Color BlobTop, BlobBottom, Glow, Eye, WispTop, WispBottom, WispGlow;
        public MonsterFeature Feature;
        /// <summary>The stage's design sheet (Resources/Stages/&lt;Kit&gt;: scenery, ground, platforms, props).</summary>
        public string Kit;
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
                Name = "MONDLICHT-RUINEN", Tagline = "Wo alles begann",
                Description = "Überwucherte Arkaden eines vergessenen Stadions im kalten Mondlicht. Glühwürmchen, schwingende Laternen und Nebel über dem Rasen.",
                Background = "Mondhimmel mit Wolkenbänken, ferne Gipfel, Aquädukt und Säulengang, dichtes Blattwerk, schwebende Moosfelsen als Plattformen.",
                MechanicName = "KEINE", MechanicText = "Der Einstieg: lerne Schuss, Power-Schuss und die Plattformen kennen.",
                Mechanic = StageMechanic.None, Weather = Weather.Fireflies, AmbientColor = H("#C8FF8A"), Accent = H("#5CF0FF"),
                BlobTop = H("#6A45A8"), BlobBottom = H("#2A1650"), Glow = H("#FF4FD8"), Eye = H("#FFE98A"),
                WispTop = H("#3E5AC8"), WispBottom = H("#1A1F5C"), WispGlow = H("#7AA8FF"), Feature = MonsterFeature.Horns, Kit = "mondlicht",
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
                Mechanic = StageMechanic.Wind, Hue = -156f, Saturation = 0.95f, Brightness = 1.02f, Contrast = 1.04f, Tint = new Color(1.16f, 0.86f, 0.62f),
                Weather = Weather.Leaves, AmbientColor = H("#FF9A4A"), Accent = H("#FFB347"),
                BlobTop = H("#8A5230"), BlobBottom = H("#3A1E14"), Glow = H("#FFB347"), Eye = H("#FFF1A8"),
                WispTop = H("#D9A441"), WispBottom = H("#7A4A1A"), WispGlow = H("#FFE08A"), Feature = MonsterFeature.Thorns, Kit = "bernstein",
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
                Mechanic = StageMechanic.Lightning, Hue = 18f, Saturation = 0.5f, Brightness = 0.9f, Contrast = 1.08f, Tint = new Color(0.84f, 0.95f, 1.12f),
                Weather = Weather.Rain, AmbientColor = H("#B8D8FF"), Accent = H("#7FD4FF"),
                BlobTop = H("#4B5E7A"), BlobBottom = H("#1C2433"), Glow = H("#7FD4FF"), Eye = H("#E8F6FF"),
                WispTop = H("#FFD27A"), WispBottom = H("#8A5E24"), WispGlow = H("#FFE7A3"), Feature = MonsterFeature.Lamps, Kit = "regen",
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
                Mechanic = StageMechanic.Darkness, Hue = 88f, Saturation = 1.15f, Brightness = 1.12f, Contrast = 1.04f, Tint = new Color(0.95f, 0.88f, 1.18f), Vignette = 0.03f,
                Weather = Weather.Spores, AmbientColor = H("#7CFFE0"), Accent = H("#B070FF"),
                BlobTop = H("#3A2A6A"), BlobBottom = H("#140C2E"), Glow = H("#6BF2FF"), Eye = H("#C8FFF6"),
                WispTop = H("#2A1E4A"), WispBottom = H("#0C0818"), WispGlow = H("#B070FF"), Feature = MonsterFeature.Crystals, Kit = "grotte",
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
                Mechanic = StageMechanic.Geysers, Hue = -165f, Saturation = 1.1f, Brightness = 0.82f, Contrast = 1.14f, Tint = new Color(1.22f, 0.8f, 0.5f),
                Weather = Weather.Embers, AmbientColor = H("#FF8A3A"), Accent = H("#FF6A2A"),
                BlobTop = H("#3E2A24"), BlobBottom = H("#140C0A"), Glow = H("#FF6A2A"), Eye = H("#FFE36A"),
                WispTop = H("#FF8A3A"), WispBottom = H("#7A1E0A"), WispGlow = H("#FFB070"), Feature = MonsterFeature.Flames, Kit = "glut",
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
                Mechanic = StageMechanic.Ice, Hue = 12f, Saturation = 0.36f, Brightness = 1.2f, Contrast = 0.95f, Tint = new Color(0.92f, 0.98f, 1.08f),
                Weather = Weather.Snow, AmbientColor = H("#F2FAFF"), Accent = H("#9BE8FF"),
                BlobTop = H("#CDE8F5"), BlobBottom = H("#6C8FB0"), Glow = H("#9BE8FF"), Eye = H("#1C3A5A"),
                WispTop = H("#E6FAFF"), WispBottom = H("#7FB4D6"), WispGlow = H("#BFF3FF"), Feature = MonsterFeature.Ice, Kit = "frost",
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
                Mechanic = StageMechanic.LowGravity, Hue = 48f, Saturation = 1.3f, Brightness = 1f, Contrast = 1.05f, Tint = new Color(0.94f, 0.94f, 1.14f),
                Weather = Weather.Stars, AmbientColor = H("#FFF3C4"), Accent = H("#FF7AE0"),
                BlobTop = H("#2A2255"), BlobBottom = H("#0A081E"), Glow = H("#FF7AE0"), Eye = H("#FFF6B0"),
                WispTop = H("#FFF3C4"), WispBottom = H("#8A6ACF"), WispGlow = H("#FFE89A"), Feature = MonsterFeature.Stars, Kit = "stern",
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
                Mechanic = StageMechanic.Eclipse, Hue = 160f, Saturation = 0.95f, Brightness = 0.72f, Contrast = 1.2f, Tint = new Color(1.1f, 0.85f, 0.9f), Vignette = 0.12f,
                Weather = Weather.Ash, AmbientColor = H("#C9B8BC"), Accent = H("#FF2A4A"),
                BlobTop = H("#2A121C"), BlobBottom = H("#07030A"), Glow = H("#FF2A4A"), Eye = H("#FF8A9A"),
                WispTop = H("#3A0E22"), WispBottom = H("#0A0206"), WispGlow = H("#FF4A6A"), Feature = MonsterFeature.Void, Kit = "eklipse",
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

        /// <summary>Cycle suffix after the first loop ("MONDLICHT-RUINEN II").</summary>
        public static string Title(int stage)
        {
            int loop = (Mathf.Max(1, stage) - 1) / All.Length;
            string[] roman = { "", " II", " III", " IV", " V", " VI", " VII", " VIII", " IX", " X" };
            return For(stage).Name + (loop < roman.Length ? roman[loop] : " +" + loop);
        }
    }
}
