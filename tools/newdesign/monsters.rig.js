// Knochen der gemalten Monster (von monsters.js ins JSON geschrieben, im Spiel von MonsterWarp.cs bewegt).
// Koordinaten in Prozent des fertigen Monsterbilds (Resources/Monsters/<stage>/<look>.png): u nach rechts,
// v nach unten, 0–100. Zum Nachmessen und Prüfen: node rigview.js <stage>/<look>,… <ziel.png> zeichnet Raster und
// Knochen auf die Bilder. Im Spiel prüfen: capture.ps1 -Scenario monster-motion (Ruhe, Ausholen, Flug, Steigen,
// Fallen, Landung, Nachfedern, Treffer). Nach Änderungen: node monsters.js (schreibt die JSON neu).
//
// Je Monster eine Liste von Ketten (Knochen von der Wurzel zur Spitze) und Schwellungen:
//   chain(type, pts, w, extra) – type bestimmt die Grundwerte (TYPES unten), pts = Gelenke [u, v] ab der Wurzel,
//     w = Reichweite um die Kette in Prozent (mit poly: weicher Rand außerhalb des Umrisses),
//     extra = eigene Werte, z. B. { poly: [[u,v], …] } (alles im Umriss bewegt sich mit), amp, freq, phase, windup …
//   pulse(center, r, amp, freq, extra) – Schwellung um center mit Radius r (Prozent), amp = relative Größenänderung.
// Winkel positiv = gegen den Uhrzeigersinn (das Bild schaut nach rechts). windup/thrust drehen die Wurzel beim
// Ausholen/Zustoßen: ein nach unten hängender Arm schwingt mit positivem Winkel nach vorn.
'use strict';

// Grundwerte je Art: eigene Bewegung (amp Grad, freq rad/s, lag rad je Knochen) und Federphysik
// (drag Grad je Einheit/s Fahrtwind, inertia Schwung bei Beschleunigung, stiff rad/s, damp Dämpfung).
const TYPES = {
    wing: { amp: 18, freq: 10, lag: 0.7, drag: 1.2, inertia: 4, stiff: 16, damp: 0.5, windup: 28, thrust: 40, speedup: 0.6 },
    tail: { amp: 7, freq: 4.5, lag: 0.9, drag: 5, inertia: 18, stiff: 9, damp: 0.3 },
    tentacle: { amp: 9, freq: 3, lag: 1.0, drag: 6, inertia: 20, stiff: 7, damp: 0.28 },
    cloth: { amp: 5, freq: 2.6, lag: 0.8, drag: 5, inertia: 16, stiff: 7, damp: 0.32 },
    arm: { amp: 3, freq: 2, lag: 0.3, drag: 1.5, inertia: 9, stiff: 10, damp: 0.42, windup: -22, thrust: 32 },
    leg: { amp: 0, freq: 0, lag: 0, drag: 2.5, inertia: 7, stiff: 15, damp: 0.5 },
    sway: { amp: 3, freq: 2.2, lag: 0.5, drag: 2, inertia: 14, stiff: 11, damp: 0.28 },
};

const chain = (type, pts, w, extra = {}) => ({ type, pts, w, ...TYPES[type], ...extra });
const pulse = (c, r, amp, freq, extra = {}) => ({ c, r, amp, freq, phase: 0, charge: 0, ...extra });

const RIGS = {
    // ---------------------------------------------------------------- Irrlicht: Flügel + Schwanz
    'mondlicht/diver': {
        chains: [
            chain('wing', [[50, 50], [42, 25], [22, 5]], 3, {
                poly: [[20, 3], [35, 7], [47, 18], [51, 32], [54, 44], [50, 53], [40, 56], [30, 50], [18, 50], [24, 42], [6, 34], [15, 28], [12, 20]],
            }),
            chain('tail', [[42, 62], [28, 69], [15, 76], [2, 81]], 7),
        ],
    },
    'bernstein/diver': {
        chains: [
            chain('wing', [[47, 42], [43, 18], [18, 6]], 3, {
                poly: [[50, 35], [47, 20], [42, 10], [20, 5], [5, 25], [15, 28], [22, 40], [30, 48], [40, 45], [48, 45]],
            }),
            chain('tail', [[35, 62], [18, 68], [5, 78], [12, 88], [30, 96]], 7),
        ],
    },
    'glut/diver': {
        chains: [
            chain('wing', [[49, 42], [44, 18], [20, 6]], 3, {
                poly: [[52, 38], [48, 20], [42, 10], [22, 5], [5, 25], [15, 28], [22, 40], [32, 48], [42, 46], [50, 45]],
            }),
            chain('tail', [[36, 62], [18, 68], [6, 78], [14, 90], [36, 97]], 7),
        ],
    },
    'regen/diver': {
        chains: [
            chain('wing', [[47, 45], [40, 20], [5, 5]], 3, {
                poly: [[52, 45], [52, 25], [45, 8], [25, 2], [3, 4], [0, 33], [15, 35], [22, 48], [35, 50], [45, 52]],
            }),
            chain('tail', [[38, 64], [28, 68], [22, 78], [22, 88], [30, 97], [45, 99], [58, 92]], 5),
        ],
    },
    'stern/diver': {
        chains: [
            chain('wing', [[47, 55], [43, 25], [38, 5]], 3, {
                poly: [[52, 48], [55, 15], [45, 3], [38, 8], [22, 15], [0, 42], [10, 45], [20, 62], [30, 55], [40, 58], [48, 58]],
            }),
            chain('tail', [[38, 65], [30, 75], [20, 82], [5, 85]], 6),
        ],
    },
    'eklipse/diver': {
        chains: [
            chain('wing', [[50, 45], [45, 20], [15, 15]], 3, {
                poly: [[58, 35], [58, 10], [48, 2], [35, 5], [15, 12], [0, 23], [25, 22], [25, 40], [35, 42], [48, 48]],
            }),
            chain('tail', [[44, 60], [28, 67], [12, 73], [14, 80], [30, 82], [42, 80]], 5),
        ],
    },

    // ---------------------------------------------------------------- Schemen: Umhang + schwebende Hände
    'grotte/shade': {
        chains: [
            chain('cloth', [[47, 52], [40, 66], [28, 76], [15, 85]], 11),
            chain('arm', [[32, 45], [10, 58]], 3, { poly: [[0, 50], [12, 48], [22, 52], [25, 62], [15, 68], [3, 65]], amp: 6, freq: 2.4, windup: -10, thrust: 15 }),
            chain('arm', [[60, 38], [85, 52]], 3, { poly: [[70, 45], [90, 42], [100, 55], [95, 62], [80, 60], [70, 55]], amp: 6, freq: 2.4, phase: 1.6, windup: 10, thrust: -20 }),
        ],
    },
    'frost/shade': {
        chains: [
            chain('cloth', [[45, 55], [35, 70], [22, 82], [8, 92], [12, 100]], 14),
            chain('sway', [[30, 12], [18, 8], [12, 22]], 5),
            chain('arm', [[55, 45], [78, 58]], 3, { poly: [[62, 50], [82, 48], [92, 55], [88, 68], [75, 70], [64, 62]], amp: 6, freq: 2.4, windup: 10, thrust: -20 }),
        ],
    },
    'stern/shade': {
        chains: [
            chain('cloth', [[40, 55], [30, 68], [20, 80], [12, 92]], 16),
            chain('arm', [[60, 40], [85, 52]], 3, { poly: [[72, 48], [80, 40], [92, 45], [95, 58], [85, 62], [75, 58]], amp: 6, freq: 2.4, windup: 10, thrust: -20 }),
            chain('arm', [[52, 48], [68, 68]], 3, { poly: [[58, 60], [68, 57], [78, 62], [78, 75], [65, 75], [58, 68]], amp: 6, freq: 2.4, phase: 1.6, windup: 10, thrust: -20 }),
        ],
    },
    'eklipse/shade': {
        chains: [
            chain('cloth', [[45, 50], [40, 65], [35, 80], [30, 97]], 16),
            chain('cloth', [[38, 35], [20, 33], [3, 35]], 9, { amp: 6, phase: 1 }),
            chain('arm', [[25, 50], [8, 68]], 3, { poly: [[0, 60], [10, 57], [20, 62], [18, 75], [5, 75]], amp: 6, freq: 2.4, windup: -10, thrust: 15 }),
            chain('arm', [[55, 45], [76, 62]], 3, { poly: [[64, 55], [72, 50], [86, 58], [88, 70], [75, 72], [66, 65]], amp: 6, freq: 2.4, phase: 1.6, windup: 10, thrust: -20 }),
        ],
    },

    // ---------------------------------------------------------------- Laterne: Glocke pumpt, Tentakel wehen
    'regen/lantern': {
        chains: [
            chain('tentacle', [[22, 62], [15, 75], [8, 87], [5, 96]], 7),
            chain('tentacle', [[37, 63], [33, 78], [34, 90], [39, 97]], 7, { phase: 1.7 }),
            chain('tentacle', [[58, 62], [62, 75], [62, 87], [58, 96]], 7, { phase: 3.1 }),
        ],
        pulses: [pulse([40, 30], 36, -0.035, 3)],
    },
    'frost/lantern': {
        chains: [
            chain('tentacle', [[18, 58], [12, 72], [8, 85], [8, 95]], 5),
            chain('tentacle', [[26, 60], [24, 75], [22, 88], [24, 97]], 5, { phase: 1.2 }),
            chain('tentacle', [[37, 58], [40, 75], [42, 88], [44, 95]], 5, { phase: 2.4 }),
            chain('tentacle', [[48, 58], [55, 70], [58, 82], [56, 92]], 5, { phase: 3.6 }),
        ],
        pulses: [pulse([32, 32], 30, -0.035, 3)],
    },
    'stern/lantern': {
        chains: [
            chain('sway', [[33, 18], [33, 3]], 5),
            chain('tentacle', [[22, 58], [14, 72], [8, 85], [5, 96]], 5),
            chain('tentacle', [[32, 60], [30, 75], [26, 88], [24, 98]], 5, { phase: 1.2 }),
            chain('tentacle', [[42, 60], [46, 73], [44, 86], [40, 96]], 5, { phase: 2.4 }),
            chain('tentacle', [[52, 58], [54, 70], [52, 80]], 5, { phase: 3.6 }),
        ],
        pulses: [pulse([35, 35], 28, -0.035, 3)],
    },
    'eklipse/lantern': {
        chains: [
            chain('tentacle', [[20, 55], [10, 65], [5, 78], [12, 92]], 5.5),
            chain('tentacle', [[30, 58], [28, 75], [22, 88], [30, 97]], 5.5, { phase: 1.2 }),
            chain('tentacle', [[42, 58], [45, 72], [38, 85], [38, 97]], 5.5, { phase: 2.4 }),
            chain('tentacle', [[52, 55], [60, 68], [58, 82], [55, 92]], 5.5, { phase: 3.6 }),
        ],
        pulses: [pulse([35, 30], 28, -0.035, 3)],
    },

    // ---------------------------------------------------------------- Düsterling: Hörner + Füße
    'mondlicht/hopper': {
        chains: [
            chain('sway', [[36, 27], [35, 5]], 11),
            chain('sway', [[62, 22], [66, 12]], 8, { phase: 1.3 }),
            chain('leg', [[33, 78], [30, 93]], 13),
            chain('leg', [[80, 80], [84, 93]], 11),
        ],
    },
    'bernstein/hopper': {
        chains: [
            chain('sway', [[42, 22], [40, 5]], 10),
            chain('sway', [[68, 20], [71, 12]], 7, { phase: 1.3 }),
            chain('leg', [[32, 68], [30, 84]], 11),
            chain('leg', [[82, 70], [84, 84]], 11),
        ],
    },
    'regen/hopper': {
        chains: [
            chain('sway', [[48, 15], [46, 5]], 7),
            chain('sway', [[70, 17], [72, 8]], 6, { phase: 1.3 }),
            chain('leg', [[45, 64], [44, 76]], 8),
            chain('leg', [[86, 64], [88, 76]], 8),
        ],
    },
    'grotte/hopper': {
        chains: [
            chain('sway', [[42, 22], [40, 5]], 10),
            chain('sway', [[70, 17], [72, 10]], 7, { phase: 1.3 }),
            chain('leg', [[35, 68], [33, 84]], 11),
            chain('leg', [[85, 70], [87, 84]], 11),
        ],
    },
    'glut/hopper': {
        chains: [
            chain('sway', [[58, 18], [57, 3]], 7),
            chain('sway', [[72, 18], [74, 7]], 6, { phase: 1.3 }),
            chain('leg', [[20, 52], [18, 62]], 9),
            chain('leg', [[75, 53], [76, 63]], 9),
        ],
    },
    'frost/hopper': {
        chains: [
            chain('sway', [[58, 20], [58, 4]], 7),
            chain('sway', [[75, 18], [77, 7]], 6, { phase: 1.3 }),
            chain('leg', [[18, 58], [16, 70]], 9),
            chain('leg', [[50, 58], [50, 70]], 9),
        ],
    },
    'eklipse/hopper': {
        chains: [
            chain('sway', [[84, 18], [88, 2]], 7),
            chain('sway', [[68, 14], [67, 5]], 5, { phase: 1.3 }),
            chain('leg', [[17, 52], [15, 64]], 9),
            chain('leg', [[40, 53], [40, 65]], 9),
        ],
    },

    // ---------------------------------------------------------------- Knospling: Knospen wiegen und atmen
    'mondlicht/splitter': {
        chains: [
            chain('sway', [[28, 28], [28, 8]], 11, { amp: 4, freq: 1.6 }),
            chain('sway', [[58, 26], [60, 6]], 11, { amp: 4, freq: 1.6, phase: 2 }),
            chain('leg', [[28, 75], [25, 92]], 11),
            chain('leg', [[80, 78], [82, 92]], 11),
        ],
        // die schlafenden Knospen atmen
        pulses: [pulse([28, 18], 13, 0.04, 1.8), pulse([59, 16], 13, 0.04, 1.8, { phase: 2 })],
    },
    'bernstein/splitter': {
        chains: [
            chain('sway', [[28, 30], [28, 8]], 11, { amp: 4, freq: 1.6 }),
            chain('sway', [[65, 28], [66, 6]], 11, { amp: 4, freq: 1.6, phase: 2 }),
            chain('leg', [[28, 77], [26, 93]], 11),
            chain('leg', [[84, 80], [86, 93]], 11),
        ],
        // die schlafenden Knospen atmen
        pulses: [pulse([28, 19], 13, 0.04, 1.8), pulse([65, 17], 13, 0.04, 1.8, { phase: 2 })],
    },
    'grotte/splitter': {
        chains: [
            chain('sway', [[25, 42], [25, 25]], 10, { amp: 4, freq: 1.6 }),
            chain('sway', [[72, 38], [72, 20]], 10, { amp: 4, freq: 1.6, phase: 2 }),
            chain('leg', [[15, 85], [14, 95]], 8),
            chain('leg', [[88, 85], [90, 95]], 8),
            chain('sway', [[40, 35], [40, 5]], 10, { amp: 1.5, stiff: 14 }),
        ],
        // die schlafenden Knospen atmen
        pulses: [pulse([25, 33], 12, 0.04, 1.8), pulse([72, 29], 12, 0.04, 1.8, { phase: 2 })],
    },
    'stern/splitter': {
        chains: [
            chain('sway', [[30, 28], [30, 5]], 10, { amp: 4, freq: 1.6 }),
            chain('sway', [[62, 18], [62, 2]], 10, { amp: 4, freq: 1.6, phase: 2 }),
            chain('leg', [[42, 80], [42, 90]], 7),
            chain('leg', [[64, 80], [64, 90]], 7),
        ],
        // die schlafenden Knospen atmen
        pulses: [pulse([30, 16], 12, 0.04, 1.8), pulse([62, 10], 12, 0.04, 1.8, { phase: 2 })],
    },
    'eklipse/splitter': {
        chains: [
            chain('sway', [[26, 32], [26, 5]], 10, { amp: 4, freq: 1.6 }),
            chain('sway', [[76, 30], [76, 5]], 10, { amp: 4, freq: 1.6, phase: 2 }),
            chain('leg', [[25, 78], [25, 90]], 9),
            chain('leg', [[80, 78], [80, 90]], 9),
        ],
        // die schlafenden Knospen atmen
        pulses: [pulse([26, 18], 12, 0.04, 1.8), pulse([76, 17], 12, 0.04, 1.8, { phase: 2 })],
    },

    // ---------------------------------------------------------------- Keimling: zwei Blätter, Blattschwanz, Füßchen
    'mondlicht/spawnling': {
        chains: [
            chain('sway', [[38, 32], [20, 22], [2, 18]], 8, { amp: 6, freq: 2.6 }),
            chain('sway', [[40, 30], [55, 15], [68, 5]], 8, { amp: 6, freq: 2.6, phase: 1.1 }),
            chain('sway', [[42, 78], [28, 76], [15, 75]], 9, { amp: 5, freq: 2.2, phase: 0.6 }),
            chain('leg', [[40, 88], [38, 97]], 7),
            chain('leg', [[55, 88], [56, 97]], 7),
        ],
    },
    'bernstein/spawnling': {
        chains: [
            chain('sway', [[32, 25], [20, 12], [10, 4]], 7, { amp: 6, freq: 2.6 }),
            chain('sway', [[36, 25], [46, 15], [54, 8]], 7, { amp: 6, freq: 2.6, phase: 1.1 }),
            chain('leg', [[27, 87], [26, 97]], 7),
            chain('leg', [[46, 87], [47, 97]], 7),
        ],
    },
    'grotte/spawnling': {
        chains: [
            chain('sway', [[35, 32], [20, 15], [3, 3]], 9, { amp: 6, freq: 2.6 }),
            chain('sway', [[37, 30], [48, 20], [58, 15]], 7, { amp: 6, freq: 2.6, phase: 1.1 }),
            chain('leg', [[25, 86], [25, 97]], 7),
            chain('leg', [[52, 86], [52, 97]], 7),
        ],
    },
    'stern/spawnling': {
        chains: [
            chain('sway', [[48, 32], [42, 15], [38, 2]], 7, { amp: 6, freq: 2.6 }),
            chain('sway', [[50, 30], [62, 24], [84, 28]], 7, { amp: 6, freq: 2.6, phase: 1.1 }),
            chain('sway', [[30, 75], [15, 72], [2, 70]], 9, { amp: 5, freq: 2.2, phase: 0.6 }),
            chain('leg', [[37, 86], [37, 97]], 7),
            chain('leg', [[51, 86], [51, 97]], 7),
        ],
    },
    'eklipse/spawnling': {
        chains: [
            chain('sway', [[47, 28], [35, 12], [20, 2]], 8, { amp: 6, freq: 2.6 }),
            chain('sway', [[48, 25], [55, 15], [65, 8]], 6, { amp: 6, freq: 2.6, phase: 1.1 }),
            chain('sway', [[30, 78], [15, 78], [2, 78]], 9, { amp: 5, freq: 2.2, phase: 0.6 }),
        ],
    },

    // ---------------------------------------------------------------- Spucker: Augenstiele, Trichter, Kehlsack schwillt beim Ausholen
    'bernstein/spitter': {
        chains: [
            chain('sway', [[60, 24], [62, 12]], 5, { amp: 6, freq: 2.4 }),
            chain('sway', [[68, 24], [72, 10]], 5, { amp: 6, freq: 2.4, phase: 1.4 }),
            chain('sway', [[78, 28], [95, 20]], 7, { amp: 2, windup: 8, thrust: -10 }),
            chain('leg', [[65, 50], [65, 70]], 7),
            chain('leg', [[35, 50], [25, 70]], 9),
        ],
        pulses: [pulse([80, 38], 15, 0.03, 3, { charge: 0.14 })],
    },
    'grotte/spitter': {
        chains: [
            chain('sway', [[52, 24], [55, 8]], 9, { amp: 5, freq: 2.4 }),
            chain('sway', [[60, 30], [95, 25]], 10, { amp: 1.5, windup: 8, thrust: -10 }),
            chain('leg', [[68, 55], [68, 70]], 7),
            chain('leg', [[20, 55], [20, 70]], 8),
        ],
        pulses: [pulse([70, 45], 16, 0.03, 3, { charge: 0.14 })],
    },
    'eklipse/spitter': {
        chains: [
            chain('sway', [[60, 22], [58, 6]], 5, { amp: 6, freq: 2.4 }),
            chain('sway', [[68, 22], [70, 6]], 5, { amp: 6, freq: 2.4, phase: 1.4 }),
            chain('sway', [[76, 26], [98, 22]], 8, { amp: 2, windup: 8, thrust: -10 }),
            chain('leg', [[62, 50], [62, 70]], 7),
            chain('leg', [[20, 50], [12, 70]], 9),
        ],
        pulses: [pulse([70, 45], 14, 0.03, 3, { charge: 0.14 })],
    },

    // ---------------------------------------------------------------- Bombe: Zündschnur, Füße
    'glut/bomber': {
        chains: [
            chain('sway', [[30, 20], [32, 6], [22, 3], [10, 10]], 5, { amp: 6, freq: 3 }),
            chain('leg', [[18, 85], [18, 97]], 9),
            chain('leg', [[60, 88], [60, 98]], 9),
        ],
    },
    'eklipse/bomber': {
        chains: [
            chain('sway', [[38, 15], [35, 3], [25, 2], [15, 8]], 4, { amp: 6, freq: 3 }),
            chain('leg', [[28, 80], [28, 97]], 7),
            chain('leg', [[52, 82], [52, 97]], 7),
        ],
    },

    // ---------------------------------------------------------------- Koloss: schwere Arme holen aus, Schwanz, Beine
    'regen/brute': {
        chains: [
            chain('arm', [[72, 40], [80, 58], [86, 72]], 9, { phase: 1.2 }),
            chain('arm', [[55, 35], [57, 55], [62, 75]], 11),
            chain('tail', [[22, 55], [10, 57], [0, 57]], 6),
            chain('leg', [[30, 55], [22, 80]], 9),
        ],
    },
    'glut/brute': {
        chains: [
            chain('arm', [[70, 38], [80, 55], [85, 72]], 10, { phase: 1.2 }),
            chain('arm', [[40, 40], [38, 60], [38, 78]], 12),
            chain('tail', [[20, 65], [8, 72], [0, 75]], 6),
            chain('leg', [[25, 65], [18, 85]], 8),
        ],
    },
    'frost/brute': {
        chains: [
            chain('arm', [[72, 42], [80, 65], [86, 85]], 10, { phase: 1.2 }),
            chain('arm', [[40, 48], [38, 70], [38, 90]], 11),
            chain('tail', [[18, 68], [8, 72], [0, 72]], 6),
            chain('leg', [[25, 65], [22, 92]], 8),
        ],
    },
    'eklipse/brute': {
        chains: [
            chain('arm', [[70, 48], [78, 70], [82, 88]], 11, { phase: 1.2 }),
            chain('arm', [[35, 45], [30, 68], [25, 88]], 12),
        ],
    },

    // ---------------------------------------------------------------- Bosse
    'mondlicht/king': {
        chains: [
            chain('cloth', [[30, 32], [15, 46], [3, 63]], 13, { amp: 4 }),
            chain('arm', [[60, 48], [75, 52], [82, 32]], 3, {
                poly: [[68, 45], [72, 28], [78, 25], [86, 30], [82, 40], [78, 58], [74, 70], [70, 65], [66, 56]],
                amp: 4, windup: 18, thrust: -24,
            }),
            chain('sway', [[48, 16], [48, 3]], 10, { amp: 2, stiff: 12 }),
            chain('sway', [[35, 28], [33, 15]], 5),
            chain('leg', [[38, 58], [35, 72]], 8),
            chain('leg', [[60, 60], [62, 72]], 7),
        ],
    },
    'bernstein/thornmother': {
        chains: [
            chain('sway', [[25, 22], [5, 35]], 10, { amp: 3 }),
            chain('sway', [[50, 20], [68, 22]], 10, { amp: 3, phase: 1.5 }),
            chain('arm', [[22, 48], [25, 62], [42, 66]], 8, { amp: 6, windup: 15, thrust: -25 }),
            chain('arm', [[62, 55], [70, 58], [78, 58]], 8, { amp: 6, phase: 1.5, windup: 15, thrust: -25 }),
            chain('leg', [[28, 62], [27, 76]], 7),
            chain('leg', [[52, 62], [53, 76]], 7),
        ],
        pulses: [pulse([35, 22], 22, 0.03, 2, { charge: 0.08 })],
    },
    'regen/stormlantern': {
        chains: [
            chain('sway', [[25, 15], [25, 3]], 6, { amp: 2 }),
            chain('tail', [[13, 62], [12, 75]], 4, { amp: 3, stiff: 11 }),
            chain('tail', [[25, 63], [25, 76], [25, 88]], 4, { amp: 3, stiff: 11, phase: 1 }),
            chain('tail', [[37, 62], [37, 75]], 4, { amp: 3, stiff: 11, phase: 2 }),
        ],
    },
    'grotte/crystalguard': {
        chains: [
            chain('arm', [[25, 38], [15, 55], [12, 68]], 10, { phase: 1.2 }),
            chain('arm', [[55, 40], [60, 55], [62, 65]], 10),
            chain('sway', [[25, 15], [25, 2]], 8, { amp: 1.5, stiff: 14 }),
            chain('leg', [[32, 60], [32, 72]], 8),
        ],
    },
    'glut/magmacolossus': {
        chains: [
            chain('arm', [[25, 25], [15, 45], [12, 60]], 11, { phase: 1.2 }),
            chain('arm', [[52, 28], [60, 45], [66, 58]], 11),
            chain('leg', [[30, 62], [30, 80]], 8),
            chain('leg', [[50, 62], [52, 80]], 8),
        ],
        pulses: [pulse([40, 30], 15, 0.015, 1.8, { charge: 0.05 })],
    },
    'frost/frostwyrm': {
        chains: [
            chain('wing', [[45, 30], [38, 15], [33, 5]], 3, {
                poly: [[46, 28], [40, 15], [35, 4], [28, 8], [15, 28], [12, 33], [22, 35], [30, 45], [38, 42], [44, 36]],
                freq: 6, amp: 16,
            }),
            chain('tail', [[60, 52], [50, 62], [35, 60], [20, 58], [8, 62], [5, 68], [15, 72]], 7, { amp: 4, freq: 3, lag: 0.8 }),
        ],
    },
    'stern/cometoracle': {
        chains: [
            chain('cloth', [[45, 55], [30, 55], [15, 60], [3, 70]], 8),
            chain('tentacle', [[50, 58], [45, 68], [40, 75]], 5, { phase: 1.5 }),
        ],
        pulses: [pulse([58, 32], 20, 0.02, 2)],
    },
    'eklipse/voidlord': {
        chains: [
            chain('cloth', [[35, 30], [20, 50], [5, 68]], 12),
            chain('tentacle', [[25, 65], [15, 75], [12, 82]], 6),
            chain('tentacle', [[40, 68], [35, 78], [38, 85]], 6, { phase: 1.3 }),
            chain('tentacle', [[55, 68], [60, 78], [58, 85]], 6, { phase: 2.6 }),
            chain('arm', [[50, 35], [62, 45]], 3, { poly: [[56, 34], [62, 30], [70, 38], [75, 55], [68, 58], [60, 50]], amp: 5, windup: 15, thrust: -25 }),
            chain('sway', [[42, 12], [42, 1]], 7, { amp: 1.5 }),
        ],
    },
};

module.exports = { RIGS };
