using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Imports the slam press frames in Art/MovingObstacles/presser and measures
    /// them with <see cref="PressRig"/>: each frame's pivot goes on the middle of
    /// the top of its base plate - the point that stands flush with the roof -
    /// so frames drawn on canvases of different heights still line up, and the
    /// press gets a head box and a duration for every frame.
    ///
    /// The frames are imported where they are; nothing is generated. Replacing
    /// them and rebuilding re-measures everything.
    /// </summary>
    public static class PressImport
    {
        public const string SourceFolder = MovingObstacleImport.RootFolder + "/presser";

        /// <summary>
        /// World height of the gap under the waiting head, which sets the press's
        /// scale.
        ///
        /// Under the runner's 1.75, so the press cannot be run through standing
        /// even while it waits, and over the 0.79 slide, so it can be slid under:
        /// the question the old red press asked, now with timing, because the
        /// drawn head comes all the way down onto the plate. At 1.3 the machine is
        /// 4.8 tall and 4.1 wide - a heavy industrial press next to the runner.
        /// </summary>
        public const float GapHeight = 1.3f;

        /// <summary>How long each part of the cycle takes, in seconds. About the
        /// old red press's cycle, so the chunks built around its rhythm - two
        /// presses half a cycle apart - keep their spacing.</summary>
        public const float WaitingSeconds = 1.0f;
        public const float SlammingSeconds = 0.15f;
        public const float HoldingSeconds = 0.45f;
        public const float RisingSeconds = 0.7f;

        /// <summary>How long before the waiting ends the runner reaches the press.
        /// Going under takes about a quarter of a second at run speed, so at 0.4 the
        /// head slams a quarter of a second behind the runner - close enough to
        /// feel, far enough to be fair at any speed the runner reaches.</summary>
        public const float ArrivalLead = 0.4f;

        /// <summary>The run speed the approach is scaled to: the press plays at its
        /// drawn pace for a runner doing this, a little faster for one going
        /// faster.</summary>
        public const float ApproachSpeed = 10f;

        /// <summary>Share of the head's drawn width that kills: the block's ends
        /// are rounded and shaded, and a runner who clears the edge by a sliver
        /// has cleared it.</summary>
        public const float HeadHitShare = 0.92f;

        private const int MaxTextureSize = 2048;

        /// <summary>Everything the chunk factory needs to build a press.</summary>
        public sealed class Rig
        {
            public Sprite[] Frames;
            public float[] Durations;

            /// <summary>Per frame, the head block's centre and size, relative to
            /// the press's foot on the roof.</summary>
            public Vector2[] HeadCentres;
            public Vector2[] HeadSizes;

            /// <summary>The piston rod's span across, and where it meets the clamp.</summary>
            public float RodLeft;
            public float RodRight;
            public float RodTop;

            public float MachineWidth;
            public float MachineHeight;

            /// <summary>Seconds into the cycle when the runner reaches the press.</summary>
            public float ArrivalTime;

            /// <summary>World units of approach per whole cycle.</summary>
            public float Wavelength;
        }

        private static Rig _cached;

        // ================================================================= //

        /// <summary>The press, measuring the frames first if this editor session
        /// has not already. Null when there are no press frames.</summary>
        public static Rig Load()
        {
            if (_cached != null && _cached.Frames.All(s => s != null)) return _cached;
            return ImportAll();
        }

        [MenuItem("ArvinRunner/Re-import Press", priority = 14)]
        public static Rig ImportAll()
        {
            _cached = null;

            string[] paths = FramePaths();
            if (paths.Length < 4)
            {
                Debug.LogWarning($"[ArvinRunner] Fewer than four press frames in {SourceFolder}; " +
                                 "the press falls back to the plain red block.");
                return null;
            }

            try
            {
                var frames = new List<byte[]>();
                var widths = new List<int>();
                var heights = new List<int>();

                for (int i = 0; i < paths.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("ArvinRunner", "Reading the press", 0.5f * i / paths.Length);
                    SpriteMeasure.MakeReadable(paths[i], MaxTextureSize);

                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i]);
                    if (texture == null)
                    {
                        Debug.LogWarning($"[ArvinRunner] Could not load {paths[i]}; the press falls back.");
                        return null;
                    }

                    frames.Add(ToBytes(texture.GetPixels32()));
                    widths.Add(texture.width);
                    heights.Add(texture.height);
                }

                PressRig rig;
                try
                {
                    rig = PressRig.Measure(frames, widths, heights);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[ArvinRunner] Could not measure the press frames: {e.Message}");
                    return null;
                }

                // The lowest the waiting head is drawn sets the gap - it lifts a
                // little with the warning lamp, and the gap must hold before that.
                float gap = float.MaxValue;
                for (int k = 0; k < rig.FrameCount; k++)
                    if (rig.Parts[k] == PressRig.Part.Waiting)
                        gap = Mathf.Min(gap, rig.HeadBottom[k]);

                float pixelsPerUnit = gap / GapHeight;

                var sprites = new Sprite[paths.Length];
                var centres = new Vector2[paths.Length];
                var sizes = new Vector2[paths.Length];

                for (int k = 0; k < paths.Length; k++)
                {
                    EditorUtility.DisplayProgressBar("ArvinRunner", "Importing the press",
                                                     0.5f + 0.5f * k / paths.Length);

                    var pivot = new Vector2(rig.AnchorX[k] / widths[k], rig.AnchorY[k] / heights[k]);
                    SpriteMeasure.ApplyFrame(paths[k], pivot, pixelsPerUnit);
                    sprites[k] = AssetDatabase.LoadAssetAtPath<Sprite>(paths[k]);

                    centres[k] = new Vector2(rig.HeadLeft[k] + rig.HeadRight[k],
                                             rig.HeadBottom[k] + rig.HeadTop[k]) * 0.5f / pixelsPerUnit;
                    sizes[k] = new Vector2(rig.HeadRight[k] - rig.HeadLeft[k],
                                           rig.HeadTop[k] - rig.HeadBottom[k]) / pixelsPerUnit;
                }

                float[] durations = rig.Durations(WaitingSeconds, SlammingSeconds, HoldingSeconds, RisingSeconds);

                _cached = new Rig
                {
                    Frames = sprites,
                    Durations = durations,
                    HeadCentres = centres,
                    HeadSizes = sizes,
                    RodLeft = rig.RodLeft / pixelsPerUnit,
                    RodRight = rig.RodRight / pixelsPerUnit,
                    RodTop = rig.RodTop / pixelsPerUnit,
                    MachineWidth = (rig.MachineRight - rig.MachineLeft) / pixelsPerUnit,
                    MachineHeight = (rig.MachineTop + rig.PlateThickness) / pixelsPerUnit,
                    ArrivalTime = rig.ArrivalTime(durations, ArrivalLead),
                    Wavelength = durations.Sum() * ApproachSpeed
                };

                string parts = string.Join(" ", rig.Parts.Select(p => p.ToString()[0]));
                Debug.Log($"[ArvinRunner] Press imported: {paths.Length} frames ({parts}), " +
                          $"{_cached.MachineWidth:F2}x{_cached.MachineHeight:F2}, " +
                          $"head travel {(rig.HeadUp - rig.HeadDown) / pixelsPerUnit:F2}, " +
                          $"cycle {durations.Sum():F2}s.");

                return _cached;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ================================================================= //

        private static string[] FramePaths()
        {
            if (!Directory.Exists(SourceFolder)) return new string[0];

            return Directory.GetFiles(SourceFolder, "*.png", SearchOption.TopDirectoryOnly)
                            .Select(p => p.Replace('\\', '/'))
                            .OrderBy(Number)
                            .ThenBy(p => p)
                            .ToArray();
        }

        /// <summary>presser1 ... presser24: the number in the name, so 10 follows 9.</summary>
        private static int Number(string path)
        {
            string digits = new string(Path.GetFileNameWithoutExtension(path).Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out int value) ? value : 0;
        }

        private static byte[] ToBytes(Color32[] pixels)
        {
            var bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                bytes[i * 4] = pixels[i].r;
                bytes[i * 4 + 1] = pixels[i].g;
                bytes[i * 4 + 2] = pixels[i].b;
                bytes[i * 4 + 3] = pixels[i].a;
            }
            return bytes;
        }
    }
}
