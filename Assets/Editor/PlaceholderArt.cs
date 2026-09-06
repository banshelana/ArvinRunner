using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Generates the temporary sprites the setup tool wires up, so the game is
    /// playable before any real art exists.
    ///
    /// Everything here is a placeholder. When your own sprites arrive, replace
    /// the files in Assets/Art - the prefabs keep their references and nothing
    /// else has to change.
    /// </summary>
    public static class PlaceholderArt
    {
        public const int PixelsPerUnit = 32;
        private const string ArtRoot = "Assets/Art/Generated";

        public static void GenerateAll()
        {
            Directory.CreateDirectory(ArtRoot);

            WriteSprite("box", Box(32, 32), tileable: true);
            WriteSprite("runner", Runner(28, 56));
            WriteSprite("spikes", Spikes(64, 24));
            WriteSprite("coin", Coin(24));
            WriteSprite("beam", Box(8, 8, border: false), tileable: true);
            WriteSprite("glass", Glass(48, 48));
            WriteSprite("finish", Checker(32, 64, 16), tileable: true);

            WriteSprite("sky", Gradient(64, 256,
                new Color(0.05f, 0.06f, 0.14f), new Color(0.16f, 0.10f, 0.26f)), tileable: true);
            WriteSprite("skyline_far", Skyline(512, 200, seed: 11,
                new Color(0.13f, 0.14f, 0.26f), minH: 40, maxH: 150, windowChance: 0f), tileable: true);
            WriteSprite("skyline_mid", Skyline(512, 240, seed: 27,
                new Color(0.09f, 0.10f, 0.20f), minH: 70, maxH: 210, windowChance: 0.35f), tileable: true);
            WriteSprite("skyline_near", Skyline(512, 300, seed: 43,
                new Color(0.05f, 0.05f, 0.12f), minH: 110, maxH: 280, windowChance: 0.2f), tileable: true);

            AssetDatabase.Refresh();
        }

        public static Sprite Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/{name}.png");
        }

        // ================================================================= //
        // Texture painters
        // ================================================================= //

        private static Texture2D Box(int w, int h, bool border = true)
        {
            var tex = NewTexture(w, h);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                tex.SetPixel(x, y, border && edge ? new Color(1f, 1f, 1f, 0.55f) : Color.white);
            }

            tex.Apply();
            return tex;
        }

        /// <summary>A blocky humanoid silhouette - readable at a glance, obviously temporary.</summary>
        private static Texture2D Runner(int w, int h)
        {
            var tex = NewTexture(w, h);
            Color body = new Color(0.95f, 0.95f, 1f, 1f);

            // legs, torso, head as stacked bands
            FillRect(tex, w / 2 - 8, 0, 6, h / 3, body);              // back leg
            FillRect(tex, w / 2 + 1, 0, 6, h / 3, body);              // front leg
            FillRect(tex, w / 2 - 7, h / 3, 14, h / 3, body);         // torso
            FillCircle(tex, w / 2, h - h / 6, h / 7, body);           // head
            FillRect(tex, w / 2 + 5, h / 2, 9, 4, body);              // outstretched arm

            tex.Apply();
            return tex;
        }

        private static Texture2D Spikes(int w, int h)
        {
            var tex = NewTexture(w, h);
            int teeth = 4;
            int toothWidth = w / teeth;

            for (int t = 0; t < teeth; t++)
            for (int x = 0; x < toothWidth; x++)
            {
                float centre = Mathf.Abs(x - toothWidth * 0.5f) / (toothWidth * 0.5f);
                int height = Mathf.RoundToInt(h * (1f - centre));
                for (int y = 0; y < height; y++)
                    tex.SetPixel(t * toothWidth + x, y, Color.white);
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D Coin(int size)
        {
            var tex = NewTexture(size, size);
            FillCircle(tex, size / 2, size / 2, size / 2 - 1, Color.white);
            FillCircle(tex, size / 2, size / 2, size / 4, new Color(1f, 1f, 1f, 0.45f));
            tex.Apply();
            return tex;
        }

        private static Texture2D Glass(int w, int h)
        {
            var tex = NewTexture(w, h);
            Color pane = new Color(1f, 1f, 1f, 0.30f);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool edge = x < 2 || y < 2 || x >= w - 2 || y >= h - 2;
                bool shine = Mathf.Abs((x + y) % 20) < 3;
                tex.SetPixel(x, y, edge ? new Color(1f, 1f, 1f, 0.8f)
                                        : (shine ? new Color(1f, 1f, 1f, 0.5f) : pane));
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Chequered flag pattern. A solid coloured slab reads as an obstacle;
        /// chequers read as a finish line in any language.
        /// </summary>
        private static Texture2D Checker(int w, int h, int cell)
        {
            var tex = NewTexture(w, h);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool dark = ((x / cell) + (y / cell)) % 2 == 0;
                tex.SetPixel(x, y, dark ? new Color(0.06f, 0.07f, 0.10f) : Color.white);
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D Gradient(int w, int h, Color bottom, Color top)
        {
            var tex = NewTexture(w, h);

            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(bottom, top, y / (float)(h - 1));
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, c);
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// A tileable row of towers. Buildings never straddle the left or right
        /// edge, so the strip repeats without a visible seam.
        /// </summary>
        private static Texture2D Skyline(int w, int h, int seed, Color colour, int minH, int maxH, float windowChance)
        {
            var tex = NewTexture(w, h);
            var random = new System.Random(seed);

            int x = 2;
            while (x < w - 2)
            {
                int buildingWidth = random.Next(24, 70);
                if (x + buildingWidth > w - 2) buildingWidth = w - 2 - x;
                if (buildingWidth < 8) break;

                int buildingHeight = random.Next(minH, maxH);
                FillRect(tex, x, 0, buildingWidth, buildingHeight, colour);

                // A slimmer rooftop block, so the silhouette is not just rectangles.
                if (random.NextDouble() < 0.4)
                {
                    int capWidth = Mathf.Max(4, buildingWidth / 3);
                    FillRect(tex, x + buildingWidth / 2 - capWidth / 2, buildingHeight,
                             capWidth, random.Next(10, 40), colour);
                }

                if (windowChance > 0f)
                {
                    Color lit = new Color(1f, 0.85f, 0.5f, 0.5f);
                    for (int wy = 8; wy < buildingHeight - 8; wy += 10)
                    for (int wx = 5; wx < buildingWidth - 5; wx += 9)
                        if (random.NextDouble() < windowChance)
                            FillRect(tex, x + wx, wy, 4, 5, lit);
                }

                x += buildingWidth + random.Next(3, 14);
            }

            tex.Apply();
            return tex;
        }

        // ================================================================= //
        // Helpers
        // ================================================================= //

        private static Texture2D NewTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var clear = new Color[w * h];
            tex.SetPixels(clear);
            return tex;
        }

        private static void FillRect(Texture2D tex, int x0, int y0, int w, int h, Color colour)
        {
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                if (x >= 0 && y >= 0 && x < tex.width && y < tex.height)
                    tex.SetPixel(x, y, colour);
        }

        private static void FillCircle(Texture2D tex, int cx, int cy, int radius, Color colour)
        {
            for (int y = cy - radius; y <= cy + radius; y++)
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) continue;
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= radius * radius) tex.SetPixel(x, y, colour);
            }
        }

        private static void WriteSprite(string name, Texture2D texture, bool tileable = false)
        {
            string path = $"{ArtRoot}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;

            // Tiled SpriteRenderers need Full Rect meshes and repeating wrap.
            importer.wrapMode = tileable ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }
    }
}
