// Gemeinsame Bild-Helfer für build.js und stages.js: skalieren, vormultiplizieren, speichern,
// Unity-Metadaten schreiben.
'use strict';
const sharp = require('sharp');
const fs = require('fs');
const crypto = require('crypto');

const up4 = v => Math.ceil(v / 4) * 4;

/**
 * Scales an RGBA image (straight alpha), pads it to a multiple of 4 (2 px free rim, bottom-aligned
 * unless opts.center) and premultiplies. Returns { buf, w, h, ox, oy, iw, ih } — ox/oy: where the
 * scaled image sits in the padded one.
 */
async function finish(img, scale, sharpen, opts = {}) {
    const w = Math.max(1, Math.round(img.w * scale)), h = Math.max(1, Math.round(img.h * scale));
    let p = sharp(img.buf, { raw: { width: img.w, height: img.h, channels: 4 } });
    if (scale != 1) p = p.resize(w, h, { kernel: 'lanczos3' });
    if (sharpen) p = p.sharpen({ sigma: 0.7, m1: 0.6, m2: 1.2 });
    const scaled = await p.raw().toBuffer();
    const pad = opts.pad ?? 2, W = up4(w + pad * 2), H = up4(h + pad * 2);
    const out = Buffer.alloc(W * H * 4);
    const ox = Math.floor((W - w) / 2), oy = opts.top ? pad : H - h - pad;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const i = (y * w + x) * 4, o = ((y + oy) * W + x + ox) * 4, a = scaled[i + 3];
        out[o] = Math.round(scaled[i] * a / 255); out[o + 1] = Math.round(scaled[i + 1] * a / 255);
        out[o + 2] = Math.round(scaled[i + 2] * a / 255); out[o + 3] = a;
    }
    return { buf: out, w: W, h: H, ox, oy, iw: w, ih: h };
}

/** Saves raw pixels as PNG (channels 3 or 4) and writes the Unity import settings. */
async function save(file, f, opts = {}) {
    const ch = opts.channels || 4;
    await sharp(f.buf, { raw: { width: f.w, height: f.h, channels: ch } }).png({ compressionLevel: 9 }).toFile(file);
    writeMeta(file, opts.compression ?? 1, opts.wrap ?? 1, opts.mips ?? 1, opts.crunch ? 1 : 0);
}

/** Unity-Importeinstellungen: vormultipliziert (kein Alpha-Auffüllen), Mipmaps, Clamp, bis 4096 px;
 *  crunch: Crunch-Kompression (kleiner Download im Browser, gleicher Grafikspeicher). */
function writeMeta(file, compression, wrap = 1, mips = 1, crunch = 0) {
    const meta = file + '.meta';
    let guid = crypto.randomBytes(16).toString('hex');
    if (fs.existsSync(meta)) { const m = /guid: ([0-9a-f]{32})/.exec(fs.readFileSync(meta, 'utf8')); if (m) guid = m[1]; }
    const platform = t => `  - serializedVersion: 4
    buildTarget: ${t}
    maxTextureSize: 4096
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: ${compression}
    compressionQuality: ${crunch ? 75 : 100}
    crunchedCompression: ${crunch}
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
    enableMipMap: ${mips}
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
  maxTextureSize: 4096
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: ${wrap}
    wrapV: ${wrap}
    wrapW: ${wrap}
  nPOTScale: 0
  lightmap: 0
  compressionQuality: ${crunch ? 75 : 100}
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

/** Ordner- und Text-Metadaten (einmal anlegen, GUID bleibt). */
function plainMeta(file, folder) {
    const meta = file + '.meta';
    if (fs.existsSync(meta)) return;
    const guid = crypto.randomBytes(16).toString('hex');
    fs.writeFileSync(meta, folder
        ? `fileFormatVersion: 2\nguid: ${guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`
        : `fileFormatVersion: 2\nguid: ${guid}\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
}

module.exports = { up4, finish, save, writeMeta, plainMeta };
