using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Obstacles with more than one answer.
    ///
    /// Vector's best moments come from a barrier you can take two ways - hurdle
    /// it or drop under it - so the same layout plays differently every run. The
    /// geometry here is tuned around three numbers from PlayerConfig:
    ///
    ///     standing height  1.75
    ///     sliding height   0.79   (slideHeightFraction 0.45)
    ///     jump height      3.20
    ///
    /// Anything whose underside sits between those first two numbers can be slid
    /// under; anything whose top is under the jump height can be cleared. Put a
    /// bar in both windows at once and the player gets to choose.
    /// </summary>
    public static partial class ChunkFactory
    {
        private static readonly Color RailTone = new Color(0.62f, 0.66f, 0.74f);
        private static readonly Color DeckTone = new Color(0.38f, 0.40f, 0.48f);

        private static List<LevelChunk> CreateRouteChunks()
        {
            return new List<LevelChunk>
            {
                Save(RailRun()),
                Save(Overpass()),
                Save(Scaffold())
            };
        }

        /// <summary>
        /// Scaffold rails hung at chest height. Hurdle them or slide under -
        /// both work, and the pickups deliberately sit on opposite sides so
        /// taking all of them means switching between the two.
        /// </summary>
        private static GameObject RailRun()
        {
            GameObject root = NewChunk("Chunk_Rails", 32f, 0f, 0f, 2,
                                       ChunkSkill.Jump | ChunkSkill.Slide, "rails");
            Ground(root, 0f, 0f, 32f);

            float[] positions = { 8f, 15f, 22f };

            for (int i = 0; i < positions.Length; i++)
            {
                // Underside at 0.95 clears a slide (0.79); top at 1.50 is well
                // under a jump. Both routes are open.
                Box(root, "Rail" + i, positions[i], 0.95f, 1.4f, 0.55f,
                    GameLayers.Ground, RailTone, anchorBottom: true);

                // Posts, drawn only - no collider, so they never block a slide.
                Box(root, "Post" + i, positions[i], 0f, 0.16f, 0.95f,
                    GameLayers.Ground, RailTone, anchorBottom: true, collider: false);
                Box(root, "Post" + i + "b", positions[i] + 1.24f, 0f, 0.16f, 0.95f,
                    GameLayers.Ground, RailTone, anchorBottom: true, collider: false);

                // Alternate the reward: over the first rail, under the second.
                if (i % 2 == 0) Coins(root, positions[i] - 0.2f, 2.3f, 3, 0.9f, 0.4f);
                else Coins(root, positions[i] - 0.2f, 0.45f, 3, 0.9f, 0f);
            }

            return root;
        }

        /// <summary>
        /// A low overpass. Slide the tunnel for speed, or take the roof for the
        /// pickups - and the drop off the far end is hard enough to force a roll.
        /// </summary>
        private static GameObject Overpass()
        {
            GameObject root = NewChunk("Chunk_Overpass", 32f, 0f, 0f, 3,
                                       ChunkSkill.Jump | ChunkSkill.Slide, "overpass");
            Ground(root, 0f, 0f, 32f);

            // Deck underside at 1.20 - too low to run through standing, high
            // enough to slide. Its top at 2.00 is an easy jump.
            Box(root, "Deck", 10f, 1.2f, 9f, 0.8f, GameLayers.Ground, DeckTone, anchorBottom: true);

            // A step up, so the roof route is a hop rather than a leap of faith.
            Box(root, "Kerb", 8.2f, 0f, 1.2f, 0.85f, GameLayers.Vaultable, DeckTone);

            Coins(root, 11f, 2.6f, 6, 1.2f, 0.5f);   // the roof line
            Coins(root, 12f, 0.45f, 3, 1.1f, 0f);    // the tunnel line

            return root;
        }

        /// <summary>Rail, overpass, rail - the choice made three times in a row.</summary>
        private static GameObject Scaffold()
        {
            GameObject root = NewChunk("Chunk_Scaffold", 38f, 0f, 0f, 4,
                                       ChunkSkill.Jump | ChunkSkill.Slide | ChunkSkill.Timing, "rails");
            Ground(root, 0f, 0f, 38f);

            Box(root, "Rail0", 7f, 0.95f, 1.4f, 0.55f, GameLayers.Ground, RailTone, anchorBottom: true);
            Box(root, "Deck", 14f, 1.2f, 7f, 0.8f, GameLayers.Ground, DeckTone, anchorBottom: true);
            Box(root, "Rail1", 26f, 0.95f, 1.4f, 0.55f, GameLayers.Ground, RailTone, anchorBottom: true);

            Coins(root, 15f, 2.6f, 5, 1.2f, 0.4f);
            Coins(root, 26.5f, 0.45f, 2, 0.9f, 0f);

            return root;
        }
    }
}
