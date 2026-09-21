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

        /// <summary>
        /// The other place frame folders live: whatever is chasing the runner
        /// through a level. Not an obstacle in a chunk, but imported exactly like
        /// one, so it is searched alongside the obstacles rather than given its
        /// own importer.
        /// </summary>
        public const string ChasingFolder = "Assets/Art/Chasing";

        private static readonly string[] SearchRoots = { RootFolder, ChasingFolder };

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

            /// <summary>
            /// Pin cropped frames by their top edge rather than their lowest solid
            /// pixel. For art that moves about a point near its top: the drone
            /// pitches around its tail, so its skids and nose rise and fall 11-21px
            /// through the loop while the tail fin and prop tip hold still. Pinned
            /// by the bottom, the drawn pitch would come out as the tail bouncing.
            /// </summary>
            public bool AnchorTop;

            /// <summary>
            /// Pin cropped frames on a bench: centred across on the seat, and down
            /// on the bottom of the bench legs. Scaled from the first frame alone.
            ///
            /// For a figure sitting on something that does not move while he does.
            /// The newspaper man's crops change with his arms and the flying pages,
            /// so neither his centre of mass nor his lowest pixel holds still - in
            /// the last six frames his shoes hang 13px below the bench legs, and
            /// the startle frames are 40px taller for the pages above his head.
            /// Pinned on those, the whole bench would jump and he would shrink
            /// every time the paper went up. The bench is the one thing that is
            /// in the same place in every drawing.
            ///
            /// Scaled per frame by the bench, too. Frames generated one at a time
            /// are not drawn at one scale - the old woman's bench is 200 to 214px
            /// across, the newspaper man's 215 to 228 - and at a single
            /// pixels-per-unit the whole picture would swell and shrink by up to 7%
            /// as it played. Each frame's scale is set so its bench comes out the
            /// width the first frame's does.
            /// </summary>
            public bool AnchorBench;

            /// <summary>
            /// Pin cropped frames by the back end of the drawn figure: its
            /// leftmost solid pixel, and its lowest.
            ///
            /// For an animal that holds its hind quarters still and moves
            /// everything in front of them. The crocodile rears up out of the
            /// water to snap, and between the flat frames and the full lunge its
            /// solid box goes from 307x62 to 300x122 - the head rises 65px and
            /// the body foreshortens 15% as the tail curls, so its length is not
            /// even constant. Anchored on the centre of mass the whole animal
            /// lurches 26px sideways in a single frame as the tail straightens;
            /// anchored on the snout, the body slides out from under a head that
            /// is pinned to the one point that fans the widest.
            ///
            /// Overlaying all 24 frames under each rule settles it: pinned by the
            /// tail the rear half stays sharp and only the head and jaws fan out,
            /// which is the animation. The crops are already tight, so this is
            /// very nearly the bottom-left corner of each one - but it is measured
            /// rather than assumed, because a stray soft pixel in the crop would
            /// otherwise shift the animal.
            /// </summary>
            public bool AnchorTail;

            /// <summary>
            /// Pin cropped frames by the head: the mean x of the top of the
            /// figure, and its lowest pixel.
            ///
            /// For something that runs. It is the rule the runner's own clips use
            /// (PlayerAnimationImport, FrameAnchor.Feet) and for the same reason:
            /// the centre of mass swings with the arms and the trailing leg every
            /// frame, and a sprinter's head barely moves. Venom's crops run 390 to
            /// 477 wide as his reach opens and closes, and on the centre of mass
            /// that shows as the whole body surging back and forth under him.
            /// </summary>
            public bool AnchorHead;
        }

        private static readonly SetSpec[] Sets =
        {
            // 1.55 tall and 1.67-1.96 long depending on how far the rider is
            // leaned over. Comfortably under the 3.20 jump.
            new SetSpec { Folder = "moto", Height = 1.55f },

            // Airframe and rotor together.
            new SetSpec { Folder = "helli", Height = 2.85f },

            // Tail fin to skids at their lowest, the same 1.35 the still drawing
            // was imported at, so the frames drop in without the colliders and
            // muzzles moving. 24 frames cropped one by one, 452 wide, 151-162 tall.
            new SetSpec { Folder = "drone", Height = 1.35f, AnchorTop = true },

            // Bench feet to the top of his head while reading. 1.7 is picked
            // against the runner, not against a real sitting man: just over the
            // 1.6 vault ceiling, so he is jumped over rather than vaulted onto -
            // landing on someone's head is not the joke - and under standing
            // height, so he still reads as sitting down.
            new SetSpec { Folder = "newspaperMan", Height = 1.7f, AnchorBench = true },

            // The same height as the newspaper man, and for the same reason: over
            // the vault ceiling, so she is jumped and never vaulted onto.
            new SetSpec { Folder = "oldWoman", Height = 1.7f, AnchorBench = true },

            // The crocodile, measured at full lunge - the tallest frame, which is
            // how ScaleFor scales a folder of individual crops. That puts the
            // reared head 1.8 above the water and leaves the animal lying about
            // 0.85 tall and 4.3 long, so a pit holds two or three of them and
            // each one is worth looking at.
            new SetSpec { Folder = "crocodile", Height = 1.8f, AnchorTail = true },

            // Flame only: the smoke above it and the sparks around it are drawn
            // well under the solid-alpha cut, so they ride along in the picture
            // without being measured as part of it. 2.8 in a pit 3.5 deep leaves
            // the tips below the lip and lets the smoke drift over it.
            new SetSpec { Folder = "fire", Height = 2.8f },

            // Measured at full rear, which is the tallest frame and so what
            // ScaleFor picks. That leaves the snake lying 0.28 deep and 2.1 long
            // when it is idling and standing 1.6 when it comes up - well inside a
            // pit 3.5 deep, and tall enough to be read from the roof above it.
            //
            // No anchor flag: its crops are already bottom-tight in all 24 frames,
            // and across, the centre of mass is the steadiest thing it has. Pinned
            // by the tail the head swings 61px as the body gathers into the coil;
            // pinned by the head the tail does. The mass barely moves either way,
            // which is what a snake drawing itself in actually does.
            new SetSpec { Folder = "snake", Height = 1.6f },

            // The wave that chases the runner through level 14. 5.0 at the top of
            // its swell, nearly three times a standing runner, and about 11.5
            // long. The first cut was 3.2, and at that size it read as a splash
            // rather than something that could swallow him. This is as big as it
            // gets before it stops being a wave coming at him and starts being a
            // wall the size of the screen again.
            //
            // Pinned by its tail, like the crocodile, and for the same reason.
            // Across the loop the crest swells and flattens and leans forward as
            // it goes, and the crops follow it: 435 wide at the tallest, 448 at
            // the flattest. Overlaying all 24 by each rule, the tail keeps the
            // base still and lets the crest move, which is the animation; by the
            // right edge the base slides under a crest that is pinned to spray.
            new SetSpec { Folder = "wave", Height = 5.0f, AnchorTail = true },

            // Venom, who chases the runner through level 14. 2.45 against the
            // runner's 1.75: half a head taller than him while hunched over into
            // a run, so he reads as bigger without filling the screen.
            new SetSpec { Folder = "venom_running", Height = 2.45f, AnchorHead = true },

            // What he does when he catches the runner. 3.0 rather than the run's
            // 2.45 because he is upright in most of it, and because the two sets
            // are not drawn at the same size: his skull measures 191px across the
            // run and 139 here, so this folder is drawn at 0.73 of the other one.
            // Imported at its own face value he would shrink by a quarter at the
            // moment he arrives. 374px of drawing at that scale is 2.98.
            //
            // No anchor flag. Overlaying the 24 frames, the centre of mass holds
            // the body steadier than either edge does - he crouches, lunges and
            // straightens up on the spot, so nothing about him travels the way a
            // runner's head or a crocodile's tail does.
            new SetSpec { Folder = "venomGrab", Height = 3.0f }
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
                    Debug.LogWarning($"[ArvinRunner] No folder called '{spec.Folder}' under " +
                                     $"{string.Join(" or ", SearchRoots)}, so it falls back to a " +
                                     "plain box. The name here is the folder's, not the frames' - " +
                                     "venom_running, not venomRunning.");
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
            if (spec.AnchorBench)
            {
                ApplyBench(spec, paths, ref done, total);
                return;
            }

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

            // For top-anchored frames: the shortest crop, whose bottom is where every
            // frame's pivot sits - that same distance below its own top edge.
            int shortest = int.MaxValue;
            if (spec.AnchorTop && !aligned)
            {
                foreach (string path in paths)
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture != null) shortest = Mathf.Min(shortest, texture.height);
                }
            }

            foreach (string path in paths)
            {
                EditorUtility.DisplayProgressBar("ArvinRunner", "Importing " + Path.GetFileName(path),
                                                 0.5f + 0.5f * done++ / total);

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) continue;

                Vector2 pivot;

                if (shortest != int.MaxValue)
                {
                    // Crops the same width are the same span across, so the centre
                    // holds still; down, a fixed depth from the top edge.
                    pivot = new Vector2(0.5f, (texture.height - shortest) / (float)texture.height);
                }
                else if (aligned)
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

                    if (spec.AnchorTail)
                    {
                        pivot = new Vector2(figure.Bounds.x / (float)texture.width,
                                            figure.Bounds.y / (float)texture.height);
                    }
                    else if (spec.AnchorHead)
                    {
                        pivot = new Vector2(figure.HeadX / texture.width,
                                            figure.Bounds.y / (float)texture.height);
                    }
                    else
                    {
                        pivot = SpriteMeasure.PivotFor(figure, texture.width, texture.height);
                    }
                }

                SpriteMeasure.ApplyFrame(path, pivot, pixelsPerUnit);
            }
        }

        /// <summary>Scales and anchors a folder of frames drawn on a bench - see SetSpec.AnchorBench.</summary>
        private static void ApplyBench(SetSpec spec, string[] paths, ref int done, int total)
        {
            // The first frame sets the scale: bench feet to the top of the crop,
            // which in a reading frame is the top of his head.
            var first = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[0]);
            if (first == null || !TryFindBench(first, out _, out int firstLegs, out int firstSeat))
            {
                Debug.LogWarning($"[ArvinRunner] Could not find the bench in the first frame of " +
                                 $"{spec.Folder}, so it keeps its previous import.");
                return;
            }

            float firstScale = (first.height - firstLegs) / Mathf.Max(0.05f, spec.Height);

            // The bench's world width, fixed by the first frame. Every frame is
            // scaled to match it, so the drawings' own scale drift never shows.
            float benchWidth = firstSeat / firstScale;

            foreach (string path in paths)
            {
                EditorUtility.DisplayProgressBar("ArvinRunner", "Importing " + Path.GetFileName(path),
                                                 0.5f + 0.5f * done++ / total);

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) continue;

                if (!TryFindBench(texture, out Vector2 pivot, out _, out int seat))
                {
                    Debug.LogWarning($"[ArvinRunner] Could not find the bench in {path}; it keeps its previous import.");
                    continue;
                }

                SpriteMeasure.ApplyFrame(path, pivot, seat / Mathf.Max(0.01f, benchWidth));
            }
        }

        /// <summary>
        /// Finds the bench in a frame. Across, the seat: the solid span of the band
        /// 28-36% up the crop, where the seat slats are and nothing of him reaches
        /// past them. Down, the lowest solid row in the outer eighth of that span
        /// at either end - the bench legs, clear of his feet in the middle.
        /// <paramref name="legsY"/> is that row, counted up from the bottom, and
        /// <paramref name="seatWidth"/> the seat's span in pixels.
        /// </summary>
        private static bool TryFindBench(Texture2D texture, out Vector2 pivot, out int legsY, out int seatWidth)
        {
            pivot = default;
            legsY = -1;
            seatWidth = 0;

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (UnityException)
            {
                return false;
            }

            int width = texture.width;
            int height = texture.height;

            int seatFrom = Mathf.RoundToInt(height * 0.28f);
            int seatTo = Mathf.Max(seatFrom + 1, Mathf.RoundToInt(height * 0.36f));
            int left = width, right = -1;

            for (int y = seatFrom; y < seatTo; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x].a < SolidAlpha) continue;
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
            }

            if (right < left) return false;

            int edge = Mathf.Max(1, Mathf.RoundToInt((right - left) * 0.12f));

            for (int y = 0; y < height && legsY < 0; y++)
            {
                int row = y * width;
                for (int x = left; x <= right; x++)
                {
                    if (x > left + edge && x < right - edge) continue;
                    if (pixels[row + x].a < SolidAlpha) continue;

                    legsY = y;
                    break;
                }
            }

            if (legsY < 0) return false;

            seatWidth = right - left + 1;
            pivot = new Vector2((left + right + 1) * 0.5f / width, legsY / (float)height);
            return true;
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
            foreach (string root in SearchRoots)
            {
                if (!Directory.Exists(root)) continue;

                foreach (string candidate in Directory.GetDirectories(root))
                {
                    if (string.Equals(Path.GetFileName(candidate), folder,
                                      System.StringComparison.OrdinalIgnoreCase))
                        return candidate.Replace('\\', '/');
                }
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
