// Schneidet die neuen Monsterbilder (Inspiration/Monsterpaket) in bewegliche Teile und legt pro Stage+Look
// einen Atlas + JSON nach SoccerFight/Assets/Resources/Monsters/<stage>/<look>.png|.json|_rim.png. Aufruf:
// node monsters.js [look ...]   (ohne Angabe: alle Looks aus monsters.def.js, für jede Stage, in der sie laut
// Inspiration/Monsterpaket/sprites.json vorkommen)
//
// Technik identisch zu characters.js (region/inpaint/orient/Umriss-Maske), aber die Schnittkoordinaten werden
// nur EINMAL pro Look an einem Referenzbild vermessen (monsters.def.js) und für jede andere Stage-Variante
// automatisch auf deren eigene, in sprites.json bereits gemessene Alpha-Bounding-Box umgerechnet — die Prompts
// hinter dem Bildersatz verlangen ausdrücklich dieselbe Pose/Proportion je Look ("the EXACT approved ... from
// reference"), nur Farbe/Merkmal wechseln pro Stage.
//
// Geschnitten wird immer nur die "rechts"-Ansicht; die Blickrichtung nach links entsteht im Spiel weiterhin
// durchs Spiegeln des Objekts (wie schon bei den alten prozeduralen Körpern, Monster.cs body.localScale.x) —
// ein an einer Stelle asymmetrisches Requisit (z. B. das Zepter des Königs) spiegelt dabei mit, genau wie es
// die prozeduralen Körper bisher auch taten.
'use strict';
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');
const { load, region, orient, inPoly, writeMeta, plainMeta } = require('./characters.js');

// Inspiration/ is gitignored, so a git worktree checkout (as opposed to the main SoccerFighMaster folder)
// never has it locally — fall back to the shared main folder's copy in that case.
const SRC_LOCAL = path.join(__dirname, '../../Inspiration/Monsterpaket');
const SRC = fs.existsSync(SRC_LOCAL) ? SRC_LOCAL : 'C:/Users/Master/SoccerFighMaster/Inspiration/Monsterpaket';
const OUT = path.join(__dirname, '../../SoccerFight/Assets/Resources/Monsters');
const DEBUG = process.env.SF_DEBUG;

const STAGE_KEY = {
    '01-mondlicht': 'mondlicht', '02-bernstein': 'bernstein', '03-regen': 'regen', '04-grotte': 'grotte',
    '05-glut': 'glut', '06-frost': 'frost', '07-stern': 'stern', '08-eklipse': 'eklipse',
};

// StageThemes.cs Eye colour per stage (the same hex the image prompts used) — the eye-colour flood fill
// must use the TARGET stage's own colour, not the ref sheet's, or it misses stages whose eye colour differs
// a lot from the ref (e.g. Frostgipfel's dark navy eye, Eklipse's rose eye).
const STAGE_EYE = {
    mondlicht: '#FFE98A', bernstein: '#FFF1A8', regen: '#E8F6FF', grotte: '#C8FFF6',
    glut: '#FFE36A', frost: '#1C3A5A', stern: '#FFF6B0', eklipse: '#FF8A9A',
};

const SPRITES = JSON.parse(fs.readFileSync(path.join(SRC, 'sprites.json'), 'utf8')).sprites;
const DEF = require('./monsters.def.js');

function entry(stageFolder, look) {
    return SPRITES.find(s => s.datei === `${stageFolder}/${look}.png`);
}

function rightView(e) {
    const v = e.ansichten.find(a => a.richtung === 'rechts');
    // sprites.json: alpha_kern_bounds_oben_links is [x0,y0,x1,y1] (two corners), local to the half — NOT x,y,w,h
    const [x0, y0, x1, y1] = v.alpha_kern_bounds_oben_links;
    const [vx] = v.unity_rect;
    // offset it into full-sheet pixel space (what load()/region() use)
    return { offsetX: vx, bounds: [x0 + vx, y0, x1 + vx, y1] };
}

/** Maps a point measured on the ref sheet onto another sheet's own (already offset) alpha bounds. */
function remap([x, y], ref, tgt) {
    const [rx0, ry0, rx1, ry1] = ref.bounds, [tx0, ty0, tx1, ty1] = tgt.bounds;
    const fx = (x + ref.offsetX - rx0) / (rx1 - rx0), fy = (y - ry0) / (ry1 - ry0);
    return [tx0 + fx * (tx1 - tx0), ty0 + fy * (ty1 - ty0)];
}
function remapPoly(poly, ref, tgt) { return poly.map(p => remap(p, ref, tgt)); }
function remapCircle([cx, cy, r], ref, tgt) {
    const [x, y] = remap([cx, cy], ref, tgt);
    const scale = (tgt.bounds[3] - tgt.bounds[1]) / (ref.bounds[3] - ref.bounds[1]);
    return [x, y, r * scale];
}
function circlePoly([cx, cy, r], n = 20) {
    const pts = [];
    for (let i = 0; i < n; i++) { const a = i / n * Math.PI * 2; pts.push([cx + Math.cos(a) * r, cy + Math.sin(a) * r]); }
    return pts;
}

/** Bright-blob eye finder: seeds near `seed`, flood-fills pixels close to `color`, returns a padded ellipse
 * (so the dark pupil inside a bright iris is included even though only the bright ring matched the colour). */
function findEye(img, seed, colorHex, radiusPad, search) {
    const hex = colorHex.replace('#', '');
    const target = [parseInt(hex.slice(0, 2), 16) / 255, parseInt(hex.slice(2, 4), 16) / 255, parseInt(hex.slice(4, 6), 16) / 255];
    const W = img.W, H = img.H, tol = 0.22;
    const [sx, sy] = seed.map(Math.round);
    let best = null, bestD = Infinity;
    for (let dy = -search; dy <= search; dy++) for (let dx = -search; dx <= search; dx++) {
        const x = sx + dx, y = sy + dy;
        if (x < 0 || y < 0 || x >= W || y >= H) continue;
        const p = y * W + x;
        if (img.a[p] < 0.5) continue;
        const d = Math.hypot(img.rgb[p * 3] - target[0], img.rgb[p * 3 + 1] - target[1], img.rgb[p * 3 + 2] - target[2]);
        if (d < bestD) { bestD = d; best = [x, y]; }
    }
    if (!best || bestD > tol * 2.5) return null;
    const visited = new Uint8Array(W * H);
    const stack = [best[1] * W + best[0]];
    visited[stack[0]] = 1;
    let x0 = best[0], y0 = best[1], x1 = best[0], y1 = best[1], n = 0;
    while (stack.length) {
        const p = stack.pop(), x = p % W, y = (p / W) | 0;
        const d = Math.hypot(img.rgb[p * 3] - target[0], img.rgb[p * 3 + 1] - target[1], img.rgb[p * 3 + 2] - target[2]);
        if (d > tol || img.a[p] < 0.4) continue;
        n++; x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y);
        for (const [ddx, ddy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
            const nx = x + ddx, ny = y + ddy;
            if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
            const np = ny * W + nx;
            if (visited[np]) continue;
            visited[np] = 1;
            if (Math.hypot(nx - best[0], ny - best[1]) < search * 1.6) stack.push(np);
        }
    }
    if (n < 4) return null;
    const cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, rx = (x1 - x0) / 2 * radiusPad, ry = (y1 - y0) / 2 * radiusPad;
    return { cx, cy, rx, ry };
}
function ellipsePoly(cx, cy, rx, ry, n = 20) {
    const pts = [];
    for (let i = 0; i < n; i++) { const a = i / n * Math.PI * 2; pts.push([cx + Math.cos(a) * rx, cy + Math.sin(a) * ry]); }
    return pts;
}

async function cutLook(look, stageFolders) {
    const base = DEF[look];
    if (!base) throw new Error('Schnittdefinition fehlt: ' + look);

    for (const stageFolder of stageFolders) {
        const e = entry(stageFolder, look);
        if (!e) continue;
        const stageKey = STAGE_KEY[stageFolder];
        // Varianten mit eigener Referenz werden vollständig in deren lokalen Bildkoordinaten vermessen.
        const D = { ...base, ...(base.variants && base.variants[stageKey]) };
        const refEntry = entry(D.ref, look);
        const refView = rightView(refEntry);
        const file = path.join(SRC, e.datei);
        const img = await load(file);
        const view = rightView(e);
        const [bx0, by0, bx1, by1] = view.bounds;
        const worldH = D.worldHeight || 1.0;
        const ppu = (by1 - by0) / worldH;
        // unterkante_y_oben is already a Y coordinate (top-origin, local to the half) — it needs no X offset.
        const soleY = e.ansichten.find(a => a.richtung === 'rechts').unterkante_y_oben;
        const rootPx = D.root === 'ground' && soleY != null ? [(bx0 + bx1) / 2, soleY] : [(bx0 + bx1) / 2, (by0 + by1) / 2];
        const toWorld = ([x, y]) => [(x - rootPx[0]) / ppu, -(y - rootPx[1]) / ppu];

        const scale = (view.bounds[3] - view.bounds[1]) / (refView.bounds[3] - refView.bounds[1]);
        const seamDisc = (refPoint, seamRadius) => {
            const [x, y] = remap(refPoint, refView, view);
            return circlePoly([x, y, (seamRadius ?? 20) * scale]);
        };
        // Shrinks a poly toward its pivot: hiding a slightly SMALLER copy of the part's own outline (not a
        // bounding circle — wrong shape for anything long and thin like an arm or tentacle) still leaves the
        // full-size rendered part covering it completely, edge and all, while keeping the inpainted area as
        // small as the shape allows — smaller holes blend far better than an oversized one.
        const shrinkPoly = (poly, pivot, factor) => poly.map(([x, y]) => [pivot[0] + (x - pivot[0]) * factor, pivot[1] + (y - pivot[1]) * factor]);

        const partPolys = {};
        for (const [name, p] of Object.entries(D.parts || {})) partPolys[name] = remapPoly(p.poly, refView, view);
        // The body poly is drawn tight (without the moving parts' own territory) precisely so hiding never
        // has to inpaint-fill a whole part-shaped hole — that only smooths colour, not silhouette, and on a
        // gradient-shaded surface (inpaint sources from a single dominant colour bin, built for flat cloth)
        // it leaves a visibly flat/dark patch instead of a plausible continuation of the surface. Prefer a
        // seam disc sized to the part's own reach, or (for D.hide's redundant "hide the whole part" entries)
        // a shrunk copy of the part's own outline — either way the part fully covers its hole when drawn back
        // on top, leaving only the true cut edge to blend. D.hide stays available at full size for spots that
        // genuinely have nothing else to reveal underneath (e.g. a tail's full sweep for a chain tile).
        const hidePolys = [];
        for (const p of Object.values(D.parts || {})) hidePolys.push(seamDisc(p.pivot, p.seam));
        // a D.hide entry keyed exactly like a D.parts entry is that helper's own "hide the whole part" poly
        // (ground/bosses/extra .def.js all build it that way) — shrink it toward the part's pivot instead of
        // hiding it full-size; a hide entry with no matching part (nothing else will cover it) stays full-size.
        for (const [key, poly] of Object.entries(D.hide || {})) {
            const part = D.parts && D.parts[key];
            hidePolys.push(remapPoly(part ? shrinkPoly(poly, part.pivot, 0.8) : poly, refView, view));
        }
        for (const c of Object.values(D.chains || {})) hidePolys.push(seamDisc(c.anchor, c.seam || 60));

        // each named eye either gets a per-stage manual override (an explicit ellipse, already in THIS
        // sheet's own pixel space — look at the actual stage image, not the ref, when writing one) or falls
        // back to colour-based auto-detection; either way it's cut out of Body and drawn back on top.
        const eyeInfos = {};
        for (const [eyeName, ed] of Object.entries(D.eyes || {})) {
            const override = ed.overrides && ed.overrides[stageKey];
            let info = null;
            if (override) {
                // override coords are measured by eye on the cropped right-half image (x from 0 at the
                // half's own left edge) — shift into this sheet's full-image space like everything else here.
                const [cx, cy, rx, ry] = override;
                info = { cx: cx + view.offsetX, cy, rx, ry };
            } else {
                const seed = remap(ed.seed, refView, view);
                // a search window scaled up with sheet size sometimes locks onto a closer-coloured but wrong
                // bright spot (a highlight, not the eye) before it ever reaches the true eye — a modest fixed
                // window that's still generous relative to how large an eye actually is works better here.
                info = findEye(img, seed, STAGE_EYE[stageKey] || ed.color, ed.radiusPad || 1.5, 55);
            }
            if (info) {
                eyeInfos[eyeName] = info;
                // Das ganze aufgemalte Auge entfernen: ein kleiner Mittelpunkt-Kreis ließe beim Blinzeln
                // den unbewegten Außenrand stehen. Nur wenige Pixel Reserve gegen doppelte Konturen.
                if (!ed.parent) hidePolys.push(ellipsePoly(info.cx, info.cy, info.rx, info.ry));
            } else throw new Error(look + ' ' + stageKey + ': Auge ' + eyeName + ' fehlt; manuellen Override ergänzen.');
        }

        const parts = {};
        const bodyPoly = remapPoly(D.body, refView, view);
        // wide colour tolerance: the body is a smoothly gradient-shaded dome/torso, not flat-coloured cloth,
        // so restricting inpaint sources to one dominant colour bin (characters.js's default, tuned for
        // trim-free cloth) flattens a hidden area to a single dark/light tone instead of continuing the
        // surrounding light-to-shadow gradient — let every known body pixel act as a source instead.
        parts.Body = orient(img, region(img, bodyPoly, hidePolys, [], null, null, 1.8), rootPx, null);

        for (const [name, poly] of Object.entries(partPolys)) {
            const pivot = remap(D.parts[name].pivot, refView, view);
            const hide = [];
            for (const [eyeName, ed] of Object.entries(D.eyes || {})) {
                const info = eyeInfos[eyeName];
                if (ed.parent === name && info) hide.push(ellipsePoly(info.cx, info.cy, info.rx, info.ry));
            }
            parts[cap(name)] = orient(img, region(img, poly, hide), pivot, null);
        }

        const anchors = {};
        for (const [name, p] of Object.entries(D.parts || {})) anchors[name] = toWorld(remap(p.pivot, refView, view));

        const chainSprites = {};
        for (const [name, c] of Object.entries(D.chains || {})) {
            const circ = remapCircle(c.circle, refView, view);
            const center = [circ[0], circ[1]];
            parts[cap(name)] = orient(img, region(img, circlePoly(circ)), center, null);
            anchors[name] = toWorld(remap(c.anchor, refView, view));
        }

        for (const [eyeName, info] of Object.entries(eyeInfos)) {
            // region() glättet fünf Pixel außerhalb der verdeckten Fläche. Der Augen-Ausschnitt
            // überdeckt diesen Rand vollständig, damit in Ruhe kein ovaler Auffüllsaum sichtbar bleibt.
            parts[cap(eyeName)] = orient(img, region(img, ellipsePoly(info.cx, info.cy, info.rx + 8, info.ry + 8)), [info.cx, info.cy], null);
            anchors[eyeName] = toWorld([info.cx, info.cy]);
        }

        await writeAtlas(stageKey, look, { ppu, parts, anchors });
        console.log(look, stageKey, 'ppu', ppu.toFixed(1), 'anchors', JSON.stringify(round(anchors)));
    }
}

function cap(s) { return s.charAt(0).toUpperCase() + s.slice(1); }
function round(o) {
    if (Array.isArray(o)) return o.map(round);
    if (typeof o === 'number') return +o.toFixed(4);
    const r = {}; for (const k in o) r[k] = round(o[k]); return r;
}

async function writeAtlas(stageKey, look, fig) {
    const names = Object.keys(fig.parts);
    const order = names.slice().sort((a, b) => fig.parts[b].H - fig.parts[a].H);
    const gap = 6, maxW = 1024;
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
    const out = Buffer.alloc(AW * AH * 4), rim = Buffer.alloc(AW * AH);
    const sprites = [];
    for (const n of names) {
        const p = fig.parts[n], [ox, oy] = place[n];
        for (let yy = 0; yy < p.H; yy++) for (let xx = 0; xx < p.W; xx++) {
            const i = (yy * p.W + xx) * 4, o = ((oy + yy) * AW + ox + xx) * 4;
            for (let k = 0; k < 4; k++) out[o + k] = Math.max(0, Math.min(255, Math.round(p.buf[i + k] * 255)));
            rim[(oy + yy) * AW + ox + xx] = Math.max(0, Math.min(255, Math.round(p.mask[yy * p.W + xx] * 255)));
        }
        sprites.push({ name: n, x: ox, y: AH - oy - p.H, w: p.W, h: p.H, px: p.px, py: p.H - p.py });
    }
    const dir = path.join(OUT, stageKey);
    fs.mkdirSync(dir, { recursive: true });
    plainMeta(path.join(OUT), true);
    plainMeta(dir, true);
    const file = path.join(dir, look + '.png');
    await sharp(out, { raw: { width: AW, height: AH, channels: 4 } }).png({ compressionLevel: 9 }).toFile(file);
    // Standalone/WebGL crunch (same fix as the stage graphics, commit b4678ff): 48 atlases across up to
    // 8 stages each, lossless, pushed the WebGL build past GitHub's 100 MB file limit.
    writeMeta(file, false, true);
    const rimFile = path.join(dir, look + '_rim.png');
    await sharp(rim, { raw: { width: AW, height: AH, channels: 1 } }).png({ compressionLevel: 9 }).toFile(rimFile);
    writeMeta(rimFile, true);
    // Ein zu kleiner Unity-Import würde die Pixel-Rechtecke im JSON ungültig machen.
    const importSize = Math.max(2048, 2 ** Math.ceil(Math.log2(Math.max(AW, AH))));
    for (const metaFile of [file + '.meta', rimFile + '.meta']) {
        const meta = fs.readFileSync(metaFile, 'utf8');
        fs.writeFileSync(metaFile, meta.replace(/maxTextureSize: \d+/g, 'maxTextureSize: ' + importSize));
    }
    const json = { ppu: +fig.ppu.toFixed(3), sprites, anchors: Object.entries(fig.anchors).map(([name, [x, y]]) => ({ name, x: +x.toFixed(4), y: +y.toFixed(4) })) };
    fs.writeFileSync(path.join(dir, look + '.json'), JSON.stringify(json, null, 1) + '\n');
    plainMeta(path.join(dir, look + '.json'), false);
    if (DEBUG) await sharp(out, { raw: { width: AW, height: AH, channels: 4 } }).png().toFile(path.join(DEBUG, stageKey + '_' + look + '_atlas.png'));
}

module.exports = { cutLook };

if (require.main === module) (async () => {
    const looks = process.argv.slice(2).length ? process.argv.slice(2) : Object.keys(DEF);
    const allFolders = Object.keys(STAGE_KEY);
    for (const look of looks) {
        const t = Date.now();
        await cutLook(look, allFolders);
        console.log(look, 'done in', (Date.now() - t) + ' ms');
    }
})().catch(e => { console.error(e); process.exit(1); });
