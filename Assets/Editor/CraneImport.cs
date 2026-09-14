using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Turns the crane frames in Art/MovingObstacles/Crane into the pieces the
    /// swinging crane is built from: one sprite of the tower and jib, and one
    /// sprite per frame of the rope, hook and crate. See <see cref="CraneRig"/>
    /// for why the frames are taken apart rather than played as they are.
    ///
    /// The pieces are written to Art/Generated/Crane, and are regenerated from
    /// the frames on every build, so replacing the frames is all a new crane
    /// needs. Every piece keeps the frames' canvas and has its pivot on the point
    /// the load swings about, so tower and load line up by sharing a position.
    /// </summary>
    public static class CraneImport
    {
        public const string SourceFolder = MovingObstacleImport.RootFolder + "/Crane";
        private const string OutputFolder = "Assets/Art/Generated/Crane";

        /// <summary>
        /// World height of the crane, foot of the tower to the top of the mast.
        ///
        /// Set by the crate rather than by the tower, because the crate is what
        /// the runner deals with. At 6.6 it is 1.8 across and 1.7 tall, riding
        /// 0.2 off the roof at the bottom of its swing and 0.6 at the ends: about
        /// the runner's size, and far enough under the 3.2 jump that one jump
        /// clears it at any point in the swing on a flat roof. The tower is small
        /// for a real crane, but reads as one - a rooftop crane beside a runner.
        /// </summary>
        public const float CraneHeight = 6.6f;

        /// <summary>Share of the crate's drawn box that kills. The top face is
        /// drawn in perspective and the corners are rounded, and a runner who
        /// sees daylight between their feet and the crate has cleared it.</summary>
        public const float CrateHitShare = 0.85f;

        private const int MaxTextureSize = 2048;

        /// <summary>Everything the chunk factory needs to build a crane.</summary>
        public sealed class Rig
        {
            public Sprite Tower;
            public Sprite[] Loads;

            /// <summary>Per load sprite, the swing angle it was drawn at, in degrees.</summary>
            public float[] Angles;

            /// <summary>Per load sprite, the crate's centre relative to the swing
            /// pivot, in the drawing's own frame - the load sprite's local space.</summary>
            public Vector2[] CrateCentres;

            /// <summary>Per load sprite, the crate's tilt as drawn, in degrees.</summary>
            public float[] CrateTilts;

            /// <summary>The crate's box, square-on.</summary>
            public Vector2 CrateSize;

            /// <summary>The swing pivot's height above the tower's foot.</summary>
            public float PivotHeight;

            /// <summary>The base plate's left and right edges, relative to the pivot.</summary>
            public float BaseLeft;
            public float BaseRight;

            /// <summary>Seconds for one whole swing.</summary>
            public float Period;
        }

        private static Rig _cached;

        // ================================================================= //

        /// <summary>The crane pieces, splitting the frames first if this editor
        /// session has not already. Null when there are no crane frames.</summary>
        public static Rig Load()
        {
            if (_cached != null && _cached.Tower != null && _cached.Loads.All(s => s != null))
                return _cached;

            return ImportAll();
        }

        [MenuItem("ArvinRunner/Re-import Crane", priority = 13)]
        public static Rig ImportAll()
        {
            _cached = null;

            string[] paths = FramePaths();
            if (paths.Length < 3)
            {
                Debug.LogWarning($"[ArvinRunner] Fewer than three crane frames in {SourceFolder}; " +
                                 "the crane falls back to the plain pendulum.");
                return null;
            }

            try
            {
                EditorUtility.DisplayProgressBar("ArvinRunner", "Reading the crane", 0f);
                var frames = new List<byte[]>();
                int width = 0, height = 0;

                foreach (string path in paths)
                {
                    SpriteMeasure.MakeReadable(path, MaxTextureSize);
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture == null) continue;

                    if (width == 0)
                    {
                        width = texture.width;
                        height = texture.height;
                    }
                    else if (texture.width != width || texture.height != height)
                    {
                        Debug.LogWarning($"[ArvinRunner] {path} is {texture.width}x{texture.height}, " +
                                         $"not {width}x{height} like the first crane frame. The " +
                                         "frames must share one canvas; the crane falls back to " +
                                         "the plain pendulum.");
                        return null;
                    }

                    frames.Add(ToBytes(texture.GetPixels32()));
                }

                EditorUtility.DisplayProgressBar("ArvinRunner", "Taking the crane apart", 0.3f);
                CraneRig rig;
                try
                {
                    rig = CraneRig.Split(frames, width, height);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[ArvinRunner] Could not split the crane frames: {e.Message}");
                    return null;
                }

                // Solid height of the tower, mast to foot, sets the scale.
                float pixelsPerUnit = (rig.Top - rig.BaseBottom + 1) / CraneHeight;
                var pivot = new Vector2(rig.PivotX / width, rig.PivotY / height);

                EditorUtil.EnsureFolder(OutputFolder);
                EditorUtility.DisplayProgressBar("ArvinRunner", "Writing the crane", 0.6f);

                Sprite tower = Write("crane_tower", rig.Tower, width, height, pivot, pixelsPerUnit);
                var loads = new Sprite[rig.Loads.Length];
                for (int k = 0; k < loads.Length; k++)
                    loads[k] = Write($"crane_load_{k:00}", rig.Loads[k], width, height, pivot, pixelsPerUnit);

                RemoveStaleLoads(loads.Length);

                var centres = new Vector2[loads.Length];
                float reach = 0f;
                for (int k = 0; k < loads.Length; k++)
                {
                    centres[k] = new Vector2(rig.CrateX[k] - rig.PivotX, rig.CrateY[k] - rig.PivotY) / pixelsPerUnit;
                    reach += centres[k].magnitude / loads.Length;
                }

                // A pendulum's own period, for a crate hanging this far below the
                // pivot at this scale: 2π√(L/g). About 4.2 seconds at 6.6 - slow,
                // which is what makes it read as a heavy load rather than a toy.
                float period = 2f * Mathf.PI * Mathf.Sqrt(reach / 9.81f);

                _cached = new Rig
                {
                    Tower = tower,
                    Loads = loads,
                    Angles = rig.Angles,
                    CrateCentres = centres,
                    CrateTilts = rig.CrateTilt,
                    CrateSize = new Vector2(rig.CrateWidth, rig.CrateHeight) / pixelsPerUnit,
                    PivotHeight = (rig.PivotY - rig.BaseBottom) / pixelsPerUnit,
                    BaseLeft = (rig.BaseLeft - rig.PivotX) / pixelsPerUnit,
                    BaseRight = (rig.BaseRight + 1 - rig.PivotX) / pixelsPerUnit,
                    Period = period
                };

                Debug.Log($"[ArvinRunner] Crane imported: {loads.Length} frames, swing ±{rig.Amplitude:F1}° " +
                          $"about a pivot {_cached.PivotHeight:F2} up (radius spread {rig.RadiusSpread:F1}px), " +
                          $"crate {_cached.CrateSize.x:F2}x{_cached.CrateSize.y:F2}, period {period:F2}s.");

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

        /// <summary>Crane1 ... Crane24: the number in the name, so 10 follows 9.</summary>
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

        private static Sprite Write(string name, byte[] rgba, int width, int height,
                                    Vector2 pivot, float pixelsPerUnit)
        {
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();

            string path = $"{OutputFolder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.maxTextureSize = MaxTextureSize;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 1;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Load sprites left over from a crane that had more frames.</summary>
        private static void RemoveStaleLoads(int count)
        {
            if (!Directory.Exists(OutputFolder)) return;

            foreach (string file in Directory.GetFiles(OutputFolder, "crane_load_*.png"))
            {
                string digits = new string(Path.GetFileNameWithoutExtension(file).Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out int index) && index >= count)
                    AssetDatabase.DeleteAsset(file.Replace('\\', '/'));
            }
        }
    }
}
