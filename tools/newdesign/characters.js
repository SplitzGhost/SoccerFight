// Schneidet die Spielfiguren aus den Figurenbögen (Inspiration/CharackterNewDesign) aus und legt pro Figur
// einen Atlas + JSON nach SoccerFight/Assets/Resources/Characters. Aufruf: node characters.js [id ...]
//
// Maßgeblich ist die Seitenansicht (dritte Figur oben im Bogen). Pro Figur stehen unten in FIG:
//  - Gelenke (Hüfte, Knie, Knöchel, Schulter, Ellbogen, Handgelenk, Halsansatz, Kopfdrehpunkt) in Bogen-Pixeln
//  - Umrisse (Polygone) für Kopf, Hals, Rumpf, Hose, das vordere Bein und den vorderen Arm, Schuh
// Arm und Bein werden an den Gelenken in Glieder zerlegt; jedes Glied bekommt runde Enden, die unter das
// Nachbarglied reichen, damit beim Beugen keine Lücke aufgeht. Was in der Seitenansicht verdeckt ist
// (Rumpf und Hose hinter dem Arm, Socke im Schuh), wird aus den Nachbarpixeln aufgefüllt.
// Die Glieder werden so gedreht, dass der Knochen senkrecht nach unten zeigt (so erwartet es PlayerRig).
// Texturen: vormultipliziertes Alpha, wie bei den Design-Bildern (build.js).
'use strict';
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const SRC = path.join(__dirname, '../../Inspiration/CharackterNewDesign');
const OUT = path.join(__dirname, '../../SoccerFight/Assets/Resources/Characters');
const DEBUG = process.env.SF_DEBUG;   // Ordner für Prüfbilder (optional)

/** Knöchelhöhe über der Sohle in Spieleinheiten (PlayerDims.AnkleHeight). */
const ANKLE = 0.13;

// ------------------------------------------------------------------ Figuren

const FIG = require('./characters.def.js');

// ------------------------------------------------------------------ Bild-Helfer

async function load(file) {
    const { data, info } = await sharp(path.join(SRC, file)).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    const W = info.width, H = info.height, N = W * H;
    const rgb = new Float32Array(N * 3), a = new Float32Array(N);
    for (let i = 0; i < N; i++) {
        // Figuren haben Alpha ~250 statt 255: innen voll deckend, Kante bleibt weich
        a[i] = Math.min(1, Math.max(0, (data[i * 4 + 3] - 10) / 235));
        for (let k = 0; k < 3; k++) rgb[i * 3 + k] = data[i * 4 + k] / 255;
    }
    // Kantenpixel tragen Farbe des (unsichtbaren) Hintergrund-Schleiers: Farbe von innen nach außen ziehen
    const solid = new Uint8Array(N);
    for (let i = 0; i < N; i++) solid[i] = a[i] > 0.92 ? 1 : 0;
    for (let pass = 0; pass < 4; pass++) {
        const add = [];
        for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) {
            const p = y * W + x;
            if (solid[p] || a[p] <= 0) continue;
            let n = 0, r = 0, g = 0, b = 0;
            for (const q of [p - 1, p + 1, p - W, p + W, p - W - 1, p - W + 1, p + W - 1, p + W + 1]) if (solid[q] == 1) {
                n++; r += rgb[q * 3]; g += rgb[q * 3 + 1]; b += rgb[q * 3 + 2];
            }
            if (n) add.push([p, r / n, g / n, b / n]);
        }
        for (const [p, r, g, b] of add) { rgb[p * 3] = r; rgb[p * 3 + 1] = g; rgb[p * 3 + 2] = b; solid[p] = 2; }
        for (const [p] of add) solid[p] = 1;
    }
    return { W, H, rgb, a };
}

/** Punkt-in-Polygon (gerade/ungerade). */
function inPoly(poly, x, y) {
    let c = false;
    for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
        const [xi, yi] = poly[i], [xj, yj] = poly[j];
        if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) c = !c;
    }
    return c;
}

/** Deckung 0..1 einer Form pro Pixel (4×4 Unterabtastung) im Rechteck box. */
function cover(box, test) {
    const [x0, y0, w, h] = box, out = new Float32Array(w * h);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        let n = 0;
        for (let sy = 0; sy < 4; sy++) for (let sx = 0; sx < 4; sx++) if (test(x0 + x + (sx + 0.5) / 4, y0 + y + (sy + 0.5) / 4)) n++;
        out[y * w + x] = n / 16;
    }
    return out;
}

const polyTest = poly => (x, y) => inPoly(poly, x, y);
const anyPoly = polys => (x, y) => polys.some(p => inPoly(p, x, y));

function bboxOf(polys, pad) {
    let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9;
    for (const p of polys) for (const [x, y] of p) { x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y); }
    x0 = Math.floor(x0 - pad); y0 = Math.floor(y0 - pad);
    return [x0, y0, Math.ceil(x1 + pad) - x0, Math.ceil(y1 + pad) - y0];
}

/**
 * Ein Bereich des Bogens als eigenes Bild: Umriss poly; was unter hide liegt (davor liegende Teile), gilt als
 * unbekannt und wird aufgefüllt, ebenso Stellen im Umriss, die in der Vorlage fehlen, aber unter hide liegen.
 * zones: Unterbereiche, deren Lücken nur aus ihren eigenen Pixeln gefüllt werden (Hose vs. Haut).
 */
function region(img, poly, hide = [], zones = [], fillFrom = null) {
    const box = bboxOf([poly], 3);
    const [bx, by, w, h] = box;
    const inside = cover(box, polyTest(poly));
    const hidden = hide.length ? cover(box, anyPoly(hide)) : new Float32Array(w * h);
    const rgb = new Float32Array(w * h * 3), a = new Float32Array(w * h), known = new Uint8Array(w * h);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const i = y * w + x, sx = bx + x, sy = by + y;
        let sa = 0;
        if (sx >= 0 && sy >= 0 && sx < img.W && sy < img.H) {
            const p = sy * img.W + sx;
            sa = img.a[p];
            for (let k = 0; k < 3; k++) rgb[i * 3 + k] = img.rgb[p * 3 + k];
        }
        a[i] = inside[i] * Math.max(sa, hidden[i]);
        known[i] = sa > 0.5 && hidden[i] < 0.02 && inside[i] > 0 ? 1 : 0;
    }
    // Zonen: jeder Pixel gehört zur ersten Zone, die ihn enthält (sonst Zone 0 = alles)
    const zone = new Uint8Array(w * h);
    zones.forEach((z, zi) => {
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (!zone[y * w + x] && inPoly(z, bx + x + 0.5, by + y + 0.5)) zone[y * w + x] = zi + 1;
    });
    if (fillFrom) for (let i = 0; i < w * h; i++) if (known[i] && !fillFrom(bx + i % w, by + ((i / w) | 0), rgb, i)) known[i] = 0;
    inpaint(rgb, known, a, zone, w, h);
    return { box, w, h, rgb, a };
}

/** Füllt unbekannte Pixel glatt aus den bekannten (Diffusion, grob → fein), getrennt nach Zonen. */
function inpaint(rgb, known, a, zone, w, h) {
    const need = new Uint8Array(w * h);
    let any = false;
    for (let i = 0; i < w * h; i++) if (!known[i] && a[i] > 0) { need[i] = 1; any = true; }
    if (!any) return;
    const zones = new Set(zone);
    for (const z of zones) {
        const todo = [];
        for (let i = 0; i < w * h; i++) if (need[i] && zone[i] == z) todo.push(i);
        if (!todo.length) continue;
        // Quellen: nur bekannte Pixel in der Hauptfarbe der Zone. Seitenstreifen, Borten und Nummern laufen sonst
        // als Schlieren in die verdeckte Fläche; hinter dem Arm liegt ohnehin nur der Grundstoff.
        const bins = new Map();
        for (let i = 0; i < w * h; i++) if (known[i] && zone[i] == z) {
            const key = ((rgb[i * 3] * 7.99) | 0) * 64 + ((rgb[i * 3 + 1] * 7.99) | 0) * 8 + ((rgb[i * 3 + 2] * 7.99) | 0);
            const b = bins.get(key) || [0, 0, 0, 0];
            b[0]++; b[1] += rgb[i * 3]; b[2] += rgb[i * 3 + 1]; b[3] += rgb[i * 3 + 2];
            bins.set(key, b);
        }
        if (!bins.size) continue;
        let best = null;
        for (const b of bins.values()) if (!best || b[0] > best[0]) best = b;
        const dom = [best[1] / best[0], best[2] / best[0], best[3] / best[0]];
        const kn = [];
        for (let i = 0; i < w * h; i++) if (known[i] && zone[i] == z &&
            Math.hypot(rgb[i * 3] - dom[0], rgb[i * 3 + 1] - dom[1], rgb[i * 3 + 2] - dom[2]) < 0.2) kn.push(i);
        // Mehrquellen-Breitensuche für den Startwert (andersfarbige bekannte Pixel bleiben unberührt)
        const dist = new Int32Array(w * h).fill(-1), q = new Int32Array(w * h);
        let qh = 0, qt = 0;
        for (const i of kn) { dist[i] = 0; q[qt++] = i; }
        while (qh < qt) {
            const p = q[qh++], x = p % w, y = (p / w) | 0;
            for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
                const nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                const n = ny * w + nx;
                if (dist[n] >= 0 || zone[n] != z || known[n]) continue;
                dist[n] = dist[p] + 1;
                for (let k = 0; k < 3; k++) rgb[n * 3 + k] = rgb[p * 3 + k];
                q[qt++] = n;
            }
        }
        // glätten (Laplace), nur unbekannte Pixel ändern sich
        for (let it = 0; it < 300; it++) {
            for (const p of todo) {
                const x = p % w, y = (p / w) | 0;
                let n = 0, r = 0, g = 0, b = 0;
                for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
                    const nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    const m = ny * w + nx;
                    if (zone[m] != z || dist[m] < 0) continue;
                    n++; r += rgb[m * 3]; g += rgb[m * 3 + 1]; b += rgb[m * 3 + 2];
                }
                if (n) { rgb[p * 3] = r / n; rgb[p * 3 + 1] = g / n; rgb[p * 3 + 2] = b / n; }
            }
        }
    }
}

/** Beschneidet einen Bereich mit einer Deckungsfunktion (Bogen-Koordinaten). */
function clip(reg, test) {
    const cov = cover(reg.box, test);
    const a = new Float32Array(reg.a.length);
    for (let i = 0; i < a.length; i++) a[i] = reg.a[i] * cov[i];
    return { ...reg, a };
}

/**
 * Glied zwischen Gelenk A (oben) und B (unten): Streifen zwischen den Querschnitten durch A und B, an beiden Enden
 * um eine Kreisscheibe verlängert. rA/rB = null: an diesem Ende nicht beschneiden.
 */
function segTest(A, B, rA, rB, half = 1e9) {
    const dx = B[0] - A[0], dy = B[1] - A[1], L = Math.hypot(dx, dy), ux = dx / L, uy = dy / L;
    return (x, y) => {
        const t = (x - A[0]) * ux + (y - A[1]) * uy;
        if (Math.abs((x - A[0]) * uy - (y - A[1]) * ux) > half) return false;
        const okA = rA == null || t >= 0 || Math.hypot(x - A[0], y - A[1]) < rA;
        const okB = rB == null || t <= L || Math.hypot(x - B[0], y - B[1]) < rB;
        return okA && okB;
    };
}

/** Dreht/verschiebt ein Teil so, dass pivot (Bogen-Koordinaten) der Drehpunkt ist und dir nach unten zeigt. */
function orient(reg, pivot, dir) {
    // Winkel, um den gedreht wird: dir → (0, 1) (Bild-y zeigt nach unten)
    const ang = dir ? Math.atan2(dir[0], dir[1]) : 0;   // dir = (sinθ, cosθ) relativ zu unten
    const c = Math.cos(ang), s = Math.sin(ang);
    // Bildpunkt (bogen) → Ziel: rotiere um pivot um +ang (so dass dir auf (0,1) fällt)
    const rot = (x, y) => { const px = x - pivot[0], py = y - pivot[1]; return [px * c - py * s, px * s + py * c]; };
    const inv = (u, v) => [u * c + v * s + pivot[0], -u * s + v * c + pivot[1]];
    let u0 = 1e9, v0 = 1e9, u1 = -1e9, v1 = -1e9;
    const [bx, by, w, h] = reg.box;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (reg.a[y * w + x] > 0.003) {
        for (const [ox, oy] of [[0, 0], [1, 0], [0, 1], [1, 1]]) {
            const [u, v] = rot(bx + x + ox, by + y + oy);
            u0 = Math.min(u0, u); v0 = Math.min(v0, v); u1 = Math.max(u1, u); v1 = Math.max(v1, v);
        }
    }
    const pad = 2;
    const U0 = Math.floor(u0) - pad, V0 = Math.floor(v0) - pad, W = Math.ceil(u1) + pad - U0, H = Math.ceil(v1) + pad - V0;
    const buf = new Float32Array(W * H * 4);   // vormultipliziert
    const sample = (sx, sy) => {
        // bilinear im vormultiplizierten Raum
        const fx = sx - bx - 0.5, fy = sy - by - 0.5, x0 = Math.floor(fx), y0 = Math.floor(fy), tx = fx - x0, ty = fy - y0;
        const out = [0, 0, 0, 0];
        for (const [ox, oy, wt] of [[0, 0, (1 - tx) * (1 - ty)], [1, 0, tx * (1 - ty)], [0, 1, (1 - tx) * ty], [1, 1, tx * ty]]) {
            const x = x0 + ox, y = y0 + oy;
            if (x < 0 || y < 0 || x >= w || y >= h || wt == 0) continue;
            const i = y * w + x, al = reg.a[i];
            out[0] += reg.rgb[i * 3] * al * wt; out[1] += reg.rgb[i * 3 + 1] * al * wt; out[2] += reg.rgb[i * 3 + 2] * al * wt; out[3] += al * wt;
        }
        return out;
    };
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
        const [sx, sy] = inv(U0 + x + 0.5, V0 + y + 0.5);
        const v = sample(sx, sy);
        buf.set(v, (y * W + x) * 4);
    }
    // Drehpunkt im Teilbild (Pixel von links/oben)
    return { W, H, buf, px: -U0, py: -V0 };
}

const lum = (r, g, b) => 0.3 * r + 0.55 * g + 0.15 * b;

/** Färbt helle, ungesättigte Stoffpixel in Haut um (Schattierung bleibt); die Hautfarbe stammt unterhalb von below (Faust). */
function skinned(reg, below) {
    const [, by, w, h] = reg.box, rgb = Float32Array.from(reg.rgb);
    const cloth = i => {
        const r = rgb[i * 3], g = rgb[i * 3 + 1], b = rgb[i * 3 + 2], mx = Math.max(r, g, b), mn = Math.min(r, g, b);
        return mx > 0.45 && (mx - mn) / mx < 0.2;
    };
    let n = 0, sr = 0, sg = 0, sb = 0, cl = 0, cn = 0;
    for (let i = 0; i < w * h; i++) {
        if (reg.a[i] < 0.9) continue;
        if (cloth(i)) { cl += lum(rgb[i * 3], rgb[i * 3 + 1], rgb[i * 3 + 2]); cn++; }
        else if (by + ((i / w) | 0) > below[1] + 8) { n++; sr += rgb[i * 3]; sg += rgb[i * 3 + 1]; sb += rgb[i * 3 + 2]; }
    }
    const skin = [sr / n, sg / n, sb / n];
    cl /= Math.max(1, cn);
    for (let i = 0; i < w * h; i++) if (reg.a[i] > 0 && cloth(i)) {
        const k = lum(rgb[i * 3], rgb[i * 3 + 1], rgb[i * 3 + 2]) / cl;
        for (let c = 0; c < 3; c++) rgb[i * 3 + c] = Math.min(1, skin[c] * k);
    }
    return { ...reg, rgb };
}

// ------------------------------------------------------------------ eine Figur

async function buildFigure(id) {
    const F = FIG[id];
    const img = await load(id + '.png');
    const ppu = (F.sole - F.top) / F.height;
    const J = { ...F.joints };
    J.ankle = [J.ankle[0], F.sole - ANKLE * ppu];
    const parts = {};

    const armHide = [F.arm];
    // Rumpf, Hose, Hals, Kopf
    parts.Torso = orient(region(img, F.torso, armHide), J.hip, null);
    parts.Pelvis = orient(region(img, F.pelvis, [F.arm, F.torso]), J.hip, null);
    parts.Neck = orient(region(img, F.neck, [F.torso, ...(F.neckHide || [])]), J.neck, null);
    parts.Head = orient(region(img, F.head, F.headHide || []), J.head, null);
    if (F.tuft) parts.HairTuft = orient(region(img, F.tuft.poly), F.tuft.root, null);

    // Bein: ein Bereich (Hosenbein, Oberschenkel, Unterschenkel), der Schuh eigens
    const leg = region(img, F.leg, [F.arm, F.boot], F.legZones || []);
    const d = (A, B) => [B[0] - A[0], B[1] - A[1]];
    parts.Thigh = orient(clip(leg, segTest(J.hip, J.knee, 999, F.r.kneeCap, F.r.thighHalf)), J.hip, d(J.hip, J.knee));
    parts.Shin = orient(clip(leg, segTest(J.knee, J.ankle, F.r.knee * 1.05, F.r.ankle)), J.knee, d(J.knee, J.ankle));
    parts.Boot = orient(region(img, F.boot), J.ankle, null);

    // Arm: Oberarm (mit Ärmel), Unterarm, Hand
    const arm = region(img, F.arm, [], F.armZones || []);
    parts.UpperArm = orient(clip(arm, segTest(J.shoulder, J.elbow, 999, F.r.elbowCap || F.r.elbow)), J.shoulder, d(J.shoulder, J.elbow));
    parts.Forearm = orient(clip(arm, segTest(J.elbow, J.wrist, F.r.elbow * 1.05, F.r.wrist)), J.elbow, d(J.elbow, J.wrist));
    parts.Hand = orient(clip(arm, segTest(J.wrist, [J.wrist[0] + d(J.elbow, J.wrist)[0], J.wrist[1] + d(J.elbow, J.wrist)[1]], F.r.wrist * 1.05, null)), J.wrist, d(J.elbow, J.wrist));

    // hinterer Arm ohne Ärmel (Kompressionsärmel nur am vorderen Arm): helle Stoffpixel bekommen die Hautfarbe der Faust
    if (F.farSkin) {
        const bare = skinned(arm, J.wrist);
        parts.UpperArmFar = orient(clip(bare, segTest(J.shoulder, J.elbow, 999, F.r.elbowCap || F.r.elbow)), J.shoulder, d(J.shoulder, J.elbow));
        parts.ForearmFar = orient(clip(bare, segTest(J.elbow, J.wrist, F.r.elbow * 1.05, F.r.wrist)), J.elbow, d(J.elbow, J.wrist));
    }

    // Maße in Spieleinheiten (Bild-y zeigt nach unten → Spiel-y umdrehen)
    const u = (A, B) => [(B[0] - A[0]) / ppu, -(B[1] - A[1]) / ppu];
    const len = (A, B) => Math.hypot(B[0] - A[0], B[1] - A[1]) / ppu;
    const body = {
        thigh: len(J.hip, J.knee), shin: len(J.knee, J.ankle),
        upperArm: len(J.shoulder, J.elbow), forearm: len(J.elbow, J.wrist),
        shoulder: u(J.hip, J.shoulder), neck: u(J.hip, J.neck), head: u(J.neck, J.head),
        tuft: F.tuft ? u(J.head, F.tuft.root) : [0, 0],
        hipHeight: (F.sole - J.hip[1]) / ppu, headTop: (F.sole - F.top) / ppu,
        tuftFlex: F.tuft ? F.tuft.flex : 0,
    };
    return { id, ppu, parts, body };
}

// ------------------------------------------------------------------ Atlas

async function writeAtlas(fig) {
    const names = Object.keys(fig.parts);
    // Regalpacken, größte zuerst
    const order = names.slice().sort((a, b) => fig.parts[b].H - fig.parts[a].H);
    const gap = 6, maxW = 1024;   // Abstand: auch in kleinen Mipmap-Stufen kein Farbsaum vom Nachbarteil
    let x = gap, y = gap, row = 0, W = 0;
    const place = {};
    for (const n of order) {
        const p = fig.parts[n];
        if (x + p.W + gap > maxW) { x = gap; y += row + gap; row = 0; }
        place[n] = [x, y];
        x += p.W + gap; row = Math.max(row, p.H); W = Math.max(W, x);
    }
    const H = y + row + gap;
    const AW = Math.ceil(W / 4) * 4, AH = Math.ceil(H / 4) * 4;
    const out = Buffer.alloc(AW * AH * 4);
    const sprites = [];
    for (const n of names) {
        const p = fig.parts[n], [ox, oy] = place[n];
        for (let yy = 0; yy < p.H; yy++) for (let xx = 0; xx < p.W; xx++) {
            const i = (yy * p.W + xx) * 4, o = ((oy + yy) * AW + ox + xx) * 4;
            for (let k = 0; k < 4; k++) out[o + k] = Math.max(0, Math.min(255, Math.round(p.buf[i + k] * 255)));
        }
        // Unity: Rechteck und Drehpunkt von unten links
        sprites.push({ name: n, x: ox, y: AH - oy - p.H, w: p.W, h: p.H, px: p.px, py: p.H - p.py });
    }
    fs.mkdirSync(OUT, { recursive: true });
    const file = path.join(OUT, fig.id + '.png');
    await sharp(out, { raw: { width: AW, height: AH, channels: 4 } }).png({ compressionLevel: 9 }).toFile(file);
    writeMeta(file);
    const json = { id: fig.id, ppu: +fig.ppu.toFixed(3), body: round(fig.body), sprites };
    fs.writeFileSync(path.join(OUT, fig.id + '.json'), JSON.stringify(json, null, 1) + '\n');
    plainMeta(path.join(OUT, fig.id + '.json'), false);
    if (DEBUG) await sharp(out, { raw: { width: AW, height: AH, channels: 4 } }).png().toFile(path.join(DEBUG, fig.id + '_atlas.png'));
    return json;
}

function round(o) {
    if (Array.isArray(o)) return o.map(round);
    if (typeof o == 'number') return +o.toFixed(4);
    const r = {};
    for (const k in o) r[k] = round(o[k]);
    return r;
}

/** Unity-Importeinstellungen wie bei den Design-Bildern: vormultipliziert, Mipmaps, Clamp, verlustfrei. */
function writeMeta(file) {
    const meta = file + '.meta';
    let guid = require('crypto').randomBytes(16).toString('hex');
    if (fs.existsSync(meta)) { const m = /guid: ([0-9a-f]{32})/.exec(fs.readFileSync(meta, 'utf8')); if (m) guid = m[1]; }
    const platform = t => `  - serializedVersion: 4
    buildTarget: ${t}
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
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
  maxTextureSize: 2048
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

function plainMeta(file, folder) {
    const meta = file + '.meta';
    if (fs.existsSync(meta)) return;
    const guid = require('crypto').randomBytes(16).toString('hex');
    fs.writeFileSync(meta, folder
        ? `fileFormatVersion: 2\nguid: ${guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`
        : `fileFormatVersion: 2\nguid: ${guid}\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
}

// ------------------------------------------------------------------ Start

module.exports = { buildFigure, load, region };

if (require.main === module) (async () => {
    const ids = process.argv.slice(2).length ? process.argv.slice(2) : Object.keys(FIG);
    fs.mkdirSync(OUT, { recursive: true });
    plainMeta(OUT, true);
    for (const id of ids) {
        const t = Date.now();
        const fig = await buildFigure(id);
        const j = await writeAtlas(fig);
        console.log(id, 'ppu', j.ppu, JSON.stringify(j.body), (Date.now() - t) + ' ms');
        if (DEBUG) fs.writeFileSync(path.join(DEBUG, id + '_fig.json'), JSON.stringify(j));
    }
})().catch(e => { console.error(e); process.exit(1); });
