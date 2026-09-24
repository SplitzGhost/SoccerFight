// Inhaltsbasiertes Auffüllen (wie „Content-Aware Fill“): Lücken im Bild werden aus passenden Stücken
// des restlichen Bildes zusammengesetzt. Verfahren nach Wexler et al. (Space-Time Completion):
// Bildpyramide, auf jeder Stufe PatchMatch (nächster passender 7×7-Ausschnitt je Lückenpixel) und
// gewichtetes Mitteln der überlappenden Ausschnitte. Ein Höhen-Malus sorgt dafür, dass Himmel aus dem
// Himmel und Boden aus dem Boden kommt (die Kulissen sind waagrecht geschichtet).
'use strict';

const R = 3, P = 2 * R + 1;

function downsample(img, W, H) {
    const w = Math.max(1, W >> 1), h = Math.max(1, H >> 1), out = new Float32Array(w * h * 3);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) for (let c = 0; c < 3; c++) {
        let s = 0, n = 0;
        for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) { const X = Math.min(W - 1, 2 * x + dx), Y = Math.min(H - 1, 2 * y + dy); s += img[(Y * W + X) * 3 + c]; n++; }
        out[(y * w + x) * 3 + c] = s / n;
    }
    return { img: out, w, h };
}

function downMask(m, W, H, any) {
    const w = Math.max(1, W >> 1), h = Math.max(1, H >> 1), out = new Uint8Array(w * h);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        let a = 0, n = 0;
        for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) { const X = Math.min(W - 1, 2 * x + dx), Y = Math.min(H - 1, 2 * y + dy); a += m[Y * W + X]; n++; }
        out[y * w + x] = any ? (a > 0 ? 1 : 0) : (a == n ? 1 : 0);
    }
    return out;
}

function rng(seed) { let s = seed >>> 0 || 1; return () => { s ^= s << 13; s >>>= 0; s ^= s >> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; }; }

/**
 * img: Float32Array RGB (W*H*3), hole: Uint8Array (1 = fill), src: Uint8Array (1 = may be copied from; default: not hole).
 * opts.yWeight: cost per pixel of height difference (squared distance units), opts.xWeight likewise.
 * opts.yMap(y) -> preferred source row for target row y (outpainting: rows outside the picture).
 * Returns a new Float32Array.
 */
function inpaint(img, W, H, hole, src, opts = {}) {
    if (!src) { src = new Uint8Array(W * H); for (let i = 0; i < W * H; i++) src[i] = hole[i] ? 0 : 1; }
    const levels = [];
    let cur = { img, w: W, h: H, hole, src };
    levels.push(cur);
    while (Math.min(cur.w, cur.h) / 2 >= 48 && levels.length < (opts.levels || 6)) {
        const d = downsample(cur.img, cur.w, cur.h);
        cur = { img: d.img, w: d.w, h: d.h, hole: downMask(cur.hole, cur.w, cur.h, true), src: downMask(cur.src, cur.w, cur.h, false) };
        levels.push(cur);
    }
    const rand = rng(opts.seed || 12345);
    let prevNNF = null, prevImg = null, prevW = 0, prevH = 0;
    for (let L = levels.length - 1; L >= 0; L--) {
        const { w, h, hole: Hm, src: Sm } = levels[L];
        const scale = 1 << L;
        const J = new Float32Array(levels[L].img);   // current estimate
        // valid source centres: whole patch inside the picture, known and allowed
        const valid = new Uint8Array(w * h);
        const srcList = [];
        for (let y = R; y < h - R; y++) for (let x = R; x < w - R; x++) {
            let ok = true;
            for (let dy = -R; dy <= R && ok; dy++) for (let dx = -R; dx <= R; dx++) if (!Sm[(y + dy) * w + x + dx] || Hm[(y + dy) * w + x + dx]) { ok = false; break; }
            if (ok) { valid[y * w + x] = 1; srcList.push(y * w + x); }
        }
        if (!srcList.length) throw new Error('keine Quelle auf Stufe ' + L);
        // targets: pixels whose patch touches the hole
        const near = new Uint8Array(w * h);
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (Hm[y * w + x])
            for (let dy = -R; dy <= R; dy++) for (let dx = -R; dx <= R; dx++) { const X = x + dx, Y = y + dy; if (X >= 0 && Y >= 0 && X < w && Y < h) near[Y * w + X] = 1; }
        const targets = [];
        for (let i = 0; i < w * h; i++) if (near[i]) targets.push(i);

        // initial fill
        if (prevImg) {
            for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (Hm[y * w + x]) {
                const fx = Math.min(prevW - 1, (x + 0.5) / 2 - 0.5), fy = Math.min(prevH - 1, (y + 0.5) / 2 - 0.5);
                const x0 = Math.max(0, Math.floor(fx)), y0 = Math.max(0, Math.floor(fy)), x1 = Math.min(prevW - 1, x0 + 1), y1 = Math.min(prevH - 1, y0 + 1);
                const ax = Math.max(0, fx - x0), ay = Math.max(0, fy - y0);
                for (let c = 0; c < 3; c++) {
                    const a = prevImg[(y0 * prevW + x0) * 3 + c] * (1 - ax) + prevImg[(y0 * prevW + x1) * 3 + c] * ax;
                    const b = prevImg[(y1 * prevW + x0) * 3 + c] * (1 - ax) + prevImg[(y1 * prevW + x1) * 3 + c] * ax;
                    J[(y * w + x) * 3 + c] = a * (1 - ay) + b * ay;
                }
            }
        } else {
            blockInit(J, w, h, Hm, Sm);
        }
        const S = levels[L].img;   // source pixels (known part never changes)
        const yW = opts.yWeight ?? 1, xW = opts.xWeight ?? 0;
        const yMap = opts.yMap ? (y => opts.yMap(y * scale) / scale) : (y => y);
        let skipHole = false;
        const cost = (t, s, best) => {
            const tx = t % w, ty = (t / w) | 0, sx = s % w, sy = (s / w) | 0;
            const dyp = sy - yMap(ty), dxp = sx - tx;
            let d = (yW * dyp * dyp + xW * dxp * dxp) * scale * scale;
            if (d >= best) return d;
            for (let dy = -R; dy <= R; dy++) {
                const Y = ty + dy; if (Y < 0 || Y >= h) continue;
                for (let dx = -R; dx <= R; dx++) {
                    const X = tx + dx; if (X < 0 || X >= w) continue;
                    if (skipHole && Hm[Y * w + X]) continue;
                    const a = (Y * w + X) * 3, b = ((sy + dy) * w + sx + dx) * 3;
                    const e0 = J[a] - S[b], e1 = J[a + 1] - S[b + 1], e2 = J[a + 2] - S[b + 2];
                    d += e0 * e0 + e1 * e1 + e2 * e2;
                }
                if (d >= best) return d;
            }
            return d;
        };
        const nnf = new Int32Array(w * h).fill(-1), nd = new Float32Array(w * h).fill(Infinity);
        skipHole = false;
        for (const t of targets) {
            let s = -1;
            if (prevNNF) {
                const x = t % w, y = (t / w) | 0, px = Math.min(prevW - 1, x >> 1), py = Math.min(prevH - 1, y >> 1);
                const ps = prevNNF[py * prevW + px];
                if (ps >= 0) { const sx = Math.min(w - R - 1, (ps % prevW) * 2 + (x & 1)), sy = Math.min(h - R - 1, ((ps / prevW) | 0) * 2 + (y & 1)); if (valid[sy * w + sx]) s = sy * w + sx; }
            }
            if (s < 0) s = srcList[(rand() * srcList.length) | 0];
            nnf[t] = s; nd[t] = cost(t, s, Infinity);
        }
        const tryS = (t, s) => { if (s < 0 || !valid[s] || s == nnf[t]) return; const d = cost(t, s, nd[t]); if (d < nd[t]) { nd[t] = d; nnf[t] = s; } };
        const pmIter = (iters) => {
            for (let it = 0; it < iters; it++) {
                const fwd = it % 2 == 0;
                for (let k = 0; k < targets.length; k++) {
                    const t = targets[fwd ? k : targets.length - 1 - k];
                    const x = t % w, y = (t / w) | 0, st = fwd ? -1 : 1;
                    // propagation
                    if (x + st >= 0 && x + st < w && near[t + st]) { const s = nnf[t + st]; if (s >= 0) { const sx = s % w - st; if (sx >= 0 && sx < w) tryS(t, s - st); } }
                    if (y + st >= 0 && y + st < h && near[t + st * w]) { const s = nnf[t + st * w]; if (s >= 0) tryS(t, s - st * w); }
                    // random search
                    let rad = Math.max(w, h);
                    const s0 = nnf[t], sx0 = s0 % w, sy0 = (s0 / w) | 0;
                    while (rad >= 1) {
                        const sx = Math.round(sx0 + (rand() * 2 - 1) * rad), sy = Math.round(sy0 + (rand() * 2 - 1) * Math.min(rad, h));
                        if (sx >= R && sy >= R && sx < w - R && sy < h - R) tryS(t, sy * w + sx);
                        rad >>= 1;
                    }
                }
            }
        };
        const em = L == 0 ? (opts.emFine || 4) : L >= levels.length - 2 ? 10 : 6;
        for (let e = 0; e < em; e++) {
            if (!(e == 0 && prevNNF)) pmIter(e == 0 ? 5 : 3);   // finer level: first rebuild from the upsampled matches (keeps texture)
            if (skipHole) { skipHole = false; }
            // weights from the distance distribution
            const ds = targets.map(t => nd[t]).sort((a, b) => a - b);
            const sig2 = Math.max(1, ds[Math.floor(ds.length * (opts.sigmaQ ?? 0.35))]);
            const acc = new Float32Array(w * h * 4);
            for (const t of targets) {
                const tx = t % w, ty = (t / w) | 0, s = nnf[t], sx = s % w, sy = (s / w) | 0;
                const wt = Math.exp(-nd[t] / (2 * sig2)) + 1e-4;
                for (let dy = -R; dy <= R; dy++) {
                    const Y = ty + dy; if (Y < 0 || Y >= h) continue;
                    for (let dx = -R; dx <= R; dx++) {
                        const X = tx + dx; if (X < 0 || X >= w) continue;
                        const i = Y * w + X; if (!Hm[i]) continue;
                        const b = ((sy + dy) * w + sx + dx) * 3;
                        acc[i * 4] += wt * S[b]; acc[i * 4 + 1] += wt * S[b + 1]; acc[i * 4 + 2] += wt * S[b + 2]; acc[i * 4 + 3] += wt;
                    }
                }
            }
            for (let i = 0; i < w * h; i++) if (Hm[i] && acc[i * 4 + 3] > 0) for (let c = 0; c < 3; c++) J[i * 3 + c] = acc[i * 4 + c] / acc[i * 4 + 3];
            // distances are stale after the update: refresh
            for (const t of targets) nd[t] = cost(t, nnf[t], Infinity);
        }
        prevNNF = nnf; prevImg = J; prevW = w; prevH = h;
        if (opts.log) console.log(`    Stufe ${L}: ${w}×${h}, ${targets.length} Ziele`);
    }
    return prevImg;
}

/**
 * Coarsest level: every hole gets whole blocks copied from a place whose surroundings match best
 * (coherent texture to start from — averaging random patches would converge to flat colour).
 */
function blockInit(J, w, h, Hm, Sm) {
    const lab = new Int32Array(w * h).fill(-1), boxes = [];
    for (let s0 = 0; s0 < w * h; s0++) {
        if (!Hm[s0] || lab[s0] >= 0) continue;
        const st = [s0]; lab[s0] = boxes.length; let x0 = w, y0 = h, x1 = -1, y1 = -1;
        while (st.length) {
            const p = st.pop(), x = p % w, y = (p / w) | 0;
            x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y);
            for (const q of [x > 0 ? p - 1 : -1, x < w - 1 ? p + 1 : -1, y > 0 ? p - w : -1, y < h - 1 ? p + w : -1])
                if (q >= 0 && Hm[q] && lab[q] < 0) { lab[q] = boxes.length; st.push(q); }
        }
        boxes.push([x0, y0, x1 + 1, y1 + 1]);
    }
    const filled = new Uint8Array(w * h);
    const maxW = Math.max(6, Math.round(w / 5)), maxH = Math.max(6, Math.round(h / 3));
    const RING = 3, dyMax = Math.max(2, Math.round(h / 10));
    for (const [bx0, by0, bx1, by1] of boxes) for (let cy = by0; cy < by1; cy += maxH) for (let cx = bx0; cx < bx1; cx += maxW) {
        const X0 = cx, X1 = Math.min(bx1, cx + maxW), Y0 = cy, Y1 = Math.min(by1, cy + maxH);
        let best = null, bestS = Infinity;
        for (const dyLim of [dyMax, h]) { if (best) break;
        for (let dy = -dyLim; dy <= dyLim; dy++) for (let dx = -w; dx <= w; dx++) {
            if (!dx && !dy) continue;
            if (X0 + dx < 0 || X1 + dx > w || Y0 + dy < 0 || Y1 + dy > h) continue;
            let ok = true;
            for (let y = Y0; y < Y1 && ok; y++) for (let x = X0; x < X1; x++) { const q = (y + dy) * w + x + dx; if (!Sm[q] || Hm[q]) { ok = false; break; } }
            if (!ok) continue;
            let sc = 0, n = 0;
            for (let y = Y0 - RING; y < Y1 + RING; y++) for (let x = X0 - RING; x < X1 + RING; x++) {
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                if (x >= X0 && x < X1 && y >= Y0 && y < Y1) continue;
                const p = y * w + x; if (Hm[p] && !filled[p]) continue;
                const X = x + dx, Y = y + dy; if (X < 0 || Y < 0 || X >= w || Y >= h) continue;
                const q = Y * w + X;
                for (let c = 0; c < 3; c++) { const d = J[p * 3 + c] - J[q * 3 + c]; sc += d * d; }
                n++;
            }
            if (!n) continue;
            sc = sc / n + dy * dy * 30;
            if (sc < bestS) { bestS = sc; best = [dx, dy]; }
        }
        }
        if (!best) { if (process.env.IPDBG) console.log("    kein Block", X0, Y0, X1, Y1, w, h); continue; }
        for (let y = Y0; y < Y1; y++) for (let x = X0; x < X1; x++) {
            const p = y * w + x; if (!Hm[p]) continue;
            const q = (y + best[1]) * w + x + best[0];
            for (let c = 0; c < 3; c++) J[p * 3 + c] = J[q * 3 + c];
            filled[p] = 1;
        }
    }
    // anything left (no valid block): nearest filled/known value along the row
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const p = y * w + x; if (!Hm[p] || filled[p]) continue;
        for (let d = 1; d < w; d++) { const q = x - d >= 0 && (!Hm[p - d] || filled[p - d]) ? p - d : x + d < w && (!Hm[p + d] || filled[p + d]) ? p + d : -1; if (q >= 0) { for (let c = 0; c < 3; c++) J[p * 3 + c] = J[q * 3 + c]; break; } }
    }
}

module.exports = { inpaint };
