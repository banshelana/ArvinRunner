using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Chunks that put two existing mechanics on top of each other.
    ///
    /// The library up to here teaches one thing per chunk: a gap is a gap, a
    /// laser is a laser. That is right for the first half of a campaign and runs
    /// out in the second, where the interest has to come from having to solve
    /// two problems on the same beat - jump a gap *and* read a beam, slide a
    /// press *and* land on a panel that is already falling.
    ///
    /// It also fills two holes in the spread. Before these there was a single
    /// difficulty-5 chunk in the whole game and only two that used the wall run,
    /// so the last levels had nothing to escalate into.
    ///
    /// The rule followed throughout: <b>two demands may overlap, but only one of
    /// them may be invisible.</b> A beam over a gap is fair because both are on
    /// screen while there is still time to act. A collapsing panel under a blast
    /// marker is fair for the same reason. What is avoided is stacking two things
    /// that each need the same second of warning.
    /// </summary>
    public static partial class ChunkFactory
    {
        private static List<LevelChunk> CreateAdvancedChunks()
        {
            return new List<LevelChunk>
            {
                Save(LaserGap()),
                Save(CraneGap()),
                Save(PressAlley()),
                Save(StrikeRun()),
                Save(BikeGauntlet()),
                Save(TowerLasers()),
                Save(LiftBeam()),
                Save(GustLift()),
                Save(BusJump())
            };
        }

        // ================================================================= //

        /// <summary>
        /// A beam standing in the middle of a gap. The jump is easy and the
        /// timing is not: commit while it is lit and there is no way to stop.
        /// </summary>
        private static GameObject LaserGap()
        {
            GameObject root = NewChunk("Chunk_LaserGap", 34f, 0f, 0f, 4,
                                       ChunkSkill.Jump | ChunkSkill.Gap | ChunkSkill.Timing, "laser");
            Ground(root, 0f, 0f, 13f);
            Ground(root, 20f, 0f, 14f);

            // Mid-gap, and open when the runner arrives - which it has to be.
            // A lit beam here would be unanswerable: the runner is airborne over
            // a gap with nowhere to land and nothing to jump from. What it does
            // instead is blink through the approach and settle dark as they
            // commit, which is a tell that the jump is on rather than a gate.
            Laser(root, 16.5f, 0f, 5.5f, arrivalPhase: 0.8f);

            // The gate that actually asks something is on the far ledge, lit, at
            // a height the landing jump can clear - so the gap and the beam are
            // one continuous move rather than two.
            Laser(root, 23f, 0f, 2.6f, arrivalPhase: 0.2f);

            Coins(root, 14f, 3.4f, 5, 1.2f, 1.2f);
            return root;
        }

        /// <summary>
        /// The crane swings its load across a gap instead of along a roof. Same
        /// crate, entirely different question - a mistimed jump has nowhere to
        /// land rather than just costing the run.
        /// </summary>
        private static GameObject CraneGap()
        {
            GameObject root = NewChunk("Chunk_CraneGap", 34f, 0f, 0f, 5,
                                       ChunkSkill.Jump | ChunkSkill.Gap | ChunkSkill.Timing, "crane");
            Ground(root, 0f, 0f, 12f);
            Ground(root, 19f, 0f, 15f);

            // The tower's foot at the very edge of the near roof, so the crate
            // swings out over the gap - 11.8 to 17.2 - and only just reaches back
            // over the ledge at the end of its swing. Simulated against the jump:
            // one jump gets across through 40-60% of the swing depending on speed,
            // and jump plus air jump through all of it.
            CraneImport.Rig art = CraneImport.Load();
            float swingX = art != null ? 12f - art.BaseRight - 0.05f : 15.5f;
            Crane(root, swingX, 0f, phase: 0f);

            Coins(root, 13f, 3.2f, 4, 1.3f, 1.0f);
            return root;
        }

        /// <summary>
        /// Two presses out of step. The first is read on the way in; the second
        /// has to be read while still under the first, which is the whole idea.
        /// </summary>
        private static GameObject PressAlley()
        {
            GameObject root = NewChunk("Chunk_PressAlley", 34f, 0f, 0f, 5,
                                       ChunkSkill.Slide | ChunkSkill.Timing, "press");
            Ground(root, 0f, 0f, 34f);

            // Half a cycle apart, so one is always rising as the other falls. The
            // drawn presses are each timed to the runner's arrival instead, and
            // still read that way: ten units apart is under half their 23-unit
            // cycle, so the second is slamming as the runner comes out from under
            // the first - and waiting again by the time they reach it. Two slides.
            Press(root, 12f, phase: 0f);
            Press(root, 22f, phase: 0.5f);

            Coins(root, 16f, 0.6f, 4, 1.2f, 0f);
            return root;
        }

        /// <summary>
        /// The gunship strike, and then the roof gives way.
        ///
        /// The panels are deliberately *after* the blast rather than under it.
        /// Under it they would be a trap - the fair line is to jump the fire, and
        /// jumping onto a panel that is already falling is not a read, it is a
        /// coin toss. Put them a beat later and the same jump becomes the setup
        /// for the next problem instead.
        ///
        /// The helicopter geometry is lifted from Chunk_AirStrike unchanged,
        /// because those numbers were solved against the 9-to-15 speed range and
        /// re-deriving them for a different roof would only find them again.
        /// </summary>
        private static GameObject StrikeRun()
        {
            GameObject root = NewChunk("Chunk_StrikeRun", 44f, 0f, 0f, 5,
                                       ChunkSkill.Jump | ChunkSkill.Timing | ChunkSkill.Gap, "airstrike");
            Ground(root, 0f, 0f, 29f);

            GameObject strike = Strike(root, 24f, 0f);
            Helicopter(root, 27f, 6f, strike);

            for (int i = 0; i < 3; i++)
            {
                GameObject panel = Box(root, "Panel" + i, 30f + i * 4.2f, 0f, 3f, 0.6f,
                                       GameLayers.Ground, new Color(0.55f, 0.35f, 0.30f));
                panel.AddComponent<CollapsingPlatform>();
            }

            Ground(root, 42.5f, 0f, 1.5f);

            Coins(root, 22f, 2.8f, 4, 1.2f, 1.0f);
            return root;
        }

        /// <summary>
        /// Two bikes rather than one.
        ///
        /// Spaced by where they will actually be met, not by where they sit. A
        /// bike wakes 30 units out and closes at the sum of both speeds, so it
        /// arrives 15 units short of its start against a runner doing 9 and 11.25
        /// short against one doing 15 - a window, not a point. The two windows
        /// here are 11-14.75 and 25-28.75, which leaves ten units of clear ground
        /// between them at any speed: enough to land, breathe and read the second.
        /// </summary>
        private static GameObject BikeGauntlet()
        {
            GameObject root = NewChunk("Chunk_BikeGauntlet", 46f, 0f, 0f, 4,
                                       ChunkSkill.Jump, "oncoming");
            Ground(root, 0f, 0f, 46f);

            Motorbike(root, 26f, 0f);
            Motorbike(root, 40f, 0f);

            Coins(root, 11f, 2.6f, 4, 1.2f, 0.8f);
            Coins(root, 25f, 2.6f, 4, 1.2f, 0.8f);
            return root;
        }

        /// <summary>
        /// A tower with a beam across the deck at the top. The wall run and the
        /// ledge climb are committed moves - once started there is no stopping -
        /// so the beam has to be readable from the bottom of the wall, which is
        /// why it is slow and set well back from the ledge.
        /// </summary>
        private static GameObject TowerLasers()
        {
            GameObject root = NewChunk("Chunk_TowerLasers", 34f, 0f, 4f, 5,
                                       ChunkSkill.WallRun | ChunkSkill.LedgeGrab | ChunkSkill.Timing, "wall");
            Ground(root, 0f, 0f, 13f);

            Box(root, "TowerFace", 13f, 4f, 2f, 7f, GameLayers.Wall, WallTone);

            Ground(root, 15f, 4f, 19f);

            // Eight units past the ledge, lit, and low enough to hurdle. The
            // climb is a committed move with no room to react inside it, so the
            // beam is set back far enough that the runner is on their feet and
            // running before it has to be answered.
            Laser(root, 23f, 4f, 2.6f, arrivalPhase: 0.2f);

            Coins(root, 16f, 5.4f, 4, 1.2f, 0.5f);
            return root;
        }

        /// <summary>
        /// A rising platform through a beam. The platform decides when you can be
        /// there and the beam decides whether you may - and neither waits.
        /// </summary>
        private static GameObject LiftBeam()
        {
            GameObject root = NewChunk("Chunk_LiftBeam", 34f, 0f, 0f, 4,
                                       ChunkSkill.Timing | ChunkSkill.Gap, "moving");
            Ground(root, 0f, 0f, 10f);

            GameObject lift = Box(root, "Lift", 13f, 0f, 3.5f, 0.7f,
                                  GameLayers.Ground, new Color(0.45f, 0.5f, 0.7f));
            var mover = lift.AddComponent<MovingPlatform>();
            EditorUtil.SetVector2(mover, "travel", new Vector2(0f, 3.2f));
            EditorUtil.SetFloat(mover, "speed", 1.6f);

            // Raised clear of the ground and lit on arrival, so it threatens only
            // the top of the lift's travel: the answer is to cross while the
            // platform is low rather than to time the beam itself.
            Laser(root, 18.5f, 2.6f, 4f, arrivalPhase: 0.2f);

            Ground(root, 22f, 0f, 12f);

            Coins(root, 12f, 3.6f, 4, 1.2f, 0.9f);
            return root;
        }

        /// <summary>
        /// Crosswind over a moving platform. The gust shortens the jump on and
        /// the jump off, and the platform is only in the right place for part of
        /// its travel - so the wind decides the distance and the lift decides the
        /// moment.
        /// </summary>
        private static GameObject GustLift()
        {
            GameObject root = NewChunk("Chunk_GustLift", 36f, 0f, 0f, 5,
                                       ChunkSkill.Gap | ChunkSkill.DoubleJump | ChunkSkill.Timing, "gust");
            Ground(root, 0f, 0f, 11f);

            GameObject lift = Box(root, "Lift", 15f, 0f, 3.5f, 0.7f,
                                  GameLayers.Ground, new Color(0.45f, 0.5f, 0.7f));
            var mover = lift.AddComponent<MovingPlatform>();
            EditorUtil.SetVector2(mover, "travel", new Vector2(0f, 2.6f));
            EditorUtil.SetFloat(mover, "speed", 1.5f);

            GameObject zone = Box(root, "GustZone", 11f, 0f, 11f, 9f, GameLayers.Hazard,
                                  new Color(0.5f, 0.7f, 1f, 0.10f), anchorBottom: true, trigger: true);
            var gust = zone.AddComponent<GustZone>();
            EditorUtil.SetVector2(gust, "force", new Vector2(-7f, 1.2f));

            Ground(root, 23f, 0f, 13f);

            Coins(root, 13f, 3.4f, 5, 1.2f, 1.2f);
            return root;
        }

        /// <summary>
        /// A gap that lands on a bus roof rather than on a roof.
        ///
        /// The bus is 3.0 tall against a 3.2 jump, so this is the one place in
        /// the game where the landing is nearly at the ceiling of what the runner
        /// can reach. Getting it wrong is a clean miss into the gap rather than a
        /// clip, which is what makes it readable.
        /// </summary>
        private static GameObject BusJump()
        {
            GameObject root = NewChunk("Chunk_BusJump", 40f, 0f, 0f, 4,
                                       ChunkSkill.Jump | ChunkSkill.Gap | ChunkSkill.Vault, "bus");
            Ground(root, 0f, 0f, 14f);
            Ground(root, 19f, 0f, 21f);

            // Sat right on the landing edge, so clearing the gap and getting onto
            // the roof are the same jump.
            Prop(root, "bus", 19.5f, 0f);

            Prop(root, "sedan", 33f, 0f);

            Coins(root, 21f, 4.2f, 5, 1.3f, 0.6f);
            return root;
        }

        // ================================================================= //
        // Shared pieces
        // ================================================================= //

        /// <summary>
        /// A beam standing on the surface at baseY.
        ///
        /// <paramref name="arrivalPhase"/> decides the state it holds when the
        /// runner reaches it: under 0.5 it is lit and has to be answered, over
        /// 0.5 it is open. Deterministic on purpose - see LaserGate for why a
        /// clock-driven beam cannot be read in an auto-runner.
        /// </summary>
        private static GameObject Laser(GameObject parent, float x, float baseY, float height,
                                        float arrivalPhase)
        {
            GameObject beam = Box(parent, "Laser", x, baseY, 0.35f, height,
                                  GameLayers.Hazard, HazardTone, anchorBottom: true, trigger: true);
            beam.AddComponent<Hazard>();

            var gate = beam.AddComponent<LaserGate>();
            EditorUtil.SetFloat(gate, "arrivalPhase", arrivalPhase);
            EditorUtil.SetFloat(gate, "armedFraction", 0.5f);
            EditorUtil.SetFloat(gate, "wavelength", 6f);

            return beam;
        }

        /// <summary>
        /// A tower crane standing on the roof at groundY, its load swinging
        /// through x - x is where the crate hangs at rest, and the tower stands
        /// to its left.
        ///
        /// Built from <see cref="CraneImport"/>: the tower is one still sprite,
        /// and the rope, hook and crate turn on a child about the drawn pivot,
        /// with the crate's collider on the same child so it goes wherever the
        /// crate is drawn. The tower has no collider - it stands behind the
        /// runner, the way scenery does. Without crane frames this falls back to
        /// the old red pendulum.
        /// </summary>
        private static GameObject Crane(GameObject parent, float x, float groundY, float phase)
        {
            CraneImport.Rig art = CraneImport.Load();
            if (art == null) return PendulumCrane(parent, x, groundY + 9f, phase);

            var root = new GameObject("Crane");
            root.transform.SetParent(parent.transform, false);
            root.transform.localPosition = new Vector3(x, groundY + art.PivotHeight, 0f);

            // In front of the roof, behind the runner and the load.
            var tower = new GameObject("Tower");
            tower.transform.SetParent(root.transform, false);
            var towerRenderer = tower.AddComponent<SpriteRenderer>();
            towerRenderer.sprite = art.Tower;
            towerRenderer.sortingOrder = 9;

            var swing = new GameObject("Swing");
            swing.transform.SetParent(root.transform, false);

            var load = new GameObject("Load");
            load.transform.SetParent(swing.transform, false);
            var loadRenderer = load.AddComponent<SpriteRenderer>();
            loadRenderer.sprite = art.Loads[0];
            loadRenderer.sortingOrder = 10;
            load.transform.localRotation = Quaternion.Euler(0f, 0f, -art.Angles[0]);

            // On the load rather than the swing: the crate hangs on its slings and
            // tilts less than the rope, so it is placed on each drawing's own
            // crate as the drawings change.
            var crate = new GameObject("Crate") { layer = GameLayers.Hazard };
            crate.transform.SetParent(load.transform, false);
            crate.transform.localPosition = art.CrateCentres[0];
            crate.transform.localRotation = Quaternion.Euler(0f, 0f, art.CrateTilts[0]);
            var box = crate.AddComponent<BoxCollider2D>();
            box.size = art.CrateSize * CraneImport.CrateHitShare;
            box.isTrigger = true;
            crate.AddComponent<Hazard>();

            var crane = root.AddComponent<SwingingCrane>();
            EditorUtil.SetObject(crane, "pivot", swing.transform);
            EditorUtil.SetObject(crane, "load", loadRenderer);
            EditorUtil.SetObjectArray(crane, "frames", art.Loads);
            EditorUtil.SetFloatArray(crane, "frameAngles", art.Angles);
            EditorUtil.SetObject(crane, "crate", crate.transform);
            EditorUtil.SetVector2Array(crane, "crateCentres", art.CrateCentres);
            EditorUtil.SetFloatArray(crane, "crateTilts", art.CrateTilts);
            EditorUtil.SetFloat(crane, "period", art.Period);
            EditorUtil.SetFloat(crane, "phase", phase);

            return root;
        }

        /// <summary>The crane before there was crane art: a red ball on a grey
        /// arm, swinging from a pivot at height.</summary>
        private static GameObject PendulumCrane(GameObject parent, float x, float height, float phase)
        {
            var pivot = new GameObject("CranePivot");
            pivot.transform.SetParent(parent.transform, false);
            pivot.transform.localPosition = new Vector3(x, height, 0f);

            var crane = pivot.AddComponent<SwingingCrane>();
            EditorUtil.SetFloat(crane, "phase", phase);

            GameObject arm = Box(pivot, "Arm", 0f, -3.25f, 0.25f, 6.5f, GameLayers.Ground, MetalTone,
                                 centred: true, collider: false);
            arm.transform.localPosition = new Vector3(0f, -3.25f, 0f);

            GameObject ball = Box(pivot, "Ball", 0f, -7f, 2.2f, 2.2f, GameLayers.Hazard, HazardTone,
                                  centred: true, trigger: true);
            ball.transform.localPosition = new Vector3(0f, -7f, 0f);
            ball.AddComponent<Hazard>();

            return pivot;
        }

        /// <summary>An industrial press with its head at x.</summary>
        /// <summary>
        /// The slam press, standing on the roof at x: its base plate flush with
        /// the roof, the head waiting high enough to slide under.
        ///
        /// Built from <see cref="PressImport"/>. The machine is one sprite that
        /// changes frame by frame; the head and the piston rod kill, on colliders
        /// that <see cref="CrusherPress"/> moves onto them as each frame draws
        /// them. The frame and posts are scenery. Without press frames this falls
        /// back to the old red block.
        /// </summary>
        private static GameObject Press(GameObject parent, float x, float phase)
        {
            PressImport.Rig art = PressImport.Load();
            if (art == null) return PressBlock(parent, x, phase);

            var rig = new GameObject("Press");
            rig.transform.SetParent(parent.transform, false);
            rig.transform.localPosition = new Vector3(x, 0f, 0f);

            // In front of the roof, behind the runner.
            var body = new GameObject("Body");
            body.transform.SetParent(rig.transform, false);
            var renderer = body.AddComponent<SpriteRenderer>();
            renderer.sprite = art.Frames[0];
            renderer.sortingOrder = 9;

            var head = new GameObject("Head") { layer = GameLayers.Hazard };
            head.transform.SetParent(rig.transform, false);

            var headBox = head.AddComponent<BoxCollider2D>();
            headBox.isTrigger = true;
            headBox.offset = art.HeadCentres[0];
            headBox.size = art.HeadSizes[0];

            var rodBox = head.AddComponent<BoxCollider2D>();
            rodBox.isTrigger = true;

            head.AddComponent<Hazard>();

            // The head's drawn width, trimmed at the rounded ends.
            var sizes = art.HeadSizes
                           .Select(s => new Vector2(s.x * PressImport.HeadHitShare, s.y))
                           .ToArray();

            var press = rig.AddComponent<CrusherPress>();
            EditorUtil.SetObject(press, "body", renderer);
            EditorUtil.SetObjectArray(press, "frames", art.Frames);
            EditorUtil.SetFloatArray(press, "frameDurations", art.Durations);
            EditorUtil.SetObject(press, "headBox", headBox);
            EditorUtil.SetObject(press, "rodBox", rodBox);
            EditorUtil.SetVector2Array(press, "headCentres", art.HeadCentres);
            EditorUtil.SetVector2Array(press, "headSizes", sizes);
            EditorUtil.SetVector2(press, "rodSpan", new Vector2(art.RodLeft, art.RodRight));
            EditorUtil.SetFloat(press, "rodTop", art.RodTop);

            // Timed to the runner's arrival rather than to a phase - see
            // CrusherPress. Every drawn press is waiting when the runner reaches
            // it; phase only offsets the clock a press falls back to with no runner.
            EditorUtil.SetFloat(press, "arrivalTime", art.ArrivalTime);
            EditorUtil.SetFloat(press, "wavelength", art.Wavelength);
            EditorUtil.SetFloat(press, "phase", phase);

            return rig;
        }

        /// <summary>The press before there was press art: a red block on a grey
        /// frame, moved between two heights.</summary>
        private static GameObject PressBlock(GameObject parent, float x, float phase)
        {
            var rig = new GameObject("Press");
            rig.transform.SetParent(parent.transform, false);
            rig.transform.localPosition = new Vector3(x, 0f, 0f);

            Box(rig, "Frame", 0f, 6.4f, 4.5f, 0.6f, GameLayers.Ground, MetalTone,
                centred: true, collider: false);

            GameObject head = Box(rig, "Head", 0f, 4.6f, 3.4f, 2.2f, GameLayers.Hazard, HazardTone,
                                  centred: true, trigger: true);
            head.transform.localPosition = new Vector3(0f, 4.6f, 0f);
            head.AddComponent<Hazard>();

            var press = rig.AddComponent<CrusherPress>();
            EditorUtil.SetObject(press, "head", head.transform);
            EditorUtil.SetFloat(press, "travel", 2.4f);
            EditorUtil.SetFloat(press, "holdUp", 1.0f);
            EditorUtil.SetFloat(press, "holdDown", 0.4f);
            EditorUtil.SetFloat(press, "phase", phase);

            return rig;
        }
    }
}
