using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A single level: which chunks it is made of and where it ends.
    ///
    /// Two authoring modes:
    ///   Sequenced - you list the chunks in order. Full control, best for the
    ///               early levels that teach each move.
    ///   Assembled - you give a pool and a target length, and the builder picks
    ///               a varied mix inside a difficulty band. Fast to make, and
    ///               the fixed seed keeps it identical on every replay.
    ///
    /// Create via Assets > Create > ArvinRunner > Level.
    /// </summary>
    [CreateAssetMenu(menuName = "ArvinRunner/Level", fileName = "Level_01")]
    public class LevelDefinition : ScriptableObject
    {
        public enum BuildMode { Sequenced, Assembled }

        [Header("Identity")]
        public int levelNumber = 1;
        public string displayName = "Rooftops";
        [TextArea] public string hint = "Swipe up to jump, swipe down to slide.";

        [Header("Build")]
        public BuildMode mode = BuildMode.Sequenced;

        [Tooltip("Sequenced mode: the exact chunks, in order.")]
        public LevelChunk[] sequence;

        [Tooltip("Assembled mode: the pool to draw from.")]
        public LevelChunk[] pool;

        [Tooltip("Assembled mode: how long the level should be, in world units.")]
        public float targetLength = 400f;

        [Tooltip("Assembled mode: difficulty across the level (x = progress 0-1, y = difficulty 1-5).")]
        public AnimationCurve difficultyCurve = AnimationCurve.Linear(0f, 1f, 1f, 4f);

        [Tooltip("Assembled mode: same seed = same level every time.")]
        public int seed = 12345;

        [Header("Padding")]
        [Tooltip("Flat run-up before the first obstacle.")]
        public float startPadLength = 18f;
        [Tooltip("Flat run-out after the last chunk, ending at the finish line.")]
        public float finishPadLength = 16f;

        [Header("Presentation")]
        public ParallaxTheme theme;
        public AudioClip music;

        [Header("Scoring")]
        [Tooltip("Time under which the level counts as a fast clear, in seconds.")]
        public float parTime = 45f;

        [Header("Chase")]
        [Tooltip("Venom runs the level down behind the runner, and kills him if he " +
                 "catches him. See VenomChase.")]
        public bool chase;

        [Tooltip("His speed, as a share of the runner's nominal pace. Under 1, so a " +
                 "clean run always pulls away and only slow moves let him gain. Tune it " +
                 "against the slowest clean line through the level, never an average one.")]
        [Range(0.5f, 1f)] public float chasePace = 0.83f;

        [Tooltip("The furthest behind the runner he is ever allowed to fall. Caps the " +
                 "lead a fast start can bank, so every slow move is paid out of the same " +
                 "few units. 14 sits just off the left of the screen.")]
        public float chaseLeash = 14f;

        [Tooltip("How far behind the spawn he starts. Small enough to be on screen, so " +
                 "the player sees what is behind him before he has to outrun it.")]
        public float chaseStartLag = 6f;

        // ---------------------------------------------------------------- //

        /// <summary>
        /// Resolves the chunk order for this level. Deterministic: the same
        /// definition always produces the same list.
        /// </summary>
        public List<LevelChunk> BuildChunkOrder()
        {
            var result = new List<LevelChunk>();

            if (mode == BuildMode.Sequenced)
            {
                if (sequence != null)
                    foreach (LevelChunk chunk in sequence)
                        if (chunk != null) result.Add(chunk);

                return result;
            }

            if (pool == null || pool.Length == 0)
            {
                Debug.LogWarning($"[LevelDefinition] '{name}' is Assembled but its pool is empty.", this);
                return result;
            }

            var random = new System.Random(seed);
            float placed = 0f;
            string lastTag = null;
            LevelChunk lastChunk = null;

            // Guard against a pool whose chunks are all zero-length.
            int safety = 0;

            while (placed < targetLength && safety++ < 500)
            {
                float progress = Mathf.Clamp01(placed / Mathf.Max(1f, targetLength));
                int wanted = Mathf.RoundToInt(difficultyCurve.Evaluate(progress));

                LevelChunk pick = PickChunk(random, wanted, lastTag, lastChunk);
                if (pick == null) break;

                result.Add(pick);
                placed += pick.length + pick.trailingGap;
                lastTag = string.IsNullOrEmpty(pick.variantTag) ? null : pick.variantTag;
                lastChunk = pick;
            }

            return result;
        }

        private LevelChunk PickChunk(System.Random random, int wantedDifficulty, string lastTag, LevelChunk lastChunk)
        {
            var candidates = new List<LevelChunk>();

            // Widen the difficulty window until something fits.
            for (int window = 0; window <= 4 && candidates.Count == 0; window++)
            {
                foreach (LevelChunk chunk in pool)
                {
                    if (chunk == null || chunk.length <= 0f) continue;
                    if (Mathf.Abs(chunk.difficulty - wantedDifficulty) > window) continue;
                    if (lastTag != null && chunk.variantTag == lastTag) continue;
                    if (chunk == lastChunk && pool.Length > 1) continue;
                    candidates.Add(chunk);
                }
            }

            if (candidates.Count == 0) return null;
            return candidates[random.Next(candidates.Count)];
        }
    }
}
