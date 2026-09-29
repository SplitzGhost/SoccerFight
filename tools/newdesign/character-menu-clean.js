// Charakter-Detailbilder (Resources/Menu/CharacterDetails): entfernt den gemalten Zurück-Knopf (oben links) und die
// gemalten Währungsleisten (oben rechts). Auf der Detailseite liegt die normale Menüleiste darüber (Logo, Reiter,
// Kristalle, Münzen), also dürfen diese Stellen nur noch Himmel zeigen. Freigestellt wird nach Form (alles, was sich
// vom Himmel abhebt, etwas erweitert), aufgefüllt inhaltsbasiert (inpaint.js) aus der Umgebung, mit weichem Rand.
// Läuft automatisch am Ende von tools/extract-character-menu.py; die Videoloops überdeckt CharacterDetailPage an
// denselben Stellen mit Ausschnitten dieses bereinigten Standbilds.
'use strict';
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const { inpaint } = require('./inpaint');

const DIR = path.join(__dirname, '../../SoccerFight/Assets/Resources/Menu/CharacterDetails');
const IDS = ['rio', 'bruno', 'mira', 'dre', 'titan', 'nova'];
const FEATHER = 3;   // die Füllung greift so weit über die Maske hinaus
// box: wo das Element liegt; crop: woraus gefüllt wird; grow: Erweiterung der Maske (Schatten liegt unten);
// isPart(r, g, b): gehört der Pixel zum Element statt zum Himmel? (alles in Bildpixeln des 1672 × 941 großen Bildes)
const AREAS = [
    {   // Zurück-Knopf: helle Steinplatte auf dunkelblauem Nachthimmel
        box: [12, 8, 150, 142], crop: [0, 0, 400, 300], grow: [5, 5, 5, 11],
        isPart: (r, g, b) => 0.3 * r + 0.59 * g + 0.11 * b > 118 || r > 95,
    },
    {   // Kristall- und Münzleiste auf hellem Wolkenhimmel
        box: [1358, 4, 1660, 78], crop: [1150, 0, 1672, 220], grow: [9, 8, 9, 10],
        isPart: (r, g, b) => 0.3 * r + 0.59 * g + 0.11 * b < 212 || Math.abs(r - b) > 22,
    },
];

async function clean(id) {
    const file = path.join(DIR, id + '.png');
    const { data, info } = await sharp(file).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    const W = info.width, H = info.height;
    if (W != 1672 || H != 941) throw new Error(id + ': unerwartete Größe ' + W + '×' + H);
    for (const a of AREAS) {
        const [cx0, cy0, cx1, cy1] = a.crop, w = cx1 - cx0, h = cy1 - cy0;
        const img = new Float32Array(w * h * 3), part = new Uint8Array(w * h);
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
            const X = cx0 + x, Y = cy0 + y, i = (Y * W + X) * 3;
            for (let c = 0; c < 3; c++) img[(y * w + x) * 3 + c] = data[i + c];
            const inBox = X >= a.box[0] && X < a.box[2] && Y >= a.box[1] && Y < a.box[3];
            part[y * w + x] = inBox && a.isPart(data[i], data[i + 1], data[i + 2]) ? 1 : 0;
        }
        // Maske erweitern (links, oben, rechts, unten), Löcher im Inneren schließen
        const [gl, gt, gr, gb] = a.grow, hole = new Uint8Array(w * h);
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
            if (!part[y * w + x]) continue;
            for (let dy = -gt; dy <= gb; dy++) for (let dx = -gl; dx <= gr; dx++) {
                const xx = x + dx, yy = y + dy;
                if (xx >= 0 && xx < w && yy >= 0 && yy < h) hole[yy * w + xx] = 1;
            }
        }
        for (let y = 0; y < h; y++) {
            let first = -1, last = -1;
            for (let x = 0; x < w; x++) if (hole[y * w + x]) { if (first < 0) first = x; last = x; }
            for (let x = first; x >= 0 && x <= last; x++) hole[y * w + x] = 1;
        }
        // Abstand zur Maske: der Rand wird weich ins Original geblendet
        const dist = new Float32Array(w * h).fill(1e9);
        for (let i = 0; i < w * h; i++) if (hole[i]) dist[i] = 0;
        for (let pass = 0; pass < 2; pass++) for (let k = 0; k < w * h; k++) {
            const i = pass ? w * h - 1 - k : k, x = i % w, y = (i / w) | 0, st = pass ? 1 : -1;
            if (x + st >= 0 && x + st < w) dist[i] = Math.min(dist[i], dist[i + st] + 1);
            if (y + st >= 0 && y + st < h) dist[i] = Math.min(dist[i], dist[i + st * w] + 1);
        }
        const fill = new Uint8Array(w * h);
        for (let i = 0; i < w * h; i++) fill[i] = dist[i] <= FEATHER ? 1 : 0;
        const res = inpaint(img, w, h, fill, null, { yWeight: 3, seed: 7 });
        // Poisson-Angleichung: die Struktur der Füllung bleibt, ihre Helligkeit schließt nahtlos an den Rand an
        // (sonst hebt sich die Füllung als hellerer oder dunklerer Fleck ab)
        const f = res.slice(), cells = [];
        for (let i = 0; i < w * h; i++) {
            const x = i % w, y = (i / w) | 0;
            if (fill[i] && x > 0 && y > 0 && x < w - 1 && y < h - 1) cells.push(i);
        }
        for (let it = 0; it < 800; it++) for (const i of cells) for (let c = 0; c < 3; c++) {
            let s = 0;
            for (const n of [i - 1, i + 1, i - w, i + w])
                s += fill[n] ? f[n * 3 + c] + res[i * 3 + c] - res[n * 3 + c] : img[n * 3 + c];   // am Rand kein Sprung
            f[i * 3 + c] += 1.9 * (s / 4 - f[i * 3 + c]);
        }
        for (const i of cells) {
            const o = ((cy0 + ((i / w) | 0)) * W + cx0 + i % w) * 3;
            for (let c = 0; c < 3; c++) data[o + c] = Math.max(0, Math.min(255, Math.round(f[i * 3 + c])));
        }
    }
    await sharp(data, { raw: { width: W, height: H, channels: 3 } }).png().toFile(file + '.tmp');
    fs.renameSync(file + '.tmp', file);
    console.log(id);
}

(async () => { for (const id of process.argv.slice(2).length ? process.argv.slice(2) : IDS) await clean(id); })()
    .catch(e => { console.error(e); process.exit(1); });
