// Spielerauswahl der Boxer aus ihren drei Bildentwürfen (Inspiration/boxer/Auswahlentwuerfe/<NAME>.png, 1672 × 941).
// Dieselben Werkzeuge wie playerselect.js, aber eine eigene Familie: Die Boxer-Entwürfe zeigen die Kulisse leicht anders
// gemalt (etwas größer, verschoben, die Menüleiste oben ist mit eingemalt), deshalb bekommen sie ihre eigene Kulisse
// (scene_box) statt Rios. Pro Boxer entstehen wie bei den anderen: Steinbild-Ausschnitt (back_<id>), freigestellte Figur
// (figure_<id>), Schriftzug (title_<id>), Fähigkeits-Medaillon (ability_<id>) und die Kacheln (tile_<id>, tile_<id>_on).
// Die Teile werden an layout.json angehängt (bestehende Einträge der Boxer ersetzt), die Figuren tragen "scene".
//
// Aufruf: node tools/newdesign/playerselect.box.js   (läuft auch am Ende von playerselect.js mit; Real-ESRGAN wie dort)
'use strict';
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');
const P = require('./playerselect');
const { upscaleRGBA } = require('./aiup');
const { save, plainMeta } = require('./texio');
const { W, H, N, F, BACK, UI_CORNER, OUT, DEBUG } = P;

const ROOT = path.join(__dirname, '..', '..');
const SRC_DIR = path.join(ROOT, 'Inspiration/boxer/Auswahlentwuerfe');
const IDS = ['kai', 'vera', 'luz'];

// Gelenke der Figuren im Entwurf (Bildpunkte, nach Augenmaß) wie in playerselect.js
const RIG = {
    kai: { ground: 745, hip: 455, chest: [545, 390], neck: [535, 310], head: [515, 250] },
    vera: { ground: 748, hip: 440, chest: [535, 380], neck: [545, 290], head: [550, 240], swing: [{ root: [497, 200], tip: [445, 228], radius: 36, flex: 0.3 }] },
    luz: { ground: 748, hip: 450, chest: [550, 400], neck: [565, 315], head: [580, 260], swing: [{ root: [525, 190], tip: [412, 298], radius: 38, flex: 1 }] },
};
// Grober Umriss des Unterkörpers (ab y = 500): Fackeln, Büsche und Gras zwischen den Beinen sind in den drei Entwürfen
// verschieden gemalt und würden sonst als Figur gelten
const LEGS = {
    kai: [[300, 500], [410, 505], [386, 580], [378, 625], [376, 660], [302, 705], [298, 762], [442, 762], [438, 662], [447, 594], [462, 566],
        [494, 576], [562, 573], [594, 583], [632, 604], [660, 646], [664, 664], [664, 762], [800, 762], [795, 700], [724, 665], [718, 620],
        [706, 580], [668, 510], [800, 500]],
    vera: [[300, 500], [404, 505], [386, 566], [372, 616], [350, 680], [300, 720], [298, 762], [416, 762], [412, 705], [428, 646], [436, 632],
        [446, 587], [452, 559], [494, 560], [572, 560], [616, 588], [650, 630], [658, 646], [676, 694], [680, 762], [800, 762], [796, 700],
        [724, 660], [720, 625], [706, 580], [668, 510], [800, 500]],
    luz: [[300, 500], [444, 500], [418, 544], [394, 594], [382, 620], [372, 664], [302, 720], [300, 762], [428, 762], [428, 676], [442, 634],
        [449, 598], [476, 551], [509, 560], [592, 563], [624, 592], [652, 617], [674, 660], [668, 700], [672, 762], [800, 762], [798, 700],
        [740, 640], [716, 610], [690, 560], [666, 505], [800, 500]],
};
const LEGS_FROM = 500;

function inPoly(poly, x, y) {
    let c = false;
    for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
        const [xi, yi] = poly[i], [xj, yj] = poly[j];
        if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) c = !c;
    }
    return c;
}

// Fächer der drei Kacheln in der Figurenleiste der Boxer-Entwürfe (Grenzen in x)
const SLOTS = [530, 724, 928, 1146];
// Größe, in die eine Boxer-Kachel passen muss (so groß wie die Kacheln der anderen Spieler)
const TILE_FIT = [178, 133];

/** Wo stimmen mindestens zwei der drei Entwürfe überein? Dort ist der Hintergrund bekannt (known = Zahl der Übereinstimmungen). */
function consensus(soft) {
    const bg = new Float32Array(N * 3), known = new Uint8Array(N), T = 20;
    const dist = (a, b, p) => Math.max(Math.abs(a[p * 3] - b[p * 3]), Math.abs(a[p * 3 + 1] - b[p * 3 + 1]), Math.abs(a[p * 3 + 2] - b[p * 3 + 2]));
    for (let p = 0; p < N; p++) {
        let best = -1, bestN = 0;
        for (let i = 0; i < soft.length; i++) {
            let n = 0;
            for (let j = 0; j < soft.length; j++) if (dist(soft[i], soft[j], p) < T) n++;
            if (n > bestN) { bestN = n; best = i; }
        }
        if (bestN < 2) continue;
        const r = soft[best][p * 3], g = soft[best][p * 3 + 1], b = soft[best][p * 3 + 2], l = P.lumOf(r, g, b), mx = Math.max(r, g, b);
        if (l < 48 || (l < 100 && (mx - Math.min(r, g, b)) / mx < 0.15)) continue;
        known[p] = bestN;
        for (let c = 0; c < 3; c++) bg[p * 3 + c] = soft[best][p * 3 + c];
    }
    return { bg, known };
}

/** Teil in eine Höchstgröße einpassen (gleichmäßig verkleinern, nie vergrößern). */
async function fit(part, maxW, maxH) {
    const k = Math.min(1, maxW / part.w, maxH / part.h);
    if (k >= 0.999) return part;
    const w = Math.round(part.w * k), h = Math.round(part.h * k);
    const buf = await sharp(part.buf, { raw: { width: part.w, height: part.h, channels: 4 } }).resize(w, h, { kernel: 'lanczos3' }).raw().toBuffer();
    return { buf, w, h, x0: part.x0, y0: part.y0 };
}

async function run() {
    const t0 = Date.now();
    const imgs = [], soft = [];
    for (const id of IDS) {
        const img = await P.loadRGB(path.join(SRC_DIR, id.toUpperCase() + '.png'));
        imgs.push(img); soft.push(P.blurRGB(Float32Array.from(img), 2));
    }

    // ---- Figuren
    const { bg, known } = consensus(soft);
    const masks = IDS.map((id, a) => {
        const m = P.figureMask(imgs[a], soft[a], bg, known);
        for (let y = LEGS_FROM; y < H; y++) for (let x = 0; x < W; x++) if (m[y * W + x] && !inPoly(LEGS[id], x + 0.5, y + 0.5)) m[y * W + x] = 0;
        // eingeschlossene Löcher (heller Hosenbund) schließen
        const holes = P.components(Uint8Array.from(m, v => v ? 0 : 1), W, H);
        const edge = new Uint8Array(holes.sizes.length);
        for (let p = 0; p < N; p++) { const k = holes.lab[p]; if (k < 0) continue; const x = p % W, y = (p / W) | 0; if (x == 0 || y == 0 || x == W - 1 || y == H - 1) edge[k] = 1; }
        for (let p = 0; p < N; p++) { const k = holes.lab[p]; if (k >= 0 && !edge[k] && holes.sizes[k] < 4000) m[p] = 1; }
        return m;
    });
    if (DEBUG) for (let a = 0; a < IDS.length; a++) {
        const out = Buffer.alloc(N * 3);
        for (let p = 0; p < N; p++) { const m = masks[a][p]; out[p * 3] = m ? imgs[a][p * 3] : 40; out[p * 3 + 1] = m ? imgs[a][p * 3 + 1] : 200; out[p * 3 + 2] = m ? imgs[a][p * 3 + 2] : 60; }
        await P.debug('box_figur_' + IDS[a], out, W, H, 3);
    }
    console.log(`  Boxer: Figurenmasken (${((Date.now() - t0) / 1000).toFixed(0)} s)`);

    // ---- Einzelteile: Schriftzug, Medaillon, Kacheln
    const parts = {};
    for (let a = 0; a < IDS.length; a++) {
        const id = IDS[a], other = imgs[(a + 1) % IDS.length];
        parts['title_' + id] = P.trim(P.keyDarkText(imgs[a], [1164, 204, 1480, 328]), 3);
        parts['ability_' + id] = P.trim(P.cutAbility(imgs[a]), 2);
        parts['tile_' + id + '_on'] = await fit(P.cutTile(imgs[a], SLOTS[a], SLOTS[a + 1], true), TILE_FIT[0], TILE_FIT[1]);
        parts['tile_' + id] = await fit(P.cutTile(other, SLOTS[a], SLOTS[a + 1], false), TILE_FIT[0], TILE_FIT[1]);
    }

    // ---- Steinbild-Ausschnitt je Boxer, Figur herausgefüllt
    const backs = [];
    for (let a = 0; a < IDS.length; a++) {
        const img = Buffer.from(imgs[a]);
        // die gemalte Menüleiste oben gehört nicht zum Steinbild: Himmel
        const bar = new Uint8Array(N);
        for (let y = 0; y < 102; y++) for (let x = 400; x < BACK[2]; x++) bar[y * W + x] = 1;
        P.fillSmooth(img, [380, 0, BACK[2] + 40, 240], bar);
        P.fillHole(img, BACK, P.dilate(masks[a], W, H, 5), { yWeight: 3, xWeight: 0.6, seed: 21 + a });
        backs.push(img);
        console.log(`  Boxer: Ausschnitt ${IDS[a]} (${((Date.now() - t0) / 1000).toFixed(0)} s)`);
    }

    // ---- Kulisse der Boxer: KAIs Entwurf ohne Figur, ohne Menüleiste, Währung, Tafel und Figurenleiste
    const scene = Buffer.from(backs[0]);
    {
        const hole = new Uint8Array(N);
        const rect = ([x0, y0, x1, y1], m) => { for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) m[y * W + x] = 1; };
        const ui = imgs[0];
        // die gemalten Teile der Tafel an denselben Stellen wie in Rios Entwurf: Abzeichen, Name, Medaillon, Werteplatten, Knöpfe
        const mark = (part, grow) => {
            const m = new Uint8Array(N);
            for (let y = 0; y < part.h; y++) for (let x = 0; x < part.w; x++) if (part.buf[(y * part.w + x) * 4 + 3] > 12) m[(y + part.y0) * W + x + part.x0] = 1;
            const g = P.dilate(m, W, H, grow);
            for (let p = 0; p < N; p++) if (g[p]) hole[p] = 1;
        };
        mark(P.trim(P.cutByColour(ui, [1032, 190, 1158, 332], (r, g, b) => r > b + 15, false)), 7);
        for (const p of [[974, 539, 1158, 628.5], [1163.5, 539, 1353, 628.5], [1358.5, 539, 1546, 628.5]]) mark(P.cutOctagon(ui, p, 16), 7);
        mark(P.trim(P.cutByColour(ui, [950, 634, 1402, 748], P.isGold)), 7);
        mark(P.trim(P.cutByColour(ui, [1403, 636, 1648, 751], P.isBlue)), 7);
        mark(P.trim(P.keyDarkText(ui, [1164, 204, 1480, 328]), 3), 7);
        mark(P.trim(P.cutAbility(ui), 2), 7);
        // Fähigkeitstext (Trennstrich, Überschrift, Zeilen): alles Dunkle im Feld
        const text = new Uint8Array(N);
        for (let y = 360; y < 504; y++) for (let x = 1178; x < 1650; x++) { const i = (y * W + x) * 3; if (P.lumOf(ui[i], ui[i + 1], ui[i + 2]) < 150) text[y * W + x] = 1; }
        const grown = P.dilate(text, W, H, 5);
        for (let p = 0; p < N; p++) if (grown[p]) hole[p] = 1;
        rect([400, 0, W, 102], hole);           // gemalte Menüleiste und Währung
        P.fillSmooth(scene, [380, 0, W, 240], hole);
        P.fillSmooth(scene, [900, 150, W, 800], hole);
        const strip = new Uint8Array(N);
        rect([500, 760, 1180, 906], strip);
        P.fillHole(scene, [120, 720, 1560, H], strip, { yWeight: 6, seed: 18 });
    }
    await save(path.join(OUT, 'scene_box.png'), { buf: scene, w: W, h: H }, { channels: 3, crunch: true, mips: 0 });
    for (let a = 0; a < IDS.length; a++) {
        for (let y = UI_CORNER[1]; y < BACK[3]; y++) for (let x = UI_CORNER[0]; x < BACK[2]; x++) {
            const p = (y * W + x) * 3, e = P.clamp01(Math.min(x - UI_CORNER[0], y - UI_CORNER[1]) / 10);
            for (let c = 0; c < 3; c++) backs[a][p + c] = Math.round(backs[a][p + c] * (1 - e) + scene[p + c] * e);
        }
        await save(path.join(OUT, 'back_' + IDS[a] + '.png'), P.crop(backs[a], BACK), { channels: 3, crunch: true, mips: 0 });
    }
    await P.debug('box_kulisse', scene, W, H, 3);
    console.log(`  Boxer: Kulisse (${((Date.now() - t0) / 1000).toFixed(0)} s)`);

    // ---- Figuren freistellen, alles vergrößern
    const figures = [];
    for (let a = 0; a < IDS.length; a++) {
        const alpha = P.soften(masks[a], W, H);
        let x0 = W, x1 = 0, y0 = H, y1 = 0;
        for (let p = 0; p < N; p++) if (alpha[p] > 0.03) { const x = p % W, y = (p / W) | 0; x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y); }
        const box = [x0 - 6, y0 - 6, x1 + 7, y1 + 7], c = P.crop(imgs[a], box), al = new Float32Array(c.w * c.h);
        for (let y = 0; y < c.h; y++) for (let x = 0; x < c.w; x++) al[y * c.w + x] = alpha[(y + box[1]) * W + x + box[0]];
        figures.push(P.withAlpha(c, al));
    }
    const names = Object.keys(parts);
    const big = await upscaleRGBA([...figures, ...names.map(n => parts[n])], F);

    const file = path.join(OUT, 'layout.json');
    const layout = JSON.parse(fs.readFileSync(file, 'utf8'));
    layout.figures = layout.figures.filter(f => !IDS.includes(f.id));
    layout.parts = layout.parts.filter(p => !names.includes(p.name));
    for (let a = 0; a < IDS.length; a++) {
        const f = figures[a], rig = RIG[IDS[a]];
        await P.savePart('figure_' + IDS[a], big[a], true);
        layout.figures.push({
            id: IDS[a], x: f.x0, y: f.y0, w: f.w, h: f.h, ground: rig.ground, hip: rig.hip,
            chestX: rig.chest[0], chestY: rig.chest[1], neckX: rig.neck[0], neckY: rig.neck[1], headX: rig.head[0], headY: rig.head[1],
            swing: (rig.swing || []).map(s => ({ rootX: s.root[0], rootY: s.root[1], tipX: s.tip[0], tipY: s.tip[1], radius: s.radius, flex: s.flex })),
            scene: 'scene_box',
        });
        await P.debug('box_teil_figure_' + IDS[a], big[a].buf, big[a].w, big[a].h, 4);
    }
    for (let i = 0; i < names.length; i++) {
        const p = parts[names[i]], b = big[IDS.length + i];
        await P.savePart(names[i], b, names[i].startsWith('tile_'));
        // die Kacheln stehen in der Leiste wie die der anderen; ihre Lage im Entwurf zählt nur für Name und Medaillon
        layout.parts.push({ name: names[i], x: p.x0, y: p.y0, w: p.w, h: p.h });
        await P.debug('box_teil_' + names[i], b.buf, b.w, b.h, 4);
    }
    fs.writeFileSync(file, JSON.stringify(layout, null, 1) + '\n');
    plainMeta(file, false);

    if (DEBUG) for (let a = 0; a < IDS.length; a++) {
        const out = Buffer.from(scene), f = figures[a];
        for (let y = BACK[1]; y < BACK[3]; y++) for (let x = BACK[0]; x < BACK[2]; x++) {
            const p = y * W + x, e = Math.min(1, (x - BACK[0]) / 24, (BACK[2] - 1 - x) / 24, (BACK[3] - 1 - y) / 12);
            for (let c = 0; c < 3; c++) out[p * 3 + c] = Math.round(out[p * 3 + c] * (1 - e) + backs[a][p * 3 + c] * e);
        }
        for (let y = 0; y < f.h; y++) for (let x = 0; x < f.w; x++) {
            const p = (y + f.y0) * W + x + f.x0, al = f.buf[(y * f.w + x) * 4 + 3] / 255;
            for (let c = 0; c < 3; c++) out[p * 3 + c] = Math.round(out[p * 3 + c] * (1 - al) + f.buf[(y * f.w + x) * 4 + c] * al);
        }
        await P.debug('box_probe_' + IDS[a], out, W, H, 3);
    }
    console.log(`  Boxer fertig: ${names.length} Teile, ${IDS.length} Figuren (${((Date.now() - t0) / 1000).toFixed(0)} s)`);
}

module.exports = { run };

if (require.main === module) run().catch(e => { console.error(e); process.exit(1); });
