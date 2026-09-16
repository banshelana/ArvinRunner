using System;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Paints the helicopter strike: the laser designator's mark on the roof, the
    /// missile and its motor flame, the smoke it trails, the laser beam, the
    /// fireball frames and the scorch left behind.
    ///
    /// Kept free of UnityEngine like <see cref="BackdropPainter"/>, so the same
    /// pixels can be painted and looked at outside the editor.
    ///
    /// What makes each read as real rather than as an icon:
    ///
    ///  * <b>Light.</b> The missile is shaded as a cylinder lit from above, with a
    ///    specular band, so it has volume instead of being a flat dart.
    ///  * <b>Perspective.</b> The mark is an ellipse, not a circle, because it
    ///    lies on the roof and the camera looks along the roof.
    ///  * <b>Heat.</b> Fire is coloured by temperature - white core, yellow,
    ///    orange, deep red - and breaks up with noise, so it billows.
    ///  * <b>Aftermath.</b> Fire turns to smoke that rises and thins, and the roof
    ///    keeps a scorch mark after it has gone.
    /// </summary>
    public static class StrikePainter
    {
        /// <summary>
        /// Shared by the missile and its flame, which the factory lines up in the
        /// missile's local units; the other pieces are fitted to world sizes.
        /// </summary>
        public const int PixelsPerUnit = 200;

        public const int SmokeVariants = 3;
        public const int BlastFrames = 16;

        /// <summary>The motor nozzle, as a share of the missile texture's width from the left.</summary>
        public const float MissileNozzleX = 30f / 256f;

        /// <summary>The mark's outer ring radius, as a share of its texture's width.</summary>
        public const float MarkerRingRadius = 0.96f * 0.47f;

        /// <summary>The fireball's largest radius, as a share of a blast frame's width.</summary>
        public const float BlastRadius = 0.46f;

        /// <summary>The ground line in the blast frames, as a share of their height from the bottom.</summary>
        public const float BlastGround = 40f / 256f;

        /// <summary>The scorch's radius, as a share of its texture's width.</summary>
        public const float ScorchRadius = 0.44f;

        // ---- colour ---------------------------------------------------------- //

        private struct Rgb
        {
            public float R, G, B;

            public Rgb(float r, float g, float b)
            {
                R = r;
                G = g;
                B = b;
            }

            public static Rgb Lerp(Rgb a, Rgb b, float t)
            {
                t = Clamp01(t);
                return new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
            }

            public static Rgb operator *(Rgb a, float k) => new Rgb(a.R * k, a.G * k, a.B * k);
            public static Rgb operator +(Rgb a, Rgb b) => new Rgb(a.R + b.R, a.G + b.G, a.B + b.B);
        }

        /// <summary>Colour by temperature, 0 (dull red) to 1 (white hot).</summary>
        private static Rgb FireRamp(float heat)
        {
            var ember = new Rgb(0.50f, 0.10f, 0.03f);
            var orange = new Rgb(1.00f, 0.45f, 0.08f);
            var yellow = new Rgb(1.00f, 0.80f, 0.30f);
            var white = new Rgb(1.00f, 0.97f, 0.86f);

            heat = Clamp01(heat);
            if (heat < 0.35f) return Rgb.Lerp(ember, orange, heat / 0.35f);
            if (heat < 0.70f) return Rgb.Lerp(orange, yellow, (heat - 0.35f) / 0.35f);
            return Rgb.Lerp(yellow, white, (heat - 0.70f) / 0.30f);
        }

        private delegate bool Shader(float x, float y, out Rgb colour, out float alpha);

        /// <summary>
        /// Evaluates a shader at samples x samples points per pixel and lays the
        /// average over the image - anti-aliasing for any shape a shader describes.
        /// </summary>
        private static void Paint(PaintImage image, int samples, Shader shader)
        {
            float step = 1f / samples;
            float count = samples * samples;

            for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;

                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    if (!shader(x + (sx + 0.5f) * step, y + (sy + 0.5f) * step, out Rgb c, out float alpha)) continue;
                    if (alpha <= 0f) continue;

                    r += c.R * alpha;
                    g += c.G * alpha;
                    b += c.B * alpha;
                    a += alpha;
                }

                if (a <= 0f) continue;
                image.Blend(x, y, Clamp01(r / a), Clamp01(g / a), Clamp01(b / a), a / count);
            }
        }

        // ---- the missile ------------------------------------------------------ //

        /// <summary>Side view, nose pointing +X so the code can rotate it along its flight.</summary>
        public static PaintImage Missile(int width, int height)
        {
            var image = new PaintImage(width, height);
            float k = width / 256f;
            float cy = height * 0.5f;
            float radius = 8.5f * k;

            float nozzleX = MissileNozzleX * width;
            float bodyX = 36f * k;
            float noseX = 204f * k;
            float tipX = 250f * k;

            var finColour = new Rgb(0.30f, 0.32f, 0.29f);

            // Fins first, so the body lays over their roots. Tail fins sweep back
            // from the body; the small canards near the front sweep the same way.
            Paint(image, 4, (float x, float y, out Rgb c, out float a) =>
            {
                c = finColour;
                a = 1f;

                float up = y - cy;
                float span = Math.Abs(up) - radius * 0.6f;
                if (span < 0f) return false;

                bool tail = span <= radius * 0.4f + 15f * k &&
                            x <= bodyX + 34f * k - span * 1.6f &&
                            x >= bodyX - 2f * k + Math.Max(0f, span - radius * 0.4f) * 0.35f;

                bool canard = span <= radius * 0.4f + 6f * k &&
                              x >= 150f * k &&
                              x <= 170f * k - span * 1.4f;

                if (!tail && !canard) return false;

                // Lit from above like the body: the upper fin catches the light.
                float tone = up > 0f ? 1.05f : 0.72f;
                tone *= 1f - 0.2f * Clamp01(span / (radius * 0.4f + 15f * k));
                c = finColour * tone;
                return true;
            });

            // The body: nozzle skirt, olive airframe with a yellow warning band and
            // panel seams, and a darker radome nose.
            Paint(image, 4, (float x, float y, out Rgb c, out float a) =>
            {
                c = default;
                a = 1f;
                if (x < nozzleX || x > tipX) return false;

                float rad;
                if (x < bodyX) rad = radius * 0.78f;
                else if (x < noseX) rad = radius;
                else
                {
                    float u = (x - noseX) / (tipX - noseX);
                    rad = radius * (float)Math.Pow(Math.Max(0.0, 1.0 - u * u), 0.55);
                }

                float dy = y - cy;
                if (rad <= 0.01f || Math.Abs(dy) > rad) return false;

                // A cylinder lit from above and in front of the screen.
                float v = dy / rad;
                float nz = (float)Math.Sqrt(Math.Max(0f, 1f - v * v));
                float lambert = Math.Max(0f, v * 0.55f + nz * 0.83f);
                float spec = (float)Math.Exp(-Math.Pow((v - 0.5f) / 0.13f, 2)) * 0.32f;

                Rgb paint;
                if (x < bodyX) paint = new Rgb(0.16f, 0.16f, 0.17f);
                else if (x >= noseX) paint = new Rgb(0.24f, 0.25f, 0.26f);
                else if (x > 186f * k && x < 194f * k) paint = new Rgb(0.88f, 0.70f, 0.16f);
                else paint = new Rgb(0.46f, 0.49f, 0.43f);

                bool seam = x >= bodyX && x < noseX &&
                            (Math.Abs(x - 96f * k) < 0.7f * k || Math.Abs(x - 146f * k) < 0.7f * k);
                if (seam) paint = paint * 0.62f;

                float shade = 0.32f + 0.85f * lambert;
                c = paint * shade + new Rgb(spec, spec, spec);
                return true;
            });

            return image;
        }

        /// <summary>
        /// The drone's bomb. Side view, nose pointing +X like the missile so the
        /// same code turns it along its fall: a fat teardrop body fullest ahead of
        /// centre, a box tail, a warning band and a fuse cap.
        /// </summary>
        public static PaintImage Bomb(int width, int height)
        {
            var image = new PaintImage(width, height);
            float k = width / 256f;
            float cy = height * 0.5f;
            float radius = 24f * k;

            float tailX = 6f * k;
            float bodyX = 44f * k;
            float tipX = 248f * k;

            var finColour = new Rgb(0.26f, 0.28f, 0.24f);

            // The box tail: two fin plates and the struts that hold them, behind
            // the body so it lays over their roots.
            Paint(image, 4, (float x, float y, out Rgb c, out float a) =>
            {
                c = finColour;
                a = 1f;
                if (x < tailX || x > bodyX + 26f * k) return false;

                float up = y - cy;
                float span = Math.Abs(up);
                float reach = radius * 0.95f - Math.Max(0f, x - bodyX) * 0.9f;
                if (span > reach) return false;

                bool plate = span >= reach - 3.5f * k;
                bool strut = x <= tailX + 5f * k || span <= 2.5f * k;
                if (!plate && !strut) return false;

                c = finColour * (up > 0f ? 1.08f : 0.74f);
                return true;
            });

            Paint(image, 4, (float x, float y, out Rgb c, out float a) =>
            {
                c = default;
                a = 1f;
                if (x < bodyX || x > tipX) return false;

                // Tapered to the tail, round at the nose, fullest 60% along.
                float u = (x - bodyX) / (tipX - bodyX);
                float rad = radius * (u < 0.6f
                    ? 0.35f + 0.65f * Smooth(0f, 1f, u / 0.6f)
                    : (float)Math.Sqrt(Math.Max(0.0, 1.0 - Math.Pow((u - 0.6f) / 0.4f, 2))));

                float dy = y - cy;
                if (rad <= 0.01f || Math.Abs(dy) > rad) return false;

                // Lit from above and in front, like the missile.
                float v = dy / rad;
                float nz = (float)Math.Sqrt(Math.Max(0f, 1f - v * v));
                float lambert = Math.Max(0f, v * 0.55f + nz * 0.83f);
                float spec = (float)Math.Exp(-Math.Pow((v - 0.45f) / 0.15f, 2)) * 0.28f;

                var paint = new Rgb(0.34f, 0.37f, 0.30f);
                if (x > 234f * k) paint = new Rgb(0.20f, 0.20f, 0.21f);
                else if (Math.Abs(x - 170f * k) < 4f * k) paint = new Rgb(0.88f, 0.70f, 0.16f);
                else if (Math.Abs(x - 110f * k) < 0.7f * k) paint = paint * 0.62f;

                float shade = 0.32f + 0.85f * lambert;
                c = paint * shade + new Rgb(spec, spec, spec);
                return true;
            });

            return image;
        }

        /// <summary>The motor plume, streaming left from a nozzle at the right edge.</summary>
        public static PaintImage Flame(int width, int height)
        {
            var image = new PaintImage(width, height);
            float cy = height * 0.5f;
            float r0 = height * 0.2f;

            Paint(image, 3, (float x, float y, out Rgb c, out float a) =>
            {
                c = default;
                a = 0f;

                float d = 1f - x / width;
                float rad = r0 * (1f - d * 0.85f) * (1f + 0.18f * (float)Math.Sin(d * 26f) * (1f - d));
                float v = Math.Abs(y - cy) / Math.Max(0.5f, rad);
                if (v >= 1f) return false;

                float noise = Fbm(x * 0.09f, y * 0.25f, 11, 3);
                float heat = (1f - v * v) * (float)Math.Pow(1f - d, 1.3f) * (0.75f + 0.5f * noise);
                if (heat <= 0.02f) return false;

                c = FireRamp(heat * 1.25f);
                a = Clamp01(heat * 2.2f);
                return true;
            });

            return image;
        }

        /// <summary>A soft, billowing puff of smoke in light grey, for the trail. Tinted at runtime.</summary>
        public static PaintImage Smoke(int size, int variant)
        {
            var image = new PaintImage(size, size);
            int seed = 101 + variant * 37;
            float centre = size * 0.5f;
            float radius = size * 0.44f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - centre, dy = y + 0.5f - centre;
                float n = (float)Math.Sqrt(dx * dx + dy * dy) / radius;
                if (n > 1.4f) continue;

                float f = Fbm(x * 6f / size, y * 6f / size, seed, 4);
                float mask = 1f - Smooth(0.55f, 1f, n + (f - 0.5f) * 0.6f);
                if (mask <= 0f) continue;

                // Lighter on top, where the sky lights it.
                float shade = Clamp01(0.80f + 0.14f * (dy / radius) + 0.12f * (f - 0.5f));
                image.Blend(x, y, shade, mask * (0.55f + 0.45f * f));
            }

            return image;
        }

        // ---- the designator ----------------------------------------------------- //

        /// <summary>
        /// The laser designator's mark, as it lies on the roof: foreshortened to an
        /// ellipse, a solid outer ring on the lethal edge, a dashed inner ring,
        /// range ticks, and the laser's hot spot at the centre over a red glow.
        /// </summary>
        public static PaintImage Marker(int width, int height)
        {
            var image = new PaintImage(width, height);
            float cx = width * 0.5f, cy = height * 0.5f;
            float rx = width * 0.47f, ry = height * 0.40f;
            float s = width / 512f;

            var red = new Rgb(1f, 0.16f, 0.10f);
            var hot = new Rgb(1f, 0.93f, 0.88f);

            Paint(image, 3, (float x, float y, out Rgb c, out float a) =>
            {
                c = red;
                a = 0f;

                float ex = (x - cx) / rx, ey = (y - cy) / ry;
                float q = (float)Math.Sqrt(ex * ex + ey * ey);

                // How far q moves per pixel here, so every line is the same width
                // on screen however squashed the ellipse is at that point.
                float safeQ = Math.Max(q, 1e-4f);
                float gx = ex / (rx * safeQ), gy = ey / (ry * safeQ);
                float perPixel = Math.Max((float)Math.Sqrt(gx * gx + gy * gy), 1e-5f);

                if (q < 1f) a = 0.30f * (float)Math.Pow(1f - q, 1.4f);

                if (Math.Abs(q - 0.96f) / perPixel < 1.9f * s) a = Math.Max(a, 0.95f);

                double angle = Math.Atan2(ey, ex);
                bool dash = (angle + Math.PI) / (2.0 * Math.PI) * 28.0 % 1.0 < 0.5;
                if (dash && Math.Abs(q - 0.58f) / perPixel < 1.3f * s) a = Math.Max(a, 0.8f);

                float ax = Math.Abs(ex), ay = Math.Abs(ey);
                if (Math.Abs(ey * ry) < 1.6f * s && ax > 0.66f && ax < 0.88f) a = Math.Max(a, 0.9f);
                if (Math.Abs(ex * rx) < 1.6f * s && ay > 0.64f && ay < 0.90f) a = Math.Max(a, 0.9f);

                if (q < 0.12f)
                {
                    a = Math.Max(a, 1f - Smooth(0.04f, 0.12f, q));
                    c = Rgb.Lerp(red, hot, 1f - Smooth(0f, 0.06f, q));
                }

                return a > 0f;
            });

            return image;
        }

        /// <summary>The designator's beam: a thin hot core in a red glow, stretched along X at runtime.</summary>
        public static PaintImage Laser(int width, int height)
        {
            var image = new PaintImage(width, height);
            float centre = height * 0.5f;
            var red = new Rgb(1f, 0.15f, 0.10f);
            var core = new Rgb(1f, 0.90f, 0.85f);

            for (int y = 0; y < height; y++)
            {
                float v = Math.Abs(y + 0.5f - centre) / centre;
                float hotness = (float)Math.Exp(-Math.Pow(v / 0.16f, 2));
                float glow = 0.55f * (float)Math.Exp(-Math.Pow(v / 0.6f, 2));
                float alpha = Clamp01(hotness + glow);
                if (alpha <= 0.003f) continue;

                Rgb colour = Rgb.Lerp(red, core, hotness / Math.Max(1e-4f, hotness + glow));
                for (int x = 0; x < width; x++)
                    image.Blend(x, y, colour.R, colour.G, colour.B, alpha);
            }

            return image;
        }

        // ---- the impact ----------------------------------------------------------- //

        /// <summary>
        /// One frame of the explosion, <paramref name="frame"/> of <see cref="BlastFrames"/>.
        ///
        /// A ground burst: the fireball swells fast out of a white flash, sits on
        /// the roof rather than floating over it, throws sparks and debris, cools
        /// from white through orange to smoke, and the smoke rises and thins away.
        /// The ground line is <see cref="BlastGround"/>; nothing is drawn below it.
        /// </summary>
        public static PaintImage Blast(int size, int frame)
        {
            var image = new PaintImage(size, size);
            const int seed = 3;

            float t = (frame + 0.6f) / BlastFrames;
            float s = size / 256f;
            float ground = BlastGround * size;
            float maxRadius = BlastRadius * size;

            float radius = maxRadius * (1f - (float)Math.Pow(1f - Math.Min(t * 1.25f, 1f), 2.4f));
            float rise = (float)Math.Pow(t, 1.6f) * 46f * s;
            float cx = size * 0.5f;
            float cy = ground + radius * 0.42f + rise;

            float fire = 1f - Smooth(0.10f, 0.68f, t);
            float fade = 1f - Smooth(0.72f, 1f, t);

            for (int y = Math.Max(0, (int)ground - 1); y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float groundCut = Clamp01(py - ground + 1f);
                if (groundCut <= 0f) continue;

                float dx = px - cx, dy = (py - cy) * 1.08f;
                float n = (float)Math.Sqrt(dx * dx + dy * dy) / Math.Max(1f, radius);
                if (n > 1.5f) continue;

                float f = Fbm(px * 0.03f / s + t * 1.1f, py * 0.03f / s - t * 1.8f, seed, 5);
                float mask = 1f - Smooth(0.74f, 1f, n + (f - 0.5f) * 0.62f);
                if (mask <= 0f) continue;

                float heat = Clamp01((1f - n * 0.95f) * 1.3f + (f - 0.5f) * 0.9f) * fire;

                float grey = (0.14f + 0.26f * t) * (0.7f + 0.6f * f) + 0.10f * Clamp01(dy / Math.Max(1f, radius));
                var smoke = new Rgb(grey, grey * 0.97f, grey * 0.94f);

                Rgb colour = heat > 0.05f
                    ? Rgb.Lerp(smoke, FireRamp(heat), Smooth(0.05f, 0.35f, heat))
                    : smoke;

                image.Blend(x, y, Clamp01(colour.R), Clamp01(colour.G), Clamp01(colour.B), mask * fade * groundCut);
            }

            // The flash, over everything for the first two frames.
            if (frame < 2)
            {
                float flashRadius = maxRadius * (0.55f + frame * 0.35f);
                float strength = frame == 0 ? 0.9f : 0.45f;
                DiscGlow(image, cx, ground + flashRadius * 0.2f, flashRadius, ground,
                         new Rgb(1f, 0.96f, 0.80f), strength);
            }

            // Sparks thrown up and out, falling as they fade.
            if (frame < 7)
            {
                float alpha = Clamp01(1f - t * 6.5f);
                for (int i = 0; i < 22; i++)
                {
                    double angle = Math.PI * (0.08 + 0.84 * Hash(i, 1, seed));
                    float speed = 0.9f + 1.1f * Hash(i, 2, seed);
                    float reach = maxRadius * (0.25f + 1.5f * t * speed);
                    float sx = cx + (float)Math.Cos(angle) * reach;
                    float sy = ground + (float)Math.Sin(angle) * reach * 0.9f - 30f * s * t * t * speed;
                    if (sy < ground) continue;
                    Dot(image, sx, sy, 2.2f * s, new Rgb(1f, 0.82f, 0.35f), alpha);
                }
            }

            // Dark debris on heavier arcs.
            if (frame >= 1 && frame < 11)
            {
                float alpha = Clamp01(1.2f - t * 1.6f);
                for (int i = 0; i < 14; i++)
                {
                    double angle = Math.PI * (0.2 + 0.6 * Hash(i, 5, seed));
                    float speed = 0.7f + 0.8f * Hash(i, 6, seed);
                    float reach = maxRadius * (0.3f + 1.7f * t * speed);
                    float sx = cx + (float)Math.Cos(angle) * reach;
                    float sy = ground + (float)Math.Sin(angle) * reach - 160f * s * t * t;
                    if (sy < ground) continue;
                    Dot(image, sx, sy, 2.8f * s, new Rgb(0.12f, 0.11f, 0.10f), alpha);
                }
            }

            return image;
        }

        /// <summary>The black scorch a strike leaves on the roof, seen at the same angle as the mark.</summary>
        public static PaintImage Scorch(int width, int height)
        {
            var image = new PaintImage(width, height);
            float cx = width * 0.5f, cy = height * 0.5f;
            float rx = ScorchRadius * width, ry = height * 0.38f;

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float ex = (x + 0.5f - cx) / rx, ey = (y + 0.5f - cy) / ry;
                float q = (float)Math.Sqrt(ex * ex + ey * ey);
                if (q > 1.4f) continue;

                float f = Fbm(x * 0.05f, y * 0.12f, 7, 4);
                float mask = 1f - Smooth(0.35f, 1f, q + (f - 0.5f) * 0.5f);
                if (mask <= 0f) continue;

                // Streaks of soot thrown outward from the centre, rather than
                // per-pixel specks, which read as noise on the screen.
                float streak = Fbm((float)Math.Atan2(ey, ex) * 6f, q * 3f, 13, 3);
                float soot = 0.7f + 0.4f * f + 0.35f * (streak - 0.5f);
                image.Blend(x, y, 0.06f, 0.05f, 0.045f, Clamp01(mask * 0.75f * soot));
            }

            return image;
        }

        // ---- drawing helpers ---------------------------------------------------- //

        private static void Dot(PaintImage image, float cx, float cy, float radius, Rgb colour, float alpha)
        {
            if (alpha <= 0f) return;

            int xa = (int)Math.Floor(cx - radius - 1), xb = (int)Math.Ceiling(cx + radius + 1);
            int ya = (int)Math.Floor(cy - radius - 1), yb = (int)Math.Ceiling(cy + radius + 1);

            for (int y = ya; y <= yb; y++)
            for (int x = xa; x <= xb; x++)
            {
                float d = (float)Math.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                image.Blend(x, y, colour.R, colour.G, colour.B, Clamp01(radius - d + 0.5f) * alpha);
            }
        }

        private static void DiscGlow(PaintImage image, float cx, float cy, float radius, float ground,
                                     Rgb colour, float strength)
        {
            int xa = (int)Math.Floor(cx - radius), xb = (int)Math.Ceiling(cx + radius);
            int ya = (int)Math.Max(Math.Floor(ground), Math.Floor(cy - radius));
            int yb = (int)Math.Ceiling(cy + radius);

            for (int y = ya; y <= yb; y++)
            for (int x = xa; x <= xb; x++)
            {
                float d = (float)Math.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) / radius;
                if (d >= 1f) continue;
                image.Blend(x, y, colour.R, colour.G, colour.B, strength * (1f - Smooth(0f, 1f, d)));
            }
        }

        // ---- noise ------------------------------------------------------------------ //

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)(seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        private static float Noise(float x, float y, int seed)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            float fx = x - xi, fy = y - yi;
            float u = fx * fx * (3f - 2f * fx), v = fy * fy * (3f - 2f * fy);

            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
            float c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);

            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        private static float Fbm(float x, float y, int seed, int octaves)
        {
            float sum = 0f, amplitude = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x, y, seed + i * 17) * amplitude;
                norm += amplitude;
                x *= 2.03f;
                y *= 2.03f;
                amplitude *= 0.5f;
            }
            return sum / norm;
        }

        private static float Smooth(float from, float to, float x)
        {
            float t = Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
