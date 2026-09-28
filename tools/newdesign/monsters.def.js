// Schnittdaten für monsters.js. Jeder Look hat GENAU EIN Referenzbild (ref), an dem die Koordinaten (Pixel,
// x nach rechts, y nach unten, Ursprung oben links IM AUSGESCHNITTENEN Bildbogen-Halbbild "rechts") gemessen
// wurden. Beim Schneiden einer anderen Stage-Variante desselben Looks werden diese Koordinaten automatisch
// auf die (in sprites.json bereits vermessene) Alpha-Bounding-Box jener Variante umgerechnet — kein erneutes
// Vermessen pro Stage nötig, solange die Pose/Proportion dem Referenzbild ähnelt (die Bild-Prompts verlangen
// genau das: "the EXACT approved ... from reference").
//
// Pro Look:
//  view:    welche Bogenhälfte geschnitten wird (immer 'rechts' — die andere Blickrichtung entsteht im Spiel
//           weiterhin durch Spiegeln, wie schon bei den alten prozeduralen Körpern; siehe Monster.cs body.localScale.x).
//  body:    Umriss des STEHENDEN Rumpfes OHNE die beweglichen Teile (Flügel, Arme, Schwanz-Ansatz...) — bewusst
//           eng gezeichnet, nicht die volle Boundingbox: Auffüllen (inpaint) glättet nur Farbe, nicht Form, ein
//           zu großzügiger Rumpf-Umriss ergäbe an der Trennstelle einen sichtbaren flachen "Fleck" in Teilform.
//           Kleine Übergänge zwischen Rumpf und einem Teil deckt dessen seam (Radius um den Drehpunkt/Anker) ab.
//  parts:   benannte bewegliche Teile. poly = Umriss (eigenes Schnitt-Sprite), pivot = Drehpunkt (muss zum
//           PartDef.Pos-Ankerpunkt in MonsterArt.cs passen, z. B. die Flügelwurzel an der Schulter). seam =
//           Radius der Nahtglättung am Rumpf (Kreis um pivot, Standard 70).
//  chains:  Kettenglied-Kachel (Schwanz/Tentakel-Segment): circle = Kreis-Ausschnitt, anchor = ChainDef.Anchor-
//           Punkt (wo die Kette am Körper beginnt), seam = Nahtradius am Rumpf dort (Standard 60).
//  eyes:    benannte Augen (meistens nur eins: `{ eye: {...} }`; zwei/drei Augen z. B. Splitter/ThornMother/
//           VoidLord: eigener Schlüssel pro Auge, taucht dann als "EyeL"/"EyeR" usw. im Atlas auf). Je Auge:
//           seed = ungefährer Pixel nahe der Augenmitte am REFERENZBILD, color = erwartete Augenfarbe (Fallback,
//           falls die Stage in STAGE_EYE unten fehlt) für die automatische Farberkennung; radiusPad = wie viel
//           breiter als der erkannte helle Kern ausgeschnitten wird (damit die dunkle Pupille mit drin ist);
//           seam = Nahtradius am Rumpf. overrides: { stageKey: [cx,cy,rx,ry] } — manueller Ausschnitt, wenn die
//           Farberkennung für eine bestimmte Stage falsch oder gar nicht greift (am AUSGESCHNITTENEN "rechts"-
//           Halbbild JENER Stage nachmessen, nicht am Referenzbild umrechnen — monsters.js addiert den
//           Halbbild-Versatz selbst; die Koordinaten hier sind relativ zur linken Kante DER HÄLFTE, wie überall
//           sonst in dieser Datei auch).
//           Schlägt für eine Stage weder Erkennung noch Override an, bleibt dort einfach das im Körperbild schon
//           aufgemalte (unbewegte) Auge stehen statt eines falsch platzierten Punkts — Konsole warnt dann.
'use strict';

module.exports = {
    diver: {
        ref: '01-mondlicht',
        // creature height (alpha bbox) in world units, for roughly the old SDF Diver's visual size
        // (Radius = 0.34 * sizeMul in Monster.Spawn, EnemyDef.Size 1 for Diver).
        worldHeight: 0.75,
        // Kopf/Schnauze, Hals, Rumpf bis zum Schwanzansatz — bewusst ohne die Flügelfläche (die beginnt erst
        // hinter dieser Linie am Rücken) und ohne den vollen Schwanz (nur sein Ansatz).
        body: [
            [840, 470], [790, 400], [720, 360], [660, 330], [615, 290], [600, 250],
            [570, 270], [560, 330], [530, 370], [470, 410], [420, 460], [400, 510],
            [390, 545], [430, 565], [480, 550], [560, 515], [630, 485], [700, 465], [790, 470],
        ],
        parts: {
            wing: {
                pivot: [560, 390],
                seam: 85,
                poly: [
                    [560, 390], [545, 330], [460, 240], [350, 150], [285, 105], [320, 190],
                    [230, 230], [160, 300], [200, 360], [260, 340], [170, 430], [250, 410],
                    [350, 500], [440, 460], [510, 430],
                ],
            },
        },
        chains: {
            tail: { anchor: [390, 545], seam: 55, circle: [270, 625, 46] },
        },
        // seam stays small on purpose: the cut Eye sprite is drawn back on top at the same spot and covers
        // it completely, so the hidden/inpainted disc only needs to erase the boundary, not the whole eye —
        // a bigger one just invites Laplace-fill to smear across nearby detailed scale/crack texture.
        eyes: {
            eye: {
                seed: [700, 460], color: '#FFE98A', radiusPad: 1.3, seam: 16,
                // auto-detection missed these three (regen's is a stylised cyan gem-eye on a very differently
                // posed sheet — lantern prop, curled tail; stern/eklipse just drift far enough from the ref
                // pose that the seed lands outside the search window) — measured by hand on each sheet instead.
                overrides: { regen: [705, 368, 32, 24], stern: [675, 375, 32, 24], eklipse: [720, 395, 30, 22] },
            },
        },
    },

    // ENTWURF, nicht geprüft — nur einmal am Referenzbild grob abgemessen (per Augenmaß am Pixel-Raster-Bild,
    // ohne Testlauf). Vor Verwendung: `SF_DEBUG=<ordner> node monsters.js brute` laufen lassen und die
    // *_atlas.png je Stage ansehen (die alte "Fleck"-Falle: hide/seam-Bereiche klein halten, siehe diver oben).
    // Besonderheit bei brute: beide Arme überlappen in dieser Pose sichtbar den Rumpf (der Vorderarm hängt vor
    // dem Bauch) — die Rumpf-Kontur unten wurde versucht, außen um beide Arme herumzugehen, aber gerade dort
    // lohnt ein genauer Blick auf den Atlas, ob am Bauch trotzdem ein Fleck bleibt (dann seam dort vergrößern
    // oder den body-Umriss enger um den sichtbaren Bauchrand ziehen). Zwei eigene Arm-Zuschnitte (nicht wie beim
    // Flügel geteilt): der Vorder- und Hinterarm sehen in der Vorlage unterschiedlich aus (verschiedene Pose).
    brute: {
        ref: '03-regen',
        // Brute ist deutlich wuchtiger als Diver (EnemyDef.Size 1.6 vs 1, Radius = 0.42 * sizeMul für Blob-Typen).
        worldHeight: 1.3,
        body: [
            [830, 420], [770, 465], [700, 475], [650, 455], [600, 390], [580, 320], [520, 295],
            [430, 300], [350, 330], [290, 380], [250, 440], [230, 500], [210, 570], [170, 650],
            [170, 760], [290, 775], [400, 720], [480, 690], [560, 700], [620, 750], [700, 770],
            [760, 720], [770, 620], [750, 500], [720, 430],
        ],
        parts: {
            armFront: {
                pivot: [630, 400],
                seam: 70,
                poly: [
                    [630, 400], [670, 430], [700, 470], [720, 520], [700, 560], [770, 600],
                    [780, 650], [760, 700], [700, 750], [630, 760], [590, 720], [580, 650],
                    [600, 580], [590, 520], [610, 450],
                ],
            },
            armBack: {
                pivot: [350, 340],
                seam: 70,
                poly: [
                    [350, 340], [320, 380], [280, 430], [230, 470], [180, 500], [145, 540],
                    [150, 600], [190, 650], [170, 700], [200, 745], [270, 730], [320, 690],
                    [340, 630], [310, 570], [340, 510], [360, 450], [370, 400],
                ],
            },
            // one back spike, reused (scaled/rotated in MonsterArt.cs) for all three like the old Feature() share
            spike: {
                pivot: [445, 250],
                seam: 40,
                poly: [[445, 250], [410, 220], [400, 180], [430, 135], [460, 175], [480, 220], [470, 250]],
            },
        },
        eyes: { eye: { seed: [745, 400], color: '#7FD4FF', radiusPad: 1.3, seam: 16 } },
    },
};

// Getrennte Dateien erlauben gleichzeitig vermessene Looks ohne konkurrierende Änderungen.
Object.assign(module.exports, require('./monsters.ground.def.js'));
Object.assign(module.exports, require('./monsters.bosses-a.def.js'), require('./monsters.bosses-b.def.js'));
Object.assign(module.exports, require('./monsters.extra.def.js'));
