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

        [Tooltip("Playback speed in frames per second.")]
        public float fps = 12f;

        [Tooltip("Looping clips (run, idle) repeat. One-shots (vault, death) hold the last frame.")]
        public bool loop = true;
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

        /// <summary>Returns the clip for a slot, or the fallback if it is empty.</summary>
        public SpriteAnimationClip Get(PlayerAnim anim)
        {
            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    SpriteAnimationClip clip = clips[i];
                    if (clip != null && clip.anim == anim && clip.frames != null && clip.frames.Length > 0)
                        return clip;
                }
            }

            return (fallback != null && fallback.frames != null && fallback.frames.Length > 0)
                ? fallback
                : null;
        }

#if UNITY_EDITOR
        /// <summary>Fills in one empty slot per PlayerAnim so the list is easy to populate.</summary>
        [ContextMenu("Create Empty Slots For All Animations")]
        private void CreateEmptySlots()
        {
            Array values = Enum.GetValues(typeof(PlayerAnim));
            var built = new SpriteAnimationClip[values.Length];

            for (int i = 0; i < values.Length; i++)
            {
                var anim = (PlayerAnim)values.GetValue(i);
                SpriteAnimationClip existing = null;

                if (clips != null)
                    foreach (SpriteAnimationClip c in clips)
                        if (c != null && c.anim == anim) { existing = c; break; }

                built[i] = existing ?? new SpriteAnimationClip
                {
                    anim = anim,
                    fps = 12f,
                    loop = anim == PlayerAnim.Idle || anim == PlayerAnim.Run || anim == PlayerAnim.WallRun
                };
            }

            clips = built;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
