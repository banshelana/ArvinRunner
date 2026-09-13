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

            // The helicopter strike. No art was drawn for the shot itself - the
            // heli frames only carry the muzzle flash - so these three stand in.
            WriteSprite("target", Target(64));
            WriteSprite("missile", Missile(28, 10));
            WriteSprite("blast", Blast(96));

            // The backdrop, far to near: the sun, a far ridge of hills, nearer
            // hills, a band of towers, and a street of houses. Each is painted in
            // a light neutral grey and the level's theme tints it - a tint can
            // only darken, so the textures carry the brightness and the theme the
            // colour. See BackdropPainter for how the layers carry depth.
            WriteSprite("sky", Gradient(64, 256,
                new Color(1f, 1f, 1f), new Color(0.93f, 0.93f, 0.93f)), tileable: true);

            int backdropPpu = BackdropPainter.PixelsPerUnit;
            WriteSprite("sun", ToTexture(BackdropPainter.Sun(384)), tileable: false, pixelsPerUnit: backdropPpu);
            WriteSprite("ridge", ToTexture(BackdropPainter.Hills(1536, 384, 5, BackdropPainter.FarRidgeGrey,
                0.35f, 0.80f, 0.5f, 0.35f)), tileable: true, pixelsPerUnit: backdropPpu);
            WriteSprite("hills", ToTexture(BackdropPainter.Hills(1024, 448, 17, BackdropPainter.HillGrey,
                0.30f, 0.70f, 0.9f, 0.40f)), tileable: true, pixelsPerUnit: backdropPpu);
            WriteSprite("towers", ToTexture(BackdropPainter.Towers(1024, 768, 29, BackdropPainter.TowerGrey)),
                tileable: true, pixelsPerUnit: backdropPpu);
            WriteSprite("houses", ToTexture(BackdropPainter.Houses(1024, 1024, 43, BackdropPainter.HouseGrey, 640f)),
                tileable: true, pixelsPerUnit: backdropPpu);

            // The single-row skylines these replaced.
            foreach (string old in new[] { "skyline_far", "skyline_mid", "skyline_near" })
            {
                string path = $"{ArtRoot}/{old}.png";
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) AssetDatabase.DeleteAsset(path);
            }

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
            // A dark rim, so a gold coin still reads against a pale sky. The tint
            // multiplies it into a deep amber edge rather than washing it out.
            FillCircle(tex, size / 2, size / 2, size / 2 - 1, new Color(0.28f, 0.28f, 0.28f));
            FillCircle(tex, size / 2, size / 2, size / 2 - 3, Color.white);
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

        // ================================================================= //
        // The helicopter strike
        // ================================================================= //

        /// <summary>
        /// The warning marker: a ring with cross-hairs, drawn as an outline so it
        /// reads on top of the roof without hiding what is underneath it.
        /// </summary>
        private static Texture2D Target(int size)
        {
            var tex = NewTexture(size, size);
            Color ink = new Color(1f, 0.35f, 0.15f, 1f);

            int centre = size / 2;
            int outer = centre - 2;
            int inner = outer - 4;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int dx = x - centre, dy = y - centre;
                int distance = dx * dx + dy * dy;

                bool ring = distance <= outer * outer && distance >= inner * inner;

                // Cross-hairs, broken at the centre so the ring stays the shape
                // the eye catches first.
                bool spoke = (Mathf.Abs(dx) <= 1 || Mathf.Abs(dy) <= 1) &&
                             distance <= outer * outer &&
                             distance >= (inner - 8) * (inner - 8);

                if (ring || spoke) tex.SetPixel(x, y, ink);
            }

            tex.Apply();
            return tex;
        }

        /// <summary>A blunt dart, pointing +X so the code can just rotate it.</summary>
        private static Texture2D Missile(int w, int h)
        {
            var tex = NewTexture(w, h);
            Color body = new Color(0.85f, 0.85f, 0.88f, 1f);
            Color tip = new Color(1f, 0.5f, 0.2f, 1f);

            FillRect(tex, 0, h / 2 - 2, w - 8, 4, body);
            FillRect(tex, 0, 0, 5, h, body * 0.7f);        // fins at the tail
            FillCircle(tex, w - 8, h / 2, 4, tip);         // nose

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// The impact: a hot core falling off to a soft edge. Drawn with the
        /// falloff baked in so the sprite can simply be scaled and faded rather
        /// than needing a particle system.
        /// </summary>
        private static Texture2D Blast(int size)
        {
            var tex = NewTexture(size, size);

            int centre = size / 2;
            float radius = centre - 1f;

            Color core = new Color(1f, 0.95f, 0.70f, 1f);
            Color edge = new Color(0.95f, 0.30f, 0.08f, 0f);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - centre, dy = y - centre;
                float distance = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                if (distance > 1f) continue;

                tex.SetPixel(x, y, Color.Lerp(core, edge, distance * distance));
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

        /// <summary>A painted image as a texture. Both keep the bottom row first.</summary>
        private static Texture2D ToTexture(PaintImage image)
        {
            var tex = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false);
            var colours = new Color[image.Width * image.Height];

            for (int i = 0; i < colours.Length; i++)
                colours[i] = new Color(image.Rgba[i * 4], image.Rgba[i * 4 + 1],
                                       image.Rgba[i * 4 + 2], image.Rgba[i * 4 + 3]);

            tex.SetPixels(colours);
            tex.Apply();
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

        private static void WriteSprite(string name, Texture2D texture, bool tileable = false,
                                        int pixelsPerUnit = PixelsPerUnit)
        {
            string path = $"{ArtRoot}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;

            // The backdrop layers are 1536 wide; the default 2048 cap is enough,
            // but say it so a wider strip is not quietly downscaled.
            importer.maxTextureSize = 2048;
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
