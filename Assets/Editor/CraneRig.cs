using System;
using System.Collections.Generic;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Takes the crane apart: the frames in Art/MovingObstacles/Crane are one
    /// swing of the load, drawn on a shared canvas, with the tower and jib
    /// pixel-identical in every frame. This separates the two, and measures the
    /// swing well enough to play it back at any frame rate.
    ///
    /// <b>Why split it at all.</b> Played as a flipbook the crane is 24 drawings
    /// of a heavy load swinging over about four seconds - under six a second,
    /// and it steps. The drawings swing about one pivot: the load's centre stays
    /// the same distance from it in every frame, to within about a pixel, and the
    /// frames sample a sine of the angle at even steps in time. So the load can
    /// be shown as the nearest drawing, turned about that pivot by the few
    /// degrees between the drawing and the true angle at this instant. Nothing
    /// is invented - every pixel on screen is the artist's - but the swing moves
    /// every frame the game renders, and the switch from one drawing to the next
    /// lands on nearly the same pose.
    ///
    /// <b>The crate is measured frame by frame.</b> It is not fixed to the rope:
    /// the rope is drawn at the full swing angle, while the crate hangs on its
    /// slings and tilts less, the way a real one lags. A collider carried rigidly
    /// with the rope ends up well outside the crate at the ends of the swing, so
    /// each drawing gets its own crate centre and tilt.
    ///
    /// <b>Why here and not in the importer.</b> No UnityEngine types, so the same
    /// code can be run and checked outside the editor.
    ///
    /// Images are RGBA, one byte a channel, bottom row first - as
    /// Texture2D.GetPixels32 hands them over - and every measurement below is in
    /// those pixel coordinates, y up. Angles are degrees, anticlockwise positive
    /// - the same sense as a Unity z rotation.
    /// </summary>
    public sealed class CraneRig
    {
        /// <summary>Channels within this of each other are the same pixel.</summary>
        private const int Tolerance = 6;

        /// <summary>Alpha at or above this counts as solid for measuring.</summary>
        private const byte SolidAlpha = 128;

        /// <summary>
        /// How far the load's silhouette is shaved in to find the crate, as a
        /// share of the load's size (the square root of its solid pixels). About
        /// nine pixels on this art: the rope and slings are three to five wide and
        /// vanish, the hook is under thirty and shrinks to a scrap, and the crate,
        /// over seventy, keeps most of itself.
        /// </summary>
        private const float CrateShave = 0.1f;

        /// <summary>Tilts tried when fitting the crate's box, either side of square.</summary>
        private const float MaxCrateTilt = 45f;
        private const float CrateTiltStep = 0.5f;

        public int Width { get; private set; }
        public int Height { get; private set; }

        /// <summary>The tower and jib: what every frame has in common.</summary>
        public byte[] Tower { get; private set; }

        /// <summary>Per frame, what swings: rope, hook, slings and crate.</summary>
        public byte[][] Loads { get; private set; }

        /// <summary>The point the load swings about, in pixels.</summary>
        public float PivotX { get; private set; }
        public float PivotY { get; private set; }

        /// <summary>Per frame, the swing angle. Positive carries the load towards +x.</summary>
        public float[] Angles { get; private set; }

        /// <summary>Per frame, the centre of the crate's box as drawn, in pixels.</summary>
        public float[] CrateX { get; private set; }
        public float[] CrateY { get; private set; }

        /// <summary>Per frame, how far the crate's box is turned as drawn.</summary>
        public float[] CrateTilt { get; private set; }

        /// <summary>The crate's box, square-on: the median over the frames.</summary>
        public float CrateWidth { get; private set; }
        public float CrateHeight { get; private set; }

        /// <summary>The top of the mast: the tower's highest solid row.</summary>
        public int Top { get; private set; }

        /// <summary>The foot of the tower: its lowest solid row, and the span of
        /// the base plate across.</summary>
        public int BaseBottom { get; private set; }
        public int BaseLeft { get; private set; }
        public int BaseRight { get; private set; }

        /// <summary>Radius of the swing, and how far the fit strays from it
        /// across the frames - a check that the drawings really do swing about
        /// one point.</summary>
        public float Radius { get; private set; }
        public float RadiusSpread { get; private set; }

        public float Amplitude
        {
            get
            {
                float max = 0f;
                foreach (float angle in Angles) max = Math.Max(max, Math.Abs(angle));
                return max;
            }
        }

        // ================================================================= //

        public static CraneRig Split(IList<byte[]> frames, int width, int height)
        {
            if (frames == null || frames.Count < 3)
                throw new ArgumentException("A swing needs at least three frames.");

            int count = frames.Count;
            int pixels = width * height;
            foreach (byte[] frame in frames)
                if (frame == null || frame.Length != pixels * 4)
                    throw new ArgumentException("Every frame must be the same size.");

            var rig = new CraneRig { Width = width, Height = height };

            // ---- the tower: per pixel, the value most frames agree on ---------
            //
            // Majority rather than "identical everywhere", because where the rope
            // crosses the trolley a few frames differ from the rest, and the
            // trolley must still be kept whole.
            var tower = new byte[pixels * 4];
            for (int p = 0; p < pixels; p++)
            {
                int i = p * 4;
                int best = 0, bestVotes = -1;

                for (int a = 0; a < count; a++)
                {
                    int votes = 0;
                    for (int b = 0; b < count; b++)
                        if (Same(frames[a], frames[b], i)) votes++;

                    if (votes > bestVotes) { bestVotes = votes; best = a; }
                    if (votes * 2 > count) break;
                }

                Buffer.BlockCopy(frames[best], i, tower, i, 4);
            }

            rig.Tower = tower;

            // ---- the loads: whatever each frame has that the tower does not ---
            rig.Loads = new byte[count][];
            var centreX = new double[count];
            var centreY = new double[count];

            for (int k = 0; k < count; k++)
            {
                byte[] frame = frames[k];
                var load = new byte[pixels * 4];
                double sumX = 0, sumY = 0;
                long solid = 0;

                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    if (Same(frame, tower, i)) continue;

                    Buffer.BlockCopy(frame, i, load, i, 4);
                    if (frame[i + 3] < SolidAlpha) continue;

                    sumX += x;
                    sumY += y;
                    solid++;
                }

                if (solid == 0)
                    throw new InvalidOperationException($"Frame {k + 1} has nothing swinging in it.");

                rig.Loads[k] = load;
                centreX[k] = sumX / solid;
                centreY[k] = sumY / solid;
            }

            // ---- the pivot: the circle the load's centre travels on -----------
            FitCircle(centreX, centreY, out double pivotX, out double pivotY);
            rig.PivotX = (float)pivotX;
            rig.PivotY = (float)pivotY;

            rig.Angles = new float[count];
            double minR = double.MaxValue, maxR = 0, sumR = 0;

            for (int k = 0; k < count; k++)
            {
                double dx = centreX[k] - pivotX;
                double dy = pivotY - centreY[k];
                double r = Math.Sqrt(dx * dx + dy * dy);

                rig.Angles[k] = (float)(Math.Atan2(dx, dy) * 180.0 / Math.PI);
                minR = Math.Min(minR, r);
                maxR = Math.Max(maxR, r);
                sumR += r;
            }

            rig.Radius = (float)(sumR / count);
            rig.RadiusSpread = (float)(maxR - minR);

            rig.MeasureCrates();
            rig.MeasureTower();
            return rig;
        }

        // ================================================================= //

        private static bool Same(byte[] a, byte[] b, int i)
        {
            // Fully transparent is the same pixel whatever colour it hides.
            if (a[i + 3] == 0 && b[i + 3] == 0) return true;

            return Math.Abs(a[i] - b[i]) <= Tolerance &&
                   Math.Abs(a[i + 1] - b[i + 1]) <= Tolerance &&
                   Math.Abs(a[i + 2] - b[i + 2]) <= Tolerance &&
                   Math.Abs(a[i + 3] - b[i + 3]) <= Tolerance;
        }

        /// <summary>
        /// Least-squares circle through the points (Kasa's method). Falls back to
        /// straight above the mean when the points are too nearly in a line to
        /// place a centre - a swing drawn with almost no travel.
        /// </summary>
        private static void FitCircle(double[] xs, double[] ys, out double cx, out double cy)
        {
            int n = xs.Length;
            double mx = 0, my = 0;
            for (int i = 0; i < n; i++) { mx += xs[i]; my += ys[i]; }
            mx /= n;
            my /= n;

            double suu = 0, svv = 0, suv = 0, suuu = 0, svvv = 0, suvv = 0, svuu = 0;
            for (int i = 0; i < n; i++)
            {
                double u = xs[i] - mx, v = ys[i] - my;
                suu += u * u; svv += v * v; suv += u * v;
                suuu += u * u * u; svvv += v * v * v;
                suvv += u * v * v; svuu += v * u * u;
            }

            double det = suu * svv - suv * suv;
            if (Math.Abs(det) < 1e-6 * Math.Max(1.0, suu * svv))
            {
                cx = mx;
                cy = my + Math.Sqrt(suu / n) * 8.0;
                return;
            }

            double a = (suuu + suvv) * 0.5, b = (svvv + svuu) * 0.5;
            cx = (a * svv - b * suv) / det + mx;
            cy = (b * suu - a * suv) / det + my;
        }

        /// <summary>
        /// Per frame, the crate's box: which solid pixels are the crate, then the
        /// smallest rectangle around them over a range of tilts.
        ///
        /// The crate is told from the rope, hook and slings by thickness. The
        /// load's silhouette is shaved in on every side: everything thin is gone,
        /// the hook is left as a scrap, and the biggest piece that remains is the
        /// middle of the crate. Grown back out by the same amount - but only onto
        /// pixels the load actually has - it is the crate, and nothing touching it.
        ///
        /// Not by distance from the pivot, which was tried: in the drawings at the
        /// ends of the swing the hook hangs beside the crate's top corner, as far
        /// from the pivot as the crate is, and was taken for part of it.
        ///
        /// The smallest rectangle rather than the principal axes, because a crate
        /// is close to square, and a square has no principal axis to find.
        /// </summary>
        private void MeasureCrates()
        {
            int count = Loads.Length;
            CrateX = new float[count];
            CrateY = new float[count];
            CrateTilt = new float[count];
            var widths = new List<float>();
            var heights = new List<float>();

            for (int k = 0; k < count; k++)
            {
                byte[] load = Loads[k];

                var solid = new bool[Width * Height];
                int solidCount = 0;
                for (int p = 0; p < solid.Length; p++)
                {
                    solid[p] = load[p * 4 + 3] >= SolidAlpha;
                    if (solid[p]) solidCount++;
                }

                int shave = Math.Max(2, (int)Math.Round(CrateShave * Math.Sqrt(solidCount)));

                bool[] core = Erode(solid, shave);
                bool[] biggest = LargestPiece(core);
                bool[] grown = Dilate(biggest, shave);

                var crateX = new List<float>();
                var crateY = new List<float>();
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int p = y * Width + x;
                    if (!grown[p] || !solid[p]) continue;
                    crateX.Add(x);
                    crateY.Add(y);
                }

                // Nothing survived the shave - a load with no bulk to it. Fall
                // back to the whole load, which at least puts the box on it.
                if (crateX.Count == 0)
                {
                    for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                    {
                        if (!solid[y * Width + x]) continue;
                        crateX.Add(x);
                        crateY.Add(y);
                    }
                }

                FitBox(crateX, crateY, out float cx, out float cy, out float tilt, out float w, out float h);
                CrateX[k] = cx;
                CrateY[k] = cy;
                CrateTilt[k] = tilt;
                widths.Add(w);
                heights.Add(h);
            }

            CrateWidth = Median(widths);
            CrateHeight = Median(heights);
        }

        /// <summary>Keeps a pixel only if the whole square of the given radius
        /// around it is set. Rows then columns, so the cost does not grow with
        /// the radius squared.</summary>
        private bool[] Erode(bool[] mask, int radius) => Square(mask, radius, erode: true);

        /// <summary>Sets a pixel if anything in the square of the given radius
        /// around it is set.</summary>
        private bool[] Dilate(bool[] mask, int radius) => Square(mask, radius, erode: false);

        private bool[] Square(bool[] mask, int radius, bool erode)
        {
            var across = new bool[mask.Length];
            var result = new bool[mask.Length];

            // Running count of set pixels in the window, along rows.
            for (int y = 0; y < Height; y++)
            {
                int set = 0;
                for (int x = -radius; x < Width; x++)
                {
                    int enter = x + radius, leave = x - radius - 1;
                    if (enter < Width && mask[y * Width + enter]) set++;
                    if (leave >= 0 && mask[y * Width + leave]) set--;
                    if (x < 0) continue;

                    int span = Math.Min(Width - 1, x + radius) - Math.Max(0, x - radius) + 1;
                    across[y * Width + x] = erode ? set == span && span == 2 * radius + 1 : set > 0;
                }
            }

            // Then down columns.
            for (int x = 0; x < Width; x++)
            {
                int set = 0;
                for (int y = -radius; y < Height; y++)
                {
                    int enter = y + radius, leave = y - radius - 1;
                    if (enter < Height && across[enter * Width + x]) set++;
                    if (leave >= 0 && across[leave * Width + x]) set--;
                    if (y < 0) continue;

                    int span = Math.Min(Height - 1, y + radius) - Math.Max(0, y - radius) + 1;
                    result[y * Width + x] = erode ? set == span && span == 2 * radius + 1 : set > 0;
                }
            }

            return result;
        }

        /// <summary>The biggest 4-connected piece of the mask.</summary>
        private bool[] LargestPiece(bool[] mask)
        {
            var label = new int[mask.Length];
            var stack = new Stack<int>();
            int bestLabel = 0, bestSize = 0, next = 0;

            for (int start = 0; start < mask.Length; start++)
            {
                if (!mask[start] || label[start] != 0) continue;

                next++;
                int size = 0;
                label[start] = next;
                stack.Push(start);

                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    size++;
                    int x = p % Width, y = p / Width;

                    if (x > 0) Visit(p - 1);
                    if (x < Width - 1) Visit(p + 1);
                    if (y > 0) Visit(p - Width);
                    if (y < Height - 1) Visit(p + Width);
                }

                if (size > bestSize)
                {
                    bestSize = size;
                    bestLabel = next;
                }

                void Visit(int q)
                {
                    if (!mask[q] || label[q] != 0) return;
                    label[q] = next;
                    stack.Push(q);
                }
            }

            var result = new bool[mask.Length];
            for (int p = 0; p < mask.Length; p++) result[p] = bestLabel != 0 && label[p] == bestLabel;
            return result;
        }

        /// <summary>The smallest-area rectangle around the points, tilted by up to
        /// <see cref="MaxCrateTilt"/> either way.</summary>
        private static void FitBox(List<float> xs, List<float> ys,
                                   out float centreX, out float centreY, out float tilt,
                                   out float width, out float height)
        {
            double bestArea = double.MaxValue;
            centreX = centreY = tilt = width = height = 0f;
            if (xs.Count == 0) return;

            for (float degrees = -MaxCrateTilt; degrees <= MaxCrateTilt; degrees += CrateTiltStep)
            {
                double a = degrees * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
                double uMin = double.MaxValue, uMax = double.MinValue;
                double vMin = double.MaxValue, vMax = double.MinValue;

                for (int i = 0; i < xs.Count; i++)
                {
                    double u = xs[i] * c + ys[i] * s;
                    double v = -xs[i] * s + ys[i] * c;
                    if (u < uMin) uMin = u;
                    if (u > uMax) uMax = u;
                    if (v < vMin) vMin = v;
                    if (v > vMax) vMax = v;
                }

                double area = (uMax - uMin + 1) * (vMax - vMin + 1);
                if (area >= bestArea) continue;

                bestArea = area;
                double uMid = (uMin + uMax) * 0.5, vMid = (vMin + vMax) * 0.5;
                centreX = (float)(uMid * c - vMid * s);
                centreY = (float)(uMid * s + vMid * c);
                tilt = degrees;
                width = (float)(uMax - uMin + 1);
                height = (float)(vMax - vMin + 1);
            }
        }

        private static float Median(List<float> values)
        {
            var sorted = new List<float>(values);
            sorted.Sort();
            int n = sorted.Count;
            if (n == 0) return 0f;
            return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) * 0.5f;
        }

        /// <summary>The tower's extent: the top of the mast, the lowest solid row,
        /// and the base plate's span.</summary>
        private void MeasureTower()
        {
            int lowest = -1;
            for (int y = 0; y < Height && lowest < 0; y++)
            for (int x = 0; x < Width; x++)
                if (Tower[(y * Width + x) * 4 + 3] >= SolidAlpha) { lowest = y; break; }

            BaseBottom = Math.Max(0, lowest);

            int highest = -1;
            for (int y = Height - 1; y >= 0 && highest < 0; y--)
            for (int x = 0; x < Width; x++)
                if (Tower[(y * Width + x) * 4 + 3] >= SolidAlpha) { highest = y; break; }

            Top = highest >= 0 ? highest : Height - 1;

            // The plate is the bottom few rows; the lattice above it is narrower.
            int band = Math.Max(2, Height / 40);
            int left = Width, right = -1;
            for (int y = BaseBottom; y < Math.Min(Height, BaseBottom + band); y++)
            for (int x = 0; x < Width; x++)
            {
                if (Tower[(y * Width + x) * 4 + 3] < SolidAlpha) continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }

            BaseLeft = right >= 0 ? left : 0;
            BaseRight = right >= 0 ? right : Width - 1;
        }
    }
}
