# AGENTS.md – gemeinsame Anleitung für alle KI-Helfer

An diesem Projekt arbeiten **zwei KIs**: Claude (Claude Code) und ChatGPT Codex. Beide lesen diese Datei
(Claude über `CLAUDE.md`, das hierher verweist). Projektwissen und Regeln gehören **hierher**, nicht in einen
privaten Speicher, damit die andere KI sie auch kennt.

## Der Nutzer

- Schreibt Deutsch → **immer auf Deutsch antworten**, Kommentare und Commit-Nachrichten auch auf Deutsch.
- Kein Profi-Programmierer: Dinge kurz und verständlich erklären, keine Fachwort-Wände.
- Priorität: **Grafik, flüssige Animation, saubere Effekte, hohe FPS** vor Gameplay-Tiefe.
  Flache/minimalistische Optik wurde mehrfach abgelehnt; Ziel ist Ori-/Hollow-Knight-Niveau
  (viele Ebenen, viel feine Bewegung). Menüs, die „KI-generiert“ oder „kindisch“ aussehen, wurden verworfen.

## Das Spiel: SportFighter (Repo-/Ordnername noch SoccerFight)

2D-Side-View-Roguelite: Sportler (Fußball, Basketball; Boxen und Tennis folgen) kämpfen mit ihrem Ball gegen
Monster-Wellen. Stage → Wellen → Boss, Upgrade-Karten, 3 Klassen pro Sportart, Meta-Fortschritt mit Münzen,
Online-Duo per Raumcode. Details stehen im `README.md` – **das README ist die Spielbeschreibung und muss bei
Spieländerungen mitgepflegt werden.**

- Unity **6000.6.0f1**, URP 2D, nur das neue Input System. Unity-Projekt liegt in `SoccerFight/`.
- **Alles ist Code:** Grafik, Animation, Effekte und UI werden zur Laufzeit prozedural erzeugt, es gibt keine
  importierten Sprites. Die Szene `Assets/Scenes/Game.unity` enthält nur ein GameObject mit `Game`.
- Skripte: `SoccerFight/Assets/Scripts/` (Namespace `SoccerFight`), Ordner Art, Ball, Combat, Core, Enemies, FX,
  Meta, Net, Player, Run, UI, World, Editor, DevTools.
- Live im Browser: https://splitzghost.github.io/SoccerFight/ (WebGL, Branch `gh-pages`).
- Design-Dokument (Balance, Upgrades, Stage-Themen) pflegt Claude als Artifact – Änderungen an `Difficulty`,
  Upgrades oder Stage-Themen im Commit klar benennen, damit es nachgezogen werden kann.

## Zusammenarbeit (wichtig!)

**Ordner und Branches**

| Ordner | Branch | Wer |
|---|---|---|
| `C:\Users\Master\SoccerFighMaster` | `main` | Claude + Unity-Editor des Nutzers |
| `C:\Users\Master\SoccerFight-codex` | `codex` | Codex |

- `main` ist **live**: Nach jeder Claude-Antwort committet und pusht `tools/publish.ps1` (Stop-Hook) alles im
  Hauptordner und baut die Webseite neu. Deshalb arbeitet Codex **nie** im Hauptordner, sondern in seinem eigenen
  Worktree auf `codex`.
- Muss Codex doch einmal im Hauptordner arbeiten: zuerst eine leere Datei `.codex-busy` im Hauptordner anlegen,
  am Ende wieder löschen. Solange sie existiert, pausiert `publish.ps1`.
- **`tools/publish.ps1` nie selbst starten**, nicht auf `main` pushen, `gh-pages` nie anfassen.
- Branch `redesign` (Tag `design-alt`) ist ein verworfener Design-Versuch – nicht mergen, nicht anfassen.

**Ablauf**

1. Vor dem Start: `git log --oneline -15` lesen – die andere KI hat vielleicht gerade etwas geändert.
   Codex holt vorher den neuesten Stand: `git merge main` im Codex-Ordner.
2. Kleine, abgeschlossene Aufgaben. Nach jeder Aufgabe **ein Commit mit aussagekräftiger deutscher
   Nachricht** (was und warum), z. B. `Basketball: Dunk-Druckwelle trifft jetzt auch fliegende Gegner`.
   „Auto-Update: …“-Commits kommen nur vom Hook.
3. Fertig → der Nutzer (oder Claude) merged `codex` nach `main`, dann geht es live.
4. Nicht gleichzeitig dieselben Dateien bearbeiten. Große Dateien, die oft beide betreffen:
   `Player.cs`, `Monster.cs`, `RunDirector.cs`, `Upgrades.cs`, `MainMenu.cs`, `Game.cs`.
5. Szenen, Prefabs und `ProjectSettings` nur ändern, wenn es wirklich nötig ist (schwer zu mergen).
   `.meta`-Dateien immer mit committen; neue Skripte brauchen eine `.meta` (Unity legt sie an, sonst
   eine mit neuer GUID schreiben).
6. Die andere KI kann reviewen: „Prüf den letzten Commit / den Branch `codex` auf Bugs“ ist ausdrücklich
   erwünscht.

## Technische Regeln und Fallstricke

- **Zeilenenden LF.** Keine Dateien mit PowerShell `WriteAllLines`/`Set-Content` umschreiben (erzeugt CRLF
  und Ganz-Datei-Diffs).
- **WebGL hat keine Threads:** `Par.Run`/`Par.For` (`ArtJobs.cs`) statt `Task.Run`/`Parallel.For`.
  Laufzeit-Grafik geht über `ArtQueue`.
- Domain Reload beim Play ist aus → statische Felder über `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`
  zurücksetzen.
- UI-Animation mit `TimeFx.UiDelta` (sonst laufen Screenshots in Captures auseinander).
- Alle Monster-KI zielt auf `Decoys.Focus(...)`/`Monster.focus`, nie direkt auf `player.Pos`.
- UI mischt im linearen Farbraum: schon 5 % Akzentfarbe über dunklem Glas wirkt stark (Waschungen ~1 %).
  Weiße Blitze/additive Glows überstrahlen mit Bloom schnell – gedämpft halten.
- Spielstand: ein JSON in PlayerPrefs `sf_profile`. Tests/Captures nutzen `Profile.UseTransient()`.
- `ProjectSettings` productName bleibt absichtlich `SoccerFight` (sonst geht der WebGL-Spielstand verloren).
- `Inspiration/` ist gitignored (urheberrechtlich geschützte Referenzbilder) – ansehen ja, nie committen.
- Online-Duo: Code in `Assets/Scripts/Net/` (`NetLink`, `Coop`, `RemotePlayer`, Partials `*.Net.cs`),
  host-autoritativ über Unity Relay (wss). Änderungen an Player/Monster/Ball/RunDirector auf Duo-Sync prüfen.

## Prüfen, ob es kompiliert und aussieht

Der Nutzer hat meist den Unity-Editor offen, der das Projekt sperrt. Deshalb:

- `powershell -ExecutionPolicy Bypass -File tools\capture.ps1 -Scenario <name> -Out <ordner>` spiegelt das
  Projekt nach `.build/project` (im jeweiligen Arbeitsordner), startet Unity im Batchmode, spielt ein
  Szenario ab und schreibt Screenshots + `unity.log`. Kompilierfehler: `error CS` im Log.
  Szenarien u. a.: `quick`, `portrait`, `moves`, `menu`, `vista`, `run`, `themes`, `sim`, `meta`, `hoops`,
  `look`, `bestiary`, `layouts`. Das erste Mal in einem neuen Ordner dauert es lange (~20 min Import).
- Kompilierfehler im offenen Editor: `SoccerFight/Logs/Editor.log` nach `error CS` durchsuchen.
- **Nie** den Kopf des Editor.log oder die Kommandozeilen von Unity-Prozessen ausgeben – dort steht ein
  Zugangstoken.
- Unity-Pfad: `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`.
