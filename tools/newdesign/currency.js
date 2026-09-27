// Kristall- und Münzleiste aus der freigegebenen Charakteransicht (oben rechts) als eigene Menügrafik.
// Die Leisten liegen dort auf hellem Himmel: der Himmel wird pro Zeile zwischen linkem und rechtem Rand
// geschätzt, alles vom Rand aus zusammenhängend Himmelfarbene wird durchsichtig, Kanten und Schatten
// werden gegen diese Himmelfarbe entmischt. Innen (Glanzlichter des Kristalls) bleibt alles deckend.
// Aufruf: node tools/newdesign/currency.js  (braucht die Grafikkarte für Real-ESRGAN, sonst Lanczos)
'use strict';
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { upscaleRGB } = require('./aiup');

const ROOT = path.join(__dirname, '..', '..');
const SRC = path.join(ROOT, 'SoccerFight/Assets/Resources/Menu/CharacterDetails/dre.png');
const OUT = path.join(ROOT, 'SoccerFight/Assets/Resources/Menu/Currency');
const META = path.join(ROOT, 'SoccerFight/Assets/Resources/Menu/CharacterDetails/dre.png.meta');
// Ausschnitt im 1672×941-Bild und Trennspalte zwischen Kristall- und Münzleiste
const BOX = { left: 1360, top: 6, width: 296, height: 68 };
const SPLIT = 1500;
const F = 3;   // Endgröße: dreifache Auflösung des Probebilds

(async () => {
    const { data, info } = await sharp(SRC).extract(BOX).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    const W = BOX.width * F, H = BOX.height * F;
    const px = await upscaleRGB({ buf: data, w: info.width, h: info.height }, W, H);

    // Himmel pro Zeile: Mittel der äußersten Spalten, dazwischen linear
    const EDGE = 6 * F;
    const bg = new Float32Array(W * H * 3);
    for (let y = 0; y < H; y++) {
        const l = [0, 0, 0], r = [0, 0, 0];
        for (let x = 0; x < EDGE; x++) for (let c = 0; c < 3; c++) {
            l[c] += px[(y * W + x) * 3 + c] / EDGE;
            r[c] += px[(y * W + W - 1 - x) * 3 + c] / EDGE;
        }
        for (let x = 0; x < W; x++) {
            const t = x / (W - 1);
            for (let c = 0; c < 3; c++) bg[(y * W + x) * 3 + c] = l[c] * (1 - t) + r[c] * t;
        }
    }
    const dist = new Float32Array(W * H);
    for (let p = 0; p < W * H; p++) {
        let d = 0;
        for (let c = 0; c < 3; c++) d = Math.max(d, Math.abs(px[p * 3 + c] - bg[p * 3 + c]));
        dist[p] = d;
    }
    // vom Rand aus zusammenhängender Himmel
    const T = 46;
    const sky = new Uint8Array(W * H);
    const stack = [];
    for (let x = 0; x < W; x++) stack.push(x, (H - 1) * W + x);
    for (let y = 0; y < H; y++) stack.push(y * W, y * W + W - 1);
    while (stack.length) {
        const p = stack.pop();
        if (sky[p] || dist[p] >= T) continue;
        sky[p] = 1;
        const x = p % W, y = (p / W) | 0;
        if (x > 0) stack.push(p - 1);
        if (x < W - 1) stack.push(p + 1);
        if (y > 0) stack.push(p - W);
        if (y < H - 1) stack.push(p + W);
    }
    const rgba = Buffer.alloc(W * H * 4);
    for (let p = 0; p < W * H; p++) {
        let a = 1;
        if (sky[p]) { const t = Math.min(1, Math.max(0, (dist[p] - 12) / (T - 12))); a = t * t * (3 - 2 * t); }
        for (let c = 0; c < 3; c++) {
            const v = a > 0.02 ? bg[p * 3 + c] + (px[p * 3 + c] - bg[p * 3 + c]) / a : px[p * 3 + c];
            rgba[p * 4 + c] = Math.max(0, Math.min(255, Math.round(v)));
        }
        rgba[p * 4 + 3] = Math.round(a * 255);
    }

    fs.mkdirSync(OUT, { recursive: true });
    const split = (SPLIT - BOX.left) * F;
    for (const [name, x0, x1] of [['gems', 0, split], ['coins', split, W]]) {
        // knapp um das Sichtbare zuschneiden
        let minX = W, maxX = 0, minY = H, maxY = 0;
        for (let y = 0; y < H; y++) for (let x = x0; x < x1; x++)
            if (rgba[(y * W + x) * 4 + 3] > 8) { minX = Math.min(minX, x); maxX = Math.max(maxX, x); minY = Math.min(minY, y); maxY = Math.max(maxY, y); }
        const pad = 2 * F;
        minX = Math.max(x0, minX - pad); maxX = Math.min(x1 - 1, maxX + pad); minY = Math.max(0, minY - pad); maxY = Math.min(H - 1, maxY + pad);
        const w = maxX - minX + 1, h = maxY - minY + 1;
        const buf = Buffer.alloc(w * h * 4);
        for (let y = 0; y < h; y++) rgba.copy(buf, y * w * 4, ((y + minY) * W + minX) * 4, ((y + minY) * W + maxX + 1) * 4);
        const file = path.join(OUT, name + '.png');
        await sharp(buf, { raw: { width: w, height: h, channels: 4 } }).png().toFile(file);
        // Lage im Probebild (für die Charakteransicht, die ihre gemalten Leisten damit genau überdeckt)
        console.log(name, 'bei', ((BOX.left + (minX + w / 2) / F)).toFixed(1), ((BOX.top + (minY + h / 2) / F)).toFixed(1),
            'Größe', (w / F).toFixed(1), (h / F).toFixed(1), 'Pixel', w, h);
        const meta = file + '.meta';
        if (!fs.existsSync(meta)) {
            let m = fs.readFileSync(META, 'utf8');
            m = m.replace(/^guid: .*$/m, 'guid: ' + crypto.randomBytes(16).toString('hex'))
                .replace('alphaIsTransparency: 0', 'alphaIsTransparency: 1');
            fs.writeFileSync(meta, m);
        }
    }
})().catch(e => { console.error(e); process.exit(1); });
