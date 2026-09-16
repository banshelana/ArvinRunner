using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>
    /// People in the way. Obstacles that are someone rather than something, and
    /// react to the runner going over them.
    ///
    /// There are two of them, and a level that has a bench twice gives the second
    /// one to the other person - the same jump, but not the same joke twice.
    /// </summary>
    public static partial class ChunkFactory
    {
        /// <summary>
        /// Everything that differs between one person on a bench and another: their
        /// frames, and the order and pace the moment plays in. Frame lists are file
        /// numbers, first file being 1.
        /// </summary>
        private sealed class BystanderArt
        {
            public string Name;
            public string Folder;
            public int FrameCount;
            public Color FallbackTint;

            public int[] Idle;
            public float IdleFps;
            public int[] Startle;
            public float StartleFps;
            public int[] After;
            public float AfterFps;
        }

        /// <summary>
        /// The man reading his paper. Ordered by eye:
        ///
        ///   reading    1-6 and back, so the loop never jumps from 6 to 1
        ///   startle    7-10  the paper thrown up and scattering, arms flung out
        ///              17 18 the last pages coming down
        ///              14-16 a fist shaken after the runner
        ///              22-24 13  hands on his head
        ///   after      19-21 gripping the bench, getting his breath back
        ///
        /// 11 and 12 - catching the paper and reading on - are left out. He does
        /// not get over it that quickly.
        /// </summary>
        private static readonly BystanderArt NewspaperManArt = new BystanderArt
        {
            Name = "NewspaperMan",
            Folder = "newspaperMan",
            FrameCount = 24,
            FallbackTint = new Color(0.62f, 0.52f, 0.36f),
            Idle = new[] { 1, 2, 3, 4, 5, 6, 5, 4, 3, 2 },
            IdleFps = 5f,
            Startle = new[] { 7, 8, 9, 10, 17, 18, 14, 15, 16, 22, 23, 24, 13 },
            StartleFps = 10f,
            After = new[] { 19, 20, 21, 20 },
            AfterFps = 6f
        };

        /// <summary>
        /// The old woman with her cane. Her frames were ordered by measurement
        /// rather than by eye, because they do not agree with each other about
        /// where the cane is: every frame was pinned on its bench, scaled to the
        /// same bench width, and compared with every other, and the orders below
        /// are the ones that change the least from one drawing to the next.
        ///
        ///   sitting    1 5 3 4 2 6 - no step bigger than 15% of the silhouette
        ///   startle    11 10       the flinch, the cane leaving her hands
        ///              7 12 20 17 9 the cane away through the air
        ///              8           an arm out after it
        ///              14 15 16    the arm flung up
        ///              18 19       hands to her head
        ///   after      13 19 18 19 holding her head
        ///
        /// In file order the startle steps reach 52%; in this order the worst is
        /// 31%, and it tells the moment the right way round as well. The flinch is
        /// held a beat by doubling its first drawing, because the fright is the
        /// part the eye needs longest to take in.
        /// </summary>
        private static readonly BystanderArt OldWomanArt = new BystanderArt
        {
            Name = "OldWoman",
            Folder = "oldWoman",
            FrameCount = 20,
            FallbackTint = new Color(0.58f, 0.48f, 0.66f),
            Idle = new[] { 1, 5, 3, 4, 2, 6 },
            IdleFps = 3.5f,
            Startle = new[] { 11, 11, 10, 7, 12, 20, 17, 9, 8, 14, 15, 16, 18, 19 },
            StartleFps = 12f,
            After = new[] { 13, 19, 18, 19 },
            AfterFps = 5f
        };

        /// <summary>
        /// Top of the head above the bench feet, for both - see MovingObstacleImport.
        /// Over the 1.6 vault ceiling, so they are jumped and never vaulted onto.
        /// </summary>
        private const float BystanderHeight = 1.7f;

        /// <summary>
        /// True for the chunks made here. Like the war zone's, they stay out of the
        /// assembled levels' pool, so adding them does not reshuffle levels 4 and 5.
        /// </summary>
        public static bool IsBystander(LevelChunk chunk) => chunk != null && chunk.variantTag == "bench";

        private static List<LevelChunk> CreateBystanderChunks()
        {
            return new List<LevelChunk>
            {
                Save(ParkBench()),
                Save(BenchRow()),
                Save(Wreckers(oldWoman: true))
            };
        }

        /// <summary>
        /// One man on one bench, with open roof either side. A first jump that is
        /// also a joke, which is why it replaced the cones at the start of level 1.
        /// </summary>
        private static GameObject ParkBench()
        {
            GameObject root = NewChunk("Chunk_ParkBench", 28f, 0f, 0f, 1, ChunkSkill.Jump, "bench");
            Ground(root, 0f, 0f, 28f);

            NewspaperMan(root, 11f, 0f);
            Coins(root, 10.8f, 2.9f, 4, 1.0f, 0.6f);
            return root;
        }

        /// <summary>
        /// Two benches twelve units apart: the man, then the old woman. The jump
        /// over the first lands six to seven and a half units after take-off, so
        /// the runner is down and has a clean run at the second.
        /// </summary>
        private static GameObject BenchRow()
        {
            GameObject root = NewChunk("Chunk_BenchRow", 34f, 0f, 0f, 2, ChunkSkill.Jump, "bench");
            Ground(root, 0f, 0f, 34f);

            NewspaperMan(root, 8f, 0f);
            Coins(root, 7.8f, 2.9f, 4, 1.0f, 0.6f);

            OldWoman(root, 20f, 0f);
            Coins(root, 19.8f, 2.9f, 4, 1.0f, 0.6f);
            return root;
        }

        /// <summary>The man reading his paper - see NewspaperManArt. <paramref name="leftX"/> is the bench's left end.</summary>
        private static GameObject NewspaperMan(GameObject parent, float leftX, float groundY) =>
            Bystander(parent, leftX, groundY, NewspaperManArt);

        /// <summary>The old woman with her cane - see OldWomanArt. <paramref name="leftX"/> is the bench's left end.</summary>
        private static GameObject OldWoman(GameObject parent, float leftX, float groundY) =>
            Bystander(parent, leftX, groundY, OldWomanArt);

        /// <summary>
        /// Someone on a bench. Jump over their head and they jump out of their skin.
        ///
        /// Solid, on the Vaultable layer like the cars - so the wall probe stays off
        /// them - but at 1.7 over the 1.6 vault ceiling, so the vault never offers
        /// itself and the jump is the only way past. Running into them is a crash,
        /// as the fence is. The collider is only as wide as the person, not the
        /// bench: the bench ends are scenery, and the jump keeps the feet above 1.7
        /// for about 3.3 units clear of the body, which a 1.2 box leaves a
        /// comfortable margin inside.
        /// </summary>
        private static GameObject Bystander(GameObject parent, float leftX, float groundY, BystanderArt art)
        {
            Sprite[] drawn = MovingObstacleImport.Frames(art.Folder);

            // The first frame's crop is the bench end to end.
            float width = drawn.Length > 0 && drawn[0] != null ? drawn[0].bounds.size.x : 2.3f;

            var go = new GameObject(art.Name) { layer = GameLayers.Vaultable };
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(leftX + width * 0.5f, groundY, 0f);

            const float bodyWidth = 1.2f;
            float bodyHeight = BystanderHeight - 0.05f;

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(bodyWidth, bodyHeight);
            box.offset = new Vector2(0f, bodyHeight * 0.5f);

            var visual = new GameObject("Visual") { layer = go.layer };
            visual.transform.SetParent(go.transform, false);

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;

            if (drawn.Length < art.FrameCount)
            {
                // Frames missing: a box the size of the person so the layout still plays.
                renderer.sprite = PlaceholderArt.Load("box");
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = new Vector2(bodyWidth, BystanderHeight);
                renderer.color = art.FallbackTint;
                visual.transform.localPosition = new Vector3(0f, BystanderHeight * 0.5f, 0f);

                Debug.LogWarning($"[ArvinRunner] Expected {art.FrameCount} frames in " +
                                 $"{MovingObstacleImport.RootFolder}/{art.Folder} and found {drawn.Length}, " +
                                 $"so {art.Name} is a box.");
                return go;
            }

            renderer.sprite = drawn[art.Idle[0] - 1];

            var person = go.AddComponent<StartledBystander>();
            EditorUtil.SetObject(person, "body", renderer);
            EditorUtil.SetObjectArray(person, "idle", ByFileNumber(drawn, art.Idle));
            EditorUtil.SetObjectArray(person, "startle", ByFileNumber(drawn, art.Startle));
            EditorUtil.SetObjectArray(person, "after", ByFileNumber(drawn, art.After));
            EditorUtil.SetFloat(person, "idleFps", art.IdleFps);
            EditorUtil.SetFloat(person, "startleFps", art.StartleFps);
            EditorUtil.SetFloat(person, "afterFps", art.AfterFps);
            EditorUtil.SetFloat(person, "headHeight", BystanderHeight);

            return go;
        }

        /// <summary>Frames picked by the numbers in their file names, first file being 1.</summary>
        private static Sprite[] ByFileNumber(Sprite[] frames, params int[] numbers)
        {
            var picked = new List<Sprite>(numbers.Length);
            foreach (int number in numbers)
                if (number >= 1 && number <= frames.Length && frames[number - 1] != null)
                    picked.Add(frames[number - 1]);

            return picked.ToArray();
        }
    }
}
