using System;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// A plain RGBA image, bottom row first like a Unity texture. Kept free of
    /// UnityEngine so the backdrop painters below can be run and checked outside
    /// the editor, and produce the same pixels there as they do here.
    /// </summary>
    public sealed class PaintImage
    {
        public readonly int Width;
        public readonly int Height;

        /// <summary>Four floats per pixel, 0-1, straight (not premultiplied) alpha.</summary>
        public readonly float[] Rgba;

        public PaintImage(int width, int height)
        {
            Width = width;
            Height = height;
            Rgba = new float[width * height * 4];
        }

        /// <summary>Lays a colour over one pixel at the given coverage.</summary>
        public void Blend(int x, int y, float grey, float coverage) => Blend(x, y, grey, grey, grey, coverage);

        public void Blend(int x, int y, float r, float g, float b, float coverage)
        {
            if (coverage <= 0f || x < 0 || y < 0 || x >= Width || y >= Height) return;
            if (coverage > 1f) coverage = 1f;

            int i = (y * Width + x) * 4;
            float below = Rgba[i + 3];
            float alpha = coverage + below * (1f - coverage);
            if (alpha <= 0f) return;

            float keep = below * (1f - coverage);
            Rgba[i] = (r * coverage + Rgba[i] * keep) / alpha;
            Rgba[i + 1] = (g * coverage + Rgba[i + 1] * keep) / alpha;
            Rgba[i + 2] = (b * coverage + Rgba[i + 2] * keep) / alpha;
            Rgba[i + 3] = alpha;
        }

        public void Rect(float x0, float y0, float width, float height, float grey)
        {
            int xa = (int)Math.Floor(x0), xb = (int)Math.Ceiling(x0 + width);
            int ya = (int)Math.Floor(y0), yb = (int)Math.Ceiling(y0 + height);

            for (int y = ya; y < yb; y++)
            for (int x = xa; x < xb; x++)
            {
                // Edge pixels get the share of them the rectangle actually covers.
                float cx = Overlap(x, x0, x0 + width);
                float cy = Overlap(y, y0, y0 + height);
                Blend(x, y, grey, cx * cy);
            }
        }

        public void Disc(float cx, float cy, float radius, float grey)
        {
            int xa = (int)Math.Floor(cx - radius - 1), xb = (int)Math.Ceiling(cx + radius + 1);
            int ya = (int)Math.Floor(cy - radius - 1), yb = (int)Math.Ceiling(cy + radius + 1);

            for (int y = ya; y <= yb; y++)
            for (int x = xa; x <= xb; x++)
            {
                double d = Math.Sqrt((x + 0.5 - cx) * (x + 0.5 - cx) + (y + 0.5 - cy) * (y + 0.5 - cy));
                Blend(x, y, grey, Clamp01((float)(radius - d + 0.5)));
            }
        }

        /// <summary>A convex polygon, anti-aliased by 4x4 supersampling.</summary>
        public void Polygon(float grey, params float[] xy)
        {
            int n = xy.Length / 2;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int k = 0; k < n; k++)
            {
                minX = Math.Min(minX, xy[k * 2]); maxX = Math.Max(maxX, xy[k * 2]);
                minY = Math.Min(minY, xy[k * 2 + 1]); maxY = Math.Max(maxY, xy[k * 2 + 1]);
            }

            for (int y = (int)Math.Floor(minY); y < (int)Math.Ceiling(maxY); y++)
            for (int x = (int)Math.Floor(minX); x < (int)Math.Ceiling(maxX); x++)
            {
                int inside = 0;
                for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                    if (Contains(xy, x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f)) inside++;

                Blend(x, y, grey, inside / 16f);
            }
        }

        private static bool Contains(float[] xy, float px, float py)
        {
            int n = xy.Length / 2;
            bool? side = null;
            for (int k = 0; k < n; k++)
            {
                float ax = xy[k * 2], ay = xy[k * 2 + 1];
                float bx = xy[(k + 1) % n * 2], by = xy[(k + 1) % n * 2 + 1];
                float cross = (bx - ax) * (py - ay) - (by - ay) * (px - ax);
                if (Math.Abs(cross) < 1e-6f) continue;
                bool s = cross > 0f;
                if (side == null) side = s;
                else if (side != s) return false;
            }
            return true;
        }

        private static float Overlap(int cell, float from, float to) =>
            Clamp01(Math.Min(cell + 1f, to) - Math.Max(cell, from));

        internal static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    /// <summary>
    /// Paints the layered backdrop: the sun, two ridges of hills, a band of
    /// towers and a street of houses, each a tileable strip in a light neutral
    /// grey that the level's theme tints.
    ///
    /// Depth is carried three ways at once, because any one alone reads flat:
    ///
    ///  * <b>Parallax</b> - further layers scroll slower (set in the theme).
    ///  * <b>Air</b> - further layers are lighter, closer to the sky, as distance
    ///    washes colour out: ridges 0.88 and 0.82, towers 0.74, houses 0.64. The
    ///    runner is black and nearest of all. Each step is at least 1.12:1 from
    ///    the next - less and neighbouring layers merge into one flat shape, which
    ///    is what the first cut did to the hills.
    ///  * <b>Size</b> - things of known size shrink with distance. Trees are
    ///    drawn on the far ridge a few pixels tall and between the houses several
    ///    times that, so the eye measures the gap between them.
    ///
    /// Nothing crosses the left or right edge except shapes that are continuous
    /// across it by construction (the hill curves, the street), so every strip
    /// repeats without a seam.
    /// </summary>
    public static class BackdropPainter
    {
        /// <summary>
        /// Twice the resolution of the other placeholder sprites. Curves and roof
        /// lines are what these layers are made of, and at 32 per unit they would
        /// be magnified into visible steps on a 1080p screen.
        /// </summary>
        public const int PixelsPerUnit = 64;

        public const float FarRidgeGrey = 0.88f;
        public const float HillGrey = 0.82f;
        public const float TowerGrey = 0.74f;
        public const float HouseGrey = 0.64f;

        /// <summary>
        /// The darkest detail any layer is allowed: houses times the window shade.
        /// The contrast targets were measured down to this.
        /// </summary>
        public const float DetailShade = 0.80f;

        // ---- sun ------------------------------------------------------------ //

        public static PaintImage Sun(int size)
        {
            var image = new PaintImage(size, size);
            float c = size * 0.5f;
            float disc = size * 0.16f;
            float glow = size * 0.5f - 1f;

            // A soft halo first, then the disc on top of it.
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                double d = Math.Sqrt((x + 0.5 - c) * (x + 0.5 - c) + (y + 0.5 - c) * (y + 0.5 - c));
                if (d >= glow) continue;
                float t = (float)((d - disc) / (glow - disc));
                if (t < 0f) t = 0f;
                image.Blend(x, y, 1f, 0.42f * (1f - t) * (1f - t));
            }

            image.Disc(c, c, disc, 1f);
            return image;
        }

        // ---- hills ---------------------------------------------------------- //

        /// <summary>
        /// A ridge of rolling hills with trees along it. The height is a sum of
        /// sine waves whose periods divide the strip exactly, so the right edge
        /// meets the left. Filled solid below the crest to the bottom of the strip.
        /// </summary>
        public static PaintImage Hills(int width, int height, int seed, float grey,
                                       float crestLow, float crestHigh, float treeScale, float treeChance)
        {
            var image = new PaintImage(width, height);
            var random = new Random(seed);

            int[] periods = { 1, 2, 3, 7 };
            float[] weights = { 0.50f, 0.26f, 0.16f, 0.08f };
            var phases = new double[periods.Length];
            for (int k = 0; k < phases.Length; k++) phases[k] = random.NextDouble() * Math.PI * 2;

            double Crest(double x)
            {
                double v = 0;
                for (int k = 0; k < periods.Length; k++)
                    v += weights[k] * Math.Sin(2 * Math.PI * periods[k] * x / width + phases[k]);
                // v spans about -1..1; map onto the crest band.
                double t = (v + 1) * 0.5;
                return (crestLow + (crestHigh - crestLow) * t) * height;
            }

            for (int x = 0; x < width; x++)
            {
                double top = Crest(x + 0.5);
                for (int y = 0; y < height; y++)
                    image.Blend(x, y, grey, PaintImage.Clamp01((float)(top - y)));
            }

            // Trees along the crest, the size cue. Kept off the edges so none is
            // cut in half at the seam.
            int margin = (int)(24 * treeScale) + 4;
            for (int x = margin; x < width - margin; x += 3 + random.Next((int)(10 * treeScale) + 6))
            {
                if (random.NextDouble() > treeChance) continue;

                float baseY = (float)Crest(x) - 2f * treeScale;
                float tall = (10f + (float)random.NextDouble() * 10f) * treeScale;

                if (random.NextDouble() < 0.55)
                {
                    float half = tall * 0.32f;
                    image.Polygon(grey, x - half, baseY, x + half, baseY, x, baseY + tall);
                }
                else
                {
                    float r = tall * 0.36f;
                    image.Rect(x - 0.8f * treeScale, baseY, 1.6f * treeScale, tall * 0.4f, grey);
                    image.Disc(x, baseY + tall * 0.4f + r * 0.8f, r, grey);
                }
            }

            return image;
        }

        // ---- towers --------------------------------------------------------- //

        /// <summary>
        /// A downtown band of tall towers: set-backs, spires, slanted crowns and
        /// masts, with window mullions running up the faces. Heights swell towards
        /// two clusters along the strip so it reads as a city centre rather than a
        /// comb.
        /// </summary>
        public static PaintImage Towers(int width, int height, int seed, float grey)
        {
            var image = new PaintImage(width, height);
            var random = new Random(seed);
            float shade = grey * DetailShade;
            double phase = random.NextDouble() * Math.PI * 2;

            int x = 6;
            while (x < width - 6)
            {
                int w = 36 + random.Next(70);
                if (x + w > width - 6) w = width - 6 - x;
                if (w < 24) break;

                double cluster = 0.5 + 0.5 * Math.Sin(2 * Math.PI * 2 * (x + w * 0.5) / width + phase);

                // Between the clusters the city thins out and leaves open sky, so
                // the hills behind can be seen. Layers only read as depth where
                // one shows through another; a solid wall of towers hid the hills
                // completely.
                if (cluster < 0.35 && random.NextDouble() < 0.65)
                {
                    x += 40 + random.Next(60);
                    continue;
                }

                float envelope = (float)(0.35 + 0.65 * cluster);
                float h = Math.Max(150f, (0.40f + 0.60f * (float)random.NextDouble()) * envelope * height * 0.86f);

                image.Rect(x, 0, w, h, grey);
                float roof = h;

                switch (random.Next(4))
                {
                    case 0:     // flat top with a plant room
                    {
                        float bw = w * (0.35f + 0.25f * (float)random.NextDouble());
                        float bh = 10 + random.Next(18);
                        image.Rect(x + (w - bw) * 0.5f, h, bw, bh, grey);
                        roof = h + bh;
                        break;
                    }
                    case 1:     // stepped set-backs
                    {
                        float tw = w, top = h;
                        int tiers = 1 + random.Next(2);
                        for (int t = 0; t < tiers; t++)
                        {
                            tw *= 0.72f;
                            float th = 18 + random.Next(36);
                            image.Rect(x + (w - tw) * 0.5f, top, tw, th, grey);
                            top += th;
                        }
                        roof = top;
                        break;
                    }
                    case 2:     // spire
                    {
                        float sh = w * (0.6f + 0.8f * (float)random.NextDouble());
                        image.Polygon(grey, x, h, x + w, h, x + w * 0.5f, h + sh);
                        roof = h + sh;
                        break;
                    }
                    default:    // slanted crown
                    {
                        float rise = w * (0.25f + 0.35f * (float)random.NextDouble());
                        if (random.Next(2) == 0) image.Polygon(grey, x, h, x + w, h, x + w, h + rise);
                        else image.Polygon(grey, x, h, x + w, h, x, h + rise);
                        break;
                    }
                }

                if (random.NextDouble() < 0.3)
                    image.Rect(x + w * 0.5f - 1f, roof, 2f, 18 + random.Next(34), grey);

                // Mullions: thin vertical strips up the face, stopping short of the
                // top so the silhouette stays clean.
                for (float wx = x + 6; wx + 3 <= x + w - 5; wx += 8)
                    image.Rect(wx, 10, 3, Math.Max(0f, h - 24), shade);

                // Floor bands every few storeys break up the strips.
                for (float wy = 26; wy < h - 20; wy += 42)
                    image.Rect(x + 4, wy, w - 8, 3, grey);

                x += w + 2 + random.Next(12) + (random.NextDouble() < 0.1 ? 26 : 0);
            }

            return image;
        }

        // ---- houses --------------------------------------------------------- //

        /// <summary>
        /// A street of pitched-roof houses with chimneys, doors and windows, trees
        /// and short fences between them, standing on a solid band that runs the
        /// full width. The band is deliberately deep: it is the nearest backdrop
        /// layer, so it is what shows through the gaps between rooftops, and it
        /// has to reach the bottom of the screen.
        /// </summary>
        public static PaintImage Houses(int width, int height, int seed, float grey, float street)
        {
            var image = new PaintImage(width, height);
            var random = new Random(seed);
            float shade = grey * DetailShade;
            float treeGrey = grey * 0.92f;

            image.Rect(0, 0, width, street, grey);

            int x = 10;
            while (x < width - 10)
            {
                double pick = random.NextDouble();

                if (pick < 0.68)
                {
                    int w = 110 + random.Next(90);
                    if (x + w + 12 > width - 4) { x = width; break; }

                    float wall = 80 + random.Next(70);
                    float roofH = 44 + random.Next(46);
                    float top = street + wall;
                    const float eave = 9f;

                    // Chimney before the roof, so the roof overlaps its foot.
                    if (random.NextDouble() < 0.55)
                    {
                        float cx = x + w * (0.62f + 0.18f * (float)random.NextDouble());
                        image.Rect(cx, top, 14, roofH * 0.75f + 10, grey);
                        image.Rect(cx - 2, top + roofH * 0.75f + 10, 18, 4, grey);
                    }

                    image.Rect(x, street, w, wall, grey);

                    if (random.NextDouble() < 0.6)
                        image.Polygon(grey, x - eave, top, x + w + eave, top, x + w * 0.5f, top + roofH);
                    else
                        image.Polygon(grey, x - eave, top, x + w + eave, top,
                                      x + w * 0.72f, top + roofH, x + w * 0.28f, top + roofH);

                    int storeys = wall > 118 ? 2 : 1;
                    int across = Math.Max(2, w / 52);
                    int door = random.Next(across);
                    float step = w / (float)across;

                    for (int s = 0; s < storeys; s++)
                    for (int c = 0; c < across; c++)
                    {
                        float cx = x + step * (c + 0.5f);
                        if (s == 0 && c == door)
                        {
                            image.Rect(cx - 11, street, 22, 42, shade);
                            continue;
                        }

                        float wy = street + 16 + s * (wall / storeys);
                        image.Rect(cx - 9, wy, 18, Math.Min(24f, wall / storeys - 24f), shade);
                    }

                    x += w + 14 + random.Next(10);
                }
                else if (pick < 0.90)
                {
                    float r = 22 + random.Next(20);
                    if (x + r * 2 + 8 > width - 4) { x = width; break; }

                    float cx = x + r + 4;
                    float trunk = 18 + random.Next(20);
                    image.Rect(cx - 3, street, 6, trunk + r * 0.5f, treeGrey);
                    image.Disc(cx, street + trunk + r * 0.8f, r, treeGrey);
                    if (random.NextDouble() < 0.5)
                        image.Disc(cx + r * 0.55f, street + trunk + r * 0.45f, r * 0.7f, treeGrey);

                    x += (int)(r * 2) + 14;
                }
                else
                {
                    int w = 40 + random.Next(50);
                    if (x + w > width - 4) { x = width; break; }

                    for (int px = x; px < x + w; px += 10) image.Rect(px, street, 3, 24, grey);
                    image.Rect(x, street + 16, w, 3, grey);
                    x += w + 12;
                }
            }

            return image;
        }
    }
}
