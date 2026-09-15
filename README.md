# SoccerFight

2D-Side-View-Roguelite: Ein Fußballspieler kämpft sich mit seinem Ball durch Stages voller Monster-Wellen.
Alles (Grafik, Animation, Effekte, HUD) wird zur Laufzeit im Code erzeugt, es gibt keine importierten Sprites.

## Ein Lauf

- **Stage → Wellen → Boss:** Stage 1 hat 3 Wellen, Stage 2 hat 4, ab Stage 3 sind es 5, danach kommt der Stage-Boss.
  Die Schwierigkeit ist eine durchgehende Kurve (`Difficulty.cs`): Jede Welle +1 Stufe, jede neue Stage beginnt
  0,35 Stufen unter dem Ende der vorigen – die letzte Welle einer Stage ist also etwas härter als die erste der nächsten.
- **Nach jeder zweiten Runde** (Wellen und Bosskämpfe zählen): 3 zufällige Upgrade-Karten (Gewöhnlich / Selten /
  Episch / Legendär), eine davon nimmst du (Taste 1/2/3 oder Klick). 59 Upgrades inkl. Synergien, siehe `Upgrades.cs`.
- **Nach jedem Boss:** die Wahl zwischen 2 zufälligen Fähigkeiten – fällt der Boss auf eine Upgrade-Runde, vorher
  eine Boss-Belohnung (nur Selten+). Hinter der Fähigkeitswahl entsteht schon die Arena der nächsten Stage.
  Zu Beginn hast du nur **Schuss** und **Power-Schuss** (der Luft-Rückstoß beim Schießen in der Luft geht immer);
  Rainbow Flick, Hochhalten, Übersteiger und Fallrückzieher werden Stage für Stage freigeschaltet.
  Die nicht gewählte Fähigkeit kommt zurück in den Pool.
- **8 Stage-Themen** mit eigener Farbstimmung, Wetter, Monster-Aussehen, Gegnern, Miniboss, Boss und Spezialregel:
  Mondlicht-Ruinen, Bernsteinhain (Windböen), Regenwacht (Blitzeinschläge), Glimmergrotte (Dunkelheit),
  Glutschmiede (Lavageysire), Frostgipfel (Glatteis), Sternengarten (geringe Schwerkraft), Eklipse (Verstärkungs-Pulse).
  Danach beginnt der Zyklus von vorn („II“, „III“ …) mit weiter steigender Kurve.
- **Gegner:** 9 Verhaltensarten (Hüpfer, Sturzflieger, Teiler, Spucker, Koloss, Schemen, Laterne, Bombe …), jede mit
  eigenem Körper (Kröte mit Stielaugen, Panzerbuckel mit Fäusten, Quallenlaterne, Kapuzengeist …), Elite-Gegner mit
  Eigenschaften (Flink, Gepanzert, Regenerierend, Instabil, Rasend), Minibosse in den späten Wellen und acht Bosse mit
  Phasen und ganz eigener Gestalt (Düsterkönig, Dornenmutter, Sturmlaterne, Kristallwächter, Magmakoloss, Frostwyrm,
  Kometen-Orakel, Leerenfürst).

## ▶ Im Browser spielen

**https://splitzghost.github.io/SoccerFight/**

Die Seite wird nach jeder Änderung automatisch neu gebaut (siehe [Veröffentlichen](#veröffentlichen)).
Am besten am Desktop mit Maus und Tastatur spielen.

## In Unity starten

1. Den Ordner `SoccerFight/` mit Unity 6000.6.0f1 öffnen
2. `Assets/Scenes/Game.unity` öffnen
3. Play drücken

Die Szene enthält nur ein GameObject mit der Komponente `Game`, der Rest wird beim Start aufgebaut (~2 s).

## Steuerung

| Taste | Aktion |
|---|---|
| A / D | Laufen |
| Leertaste | Springen (länger halten = höher), auch durch Plattformen hindurch nach oben |
| S | Durch die Plattform unter dir nach unten fallen (in der Luft gehalten: durch alle Plattformen) |
| Linksklick | Schuss Richtung Mauszeiger (Cooldown 0,45 s). In der Luft stößt dich der Rückstoß in die Gegenrichtung: einmal pro Sprung, nach unten geschossen wie ein Doppelsprung |
| Rechtsklick | Power-Schuss (nur im Stand, Cooldown 3,5 s): langes Ausholen, dann ein gerader goldener Schuss, der durch alle Gegner hindurchfliegt. Macht dafür weniger Schaden (12 statt 18) |
| E | Übersteiger (am Boden, Cooldown 3,5 s): Fuß kreist über den Ball, dann ein Dash (~5 m) in Blick- oder Laufrichtung. Während des ganzen Moves unverwundbar, der Ball wird mitgenommen |
| Q | Fallrückzieher (nur in der Luft, Cooldown 5 s): Rückwärtssalto mit Scherenschlag, harter Schuss Richtung Mauszeiger. Der Ball explodiert beim ersten Aufprall auf Boden, Plattform oder Gegner (Flächenschaden) |
| R | Rainbow Flick (Cooldown 6 s, Flächenschaden beim Aufprall) |
| Shift | Ball hochhalten: drücken, wenn der Ball in den Ring fällt. Jede Berührung heilt ein wenig (perfekt = mehr, alle 10 ein Bonus). Zu früh oder zu spät und der Ball fällt, das Zeitfenster wird mit jeder Berührung enger. Währenddessen kein Laufen und kein Schuss |
| 1 / 2 / 3 | Upgrade- bzw. Fähigkeitskarte wählen |
| Esc | Pausemenü (Weiter, Einstellungen, Neu starten, Beenden) |
| F1 | FPS-Anzeige an/aus |
| F2 | VSync an/aus (aus = unbegrenzte FPS) |
| F3 | Developer-Modus (auch im Pausemenü): Unverwundbar, keine Abklingzeiten, Ein-Treffer-Kills, Spieltempo, Stage-Sprung, Welle überspringen, Boss/Gegner rufen, Karten öffnen, jedes Upgrade gezielt hinzufügen, Live-Zahlen. Dev-Läufe zählen nicht für den Rekord |
| Enter | Neuer Lauf nach Niederlage |

Flick, Hochhalten, Übersteiger und Fallrückzieher sind zu Beginn gesperrt und erscheinen erst nach ihrer Freischaltung (nach Boss-Siegen) in der Skill-Leiste – links neben den vorhandenen, alle gleich groß; der normale Schuss sitzt etwas größer ganz rechts.

Alle zehn Aktionen (Laufen, Springen, Durchfallen, Schuss, Power-Schuss, Übersteiger, Fallrückzieher, Rainbow Flick, Hochhalten) lassen sich im Pausemenü unter **Einstellungen → Steuerung** frei belegen
(auch Maustasten). Dort gibt es außerdem Vollbild (nur im Build), VSync, FPS-Anzeige, Bildschirmwackeln,
Leuchten (Bloom) und den Farbsaum-Effekt. Alles wird automatisch gespeichert.

## Arena

- **Plattformen:** Stage 1 hat immer das klassische Layout – links und rechts je eine Ruinen-Terrasse, dazwischen zwei
  Säulenkapitelle (Höhe ~2,2, ein Sprung vom Rasen) und drei schwebende Felsen (Höhe ~4,1). Ab Stage 2 würfelt jede Stage
  ihr eigenes Layout (`Level.Generate`): mal wenige, mal viele Ebenen, mal kleine, mal breite, mal stillstehende, mal
  seitlich gleitende oder als Aufzug fahrende – in den Stilen des Themas (Terrasse, Kapitell, Moosfels, Kristallplatte,
  Runenblock, Holzsteg an Ketten, Riesenpilz). Alle sind von unten durchspringbar; wer auf einer bewegten steht, fährt
  mit. Blobs springen dem Spieler gezielt hinterher (erkennbar am langen Ducken davor) und hüpfen von der Kante, wenn der
  Spieler unten ist. Ball, Schatten, Gras und Rainbow Flick funktionieren auf jeder Ebene.
- **Tiefe:** acht Parallax-Ebenen hinter dem Spielfeld (Büsche, Säulen und Riesenstamm, Arkaden-Ruine, Aquädukt mit
  Wasserlauf, große Bäume, Waldhügel mit verfallenem Stadion und Flutlichtmast, Berge mit Wasserfall, Gipfel mit Burg vor
  dem Mond). Jede Ebene bewegt sich entsprechend ihrer Entfernung mit der Kamera, horizontal wie vertikal, und wird nach
  hinten dunkler (`WorldEnvironment.DepthTint`). Zwischen den Ebenen liegt Nebel.
- **Spielfeld:** Kreidelinien (Mittellinie, Mittelkreis, Strafräume, Torräume, Elfmeterpunkte) in derselben Perspektive
  wie die Mähstreifen, ausgetretener Matsch vor den Toren, Pfützen mit Mondspiegelung, die spritzen, wenn man durchläuft.

## Code-Überblick (`SoccerFight/Assets/Scripts`)

| Ordner | Inhalt |
|---|---|
| `Core/` | `Game` (Einstiegspunkt + Update-Reihenfolge), Input, Federn/Easing/IK (`MathUtil`), Hit-Stop & Slow-Mo (`TimeFx`) |
| `Art/` | SDF-Rasterizer (`SdfCanvas`, `Sdf`), Farbpalette, prozedurale Grafiken für Spieler, Ball, die 17 Monster-Körper (`MonsterArt`), Umgebung (`EnvironmentArt`), tiefe Ebenen und Spielfeldlinien (`DepthArt`), Plattformen (`PlatformArt`) und Pflanzen (`FoliageArt`); `ArtJobs` erzeugt den Hintergrund parallel auf Worker-Threads (im Browser nacheinander, siehe `Par`), `ArtQueue` die Grafik neuer Stages zur Laufzeit |
| `World/` | Begehbare Geometrie, Plattform-Layouts und -Bewegung (`Level`), Plattform-Darstellung (`PlatformViews`), Parallax-Ebenen mit Tiefenabdunklung (`WorldEnvironment`), Vegetations-Meshes mit GPU-Wind (`FoliageLayer` + Shader `SF_Foliage`), lebendige Details wie Wolken, Fledermäuse, Wasserfälle, Blätter, Laternen, Geisterlichter (`Ambient`) |
| `Player/` | Bewegung & Fähigkeiten inkl. Hochhalten und Luft-Rückstoß (`Player`), prozedurale Animation mit IK, Bremsen und Drehung (`PlayerRig`), Nachbilder |
| `Ball/` | Dribbeln, Schuss, Regenbogen-Bogen, Rückkehr |
| `Run/` | Roguelite-Lauf: Zustandsautomat (`RunDirector`), Lauf-Zustand (`RunState`), Schwierigkeitskurve (`Difficulty`), Upgrade-Datenbank und Kartenziehung (`Upgrades`), Werte des Builds (`PlayerStats`), Fähigkeiten (`Abilities`), Stage-Themen (`StageThemes`), Spezialregeln und Gefahren (`StageMechanics`) |
| `Combat/` | Zentrale Trefferberechnung mit Krits, Brand, Frost, Kettenfunken, Explosionen und Kill-Effekten (`Combat`), Echo-Bälle, Wirbel/Schwarzes Loch, Zwillingssonne |
| `Enemies/` | Monster mit 9 Verhaltensarten, Elite-Eigenschaften, Minibossen und Bossen (`Monster`, `EnemyDefs`), Gegner-Geschosse, Monster-Pool und Kollisionen (`WaveDirector`) |
| `FX/` | Partikelsystem, Blitze, Kamera (Follow, Shake, Zoom), Post-Processing (inkl. Eklipse und Dunkelheit) |
| `UI/` | HUD (Healthbar, Build-Leiste, Stage-/Wellen-Anzeige, Boss-Leiste, Namensschilder, gesperrte Fähigkeiten, Stage- und Boss-Intro), Karten-Bildschirm (`RewardScreen`), Upgrade-Symbole (`UpgradeIcons`), Pausemenü (`PauseMenu`, Widgets in `UiKit`) |
| `World/ThemeGrade` | Farbstimmung pro Stage: eine globale Farbmatrix wirkt nur auf Umgebungsmaterialien (Shader-Eigenschaft `_EnvGraded`), dazu Wetterpartikel |
| `Core/` (Einstellungen) | `KeyBindings` (frei belegbare Tasten), `GameSettings` (Optionen, in PlayerPrefs gespeichert) |
| `DevTools/`, `Editor/` | Screenshot-Tool für automatisierte Prüfung, Szenen-Setup, WebGL-Build (`WebGLBuilder`) |

## Wo man dreht

- **Schwierigkeit:** alle Formeln in `Difficulty.cs` (Leben, Schaden, Tempo, Budget, Elite-Chance, Minibosse, Boss-Werte), Grundwerte der Gegner in `EnemyDefs.cs`, Heilung nach Welle/Boss in `RunDirector`
- **Developer-Modus:** Schalter in `DevMode.cs`, Panel in `UI/DevPanel.cs`, Aktionen (`Dev*`-Methoden) in `RunDirector`
- **Upgrades:** Werte, Beschreibung und Stapelgrenze in `UpgradeDb` (`Upgrades.cs`), Seltenheits-Gewichte in `UpgradeRoller.Weights`
- **Stages:** Name, Farbstimmung, Wetter, Gegner, Boss und Regel in `StageThemes.cs`
- **Spielgefühl:** Konstanten oben in `Player.cs` (Tempo, Sprung, Cooldowns, Schaden)
- **Timing von Schuss & Flick:** `KickWindup/KickContact/...` und `FlickSet/FlickRoll/...` in `Player.cs`
- **Hochhalten:** `Juggle*`-Konstanten in `Player.cs` (Zeitfenster, Heilung, Flughöhen, Reihenfolge Fuß/Knie/Kopf)
- **Luft-Rückstoß:** `AirKickBoost` / `BoostControlTime` in `Player.cs`
- **Skills:** `Power*`, `StepOver*`/`DashTime`/`DashSpeed`, `Bicycle*`/`Blast*` in `Player.cs` (Timing, Cooldowns, Schaden, Explosionsradius)
- **Posen:** `PoseKick` (auch Power-Schuss) / `PoseFlick` / `PoseJuggle` / `PoseStepOver` / `PoseBicycle` in `PlayerRig.cs`, Salto über `BicycleSpin`
- **Spieler-Look:** Formen in `PlayerArt.cs`, Mondlicht-Randlicht und Bodenreflex im Shader `SF_Character`
- **Farben:** `Palette.cs`
- **Kamera:** `BaseSize` (Zoom) und `BaseY` in `CameraRig.cs`; wie stark sie der Plattformhöhe folgt in `CameraRig.Target`
- **Plattformen:** Lage und Höhe in `Level.Platforms` (Grafik und Pflanzen passen sich automatisch an), Sprungverhalten der Blobs in `Monster.PlanLeap`
- **Tiefenwirkung:** Parallax-Faktoren in `WorldEnvironment.AddLayer(...)`-Aufrufen, Abdunklung pro Ebene über die `D*`-Konstanten und `DepthTint`
- **Glow/Bloom:** `PostFx.cs` und die Material-Intensitäten in `Art.cs`
- **Wind:** Stärke von Neigung und Böen in `WorldEnvironment.Update`, Wellenform im Shader `SF_Foliage`
- **Pflanzendichte:** die Schleifen in `WorldEnvironment.BuildNear/BuildGround/BuildRuins`

## Veröffentlichen

Nach jeder Antwort von Claude Code startet ein Stop-Hook (`.claude/settings.local.json`) im Hintergrund `tools/publish.ps1`:

1. Alle Änderungen werden committet und auf `main` gepusht.
2. Wenn sich `Assets`, `Packages` oder `ProjectSettings` geändert haben, wird das Projekt nach `.build/project` gespiegelt
   (der offene Editor sperrt das Original) und dort per Batchmode als WebGL gebaut.
3. Der Build landet als einzelner Commit auf dem Branch `gh-pages`, den GitHub Pages ausliefert.

Von Hand geht es mit `powershell -File tools\publish.ps1` (mit `-Force` wird auch ohne Änderung neu gebaut).
Visuelle Prüfung: `tools\capture.ps1 -Scenario run` (Stage-Karte, Welle, Karten, Fähigkeitswahl, Boss, alle 8 Themen,
Zusammenfassung) und `-Scenario sim` (ein Bot spielt einen echten Lauf und protokolliert jede Phase im Unity-Log).
Protokoll: `.build/publish.log`, Unity-Log des letzten Builds: `.build/unity-build.log`.
Für die Pushes muss die GitHub-CLI eingeloggt sein (`gh auth login`).

## Schriftart

Inter (SIL Open Font License 1.1) liegt in `SoccerFight/Assets/Resources/Fonts`.
