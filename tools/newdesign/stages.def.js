// Was aus jedem Stage-Bogen (Inspiration/StagesNewDesigns/0N-*.png) ausgeschnitten wird und wofür.
//
// Bogenaufbau: oben die Szene (1536 × ~505), darunter 12 Nahaufnahmen (6 × 2 Kacheln, Nummer oben links).
// Koordinaten sind Pixel im Bogen.
//
// scene.walk     Zeile, auf der die Figur in der Szene steht (Oberkante Boden)
// scene.crop     [x0, x1] Ausschnitt der Kulisse (Rahmenobjekte am Rand fallen so gleich weg)
// scene.holes    Rechtecke, die aus der Kulisse entfernt und aufgefüllt werden (Plattformen, Figur, Titel …)
// scene.keep     Rechtecke, die stehen bleiben, aber nicht zum Auffüllen kopiert werden dürfen (Mond, Eklipse …)
// stars          Nachthimmel: der weitergemalte Himmel über der Szene bekommt Sterne
// ground.x       [x0, x1] Streifen des Bodens, aus dem die kachelbare Mauer entsteht
// ground.pad     wie viele Zeilen über der Standlinie die Bodengrafik beginnt (Moos-, Schnee-, Laubkante)
// ground.holes   [x0, x1] Stellen, an denen etwas auf dem Boden steht (wird aufgefüllt)
//
// cells: Nummer (1–12) → [Name, Rolle, Optionen]
//   Rollen: float (schwebende Plattform), stand (steht auf dem Rasen, Oberkante begehbar),
//           prop (Deko; tags: mid = Mittelgrund, back = Hinterkante Rasen, frame = Arenaende, hang = hängt,
//           ceil = hängt von oben, sky = schwebt in der Ferne), end (Bodenabschluss mit Fels)
//   Optionen: glow [r,g,b] (Leuchthof wird entfernt, das Spiel malt ihn), light [r,g,b] (Lichtquelle für den Schein),
//           cut: Freistell-Parameter (siehe cutout.js), walkTop: Pixel ab Oberkante bis zur Lauffläche
// derived: aus Kacheln erzeugte Teile (cap = Plattformkopf ohne Säule, hang = an Seilen/Ketten hängend)
'use strict';

// Freistell-Grundeinstellung (streng: dunkle Objekte vor dunklem Grund bleiben ganz)
const CUT = { tol: 7, loose: 9, grad: 2.2 };
const S = (file, def) => ({ file, cut: { ...CUT, ...(def.cut || {}) }, ...def, cut: { ...CUT, ...(def.cut || {}) } });

module.exports = [
    S('01-Mondlicht-Ruinen.png', {
        id: 'mondlicht', stars: true,
        scene: {
            walk: 412, crop: [340, 1536],
            keep: [[930, 0, 1100, 150]], holes: [[330, 6, 452, 56], [282, 146, 480, 232], [296, 228, 462, 304], [543, 186, 744, 324], [586, 300, 712, 412],
                [826, 248, 1009, 368], [878, 330, 967, 412], [1151, 194, 1350, 302], [1203, 280, 1297, 412]],
        },
        ground: { x: [110, 1340], pad: 7, holes: [[222, 308], [590, 712], [880, 968], [1205, 1298], [735, 805]] },
        cells: {
            1: ['isle_crystal', 'float', { light: [90, 220, 255], cut: { glow: true } }],
            2: ['ped_tall', 'stand', {}],
            3: ['ped_mid', 'stand', {}],
            4: ['ped_short', 'stand', {}],
            6: ['end', 'end', {}],
            7: ['tree', 'prop', { tags: ['frame', 'mid'] }],
            8: ['lantern', 'prop', { tags: ['hang'], light: [255, 170, 60], cut: { glow: true } }],
            9: ['banner', 'prop', { tags: ['hang'] }],
            10: ['arch', 'prop', { tags: ['mid'] }],
            11: ['crystal', 'prop', { tags: ['mid', 'back'], light: [90, 220, 255], cut: { glow: true } }],
            12: ['bush', 'prop', { tags: ['back', 'mid'] }],
        },
        derived: [
            { name: 'isle_cap_a', from: 2, kind: 'cap', role: 'float' },
            { name: 'isle_cap_b', from: 3, kind: 'cap', role: 'float' },
        ],
    }),
    S('02-Bernsteinhain.png', {
        id: 'bernstein',
        scene: {
            walk: 408, crop: [240, 1536], keep: [[1030, 145, 1400, 412]],
            holes: [[236, 6, 392, 56], [236, 0, 500, 172], [232, 170, 487, 248], [284, 236, 424, 412], [274, 340, 452, 412],
                [492, 0, 530, 412], [730, 0, 764, 412], [492, 20, 764, 60], [534, 0, 562, 238], [694, 0, 722, 238], [514, 178, 736, 222],
                [462, 350, 566, 412], [700, 350, 1004, 412], [768, 200, 980, 360], [1338, 322, 1536, 412], [220, 298, 312, 412]],
        },
        ground: { x: [0, 1345], pad: 9, holes: [[225, 305], [270, 450], [470, 560], [700, 1000], [1040, 1200]] },
        cells: {
            1: ['mushroom', 'stand', {}],
            2: ['swing', 'prop', { levels: 'swing' }],
            3: ['stump', 'stand', { light: [255, 160, 40] }],
            4: ['branch', 'prop', { tags: ['mid'] }],
            6: ['end', 'end', {}],
            7: ['tree', 'prop', { tags: ['frame', 'mid'] }],
            8: ['amber', 'prop', { tags: ['back', 'mid'], light: [255, 170, 50], cut: { glow: true } }],
            9: ['chestnut', 'prop', { tags: ['back'] }],
            10: ['sapling', 'prop', { tags: ['mid', 'back'] }],
            11: ['rootarch', 'prop', { tags: ['mid'] }],
            12: ['leaves', 'prop', { tags: ['back'] }],
        },
        derived: [
            // Koordinaten im freigestellten Schaukelbild: Brett und Knoten, ohne Gestellfüße.
            { name: 'plank', from: 2, kind: 'hang', role: 'float', rope: 'rope',
                keep: [[42, 104, 194, 133], [50, 90, 71, 149], [165, 90, 187, 149]], anchors: [60, 177] },
        ],
    }),
    S('03-Regenwacht.png', {
        id: 'regen',
        scene: {
            walk: 407, crop: [130, 1440],
            keep: [[920, 0, 1010, 90]], holes: [[126, 6, 338, 56], [124, 292, 230, 407], [280, 148, 500, 198], [300, 190, 406, 260], [300, 250, 366, 407],
                [562, 170, 604, 407], [718, 170, 756, 407], [562, 178, 756, 268], [562, 302, 756, 324],
                [830, 153, 1022, 188], [842, 153, 882, 407], [972, 153, 1012, 407], [858, 176, 1002, 240],
                [1170, 176, 1280, 407], [1283, 272, 1440, 407], [1376, 176, 1440, 412], [258, 302, 334, 407]],
        },
        ground: { x: [0, 1380], pad: 5, holes: [[258, 334], [300, 366], [560, 760], [840, 1012], [1170, 1280], [1285, 1440], [45, 230]] },
        cells: {
            1: ['ledge', 'stand', {}],
            2: ['scaffold', 'stand', {}],
            3: ['gallows', 'stand', {}],
            4: ['chimney', 'prop', { tags: ['mid'] }],
            6: ['end', 'end', {}],
            7: ['lamp', 'prop', { tags: ['back', 'mid'], light: [255, 180, 80], cut: { glow: true } }],
            8: ['fence', 'prop', { tags: ['back', 'mid'], cut: { pocket: 60 } }],
            9: ['gargoyle', 'prop', { tags: ['frame'] }],
            10: ['spire', 'prop', { tags: ['mid', 'back'] }],
            11: ['window', 'prop', { tags: ['mid'], light: [255, 190, 90] }],
            12: ['drain', 'prop', { tags: ['mid'] }],
        },
        derived: [
            { name: 'beam', from: 3, kind: 'hang', role: 'float', rope: 'chain' },
        ],
    }),
    S('04-Glimmergrotte.png', {
        id: 'grotte',
        scene: {
            walk: 411, crop: [170, 1536],
            holes: [[166, 6, 412, 56], [252, 172, 500, 280], [532, 216, 750, 266], [586, 248, 684, 412], [580, 376, 694, 412],
                [815, 270, 1043, 350], [856, 330, 994, 412], [1165, 220, 1338, 412], [224, 302, 306, 412], [1425, 360, 1536, 412]],
        },
        ground: { x: [0, 1425], pad: 5, holes: [[224, 306], [580, 694], [856, 994], [1165, 1338]] },
        cells: {
            1: ['isle_crystal', 'float', { light: [180, 90, 255] }],
            2: ['mushroom', 'stand', { light: [80, 255, 220], cut: { glow: true } }],
            3: ['ped_vein', 'stand', { light: [180, 90, 255] }],
            4: ['pillar', 'stand', {}],
            6: ['end', 'end', {}],
            7: ['crystal', 'prop', { tags: ['mid', 'back', 'frame'], light: [190, 100, 255], cut: { glow: true } }],
            8: ['mushroom_small', 'prop', { tags: ['back', 'mid'], light: [80, 255, 220], cut: { glow: true } }],
            9: ['stalactite', 'prop', { tags: ['ceil'], cut: { tol: 14, loose: 30, grad: 3.2 } }],
            10: ['bulbs', 'prop', { tags: ['back', 'mid'], light: [80, 255, 220], cut: { glow: true } }],
            11: ['boulder', 'prop', { tags: ['mid', 'back'] }],
            12: ['pool', 'prop', { tags: ['mid'], light: [60, 230, 220] }],
        },
        derived: [
            { name: 'isle_vein', from: 3, kind: 'cap', role: 'float' },
        ],
    }),
    S('05-Glutschmiede.png', {
        id: 'glut', cut: { pocketTol: 6, pocket: 160, grow: 4 },
        scene: {
            walk: 412, crop: [240, 1405],
            keep: [[1040, 60, 1350, 412]], holes: [[236, 6, 372, 56], [272, 192, 480, 242], [284, 228, 336, 412], [418, 228, 472, 412], [328, 232, 428, 292],
                [494, 66, 728, 114], [498, 66, 538, 412], [678, 66, 728, 412], [536, 104, 664, 222], [532, 206, 688, 292],
                [810, 268, 1010, 412], [1036, 326, 1112, 412], [1115, 220, 1335, 278], [1118, 220, 1162, 412], [1288, 220, 1335, 412],
                [226, 302, 302, 412]],
        },
        ground: { x: [0, 1420], pad: 3, holes: [[226, 302], [284, 336], [418, 472], [498, 538], [678, 728], [830, 950], [1036, 1112], [1118, 1162], [1288, 1335], [0, 240]] },
        cells: {
            1: ['table', 'stand', {}],
            2: ['anvil_frame', 'prop', { levels: 'anvil' }],
            3: ['lava_ped', 'stand', { light: [255, 110, 30], cut: { glow: true } }],
            4: ['grate', 'stand', { light: [255, 120, 30] }],
            6: ['end', 'end', {}],
            7: ['cauldron', 'prop', { tags: ['back', 'mid'], light: [255, 110, 30], cut: { glow: true } }],
            8: ['anvil', 'prop', { tags: ['back'] }],
            9: ['furnace', 'prop', { tags: ['mid', 'frame'], light: [255, 130, 30], cut: { glow: true } }],
            10: ['pipe', 'prop', { tags: ['mid'] }],
            11: ['bucket', 'prop', { tags: ['ceil'], light: [255, 110, 30], cut: { glow: true } }],
            12: ['vent', 'prop', { tags: ['back', 'mid'], light: [255, 120, 30], cut: { glow: true } }],
        },
        derived: [
            // Nur die beiden Ketten über dem Amboss; der linke Gestellpfosten ist kein Anker.
            { name: 'anvil_hang', from: 2, kind: 'hang', role: 'float', rope: 'chain',
                keep: [[46, 78, 160, 150]], anchors: [78, 130] },
        ],
    }),
    S('06-Frostgipfel.png', {
        id: 'frost', stars: true, cut: { glow: true, grow: 18 },
        scene: {
            walk: 410, crop: [205, 1360],
            holes: [[201, 6, 332, 56], [272, 178, 482, 412], [538, 220, 748, 412], [842, 252, 1018, 412], [1136, 230, 1328, 412], [224, 302, 304, 412], [1296, 258, 1360, 412]],
        },
        ground: { x: [0, 1300], pad: 9, holes: [[224, 304], [320, 420], [596, 690], [880, 980], [1200, 1270], [0, 200]] },
        cells: {
            1: ['ice_pillar', 'stand', { light: [140, 220, 255] }],
            2: ['rock_ped', 'stand', {}],
            3: ['rune_pillar', 'stand', {}],
            4: ['ice_thin', 'stand', { light: [140, 220, 255] }],
            6: ['end', 'end', {}],
            7: ['monolith', 'prop', { tags: ['frame', 'mid'] }],
            8: ['branch', 'prop', { tags: ['back', 'mid'] }],
            9: ['ice_crystal', 'prop', { tags: ['back', 'mid'], light: [140, 220, 255] }],
            10: ['snow', 'prop', { tags: ['back'] }],
            11: ['ice_boulder', 'prop', { tags: ['mid', 'back'] }],
            12: ['rune_stone', 'prop', { tags: ['mid', 'back'] }],
        },
        derived: [
            { name: 'floe_a', from: 1, kind: 'cap', role: 'float' },
            { name: 'floe_b', from: 4, kind: 'cap', role: 'float' },
            { name: 'floe_rock', from: 2, kind: 'cap', role: 'float' },
        ],
    }),
    S('07-Sternengarten.png', {
        id: 'stern', stars: true,
        scene: {
            walk: 408, crop: [235, 1400],
            keep: [[1080, 20, 1340, 230]], holes: [[231, 6, 398, 56], [280, 160, 455, 278], [532, 170, 720, 352], [808, 242, 982, 368], [1058, 232, 1316, 284], [686, 352, 834, 408], [1340, 330, 1400, 408], [231, 296, 272, 410]],
        },
        ground: { x: [0, 1450], pad: 3, holes: [[190, 262], [686, 834], [0, 240], [1340, 1450]] },
        cells: {
            1: ['slab_crystal', 'float', { light: [255, 170, 220] }],
            2: ['planter', 'float', {}],
            3: ['slab_small', 'float', {}],
            4: ['bar', 'float', {}],
            6: ['end', 'end', {}],
            7: ['armillary', 'prop', { tags: ['frame', 'mid'] }],
            8: ['planter_box', 'prop', { tags: ['back', 'mid'] }],
            9: ['shard', 'prop', { tags: ['sky'] }],
            10: ['tower', 'prop', { tags: ['mid'], light: [255, 210, 110] }],
            11: ['star', 'prop', { tags: ['hang'], light: [255, 210, 110], cut: { glow: true } }],
            12: ['plant', 'prop', { tags: ['back'] }],
        },
        derived: [],
    }),
    S('08-Eklipse.png', {
        id: 'eklipse',
        scene: {
            walk: 412, crop: [230, 1440],
            keep: [[860, 0, 1080, 190], [270, 0, 390, 160]], holes: [[226, 6, 252, 56], [222, 166, 464, 278], [528, 222, 738, 320], [806, 240, 1028, 344], [1066, 228, 1330, 306], [224, 302, 302, 412]],
        },
        ground: { x: [0, 1380], pad: 6, holes: [[224, 302], [0, 230]] },
        cells: {
            1: ['slab_broken', 'float', {}],
            2: ['slab_dark', 'float', { light: [255, 40, 60] }],
            3: ['truss', 'float', {}],
            4: ['ibeam', 'float', {}],
            6: ['end', 'end', {}],
            7: ['floodlight', 'prop', { tags: ['frame', 'mid'], light: [255, 90, 100], cut: { glow: true } }],
            8: ['banner', 'prop', { tags: ['mid', 'hang'] }],
            9: ['obelisk', 'prop', { tags: ['mid'], light: [255, 40, 60], cut: { glow: true } }],
            10: ['stands', 'prop', { tags: ['mid'] }],
            11: ['scoreboard', 'prop', { tags: ['mid'] }],
            12: ['rock', 'prop', { tags: ['sky'] }],
        },
        derived: [],
    }),
];
