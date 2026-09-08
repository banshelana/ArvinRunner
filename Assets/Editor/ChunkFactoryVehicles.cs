using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Chunks built from the vehicles and site props in Art/OstaclesNew.
    ///
    /// The heights live in <see cref="ArtImport"/>, and they are what decide how
    /// each of these plays. Two consequences worth holding on to while reading
    /// the layouts below:
    ///
    ///  * A vault does not carry the runner past an obstacle, it lands them on
    ///    top of it (see PlayerController.BeginVault). So a car under the 1.60
    ///    vault ceiling is not a hurdle - it is a piece of raised ground, and a
    ///    limo is nearly six units of it. Several chunks here use a car roof as
    ///    the launch pad for the thing after it.
    ///
    ///  * Anything over the ceiling has to be jumped onto, and a jump from
    ///    standing tops out at 3.20. The bus roof sits at 2.90 and the digger's
    ///    cab at 3.40, so both get a prop placed in front as a step. Take the
    ///    step away and the bus becomes a wall you die against.
    ///
    ///  * A step has to be wide. BeginVault puts the runner down 2.3 units past
    ///    where their centre was, while the obstacle face is only 0.4 to 1.5
    ///    ahead of that centre when the probe fires - so the landing falls
    ///    somewhere between 0.8 and 1.9 units past the front face, depending on
    ///    where in the probe window the vault triggered. On anything narrower
    ///    than about two units that spread reaches past the back edge and the
    ///    runner drops to the ground instead of standing on it. Harmless for a
    ///    pallet you are only hopping over; fatal for a pallet you were relying
    ///    on to reach a bus roof. Every step below is therefore a car.
    ///
    /// Widths in the comments are what the art works out to at those heights;
    /// nothing reads them at build time, the layouts measure the real sprite.
    /// </summary>
    public static partial class ChunkFactory
    {
        // Solid but climbable goes on Vaultable, never Ground or Wall, so the
        // wall probe stays off it - the same rule the rooftop props follow.
        private static readonly Dictionary<string, PropSpec> VehicleProps =
            new Dictionary<string, PropSpec>
        {
            // -- low props, and the steps that make the tall ones reachable --

            ["pallet"] = new PropSpec
            {
                Sprite = "pallet", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.02f, 0f, 0.96f, 1f) },
                FallbackSize = new Vector2(1.07f, 1.15f),
                FallbackTint = new Color(0.68f, 0.50f, 0.30f)
            },

            ["motorbike"] = new PropSpec
            {
                Sprite = "motorbike", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.05f, 0f, 0.90f, 1f) },
                FallbackSize = new Vector2(2.26f, 1.30f),
                FallbackTint = new Color(0.30f, 0.32f, 0.36f)
            },

            ["tires"] = new PropSpec
            {
                Sprite = "tires", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.06f, 0f, 0.88f, 1f) },
                FallbackSize = new Vector2(0.94f, 1.45f),
                FallbackTint = new Color(0.18f, 0.18f, 0.20f)
            },

            // -- cars: one box each, deliberately -----------------------------
            //
            // A car's real silhouette is a low bonnet with a cabin behind it,
            // which would be two boxes. It is one here on purpose: the vault
            // probe takes the first thing it hits at knee height and then checks
            // there is standing room on top of it. Split a car in two and the
            // probe finds the bonnet, sees the cabin filling the landing space,
            // and refuses - leaving a waist-high car that cannot be vaulted and
            // kills on contact. One box at roof height means the vault always
            // reads, at the cost of the runner crossing the bonnet at roof
            // level. In a car that is on screen for half a second, that trade is
            // the right way round.

            ["limo"] = new PropSpec
            {
                Sprite = "limo", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.02f, 0f, 0.96f, 1f) },
                FallbackSize = new Vector2(5.92f, 1.40f),
                FallbackTint = new Color(0.12f, 0.12f, 0.14f)
            },

            ["sedan"] = new PropSpec
            {
                Sprite = "sedan", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.02f, 0f, 0.96f, 1f) },
                FallbackSize = new Vector2(4.70f, 1.45f),
                FallbackTint = new Color(0.55f, 0.18f, 0.16f)
            },

            ["taxi"] = new PropSpec
            {
                Sprite = "taxi", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.02f, 0f, 0.96f, 1f) },
                FallbackSize = new Vector2(4.64f, 1.45f),
                FallbackTint = new Color(0.92f, 0.72f, 0.16f)
            },

            ["coupe"] = new PropSpec
            {
                Sprite = "coupe", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.02f, 0f, 0.96f, 1f) },
                FallbackSize = new Vector2(3.28f, 1.45f),
                FallbackTint = new Color(0.20f, 0.34f, 0.52f)
            },

            ["hatchback"] = new PropSpec
            {
                Sprite = "hatchback", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.02f, 0f, 0.96f, 1f) },
                FallbackSize = new Vector2(3.33f, 1.50f),
                FallbackTint = new Color(0.40f, 0.46f, 0.42f)
            },

            ["pickup"] = new PropSpec
            {
                Sprite = "pickup", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.02f, 0f, 0.96f, 1f) },
                FallbackSize = new Vector2(4.53f, 1.55f),
                FallbackTint = new Color(0.48f, 0.44f, 0.36f)
            },

            // -- over the vault ceiling ---------------------------------------

            ["fence"] = new PropSpec
            {
                Sprite = "fence", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.02f, 0f, 0.96f, 1f) },
                FallbackSize = new Vector2(2.96f, 2.10f),
                FallbackTint = new Color(0.52f, 0.55f, 0.58f)
            },

            ["van"] = new PropSpec
            {
                Sprite = "van", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.02f, 0f, 0.96f, 1f) },
                FallbackSize = new Vector2(5.55f, 2.50f),
                FallbackTint = new Color(0.88f, 0.88f, 0.90f)
            },

            ["firetruck"] = new PropSpec
            {
                Sprite = "firetruck", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.02f, 0f, 0.96f, 1f) },
                FallbackSize = new Vector2(8.69f, 2.70f),
                FallbackTint = new Color(0.62f, 0.14f, 0.12f)
            },

            ["bus"] = new PropSpec
            {
                Sprite = "bus", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.02f, 0f, 0.96f, 1f) },
                FallbackSize = new Vector2(10.64f, 2.90f),
                FallbackTint = new Color(0.85f, 0.62f, 0.20f)
            },

            // -- set pieces ---------------------------------------------------

            // Mirrored, because it matters which end you meet. Drawn, the digger
            // puts its cab a metre inside the left edge, so a runner arriving
            // from the left lands on the body and is immediately into the cab
            // with no room to jump again. Flipped, they duck under the arm - it
            // deliberately has no collider - land on the body, and take the cab
            // as a second step near the far end.
            ["excavator"] = new PropSpec
            {
                Sprite = "excavator", Layer = GameLayers.Vaultable, Flip = true,
                Boxes = new[]
                {
                    new PropBox(0.02f, 0.00f, 0.60f, 0.56f),   // tracks and body, top at 1.90
                    new PropBox(0.14f, 0.56f, 0.30f, 0.44f)    // cab, top at 3.40
                },
                FallbackSize = new Vector2(7.45f, 3.40f),
                FallbackTint = new Color(0.90f, 0.68f, 0.12f)
            },

            // On Wall, not Vaultable, because this one is meant to start a wall
            // run. The box stops at the lower deck (0.67 of 4.40, so a top at
            // 2.95); everything drawn above that is the upper frame, and the
            // ledge the runner grabs is the deck itself.
            ["scaffold"] = new PropSpec
            {
                Sprite = "scaffold", Layer = GameLayers.Wall,
                Boxes = new[] { new PropBox(0.02f, 0f, 0.96f, 0.67f) },
                FallbackSize = new Vector2(5.98f, 4.40f),
                FallbackTint = new Color(0.58f, 0.56f, 0.50f)
            }
        };

        // ================================================================= //
        // Chunks
        // ================================================================= //

        private static List<LevelChunk> CreateVehicleChunks()
        {
            return new List<LevelChunk>
            {
                Save(Traffic()),
                Save(Wreckers()),
                Save(FenceLine()),
                Save(LimoGap()),
                Save(BusStop()),
                Save(DeliveryYard()),
                Save(FireLane()),
                Save(ScaffoldTower()),
                Save(DigSite())
            };
        }

        /// <summary>
        /// Three cars, spaced so each vault has landed and the runner is back on
        /// the roof before the next one arrives. A vault takes 0.35s, which is
        /// about three units of ground at running speed - the gaps here are four
        /// and six.
        /// </summary>
        private static GameObject Traffic()
        {
            GameObject root = NewChunk("Chunk_Traffic", 34f, 0f, 0f, 2,
                                       ChunkSkill.Vault | ChunkSkill.Jump, "traffic");
            Ground(root, 0f, 0f, 34f);

            Prop(root, "sedan", 7f, 0f);        // 4.70 wide, roof at 1.45
            Prop(root, "taxi", 16f, 0f);        // 4.64 wide, roof at 1.45
            Prop(root, "hatchback", 26f, 0f);   // 3.33 wide, roof at 1.50

            Coins(root, 8f, 2.4f, 4, 1.1f, 0.5f);
            Coins(root, 17f, 2.4f, 4, 1.1f, 0.5f);
            return root;
        }

        /// <summary>A yard of scrap. Everything here is under the vault ceiling,
        /// so it plays as one long rhythm rather than a set of decisions.</summary>
        private static GameObject Wreckers()
        {
            GameObject root = NewChunk("Chunk_Wreckers", 30f, 0f, 0f, 2, ChunkSkill.Vault, "wreck");
            Ground(root, 0f, 0f, 30f);

            Prop(root, "coupe", 7f, 0f);        // 3.28 wide
            Prop(root, "tires", 15f, 0f);       // 0.94 wide, 1.45 tall
            Prop(root, "pallet", 19f, 0f);      // 1.07 wide
            Prop(root, "motorbike", 24f, 0f);   // 2.26 wide

            Coins(root, 15.5f, 2.6f, 5, 1.1f, 0.6f);
            return root;
        }

        /// <summary>
        /// The only new prop meant to be cleared outright rather than landed on.
        /// Both fences sit in open ground so the runner has the room to commit
        /// to the jump early, which is what clearing 2.10 actually needs.
        /// </summary>
        private static GameObject FenceLine()
        {
            GameObject root = NewChunk("Chunk_FenceLine", 30f, 0f, 0f, 2, ChunkSkill.Jump, "fence");
            Ground(root, 0f, 0f, 30f);

            Prop(root, "fence", 9f, 0f);
            Prop(root, "fence", 20f, 0f);

            // The arcs sit at the height the jump has to reach, so they read as
            // the line through rather than as a reward hung over the obstacle.
            Coins(root, 9f, 2.6f, 4, 1.1f, 0.7f);
            Coins(root, 20f, 2.6f, 4, 1.1f, 0.7f);
            return root;
        }

        /// <summary>
        /// A limo parked at the lip of a gap. Vault onto it and the roof becomes
        /// the run-up: you leave the ground 1.40 higher and a car's length
        /// further on than you would have from the roof edge.
        /// </summary>
        private static GameObject LimoGap()
        {
            GameObject root = NewChunk("Chunk_LimoGap", 32f, 0f, 0f, 3,
                                       ChunkSkill.Vault | ChunkSkill.Gap, "limo");
            Ground(root, 0f, 0f, 12f);
            Prop(root, "limo", 6f, 0f);         // 5.92 wide, roof at 1.40

            Ground(root, 18f, 0f, 14f);
            Coins(root, 13f, 3.4f, 5, 1.2f, 1.2f);
            return root;
        }

        /// <summary>
        /// A bus roof at 2.90 against a 3.20 jump - reachable from the ground in
        /// theory, and horrible in practice. The hatchback in front is the whole
        /// chunk: vault onto it, jump again from 1.50, and the roof becomes a
        /// comfortable step rather than a coin flip.
        /// </summary>
        private static GameObject BusStop()
        {
            GameObject root = NewChunk("Chunk_BusStop", 34f, 0f, 0f, 3,
                                       ChunkSkill.Vault | ChunkSkill.Jump, "bus");
            Ground(root, 0f, 0f, 34f);

            Prop(root, "hatchback", 6f, 0f);    // the step: 3.32 wide, roof at 1.50
            Prop(root, "bus", 11f, 0f);         // 10.64 wide, roof at 2.90

            // Along the roof, so the reward is for staying up there rather than
            // dropping off the front of the bus.
            Coins(root, 13f, 3.9f, 6, 1.3f, 0.4f);
            return root;
        }

        /// <summary>
        /// A box van, with the coupe in front of it as the step up. The tyres
        /// and pallet on the far side are only ever hopped over, so their being
        /// too narrow to land on does not matter there.
        /// </summary>
        private static GameObject DeliveryYard()
        {
            GameObject root = NewChunk("Chunk_DeliveryYard", 32f, 0f, 0f, 3,
                                       ChunkSkill.Vault | ChunkSkill.Jump, "van");
            Ground(root, 0f, 0f, 32f);

            Prop(root, "coupe", 6f, 0f);        // the step: 3.28 wide, roof at 1.45
            Prop(root, "van", 11f, 0f);         // 5.54 wide, roof at 2.50
            Prop(root, "tires", 21f, 0f);
            Prop(root, "pallet", 26f, 0f);

            Coins(root, 12f, 3.5f, 4, 1.2f, 0.4f);
            return root;
        }

        /// <summary>
        /// A cone warns, the pickup is the step, the appliance is the climb -
        /// nearly nine units of roof once you are up there.
        ///
        /// One cone, not the pair the other street chunks use. A hop off a cone
        /// puts the runner down anywhere in a unit-wide spread, and with a
        /// second cone 1.5 along that spread reaches it: the vault is then
        /// refused for want of landing room and the warning prop becomes the
        /// thing that trips you. Everything here is at least two units clear of
        /// whatever lands on it.
        /// </summary>
        private static GameObject FireLane()
        {
            GameObject root = NewChunk("Chunk_FireLane", 34f, 0f, 0f, 3,
                                       ChunkSkill.Vault | ChunkSkill.Jump, "firetruck");
            Ground(root, 0f, 0f, 34f);

            Prop(root, "cone_large", 5f, 0f);
            Prop(root, "pickup", 9f, 0f);       // the step: 4.53 wide, bed at 1.55
            Prop(root, "firetruck", 15f, 0f);   // 8.70 wide, roof at 2.70

            Coins(root, 17f, 3.7f, 6, 1.3f, 0.4f);
            return root;
        }

        /// <summary>
        /// The same shape as Chunk_WallClimb, but the tower is a scaffold and it
        /// stands in open ground rather than dividing two roof levels: run at the
        /// face, up it, grab the deck at 2.95, climb out and cross the top.
        /// </summary>
        private static GameObject ScaffoldTower()
        {
            GameObject root = NewChunk("Chunk_ScaffoldTower", 30f, 0f, 0f, 4,
                                       ChunkSkill.WallRun | ChunkSkill.LedgeGrab, "wall");
            Ground(root, 0f, 0f, 30f);

            Prop(root, "scaffold", 11f, 0f);    // 5.98 wide, deck at 2.95

            Coins(root, 12f, 3.4f, 4, 1.2f, 0.5f);
            return root;
        }

        /// <summary>
        /// The digger, mirrored so the arm comes first. Duck under it, jump the
        /// body at 1.90, then the cab at 3.40 - which only clears because the
        /// jump starts from the body rather than the ground.
        /// </summary>
        private static GameObject DigSite()
        {
            GameObject root = NewChunk("Chunk_DigSite", 34f, 0f, 0f, 4,
                                       ChunkSkill.Jump | ChunkSkill.DoubleJump, "dig");
            Ground(root, 0f, 0f, 34f);

            Prop(root, "pallet", 7f, 0f);
            Prop(root, "excavator", 10f, 0f);   // 7.45 wide, body 1.90, cab 3.40
            Prop(root, "tires", 22f, 0f);
            Prop(root, "barrier", 27f, 0f);

            Coins(root, 12f, 2.6f, 4, 1.2f, 0.6f);
            return root;
        }
    }
}
