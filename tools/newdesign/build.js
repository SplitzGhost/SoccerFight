// Schneidet die Spielgrafik aus den Design-Vorlagen (Inspiration/nnewDesign) aus und legt sie als
// PNG + design.json nach SoccerFight/Assets/Resources/NewDesign. Aufruf: node build.js
//
// - Sprite-Sheet und Einzelplattform haben ein aufgemaltes Karomuster statt Transparenz: das wird
//   erkannt und entfernt, danach zerfällt das Bild in einzelne Teile (Zusammenhangskomponenten).
// - Die Kulisse ist das Hauptbild ohne Vordergrund: Baum, Plattformen, Ruinen, Boden werden
//   ausgeschnitten und die Lücken mit passenden Stücken des Hintergrunds aufgefüllt.
// - Alle Texturen werden vormultipliziert (premultiplied alpha) gespeichert, so wie die Shader es erwarten.
'use strict';
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const SRC = path.join(__dirname, '../../Inspiration/nnewDesign');
const OUT = path.join(__dirname, '../../SoccerFight/Assets/Resources/NewDesign');
const F = {
    sheet: 'FCAA83BB-08E3-4670-9714-4EFEE7570777.PNG',
    big: '114E2DF1-722E-4DB0-B22C-036BD960C054.png',
    scene: '59DFF31E-4697-4742-AABD-4BDCACB23303.PNG',
};

// Maßstab: das Hauptbild zeigt die Welt mit ~100 px pro Spieleinheit, das Sprite-Sheet mit ~56 px
// (wird beim Export verdoppelt, damit es in 1080p nicht matscht).
const SCENE_PPU = 100, SHEET_PPU = 56, SHEET_UP = 2;

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

const up4 = v => Math.ceil(v / 4) * 4;

/** Scales (straight alpha, sharp premultiplies internally), pads to a multiple of 4, premultiplies. */
async function finish(img, scale, sharpen) {
    let w = Math.max(1, Math.round(img.w * scale)), h = Math.max(1, Math.round(img.h * scale));
    let p = sharp(img.buf, { raw: { width: img.w, height: img.h, channels: 4 } });
    if (scale != 1) p = p.resize(w, h, { kernel: 'lanczos3' });
    if (sharpen) p = p.sharpen({ sigma: 0.7, m1: 0.6, m2: 1.2 });
    const scaled = await p.raw().toBuffer();
    const pad = 2, W = up4(w + pad * 2), H = up4(h + pad * 2);
    const out = Buffer.alloc(W * H * 4);
    const ox = Math.floor((W - w) / 2), oy = H - h - pad;   // unten bündig (Standlinie), 2 px Rand
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const i = (y * w + x) * 4, o = ((y + oy) * W + x + ox) * 4, a = scaled[i + 3];
        out[o] = Math.round(scaled[i] * a / 255); out[o + 1] = Math.round(scaled[i + 1] * a / 255);
        out[o + 2] = Math.round(scaled[i + 2] * a / 255); out[o + 3] = a;
    }
    return { buf: out, w: W, h: H, ox, oy, iw: w, ih: h };
}

async function save(name, f, opts = {}) {
    const file = path.join(OUT, name + '.png');
    await sharp(f.buf, { raw: { width: f.w, height: f.h, channels: 4 } }).png({ compressionLevel: 9 }).toFile(file);
    writeMeta(file, opts.raw ? 0 : 1);
}

/** Unity-Importeinstellungen: vormultipliziert (kein Alpha-Auffüllen), Mipmaps, Clamp, bis 4096 px. */
function writeMeta(file, compression) {
    const meta = file + '.meta';
    let guid = require('crypto').randomBytes(16).toString('hex');
    if (fs.existsSync(meta)) { const m = /guid: ([0-9a-f]{32})/.exec(fs.readFileSync(meta, 'utf8')); if (m) guid = m[1]; }
    const platform = t => `  - serializedVersion: 4
    buildTarget: ${t}
    maxTextureSize: 4096
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: ${compression}
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
`;
    fs.writeFileSync(meta, `fileFormatVersion: 2
guid: ${guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  isReadable: 0
  streamingMipmaps: 0
  textureFormat: 1
  maxTextureSize: 4096
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 100
  spriteMode: 0
  alphaUsage: 1
  alphaIsTransparency: 0
  textureType: 0
  textureShape: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  platformSettings:
${platform('DefaultTexturePlatform')}${platform('Standalone')}${platform('WebGL')}  userData:
  assetBundleName:
  assetBundleVariant:
`);
}

/** Ordner- und Text-Metadaten (einmal anlegen, GUID bleibt). */
function plainMeta(file, folder) {
    const meta = file + '.meta';
    if (fs.existsSync(meta)) return;
    const guid = require('crypto').randomBytes(16).toString('hex');
    fs.writeFileSync(meta, folder
        ? `fileFormatVersion: 2\nguid: ${guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`
        : `fileFormatVersion: 2\nguid: ${guid}\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
}

/** Topmost row that is (nearly) fully covered: the walkable grass line of a platform. */
function surface(f) {
    const cov = new Array(f.h).fill(0);
    for (let y = 0; y < f.h; y++) for (let x = 0; x < f.w; x++) if (f.buf[(y * f.w + x) * 4 + 3] > 128) cov[y]++;
    const max = Math.max(...cov);
    let row = cov.findIndex(c => c >= max * 0.8);
    const probe = Math.min(f.h - 1, row + 4);
    let x0 = 0, x1 = f.w - 1;
    while (x0 < f.w && f.buf[(probe * f.w + x0) * 4 + 3] < 128) x0++;
    while (x1 > 0 && f.buf[(probe * f.w + x1) * 4 + 3] < 128) x1--;
    return { row, x0, x1 };
}

/**
 * kind: 'prop' (Pivot unten Mitte), 'platform' (Pivot auf der Graskante, Laufbreite gemessen).
 * night: in die dunklen Blau-/Türkistöne des Mittelgrunds umgefärbt.
 */
async function sprite(name, img, ppu, kind = 'prop', opts = {}) {
    if (opts.night) img = nightTone(img, opts.night);
    const f = await finish(img, opts.scale || 1, opts.sharpen);
    await save(name, f);
    const e = { name, w: f.w, h: f.h, ppu: ppu * (opts.scale || 1), px: f.w / 2, py: f.h - (f.oy + f.ih) };
    if (kind == 'platform') {
        const s = surface(f);
        e.py = f.h - s.row - 3;             // Standlinie ein paar Pixel unter der Grasoberkante
        e.px = (s.x0 + s.x1) / 2;
        e.sx0 = s.x0; e.sx1 = s.x1;
    }
    if (opts.anchors) e.anchors = opts.anchors;
    manifest.sprites.push(e);
    return e;
}

/** Leuchthof-Reste am Rand (Laternen): helle Pixel nahe der Transparenz entfernen. */
function trimGlow(img, thr = 150, passes = 8) {
    const { w, h } = img, buf = Buffer.from(img.buf);
    for (let pass = 0; pass < passes; pass++) {
        const kill = [];
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
            const o = (y * w + x) * 4;
            if (!buf[o + 3]) continue;
            const edge = x == 0 || y == 0 || x == w - 1 || y == h - 1 || !buf[o - 1] || !buf[o + 7] || !buf[o - w * 4 + 3] || !buf[o + w * 4 + 3];
            if (edge && Math.min(buf[o], buf[o + 1], buf[o + 2]) > thr) kill.push(o);
        }
        if (!kill.length) break;
        for (const o of kill) buf[o + 3] = 0;
    }
    return { buf, w, h };
}

/** Gradient map by luminance into the moonlit mid-ground palette (keeps alpha). */
function nightTone(img, tone) {
    const [lo, hi] = tone == 'deep' ? [[8, 18, 42], [42, 92, 120]] : [[12, 28, 52], [70, 140, 150]];
    const buf = Buffer.from(img.buf);
    for (let i = 0; i < img.w * img.h; i++) {
        const o = i * 4;
        if (!buf[o + 3]) continue;
        const l = Math.pow((0.3 * buf[o] + 0.59 * buf[o + 1] + 0.11 * buf[o + 2]) / 255, 0.9);
        for (let c = 0; c < 3; c++) buf[o + c] = Math.round(lo[c] + (hi[c] - lo[c]) * l);
    }
    return { buf, w: img.w, h: img.h };
}

// ------------------------------------------------------------------ Kulisse: Hauptbild ohne Vordergrund

async function plate() {
    const { data, info } = await sharp(path.join(SRC, F.scene)).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    const W = info.width, H = 700;
    const img = new Float32Array(W * H * 3);
    for (let i = 0; i < W * H * 3; i++) img[i] = data[i];
    // Vordergrund: Baum links, Plattformen, Torbogen, Galgenlaterne, Turm rechts, alles am Boden
    const HOLES = [[0, 0, 396, 172], [0, 172, 232, 420], [160, 100, 316, 290], [204, 246, 574, 540], [0, 300, 290, 700],
        [1612, 150, 1983, 700], [1086, 352, 1410, 572], [1298, 505, 1392, 670], [594, 466, 724, 700], [290, 540, 480, 700],
        [480, 600, 560, 700], [740, 580, 985, 700], [985, 630, 1300, 700], [1300, 600, 1612, 700]];
    const BAND = 80, RING = 10, FEATHER = 12;
    const hole = new Uint8Array(W * H), filled = new Uint8Array(W * H);
    for (const [x0, y0, x1, y1] of HOLES) for (let y = y0; y < Math.min(H, y1); y++) for (let x = x0; x < Math.min(W, x1); x++) hole[y * W + x] = 1;
    const I = new Int32Array((W + 1) * (H + 1));
    const rebuild = () => {
        for (let y = 0; y < H; y++) { let s = 0; for (let x = 0; x < W; x++) { s += hole[y * W + x] && !filled[y * W + x] ? 1 : 0; I[(y + 1) * (W + 1) + x + 1] = I[y * (W + 1) + x + 1] + s; } }
    };
    const open = (x0, y0, x1, y1) => I[y1 * (W + 1) + x1] - I[y0 * (W + 1) + x1] - I[y1 * (W + 1) + x0] + I[y0 * (W + 1) + x0];
    rebuild();
    // jedes Loch streifenweise von oben: das Stück des Bildes suchen, dessen Rand am besten passt
    for (const [hx0, hy0, hx1, hy1] of HOLES) for (let by0 = hy0; by0 < Math.min(H, hy1); by0 += BAND) {
        const bx0 = hx0, bx1 = Math.min(W, hx1), by1 = Math.min(H, hy1, by0 + BAND);
        const tx0 = bx0 - RING, tx1 = bx1 + RING, ty0 = by0 - RING, ty1 = by1 + RING;
        let best = null, bestS = 1e30;
        for (const R of [60, 200]) {
            if (best) break;
            for (let dy = -R; dy <= R; dy += 6) for (let dx = -W; dx <= W; dx += 6) {
                if (!dx && !dy) continue;
                const sx0 = tx0 + dx, sy0 = ty0 + dy, sx1 = tx1 + dx, sy1 = ty1 + dy;
                if (sx0 < 0 || sy0 < 0 || sx1 > W || sy1 > H || open(sx0, sy0, sx1, sy1) > 0) continue;
                let s = 0, n = 0;
                for (let y = ty0; y < ty1; y += 2) for (let x = tx0; x < tx1; x += 2) {
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    if (x >= bx0 && x < bx1 && y >= by0 && y < by1) continue;
                    const p = y * W + x;
                    if (hole[p] && !filled[p]) continue;
                    const q = (y + dy) * W + x + dx;
                    const dr = img[p * 3] - img[q * 3], dg = img[p * 3 + 1] - img[q * 3 + 1], db = img[p * 3 + 2] - img[q * 3 + 2];
                    s += dr * dr + dg * dg + db * db; n++;
                }
                if (n < 20) continue;
                s = s / n + Math.abs(dy) * 4;
                if (s < bestS) { bestS = s; best = [dx, dy]; }
            }
        }
        if (!best) { console.log('  Kulisse: keine Quelle für', bx0, by0); continue; }
        const [dx, dy] = best;
        for (let y = by0 - FEATHER; y < by1 + FEATHER; y++) for (let x = bx0 - FEATHER; x < bx1 + FEATHER; x++) {
            if (x < 0 || y < 0 || x >= W || y >= H) continue;
            const p = y * W + x, inside = x >= bx0 && x < bx1 && y >= by0 && y < by1;
            const d = Math.max(bx0 - x, 0, x - (bx1 - 1), by0 - y, 0, y - (by1 - 1));
            let a = d == 0 ? 1 : 1 - d / FEATHER;
            if (a <= 0) continue;
            if (hole[p] && !filled[p]) { if (!inside) continue; a = 1; }
            const q = (y + dy) * W + x + dx;
            for (let c = 0; c < 3; c++) img[p * 3 + c] = img[p * 3 + c] * (1 - a) + img[q * 3 + c] * a;
        }
        for (let y = by0; y < by1; y++) for (let x = bx0; x < bx1; x++) filled[y * W + x] = 1;
        rebuild();
    }
    const out = Buffer.alloc(W * H * 4);
    for (let i = 0; i < W * H; i++) {
        for (let c = 0; c < 3; c++) out[i * 4 + c] = Math.max(0, Math.min(255, Math.round(img[i * 3 + c])));
        out[i * 4 + 3] = 255;
    }
    // Himmelsfarbe an der Oberkante (ohne Sterne): der Himmel darüber wird damit weitergemalt
    const top = [0, 0, 0]; let n = 0;
    for (let y = 0; y < 3; y++) for (let x = 0; x < W; x++) {
        const o = (y * W + x) * 4;
        if (out[o] + out[o + 1] + out[o + 2] > 200) continue;
        top[0] += out[o]; top[1] += out[o + 1]; top[2] += out[o + 2]; n++;
    }
    manifest.skyTop = top.map(v => +(v / n / 255).toFixed(4));
    // die obersten Zeilen laufen in diese Farbe aus, damit keine Kante zum weitergemalten Himmel bleibt
    const FADE_TOP = 90;
    for (let y = 0; y < FADE_TOP; y++) {
        const t = y / FADE_TOP, k = t * t * (3 - 2 * t);
        for (let x = 0; x < W; x++) for (let c = 0; c < 3; c++) { const o = (y * W + x) * 4 + c; out[o] = Math.round(top[c] / n * (1 - k) + out[o] * k); }
    }
    const f = { buf: out, w: up4(W), h: H };
    if (f.w != W) {   // auf ein Vielfaches von 4 auffüllen (letzte Spalte wiederholen)
        const b = Buffer.alloc(f.w * H * 4);
        for (let y = 0; y < H; y++) for (let x = 0; x < f.w; x++) out.copy(b, (y * f.w + x) * 4, (y * W + Math.min(x, W - 1)) * 4, (y * W + Math.min(x, W - 1)) * 4 + 4);
        f.buf = b;
    }
    await save('plate', f, { raw: true });
    // Standlinie des Bodens im Hauptbild: Zeile 703
    manifest.sprites.push({ name: 'plate', w: f.w, h: f.h, ppu: SCENE_PPU, px: f.w / 2, py: f.h - 703 });
    manifest.plateGroundRow = 703;
}

// ------------------------------------------------------------------ Boden: Grasdecke über Quadermauer, kachelbar

async function ground() {
    const X0 = 560, X1 = 1980, Y0 = 694, Y1 = 793, FADE = 90;
    const { data, info } = await sharp(path.join(SRC, F.scene)).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    const SW = info.width;
    // unter dem Bild geht die Mauer mit wiederholten Quaderreihen weiter (Moos dort zu Stein), und das Ganze
    // dunkelt nach unten gleichmäßig ab bis in die Farbe der Tiefe: keine sichtbare Kante am unteren Bildrand
    const L = X1 - X0 - FADE, EXTRA = 42 * 3, DEEP = [10, 11, 24];
    const H = (Y1 - Y0) + EXTRA, W = L;
    const out = Buffer.alloc(W * H * 4);
    const px = (x, y) => { const i = (y * SW + x) * 3; return [data[i], data[i + 1], data[i + 2]]; };
    const sample = (x, y) => {
        // die ersten FADE Spalten mischen sich mit dem Ende des Streifens → nahtlos kachelbar
        const a = px(X0 + x, y);
        if (x >= FADE) return a;
        const b = px(X0 + L + x, y), t = x / FADE;
        return [a[0] * t + b[0] * (1 - t), a[1] * t + b[1] * (1 - t), a[2] * t + b[2] * (1 - t)];
    };
    // unterhalb: alles in Steinfarbe (Moos wird zu Stein, zu helle Stellen gedeckelt)
    const stone = c => {
        const l = Math.min(70, 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2]);
        return [l * 0.78, l * 0.82, l * 1.35];
    };
    const smooth = t => t * t * (3 - 2 * t);
    for (let row = 0; row < H; row++) {
        const fromSrc = row < Y1 - Y0;
        // gespiegelt an der Unterkante des Bildes, dann hin und her: keine Naht zwischen den Wiederholungen
        const m = row - (Y1 - Y0), t = ((m % 84) + 84) % 84;
        const sy = fromSrc ? Y0 + row : t < 42 ? Y1 - 1 - t : Y1 - 42 + (t - 42);
        const shift = 0;
        // Abdunkeln ab der zweiten Quaderreihe, die letzten Zeilen laufen in DEEP aus
        const dark = 1 - 0.72 * smooth(Math.max(0, Math.min(1, (row - 50) / (H - 50))));
        const toDeep = smooth(Math.max(0, (row - (H - 36)) / 36));
        for (let x = 0; x < W; x++) {
            let c = sample((x + shift) % W, sy);
            if (!fromSrc) c = stone(c);
            const o = (row * W + x) * 4;
            for (let k = 0; k < 3; k++) out[o + k] = Math.round(c[k] * dark * (1 - toDeep) + DEEP[k] * toDeep);
            out[o + 3] = 255;
        }
    }
    manifest.groundDeep = DEEP.map(v => +(v / 255).toFixed(4));
    // oberste Pixelreihen weich ausblenden (die Grashalme darüber verdecken die Kante)
    for (let y = 0; y < 3; y++) for (let x = 0; x < W; x++) {
        const o = (y * W + x) * 4, a = [0.35, 0.7, 0.9][y];
        out[o] *= a; out[o + 1] *= a; out[o + 2] *= a; out[o + 3] = 255 * a;
    }
    await save('ground', { buf: out, w: W, h: H }, { raw: true });
    manifest.sprites.push({ name: 'ground', w: W, h: H, ppu: SCENE_PPU, px: 0, py: H - (703 - Y0), tile: true });
}

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
    const S = (x, y, extra) => partRGBA(sheet, x, y, extra);
    const P = SHEET_PPU, up = { scale: SHEET_UP, sharpen: true };

    // schwebende Plattformen (Pivot = Mitte der Graskante)
    await sprite('plat_banner', S(583, 10), P, 'platform', up);
    await sprite('plat_flowers', S(833, 14), P, 'platform', up);
    await sprite('plat_ferns', S(813, 157), P, 'platform', up);
    await sprite('plat_small', S(589, 172), P, 'platform', up);
    await sprite('plat_crystal', S(662, 247), P, 'platform', up);
    await sprite('plat_wide', S(332, 196), P, 'platform', up);

    // große Einzelplattform in hoher Auflösung
    console.log('Einzelplattform …');
    const big = await segment(F.big, 200);
    const bc = big.comps.sort((a, b) => b.a - a.a)[0];
    await sprite('plat_big', partRGBA(big, bc.x, bc.y), 300, 'platform', { scale: 0.5 });

    // Bäume (hell: Rahmen vorn; nacht: Mittelgrund)
    for (const [n, x, y] of [['tree_big', 998, 0], ['tree_a', 1232, 7], ['tree_b', 1347, 10], ['tree_c', 1464, 96], ['tree_d', 1220, 254]]) {
        await sprite(n, S(x, y), P, 'prop', up);
        await sprite(n + '_night', S(x, y), P, 'prop', { ...up, night: 'mid' });
    }
    // Ruinen, Säulen, Mauern
    for (const [n, x, y] of [['arch', 921, 385], ['wall_a', 1126, 413], ['wall_b', 754, 433], ['wall_c', 1344, 463], ['wall_d', 1459, 469],
        ['wall_e', 1254, 454], ['column_a', 850, 446], ['column_b', 671, 470], ['pillar_block', 596, 477], ['pillar_broken', 839, 342]]) {
        await sprite(n, S(x, y), P, 'prop', up);
    }
    for (const [n, x, y] of [['arch', 921, 385], ['wall_a', 1126, 413], ['wall_c', 1344, 463], ['column_a', 850, 446], ['falls_a', 1409, 595], ['falls_b', 1409, 786]])
        await sprite(n + '_night', S(x, y), P, 'prop', { ...up, night: 'deep' });
    // Steine, Blöcke
    for (const [n, x, y] of [['rock_a', 490, 413], ['rock_b', 431, 417], ['rock_c', 321, 472], ['rock_d', 247, 511], ['rock_e', 156, 522],
        ['block_a', 89, 525], ['block_b', 13, 528], ['block_c', 318, 528], ['block_d', 407, 534], ['block_e', 1264, 536], ['block_f', 1187, 539], ['block_g', 1135, 549]])
        await sprite(n, S(x, y), P, 'prop', up);
    // Requisiten
    for (const [n, x, y] of [['stump', 1180, 167], ['fence', 1284, 176], 
        ['crate', 1284, 255], ['barrel', 1455, 315], ['crystal_big', 1217, 335], ['crystal_mid', 1386, 411], ['crystal_small', 1454, 429],
        ['falls_a', 1409, 595], ['falls_b', 1409, 786], ['mushroom_blue', 658, 822]])
        await sprite(n, S(x, y), P, 'prop', up);
    await sprite('lantern_post_a', trimGlow(S(1438, 176)), P, 'prop', up);
    await sprite('lantern_post_b', trimGlow(S(1372, 192)), P, 'prop', up);
    // Grasplatten und Bodenstücke (für Terrassen und Sockel)
    for (const [n, x, y] of [['ledge_long', 64, 10], ['ledge_block', 342, 10], ['ledge_pair', 91, 395], ['ledge_tall', 465, 10]])
        await sprite(n, S(x, y), P, 'platform', up);

    console.log('Pflanzen-Atlas …');
    await plants(sheet);
    console.log('Kulisse …');
    await plate();
    console.log('Boden …');
    await ground();

    fs.writeFileSync(path.join(OUT, 'design.json'), JSON.stringify(manifest, null, 1));
    plainMeta(path.join(OUT, 'design.json'), false);
    plainMeta(OUT, true);
    console.log('fertig:', manifest.sprites.length, 'Sprites,', manifest.plants.length, 'Pflanzen');
})().catch(e => { console.error(e); process.exit(1); });
