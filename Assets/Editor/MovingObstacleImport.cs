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
    /// code (<see cref="SpriteMeasure"/>): a pivot on the drawn figure rather
    /// than the canvas, so a frame's cropping stops mattering. The scaling is
    /// the part that differs. The runner's clips all share one pixels-per-unit
    /// because they are all the same character and it must not change size; two
    /// unrelated vehicles have no such relationship, so each folder here gets
    /// its own scale from its own declared height.
    ///
    /// Heights are chosen against the runner rather than against the real
    /// machines. The bike is 1.55 so it reads as something to clear against a
    /// 1.75 standing runner and a 3.20 jump; the helicopter is 3.20 at the top
    /// of its rotor so it has the presence of a real aircraft next to the 10.6
    /// unit bus, without filling the screen.
    /// </summary>
    public static class MovingObstacleImport
    {
        public const string RootFolder = "Assets/Art/MovingObstacles";

        /// <summary>The frames top out at 363x287, so 512 never downscales one.</summary>
        private const int MaxTextureSize = 512;

        private struct SetSpec
        {
            public string Folder;

            /// <summary>World height of the tallest frame in the folder.</summary>
            public float TallestHeight;
        }

        private static readonly SetSpec[] Sets =
        {
            // 1.55 tall, and 1.67-1.96 long depending on how far the rider is
            // leaned over. Comfortably under the 3.20 jump.
            new SetSpec { Folder = "moto", TallestHeight = 1.55f },

            // Measured at the rotor's full extent, which is the tallest frame -
            // so the fuselage lands around 2.33 and the aircraft about 6.8 long.
            new SetSpec { Folder = "helli", TallestHeight = 3.20f }
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

                // --- pass two: one scale per folder, pivots per frame ----------
                done = 0;
                foreach (KeyValuePair<SetSpec, string[]> set in found)
                {
                    float pixelsPerUnit = ScaleFor(set.Key, set.Value);

                    foreach (string path in set.Value)
                    {
                        EditorUtility.DisplayProgressBar("ArvinRunner", "Importing " + Path.GetFileName(path),
                                                         0.5f + 0.5f * done++ / total);

                        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        if (texture == null) continue;

                        Figure figure = SpriteMeasure.Measure(path);
                        if (figure.IsEmpty)
                        {
                            Debug.LogWarning($"[ArvinRunner] {path} looks fully transparent.");
                            continue;
                        }

                        SpriteMeasure.ApplyFrame(
                            path,
                            SpriteMeasure.PivotFor(figure, texture.width, texture.height),
                            pixelsPerUnit);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            string summary = string.Join(", ", found.Select(f => $"{f.Key.Folder} {f.Value.Length}"));
            Debug.Log($"[ArvinRunner] Moving obstacles imported ({summary}).");
        }

        /// <summary>Pixels-per-unit that brings the folder's tallest frame out at
        /// its declared height.</summary>
        private static float ScaleFor(SetSpec spec, string[] paths)
        {
            int tallest = 0;
            foreach (string path in paths)
            {
                Figure figure = SpriteMeasure.Measure(path);
                if (figure.Bounds.height > tallest) tallest = figure.Bounds.height;
            }

            if (tallest <= 0)
            {
                Debug.LogWarning($"[ArvinRunner] Could not measure {spec.Folder}; using 100 PPU.");
                return 100f;
            }

            return tallest / Mathf.Max(0.05f, spec.TallestHeight);
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

        /// <summary>
        /// One frame by index, clamped. Used for the poses a component needs to
        /// hold rather than cycle - the helicopter's firing frame, for instance.
        /// </summary>
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
                            .OrderBy(TrailingNumber)
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

        /// <summary>Orders by the number at the end of the name, so frame 10
        /// plays after frame 9 rather than after frame 1.</summary>
        private static int TrailingNumber(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            int end = name.Length;

            while (end > 0 && char.IsDigit(name[end - 1])) end--;

            return end < name.Length && int.TryParse(name.Substring(end), out int value)
                ? value
                : 0;
        }
    }
}
