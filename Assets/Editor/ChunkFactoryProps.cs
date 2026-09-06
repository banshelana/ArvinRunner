using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// Chunks built from the hand-made prop art rather than placeholder boxes.
    ///
    /// Each prop is described once - which sprite, which layer, and the boxes
    /// that approximate its silhouette - then dropped into layouts. If a sprite
    /// has not been imported yet the prop falls back to a tinted box of the same
    /// size, so these chunks always build.
    /// </summary>
    public static partial class ChunkFactory
    {
        /// <summary>
        /// A collider box in normalised sprite space: (0,0) is the prop's
        /// bottom-left, (1,1) its top-right.
        /// </summary>
        private struct PropBox
        {
            public float X, Y, Width, Height;

            public PropBox(float x, float y, float width, float height)
            {
                X = x; Y = y; Width = width; Height = height;
            }
        }

        private struct PropSpec
        {
            public string Sprite;
            public int Layer;
            public PropBox[] Boxes;
            /// <summary>Fallback size in world units if the sprite is missing.</summary>
            public Vector2 FallbackSize;
            public Color FallbackTint;
        }

        // Everything solid but climbable sits on Vaultable rather than Ground or
        // Wall. That keeps the wall probe off it, so jumping near a skip does not
        // start a wall run.
        private static readonly Dictionary<string, PropSpec> Props = new Dictionary<string, PropSpec>
        {
            ["dumpster"] = new PropSpec
            {
                Sprite = "dumpster", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.03f, 0f, 0.94f, 1f) },
                FallbackSize = new Vector2(3.9f, 1.5f),
                FallbackTint = new Color(0.15f, 0.35f, 0.75f)
            },

            // A pyramid, so three steps: low shoulder, tall centre, mid shoulder.
            ["crates"] = new PropSpec
            {
                Sprite = "crates", Layer = GameLayers.Vaultable,
                Boxes = new[]
                {
                    new PropBox(0.00f, 0f, 0.36f, 0.36f),
                    new PropBox(0.36f, 0f, 0.38f, 1.00f),
                    new PropBox(0.74f, 0f, 0.26f, 0.60f)
                },
                FallbackSize = new Vector2(3.7f, 3.2f),
                FallbackTint = new Color(0.72f, 0.52f, 0.28f)
            },

            ["cone_large"] = new PropSpec
            {
                Sprite = "cone_large", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.18f, 0f, 0.64f, 0.92f) },
                FallbackSize = new Vector2(0.8f, 1.0f),
                FallbackTint = new Color(0.95f, 0.45f, 0.15f)
            },

            ["cone_small"] = new PropSpec
            {
                Sprite = "cone_small", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.18f, 0f, 0.64f, 0.92f) },
                FallbackSize = new Vector2(0.6f, 0.7f),
                FallbackTint = new Color(0.95f, 0.45f, 0.15f)
            },

            ["barricade"] = new PropSpec
            {
                Sprite = "barricade", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.04f, 0f, 0.92f, 1f) },
                FallbackSize = new Vector2(2.0f, 0.8f),
                FallbackTint = new Color(0.95f, 0.45f, 0.15f)
            },

            ["barrier"] = new PropSpec
            {
                Sprite = "barrier", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.03f, 0f, 0.94f, 1f) },
                FallbackSize = new Vector2(2.8f, 1.05f),
                FallbackTint = new Color(0.80f, 0.80f, 0.78f)
            },

            ["acunit"] = new PropSpec
            {
                Sprite = "acunit", Layer = GameLayers.Vaultable,
                Boxes = new[] { new PropBox(0.03f, 0f, 0.94f, 1f) },
                FallbackSize = new Vector2(2.6f, 1.4f),
                FallbackTint = new Color(0.55f, 0.58f, 0.66f)
            }
        };

        // ================================================================= //
        // Chunks
        // ================================================================= //

        private static List<LevelChunk> CreatePropChunks()
        {
            return new List<LevelChunk>
            {
                Save(SkipVault()),
                Save(ConeSlalom()),
                Save(BarrierRhythm()),
                Save(CondenserRow()),
                Save(CrateStack()),
                Save(StreetWorks())
            };
        }

        /// <summary>A single skip. Low enough to vault straight onto the lid.</summary>
        private static GameObject SkipVault()
        {
            GameObject root = NewChunk("Chunk_Skip", 26f, 0f, 0f, 2, ChunkSkill.Vault, "skip");
            Ground(root, 0f, 0f, 26f);
            Prop(root, "dumpster", 11f, 0f);
            Coins(root, 11.5f, 2.4f, 4, 1.1f, 0.6f);
            return root;
        }

        /// <summary>Cones and a barricade at an uneven rhythm.</summary>
        private static GameObject ConeSlalom()
        {
            GameObject root = NewChunk("Chunk_Cones", 30f, 0f, 0f, 2,
                                       ChunkSkill.Jump | ChunkSkill.Vault, "cones");
            Ground(root, 0f, 0f, 30f);

            Prop(root, "cone_large", 8f, 0f);
            Prop(root, "cone_small", 11.5f, 0f);
            Prop(root, "barricade", 15f, 0f);
            Prop(root, "cone_small", 20f, 0f);
            Prop(root, "cone_large", 22.5f, 0f);

            Coins(root, 16f, 2.2f, 3, 1.1f, 0.5f);
            return root;
        }

        /// <summary>Three barriers on an even beat - pure jump timing.</summary>
        private static GameObject BarrierRhythm()
        {
            GameObject root = NewChunk("Chunk_Barriers", 32f, 0f, 0f, 2, ChunkSkill.Jump, "barrier");
            Ground(root, 0f, 0f, 32f);

            for (int i = 0; i < 3; i++)
            {
                Prop(root, "barrier", 8f + i * 7f, 0f);
                Coins(root, 8.6f + i * 7f, 2.2f, 2, 1.1f, 0.3f);
            }

            return root;
        }

        /// <summary>Rooftop condenser units, the last one across a gap.</summary>
        private static GameObject CondenserRow()
        {
            GameObject root = NewChunk("Chunk_Condensers", 32f, 0f, 0f, 3,
                                       ChunkSkill.Vault | ChunkSkill.Gap, "acunit");
            Ground(root, 0f, 0f, 13f);
            Prop(root, "acunit", 8f, 0f);

            Ground(root, 17f, 0f, 15f);
            Prop(root, "acunit", 19f, 0f);
            Prop(root, "acunit", 25f, 0f);

            Coins(root, 13.5f, 3f, 4, 1.1f, 1.2f);
            return root;
        }

        /// <summary>
        /// A crate pyramid taller than a single jump. The low shoulder is the
        /// way up: hop on, jump the centre, drop off the far side.
        /// </summary>
        private static GameObject CrateStack()
        {
            GameObject root = NewChunk("Chunk_Crates", 30f, 0f, 0f, 4,
                                       ChunkSkill.Jump | ChunkSkill.DoubleJump, "crates");
            Ground(root, 0f, 0f, 30f);
            Prop(root, "crates", 11f, 0f);
            Coins(root, 12.2f, 3.8f, 4, 1.1f, 0.6f);
            return root;
        }

        /// <summary>Street works: cones warn you, then the barrier and the skip.</summary>
        private static GameObject StreetWorks()
        {
            GameObject root = NewChunk("Chunk_StreetWorks", 36f, 0f, 0f, 3,
                                       ChunkSkill.Vault | ChunkSkill.Jump, "works");
            Ground(root, 0f, 0f, 36f);

            Prop(root, "cone_small", 7f, 0f);
            Prop(root, "cone_large", 9f, 0f);
            Prop(root, "barrier", 13f, 0f);
            Prop(root, "dumpster", 20f, 0f);
            Prop(root, "barricade", 28f, 0f);

            Coins(root, 20.5f, 2.4f, 5, 1.1f, 0.7f);
            return root;
        }

        // ================================================================= //
        // Placement
        // ================================================================= //

        /// <summary>
        /// Drops a prop with its base at (leftX, groundY). Sprites are imported
        /// with a bottom-centre pivot and an exact world size, so there is no
        /// scaling here - the art defines how big the obstacle is.
        /// </summary>
        private static GameObject Prop(GameObject parent, string propName, float leftX, float groundY)
        {
            if (!Props.TryGetValue(propName, out PropSpec spec))
            {
                Debug.LogWarning($"[ArvinRunner] Unknown prop '{propName}'.");
                return null;
            }

            Sprite sprite = ArtImport.Load(spec.Sprite);
            Vector2 size = sprite != null ? (Vector2)sprite.bounds.size : spec.FallbackSize;

            // The root sits on the ground; the sprite hangs off a child. That
            // way the fallback box, which has a centred pivot, can be nudged up
            // without dragging the colliders with it.
            var go = new GameObject(propName);
            go.transform.SetParent(parent.transform, false);
            go.layer = spec.Layer;
            go.transform.localPosition = new Vector3(leftX + size.x * 0.5f, groundY, 0f);

            var visual = new GameObject("Visual") { layer = spec.Layer };
            visual.transform.SetParent(go.transform, false);

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;

            if (sprite != null)
            {
                renderer.sprite = sprite;   // bottom-centre pivot, already sized
            }
            else
            {
                // No art yet - stand in a box of the right size so the layout
                // still plays.
                renderer.sprite = PlaceholderArt.Load("box");
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = size;
                renderer.color = spec.FallbackTint;
                visual.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            }

            AddPropColliders(go, spec, size);
            return go;
        }

        /// <summary>
        /// Maps the normalised boxes onto the prop. The root origin is at the
        /// base, horizontally centred, so x = 0.5 is the middle and y = 0 the
        /// ground line.
        /// </summary>
        private static void AddPropColliders(GameObject go, PropSpec spec, Vector2 size)
        {
            foreach (PropBox definition in spec.Boxes)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(definition.Width * size.x, definition.Height * size.y);
                box.offset = new Vector2(
                    (definition.X + definition.Width * 0.5f - 0.5f) * size.x,
                    (definition.Y + definition.Height * 0.5f) * size.y);
            }
        }
    }
}
