using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// The container port: belts let into the quay, steam venting out of it, and
    /// stacks of containers to get over.
    ///
    /// <b>The belt is the new idea here, and it is not an obstacle on its own.</b>
    /// A stretch of faster or slower running is only a stretch of running. What
    /// makes it one is the gap at the end: every jump in this game is taken at
    /// the speed the runner happens to be doing, and a belt is the first thing
    /// that changes that speed on purpose. Off a belt running against him the
    /// same take-off falls short, so the jump has to be taken earlier; off one
    /// running with him it throws him further than he means to go, which matters
    /// when there is something to land on rather than a hole to clear.
    ///
    /// Belts are always laid so the runner can see where they end before he has
    /// to commit - the lip of the gap is on screen while he is still on the belt.
    /// A belt that changed the answer to a jump he had already started would be
    /// a trick rather than an obstacle.
    ///
    /// <b>The vents</b> are the laser gate's timing in a different shape: a
    /// column of steam that blows on a cycle keyed to when the runner arrives.
    /// The same warning, the same rhythm, read completely differently because it
    /// is vertical and because it is over your head rather than at your knees.
    /// </summary>
    public static partial class ChunkFactory
    {
        // Wet concrete, a shade cooler than the rooftops.
        private static readonly Color QuayTone = new Color(0.30f, 0.31f, 0.33f);

        // The belt itself: darker than the quay, so it reads as a hole in the
        // floor with something moving in it.
        private static readonly Color BeltTone = new Color(0.14f, 0.15f, 0.17f);
        private static readonly Color BeltRoller = new Color(0.38f, 0.39f, 0.42f);
        private static readonly Color ChevronTone = new Color(0.86f, 0.74f, 0.22f);

        // Steam: near white, and thin, because it is drawn over the runner.
        private static readonly Color SteamTone = new Color(0.92f, 0.94f, 0.96f, 0.72f);
        private static readonly Color VentMetal = new Color(0.33f, 0.34f, 0.36f);

        // Containers, in the two liveries the yard uses.
        private static readonly Color ContainerRust = new Color(0.52f, 0.31f, 0.22f);
        private static readonly Color ContainerTeal = new Color(0.21f, 0.38f, 0.40f);
        private static readonly Color ContainerRib = new Color(0f, 0f, 0f, 0.18f);

        /// <summary>How deep a belt is sunk into the quay. Shallow: it is a floor, not a trench.</summary>
        private const float BeltDepth = 0.12f;

        /// <summary>
        /// How much a belt takes off, or puts on, the runner's pace.
        ///
        /// 3.2 against him drops a 9-11 run to 6-8, which is a fifth to a third
        /// off the distance of the jump at the end of it. That is plainly visible
        /// without being unanswerable: the gaps after the against-belts here are
        /// laid out to be cleared at the slower speed, so a runner who reads it
        /// and jumps from the lip makes it every time.
        /// </summary>
        private const float BeltDrift = 3.2f;

        /// <summary>
        /// How high the steam reaches.
        ///
        /// <b>Set so it can be jumped.</b> It was 4.2, which is over the 3.2 a
        /// jump reaches - so the only answer was to wait for it, and a mistimed
        /// approach had nothing left to try. At 2.2 the arc clears it: the feet
        /// are above 2.2 for 0.384s, which carries 3.45 units at the opening run
        /// of 9 and 4.22 at the top speed of 11, against the 2.1 needed to take a
        /// 1.3 wide column plus the width of the runner. Half as much again as it
        /// needs, at the worst speed.
        ///
        /// Still well over a standing runner at 1.75, so it cannot be run through,
        /// and it stands on the floor, so it cannot be slid under. There are now
        /// two answers to it - jump it, or hold down and let it pass - which is
        /// one more than it had.
        /// </summary>
        private const float VentHeight = 2.2f;

        /// <summary>How wide the column is. Narrow, because its width is what a jump has to cross.</summary>
        private const float VentWidth = 1.3f;

        private static readonly HashSet<string> DockTags = new HashSet<string> { "belt", "vent", "yard" };

        /// <summary>
        /// True for the chunks made here. Kept out of the assembled levels' pool,
        /// like every set since the war zone, so adding the docks never reshuffles
        /// a level somebody has already learned.
        /// </summary>
        public static bool IsDockside(LevelChunk chunk) =>
            chunk != null && DockTags.Contains(chunk.variantTag);

        private static List<LevelChunk> CreateDockChunks()
        {
            return new List<LevelChunk>
            {
                Save(BeltRun()),
                Save(BeltGap()),
                Save(SteamVents()),
                Save(BeltVents()),
                Save(ContainerYard())
            };
        }

        // ================================================================= //
        // The chunks
        // ================================================================= //

        /// <summary>
        /// The belt taught on its own: one running with the runner, one running
        /// against him, and nothing to fall into either way.
        ///
        /// The pair is the lesson. A single belt is just a patch of floor that
        /// felt odd; two that do opposite things, a few strides apart, say what
        /// the chevrons mean. The only thing to be got over is a crate at the far
        /// end, which is there to be met at the wrong speed and vaulted anyway.
        /// </summary>
        private static GameObject BeltRun()
        {
            GameObject root = NewChunk("Chunk_BeltRun", 40f, 0f, 0f, 2,
                                       ChunkSkill.Timing | ChunkSkill.Vault, "belt");
            Quay(root, 0f, 0f, 40f);

            Belt(root, 8f, 0f, 7f, forward: true);
            Belt(root, 21f, 0f, 7f, forward: false);

            Box(root, "Crate", 33f, 0f, 1.6f, 1.1f, GameLayers.Vaultable, ContainerRust, anchorBottom: true);

            Coins(root, 9f, 1.6f, 5, 1.3f, 0.5f);
            Coins(root, 22f, 1.6f, 5, 1.3f, 0.5f);
            return root;
        }

        /// <summary>
        /// A belt running against the runner, straight into a gap.
        ///
        /// This is the belt doing its job. Seven units of belt drops him to about
        /// seven, and a jump at seven carries roughly 4.9 - so the gap is 4.2,
        /// which is comfortable off the belt and trivially clear at full speed.
        /// The cost of misreading it is a short fall onto a lower deck rather
        /// than a death, because the first time a mechanic changes the length of
        /// your jump it should not also end the run.
        /// </summary>
        private static GameObject BeltGap()
        {
            GameObject root = NewChunk("Chunk_BeltGap", 42f, 0f, 0f, 3,
                                       ChunkSkill.Timing | ChunkSkill.Jump | ChunkSkill.Gap, "belt");
            Quay(root, 0f, 0f, 19f);

            Belt(root, 11f, 0f, 7f, forward: false);

            // The gap, with a lower deck under it, so a miss costs the drop and
            // the climb out rather than the run.
            //
            // 2.4 down: over the 1.6 vault ceiling, so it cannot simply be run
            // through and vaulted out of, and under the 3.2 jump, so the far face
            // is a wall run and a ledge grab - moves he has had since level 3. The
            // decks either side are three thick and their undersides are below the
            // lower deck, so both faces are solid wall all the way up with no lip
            // to be caught under.
            Quay(root, 19f, -2.4f, 4.2f);
            RuinPiece(root, "QuayFaceNear", 18.8f, -2.4f, 0.25f, 2.4f, QuayTone, 6);
            RuinPiece(root, "QuayFaceFar", 23.2f, -2.4f, 0.25f, 2.4f, QuayTone, 6);

            Quay(root, 23.2f, 0f, 18.8f);

            Coins(root, 12f, 1.6f, 5, 1.3f, 0.5f);
            Coins(root, 19.5f, 2.2f, 4, 1.1f, 0.9f);
            return root;
        }

        /// <summary>
        /// Three steam vents in the quay, opening and shutting on the clock.
        ///
        /// <b>The spacing is the fairness.</b> A vent cannot be jumped or slid
        /// under, so the only answer to one that is blowing is to arrive later -
        /// and arriving later takes room to slow down in. Braking from 9 to about
        /// 4 over nine units shifts the arrival by roughly three quarters of a
        /// second, against an armed window of 0.84. So: fourteen units of open
        /// quay before the first one, and ten between each - enough to step past
        /// any window from any start, and enough to land a jump over one and be
        /// back up to pace before the next.
        ///
        /// Their phases are a third apart, so they blow in sequence rather than
        /// together. A runner who gets the timing of the first is not handed the
        /// other two.
        /// </summary>
        private static GameObject SteamVents()
        {
            GameObject root = NewChunk("Chunk_SteamVents", 46f, 0f, 0f, 4,
                                       ChunkSkill.Timing, "vent");
            Quay(root, 0f, 0f, 46f);

            Vent(root, 14f, 0f, 0f);
            Vent(root, 24f, 0f, 0.34f);
            Vent(root, 34f, 0f, 0.67f);

            // Over the top of each one, for anyone taking the jump rather than
            // the wait.
            Coins(root, 12.5f, 2.6f, 4, 1.2f, 0.7f);
            Coins(root, 22.5f, 2.6f, 4, 1.2f, 0.7f);
            return root;
        }

        /// <summary>
        /// The two of them together, which is what the pair is for.
        ///
        /// A belt running against the runner through a pair of vents. The vents
        /// are timed off his arrival, and the belt changes how long he takes to
        /// cross them - so the rhythm he learned in Chunk_SteamVents is not the
        /// rhythm here, and he has to read the steam rather than count it.
        /// </summary>
        private static GameObject BeltVents()
        {
            GameObject root = NewChunk("Chunk_BeltVents", 50f, 0f, 0f, 5,
                                       ChunkSkill.Timing | ChunkSkill.Jump, "vent");
            Quay(root, 0f, 0f, 50f);

            Belt(root, 12f, 0f, 10f, forward: false);

            // The belt has already taken a third off his pace by the time he
            // reaches the first one, so the brake has less left to give - which is
            // the point of putting them together, and why these two are ten apart
            // rather than nine.
            Vent(root, 21f, 0f, 0.1f);
            Vent(root, 32f, 0f, 0.5f);

            // Past the belt, at full pace again, one more on its own.
            Vent(root, 40f, 0f, 0.85f);

            Coins(root, 13f, 1.5f, 4, 1.2f, 0.4f);
            Coins(root, 24f, 1.5f, 4, 1.2f, 0.4f);
            return root;
        }

        /// <summary>
        /// A block of containers across the quay: up the face, along the top, and
        /// off the far end.
        ///
        /// The face is on the Wall layer, so it is run up and the top edge grabbed
        /// - the move from Chunk_WallClimb, in a place where what is being climbed
        /// is obviously climbable. Four units up, so the level carries on along
        /// the top of the stack and drops back down at the end of it.
        /// </summary>
        private static GameObject ContainerYard()
        {
            const float up = 4f;

            GameObject root = NewChunk("Chunk_ContainerYard", 44f, 0f, 0f, 5,
                                       ChunkSkill.WallRun | ChunkSkill.LedgeGrab | ChunkSkill.Jump, "yard");
            Quay(root, 0f, 0f, 14f);

            // The stack that is climbed, drawn as three containers on top of each
            // other so the thing being run up has a scale to it.
            Box(root, "StackFace", 14f, up, 2.4f, 7f, GameLayers.Wall, ContainerTeal);
            Ribs(root, 14f, up - 4f, 2.4f, 4f);

            // Along the top.
            Quay(root, 16.4f, up, 12f);
            Container(root, 18f, up, 3.2f, 1.2f, ContainerRust);
            Container(root, 24f, up, 3.2f, 1.2f, ContainerTeal);

            // Down the far side in two steps, so the drop is a pair of hops
            // rather than one fall that forces a roll.
            Quay(root, 28.4f, up - 2f, 4f);
            Quay(root, 32.4f, 0f, 11.6f);

            Coins(root, 13f, 1.6f, 4, 1.2f, 1.6f);
            Coins(root, 20f, up + 2.2f, 4, 1.2f, 0.5f);
            return root;
        }

        // ================================================================= //
        // The pieces
        // ================================================================= //

        /// <summary>Quay decking. The rooftop slab in the port's own colour.</summary>
        private static GameObject Quay(GameObject parent, float leftX, float topY, float width)
        {
            GameObject go = Ground(parent, leftX, topY, width);
            go.GetComponent<SpriteRenderer>().color = QuayTone;
            return go;
        }

        /// <summary>
        /// A belt let into the quay, running with the runner or against him.
        ///
        /// The trigger is only as tall as a bootlace: it is asking whether he is
        /// standing on the belt, and the component checks that he is grounded
        /// besides, so jumping along a belt does not carry its speed.
        /// </summary>
        private static void Belt(GameObject parent, float x, float groundY, float width, bool forward)
        {
            float drift = forward ? BeltDrift : -BeltDrift;

            // The recess, and the rollers at either end.
            RuinPiece(parent, "BeltBed", x, groundY - BeltDepth, width, BeltDepth, BeltTone, 6);
            RuinPiece(parent, "Roller0", x - 0.18f, groundY - BeltDepth - 0.06f, 0.3f, BeltDepth + 0.12f, BeltRoller, 7);
            RuinPiece(parent, "Roller1", x + width - 0.12f, groundY - BeltDepth - 0.06f, 0.3f, BeltDepth + 0.12f, BeltRoller, 7);

            var go = new GameObject(forward ? "BeltForward" : "BeltBack");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(x, groundY, 0f);

            // Which way it runs, painted on it. The chevrons are children of the
            // belt and slid along by the component.
            const float spacing = 1.2f;
            int count = Mathf.Max(2, Mathf.FloorToInt(width / spacing));
            var marks = new Transform[count];

            for (int i = 0; i < count; i++)
            {
                GameObject mark = RuinPiece(go, "Chevron" + i, 0f, -BeltDepth + 0.02f,
                                            0.5f, 0.07f, ChevronTone, 8);
                mark.transform.localPosition = new Vector3(i * spacing, -BeltDepth * 0.5f, 0f);
                marks[i] = mark.transform;
            }

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(width, 0.3f);
            box.offset = new Vector2(width * 0.5f, 0.1f);
            box.isTrigger = true;

            var belt = go.AddComponent<ConveyorStrip>();
            EditorUtil.SetFloat(belt, "drift", drift);
            EditorUtil.SetFloat(belt, "chevronSpacing", spacing);
            EditorUtil.SetObjectArray(belt, "chevrons", marks);
        }

        /// <summary>
        /// A steam vent: a grating in the quay and a column of steam over it, on
        /// the laser gate's arrival timing.
        ///
        /// <paramref name="phase"/> is where in its cycle it is when the runner
        /// gets to it, so a row of them can be set against each other rather than
        /// all blowing together.
        /// </summary>
        private static void Vent(GameObject parent, float x, float groundY, float phase)
        {
            const float width = VentWidth;

            RuinPiece(parent, "VentGrate", x - 0.1f, groundY, width + 0.2f, 0.12f, VentMetal, 7);
            for (int i = 0; i < 3; i++)
                RuinPiece(parent, "VentSlot" + i, x + 0.2f + i * 0.45f, groundY + 0.12f, 0.28f, 0.05f, BeltTone, 8);

            GameObject steam = Box(parent, "Steam", x, groundY, width, VentHeight,
                                   GameLayers.Hazard, SteamTone, anchorBottom: true, trigger: true);
            steam.GetComponent<SpriteRenderer>().sortingOrder = 14;
            steam.AddComponent<Hazard>();

            // On the clock, not on the approach - and that is the whole of what
            // makes a vent playable.
            //
            // A laser sits at knee height, so deciding before the runner arrives
            // whether it is lit is fair: the answer is "jump" either way. Steam
            // standing on the floor is not that. The first cut was 4.2 tall and
            // authored lit, which could be neither jumped nor slid under - a
            // guaranteed death with nothing to be done about it.
            //
            // It is 2.2 now and can be jumped (see VentHeight), and it runs on the
            // clock, so it opens and shuts in front of the runner instead of being
            // decided before he gets there. That leaves two answers rather than
            // none: jump it, or hold down and let it pass.
            //
            // 2.4 seconds round, armed for a third of it: 0.84 lethal against 1.56
            // open, and a braked runner crosses the 1.5 units of steam in under
            // half a second. The clock is the run's own, reset every attempt.
            var gate = steam.AddComponent<LaserGate>();
            EditorUtil.SetInt(gate, "timing", (int)LaserGate.GateTiming.RunClock);
            EditorUtil.SetFloat(gate, "period", 2.4f);
            EditorUtil.SetFloat(gate, "arrivalPhase", phase);
            EditorUtil.SetFloat(gate, "armedFraction", 0.35f);
            EditorUtil.SetFloat(gate, "warnFraction", 0.3f);
            EditorUtil.SetObject(gate, "beamRenderer", steam.GetComponent<SpriteRenderer>());
            EditorUtil.SetColor(gate, "armedColour", SteamTone);
            EditorUtil.SetColor(gate, "idleColour", new Color(0.92f, 0.94f, 0.96f, 0f));
        }

        /// <summary>A container standing on the deck, ribbed down its face.</summary>
        private static void Container(GameObject parent, float x, float groundY,
                                      float width, float height, Color colour)
        {
            Box(parent, "Container", x, groundY, width, height, GameLayers.Vaultable, colour,
                anchorBottom: true);
            Ribs(parent, x, groundY, width, height);
        }

        /// <summary>
        /// The corrugation down a container's side. Thin dark lines, which is what
        /// stops a coloured rectangle reading as a coloured rectangle.
        /// </summary>
        private static void Ribs(GameObject parent, float x, float bottomY, float width, float height)
        {
            int count = Mathf.Max(2, Mathf.FloorToInt(width / 0.32f));
            float step = width / count;

            for (int i = 1; i < count; i++)
                RuinPiece(parent, "Rib" + i, x + i * step, bottomY + 0.08f,
                          0.06f, height - 0.16f, ContainerRib, 11);
        }
    }
}
