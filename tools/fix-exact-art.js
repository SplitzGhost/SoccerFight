// Bessert einzelne Ausschnitte im Menü-Atlas (SoccerFight/Assets/Resources/UiButtons/Exact) nach, die
// extract-menu-buttons.py zu grob freistellt: Tafelrahmen mit Hintergrundresten in den oberen Ecken,
// der SPIELMODUS-Knopf (Fahne mit dunklem Kasten, abgeschnittene Oberkante, verwaschene Namensfläche),
// die leere Namensplatte über der Figur und die AN/AUS-Schalter (AN stand mal links, mal rechts).
// Die Pixel stammen weiterhin aus den freigegebenen Probebildern; geändert werden nur Masken und leere Flächen.
//
// Tafel (panel-border), Rahmen (frame-only) und Belohnungskarte (reward-frame) entstehen bei jedem Lauf neu aus dem
// Probebild: ihre Innenfläche folgt der gemalten Innenkante des Steinrahmens (gestufte Ecken, Schrägen, Lasche) statt
// eines Rechtecks, das über die Ecksteine ragte. Alles andere wird nur einmal auf frisch ausgeschnittene Atlanten angewendet.
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

/** Abstand jedes Pixels zum nächsten Pixel außerhalb der Maske (Chamfer 1/√2, in Pixeln). */
function insideDistance(w, h, m) {
    const d = new Float32Array(w * h);
    for (let i = 0; i < w * h; i++) d[i] = m[i] >= 0.5 ? 1e9 : 0;
    const at = (x, y) => x < 0 || y < 0 || x >= w || y >= h ? 0 : d[y * w + x];
    const D = Math.SQRT2;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const i = y * w + x;
        if (d[i]) d[i] = Math.min(d[i], at(x - 1, y) + 1, at(x, y - 1) + 1, at(x - 1, y - 1) + D, at(x + 1, y - 1) + D);
    }
    for (let y = h - 1; y >= 0; y--) for (let x = w - 1; x >= 0; x--) {
        const i = y * w + x;
        if (d[i]) d[i] = Math.min(d[i], at(x + 1, y) + 1, at(x, y + 1) + 1, at(x + 1, y + 1) + D, at(x - 1, y + 1) + D);
    }
    return d;
}

// ------------------------------------------------------------------ Tafelrahmen

// Ausschnitt der Pause-Tafel im Probebild und die Konturen ihres Steinrahmens (Pixel des Ausschnitts, y nach unten).
const PAUSE = 'exec-93321712-69c7-4853-a81f-f3a3c67d82b1.png';
const PANEL_BOX = [607, 98, 461, 745];
const PANEL_OUTER = [[75, 3], [374, 3], [398, 21.5], [437, 21.5], [458.5, 44], [458.5, 707], [425, 741.5], [34, 741.5], [0.5, 708],
    [0.5, 42], [21, 21.5], [59, 21.5]];
// Innenkante: oben die gestuften Ecksteine mit der schrägen Stufe zur oberen Leiste, unten die Schrägen
const PANEL_INNER = [[17, 63], [34, 44], [44, 40.5], [63, 40], [89, 17.5], [371, 17.5], [397, 40], [416, 40.5], [426, 44], [443, 63],
    [443, 690], [411, 722], [46, 722], [17, 693]];
// Neun-Teilung (links, unten, rechts, oben): Ecksteine, Stufen und Schrägen liegen ganz in den Ecken und werden nie gestreckt
const PANEL_BORDERS = [92, 56, 92, 64];

async function rebuildPanels(get, set) {
    const src = crop(await load(path.join(SRC, PAUSE)), ...PANEL_BOX);
    const { w, h } = src;
    const outer = polyMask(w, h, PANEL_OUTER), inner = polyMask(w, h, PANEL_INNER);
    const dist = insideDistance(w, h, inner);
    // Fläche: Spaltenmittel eines schriftfreien Streifens zwischen Untertitel und erstem Knopf (feine senkrechte Maserung)
    const fill = [];
    for (let x = 0; x < w; x++) {
        const sx = Math.max(45, Math.min(414, x)), c = [0, 0, 0];
        for (let y = 154; y < 163; y++) { const p = px(src, sx, y); for (let k = 0; k < 3; k++) c[k] += p[k] / 9; }
        fill.push(c);
    }
    const panel = blank(w, h), frame = blank(w, h);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const i = y * w + x, o = px(src, x, y);
        // die ersten Pixel an der Innenkante bleiben original (Schattenfuge), weiter innen die leere Fläche
        const keep = Math.max(0, Math.min(1, (3.5 - dist[i]) / 1.5));
        const f = fill[x].map((v, k) => v + (o[k] - v) * keep);
        const c = o.slice(0, 3).map((v, k) => v + (f[k] - v) * inner[i]);
        put(panel, x, y, [...c, 255 * outer[i]]);
        put(frame, x, y, [o[0], o[1], o[2], 255 * outer[i] * (1 - inner[i])]);
    }
    set('panel-border', panel);
    set('frame-only', frame);
    // die ganze Pause-Tafel (mit ihren Knopfflächen) bekommt dieselbe Außenkontur statt des groben Achtecks
    const pause = get('pause-panel');
    for (let i = 0; i < w * h; i++) pause.d[i * 4 + 3] = Math.round(255 * outer[i]);
    set('pause-panel', pause);
    item('panel-border').borders = PANEL_BORDERS;
    item('frame-only').borders = PANEL_BORDERS;
}

// Knopfplatten aus den Pause-Knöpfen: Symbol und Schrift weichen einer durchgehenden Fläche (Zeilenmittel einer
// freien Stelle). Vollständig ist nur die rechte Spirale der goldenen Platte (links liegen überall die Symbole darüber,
// bei EINSTELLUNGEN auch rechts die Schrift): ihr Relief wird auf jede Farbe übertragen und links gespiegelt.
// Die Enden liegen ganz in den Rändern der Neun-Teilung.
const PLATES = [['plate-gold', [648, 263, 1026, 349]], ['plate-petrol', [648, 353, 1026, 441]], ['plate-coral', [648, 716, 1026, 803]]];
const PLATE_END = 78;
const PLATE_BORDERS = [PLATE_END, 24, PLATE_END, 24];

async function rebuildPlates(set) {
    const src = await load(path.join(SRC, PAUSE));
    const lum = c => 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2] + 1;
    const profileOf = img => {
        const rows = [];
        for (let y = 0; y < img.h; y++) {
            const c = [0, 0, 0];
            for (let x = 125; x < 145; x++) { const p = px(img, x, y); for (let k = 0; k < 3; k++) c[k] += p[k] / 20; }
            rows.push(c);
        }
        return rows;
    };
    const gb = PLATES[0][1], gold = crop(src, gb[0], gb[1], gb[2] - gb[0], gb[3] - gb[1]), goldRows = profileOf(gold);
    let goldAlpha = null;
    for (const [name, b] of PLATES) {
        const img = crop(src, b[0], b[1], b[2] - b[0], b[3] - b[1]), { w, h } = img;
        const rows = profileOf(img);
        // Ende an Stelle x (0 = äußerster Rand): goldenes Original bzw. dessen Helligkeitsrelief auf der eigenen Farbe
        const end = (x, y) => {
            const gy = Math.round(y * (gold.h - 1) / (h - 1)), g = px(gold, gold.w - 1 - x, gy);
            if (name === 'plate-gold') return g;
            const r = lum(g) / lum(goldRows[gy]);
            return rows[y].map(v => v * r);
        };
        const out = blank(w, h);
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
            const fromEdge = Math.min(x, w - 1 - x);
            // die Enden laufen über 10 Pixel in die Fläche aus (dort sind sie selbst noch glatte Fläche)
            const t = Math.max(0, Math.min(1, (fromEdge - (PLATE_END - 10)) / 10));
            const e = fromEdge < PLATE_END ? end(fromEdge, y) : rows[y];
            put(out, x, y, [0, 1, 2].map(k => e[k] + (rows[y][k] - e[k]) * t).concat(255));
        }
        // Ecken: der dunkle Tafelgrund außerhalb der abgeschrägten Platte wird bei der goldenen Platte von den vier
        // Ecken aus freigestellt (Gold hebt sich klar vom Grund ab); alle Platten bekommen diese Silhouette
        if (name === 'plate-gold') goldAlpha = plateSilhouette(out);
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++)
            out.d[(y * w + x) * 4 + 3] = goldAlpha[Math.round(y * (gold.h - 1) / (h - 1)) * gold.w + x];
        set(name, out);
        item(name).borders = PLATE_BORDERS;
    }
}

/** Deckung einer Platte: Flutfüllung des einfarbigen Grunds von den vier Ecken aus, an der Kante ein Pixel weich. */
function plateSilhouette(img) {
    const { w, h } = img;
    const ref = [0, 1, 2].map(k => (px(img, 0, 0)[k] + px(img, w - 1, 0)[k] + px(img, 0, h - 1)[k] + px(img, w - 1, h - 1)[k]) / 4);
    const bg = new Uint8Array(w * h), q = [];
    for (const [x, y] of [[0, 0], [w - 1, 0], [0, h - 1], [w - 1, h - 1]]) { bg[y * w + x] = 1; q.push(y * w + x); }
    while (q.length) {
        const p = q.pop(), x = p % w, y = (p / w) | 0;
        for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
            const nx = x + dx, ny = y + dy, n = ny * w + nx;
            if (nx < 0 || ny < 0 || nx >= w || ny >= h || bg[n]) continue;
            const c = px(img, nx, ny);
            if (Math.hypot(c[0] - ref[0], c[1] - ref[1], c[2] - ref[2]) > 34) continue;
            bg[n] = 1; q.push(n);
        }
    }
    const a = new Uint8Array(w * h);
    for (let i = 0; i < w * h; i++) {
        if (bg[i]) continue;
        const x = i % w, y = (i / w) | 0;
        let n = 0;
        for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) { const nx = x + dx, ny = y + dy; if (nx >= 0 && ny >= 0 && nx < w && ny < h && bg[ny * w + nx]) n++; }
        a[i] = Math.round(255 * (1 - 0.2 * n));
    }
    return a;
}

/** Symbole, deren Hintergrund zwischen Henkeln und Fuß stehen blieb: bläuliche Pixel werden durchsichtig. */
function clearBluish(img) {
    for (let i = 0; i < img.w * img.h; i++) {
        if (img.d[i * 4 + 2] > img.d[i * 4] + 20) img.d[i * 4 + 3] = 0;
    }
}

// Belohnungskarte: Steinrahmen mit Namenslasche oben; innen eine dunkle Fläche bis an die Innenkante der Fase.
const REWARDS = 'exec-ac920881-1b84-4135-a559-c9f72682b3d5.png';
const REWARD_BOX = [297, 245, 337, 451];
const REWARD_OUTER = [[71, 0], [269, 0], [287, 16], [317, 16], [336, 40], [336, 411], [303, 451], [33, 451], [0, 414], [0, 40], [24, 16], [52, 16]];
// unter der Lasche folgt die Fläche deren Unterkante, daneben den Schultersteinen
const REWARD_INNER = [[9.5, 40], [30, 21.5], [67, 21.5], [80, 49], [256, 49], [270, 21.5], [307, 21.5], [327.5, 42], [327.5, 413], [311, 430],
    [26, 430], [9.5, 413]];
// Lasche und Ecken ganz in den Rändern (die Lasche wird nur in der Breite gezogen)
const REWARD_BORDERS = [30, 40, 30, 50];

async function rebuildReward(set) {
    const img = crop(await load(path.join(SRC, REWARDS)), ...REWARD_BOX);
    const { w, h } = img;
    // Lasche ohne „GEWÖHNLICH“: die helle Einlassung pro Spalte als Verlauf zwischen ihrer Zeile über und unter der
    // Schrift (waagrecht geglättet, sonst ziehen die Ö-Punkte Streifen); links weich in die Einlassung übergehend
    {
        const x0 = 84, x1 = 252, y0 = 7, y1 = 37;
        const row = (y, x) => { const v = []; for (let d = -8; d <= 8; d++) v.push(px(img, Math.max(x0, Math.min(x1 - 1, x + d)), y)); return [0, 1, 2].map(k => v.map(c => c[k]).sort((a, b) => a - b)[8]); };
        for (let x = x0; x < x1; x++) {
            const a = row(y0 - 1, x), b = row(y1, x), e = Math.min(1, (x - x0) / 8);
            for (let y = y0; y < y1; y++) {
                const t = (y - y0 + 0.5) / (y1 - y0), o = px(img, x, y);
                put(img, x, y, [0, 1, 2].map(k => o[k] + (a[k] + (b[k] - a[k]) * t - o[k]) * e).concat(o[3]));
            }
        }
    }
    const outer = polyMask(w, h, REWARD_OUTER), inner = polyMask(w, h, REWARD_INNER);
    const dist = insideDistance(w, h, inner);
    // dunkle Fläche: Farbe der Beschreibungstafel im Probebild, deren Körnung nur halb so stark
    const sx = 26, sy = 285, sw = 9, sh = 25, mean = [0, 0, 0];
    for (let y = 0; y < sh; y++) for (let x = 0; x < sw; x++) { const p = px(img, sx + x, sy + y); for (let k = 0; k < 3; k++) mean[k] += p[k] / (sw * sh); }
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const i = y * w + x, o = px(img, x, y), g = px(img, sx + x % sw, sy + y % sh);
        // an der Kante eine schmale, noch dunklere Fuge: die Fläche liegt eingelassen im Stein
        const shade = 1 - 0.25 * Math.max(0, Math.min(1, (3 - dist[i]) / 2));
        const c = [0, 1, 2].map(k => o[k] + ((mean[k] + (g[k] - mean[k]) * 0.5) * shade - o[k]) * inner[i]);
        put(img, x, y, [...c, 255 * outer[i]]);
    }
    set('reward-frame', img);
    item('reward-frame').borders = REWARD_BORDERS;
}

// ------------------------------------------------------------------ Atlas

const layoutFile = path.join(EXACT, 'layout.json');
const layout = JSON.parse(fs.readFileSync(layoutFile, 'utf8'));
const item = name => { const e = layout.items.find(i => i.name === name); if (!e) throw new Error('fehlt: ' + name); return e; };

async function main() {
    const pages = [];
    for (let p = 0; p < layout.pages; p++) pages.push(await load(path.join(EXACT, 'atlas-' + p + '.png')));
    const get = name => { const e = item(name), pg = pages[e.page]; return crop(pg, e.x, pg.h - e.y - e.height, e.width, e.height); };
    const set = (name, img) => {
        const e = item(name), pg = pages[e.page];
        if (img.w !== e.width || img.h !== e.height) throw new Error(name + ': Größe ' + img.w + '×' + img.h);
        const top = pg.h - e.y - e.height;
        for (let y = 0; y < img.h; y++) img.d.copy(pg.d, ((top + y) * pg.w + e.x) * 4, y * img.w * 4, (y + 1) * img.w * 4);
    };

    // --- Tafelrahmen: bei jedem Lauf neu aus dem Probebild (unabhängig vom Zustand des Atlas)
    await rebuildPanels(get, set);
    await rebuildReward(set);
    await rebuildPlates(set);
    // Pokal: bläulicher Grund in den Henkeln und neben dem Fuß (goldene Pixel sind nie bläulich, darum wiederholbar)
    { const img = get('menu-icon-4'); clearBluish(img); set('menu-icon-4', img); }
    // alles Weitere nur einmal auf frisch ausgeschnittene Atlanten (Abdunkeln und Masken würden sich sonst verdoppeln)
    if (!layout.fixed) await fixOnce(get, set);

    for (let p = 0; p < pages.length; p++) {
        const pg = pages[p];
        await sharp(pg.d, { raw: { width: pg.w, height: pg.h, channels: 4 } }).png({ compressionLevel: 9, adaptiveFiltering: true }).toFile(path.join(EXACT, 'atlas-' + p + '.png'));
    }
    layout.fixed = true;
    fs.writeFileSync(layoutFile, JSON.stringify(layout, null, 2) + '\n');
    console.log('Atlas nachgebessert');
}

async function fixOnce(get, set) {
    const main = await load(path.join(SRC, MAIN));

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
}

main().catch(e => { console.error(e); process.exit(1); });
