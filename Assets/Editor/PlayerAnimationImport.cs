using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Imports the drawn runner frames in Art/Player/PlayerAnimations.
    ///
    /// One folder per animation, one PNG per frame. The frames arrive as
    /// individually cropped images - Run is around 250x330, Idle 100x176, and
    /// the figure does not sit in the same place on every canvas - so importing
    /// them with Unity's defaults would make the runner change size and hop
    /// about between frames. Two measurements fix that:
    ///
    ///  1. One pixels-per-unit for every frame in every clip, derived from the
    ///     Idle pose. Idle is the standing pose, so it is the honest reference
    ///     for "how tall is this character"; scaling off each clip's own
    ///     tallest frame would make the runner grow whenever a limb extends.
    ///
    ///  2. A custom pivot per frame, measured off the drawn figure rather than
    ///     off the canvas, and measured differently in each axis:
    ///
    ///     Vertically it is the bottom of the alpha bounds, so the runner's
    ///     lowest pixel is what meets the roof. Tackle1 carries 30 transparent
    ///     pixels below the body; pivoting on the canvas would leave the runner
    ///     hovering a quarter of a unit above the ground for the whole slide.
    ///
    ///     Horizontally it is the alpha centroid - the centre of mass of the
    ///     drawn pixels - because the two obvious alternatives both slide. The
    ///     canvas centre drifts 28px across the jump frames; the centre of the
    ///     bounding box drifts 21px across the slide, since the box grows and
    ///     shrinks with whichever limb is extended. The centroid holds the body
    ///     still and lets the limbs move around it, which is what the eye reads
    ///     as "planted".
    ///
    ///     Feet were the tempting third option and are the worst of the four:
    ///     mid-stride the lowest pixels are one trailing toe, so pinning them
    ///     swings the runner 71px back and forth across the run cycle.
    ///
    ///  3. An exception to (1) for any folder drawn at a different resolution,
    ///     listed in <see cref="ScaleOverrides"/>. One shared scale is what stops
    ///     the runner changing size between clips, but it only works while every
    ///     folder was drawn at the same size, and lowFlip and Run were not.
    ///     Anything off-scale and unlisted gets a warning rather than silence.
    ///
    ///  4. An exception to the exception, in <see cref="LevelFrameHeights"/>, for
    ///     a folder whose own frames disagree with each other about scale.
    ///
    /// The result is a set of sprites that can be swapped frame to frame with
    /// nothing else moving.
    /// </summary>
    public static class PlayerAnimationImport
    {
        public const string RootFolder = "Assets/Art/Player/PlayerAnimations";

        /// <summary>
        /// World height of the standing runner. Matches the capsule (1.75) with
        /// a little overhang, the same figure the placeholder runner used.
        /// </summary>
        public const float StandingHeight = 1.80f;

        /// <summary>The clip whose height defines the scale of all the others.</summary>
        private const string ReferenceSet = "Idle";

        /// <summary>The frames top out at 291x369 (lowFlip), so 512 never downscales one.</summary>
        private const int MaxTextureSize = 512;

        /// <summary>
        /// Folder names to look for, in the order they are reported. Matched
        /// case-insensitively, because the jump frames live in "jump" while
        /// everything else is capitalised.
        /// </summary>
        private static readonly string[] SetNames =
        {
            "Idle", "Run", "Jump", "FlipJump", "BigJump", "LowFlip", "Fall", "Tackle"
        };

        /// <summary>
        /// Sets drawn at a different resolution from the rest, and the world
        /// height their tallest frame should come out at.
        ///
        /// One shared scale is the right default - it is what stops the runner
        /// changing size between clips - but it assumes every folder was drawn
        /// at the same resolution, and lowFlip was not. Its frames are around
        /// twice the linear size of the others, so under the shared scale the
        /// runner would balloon to 3.8 units mid-jump. 1.94 is the height Run's
        /// tallest frame comes out at, and lowFlip's tallest is the same kind of
        /// extended stride, so matching them is what keeps the two consistent.
        ///
        /// <see cref="WarnAboutScale"/> catches the next folder that lands here.
        /// </summary>
        private static readonly Dictionary<string, float> ScaleOverrides =
            new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["LowFlip"] = 1.94f,

            // Redrawn larger and smoother, at roughly twice the linear size of
            // Idle, so it needs the same treatment. 1.94 is the height Run came
            // out at before, which keeps the runner the size it has always been.
            ["Run"] = 1.94f
        };

        /// <summary>
        /// Clips brought out at exactly the override height frame by frame,
        /// rather than sharing one scale across the whole clip.
        ///
        /// One scale per clip is the right default: it keeps the figure's own
        /// rise and fall, so the head lifts through a stride instead of being
        /// ironed flat.
        ///
        /// Run is here because its frames were not all drawn at one size. The 16
        /// arrived as two batches - frames 1-8 average 330px tall, frames 9-16
        /// average 300px, each batch internally consistent to within a few
        /// percent, with a flat 10% step between them. That is not gait; gait
        /// varies smoothly across a cycle rather than sitting on two plateaus.
        /// On one shared scale the runner would shrink a tenth of their height
        /// halfway through every stride and snap back, twice a second. Levelling
        /// costs the ~4% of real head movement and removes the 10% of pulsing.
        /// </summary>
        private static readonly HashSet<string> LevelFrameHeights =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "Run" };

        /// <summary>
        /// How far a set's tallest frame may sit from the standing height before
        /// the importer says something. Tackle is the honest low end at 0.80x
        /// (the runner is lying down); Run is the high end at 1.08x.
        /// </summary>
        private const float MinPlausibleScale = 0.60f;
        private const float MaxPlausibleScale = 1.30f;

        private static readonly Dictionary<string, Sprite[]> Cache =
            new Dictionary<string, Sprite[]>();

        // ================================================================= //

        [MenuItem("ArvinRunner/Re-import Player Frames", priority = 11)]
        public static void ImportAll()
        {
            Cache.Clear();

            if (!AssetDatabase.IsValidFolder(RootFolder))
            {
                Debug.LogWarning($"[ArvinRunner] No player frames at {RootFolder}. " +
                                 "The runner falls back to a single pose.");
                return;
            }

            // Every frame path, grouped by clip, in playback order.
            var sets = new Dictionary<string, string[]>();
            foreach (string name in SetNames)
            {
                string[] paths = FramePaths(name);
                if (paths.Length > 0) sets[name] = paths;
            }

            if (sets.Count == 0)
            {
                Debug.LogWarning($"[ArvinRunner] {RootFolder} holds no frames.");
                return;
            }

            try
            {
                // --- pass one: make every frame readable so we can measure it ---
                int done = 0;
                foreach (string path in sets.Values.SelectMany(p => p))
                {
                    EditorUtility.DisplayProgressBar("ArvinRunner", "Measuring " + Path.GetFileName(path),
                                                     0.5f * done++ / TotalFrames(sets));
                    SpriteMeasure.MakeReadable(path, MaxTextureSize);
                }

                // --- the scale, from the standing pose --------------------------
                float shared = MeasureScale(sets);

                // --- pass two: pivot and scale each frame -----------------------
                done = 0;
                foreach (KeyValuePair<string, string[]> set in sets)
                {
                    float pixelsPerUnit = ScaleFor(set.Key, set.Value, shared);

                    // A levelled clip resolves its scale per frame instead, so
                    // every frame lands on the same drawn height.
                    float levelTo = 0f;
                    if (LevelFrameHeights.Contains(set.Key) &&
                        ScaleOverrides.TryGetValue(set.Key, out float target))
                    {
                        levelTo = target;
                        ReportLevelling(set.Key, set.Value);
                    }

                    foreach (string path in set.Value)
                    {
                        EditorUtility.DisplayProgressBar("ArvinRunner", "Importing " + Path.GetFileName(path),
                                                         0.5f + 0.5f * done++ / TotalFrames(sets));
                        Finalise(path, pixelsPerUnit, levelTo);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            string summary = string.Join(", ", sets.Select(s => $"{s.Key} {s.Value.Length}"));
            Debug.Log($"[ArvinRunner] Player frames imported ({summary}).");
        }

        // ================================================================= //
        // Reading the imported frames back
        // ================================================================= //

        /// <summary>
        /// The frames of one clip, in order, or an empty array if that folder is
        /// missing. Callers fall back to a single pose when this comes back empty.
        /// </summary>
        public static Sprite[] Frames(string setName)
        {
            if (Cache.TryGetValue(setName, out Sprite[] cached) &&
                cached.Length > 0 && cached[0] != null)
                return cached;

            var sprites = new List<Sprite>();

            foreach (string path in FramePaths(setName))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) sprites.Add(sprite);
            }

            Sprite[] result = sprites.ToArray();
            Cache[setName] = result;
            return result;
        }

        /// <summary>One frame of a clip, for slots that only need a held pose.</summary>
        public static Sprite Frame(string setName, int index)
        {
            Sprite[] frames = Frames(setName);
            if (frames.Length == 0) return null;

            return frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }

        public static bool IsAvailable => Frames(ReferenceSet).Length > 0;

        // ================================================================= //
        // Finding the files
        // ================================================================= //

        /// <summary>
        /// The PNGs of one clip, ordered by the number at the end of the file
        /// name rather than alphabetically - otherwise Run10 would play between
        /// Run1 and Run2.
        /// </summary>
        private static string[] FramePaths(string setName)
        {
            string folder = FindFolder(setName);
            if (folder == null) return new string[0];

            return Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly)
                            .Select(p => p.Replace('\\', '/'))
                            .OrderBy(TrailingNumber)
                            .ThenBy(p => p)
                            .ToArray();
        }

        /// <summary>Matches the folder case-insensitively, so "jump" is found too.</summary>
        private static string FindFolder(string setName)
        {
            if (!Directory.Exists(RootFolder)) return null;

            foreach (string folder in Directory.GetDirectories(RootFolder))
            {
                string leaf = Path.GetFileName(folder);
                if (string.Equals(leaf, setName, System.StringComparison.OrdinalIgnoreCase))
                    return folder.Replace('\\', '/');
            }

            return null;
        }

        private static int TrailingNumber(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            int end = name.Length;

            while (end > 0 && char.IsDigit(name[end - 1])) end--;

            return end < name.Length && int.TryParse(name.Substring(end), out int value)
                ? value
                : 0;
        }

        private static int TotalFrames(Dictionary<string, string[]> sets)
        {
            int total = 0;
            foreach (string[] paths in sets.Values) total += paths.Length;
            return Mathf.Max(1, total);
        }

        // ================================================================= //
        // Importing
        // ================================================================= //

        /// <summary>
        /// Pixels-per-unit such that the tallest standing pose comes out at
        /// <see cref="StandingHeight"/>. Every clip is imported at this scale,
        /// so a frame with an outstretched leg is drawn bigger rather than being
        /// squeezed back down to the same bounding box.
        /// </summary>
        private static float MeasureScale(Dictionary<string, string[]> sets)
        {
            if (!sets.TryGetValue(ReferenceSet, out string[] reference))
            {
                // No idle pose to measure. Fall back to whatever clip we do
                // have, which keeps the frames consistent with each other even
                // if the absolute size needs a nudge afterwards.
                reference = sets.Values.First();
                Debug.LogWarning($"[ArvinRunner] No '{ReferenceSet}' frames, so the runner's scale " +
                                 $"is taken from '{sets.Keys.First()}' instead. Check the size in play.");
            }

            int tallest = 0;
            foreach (string path in reference)
            {
                Figure figure = SpriteMeasure.Measure(path);
                if (figure.Bounds.height > tallest) tallest = figure.Bounds.height;
            }

            if (tallest <= 0)
            {
                Debug.LogWarning("[ArvinRunner] Could not measure the runner's height; using 100 PPU.");
                return 100f;
            }

            return tallest / StandingHeight;
        }

        /// <summary>
        /// The scale one clip is imported at: the shared one, unless the folder
        /// is listed in <see cref="ScaleOverrides"/> as having been drawn at a
        /// different resolution.
        /// </summary>
        private static float ScaleFor(string setName, string[] paths, float shared)
        {
            int tallest = 0;
            foreach (string path in paths)
            {
                Figure figure = SpriteMeasure.Measure(path);
                if (figure.Bounds.height > tallest) tallest = figure.Bounds.height;
            }

            if (tallest <= 0) return shared;

            if (ScaleOverrides.TryGetValue(setName, out float wanted))
                return tallest / Mathf.Max(0.05f, wanted);

            WarnAboutScale(setName, tallest / shared);
            return shared;
        }

        /// <summary>
        /// Says something when a folder was clearly drawn at a different size
        /// from the rest, rather than letting it import at twice the runner's
        /// height and leaving someone to find it in play.
        /// </summary>
        private static void WarnAboutScale(string setName, float height)
        {
            float ratio = height / StandingHeight;
            if (ratio >= MinPlausibleScale && ratio <= MaxPlausibleScale) return;

            Debug.LogWarning(
                $"[ArvinRunner] The '{setName}' frames come out {height:0.00} units tall against a " +
                $"{StandingHeight:0.00} standing runner ({ratio:0.0}x). They were probably drawn at a " +
                "different resolution from the other clips. Add an entry to " +
                "PlayerAnimationImport.ScaleOverrides giving the height its tallest frame should be.");
        }

        /// <summary>
        /// Gives one frame its pivot and its scale. The measuring and the import
        /// settings both live in SpriteMeasure, which the moving-obstacle frames
        /// share - they arrive cropped just as inconsistently.
        ///
        /// <paramref name="levelTo"/> above zero means this frame gets its own
        /// scale, so the drawn figure comes out exactly that tall - see
        /// <see cref="LevelFrameHeights"/> for when that is the right thing.
        /// </summary>
        private static void Finalise(string path, float pixelsPerUnit, float levelTo = 0f)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) return;

            Figure figure = SpriteMeasure.Measure(path);
            if (figure.IsEmpty)
            {
                Debug.LogWarning($"[ArvinRunner] {path} looks fully transparent.");
                return;
            }

            if (levelTo > 0.01f) pixelsPerUnit = figure.Bounds.height / levelTo;

            SpriteMeasure.ApplyFrame(path,
                                     SpriteMeasure.PivotFor(figure, texture.width, texture.height),
                                     pixelsPerUnit);
        }

        /// <summary>
        /// Says what levelling corrected, so a clip being quietly adjusted on
        /// every import does not stay quiet about it. A small spread is the
        /// drawing breathing; a large one means the frames were not all drawn at
        /// one size, and the art is the better place to fix that.
        /// </summary>
        private static void ReportLevelling(string setName, string[] paths)
        {
            int shortest = int.MaxValue, tallest = 0;

            foreach (string path in paths)
            {
                int height = SpriteMeasure.Measure(path).Bounds.height;
                if (height <= 0) continue;

                if (height < shortest) shortest = height;
                if (height > tallest) tallest = height;
            }

            if (tallest <= 0 || shortest == int.MaxValue) return;

            float spread = (float)tallest / shortest - 1f;
            if (spread < 0.05f) return;

            Debug.Log($"[ArvinRunner] The '{setName}' frames vary {spread:P0} in drawn height " +
                      $"({shortest}-{tallest}px) and are being levelled to one size. Much above " +
                      "5% is usually two batches drawn at different scales rather than the figure " +
                      "moving - worth re-exporting them to match.");
        }
    }
}
