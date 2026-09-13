using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Chunks built around obstacles that travel the other way down the level.
    ///
    /// Everything else in the library is passed at the runner's own speed. These
    /// close at the sum of two, which changes what the layout has to guarantee:
    ///
    ///  * <b>The encounter has to land on open ground.</b> Where the two meet is
    ///    decided by arithmetic, not by where the obstacle was placed - and the
    ///    runner's speed ramps from 9 to 11 over a level, so it is a window
    ///    rather than a point. Each chunk below works that window out and keeps
    ///    it clear of anything else.
    ///
    ///  * <b>Nothing may be seen standing still and then start.</b> The camera
    ///    shows about 18 units ahead, so every activationDistance here is well
    ///    past that - the obstacle is already moving before it can be seen.
    ///
    /// Both chunks are flat and empty besides their one event, deliberately. A
    /// head-on obstacle is enough to be reading at once.
    /// </summary>
    public static partial class ChunkFactory
    {
        private static List<LevelChunk> CreateMovingChunks()
        {
            return new List<LevelChunk>
            {
                Save(Oncoming()),
                Save(AirStrike())
            };
        }

        /// <summary>
        /// A motorbike coming the other way. Jump it - there is no second answer,
        /// which is why this one is otherwise empty.
        ///
        /// Where they meet: the bike wakes at 30 units and the two close at 18
        /// (9 + 9), so the runner covers 15 of those 30 and they meet around x=15.
        /// At the 15 the run speed ramps to, they close at 24 and the runner
        /// covers 18.75, meeting at x=19. The whole 13-21 window is bare ground.
        /// </summary>
        private static GameObject Oncoming()
        {
            GameObject root = NewChunk("Chunk_Oncoming", 36f, 0f, 0f, 3, ChunkSkill.Jump, "oncoming");
            Ground(root, 0f, 0f, 36f);

            Motorbike(root, 30f, 0f);

            // Laid across the whole 15-19 meeting window rather than at a point,
            // because that window moves with the run speed. Wherever the bike is
            // actually met, the arc that clears it is the arc that collects.
            Coins(root, 15f, 2.6f, 5, 1.2f, 0.8f);
            return root;
        }

        /// <summary>
        /// A gunship crosses, marks the roof ahead of the runner and puts a
        /// missile into it. The aircraft is not the obstacle - the fire is.
        ///
        /// The timing solves itself: HelicopterStrike fires when the runner is
        /// one warning away from the target *at their current speed*, so the
        /// blast is always alight as they arrive whatever that speed is.
        ///
        /// The geometry does not solve itself, and the first numbers here were
        /// wrong. Because the fire is triggered on the runner's time-to-target,
        /// a faster runner triggers it from further back - and the helicopter,
        /// drifting at a fixed speed, has had less time to close. At a run speed
        /// of 15 the original layout fired it from 23 units ahead of the runner,
        /// five units outside what the camera shows, so the shot happened off
        /// screen and only the marker ever appeared.
        ///
        /// These numbers are solved against the whole 9-to-15 speed range
        /// instead. The helicopter starts at 27 and drifts at 1.2, which puts it
        /// 8.7 units ahead of a slow runner when it fires and 15.5 ahead of a
        /// fast one - both inside the ~18 the camera shows - and still right of
        /// the target either way, so the missile goes down and forward as the art
        /// draws it rather than backwards over its own shoulder.
        ///
        /// Slowing it down tightens the fast end rather than the slow one, which
        /// is the counter-intuitive part: the less ground the aircraft covers
        /// before the trigger, the further ahead it still is when it fires. 1.2
        /// is about as slow as it goes before the shot starts leaving the screen
        /// at top speed.
        /// </summary>
        private static GameObject AirStrike()
        {
            GameObject root = NewChunk("Chunk_AirStrike", 40f, 0f, 0f, 4,
                                       ChunkSkill.Jump | ChunkSkill.Timing, "airstrike");
            Ground(root, 0f, 0f, 40f);

            GameObject strike = Strike(root, 24f, 0f);
            Helicopter(root, 27f, 6f, strike);

            // Over the strike point, so the line that clears the fire is the
            // line that collects.
            Coins(root, 22f, 2.8f, 5, 1.2f, 1.1f);
            return root;
        }

        // ================================================================= //
        // The obstacles
        // ================================================================= //

        /// <summary>
        /// Root carries the collider and the movement; the sprite hangs off a
        /// child. The frames change size as the rider leans and the exhaust
        /// trails, so a collider derived from the sprite would breathe with them
        /// - these are authored in world units instead and stay put.
        /// </summary>
        private static GameObject Motorbike(GameObject parent, float x, float groundY)
        {
            var go = new GameObject("Motorbike") { layer = GameLayers.Hazard };
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(x, groundY, 0f);

            // The bike and rider, not the exhaust plume drawn off the back.
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1.5f, 1.4f);
            box.offset = new Vector2(-0.1f, 0.7f);
            box.isTrigger = true;

            go.AddComponent<Hazard>();

            var travel = go.AddComponent<MovingObstacle>();
            EditorUtil.SetFloat(travel, "speed", 9f);
            EditorUtil.SetFloat(travel, "activationDistance", 30f);

            Frames(go, MovingObstacleImport.Frames("moto"), 16f,
                   fallbackSize: new Vector2(1.86f, 1.55f),
                   fallbackTint: new Color(0.20f, 0.20f, 0.22f));

            return go;
        }

        private static GameObject Helicopter(GameObject parent, float x, float flyHeight,
                                             GameObject strike)
        {
            var go = new GameObject("Helicopter") { layer = GameLayers.Hazard };
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(x, flyHeight, 0f);

            // Fuselage and tail boom, not the rotor disc. Measured off the art at
            // its 2.85 import height: the solid body runs up to about 2.0 above
            // the skids and 7.4 nose to tail, with the cabin forward of centre.
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(6.8f, 1.7f);
            box.offset = new Vector2(-0.3f, 1.0f);
            box.isTrigger = true;

            go.AddComponent<Hazard>();

            // Solved, not chosen - see the note on AirStrike. Slow enough that it
            // is still short of the target when a fast runner triggers the shot,
            // and still closing on a slow one, so the shot always starts inside
            // what the camera shows.
            var travel = go.AddComponent<MovingObstacle>();
            EditorUtil.SetFloat(travel, "speed", 1.2f);
            EditorUtil.SetFloat(travel, "activationDistance", 30f);

            // 24 frames of one flying loop: the rotor swaps between a spread blade
            // and an edge-on one every frame, and the body bobs through all 24. At
            // 24fps the bob takes a second and the blade flickers at 12 a second,
            // which reads as spinning rather than as two drawings taking turns.
            Sprite[] frames = MovingObstacleImport.Frames("helli");
            Frames(go, frames, 24f,
                   fallbackSize: new Vector2(7.4f, 2.85f),
                   fallbackTint: new Color(0.28f, 0.32f, 0.24f));

            // Under the front of the cabin, between the skids, where the missile
            // leaves from.
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(go.transform, false);
            muzzle.transform.localPosition = new Vector3(-1.2f, 0.5f, 0f);

            var gun = go.AddComponent<HelicopterStrike>();
            EditorUtil.SetObject(gun, "strike", strike.GetComponent<MissileStrike>());
            EditorUtil.SetObject(gun, "muzzle", muzzle.transform);

            // The frames are a flying loop with no shot drawn in them, so the loop
            // plays throughout and the strike flies its own missile. The old art
            // was a storyboard - fly, descend, launch, missile away, recover - and
            // switched to its firing frames here, holding the last one. On this
            // art that would stop the rotor dead the moment it fired.
            int last = Mathf.Max(0, frames.Length - 1);
            EditorUtil.SetInt(gun, "approachFirst", 0);
            EditorUtil.SetInt(gun, "approachLast", last);
            EditorUtil.SetInt(gun, "fireFirst", 0);
            EditorUtil.SetInt(gun, "fireLast", last);
            EditorUtil.SetBool(gun, "launchDrawn", false);

            return go;
        }

        /// <summary>
        /// The strike point, dormant until the helicopter sets it off. Sized so
        /// the marker the player reads and the fire that kills them are the same
        /// circle - a marker smaller than the blast would be a lie.
        /// </summary>
        private static GameObject Strike(GameObject parent, float x, float groundY)
        {
            const float radius = 1.4f;

            var go = new GameObject("MissileStrike") { layer = GameLayers.Hazard };
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(x, groundY, 0f);

            var circle = go.AddComponent<CircleCollider2D>();
            circle.radius = radius;
            circle.isTrigger = true;

            go.AddComponent<Hazard>();

            // The placeholder target is drawn 2.0 units across, so scaling it by
            // the radius brings it out at the lethal 2.8. It sits on the roof,
            // under the runner.
            const float markerSpriteSize = 2f;
            SpriteRenderer marker = StrikePart(go, "Marker", "target",
                                               new Color(1f, 0.35f, 0.15f), 6,
                                               radius * 2f / markerSpriteSize);

            // The fireball is 3.0 across and peaks at 1.15 of whatever scale it
            // is given, so 0.8 lands the bright core on the lethal circle.
            SpriteRenderer blast = StrikePart(go, "Blast", "blast",
                                              new Color(1f, 0.85f, 0.55f), 15, 0.8f);

            // The missile, flown by the strike from the helicopter's muzzle down to
            // the marker. The helicopter frames are a flying loop with no shot
            // drawn in them, so this is the only missile there is. The placeholder
            // is 0.9 units long; 1.4 brings it to 1.2, readable against a 7.4 unit
            // aircraft.
            SpriteRenderer missile = StrikePart(go, "Missile", "missile", Color.white, 14, 1.4f);

            var strike = go.AddComponent<MissileStrike>();
            EditorUtil.SetObject(strike, "marker", marker);
            EditorUtil.SetObject(strike, "blast", blast);
            EditorUtil.SetObject(strike, "missile", missile);
            EditorUtil.SetFloat(strike, "warnDuration", 0.75f);
            EditorUtil.SetFloat(strike, "blastDuration", 0.9f);
            EditorUtil.SetFloat(strike, "blastRadius", radius);

            return go;
        }

        private static SpriteRenderer StrikePart(GameObject parent, string name, string sprite,
                                                 Color tint, int sortingOrder, float scale)
        {
            var go = new GameObject(name) { layer = parent.layer };
            go.transform.SetParent(parent.transform, false);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PlaceholderArt.Load(sprite);
            renderer.color = tint;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = false;   // MissileStrike turns these on in sequence

            return renderer;
        }

        /// <summary>
        /// Hangs the animated sprite off a child and drives it. Falls back to a
        /// tinted box of the right size when the frames have not been imported,
        /// the same way the still props do.
        /// </summary>
        private static void Frames(GameObject parent, Sprite[] frames, float fps,
                                   Vector2 fallbackSize, Color fallbackTint)
        {
            var visual = new GameObject("Visual") { layer = parent.layer };
            visual.transform.SetParent(parent.transform, false);

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 12;

            if (frames != null && frames.Length > 0)
            {
                renderer.sprite = frames[0];

                var book = visual.AddComponent<SpriteFlipbook>();
                EditorUtil.SetObject(book, "target", renderer);
                EditorUtil.SetObjectArray(book, "frames", frames);
                EditorUtil.SetFloat(book, "fps", fps);
                EditorUtil.SetBool(book, "loop", true);
            }
            else
            {
                renderer.sprite = PlaceholderArt.Load("box");
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = fallbackSize;
                renderer.color = fallbackTint;
                visual.transform.localPosition = new Vector3(0f, fallbackSize.y * 0.5f, 0f);
            }
        }
    }
}
