using System.Collections.Generic;
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

            // Mid-gap, so it is cleared on the way across rather than before or
            // after. A long off phase because the runner cannot wait mid-air -
            // they have to leave the ground already knowing it will be dark.
            Laser(root, 16.5f, 0f, 5.5f, onDuration: 0.9f, offDuration: 1.5f, offset: 0f);

            Coins(root, 14f, 3.4f, 5, 1.2f, 1.2f);
            return root;
        }

        /// <summary>
        /// The wrecking ball swings across a gap instead of along a roof. Same
        /// ball, entirely different question - a mistimed jump has nowhere to
        /// land rather than just costing the run.
        /// </summary>
        private static GameObject CraneGap()
        {
            GameObject root = NewChunk("Chunk_CraneGap", 34f, 0f, 0f, 5,
                                       ChunkSkill.Jump | ChunkSkill.Gap | ChunkSkill.Timing, "crane");
            Ground(root, 0f, 0f, 12f);
            Ground(root, 19f, 0f, 15f);

            Crane(root, 15.5f, 9f, phase: 0f);

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

            // Half a cycle apart, so one is always rising as the other falls.
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

            // Six units past the ledge: far enough that the climb finishes before
            // the beam matters, close enough that it was visible during the climb.
            Laser(root, 23f, 4f, 4.5f, onDuration: 1.3f, offDuration: 1.4f, offset: 0.7f);

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

            // High enough that it only threatens the top of the platform's rise,
            // so the safe answer is to cross while the lift is low.
            Laser(root, 18.5f, 2.6f, 4f, onDuration: 1f, offDuration: 1.2f, offset: 0.4f);

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

        /// <summary>A pulsing beam standing on the surface at baseY.</summary>
        private static GameObject Laser(GameObject parent, float x, float baseY, float height,
                                        float onDuration, float offDuration, float offset)
        {
            GameObject beam = Box(parent, "Laser", x, baseY, 0.35f, height,
                                  GameLayers.Hazard, HazardTone, anchorBottom: true, trigger: true);
            beam.AddComponent<Hazard>();

            var gate = beam.AddComponent<LaserGate>();
            EditorUtil.SetFloat(gate, "startOffset", offset);
            EditorUtil.SetFloat(gate, "onDuration", onDuration);
            EditorUtil.SetFloat(gate, "offDuration", offDuration);

            return beam;
        }

        /// <summary>A crane whose ball sweeps the ground at x.</summary>
        private static GameObject Crane(GameObject parent, float x, float height, float phase)
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
        private static GameObject Press(GameObject parent, float x, float phase)
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
