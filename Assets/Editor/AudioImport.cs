using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Sets up the music in Assets/Audio.
    ///
    /// This exists because of how the tracks arrive rather than to be clever
    /// about sound. chasing.wav is 35MB of 32-bit float PCM and mainmenu.wav
    /// another 11MB, and Unity's defaults would decompress both into memory at
    /// load - so the two of them would cost more RAM than everything else in the
    /// game put together, on a runner meant for a phone.
    ///
    /// So both are imported as Vorbis and streamed from disk. Music is the one
    /// thing streaming is unambiguously right for: it is long, it plays once
    /// from start to end, and nothing needs it to start on a specific frame.
    /// </summary>
    public static class AudioImport
    {
        public const string RootFolder = "Assets/Audio";

        /// <summary>Plays through a run.</summary>
        public const string GameplayTrack = "chasing";

        /// <summary>Plays on the front end.</summary>
        public const string MenuTrack = "mainmenu";

        private static readonly string[] Extensions = { ".wav", ".ogg", ".mp3", ".aiff", ".aif" };

        private static readonly Dictionary<string, AudioClip> Cache =
            new Dictionary<string, AudioClip>();

        // ================================================================= //

        [MenuItem("ArvinRunner/Re-import Audio", priority = 13)]
        public static void ImportAll()
        {
            Cache.Clear();

            if (!AssetDatabase.IsValidFolder(RootFolder))
            {
                Debug.LogWarning($"[ArvinRunner] No audio folder at {RootFolder}. The game runs silent.");
                return;
            }

            var imported = new List<string>();

            foreach (string path in Directory.GetFiles(RootFolder))
            {
                string clean = path.Replace('\\', '/');
                if (!IsAudio(clean)) continue;

                if (Import(clean)) imported.Add(Path.GetFileNameWithoutExtension(clean));
            }

            AssetDatabase.Refresh();

            if (imported.Count == 0)
            {
                Debug.LogWarning($"[ArvinRunner] {RootFolder} holds no audio.");
                return;
            }

            Debug.Log($"[ArvinRunner] Audio imported: {string.Join(", ", imported)}.");
            Verify();
        }

        /// <summary>
        /// Says so when a track the game asks for by name is not there, rather
        /// than letting the scene build with an empty AudioSource and leaving it
        /// to be noticed as silence.
        /// </summary>
        private static void Verify()
        {
            foreach (string wanted in new[] { GameplayTrack, MenuTrack })
            {
                if (Load(wanted) == null)
                    Debug.LogWarning($"[ArvinRunner] No '{wanted}' track in {RootFolder}. " +
                                     "That scene will be silent.");
            }
        }

        private static bool Import(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return false;

            importer.forceToMono = false;
            importer.loadInBackground = true;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;

            importer.SaveAndReimport();
            return true;
        }

        /// <summary>The clip by file name, whatever it was authored as.</summary>
        public static AudioClip Load(string trackName)
        {
            if (Cache.TryGetValue(trackName, out AudioClip cached) && cached != null)
                return cached;

            foreach (string extension in Extensions)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    $"{RootFolder}/{trackName}{extension}");

                if (clip == null) continue;

                Cache[trackName] = clip;
                return clip;
            }

            return null;
        }

        private static bool IsAudio(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();

            foreach (string candidate in Extensions)
                if (extension == candidate) return true;

            return false;
        }
    }
}
