using System;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Paints the coin: a minted gold coin with a raised rim, a dished field and
    /// a faceted star, in frames of half a turn, plus the sparkle that glints on
    /// it.
    ///
    /// Kept free of UnityEngine like <see cref="StrikePainter"/>, so the same
    /// pixels can be painted and looked at outside the editor.
    ///
    /// The flat tinted disc it replaces read as a cartoon because nothing about
    /// it behaved like metal. What does, here:
    ///
    ///  * <b>It is a solid.</b> Each frame traces rays against a real disc - two
    ///    faces and a reeded edge - turned about its upright axis, so the turn is
    ///    drawn with the coin's thickness, not faked by squashing a flat sprite.
    ///  * <b>Metal reflects rather than being coloured.</b> Gold has no paint
    ///    colour of its own; it is its surroundings, tinted. Every point reflects
    ///    a studio - bright sky above, a dark floor below, a softbox up and to the
    ///    left - through gold's reflectance, so the relief is drawn by where each
    ///    slope points: facets of the star that face the softbox blaze, the ones
    ///    that face the floor go deep amber.
    ///  * <b>Light stays put while the coin turns,</b> so highlights slide across
    ///    the face and flash on the edge as it passes - which is what reads as a
    ///    turning coin more than the change of shape does.
    ///  * <b>Film, not clipping.</b> Highlights are rolled off to pale gold, and
    ///    recesses beside the rim and star are shaded, as a photograph would.
    ///
    /// Relief is kept bold - a rim, a star, a shallow dish - because the coin is
    /// about fifty pixels across in play, where finer engraving would only
    /// shimmer.
    /// </summary>
    public static class CoinPainter
    {
        public const int FrameSize = 96;

        /// <summary>96 pixels over 128 a unit: a 0.71 coin in a 0.75 frame, the
        /// size of the coin it replaced.</summary>
        public const int PixelsPerUnit = 128;

        /// <summary>Frames of half a turn. Both faces are struck alike, so half a
        /// turn repeats as a whole one.</summary>
        public const int SpinFrames = 16;

        public const int GlintSize = 64;

        /// <summary>The coin's radius, as a share of half the frame.</summary>
        public const float Radius = 0.94f;

        private const float HalfThickness = 0.075f;
        private const int Samples = 4;
        private const int Reeds = 72;

        // Relief, in units of the radius.
        private const float RimInner = 0.84f;
        private const float RimHeight = 0.10f;
        private const float StarRadius = 0.58f;
        private const float StarInner = 0.42f;
        private const float StarHeight = 0.085f;
        private const float StarBevel = 0.22f;

        /// <summary>Deeper than measured gold, and a warm studio: the levels are
        /// pale daylight, and a true-to-life gold against them read as pale khaki.</summary>
        private static readonly V3 Gold = new V3(1.00f, 0.58f, 0.12f);

        // The studio.
        private static readonly V3 Zenith = new V3(1.10f, 1.00f, 0.86f);
        private static readonly V3 Horizon = new V3(0.46f, 0.40f, 0.32f);
        private static readonly V3 Floor = new V3(0.05f, 0.035f, 0.02f);
        private static readonly V3 KeyDirection = Normalize(new V3(-0.50f, 0.62f, 0.60f));
        private static readonly V3 FillDirection = Normalize(new V3(0.75f, 0.15f, 0.65f));

        // ================================================================= //

        /// <summary>One frame of the turn: 0 face-on, <see cref="SpinFrames"/>/2
        /// edge-on.</summary>
        public static PaintImage Coin(int size, int frame)
        {
            var image = new PaintImage(size, size);
            double angle = Math.PI * frame / SpinFrames;
            float sin = (float)Math.Sin(angle), cos = (float)Math.Cos(angle);

            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float r = 0f, g = 0f, b = 0f;
                int hits = 0;

                for (int sy = 0; sy < Samples; sy++)
                for (int sx = 0; sx < Samples; sx++)
                {
                    float x = (px + (sx + 0.5f) / Samples) / size * 2f - 1f;
                    float y = (py + (sy + 0.5f) / Samples) / size * 2f - 1f;
                    if (!Shade(x, y, sin, cos, out V3 colour)) continue;

                    r += colour.X;
                    g += colour.Y;
                    b += colour.Z;
                    hits++;
                }

                if (hits == 0) continue;

                int i = (py * size + px) * 4;
                image.Rgba[i] = r / hits;
                image.Rgba[i + 1] = g / hits;
                image.Rgba[i + 2] = b / hits;
                image.Rgba[i + 3] = hits / (float)(Samples * Samples);
            }

            return image;
        }

        /// <summary>
        /// A four-point sparkle with shorter diagonals and a soft core: the shape
        /// a camera draws around a point of light too bright for it.
        /// </summary>
        public static PaintImage Glint(int size)
        {
            var image = new PaintImage(size, size);
            const int n = 3;

            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float sum = 0f;

                for (int sy = 0; sy < n; sy++)
                for (int sx = 0; sx < n; sx++)
                {
                    float x = (px + (sx + 0.5f) / n) / size * 2f - 1f;
                    float y = (py + (sy + 0.5f) / n) / size * 2f - 1f;

                    float light = Math.Max(Streak(x, y), Streak(y, x));
                    float a = (x + y) * 0.7071f, c = (x - y) * 0.7071f;
                    light = Math.Max(light, 0.55f * Math.Max(Streak(a * 1.8f, c), Streak(c * 1.8f, a)));
                    light += 1.0f * (float)Math.Exp(-(x * x + y * y) / 0.05f);

                    sum += Clamp01(light);
                }

                int i = (py * size + px) * 4;
                image.Rgba[i] = 1.00f;
                image.Rgba[i + 1] = 0.97f;
                image.Rgba[i + 2] = 0.86f;
                image.Rgba[i + 3] = sum / (n * n);
            }

            return image;
        }

        // ================================================================= //

        /// <summary>
        /// The colour of the coin along one ray, looking straight in along -z at
        /// (x, y). The coin sits at the origin, its faces at z = ±HalfThickness in
        /// its own space, turned by the angle about y. False when the ray misses.
        /// </summary>
        private static bool Shade(float x, float y, float sin, float cos, out V3 colour)
        {
            colour = default;

            // The ray, in the coin's own space.
            float ox = x * cos - 10f * sin, oz = x * sin + 10f * cos;
            float dx = sin, dz = -cos;

            float R = Radius, H = HalfThickness;
            float best = float.MaxValue;
            int kind = 0; // 1 front face, 2 back face, 3 edge

            if (Math.Abs(dz) > 1e-5f)
            {
                foreach (float face in new[] { H, -H })
                {
                    float t = (face - oz) / dz;
                    float hx = ox + dx * t;
                    if (t > 0f && t < best && hx * hx + y * y <= R * R)
                    {
                        best = t;
                        kind = face > 0f ? 1 : 2;
                    }
                }
            }

            if (Math.Abs(dx) > 1e-5f && y * y < R * R)
            {
                float half = (float)Math.Sqrt(R * R - y * y);
                foreach (float side in new[] { half, -half })
                {
                    float t = (side - ox) / dx;
                    float hz = oz + dz * t;
                    if (t > 0f && t < best && Math.Abs(hz) <= H)
                    {
                        best = t;
                        kind = 3;
                    }
                }
            }

            if (kind == 0) return false;

            float lx = ox + dx * best, lz = oz + dz * best;
            V3 normal;
            float occlusion;
            V3 reflectance = Gold;

            if (kind == 3)
            {
                // The reeded edge: ridges round the rim, and a small chamfer where
                // it meets each face.
                float nx = lx / R, ny = y / R;
                float ridge = (float)Math.Sin(Math.Atan2(ny, nx) * Reeds);
                normal = Normalize(new V3(nx - ny * 0.45f * ridge, ny + nx * 0.45f * ridge, 0f));

                float chamfer = Smooth(0.6f, 1f, Math.Abs(lz) / H);
                normal = Normalize(normal + new V3(0f, 0f, Math.Sign(lz) * chamfer * 0.8f));

                occlusion = 0.75f + 0.2f * (0.5f + 0.5f * ridge);
                reflectance = Gold * 0.85f;
            }
            else
            {
                float u = lx / R, v = y / R;
                const float e = 0.006f;
                float hu = (Height(u + e, v) - Height(u - e, v)) / (2f * e);
                float hv = (Height(u, v + e) - Height(u, v - e)) / (2f * e);
                normal = Normalize(new V3(-hu, -hv, kind == 1 ? 1f : -1f));
                occlusion = Occlusion(u, v);
            }

            // To the viewer's space.
            var n = new V3(normal.X * cos + normal.Z * sin, normal.Y, -normal.X * sin + normal.Z * cos);
            float facing = Clamp01(n.Z);

            // Straight-on view: the reflection of (0, 0, 1) about the normal.
            var reflected = new V3(2f * n.Z * n.X, 2f * n.Z * n.Y, 2f * n.Z * n.Z - 1f);

            float grazing = (float)Math.Pow(1f - facing, 5f);
            var fresnel = new V3(reflectance.X + (1f - reflectance.X) * grazing,
                                 reflectance.Y + (1f - reflectance.Y) * grazing,
                                 reflectance.Z + (1f - reflectance.Z) * grazing);

            V3 light = Studio(reflected);
            var linear = new V3(fresnel.X * light.X, fresnel.Y * light.Y, fresnel.Z * light.Z) * occlusion
                         + Gold * 0.03f;

            colour = new V3(Display(linear.X), Display(linear.Y), Display(linear.Z));
            return true;
        }

        /// <summary>Relief of a face at (u, v), in units of the radius.</summary>
        private static float Height(float u, float v)
        {
            float r = (float)Math.Sqrt(u * u + v * v);

            // A shallow dish, so the field is not one flat colour.
            float field = -0.035f * (1f - r * r);

            float rise = Smooth(RimInner - 0.08f, RimInner, r);
            float round = Smooth(0.92f, 1.0f, r);
            float rim = RimHeight * rise - RimHeight * 0.8f * round * round;

            // Pyramids along the arms: height grows with distance inside the
            // outline, so each arm has a ridge down its middle and two facets.
            float star = StarHeight * Clamp01(-Star(u, v) / StarBevel) + field * 0.3f;

            return Math.Max(field, Math.Max(rim, star));
        }

        /// <summary>Darkening in the field where it meets the star and the rim.</summary>
        private static float Occlusion(float u, float v)
        {
            float r = (float)Math.Sqrt(u * u + v * v);
            float occlusion = 1f;

            float outside = Star(u, v);
            if (outside > 0f) occlusion *= 1f - 0.35f * (float)Math.Exp(-outside / 0.045f);

            float inside = RimInner - 0.08f - r;
            if (inside > 0f) occlusion *= 1f - 0.30f * (float)Math.Exp(-inside / 0.05f);

            // The rounded outer edge turns away into shadow, which gives the coin
            // a dark contour to stand out from a pale sky by.
            occlusion *= 1f - 0.45f * Smooth(0.95f, 1f, r);

            return occlusion;
        }

        /// <summary>Signed distance to a five-point star, point up; negative inside.</summary>
        private static float Star(float px, float py)
        {
            const float k1x = 0.809016994f, k1y = -0.587785252f;
            const float k2x = -k1x, k2y = k1y;

            px = Math.Abs(px);
            float d1 = Math.Max(k1x * px + k1y * py, 0f);
            px -= 2f * d1 * k1x;
            py -= 2f * d1 * k1y;
            float d2 = Math.Max(k2x * px + k2y * py, 0f);
            px -= 2f * d2 * k2x;
            py -= 2f * d2 * k2y;
            px = Math.Abs(px);
            py -= StarRadius;

            float bax = StarInner * -k1y, bay = StarInner * k1x - 1f;
            float h = Math.Max(0f, Math.Min(StarRadius, (px * bax + py * bay) / (bax * bax + bay * bay)));
            float lx = px - bax * h, ly = py - bay * h;
            return (float)Math.Sqrt(lx * lx + ly * ly) * Math.Sign(py * bax - px * bay);
        }

        /// <summary>What a direction sees: sky above, floor below, a softbox up
        /// and to the left and a weaker fill from the right.</summary>
        private static V3 Studio(V3 d)
        {
            V3 ambient = d.Y >= 0f
                ? Lerp(Horizon, Zenith, (float)Math.Pow(d.Y, 0.7f))
                : Lerp(Horizon, Floor, Clamp01(-d.Y * 1.8f));

            float key = Math.Max(0f, Dot(d, KeyDirection));
            float fill = Math.Max(0f, Dot(d, FillDirection));

            float boost = (float)Math.Pow(key, 30f) * 3.5f
                        + (float)Math.Pow(key, 600f) * 12f
                        + (float)Math.Pow(fill, 8f) * 0.6f;

            return ambient + new V3(1.00f, 0.96f, 0.88f) * boost;
        }

        /// <summary>Filmic roll-off, then to display gamma.</summary>
        private static float Display(float x)
        {
            x = Math.Max(0f, x);
            float mapped = x * (2.51f * x + 0.03f) / (x * (2.43f * x + 0.59f) + 0.14f);
            return (float)Math.Pow(Clamp01(mapped), 1f / 2.2f);
        }

        private static float Streak(float along, float across)
        {
            float t = 1f - Math.Abs(along);
            if (t <= 0f) return 0f;
            // Wide enough to survive being drawn at about thirty pixels across.
            float width = 0.03f + 0.07f * t * t;
            return t * t * (float)Math.Exp(-Math.Abs(across) / width);
        }

        // ---- maths ----------------------------------------------------------- //

        private struct V3
        {
            public float X, Y, Z;

            public V3(float x, float y, float z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static V3 operator *(V3 a, float k) => new V3(a.X * k, a.Y * k, a.Z * k);
        }

        private static float Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        private static V3 Normalize(V3 v)
        {
            float length = (float)Math.Sqrt(Dot(v, v));
            return length > 1e-6f ? v * (1f / length) : new V3(0f, 0f, 1f);
        }

        private static V3 Lerp(V3 a, V3 b, float t) =>
            new V3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

        private static float Smooth(float from, float to, float x)
        {
            float t = Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
