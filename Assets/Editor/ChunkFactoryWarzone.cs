using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// The war zone: drones that bomb or fire on the roof, ruined buildings the
    /// runner goes through by the window, a machine-gun bunker and a trench.
    ///
    /// <b>The drone</b> is the helicopter's job done by a smaller machine, and it
    /// reuses every solved number from Chunk_AirStrike - start three units past
    /// the target, drift at 1.2, fire off the runner's time to the target - so it
    /// is fair across the whole speed range for the same reasons. It delivers
    /// either a missile, lased onto the roof, or a bomb, released and left to
    /// fall with only the mark as warning.
    ///
    /// <b>The ruin</b> is a wall with one hole in it. Below the window it is a
    /// sill too tall to vault (2.0, against the 1.6 vault ceiling) and above it
    /// the facade runs up past anything a double jump reaches, so the only line
    /// through is a jump timed to have the feet over the sill as the body reaches
    /// the wall. A single jump keeps the feet above 2.0 for 3.8-4.6 units against
    /// a 1.8 unit crossing - roughly two units of take-off to choose from.
    ///
    /// It comes at three heights, because a level that never leaves the roof it
    /// started on has one rhythm however its obstacles change. The plain ruin
    /// stands on the roof. The tower ruin stands on a rubble mound that has to be
    /// wall-run first, and leaves the level four units higher. The basement ruin
    /// has lost its floor: through the window is a drop into the cellar, and the
    /// way out leaves the level four units lower.
    ///
    /// Every piece of a ruin the runner can touch is on the Vaultable layer, as
    /// the glass is. The wall-run probe looks for Wall and Ground only, so a
    /// missed window is a crash against the stone rather than a scramble up the
    /// face and over the top of the building.
    /// </summary>
    public static partial class ChunkFactory
    {
        // Dusty stone, lighter than the rooftop so the building stands forward of
        // it, and an interior only a shade darker than the roof - the runner is a
        // black silhouette and spends a whole room in front of it.
        private static readonly Color RuinStone = new Color(0.62f, 0.58f, 0.52f);
        private static readonly Color RuinShade = new Color(0.47f, 0.44f, 0.40f);
        private static readonly Color RuinInside = new Color(0.30f, 0.28f, 0.26f);
        private static readonly Color RuinRubble = new Color(0.55f, 0.49f, 0.42f);

        // What you can see through an opening: darker than the room behind it, so
        // the way through reads as a hole from a screen away instead of as another
        // patch of wall. The room itself cannot go this dark - the runner is a
        // black silhouette and spends the length of it in front of the far wall.
        private static readonly Color OpeningDark = new Color(0.13f, 0.12f, 0.12f);
        private static readonly Color Soot = new Color(0.08f, 0.07f, 0.07f, 0.45f);

        private static readonly Color Sandbag = new Color(0.66f, 0.58f, 0.42f);
        private static readonly Color SandbagShade = new Color(0.50f, 0.43f, 0.31f);
        private static readonly Color GunMetal = new Color(0.22f, 0.23f, 0.24f);
        private static readonly Color TrenchSoil = new Color(0.40f, 0.34f, 0.27f);

        /// <summary>Top of the wall under the window. Above the 1.6 vault ceiling, under the 3.2 jump.</summary>
        private const float RuinSillHeight = 2.0f;

        /// <summary>
        /// Underside of the lintel. The top of a jump puts the head at 4.95, so
        /// 5.3 lets the apex pass under it with room rather than grazing it.
        /// </summary>
        private const float RuinWindowTop = 5.3f;

        /// <summary>Clear of a double jump from the roof (7.55 at the head).</summary>
        private const float RuinHeight = 10.5f;

        /// <summary>Inside, facade to back wall.</summary>
        private const float RuinRoomLength = 15f;

        /// <summary>The blown-out back wall's opening - walked through standing.</summary>
        private const float RuinBreachHeight = 2.6f;

        /// <summary>Well over the 4.95 a jump reaches, like the helicopter's 6.</summary>
        private const float DroneFlyHeight = 6.2f;

        // The gun's band, in the one gap there is between a sliding runner (0.79
        // tall) and a standing one (1.75).
        private const float GunBandBottom = 0.95f;
        private const float GunBandTop = 1.35f;

        // The bunker the gun hangs under: an overhead to slide on through, and a
        // ledge to grab for anyone who jumped the fire instead.
        private const float BunkerUnderside = 1.4f;
        private const float BunkerTop = 3.0f;

        private static readonly HashSet<string> WarzoneTags =
            new HashSet<string> { "drone", "ruin", "gunnest", "trench" };

        /// <summary>
        /// True for the chunks made here. The assembled levels leave them out, so
        /// adding war-zone content never reshuffles a level already learned.
        /// </summary>
        public static bool IsWarzone(LevelChunk chunk) =>
            chunk != null && WarzoneTags.Contains(chunk.variantTag);

        private static List<LevelChunk> CreateWarzoneChunks()
        {
            return new List<LevelChunk>
            {
                Save(DroneBomb()),
                Save(DroneMissile()),
                Save(RuinWindow()),
                Save(RuinStrike()),
                Save(RuinTower()),
                Save(RuinBasement()),
                Save(DroneBarrage()),
                Save(GunNest()),
                Save(Trench())
            };
        }

        // ================================================================= //
        // Drones
        // ================================================================= //

        /// <summary>A drone drops a bomb on the roof ahead. Jump the fire.</summary>
        private static GameObject DroneBomb()
        {
            GameObject root = NewChunk("Chunk_DroneBomb", 40f, 0f, 0f, 4,
                                       ChunkSkill.Jump | ChunkSkill.Timing, "drone");
            Ground(root, 0f, 0f, 40f);

            GameObject strike = Strike(root, 22f, 0f, bomb: true);
            Drone(root, 25f, DroneFlyHeight, strike, bomb: true);

            Coins(root, 20f, 2.8f, 5, 1.2f, 1.1f);
            return root;
        }

        /// <summary>A drone lases the roof and fires a missile into it.</summary>
        private static GameObject DroneMissile()
        {
            GameObject root = NewChunk("Chunk_DroneMissile", 40f, 0f, 0f, 4,
                                       ChunkSkill.Jump | ChunkSkill.Timing, "drone");
            Ground(root, 0f, 0f, 40f);

            GameObject strike = Strike(root, 24f, 0f);
            Drone(root, 27f, DroneFlyHeight, strike, bomb: false);

            Coins(root, 22f, 2.8f, 5, 1.2f, 1.1f);
            return root;
        }

        /// <summary>
        /// Two drones, a bomb and then a missile. Sixteen units apart: a jump over
        /// the first fire lands about six units on, which leaves the second mark
        /// a full second of running away when it lights - the same spacing that
        /// made the laser corridor fair.
        /// </summary>
        private static GameObject DroneBarrage()
        {
            GameObject root = NewChunk("Chunk_DroneBarrage", 52f, 0f, 0f, 5,
                                       ChunkSkill.Jump | ChunkSkill.Timing, "drone");
            Ground(root, 0f, 0f, 52f);

            GameObject bomb = Strike(root, 18f, 0f, bomb: true);
            Drone(root, 21f, DroneFlyHeight, bomb, bomb: true);

            GameObject missile = Strike(root, 34f, 0f);
            Drone(root, 37f, DroneFlyHeight + 0.6f, missile, bomb: false);

            Coins(root, 16f, 2.8f, 5, 1.2f, 1.1f);
            Coins(root, 32f, 2.8f, 5, 1.2f, 1.1f);
            return root;
        }

        // ================================================================= //
        // Ruins
        // ================================================================= //

        /// <summary>
        /// A ruined building across the roof. In by the window, over the rubble,
        /// out through the hole where the back wall was.
        /// </summary>
        private static GameObject RuinWindow()
        {
            GameObject root = NewChunk("Chunk_RuinWindow", 40f, 0f, 0f, 4,
                                       ChunkSkill.Jump | ChunkSkill.Vault | ChunkSkill.Timing, "ruin");
            Ground(root, 0f, 0f, 40f);

            Ruin(root, 14f, 0f, 0f, rubble: true);
            return root;
        }

        /// <summary>
        /// The ruin, and a bomb waiting outside the back of it.
        ///
        /// The drone wakes while the runner is still inside - thirty units out, off
        /// screen as always - and the bomb is laid seventeen units past the breach,
        /// so the runner is on open roof and running before the mark appears.
        /// </summary>
        private static GameObject RuinStrike()
        {
            GameObject root = NewChunk("Chunk_RuinStrike", 56f, 0f, 0f, 5,
                                       ChunkSkill.Jump | ChunkSkill.Vault | ChunkSkill.Timing, "ruin");
            Ground(root, 0f, 0f, 56f);

            Ruin(root, 6f, 0f, 0f, rubble: true);

            GameObject strike = Strike(root, 40f, 0f, bomb: true);
            Drone(root, 43f, DroneFlyHeight, strike, bomb: true);

            Coins(root, 38f, 2.8f, 5, 1.2f, 1.1f);
            return root;
        }

        /// <summary>
        /// A ruin standing on a mound of rubble four units up. Jump into the
        /// mound's face to run up it, grab the top, then take the window from up
        /// there. The level carries on at the new height.
        ///
        /// The mound is the tower face from Chunk_WallClimb - same height, same
        /// Wall layer - so the climb is one the player already knows. Ten units
        /// of run between the top of it and the facade: the ledge climb is a
        /// committed move, and the window jump wants a take-off chosen on the
        /// run, not straight out of a climb.
        /// </summary>
        private static GameObject RuinTower()
        {
            const float up = 4f;

            GameObject root = NewChunk("Chunk_RuinTower", 48f, 0f, up, 5,
                                       ChunkSkill.WallRun | ChunkSkill.LedgeGrab | ChunkSkill.Jump | ChunkSkill.Vault,
                                       "ruin");
            Ground(root, 0f, 0f, 13f);

            Box(root, "RubbleMound", 13f, up, 2f, 7f, GameLayers.Wall, RuinRubble);
            RuinPiece(root, "MoundBroken0", 12.7f, up - 0.2f, 0.5f, 0.45f, RuinRubble, 11);
            RuinPiece(root, "MoundBroken1", 14.4f, up - 0.1f, 0.7f, 0.3f, RuinShade, 11);

            Ground(root, 15f, up, 33f);
            Ruin(root, 25f, up, up, rubble: true);

            Coins(root, 14f, up + 1.5f, 3, 1.2f, 0.4f);
            return root;
        }

        /// <summary>
        /// A ruin whose floor has fallen into the cellar. Through the window is a
        /// four-unit drop onto the cellar floor - hard enough to roll - and the way
        /// out is the cellar breach, so the level carries on four units lower.
        ///
        /// No rubble in here. The window jump lands six units in and the roll off
        /// it runs another four and a half, so anything to vault inside the room
        /// would be met mid-roll, which cannot be answered.
        /// </summary>
        private static GameObject RuinBasement()
        {
            const float down = -4f;

            GameObject root = NewChunk("Chunk_RuinBasement", 40f, 0f, down, 4,
                                       ChunkSkill.Jump | ChunkSkill.Timing, "ruin");
            Ground(root, 0f, 0f, 14f);

            Ruin(root, 14f, 0f, down, rubble: false);

            // The cellar floor, running on out of the breach.
            Ground(root, 15f, down, 25f);

            // Where the floor went: slabs hanging off the facade and the back wall.
            RuinPiece(root, "BrokenFloor0", 15f, -0.35f, 2.2f, 0.35f, RuinShade, 4);
            RuinPiece(root, "BrokenFloor1", 27.8f, -0.35f, 2.2f, 0.35f, RuinShade, 4);

            Coins(root, 17f, 0.5f, 4, 1.1f, -2.2f);
            return root;
        }

        /// <summary>
        /// A ruined building standing on ground at <paramref name="groundY"/>, its
        /// facade's left face at x. It spans x to x + 17: a one-unit facade, a
        /// fifteen-unit room, and a one-unit back wall. The room's floor is at
        /// <paramref name="floorY"/>, and the chunk has to lay that floor itself -
        /// the building only stands on it.
        /// </summary>
        private static void Ruin(GameObject parent, float x, float groundY, float floorY, bool rubble)
        {
            float inner = x + 1f;
            float back = inner + RuinRoomLength;
            float sill = groundY + RuinSillHeight;
            float lintel = groundY + RuinWindowTop;

            // A sunken room's facade reaches down past the cellar floor, so the
            // floor slab butts into it rather than leaving a slot under the wall.
            float facadeFoot = floorY < groundY ? floorY - GroundThickness : groundY;

            // ---- what the runner touches ---------------------------------- //

            // The facade, in two pieces with the window between them.
            Box(parent, "RuinSill", x, facadeFoot, 1f, sill - facadeFoot,
                GameLayers.Vaultable, RuinStone, anchorBottom: true);
            Box(parent, "RuinFacade", x, lintel, 1f, RuinHeight - RuinWindowTop,
                GameLayers.Vaultable, RuinStone, anchorBottom: true);

            // The floor above, which is the room's ceiling. A jump inside passes
            // under it; a double jump taps it and comes back down.
            Box(parent, "RuinFloorAbove", inner, lintel, RuinRoomLength, 0.6f,
                GameLayers.Ground, RuinShade, anchorBottom: true);

            // Eight units in, past where the window jump lands at any speed, and
            // low enough to vault - 1.8 wide, so the vault goes clean over.
            if (rubble)
            {
                Box(parent, "Rubble", inner + 8f, floorY, 1.8f, 1.2f,
                    GameLayers.Vaultable, RuinRubble, anchorBottom: true);
            }

            // What is left of the back wall hangs from the floor above; the rest
            // of it is the way out.
            float breachTop = floorY + RuinBreachHeight;
            Box(parent, "RuinBackWall", back, breachTop, 1f, lintel + 0.6f - breachTop,
                GameLayers.Vaultable, RuinStone, anchorBottom: true);

            // ---- scenery ------------------------------------------------------ //

            // Behind the runner: the room, and a storey above it open to the sky
            // where the roof came down.
            RuinPiece(parent, "RuinInterior", inner, floorY, RuinRoomLength, lintel - floorY, RuinInside, 3);
            RuinPiece(parent, "RuinUpperStorey", inner, lintel + 0.6f,
                      RuinRoomLength * 0.55f, 3.6f, RuinInside, 3);
            RuinPiece(parent, "RuinUpperStoreyBroken", inner + RuinRoomLength * 0.55f, lintel + 0.6f,
                      2.6f, 1.7f, RuinInside, 3);
            RuinPiece(parent, "HangingSlab", inner + 4f, lintel - 0.55f, 0.35f, 0.55f, RuinShade, 4);

            // A broken crown on the facade and a stub of back wall above the floor.
            RuinPiece(parent, "FacadeCrown0", x, groundY + RuinHeight, 0.45f, 0.7f, RuinStone, 10);
            RuinPiece(parent, "FacadeCrown1", x + 0.6f, groundY + RuinHeight, 0.3f, 0.35f, RuinStone, 10);
            RuinPiece(parent, "BackWallStub", back + 0.15f, lintel + 0.6f, 0.7f, 1.3f, RuinStone, 10);

            // The window picked out, so it reads as the way in from a screen away:
            // a sill ledge, a lintel, and soot where the fire came out of it.
            RuinPiece(parent, "SillLedge", x - 0.15f, sill - 0.18f, 1.3f, 0.18f, RuinShade, 11);
            RuinPiece(parent, "Lintel", x - 0.1f, lintel, 1.2f, 0.25f, RuinShade, 11);
            RuinPiece(parent, "WindowHole", x - 0.05f, sill, 1.1f, lintel - sill, OpeningDark, 4);
            RuinPiece(parent, "BreachHole", back - 0.05f, floorY, 1.1f, RuinBreachHeight, OpeningDark, 4);
            RuinPiece(parent, "WindowSoot", x, lintel + 0.25f, 1f, 1.2f, Soot, 11);
            RuinPiece(parent, "BreachSoot", back, breachTop, 1f, 0.9f, Soot, 11);

            // The line through: an arc from the take-off through the window, and
            // a smaller one over the rubble.
            Coins(parent, x - 3f, sill + 0.6f, 5, 1.2f, 1.0f);
            if (rubble) Coins(parent, inner + 7.2f, floorY + 1.9f, 4, 1.0f, 0.6f);
        }

        // ================================================================= //
        // Gun nest and trench
        // ================================================================= //

        /// <summary>
        /// A machine gun under a sandbagged bunker, sweeping the roof in front of
        /// it at waist height. Two ways past, and both are fair:
        ///
        ///  * <b>Under.</b> Slide beneath the fire (a sliding runner is 0.79 tall,
        ///    the band starts at 0.95) and carry straight on under the bunker. Its
        ///    underside is 1.4 and it is five long, so the slide turns into a skate
        ///    and rides out the far end.
        ///  * <b>Over.</b> Jump before the fire - from 12 to about 13.7 - and the arc
        ///    is still above the band as it reaches the bunker face, low enough
        ///    there to grab its 3.0 ledge and climb onto the roof. Later take-offs
        ///    land on the roof outright.
        ///
        /// What does not work is standing up in the band, or a jump from so far
        /// back that it comes down in it. The fire comes in bursts on the approach
        /// - driven off the runner's distance like a laser gate - and is always
        /// firing by the time they get there, so the bursts are a warning, not a
        /// gap to wait for.
        /// </summary>
        private static GameObject GunNest()
        {
            const float bandStart = 14f;
            const float bunkerX = 18f;
            const float bunkerLength = 5f;
            float muzzleX = bunkerX - 0.7f;

            GameObject root = NewChunk("Chunk_GunNest", 36f, 0f, 0f, 4,
                                       ChunkSkill.Slide | ChunkSkill.LedgeGrab | ChunkSkill.Timing, "gunnest");
            Ground(root, 0f, 0f, 36f);

            // ---- the bunker ------------------------------------------------- //

            // Ground layer on purpose, unlike the ruins: its face is the ledge the
            // over route grabs.
            Box(root, "Bunker", bunkerX, BunkerUnderside, bunkerLength, BunkerTop - BunkerUnderside,
                GameLayers.Ground, Sandbag, anchorBottom: true);

            RuinPiece(root, "BunkerSeam0", bunkerX, BunkerUnderside + 0.5f, bunkerLength, 0.06f, SandbagShade, 6);
            RuinPiece(root, "BunkerSeam1", bunkerX, BunkerUnderside + 1.05f, bunkerLength, 0.06f, SandbagShade, 6);
            RuinPiece(root, "BunkerLip", bunkerX - 0.1f, BunkerTop - 0.2f, bunkerLength + 0.2f, 0.2f, SandbagShade, 6);

            // Props behind the runner, holding it up. No colliders.
            RuinPiece(root, "BunkerPost0", bunkerX + 0.4f, 0f, 0.3f, BunkerUnderside, RuinShade, 4);
            RuinPiece(root, "BunkerPost1", bunkerX + bunkerLength - 0.7f, 0f, 0.3f, BunkerUnderside, RuinShade, 4);

            // ---- the gun --------------------------------------------------- //

            // On a mount under the bunker's front lip, barrel pointing back up the
            // roof. Scenery: its lowest edge is above a sliding runner.
            RuinPiece(root, "GunMount", bunkerX - 0.2f, 1.05f, 0.22f, BunkerUnderside - 1.05f, GunMetal, 12);
            RuinPiece(root, "GunBody", bunkerX - 0.45f, 1.02f, 0.6f, 0.22f, GunMetal, 12);
            RuinPiece(root, "GunBarrel", muzzleX, 1.1f, 0.5f, 0.07f, GunMetal, 12);

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(muzzleX, (GunBandBottom + GunBandTop) * 0.5f, 0f);

            SpriteRenderer flash = StrikePart(muzzle, "Flash", PlaceholderArt.Load("missile_flame"), 13);
            flash.transform.localScale = new Vector3(0.7f, 0.9f, 1f);
            if (flash.sprite != null)
                flash.transform.localPosition = new Vector3(-flash.sprite.bounds.size.x * 0.35f, 0f, 0f);

            // ---- the fire --------------------------------------------------- //

            float range = muzzleX - bandStart;

            GameObject fire = Box(root, "GunFire", bandStart, GunBandBottom, range, GunBandTop - GunBandBottom,
                                  GameLayers.Hazard, new Color(1f, 0.8f, 0.35f, 0.12f),
                                  anchorBottom: true, trigger: true);
            fire.AddComponent<Hazard>();

            // Bursts on the approach, firing on arrival - see the note above.
            var gate = fire.AddComponent<LaserGate>();
            EditorUtil.SetFloat(gate, "arrivalPhase", 0.15f);
            EditorUtil.SetFloat(gate, "armedFraction", 0.6f);
            EditorUtil.SetFloat(gate, "wavelength", 5f);
            EditorUtil.SetFloat(gate, "warnFraction", 0.2f);
            EditorUtil.SetObject(gate, "beamRenderer", fire.GetComponent<SpriteRenderer>());
            EditorUtil.SetColor(gate, "armedColour", new Color(1f, 0.8f, 0.35f, 0.16f));
            EditorUtil.SetColor(gate, "idleColour", new Color(1f, 0.8f, 0.35f, 0f));

            var tracer = fire.AddComponent<TracerFire>();
            EditorUtil.SetObject(tracer, "muzzle", muzzle.transform);
            EditorUtil.SetObject(tracer, "flash", flash);
            EditorUtil.SetObject(tracer, "round", PlaceholderArt.Load("laser"));
            EditorUtil.SetFloat(tracer, "range", range);

            // Both lines: along the slide, and up over the bunker roof.
            Coins(root, bandStart - 0.5f, 0.55f, 7, 1.3f, 0f);
            Coins(root, bunkerX + 0.8f, BunkerTop + 0.8f, 3, 1.3f, 0.3f);
            return root;
        }

        /// <summary>
        /// A trench across the roof with wire at the bottom, sandbags on the far
        /// side and a coil of wire after them: jump, vault, jump.
        ///
        /// The sandbags are seven units past the far lip. A jump over the trench
        /// lands between 16.7 and 19.6 depending on speed, and the vault wants
        /// the runner back on their feet and within 2.4 of the bags - so the three
        /// moves come one after another without any of them landing on the next.
        /// </summary>
        private static GameObject Trench()
        {
            GameObject root = NewChunk("Chunk_Trench", 38f, 0f, 0f, 3,
                                       ChunkSkill.Jump | ChunkSkill.Gap | ChunkSkill.Vault, "trench");
            Ground(root, 0f, 0f, 12f);

            // The trench: soil walls behind, a floor, and wire along it.
            RuinPiece(root, "TrenchSoil", 12f, -2.6f, 5f, 2.6f, TrenchSoil, 3);
            Ground(root, 12f, -2.6f, 5f);
            Spikes(root, 12f, -2.6f, 5f);

            Ground(root, 17f, 0f, 21f);

            // Sandbags, under the vault ceiling and narrow enough to go clean over.
            Box(root, "Sandbags", 24f, 0f, 1.8f, 1.1f, GameLayers.Vaultable, Sandbag, anchorBottom: true);
            RuinPiece(root, "SandbagSeam0", 24f, 0.36f, 1.8f, 0.06f, SandbagShade, 11);
            RuinPiece(root, "SandbagSeam1", 24f, 0.73f, 1.8f, 0.06f, SandbagShade, 11);

            Spikes(root, 31f, 0f, 2.2f);

            Coins(root, 12.5f, 2.5f, 4, 1.1f, 1.0f);
            Coins(root, 23.5f, 2.0f, 3, 1.0f, 0.5f);
            Coins(root, 30.5f, 2.2f, 3, 1.1f, 0.8f);
            return root;
        }

        // ================================================================= //
        // The pieces
        // ================================================================= //

        /// <summary>
        /// The drone: the helicopter's strike logic on a smaller airframe, with
        /// the one drawing bobbed so it hovers rather than hangs.
        /// </summary>
        private static GameObject Drone(GameObject parent, float x, float flyHeight,
                                        GameObject strike, bool bomb)
        {
            var go = new GameObject(bomb ? "BomberDrone" : "MissileDrone") { layer = GameLayers.Hazard };
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(x, flyHeight, 0f);

            // The fuselage, not the prop disc or the skids. At its 1.35 import
            // height the body runs about 0.35-1.05 above the skids and 3.2 from
            // the camera ball back to the engine.
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(3.2f, 0.7f);
            box.offset = new Vector2(-0.2f, 0.7f);
            box.isTrigger = true;

            go.AddComponent<Hazard>();

            // The helicopter's solved numbers - see ChunkFactory.AirStrike.
            var travel = go.AddComponent<MovingObstacle>();
            EditorUtil.SetFloat(travel, "speed", 1.2f);
            EditorUtil.SetFloat(travel, "activationDistance", 30f);

            // The flying loop from Art/MovingObstacles/drone: the prop turning and
            // the airframe pitching nose-down and back about its tail, once a loop.
            // At 16fps that pitch takes 1.5s, slow enough to read as the aircraft
            // riding the air rather than rocking. Without the frames, the still
            // drawing; without that, a box. Drawn nose-left, the way it flies.
            Sprite[] frames = MovingObstacleImport.Frames("drone");
            bool animated = frames.Length > 1;

            if (!animated)
            {
                Sprite still = ArtImport.Load("war_drone");
                frames = still != null ? new[] { still } : new Sprite[0];
            }

            Frames(go, frames, 16f,
                   fallbackSize: new Vector2(3.9f, 1.35f),
                   fallbackTint: new Color(0.24f, 0.26f, 0.27f));

            // The bob is on top of the drawn pitch, so it keeps only a trace of
            // its own tilt when the frames are there - two pitches out of step
            // would read as a wobble.
            var hover = go.transform.Find("Visual").gameObject.AddComponent<HoverBob>();
            EditorUtil.SetFloat(hover, "amplitude", 0.1f);
            EditorUtil.SetFloat(hover, "tilt", animated ? 0.4f : 2f);

            // The bomb falls from the belly; the missile leaves the rail under the
            // wing, where the art draws one.
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(go.transform, false);
            muzzle.transform.localPosition = bomb ? new Vector3(-0.1f, 0.3f, 0f)
                                                  : new Vector3(0.1f, 0.35f, 0f);

            var gun = go.AddComponent<HelicopterStrike>();
            EditorUtil.SetObject(gun, "strike", strike.GetComponent<MissileStrike>());
            EditorUtil.SetObject(gun, "muzzle", muzzle.transform);

            // A flying loop with no shot drawn in it: loop the whole thing through
            // the shot, as the helicopter does. Left at the defaults, the approach
            // would loop only the first two frames.
            int last = Mathf.Max(0, frames.Length - 1);
            EditorUtil.SetInt(gun, "approachFirst", 0);
            EditorUtil.SetInt(gun, "approachLast", last);
            EditorUtil.SetInt(gun, "fireFirst", 0);
            EditorUtil.SetInt(gun, "fireLast", last);
            EditorUtil.SetBool(gun, "launchDrawn", false);

            // Lighter by a bomb, it lifts; a missile off the rail shoves it back
            // the way it came. Both spring back inside half a second.
            EditorUtil.SetVector2(gun, "recoil", bomb ? new Vector2(0f, 1.1f) : new Vector2(1.4f, 0.4f));

            return go;
        }

        /// <summary>A piece of scenery with no collider, bottom edge at bottomY.</summary>
        private static GameObject RuinPiece(GameObject parent, string name, float x, float bottomY,
                                            float width, float height, Color colour, int sortingOrder)
        {
            GameObject go = Box(parent, name, x, bottomY, width, height, 0, colour,
                                anchorBottom: true, collider: false);
            go.GetComponent<SpriteRenderer>().sortingOrder = sortingOrder;
            return go;
        }
    }
}
