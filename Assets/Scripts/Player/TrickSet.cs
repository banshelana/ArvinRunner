using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// One procedural move: how the runner's body twists, stretches and shifts
    /// over the course of a state.
    ///
    /// This is what turns a single drawn pose into a flip, a roll or a dive. A
    /// silhouette runner reads convincingly this way - a 360 degree spin on a
    /// jump is a somersault to the eye, no extra frames needed.
    /// </summary>
    [System.Serializable]
    public class TrickClip
    {
        public string name = "Trick";

        [Tooltip("Which state this move belongs to. Several clips can share a " +
                 "state - one is picked at random each time, so a move never " +
                 "looks identical twice.")]
        public PlayerAnim anim;

        [Tooltip("How long the move takes. Looping clips use this as the cycle length.")]
        public float duration = 0.4f;

        [Tooltip("Replay while the state lasts, instead of holding the final pose.")]
        public bool loop;

        [Header("Rotation")]
        [Tooltip("Total spin in degrees. 360 is a full somersault, -360 a backflip.")]
        public float spinDegrees;

        [Tooltip("How the spin is spread across the clip. Linear reads best for " +
                 "a full rotation; ease-out for a partial twist.")]
        public AnimationCurve spin = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("A steady tilt held on top of the spin. Negative leans forward.")]
        public float leanDegrees;

        [Header("Shape")]
        [Tooltip("Vertical scale over the clip. 1 is neutral, below 1 squashes.")]
        public AnimationCurve squash = AnimationCurve.Constant(0f, 1f, 1f);

        [Tooltip("How much the body widens as it squashes. 0 disables it.")]
        [Range(0f, 1.5f)] public float squashCounter = 0.7f;

        [Header("Offset")]
        [Tooltip("Peak positional shift, in world units.")]
        public Vector2 offset;

        [Tooltip("Shape of that shift over the clip.")]
        public AnimationCurve offsetCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    /// <summary>
    /// The runner's whole procedural repertoire. Sits alongside
    /// <see cref="SpriteAnimationSet"/> rather than replacing it: drawn frames
    /// carry the pose, these curves carry the motion, and they layer.
    ///
    /// Create via Assets > Create > ArvinRunner > Trick Set.
    /// </summary>
    [CreateAssetMenu(menuName = "ArvinRunner/Trick Set", fileName = "PlayerTricks")]
    public class TrickSet : ScriptableObject
    {
        public TrickClip[] clips;

        private readonly Dictionary<PlayerAnim, List<TrickClip>> _byState =
            new Dictionary<PlayerAnim, List<TrickClip>>();

        private void OnEnable() => _byState.Clear();

        /// <summary>
        /// A clip for this state, chosen at random when there are several.
        /// Returns null when the state has no procedural move, which leaves the
        /// runner in its neutral pose.
        /// </summary>
        public TrickClip Pick(PlayerAnim anim)
        {
            if (_byState.Count == 0) Rebuild();

            if (!_byState.TryGetValue(anim, out List<TrickClip> options) || options.Count == 0)
                return null;

            return options.Count == 1 ? options[0] : options[Random.Range(0, options.Count)];
        }

        /// <summary>How many variants a state has. Useful for editor checks.</summary>
        public int VariantCount(PlayerAnim anim)
        {
            if (_byState.Count == 0) Rebuild();
            return _byState.TryGetValue(anim, out List<TrickClip> options) ? options.Count : 0;
        }

        private void Rebuild()
        {
            _byState.Clear();
            if (clips == null) return;

            foreach (TrickClip clip in clips)
            {
                if (clip == null) continue;

                if (!_byState.TryGetValue(clip.anim, out List<TrickClip> list))
                {
                    list = new List<TrickClip>();
                    _byState[clip.anim] = list;
                }

                list.Add(clip);
            }
        }
    }
}
