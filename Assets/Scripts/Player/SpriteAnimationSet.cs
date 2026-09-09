using System;
using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// One flip-book animation: the frames for a single player state.
    /// </summary>
    [Serializable]
    public class SpriteAnimationClip
    {
        [Tooltip("Which player animation slot these frames fill.")]
        public PlayerAnim anim;

        [Tooltip("Drag the sliced sprite frames here, in order.")]
        public Sprite[] frames;

        [Tooltip("Playback speed in frames per second. Ignored when strideDistance " +
                 "is set, since the clip is then driven by ground covered instead.")]
        public float fps = 12f;

        [Tooltip("Looping clips (run, idle) repeat. One-shots (vault, death) hold the last frame.")]
        public bool loop = true;

        [Tooltip("Ground distance one full cycle of this clip covers, in world units. " +
                 "Above zero the clip advances by how far the runner has actually " +
                 "travelled rather than by a frame rate, which is what keeps the feet " +
                 "with the ground as the run speed ramps. Leave at zero for anything " +
                 "that is not locomotion.")]
        public float strideDistance;
    }

    /// <summary>
    /// The complete sprite set for the runner - one clip per <see cref="PlayerAnim"/>.
    ///
    /// This is a deliberately simple alternative to an AnimatorController: import
    /// your sprite sheet, slice it in the Sprite Editor, then drag the frames into
    /// the matching slot here. No state machine to wire by hand.
    ///
    /// Create via Assets > Create > ArvinRunner > Sprite Animation Set.
    /// </summary>
    [CreateAssetMenu(menuName = "ArvinRunner/Sprite Animation Set", fileName = "PlayerAnimations")]
    public class SpriteAnimationSet : ScriptableObject
    {
        [Tooltip("Used whenever the requested animation has no frames assigned yet.")]
        public SpriteAnimationClip fallback;

        public SpriteAnimationClip[] clips;

        /// <summary>
        /// A clip for a slot, or the fallback if nothing fills it.
        ///
        /// A slot may hold several clips, and one is picked at random each time
        /// - so listing both the plain jump and the somersault under JumpRise
        /// makes repeated jumps stop looking canned. This mirrors what
        /// TrickSet.Pick already does for the procedural moves, and it is called
        /// only when the state changes, so the cost is a handful of comparisons
        /// per jump rather than per frame.
        /// </summary>
        public SpriteAnimationClip Get(PlayerAnim anim)
        {
            SpriteAnimationClip chosen = null;
            int matches = 0;

            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    SpriteAnimationClip clip = clips[i];
                    if (clip == null || clip.anim != anim) continue;
                    if (clip.frames == null || clip.frames.Length == 0) continue;

                    matches++;

                    // Reservoir sampling: the nth match wins with probability
                    // 1/n, which leaves every variant equally likely without
                    // building a list of them first. Qualified because this file
                    // has `using System`, so a bare Random is ambiguous.
                    if (UnityEngine.Random.Range(0, matches) == 0) chosen = clip;
                }
            }

            if (chosen != null) return chosen;

            return (fallback != null && fallback.frames != null && fallback.frames.Length > 0)
                ? fallback
                : null;
        }

        /// <summary>How many variants fill a slot. Handy when checking a set by hand.</summary>
        public int VariantCount(PlayerAnim anim)
        {
            int matches = 0;
            if (clips == null) return 0;

            foreach (SpriteAnimationClip clip in clips)
                if (clip != null && clip.anim == anim &&
                    clip.frames != null && clip.frames.Length > 0)
                    matches++;

            return matches;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Adds an empty clip for any PlayerAnim that has none, so the list is
        /// easy to populate by hand.
        ///
        /// Every existing clip is kept, including duplicates: a slot is allowed
        /// several variants and this must not be the thing that quietly deletes
        /// the second one.
        /// </summary>
        [ContextMenu("Create Empty Slots For All Animations")]
        private void CreateEmptySlots()
        {
            var built = new System.Collections.Generic.List<SpriteAnimationClip>();

            if (clips != null)
                foreach (SpriteAnimationClip clip in clips)
                    if (clip != null) built.Add(clip);

            foreach (PlayerAnim anim in Enum.GetValues(typeof(PlayerAnim)))
            {
                bool filled = false;
                foreach (SpriteAnimationClip clip in built)
                    if (clip.anim == anim) { filled = true; break; }

                if (filled) continue;

                built.Add(new SpriteAnimationClip
                {
                    anim = anim,
                    fps = 12f,
                    loop = anim == PlayerAnim.Idle || anim == PlayerAnim.Run || anim == PlayerAnim.WallRun
                });
            }

            clips = built.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
