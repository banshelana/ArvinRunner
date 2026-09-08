using UnityEditor;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>What a frame's drawn figure occupies, and where its weight sits.</summary>
    public struct Figure
    {
        /// <summary>Alpha bounds within the canvas, in Unity's bottom-up texture
        /// space - so y is already the height above the bottom edge.</summary>
        public RectInt Bounds;

        /// <summary>Mean x of every opaque pixel, in the same space.</summary>
        public float CentroidX;

        public bool IsEmpty => Bounds.width <= 0 || Bounds.height <= 0;
    }

    /// <summary>
    /// Reads the shape out of a PNG so the importers can place a pivot on the
    /// drawn figure rather than on its canvas.
    ///
    /// Shared by the two frame importers because they want the same two numbers
    /// for the same reason: art arrives cropped inconsistently, and both the
    /// runner and the moving obstacles have to hold still while their frames
    /// change underneath them.
    /// </summary>
    public static class SpriteMeasure
    {
        /// <summary>Alpha at or below this counts as empty.</summary>
        public const byte AlphaThreshold = 12;

        /// <summary>
        /// Re-imports a texture in the state needed to read its pixels. Call this
        /// on every frame before measuring any of them - GetPixels32 throws on a
        /// texture that is not marked readable.
        /// </summary>
        public static void MakeReadable(string path, int maxTextureSize)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = maxTextureSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Measures a frame in one pass over its pixels, returning both the alpha
        /// bounds and the centroid together because walking the texture twice for
        /// them separately would be the expensive part.
        ///
        /// <paramref name="threshold"/> decides what counts as drawn. The default
        /// catches everything, which is what you want for a figure whose outline
        /// is the whole subject. Raise it to anchor on solid pixels only, and so
        /// ignore rotor blur, smoke and exhaust - soft effects that belong to the
        /// picture but should not be allowed to define where the object *is*.
        /// </summary>
        public static Figure Measure(string path, byte threshold = AlphaThreshold)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) return default;

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (UnityException e)
            {
                Debug.LogWarning($"[ArvinRunner] {path} is not readable: {e.Message}");
                return default;
            }

            int width = texture.width;
            int height = texture.height;
            int xMin = width, yMin = height, xMax = -1, yMax = -1;

            long opaque = 0;
            long sumX = 0;

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x].a <= threshold) continue;

                    if (x < xMin) xMin = x;
                    if (x > xMax) xMax = x;
                    if (y < yMin) yMin = y;
                    if (y > yMax) yMax = y;

                    opaque++;
                    sumX += x;
                }
            }

            if (xMax < xMin) return default;

            return new Figure
            {
                Bounds = new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1),
                CentroidX = sumX / (float)opaque
            };
        }

        /// <summary>
        /// The pivot to give a frame: the centre of mass across, the lowest drawn
        /// pixel down, both as a fraction of the canvas.
        ///
        /// The two axes are anchored differently on purpose. Down, the lowest
        /// pixel is what meets the ground, and cropping varies enough between
        /// frames that using the canvas edge leaves things hovering. Across, the
        /// centroid holds the mass still and lets limbs, exhaust plumes and rotor
        /// blades move around it, where the bounding box would be dragged about
        /// by whatever is sticking out that frame.
        /// </summary>
        public static Vector2 PivotFor(Figure figure, int textureWidth, int textureHeight)
        {
            return new Vector2(figure.CentroidX / textureWidth,
                               figure.Bounds.y / (float)textureHeight);
        }

        /// <summary>
        /// Applies a pivot and scale to one frame and turns readability back off.
        /// </summary>
        public static void ApplyFrame(string path, Vector2 pivot, float pixelsPerUnit)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.isReadable = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 1;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }
    }
}
