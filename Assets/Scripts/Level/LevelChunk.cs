using UnityEngine;

namespace ArvinRunner
{
    /// <summary>What the player has to do to get through a chunk. Used to pick a
    /// varied mix when a level is generated, and to avoid two identical
    /// challenges back to back.</summary>
    [System.Flags]
    public enum ChunkSkill
    {
        None     = 0,
        Jump     = 1 << 0,
        DoubleJump = 1 << 1,
        Slide    = 1 << 2,
        Vault    = 1 << 3,
        WallRun  = 1 << 4,
        LedgeGrab = 1 << 5,
        Timing   = 1 << 6,   // moving parts, lasers, presses
        Gap      = 1 << 7    // a hole you must clear
    }

    /// <summary>
    /// One authored piece of level. Chunks are laid end to end by the
    /// <see cref="LevelBuilder"/>, so each one only needs to describe its own
    /// footprint: how long it is, and how high the ground sits at each end.
    ///
    /// Build the prefab with its entry point at local (0, 0).
    /// </summary>
    public class LevelChunk : MonoBehaviour
    {
        [Header("Footprint")]
        [Tooltip("Horizontal length in world units. The next chunk starts here.")]
        public float length = 20f;

        [Tooltip("Ground height at the left edge, relative to the chunk origin.")]
        public float entryHeight;

        [Tooltip("Ground height at the right edge. Lets chunks step up or down.")]
        public float exitHeight;

        [Header("Design")]
        [Tooltip("1 = warm-up, 5 = brutal. Drives which chunks a level picks.")]
        [Range(1, 5)] public int difficulty = 1;

        [Tooltip("Which moves this chunk demands.")]
        public ChunkSkill skills = ChunkSkill.Jump;

        [Tooltip("Chunks with the same tag will not be placed back to back.")]
        public string variantTag = "";

        [Header("Optional")]
        [Tooltip("Extra clearance to add after this chunk, for breathing room.")]
        public float trailingGap;

        /// <summary>World-space X where the next chunk should begin.</summary>
        public Vector3 ExitPoint => transform.position + new Vector3(length + trailingGap, exitHeight - entryHeight, 0f);

        private void OnDrawGizmos()
        {
            Vector3 origin = transform.position;
            Vector3 entry = origin + Vector3.up * entryHeight;
            Vector3 exit = origin + new Vector3(length, exitHeight, 0f);

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
            Gizmos.DrawLine(entry + Vector3.down * 2f, entry + Vector3.up * 8f);

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            Gizmos.DrawLine(exit + Vector3.down * 2f, exit + Vector3.up * 8f);

            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Gizmos.DrawLine(entry, exit);
        }
    }
}
