# SportFighter

2D-Side-View-Roguelite: Sportler aus verschiedenen Sportarten (Fußball, Basketball – Boxen und Tennis folgen) kämpfen sich mit ihrem Ball durch Stages voller Monster-Wellen.
Animation, Effekte und HUD werden zur Laufzeit im Code erzeugt. Die sechs Spielfiguren sind aus den Figurenbögen des Designs ausgeschnitten (`tools/newdesign/characters.js` → `Assets/Resources/Characters`, siehe unten) und werden prozedural animiert. Die Spielwelt (Kulisse, Boden, Plattformen, Ruinen, Bäume, Pflanzen) ist aus den Design-Vorlagen ausgeschnitten (`tools/newdesign/build.js` → `Assets/Resources/NewDesign`) und wird im Spiel zu mitlaufenden Ebenen zusammengesetzt.

## Ein Lauf

- **Fünf Level:** Vor dem Start wird eine endliche Route gewählt. Level 1 ist ab Charakter-Stufe 1 offen und endet
  nach 3 Stages; Level 2 braucht Stufe 3 und hat 4 Stages, Level 3 braucht Stufe 5 und hat 5 Stages, Level 4 braucht
  Stufe 7 und hat 6 Stages, Level 5 braucht Stufe 9 und umfasst alle 8 Stage-Themen. Höhere Level beginnen auf einer
  deutlich stärkeren Schwierigkeitsstufe und zahlen entsprechend mehr Kristalle. Jede Route kann beliebig oft
  wiederholt und gewonnen werden.

- **Stage → Wellen → Boss:** Stage 1 hat 3 Wellen, Stage 2 hat 4, ab Stage 3 sind es 5, danach kommt der Stage-Boss.
  Die Schwierigkeit ist eine durchgehende Kurve (`Difficulty.cs`): Jede Welle +1 Stufe, jede neue Stage beginnt
  0,35 Stufen unter dem Ende der vorigen – die letzte Welle einer Stage ist also etwas härter als die erste der nächsten.
- **Nach jeder zweiten Runde** (Wellen und Bosskämpfe zählen): 3 zufällige Upgrade-Karten (Gewöhnlich / Selten /
  Episch / Legendär), eine davon nimmst du (Taste 1/2/3 oder Klick). 74 Upgrades inkl. Synergien, siehe `Upgrades.cs`.
- **Deine Fähigkeiten bringst du mit:** Neben **Schuss** und **Power-Schuss** (und dem Luft-Rückstoß) startet
  jeder Lauf mit den bis zu **vier Fähigkeiten, die du im Menü ausgerüstet hast** (siehe „Fortschritt“).
  Nach jedem Boss gibt es eine Boss-Belohnung (nur Selten+); ist noch ein Platz frei und besitzt du eine nicht
  ausgerüstete Fähigkeit, darfst du sie für den Rest des Laufs mitnehmen, sonst gibt es eine zweite Belohnungskarte.
  Hinter der Belohnung entsteht schon die Arena der nächsten Stage.
- **Münzen:** Jeder besiegte Gegner lässt Münzen fallen (Elite 4, Miniboss 12, Boss 30, pro Stage +12 %), jede
  geschaffte Stage regnet einen Bonus herab. Sie springen auf den Rasen, glitzern kurz und fliegen dann von selbst in
  den Zähler oben rechts – siehe „Fortschritt“.
- **Kristalle:** Jede geschaffte Welle und jede geschaffte Stage zahlt Kristalle direkt ins Profil. Der Betrag wächst
  innerhalb eines Laufs mit der Stage und wird in höheren Leveln mit ×1,5 / ×2,1 / ×2,9 / ×4 verstärkt.
- **8 Stage-Themen** mit eigener Farbstimmung, Wetter, Monster-Aussehen, Gegnern, Miniboss, Boss und Spezialregel:
  Mondlicht-Ruinen, Bernsteinhain (Windböen), Regenwacht (Blitzeinschläge), Glimmergrotte (Dunkelheit),
  Glutschmiede (Lavageysire), Frostgipfel (Glatteis), Sternengarten (geringe Schwerkraft), Eklipse (Verstärkungs-Pulse).
  Danach beginnt der Zyklus von vorn („II“, „III“ …) mit weiter steigender Kurve.
- **Gegner:** 9 Verhaltensarten (Hüpfer, Sturzflieger, Teiler, Spucker, Koloss, Schemen, Laterne, Bombe …), jede mit
  eigenem Körper (Kröte mit Stielaugen, Panzerbuckel mit Fäusten, Quallenlaterne, Kapuzengeist …), Elite-Gegner mit
  Eigenschaften (Flink, Gepanzert, Regenerierend, Instabil, Rasend), Minibosse in den späten Wellen und acht Bosse mit
  Phasen und ganz eigener Gestalt (Düsterkönig, Dornenmutter, Sturmlaterne, Kristallwächter, Magmakoloss, Frostwyrm,
  Kometen-Orakel, Leerenfürst).
- **Bosse:** Jeder Boss bringt nur so viele Begleiter mit, wie `Difficulty.BossAdds` (0,5) erlaubt – ein Faktor für
  alle Bosse, damit sie weiter aufeinander aufbauen (Beschwörung und Wutphase: 1 / 2 / 2 statt 2 / 3 / 4 Monster,
  Nachschub aus den Toren halb so oft und nur bis 3 Gegner). Vor Sprung, Sturmlauf, Salven, Rundumschuss und
  Teleport zeigt eine kleine rot leuchtende Markierung, **wann** der Angriff kommt (sie füllt sich und blitzt auf)
  und **wo** (Landepunkt, Laufbahn, Ring, Zielort) – `Enemies/BossTells.cs`. Minibosse bekommen sie auch.
- **Upgrades sieht man** (`Player/UpgradeVisuals.cs`): Schadens-Upgrades machen den Ball bis zu 30 % größer
  (auch seine Trefferfläche), Feuer, Frost, Kettenfunke und Explosionen geben ihm Glühen, Partikel und eine passende
  Schweif-Farbe, schnellere Bälle ziehen einen längeren Schweif, Echo-Bälle kreisen um den Ball am Fuß, der goldene
  Schuh glänzt vor seinem Goldschuss, das Kapitänsschild ist eine sichtbare Blase, Tempo-Upgrades lassen die Schuhe
  heller leuchten und Streifen ziehen, Sprung-Upgrades puffen beim Absprung, Regeneration funkelt grün, Blutrausch
  glüht rot. Verlangsamte Gegner bekommen einen leichten Eisüberzug mit wachsenden Eiskristallen, eingefrorene
  einen stärkeren, brennende glühen und züngeln.

## Sportarten: Fußball und Basketball

SportFighter hat mehrere Sportarten mit denselben drei Klassen (Angreifer/Stürmer, Verteidiger, Skiller); Boxen und
Tennis stehen schon als „kommt bald“ im Spielermenü. Welt, Monster und Ablauf eines Laufs sind für alle gleich, jede
Sportart bringt aber ihre eigenen Moves, Boss-Fähigkeiten und Upgrade-Karten mit (`Run/Characters.cs`, `Sport`).

**Basketball** (`Player/Player.Hoops.cs`, Posen in `Player/PlayerRig.Hoops.cs`): größere Spieler mit eigenen
Knochenlängen (`PlayerBody`) in Tanktop mit Nummer, Shorts, Crew-Socken und High-Tops. Beim Laufen und Stehen wird der Ball automatisch gedribbelt (ein Aufsetzer pro Schritt, reine Optik), in
der Luft halten ihn beide Hände vor der Brust.

| | Spieler | Rechtsklick |
|---|---|---|
| Angreifer | **DRE** (Rot-Schwarz, Stirnband, Armsleeve, #3) | **Dreier:** Step-back, Sprungwurf im hohen Bogen genau aufs Fadenkreuz, kleine Explosion beim Einschlag („+3“) |
| Skiller | **NOVA** (Türkis-Magenta, Zöpfe, #11) | **Crossover:** zweimal durch die Beine, danach 3 s schneller, und alle Würfe fliegen durch Gegner hindurch |
| Verteidiger | **TITAN** (Navy-Gold, Bart, Knieschoner, #34) | **Dunk:** springt zum Fadenkreuz (bis 7,5 m) und hämmert den Ball in den Boden – Druckwellen in alle Richtungen mit Rückstoß und Schaden |

Linksklick ist ein **Wurf**, der wie der Schuss zurückkommt; ein Wurf in der Luft stößt ab (**Bodenpass**, der
Doppelsprung). Boss-Fähigkeiten: **Alley-Oop** (hoch, kurz hängen, auf den nächsten Gegner), **Block** (Sprung mit
hochgerissenen Armen, Geschosse fliegen zurück) und **Fastbreak** (unverwundbarer Sprint, Gegner werden umgeworfen).
77 eigene Upgrade-Karten (die passenden Fußball-Karten umbenannt, dazu Karten für jeden Move sowie z. B. Heiße Hand,
Brettwurf, Downtown, Skywalker, Splash Zone). Treffer und Effekte laufen über `Combat/Court.cs`.

## ▶ Im Browser spielen

**https://splitzghost.github.io/SoccerFight/**

Die Seite wird nach jeder Änderung automatisch neu gebaut (siehe [Veröffentlichen](#veröffentlichen)).
Am besten am Desktop mit Maus und Tastatur spielen.

## In Unity starten

1. Den Ordner `SoccerFight/` mit Unity 6000.6.0f1 öffnen
2. `Assets/Scenes/Game.unity` öffnen
3. Play drücken

Die Szene enthält nur ein GameObject mit der Komponente `Game`, der Rest wird beim Start aufgebaut (~2 s).

## Hauptmenü

Das Spiel startet im Titelbildschirm. Sein Hintergrund ist **ein Ort in der Spielwelt selbst**, nicht die Arena und
kein gemaltes Bild: Die Spielkamera filmt ihn mit denselben Materialien, demselben Wind, Leuchten und Farbton wie
die Arenen (`World/MenuVista.cs`, eigene Ebene 25 – solange das Menü offen ist, zeichnet die Kamera nur diese Ebene).
Zu sehen ist die Mondnacht der Mondlicht-Ruinen: ein großer Mond geht in einem Tal auf, darunter die Stadionruine, davor
ein Stück Spielfeld, das von einer Schlucht voller Kristalllicht zerrissen ist. Der Mittelkreis schwebt als Sockel
des Spielers in der Lücke, das letzte Flutlicht lehnt schief darüber und leuchtet ihn an (eine Lampe flackert). Dazu
schwebende Felsen, ein Tor mit einem schwach pulsierenden Monster-Portal, Augen im Dunkeln, Bäume, Moos und Laternen in
den Ecken, Glühwürmchen, Nebel, Fledermäuse und fallende Blätter; jede Ebene neigt sich mit dem Fadenkreuz. Die neuen
Teile zeichnet `Art/MenuScenery.cs` mit den Rezepten der Spielwelt, der Rest sind die Grafiken der Arena.

Aufbau: oben das **Logo aus Mondstein** (`Art/LogoArt.cs` – eigene Blockbuchstaben, dunkle Steinkante, Moos an den
Kanten, Risse mit Kristalllicht; im O dreht sich ein echter Ball), in der Mitte der **gewählte Spieler auf dem
schwebenden Mittelkreis**, der den Ball
hochhält – wer ihn (oder sein Namensschild) abschießt, landet in der Spielerauswahl. Links **Shop, Rangliste,
Freunde**, rechts **Fähigkeiten, Events, Optionen**, oben links das Profil, oben rechts Münzen und Kristalle, Info (und am
Desktop Beenden), darunter der große **SPIELEN**-Knopf. Rangliste, Freunde und Events sind Platzhalter-Seiten
(„kommt bald“), Info erklärt Steuerung und Spielprinzip. **Beim allerersten Start** öffnet das Menü auf der
Starterwahl (siehe „Fortschritt“) und lässt einen erst danach aufs Hauptmenü. **SPIELEN:** das Menü räumt sich weg, der Spieler tritt den
Ball direkt auf die Kamera zu, der Ball füllt das Bild, und hinter dem Aufblitzen beginnt der Lauf. Die Knöpfe und
Seiten sprechen die Sprache der Karten und Anzeigen im Spiel: dunkles Glas, feine Rahmen und Leuchten in einer
Akzentfarbe pro Knopf, weiße Symbole, gesperrte Großbuchstaben; nur SPIELEN ist massiv laternengolden.

Statt eines Mauszeigers gibt es das Fadenkreuz aus dem Spiel. **Knöpfe werden nicht angeklickt, sondern
abgeschossen:** Ein Linksklick tritt einen Ball aus der Ich-Perspektive ins Bild – er startet groß am unteren Rand,
wird auf dem Weg nach hinten kleiner und trifft genau dort, wo das Fadenkreuz stand. Erst beim Aufschlag löst der
Knopf aus; der Ball prallt zurück in Richtung Kamera und fällt mit Schwerkraft, Drall und Luftwiderstand aus dem
Bild, während der Bildschirm dahinter längst wechselt. Ein Schuss ins Leere macht nichts außer fliegen und fallen.
Die Einstellungskarte ist die Ausnahme: Innerhalb der Karte arbeiten Schalter, Regler und Tastenfelder ganz normal,
geschossen wird nur daneben.

Zurück ins Hauptmenü kommt man über **Pause → Hauptmenü** (`Game.ToMenu`).


## Fortschritt: Klassen, Spieler, Fähigkeiten, Münzen und Kristalle

**Charakter-Stufen 1–10** (`Meta/CharacterProgression.cs`): Jeder gekaufte oder als Starter gewählte Charakter
beginnt auf Stufe 1 und wird separat gespeichert. Ist er in der Spieler-Seite gewählt, zeigt sein Knopf das nächste
Kristall-Upgrade. Die Kosten steigen quadratisch. Jede Stufe erhöht allgemeinen Angriff, maximales Leben,
Schadensminderung, Fähigkeits-Abklingzeiten und Wirkungsfläche; kleine Tempoboni kommen dazu. Stufe 10 ist das Maximum.
Die Charakter-Stufen 1 / 3 / 5 / 7 / 9 öffnen die fünf Level.

**Drei Klassen, drei Talente** (`Meta/ClassDefs.cs`, alle Zahlen in `ClassTuning`):

| Klasse | Talent | Wirkung |
|---|---|---|
| Stürmer | **Schussgewalt** | +30 % Schaden mit allen Schüssen (Schuss, Rückpraller, Echo-Bälle, Power-Schuss, Fallrückzieher, Abstoß). Verstärkte Treffer schlagen mit einem heißen **Wuchtstern** ein, die Schadenszahl leuchtet rot |
| Skiller | **Technikmeister** | Trick-Fähigkeiten (Rainbow Flick, Übersteiger, Tunnel, Lockvogel) laufen 35 % schneller ab (gleiche Strecke in kürzerer Zeit), machen +25 % Schaden und geben danach 1,6 s lang +30 % Tempo mit violetten Nachbildern |
| Verteidiger | **Kopfballspezialist** | +40 Leben (160 statt 120), 15 % weniger erlittener Schaden (zusätzlich zu Schienbeinschonern), dafür 10 % weniger Schaden. Nur Verteidiger können den **Kopfball** spielen (+25 % Schaden) |

**Kopfball** (neue Fähigkeit, nur Verteidiger, 4,5 s): Der Fuß lupft den Ball hoch, der Spieler steigt hinein und
köpft ihn wuchtig Richtung Fadenkreuz. Der Getroffene wird 1,1 s betäubt, der Ball springt von seinem Kopf hoch und
kommt zurück. Dazu drei Upgrade-Karten: Lufthoheit (+15 % Schaden), Kopfnuss (längere Betäubung), Flugkopfball
(fliegt durch zwei Gegner).

**Sechs Spieler** (`Run/Characters.cs`), einer pro Klasse und Sportart: RIO, BRUNO und MIRA (Fußball), DRE, TITAN und
NOVA (Basketball), jeder mit einem kleinen persönlichen **Perk** zusätzlich zum Klassen-Talent (z. B. DRE: Dreier
+20 % Schaden, TITAN: +25 Leben und größere Druckwellen, NOVA: längerer Crossover-Boost). Die früheren Shop-Spieler
KAI, ZARA, IVO, TALA, LUNA und NICO sind weg; wer sie gekauft hatte, bekommt die Münzen einmalig zurück (`Profile`, Version 2).

**Erster Start** (`UI/OnboardingPages.cs`): Das Menü öffnet auf *Wähle deinen Spieler* – ein Tab pro Sportart mit je drei
Karten (Porträt, Klasse, Talent, Rechtsklick-Move und Perk); ein beliebiger Spieler ist gratis, *Los geht's* speichert die Wahl.

**Fähigkeiten-Menü** (`UI/SkillPage.cs`): oben die vier Plätze mit ihren Tasten, darunter alle Fähigkeiten. Eine
gekaufte Fähigkeit abschießen legt sie in den nächsten freien Platz, nochmal (oder ihren Platz) abschießen nimmt sie
heraus. Nicht gekaufte zeigen ihren Preis und führen in den Shop; was das Talent der Klasse verstärkt, steht in der Ecke.

**Shop** (`UI/ShopPage.cs`, Logik `Meta/Shop.cs`): Reiter *Spieler* (alle neun, eine Reihe pro Klasse) und
*Fähigkeiten*. Kaufen braucht **zwei Treffer** – der erste macht aus dem Knopf ein pulsierendes KAUFEN?, der zweite
kauft –, damit kein verirrter Ball Münzen ausgibt. Gekaufte Spieler lassen sich gleich dort wählen, gekaufte
Fähigkeiten wandern sofort in einen freien Platz.

**Münzen** (`Run/CoinDrops.cs`, `UI/CoinCounter.cs`, Regeln in `Meta/CoinRewards.cs`): Münzen springen aus besiegten
Gegnern, klingen beim Aufprall, hüpfen, drehen sich und glitzern, heben nach einer halben Sekunde ab und fliegen auf
einer geschwungenen Bahn mit Funkenschweif in den Zähler oben rechts. Erst dort werden sie gutgeschrieben: der Zähler
ploppt, zählt hoch, ein Ring und Funken springen vom Symbol, ein Glockenton steigt mit jeder weiteren Münze eine Stufe
höher. Was bei Laufende noch unterwegs ist, wird sofort ausgezahlt.

**Speicherstand** (`Meta/Profile.cs`): ein JSON-Dokument in PlayerPrefs (im Browser IndexedDB) mit Besitz, Auswahl,
Loadout, Guthaben und Stufen für spätere Upgrades; gespeichert wird nach Käufen, Wellen, Stages, am Laufende und beim
Wechsel ins Menü. **Ton** (`Audio/Sfx.cs`): Die Geräusche werden wie die Grafik beim Start synthetisiert,
Lautstärke unter Optionen → Ton.

**Figuren-Grafik:** Jede Figur ist ein Bogen aus dem Design (`Inspiration/CharackterNewDesign`, nicht im Repo). Das
Node-Skript `tools/newdesign/characters.js` schneidet aus der Seitenansicht Kopf, Hals, Rumpf, Hose, Oberschenkel,
Unterschenkel, Schuh, Oberarm, Unterarm und Hand aus (Umrisse und Gelenkpunkte stehen in `characters.def.js`), gibt
den Gliedern runde, überlappende Gelenkenden, füllt verdeckte Stellen (Rumpf hinter dem Arm) mit dem Grundstoff auf
und legt pro Figur einen Atlas + JSON nach `Resources/Characters`. Das JSON enthält auch das Skelett (Knochenlängen,
Schulter, Hals, Kopf, Zopf-Wurzel), das `PlayerArt` beim Start in den `PlayerBody` der Figur schreibt – das Rig
bewegt die Teile damit wie gehabt (IK, Federn). Miras Pferdeschwanz und Novas Zöpfe schwingen als eigenes Teil nach;
Dre trägt den Kompressionsärmel nur am vorderen Arm. Mitgemessen werden auch Faustlänge und Schuhspitze: Beim Dribbeln und bei
den Basketball-Moves liegt die Faust damit auf dem Ball (sie zeigt zum Ballmittelpunkt, `PlayerRig.Palm/GripNear`), und der
Ball springt vor dem Schuh auf statt darauf. Neue oder bessere Bögen → Umrisse/Gelenke in `characters.def.js`
anpassen und `node characters.js` laufen lassen (`SF_DEBUG=<ordner>` schreibt zusätzlich Prüfbilder).
Die Schnitte an den Gelenken laufen weich aus, das Hosenbein ist an der Hüfte rund, und was am Rand von Unterarm und
Faust nach Hose/Trikot aussieht, wird abgeschabt. Neben dem Atlas entsteht eine Umriss-Maske (`<id>_rim.png`): Der
Shader `SF_Character` setzt das Mondlicht nur an die echte Außenkante, nicht an die Schnittkanten zwischen den Teilen.
Die Atlanten haben normales (nicht vormultipliziertes) Alpha; im Menü zeichnet `SF_UIFigure` die Teile.
Die Farben im **Kit** (`Characters.cs`) färben nur noch Akzente wie das Leuchten unter den Schuhen.

## Steuerung

| Taste | Aktion |
|---|---|
| A / D | Laufen |
| Leertaste | Springen (länger halten = höher), auch durch Plattformen hindurch nach oben |
| S | Durch die Plattform unter dir nach unten fallen (in der Luft gehalten: durch alle Plattformen) |
| Linksklick | Schuss Richtung Mauszeiger (Cooldown 0,45 s). **Gedrückt halten = Dauerfeuer:** geschossen wird, sobald der Ball zurück ist und der Cooldown abläuft. In der Luft stößt dich der Rückstoß in die Gegenrichtung: einmal pro Sprung, nach unten geschossen wie ein Doppelsprung |
| Rechtsklick | Power-Schuss (nur im Stand, Cooldown 3,5 s): langes Ausholen, dann ein gerader goldener Schuss, der durch alle Gegner hindurchfliegt. Macht dafür weniger Schaden (12 statt 18) |
| E / Q / R / F | **Fähigkeit 1 bis 4** – die vier Plätze deines Loadouts (siehe unten) |
| 1 / 2 / 3 | Upgrade- bzw. Fähigkeitskarte wählen |
| Esc | Pausemenü (Weiter, Einstellungen, Neu starten, Hauptmenü, Beenden) |
| F1 | FPS-Anzeige an/aus |
| F2 | VSync an/aus (aus = unbegrenzte FPS) |
| F3 | Developer-Modus (auch im Pausemenü): Unverwundbar, keine Abklingzeiten, Ein-Treffer-Kills, Spieltempo, Stage-Sprung, Welle überspringen, Boss/Gegner rufen, Karten öffnen, jedes Upgrade gezielt hinzufügen, Live-Zahlen. Dev-Läufe zählen nicht für den Rekord |
| Enter | Neuer Lauf nach Niederlage |

### Die vier Fähigkeits-Plätze

Schuss und Power-Schuss liegen fest auf den Maustasten. Alles andere wird **nicht einzeln belegt**: Es gibt
vier Plätze auf **E, Q, R und F**, und das Loadout aus dem Fähigkeiten-Menü liegt der Reihe nach darin – die erste
Fähigkeit auf Platz 1, die zweite auf Platz 2 und so weiter. **Mehr als vier gibt es pro Lauf nicht**; ist das
Loadout voll, bringt jeder Boss eine zweite Belohnungskarte. Elf Fähigkeiten gibt es (eine davon nur für
Verteidiger), drei suchst du dir beim ersten Start aus, die anderen kaufst du im Shop:

| Fähigkeit | Was sie tut |
|---|---|
| Rainbow Flick | Der Ball fliegt im Regenbogen über die Gegner und schlägt mit Flächenschaden ein (Cooldown 6 s) |
| Ball hochhalten | Im Takt tippen, jede Berührung heilt; perfekt getimt doppelt, alle 10 Berührungen ein Bonus |
| Übersteiger | Täuschung über den Ball, dann ein unverwundbarer Dash (~5 m) mitten durch die Gegner (3,5 s) |
| Fallrückzieher | Nur in der Luft: Salto mit Scherenschlag, der Ball explodiert beim ersten Aufprall (5 s) |
| Grätsche | Rutscht flach über den Rasen (4 s): wirft Gegner um, die dann kurz gar nichts tun, und duckt sich unter brusthohen Geschossen weg. Auf Eis rutschst du deutlich weiter |
| Abstoß | Drischt den Ball aus dem Bild (8 s). Nach gut einer Sekunde kommt er als Meteor genau auf dem markierten Punkt herunter (55 Flächenschaden) – in der Wartezeit bist du ohne Ball |
| Mauer | Drei Geister-Spieler stellen sich 4 s lang in den Weg (12 s): sie schlucken Geschosse, halten Gegner auf, und dein eigener Ball prallt von ihnen ab |
| Tunnel | Der Ball geht durch die Beine (6 s): Der Getunnelte taumelt und nimmt 3 s lang 40 % mehr Schaden von allem |
| Lockvogel | Körpertäuschung zur Seite (7 s). Das Nachbild bleibt stehen, die Gegner greifen es an und es platzt am Ende mit einem Stoß |
| Schlusspfiff | Keine Abklingzeit, sondern eine Leiste, die sich mit jedem Sieg füllt: Ein Pfiff friert alle Gegner 2 s ein und lässt ihre Geschosse aus der Luft fallen (nur im Shop) |
| Kopfball | Nur Verteidiger (4,5 s): Ball hochlupfen, hineinsteigen, wuchtig aufs Fadenkreuz köpfen – der Getroffene ist 1,1 s betäubt, der Ball springt zurück |

Bewegung, die beiden Schüsse und die vier Fähigkeits-Plätze lassen sich im Pausemenü unter
**Einstellungen → Steuerung** frei belegen (auch Maustasten). Dort gibt es außerdem Vollbild (nur im Build),
VSync, FPS-Anzeige, Bildschirmwackeln, Leuchten (Bloom), den Farbsaum-Effekt und die Lautstärke. Alles wird automatisch gespeichert.

## Arena

- **Plattformen:** Stage 1 hat immer das klassische Layout – links und rechts je eine Ruinen-Terrasse, dazwischen zwei
  Säulenkapitelle (Höhe ~2,2, ein Sprung vom Rasen) und drei schwebende Felsen (Höhe ~4,1). Ab Stage 2 würfelt jede Stage
  ihr eigenes Layout (`Level.Generate`): mal wenige, mal viele Ebenen, mal kleine, mal breite, mal stillstehende, mal
  seitlich gleitende oder als Aufzug fahrende – in den Stilen des Themas (Terrasse, Kapitell, Moosfels, Kristallplatte,
  Runenblock, Holzsteg an Ketten, Riesenpilz). Alle sind von unten durchspringbar; wer auf einer bewegten steht, fährt
  mit. Blobs springen dem Spieler gezielt hinterher (erkennbar am langen Ducken davor) und hüpfen von der Kante, wenn der
  Spieler unten ist. Ball, Schatten, Gras und Rainbow Flick funktionieren auf jeder Ebene.
- **Die Welt – Mondlicht-Ruinen:** genau nach den Design-Vorlagen. Von hinten nach vorn: Nachthimmel mit funkelnden
  Sternen, die gemalte Kulisse (Vollmond hinter Wolken, Turmruinen, Aquädukte, viele Wasserfälle, Baumkronen), ein
  dunkler Gürtel aus Bäumen, Ruinen und Wasserfällen im Mondlicht, davor eine eigene, langsamer mitlaufende Ruinenreihe
  (Torbögen mit Laterne, Laternengalgen, Mauerreste, Säule, Kristall) – bewusst abgedunkelt und hinter einer dunklen
  Buschreihe, damit sie als Kulisse und nicht als Spielobjekt wirkt –, große grüne Rahmenbäume an beiden Arenaenden, die
  hellgrüne Grasdecke mit nur vereinzelten Grasbüscheln über der moosigen Quadermauer, die nach unten ins Dunkle ausläuft,
  und dunkles Laub nur ganz außen im Vordergrund. Das Spielfeld selbst bleibt ruhig, damit Spieler und Gegner lesbar sind. Jede Ebene läuft unterschiedlich schnell mit der Kamera. Bewegt: Gras und Büsche im Wind (und um
  Spieler und Ball), flackernde Laternen, pulsierende Kristalle mit fallenden Lichtfunken, atmender Mondschein,
  ziehender Nebel, warme Glühwürmchen. Die Plattformen sind schwebende Grasinseln aus Quadersteinen mit Ranken und
  hängendem Kristall (eine trägt das blaue Fußball-Banner) bzw. Grasplatten auf Säulen; die Grafik wird passend zur
  Plattformbreite skaliert. Alle Stages nutzen diese Welt, jede färbt sie über ihre Farbstimmung um.
- **Tiefe:** Himmel, gemalte Kulisse, Baumgürtel, Nebelbänder, Ruinen, Rasen und Vordergrund-Laub bewegen sich je nach
  Entfernung unterschiedlich stark mit der Kamera, horizontal wie vertikal (Faktoren in `WorldEnvironment.AddLayer`).

## Code-Überblick (`SoccerFight/Assets/Scripts`)

| Ordner | Inhalt |
|---|---|
| `Core/` | `Game` (Einstiegspunkt + Update-Reihenfolge), Input, Federn/Easing/IK (`MathUtil`), Hit-Stop & Slow-Mo (`TimeFx`) |
| `Art/` | SDF-Rasterizer (`SdfCanvas`, `Sdf`), Farbpalette, Laden der ausgeschnittenen Spielfiguren (`PlayerArt`), prozedurale Grafiken für Ball, die 17 Monster-Körper (`MonsterArt`), die Welt-Grafik aus den Design-Vorlagen (`DesignArt`), Kulissen-Teile für den Titelbildschirm (`EnvironmentArt`, `DepthArt`, `PlatformArt`), Titelbildschirm (`MenuArt`: Logo-Materialien, Vignette, Ziel-Klammern, Treffer-Formen), Pflanzen (`FoliageArt`); `ArtJobs` erzeugt den Hintergrund parallel auf Worker-Threads (im Browser nacheinander, siehe `Par`), `ArtQueue` die Grafik neuer Stages zur Laufzeit |
| `World/` | Begehbare Geometrie, Plattform-Layouts und -Bewegung (`Level`), Plattform-Darstellung (`PlatformViews`), die Spielwelt aus Parallax-Ebenen (`WorldEnvironment`), Vegetations-Meshes mit GPU-Wind (`FoliageLayer` + Shader `SF_Foliage`), lebendige Details wie Wolken, Fledermäuse, Wasserfälle, Blätter, Laternen, Geisterlichter (`Ambient`), die Szene des Titelbildschirms (`MenuVista`) |
| `Player/` | Bewegung & Fähigkeiten inkl. Hochhalten und Luft-Rückstoß (`Player`), prozedurale Animation mit IK, Bremsen und Drehung (`PlayerRig`), Nachbilder |
| `Ball/` | Dribbeln, Schuss, Regenbogen-Bogen, Rückkehr |
| `Meta/` | Fortschritt über Läufe hinweg, alles datengetrieben: Währungen (`Currencies`), Speicherstand und Geldbörse (`Profile`, `Wallet`), Klassen und ihre Talente (`ClassDefs`, Zahlen in `ClassTuning`), dauerhafte Passive aus Talent/Perk/gekauften Stufen (`Passives`), Fähigkeiten mit Kategorie, Preis und Klassen-Sperre (`SkillCatalog`), Shop-Katalog und Kasse (`Shop`), Münz-Regeln (`CoinRewards`) |
| `Audio/` | Synthetisierte Geräusche und ihr Stimmen-Pool (`Sfx`) |
| `Run/` | Roguelite-Lauf: Zustandsautomat (`RunDirector`), Spielerfiguren mit Klasse, Perk, Preis und Farb-Kit (`Characters`), Münzen im Spiel (`CoinDrops`), Lauf-Zustand (`RunState`), Schwierigkeitskurve (`Difficulty`), Upgrade-Datenbank und Kartenziehung (`Upgrades`), Werte des Builds (`PlayerStats`), Fähigkeiten (`Abilities`), Stage-Themen (`StageThemes`), Spezialregeln und Gefahren (`StageMechanics`), Arena und Körper einer Stage vorbereiten und eintauschen (`StageArt`) |
| `Combat/` | Zentrale Trefferberechnung mit Krits, Brand, Frost, Kettenfunken, Explosionen und Kill-Effekten (`Combat`), Echo-Bälle, Wirbel/Schwarzes Loch, Zwillingssonne, Freistoß-Mauer (`Barrier`), Lockvogel (`Decoys`) |
| `Enemies/` | Monster mit 9 Verhaltensarten, Elite-Eigenschaften, Minibossen und Bossen und einem Rig für alle Körper (Teile, Augen, Ketten; `Monster`, `EnemyDefs`), Gegner-Geschosse, Monster-Pool je Körper und Kollisionen (`WaveDirector`) |
| `FX/` | Partikelsystem, Blitze, Kamera (Follow, Shake, Zoom), Post-Processing (inkl. Eklipse und Dunkelheit) |
| `UI/` | HUD (Healthbar, Build-Leiste, Stage-/Wellen-Anzeige, Boss-Leiste, Namensschilder, gesperrte Fähigkeiten, Stage- und Boss-Intro), Karten-Bildschirm (`RewardScreen`), Upgrade-Symbole (`UpgradeIcons`), Titelbildschirm mit Ball-Beschuss (`MainMenu`), Menü-Spielerfigur (`MenuFigure`), Glas-Knöpfe und Rahmen des Menüs (`MenuWidgets`), Unterseiten (`MenuPages`), Spieler-Kader (`CharacterPage`), erster Start (`OnboardingPages`), Fähigkeiten-Menü (`SkillPage`), Shop (`ShopPage`), deren Karten, Kacheln und Preisschilder (`MetaWidgets`), Münzzähler mit einfliegenden Münzen (`CoinCounter`), Pausemenü (`PauseMenu`), gemeinsame Optionsseite (`SettingsPanel`), Widgets in `UiKit` |
| `World/ThemeGrade` | Farbstimmung pro Stage: eine globale Farbmatrix wirkt nur auf Umgebungsmaterialien (Shader-Eigenschaft `_EnvGraded`), dazu Wetterpartikel |
| `Core/` (Einstellungen) | `KeyBindings` (frei belegbare Tasten), `GameSettings` (Optionen, in PlayerPrefs gespeichert) |
| `DevTools/`, `Editor/` | Screenshot-Tool für automatisierte Prüfung, Szenen-Setup, WebGL-Build (`WebGLBuilder`) |

## Wo man dreht

- **Schwierigkeit:** alle Formeln in `Difficulty.cs` (Leben, Schaden, Tempo, Budget, Elite-Chance, Minibosse, Boss-Werte), Grundwerte der Gegner in `EnemyDefs.cs`, Heilung nach Welle/Boss in `RunDirector`
- **Developer-Modus:** Schalter in `DevMode.cs`, Panel in `UI/DevPanel.cs`, Aktionen (`Dev*`-Methoden) in `RunDirector`
- **Upgrades:** Werte, Beschreibung und Stapelgrenze in `UpgradeDb` (`Upgrades.cs`), Seltenheits-Gewichte in `UpgradeRoller.Weights`
- **Stages:** Name, Farbstimmung, Wetter, Gegner, Plattform-Stile, Boss (samt Körper `Look`) und Regel in `StageThemes.cs`
- **Upgrade-Takt:** `RunState.RoundsPerUpgrade` (heute 2)
- **Monster-Körper:** je eine Funktion pro Körper in `MonsterArt.cs` (Form, Teile, Augen, Ketten, Animationsart)
- **Fähigkeits-Plätze:** Anzahl in `RunState.MaxSkills` (heute 4); welche Fähigkeit auf welcher Taste liegt, ergibt sich aus der Reihenfolge in `RunState.Skills` (`Player.PressSkill`, `Hud.LayoutSlots`)
- **Die späteren Moves:** Timing, Reichweiten und Schaden als Konstanten oben in `Player.cs` (`Tackle*`, `Punt*`, `Wall*`, `Nutmeg*`, `Decoy*`, `Whistle*`), Wirkung in den gleichnamigen `Start`/`Update`-Methoden; Mauer in `Combat/Barrier.cs`, Lockvogel in `Combat/Decoys.cs`, Meteor im `Ball.Punt`-Zustand
- **Schlusspfiff-Leiste:** Ladung pro Kill in `Combat.OnKill`, Wirkung in `Player.BlowWhistle`
- **Spielgefühl:** Konstanten oben in `Player.cs` (Tempo, Sprung, Cooldowns, Schaden)
- **Timing von Schuss & Flick:** `KickWindup/KickContact/...` und `FlickSet/FlickRoll/...` in `Player.cs`
- **Hochhalten:** `Juggle*`-Konstanten in `Player.cs` (Zeitfenster, Heilung, Flughöhen, Reihenfolge Fuß/Knie/Kopf)
- **Luft-Rückstoß:** `AirKickBoost` / `BoostControlTime` in `Player.cs`
- **Skills:** `Power*`, `StepOver*`/`DashTime`/`DashSpeed`, `Bicycle*`/`Blast*` in `Player.cs` (Timing, Cooldowns, Schaden, Explosionsradius)
- **Posen:** `PoseKick` (auch Power-Schuss) / `PoseFlick` / `PoseJuggle` / `PoseStepOver` / `PoseBicycle` in `PlayerRig.cs`, Salto über `BicycleSpin`
- **Spieler-Look:** Umrisse und Gelenke in `tools/newdesign/characters.def.js`, danach `node characters.js`; Mondlicht-Randlicht und Bodenreflex im Shader `SF_Character`
- **Farben:** `Palette.cs`
- **Charaktere:** Namen, Klassen, Perks, Preise und alle Farben in `Run/Characters.cs` (neue Figur = neuer Eintrag, hinten anhängen); das Kartenlayout in `UI/MetaWidgets.cs` (`CharacterCard`, `ShopCharacterCard`), die Figur-Posen (Stehen, Hochhalten, Schuss) in `UI/MenuFigure.cs`
- **Klassen-Talente:** alle Zahlen in `ClassTuning` (`Meta/ClassDefs.cs`); eine neue Klasse = Enum-Wert + `ClassDef` mit Talent (`PassiveDef`)
- **Fähigkeiten-Preise, Gratis-Wahl, Klassen-Sperre:** `SkillCatalog.All` (`Meta/SkillCatalog.cs`); Kopfball-Werte `Header*` oben in `Player.cs`, Pose `PoseHeader` in `PlayerRig.cs`
- **Münzen:** Werte pro Rang, Stage-Aufschlag und Stage-Bonus in `Meta/CoinRewards.cs`; Sprung, Zeigezeit und Flug in `Run/CoinDrops.cs` (`ShowTime`) und `UI/CoinCounter.cs`
- **Neue Shop-Artikel / Währungen / Upgrades:** `ShopKind` + Eintrag in `Shop.Build`; Währung = Eintrag in `Currencies`; kaufbare Stufen = `PassiveDef` mit `MaxLevel` in `MetaPassives.Leveled` (Stufe liegt in `Profile.Level`)
- **Hauptmenü:** Aufbau und Knöpfe in `MainMenu.Build*` (`BuildColumns`, `BuildTopBars`, `BuildPlay`); Logo-Buchstaben und -Farben in `Art/LogoArt.cs`; Szene in `World/MenuVista.cs` (Aufbau, Mondposition, Flutlicht, Portal, Augen), ihre neuen Grafiken in `Art/MenuScenery.cs`; Knopf-, Symbol- und Schriftstil in `Art/MenuArt.cs`; Platzhalter-Seiten in `UI/MenuPages.cs`; Flugbahn und Fall des Balls in `Shoot`/`UpdateShots`/`Land`, der Einstieg ins Spiel in `UpdateTransition`
- **Kamera:** `BaseSize` (Zoom) und `BaseY` in `CameraRig.cs`; wie stark sie der Plattformhöhe folgt in `CameraRig.Target`
- **Plattformen:** klassisches Layout in `Level.Classic`, Generator (Dichte, Größen, Höhen, Bewegung) in `Level.Generate`, Aussehen in `PlatformViews.cs`, Sprungverhalten der Blobs in `Monster.PlanLeap`
- **Welt-Grafik:** Ausschnitte, Maßstab und das Auffüllen der Kulisse in `tools/newdesign/build.js`; Platzierung von Ruinen, Bäumen, Laternen, Pflanzen und die Parallax-Faktoren in `World/WorldEnvironment.cs`; welche Plattform-Grafik bei welcher Breite in `World/PlatformViews.cs`
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
Zusammenfassung), `-Scenario bestiary` (alle Monster-Körper), `-Scenario layouts` (die Arenen aller Stages samt
Mitfahr-Test), `-Scenario blackhole` (Singularität am Zielpunkt), `-Scenario menu` (Titelbildschirm, Ballschuss, Seitenwechsel), `-Scenario newskills` (Grätsche, Abstoß, Mauer, Tunnel, Lockvogel, Schlusspfiff) und `-Scenario sim` (ein Bot spielt einen echten Lauf
und protokolliert jede Phase im Unity-Log).
Protokoll: `.build/publish.log`, Unity-Log des letzten Builds: `.build/unity-build.log`.
Für die Pushes muss die GitHub-CLI eingeloggt sein (`gh auth login`).

## Schriftart

Inter (SIL Open Font License 1.1) liegt in `SoccerFight/Assets/Resources/Fonts`.
