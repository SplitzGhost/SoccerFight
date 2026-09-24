// Freistellen von Einzelgrafiken vor dem dunklen, leicht verlaufenden Hintergrund der Stage-Bögen
// (Inspiration/StagesNewDesigns). Genutzt von stages.js.
//
// - Hintergrundmodell pro Kachel: Zeilen- plus Spaltenprofil (Verlauf, Bodenstreifen, Vignette),
//   zweimal nachgeschärft an den als Hintergrund erkannten Pixeln.
// - Hintergrund = vom Kachelrand aus erreichbare Pixel, die dem Modell ähneln (auch Schlagschatten,
//   die nur dunkler sind). Große eingeschlossene Hintergrundflächen (Torbogen) werden ebenfalls frei.
// - Kanten: weiches Alpha aus dem Abstand zum Hintergrund, Farbe entmischt (kein dunkler Saum).
// - Leuchthöfe (Laternen, Kristalle, Lava): von außen abgeschält, solange die Farbe nur „Hintergrund
//   plus Leuchtfarbe“ ist — den Schein malt das Spiel selbst.
'use strict';
const sharp = require('sharp');

async function load(file) {
    const { data, info } = await sharp(file).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    return { W: info.width, H: info.height, rgb: data };
}

/** Grid of the close-up panel: rows/cols of light separator lines below the scene. */
function grid(img, fromY = 480) {
    const { W, H, rgb } = img;
    const light = (x, y) => { const i = (y * W + x) * 3, r = rgb[i], g = rgb[i + 1], b = rgb[i + 2]; return Math.min(r, g, b) > 70 && Math.max(r, g, b) - Math.min(r, g, b) < 40; };
    const rows = [];
    for (let y = fromY; y < H; y++) { let n = 0; for (let x = 0; x < W; x++) if (light(x, y)) n++; if (n > W * 0.6) rows.push(y); }
    const lines = a => { const out = []; for (const v of a) { if (out.length && v - out[out.length - 1][1] <= 2) out[out.length - 1][1] = v; else out.push([v, v]); } return out; };
    const R = lines(rows);
    const y0 = R[0][1] + 1;
    const cols = [];
    for (let x = 0; x < W; x++) { let n = 0; for (let y = y0; y < H; y++) if (light(x, y)) n++; if (n > (H - y0) * 0.6) cols.push(x); }
    const C = lines(cols);
    // cells: between consecutive lines (and the image edges)
    const ys = [[R[0][1] + 1, R[1][0] - 1], [R[1][1] + 1, H - 1]];
    const xb = [-1, ...C.map(c => c[0]), W];
    const xe = [-1, ...C.map(c => c[1]), W];
    const cells = [];
    for (const [cy0, cy1] of ys) for (let i = 0; i + 1 < xb.length; i++) cells.push({ x0: xe[i] + 1, y0: cy0, x1: xb[i + 1] - 1, y1: cy1 });
    return { sceneBottom: R[0][0], cells };
}

const lum = (r, g, b) => 0.3 * r + 0.59 * g + 0.11 * b;

/**
 * Cut the object out of one cell. opts:
 *  glow: [r,g,b] halo colour to peel (or null), glowMax: how far (px) to peel,
 *  label: [w,h] box in the top-left holding the number (forced background),
 *  keep: minimum component area to keep, tol: background tolerance.
 * Returns { buf (RGBA straight), w, h, x, y } cropped to the object.
 */
function cut(img, cell, opts = {}) {
    const { W, rgb } = img;
    const cx0 = cell.x0 + 2, cy0 = cell.y0 + 2, cx1 = cell.x1 - 2, cy1 = cell.y1 - 2;
    const w = cx1 - cx0 + 1, h = cy1 - cy0 + 1, N = w * h;
    const C = new Float32Array(N * 3);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const i = ((cy0 + y) * W + cx0 + x) * 3, o = (y * w + x) * 3;
        C[o] = rgb[i]; C[o + 1] = rgb[i + 1]; C[o + 2] = rgb[i + 2];
    }
    const [lw, lh] = opts.label || [48, 36];
    const tol = opts.tol || 16;
    const isLabel = (x, y) => x < lw && y < lh;

    // --- background model: row profile + column profile, refined on background pixels
    let bgMask = new Uint8Array(N);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (x < 10 || x >= w - 10 || y >= h - 6 || (y < 10 && !isLabel(x, y))) bgMask[y * w + x] = 1;
    const B = new Float32Array(N * 3);
    const median = a => { if (!a.length) return NaN; a.sort((p, q) => p - q); return a[a.length >> 1]; };
    function model() {
        // mean of the background pixels, then per-row and per-column offsets (medians)
        const mean = [0, 0, 0]; let n = 0;
        for (let p = 0; p < N; p++) if (bgMask[p]) { for (let c = 0; c < 3; c++) mean[c] += C[p * 3 + c]; n++; }
        for (let c = 0; c < 3; c++) mean[c] /= Math.max(1, n);
        const rowOff = [], colOff = [];
        for (let c = 0; c < 3; c++) {
            const ro = new Float32Array(h), co = new Float32Array(w);
            for (let y = 0; y < h; y++) { const a = []; for (let x = 0; x < w; x++) if (bgMask[y * w + x]) a.push(C[(y * w + x) * 3 + c]); ro[y] = a.length >= 4 ? median(a) - mean[c] : NaN; }
            fillNaN(ro);
            for (let x = 0; x < w; x++) { const a = []; for (let y = 0; y < h; y++) if (bgMask[y * w + x]) a.push(C[(y * w + x) * 3 + c] - ro[y]); co[x] = a.length >= 4 ? median(a) - mean[c] : NaN; }
            fillNaN(co);
            rowOff.push(smooth1(ro, 6)); colOff.push(smooth1(co, 10));
        }
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) for (let c = 0; c < 3; c++) B[(y * w + x) * 3 + c] = mean[c] + rowOff[c][y] + colOff[c][x];
    }
    const D = new Float32Array(N);
    // edge strength: Sobel on a lightly blurred copy (the background is smooth, objects have rims)
    const Gm = new Float32Array(N);
    {
        const Bl = new Float32Array(N * 3), k = [0.25, 0.5, 0.25];
        const tmp = new Float32Array(N * 3);
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) for (let c = 0; c < 3; c++) {
            let v = 0; for (let i = -1; i <= 1; i++) { const X = Math.min(w - 1, Math.max(0, x + i)); v += k[i + 1] * C[(y * w + X) * 3 + c]; }
            tmp[(y * w + x) * 3 + c] = v;
        }
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) for (let c = 0; c < 3; c++) {
            let v = 0; for (let i = -1; i <= 1; i++) { const Y = Math.min(h - 1, Math.max(0, y + i)); v += k[i + 1] * tmp[(Y * w + x) * 3 + c]; }
            Bl[(y * w + x) * 3 + c] = v;
        }
        const at = (x, y, c) => Bl[(Math.min(h - 1, Math.max(0, y)) * w + Math.min(w - 1, Math.max(0, x))) * 3 + c];
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
            let m = 0;
            for (let c = 0; c < 3; c++) {
                const gx = at(x + 1, y - 1, c) + 2 * at(x + 1, y, c) + at(x + 1, y + 1, c) - at(x - 1, y - 1, c) - 2 * at(x - 1, y, c) - at(x - 1, y + 1, c);
                const gy = at(x - 1, y + 1, c) + 2 * at(x, y + 1, c) + at(x + 1, y + 1, c) - at(x - 1, y - 1, c) - 2 * at(x, y - 1, c) - at(x + 1, y - 1, c);
                m = Math.max(m, Math.hypot(gx, gy) / 4);
            }
            Gm[y * w + x] = m;
        }
    }
    const gT = opts.grad || 3.2, loose = opts.loose || 34;
    function classify() {
        for (let p = 0; p < N; p++) { const o = p * 3; const dr = C[o] - B[o], dg = C[o + 1] - B[o + 1], db = C[o + 2] - B[o + 2]; D[p] = Math.sqrt(dr * dr + dg * dg + db * db); }
        const bg = new Uint8Array(N), stack = new Int32Array(N); let sp = 0;
        // background: very close to the model, or smooth (no rim) and not too far from it (shadows, glow)
        const ok = p => { const x = p % w, y = (p / w) | 0; return isLabel(x, y) || D[p] < tol || (Gm[p] < gT && D[p] < loose); };
        for (let x = 0; x < w; x++) for (const y of [0, h - 1]) { const p = y * w + x; if (!bg[p] && ok(p)) { bg[p] = 1; stack[sp++] = p; } }
        for (let y = 0; y < h; y++) for (const x of [0, w - 1]) { const p = y * w + x; if (!bg[p] && ok(p)) { bg[p] = 1; stack[sp++] = p; } }
        while (sp) {
            const p = stack[--sp], x = p % w, y = (p / w) | 0;
            const nb = [x > 0 ? p - 1 : -1, x < w - 1 ? p + 1 : -1, y > 0 ? p - w : -1, y < h - 1 ? p + w : -1];
            for (const q of nb) if (q >= 0 && !bg[q] && ok(q)) { bg[q] = 1; stack[sp++] = q; }
        }
        return bg;
    }
    let bg;
    for (let it = 0; it < 3; it++) { model(); bg = classify(); bgMask = bg; }

    // --- enclosed background pockets (arch openings, gaps between posts)
    {
        const seen = new Uint8Array(N), stack = new Int32Array(N);
        const pk = p => D[p] < (opts.pocketTol ?? 13) && Gm[p] < (opts.pocketGrad ?? 4);
        for (let s = 0; s < N; s++) {
            if (bg[s] || seen[s] || !pk(s)) continue;
            let sp = 0; stack[sp++] = s; seen[s] = 1; const px = [];
            while (sp) {
                const p = stack[--sp]; px.push(p);
                const x = p % w, y = (p / w) | 0;
                for (const q of [x > 0 ? p - 1 : -1, x < w - 1 ? p + 1 : -1, y > 0 ? p - w : -1, y < h - 1 ? p + w : -1])
                    if (q >= 0 && !bg[q] && !seen[q] && pk(q)) { seen[q] = 1; stack[sp++] = q; }
            }
            if (px.length >= (opts.pocket || 30)) for (const p of px) bg[p] = 1;
        }
    }
    // glow halo (lanterns, crystals, lava): peel from the outside while a pixel is only
    // "background + some of the glow colour" and has no hard edge; the game draws the glow itself
    if (opts.glow) {
        // glow colour: average direction of (C - B) over the ring of object pixels touching the background
        const gsum = [0, 0, 0];
        for (let p = 0; p < N; p++) {
            if (bg[p]) continue;
            const x = p % w, y = (p / w) | 0;
            const n = (x > 0 && bg[p - 1]) || (x < w - 1 && bg[p + 1]) || (y > 0 && bg[p - w]) || (y < h - 1 && bg[p + w]);
            if (!n) continue;
            const d = [C[p * 3] - B[p * 3], C[p * 3 + 1] - B[p * 3 + 1], C[p * 3 + 2] - B[p * 3 + 2]];
            const m = Math.hypot(d[0], d[1], d[2]); if (m < 6) continue;
            for (let c = 0; c < 3; c++) gsum[c] += d[c] / m;
        }
        const gm = Math.hypot(gsum[0], gsum[1], gsum[2]) || 1, G = gsum.map(v => v / gm);
        const halo = p => {
            const o = p * 3, d = [C[o] - B[o], C[o + 1] - B[o + 1], C[o + 2] - B[o + 2]];
            const k = d[0] * G[0] + d[1] * G[1] + d[2] * G[2];
            if (k <= 4) return D[p] < tol * 1.5;
            const r = Math.hypot(d[0] - k * G[0], d[1] - k * G[1], d[2] - k * G[2]);
            return r < 10 + k * 0.28 && Gm[p] < (opts.glowGrad || 14);
        };
        let front = [];
        for (let p = 0; p < N; p++) if (bg[p]) front.push(p);
        for (let pass = 0; pass < (opts.glowMax || 60) && front.length; pass++) {
            const next = [];
            for (const p of front) {
                const x = p % w, y = (p / w) | 0;
                for (const q of [x > 0 ? p - 1 : -1, x < w - 1 ? p + 1 : -1, y > 0 ? p - w : -1, y < h - 1 ? p + w : -1])
                    if (q >= 0 && !bg[q] && halo(q)) { bg[q] = 2; next.push(q); }
            }
            front = next;
        }
    }
    // a few rounds of edge growth: rim pixels that are nearly background join it
    for (let r = 0; r < 3; r++) {
        const add = [];
        for (let p = 0; p < N; p++) {
            if (bg[p]) continue;
            const x = p % w, y = (p / w) | 0;
            const n = (x > 0 && bg[p - 1]) || (x < w - 1 && bg[p + 1]) || (y > 0 && bg[p - w]) || (y < h - 1 && bg[p + w]);
            if (n && D[p] < (opts.grow ?? 10)) add.push(p);
        }
        for (const p of add) bg[p] = 1;
    }

    // --- alpha + colour unmixing at the rim: alpha = how far the pixel is from the background,
    // relative to the object just inside it (dark objects on the dark ground keep solid edges)
    const A = new Float32Array(N);
    const dist = new Int32Array(N).fill(99);
    {
        let front = [];
        for (let p = 0; p < N; p++) if (bg[p]) { dist[p] = 0; front.push(p); }
        for (let d = 1; d <= 4 && front.length; d++) {
            const next = [];
            for (const p of front) { const x = p % w, y = (p / w) | 0;
                for (const q of [x > 0 ? p - 1 : -1, x < w - 1 ? p + 1 : -1, y > 0 ? p - w : -1, y < h - 1 ? p + w : -1])
                    if (q >= 0 && dist[q] > d) { dist[q] = d; next.push(q); } }
            front = next;
        }
    }
    for (let p = 0; p < N; p++) {
        if (bg[p]) continue;
        if (dist[p] > 2) { A[p] = 1; continue; }
        const x = p % w, y = (p / w) | 0;
        let ref = 0;
        for (let dy = -3; dy <= 3; dy++) for (let dx = -3; dx <= 3; dx++) {
            const X = x + dx, Y = y + dy; if (X < 0 || Y < 0 || X >= w || Y >= h) continue;
            const q = Y * w + X; if (dist[q] >= 3) ref = Math.max(ref, D[q]);
        }
        if (ref < 1) ref = Math.max(D[p], tol);
        A[p] = Math.max(0, Math.min(1, dist[p] == 1 ? (D[p] / ref) * 1.1 - 0.05 : (D[p] / ref) * 1.4));
    }

    // --- components: drop specks, keep everything big enough
    const lab = new Int32Array(N).fill(-1), comps = [], stack = new Int32Array(N);
    for (let s = 0; s < N; s++) {
        if (A[s] < 0.2 || lab[s] >= 0) continue;
        let sp = 0; stack[sp++] = s; lab[s] = comps.length; let area = 0;
        while (sp) {
            const p = stack[--sp]; area++;
            const x = p % w, y = (p / w) | 0;
            for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
                const X = x + dx, Y = y + dy; if (X < 0 || Y < 0 || X >= w || Y >= h) continue;
                const q = Y * w + X; if (A[q] >= 0.2 && lab[q] < 0) { lab[q] = comps.length; stack[sp++] = q; }
            }
        }
        comps.push(area);
    }
    const keep = opts.keep || 60;
    for (let p = 0; p < N; p++) {
        if (A[p] <= 0) continue;
        // faint rim pixels belong to the nearest component; lonely ones go
        const l = lab[p];
        if (l >= 0 && comps[l] < keep) A[p] = 0;
    }
    // remove rim pixels no longer next to anything solid
    for (let p = 0; p < N; p++) if (A[p] > 0 && A[p] < 0.2) {
        const x = p % w, y = (p / w) | 0; let near = false;
        for (let dy = -2; dy <= 2 && !near; dy++) for (let dx = -2; dx <= 2; dx++) {
            const X = x + dx, Y = y + dy; if (X < 0 || Y < 0 || X >= w || Y >= h) continue;
            const q = Y * w + X; if (A[q] >= 0.2 && lab[q] >= 0 && comps[lab[q]] >= keep) { near = true; break; }
        }
        if (!near) A[p] = 0;
    }

    // --- crop + unmix
    let bx0 = w, by0 = h, bx1 = -1, by1 = -1;
    for (let p = 0; p < N; p++) if (A[p] > 0.02) { const x = p % w, y = (p / w) | 0; if (x < bx0) bx0 = x; if (x > bx1) bx1 = x; if (y < by0) by0 = y; if (y > by1) by1 = y; }
    if (bx1 < 0) throw new Error('leere Kachel');
    const ow = bx1 - bx0 + 1, oh = by1 - by0 + 1, buf = Buffer.alloc(ow * oh * 4);
    for (let y = 0; y < oh; y++) for (let x = 0; x < ow; x++) {
        const p = (by0 + y) * w + bx0 + x, o = (y * ow + x) * 4, a = A[p];
        if (a <= 0) continue;
        for (let c = 0; c < 3; c++) {
            const b = B[p * 3 + c], v = C[p * 3 + c];
            buf[o + c] = Math.max(0, Math.min(255, Math.round(a >= 0.999 ? v : b + (v - b) / a)));
        }
        buf[o + 3] = Math.round(a * 255);
    }
    return { buf, w: ow, h: oh, x: cx0 + bx0, y: cy0 + by0 };
}

function fillNaN(a) {
    let last = NaN;
    for (let i = 0; i < a.length; i++) if (isNaN(a[i])) a[i] = last; else last = a[i];
    last = NaN;
    for (let i = a.length - 1; i >= 0; i--) if (isNaN(a[i])) a[i] = last; else last = a[i];
    for (let i = 0; i < a.length; i++) if (isNaN(a[i])) a[i] = 0;
}

function smooth1(a, r) {
    const out = new Float32Array(a.length);
    for (let i = 0; i < a.length; i++) { let s = 0, n = 0; for (let k = -r; k <= r; k++) { const j = i + k; if (j >= 0 && j < a.length) { s += a[j]; n++; } } out[i] = s / n; }
    return out;
}

module.exports = { load, grid, cut };
