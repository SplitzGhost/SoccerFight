// Legt die neuen Monsterbilder (Inspiration/Monsterpaket) als GANZE Körper ins Spiel:
// SoccerFight/Assets/Resources/Monsters/<stage>/<look>.png|.json|_rim.png. Aufruf:
// node monsters.js [look ...]   (ohne Angabe: jedes Monster jeder Stage aus Inspiration/Monsterpaket/sprites.json)
//
// Die Bilder sind fertige, geschlossene Körper in Ruhepose, keine getrennten Glieder. Ein früherer Versuch hat
// Arme, Flügel, Hörner und Augen herausgeschnitten und die Löcher im Rumpf aufgefüllt: im Spiel sahen die
// Monster dadurch zerrissen und verschmiert aus. Deshalb bleibt das Bild jetzt unangetastet – nur freigestellt,
// verkleinert und mit sauberen Kanten. Die Bewegung macht der ganze Körper im Spiel (Monster.PoseWhole).
//
// Verwendet wird die "rechts"-Ansicht; nach links dreht das Spiel die Figur per Spiegelung (Monster.cs).
'use strict';
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');
const { writeMeta, plainMeta } = require('./characters.js');
const { RIGS } = require('./monsters.rig.js');

// Inspiration/ ist gitignored: in einem anderen Arbeitsordner die Kopie im Hauptordner nehmen.
const SRC_LOCAL = path.join(__dirname, '../../Inspiration/Monsterpaket');
const SRC = fs.existsSync(SRC_LOCAL) ? SRC_LOCAL : 'C:/Users/Master/SoccerFighMaster/Inspiration/Monsterpaket';
const OUT = path.join(__dirname, '../../SoccerFight/Assets/Resources/Monsters');

const STAGE_KEY = {
    '01-mondlicht': 'mondlicht', '02-bernstein': 'bernstein', '03-regen': 'regen', '04-grotte': 'grotte',
    '05-glut': 'glut', '06-frost': 'frost', '07-stern': 'stern', '08-eklipse': 'eklipse',
};

// height: Höhe des sichtbaren Körpers in Welteinheiten (vor Rang-/Größenfaktor im Spiel).
// root: 'ground' = Drehpunkt mittig auf der Sohle (steht auf dem Boden), 'center' = Mitte (Flieger).
const LOOKS = {
    hopper: { height: 1.0, root: 'ground' },
    spawnling: { height: 0.95, root: 'ground' },
    splitter: { height: 1.0, root: 'ground' },
    spitter: { height: 1.0, root: 'ground' },
    brute: { height: 1.1, root: 'ground' },
    bomber: { height: 1.0, root: 'ground' },
    diver: { height: 0.75, root: 'center' },
    shade: { height: 1.0, root: 'center' },
    lantern: { height: 1.0, root: 'center' },
    king: { height: 1.1, root: 'ground' },
    thornmother: { height: 1.2, root: 'ground' },
    stormlantern: { height: 1.35, root: 'center' },
    crystalguard: { height: 1.15, root: 'ground' },
    magmacolossus: { height: 1.12, root: 'ground' },
    frostwyrm: { height: 1.05, root: 'center' },
    cometoracle: { height: 1.05, root: 'center' },
    voidlord: { height: 1.16, root: 'ground' },
};

// Bildgröße (Zweierpotenz, quadratisch): Unity streckt komprimierte Texturen sonst selbst auf Zweierpotenz-Maße
// (nPOTScale), und der Pixel-Ausschnitt im JSON zeigte dann nur noch die linke untere Ecke des Monsters.
// Bosse bekommen mehr Pixel, weil sie fast doppelt so groß im Bild stehen.
const BOSSES = ['king', 'thornmother', 'stormlantern', 'crystalguard', 'magmacolossus', 'frostwyrm', 'cometoracle', 'voidlord'];
const PAD = 6;

const SPRITES = JSON.parse(fs.readFileSync(path.join(SRC, 'sprites.json'), 'utf8')).sprites;

async function cut(entry) {
    const [stageFolder, file] = entry.datei.split('/');
    const look = file.replace('.png', '');
    const def = LOOKS[look];
    if (!def) throw new Error('Unbekanntes Monster: ' + look);
    const stageKey = STAGE_KEY[stageFolder];
    const view = entry.ansichten.find(a => a.richtung === 'rechts');
    // unity_rect: Ursprung unten links; alpha_kern_bounds: [x0,y0,x1,y1] oben links, lokal zur Hälfte
    const [vx, vyBottom, vw, vh] = view.unity_rect;
    const sheetH = entry.groesse[1];
    const vy = sheetH - vyBottom - vh;
    const [bx0, by0, bx1, by1] = view.alpha_kern_bounds_oben_links;

    // Ausschnitt: der ganze sichtbare Körper samt weichem Rand (Alpha < 128 liegt außerhalb der Kern-Box)
    const src = sharp(path.join(SRC, entry.datei)).extract({ left: vx, top: vy, width: vw, height: vh });
    const { data: half } = await src.clone().raw().toBuffer({ resolveWithObject: true });
    let x0 = vw, y0 = vh, x1 = -1, y1 = -1;
    for (let y = 0; y < vh; y++) for (let x = 0; x < vw; x++) if (half[(y * vw + x) * 4 + 3] > 3) {
        if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
    }
    x0 = Math.max(0, x0 - PAD); y0 = Math.max(0, y0 - PAD);
    x1 = Math.min(vw - 1, x1 + PAD); y1 = Math.min(vh - 1, y1 + PAD);
    const cw = x1 - x0 + 1, ch = y1 - y0 + 1;

    const W = BOSSES.includes(look) ? 1024 : 512, H = W;
    const scale = Math.min(1, W / Math.max(cw, ch));
    const { data } = await sharp(path.join(SRC, entry.datei))
        .extract({ left: vx + x0, top: vy + y0, width: cw, height: ch })
        .resize(Math.round(cw * scale), Math.round(ch * scale), { kernel: 'lanczos3' })
        .extend({ right: W - Math.round(cw * scale), bottom: H - Math.round(ch * scale), background: { r: 0, g: 0, b: 0, alpha: 0 } })
        .raw().toBuffer({ resolveWithObject: true });
    bleed(data, W, H);

    // Maßstab: die Kern-Box (Alpha >= 128) ist `height` Welteinheiten hoch
    const ppu = (by1 - by0) * scale / def.height;
    const soleY = view.unterkante_y_oben ?? by1;
    const rootX = ((bx0 + bx1) / 2 - x0) * scale;
    const rootY = def.root === 'ground' ? (soleY - y0) * scale : ((by0 + by1) / 2 - y0) * scale;

    const dir = path.join(OUT, stageKey);
    fs.mkdirSync(dir, { recursive: true });
    plainMeta(OUT, true);
    plainMeta(dir, true);
    const png = path.join(dir, look + '.png');
    await sharp(data, { raw: { width: W, height: H, channels: 4 } }).png({ compressionLevel: 9 }).toFile(png);
    // Web-Build: komprimiert (Crunch), sonst sprengen die Atlanten die 100-MB-Grenze von GitHub
    writeMeta(png, false, true);
    // Umriss-Maske fürs Mondlicht: die ganze Silhouette
    const rim = Buffer.alloc(W * H);
    for (let i = 0; i < W * H; i++) rim[i] = data[i * 4 + 3];
    const rimPng = path.join(dir, look + '_rim.png');
    await sharp(rim, { raw: { width: W, height: H, channels: 1 } }).png({ compressionLevel: 9 }).toFile(rimPng);
    writeMeta(rimPng, true);

    const json = { ppu: +ppu.toFixed(3),
        sprites: [{ name: 'Body', x: 0, y: 0, w: W, h: H, px: +rootX.toFixed(2), py: +(H - rootY).toFixed(2) }], anchors: [] };
    const rig = RIGS[stageKey + '/' + look];
    if (rig) json.rig = rigJson(rig, W, H, rootX, rootY, ppu);
    fs.writeFileSync(path.join(dir, look + '.json'), JSON.stringify(json, null, 1) + '\n');
    plainMeta(path.join(dir, look + '.json'), false);
    console.log(stageKey + '/' + look, W + 'x' + H, 'ppu', ppu.toFixed(1));
}

/** Knochen aus monsters.rig.js (Prozent des Bildes) in Körper-Einheiten umrechnen (x nach vorn, y nach oben, Drehpunkt 0). */
function rigJson(rig, W, H, rootX, rootY, ppu) {
    const r = v => +v.toFixed(4);
    const pt = ([u, v]) => [r((u / 100 * W - rootX) / ppu), r((rootY - v / 100 * H) / ppu)];
    const len = p => r(p / 100 * W / ppu);
    const flat = pts => pts.flatMap(pt);
    return {
        bend: rig.bend ?? 1,
        chains: (rig.chains || []).map(c => {
            const o = { ...c, pts: flat(c.pts), w: len(c.w) };
            o.poly = c.poly ? flat(c.poly) : [];
            return o;
        }),
        pulses: (rig.pulses || []).map(q => {
            const [x, y] = pt(q.c);
            return { x, y, r: len(q.r), amp: q.amp, freq: q.freq, phase: q.phase, charge: q.charge };
        }),
    };
}

/** Zieht die Randfarben nach außen in die durchsichtigen Pixel (glattes Alpha, keine dunklen Säume beim Filtern). */
function bleed(d, W, H) {
    const known = new Uint8Array(W * H);
    for (let i = 0; i < W * H; i++) known[i] = d[i * 4 + 3] > 8 ? 1 : 0;
    for (let pass = 0; pass < 16; pass++) {
        const add = [];
        for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
            const p = y * W + x;
            if (known[p]) continue;
            let n = 0, r = 0, g = 0, b = 0;
            for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
                const xx = x + dx, yy = y + dy;
                if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                const q = yy * W + xx;
                if (!known[q]) continue;
                n++; r += d[q * 4]; g += d[q * 4 + 1]; b += d[q * 4 + 2];
            }
            if (n) add.push(p, r / n, g / n, b / n);
        }
        if (!add.length) break;
        for (let i = 0; i < add.length; i += 4) {
            const p = add[i];
            d[p * 4] = add[i + 1]; d[p * 4 + 1] = add[i + 2]; d[p * 4 + 2] = add[i + 3];
            known[p] = 1;
        }
    }
}

if (require.main === module) (async () => {
    const only = process.argv.slice(2);
    for (const entry of SPRITES) {
        const look = entry.datei.split('/')[1].replace('.png', '');
        if (only.length && !only.includes(look)) continue;
        await cut(entry);
    }
})().catch(e => { console.error(e); process.exit(1); });
