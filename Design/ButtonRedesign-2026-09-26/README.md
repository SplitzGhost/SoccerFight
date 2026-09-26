# Button-Entwürfe für SportFighter

Ursprünglicher Entwurfsstand: 26.09.2026. Sieben mit dem integrierten Imagegen-Werkzeug erzeugte Konzeptbilder.

## Umsetzung im Spiel

Die ursprüngliche Gestaltung ist inzwischen eingebaut. Nach der Korrektur des Nutzers werden die vereinfachten Flächen wieder durch gemalte Platten mit breiten Facetten, Steinstruktur und eingravierten Spiralen ersetzt. Der Quest-Platzhalter bleibt entfernt. Nur der aktive Navigationstab bleibt hell, die übrigen Symbole sind gedämpft. Funktionen, Preise, Tastenbelegungen und die Auswahl durch Balltreffer bleiben erhalten.

`UI/ButtonSkin.cs` verwendet skalierbare, gemalte Platten aus `Resources/UiButtons/original-plates.png`. Der Originalbogen `01-hauptmenue.png` wurde mit dem integrierten Imagegen-Werkzeug für die Verwendung im Spiel aufbereitet: Beschriftungen und Symbole getrennt, Oberfläche und Verzierungen beibehalten, neutral eingefärbt für Petrol/Gold im Spiel. Zwei weitere transparente Atlanten enthalten die gemalten Menü- und Sportsymbole. Die zugehörigen Layoutdateien beschreiben die tatsächlichen Ausschnitte; sie vermeiden angeschnittene Grafiken und unnötige transparente Ränder. Beschriftungen bleiben echte Spieltexte. Gemeinsam verwendet in `ChunkButton`, `NavTab`, `UiKit`, Entwicklermenü und Belohnungskarten.

Prompt für die Originalplatten: Originalform des SPIELEN-Buttons aus Bild 01 mit breiten gemalten Facetten, sichtbarer Unterkante, kleinen Kerben und allen vier Spiralgravuren exakt beibehalten; Text, Ball und Symbole entfernen. Zwei transparente, neutral silbergraue Platten übereinander: eine gefüllte Buttonfläche und eine passende Platte mit dunklem eingelassenem Zentrum. Keine vereinfachte Vektorform, keine neue Gestaltung. Die Farben werden zur Laufzeit gesetzt.

Unity-Prüfung: Szenarien `menu`, `meta` und `buttons`; Aufnahmen der Originalflächen liegen unter `.build/cap/buttons-original`. Der folgende Abschnitt dokumentiert die ursprünglichen Konzeptbögen.

## Gestaltungsrichtung

Die vom Nutzer hochgeladene Vorlage liefert kantige, kräftige Platten mit abgeschrägten Ecken, gemalten Facetten, sichtbarer Unterkante, dezenten eingravierten Spiralen und großen plastischen Symbolen. Farben aus dem bestehenden UFO-Stadion: Petrol, Schieferblau, gedämpftes Cyan; warmes Gold für Hauptaktionen, Koralle für Verlassen/Löschen, Violett für seltene Belohnungen.

## Bilder und Zuordnung zum bestehenden Code

| Bild | Inhalt | Bestehende UI-Dateien unter SoccerFight/Assets/Scripts/UI |
| --- | --- | --- |
| [01 Hauptmenü](01-hauptmenue.png) | Spielen, sechs Navigationstabs, Quests, Moduswechsel, Spielerwechsel, Währungen und kleine Symbolbuttons | MainMenu.cs, MenuWidgets.cs |
| [02 Spieler und Shop](02-spieler-shop.png) | Sporttabs inklusive gesperrtem Boxen/Tennis, Wählen/Gewählt/Auswählen, Shop, Stufen/Upgrade/Maximum, Preis/Kaufbestätigung/fehlende Münzen, Startauswahl und Zurück | CharacterPage.cs, ShopPage.cs, MetaWidgets.cs, OnboardingPages.cs |
| [03 Optionen](03-optionen.png) | Vier Schalter, drei Regler, zehn Tastenbelegungen, Standard wiederherstellen, Zurück und Tastenaufnahme | SettingsPanel.cs, UiKit.cs |
| [04 Duo und Pause](04-duo-pause.png) | Raum erstellen/beitreten/starten/schließen/abbrechen/verlassen, unterstützendes Codefeld, sämtliche Pauseaktionen | DuoPage.cs, PauseMenu.cs |
| [05 Events und Belohnungen](05-events-belohnungen-zustaende.png) | Mitmachen, gesperrte Events, Belohnungskarten als anklickbare Flächen, Fokus/gedrückt/gesperrt/ausgewählt und Zubehör-Badges | MenuPages.cs, RewardScreen.cs, MenuWidgets.cs |
| [06 Entwickler](06-entwicklermenue.png) | Entwickler-Schalter und Tempo, Heilung/Fähigkeiten/Upgrades/Build/Münzen/Profil, Stagestepper, Stage-/Wellen-/Bossaktionen, Gegnerwahl, Karten und Upgradeliste | DevPanel.cs |
| [07 Probeansicht](07-hauptmenue-probeansicht.png) | Hypothetische Hauptmenüansicht vor der vorhandenen Stadionkulisse | MainMenu.cs, Resources/Menu/StadionUfo.png |

Die vorhandenen Menüs, dynamischen Beschriftungen und Button-Erzeuger wurden im Code geprüft. Für die Farbabstimmung wurden die Stadiontextur und vorhandene Menü-Captures angesehen. Rangliste und Info verwenden die gemeinsame Navigation/Zurück-Familie; zusätzliche eigenständige Aktionsbuttons wurden dort nicht gefunden. Wiederholte Instanzen und wechselnde Preis-/Stufenwerte teilen sich eine Vorlage.

## Vor einem späteren Einbau

Diese Bilder sind Konzeptbögen mit Hintergrund und Beschriftung, keine freigestellten produktionsfertigen Sprite-Atlanten. Die Probeansicht wurde generativ bearbeitet und ist kein Screenshot einer implementierten Version. Das erzeugte Schriftlogo ist kein Vorschlag zur Änderung des bestehenden Logos.

Platte, Symbol, Beschriftung und Zustandsmarkierung später separat aufbereiten. Deutsche Texte weiter zur Laufzeit setzen, damit wechselnde Preise, Stufen und Tastenbelegungen funktionieren. Für unterschiedliche Breiten skalierbare Ränder vorsehen und Symbole bei schmalen Tabs verkleinern. Die gesamte Belohnungskarte bleibt anklickbar; der gezeichnete Wählen-Fußbereich ist keine neue Gameplay-Funktion.

Bei Schaltern müssen Position und AN/AUS eindeutig und überall gleich sein. Der Optionenbogen zeigt noch uneinheitliche Positionen und der Entwicklerbogen rundere Schalter; beim Einbau beide auf dieselbe rechteckige Schalterfamilie vereinheitlichen. Das Maximum-Symbol sollte Fortschrittsabschluss statt fehlenden Besitz anzeigen. Gold auf Standard wiederherstellen im Optionenbogen ist eine Entwurfsabweichung: für den Einbau sekundär in Petrol ausführen. Fokusglühen im Zustandsbogen abschwächen, damit Bloom im Spiel nicht überstrahlt.

Keine neue Unity-Kompilierung erforderlich: ausschließlich Bilder und diese Dokumentation außerhalb des Unity-Projekts hinzugefügt. Bilder visuell geprüft; bisherige Captures nur als Referenzen verwendet.

## Verwendete Prompts

Werkzeug: integriertes Imagegen, kein API-/CLI-Fallback. Bildreferenzen: Nutzer-Vorlage und vorhandene Stadiontextur; ab Bild 02 zusätzlich Bild 01 für die gemeinsame Formsprache. Probeansicht: vorhandener Hauptmenü-Capture, Bild 01 und Nutzer-Vorlage.

### Gemeinsame Bildvorgabe

Use case: ui-mockup. Create a polished high-resolution landscape game UI concept sheet for SportFighter. Reference 1 is the user's button inspiration: faithfully follow its chunky clipped-corner silhouettes, thick extruded bottom edges, matte hand-painted faceted surfaces, subtle carved spiral motifs, big tangible pictorial icons overlapping the top edge, bold condensed lettering. Reference 2 is the actual game's night UFO stadium, palette and atmosphere reference only. Adapt colors to slate blue, deep petrol, muted moonlit turquoise, warm ochre gold primary actions, subdued coral for destructive actions. Strong readable physical depth like the inspiration, not flat glass UI, not generic neon sci-fi, not ornate medieval filigree, not shiny plastic, no childish bubbly shapes. Keep a coherent reusable family across every component. Face colors lighter than backdrop with ivory text on slate and charcoal text on gold; dim cyan bevel highlights and restrained shadows, no excessive bloom. Use neatly spaced isolated front-facing controls at large readable size on a plain midnight-blue presentation background with only faint stadium silhouettes at the bottom; no busy scenes behind controls. Every control fully visible and separated. German labels exactly as specified. This is a concept sheet, not a screenshot or production sprite atlas. 

### 01-hauptmenue

Title: HAUPTMENÜ & NAVIGATION. Large hero gold SPIELEN button with a sculpted silver sports-ball / forward emblem above its left half. Six medium navigation plaques SPIELEN, SPIELER, SHOP, EVENTS, RANGLISTE, OPTIONEN, each with appropriate sport portrait silhouette, chest, calendar, trophy, gear pictorial top icon. A wider QUESTS plaque with clipboard emblem and small BALD badge; a mode selector plaque ERSTE SCHRITTE with mountain/flag icon and inset LEVEL WECHSELN; compact RIO / STÜRMER player-switch plaque with football badge and swap arrows. Bottom row isolated square icon buttons friends (two people), info (i), power, back arrow, plus, close X, and two wallet plaques gold coin 1250 plus and cyan crystal 80 plus. All main menu button types represented, total around 19 controls. Prioritize the tangible shapes in reference 1.

### 02-spieler-shop

Title: SPIELER & SHOP. Four sports tab plaques FUSSBALL with football icon, BASKETBALL with orange ball, BOXEN and TENNIS each desaturated with small padlock and BALD tag. Nine separated action buttons WÄHLEN (cyan tick), GEWÄHLT (gold tick), AUSWÄHLEN, ZUM SHOP (chest), STUFE 3 · WÄHLEN, UPGRADE 4 · 120 with cyan crystal, STUFE 10 · MAXIMUM (subdued locked progression), coin icon 500 (affordable purchase), KAUFEN? 500 (gold purchase confirmation). One subdued coin price 500 in coral with small FEHLENDE MÜNZEN annotation. Large LOS GEHT'S gold button and ZURÜCK slate button. Include inset ownership tick badge and lock badge as separate accessories. Sports emblems should be chunky painterly objects like reference.

### 03-optionen

Title: OPTIONEN & STEUERUNG. Show complete practical settings layout as a spacious concept board, all controls in the same painted beveled plaque system as reference, avoid modern pill switches. Left column four full-width plaque switch rows VOLLBILD, VSYNC, FPS ANZEIGEN, FARBSAUM-EFFEKT; use carved rectangular inset socket with square beveled physical slider/toggle handle, clear AN and AUS with tick or dash, mix on and off states. Three real slider rows BILDSCHIRMWACKELN 100%, LEUCHTEN (BLOOM) 100%, LAUTSTÄRKE 80%, recessed dark channel, muted turquoise filled segments, chunky diamond thumb. Right column 10 mapping rows with small sculpted key buttons: NACH LINKS / A, NACH RECHTS / D, SPRINGEN / LEERTASTE, DURCHFALLEN / S, SCHUSS / LINKSKLICK, KLASSEN-FÄHIGKEIT / RECHTSKLICK, FÄHIGKEIT 1 / SHIFT, FÄHIGKEIT 2 / STRG, FÄHIGKEIT 3 / MAUS 5, FÄHIGKEIT 4 / MAUS 4. Bottom STANDARD WIEDERHERSTELLEN reset plaque, ZURÜCK back plaque, and small highlighted NEUE TASTE DRÜCKEN capture-state key plaque. Labels readable, dense but spacious high-resolution wide landscape.

### 04-duo-pause

Title: ONLINE-DUO & PAUSE. Two balanced groups of separated buttons. ONLINE-DUO: RAUM ERSTELLEN large slate-turquoise plaque with two tangible player silhouettes; BEITRETEN with forward emblem; DUO STARTEN large gold with two sports balls; RAUM SCHLIESSEN coral; ABBRECHEN desaturated coral; VERLASSEN coral; a recessed editable six-character RAUMCODE plaque displaying AB12CD (supporting input, not action). PAUSE: WEITER gold with play emblem; EINSTELLUNGEN with gear; NEU STARTEN with circular arrow; HAUPTMENÜ with stadium emblem; DEVELOPER-MODUS with tools; BEENDEN coral with power icon. Also a pair of small back-arrow and close-X square plaques. Same chunky reference silhouette and painted bevels, no huge metal trim.

### 05-events-belohnungen-zustaende

Title: EVENTS, BELOHNUNGEN & ZUSTÄNDE. Top show two separate event buttons MITMACHEN with chunky lock emblems and BALD tags, one muted turquoise for WOCHEN-CHALLENGE and one muted violet for BOSS-RUSH. Middle show five selectable tall reward card buttons (entire card is clickable) using dark painted faceted plaque borders and large sculpted symbolic icons: common gray card, rare cyan card, epic violet card, legendary gold card, ability mint card. Simple labels GEWÖHNLICH, SELTEN, EPISCH, LEGENDÄR, FÄHIGKEIT, each includes WÄHLEN footer and keyboard badge 1, 2 or 3; represent generic template without inventing actual upgrade names or stats. Bottom a state study of the SAME SPIELEN button four times with small external labels NORMAL, FOKUS, GEDRÜCKT, GESPERRT: normal thick shadow, focus slightly brighter bevel, pressed thinner bottom depth shifted down, disabled gray and lock; and same WÄHLEN button twice with external labels NORMAL and AUSGEWÄHLT, latter gold with tick. No white flash. Include separate small BALD and NEU badge accessories. Style closely like reference 1, not an ornate collectible card game.

### 06-entwicklermenue

Title: ENTWICKLERMENÜ. Spacious dense grid of front-facing separate small utility plaques, all in the same handpainted clipped-corner beveled family, minimal icons only when useful. Four toggle-row buttons UNVERWUNDBAR, KEINE ABKLINGZEITEN, EIN-TREFFER-KILLS, INFO-ANZEIGE; one SPIELTEMPO slider. Utility actions VOLLE HEILUNG, ALLE FÄHIGKEITEN FREISCHALTEN, +5 ZUFÄLLIGE UPGRADES, BUILD LEEREN, +500 MÜNZEN, PROFIL LÖSCHEN (muted coral). Stage stepper minus, STAGE 3, plus. Actions ZU STAGE SPRINGEN (gold), WELLE ÜBERSPRINGEN, BOSS RUFEN, ALLE GEGNER BESIEGEN. Three spawn tabs NORMAL, ELITE, MINIBOSS. Three wide action plaques UPGRADE-KARTEN, BOSS-KARTEN (SELTEN+), FÄHIGKEIT WÄHLEN. One compact selectable upgrade-list-row template with small icon and label UPGRADE; selected variation outlined muted gold. Small close X. Keep every label legible. This sheet completes the less visible internal button set.

### Probeansicht

Use case: ui-mockup. Create a convincing 16:9 visual mockup of the actual SportFighter main menu. Image 1 is the actual screenshot edit target, preserve its UFO stadium background, central red-shirted soccer character, ball, title MONDNACHT and overall layout. Image 2 is the newly designed button family, use its chunky faceted hand-painted slate-turquoise plaques, carved subtle swirls, thick bottom depth, big silver pictorial symbols and warm gold primary button. Image 3 is the user's original inspiration, fidelity to its physical beveled button shapes. Replace the flat existing UI controls in image 1 with this new button style while keeping the scene and figure unchanged. Top navigation: compact plaques SPIELEN, SPIELER, SHOP, EVENTS, RANGLISTE, OPTIONEN; mini pictorial icons contained above their labels but keep top bar modest and do not occlude UFO. Top right retain compact currency and icon buttons. Left QUESTS panel must become a painted slate-turquoise beveled tablet with clipboard emblem overlapping top edge, preserve its three readable quest rows and reward counters, small BALD tag. Player selector RIO STÜRMER above character becomes small physical plaque. Right mode ERSTE SCHRITTE plaque with mountain/flag emblem and inset LEVEL WECHSELN. Below it big warm ochre SPIELEN plaque with silver football/play emblem, exactly the style of image 2 but sized to fit screenshot layout. No giant logo additions, no new scene objects, no changed character, no reinvented background. Keep original background atmosphere and readable button contrast. This is a hypothetical art direction preview, not a running game screenshot.
