using UnityEngine;

namespace SoccerFight
{
    /// <summary>
    /// Tiny vector rasterizer: composites SDF shapes with analytic anti-aliasing into a texture.
    /// Coordinates are world units; the canvas maps them to pixels with a fixed pixels-per-unit.
    /// Output textures are premultiplied alpha so filtering and mipmaps never produce dark fringes.
    /// </summary>
    public sealed class SdfCanvas
    {
        public delegate float SdfFn(Vector2 p);
        public delegate Color ColorFn(Vector2 p);

        public readonly int Width;
        public readonly int Height;
        public readonly float Ppu;
        readonly Vector2 origin;
        readonly Color[] px;

        public Rect UnitRect => new Rect(origin, new Vector2(Width / Ppu, Height / Ppu));

        public SdfCanvas(Rect units, float ppu)
        {
            Ppu = ppu;
            Width = Mathf.Max(4, Mathf.CeilToInt(units.width * ppu));
            Height = Mathf.Max(4, Mathf.CeilToInt(units.height * ppu));
            origin = units.min;
            px = new Color[Width * Height];
        }

        public Vector2 ToUnits(int x, int y) => origin + new Vector2((x + 0.5f) / Ppu, (y + 0.5f) / Ppu);

        void PixelRange(Rect? bounds, out int x0, out int y0, out int x1, out int y1)
        {
            if (bounds.HasValue)
            {
                Rect b = bounds.Value;
                x0 = Mathf.Clamp(Mathf.FloorToInt((b.xMin - origin.x) * Ppu) - 2, 0, Width);
                x1 = Mathf.Clamp(Mathf.CeilToInt((b.xMax - origin.x) * Ppu) + 2, 0, Width);
                y0 = Mathf.Clamp(Mathf.FloorToInt((b.yMin - origin.y) * Ppu) - 2, 0, Height);
                y1 = Mathf.Clamp(Mathf.CeilToInt((b.yMax - origin.y) * Ppu) + 2, 0, Height);
            }
            else
            {
                x0 = 0; y0 = 0; x1 = Width; y1 = Height;
            }
        }

        float Coverage(float d, float softness)
        {
            float aa = 1f / Ppu + softness;
            return Mathf.Clamp01(0.5f - d / aa);
        }

        static void Over(ref Color dst, Color src, float cov)
        {
            float sa = src.a * cov;
            if (sa <= 0f) return;
            float da = dst.a;
            float oa = sa + da * (1f - sa);
            if (oa <= 1e-6f) { dst = new Color(0, 0, 0, 0); return; }
            float inv = 1f / oa;
            dst.r = (src.r * sa + dst.r * da * (1f - sa)) * inv;
            dst.g = (src.g * sa + dst.g * da * (1f - sa)) * inv;
            dst.b = (src.b * sa + dst.b * da * (1f - sa)) * inv;
            dst.a = oa;
        }

        /// <summary>
        /// Runs a row kernel over [y0, y1). Large areas are split across CPU cores — every row is owned
        /// by exactly one thread and all shape functions are pure math, so this is race-free.
        /// </summary>
        static void ForRows(int x0, int x1, int y0, int y1, System.Action<int> row)
        {
            if (y1 <= y0 || x1 <= x0) return;
            if ((long)(x1 - x0) * (y1 - y0) > 12000) Par.For(y0, y1, row);
            else for (int y = y0; y < y1; y++) row(y);
        }

        /// <summary>Composite a filled shape over the canvas.</summary>
        public void Fill(SdfFn sdf, Color color, float softness = 0f, Rect? bounds = null)
        {
            PixelRange(bounds, out int x0, out int y0, out int x1, out int y1);
            ForRows(x0, x1, y0, y1, y =>
            {
                for (int x = x0; x < x1; x++)
                {
                    float cov = Coverage(sdf(ToUnits(x, y)), softness);
                    if (cov > 0f) Over(ref px[y * Width + x], color, cov);
                }
            });
        }

        public void Fill(SdfFn sdf, ColorFn color, float softness = 0f, Rect? bounds = null)
        {
            PixelRange(bounds, out int x0, out int y0, out int x1, out int y1);
            ForRows(x0, x1, y0, y1, y =>
            {
                for (int x = x0; x < x1; x++)
                {
                    Vector2 p = ToUnits(x, y);
                    float cov = Coverage(sdf(p), softness);
                    if (cov > 0f) Over(ref px[y * Width + x], color(p), cov);
                }
            });
        }

        /// <summary>Cut a shape out of what is already there (amount 1 = fully transparent).</summary>
        public void Erase(SdfFn sdf, float amount = 1f, float softness = 0f, Rect? bounds = null)
        {
            PixelRange(bounds, out int x0, out int y0, out int x1, out int y1);
            ForRows(x0, x1, y0, y1, y =>
            {
                for (int x = x0; x < x1; x++)
                {
                    int i = y * Width + x;
                    if (px[i].a <= 0f) continue;
                    float cov = Coverage(sdf(ToUnits(x, y)), softness) * amount;
                    if (cov > 0f) px[i].a *= 1f - cov;
                }
            });
        }

        /// <summary>Paint inside existing pixels only (like a clipping mask). Alpha is preserved.</summary>
        public void Paint(SdfFn region, Color color, float softness = 0f, Rect? bounds = null)
        {
            PixelRange(bounds, out int x0, out int y0, out int x1, out int y1);
            ForRows(x0, x1, y0, y1, y =>
            {
                for (int x = x0; x < x1; x++)
                {
                    int i = y * Width + x;
                    if (px[i].a <= 0f) continue;
                    float cov = Coverage(region(ToUnits(x, y)), softness) * color.a;
                    if (cov <= 0f) continue;
                    Color c = px[i];
                    c.r = Mathf.Lerp(c.r, color.r, cov);
                    c.g = Mathf.Lerp(c.g, color.g, cov);
                    c.b = Mathf.Lerp(c.b, color.b, cov);
                    px[i] = c;
                }
            });
        }

        /// <summary>Per-pixel recolor inside existing pixels: color fn returns rgb + blend amount in alpha.</summary>
        public void Paint(ColorFn fn, Rect? bounds = null)
        {
            PixelRange(bounds, out int x0, out int y0, out int x1, out int y1);
            ForRows(x0, x1, y0, y1, y =>
            {
                for (int x = x0; x < x1; x++)
                {
                    int i = y * Width + x;
                    if (px[i].a <= 0f) continue;
                    Color src = fn(ToUnits(x, y));
                    if (src.a <= 0f) continue;
                    Color c = px[i];
                    c.r = Mathf.Lerp(c.r, src.r, src.a);
                    c.g = Mathf.Lerp(c.g, src.g, src.a);
                    c.b = Mathf.Lerp(c.b, src.b, src.a);
                    px[i] = c;
                }
            });
        }

        /// <summary>Multiply existing rgb by a factor field (shading).</summary>
        public void Shade(System.Func<Vector2, float> factor, Rect? bounds = null)
        {
            PixelRange(bounds, out int x0, out int y0, out int x1, out int y1);
            ForRows(x0, x1, y0, y1, y =>
            {
                for (int x = x0; x < x1; x++)
                {
                    int i = y * Width + x;
                    if (px[i].a <= 0f) continue;
                    float f = factor(ToUnits(x, y));
                    Color c = px[i];
                    c.r *= f; c.g *= f; c.b *= f;
                    px[i] = c;
                }
            });
        }

        /// <summary>
        /// Light the silhouette edge that faces the given direction (e.g. towards the moon): pixels whose
        /// neighbour in that direction is more transparent get blended towards the rim color.
        /// Only rgb is written and only alpha is read, so rows can safely run in parallel.
        /// </summary>
        public void RimLight(Vector2 offsetUnits, Color color, float strength)
        {
            int dx = Mathf.RoundToInt(offsetUnits.x * Ppu), dy = Mathf.RoundToInt(offsetUnits.y * Ppu);
            ForRows(0, Width, 0, Height, y =>
            {
                int ny = y + dy;
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    float a = px[i].a;
                    if (a <= 0.02f) continue;
                    int nx = x + dx;
                    float an = (nx < 0 || ny < 0 || nx >= Width || ny >= Height) ? 0f : px[ny * Width + nx].a;
                    float f = Mathf.Clamp01(a - an) * strength * color.a;
                    if (f <= 0f) continue;
                    Color c = px[i];
                    c.r = Mathf.Lerp(c.r, color.r, f);
                    c.g = Mathf.Lerp(c.g, color.g, f);
                    c.b = Mathf.Lerp(c.b, color.b, f);
                    px[i] = c;
                }
            });
        }

        /// <summary>
        /// Tint a band along silhouette edges facing a direction (e.g. moss on top surfaces).
        /// mask(p) in 0..1 modulates it (noise patches). Reads alpha only → row-parallel safe.
        /// </summary>
        public void EdgeBand(Vector2 dirUnits, float thicknessUnits, Color color, System.Func<Vector2, float> mask)
        {
            int steps = Mathf.Max(1, Mathf.RoundToInt(thicknessUnits * Ppu));
            Vector2 d = dirUnits.normalized;
            ForRows(0, Width, 0, Height, y =>
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    if (px[i].a <= 0.5f) continue;
                    float edge = 0f;
                    for (int k = 1; k <= steps; k++)
                    {
                        int nx = x + Mathf.RoundToInt(d.x * k), ny = y + Mathf.RoundToInt(d.y * k);
                        float an = (nx < 0 || ny < 0 || nx >= Width || ny >= Height) ? 0f : px[ny * Width + nx].a;
                        if (an < 0.5f) { edge = 1f - (k - 1) / (float)steps; break; }
                    }
                    if (edge <= 0f) continue;
                    float f = edge * mask(ToUnits(x, y)) * color.a;
                    if (f <= 0f) continue;
                    Color c = px[i];
                    c.r = Mathf.Lerp(c.r, color.r, f);
                    c.g = Mathf.Lerp(c.g, color.g, f);
                    c.b = Mathf.Lerp(c.b, color.b, f);
                    px[i] = c;
                }
            });
        }

        /// <summary>
        /// Cartoon outline: a band of <paramref name="color"/> around everything drawn so far, laid
        /// underneath it (the alpha grown by <paramref name="widthUnits"/>, the drawing composited on top).
        /// </summary>
        public void Outline(Color color, float widthUnits)
        {
            int r = Mathf.Max(1, Mathf.RoundToInt(widthUnits * Ppu));
            var src = (Color[])px.Clone();
            ForRows(0, Width, 0, Height, y =>
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    Color c = src[i];
                    if (c.a >= 0.999f) continue;
                    // the grown alpha: the strongest neighbour inside the disc, faded by distance at the rim
                    float grown = 0f;
                    for (int oy = -r; oy <= r && grown < 1f; oy++)
                    {
                        int ny = y + oy;
                        if (ny < 0 || ny >= Height) continue;
                        for (int ox = -r; ox <= r; ox++)
                        {
                            int nx = x + ox;
                            if (nx < 0 || nx >= Width) continue;
                            float d = Mathf.Sqrt(ox * ox + oy * oy);
                            if (d > r + 0.5f) continue;
                            float a = src[ny * Width + nx].a * Mathf.Clamp01(r + 0.5f - d);
                            if (a > grown) grown = a;
                        }
                    }
                    float ua = grown * color.a;
                    if (ua <= 0f) continue;
                    // the drawing over the outline (straight alpha "over")
                    float outA = c.a + ua * (1f - c.a);
                    Color o = (c * c.a + color * ua * (1f - c.a)) / Mathf.Max(outA, 1e-5f);
                    o.a = outA;
                    px[i] = o;
                }
            });
        }

        /// <summary>Remove alpha outside a shape.</summary>
        public void Clip(SdfFn keep, float softness = 0f)
        {
            ForRows(0, Width, 0, Height, y =>
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    if (px[i].a <= 0f) continue;
                    px[i].a *= Coverage(keep(ToUnits(x, y)), softness);
                }
            });
        }

        /// <summary>Directly write every pixel (used for gradients and procedural fields).</summary>
        public void Field(ColorFn fn)
        {
            ForRows(0, Width, 0, Height, y =>
            {
                for (int x = 0; x < Width; x++)
                    px[y * Width + x] = fn(ToUnits(x, y));
            });
        }

        /// <summary>Box blur of the whole canvas (straight alpha aware). Used for soft/defocused layers.</summary>
        public void Blur(int radius)
        {
            if (radius <= 0) return;
            var tmp = new Color[px.Length];
            ForRows(0, Width, 0, Height, y =>
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    Color c = px[i];
                    px[i] = new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                }
            });
            BlurPass(px, tmp, radius, true);
            BlurPass(tmp, px, radius, false);
            ForRows(0, Width, 0, Height, y =>
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    Color c = px[i];
                    px[i] = c.a > 1e-5f ? new Color(c.r / c.a, c.g / c.a, c.b / c.a, c.a) : new Color(0, 0, 0, 0);
                }
            });
        }

        void BlurPass(Color[] src, Color[] dst, int r, bool horizontal)
        {
            int w = Width, h = Height;
            float inv = 1f / (2 * r + 1);
            if (horizontal)
            {
                Par.For(0, h, y =>
                {
                    Color acc = new Color(0, 0, 0, 0);
                    int row = y * w;
                    for (int k = -r; k <= r; k++) acc += src[row + Mathf.Clamp(k, 0, w - 1)];
                    for (int x = 0; x < w; x++)
                    {
                        dst[row + x] = acc * inv;
                        acc -= src[row + Mathf.Clamp(x - r, 0, w - 1)];
                        acc += src[row + Mathf.Clamp(x + r + 1, 0, w - 1)];
                    }
                });
            }
            else
            {
                Par.For(0, w, x =>
                {
                    Color acc = new Color(0, 0, 0, 0);
                    for (int k = -r; k <= r; k++) acc += src[Mathf.Clamp(k, 0, h - 1) * w + x];
                    for (int y = 0; y < h; y++)
                    {
                        dst[y * w + x] = acc * inv;
                        acc -= src[Mathf.Clamp(y - r, 0, h - 1) * w + x];
                        acc += src[Mathf.Clamp(y + r + 1, 0, h - 1) * w + x];
                    }
                });
            }
        }

        static float SrgbToLinear(float c)
            => c <= 0.04045f ? c / 12.92f : (float)System.Math.Pow((c + 0.055f) / 1.055f, 2.4f);

        public Color Get(int x, int y) => px[y * Width + x];
        public void Set(int x, int y, Color c) => px[y * Width + x] = c;

        /// <summary>
        /// Build a premultiplied texture. linear=true: colors are converted to linear space first
        /// (correct anti-aliasing for characters/FX). linear=false keeps sRGB storage (better for
        /// dark background gradients) and optionally dithers to hide banding.
        /// </summary>
        public Texture2D ToTexture(string name, bool linear = true, bool mips = true,
            TextureWrapMode wrap = TextureWrapMode.Clamp, bool dither = false, bool premultiply = true)
            => CreateTexture(name, Width, Height, Encode(linear, dither, premultiply), linear, mips, wrap);

        /// <summary>Main thread only: upload encoded pixels.</summary>
        public static Texture2D CreateTexture(string name, int width, int height, Color32[] data, bool linear, bool mips,
            TextureWrapMode wrap)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mips, linear)
            {
                name = name,
                wrapMode = wrap,
                filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear,
                anisoLevel = 2
            };
            tex.SetPixels32(data);
            tex.Apply(mips, false);
            return tex;
        }

        /// <summary>Main thread only: sprite for a texture produced from this canvas; scale above 1 shows it bigger (around the pivot) than it was drawn.</summary>
        public Sprite CreateSprite(Texture2D tex, string name, Vector2 pivotUnits, Vector4 border = default, float scale = 1f)
        {
            Vector2 size = new Vector2(Width / Ppu, Height / Ppu);
            Vector2 pivot = new Vector2((pivotUnits.x - origin.x) / size.x, (pivotUnits.y - origin.y) / size.y);
            var s = Sprite.Create(tex, new Rect(0, 0, Width, Height), pivot, Ppu / scale, 0, SpriteMeshType.FullRect, border);
            s.name = name;
            return s;
        }

        /// <summary>
        /// Thread-safe: convert the canvas into texture bytes (premultiplied / linear / dithered as requested).
        /// </summary>
        public Color32[] Encode(bool linear, bool dither, bool premultiply)
        {
            if (!premultiply) BleedColors(6);
            var data = new Color32[px.Length];
            ForRows(0, Width, 0, Height, y =>
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = y * Width + x;
                    Color c = px[i];
                    if (linear) { c.r = SrgbToLinear(c.r); c.g = SrgbToLinear(c.g); c.b = SrgbToLinear(c.b); }
                    float pm = premultiply ? c.a : 1f;
                    float r = c.r * pm, g = c.g * pm, b = c.b * pm;
                    float n = 0f;
                    if (dither)
                    {
                        uint h = (uint)i * 2654435761u;
                        h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                        n = ((h & 0xFFFF) / 65535f - 0.5f) / 255f;
                    }
                    data[i] = new Color32(
                        (byte)Mathf.Clamp((int)((r + n) * 255f + 0.5f), 0, 255),
                        (byte)Mathf.Clamp((int)((g + n) * 255f + 0.5f), 0, 255),
                        (byte)Mathf.Clamp((int)((b + n) * 255f + 0.5f), 0, 255),
                        (byte)Mathf.Clamp((int)(c.a * 255f + 0.5f), 0, 255));
                }
            });
            return data;
        }

        /// <summary>
        /// Straight-alpha textures need color in transparent pixels, otherwise bilinear filtering and
        /// mipmaps pull black into the edges. Spread edge colors outward a few pixels.
        /// </summary>
        void BleedColors(int passes)
        {
            var filled = new bool[px.Length];
            for (int i = 0; i < px.Length; i++) filled[i] = px[i].a > 0.004f;
            var next = new bool[px.Length];
            for (int pass = 0; pass < passes; pass++)
            {
                System.Array.Copy(filled, next, filled.Length);
                // Only unfilled pixels are written and only filled pixels are read, so rows can run in parallel.
                ForRows(0, Width, 0, Height, y =>
                {
                    for (int x = 0; x < Width; x++)
                    {
                        int i = y * Width + x;
                        if (filled[i]) continue;
                        float r = 0, g = 0, b = 0; int n = 0;
                        for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int nx = x + ox, ny = y + oy;
                            if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                            int j = ny * Width + nx;
                            if (!filled[j]) continue;
                            r += px[j].r; g += px[j].g; b += px[j].b; n++;
                        }
                        if (n == 0) continue;
                        px[i] = new Color(r / n, g / n, b / n, px[i].a);
                        next[i] = true;
                    }
                });
                System.Array.Copy(next, filled, filled.Length);
            }
        }

        /// <summary>Create a sprite whose pivot sits at the given unit-space position.</summary>
        public Sprite ToSprite(string name, Vector2 pivotUnits, bool linear = true, bool mips = true,
            TextureWrapMode wrap = TextureWrapMode.Clamp, bool dither = false, Vector4 border = default,
            bool premultiply = true, float scale = 1f)
        {
            var tex = ToTexture(name, linear, mips, wrap, dither, premultiply);
            return CreateSprite(tex, name, pivotUnits, border, scale);
        }
    }
}
