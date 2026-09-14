using System;
using System.Collections.Generic;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Measures the slam press frames in Art/MovingObstacles/presser: where the
    /// machine stands, where its head is in each frame, and how long each frame
    /// should be shown.
    ///
    /// <b>What the frames are.</b> One cycle of the press: the head waiting at
    /// the top (with the warning lamp lit on the last of those frames), the
    /// slam, the impact and dust, the hold at the bottom, and the hydraulic
    /// rise back up. They are not all the same size - the lamp's glow makes two
    /// of them taller at the top - so frames are lined up by the foot of the
    /// machine, not by the canvas.
    ///
    /// <b>Why each frame gets its own duration.</b> Shown at one rate, a slam
    /// drawn over three frames takes as long as a rise drawn over six, which is
    /// backwards for a press: it drops in a blink and climbs slowly. So frames are
    /// sorted into waiting, slamming, holding and rising by where the head is, and
    /// each part gets its own time, shared out by how far the head moves in each
    /// frame - which keeps the head's speed even within the part.
    ///
    /// <b>Why here and not in the importer.</b> No UnityEngine types, so the same
    /// code can be run and checked outside the editor.
    ///
    /// Images are RGBA, one byte a channel, bottom row first - as
    /// Texture2D.GetPixels32 hands them over. Every measurement is in pixels, y up,
    /// relative to the frame's anchor: the middle of the top of the base plate,
    /// which is the point that stands on the roof.
    /// </summary>
    public sealed class PressRig
    {
        private const byte SolidAlpha = 128;

        /// <summary>The head block is dark steel. Motion-blur ghosts are dark but
        /// see-through, and are left out by asking for nearly opaque pixels.</summary>
        private const byte OpaqueAlpha = 200;
        private const int DarkLevel = 90;

        /// <summary>A row belongs to the base plate while at least this share of
        /// the machine's width is dark along it.</summary>
        private const float PlateRowShare = 0.5f;

        /// <summary>A head within this share of its travel of its highest or lowest
        /// point counts as up or down. Not a few pixels: the waiting head is not
        /// drawn at one height - it lifts a little with the warning lamp, winding
        /// up for the slam - and those frames are still waiting.</summary>
        private const float RestShare = 0.1f;

        public int FrameCount { get; private set; }

        /// <summary>Per frame, the anchor in that frame's own pixels (bottom-up).</summary>
        public float[] AnchorX { get; private set; }
        public float[] AnchorY { get; private set; }

        /// <summary>Per frame, the head block's box, relative to the anchor.</summary>
        public float[] HeadLeft { get; private set; }
        public float[] HeadRight { get; private set; }
        public float[] HeadBottom { get; private set; }
        public float[] HeadTop { get; private set; }

        /// <summary>The piston rod, relative to the anchor: its span across, and
        /// where it enters the clamp under the top beam.</summary>
        public float RodLeft { get; private set; }
        public float RodRight { get; private set; }
        public float RodTop { get; private set; }

        /// <summary>The base plate's thickness, below the anchor.</summary>
        public float PlateThickness { get; private set; }

        /// <summary>The machine's extent, relative to the anchor: the top of the
        /// lamp, and the span across the plate.</summary>
        public float MachineTop { get; private set; }
        public float MachineLeft { get; private set; }
        public float MachineRight { get; private set; }

        /// <summary>The inside faces of the two posts, relative to the anchor.</summary>
        public float InnerLeft { get; private set; }
        public float InnerRight { get; private set; }

        /// <summary>The head's lowest point at the top of its travel, and at the
        /// bottom: the gap under the waiting head is HeadUp.</summary>
        public float HeadUp { get; private set; }
        public float HeadDown { get; private set; }

        /// <summary>Per frame, which part of the cycle it belongs to.</summary>
        public Part[] Parts { get; private set; }

        public enum Part { Waiting, Slamming, Holding, Rising }

        // ================================================================= //

        public static PressRig Measure(IList<byte[]> frames, IList<int> widths, IList<int> heights)
        {
            if (frames == null || frames.Count < 4)
                throw new ArgumentException("A press cycle needs at least four frames.");

            int count = frames.Count;
            var rig = new PressRig
            {
                FrameCount = count,
                AnchorX = new float[count],
                AnchorY = new float[count],
                HeadLeft = new float[count],
                HeadRight = new float[count],
                HeadBottom = new float[count],
                HeadTop = new float[count],
                Parts = new Part[count]
            };

            // ---- the machine, measured on the first frame ----------------------
            byte[] first = frames[0];
            int w0 = widths[0], h0 = heights[0];

            int foot0 = LowestSolidRow(first, w0, h0);
            if (foot0 < 0) throw new InvalidOperationException("The first press frame is empty.");

            // Plate: dark rows from the foot up.
            int machineLeft = w0, machineRight = -1;
            for (int y = foot0; y < Math.Min(h0, foot0 + 4); y++)
            for (int x = 0; x < w0; x++)
            {
                if (first[(y * w0 + x) * 4 + 3] < SolidAlpha) continue;
                machineLeft = Math.Min(machineLeft, x);
                machineRight = Math.Max(machineRight, x);
            }

            int plateTopRow = foot0;
            int machineWidth = Math.Max(1, machineRight - machineLeft + 1);
            for (int y = foot0; y < h0; y++)
            {
                int dark = 0;
                for (int x = 0; x < w0; x++)
                    if (IsDark(first, (y * w0 + x) * 4)) dark++;

                if (dark < machineWidth * PlateRowShare) break;
                plateTopRow = y;
            }

            float anchorX0 = (machineLeft + machineRight) * 0.5f;
            float anchorY0 = plateTopRow + 1;
            rig.PlateThickness = anchorY0 - foot0;
            rig.MachineLeft = machineLeft - anchorX0;
            rig.MachineRight = machineRight + 1 - anchorX0;
            rig.MachineTop = HighestSolidRow(first, w0, h0) + 1 - anchorY0;

            // Posts: the solid runs either side of the middle, just above the plate.
            int gapRow = Math.Min(h0 - 1, plateTopRow + 4);
            int innerLeft = 0, innerRight = w0 - 1;
            for (int x = (int)anchorX0; x >= 0; x--)
                if (first[(gapRow * w0 + x) * 4 + 3] >= SolidAlpha) { innerLeft = x + 1; break; }
            for (int x = (int)anchorX0; x < w0; x++)
                if (first[(gapRow * w0 + x) * 4 + 3] >= SolidAlpha) { innerRight = x - 1; break; }

            rig.InnerLeft = innerLeft - anchorX0;
            rig.InnerRight = innerRight + 1 - anchorX0;

            // ---- per frame: anchor and head ------------------------------------
            for (int k = 0; k < count; k++)
            {
                byte[] frame = frames[k];
                int w = widths[k], h = heights[k];

                int foot = LowestSolidRow(frame, w, h);
                if (foot < 0) throw new InvalidOperationException($"Press frame {k + 1} is empty.");

                // Frames differ in height at the top, not across: line them up by
                // the foot, and keep the same position across the canvas.
                float anchorX = anchorX0 + (w - w0) * 0.5f;
                float anchorY = foot + rig.PlateThickness;
                rig.AnchorX[k] = anchorX;
                rig.AnchorY[k] = anchorY;

                int left = (int)Math.Round(anchorX + rig.InnerLeft);
                int right = (int)Math.Round(anchorX + rig.InnerRight) - 1;
                int bottom = (int)Math.Round(anchorY);
                int top = Math.Min(h - 1, (int)Math.Round(anchorY + rig.MachineTop));

                bool[] piece = LargestDarkPiece(frame, w, h, left, right, bottom, top,
                                                out int bx0, out int by0, out int bx1, out int by1);
                if (piece == null)
                    throw new InvalidOperationException($"No press head found in frame {k + 1}.");

                rig.HeadLeft[k] = bx0 - anchorX;
                rig.HeadRight[k] = bx1 + 1 - anchorX;
                rig.HeadBottom[k] = by0 - anchorY;
                rig.HeadTop[k] = by1 + 1 - anchorY;
            }

            rig.HeadUp = float.MinValue;
            rig.HeadDown = float.MaxValue;
            for (int k = 0; k < count; k++)
            {
                rig.HeadUp = Math.Max(rig.HeadUp, rig.HeadBottom[k]);
                rig.HeadDown = Math.Min(rig.HeadDown, rig.HeadBottom[k]);
            }

            rig.MeasureRod(first, w0, h0, anchorX0, anchorY0);
            rig.Classify();
            return rig;
        }

        // ================================================================= //

        /// <summary>
        /// Seconds to show each frame, given how long each part of the cycle
        /// takes. The frames of a part share its time evenly.
        ///
        /// Evenly, and not by how far the head moves in each frame, because the
        /// artist has already drawn the speed: the rise takes small steps at its
        /// ends and big ones in the middle, and the slam's steps grow as it falls.
        /// Timed evenly, that easing and that acceleration are what plays. Timed by
        /// distance, both would be flattened into a constant speed.
        /// </summary>
        public float[] Durations(float waiting, float slamming, float holding, float rising)
        {
            var durations = new float[FrameCount];
            var members = new int[4];
            foreach (Part part in Parts) members[(int)part]++;

            for (int k = 0; k < FrameCount; k++)
            {
                Part part = Parts[k];
                float total = part == Part.Waiting ? waiting
                            : part == Part.Slamming ? slamming
                            : part == Part.Holding ? holding : rising;
                durations[k] = total / members[(int)part];
            }

            return durations;
        }

        /// <summary>
        /// The moment in the cycle - seconds from the start of the first frame -
        /// that the runner should reach the press: inside the longest stretch of
        /// waiting frames, <paramref name="lead"/> seconds before it ends, so the
        /// head has long finished rising when the runner goes under and slams just
        /// after they are through. A stretch too short for that lead gets its
        /// middle instead.
        /// </summary>
        public float ArrivalTime(float[] durations, float lead)
        {
            var starts = new float[FrameCount];
            float cycle = 0f;
            for (int k = 0; k < FrameCount; k++)
            {
                starts[k] = cycle;
                cycle += durations[k];
            }

            // The longest run of waiting frames, allowed to wrap past the last frame.
            // A run starts on a waiting frame whose previous frame is not waiting -
            // or on the first frame, when every frame is.
            bool allWaiting = AllWaiting();
            float bestLength = 0f, bestStart = 0f;
            for (int k = 0; k < FrameCount; k++)
            {
                int previous = (k + FrameCount - 1) % FrameCount;
                bool starts_ = allWaiting ? k == 0
                                          : Parts[k] == Part.Waiting && Parts[previous] != Part.Waiting;
                if (!starts_) continue;

                float length = 0f;
                for (int i = 0; i < FrameCount; i++)
                {
                    int j = (k + i) % FrameCount;
                    if (Parts[j] != Part.Waiting) break;
                    length += durations[j];
                }

                if (length > bestLength)
                {
                    bestLength = length;
                    bestStart = starts[k];
                }
            }

            float into = Math.Max(bestLength * 0.5f, bestLength - lead);
            float arrival = bestStart + into;
            return cycle > 0f ? arrival - (float)Math.Floor(arrival / cycle) * cycle : 0f;
        }

        private bool AllWaiting()
        {
            foreach (Part part in Parts) if (part != Part.Waiting) return false;
            return true;
        }

        // ================================================================= //

        private static bool IsDark(byte[] image, int i)
        {
            if (image[i + 3] < OpaqueAlpha) return false;
            return Math.Max(image[i], Math.Max(image[i + 1], image[i + 2])) < DarkLevel;
        }

        private static int LowestSolidRow(byte[] image, int w, int h)
        {
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (image[(y * w + x) * 4 + 3] >= SolidAlpha) return y;
            return -1;
        }

        private static int HighestSolidRow(byte[] image, int w, int h)
        {
            for (int y = h - 1; y >= 0; y--)
            for (int x = 0; x < w; x++)
                if (image[(y * w + x) * 4 + 3] >= SolidAlpha) return y;
            return -1;
        }

        /// <summary>Dark pixels this close together belong to the same piece. The
        /// impact frame draws bright speed lines straight across the head, and
        /// without bridging them the head falls apart into strips.</summary>
        private const int BridgeRadius = 3;

        /// <summary>
        /// The biggest piece of dark, opaque pixels inside the rectangle, and the
        /// bounds of its dark pixels. Pieces are joined across gaps of up to
        /// <see cref="BridgeRadius"/>, and measured by how many dark pixels they
        /// hold, so the bridging never adds to the size. Null when there is none.
        /// </summary>
        private static bool[] LargestDarkPiece(byte[] image, int w, int h,
                                               int left, int right, int bottom, int top,
                                               out int x0, out int y0, out int x1, out int y1)
        {
            x0 = y0 = x1 = y1 = 0;
            left = Math.Max(0, left);
            right = Math.Min(w - 1, right);
            bottom = Math.Max(0, bottom);
            top = Math.Min(h - 1, top);
            if (left > right || bottom > top) return null;

            var dark = new bool[w * h];
            for (int y = bottom; y <= top; y++)
            for (int x = left; x <= right; x++)
                dark[y * w + x] = IsDark(image, (y * w + x) * 4);

            // Grown by the bridge radius, inside the rectangle only.
            var grown = new bool[w * h];
            for (int y = bottom; y <= top; y++)
            for (int x = left; x <= right; x++)
            {
                if (!dark[y * w + x]) continue;
                for (int gy = Math.Max(bottom, y - BridgeRadius); gy <= Math.Min(top, y + BridgeRadius); gy++)
                for (int gx = Math.Max(left, x - BridgeRadius); gx <= Math.Min(right, x + BridgeRadius); gx++)
                    grown[gy * w + gx] = true;
            }

            var label = new int[w * h];
            var stack = new Stack<int>();
            int next = 0, bestLabel = 0, bestSize = 0;

            for (int sy = bottom; sy <= top; sy++)
            for (int sx = left; sx <= right; sx++)
            {
                int start = sy * w + sx;
                if (label[start] != 0 || !grown[start]) continue;

                next++;
                int size = 0;
                label[start] = next;
                stack.Push(start);

                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    if (dark[p]) size++;
                    int x = p % w, y = p / w;

                    if (x > left) Visit(p - 1);
                    if (x < right) Visit(p + 1);
                    if (y > bottom) Visit(p - w);
                    if (y < top) Visit(p + w);
                }

                if (size > bestSize) { bestSize = size; bestLabel = next; }

                void Visit(int q)
                {
                    if (label[q] != 0 || !grown[q]) return;
                    label[q] = next;
                    stack.Push(q);
                }
            }

            if (bestLabel == 0) return null;

            var piece = new bool[w * h];
            var rowCount = new int[top + 1];
            int widestRow = 0;
            for (int y = bottom; y <= top; y++)
            for (int x = left; x <= right; x++)
            {
                int p = y * w + x;
                if (label[p] != bestLabel || !dark[p]) continue;
                piece[p] = true;
                rowCount[y]++;
                widestRow = Math.Max(widestRow, rowCount[y]);
            }

            // The box is the block, not everything the bridging reached: only rows
            // at least half as wide as the widest. The rod's dark outline strokes
            // and the bolts along the top join the piece but are narrow, and drop
            // out here.
            x0 = w; y0 = h; x1 = -1; y1 = -1;
            for (int y = bottom; y <= top; y++)
            {
                if (rowCount[y] * 2 < widestRow) continue;
                for (int x = left; x <= right; x++)
                {
                    if (!piece[y * w + x]) continue;
                    x0 = Math.Min(x0, x); x1 = Math.Max(x1, x);
                    y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
                }
            }
            return piece;
        }

        /// <summary>
        /// The piston rod, on the first frame: the solid run above the head that
        /// the head's middle column passes through, followed up until it widens
        /// into the clamp under the beam.
        /// </summary>
        private void MeasureRod(byte[] image, int w, int h, float anchorX, float anchorY)
        {
            int centre = (int)Math.Round(anchorX + (HeadLeft[0] + HeadRight[0]) * 0.5f);
            int start = (int)Math.Round(anchorY + HeadTop[0]) + 3;

            int runLeft = centre, runRight = centre, width = -1, y = start;
            for (; y < h; y++)
            {
                if (image[(y * w + centre) * 4 + 3] < SolidAlpha) break;

                int l = centre, r = centre;
                while (l > 0 && image[(y * w + l - 1) * 4 + 3] >= SolidAlpha) l--;
                while (r < w - 1 && image[(y * w + r + 1) * 4 + 3] >= SolidAlpha) r++;

                if (width < 0)
                {
                    width = r - l + 1;
                    runLeft = l;
                    runRight = r;
                }
                else if (r - l + 1 > width * 2)
                {
                    break;
                }
            }

            RodLeft = runLeft - anchorX;
            RodRight = runRight + 1 - anchorX;
            RodTop = y - anchorY;
        }

        /// <summary>
        /// Sorts the frames into the parts of the cycle. Up and down are read off
        /// the head's height; frames between the last waiting frame and the first
        /// holding one are the slam, and frames between the last holding frame and
        /// the next waiting one are the rise.
        /// </summary>
        private void Classify()
        {
            float tolerance = (HeadUp - HeadDown) * RestShare;
            var rest = new int[FrameCount]; // 1 up, -1 down, 0 moving
            for (int k = 0; k < FrameCount; k++)
            {
                if (HeadBottom[k] >= HeadUp - tolerance) rest[k] = 1;
                else if (HeadBottom[k] <= HeadDown + tolerance) rest[k] = -1;
            }

            // Walk the loop from a waiting frame, remembering which rest the head
            // last left, so a moving frame is a slam after "up" and a rise after
            // "down".
            int origin = Array.IndexOf(rest, 1);
            if (origin < 0) origin = 0;

            int last = 1;
            for (int i = 0; i < FrameCount; i++)
            {
                int k = (origin + i) % FrameCount;
                if (rest[k] == 1) { Parts[k] = Part.Waiting; last = 1; }
                else if (rest[k] == -1) { Parts[k] = Part.Holding; last = -1; }
                else Parts[k] = last == 1 ? Part.Slamming : Part.Rising;
            }
        }
    }
}
