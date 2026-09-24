// Schneidet den Pflanzen-Atlas (Gras, Farn, Büsche, Ranken — im Spiel mit Wind im Shader) aus dem
// Sprite-Sheet der ersten Design-Vorlage (Inspiration/nnewDesign) und legt ihn als plants.png + design.json
// nach SoccerFight/Assets/Resources/NewDesign. Aufruf: node build.js
// Die Stage-Welten selbst (Kulisse, Boden, Plattformen, Deko) baut stages.js aus den Stage-Bögen.
//
// Das Sprite-Sheet hat ein aufgemaltes Karomuster statt Transparenz: das wird erkannt und entfernt,
// danach zerfällt das Bild in einzelne Teile (Zusammenhangskomponenten).
'use strict';
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const SRC = path.join(__dirname, '../../Inspiration/nnewDesign');
const OUT = path.join(__dirname, '../../SoccerFight/Assets/Resources/NewDesign');
const F = { sheet: 'FCAA83BB-08E3-4670-9714-4EFEE7570777.PNG' };

// Maßstab: das Sprite-Sheet zeigt ~56 px pro Spieleinheit (wird beim Export verdoppelt).
const SHEET_PPU = 56, SHEET_UP = 2;

const manifest = { sprites: [], plants: [] };


// ------------------------------------------------------------------ Karomuster entfernen, Teile finden

async function segment(file, minArea) {
    const { data, info } = await sharp(path.join(SRC, file)).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    const W = info.width, H = info.height, N = W * H;
    const lum = new Uint8Array(N), cand = new Uint8Array(N);
    for (let i = 0; i < N; i++) {
        const r = data[i * 3], g = data[i * 3 + 1], b = data[i * 3 + 2];
        const mx = Math.max(r, g, b), mn = Math.min(r, g, b);
        lum[i] = mx;
        cand[i] = mx - mn <= 22 && mn >= 188 ? 1 : 0;
    }
    // helle, graue Flächen: Hintergrund, wenn sie den Rand berühren oder wie ein Karomuster aussehen
    const lab = new Int32Array(N).fill(-1), bg = new Uint8Array(N), stack = new Int32Array(N);
    let nc = 0;
    for (let s = 0; s < N; s++) {
        if (!cand[s] || lab[s] >= 0) continue;
        let sp = 0; stack[sp++] = s; lab[s] = nc;
        const px = []; let border = false, lo = 0, hi = 0;
        while (sp) {
            const p = stack[--sp]; px.push(p);
            const x = p % W, y = (p / W) | 0;
            if (x == 0 || y == 0 || x == W - 1 || y == H - 1) border = true;
            if (lum[p] <= 232) lo++;
            if (lum[p] >= 238) hi++;
            if (x > 0 && cand[p - 1] && lab[p - 1] < 0) { lab[p - 1] = nc; stack[sp++] = p - 1; }
            if (x < W - 1 && cand[p + 1] && lab[p + 1] < 0) { lab[p + 1] = nc; stack[sp++] = p + 1; }
            if (y > 0 && cand[p - W] && lab[p - W] < 0) { lab[p - W] = nc; stack[sp++] = p - W; }
            if (y < H - 1 && cand[p + W] && lab[p + W] < 0) { lab[p + W] = nc; stack[sp++] = p + W; }
        }
        const n = px.length;
        if (border || (n >= 150 && lo / n >= 0.22 && hi / n >= 0.2)) for (const p of px) bg[p] = 1;
        nc++;
    }
    // heller Saum (Leuchthöfe, die ins Karomuster übergehen): von außen Schicht um Schicht abschälen
    for (let pass = 0; pass < 6; pass++) {
        const peel = [];
        for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) {
            const p = y * W + x;
            if (bg[p] || !(bg[p - 1] || bg[p + 1] || bg[p - W] || bg[p + W])) continue;
            const mn = Math.min(data[p * 3], data[p * 3 + 1], data[p * 3 + 2]);
            if (mn > 172) peel.push(p);
        }
        if (!peel.length) break;
        for (const p of peel) bg[p] = 1;
    }
    // Kante: 1 px weg (heller Saum vom Karomuster), dunkle Kantenpixel halb durchsichtig
    const alpha = new Uint8Array(N);
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
        const p = y * W + x;
        if (bg[p]) continue;
        let edge = x == 0 || y == 0 || x == W - 1 || y == H - 1 || bg[p - 1] || bg[p + 1] || bg[p - W] || bg[p + W];
        alpha[p] = edge ? (lum[p] > 170 ? 0 : 140) : 255;
    }
    // Teile (8er-Nachbarschaft)
    const cl = new Int32Array(N).fill(-1), comps = [];
    for (let s = 0; s < N; s++) {
        if (!alpha[s] || cl[s] >= 0) continue;
        let sp = 0; stack[sp++] = s; cl[s] = comps.length;
        let x0 = W, y0 = H, x1 = 0, y1 = 0, a = 0;
        while (sp) {
            const p = stack[--sp]; a++;
            const x = p % W, y = (p / W) | 0;
            if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
            for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
                const X = x + dx, Y = y + dy;
                if (X < 0 || Y < 0 || X >= W || Y >= H) continue;
                const q = Y * W + X;
                if (alpha[q] && cl[q] < 0) { cl[q] = comps.length; stack[sp++] = q; }
            }
        }
        comps.push({ id: comps.length, x: x0, y: y0, w: x1 - x0 + 1, h: y1 - y0 + 1, a });
    }
    return { W, H, rgb: data, alpha, cl, comps: comps.filter(c => c.a >= minArea) };
}

/** RGBA (straight) of one part, cropped to its box; ids: the part and any extra parts to merge in. */
function partRGBA(seg, x, y, extra = []) {
    const main = seg.comps.find(c => Math.abs(c.x - x) <= 2 && Math.abs(c.y - y) <= 2);
    if (!main) throw new Error(`kein Teil bei ${x},${y}`);
    const parts = [main, ...extra.map(([ex, ey]) => seg.comps.find(c => Math.abs(c.x - ex) <= 2 && Math.abs(c.y - ey) <= 2))];
    const ids = new Set(parts.map(p => p.id));
    let x0 = 1e9, y0 = 1e9, x1 = -1, y1 = -1;
    for (const p of parts) { x0 = Math.min(x0, p.x); y0 = Math.min(y0, p.y); x1 = Math.max(x1, p.x + p.w); y1 = Math.max(y1, p.y + p.h); }
    const w = x1 - x0, h = y1 - y0, buf = Buffer.alloc(w * h * 4);
    for (let yy = 0; yy < h; yy++) for (let xx = 0; xx < w; xx++) {
        const p = (y0 + yy) * seg.W + x0 + xx, o = (yy * w + xx) * 4;
        if (!ids.has(seg.cl[p])) continue;
        buf[o] = seg.rgb[p * 3]; buf[o + 1] = seg.rgb[p * 3 + 1]; buf[o + 2] = seg.rgb[p * 3 + 2]; buf[o + 3] = seg.alpha[p];
    }
    return { buf, w, h };
}

// ------------------------------------------------------------------ Export

const { up4, finish, save: saveTex, plainMeta } = require('./texio');
const save = (name, f) => saveTex(path.join(OUT, name + '.png'), f);

// ------------------------------------------------------------------ Pflanzen-Atlas (Wind im Shader)

async function plants(sheet) {
    // [name, x, y, mode] — mode: rooted (steht, wiegt sich), hanging (hängt, schwingt unten)
    const list = [
        ['fern', 888, 300, 'rooted'], ['grass_a', 580, 891, 'rooted'], ['grass_b', 667, 893, 'rooted'], ['grass_c', 375, 937, 'rooted'],
        ['grass_d', 674, 953, 'rooted'], ['clover', 602, 954, 'rooted'], ['grass_e', 546, 957, 'rooted'], ['grass_f', 1493, 422, 'rooted'],
        ['bush_a', 588, 407, 'rooted'], ['bush_b', 493, 473, 'rooted'], ['bush_c', 402, 475, 'rooted'], ['bush_d', 489, 544, 'rooted'],
        ['mushroom_blue', 658, 822, 'rooted'], ['mushroom_green', 590, 832, 'rooted'],
        ['vine_a', 992, 295, 'hanging'], ['vine_b', 1037, 309, 'hanging'], ['vine_c', 1085, 309, 'hanging'], ['vine_d', 1127, 309, 'hanging'], ['vine_e', 1172, 309, 'hanging'],
    ];
    const items = [];
    for (const [name, x, y, mode] of list) {
        const f = await finish(partRGBA(sheet, x, y), SHEET_UP, true);
        items.push({ name, mode, f });
    }
    // Regalpacken
    const AW = 1024; let cx = 0, cy = 0, rowH = 0;
    for (const it of items) {
        if (cx + it.f.w > AW) { cx = 0; cy += rowH; rowH = 0; }
        it.x = cx; it.y = cy; cx += it.f.w; rowH = Math.max(rowH, it.f.h);
    }
    const AH = up4(cy + rowH);
    const atlas = Buffer.alloc(AW * AH * 4);
    for (const it of items) for (let y = 0; y < it.f.h; y++) it.f.buf.copy(atlas, ((it.y + y) * AW + it.x) * 4, y * it.f.w * 4, (y + 1) * it.f.w * 4);
    await save('plants', { buf: atlas, w: AW, h: AH });
    const ppu = SHEET_PPU * SHEET_UP;
    for (const it of items) {
        const f = it.f;
        // Pivot: unten Mitte (stehend) bzw. oben Mitte (hängend), in Pixeln vom linken unteren Rand des Eintrags
        const py = it.mode == 'hanging' ? f.h - f.oy : f.h - (f.oy + f.ih);
        manifest.plants.push({ name: it.name, mode: it.mode, x: it.x, y: AH - it.y - f.h, w: f.w, h: f.h, px: f.w / 2, py, ppu });
    }
    manifest.plantsSize = [AW, AH];
}

// ------------------------------------------------------------------ alles

(async () => {
    fs.mkdirSync(OUT, { recursive: true });
    console.log('Sprite-Sheet zerlegen …');
    const sheet = await segment(F.sheet, 80);
    console.log('  Teile:', sheet.comps.length);
    console.log('Pflanzen-Atlas …');
    await plants(sheet);
    fs.writeFileSync(path.join(OUT, 'design.json'), JSON.stringify(manifest, null, 1));
    plainMeta(path.join(OUT, 'design.json'), false);
    plainMeta(OUT, true);
    console.log('fertig:', manifest.plants.length, 'Pflanzen');
})().catch(e => { console.error(e); process.exit(1); });
