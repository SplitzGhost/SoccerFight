// Shop-Karten aus der Shop-Vorlage (Inspiration/Shop UI): pro Spieler eine fertige Karte (Bild, Rahmen, schwarze
// Namensleiste mit gemaltem Namen) und die Einzelteile der Leiste (Münze, gelber Knopf, „GEWÄHLT“).
// Hintergrund, Menüleiste und Währung der Vorlage werden NICHT übernommen – die hat das Spiel selbst.
//
//  - Karten: Ausschnitt → Preis/Knopf rechts in der Leiste wegfüllen → Real-ESRGAN ×4 → auf einheitliche Größe
//    setzen (hohe Karte 636×1100, kleine 636×536; Rahmen und Leiste überall gleich hoch, das Bild füllt den Rest).
//  - Rio und Bruno sind in der Vorlage verschieden hoch: beide bekommen dieselbe Größe (Bild oben bündig beschnitten).
//  - Im Ordner liegen mehrere Vorlagen (nach Namen sortiert, `file` in CARDS): im ersten Bild ist Mira am Rand
//    abgeschnitten, ihre Karte kommt aus dem zweiten. Dort ist die Karte schmaler: das Bild wird auf die gemeinsame
//    Breite vergrößert (unten fällt etwas weg), der Name in der Leiste bleibt unverzerrt.
//  - Leistenteile: vom fast schwarzen Leistengrund freigestellt (sie liegen im Spiel wieder auf der Leiste).
//
// Aufruf: node tools/newdesign/shop.js   (braucht die Grafikkarte für Real-ESRGAN, sonst Lanczos)
//         SF_DEBUG=<ordner> schreibt Prüfbilder.
'use strict';
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');
const { upscaleRGB } = require('./aiup');
const { save, writeMeta, plainMeta } = require('./texio');

const ROOT = path.join(__dirname, '..', '..');
const SRC_DIR = path.join(ROOT, 'Inspiration/Shop UI');
const OUT = path.join(ROOT, 'SoccerFight/Assets/Resources/Menu/Shop');
const DEBUG = process.env.SF_DEBUG;

// Ausgabe (Pixel): doppelte Anzeigegröße der Karte im Menü (318 Einheiten breit)
const W = 636, H_TALL = 1100, H_SMALL = 536, FRAME = 6, FOOT = 127;
const F = 4;          // Real-ESRGAN
const EDGE = 3;       // Rahmenstärke in der Vorlage
const CARD_W = 325;   // übliche Kartenbreite in der Vorlage (Maßstab der Namensleiste)

// box: Außenkante der Karte in der Vorlage [x0, y0, x1, y1); foot: Zeilen von der Farblinie bis zur Unterkante;
// erase: Spalten der Leiste, in denen Preis/Knopf/„GEWÄHLT“ stehen; sample: saubere Leistenspalten zum Auffüllen;
// file: welche Vorlage (0 = erste im Ordner). Die Reihenfolge hier ist die Reihenfolge im Shop.
const CARDS = {
    titan: { box: [79, 230, 405, 792], foot: 65, erase: [270, 401], sample: [255, 268] },
    dre: { box: [419, 230, 737, 792], foot: 65, erase: [600, 733], sample: [585, 598] },
    nova: { box: [750, 230, 1071, 792], foot: 65, erase: [940, 1067], sample: [925, 938] },
    rio: { box: [1084, 231, 1409, 520], foot: 60, erase: [1265, 1404], sample: [1250, 1263], small: true, stripe: [255, 120, 96] },
    bruno: { box: [1084, 534, 1409, 796], foot: 62, erase: [1236, 1404], sample: [1225, 1234], small: true },
    mira: { file: 1, box: [1273, 231, 1566, 795], foot: 65, erase: [1445, 1562], sample: [1428, 1443] },
};
// Leistenteile [x0, y0, x1, y1)
const PARTS = {
    coin: [342, 737, 394, 788],
    select: [1234, 743, 1403, 792],
    chosen: [1276, 470, 1394, 514],
};

const smooth = (a, b, v) => { const t = Math.max(0, Math.min(1, (v - a) / (b - a))); return t * t * (3 - 2 * t); };

async function loadRGB(file) {
    const { data, info } = await sharp(file).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    return { buf: data, w: info.width, h: info.height };
}

function crop(img, [x0, y0, x1, y1], ch = 3) {
    const w = x1 - x0, h = y1 - y0, buf = Buffer.alloc(w * h * ch);
    for (let y = 0; y < h; y++) img.buf.copy(buf, y * w * ch, ((y + y0) * img.w + x0) * ch, ((y + y0) * img.w + x1) * ch);
    return { buf, w, h };
}

const raw = (img, ch = 3) => sharp(img.buf, { raw: { width: img.w, height: img.h, channels: ch } });

async function debug(name, img, ch = 3) {
    if (!DEBUG) return;
    fs.mkdirSync(DEBUG, { recursive: true });
    await raw(img, ch).png().toFile(path.join(DEBUG, name + '.png'));
}

/** Leistenzeilen rechts vom Namen mit dem Leistengrund füllen (Zeilenmittel der sauberen Spalten). */
function eraseBar(src, c, x0, x1, sample) {
    const yTop = c.box[3] - c.foot + 8, yBot = c.box[3] - EDGE;
    for (let y = yTop; y < yBot; y++) {
        const m = [0, 0, 0];
        for (let x = sample[0]; x < sample[1]; x++) for (let k = 0; k < 3; k++) m[k] += src.buf[(y * src.w + x) * 3 + k] / (sample[1] - sample[0]);
        for (let x = x0; x < x1; x++) for (let k = 0; k < 3; k++) src.buf[(y * src.w + x) * 3 + k] = Math.round(m[k]);
    }
}

/** Die Farblinie über der Leiste einfärben (Rios ist in der Vorlage grün, weil er dort gewählt ist). */
function paintStripe(src, c, rgb, test) {
    const y0 = c.box[3] - c.foot - 1, y1 = y0 + 9;
    for (let y = y0; y < y1; y++) for (let x = c.box[0] + EDGE; x < c.box[2] - EDGE; x++) {
        const i = (y * src.w + x) * 3, r = src.buf[i], g = src.buf[i + 1], b = src.buf[i + 2];
        if (!test(r, g, b)) continue;
        const v = Math.max(r, g, b) / 250;
        for (let k = 0; k < 3; k++) src.buf[i + k] = Math.min(255, Math.round(rgb[k] * v));
    }
}

/**
 * Setzt die vergrößerte Karte auf die einheitliche Größe: Rahmen oben/links/rechts und Fuß (Linie + Leiste) werden
 * auf feste Maße gebracht, das Bild füllt die Fläche dazwischen (oben bündig, was übersteht, fällt weg).
 */
async function compose(up, foot, small, split) {
    const H = small ? H_SMALL : H_TALL, e = EDGE * F, f = foot * F;
    const artW = W - 2 * FRAME, artH = H - FRAME - FOOT;
    const piece = (x, y, w, h, tw, th, fit) => raw(up).extract({ left: x, top: y, width: w, height: h })
        .resize(tw, th, { fit, position: 'top', kernel: 'lanczos3' }).png().toBuffer();
    const inner = await piece(e, e, up.w - 2 * e, up.h - e - f, artW, artH, 'cover');
    const layers = [
        { input: inner, left: FRAME, top: FRAME },
        { input: await piece(0, 0, up.w, e, W, FRAME, 'fill'), left: 0, top: 0 },
        { input: await piece(0, e, e, up.h - e - f, FRAME, artH, 'fill'), left: 0, top: FRAME },
        { input: await piece(up.w - e, e, e, up.h - e - f, FRAME, artH, 'fill'), left: W - FRAME, top: FRAME },
    ];
    // Fuß: links der Name und rechts der Rahmen im üblichen Maßstab, die leere Leiste dazwischen füllt die Breite
    // (bei schmaleren Karten würde der Name sonst in die Breite gezogen)
    const k = W / (CARD_W * F), s = split * F, r = 8 * F;
    const lw = Math.round(s * k), rw = Math.round(r * k);
    layers.push(
        { input: await piece(0, up.h - f, s, f, lw, FOOT, 'fill'), left: 0, top: H - FOOT },
        { input: await piece(s, up.h - f, up.w - s - r, f, W - lw - rw, FOOT, 'fill'), left: lw, top: H - FOOT },
        { input: await piece(up.w - r, up.h - f, r, f, rw, FOOT, 'fill'), left: W - rw, top: H - FOOT });
    const buf = await sharp({ create: { width: W, height: H, channels: 3, background: '#000' } }).composite(layers).removeAlpha().raw().toBuffer();
    return { buf, w: W, h: H };
}

async function upscale(img) {
    return { buf: await upscaleRGB(img, img.w * F, img.h * F), w: img.w * F, h: img.h * F };
}

// ------------------------------------------------------------------ Leistenteile

/** Vom dunklen Leistengrund freistellen: alles vom Rand aus erreichbare Dunkle wird durchsichtig. */
function key(up) {
    const { w, h } = up, N = w * h;
    const bg = [0, 0, 0]; let n = 0;
    for (let x = 0; x < w; x++) for (const y of [0, h - 1]) { for (let k = 0; k < 3; k++) bg[k] += up.buf[(y * w + x) * 3 + k]; n++; }
    for (let k = 0; k < 3; k++) bg[k] /= n;
    const d = new Float32Array(N);
    for (let p = 0; p < N; p++) { let m = 0; for (let k = 0; k < 3; k++) m = Math.max(m, Math.abs(up.buf[p * 3 + k] - bg[k])); d[p] = m; }
    const T = 80, outside = new Uint8Array(N), stack = [];
    for (let x = 0; x < w; x++) stack.push(x, (h - 1) * w + x);
    for (let y = 0; y < h; y++) stack.push(y * w, y * w + w - 1);
    while (stack.length) {
        const p = stack.pop();
        if (outside[p] || d[p] >= T) continue;
        outside[p] = 1;
        const x = p % w, y = (p / w) | 0;
        if (x > 0) stack.push(p - 1);
        if (x < w - 1) stack.push(p + 1);
        if (y > 0) stack.push(p - w);
        if (y < h - 1) stack.push(p + w);
    }
    const out = Buffer.alloc(N * 4);
    for (let p = 0; p < N; p++) {
        const a = outside[p] ? smooth(16, T, d[p]) : 1;
        for (let k = 0; k < 3; k++) {
            const v = a > 0.03 ? bg[k] + (up.buf[p * 3 + k] - bg[k]) / a : up.buf[p * 3 + k];
            out[p * 4 + k] = Math.max(0, Math.min(255, Math.round(v)));
        }
        out[p * 4 + 3] = Math.round(a * 255);
    }
    return { buf: out, w, h, outside };
}

function trim(img, pad = 4) {
    const { w, h } = img;
    let x0 = w, x1 = 0, y0 = h, y1 = 0;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (img.buf[(y * w + x) * 4 + 3] > 10) { x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y); }
    x0 = Math.max(0, x0 - pad); y0 = Math.max(0, y0 - pad); x1 = Math.min(w - 1, x1 + pad); y1 = Math.min(h - 1, y1 + pad);
    return crop(img, [x0, y0, x1 + 1, y1 + 1], 4);
}

/** Der gelbe Knopf ohne Schrift: die dunklen Buchstaben werden zeilenweise mit der Plattenfarbe gefüllt. */
function blankPlate(keyed) {
    const { w, h } = keyed, N = w * h, buf = Buffer.from(keyed.buf);
    const lum = p => 0.3 * buf[p * 4] + 0.59 * buf[p * 4 + 1] + 0.11 * buf[p * 4 + 2];
    // Buchstaben: dunkle Pixel im Inneren (nicht der Rand der Platte), großzügig geweitet
    const text = new Uint8Array(N);
    const mx0 = Math.round(w * 0.1), mx1 = Math.round(w * 0.9), my0 = Math.round(h * 0.2), my1 = Math.round(h * 0.8);
    const R = 7;
    for (let y = my0; y < my1; y++) for (let x = mx0; x < mx1; x++) {
        if (keyed.outside[y * w + x] || lum(y * w + x) > 120) continue;
        for (let dy = -R; dy <= R; dy++) for (let dx = -R; dx <= R; dx++) text[(y + dy) * w + x + dx] = 1;
    }
    // die Platte ist waagrecht gleichmäßig: die ganze Mitte bekommt pro Zeile ihre Plattenfarbe (weich eingeblendet)
    const FADE = 24;
    for (let y = 0; y < h; y++) {
        const vals = [[], [], []];
        for (let x = mx0; x < mx1; x++) { const p = y * w + x; if (!text[p] && buf[p * 4 + 3] == 255) for (let k = 0; k < 3; k++) vals[k].push(buf[p * 4 + k]); }
        if (vals[0].length < 8) continue;
        const med = vals.map(v => v.sort((a, b) => a - b)[v.length >> 1]);
        for (let x = mx0 - FADE; x < mx1 + FADE; x++) {
            const p = y * w + x;
            if (buf[p * 4 + 3] < 255) continue;
            const t = text[p] ? 1 : smooth(0, 1, Math.min(x - (mx0 - FADE), mx1 + FADE - x) / FADE);
            for (let k = 0; k < 3; k++) buf[p * 4 + k] = Math.round(buf[p * 4 + k] * (1 - t) + med[k] * t);
        }
    }
    return { buf, w, h };
}

async function savePart(name, img, srcBox) {
    const file = path.join(OUT, name + '.png');
    await raw(img, 4).png({ compressionLevel: 9 }).toFile(file);
    writeMeta(file, 0, 1, 1);
    // normales Alpha (UI-Shader): Farbe in die durchsichtigen Ränder ziehen, trilinear verkleinern
    const meta = file + '.meta';
    fs.writeFileSync(meta, fs.readFileSync(meta, 'utf8').replace('alphaIsTransparency: 0', 'alphaIsTransparency: 1').replace('filterMode: 1', 'filterMode: 2'));
    console.log(`  ${name}: ${img.w}×${img.h} px, in der Vorlage ${(img.w / F).toFixed(1)}×${(img.h / F).toFixed(1)} (Karte ${srcBox} breit)`);
}

// ------------------------------------------------------------------ Ablauf

(async () => {
    const files = fs.readdirSync(SRC_DIR).filter(f => /\.png$/i.test(f)).sort();
    if (!files.length) throw new Error('Keine Vorlage in ' + SRC_DIR);
    const sources = [];
    const source = async i => sources[i] || (sources[i] = await loadRGB(path.join(SRC_DIR, files[i])));
    const orig = await source(0);
    fs.mkdirSync(OUT, { recursive: true });
    plainMeta(OUT, true);

    for (const [id, c] of Object.entries(CARDS)) {
        if ((c.file || 0) >= files.length) throw new Error(`Vorlage ${c.file} für ${id} fehlt in ${SRC_DIR}`);
        const o = await source(c.file || 0);
        const src = { buf: Buffer.from(o.buf), w: o.w, h: o.h };
        eraseBar(src, c, c.erase[0], c.erase[1], c.sample);
        if (c.stripe) paintStripe(src, c, c.stripe, (r, g, b) => g > r + 40 && g > b + 40);
        const up = await upscale(crop(src, c.box));
        const card = await compose(up, c.foot, c.small, c.erase[0] - c.box[0] + 4);
        await save(path.join(OUT, id + '.png'), card, { channels: 3, crunch: true });
        await debug(id, card);
        console.log('  Karte', id);
    }

    // Reihenfolge und Format der Karten (kleine Karten stehen im Shop zu zweit übereinander)
    const list = Object.entries(CARDS).map(([id, c]) => ({ id, small: !!c.small }));
    const json = path.join(OUT, 'cards.json');
    fs.writeFileSync(json, JSON.stringify({ cards: list }, null, 1) + '\n');
    plainMeta(json, false);

    for (const [name, box] of Object.entries(PARTS)) {
        const keyed = key(await upscale(crop(orig, box)));
        await savePart(name, trim(keyed), 325);
        if (name == 'select') await savePart('plate', trim(blankPlate(keyed)), 325);
    }
    // die Boxer stehen nicht in der Vorlage: ihre Karten setzt shop.box.js zusammen und hängt sie an cards.json an
    await require('./shop.box').run();
})().catch(e => { console.error(e); process.exit(1); });
