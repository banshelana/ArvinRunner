using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Builds the library of obstacle chunks as prefabs.
    ///
    /// Each chunk is authored with its entry point at local (0, 0) - that is,
    /// the walkable surface at the left edge sits on the origin. The
    /// <see cref="LevelBuilder"/> relies on that when it lays them end to end.
    ///
    /// These are gameplay blockouts using placeholder boxes. Replace the
    /// sprites and the layouts still work.
    /// </summary>
    public static partial class ChunkFactory
    {
        private const string ChunkFolder = "Assets/Prefabs/Chunks";

        private static readonly Color Rooftop   = new Color(0.20f, 0.21f, 0.30f);
        private static readonly Color WallTone  = new Color(0.26f, 0.27f, 0.38f);
        private static readonly Color VaultTone = new Color(0.85f, 0.62f, 0.24f);
        private static readonly Color HazardTone = new Color(0.92f, 0.27f, 0.28f);
        private static readonly Color MetalTone = new Color(0.55f, 0.58f, 0.66f);
        private static readonly Color GlassTone = new Color(0.55f, 0.85f, 0.95f, 0.55f);

        private const float GroundThickness = 3f;

        public static LevelChunk[] CreateAll()
        {
            EditorUtil.EnsureFolder(ChunkFolder);

            var chunks = new List<LevelChunk>
            {
                Save(Flat()),
                Save(VaultBlock()),
                Save(VaultRow()),
                Save(SlideGate()),
                Save(Gap()),
                Save(WideGap()),
                Save(SpikeRun()),
                Save(StepsDown()),
                Save(CollapsingRun()),
                Save(LaserCorridor()),
                Save(WreckingBall()),
                Save(WallClimb()),
                Save(TrampolineLeap()),
                Save(CrusherAlley()),
                Save(GlassRun()),
                Save(GustGap()),
                Save(MovingPlatforms())
            };

            // Layouts built from the hand-made prop art.
            chunks.AddRange(CreatePropChunks());

            // Obstacles that accept either a jump or a slide.
            chunks.AddRange(CreateRouteChunks());

            // Street level: the vehicles and site props.
            chunks.AddRange(CreateVehicleChunks());

            // Obstacles that come the other way.
            chunks.AddRange(CreateMovingChunks());

            return chunks.ToArray();
        }

        // ================================================================= //
        // The chunks
        // ================================================================= //

        /// <summary>Plain rooftop. Breathing room between hard chunks.</summary>
        private static GameObject Flat()
        {
            GameObject root = NewChunk("Chunk_Flat", 22f, 0f, 0f, 1, ChunkSkill.None, "flat");
            Ground(root, 0f, 0f, 22f);
            Coins(root, 8f, 2f, 5, 1.4f, 1.2f);
            return root;
        }

        /// <summary>A crate low enough to vault. Teaches the contextual vault.</summary>
        private static GameObject VaultBlock()
        {
            GameObject root = NewChunk("Chunk_VaultBlock", 24f, 0f, 0f, 1, ChunkSkill.Vault, "vault");
            Ground(root, 0f, 0f, 24f);
            Box(root, "VaultCrate", 11f, 0f, 2.4f, 1.2f, GameLayers.Vaultable, VaultTone);
            Coins(root, 10f, 2.4f, 3, 1.2f, 0.5f);
            return root;
        }

        /// <summary>Three crates in a row - vault, land, vault again.</summary>
        private static GameObject VaultRow()
        {
            GameObject root = NewChunk("Chunk_VaultRow", 30f, 0f, 0f, 2, ChunkSkill.Vault | ChunkSkill.Jump, "vault");
            Ground(root, 0f, 0f, 30f);

            for (int i = 0; i < 3; i++)
                Box(root, "VaultCrate" + i, 8f + i * 6f, 0f, 2.2f, 1.0f + i * 0.2f, GameLayers.Vaultable, VaultTone);

            return root;
        }

        /// <summary>A low overhang. The only way through is a slide.</summary>
        private static GameObject SlideGate()
        {
            GameObject root = NewChunk("Chunk_SlideGate", 26f, 0f, 0f, 2, ChunkSkill.Slide, "slide");
            Ground(root, 0f, 0f, 26f);

            // Ceiling at 1.4 - below standing height (1.75), above sliding height (0.8).
            Box(root, "Overhang", 11f, 1.4f, 5f, 2.6f, GameLayers.Ground, WallTone, anchorBottom: true);
            Coins(root, 12f, 0.6f, 4, 1.1f, 0f);
            return root;
        }

        /// <summary>A single-jump gap.</summary>
        private static GameObject Gap()
        {
            GameObject root = NewChunk("Chunk_Gap", 26f, 0f, 0f, 2, ChunkSkill.Jump | ChunkSkill.Gap, "gap");
            Ground(root, 0f, 0f, 10f);
            Ground(root, 15f, 0f, 11f);
            Coins(root, 11f, 2.5f, 4, 1.1f, 1.0f);
            return root;
        }

        /// <summary>Too wide for one jump. The air jump is mandatory.</summary>
        private static GameObject WideGap()
        {
            GameObject root = NewChunk("Chunk_WideGap", 32f, 0f, 0f, 3,
                                       ChunkSkill.DoubleJump | ChunkSkill.Gap, "gap");
            Ground(root, 0f, 0f, 10f);
            Ground(root, 18f, 0f, 14f);
            Coins(root, 11.5f, 3.2f, 6, 1.2f, 1.6f);
            return root;
        }

        /// <summary>Spikes bedded into the rooftop.</summary>
        private static GameObject SpikeRun()
        {
            GameObject root = NewChunk("Chunk_Spikes", 28f, 0f, 0f, 2, ChunkSkill.Jump, "spike");
            Ground(root, 0f, 0f, 28f);

            Spikes(root, 10f, 0f, 4f);
            Spikes(root, 18f, 0f, 3f);
            return root;
        }

        /// <summary>Descends to a lower rooftop. Landing fast forces a roll.</summary>
        private static GameObject StepsDown()
        {
            GameObject root = NewChunk("Chunk_StepsDown", 26f, 0f, -4f, 2, ChunkSkill.Jump, "steps");
            Ground(root, 0f, 0f, 9f);
            Ground(root, 11f, -1.5f, 6f);
            Ground(root, 19f, -4f, 7f);
            Coins(root, 10f, 1.5f, 3, 1.2f, 0.6f);
            return root;
        }

        /// <summary>Rooftop panels that give way. Keep moving.</summary>
        private static GameObject CollapsingRun()
        {
            GameObject root = NewChunk("Chunk_Collapsing", 32f, 0f, 0f, 3,
                                       ChunkSkill.Timing | ChunkSkill.Gap, "collapse");
            Ground(root, 0f, 0f, 9f);

            for (int i = 0; i < 3; i++)
            {
                GameObject panel = Box(root, "Panel" + i, 10f + i * 4.5f, 0f, 3.2f, 0.6f,
                                       GameLayers.Ground, new Color(0.55f, 0.35f, 0.30f), anchorBottom: false);
                panel.AddComponent<CollapsingPlatform>();
            }

            Ground(root, 23f, 0f, 9f);
            return root;
        }

        /// <summary>Pulsing beams. Read the rhythm before you commit.</summary>
        private static GameObject LaserCorridor()
        {
            GameObject root = NewChunk("Chunk_Lasers", 30f, 0f, 0f, 3, ChunkSkill.Timing, "laser");
            Ground(root, 0f, 0f, 30f);

            float[] positions = { 9f, 16f, 23f };
            float[] offsets = { 0f, 0.8f, 1.6f };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject beam = Box(root, "Laser" + i, positions[i], 0f, 0.35f, 4.5f,
                                      GameLayers.Hazard, HazardTone, anchorBottom: true, trigger: true);
                beam.AddComponent<Hazard>();

                var gate = beam.AddComponent<LaserGate>();
                EditorUtil.SetFloat(gate, "startOffset", offsets[i]);
                EditorUtil.SetFloat(gate, "onDuration", 1.1f);
                EditorUtil.SetFloat(gate, "offDuration", 1.0f);
            }

            return root;
        }

        /// <summary>A crane swinging a wrecking ball across the roof.</summary>
        private static GameObject WreckingBall()
        {
            GameObject root = NewChunk("Chunk_WreckingBall", 30f, 0f, 0f, 4,
                                       ChunkSkill.Timing | ChunkSkill.Jump, "crane");
            Ground(root, 0f, 0f, 30f);

            // The pivot rotates; the arm and ball hang beneath it.
            var pivot = new GameObject("CranePivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(15f, 9f, 0f);
            pivot.AddComponent<SwingingCrane>();

            GameObject arm = Box(pivot, "Arm", 0f, -3.25f, 0.25f, 6.5f, GameLayers.Ground, MetalTone,
                                 anchorBottom: false, centred: true, collider: false);
            arm.transform.localPosition = new Vector3(0f, -3.25f, 0f);

            GameObject ball = Box(pivot, "Ball", 0f, -7f, 2.2f, 2.2f, GameLayers.Hazard, HazardTone,
                                  anchorBottom: false, centred: true, trigger: true);
            ball.transform.localPosition = new Vector3(0f, -7f, 0f);
            ball.AddComponent<Hazard>();

            return root;
        }

        /// <summary>A tower face: jump, wall run up it, grab the ledge, climb out.</summary>
        private static GameObject WallClimb()
        {
            GameObject root = NewChunk("Chunk_WallClimb", 28f, 0f, 4f, 4,
                                       ChunkSkill.WallRun | ChunkSkill.LedgeGrab, "wall");
            Ground(root, 0f, 0f, 13f);

            // The face itself. On the Wall layer so the sensors offer a wall run.
            Box(root, "TowerFace", 13f, 4f, 2f, 7f, GameLayers.Wall, WallTone, anchorBottom: false);

            Ground(root, 15f, 4f, 13f);
            Coins(root, 14f, 5.5f, 3, 1.2f, 0.4f);
            return root;
        }

        /// <summary>An awning that launches you over a long spike bed.</summary>
        private static GameObject TrampolineLeap()
        {
            GameObject root = NewChunk("Chunk_Trampoline", 30f, 0f, 0f, 3,
                                       ChunkSkill.Jump | ChunkSkill.Timing, "spring");
            Ground(root, 0f, 0f, 30f);

            // Anchored bottom so the pad sits on the roof. Anchoring it by the
            // top buried it inside the ground slab, where nothing could hit it.
            GameObject pad = Box(root, "Awning", 8f, 0f, 2.6f, 0.5f, GameLayers.Ground,
                                 new Color(0.35f, 0.75f, 0.55f), anchorBottom: true);
            var trampoline = pad.AddComponent<Trampoline>();
            EditorUtil.SetFloat(trampoline, "launchSpeed", 19f);
            EditorUtil.SetObject(trampoline, "squashVisual", pad.transform);

            Spikes(root, 12f, 0f, 8f);
            Coins(root, 13f, 4.5f, 6, 1.2f, 1.8f);
            return root;
        }

        /// <summary>An industrial press. Slide through the gap while it is up.</summary>
        private static GameObject CrusherAlley()
        {
            GameObject root = NewChunk("Chunk_Crusher", 28f, 0f, 0f, 4,
                                       ChunkSkill.Slide | ChunkSkill.Timing, "press");
            Ground(root, 0f, 0f, 28f);

            var rig = new GameObject("Press");
            rig.transform.SetParent(root.transform, false);
            rig.transform.localPosition = new Vector3(13f, 0f, 0f);

            Box(rig, "Frame", 0f, 6.4f, 4.5f, 0.6f, GameLayers.Ground, MetalTone,
                anchorBottom: false, centred: true, collider: false);

            GameObject head = Box(rig, "Head", 0f, 4.6f, 3.4f, 2.2f, GameLayers.Hazard, HazardTone,
                                  anchorBottom: false, centred: true, trigger: true);
            head.transform.localPosition = new Vector3(0f, 4.6f, 0f);
            head.AddComponent<Hazard>();

            var press = rig.AddComponent<CrusherPress>();
            EditorUtil.SetObject(press, "head", head.transform);
            EditorUtil.SetFloat(press, "travel", 2.4f);
            EditorUtil.SetFloat(press, "holdUp", 1.0f);
            EditorUtil.SetFloat(press, "holdDown", 0.4f);

            return root;
        }

        /// <summary>Glass panels. Hit them at full speed and you go straight through.</summary>
        private static GameObject GlassRun()
        {
            GameObject root = NewChunk("Chunk_Glass", 28f, 0f, 0f, 3,
                                       ChunkSkill.Jump | ChunkSkill.Slide, "glass");
            Ground(root, 0f, 0f, 28f);

            for (int i = 0; i < 3; i++)
            {
                // Vaultable layer keeps these out of the wall-crash check - they
                // are meant to be smashed, not climbed.
                GameObject pane = Box(root, "Glass" + i, 9f + i * 6f, 0f, 0.4f, 3.2f,
                                      GameLayers.Vaultable, GlassTone, anchorBottom: true);
                var glass = pane.AddComponent<BreakableGlass>();
                EditorUtil.SetFloat(glass, "breakSpeed", 7f);
            }

            return root;
        }

        /// <summary>Crosswind between towers. The gust shortens your jump.</summary>
        private static GameObject GustGap()
        {
            GameObject root = NewChunk("Chunk_Gust", 32f, 0f, 0f, 5,
                                       ChunkSkill.Gap | ChunkSkill.DoubleJump, "gust");
            Ground(root, 0f, 0f, 11f);
            Ground(root, 18f, 0f, 14f);

            GameObject zone = Box(root, "GustZone", 10f, 0f, 9f, 9f, GameLayers.Hazard,
                                  new Color(0.5f, 0.7f, 1f, 0.10f), anchorBottom: true, trigger: true);
            var gust = zone.AddComponent<GustZone>();
            EditorUtil.SetVector2(gust, "force", new Vector2(-9f, 1.5f));

            return root;
        }

        /// <summary>Platforms rising and falling over a drop.</summary>
        private static GameObject MovingPlatforms()
        {
            GameObject root = NewChunk("Chunk_MovingPlatforms", 32f, 0f, 0f, 3,
                                       ChunkSkill.Timing | ChunkSkill.Gap, "moving");
            Ground(root, 0f, 0f, 9f);

            for (int i = 0; i < 2; i++)
            {
                GameObject platform = Box(root, "Lift" + i, 11f + i * 7f, 0f, 3.5f, 0.7f,
                                          GameLayers.Ground, new Color(0.45f, 0.5f, 0.7f), anchorBottom: false);
                var mover = platform.AddComponent<MovingPlatform>();
                EditorUtil.SetVector2(mover, "travel", new Vector2(0f, i == 0 ? 3.5f : -2.5f));
                EditorUtil.SetFloat(mover, "speed", 1.8f + i * 0.4f);
                EditorUtil.SetFloat(mover, "phase", i * 0.5f);
            }

            Ground(root, 24f, 0f, 8f);
            return root;
        }

        // ================================================================= //
        // Building blocks
        // ================================================================= //

        private static GameObject NewChunk(string name, float length, float entryHeight, float exitHeight,
                                           int difficulty, ChunkSkill skills, string variantTag)
        {
            var root = new GameObject(name);
            var chunk = root.AddComponent<LevelChunk>();

            chunk.length = length;
            chunk.entryHeight = entryHeight;
            chunk.exitHeight = exitHeight;
            chunk.difficulty = difficulty;
            chunk.skills = skills;
            chunk.variantTag = variantTag;

            return root;
        }

        /// <summary>A slab of rooftop whose walkable surface sits at topY.</summary>
        private static GameObject Ground(GameObject parent, float leftX, float topY, float width)
        {
            return Box(parent, "Ground", leftX, topY, width, GroundThickness,
                       GameLayers.Ground, Rooftop, anchorBottom: false);
        }

        /// <summary>
        /// A rectangle. anchorBottom puts its underside at y; otherwise its top
        /// surface sits at y. centred treats x as the centre rather than the
        /// left edge.
        /// </summary>
        private static GameObject Box(GameObject parent, string name, float x, float y,
                                      float width, float height, int layer, Color colour,
                                      bool anchorBottom = false, bool centred = false,
                                      bool trigger = false, bool collider = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.layer = layer;

            float centreX = centred ? x : x + width * 0.5f;
            float centreY = anchorBottom ? y + height * 0.5f : y - height * 0.5f;
            go.transform.localPosition = new Vector3(centreX, centreY, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PlaceholderArt.Load("box");
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = new Vector2(width, height);
            renderer.color = colour;
            renderer.sortingOrder = layer == GameLayers.Ground ? 5 : 10;

            if (collider)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(width, height);
                box.isTrigger = trigger;
            }

            return go;
        }

        private static void Spikes(GameObject parent, float leftX, float baseY, float width)
        {
            var go = new GameObject("Spikes");
            go.transform.SetParent(parent.transform, false);
            go.layer = GameLayers.Hazard;

            const float height = 0.75f;
            go.transform.localPosition = new Vector3(leftX + width * 0.5f, baseY + height * 0.5f, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PlaceholderArt.Load("spikes");
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = new Vector2(width, height);
            renderer.color = HazardTone;
            renderer.sortingOrder = 10;

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(width, height * 0.7f);
            box.isTrigger = true;

            go.AddComponent<Hazard>();
        }

        /// <summary>A gentle arc of pickups, which doubles as a hint at the intended line.</summary>
        private static void Coins(GameObject parent, float startX, float baseY, int count,
                                  float spacing, float arcHeight)
        {
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Coin" + i);
                go.transform.SetParent(parent.transform, false);
                go.layer = GameLayers.Collectible;

                float t = count > 1 ? i / (float)(count - 1) : 0.5f;
                float y = baseY + Mathf.Sin(t * Mathf.PI) * arcHeight;
                go.transform.localPosition = new Vector3(startX + i * spacing, y, 0f);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = PlaceholderArt.Load("coin");
                renderer.color = new Color(1f, 0.85f, 0.35f);
                renderer.sortingOrder = 8;

                var circle = go.AddComponent<CircleCollider2D>();
                circle.radius = 0.45f;
                circle.isTrigger = true;

                go.AddComponent<Collectible>();
            }
        }

        private static LevelChunk Save(GameObject root)
        {
            string path = $"{ChunkFolder}/{root.name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<LevelChunk>();
        }
    }
}
