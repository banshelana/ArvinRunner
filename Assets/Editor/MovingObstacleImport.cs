using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Imports the animated obstacle frames in Art/MovingObstacles - one folder
    /// per obstacle, numbered PNGs inside it.
    ///
    /// The measuring is the same job the runner's frames need, and uses the same
    /// code (<see cref="SpriteMeasure"/>). The scaling is the part that differs.
    /// The runner's clips all share one pixels-per-unit because they are all the
    /// same character and it must not change size; two unrelated vehicles have no
    /// such relationship, so each folder here gets its own scale.
    ///
    /// Heights are chosen against the runner rather than against the real
    /// machines. The bike is 1.55 so it reads as something to clear against a
    /// 1.75 standing runner and a 3.20 jump; the helicopter is 2.85 across
    /// airframe and rotor, which puts it about 7.4 long - the scale of the bus,
    /// so it has the presence of a real aircraft without filling the screen.
    ///
    /// How a folder is anchored depends on how it was drawn. See <see cref="Apply"/>.
    /// </summary>
    public static class MovingObstacleImport
    {
        public const string RootFolder = "Assets/Art/MovingObstacles";

        /// <summary>The helicopter frames are 1448x653, so 2048 never downscales one.</summary>
        private const int MaxTextureSize = 2048;

        /// <summary>
        /// Anchors and scales are measured on solid pixels rather than on any
        /// drawn pixel at all. Rotor blur, exhaust and a missile's smoke trail
        /// belong to the picture but not to where the object is, and letting them
        /// into the measurement moves the machine around to follow its own smoke.
        /// </summary>
        private const byte SolidAlpha = 128;

        private struct SetSpec
        {
            public string Folder;

            /// <summary>World height of the subject, measured on solid pixels.</summary>
            public float Height;
        }

        private static readonly SetSpec[] Sets =
        {
            // 1.55 tall and 1.67-1.96 long depending on how far the rider is
            // leaned over. Comfortably under the 3.20 jump.
            new SetSpec { Folder = "moto", Height = 1.55f },

            // Airframe and rotor together.
            new SetSpec { Folder = "helli", Height = 2.85f }
        };

        private static readonly Dictionary<string, Sprite[]> Cache =
            new Dictionary<string, Sprite[]>();

        // ================================================================= //

        [MenuItem("ArvinRunner/Re-import Moving Obstacles", priority = 12)]
        public static void ImportAll()
        {
            Cache.Clear();

            if (!AssetDatabase.IsValidFolder(RootFolder))
            {
                Debug.LogWarning($"[ArvinRunner] No moving-obstacle art at {RootFolder}.");
                return;
            }

            var found = new List<KeyValuePair<SetSpec, string[]>>();
            foreach (SetSpec spec in Sets)
            {
                string[] paths = FramePaths(spec.Folder);
                if (paths.Length > 0)
                    found.Add(new KeyValuePair<SetSpec, string[]>(spec, paths));
                else
                    Debug.LogWarning($"[ArvinRunner] No frames in {RootFolder}/{spec.Folder}. " +
                                     "That obstacle falls back to a plain box.");
            }

            if (found.Count == 0) return;

            int total = Mathf.Max(1, found.Sum(f => f.Value.Length));

            try
            {
                // --- pass one: readable, so the frames can be measured ---------
                int done = 0;
                foreach (string path in found.SelectMany(f => f.Value))
                {
                    EditorUtility.DisplayProgressBar("ArvinRunner", "Measuring " + Path.GetFileName(path),
                                                     0.5f * done++ / total);
                    SpriteMeasure.MakeReadable(path, MaxTextureSize);
                }

                // --- pass two: scale and anchor, one folder at a time ----------
                done = 0;
                foreach (KeyValuePair<SetSpec, string[]> set in found)
                    Apply(set.Key, set.Value, ref done, total);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            string summary = string.Join(", ",
                found.Select(f => $"{f.Key.Folder} {f.Value.Length}" +
                                  (SharesOneCanvas(f.Value) ? " (aligned)" : "")));

            Debug.Log($"[ArvinRunner] Moving obstacles imported: {summary}.");
        }

        /// <summary>
        /// Scales and anchors one folder.
        ///
        /// <b>How the anchor is chosen depends on how the frames were drawn, and
        /// getting it the wrong way round is very visible.</b>
        ///
        /// Frames cropped individually - the bike, at 344x286, 310x285, 363x246 -
        /// share no frame of reference, so each has to be measured and pinned on
        /// its own or the object jumps about as the crop changes under it.
        ///
        /// Frames drawn on one shared canvas are the opposite case. The helicopter
        /// is 24 frames on one 1448x653 canvas, and what moves between them is the
        /// animation: the body bobs 31px through the loop, and the rotor swaps
        /// between a spread blade and an edge-on one every frame. Measured frame
        /// by frame, the blade alone would move the solid box about 40px sideways
        /// on alternate frames - the aircraft would shudder twelve times a second
        /// - and the bob would be measured away. One anchor for the whole folder
        /// keeps both.
        ///
        /// The two cases are told apart by the canvases rather than by a flag
        /// somebody has to remember to set: frames that share a size were laid
        /// out together.
        /// </summary>
        private static void Apply(SetSpec spec, string[] paths, ref int done, int total)
        {
            bool aligned = SharesOneCanvas(paths);
            float pixelsPerUnit = ScaleFor(spec, paths, aligned);

            Vector2 shared = Vector2.zero;

            if (aligned)
            {
                var reference = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[0]);
                Figure figure = SpriteMeasure.Measure(paths[0], SolidAlpha);

                if (reference == null || figure.IsEmpty)
                {
                    Debug.LogWarning($"[ArvinRunner] Could not measure the first frame of " +
                                     $"{spec.Folder}, so it keeps its previous import.");
                    return;
                }

                // Centre of the solid box across, its bottom down. The box rather
                // than the centre of mass, because on a shared canvas there is no
                // cropping to correct for and the box is the steadier of the two.
                shared = new Vector2(
                    (figure.Bounds.x + figure.Bounds.width * 0.5f) / reference.width,
                    figure.Bounds.y / (float)reference.height);
            }

            foreach (string path in paths)
            {
                EditorUtility.DisplayProgressBar("ArvinRunner", "Importing " + Path.GetFileName(path),
                                                 0.5f + 0.5f * done++ / total);

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) continue;

                Vector2 pivot;

                if (aligned)
                {
                    pivot = shared;
                }
                else
                {
                    Figure figure = SpriteMeasure.Measure(path, SolidAlpha);
                    if (figure.IsEmpty)
                    {
                        Debug.LogWarning($"[ArvinRunner] {path} looks fully transparent.");
                        continue;
                    }

                    pivot = SpriteMeasure.PivotFor(figure, texture.width, texture.height);
                }

                SpriteMeasure.ApplyFrame(path, pivot, pixelsPerUnit);
            }
        }

        /// <summary>True when every frame was drawn on the same size canvas.</summary>
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
        /// Pixels-per-unit that brings the subject out at its declared height.
        ///
        /// Which frame defines "the subject" follows the same split as the anchor.
        /// On a shared canvas it is the reference frame, so the scale does not
        /// depend on which pose of the rotor happens to be the tallest. Individually
        /// cropped frames have no reference frame, so the tallest stands in.
        /// </summary>
        private static float ScaleFor(SetSpec spec, string[] paths, bool aligned)
        {
            int measured = 0;

            if (aligned)
            {
                measured = SpriteMeasure.Measure(paths[0], SolidAlpha).Bounds.height;
            }
            else
            {
                foreach (string path in paths)
                {
                    int height = SpriteMeasure.Measure(path, SolidAlpha).Bounds.height;
                    if (height > measured) measured = height;
                }
            }

            if (measured <= 0)
            {
                Debug.LogWarning($"[ArvinRunner] Could not measure {spec.Folder}; using 100 PPU.");
                return 100f;
            }

            return measured / Mathf.Max(0.05f, spec.Height);
        }

        // ================================================================= //

        /// <summary>The frames of one obstacle, in order, or an empty array.</summary>
        public static Sprite[] Frames(string folder)
        {
            if (Cache.TryGetValue(folder, out Sprite[] cached) &&
                cached.Length > 0 && cached[0] != null)
                return cached;

            var sprites = new List<Sprite>();

            foreach (string path in FramePaths(folder))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) sprites.Add(sprite);
            }

            Sprite[] result = sprites.ToArray();
            Cache[folder] = result;
            return result;
        }

        /// <summary>One frame by index, clamped, or null if the folder is empty.</summary>
        public static Sprite Frame(string folder, int index)
        {
            Sprite[] frames = Frames(folder);
            if (frames.Length == 0) return null;

            return frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }

        // ---- finding the files ------------------------------------------- //

        private static string[] FramePaths(string folder)
        {
            string found = FindFolder(folder);
            if (found == null) return new string[0];

            return Directory.GetFiles(found, "*.png", SearchOption.TopDirectoryOnly)
                            .Select(p => p.Replace('\\', '/'))
                            .OrderBy(LeadingNumber)
                            .ThenBy(p => p)
                            .ToArray();
        }

        private static string FindFolder(string folder)
        {
            if (!Directory.Exists(RootFolder)) return null;

            foreach (string candidate in Directory.GetDirectories(RootFolder))
            {
                if (string.Equals(Path.GetFileName(candidate), folder,
                                  System.StringComparison.OrdinalIgnoreCase))
                    return candidate.Replace('\\', '/');
            }

            return null;
        }

        /// <summary>
        /// Orders by the first number in the file name.
        ///
        /// Not the trailing number, which is the obvious rule and the wrong one
        /// here: these frames are named for what they do rather than numbered on
        /// the end - frame_03_fire_launch, frame_04_missile_away - so a
        /// trailing-digit rule finds no number at all and quietly leaves the
        /// sequence to alphabetical order. That happens to be right while the
        /// numbers are zero-padded, and wrong the moment there are ten frames.
        /// </summary>
        private static int LeadingNumber(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);

            for (int i = 0; i < name.Length; i++)
            {
                if (!char.IsDigit(name[i])) continue;

                int end = i;
                while (end < name.Length && char.IsDigit(name[end])) end++;

                return int.TryParse(name.Substring(i, end - i), out int value) ? value : 0;
            }

            return 0;
        }
    }
}
