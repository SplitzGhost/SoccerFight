// Shop-Karten der Boxer. Die Shop-Vorlage (Inspiration/Shop UI) kennt nur die sechs Ballspieler; die Karten der Boxer
// werden im selben Format zusammengesetzt: Rahmen, Farblinie und Namensleiste von DREs fertiger Karte (der gemalte
// Name wird aus der Leiste gefüllt), darin ein Bild aus der freigestellten Figur der Spielerauswahl
// (Resources/Menu/Players/figure_<id>.png, von playerselect.box.js) vor einem Pinsel-Hintergrund in den Farben des
// Boxers, und der Name in derselben Art Schrift (weiß, kursiv, kräftige dunkle Kontur).
// Hängt die Boxer hinten an cards.json an (hohe Karten). Aufruf: node tools/newdesign/shop.box.js
'use strict';
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');
const { save, plainMeta } = require('./texio');

const ROOT = path.join(__dirname, '..', '..');
const OUT = path.join(ROOT, 'SoccerFight/Assets/Resources/Menu/Shop');
const PLAYERS = path.join(ROOT, 'SoccerFight/Assets/Resources/Menu/Players');
const W = 636, H = 1100, FRAME = 6, FOOT = 127;
const ART_W = W - 2 * FRAME, ART_H = H - FRAME - FOOT;

// Farben des Pinsel-Hintergrunds (dunkel, Grundton, Lichtstreifen) und der sichtbare Ausschnitt der Figur im Entwurf
// (Oberkante, Unterkante, Mitte in x – Bildpunkte des Auswahlentwurfs)
const BOXERS = {
    kai: { dark: '#3a1206', base: '#e0571c', light: '#ffb070', top: 160, bottom: 610, cx: 560 },
    vera: { dark: '#08143f', base: '#2450c8', light: '#8fb4ff', top: 150, bottom: 600, cx: 545 },
    luz: { dark: '#062818', base: '#1f8a55', light: '#9df0b8', top: 140, bottom: 600, cx: 560 },
};

async function rgb(file) {
    const { data, info } = await sharp(file).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    return { buf: data, w: info.width, h: info.height };
}

/** Pinsel-Hintergrund: Verlauf von unten dunkel nach oben hell, schräge Pinselstriche, weiches Licht hinter der Figur. */
function backdrop(c) {
    let strokes = '';
    let seed = c.base.length * 7919;
    const rnd = () => (seed = (seed * 16807) % 2147483647) / 2147483647;
    for (let i = 0; i < 26; i++) {
        const x = rnd() * ART_W * 1.4 - ART_W * 0.2, y = rnd() * ART_H, w = 40 + rnd() * 260, h = 6 + rnd() * 26;
        const col = rnd() < 0.55 ? c.light : c.dark, a = 0.08 + rnd() * 0.2;
        strokes += `<rect x="${x.toFixed(0)}" y="${y.toFixed(0)}" width="${w.toFixed(0)}" height="${h.toFixed(0)}" rx="${(h / 2).toFixed(0)}" fill="${col}" opacity="${a.toFixed(2)}" transform="rotate(-32 ${x.toFixed(0)} ${y.toFixed(0)})"/>`;
    }
    return Buffer.from(`<svg width="${ART_W}" height="${ART_H}" xmlns="http://www.w3.org/2000/svg">
<defs>
 <linearGradient id="g" x1="0" y1="1" x2="0.35" y2="0"><stop offset="0" stop-color="${c.dark}"/><stop offset="0.55" stop-color="${c.base}"/><stop offset="1" stop-color="${c.light}"/></linearGradient>
 <radialGradient id="r" cx="0.5" cy="0.38" r="0.55"><stop offset="0" stop-color="${c.light}" stop-opacity="0.55"/><stop offset="1" stop-color="${c.light}" stop-opacity="0"/></radialGradient>
 <filter id="b"><feGaussianBlur stdDeviation="3"/></filter>
</defs>
<rect width="100%" height="100%" fill="url(#g)"/>
<g filter="url(#b)">${strokes}</g>
<rect width="100%" height="100%" fill="url(#r)"/>
</svg>`);
}

/** Der Name wie auf den gemalten Karten: weiß, kursiv, fett, mit kräftiger dunkler Kontur. */
function nameSvg(name, w, h) {
    return Buffer.from(`<svg width="${w}" height="${h}" xmlns="http://www.w3.org/2000/svg">
<text x="34" y="${Math.round(h * 0.7)}" font-family="Arial Black, Arial, sans-serif" font-weight="900" font-style="italic" font-size="70"
 fill="#ffffff" stroke="#0b1d3c" stroke-width="10" paint-order="stroke" letter-spacing="2">${name}</text>
</svg>`);
}

async function run() {
    const layout = JSON.parse(fs.readFileSync(path.join(PLAYERS, 'layout.json'), 'utf8'));
    const tmpl = await rgb(path.join(OUT, 'dre.png'));
    if (tmpl.w != W || tmpl.h != H) throw new Error('dre.png hat nicht das Kartenformat');

    // Leiste ohne Namen: jede Zeile bekommt links die Farbe ihrer sauberen Mitte
    const bar = { buf: Buffer.from(tmpl.buf), w: W, h: H };
    const y0 = H - FOOT + 16, y1 = H - FRAME;
    for (let y = y0; y < y1; y++) {
        const m = [0, 0, 0];
        for (let x = 300; x < 360; x++) for (let k = 0; k < 3; k++) m[k] += bar.buf[(y * W + x) * 3 + k] / 60;
        for (let x = FRAME + 2; x < 300; x++) for (let k = 0; k < 3; k++) bar.buf[(y * W + x) * 3 + k] = Math.round(m[k]);
    }
    const frame = await sharp(bar.buf, { raw: { width: W, height: H, channels: 3 } }).png().toBuffer();

    for (const [id, c] of Object.entries(BOXERS)) {
        const f = layout.figures.find(e => e.id == id);
        if (!f) throw new Error('Figur ' + id + ' fehlt in layout.json (playerselect.box.js)');
        const fig = sharp(path.join(PLAYERS, 'figure_' + id + '.png'));
        const meta = await fig.metadata();
        const k = meta.width / f.w;   // Figurbild ist größer als im Entwurf (Real-ESRGAN)
        // Ausschnitt Kopf bis Oberschenkel, so skaliert, dass er die Bildhöhe füllt (oben etwas Luft)
        const s = (ART_H - 26) / ((c.bottom - c.top) * k);
        const fw = Math.round(meta.width * s), fh = Math.round(meta.height * s);
        const scaled = await fig.resize(fw, fh, { kernel: 'lanczos3' }).png().toBuffer();
        const left = Math.round(ART_W / 2 - (c.cx - f.x) * k * s), top = Math.round(26 - (c.top - f.y) * k * s);
        // auf die Bildfläche beschneiden
        const ex = Math.max(0, -left), ey = Math.max(0, -top);
        const ew = Math.min(fw - ex, ART_W - Math.max(0, left)), eh = Math.min(fh - ey, ART_H - Math.max(0, top));
        const piece = await sharp(scaled).extract({ left: ex, top: ey, width: ew, height: eh }).png().toBuffer();
        // weicher Schatten hinter der Figur
        const shadow = await sharp(piece).ensureAlpha().extractChannel(3).toColourspace('b-w')
            .linear(0.45, 0).blur(14).raw().toBuffer({ resolveWithObject: true });
        const shadowRGBA = await sharp({ create: { width: ew, height: eh, channels: 3, background: c.dark } })
            .joinChannel(shadow.data, { raw: { width: shadow.info.width, height: shadow.info.height, channels: 1 } }).png().toBuffer();
        const art = await sharp(backdrop(c)).composite([
            { input: shadowRGBA, left: Math.max(0, left) + 10, top: Math.max(0, top) + 8 },
            { input: piece, left: Math.max(0, left), top: Math.max(0, top) },
        ]).png().toBuffer();
        const card = await sharp(frame).composite([
            { input: art, left: FRAME, top: FRAME },
            { input: nameSvg(id.toUpperCase(), 360, FOOT - 14), left: 0, top: H - FOOT + 10 },
        ]).removeAlpha().raw().toBuffer();
        await save(path.join(OUT, id + '.png'), { buf: card, w: W, h: H }, { channels: 3, crunch: true });
        if (process.env.SF_DEBUG) await sharp(card, { raw: { width: W, height: H, channels: 3 } }).png().toFile(path.join(process.env.SF_DEBUG, 'shop_' + id + '.png'));
        console.log('  Karte', id);
    }

    const json = path.join(OUT, 'cards.json');
    const list = JSON.parse(fs.readFileSync(json, 'utf8'));
    list.cards = list.cards.filter(e => !BOXERS[e.id]);
    for (const id of Object.keys(BOXERS)) list.cards.push({ id, small: false });
    fs.writeFileSync(json, JSON.stringify(list, null, 1) + '\n');
    plainMeta(json, false);
}

module.exports = { run };
if (require.main === module) run().catch(e => { console.error(e); process.exit(1); });
