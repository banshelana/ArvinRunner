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
        private int _first;
        private int _last;
        private bool _finished;

        private void Awake()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();

            _timer = startOffset;
            _first = 0;
            _last = frames != null && frames.Length > 0 ? frames.Length - 1 : 0;

            if (HasFrames) target.sprite = frames[_index = _first];
        }

        private bool HasFrames => target != null && frames != null && frames.Length > 0;

        /// <summary>
        /// Plays a slice of the frames and stops on the last one.
        ///
        /// For art drawn as a storyboard rather than a cycle. An earlier helicopter
        /// was fly, descend, fire, missile away, recover, and looping that whole
        /// folder would have had it firing on a timer forever; the approach looped
        /// the first frames and the shot played the rest, once. The current
        /// helicopter is a plain flying loop and does not use it.
        /// </summary>
        public void PlayRange(int first, int last, bool looping)
        {
            if (frames == null || frames.Length == 0) return;

            _first = Mathf.Clamp(first, 0, frames.Length - 1);
            _last = Mathf.Clamp(last, _first, frames.Length - 1);

            loop = looping;
            _index = _first;
            _timer = 0f;
            _finished = false;

            if (target != null) target.sprite = frames[_index];
        }

        /// <summary>True once a non-looping range has reached its last frame.</summary>
        public bool Finished => _finished;

        private void Update()
        {
            if (!HasFrames || _finished) return;
            if (_last <= _first) return;

            _timer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, fps);
            if (_timer < frameDuration) return;

            _timer -= frameDuration;
            _index++;

            if (_index > _last)
            {
                if (loop)
                {
                    _index = _first;
                }
                else
                {
                    _index = _last;
                    _finished = true;
                }
            }

            target.sprite = frames[_index];
        }
    }
}
