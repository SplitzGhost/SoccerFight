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
- **Fast alles ist Code:** Animation, Effekte und UI werden zur Laufzeit prozedural erzeugt. **Ausnahme: die Spielwelt**
  jeder Stage besteht aus Bildern, die aus ihrem Stage-Bogen (`Inspiration/StagesNewDesigns/0N-*.png`: oben Szene,
  unten 12 Nahaufnahmen) ausgeschnitten sind: `tools/newdesign/stages.js` (Node, einmal `npm install` im Ordner; was wofür
  dient pro Stage in `stages.def.js`) erzeugt `Assets/Resources/Stages/<id>/*.png` + `stage.json`, geladen von
  `Art/StageKit.cs`. Die Kulisse wird inhaltsbasiert aufgefüllt (`inpaint.js`, ~20 s pro Stage; `--fast` behält sie),
  Einzelteile freigestellt (`cutout.js`), alles per KI hochskaliert (`aiup.js`, Real-ESRGAN in `tools/newdesign/esrgan`,
  nicht im Repo: `realesrgan-ncnn-vulkan-20220424-windows.zip` von github.com/xinntao/Real-ESRGAN/releases dort entpacken;
  der Vulkan-Treiber wird per `VK_ICD_FILENAMES` gefunden; Ergebnisse unter `.cache/ai`). Braucht die Grafikkarte (Claude: ohne Sandbox starten). Neue/bessere Bögen → `stages.def.js` anpassen und neu laufen lassen, nie die
  PNGs von Hand ändern. `tools/newdesign/build.js` liefert nur noch den Pflanzen-Atlas (`Resources/NewDesign`, Gras in Stage 1).
- **Auch die Spielfiguren sind Bilder:** `tools/newdesign/characters.js` schneidet sie aus den Figurenbögen
  (`Inspiration/CharackterNewDesign`, Seitenansicht) in Glieder und schreibt `Resources/Characters/<id>.png/.json`
  (Atlas + Drehpunkte + gemessenes Skelett → `PlayerBody`). Umrisse/Gelenke stehen in `characters.def.js`. Die
  Animation bleibt prozedural (`PlayerRig`, `MenuFigure`). Knöchel liegt immer `PlayerDims.AnkleHeight` (0,13) über der Sohle.
  Anders als die Welt-Bilder haben die Figuren-Atlanten **normales Alpha** (Shader multipliziert, sonst dunkle Nähte
  an den Gelenken) und eine Umriss-Maske `<id>_rim.png` (Sekundärtextur `_RimMask` für das Mondlicht); Menüfigur-Teile
  brauchen das Material `Art.UiFigureMat`.
- Die Szene `Assets/Scenes/Game.unity` enthält nur ein GameObject mit `Game`.
- Skripte: `SoccerFight/Assets/Scripts/` (Namespace `SoccerFight`), Ordner Art, Ball, Combat, Core, Enemies, FX,
  Meta, Net, Player, Run, UI, World, Editor, DevTools.
- Live im Browser: https://splitzghost.github.io/SoccerFight/ (WebGL, Branch `gh-pages`).
- Design-Dokument (Balance, Upgrades, Stage-Themen) pflegt Claude als Artifact – Änderungen an `Difficulty`,
  Upgrades oder Stage-Themen im Commit klar benennen, damit es nachgezogen werden kann.

## Zusammenarbeit (wichtig!)

**Beide arbeiten direkt im Hauptordner** `C:\Users\Master\SoccerFighMaster` auf Branch `main`. Das ist
derselbe Ordner, den der Unity-Editor des Nutzers geöffnet hat – Änderungen erscheinen in Unity, sobald das
Unity-Fenster wieder aktiv wird. `main` ist **live**: `tools/publish.ps1` committet, pusht nach GitHub und baut die
Webseite (WebGL → `gh-pages`) neu. Claude startet es automatisch nach jeder Antwort (Stop-Hook).

Damit sich die beiden KIs nicht in die Quere kommen, gibt es die Markierung **`.codex-busy`** (leere Datei im
Hauptordner, gitignored). Solange sie existiert, pausiert `publish.ps1`, und Claude fängt keine eigene Arbeit an.
Eine Markierung, die älter als 4 Stunden ist, räumt `publish.ps1` als vergessen weg.

**Ablauf für Codex**

1. Prüfen, ob Claude gerade arbeitet: `git status` muss sauber sein (sonst hat Claude noch offene Änderungen →
   den Nutzer fragen). Dann `.codex-busy` anlegen: `New-Item -ItemType File -Force .codex-busy`.
2. `git log --oneline -15` lesen – Claude hat vielleicht gerade etwas geändert.
3. Arbeiten, mit `tools\capture.ps1` kompilieren und Screenshots prüfen (siehe unten).
4. Committen mit aussagekräftiger deutscher Nachricht (`git add -A`, `git commit -m "…"`).
5. `.codex-busy` löschen und veröffentlichen – im Hintergrund, denn der WebGL-Build dauert mehrere Minuten:
   `Remove-Item .codex-busy; Start-Process powershell.exe -WindowStyle Hidden -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File C:/Users/Master/SoccerFighMaster/tools/publish.ps1'`
   Ob es geklappt hat, steht in `.build\publish.log` (`push ok`, später `deployed <sha>`; bei `FAILED` die
   Meldung dem Nutzer zeigen, Build-Fehler stehen in `.build\unity-build.log`).
6. `.codex-busy` auf keinen Fall liegen lassen, auch nicht bei Fehlern oder Abbruch.

**Ablauf für Claude**

- Liegt `.codex-busy` im Hauptordner, arbeitet Codex gerade: nichts ändern, dem Nutzer Bescheid geben.
- Sonst wie gewohnt; der Stop-Hook veröffentlicht.

**Für beide**

- `gh-pages` nie direkt anfassen, nie force-pushen auf `main`, keine Historie umschreiben.
- Branch `redesign` (Tag `design-alt`) ist ein verworfener Design-Versuch – nicht mergen, nicht anfassen.
- Der alte Codex-Ordner `C:\Users\Master\SoccerFight-codex` (Branch `codex`) wird nicht mehr benutzt.

1. Vor dem Start: `git log --oneline -15` lesen – die andere KI hat vielleicht gerade etwas geändert.
2. Kleine, abgeschlossene Aufgaben. Nach jeder Aufgabe **ein Commit mit aussagekräftiger deutscher
   Nachricht** (was und warum), z. B. `Basketball: Dunk-Druckwelle trifft jetzt auch fliegende Gegner`.
   „Auto-Update: …“-Commits kommen nur vom Skript.
3. Nie beide gleichzeitig – dafür ist `.codex-busy` da. Große Dateien, die oft beide betreffen:
   `Player.cs`, `Monster.cs`, `RunDirector.cs`, `Upgrades.cs`, `MainMenu.cs`, `Game.cs`.
5. Szenen, Prefabs und `ProjectSettings` nur ändern, wenn es wirklich nötig ist (schwer zu mergen).
   `.meta`-Dateien immer mit committen; neue Skripte brauchen eine `.meta` (Unity legt sie an, sonst
   eine mit neuer GUID schreiben).
6. Die andere KI kann reviewen: „Prüf die letzten Commits von Codex/Claude auf Bugs“ ist ausdrücklich
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
- `Inspiration/` ist gitignored (Referenzbilder) – ansehen ja, nie committen. Nur die daraus ausgeschnittenen
  Spielgrafiken unter `Resources/NewDesign` gehören ins Repo (vom Nutzer ausdrücklich so gewünscht).
- Jede Stage hat ihre eigene Welt (`World/WorldEnvironment.ApplyStage`, Plattformen `World/PlatformViews.cs`, Layout aus
  den Plattform-Bildern der Stage in `Level.Generate`, deterministisch aus dem Seed → Duo-tauglich). Der Farbfilter
  `ThemeGrade` bleibt neutral. Alle Texturen außer der Kulisse sind vormultipliziert (premultiplied alpha), ihre `.meta`
  schreibt das Skript. Die Bilder einer Stage werden beim Wechsel freigegeben (`StageKit.Unload`). Screenshots aller
  Welten: Szenario `stages`.
- Online-Duo: Code in `Assets/Scripts/Net/` (`NetLink`, `Coop`, `RemotePlayer`, Partials `*.Net.cs`),
  host-autoritativ über Unity Relay (wss). Änderungen an Player/Monster/Ball/RunDirector auf Duo-Sync prüfen.

## Prüfen, ob es kompiliert und aussieht

Der Nutzer hat meist den Unity-Editor offen, der das Projekt sperrt. Deshalb:

- `powershell -ExecutionPolicy Bypass -File tools\capture.ps1 -Scenario <name> -Out <ordner>` spiegelt das
  Projekt nach `.build/project` (im jeweiligen Arbeitsordner), startet Unity im Batchmode, spielt ein
  Szenario ab und schreibt Screenshots + `unity.log`. Kompilierfehler: `error CS` im Log.
  Szenarien u. a.: `quick`, `portrait`, `moves`, `menu`, `vista`, `run`, `themes`, `sim`, `meta`, `hoops`,
  `look`, `bestiary`, `layouts`. Das erste Mal in einem neuen Ordner dauert es lange (~20 min Import).
  Den `-Out`-Ordner unter `.build\` legen (z. B. `.build\cap\menu`) – der ist gitignored, sonst landen die
  Screenshots im Commit.
- Endet `capture.ps1` sofort mit `exit 1, 0 Bilder`, hängt meist noch ein alter Unity-Batchprozess auf `.build/project`:
  ihn beenden (nicht den Editor des Nutzers!) und `.build/project/Temp/UnityLockfile` löschen.
- Kompilierfehler im offenen Editor: `SoccerFight/Logs/Editor.log` nach `error CS` durchsuchen.
- **Nie** den Kopf des Editor.log oder die Kommandozeilen von Unity-Prozessen ausgeben – dort steht ein
  Zugangstoken.
- Unity-Pfad: `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`.
