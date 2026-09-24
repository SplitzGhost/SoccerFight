// KI-Hochskalierung mit Real-ESRGAN (Modell „x4plus-anime“: klare Kanten, flächige Malweise – passt zum
// Stil der Vorlagen). Das Programm liegt in tools/newdesign/esrgan (nicht im Repo, einmal von
// github.com/xinntao/Real-ESRGAN/releases → realesrgan-ncnn-vulkan-…-windows.zip dorthin entpacken) und
// rechnet auf der Grafikkarte. Ergebnisse werden unter .cache/ai zwischengespeichert (Schlüssel: Bildinhalt).
// Fehlt das Programm, wird ersatzweise normal (Lanczos) vergrößert – deutlich weicher.
'use strict';
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { spawnSync } = require('child_process');

const DIR = path.join(__dirname, 'esrgan');
const EXE = path.join(DIR, 'realesrgan-ncnn-vulkan.exe');
const CACHE = path.join(__dirname, '.cache', 'ai');
const MODEL = 'realesrgan-x4plus-anime';

/** Windows-Treiber registrieren Vulkan nicht immer: den NVIDIA-Treiber direkt angeben (nur für diesen Prozess). */
function vulkanEnv() {
    const env = { ...process.env };
    if (env.VK_ICD_FILENAMES) return env;
    const repo = 'C:/Windows/System32/DriverStore/FileRepository';
    try {
        for (const d of fs.readdirSync(repo)) {
            if (!d.startsWith('nv_dispi.inf')) continue;
            const f = path.join(repo, d, 'nv-vk64.json');
            if (fs.existsSync(f)) { env.VK_ICD_FILENAMES = f; break; }
        }
    } catch { }
    return env;
}

let warned = false;

/** jobs: [{ buf: RGB (w*h*3), w, h }] → [{ buf: RGB, w: 4w, h: 4h }] */
async function x4(jobs) {
    fs.mkdirSync(CACHE, { recursive: true });
    const keys = jobs.map(j => crypto.createHash('sha1').update(`${MODEL}:${j.w}x${j.h}:`).update(j.buf).digest('hex'));
    const todo = jobs.map((j, i) => i).filter(i => !fs.existsSync(path.join(CACHE, keys[i] + '.png')));
    if (todo.length) {
        if (!fs.existsSync(EXE)) {
            if (!warned) { console.warn('  ! Real-ESRGAN fehlt (tools/newdesign/esrgan): normal vergrößert'); warned = true; }
            for (const i of todo) {
                const j = jobs[i];
                await sharp(j.buf, { raw: { width: j.w, height: j.h, channels: 3 } }).resize(j.w * 4, j.h * 4, { kernel: 'lanczos3' }).png().toFile(path.join(CACHE, keys[i] + '.png'));
            }
        } else {
            const tmp = path.join(CACHE, 'tmp');
            const inDir = path.join(tmp, 'in'), outDir = path.join(tmp, 'out');
            fs.rmSync(tmp, { recursive: true, force: true });
            fs.mkdirSync(inDir, { recursive: true }); fs.mkdirSync(outDir, { recursive: true });
            for (const i of todo) {
                const j = jobs[i];
                await sharp(j.buf, { raw: { width: j.w, height: j.h, channels: 3 } }).png().toFile(path.join(inDir, keys[i] + '.png'));
            }
            const r = spawnSync(EXE, ['-i', inDir, '-o', outDir, '-n', MODEL, '-f', 'png', '-m', path.join(DIR, 'models')], { env: vulkanEnv(), encoding: 'utf8' });
            for (const i of todo) {
                const f = path.join(outDir, keys[i] + '.png');
                if (!fs.existsSync(f)) throw new Error('Real-ESRGAN lieferte kein Bild: ' + (r.stderr || '').split('\n').slice(-4).join(' '));
                fs.renameSync(f, path.join(CACHE, keys[i] + '.png'));
            }
            fs.rmSync(tmp, { recursive: true, force: true });
        }
    }
    const out = [];
    for (let i = 0; i < jobs.length; i++) {
        const { data, info } = await sharp(path.join(CACHE, keys[i] + '.png')).removeAlpha().raw().toBuffer({ resolveWithObject: true });
        out.push({ buf: data, w: info.width, h: info.height });
    }
    return out;
}

/** Colours of opaque pixels spread into the transparent surroundings (no dark fringe when upscaling). */
function bleed(img) {
    const { w, h } = img, N = w * h;
    const rgb = new Float32Array(N * 3), known = new Uint8Array(N);
    const mean = [0, 0, 0]; let n = 0;
    for (let p = 0; p < N; p++) {
        if (img.buf[p * 4 + 3] >= 96) {
            known[p] = 1;
            for (let c = 0; c < 3; c++) { rgb[p * 3 + c] = img.buf[p * 4 + c]; mean[c] += img.buf[p * 4 + c]; }
            n++;
        }
    }
    for (let c = 0; c < 3; c++) mean[c] /= Math.max(1, n);
    let front = [];
    for (let p = 0; p < N; p++) if (!known[p]) front.push(p);
    for (let pass = 0; pass < 24 && front.length; pass++) {
        const set = [];
        for (const p of front) {
            const x = p % w, y = (p / w) | 0; let s0 = 0, s1 = 0, s2 = 0, k = 0;
            for (const q of [x > 0 ? p - 1 : -1, x < w - 1 ? p + 1 : -1, y > 0 ? p - w : -1, y < h - 1 ? p + w : -1])
                if (q >= 0 && known[q]) { s0 += rgb[q * 3]; s1 += rgb[q * 3 + 1]; s2 += rgb[q * 3 + 2]; k++; }
            if (k) set.push([p, s0 / k, s1 / k, s2 / k]);
        }
        for (const [p, a, b, c] of set) { rgb[p * 3] = a; rgb[p * 3 + 1] = b; rgb[p * 3 + 2] = c; known[p] = 1; }
        front = front.filter(p => !known[p]);
    }
    for (const p of front) for (let c = 0; c < 3; c++) rgb[p * 3 + c] = mean[c];
    const buf = Buffer.alloc(N * 3);
    for (let i = 0; i < N * 3; i++) buf[i] = Math.round(rgb[i]);
    return buf;
}

async function resizeRGB(r, w, h) {
    return sharp(r.buf, { raw: { width: r.w, height: r.h, channels: 3 } }).resize(w, h, { kernel: 'lanczos3', fit: 'fill' }).raw().toBuffer();
}

/**
 * Upscale RGBA pictures (straight alpha) by `factor` (≤ 4): colour and transparency each through the
 * AI (the mask gets the same crisp edges). imgs: [{ buf, w, h }] → same order, sizes w*factor.
 * wrap: 'x' / 'y' — the picture tiles in that direction (edges are computed with the other side).
 */
async function upscaleRGBA(imgs, factor, wrap) {
    const PAD = 12;
    const jobs = [];
    const padded = imgs.map(img => {
        let src = img;
        if (wrap) {
            // wrap-around border so the tile edges get the same treatment as its middle
            const w = img.w + (wrap == 'x' ? 2 * PAD : 0), h = img.h + (wrap == 'y' ? 2 * PAD : 0);
            const buf = Buffer.alloc(w * h * 4);
            for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
                const sx = wrap == 'x' ? ((x - PAD) % img.w + img.w) % img.w : x;
                const sy = wrap == 'y' ? ((y - PAD) % img.h + img.h) % img.h : y;
                img.buf.copy(buf, (y * w + x) * 4, (sy * img.w + sx) * 4, (sy * img.w + sx) * 4 + 4);
            }
            src = { buf, w, h };
        }
        jobs.push({ buf: bleed(src), w: src.w, h: src.h });
        const a = Buffer.alloc(src.w * src.h * 3);
        for (let p = 0; p < src.w * src.h; p++) a[p * 3] = a[p * 3 + 1] = a[p * 3 + 2] = src.buf[p * 4 + 3];
        jobs.push({ buf: a, w: src.w, h: src.h });
        return src;
    });
    const res = await x4(jobs);
    const out = [];
    for (let i = 0; i < imgs.length; i++) {
        const src = padded[i], img = imgs[i];
        const W = Math.round(src.w * factor), H = Math.round(src.h * factor);
        const rgb = await resizeRGB(res[i * 2], W, H), al = await resizeRGB(res[i * 2 + 1], W, H);
        const ow = Math.round(img.w * factor), oh = Math.round(img.h * factor);
        const ox = wrap == 'x' ? Math.round(PAD * factor) : 0, oy = wrap == 'y' ? Math.round(PAD * factor) : 0;
        const buf = Buffer.alloc(ow * oh * 4);
        for (let y = 0; y < oh; y++) for (let x = 0; x < ow; x++) {
            const s = ((y + oy) * W + x + ox), o = (y * ow + x) * 4;
            let a = al[s * 3];
            if (a < 6) a = 0; else if (a > 249) a = 255;
            buf[o] = rgb[s * 3]; buf[o + 1] = rgb[s * 3 + 1]; buf[o + 2] = rgb[s * 3 + 2]; buf[o + 3] = a;
        }
        out.push({ buf, w: ow, h: oh });
    }
    return out;
}

/** Upscale an opaque RGB picture by `factor` to exactly W×H. */
async function upscaleRGB(img, W, H) {
    const [r] = await x4([img]);
    return resizeRGB(r, W, H);
}

module.exports = { upscaleRGBA, upscaleRGB };
