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

            // The coin: half a turn of a minted gold coin, and the sparkle that
            // glints on it. See CoinPainter for what makes it read as metal.
            int coinPpu = CoinPainter.PixelsPerUnit;
            WriteSprite("coin", ToTexture(CoinPainter.Coin(CoinPainter.FrameSize, 0)), pixelsPerUnit: coinPpu);
            for (int i = 0; i < CoinPainter.SpinFrames; i++)
                WriteSprite($"coin_{i:00}", ToTexture(CoinPainter.Coin(CoinPainter.FrameSize, i)), pixelsPerUnit: coinPpu);
            WriteSprite("coin_glint", ToTexture(CoinPainter.Glint(CoinPainter.GlintSize)), pixelsPerUnit: coinPpu);

            WriteSprite("beam", Box(8, 8, border: false), tileable: true);
            WriteSprite("glass", Glass(48, 48));
            WriteSprite("finish", Checker(32, 64, 16), tileable: true);

            // The helicopter strike: the designator's mark, the missile with its
            // motor flame and smoke, the laser, the fireball frames and the scorch
            // left behind. See StrikePainter for what makes each read as real.
            int strikePpu = StrikePainter.PixelsPerUnit;
            WriteSprite("target", ToTexture(StrikePainter.Marker(512, 192)), pixelsPerUnit: strikePpu);
            WriteSprite("missile", ToTexture(StrikePainter.Missile(256, 64)), pixelsPerUnit: strikePpu);
            WriteSprite("missile_flame", ToTexture(StrikePainter.Flame(128, 48)), pixelsPerUnit: strikePpu);
            WriteSprite("bomb", ToTexture(StrikePainter.Bomb(256, 72)), pixelsPerUnit: strikePpu);
            WriteSprite("laser", ToTexture(StrikePainter.Laser(64, 16)), pixelsPerUnit: strikePpu);
            WriteSprite("scorch", ToTexture(StrikePainter.Scorch(256, 96)), pixelsPerUnit: strikePpu);

            for (int i = 0; i < StrikePainter.SmokeVariants; i++)
                WriteSprite($"smoke_{i}", ToTexture(StrikePainter.Smoke(128, i)), pixelsPerUnit: strikePpu);

            for (int i = 0; i < StrikePainter.BlastFrames; i++)
                WriteSprite($"blast_{i:00}", ToTexture(StrikePainter.Blast(256, i)), pixelsPerUnit: strikePpu);

            // The single blast sprite the frames replaced.
            if (File.Exists($"{ArtRoot}/blast.png")) AssetDatabase.DeleteAsset($"{ArtRoot}/blast.png");

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
