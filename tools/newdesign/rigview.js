// Kontrollbild für die Monster-Knochen (monsters.rig.js): zeichnet Ketten, Umrisse und Schwellungen mit
// 10-%-Raster auf die fertigen Monsterbilder. Aufruf:
//   node rigview.js <stage>/<look>[,<stage>/<look> …] <ziel.png> [kachelgröße]
// z. B. node rigview.js mondlicht/diver,regen/lantern ../../.build/rig.png
// Dicke Linie = Kette (großer Punkt = Wurzel), blasse Fläche = voll mitbewegter Bereich, Fläche mit Rand =
// Umriss (poly), gestrichelter Kreis = Schwellung. Ohne Knochen zeigt es nur Bild + Raster zum Ablesen.
'use strict';
const sharp = require('sharp');
const path = require('path');
const { RIGS } = require('./monsters.rig.js');

const R = path.join(__dirname, '../../SoccerFight/Assets/Resources/Monsters');
const COLORS = ['#ff4040', '#40ff40', '#40a0ff', '#ffff40', '#ff40ff', '#40ffff', '#ffa040', '#ffffff'];

(async () => {
    const items = process.argv[2].split(',');
    const dst = process.argv[3];
    const C = +(process.argv[4] || 520);
    const cols = Math.min(3, items.length);
    const comps = [];
    for (let i = 0; i < items.length; i++) {
        const img = await sharp(path.join(R, items[i] + '.png')).resize(C, C).png().toBuffer();
        const x = (i % cols) * C, y = Math.floor(i / cols) * (C + 24);
        comps.push({ input: img, left: x, top: y + 24 });
        const P = ([u, v]) => `${u * C / 100},${24 + v * C / 100}`;
        let g = '';
        for (let k = 1; k < 10; k++) {
            const p = k * C / 10;
            g += `<line x1="${p}" y1="24" x2="${p}" y2="${C + 24}" stroke="rgba(255,255,255,0.12)"/>`
                + `<line x1="0" y1="${24 + p}" x2="${C}" y2="${24 + p}" stroke="rgba(255,255,255,0.12)"/>`
                + `<text x="${p + 2}" y="36" font-size="10" fill="#aaa">${k * 10}</text><text x="2" y="${24 + p - 2}" font-size="10" fill="#aaa">${k * 10}</text>`;
        }
        const rig = RIGS[items[i]] || {};
        (rig.chains || []).forEach((c, ci) => {
            const col = COLORS[ci % COLORS.length];
            if (c.poly) g += `<polygon points="${c.poly.map(P).join(' ')}" fill="${col}" fill-opacity="0.18" stroke="${col}" stroke-width="1.5"/>`;
            else g += `<polyline points="${c.pts.map(P).join(' ')}" fill="none" stroke="${col}" stroke-opacity="0.25" stroke-width="${c.w * 2 * C / 100}" stroke-linecap="round" stroke-linejoin="round"/>`;
            g += `<polyline points="${c.pts.map(P).join(' ')}" fill="none" stroke="${col}" stroke-width="3"/>`;
            c.pts.forEach((p, k) => { const [a, b] = P(p).split(','); g += `<circle cx="${a}" cy="${b}" r="${k ? 3 : 6}" fill="${col}"/>`; });
        });
        (rig.pulses || []).forEach(q => {
            const [a, b] = P(q.c).split(',');
            g += `<circle cx="${a}" cy="${b}" r="${q.r * C / 100}" fill="none" stroke="#fff" stroke-dasharray="6 4" stroke-width="2"/>`;
        });
        comps.push({ input: Buffer.from(`<svg width="${C}" height="${C + 24}"><text x="4" y="18" font-size="16" fill="white">${items[i]}</text>${g}</svg>`), left: x, top: y });
    }
    const rows = Math.ceil(items.length / cols);
    await sharp({ create: { width: cols * C, height: rows * (C + 24), channels: 4, background: '#303848' } }).composite(comps).png().toFile(dst);
})().catch(e => { console.error(e); process.exit(1); });
