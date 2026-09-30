// Spielerauswahl aus den sechs Bildentwürfen (Inspiration/CharacterMenuPreviews/<Name>.png, 1672 × 941):
// zerlegt sie in Ebenen, damit das Menü die Szene selbst zusammensetzen und bewegen kann.
//
//  - scene.png       die Kulisse ohne Figur und ohne Oberfläche (Währung, Tafel rechts, Figurenleiste aufgefüllt)
//  - back_<id>.png   Ausschnitt der Kulisse mit dem großen Steinbild des Spielers am Himmel, Figur herausgefüllt
//  - figure_<id>.png die freigestellte Figur (wird im Menü als Ganzes verformt: Atmen, Gewicht, Haare)
//  - Einzelteile     Abzeichen, Name, Fähigkeits-Medaillon, Werteplatten, Knöpfe, Kacheln der Figurenleiste
//  - layout.json     wo alles im Entwurf liegt (Bildpunkte, Ursprung oben links) und die Gelenke der Figuren
//
// Die sechs Entwürfe zeigen dieselbe Kulisse mit je einer anderen Figur: wo mindestens vier übereinstimmen, ist
// der Hintergrund bekannt; was davon abweicht, ist Figur oder Steinbild. Die Figur trennt sich vom blassblauen
// Steinbild über ihre Farben (dunkel, warm, kräftig), unklare Stellen (Weiß) werden entlang der Farbkanten dem
// nächsten sicheren Nachbarn zugeschlagen. Zahlen, die das Spiel selbst schreibt (Stufe, Werte, Preis), werden
// aus den Platten herausgefüllt. Figuren und Einzelteile werden mit Real-ESRGAN auf doppelte Auflösung gebracht.
//
// Aufruf: node tools/newdesign/playerselect.js   (braucht die Grafikkarte für Real-ESRGAN, sonst Lanczos)
//         SF_DEBUG=<ordner> schreibt Prüfbilder, --fast überspringt das Auffüllen (nur zum Ausprobieren der Masken)
'use strict';
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');
const { inpaint } = require('./inpaint');
const { upscaleRGBA } = require('./aiup');
const { save, writeMeta, plainMeta } = require('./texio');

const ROOT = path.join(__dirname, '..', '..');
const SRC_DIR = path.join(ROOT, 'Inspiration/CharacterMenuPreviews');
const OUT = path.join(ROOT, 'SoccerFight/Assets/Resources/Menu/Players');
const DEBUG = process.env.SF_DEBUG;
const FAST = process.argv.includes('--fast');

// Reihenfolge der Figurenleiste im Entwurf
const IDS = ['rio', 'mira', 'bruno', 'dre', 'nova', 'titan'];
const W = 1672, H = 941, N = W * H;
const F = 2;   // Auflösung der Figuren und Einzelteile gegenüber dem Entwurf

// Gebiet, in dem Figur und Steinbild stehen
const ZONE = [280, 95, 1000, 778];
// Ausschnitt mit Steinbild und Figur, der pro Spieler über der gemeinsamen Kulisse liegt (Ränder blendet das Menü weich)
const BACK = [300, 0, 994, 764];
// Ecke des Ausschnitts, in die die Tafel hineinragt (Werteplatte, Auswahlknopf): ab hier [x, y] gilt die Kulisse
const UI_CORNER = [930, 515];

// Gelenke der Figuren im Entwurf (Bildpunkte, nach Augenmaß): Boden (Sohlen), Hüfte (Hosenbund), Brustmitte,
// Halsansatz, Kopfmitte; swing: Haarteile, die nachschwingen (Wurzel, Spitze, Halbmesser um die Strecke, Nachgiebigkeit)
const RIG = {
    rio: { ground: 741, hip: 442, chest: [552, 362], neck: [552, 280], head: [557, 222] },
    mira: { ground: 741, hip: 447, chest: [544, 382], neck: [549, 305], head: [569, 237], swing: [{ root: [514, 205], tip: [455, 315], radius: 30, flex: 1 }] },
    bruno: { ground: 741, hip: 442, chest: [551, 362], neck: [556, 280], head: [566, 222] },
    dre: { ground: 747, hip: 442, chest: [562, 362], neck: [557, 272], head: [557, 212] },
    nova: { ground: 752, hip: 477, chest: [554, 402], neck: [554, 312], head: [589, 257], swing: [{ root: [509, 275], tip: [389, 360], radius: 34, flex: 0.8 }] },
    titan: { ground: 749, hip: 407, chest: [541, 327], neck: [556, 242], head: [566, 187] },
};

// ------------------------------------------------------------------ Helfer

const lumOf = (r, g, b) => 0.3 * r + 0.59 * g + 0.11 * b;
const clamp01 = v => v < 0 ? 0 : v > 1 ? 1 : v;
const smooth = (a, b, v) => { const t = clamp01((v - a) / (b - a)); return t * t * (3 - 2 * t); };

async function loadRGB(file) {
    const { data, info } = await sharp(file).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    if (info.width != W || info.height != H) throw new Error(`${file}: ${info.width}×${info.height} statt ${W}×${H}`);
    return data;
}

async function debug(name, buf, w, h, ch) {
    if (!DEBUG) return;
    fs.mkdirSync(DEBUG, { recursive: true });
    await sharp(Buffer.from(buf.buffer, buf.byteOffset, buf.byteLength), { raw: { width: w, height: h, channels: ch } }).png().toFile(path.join(DEBUG, name + '.png'));
}

/** Box-Weichzeichner für RGB (Float32, ganze Bildgröße). */
function blurRGB(src, r) {
    const tmp = new Float32Array(N * 3), out = new Float32Array(N * 3);
    for (let y = 0; y < H; y++) for (let c = 0; c < 3; c++) {
        let s = 0, n = 0;
        for (let x = -r; x < W; x++) {
            const a = x + r; if (a < W) { s += src[(y * W + a) * 3 + c]; n++; }
            const b = x - r - 1; if (b >= 0) { s -= src[(y * W + b) * 3 + c]; n--; }
            if (x >= 0) tmp[(y * W + x) * 3 + c] = s / n;
        }
    }
    for (let x = 0; x < W; x++) for (let c = 0; c < 3; c++) {
        let s = 0, n = 0;
        for (let y = -r; y < H; y++) {
            const a = y + r; if (a < H) { s += tmp[(a * W + x) * 3 + c]; n++; }
            const b = y - r - 1; if (b >= 0) { s -= tmp[(b * W + x) * 3 + c]; n--; }
            if (y >= 0) out[(y * W + x) * 3 + c] = s / n;
        }
    }
    return out;
}

/** Abstand jedes Pixels zur Maske (Chamfer 3-4, in Pixeln). */
function distanceTo(mask, w, h) {
    const d = new Float32Array(w * h).fill(1e9);
    for (let i = 0; i < w * h; i++) if (mask[i]) d[i] = 0;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const i = y * w + x; let v = d[i];
        if (x > 0) v = Math.min(v, d[i - 1] + 1);
        if (y > 0) { v = Math.min(v, d[i - w] + 1); if (x > 0) v = Math.min(v, d[i - w - 1] + 1.4); if (x < w - 1) v = Math.min(v, d[i - w + 1] + 1.4); }
        d[i] = v;
    }
    for (let y = h - 1; y >= 0; y--) for (let x = w - 1; x >= 0; x--) {
        const i = y * w + x; let v = d[i];
        if (x < w - 1) v = Math.min(v, d[i + 1] + 1);
        if (y < h - 1) { v = Math.min(v, d[i + w] + 1); if (x > 0) v = Math.min(v, d[i + w - 1] + 1.4); if (x < w - 1) v = Math.min(v, d[i + w + 1] + 1.4); }
        d[i] = v;
    }
    return d;
}

function dilate(mask, w, h, r) {
    const d = distanceTo(mask, w, h), out = new Uint8Array(w * h);
    for (let i = 0; i < w * h; i++) out[i] = d[i] <= r ? 1 : 0;
    return out;
}

function erode(mask, w, h, r) {
    const inv = new Uint8Array(w * h);
    for (let i = 0; i < w * h; i++) inv[i] = mask[i] ? 0 : 1;
    const d = distanceTo(inv, w, h), out = new Uint8Array(w * h);
    for (let i = 0; i < w * h; i++) out[i] = d[i] > r ? 1 : 0;
    return out;
}

/** Zusammenhängende Gebiete einer Maske: { lab (Int32, -1 = außerhalb), sizes }. */
function components(mask, w, h) {
    const lab = new Int32Array(w * h).fill(-1), sizes = [];
    const stack = [];
    for (let s = 0; s < w * h; s++) {
        if (!mask[s] || lab[s] >= 0) continue;
        const id = sizes.length; let n = 0;
        lab[s] = id; stack.push(s);
        while (stack.length) {
            const p = stack.pop(), x = p % w; n++;
            if (x > 0 && mask[p - 1] && lab[p - 1] < 0) { lab[p - 1] = id; stack.push(p - 1); }
            if (x < w - 1 && mask[p + 1] && lab[p + 1] < 0) { lab[p + 1] = id; stack.push(p + 1); }
            if (p >= w && mask[p - w] && lab[p - w] < 0) { lab[p - w] = id; stack.push(p - w); }
            if (p < w * (h - 1) && mask[p + w] && lab[p + w] < 0) { lab[p + w] = id; stack.push(p + w); }
        }
        sizes.push(n);
    }
    return { lab, sizes };
}

function largest(mask, w, h) {
    const { lab, sizes } = components(mask, w, h);
    let best = 0;
    for (let i = 1; i < sizes.length; i++) if (sizes[i] > sizes[best]) best = i;
    const out = new Uint8Array(w * h);
    if (sizes.length) for (let i = 0; i < w * h; i++) out[i] = lab[i] == best ? 1 : 0;
    return out;
}

/** Füllt jede Zeile (und mit cols jede Spalte) zwischen erstem und letztem Maskenpixel: die Hülle gewölbter Platten. */
function hull(mask, w, h, cols) {
    const rows = new Uint8Array(w * h);
    for (let y = 0; y < h; y++) {
        let a = -1, b = -1;
        for (let x = 0; x < w; x++) if (mask[y * w + x]) { if (a < 0) a = x; b = x; }
        for (let x = a; a >= 0 && x <= b; x++) rows[y * w + x] = 1;
    }
    if (!cols) return rows;
    const out = new Uint8Array(w * h);
    for (let x = 0; x < w; x++) {
        let a = -1, b = -1;
        for (let y = 0; y < h; y++) if (mask[y * w + x]) { if (a < 0) a = y; b = y; }
        for (let y = a; a >= 0 && y <= b; y++) if (rows[y * w + x]) out[y * w + x] = 1;
    }
    return out;
}

/** Harte Maske → weiche Deckkraft (0..1) mit etwa einem Pixel breiter, leicht nach innen gezogener Kante. */
function soften(mask, w, h) {
    let a = Float32Array.from(mask);
    for (let pass = 0; pass < 2; pass++) {
        const b = new Float32Array(w * h);
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
            let s = 0, n = 0;
            for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
                const X = x + dx, Y = y + dy; if (X < 0 || Y < 0 || X >= w || Y >= h) { n++; continue; }
                s += a[Y * w + X]; n++;
            }
            b[y * w + x] = s / n;
        }
        a = b;
    }
    for (let i = 0; i < w * h; i++) a[i] = clamp01((a[i] - 0.3) / 0.5);
    return a;
}

class Heap {
    constructor(n) { this.k = new Float32Array(n); this.v = new Int32Array(n); this.n = 0; this.key = 0; }
    push(key, val) {
        if (this.n >= this.k.length) { const k = new Float32Array(this.n * 2), v = new Int32Array(this.n * 2); k.set(this.k); v.set(this.v); this.k = k; this.v = v; }
        let i = this.n++;
        while (i > 0) { const p = (i - 1) >> 1; if (this.k[p] <= key) break; this.k[i] = this.k[p]; this.v[i] = this.v[p]; i = p; }
        this.k[i] = key; this.v[i] = val;
    }
    pop() {
        const top = this.v[0]; this.key = this.k[0]; this.n--;
        if (this.n > 0) {
            const k = this.k[this.n], v = this.v[this.n]; let i = 0;
            for (; ;) { let c = 2 * i + 1; if (c >= this.n) break; if (c + 1 < this.n && this.k[c + 1] < this.k[c]) c++; if (this.k[c] >= k) break; this.k[i] = this.k[c]; this.v[i] = this.v[c]; i = c; }
            this.k[i] = k; this.v[i] = v;
        }
        return top;
    }
}

/** Ausschnitt [x0, y0, x1, y1) eines RGB-Bildes als { buf (RGB), w, h, x0, y0 }. */
function crop(img, [x0, y0, x1, y1]) {
    const w = x1 - x0, h = y1 - y0, buf = Buffer.alloc(w * h * 3);
    for (let y = 0; y < h; y++) img.copy(buf, y * w * 3, ((y + y0) * W + x0) * 3, ((y + y0) * W + x1) * 3);
    return { buf, w, h, x0, y0 };
}

/** RGB-Ausschnitt + Deckkraft → RGBA (gerades Alpha). */
function withAlpha(c, alpha) {
    const out = Buffer.alloc(c.w * c.h * 4);
    for (let p = 0; p < c.w * c.h; p++) {
        out[p * 4] = c.buf[p * 3]; out[p * 4 + 1] = c.buf[p * 3 + 1]; out[p * 4 + 2] = c.buf[p * 3 + 2];
        out[p * 4 + 3] = Math.round(clamp01(alpha[p]) * 255);
    }
    return { buf: out, w: c.w, h: c.h, x0: c.x0, y0: c.y0 };
}

/** Knapp um das Sichtbare zuschneiden (RGBA-Teil mit x0/y0). */
function trim(part, pad = 3) {
    const { w, h } = part;
    let a = w, b = -1, c = h, d = -1;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (part.buf[(y * w + x) * 4 + 3] > 8) { a = Math.min(a, x); b = Math.max(b, x); c = Math.min(c, y); d = Math.max(d, y); }
    if (b < 0) throw new Error('leeres Teil');
    a = Math.max(0, a - pad); c = Math.max(0, c - pad); b = Math.min(w - 1, b + pad); d = Math.min(h - 1, d + pad);
    const nw = b - a + 1, nh = d - c + 1, buf = Buffer.alloc(nw * nh * 4);
    for (let y = 0; y < nh; y++) part.buf.copy(buf, y * nw * 4, ((y + c) * w + a) * 4, ((y + c) * w + b + 1) * 4);
    return { buf, w: nw, h: nh, x0: part.x0 + a, y0: part.y0 + c };
}

/** Zeilenweise zwischen den Rändern des Kastens überblenden (Zahlenfeld auf einer gleichmäßigen Platte leeren). */
function fillRows(img, [x0, y0, x1, y1]) {
    for (let y = y0; y < y1; y++) {
        const l = [0, 0, 0], r = [0, 0, 0];
        for (let k = 1; k <= 3; k++) for (let c = 0; c < 3; c++) { l[c] += img[(y * W + x0 - k) * 3 + c] / 3; r[c] += img[(y * W + x1 - 1 + k) * 3 + c] / 3; }
        for (let x = x0; x < x1; x++) {
            const t = (x - x0 + 0.5) / (x1 - x0);
            for (let c = 0; c < 3; c++) img[(y * W + x) * 3 + c] = Math.round(l[c] * (1 - t) + r[c] * t);
        }
    }
}

/** Schrift in einem Kasten entfernen: jedes Schriftpixel (isText, etwas geweitet) bekommt das Mittel der nächsten freien Pixel in vier Richtungen. */
function fillText(img, [x0, y0, x1, y1], isText, grow = 2, inside = null) {
    const w = x1 - x0, h = y1 - y0;
    let m = new Uint8Array(w * h);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { const i = ((y + y0) * W + x + x0) * 3; if (isText(img[i], img[i + 1], img[i + 2])) m[y * w + x] = 1; }
    m = dilate(m, w, h, grow);
    // inside(x, y): nur innerhalb dieser Fläche füllen und nur von dort Farbe holen
    if (inside) for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (!inside(x + x0, y + y0)) m[y * w + x] = 0;
    const src = Buffer.from(img);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        if (!m[y * w + x]) continue;
        const acc = [0, 0, 0]; let wt = 0;
        for (const [dx, dy] of [[-1, 0], [1, 0], [0, -1], [0, 1]]) {
            let X = x, Y = y, n = 0;
            while (X >= 0 && Y >= 0 && X < w && Y < h && m[Y * w + X]) { X += dx; Y += dy; n++; }
            const gx = Math.max(0, Math.min(W - 1, X + x0)), gy = Math.max(0, Math.min(H - 1, Y + y0)), k = 1 / n;
            for (let c = 0; c < 3; c++) acc[c] += src[(gy * W + gx) * 3 + c] * k;
            wt += k;
        }
        for (let c = 0; c < 3; c++) img[((y + y0) * W + x + x0) * 3 + c] = Math.round(acc[c] / wt);
    }
}

// ------------------------------------------------------------------ Hintergrund, Figuren

// Rio, Mira und Bruno stehen in derselben Pose: stimmen nur sie (und eine weitere Figur) überein, kann das auch Haut
// auf Haut sein. Sicher Hintergrund ist erst, was alle vier Posen zeigen.
const POSE = { rio: 0, mira: 0, bruno: 0, dre: 1, nova: 2, titan: 3 };

/** Wo stimmen mindestens vier der sechs Entwürfe überein? Dort ist der Hintergrund bekannt. */
function consensus(soft) {
    // known: in wie vielen verschiedenen Posen der Hintergrund so zu sehen ist (0 = unbekannt)
    const bg = new Float32Array(N * 3), known = new Uint8Array(N), T = 20;
    const dist = (a, b, p) => Math.max(Math.abs(a[p * 3] - b[p * 3]), Math.abs(a[p * 3 + 1] - b[p * 3 + 1]), Math.abs(a[p * 3 + 2] - b[p * 3 + 2]));
    for (let p = 0; p < N; p++) {
        let best = -1, bestN = 0;
        for (let i = 0; i < soft.length; i++) {
            let n = 0;
            for (let j = 0; j < soft.length; j++) if (dist(soft[i], soft[j], p) < T) n++;
            if (n > bestN) { bestN = n; best = i; }
        }
        if (bestN < 4) continue;
        const ref = dist(soft[0], soft[best], p) < T ? 0 : best;
        // so dunkel ist die Kulisse nie: hier stimmen nur die schwarzen Schuhe mehrerer Figuren überein
        const r = soft[ref][p * 3], g = soft[ref][p * 3 + 1], b = soft[ref][p * 3 + 2], l = lumOf(r, g, b), mx = Math.max(r, g, b);
        if (l < 48 || (l < 100 && (mx - Math.min(r, g, b)) / mx < 0.15)) continue;   // … oder ihre grauen Glanzlichter
        let poses = 0;
        for (let j = 0; j < soft.length; j++) if (dist(soft[best], soft[j], p) < T) poses |= 1 << POSE[IDS[j]];
        known[p] = (poses & 1) + (poses >> 1 & 1) + (poses >> 2 & 1) + (poses >> 3 & 1);
        for (let c = 0; c < 3; c++) bg[p * 3 + c] = soft[ref][p * 3 + c];
    }
    return { bg, known };
}

/** Maske der Figur eines Entwurfs (1 = Figur), ganze Bildgröße. */
function figureMask(I, S, bg, known) {
    const [zx0, zy0, zx1, zy1] = ZONE;
    // 0 offen, 1 Figur, 2 Hintergrund; match: stimmt sicher mit dem bekannten Hintergrund überein
    const lab = new Uint8Array(N).fill(2), match = new Uint8Array(N);
    for (let y = zy0; y < zy1; y++) for (let x = zx0; x < zx1; x++) {
        const p = y * W + x, r = I[p * 3], g = I[p * 3 + 1], b = I[p * 3 + 2];
        const mx = Math.max(r, g, b), mn = Math.min(r, g, b), lum = lumOf(r, g, b), sat = mx ? (mx - mn) / mx : 0;
        const dark = lum < 45;
        // Farben, die nur an den Figuren vorkommen: dunkel, warm (Haut, Rot, Gold), Violett, tiefes Blau, Petrol, Marine
        // und neutrales dunkles Grau (Glanz auf Schuhen und Knieschonern)
        const figure = dark || r > b + 20 || (r > g + 22 && b > g + 22) || (b > 110 && r < 80 && g < 100 && b > g + 50)
            || (g > r + 50 && Math.abs(g - b) < 35 && lum < 165) || (lum < 62 && sat > 0.35) || (lum < 105 && sat < 0.14);
        // blasses Blau: Himmel, Wolkenschatten und das Steinbild des Spielers
        const pale = !figure && b > r + 45 && g > r + 20 && lum > 100;
        // Weiß: Stoff ist warm getönt, Wolken und die Glanzlichter des Steinbilds sind bläulich
        const light = lum > 150 && sat < 0.3, warm = light && r - b >= 6, cool = light && b - r >= 10;
        const unsure = warm ? 1 : 0;   // unklare Stellen: warmes Weiß gehört zur Figur, der Rest bleibt offen
        if (known[p]) {
            const d = Math.max(Math.abs(S[p * 3] - bg[p * 3]), Math.abs(S[p * 3 + 1] - bg[p * 3 + 1]), Math.abs(S[p * 3 + 2] - bg[p * 3 + 2]));
            if (d < 18 && !dark) {
                // Figurenfarbe, die nicht in allen vier Posen zu sehen ist: Haut mehrerer Figuren an derselben Stelle
                // Weiß, das nur zwei Posen teilen, kann Stoff auf Stoff sein (Kragen, Hose)
                if (figure && known[p] < 4 && y < 715) lab[p] = 1;
                else if (!figure && !pale && known[p] < 3) lab[p] = unsure;
                else { lab[p] = 2; match[p] = 1; }
            }
            else if (pale) { lab[p] = 2; match[p] = 1; }     // das eigene Steinbild vor dem bekannten Himmel
            else if (figure || (d >= 40 && !cool)) lab[p] = 1;
            else lab[p] = unsure;
        } else {
            if (figure) lab[p] = 1;
            else if (pale) { lab[p] = 2; match[p] = 1; }
            else lab[p] = unsure;                             // Weiß und Grau: Stoff oder Wolke
        }
    }
    // offene Pixel: Marke des Saatpunkts mit dem kleinsten Weg über Farbkanten (Figur leicht bevorzugt)
    const open = Uint8Array.from(lab, v => v == 0 ? 1 : 0);
    // Die Figur greift höchstens REACH Pixel weit in offenes Gebiet (weiße Hosen sind von beiden Seiten erreichbar,
    // ein langer heller Streifen des Steinbilds neben dem Arm nicht).
    const REACH = 56;
    const dd = new Float32Array(N).fill(Infinity), steps = new Uint16Array(N), heap = new Heap(1 << 20);
    for (let y = zy0; y < zy1; y++) for (let x = zx0; x < zx1; x++) {
        const p = y * W + x; if (open[p]) continue;
        if (open[p - 1] || open[p + 1] || open[p - W] || open[p + W]) { dd[p] = 0; heap.push(0, p); }
    }
    while (heap.n) {
        const p = heap.pop(), d = heap.key;
        if (d > dd[p]) continue;
        if (lab[p] == 1 && steps[p] >= REACH) continue;
        for (const q of [p - 1, p + 1, p - W, p + W]) {
            if (!open[q]) continue;
            const w = 0.5 + Math.max(Math.abs(I[p * 3] - I[q * 3]), Math.abs(I[p * 3 + 1] - I[q * 3 + 1]), Math.abs(I[p * 3 + 2] - I[q * 3 + 2]));
            const nd = d + (lab[p] == 1 ? w * 0.8 : w);
            if (nd < dd[q]) { dd[q] = nd; lab[q] = lab[p]; steps[q] = steps[p] + 1; heap.push(nd, q); }
        }
    }
    // was niemand erreicht hat, ist Hintergrund
    for (let p = 0; p < N; p++) if (open[p] && dd[p] == Infinity) lab[p] = 2;
    let fg = Uint8Array.from(lab, v => v == 1 ? 1 : 0);
    if (figureMask.dump) figureMask.dump(open, fg, match);
    // dünne Brücken zu Splittern (Steinbild, Gras, Nebel) kappen: nur was am Kern der Figur hängt, bleibt
    const core = largest(erode(fg, W, H, 2.2), W, H), near = dilate(core, W, H, 4);
    for (let p = 0; p < N; p++) fg[p] = fg[p] && near[p] ? 1 : 0;
    fg = largest(fg, W, H);
    // eingeschlossene Löcher schließen, außer es ist wirklich Hintergrund zu sehen (zwischen Arm und Körper)
    const bgMask = Uint8Array.from(fg, v => v ? 0 : 1), holes = components(bgMask, W, H);
    const okay = new Array(holes.sizes.length).fill(0), outside = new Uint8Array(holes.sizes.length);
    for (let p = 0; p < N; p++) {
        const id = holes.lab[p]; if (id < 0) continue;
        if (match[p]) okay[id]++;
        const x = p % W, y = (p / W) | 0;
        if (x == 0 || y == 0 || x == W - 1 || y == H - 1) outside[id] = 1;
    }
    for (let p = 0; p < N; p++) {
        const id = holes.lab[p];
        if (id >= 0 && !outside[id] && (holes.sizes[id] < 150 || okay[id] < holes.sizes[id] * 0.5)) fg[p] = 1;
    }
    // Rand glätten (Einzelpixel-Zacken)
    const sm = new Uint8Array(N);
    for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) {
        let s = 0;
        for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) s += fg[(y + dy) * W + x + dx];
        sm[y * W + x] = s >= 5 ? 1 : 0;
    }
    return sm;
}

/** Inhaltsbasiert auffüllen in einem Ausschnitt [x0, y0, x1, y1): hole (ganze Bildgröße, 1 = füllen), Ergebnis zurück ins Bild. */
function fillHole(img, box, hole, opts = {}) {
    const [x0, y0, x1, y1] = box, w = x1 - x0, h = y1 - y0;
    const f = new Float32Array(w * h * 3), m = new Uint8Array(w * h), src = opts.src ? new Uint8Array(w * h) : null;
    let any = 0;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const p = (y + y0) * W + x + x0, q = y * w + x;
        for (let c = 0; c < 3; c++) f[q * 3 + c] = img[p * 3 + c];
        m[q] = hole[p]; any += hole[p];
        if (src) src[q] = opts.src[p] && !hole[p] ? 1 : 0;
    }
    if (!any) return;
    if (FAST) {
        // nur zum Ausprobieren: waagrecht vom Rand her auffüllen
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (m[y * w + x]) { let X = x; while (X > 0 && m[y * w + X]) X--; for (let c = 0; c < 3; c++) f[(y * w + x) * 3 + c] = f[(y * w + X) * 3 + c]; }
    }
    const res = FAST ? f : inpaint(f, w, h, m, src, { yWeight: opts.yWeight ?? 3, xWeight: opts.xWeight ?? 0, seed: opts.seed || 7 });
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const q = y * w + x; if (!m[q]) continue;
        const p = (y + y0) * W + x + x0;
        for (let c = 0; c < 3; c++) img[p * 3 + c] = Math.max(0, Math.min(255, Math.round(res[q * 3 + c])));
    }
}

/**
 * Glatt auffüllen (Himmel und Wolken hinter der Oberfläche): die Lücke übernimmt weich die Farben ihres Randes.
 * Erst grob über eine Bildpyramide, dann ausgeglichen – ohne erfundene Einzelheiten, die hinter Knöpfen hervorschauen.
 */
function fillSmooth(img, box, hole) {
    const [x0, y0, x1, y1] = box, w = x1 - x0, h = y1 - y0;
    const f = new Float32Array(w * h * 3), k = new Float32Array(w * h);
    let any = 0;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const p = (y + y0) * W + x + x0, q = y * w + x;
        if (hole[p]) { any++; continue; }
        k[q] = 1;
        for (let c = 0; c < 3; c++) f[q * 3 + c] = img[p * 3 + c];
    }
    if (!any) return;
    const pull = (f, k, w, h) => {
        if (w <= 2 || h <= 2) return;
        const W2 = (w + 1) >> 1, H2 = (h + 1) >> 1, f2 = new Float32Array(W2 * H2 * 3), k2 = new Float32Array(W2 * H2);
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
            const q = y * w + x, q2 = (y >> 1) * W2 + (x >> 1);
            if (!k[q]) continue;
            k2[q2] += 1;
            for (let c = 0; c < 3; c++) f2[q2 * 3 + c] += f[q * 3 + c];
        }
        let missing = false;
        for (let q = 0; q < W2 * H2; q++) { if (k2[q]) { for (let c = 0; c < 3; c++) f2[q * 3 + c] /= k2[q]; k2[q] = 1; } else missing = true; }
        if (missing) pull(f2, k2, W2, H2);
        // Lücken aus der gröberen Stufe (bilinear)
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
            const q = y * w + x; if (k[q]) continue;
            const fx = Math.max(0, Math.min(W2 - 1, (x - 0.5) / 2)), fy = Math.max(0, Math.min(H2 - 1, (y - 0.5) / 2));
            const ax = Math.floor(fx), ay = Math.floor(fy), bx = Math.min(W2 - 1, ax + 1), by = Math.min(H2 - 1, ay + 1), tx = fx - ax, ty = fy - ay;
            for (let c = 0; c < 3; c++)
                f[q * 3 + c] = (f2[(ay * W2 + ax) * 3 + c] * (1 - tx) + f2[(ay * W2 + bx) * 3 + c] * tx) * (1 - ty)
                    + (f2[(by * W2 + ax) * 3 + c] * (1 - tx) + f2[(by * W2 + bx) * 3 + c] * tx) * ty;
        }
    };
    const fixed = Float32Array.from(k);
    pull(f, k, w, h);
    // ausgleichen: jedes Lückenpixel wird zum Mittel seiner Nachbarn
    for (let it = 0; it < 300; it++) for (let y = 1; y < h - 1; y++) for (let x = 1; x < w - 1; x++) {
        const q = y * w + x; if (fixed[q]) continue;
        for (let c = 0; c < 3; c++) {
            const m = (f[(q - 1) * 3 + c] + f[(q + 1) * 3 + c] + f[(q - w) * 3 + c] + f[(q + w) * 3 + c]) / 4;
            f[q * 3 + c] += 1.7 * (m - f[q * 3 + c]);
        }
    }
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const q = y * w + x; if (fixed[q]) continue;
        const p = (y + y0) * W + x + x0;
        for (let c = 0; c < 3; c++) img[p * 3 + c] = Math.max(0, Math.min(255, Math.round(f[q * 3 + c])));
    }
}

// ------------------------------------------------------------------ Einzelteile der Oberfläche

/** Teil über eine Farbprobe freistellen: größtes zusammenhängendes Gebiet der Probe, zur gewölbten Hülle gefüllt. */
function cutByColour(img, box, isPart, cols = true) {
    const c = crop(img, box), m = new Uint8Array(c.w * c.h);
    for (let p = 0; p < c.w * c.h; p++) m[p] = isPart(c.buf[p * 3], c.buf[p * 3 + 1], c.buf[p * 3 + 2]) ? 1 : 0;
    return withAlpha(c, soften(hull(largest(m, c.w, c.h), c.w, c.h, cols), c.w, c.h));
}

/** Achteckige Platte (Kanten bei x0..x1, y0..y1, Ecken um cut gekappt) mit weicher Kante. */
function cutOctagon(img, [x0, y0, x1, y1], cut) {
    const box = [Math.floor(x0) - 3, Math.floor(y0) - 3, Math.ceil(x1) + 3, Math.ceil(y1) + 3];
    const c = crop(img, box), a = new Float32Array(c.w * c.h);
    for (let y = 0; y < c.h; y++) for (let x = 0; x < c.w; x++) {
        const px = x + box[0] + 0.5, py = y + box[1] + 0.5;
        const dx = Math.min(px - x0, x1 - px), dy = Math.min(py - y0, y1 - py);
        // Abstand zur Innenseite: Kanten und die 45°-Schräge
        const d = Math.min(dx, dy, (dx + dy - cut) * Math.SQRT1_2);
        a[y * c.w + x] = clamp01(d + 0.5);
    }
    return withAlpha(c, a);
}

/** Dunkle gemalte Schrift auf hellem Himmel: Deckkraft aus der Dunkelheit gegenüber der hellsten Umgebung. */
function keyDarkText(img, box) {
    const c = crop(img, box), { w, h } = c, L = new Float32Array(w * h);
    for (let p = 0; p < w * h; p++) L[p] = lumOf(c.buf[p * 3], c.buf[p * 3 + 1], c.buf[p * 3 + 2]);
    // hellste Umgebung = Himmel hinter der Schrift; der Umkreis muss weiter reichen als die dicksten Striche
    // (sonst hält die Mitte eines Balkens sich selbst für Hintergrund und bekommt ein Loch)
    const R = 40, rowMax = new Float32Array(w * h), bg = new Float32Array(w * h), bgAt = new Int32Array(w * h), rowAt = new Int32Array(w * h);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        let m = -1, at = 0;
        for (let k = -R; k <= R; k++) { const X = x + k; if (X < 0 || X >= w) continue; if (L[y * w + X] > m) { m = L[y * w + X]; at = y * w + X; } }
        rowMax[y * w + x] = m; rowAt[y * w + x] = at;
    }
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        let m = -1, at = 0;
        for (let k = -R; k <= R; k++) { const Y = y + k; if (Y < 0 || Y >= h) continue; if (rowMax[Y * w + x] > m) { m = rowMax[Y * w + x]; at = rowAt[Y * w + x]; } }
        bg[y * w + x] = m; bgAt[y * w + x] = at;
    }
    const out = Buffer.alloc(w * h * 4);
    for (let p = 0; p < w * h; p++) {
        const a = smooth(0.28, 0.82, (bg[p] - L[p]) / Math.max(40, bg[p] - 28));
        const q = bgAt[p];
        for (let k = 0; k < 3; k++) {
            // Farbe vom Himmel entmischen
            const v = a > 0.2 ? c.buf[q * 3 + k] + (c.buf[p * 3 + k] - c.buf[q * 3 + k]) / a : c.buf[p * 3 + k];
            out[p * 4 + k] = Math.max(0, Math.min(255, Math.round(v)));
        }
        out[p * 4 + 3] = Math.round(a * 255);
    }
    return { buf: out, w, h, x0: c.x0, y0: c.y0 };
}

/** Fähigkeits-Medaillon (Ellipse) mit der Beschriftung darunter als ein Teil. */
function cutAbility(img) {
    const box = [986, 338, 1168, 528], CX = 1076, CY = 419.5, RX = 80, RY = 77.5, LABEL = 499;
    const text = keyDarkText(img, box), c = crop(img, box);
    const out = Buffer.alloc(c.w * c.h * 4);
    for (let y = 0; y < c.h; y++) for (let x = 0; x < c.w; x++) {
        const p = y * c.w + x, px = x + box[0] + 0.5, py = y + box[1] + 0.5;
        const e = Math.hypot((px - CX) / RX, (py - CY) / RY);
        const disc = clamp01((1 - e) * Math.min(RX, RY) + 0.5);
        const label = py >= LABEL ? text.buf[p * 4 + 3] / 255 : 0;
        const a = Math.max(disc, label);
        for (let k = 0; k < 3; k++) out[p * 4 + k] = disc >= label ? c.buf[p * 3 + k] : text.buf[p * 4 + k];
        out[p * 4 + 3] = Math.round(a * 255);
    }
    return { buf: out, w: c.w, h: c.h, x0: box[0], y0: box[1] };
}

const isGold = (r, g, b) => r > b + 40 && r > 100;
const isBlue = (r, g, b) => { const mx = Math.max(r, g, b), mn = Math.min(r, g, b); return (b > r + 80 && (mx - mn) / mx > 0.55) || lumOf(r, g, b) < 70; };

/** Der gelbe Knopf ohne Schrift: die dunklen Buchstaben bekommen zeilenweise die Plattenfarbe. */
function blankSelect(img) {
    const out = Buffer.from(img), x0 = 1046, x1 = 1336, y0 = 660, y1 = 724;
    const w = x1 - x0, h = y1 - y0;
    let text = new Uint8Array(w * h);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { const i = ((y + y0) * W + x + x0) * 3; if (lumOf(img[i], img[i + 1], img[i + 2]) < 105) text[y * w + x] = 1; }
    text = dilate(text, w, h, 3);
    for (let y = 0; y < h; y++) {
        const vals = [[], [], []];
        for (let x = 0; x < w; x++) if (!text[y * w + x]) for (let c = 0; c < 3; c++) vals[c].push(img[((y + y0) * W + x + x0) * 3 + c]);
        if (vals[0].length < 8) continue;
        const med = vals.map(v => v.sort((a, b) => a - b)[v.length >> 1]);
        for (let x = 0; x < w; x++) if (text[y * w + x]) for (let c = 0; c < 3; c++) out[((y + y0) * W + x + x0) * 3 + c] = med[c];
    }
    // Übergänge der gefüllten Stellen leicht verwischen
    const soft = Buffer.from(out);
    for (let y = 1; y < h - 1; y++) for (let x = 1; x < w - 1; x++) {
        if (!text[y * w + x]) continue;
        for (let c = 0; c < 3; c++) {
            let s = 0;
            for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) s += out[((y + y0 + dy) * W + x + x0 + dx) * 3 + c];
            soft[((y + y0) * W + x + x0) * 3 + c] = Math.round(s / 9);
        }
    }
    return soft;
}

/** Grenzen der Kacheln in der Figurenleiste: Spalten, in denen zwischen zwei Kacheln der dunkle Spalt liegt. */
function tileSlots(img) {
    // Mittelzeilen der Leiste: Anteil dunkler, weder blauer noch goldener Pixel je Spalte
    const gap = x => {
        let n = 0;
        for (let y = 800; y < 880; y++) { const i = (y * W + x) * 3, r = img[i], g = img[i + 1], b = img[i + 2]; if (!(b > r + 40 && b > 90) && !(r > b + 60 && r > 150)) n++; }
        return n / 80;
    };
    const edges = [];
    for (let k = 1; k < 6; k++) {
        const guess = Math.round(270 + k * 172.6);
        let best = guess, bestV = -1;
        for (let x = guess - 8; x <= guess + 8; x++) { const v = gap(x - 1) + gap(x) + gap(x + 1); if (v > bestV) { bestV = v; best = x; } }
        edges.push(best);
    }
    return [264, ...edges, 1304];
}

/** Eine Kachel (blau oder golden) aus ihrem Fach freistellen. */
function cutTile(img, x0, x1, gold) {
    const box = [x0 - (gold ? 5 : 1), gold ? 768 : 762, x1 + (gold ? 5 : 1), 904];
    // Gold ist rötlicher als das gelbgrüne Gras über der Leiste
    const isPart = gold
        ? (r, g, b) => r > b + 60 && r > 120 && r > g + 22
        : (r, g, b) => (b > r + 50 && b > 90) || (lumOf(r, g, b) < 42 && b >= r - 4);
    // nur zeilenweise füllen: Haare ragen bis in den oberen Rahmen, die Spaltenhülle bekäme dort eine Kerbe
    return trim(cutByColour(img, box, isPart, false), 2);
}

// ------------------------------------------------------------------ Ablauf

async function savePart(name, part, crunch) {
    const file = path.join(OUT, name + '.png');
    if (crunch) {
        await save(file, part, { channels: 4, crunch: true, mips: 1 });
    } else {
        await sharp(part.buf, { raw: { width: part.w, height: part.h, channels: 4 } }).png({ compressionLevel: 9 }).toFile(file);
        writeMeta(file, 0, 1, 1);
    }
    // normales Alpha (UI-Shader): Farbe in die durchsichtigen Ränder ziehen, trilinear verkleinern
    const meta = file + '.meta';
    fs.writeFileSync(meta, fs.readFileSync(meta, 'utf8').replace('alphaIsTransparency: 0', 'alphaIsTransparency: 1').replace('filterMode: 1', 'filterMode: 2'));
}

(async () => {
    const t0 = Date.now();
    const imgs = [], soft = [];
    for (const id of IDS) {
        const img = await loadRGB(path.join(SRC_DIR, id[0].toUpperCase() + id.slice(1) + '.png'));
        imgs.push(img); soft.push(blurRGB(Float32Array.from(img), 2));
    }
    fs.mkdirSync(OUT, { recursive: true });
    plainMeta(OUT, true);

    // ---- Figuren
    const { bg, known } = consensus(soft);
    const masks = [];
    for (let a = 0; a < IDS.length; a++) {
        if (DEBUG && process.env.SF_LABELS) figureMask.dump = (open, fg, match) => {
            // Saatpunkte: Figur rot, Hintergrund grün, offen (geodätisch zugeteilt) heller
            const out = Buffer.alloc(N * 3);
            for (let p = 0; p < N; p++) { out[p * 3] = fg[p] ? (open[p] ? 255 : 200) : 0; out[p * 3 + 1] = fg[p] ? (open[p] ? 170 : 0) : match[p] ? 160 : open[p] ? 255 : 60; out[p * 3 + 2] = open[p] ? 170 : 0; }
            sharp(out, { raw: { width: W, height: H, channels: 3 } }).png().toFile(path.join(DEBUG, 'marken_' + IDS[a] + '.png'));
        };
        masks.push(figureMask(imgs[a], soft[a], bg, known));
        if (DEBUG) {
            const out = Buffer.alloc(N * 3);
            for (let p = 0; p < N; p++) { const m = masks[a][p]; out[p * 3] = m ? imgs[a][p * 3] : 40; out[p * 3 + 1] = m ? imgs[a][p * 3 + 1] : 200; out[p * 3 + 2] = m ? imgs[a][p * 3 + 2] : 60; }
            await debug('figur_' + IDS[a], out, W, H, 3);
        }
    }
    console.log(`  Figurenmasken (${((Date.now() - t0) / 1000).toFixed(0)} s)`);

    // ---- Einzelteile: gemeinsame aus Rios Entwurf, Zahlenfelder vorher geleert
    const rio = imgs[0];
    const ui = Buffer.from(rio);
    // Stufe im Abzeichen: die helle Ziffer mit ihrer dunklen Kontur aus dem braunen Feld füllen (das Feld läuft unten
    // schräg zu), ebenso die kleine „1“ im Schild darunter
    {
        // das braune Feld bekommt zeilenweise seine eigene mittlere Farbe (ohne die helle Ziffer und ihre Kontur)
        const rows = [];
        for (let y = 221; y <= 291; y++) {
            const v = [[], [], []];
            for (let x = 1060; x <= 1132; x++) { const i = (y * W + x) * 3, l = lumOf(rio[i], rio[i + 1], rio[i + 2]); if (l > 58 && l < 118 && rio[i] > rio[i + 2] + 15) for (let c = 0; c < 3; c++) v[c].push(rio[i + c]); }
            rows.push(v[0].length >= 6 ? v.map(a => a.sort((p, q) => p - q)[a.length >> 1]) : null);
        }
        for (let i = 0; i < rows.length; i++) if (!rows[i]) rows[i] = rows.slice(0, i).reverse().find(r => r) || rows.find(r => r);
        for (let y = 221; y <= 291; y++) {
            const cut = Math.max(0, y - 270), acc = [0, 0, 0]; let n = 0;
            for (let k = -4; k <= 4; k++) { const r = rows[Math.max(0, Math.min(rows.length - 1, y - 221 + k))]; for (let c = 0; c < 3; c++) acc[c] += r[c]; n++; }
            for (let x = 1061 + cut; x <= 1131 - cut; x++) {
                // zum Rand des Feldes hin weich in den gemalten Schatten übergehen
                const e = clamp01(Math.min(x - (1061 + cut), 1131 - cut - x, y - 221, 291 - y) / 3);
                for (let c = 0; c < 3; c++) { const i = (y * W + x) * 3 + c; ui[i] = Math.round(ui[i] * (1 - e) + acc[c] / n * e); }
            }
        }
        // Reste der hellen Ziffer am Feldrand
        const field = (x, y) => { const cut = Math.max(0, y - 270); return y >= 221 && y <= 291 && x >= 1061 + cut && x <= 1131 - cut; };
        fillText(ui, [1056, 218, 1138, 294], (r, g, b) => lumOf(r, g, b) > 135, 2, field);
    }
    fillText(ui, [1080, 292, 1112, 322], (r, g, b) => lumOf(r, g, b) < 95, 2);
    // Werte auf den drei Platten, Preis auf dem Upgrade-Knopf
    const PLATES = [[974, 539, 1158, 628.5], [1163.5, 539, 1353, 628.5], [1358.5, 539, 1546, 628.5]];
    for (const [x0] of PLATES) fillText(ui, [Math.round(x0) + 88, 586, Math.round(x0) + 172, 620], (r, g, b) => lumOf(r, g, b) < 120, 2);
    fillRows(ui, [1538, 688, 1602, 737]);

    const parts = {};
    parts.badge = trim(cutByColour(ui, [1032, 190, 1158, 332], (r, g, b) => r > b + 15, false));
    PLATES.forEach((p, i) => parts[['stat_hp', 'stat_damage', 'stat_speed'][i]] = cutOctagon(ui, p, 16));
    parts.select = trim(cutByColour(ui, [950, 634, 1402, 748], isGold));
    parts.select_blank = trim(cutByColour(blankSelect(ui), [950, 634, 1402, 748], isGold));
    parts.upgrade = trim(cutByColour(ui, [1403, 636, 1648, 751], isBlue));
    {
        // Pfeil der Figurenleiste: das weiße Zeichen mit seiner dunklen Kontur
        const c = crop(rio, [1372, 806, 1416, 868]), m = new Uint8Array(c.w * c.h);
        for (let p = 0; p < c.w * c.h; p++) m[p] = Math.min(c.buf[p * 3], c.buf[p * 3 + 1], c.buf[p * 3 + 2]) > 185 ? 1 : 0;
        parts.arrow = trim(withAlpha(c, soften(dilate(hull(largest(m, c.w, c.h), c.w, c.h, false), c.w, c.h, 2.4), c.w, c.h)));
    }
    // pro Spieler: Name mit Klasse, Fähigkeit, Kachel blau und golden
    for (let a = 0; a < IDS.length; a++) {
        const id = IDS[a], other = imgs[a == 0 ? 1 : 0];
        parts['title_' + id] = trim(keyDarkText(imgs[a], [1164, 204, 1480, 328]), 3);
        parts['ability_' + id] = trim(cutAbility(imgs[a]), 2);
        const gold = tileSlots(imgs[a]), blue = tileSlots(other);
        parts['tile_' + id + '_on'] = cutTile(imgs[a], gold[a], gold[a + 1], true);
        parts['tile_' + id] = cutTile(other, blue[a], blue[a + 1], false);
    }
    {
        // leere Kachel (für Sportarten, die noch kommen): Miras blaue Kachel ohne Bild und ohne Namen – ihr Violett
        // hebt sich klar vom blauen Grund ab, der dann aus seinen freien Stellen nachwächst
        const slots = tileSlots(rio), box = [slots[1] - 1, 762, slots[2] + 1, 904];
        const blank = Buffer.from(rio), hole = new Uint8Array(N), src = new Uint8Array(N);
        const inner = (x, y) => x > slots[1] + 10 && x < slots[2] - 10 && y > 771 && y < 865;
        for (let y = box[1]; y < box[3]; y++) for (let x = box[0]; x < box[2]; x++) {
            const p = y * W + x, r = rio[p * 3], g = rio[p * 3 + 1], b = rio[p * 3 + 2];
            const backdrop = b > r + 50 && g > r + 25;   // blauer Grund und Rahmen der Kachel
            if (inner(x, y) && !backdrop) hole[p] = 1;
            src[p] = inner(x, y) && backdrop ? 1 : 0;
        }
        const grown = dilate(hole, W, H, 3);
        for (let p = 0; p < N; p++) { hole[p] = grown[p] && inner(p % W, (p / W) | 0) ? 1 : 0; if (hole[p]) src[p] = 0; }
        fillHole(blank, box, hole, { src, yWeight: 0.5, seed: 11 });
        fillText(blank, [slots[1] + 30, 862, slots[2] - 30, 892], (r, g, b) => lumOf(r, g, b) > 120 || lumOf(r, g, b) < 28, 2);
        parts.tile_blank = cutTile(blank, slots[1], slots[2], false);
    }
    console.log(`  Einzelteile (${((Date.now() - t0) / 1000).toFixed(0)} s)`);

    // ---- Ausschnitt je Spieler: Steinbild am Himmel, Figur herausgefüllt
    const backs = [];
    for (let a = 0; a < IDS.length; a++) {
        const img = Buffer.from(imgs[a]);
        fillHole(img, BACK, dilate(masks[a], W, H, 5), { yWeight: 3, xWeight: 0.6, seed: 3 + a });
        backs.push(img);
        console.log(`  Ausschnitt ${IDS[a]} (${((Date.now() - t0) / 1000).toFixed(0)} s)`);
    }

    // ---- Kulisse: Rios Entwurf ohne Figur, ohne Währung, ohne Tafel und ohne Figurenleiste
    const scene = Buffer.from(backs[0]);
    {
        const hole = new Uint8Array(N);
        const mark = (part, grow) => {
            const m = new Uint8Array(N);
            for (let y = 0; y < part.h; y++) for (let x = 0; x < part.w; x++) if (part.buf[(y * part.w + x) * 4 + 3] > 12) m[(y + part.y0) * W + x + part.x0] = 1;
            const g = dilate(m, W, H, grow);
            for (let p = 0; p < N; p++) if (g[p]) hole[p] = 1;
        };
        for (const name of ['badge', 'stat_hp', 'stat_damage', 'stat_speed', 'select', 'upgrade', 'title_rio', 'ability_rio']) mark(parts[name], 7);
        // Klassenbonus (Trennstrich, Überschrift, Text): alles Dunkle im Feld
        const text = new Uint8Array(N);
        for (let y = 360; y < 504; y++) for (let x = 1178; x < 1650; x++) { const i = (y * W + x) * 3; if (lumOf(rio[i], rio[i + 1], rio[i + 2]) < 150) text[y * W + x] = 1; }
        const grown = dilate(text, W, H, 5);
        for (let p = 0; p < N; p++) if (grown[p]) hole[p] = 1;
        const rect = ([x0, y0, x1, y1], m) => { for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) m[y * W + x] = 1; };
        rect([1236, 12, 1654, 100], hole);      // gemalte Währung
        // Himmel und Wolken hinter der Tafel: glatt (keine erfundenen Ruinen hinter den Knöpfen)
        fillSmooth(scene, [1130, 0, W, 240], hole);
        fillSmooth(scene, [900, 150, W, 800], hole);
        // Figurenleiste mit Pfeil: Gras und Pflaster wachsen aus der Umgebung nach
        const strip = new Uint8Array(N);
        rect([258, 760, 1430, 906], strip);
        fillHole(scene, [120, 720, 1560, H], strip, { yWeight: 6, seed: 8 });
    }
    await save(path.join(OUT, 'scene.png'), { buf: scene, w: W, h: H }, { channels: 3, crunch: true, mips: 0 });
    for (let a = 0; a < IDS.length; a++) {
        // der Ausschnitt reicht rechts unten bis unter Werteplatte und Auswahlknopf: dort die bereinigte Kulisse
        // (so tief reicht kein Steinbild, der Himmel ist in allen Entwürfen derselbe)
        for (let y = UI_CORNER[1]; y < BACK[3]; y++) for (let x = UI_CORNER[0]; x < BACK[2]; x++) {
            const p = (y * W + x) * 3, e = clamp01(Math.min(x - UI_CORNER[0], y - UI_CORNER[1]) / 10);
            for (let c = 0; c < 3; c++) backs[a][p + c] = Math.round(backs[a][p + c] * (1 - e) + scene[p + c] * e);
        }
        await save(path.join(OUT, 'back_' + IDS[a] + '.png'), crop(backs[a], BACK), { channels: 3, crunch: true, mips: 0 });
    }
    await debug('kulisse', scene, W, H, 3);
    console.log(`  Kulisse (${((Date.now() - t0) / 1000).toFixed(0)} s)`);

    // ---- Figuren: freistellen, vergrößern
    const figures = [];
    for (let a = 0; a < IDS.length; a++) {
        const alpha = soften(masks[a], W, H);
        let x0 = W, x1 = 0, y0 = H, y1 = 0;
        for (let p = 0; p < N; p++) if (alpha[p] > 0.03) { const x = p % W, y = (p / W) | 0; x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y); }
        const box = [x0 - 6, y0 - 6, x1 + 7, y1 + 7], c = crop(imgs[a], box), al = new Float32Array(c.w * c.h);
        for (let y = 0; y < c.h; y++) for (let x = 0; x < c.w; x++) al[y * c.w + x] = alpha[(y + box[1]) * W + x + box[0]];
        figures.push(withAlpha(c, al));
    }
    const names = Object.keys(parts);
    const big = await upscaleRGBA([...figures, ...names.map(n => parts[n])], F);
    const layout = { width: W, height: H, back: { x: BACK[0], y: BACK[1], w: BACK[2] - BACK[0], h: BACK[3] - BACK[1] }, figures: [], parts: [] };
    for (let a = 0; a < IDS.length; a++) {
        const f = figures[a], rig = RIG[IDS[a]];
        await savePart('figure_' + IDS[a], big[a], true);
        layout.figures.push({
            id: IDS[a], x: f.x0, y: f.y0, w: f.w, h: f.h, ground: rig.ground, hip: rig.hip,
            chestX: rig.chest[0], chestY: rig.chest[1], neckX: rig.neck[0], neckY: rig.neck[1], headX: rig.head[0], headY: rig.head[1],
            swing: (rig.swing || []).map(s => ({ rootX: s.root[0], rootY: s.root[1], tipX: s.tip[0], tipY: s.tip[1], radius: s.radius, flex: s.flex })),
        });
        await debug('teil_figure_' + IDS[a], big[a].buf, big[a].w, big[a].h, 4);
    }
    for (let i = 0; i < names.length; i++) {
        const p = parts[names[i]], b = big[IDS.length + i];
        await savePart(names[i], b, names[i].startsWith('tile_'));
        layout.parts.push({ name: names[i], x: p.x0, y: p.y0, w: p.w, h: p.h });
        await debug('teil_' + names[i], b.buf, b.w, b.h, 4);
    }
    const json = path.join(OUT, 'layout.json');
    fs.writeFileSync(json, JSON.stringify(layout, null, 1) + '\n');
    plainMeta(json, false);

    // Prüfbild: Kulisse + Ausschnitt + Figur muss wieder den Entwurf ergeben (ohne Oberfläche)
    if (DEBUG) for (let a = 0; a < IDS.length; a++) {
        const out = Buffer.from(scene), f = figures[a];
        for (let y = BACK[1]; y < BACK[3]; y++) for (let x = BACK[0]; x < BACK[2]; x++) {
            const p = y * W + x, e = Math.min(1, (x - BACK[0]) / 24, (BACK[2] - 1 - x) / 24, (BACK[3] - 1 - y) / 12);
            for (let c = 0; c < 3; c++) out[p * 3 + c] = Math.round(out[p * 3 + c] * (1 - e) + backs[a][p * 3 + c] * e);
        }
        const bare = Buffer.from(out);
        for (let y = 0; y < f.h; y++) for (let x = 0; x < f.w; x++) {
            const p = (y + f.y0) * W + x + f.x0, al = f.buf[(y * f.w + x) * 4 + 3] / 255;
            for (let c = 0; c < 3; c++) out[p * 3 + c] = Math.round(out[p * 3 + c] * (1 - al) + f.buf[(y * f.w + x) * 4 + c] * al);
        }
        await debug('ohne_figur_' + IDS[a], bare, W, H, 3);
        await debug('probe_' + IDS[a], out, W, H, 3);
    }
    console.log(`  fertig: ${names.length} Teile, ${IDS.length} Figuren (${((Date.now() - t0) / 1000).toFixed(0)} s)`);
})().catch(e => { console.error(e); process.exit(1); });
