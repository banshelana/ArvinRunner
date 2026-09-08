using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Cycles a SpriteRenderer through a list of frames.
    ///
    /// The runner has <see cref="PlayerAnimatorDriver"/>, which does this and a
    /// great deal more - state slots, random variants, procedural tricks. None
    /// of that applies to a bike driving past, so this is the small half of it:
    /// one clip, running forever, with the option to hold a single frame while
    /// something else is happening.
    /// </summary>
    public class SpriteFlipbook : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float fps = 14f;
        [SerializeField] private bool loop = true;

        [Tooltip("Starts the cycle part-way in, so several copies of the same " +
                 "obstacle do not beat in unison.")]
        [SerializeField] private float startOffset;

        private float _timer;
        private int _index;
        private float _holdRemaining;

        private void Awake()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();

            _timer = startOffset;
            if (HasFrames) target.sprite = frames[0];
        }

        private bool HasFrames => target != null && frames != null && frames.Length > 0;

        /// <summary>
        /// Shows one frame for a while, then picks the cycle back up. The
        /// helicopter uses it to sit on its muzzle-flash pose at the moment it
        /// fires, without needing a second animation system to do it.
        /// </summary>
        public void Hold(Sprite sprite, float seconds)
        {
            if (target == null || sprite == null) return;

            target.sprite = sprite;
            _holdRemaining = seconds;
        }

        private void Update()
        {
            if (!HasFrames) return;

            if (_holdRemaining > 0f)
            {
                _holdRemaining -= Time.deltaTime;
                return;
            }

            if (frames.Length == 1) return;

            _timer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, fps);
            if (_timer < frameDuration) return;

            _timer -= frameDuration;
            _index++;

            if (_index >= frames.Length)
                _index = loop ? 0 : frames.Length - 1;

            target.sprite = frames[_index];
        }
    }
}
