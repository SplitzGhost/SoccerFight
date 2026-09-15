# SoccerFight

2D-Side-View-PvE: Ein Fußballspieler kämpft mit seinem Ball gegen Monster-Wellen.
Alles (Grafik, Animation, Effekte, HUD) wird zur Laufzeit im Code erzeugt, es gibt keine importierten Sprites.

## Starten

1. `Assets/Scenes/Game.unity` öffnen
2. Play drücken

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

## Code-Überblick (`Assets/Scripts`)

| Ordner | Inhalt |
|---|---|
| `Core/` | `Game` (Einstiegspunkt + Update-Reihenfolge), Input, Federn/Easing/IK (`MathUtil`), Hit-Stop & Slow-Mo (`TimeFx`) |
| `Art/` | SDF-Rasterizer (`SdfCanvas`, `Sdf`), Farbpalette, prozedurale Grafiken für Spieler, Ball, Monster, Umgebung (`EnvironmentArt`) und Pflanzen (`FoliageArt`); `ArtJobs` erzeugt den Hintergrund parallel auf Worker-Threads |
| `World/` | Parallax-Ebenen (`WorldEnvironment`), Vegetations-Meshes mit GPU-Wind (`FoliageLayer` + Shader `SF_Foliage`), lebendige Details wie Wolken, Fledermäuse, Wasserfall, Blätter, Laternen, Geisterlichter (`Ambient`) |
| `Player/` | Bewegung & Fähigkeiten (`Player`), prozedurale Animation mit IK (`PlayerRig`), Regenbogen-Nachbilder |
| `Ball/` | Dribbeln, Schuss, Regenbogen-Bogen, Rückkehr |
| `Enemies/` | Monster (Blob, Wisp) und Wellen-Logik |
| `FX/` | Partikelsystem, Kamera (Follow, Shake, Zoom), Post-Processing |
| `UI/` | HUD (Healthbar, Cooldown-Icons, Banner, Schadenszahlen), Pausemenü mit Einstellungen (`PauseMenu`, Widgets in `UiKit`) |
| `Core/` (Einstellungen) | `KeyBindings` (frei belegbare Tasten), `GameSettings` (Optionen, in PlayerPrefs gespeichert) |
| `DevTools/`, `Editor/` | Screenshot-Tool für automatisierte Prüfung, Szenen-Setup |

## Wo man dreht

- **Spielgefühl:** Konstanten oben in `Player.cs` (Tempo, Sprung, Cooldowns, Schaden)
- **Timing von Schuss & Flick:** `KickWindup/KickContact/...` und `FlickSet/FlickRoll/...` in `Player.cs`
- **Posen:** `PoseKick` / `PoseFlick` in `PlayerRig.cs`
- **Farben:** `Palette.cs`
- **Kamera:** `BaseSize` (Zoom) und `BaseY` in `CameraRig.cs`
- **Glow/Bloom:** `PostFx.cs` und die Material-Intensitäten in `Art.cs`
- **Wind:** Stärke von Neigung und Böen in `WorldEnvironment.Update`, Wellenform im Shader `SF_Foliage`
- **Pflanzendichte:** die Schleifen in `WorldEnvironment.BuildNear/BuildGround/BuildRuins`

## Schriftart

Inter (SIL Open Font License 1.1) liegt in `Assets/Resources/Fonts`.
