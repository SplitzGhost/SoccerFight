// Bessert einzelne Ausschnitte im Menü-Atlas (SoccerFight/Assets/Resources/UiButtons/Exact) nach, die
// extract-menu-buttons.py zu grob freistellt: Tafelrahmen mit Hintergrundresten in den oberen Ecken,
// der SPIELMODUS-Knopf (Fahne mit dunklem Kasten, abgeschnittene Oberkante, verwaschene Namensfläche),
// die leere Namensplatte über der Figur und die AN/AUS-Schalter (AN stand mal links, mal rechts).
// Die Pixel stammen weiterhin aus den freigegebenen Probebildern; geändert werden nur Masken und leere Flächen.
//
// Aufruf (nach extract-menu-buttons.py):  node tools/fix-exact-art.js
// Braucht die Probebilder in Inspiration/MenuPreviews und sharp aus tools/newdesign/node_modules.
'use strict';
const path = require('path');
const fs = require('fs');
const sharp = require(path.join(__dirname, 'newdesign/node_modules/sharp'));

const ROOT = path.join(__dirname, '..');
const EXACT = path.join(ROOT, 'SoccerFight/Assets/Resources/UiButtons/Exact');
const SRC = path.join(ROOT, 'Inspiration/MenuPreviews');
const MAIN = 'exec-a6e223bc-4e46-43f2-950e-a71d4fe3d0b8.png';

// ------------------------------------------------------------------ Bild-Helfer (RGBA, 0..255, y nach unten)

async function load(file) {
    const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    return { w: info.width, h: info.height, d: Buffer.from(data) };
}
function blank(w, h) { return { w, h, d: Buffer.alloc(w * h * 4) }; }
function crop(img, x, y, w, h) {
    const o = blank(w, h);
    for (let yy = 0; yy < h; yy++) img.d.copy(o.d, yy * w * 4, ((y + yy) * img.w + x) * 4, ((y + yy) * img.w + x + w) * 4);
    return o;
}
const px = (img, x, y) => { x = Math.max(0, Math.min(img.w - 1, x)); y = Math.max(0, Math.min(img.h - 1, y)); const i = (y * img.w + x) * 4; return [img.d[i], img.d[i + 1], img.d[i + 2], img.d[i + 3]]; };
function put(img, x, y, c) { const i = (y * img.w + x) * 4; for (let k = 0; k < 4; k++) img.d[i + k] = Math.max(0, Math.min(255, Math.round(c[k]))); }

/** Deckung 0..1 eines Polygons pro Pixel (4×4 Unterabtastung). */
function polyMask(w, h, poly) {
    const inside = (x, y) => {
        let c = false;
        for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
            const [xi, yi] = poly[i], [xj, yj] = poly[j];
            if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) c = !c;
        }
        return c;
    };
    const m = new Float32Array(w * h);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        let n = 0;
        for (let sy = 0; sy < 4; sy++) for (let sx = 0; sx < 4; sx++) if (inside(x + (sx + 0.5) / 4, y + (sy + 0.5) / 4)) n++;
        m[y * w + x] = n / 16;
    }
    return m;
}
function mulAlpha(img, m) { for (let i = 0; i < img.w * img.h; i++) img.d[i * 4 + 3] = Math.round(img.d[i * 4 + 3] * m[i]); }

/**
 * Füllt ein Rechteck mit der umgebenden Fläche: pro Spalte ein Verlauf zwischen der Zeile darüber und darunter,
 * dazu eine leichte Körnung aus einem Musterstreifen (grainY: Zeilen einer schriftfreien Stelle derselben Fläche,
 * hier links neben der Schrift gelesen).
 */
function fillRect(img, x0, y0, x1, y1, grainY0, grainY1) {
    const top = [], bot = [];
    for (let x = x0; x < x1; x++) { top.push(px(img, x, y0 - 1)); bot.push(px(img, x, y1)); }
    const gh = grainY1 - grainY0;
    for (let x = x0; x < x1; x++) {
        let mean = [0, 0, 0];
        for (let g = grainY0; g < grainY1; g++) { const c = px(img, x0 - 12 + (x - x0) % 10, g); for (let k = 0; k < 3; k++) mean[k] += c[k] / gh; }
        for (let y = y0; y < y1; y++) {
            const t = (y - y0 + 0.5) / (y1 - y0), a = top[x - x0], b = bot[x - x0];
            const g = px(img, x0 - 12 + (x - x0) % 10, grainY0 + (y - y0) % gh);
            const c = [0, 1, 2].map(k => a[k] + (b[k] - a[k]) * t + (g[k] - mean[k]) * 0.5);
            c.push(255);
            put(img, x, y, c);
        }
    }
}

/** Kopiert ein Rechteck (mit Skalierung, nächster Nachbar) in ein anderes. */
function blit(dst, src, sx, sy, sw, sh, dx, dy, dw, dh) {
    for (let y = 0; y < dh; y++) for (let x = 0; x < dw; x++)
        put(dst, dx + x, dy + y, px(src, sx + Math.min(sw - 1, Math.floor(x * sw / dw)), sy + Math.min(sh - 1, Math.floor(y * sh / dh))));
}

/**
 * Stellt den einfarbigen Hintergrund eines Symbols frei: Flutfüllung von den deckenden Randpixeln aus durch alle
 * Pixel, die der mittleren Randfarbe ähneln. Die dunkle Kontur des Symbols hält die Füllung auf; an der neuen
 * Kante wird ein Pixel weich ausgeblendet.
 */
function keyBackground(img, tol = 42) {
    const { w, h, d } = img, N = w * h;
    const border = [];
    for (let x = 0; x < w; x++) border.push(x, (h - 1) * w + x);
    for (let y = 1; y < h - 1; y++) border.push(y * w, y * w + w - 1);
    const seeds = border.filter(i => d[i * 4 + 3] > 200);
    if (seeds.length < border.length * 0.3) return;   // kaum Rand: schon freigestellt
    const ref = [0, 1, 2].map(k => { const v = seeds.map(i => d[i * 4 + k]).sort((a, b) => a - b); return v[v.length >> 1]; });
    const near = i => Math.hypot(d[i * 4] - ref[0], d[i * 4 + 1] - ref[1], d[i * 4 + 2] - ref[2]) < tol;
    const bg = new Uint8Array(N), q = [];
    for (const i of seeds) if (near(i) && !bg[i]) { bg[i] = 1; q.push(i); }
    while (q.length) {
        const p = q.pop(), x = p % w, y = (p / w) | 0;
        for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
            const nx = x + dx, ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
            const n = ny * w + nx;
            if (bg[n] || d[n * 4 + 3] < 8 || !near(n)) continue;
            bg[n] = 1; q.push(n);
        }
    }
    // übrig gebliebene Hintergrundinseln (Farbverlauf in den Ecken) entfernen: nur große zusammenhängende Teile bleiben
    const comp = new Int32Array(N).fill(-1), sizes = [];
    for (let s = 0; s < N; s++) {
        if (comp[s] >= 0 || bg[s] || d[s * 4 + 3] < 40) continue;
        const id = sizes.length; let n = 0; const st = [s]; comp[s] = id;
        while (st.length) {
            const p = st.pop(), x = p % w, y = (p / w) | 0; n++;
            for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
                const nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                const m = ny * w + nx;
                if (comp[m] >= 0 || bg[m] || d[m * 4 + 3] < 40) continue;
                comp[m] = id; st.push(m);
            }
        }
        sizes.push(n);
    }
    const biggest = Math.max(0, ...sizes);
    for (let i = 0; i < N; i++) if (comp[i] >= 0 && sizes[comp[i]] < biggest * 0.1) bg[i] = 1;
    for (let i = 0; i < N; i++) {
        if (bg[i]) { d[i * 4 + 3] = 0; continue; }
        const x = i % w, y = (i / w) | 0;
        let n = 0;
        for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
            const nx = x + dx, ny = y + dy;
            if (nx >= 0 && ny >= 0 && nx < w && ny < h && bg[ny * w + nx]) n++;
        }
        if (n) d[i * 4 + 3] = Math.round(d[i * 4 + 3] * (1 - 0.15 * n));
    }
}

// ------------------------------------------------------------------ Atlas

const layoutFile = path.join(EXACT, 'layout.json');
const layout = JSON.parse(fs.readFileSync(layoutFile, 'utf8'));
const item = name => { const e = layout.items.find(i => i.name === name); if (!e) throw new Error('fehlt: ' + name); return e; };

async function main() {
    // nur einmal auf frisch ausgeschnittene Atlanten anwenden (Abdunkeln und Masken würden sich sonst verdoppeln)
    if (layout.fixed) { console.log('Atlas ist schon nachgebessert – erst extract-menu-buttons.py neu laufen lassen'); return; }
    const pages = [];
    for (let p = 0; p < layout.pages; p++) pages.push(await load(path.join(EXACT, 'atlas-' + p + '.png')));
    const main = await load(path.join(SRC, MAIN));
    const get = name => { const e = item(name), pg = pages[e.page]; return crop(pg, e.x, pg.h - e.y - e.height, e.width, e.height); };
    const set = (name, img) => {
        const e = item(name), pg = pages[e.page];
        if (img.w !== e.width || img.h !== e.height) throw new Error(name + ': Größe ' + img.w + '×' + img.h);
        const top = pg.h - e.y - e.height;
        for (let y = 0; y < img.h; y++) img.d.copy(pg.d, ((top + y) * pg.w + e.x) * 4, y * img.w * 4, (y + 1) * img.w * 4);
    };

    // --- Tafelrahmen: die oberen Ecken sind gestuft; der alte Achteck-Schnitt ließ dort Hintergrund stehen.
    // Neun-Teilung: die Stufe gehört ganz in den Rand, sonst wird sie bei breiten Tafeln zur langen Schräge.
    const frame = [[75, 3], [374, 3], [398, 22], [449, 22], [460, 42], [461, 700], [416, 745], [45, 745], [0, 700], [1, 43], [19, 22], [59, 22]];
    for (const name of ['panel-border', 'frame-only']) {
        const img = get(name);
        mulAlpha(img, polyMask(img.w, img.h, frame));
        set(name, img);
        item(name).borders = [80, 48, 92, 46];   // links, unten, rechts, oben (Sprite-Pixel)
    }

    // --- SPIELMODUS: Platte mit ihrer echten Kontur, darüber nur die Fahne (hell) statt eines dunklen Kastens.
    const modeBox = [1187, 529, 455, 170];
    const plate = [[28, 17], [428, 17], [454, 42], [454, 131], [427, 158], [28, 158], [2, 132], [2, 43]];
    for (const name of ['main-mode', 'main-mode-blank']) {
        const img = crop(main, ...modeBox);
        const m = polyMask(img.w, img.h, plate);
        for (let y = 0; y < 22; y++) for (let x = 84; x < 152; x++) {
            // Fahne, Mast und Knauf sind hell und kräftig; der Nachthimmel dahinter ist dunkel
            const [r, g, b] = px(img, x, y), v = Math.max(r, g, b) / 255;
            const k = Math.max(0, Math.min(1, (v - 0.34) / 0.14));
            m[y * img.w + x] = Math.max(m[y * img.w + x], k);
        }
        mulAlpha(img, m);
        // leere Fassung: der Schriftzug „ERSTE SCHRITTE“ weicht der Plattenfläche (Körnung aus der Zeile darüber)
        if (name === 'main-mode-blank') fillRect(img, 158, 37, 408, 75, 40, 44);
        set(name, img);
    }

    // --- Namensplatte über der Figur: statt verwaschener Fläche eine leere Einlassung für Name und Rolle
    {
        const img = crop(main, 690, 197, 293, 81);
        const tag = get('main-tag-blank');
        for (let i = 0; i < img.w * img.h; i++) img.d[i * 4 + 3] = tag.d[i * 4 + 3];   // Silhouette wie gehabt
        // Plattenfläche über der Einlassung: rechte, schriftfreie Spalten nach links wiederholen
        blit(img, img, 168, 3, 60, 20, 92, 3, 76, 20);
        // Einlassung: rechter Teil (ohne Schrift) in die Breite ziehen, linker Rand gespiegelt
        const box = crop(img, 176, 22, 58, 46);
        const L = 92, R = 234, T = 9, B = 68, edge = 9;
        blit(img, box, edge, 0, box.w - 2 * edge, box.h, L + edge, T, R - L - 2 * edge, B - T);
        blit(img, box, box.w - edge, 0, edge, box.h, R - edge, T, edge, B - T);
        for (let y = 0; y < B - T; y++) for (let x = 0; x < edge; x++)
            put(img, L + x, T + y, px(box, box.w - 1 - x, Math.min(box.h - 1, Math.floor(y * box.h / (B - T)))));
        set('main-tag-blank', img);
    }

    // --- Schalter: AUS immer links, AN immer rechts. Aus = Knopf links, AN-Feld dunkel; An = Häkchen rechts, AN-Feld hell.
    {
        const off = get('toggle-off'), on = get('toggle-on');
        const offNew = crop(off, 0, 0, off.w, off.h), onNew = crop(off, 0, 0, off.w, off.h);
        // AN-Feld im Aus-Zustand abdunkeln (wie das AUS-Feld)
        for (let y = 3; y < off.h - 3; y++) for (let x = 192; x < 312; x++) {
            const c = px(offNew, x, y);
            put(offNew, x, y, [c[0] * 0.55, c[1] * 0.62, c[2] * 0.68, c[3]]);
        }
        // An-Zustand: Knopf links durch leere Schiene ersetzen, Häkchenknopf rechts neben das AN-Feld
        blit(onNew, off, 170, 0, 18, off.h, 106, 0, 70, off.h);
        const knob = crop(on, 129, 0, 62, on.h);
        const kh = off.h, kw = Math.round(62 * kh / on.h);
        for (let y = 0; y < kh; y++) for (let x = 0; x < kw; x++) {
            const c = px(knob, Math.floor(x * 62 / kw), Math.floor(y * on.h / kh));
            const a = c[3] / 255, dx = 134 + x;
            const b = px(onNew, dx, y);
            put(onNew, dx, y, [b[0] + (c[0] - b[0]) * a, b[1] + (c[1] - b[1]) * a, b[2] + (c[2] - b[2]) * a, Math.max(b[3], c[3])]);
        }
        set('toggle-off', offNew);
        // der An-Ausschnitt ist 2 px höher: unten und oben je eine Zeile der Kante wiederholen
        const onOut = blank(on.w, on.h);
        blit(onOut, onNew, 0, 0, onNew.w, onNew.h, 0, 1, Math.min(on.w, onNew.w), onNew.h);
        for (let x = 0; x < on.w; x++) { put(onOut, x, 0, px(onNew, x, 0)); put(onOut, x, on.h - 1, px(onNew, x, onNew.h - 1)); }
        set('toggle-on', onOut);
    }

    // --- Symbole: viele wurden mit einem dunklen Rechteck aus dem Probebild ausgeschnitten, das auf Karten und in
    // Ringen als Kasten sichtbar bleibt. Die Randfarbe wird von außen her (bis zur dunklen Kontur) freigestellt.
    for (const e of layout.items) {
        if (!/^(menu|sport)-icon-\d+$/.test(e.name)) continue;
        const img = get(e.name);
        keyBackground(img, { 'menu-icon-12': 62, 'menu-icon-4': 62, 'menu-icon-10': 60, 'sport-icon-8': 60 }[e.name] || 42);
        set(e.name, img);
    }

    for (let p = 0; p < pages.length; p++) {
        const pg = pages[p];
        await sharp(pg.d, { raw: { width: pg.w, height: pg.h, channels: 4 } }).png({ compressionLevel: 9, adaptiveFiltering: true }).toFile(path.join(EXACT, 'atlas-' + p + '.png'));
    }
    layout.fixed = true;
    fs.writeFileSync(layoutFile, JSON.stringify(layout, null, 2) + '\n');
    console.log('Atlas nachgebessert');
}

main().catch(e => { console.error(e); process.exit(1); });
