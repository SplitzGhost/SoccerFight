// Baut die Spielwelt aller Stages aus den Stage-Bögen (Inspiration/StagesNewDesigns), gesteuert von
// stages.def.js. Ergebnis pro Stage: SoccerFight/Assets/Resources/Stages/<id>/*.png + stage.json.
//
//   node stages.js              alle Stages
//   node stages.js glut frost   nur diese
//   node stages.js --fast       Kulissen nicht neu auffüllen (vorhandene plate.png bleibt)
//
// Pro Stage:
// - Kulisse (plate): die Szene ohne Vordergrund. Plattformen, Figur, Rahmenobjekte werden entfernt und
//   inhaltsbasiert aufgefüllt (inpaint.js); unter der Bodenlinie wird die Kulisse weitergemalt, damit bei
//   hoher Kamera keine Kante sichtbar wird.
// - Boden (ground): der Mauer-/Erdstreifen unter der Figur, Lücken gefüllt, nach unten weitergemalt und
//   abgedunkelt, mit einer Naht nach „Image Quilting“ nahtlos kachelbar.
// - Einzelteile aus den Nahaufnahmen: freigestellt (cutout.js), vermessen (Lauffläche, Lichtpunkte) und
//   verdoppelt. Dazu abgeleitete Teile: Plattformköpfe ohne Säule (schwebend) und hängende Bretter/Balken
//   mit kachelbarem Seil bzw. Kette.
// Alle Texturen außer der Kulisse sind vormultipliziert (premultiplied alpha), wie die Shader es erwarten.
'use strict';
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');
const { load, grid, cut } = require('./cutout');
const { inpaint } = require('./inpaint');
const { up4, finish, save, plainMeta } = require('./texio');
const DEFS = require('./stages.def');

const SRC = path.join(__dirname, '../../Inspiration/StagesNewDesigns');
const OUT = path.join(__dirname, '../../SoccerFight/Assets/Resources/Stages');
const CACHE = path.join(__dirname, '.cache');   // Farben der Kulissen für --fast
const PPU = 53;          // Nahaufnahmen und Boden: Pixel pro Spieleinheit (die Figur ist ~95 px ≈ 1,8 Einheiten groß)
const PLATE_PPU = 44;    // Kulisse: etwas größer gezeigt, sie steht weiter hinten
const UP = 2;            // alles wird verdoppelt: 1080p zeigt ~110 px pro Einheit
const PLATE_BELOW = 110; // so viele Zeilen wird die Kulisse unter ihrer Unterkante weitergemalt
const PLATE_CUT = 14;     // die untersten Zeilen der Szene gehören schon zur Bodenkante
const GROUND_BELOW = 44; // so viele Zeilen der Boden unter dem Bogenrand

const args = process.argv.slice(2);
const fast = args.includes('--fast');
const only = args.filter(a => !a.startsWith('--'));

const smooth = t => { t = Math.max(0, Math.min(1, t)); return t * t * (3 - 2 * t); };
const lum = (r, g, b) => 0.3 * r + 0.59 * g + 0.11 * b;
const round3 = v => +v.toFixed(3);
const col = a => a.map(v => round3(v / 255));

// ------------------------------------------------------------------ Kulisse

async function plate(img, def, man, dir) {
    const { W, rgb } = img;
    const [cx0, cx1] = def.scene.crop, h0 = def.scene.walk - PLATE_CUT;
    const w = cx1 - cx0, H = h0 + PLATE_BELOW;
    const file = path.join(dir, 'plate.png');
    let px;
    if (fast && fs.existsSync(file)) {
        console.log('  Kulisse: vorhandene behalten');
    } else {
        px = new Float32Array(w * H * 3);
        for (let y = 0; y < h0; y++) for (let x = 0; x < w; x++) for (let c = 0; c < 3; c++) px[(y * w + x) * 3 + c] = rgb[(y * W + cx0 + x) * 3 + c];
        const hole = new Uint8Array(w * H);
        for (const [x0, y0, x1, y1] of def.scene.holes)
            for (let y = Math.max(0, y0); y < Math.min(h0, y1); y++) for (let x = Math.max(0, x0 - cx0); x < Math.min(w, x1 - cx0); x++) hole[y * w + x] = 1;
        const src = new Uint8Array(w * H);
        for (let i = 0; i < w * h0; i++) src[i] = hole[i] ? 0 : 1;
        // fill a slightly larger area and blend its rim into the original: no hard rectangle edges
        const FEATHER = 10;
        const dist = new Float32Array(w * h0).fill(1e9);
        for (let y = 0; y < h0; y++) for (let x = 0; x < w; x++) if (hole[y * w + x]) dist[y * w + x] = 0;
        for (let pass = 0; pass < 2; pass++) for (let y = 0; y < h0; y++) for (let x = 0; x < w; x++) {
            const i = pass ? (h0 - 1 - y) * w + (w - 1 - x) : y * w + x, xx = i % w, yy = (i / w) | 0, st = pass ? 1 : -1;
            if (xx + st >= 0 && xx + st < w) dist[i] = Math.min(dist[i], dist[i + st] + 1);
            if (yy + st >= 0 && yy + st < h0) dist[i] = Math.min(dist[i], dist[i + st * w] + 1);
        }
        const grown = new Uint8Array(w * h0);
        for (let i = 0; i < w * h0; i++) { grown[i] = dist[i] <= FEATHER ? 1 : 0; src[i] = grown[i] ? 0 : 1; }
        // parts that stay in the picture but must not be copied into the holes (a striking foreground tree)
        for (const [x0, y0, x1, y1] of def.scene.keep || [])
            for (let y = Math.max(0, y0); y < Math.min(h0, y1); y++) for (let x = Math.max(0, x0 - cx0); x < Math.min(w, x1 - cx0); x++) src[y * w + x] = 0;
        const orig = px.slice(0, w * h0 * 3);
        const top = inpaint(orig, w, h0, grown, src, {});
        for (let i = 0; i < w * h0; i++) {
            const k = dist[i] == 0 ? 1 : dist[i] <= FEATHER ? 1 - smooth(dist[i] / FEATHER) : 0;
            for (let c = 0; c < 3; c++) px[i * 3 + c] = orig[i * 3 + c] * (1 - k) + top[i * 3 + c] * k;
        }
        // below: the rows above mirrored and blurred more and more (mist over the valley)
        for (let y = h0; y < H; y++) {
            const d = y - h0, sy = Math.max(0, h0 - 1 - Math.min(d, 60)), r = 2 + Math.round(d * 0.25);
            let acc = [0, 0, 0], n = 0;
            const row = x => (sy * w + Math.min(w - 1, Math.max(0, x))) * 3;
            for (let x = -r; x <= r; x++) { const o = row(x); acc[0] += px[o]; acc[1] += px[o + 1]; acc[2] += px[o + 2]; n++; }
            for (let x = 0; x < w; x++) {
                for (let c = 0; c < 3; c++) px[(y * w + x) * 3 + c] = acc[c] / n;
                const a = row(x - r), b = row(x + r + 1);
                for (let c = 0; c < 3; c++) acc[c] += px[b + c] - px[a + c];
            }
        }
        // the continuation darkens into the valley colour
        const valley = [0, 0, 0]; let n = 0;
        for (let y = h0 - 60; y < h0; y++) for (let x = 0; x < w; x++) { for (let c = 0; c < 3; c++) valley[c] += px[(y * w + x) * 3 + c]; n++; }
        for (let c = 0; c < 3; c++) valley[c] = valley[c] / n * 0.55;
        for (let y = h0; y < H; y++) {
            const k = smooth((y - h0) / PLATE_BELOW) * 0.85;
            for (let x = 0; x < w; x++) for (let c = 0; c < 3; c++) { const o = (y * w + x) * 3 + c; px[o] = px[o] * (1 - k) + valley[c] * k; }
        }
        // top: sky colour from the top rows (median ignores stars), the top rows fade into it
        const sky = [0, 1, 2].map(c => { const a = []; for (let y = 0; y < 4; y++) for (let x = 0; x < w; x++) a.push(px[(y * w + x) * 3 + c]); a.sort((p, q) => p - q); return a[a.length >> 1]; });
        const FADE = 24;
        for (let y = 0; y < FADE; y++) { const k = 1 - smooth(y / FADE); for (let x = 0; x < w; x++) for (let c = 0; c < 3; c++) { const o = (y * w + x) * 3 + c; px[o] = px[o] * (1 - k) + sky[c] * k; } }
        const haze = [0, 0, 0]; n = 0;
        for (let y = Math.round(h0 * 0.55); y < Math.round(h0 * 0.9); y++) for (let x = 0; x < w; x += 2) { for (let c = 0; c < 3; c++) haze[c] += px[(y * w + x) * 3 + c]; n++; }
        man.sky = col(sky); man.valley = col(valley); man.haze = col(haze.map(v => v / n));
        const buf = Buffer.alloc(w * H * 3);
        for (let i = 0; i < w * H * 3; i++) buf[i] = Math.max(0, Math.min(255, Math.round(px[i])));
        const Wt = up4(w * UP), Ht = up4(H * UP);
        const big = await sharp(buf, { raw: { width: w, height: H, channels: 3 } }).resize(Wt, Ht, { kernel: 'lanczos3', fit: 'fill' })
            .sharpen({ sigma: 0.6, m1: 0.4, m2: 0.8 }).raw().toBuffer();
        await save(file, { buf: big, w: Wt, h: Ht }, { channels: 3 });
        fs.mkdirSync(CACHE, { recursive: true });
        fs.writeFileSync(path.join(CACHE, def.id + '.plate.json'), JSON.stringify({ sky: man.sky, valley: man.valley, haze: man.haze }));
    }
    if (!man.sky) Object.assign(man, JSON.parse(fs.readFileSync(path.join(CACHE, def.id + '.plate.json'), 'utf8')));
    const Wt = up4(w * UP), Ht = up4(H * UP);
    // where the plate sits in the sheet (the game puts its lights on the painted lamps, moon …)
    man.plateX0 = cx0; man.plateRow0 = h0; man.plateW = w; man.platePpu = PLATE_PPU;
    man.sprites.push({ name: 'plate', role: 'plate', w: Wt, h: Ht, ppu: PLATE_PPU * UP * (Ht / (H * UP)), px: Wt / 2, py: PLATE_BELOW * UP * (Ht / (H * UP)) });
}

// ------------------------------------------------------------------ Boden

async function ground(img, g, def, man, dir) {
    const { W, rgb } = img;
    const walk = def.scene.walk, pad = def.ground.pad, [gx0, gx1] = def.ground.x;
    const top = walk - pad, bot = g.sceneBottom - 1;
    const w = gx1 - gx0, h0 = bot - top, H = h0 + GROUND_BELOW;
    let px = new Float32Array(w * H * 3);
    for (let y = 0; y < h0; y++) for (let x = 0; x < w; x++) for (let c = 0; c < 3; c++) px[(y * w + x) * 3 + c] = rgb[((top + y) * W + gx0 + x) * 3 + c];
    const hole = new Uint8Array(w * H);
    for (const [hx0, hx1] of def.ground.holes)
        for (let y = 0; y < pad + 5; y++) for (let x = Math.max(0, hx0 - gx0); x < Math.min(w, hx1 - gx0); x++) hole[y * w + x] = 1;
    for (let y = h0; y < H; y++) for (let x = 0; x < w; x++) hole[y * w + x] = 1;
    const src = new Uint8Array(w * H);
    for (let i = 0; i < w * H; i++) src[i] = hole[i] ? 0 : 1;
    px = inpaint(px, w, H, hole, src, { yMap: y => y < h0 ? y : Math.max(pad + 20, 2 * h0 - y - 6), yWeight: 3 });

    // seamless: the first OV columns become a seam-cut blend of the strip's end and start
    const OV = 90, T = w - OV;
    const e = new Float32Array(H * OV);
    for (let y = 0; y < H; y++) for (let i = 0; i < OV; i++) {
        let s = 0; for (let c = 0; c < 3; c++) { const d = px[(y * w + T + i) * 3 + c] - px[(y * w + i) * 3 + c]; s += d * d; }
        e[y * OV + i] = s;
    }
    const acc = new Float32Array(H * OV), from = new Int8Array(H * OV);
    for (let i = 0; i < OV; i++) acc[i] = e[i] + (i < 8 || i > OV - 8 ? 1e9 : 0);
    for (let y = 1; y < H; y++) for (let i = 0; i < OV; i++) {
        let best = Infinity, bd = 0;
        for (let d = -1; d <= 1; d++) { const j = i + d; if (j < 0 || j >= OV) continue; if (acc[(y - 1) * OV + j] < best) { best = acc[(y - 1) * OV + j]; bd = d; } }
        acc[y * OV + i] = best + e[y * OV + i] + (i < 8 || i > OV - 8 ? 1e9 : 0); from[y * OV + i] = bd;
    }
    const seam = new Int32Array(H);
    { let bi = 0; for (let i = 1; i < OV; i++) if (acc[(H - 1) * OV + i] < acc[(H - 1) * OV + bi]) bi = i; for (let y = H - 1; y >= 0; y--) { seam[y] = bi; if (y > 0) bi += from[y * OV + bi]; } }
    const tile = new Float32Array(T * H * 3);
    for (let y = 0; y < H; y++) for (let x = 0; x < T; x++) for (let c = 0; c < 3; c++) {
        let v;
        if (x < OV) {
            const a = px[(y * w + T + x) * 3 + c], b = px[(y * w + x) * 3 + c];
            const k = smooth((x - seam[y] + 2) / 4);
            v = a * (1 - k) + b * k;
        } else v = px[(y * w + x) * 3 + c];
        tile[(y * T + x) * 3 + c] = v;
    }
    // darker with depth, the last rows run into the deep colour; the top rows fade in softly
    const deep = [0, 0, 0]; let n = 0;
    for (let y = H - GROUND_BELOW; y < H; y++) for (let x = 0; x < T; x++) { for (let c = 0; c < 3; c++) deep[c] += tile[(y * T + x) * 3 + c]; n++; }
    for (let c = 0; c < 3; c++) deep[c] = deep[c] / n * 0.32;
    const buf = Buffer.alloc(T * H * 4);
    for (let y = 0; y < H; y++) {
        const dark = 1 - 0.55 * smooth((y - pad - 45) / (H - pad - 45));
        const toDeep = smooth((y - (H - 22)) / 22);
        const a = y == 0 ? 0.35 : y == 1 ? 0.7 : y == 2 ? 0.9 : 1;
        for (let x = 0; x < T; x++) {
            const o = (y * T + x) * 4;
            for (let c = 0; c < 3; c++) buf[o + c] = Math.max(0, Math.min(255, Math.round(tile[(y * T + x) * 3 + c] * dark * (1 - toDeep) + deep[c] * toDeep)));
            buf[o + 3] = Math.round(a * 255);
        }
    }
    // rows above the walk line: keep only what looks like the surface material (moss, snow, leaves)
    const pal = [];
    for (let y = pad + 1; y < pad + 6; y++) for (let x = 3; x < T; x += 37) pal.push([0, 1, 2].map(c => tile[(y * T + x) * 3 + c]));
    for (let y = 0; y < pad + 1; y++) for (let x = 0; x < T; x++) {
        let best = 1e9;
        for (const p of pal) { const d = (tile[(y * T + x) * 3] - p[0]) ** 2 + (tile[(y * T + x) * 3 + 1] - p[1]) ** 2 + (tile[(y * T + x) * 3 + 2] - p[2]) ** 2; if (d < best) best = d; }
        const a = 1 - smooth((Math.sqrt(best) - 22) / 34);
        const o = (y * T + x) * 4 + 3;
        buf[o] = Math.round(buf[o] * (y >= pad - 1 ? Math.max(a, 0.85) : a));
    }
    man.deep = col(deep);
    const f = await finish({ buf, w: T, h: H }, UP, true, { pad: 0 });
    // finish pads to a multiple of 4 and centres horizontally: crop back to the exact tile width
    const Tw = T * UP, Th = f.h;
    const out = Buffer.alloc(Tw * Th * 4);
    for (let y = 0; y < Th; y++) f.buf.copy(out, y * Tw * 4, (y * f.w + f.ox) * 4, (y * f.w + f.ox + Tw) * 4);
    await save(path.join(dir, 'ground.png'), { buf: out, w: Tw, h: Th });
    man.sprites.push({ name: 'ground', role: 'ground', w: Tw, h: Th, ppu: PPU * UP, px: 0, py: Th - (f.oy + pad * UP) });
}

// ------------------------------------------------------------------ Einzelteile

function coverage(c) {
    const cov = new Array(c.h).fill(0);
    for (let y = 0; y < c.h; y++) for (let x = 0; x < c.w; x++) if (c.buf[(y * c.w + x) * 4 + 3] > 128) cov[y]++;
    return cov;
}

function span(c, row) {
    let x0 = 0, x1 = c.w - 1;
    row = Math.max(0, Math.min(c.h - 1, row));
    while (x0 < c.w && c.buf[(row * c.w + x0) * 4 + 3] < 128) x0++;
    while (x1 > 0 && c.buf[(row * c.w + x1) * 4 + 3] < 128) x1--;
    return [x0, x1];
}

/** Walkable top: the first row that is (nearly) as wide as the widest. */
function surface(c, frac = 0.8) {
    const cov = coverage(c), max = Math.max(...cov);
    const row = cov.findIndex(v => v >= max * frac);
    const [x0, x1] = span(c, row + 4);
    return { row, x0, x1, cov, max };
}

/** Second walkable band below the first (the plank in a swing frame, the anvil under its beam). */
function secondBand(c, s) {
    const { cov, max } = s;
    let y = s.row;
    while (y < c.h && cov[y] >= max * 0.6) y++;          // leave the top band
    while (y < c.h && cov[y] < max * 0.6) y++;           // the gap (posts only)
    if (y >= c.h - 4) return null;
    // walk span: between the posts (skip the outer 18 % where the posts stand)
    const probe = y + 3, m = Math.round(c.w * 0.18);
    let x0 = m, x1 = c.w - 1 - m;
    while (x0 < x1 && c.buf[(probe * c.w + x0) * 4 + 3] < 128) x0++;
    while (x1 > x0 && c.buf[(probe * c.w + x1) * 4 + 3] < 128) x1--;
    return { row: y, x0, x1 };
}

/** Brightest spot in the light's colour: centre + radius (source pixels). */
function lightSpot(c, light) {
    const L = Math.hypot(...light), n = light.map(v => v / L);
    let sx = 0, sy = 0, sw = 0, cnt = 0;
    for (let y = 0; y < c.h; y++) for (let x = 0; x < c.w; x++) {
        const o = (y * c.w + x) * 4; if (c.buf[o + 3] < 128) continue;
        const r = c.buf[o], g = c.buf[o + 1], b = c.buf[o + 2], m = Math.hypot(r, g, b);
        if (m < 1) continue;
        const cos = (r * n[0] + g * n[1] + b * n[2]) / m;
        const l = lum(r, g, b);
        if (cos < 0.93 || l < 120) continue;
        const wgt = (cos - 0.93) * l;
        sx += x * wgt; sy += y * wgt; sw += wgt; cnt++;
    }
    if (cnt < 12) return null;
    return { x: sx / sw, y: sy / sw, r: Math.max(6, Math.sqrt(cnt / Math.PI) * 1.6) };
}

/** Saves a cut piece; kind decides the pivot. */
async function piece(dir, man, name, role, c, o = {}) {
    const f = await finish(c, UP, true, { top: role == 'hangprop' });
    await save(path.join(dir, name + '.png'), f);
    const e = { name, role, tags: o.tags || [], w: f.w, h: f.h, ppu: PPU * UP };
    // pivot: bottom centre (props), top centre (hanging props), walk line (platforms)
    const toU = (x, y) => [round3((f.ox + x * UP - e.px) / e.ppu), round3((f.h - (f.oy + y * UP) - e.py) / e.ppu)];
    const hanging = (o.tags || []).includes('hang') || (o.tags || []).includes('ceil');
    if (role == 'float' || role == 'stand' || role == 'end') {
        const s = surface(c, o.walkFrac || 0.8);
        const walkRow = s.row + (o.walkDown ?? 3);
        e.px = f.ox + (s.x0 + s.x1) / 2 * UP;
        e.py = f.h - (f.oy + walkRow * UP);
        e.walk0 = toU(s.x0 + 4, 0)[0]; e.walk1 = toU(s.x1 - 4, 0)[0];
        e.bottom = round3(-(c.h - walkRow) / PPU);
        if (o.levels) {
            const b = secondBand(c, s);
            if (b) e.levels = [{ y: toU(0, b.row + 3)[1], x0: toU(b.x0 + 4, 0)[0], x1: toU(b.x1 - 4, 0)[0] }];
        }
        if (o.anchors) e.anchors = o.anchors.map(ax => toU(ax, 0)[0]);
    } else {
        e.px = f.ox + f.iw / 2;
        e.py = hanging ? f.h - f.oy : f.h - (f.oy + f.ih);
    }
    if (o.light) {
        const l = lightSpot(c, o.light);
        if (l) { const [x, y] = toU(l.x, l.y); e.lights = [{ x, y, r: round3(l.r / PPU), cr: round3(o.light[0] / 255), cg: round3(o.light[1] / 255), cb: round3(o.light[2] / 255) }]; }
    }
    if (o.rope) e.rope = o.rope;
    man.sprites.push(e);
    return e;
}

function crop(c, x0, y0, x1, y1) {
    x0 = Math.max(0, x0); y0 = Math.max(0, y0); x1 = Math.min(c.w, x1); y1 = Math.min(c.h, y1);
    const w = x1 - x0, h = y1 - y0, buf = Buffer.alloc(w * h * 4);
    for (let y = 0; y < h; y++) c.buf.copy(buf, y * w * 4, ((y0 + y) * c.w + x0) * 4, ((y0 + y) * c.w + x1) * 4);
    return { buf, w, h };
}

function trim(c) {
    let x0 = c.w, y0 = c.h, x1 = -1, y1 = -1;
    for (let y = 0; y < c.h; y++) for (let x = 0; x < c.w; x++) if (c.buf[(y * c.w + x) * 4 + 3] > 8) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
    return crop(c, x0, y0, x1 + 1, y1 + 1);
}

/** Keep only the component (alpha > 40, 8-neighbourhood) that contains the seed. */
function keepComponent(c, sx, sy) {
    const N = c.w * c.h, seen = new Uint8Array(N), st = [sy * c.w + sx];
    if (c.buf[st[0] * 4 + 3] <= 40) return c;
    seen[st[0]] = 1;
    while (st.length) {
        const p = st.pop(), x = p % c.w, y = (p / c.w) | 0;
        for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
            const X = x + dx, Y = y + dy; if (X < 0 || Y < 0 || X >= c.w || Y >= c.h) continue;
            const q = Y * c.w + X; if (!seen[q] && c.buf[q * 4 + 3] > 40) { seen[q] = 1; st.push(q); }
        }
    }
    const buf = Buffer.from(c.buf);
    for (let p = 0; p < N; p++) if (!seen[p]) { buf[p * 4 + 3] = 0; }
    // soft rim pixels next to the kept part stay
    for (let p = 0; p < N; p++) if (!seen[p] && c.buf[p * 4 + 3] > 0 && c.buf[p * 4 + 3] <= 40) {
        const x = p % c.w, y = (p / c.w) | 0;
        for (const q of [p - 1, p + 1, p - c.w, p + c.w]) if (q >= 0 && q < N && seen[q]) { buf[p * 4 + 3] = c.buf[p * 4 + 3]; break; }
    }
    return { buf, w: c.w, h: c.h };
}

/** Platform head without its column: cut below the head with a broken, jagged edge. */
function capOf(c) {
    const s = surface(c);
    const capW = s.cov[Math.min(c.h - 1, s.row + 6)];
    let r = s.row + 10;
    while (r < c.h - 8 && !(s.cov[r] < capW * 0.55 && s.cov[r + 4] < capW * 0.55 && s.cov[r + 8] < capW * 0.55)) r++;
    const cutRow = r + 4;
    const [s0, s1] = span(c, Math.min(c.h - 1, r + 12));
    const buf = Buffer.from(c.buf);
    for (let x = 0; x < c.w; x++) {
        const inStem = x >= s0 - 4 && x <= s1 + 4;
        const yc = inStem ? cutRow + Math.round(5 * Math.sin(x * 0.45) + 3 * Math.sin(x * 1.3 + 1)) : cutRow + 70;
        for (let y = Math.max(0, yc); y < c.h; y++) buf[(y * c.w + x) * 4 + 3] = 0;
        if (inStem) for (let y = Math.max(0, yc - 5); y < yc && y < c.h; y++) {
            const o = (y * c.w + x) * 4, k = 0.62 + 0.07 * (yc - y);
            buf[o] = Math.round(buf[o] * k); buf[o + 1] = Math.round(buf[o + 1] * k); buf[o + 2] = Math.round(buf[o + 2] * k);
        }
    }
    const kept = keepComponent({ buf, w: c.w, h: c.h }, Math.round((s.x0 + s.x1) / 2), s.row + 6);
    return trim(kept);
}

/** Plank/anvil/beam out of a frame piece, plus the x of its rope/chain anchors (pixels of the result). */
function hangOf(c, which) {
    const s = surface(c);
    if (which == 'top') {
        // the top beam: the first band, a few rows more for its rim
        let y = s.row; while (y < c.h && s.cov[y] >= s.max * 0.6) y++;
        const part = trim(crop(c, 0, 0, c.w, y + 3));
        return { img: part, anchors: [part.w * 0.22, part.w * 0.78] };
    }
    const b = secondBand(c, s);
    const m = Math.round(c.w * 0.16), y0 = b.row - 14;
    // everything below the band's top that hangs together with it, inside the posts
    const region = crop(c, m, y0, c.w - m, c.h);
    const kept = keepComponent(region, Math.round((b.x0 + b.x1) / 2) - m, 16);
    let kx0 = kept.w, ky0 = kept.h, kx1 = -1, ky1 = -1;
    for (let y = 0; y < kept.h; y++) for (let x = 0; x < kept.w; x++) if (kept.buf[(y * kept.w + x) * 4 + 3] > 8) { kx0 = Math.min(kx0, x); kx1 = Math.max(kx1, x); ky0 = Math.min(ky0, y); ky1 = Math.max(ky1, y); }
    const img = crop(kept, kx0, ky0, kx1 + 1, ky1 + 1);
    // anchors: rope/chain columns just above the band
    const ay = b.row - 10, groups = [];
    for (let x = m; x < c.w - m; x++) if (c.buf[(ay * c.w + x) * 4 + 3] > 128) {
        if (groups.length && x - groups[groups.length - 1][1] <= 2) groups[groups.length - 1][1] = x; else groups.push([x, x]);
    }
    const anchors = groups.filter(g => g[1] - g[0] < 30).map(g => (g[0] + g[1]) / 2 - m - kx0);
    return { img, anchors: anchors.length ? anchors : [img.w * 0.25, img.w * 0.75] };
}

/** Vertical rope/chain strip between two rows at column x, cut to a whole number of periods (tileable). */
function ropeOf(c, x, y0, y1, halfW) {
    const strip = crop(c, Math.round(x - halfW), y0, Math.round(x + halfW) + 1, y1);
    // period: best autocorrelation of the alpha-weighted luminance column profile
    const prof = [];
    for (let y = 0; y < strip.h; y++) { let s = 0; for (let xx = 0; xx < strip.w; xx++) { const o = (y * strip.w + xx) * 4; s += lum(strip.buf[o], strip.buf[o + 1], strip.buf[o + 2]) * strip.buf[o + 3] / 255; } prof.push(s); }
    let best = 0, bp = 0;
    for (let p = 8; p < strip.h / 2; p++) {
        let s = 0, n = 0; for (let y = 0; y + p < strip.h; y++) { s += prof[y] * prof[y + p]; n++; }
        const mean = prof.reduce((a, b) => a + b, 0) / prof.length;
        let v = 0; for (let y = 0; y + p < strip.h; y++) v += (prof[y] - mean) * (prof[y + p] - mean);
        v /= n; if (v > best) { best = v; bp = p; }
    }
    const period = bp || 24;
    const reps = Math.max(1, Math.floor((strip.h - 2) / period));
    return crop(strip, 0, 1, strip.w, 1 + period * reps);
}

// ------------------------------------------------------------------ alles

async function stage(def, common) {
    console.log(`${def.id}:`);
    const img = await load(path.join(SRC, def.file));
    const g = grid(img);
    const dir = path.join(OUT, def.id);
    fs.mkdirSync(dir, { recursive: true });
    plainMeta(dir, true);
    const man = { id: def.id, sprites: [] };

    console.log('  Kulisse …');
    await plate(img, def, man, dir);
    console.log('  Boden …');
    await ground(img, g, def, man, dir);

    const cuts = {};
    for (const [k, [name, role, o]] of Object.entries(def.cells)) {
        const c = cut(img, g.cells[k - 1], { ...def.cut, ...(o.cut || {}) });
        cuts[k] = c;
        const hangProp = role == 'prop' && ((o.tags || []).includes('hang') || (o.tags || []).includes('ceil'));
        await piece(dir, man, name, hangProp ? 'hangprop' : role, c, o);
        if (hangProp) man.sprites[man.sprites.length - 1].role = 'prop';
    }
    for (const d of def.derived || []) {
        const src = cuts[d.from];
        if (d.kind == 'cap') await piece(dir, man, d.name, 'float', capOf(src), {});
        else if (d.kind == 'hang') {
            const h = hangOf(src, def.cells[d.from][2].levels ? 'band' : 'top');
            await piece(dir, man, d.name, 'float', h.img, { anchors: h.anchors, rope: d.rope, walkFrac: 0.7 });
        }
    }
    const json = path.join(dir, 'stage.json');
    fs.writeFileSync(json, JSON.stringify(man, null, 1));
    plainMeta(json, false);
    return { img, g, cuts };
}

(async () => {
    fs.mkdirSync(OUT, { recursive: true });
    plainMeta(OUT, true);
    const todo = DEFS.filter(d => !only.length || only.includes(d.id));
    for (const d of todo) await stage(d);

    // Seil (Bernsteinhain, Schaukel) und Kette (Glutschmiede, Ambossgestell): kachelbar, für alle Stages
    if (!only.length || only.includes('common')) {
        const dir = path.join(OUT, 'common');
        fs.mkdirSync(dir, { recursive: true });
        plainMeta(dir, true);
        const man = { id: 'common', sprites: [] };
        for (const [id, cell, name] of [['bernstein', 2, 'rope'], ['glut', 11, 'chain']]) {
            const def = DEFS.find(d => d.id == id);
            const img = await load(path.join(SRC, def.file));
            const g = grid(img);
            const c = cut(img, g.cells[cell - 1], { ...def.cut, ...(def.cells[cell][2].cut || {}) });
            let y0, y1, row;
            if (name == 'rope') {
                // between the top beam and the plank, at the rope columns
                const s = surface(c), b = secondBand(c, s);
                let y = s.row; while (y < c.h && s.cov[y] >= s.max * 0.6) y++;
                y0 = y + 16; y1 = b.row - 8; row = Math.round((y0 + y1) / 2);
            } else {
                // the chain above the bucket
                const cov = coverage(c), max = Math.max(...cov);
                let top = cov.findIndex(v => v > max * 0.45);
                y0 = 2; y1 = top - 6; row = Math.round((y0 + y1) / 2);
            }
            const groups = [];
            for (let x = 0; x < c.w; x++) if (c.buf[(row * c.w + x) * 4 + 3] > 128) {
                if (groups.length && x - groups[groups.length - 1][1] <= 3) groups[groups.length - 1][1] = x; else groups.push([x, x]);
            }
            const cand = groups.filter(g => g[1] - g[0] < 40 && g[1] - g[0] > 2);
            // the frame's posts are the outer groups: the rope hangs just inside them
            const gr = name == 'rope' && cand.length > 2 ? cand[1] : cand[0];
            const r = ropeOf(c, (gr[0] + gr[1]) / 2, y0, y1, (gr[1] - gr[0]) / 2 + 3);
            const f = await finish(r, UP, true, { pad: 0 });
            await save(path.join(dir, name + '.png'), f);
            man.sprites.push({ name, role: 'rope', tags: [], w: f.w, h: f.h, ppu: PPU * UP, px: f.w / 2, py: 0 });
            console.log('  ' + name, gr, y0, y1, r.w, r.h);
        }
        fs.writeFileSync(path.join(dir, 'stage.json'), JSON.stringify(man, null, 1));
        plainMeta(path.join(dir, 'stage.json'), false);
    }
    console.log('fertig');
})().catch(e => { console.error(e); process.exit(1); });
