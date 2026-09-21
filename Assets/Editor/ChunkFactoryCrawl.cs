using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Things too low to slide under.
    ///
    /// <b>The third way to go low.</b> The game already had two, both off one
    /// swipe down: the tackle for something short, and the skateboard for a deck
    /// long enough that a tackle would stand the runner up half way along it.
    /// Both leave the runner 0.79 tall and both are <i>faster</i> than running -
    /// they are ways of keeping pace through an obstacle. A crawl is the
    /// opposite. It gets the runner down to 0.63, under things neither of the
    /// others fits beneath, and it costs them a fifth of their speed to do it.
    ///
    /// The runner is never asked to pick. PlayerController reads what is ahead
    /// when the swipe lands and takes the lowest answer that works - see the
    /// Sliding case of EnterState - so the same input means "get under that",
    /// and the game decides what "under" has to look like.
    ///
    /// <b>The bar height is the whole design.</b> Anything from 0.72 to 0.78 is
    /// a crawl: over the crawling runner (0.63) and under the sliding one
    /// (0.79). Below 0.72 nothing gets through and it is a wall; at 0.79 and
    /// above it is an ordinary slide or a skate, and should be built as one.
    /// <see cref="CrawlBar"/> is the only height used here, so a bar is never
    /// accidentally authored into the half-centimetre where it reads as
    /// crawlable and is not.
    ///
    /// <b>Lasers are bars too.</b> PlayerSensors.FindLowBar counts hazards as
    /// well as solids, which is the one probe in the game that does - so a beam
    /// strung at knee height asks for a crawl in exactly the way a girder does,
    /// and punishes a slide instead of merely blocking one.
    ///
    /// <b>A beam on its own is not an obstacle.</b> It sits at 0.74 and a jump
    /// goes 3.20, so the first cut of these chunks could be cleared by hopping
    /// over every laser in them and the crawl was decoration. Every beam now
    /// hangs under a deck at <see cref="BeamDeck"/>, and that is what makes the
    /// obstacle: the deck is too low to run or jump under, so the runner must go
    /// down, and the beam is too low to slide under, so down is not enough. The
    /// deck forbids the jump and the beam forbids the slide, and only flat is
    /// left. Neither alone asks for anything.
    ///
    /// <b>Nothing stands on the floor in the lane.</b> Everything here is
    /// something the runner passes through at body height, so a support drawn
    /// from the deck down to the ground reads as a wall - and since it is only
    /// scenery he then walks through it, which looks broken twice over. Decks
    /// are carried from their own ends and emitters hang off them; the floor
    /// under a crawl is left clear, and the only thing drawn on it is a shadow.
    ///
    /// <b>All of it is Vaultable</b>, for the reason the ruins are: the wall-run
    /// and ledge probes look at Ground and Wall only. On Ground, a deck this low
    /// is a face to jump at, run up and climb onto, and the whole obstacle can be
    /// gone over the top of. Both decks are also built taller than the 1.6 vault
    /// ceiling, so they cannot be vaulted onto either.
    /// </summary>
    public static partial class ChunkFactory
    {
        // Cold service-tunnel greys, darker than the rooftop, so a gallery reads
        // as something built rather than as more roof.
        private static readonly Color DuctTone = new Color(0.30f, 0.32f, 0.35f);
        private static readonly Color DuctShade = new Color(0.21f, 0.22f, 0.25f);
        private static readonly Color EmitterTone = new Color(0.28f, 0.29f, 0.33f);

        // Laid flat on the floor under a deck. Dark, and no height at all, so it
        // never reads as something standing in the way.
        private static readonly Color DeckShadow = new Color(0.10f, 0.11f, 0.13f, 0.55f);

        // The beam and the glow under it. Red because every other hazard in the
        // game is, and a colour that means "do not touch" should not be learned
        // twice.
        private static readonly Color BeamCore = new Color(1f, 0.25f, 0.28f, 0.85f);
        private static readonly Color BeamHaze = new Color(1f, 0.30f, 0.32f, 0.22f);

        /// <summary>
        /// The underside of anything the runner has to crawl under.
        ///
        /// Over a crawling runner (0.63 collider, 0.65 of drawn body) and under a
        /// sliding one (0.79), with about seven centimetres of daylight either
        /// side. The two margins are deliberately uneven: clearing it while
        /// crawling wants to look clear, and failing it while sliding wants to be
        /// unambiguous.
        /// </summary>
        private const float CrawlBar = 0.74f;

        /// <summary>How thick a laser reads as. Thin, so the gap under it is what the eye measures.</summary>
        private const float BeamThickness = 0.16f;

        /// <summary>
        /// The underside of the deck a laser hangs from. Well under a standing
        /// runner (1.75) so it cannot be run or jumped under, and well over a
        /// sliding one (0.79) so the beam, not the deck, is what a slide meets.
        /// </summary>
        private const float BeamDeck = 1.35f;

        /// <summary>
        /// How deep a deck is drawn. Enough that both decks finish above the 1.6
        /// vault ceiling - the gallery tops out at 1.84 and the laser deck at 2.25
        /// - so neither can be vaulted onto.
        /// </summary>
        private const float DeckThickness = 1.1f;

        private static readonly HashSet<string> CrawlTags = new HashSet<string> { "crawl" };

        /// <summary>
        /// True for the chunks made here. Kept out of the assembled levels' pool,
        /// like the war zone and the crossings, so adding them never reshuffles a
        /// level somebody has already learned.
        /// </summary>
        public static bool IsCrawl(LevelChunk chunk) =>
            chunk != null && CrawlTags.Contains(chunk.variantTag);

        private static List<LevelChunk> CreateCrawlChunks()
        {
            return new List<LevelChunk>
            {
                Save(DuctCrawl()),
                Save(LaserCrawl()),
                Save(CrawlGauntlet()),
                Save(CrawlStrike())
            };
        }

        // ================================================================= //
        // The chunks
        // ================================================================= //

        /// <summary>
        /// A service gallery across the roof, seven units of it. The crawl taught
        /// with something solid, because a solid thing that is plainly too low
        /// reads as "get down" from a screen away - and getting it wrong stops the
        /// runner dead rather than killing them.
        ///
        /// Seven units at the crawl's 0.45 pace is about one and three quarter
        /// seconds flat on the floor: long enough to see the animation, short
        /// enough that the level does not stop for it.
        /// </summary>
        private static GameObject DuctCrawl()
        {
            GameObject root = NewChunk("Chunk_DuctCrawl", 34f, 0f, 0f, 3,
                                       ChunkSkill.Slide | ChunkSkill.Timing, "crawl");
            Ground(root, 0f, 0f, 34f);

            Gallery(root, 14f, 0f, 7f);

            // Along the floor under it, which is the only line through.
            Coins(root, 13.5f, 0.35f, 7, 1.2f, 0f);
            return root;
        }

        /// <summary>
        /// Two laser beams at knee height, strung between posts.
        ///
        /// The same move as the gallery and a harder read - a beam is thin, and
        /// nothing about it says it cannot be jumped until the jump has been
        /// taken. So it comes second, after the gallery has taught the height,
        /// and the posts are built to the same silhouette as the gallery piers:
        /// whatever stands that high on this roof is something to get under.
        ///
        /// Two of them, four apart, is one crawl rather than two. The probe joins
        /// bars within three units of each other, so the runner stays flat across
        /// the gap instead of standing up into the second beam.
        /// </summary>
        private static GameObject LaserCrawl()
        {
            GameObject root = NewChunk("Chunk_LaserCrawl", 36f, 0f, 0f, 4,
                                       ChunkSkill.Slide | ChunkSkill.Timing, "crawl");
            Ground(root, 0f, 0f, 36f);

            // Five units each and two apart. FindLowBar joins bars within three
            // of each other, so this is one stretch to stay flat through rather
            // than a crawl, a stand, and a second crawl into the back of a beam.
            Beam(root, 13f, 0f, 5f);
            Beam(root, 20f, 0f, 5f);

            Coins(root, 12.5f, 0.35f, 10, 1.2f, 0f);
            return root;
        }

        /// <summary>
        /// A gallery, a gap of open roof, then a beam: down, up, down.
        ///
        /// The gap is nine units, which at running pace is about a second - room
        /// to stand, take a stride and go back down deliberately, rather than a
        /// join that has to be crawled straight through. Getting up and going
        /// down again is the thing being asked for here; the beam on its own was
        /// the last chunk.
        /// </summary>
        private static GameObject CrawlGauntlet()
        {
            GameObject root = NewChunk("Chunk_CrawlGauntlet", 46f, 0f, 0f, 5,
                                       ChunkSkill.Slide | ChunkSkill.Jump | ChunkSkill.Timing, "crawl");
            Ground(root, 0f, 0f, 46f);

            Gallery(root, 10f, 0f, 6f);

            // In the open between them, so the runner is up on their feet for it.
            Box(root, "Crate", 21f, 0f, 1.6f, 1.1f, GameLayers.Vaultable, VaultTone, anchorBottom: true);

            Beam(root, 28f, 0f, 4.5f);
            Beam(root, 34.5f, 0f, 4.5f);

            Coins(root, 9.5f, 0.35f, 6, 1.2f, 0f);
            Coins(root, 20.5f, 1.9f, 3, 1.0f, 0.5f);
            Coins(root, 27.5f, 0.35f, 9, 1.2f, 0f);
            return root;
        }

        /// <summary>
        /// A beam to crawl under, and a drone waiting past it.
        ///
        /// The bomb is laid eighteen units beyond the far post. A crawl is the
        /// slowest the runner ever moves and the one move they cannot break out
        /// of upward, so a mark that lit while they were still under the beam
        /// would be a death they could only watch. Eighteen units puts them back
        /// on their feet and running before it appears.
        /// </summary>
        private static GameObject CrawlStrike()
        {
            GameObject root = NewChunk("Chunk_CrawlStrike", 52f, 0f, 0f, 5,
                                       ChunkSkill.Slide | ChunkSkill.Jump | ChunkSkill.Timing, "crawl");
            Ground(root, 0f, 0f, 52f);

            Beam(root, 12f, 0f, 6f);

            GameObject strike = Strike(root, 36f, 0f, bomb: true);
            Drone(root, 39f, DroneFlyHeight, strike, bomb: true);

            Coins(root, 11.5f, 0.35f, 8, 1.2f, 0f);
            Coins(root, 34f, 2.8f, 5, 1.2f, 1.1f);
            return root;
        }

        // ================================================================= //
        // The pieces
        // ================================================================= //

        /// <summary>
        /// A service gallery: a solid deck with its underside at
        /// <see cref="CrawlBar"/>, and nothing at all beneath it.
        ///
        /// It used to stand on two stub piers, which was wrong twice. They had no
        /// collider, so the runner crawled straight through them; and they were
        /// drawn floor to underside across the only lane there is, so the mouth of
        /// the gallery read as bricked up and the coins along the floor looked
        /// walled in behind them. A support in a side view is a wall whether it
        /// has a collider or not.
        ///
        /// So the deck is carried from its ends, its weight is shown on its own
        /// face, and the only thing on the floor is its shadow.
        /// </summary>
        private static void Gallery(GameObject parent, float x, float groundY, float width)
        {
            float top = groundY + CrawlBar;

            Box(parent, "Gallery", x, top, width, DeckThickness, GameLayers.Vaultable,
                DuctTone, anchorBottom: true);

            RuinPiece(parent, "GallerySeam", x, top + 0.5f, width, 0.07f, DuctShade, 11);
            RuinPiece(parent, "GalleryLip", x - 0.12f, top + DeckThickness - 0.16f,
                      width + 0.24f, 0.16f, DuctShade, 11);

            // End caps, drawn on the deck's own face and stopping at its
            // underside. They give it the weight the piers used to, without
            // putting anything in the way.
            RuinPiece(parent, "GalleryEnd0", x - 0.14f, top, 0.34f, DeckThickness, DuctShade, 10);
            RuinPiece(parent, "GalleryEnd1", x + width - 0.2f, top, 0.34f, DeckThickness, DuctShade, 10);

            // On the floor, not standing on it: what the deck puts in shade.
            RuinPiece(parent, "GalleryShadow", x, groundY, width, 0.14f, DeckShadow, 6);
        }

        /// <summary>
        /// A laser under a deck: the deck stops the jump, the beam stops the
        /// slide, and flat is all that is left. See the note on the class.
        ///
        /// The emitters hang from the deck rather than standing up from the
        /// floor, so the lane the runner crawls is empty from end to end.
        /// </summary>
        private static void Beam(GameObject parent, float x, float groundY, float width)
        {
            float bottom = groundY + CrawlBar;
            float deck = groundY + BeamDeck;

            // The deck. Same span as the beam, so the stretch the runner has to
            // stay flat for is one stretch and not two overlapping ones.
            Box(parent, "BeamDeck", x - 0.5f, deck, width + 1f, DeckThickness,
                GameLayers.Vaultable, DuctTone, anchorBottom: true);

            RuinPiece(parent, "BeamDeckLip", x - 0.62f, deck + DeckThickness - 0.16f,
                      width + 1.24f, 0.16f, DuctShade, 11);

            // The emitters, hanging off the underside of the deck down to the
            // beam - the whole run of them above the crawl line.
            RuinPiece(parent, "BeamMount0", x - 0.3f, bottom + 0.3f, 0.3f, deck - bottom - 0.3f,
                      EmitterTone, 7);
            RuinPiece(parent, "BeamMount1", x + width, bottom + 0.3f, 0.3f, deck - bottom - 0.3f,
                      EmitterTone, 7);
            RuinPiece(parent, "BeamHead0", x - 0.38f, bottom + 0.06f, 0.46f, 0.3f, DuctShade, 8);
            RuinPiece(parent, "BeamHead1", x + width - 0.08f, bottom + 0.06f, 0.46f, 0.3f, DuctShade, 8);

            // The haze around it, so the beam has some thickness to read at speed
            // without the thing that kills getting any taller.
            RuinPiece(parent, "BeamHaze", x, bottom - 0.12f, width, BeamThickness + 0.24f, BeamHaze, 11);

            GameObject beam = Box(parent, "Beam", x, bottom, width, BeamThickness,
                                  GameLayers.Hazard, BeamCore, anchorBottom: true, trigger: true);
            beam.GetComponent<SpriteRenderer>().sortingOrder = 12;
            beam.AddComponent<Hazard>();

            RuinPiece(parent, "BeamShadow", x - 0.5f, groundY, width + 1f, 0.14f, DeckShadow, 6);
        }
    }
}
