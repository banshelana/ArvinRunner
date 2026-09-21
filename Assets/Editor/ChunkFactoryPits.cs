using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Pits with something in them that bites, and the ropes and hooks that cross
    /// them.
    ///
    /// <b>The pit</b> is a gap the runner cannot jump. A gap chunk asks for a
    /// jump; these ask for the rope, and so they are laid out wider than a jump
    /// and a double jump together can carry. The floor is solid - a runner who
    /// misses lands in the fire or among the crocodiles rather than falling out
    /// of the level, which is what makes the miss read as a miss and not as a
    /// hole in the roof.
    ///
    /// <b>The rope</b> is caught in the air with the raised hand, so the line
    /// through is: jump from the lip, catch, swing, let go. The swing is authored
    /// rather than simulated (see <see cref="SwingRope"/>), which means the
    /// release is the same every time and a pit can be measured against one
    /// landing instead of against a spread of them.
    ///
    /// The numbers: the grip rests 4.8 above the roof and the hand reaches 2.0
    /// above the feet, so a jump (3.2) puts the hand through the grip on the way
    /// up. Let go at 45 degrees at 14.1 - ten forward, ten up - from about 3.97
    /// up, and the flight runs six tenths of a second and five and a half to six
    /// and three-quarter units. That lands 11.3 to 12.5 past the lip, so a rope
    /// pit is 10.5 wide with the far lip inside the near end of that spread.
    ///
    /// <b>The fire and the crocodiles are drawn</b>, from Art/MovingObstacles.
    /// Neither is a mechanism on a timer: the flames are dealt different frames
    /// of the same loop and different sizes so no two burn alike, and the
    /// crocodiles rest between lunges for a length of time that is rolled again
    /// every time (see <see cref="LurkingAnimal"/>), and the snakes idle through their coils between rearings. Both fall back to a tinted
    /// box if their folder has not been imported, the way every other animated
    /// obstacle here does.
    ///
    /// What kills is the trigger band across the pit floor, not the art - so a
    /// runner who falls in dies wherever they land, and is never asked to read a
    /// jaw or a flame tip and time a landing between them.
    /// </summary>
    public static partial class ChunkFactory
    {
        // Down in the pit: dark, and colder than the stone above it.
        private static readonly Color PitShadow = new Color(0.10f, 0.11f, 0.14f);
        private static readonly Color PitWall = new Color(0.24f, 0.25f, 0.30f);

        // The wash the fire throws down the pit, behind the flames.
        private static readonly Color FireGlow = new Color(1f, 0.55f, 0.18f, 0.22f);

        // The water, and the film of it that is drawn back over the crocodiles so
        // they lie in it rather than on it.
        private static readonly Color Water = new Color(0.18f, 0.32f, 0.30f, 0.85f);
        private static readonly Color WaterFilm = new Color(0.22f, 0.38f, 0.35f, 0.45f);

        // Dry rock and dust at the bottom of a snake pit - no water in that one.
        private static readonly Color SnakeBed = new Color(0.26f, 0.23f, 0.19f);

        // Only ever seen if a folder has not been imported.
        private static readonly Color FireFallback = new Color(0.95f, 0.45f, 0.12f, 0.80f);
        private static readonly Color CrocFallback = new Color(0.30f, 0.42f, 0.24f);
        private static readonly Color SnakeFallback = new Color(0.52f, 0.49f, 0.24f);

        private static readonly Color RopeTone = new Color(0.62f, 0.48f, 0.28f);
        private static readonly Color KnotTone = new Color(0.48f, 0.36f, 0.20f);
        private static readonly Color BeamTone = new Color(0.33f, 0.30f, 0.27f);

        /// <summary>Deep enough that what is in it is clear of the lip, shallow enough to stay on screen.</summary>
        private const float PitDepth = 3.5f;

        /// <summary>Where a rope's grip hangs at rest, above the roof it is crossed from.</summary>
        private const float RopeGripHeight = 4.8f;

        private const float RopeLength = 4f;

        /// <summary>At 45 degrees: ten forward and ten up.</summary>
        private const float RopeReleaseSpeed = 14.1f;

        /// <summary>
        /// How long a crocodile is, at the import height, from its tail tip - which
        /// is where it is pinned - to its snout. Only used to place one: the art is
        /// anchored on its tail, so a crocodile turned to face back up the roof
        /// hangs this far to the left of where it is put.
        /// </summary>
        private const float CrocLength = 4.3f;

        /// <summary>Flames stand this far apart along the floor of a fire pit.</summary>
        private const float FlameSpacing = 1.9f;

        /// <summary>
        /// The lunge, in file numbers, with the gape at the top of it held.
        ///
        /// The folder is drawn evenly but the move is not. 1-4 is the animal lying
        /// flat, 5-10 the jaws opening as the head climbs, 11-15 the full rear with
        /// the gape, 16 the jaws shut - one frame, because that is what a snap is -
        /// and 17-24 the settle back down. Played straight through, the gape that
        /// the whole thing is for goes by in a fifth of a second while the settle
        /// takes half of it; 13, 14 and 15 are doubled so the threat is held for
        /// four tenths and the snap out of it lands.
        ///
        /// Nothing is doubled around 16. Slowing the shut would turn the one
        /// moment the animal is quick into the one moment it is slow.
        /// </summary>
        private static readonly int[] CrocLunge =
        {
            // Idle: the four flat drawings, out and back. Looping them rather
            // than freezing one is most of what stops a still pit looking dead.
            1, 2, 3, 4, 3, 2,

            // Display: the jaws cracking, the head climbing, the gape, the shut.
            5, 6, 7, 8, 9, 10, 11, 12,
            13, 13, 14, 14, 15, 15,
            16, 17, 18, 19, 20, 21, 22, 23, 24
        };

        /// <summary>Where the idle ends and the display begins in <see cref="CrocLunge"/>.</summary>
        private const int CrocRestLast = 5;

        /// <summary>
        /// The snake, in file numbers: an idle it never leaves, and a display it
        /// comes back down out of.
        ///
        /// 1-13 is the body slithering through S-curves with the head down, 14-22
        /// the coil gathering and the head climbing out of it, and 23-24 the mouth
        /// open. There is no drawn uncoil, so the way down is the way up read
        /// backwards and thinned out - a snake drops out of a strike faster than
        /// it winds up into one.
        ///
        /// The idle is taken out and back rather than round. 1 is nearly straight
        /// and 13 is gathered, so looping them would snap the body flat once a
        /// cycle; out and back, every step is one the body actually makes. The
        /// flattest two are left out of it - they are the fully relaxed extreme,
        /// and an idle wants to look awake.
        /// </summary>
        private static readonly int[] SnakeRear =
        {
            // Idle, out and back.
            4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 12, 11, 10, 9, 8, 7, 6, 5,

            // Up into the coil, the gape held and flickering, then down. It opens
            // on 13, which is also the idle's turning point, because the display
            // can begin from anywhere in the idle - and 13 is a step away from
            // every drawing in it, where 14 is a step away from only one.
            13, 14, 15, 16, 17, 18, 19, 20, 21, 22,
            23, 24, 24, 23, 24, 24, 24,
            23, 22, 21, 20, 19, 18, 16, 14
        };

        /// <summary>Where the idle ends and the display begins in <see cref="SnakeRear"/>.</summary>
        private const int SnakeRestLast = 17;

        /// <summary>What is waiting at the bottom.</summary>
        private enum PitFill
        {
            Fire,
            Crocodiles,
            Snakes
        }

        private static readonly HashSet<string> PitTags = new HashSet<string> { "rope", "hook" };

        /// <summary>
        /// True for the chunks made here. Like the war zone, they stay out of the
        /// assembled levels' pool, so a level already learned is never reshuffled
        /// by adding a crossing.
        /// </summary>
        public static bool IsPitCrossing(LevelChunk chunk) =>
            chunk != null && PitTags.Contains(chunk.variantTag);

        private static List<LevelChunk> CreatePitChunks()
        {
            return new List<LevelChunk>
            {
                Save(RopeFire()),
                Save(HookCroc()),
                Save(RopeChain()),
                Save(RopeStrike()),
                Save(RopeSnake()),
                Save(HookSnake())
            };
        }

        // ================================================================= //
        // The crossings
        // ================================================================= //

        /// <summary>
        /// One rope over one pit of fire: the crossing taught on its own.
        ///
        /// Ten units of roof to build up on, the lip at 10, and the rope three
        /// units out over the pit - far enough that the jump has to be taken from
        /// the lip and not from short of it, close enough that the hand is still
        /// rising as it arrives. The far lip is at 20.5 and the release lands
        /// between 21.3 and 22.5.
        /// </summary>
        private static GameObject RopeFire()
        {
            GameObject root = NewChunk("Chunk_RopeFire", 36f, 0f, 0f, 4,
                                       ChunkSkill.Jump | ChunkSkill.Gap | ChunkSkill.Timing, "rope");
            Ground(root, 0f, 0f, 10f);
            Pit(root, 10f, 0f, 10.5f, PitFill.Fire);
            Ground(root, 20.5f, 0f, 15.5f);

            Rope(root, 13f, RopeGripHeight);

            // Up to the catch, then along the swing.
            Coins(root, 8f, 2.6f, 4, 1.1f, 1.4f);
            Coins(root, 17f, 4.2f, 4, 1.2f, 0.8f);
            return root;
        }

        /// <summary>
        /// A cable over a crocodile pit, with a hook on a trolley waiting at the
        /// top of it.
        ///
        /// Unlike the rope, the hook carries the runner the length of the cable
        /// rather than for a fixed swing, so the pit can be wider than a swing
        /// crosses - fifteen units here. It is caught at 12, about two units past
        /// the lip, drops the runner at 24.5, and the cable runs downhill so the
        /// ride reads as going somewhere rather than hanging.
        /// </summary>
        private static GameObject HookCroc()
        {
            GameObject root = NewChunk("Chunk_HookCroc", 40f, 0f, 0f, 5,
                                       ChunkSkill.Jump | ChunkSkill.Gap | ChunkSkill.Timing, "hook");
            Ground(root, 0f, 0f, 9f);
            Pit(root, 9f, 0f, 15f, PitFill.Crocodiles);
            Ground(root, 24f, 0f, 16f);

            HookLine(root, new Vector2(7.5f, 6.2f), new Vector2(26.5f, 4.9f), 12f, 24.5f);

            Coins(root, 7f, 2.6f, 4, 1.1f, 1.3f);
            Coins(root, 15f, 3.4f, 6, 1.4f, 0.5f);
            return root;
        }

        /// <summary>
        /// Two ropes over one long pit: let go of the first, catch the second in
        /// the flight off it.
        ///
        /// The second rope is hung where the first release actually puts the hand.
        /// The first is at 13 and swings to 45 degrees, which carries its grip to
        /// 13 + 4 sin45 = 15.83; the second stands three units on from there, and
        /// its grip hangs at 6.9 rather than 4.8 because the runner arrives at it
        /// climbing, near the top of the release arc, not off the ground.
        ///
        /// Miss the second and the flight still carries most of the way across -
        /// the landing is in the pit, near the far wall, which is the right
        /// punishment for a late second catch rather than an unreadable one.
        /// </summary>
        private static GameObject RopeChain()
        {
            const float firstX = 13f;
            float releaseX = firstX + RopeLength * Mathf.Sin(45f * Mathf.Deg2Rad);

            GameObject root = NewChunk("Chunk_RopeChain", 48f, 0f, 0f, 6,
                                       ChunkSkill.Jump | ChunkSkill.Gap | ChunkSkill.Timing, "rope");
            Ground(root, 0f, 0f, 10f);
            Pit(root, 10f, 0f, 17f, PitFill.Crocodiles);
            Ground(root, 27f, 0f, 21f);

            Rope(root, firstX, RopeGripHeight);
            Rope(root, releaseX + 3f, 6.9f);

            Coins(root, 8f, 2.6f, 4, 1.1f, 1.4f);
            Coins(root, 17f, 5.4f, 4, 1.2f, 0.9f);
            Coins(root, 24f, 4.4f, 4, 1.2f, 0.8f);
            return root;
        }

        /// <summary>
        /// The rope, and a drone waiting on the far side of the pit.
        ///
        /// The bomb is laid seventeen units past the landing, which is the same
        /// spacing Chunk_RuinStrike uses coming out of the ruin: the mark appears
        /// while the runner is back on their feet and running, not while they are
        /// still in the air with nothing to steer with.
        /// </summary>
        private static GameObject RopeStrike()
        {
            GameObject root = NewChunk("Chunk_RopeStrike", 56f, 0f, 0f, 6,
                                       ChunkSkill.Jump | ChunkSkill.Gap | ChunkSkill.Timing, "rope");
            Ground(root, 0f, 0f, 10f);
            Pit(root, 10f, 0f, 10.5f, PitFill.Fire);
            Ground(root, 20.5f, 0f, 35.5f);

            Rope(root, 13f, RopeGripHeight);

            GameObject strike = Strike(root, 38f, 0f, bomb: true);
            Drone(root, 41f, DroneFlyHeight, strike, bomb: true);

            Coins(root, 8f, 2.6f, 4, 1.1f, 1.4f);
            Coins(root, 17f, 4.2f, 4, 1.2f, 0.8f);
            Coins(root, 36f, 2.8f, 5, 1.2f, 1.1f);
            return root;
        }

        /// <summary>
        /// A snake pit on a rope, and the crossing at its plainest.
        ///
        /// The same 10.5 pit and the same rope as Chunk_RopeFire, deliberately:
        /// by the time the snakes turn up the swing is a move the player owns,
        /// and changing the geometry as well as the contents of the pit would
        /// make a known crossing feel unfamiliar for no reason. What is different
        /// is underneath, and that is the only thing that should be.
        /// </summary>
        private static GameObject RopeSnake()
        {
            GameObject root = NewChunk("Chunk_RopeSnake", 36f, 0f, 0f, 4,
                                       ChunkSkill.Jump | ChunkSkill.Gap | ChunkSkill.Timing, "rope");
            Ground(root, 0f, 0f, 10f);
            Pit(root, 10f, 0f, 10.5f, PitFill.Snakes);
            Ground(root, 20.5f, 0f, 15.5f);

            Rope(root, 13f, RopeGripHeight);

            Coins(root, 8f, 2.6f, 4, 1.1f, 1.4f);
            Coins(root, 17f, 4.2f, 4, 1.2f, 0.8f);
            return root;
        }

        /// <summary>
        /// A long snake pit on the cable, with a gallery to crawl under on the
        /// far side of it.
        ///
        /// The hook drops the runner at 24.5 and the gallery starts at 31, which
        /// is six units of roof - about two thirds of a second at running pace.
        /// Enough to land, find their feet and go down on purpose; not enough to
        /// stop thinking. That join is the point of the chunk: the two new moves
        /// of this level, back to back, with the minimum of roof between them.
        /// </summary>
        private static GameObject HookSnake()
        {
            GameObject root = NewChunk("Chunk_HookSnake", 46f, 0f, 0f, 6,
                                       ChunkSkill.Jump | ChunkSkill.Gap | ChunkSkill.Slide |
                                       ChunkSkill.Timing, "hook");
            Ground(root, 0f, 0f, 9f);
            Pit(root, 9f, 0f, 15f, PitFill.Snakes);
            Ground(root, 24f, 0f, 22f);

            HookLine(root, new Vector2(7.5f, 6.2f), new Vector2(26.5f, 4.9f), 12f, 24.5f);
            Gallery(root, 31f, 0f, 6f);

            Coins(root, 7f, 2.6f, 4, 1.1f, 1.3f);
            Coins(root, 15f, 3.4f, 6, 1.4f, 0.5f);
            Coins(root, 30.5f, 0.35f, 6, 1.2f, 0f);
            return root;
        }

        // ================================================================= //
        // The pieces
        // ================================================================= //

        /// <summary>
        /// A pit in the roof at <paramref name="topY"/>, its near lip at
        /// <paramref name="leftX"/>. The chunk lays the roof either side of it;
        /// this lays the walls, the floor at the bottom, what is in it and the
        /// band that kills.
        /// </summary>
        private static void Pit(GameObject parent, float leftX, float topY, float width, PitFill fill)
        {
            float floorY = topY - PitDepth;
            float right = leftX + width;

            // What you see down there behind everything else, and the cut faces of
            // the roof either side of it.
            RuinPiece(parent, "PitBack", leftX, floorY, width, PitDepth, PitShadow, 2);
            RuinPiece(parent, "PitWallNear", leftX - 0.25f, floorY, 0.25f, PitDepth, PitWall, 6);
            RuinPiece(parent, "PitWallFar", right, floorY, 0.25f, PitDepth, PitWall, 6);

            // A floor, so a missed crossing lands in what is down there instead of
            // dropping out of the level.
            Ground(parent, leftX, floorY, width);

            // The band that kills: a trigger sitting on the floor, tall enough that
            // a runner coming down into the pit meets it whatever they were doing.
            // The art on top of it is scenery and can be replaced freely.
            GameObject band = Box(parent, fill == PitFill.Fire ? "FireBand" : "CrocBand",
                                  leftX, floorY, width, 2.2f, GameLayers.Hazard,
                                  new Color(1f, 1f, 1f, 0f), anchorBottom: true, trigger: true);

            var hazard = band.AddComponent<Hazard>();
            EditorUtil.SetInt(hazard, "cause",
                              (int)(fill == PitFill.Fire ? DeathCause.Burned : DeathCause.Eaten));

            switch (fill)
            {
                case PitFill.Fire: FireFill(parent, leftX, floorY, width); break;
                case PitFill.Crocodiles: CrocodileFill(parent, leftX, floorY, width); break;
                default: SnakeFill(parent, leftX, floorY, width); break;
            }
        }

        /// <summary>
        /// Fire along the floor of the pit: a wash of light on the walls behind
        /// it, and a row of flames.
        ///
        /// The same drawn loop for every flame, dealt out so no two of them ever
        /// line up. Each is opened on a different frame of the cycle, run at a
        /// slightly different rate, and scaled differently - tallest in the
        /// middle of the pit, shorter at the walls, so the row has a shape rather
        /// than reading as a fence. A single flame dropped in twenty times at one
        /// size and one phase pulses like a row of indicator lights, which is the
        /// one thing fire never does.
        /// </summary>
        private static void FireFill(GameObject parent, float leftX, float floorY, float width)
        {
            RuinPiece(parent, "FireGlow", leftX, floorY, width, PitDepth + 0.8f, FireGlow, 7);

            Sprite[] frames = MovingObstacleImport.Frames("fire");
            int count = Mathf.Max(2, Mathf.FloorToInt(width / FlameSpacing));
            float step = width / count;

            for (int i = 0; i < count; i++)
            {
                float x = leftX + step * (i + 0.5f);

                // 1 in the middle of the pit, falling away toward either wall.
                float middle = 1f - Mathf.Abs((x - leftX) / width - 0.5f) * 2f;
                float tall = Mathf.Lerp(0.62f, 1.05f, middle);

                Flame(parent, i, x, floorY - 0.15f, tall, frames);
            }
        }

        /// <summary>
        /// One flame. Sunk slightly into the floor, so it sits in the pit rather
        /// than standing on it.
        /// </summary>
        private static void Flame(GameObject parent, int index, float x, float bottomY,
                                  float scale, Sprite[] frames)
        {
            var go = new GameObject("Flame" + index);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(x, bottomY, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 9;

            if (frames.Length == 0)
            {
                renderer.sprite = PlaceholderArt.Load("box");
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = new Vector2(1.1f, 2.8f);
                renderer.color = FireFallback;
                go.transform.localPosition += new Vector3(0f, 1.4f * scale, 0f);
                return;
            }

            renderer.sprite = frames[0];

            var book = go.AddComponent<SpriteFlipbook>();
            EditorUtil.SetObject(book, "target", renderer);
            EditorUtil.SetObjectArray(book, "frames", frames);
            EditorUtil.SetBool(book, "loop", true);

            // Seven frames apart and 24 frames round: five flames in and the
            // sequence has not repeated a starting frame yet.
            EditorUtil.SetInt(book, "startFrame", index * 7);

            // And a rate that is never quite the rate next door, so two flames
            // that did happen to line up would drift apart again within a second.
            EditorUtil.SetFloat(book, "fps", 15f + (index % 3) * 1.5f);
        }

        /// <summary>
        /// Crocodiles in the water: one every six units or so, facing the way the
        /// runner is going or back up at them, turn and turn about.
        ///
        /// The film of water is drawn back over them afterwards. It is what makes
        /// them lie in the pool rather than on top of it, and it covers the line
        /// along the belly where the art is pinned.
        /// </summary>
        private static void CrocodileFill(GameObject parent, float leftX, float floorY, float width)
        {
            RuinPiece(parent, "Water", leftX, floorY, width, 0.9f, Water, 7);

            Sprite[] frames = MovingObstacleImport.Frames("crocodile");

            int count = Mathf.Max(1, Mathf.RoundToInt(width / 6f));
            float step = width / count;

            for (int i = 0; i < count; i++)
                Crocodile(parent, i, leftX + step * (i + 0.5f), floorY + 0.45f, frames,
                          facingRight: i % 2 == 0);

            RuinPiece(parent, "WaterFilm", leftX, floorY, width, 0.55f, WaterFilm, 11);
        }

        /// <summary>
        /// One crocodile, lying with its belly at <paramref name="y"/>.
        ///
        /// The art is pinned on the tail, which is the end of the animal that
        /// holds still while the head rears (see MovingObstacleImport.AnchorTail),
        /// so the drawing runs forward from where it is put. One turned around is
        /// mirrored about that same tail, so it is placed a body-length on to keep
        /// it inside the pit.
        /// </summary>
        private static void Crocodile(GameObject parent, int index, float x, float y,
                                      Sprite[] frames, bool facingRight)
        {
            var go = new GameObject("Crocodile" + index);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(facingRight ? x - CrocLength * 0.5f
                                                                 : x + CrocLength * 0.5f, y, 0f);
            go.transform.localScale = new Vector3(facingRight ? 1f : -1f, 1f, 1f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 9;

            if (frames.Length == 0)
            {
                renderer.sprite = PlaceholderArt.Load("box");
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = new Vector2(CrocLength, 0.85f);
                renderer.color = CrocFallback;
                visual.transform.localPosition = new Vector3(CrocLength * 0.5f, 0.42f, 0f);
                return;
            }

            Sprite[] lunge = ByFileNumber(frames, CrocLunge);
            renderer.sprite = lunge[0];

            var book = visual.AddComponent<SpriteFlipbook>();
            EditorUtil.SetObject(book, "target", renderer);
            EditorUtil.SetObjectArray(book, "frames", lunge);

            // The display is 23 frames at 15: just under two seconds, which is a
            // threat - reared up, jaws open, shut - rather than the half-second
            // strike a crocodile actually makes. The point of it is to be read
            // from the roof above. The idle runs at a third of that.
            Lurk(go, book, CrocRestLast, lunge.Length - 1, 5f, 15f, index);
        }

        /// <summary>
        /// Snakes among the rocks. Closer together than the crocodiles - a snake
        /// is half the length of one - and every other one turned around, so the
        /// pit is not a row of clones all pointing the same way.
        /// </summary>
        private static void SnakeFill(GameObject parent, float leftX, float floorY, float width)
        {
            RuinPiece(parent, "SnakeBed", leftX, floorY, width, 0.5f, SnakeBed, 7);

            Sprite[] frames = MovingObstacleImport.Frames("snake");

            int count = Mathf.Max(1, Mathf.RoundToInt(width / 4f));
            float step = width / count;

            for (int i = 0; i < count; i++)
                Snake(parent, i, leftX + step * (i + 0.5f), floorY + 0.12f, frames,
                      facingRight: i % 2 == 0);
        }

        /// <summary>
        /// One snake, lying with its underside at <paramref name="y"/>.
        ///
        /// Pinned on its centre of mass across, so unlike the crocodile it is
        /// placed where it is wanted and turning one around needs no correction.
        /// </summary>
        private static void Snake(GameObject parent, int index, float x, float y,
                                  Sprite[] frames, bool facingRight)
        {
            var go = new GameObject("Snake" + index);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale = new Vector3(facingRight ? 1f : -1f, 1f, 1f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 9;

            if (frames.Length == 0)
            {
                renderer.sprite = PlaceholderArt.Load("box");
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = new Vector2(2.1f, 0.3f);
                renderer.color = SnakeFallback;
                visual.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                return;
            }

            Sprite[] rear = ByFileNumber(frames, SnakeRear);
            renderer.sprite = rear[0];

            var book = visual.AddComponent<SpriteFlipbook>();
            EditorUtil.SetObject(book, "target", renderer);
            EditorUtil.SetObjectArray(book, "frames", rear);

            // The idle is slow - a snake at rest barely moves - and the rear is
            // twice that, which is what makes the strike out of it land.
            LurkingAnimal lurk = Lurk(go, book, SnakeRestLast, rear.Length - 1, 8f, 16f, index);

            // It lies on rock rather than in water: no float, only the body
            // working, which the drawings already do.
            EditorUtil.SetFloat(lurk, "bobHeight", 0f);
            EditorUtil.SetFloat(lurk, "drift", 0.05f);
        }

        /// <summary>
        /// Gives an animal its idle and its display, and a rest between them that
        /// no two in one pit share - see <see cref="LurkingAnimal"/>.
        /// </summary>
        private static LurkingAnimal Lurk(GameObject go, SpriteFlipbook book,
                                          int restLast, int lungeLast,
                                          float restFps, float lungeFps, int index)
        {
            var lurk = go.AddComponent<LurkingAnimal>();
            EditorUtil.SetObject(lurk, "book", book);
            EditorUtil.SetInt(lurk, "restFirst", 0);
            EditorUtil.SetInt(lurk, "restLast", restLast);
            EditorUtil.SetInt(lurk, "lungeFirst", restLast + 1);
            EditorUtil.SetInt(lurk, "lungeLast", lungeLast);
            EditorUtil.SetFloat(lurk, "restFps", restFps);
            EditorUtil.SetFloat(lurk, "lungeFps", lungeFps);

            // Not a multiple of anything: two animals in one pit drift apart
            // instead of falling into step.
            EditorUtil.SetFloat(lurk, "phase", 0.7f + index * 1.3f);
            return lurk;
        }

        /// <summary>
        /// A rope on a beam, its grip hanging <paramref name="gripY"/> above the
        /// roof. The anchor is the beam, so the anchor sits a rope's length above
        /// the grip.
        /// </summary>
        private static void Rope(GameObject parent, float x, float gripY)
        {
            float anchorY = gripY + RopeLength;

            // Something for it to hang from: a beam across the gap and a bracket
            // at the knot end of it.
            RuinPiece(parent, "RopeBeam", x - 2.4f, anchorY, 4.8f, 0.35f, BeamTone, 6);
            RuinPiece(parent, "RopeBracket", x - 0.12f, anchorY - 0.22f, 0.24f, 0.22f, BeamTone, 7);

            var go = new GameObject("SwingRope");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(x, anchorY, 0f);

            // Stretched from the anchor to the grip every frame - its authored
            // width is the rope's thickness, and its height is overwritten.
            GameObject line = RuinPiece(go, "Rope", -0.05f, -RopeLength, 0.1f, RopeLength, RopeTone, 12);
            GameObject knot = RuinPiece(go, "Knot", -0.16f, -RopeLength - 0.16f, 0.32f, 0.32f, KnotTone, 13);

            var rope = go.AddComponent<SwingRope>();
            EditorUtil.SetObject(rope, "line", line.GetComponent<SpriteRenderer>());
            EditorUtil.SetObject(rope, "knot", knot.transform);
            EditorUtil.SetFloat(rope, "length", RopeLength);
            EditorUtil.SetFloat(rope, "releaseAngle", 45f);
            EditorUtil.SetFloat(rope, "releaseSpeed", RopeReleaseSpeed);
        }

        /// <summary>
        /// A cable between two posts with a hook on it.
        /// <paramref name="catchX"/> is where the hook waits and
        /// <paramref name="letGoX"/> is where it stops and drops the runner - both
        /// given in the chunk's x, and turned into points on the cable here so the
        /// posts can be moved without redoing the ride.
        /// </summary>
        private static void HookLine(GameObject parent, Vector2 postA, Vector2 postB,
                                     float catchX, float letGoX)
        {
            RuinPiece(parent, "HookPostA", postA.x - 0.2f, 0f, 0.4f, postA.y, BeamTone, 6);
            RuinPiece(parent, "HookPostB", postB.x - 0.2f, 0f, 0.4f, postB.y, BeamTone, 6);

            // The cable: one piece, laid along the line between the posts.
            Vector2 span = postB - postA;
            GameObject cable = RuinPiece(parent, "Cable", 0f, 0f, span.magnitude, 0.08f, MetalTone, 7);
            cable.transform.localPosition = new Vector3((postA.x + postB.x) * 0.5f,
                                                        (postA.y + postB.y) * 0.5f, 0f);
            cable.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);

            var go = new GameObject("ZipHook");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(postA.x, postA.y, 0f);

            var trolley = new GameObject("Trolley");
            trolley.transform.SetParent(go.transform, false);

            RuinPiece(trolley, "Wheels", -0.25f, -0.18f, 0.5f, 0.3f, MetalTone, 12);
            RuinPiece(trolley, "HookArm", -0.05f, -0.75f, 0.1f, 0.6f, MetalTone, 12);
            RuinPiece(trolley, "HookBar", -0.35f, -0.85f, 0.7f, 0.12f, MetalTone, 12);

            var hook = go.AddComponent<ZipHook>();
            EditorUtil.SetObject(hook, "trolley", trolley.transform);
            EditorUtil.SetVector2(hook, "rideFrom", At(postA, postB, catchX) - postA);
            EditorUtil.SetVector2(hook, "rideTo", At(postA, postB, letGoX) - postA);
        }

        /// <summary>The point on the line from a to b that is directly under x.</summary>
        private static Vector2 At(Vector2 a, Vector2 b, float x)
        {
            float t = Mathf.Approximately(b.x, a.x) ? 0f : Mathf.Clamp01((x - a.x) / (b.x - a.x));
            return Vector2.Lerp(a, b, t);
        }
    }
}
