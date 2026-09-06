using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Imports the hand-made art and turns each PNG into game-ready sprites.
    ///
    /// Three things happen per image:
    ///  1. Transparent padding is trimmed, so a sprite's bottom edge is the
    ///     object's actual base and not empty pixels.
    ///  2. Sheets holding several props (the cones sheet) are split into one
    ///     sprite per object, found by looking for fully transparent columns.
    ///  3. Pixels-per-unit is computed from the trimmed height so the prop comes
    ///     out at an exact size in world units - no transform scaling anywhere.
    ///
    /// Every sprite gets a bottom-centre pivot, which means placing a prop is
    /// just "put it at ground level".
    /// </summary>
    public static class ArtImport
    {
        /// <summary>Alpha below this counts as empty when trimming and slicing.</summary>
        private const byte AlphaThreshold = 12;

        /// <summary>Transparent columns narrower than this do not split a sheet.</summary>
        private const int MinGapWidth = 14;

        private struct ArtSpec
        {
            public string Path;
            /// <summary>Sprite names, left to right. One entry means a single trimmed sprite.</summary>
            public string[] Slices;
            /// <summary>World height, in units, of the tallest slice in the image.</summary>
            public float TallestHeight;
        }

        private static readonly ArtSpec[] Specs =
        {
            // The runner is sized to the capsule (1.75) with a little overhang.
            new ArtSpec { Path = "Assets/Art/Player/runner.png",
                          Slices = new[] { "runner" }, TallestHeight = 1.80f },

            // Just under maxVaultHeight (1.6), so it reads as vaultable.
            new ArtSpec { Path = "Assets/Art/Obstacles/dumpster.png",
                          Slices = new[] { "dumpster" }, TallestHeight = 1.50f },

            // Taller than a single jump on purpose - it is climbed in steps.
            new ArtSpec { Path = "Assets/Art/Obstacles/crates.png",
                          Slices = new[] { "crates" }, TallestHeight = 3.20f },

            // Three props on one sheet, tallest (the big cone) at 1.0.
            new ArtSpec { Path = "Assets/Art/Obstacles/cones.png",
                          Slices = new[] { "cone_large", "cone_small", "barricade" },
                          TallestHeight = 1.00f },

            new ArtSpec { Path = "Assets/Art/Obstacles/barrier.png",
                          Slices = new[] { "barrier" }, TallestHeight = 1.05f },

            new ArtSpec { Path = "Assets/Art/Obstacles/acunit.png",
                          Slices = new[] { "acunit" }, TallestHeight = 1.40f }
        };

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // ================================================================= //

        [MenuItem("ArvinRunner/Re-import Art Only", priority = 10)]
        public static void ImportAll()
        {
            Cache.Clear();

            try
            {
                for (int i = 0; i < Specs.Length; i++)
                {
                    ArtSpec spec = Specs[i];
                    EditorUtility.DisplayProgressBar("ArvinRunner", "Importing " + spec.Path,
                                                     i / (float)Specs.Length);
                    Import(spec);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            Verify();
        }

        /// <summary>
        /// Confirms every expected sprite actually exists. Without this a failed
        /// slice shows up only as props quietly falling back to grey boxes.
        /// </summary>
        private static void Verify()
        {
            var missing = new List<string>();
            int found = 0;

            foreach (ArtSpec spec in Specs)
            foreach (string slice in spec.Slices)
            {
                if (Load(slice) != null) found++;
                else missing.Add(slice);
            }

            if (missing.Count == 0)
            {
                Debug.Log($"[ArvinRunner] Art imported: {found} sprites ready.");
                return;
            }

            Debug.LogWarning("[ArvinRunner] These sprites did not import, so those props will " +
                             "fall back to plain boxes: " + string.Join(", ", missing) +
                             ". Check the source PNGs are in Assets/Art/.");
        }

        /// <summary>
        /// Finds a sprite produced by <see cref="ImportAll"/>. Returns null if
        /// the source image is missing, so callers can fall back to placeholders.
        /// </summary>
        public static Sprite Load(string spriteName)
        {
            if (Cache.TryGetValue(spriteName, out Sprite cached) && cached != null)
                return cached;

            foreach (ArtSpec spec in Specs)
            {
                bool owns = false;
                foreach (string slice in spec.Slices)
                    if (slice == spriteName) { owns = true; break; }

                if (!owns) continue;

                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(spec.Path))
                {
                    if (asset is Sprite sprite && sprite.name == spriteName)
                    {
                        Cache[spriteName] = sprite;
                        return sprite;
                    }
                }
            }

            return null;
        }

        public static bool IsAvailable(string spriteName) => Load(spriteName) != null;

        // ================================================================= //

        private static void Import(ArtSpec spec)
        {
            var importer = AssetImporter.GetAtPath(spec.Path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[ArvinRunner] Missing art file: {spec.Path}");
                return;
            }

            // --- pass one: make the pixels readable so we can measure them ---
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.Path);
            if (texture == null)
            {
                Debug.LogWarning($"[ArvinRunner] Could not read {spec.Path}");
                return;
            }

            List<RectInt> rects = FindSlices(texture, spec.Slices.Length, spec.Path);
            if (rects.Count == 0) return;

            // --- pass two: apply the slices and the derived scale ------------
            int tallest = 0;
            foreach (RectInt rect in rects) tallest = Mathf.Max(tallest, rect.height);

            float pixelsPerUnit = tallest / Mathf.Max(0.01f, spec.TallestHeight);

            var sheet = new SpriteMetaData[rects.Count];
            for (int i = 0; i < rects.Count; i++)
            {
                sheet[i] = new SpriteMetaData
                {
                    name = i < spec.Slices.Length ? spec.Slices[i] : spec.Slices[0] + "_" + i,
                    rect = new Rect(rects[i].x, rects[i].y, rects[i].width, rects[i].height),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, 0f),   // stand it on the ground
                    border = Vector4.zero
                };
            }

            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = sheet;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.isReadable = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 1;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        /// <summary>
        /// Splits an image into one rect per object by scanning for columns that
        /// are entirely transparent, then trimming each group vertically.
        /// </summary>
        private static List<RectInt> FindSlices(Texture2D texture, int expected, string path)
        {
            var result = new List<RectInt>();

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (UnityException e)
            {
                Debug.LogWarning($"[ArvinRunner] {path} is not readable: {e.Message}");
                return result;
            }

            int width = texture.width;
            int height = texture.height;

            // Which columns contain anything at all.
            var occupied = new bool[width];
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                    if (pixels[row + x].a > AlphaThreshold) occupied[x] = true;
            }

            // Group consecutive occupied columns, ignoring narrow gaps so a
            // single prop with a slit in it does not get cut in half.
            var groups = new List<Vector2Int>();   // (xMin, xMax) inclusive
            int start = -1;
            int gap = 0;

            for (int x = 0; x < width; x++)
            {
                if (occupied[x])
                {
                    if (start < 0) start = x;
                    gap = 0;
                }
                else if (start >= 0)
                {
                    gap++;
                    if (gap >= MinGapWidth)
                    {
                        groups.Add(new Vector2Int(start, x - gap));
                        start = -1;
                        gap = 0;
                    }
                }
            }

            if (start >= 0) groups.Add(new Vector2Int(start, width - 1));

            if (groups.Count == 0)
            {
                Debug.LogWarning($"[ArvinRunner] {path} looks fully transparent.");
                return result;
            }

            // Too many groups usually means stray specks - keep the widest ones.
            if (groups.Count > expected)
            {
                groups.Sort((a, b) => (b.y - b.x).CompareTo(a.y - a.x));
                groups.RemoveRange(expected, groups.Count - expected);
                groups.Sort((a, b) => a.x.CompareTo(b.x));
            }
            else if (groups.Count < expected)
            {
                Debug.LogWarning($"[ArvinRunner] Expected {expected} objects in {path} " +
                                 $"but found {groups.Count}. Slice it by hand in the Sprite Editor " +
                                 "if the split is wrong.");
            }

            // Trim each group vertically.
            foreach (Vector2Int group in groups)
            {
                int yMin = height, yMax = -1;

                for (int y = 0; y < height; y++)
                {
                    int row = y * width;
                    for (int x = group.x; x <= group.y; x++)
                    {
                        if (pixels[row + x].a <= AlphaThreshold) continue;
                        if (y < yMin) yMin = y;
                        if (y > yMax) yMax = y;
                        break;
                    }
                }

                if (yMax < yMin) continue;

                result.Add(new RectInt(group.x, yMin, group.y - group.x + 1, yMax - yMin + 1));
            }

            return result;
        }
    }
}
