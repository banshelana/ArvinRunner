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
    /// One folder per clip, one PNG per frame, each frame cropped to its own
    /// figure. Cropping throws away everything that related one frame to the
    /// next, so importing with Unity's defaults would make the runner change
    /// size between clips and hop about between frames. The importer puts back
    /// what can be measured:
    ///
    ///  1. <b>Scale.</b> One pixels-per-unit from the standing frames of the
    ///     jump, times how large each folder was drawn relative to them - see
    ///     <see cref="DrawingScale"/>. Scaling each clip off its own tallest
    ///     frame instead would make the runner grow whenever a limb extends.
    ///
    ///  2. <b>A pivot per frame</b>: the alpha centroid across and the lowest
    ///     drawn pixel down. The canvas centre drifts with the crop and the
    ///     bounding box with whichever limb is out; the centroid holds the mass
    ///     still and lets the limbs move around it.
    ///
    /// The pivot is a sound default for one frame and not the last word on a
    /// sequence. What lines frames up depends on what the body is doing - the
    /// head in a run, the centre of mass in a somersault - and one drawing can be
    /// used both ways, so ArvinRunnerSetup registers frames per clip from the
    /// shapes measured here (<see cref="TryShape"/>).
    ///
    /// A folder drawn on one shared canvas is the exception to (2): the movement
    /// between its frames is the animation and is kept - see
    /// <see cref="AlignedPivot"/>.
    /// </summary>
    public static class PlayerAnimationImport
    {
        public const string RootFolder = "Assets/Art/Player/PlayerAnimations";

        /// <summary>
        /// World height of the standing runner. Matches the capsule (1.75) with
        /// a little overhang, the same figure the placeholder runner used.
        /// </summary>
        public const float StandingHeight = 1.80f;

        /// <summary>
        /// The frames whose height defines the runner's size: the first and last
        /// of the jump, where he stands upright before the crouch and after the
        /// recovery. Idle was the reference until it turned out to be drawn a
        /// fifth larger than everything else - see <see cref="DrawingScale"/>.
        /// </summary>
        private const string ReferenceSet = "Jump";
        private static readonly int[] ReferenceFrames = { 0, 23 };

        /// <summary>
        /// Comfortably above any frame, so nothing is quietly downscaled - the
        /// measurements below would then be taken off the downscaled copy.
        /// </summary>
        private const int MaxTextureSize = 1024;

        /// <summary>
        /// Folder names to look for, in the order they are reported. Matched
        /// case-insensitively, because the jump frames live in "jump" while
        /// everything else is capitalised.
        /// </summary>
        private static readonly string[] SetNames =
        {
            "Idle", "Run", "Jump", "FlipJump", "BigJump", "LowFlip", "Fall", "Tackle", "Climb", "Lose"
        };

        /// <summary>
        /// How large each folder was drawn, relative to the jump.
        ///
        /// The frames are cropped one at a time, so there is no canvas to compare
        /// and nothing in a folder says what size it was drawn at - and they are
        /// not all the same. Idle is drawn about a fifth larger than the rest and
        /// Fall about a tenth smaller; on one shared scale the runner would shrink
        /// the moment he set off and again whenever he dropped off something.
        ///
        /// Height cannot measure it, because every clip is a different pose. So
        /// the same pose was compared across folders instead - standing, running
        /// and deep crouch - by silhouette area, which grows with the square of
        /// the drawing size, and by height where the pose allows. For every folder
        /// the comparisons agree to within a few percent:
        ///
        ///     Idle      standing 1.20 by height, 1.17 by area         1.18
        ///     Run       running pose against Tackle's, 1.00          1.02
        ///     Tackle    standing 1.01 / 1.02, crouch 1.06              1.02
        ///     LowFlip   standing 1.05 / 1.06, crouch 1.02              1.04
        ///     FlipJump  crouch 0.98, running 0.99                      1.00
        ///     BigJump   standing 0.96 / 0.98, crouch 0.96              0.965
        ///     Climb     standing 0.98 / 0.96                           0.96
        ///     Fall      standing 0.91 / 0.89, crouch 0.86              0.89
        ///     Lose      running pose by height 0.78, thickness 0.75    0.78
        ///
        /// Lose is the one folder where the measures split: silhouette area says
        /// 0.86. Height and body thickness agree with each other and area does not,
        /// because that runner is drawn leaner - more area per unit of height would
        /// mean a bigger man, not a bigger drawing.
        ///
        /// Head size was tried as a fourth measure and rejected: the jump draws
        /// its head proportionally larger than every other folder, which says
        /// more about the drawing than about its scale.
        ///
        /// A folder not listed imports at the jump's scale, and
        /// <see cref="WarnAboutScale"/> says so if that looks wrong.
        /// </summary>
        private static readonly Dictionary<string, float> DrawingScale =
            new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Jump"] = 1.00f,
            ["Idle"] = 1.18f,
            ["Run"] = 1.02f,
            ["Tackle"] = 1.02f,
            ["LowFlip"] = 1.04f,
            ["FlipJump"] = 1.00f,
            ["BigJump"] = 0.965f,
            ["Climb"] = 0.96f,
            ["Fall"] = 0.89f,
            ["Lose"] = 0.78f
        };

        /// <summary>
        /// How far a set's tallest frame may sit from the standing height before
        /// the importer says something. Tackle is the honest low end at 0.80x
        /// (the runner is lying down); Run is the high end at 1.08x.
        /// </summary>
        private const float MinPlausibleScale = 0.60f;
        private const float MaxPlausibleScale = 1.30f;

        private static readonly Dictionary<string, Sprite[]> Cache =
            new Dictionary<string, Sprite[]>();

        // Every frame's measured shape, kept from the import - after which the
        // textures are no longer readable and cannot be measured again.
        private static readonly Dictionary<string, Figure> Shapes =
            new Dictionary<string, Figure>();

        // ================================================================= //

        [MenuItem("ArvinRunner/Re-import Player Frames", priority = 11)]
        public static void ImportAll()
        {
            Cache.Clear();
            Shapes.Clear();

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
                    Shape(path);
                }

                // --- the scale, from the standing pose --------------------------
                float shared = MeasureScale(sets);

                // --- pass two: pivot and scale each frame -----------------------
                done = 0;
                foreach (KeyValuePair<string, string[]> set in sets)
                {
                    float pixelsPerUnit = ScaleFor(set.Key, set.Value, shared);

                    // A clip drawn on one shared canvas is anchored once for the
                    // whole clip rather than frame by frame - see AlignedPivot.
                    bool aligned = SharesOneCanvas(set.Value);
                    Vector2 anchor = aligned ? AlignedPivot(set.Key, set.Value) : Vector2.zero;

                    foreach (string path in set.Value)
                    {
                        EditorUtility.DisplayProgressBar("ArvinRunner", "Importing " + Path.GetFileName(path),
                                                         0.5f + 0.5f * done++ / TotalFrames(sets));
                        Finalise(path, pixelsPerUnit, aligned, anchor);
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

        /// <summary>
        /// The measured shape of a player frame, from the most recent import:
        /// bounds, centre of mass and head column, in the texture's own pixels.
        /// False for a sprite that was not imported in this session - the build
        /// imports before it builds the animation set, so that means it was run
        /// out of order.
        /// </summary>
        public static bool TryShape(Sprite sprite, out Figure figure)
        {
            figure = default;
            if (sprite == null) return false;

            string path = AssetDatabase.GetAssetPath(sprite);
            return !string.IsNullOrEmpty(path) && Shapes.TryGetValue(path, out figure);
        }

        private static Figure Shape(string path)
        {
            if (!Shapes.TryGetValue(path, out Figure figure))
            {
                figure = SpriteMeasure.Measure(path);
                Shapes[path] = figure;
            }

            return figure;
        }

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
        /// Pixels-per-unit for a folder drawn at the jump's size: the jump's
        /// upright frames come out <see cref="StandingHeight"/> tall. Every folder
        /// is imported at this times its <see cref="DrawingScale"/>, so a frame
        /// with a leg thrown out is drawn bigger rather than squeezed down.
        /// </summary>
        private static float MeasureScale(Dictionary<string, string[]> sets)
        {
            int tallest = 0;

            if (sets.TryGetValue(ReferenceSet, out string[] reference))
            {
                foreach (int index in ReferenceFrames)
                    if (index < reference.Length)
                        tallest = Mathf.Max(tallest, Shape(reference[index]).Bounds.height);
            }
            else
            {
                // No jump to measure. The tallest frame of whatever there is,
                // with its drawing size taken back off, keeps the folders in
                // proportion to each other even if the overall size needs a look.
                KeyValuePair<string, string[]> first = sets.First();
                foreach (string path in first.Value)
                    tallest = Mathf.Max(tallest, Shape(path).Bounds.height);

                tallest = Mathf.RoundToInt(tallest / DrawnAt(first.Key));
                Debug.LogWarning($"[ArvinRunner] No '{ReferenceSet}' frames, so the runner's scale " +
                                 $"is taken from '{first.Key}' instead. Check the size in play.");
            }

            if (tallest <= 0)
            {
                Debug.LogWarning("[ArvinRunner] Could not measure the runner's height; using 100 PPU.");
                return 100f;
            }

            return tallest / StandingHeight;
        }

        private static float DrawnAt(string setName) =>
            DrawingScale.TryGetValue(setName, out float scale) ? scale : 1f;

        /// <summary>The scale one folder is imported at.</summary>
        private static float ScaleFor(string setName, string[] paths, float shared)
        {
            float pixelsPerUnit = shared * DrawnAt(setName);

            if (!DrawingScale.ContainsKey(setName))
            {
                int tallest = 0;
                foreach (string path in paths)
                    tallest = Mathf.Max(tallest, Shape(path).Bounds.height);

                if (tallest > 0) WarnAboutScale(setName, tallest / pixelsPerUnit);
            }

            return pixelsPerUnit;
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
                "different size from the other clips. Compare a pose they share with the jump " +
                "and add the ratio to PlayerAnimationImport.DrawingScale.");
        }

        /// <summary>
        /// Gives one frame its pivot and its scale. The measuring and the import
        /// settings both live in SpriteMeasure, which the moving-obstacle frames
        /// share - they arrive cropped just as inconsistently.
        /// </summary>
        private static void Finalise(string path, float pixelsPerUnit, bool aligned, Vector2 anchor)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) return;

            Figure figure = Shape(path);
            if (figure.IsEmpty)
            {
                Debug.LogWarning($"[ArvinRunner] {path} looks fully transparent.");
                return;
            }

            Vector2 pivot = aligned
                ? anchor
                : SpriteMeasure.PivotFor(figure, texture.width, texture.height);

            SpriteMeasure.ApplyFrame(path, pivot, pixelsPerUnit);
        }

        /// <summary>True when every frame of a clip was drawn on the same canvas.</summary>
        private static bool SharesOneCanvas(string[] paths)
        {
            int width = 0, height = 0;

            foreach (string path in paths)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) return false;

                if (width == 0)
                {
                    width = texture.width;
                    height = texture.height;
                    continue;
                }

                if (texture.width != width || texture.height != height) return false;
            }

            return width > 0;
        }

        /// <summary>
        /// The one anchor a clip drawn on a shared canvas gets.
        ///
        /// <b>This is the opposite of what individually cropped frames need, and
        /// using either rule on the other kind of art is very visible.</b>
        ///
        /// Cropped frames carry no common frame of reference, so each has to be
        /// measured and pinned on its own - that is what the per-frame pivot is
        /// for, and what the note at the top of this file is about.
        ///
        /// Frames sharing a canvas have already been positioned against each
        /// other by whoever drew them, and the movement between them *is* the
        /// animation. An earlier run was 24 frames on one 772x897 canvas whose
        /// lowest pixel lifted 35px through the flight phase - the runner leaving
        /// the ground. Measuring that per frame would pin the flight frames back
        /// down to the floor and delete the bounce from the run.
        ///
        /// So: horizontally the mean centre of mass, which is where the body is
        /// across the cycle and lets the limbs swing around it; vertically the
        /// lowest pixel the clip ever reaches, which is the ground the runner is
        /// standing on in the frames where they are touching it.
        /// </summary>
        private static Vector2 AlignedPivot(string setName, string[] paths)
        {
            var reference = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[0]);
            if (reference == null) return new Vector2(0.5f, 0f);

            int lowest = int.MaxValue;
            float centroidSum = 0f;
            int counted = 0;

            foreach (string path in paths)
            {
                Figure figure = Shape(path);
                if (figure.IsEmpty) continue;

                if (figure.Bounds.y < lowest) lowest = figure.Bounds.y;
                centroidSum += figure.CentroidX;
                counted++;
            }

            if (counted == 0)
            {
                Debug.LogWarning($"[ArvinRunner] Could not measure any frame of '{setName}'.");
                return new Vector2(0.5f, 0f);
            }

            return new Vector2(centroidSum / counted / reference.width,
                               lowest / (float)reference.height);
        }
    }
}
