# SoccerFight

2D-Side-View-PvE: Ein Fußballspieler kämpft mit seinem Ball gegen Monster-Wellen.
Alles (Grafik, Animation, Effekte, HUD) wird zur Laufzeit im Code erzeugt, es gibt keine importierten Sprites.

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
| A / D (oder Pfeiltasten) | Laufen |
| Leertaste / W | Springen (länger halten = höher) |
| Linksklick | Schuss Richtung Mauszeiger (Cooldown 0,45 s) |
| R | Rainbow Flick (Cooldown 6 s, Flächenschaden beim Aufprall) |
| Esc | Pausemenü (Weiter, Einstellungen, Neu starten, Beenden) |
| F1 | FPS-Anzeige an/aus |
| F2 | VSync an/aus (aus = unbegrenzte FPS) |
| Enter | Neustart nach Niederlage |

Laufen, Springen, Schuss und Rainbow Flick lassen sich im Pausemenü unter **Einstellungen → Steuerung** frei belegen
(auch Maustasten). Dort gibt es außerdem Vollbild (nur im Build), VSync, FPS-Anzeige, Bildschirmwackeln,
Leuchten (Bloom) und den Farbsaum-Effekt. Alles wird automatisch gespeichert.

## Code-Überblick (`SoccerFight/Assets/Scripts`)

| Ordner | Inhalt |
|---|---|
| `Core/` | `Game` (Einstiegspunkt + Update-Reihenfolge), Input, Federn/Easing/IK (`MathUtil`), Hit-Stop & Slow-Mo (`TimeFx`) |
| `Art/` | SDF-Rasterizer (`SdfCanvas`, `Sdf`), Farbpalette, prozedurale Grafiken für Spieler, Ball, Monster, Umgebung (`EnvironmentArt`) und Pflanzen (`FoliageArt`); `ArtJobs` erzeugt den Hintergrund parallel auf Worker-Threads (im Browser nacheinander, siehe `Par`) |
| `World/` | Parallax-Ebenen (`WorldEnvironment`), Vegetations-Meshes mit GPU-Wind (`FoliageLayer` + Shader `SF_Foliage`), lebendige Details wie Wolken, Fledermäuse, Wasserfall, Blätter, Laternen, Geisterlichter (`Ambient`) |
| `Player/` | Bewegung & Fähigkeiten (`Player`), prozedurale Animation mit IK (`PlayerRig`), Regenbogen-Nachbilder |
| `Ball/` | Dribbeln, Schuss, Regenbogen-Bogen, Rückkehr |
| `Enemies/` | Monster (Blob, Wisp) und Wellen-Logik |
| `FX/` | Partikelsystem, Kamera (Follow, Shake, Zoom), Post-Processing |
| `UI/` | HUD (Healthbar, Cooldown-Icons, Banner, Schadenszahlen), Pausemenü mit Einstellungen (`PauseMenu`, Widgets in `UiKit`) |
| `Core/` (Einstellungen) | `KeyBindings` (frei belegbare Tasten), `GameSettings` (Optionen, in PlayerPrefs gespeichert) |
| `DevTools/`, `Editor/` | Screenshot-Tool für automatisierte Prüfung, Szenen-Setup, WebGL-Build (`WebGLBuilder`) |

## Wo man dreht

- **Spielgefühl:** Konstanten oben in `Player.cs` (Tempo, Sprung, Cooldowns, Schaden)
- **Timing von Schuss & Flick:** `KickWindup/KickContact/...` und `FlickSet/FlickRoll/...` in `Player.cs`
- **Posen:** `PoseKick` / `PoseFlick` in `PlayerRig.cs`
- **Farben:** `Palette.cs`
- **Kamera:** `BaseSize` (Zoom) und `BaseY` in `CameraRig.cs`
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
Protokoll: `.build/publish.log`, Unity-Log des letzten Builds: `.build/unity-build.log`.
Für die Pushes muss die GitHub-CLI eingeloggt sein (`gh auth login`).

## Schriftart

Inter (SIL Open Font License 1.1) liegt in `SoccerFight/Assets/Resources/Fonts`.
