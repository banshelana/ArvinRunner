using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Drives the runner's visuals. Three layers, each optional:
    ///
    ///   1. Drawn frames - a <see cref="SpriteAnimationSet"/> flip-book, or a
    ///      Unity Animator if you prefer to author it there.
    ///   2. Procedural tricks - a <see cref="TrickSet"/> of spins, leans and
    ///      squashes. This is what makes a single drawn pose read as a
    ///      somersault or a dive roll.
    ///   3. Nothing at all - the runner is a static sprite and the game still
    ///      plays.
    ///
    /// Expected hierarchy, because rotation and scale need different origins:
    ///
    ///     Visual   (this component, sitting at the body centre) - spins here
    ///       Sprite (the SpriteRenderer, dropped to the feet)    - squashes here
    ///
    /// Spinning about the body centre is what makes a flip look like a flip;
    /// squashing from the feet is what keeps the runner planted on the ground.
    /// </summary>
    public class PlayerAnimatorDriver : MonoBehaviour
    {
        [Header("Renderer")]
        [Tooltip("The child holding the sprite. Found automatically if left empty.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Drawn frames")]
        [SerializeField] private SpriteAnimationSet animationSet;

        [Header("Procedural tricks")]
        [SerializeField] private TrickSet trickSet;
        [Tooltip("Tricks stop applying to any state that has real drawn frames, " +
                 "so hand-made animation is never fought over.")]
        [SerializeField] private bool tricksYieldToFrames = true;

        [Header("Unity Animator (alternative to frames)")]
        [SerializeField] private Animator animator;
        [SerializeField] private string stateParameter = "State";
        [SerializeField] private string speedParameter = "Speed";

        private PlayerController _player;
        private Transform _spriteTransform;

        private SpriteAnimationClip _clip;
        private TrickClip _trick;
        private PlayerAnim _current = PlayerAnim.Idle;

        private float _frameTimer;
        private int _frameIndex;
        private float _trickTime;

        private Vector3 _baseLocalPosition;
        private Vector3 _spriteBaseScale;

        private int _stateHash, _speedHash;

        // ---------------------------------------------------------------- //

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _spriteTransform = spriteRenderer != null ? spriteRenderer.transform : transform;

            _player = GetComponentInParent<PlayerController>();
            _baseLocalPosition = transform.localPosition;
            _spriteBaseScale = _spriteTransform.localScale;

            if (animator == null) animator = GetComponent<Animator>();
            _stateHash = Animator.StringToHash(stateParameter);
            _speedHash = Animator.StringToHash(speedParameter);

            if (_spriteTransform == transform && trickSet != null)
            {
                Debug.LogWarning("[PlayerAnimatorDriver] The sprite is on the same object as " +
                                 "this component, so spins will pivot on the runner's feet. " +
                                 "Put the SpriteRenderer on a child for proper flips.", this);
            }
        }

        private void Start() => Play(PlayerAnim.Idle);

        /// <summary>
        /// Switch to an animation slot. Safe to call every frame - it only does
        /// work when the state actually changes, which is also when a new random
        /// trick variant gets picked.
        /// </summary>
        public void Play(PlayerAnim anim)
        {
            if (_current == anim && _clip != null) return;

            _current = anim;
            _frameTimer = 0f;
            _frameIndex = 0;
            _trickTime = 0f;

            if (animationSet != null)
            {
                _clip = animationSet.Get(anim);
                if (_clip != null && _clip.frames.Length > 0)
                    spriteRenderer.sprite = _clip.frames[0];
            }

            // A fresh pick each time, so repeated double jumps alternate between
            // a front flip and a back flip rather than looking canned.
            _trick = TricksAllowed ? trickSet.Pick(anim) : null;

            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetInteger(_stateHash, (int)anim);
        }

        /// <summary>Drawn frames win over procedural motion for a given state.</summary>
        private bool TricksAllowed
        {
            get
            {
                if (trickSet == null) return false;
                if (!tricksYieldToFrames) return true;
                return _clip == null || _clip.frames == null || _clip.frames.Length <= 1;
            }
        }

        private void Update()
        {
            AdvanceFlipbook();
            UpdateAnimatorSpeed();
            ApplyTrick();
        }

        // ---------------------------------------------------------------- //

        /// <summary>
        /// Steps the drawn frames.
        ///
        /// A clip with a <see cref="SpriteAnimationClip.strideDistance"/> is
        /// advanced by <b>ground covered rather than by time</b>, which is the
        /// only way a run cycle can hold together in this game. The runner's
        /// speed ramps from 9 to 15 across a level, so any fixed frame rate is
        /// correct at exactly one speed and wrong either side of it - the feet
        /// skate forwards as the run gets faster. Tying the cycle to distance
        /// makes the cadence rise with the speed on its own, and the feet keep
        /// whatever relationship to the ground the artist drew.
        /// </summary>
        private void AdvanceFlipbook()
        {
            if (_clip == null || _clip.frames == null || _clip.frames.Length <= 1) return;

            int count = _clip.frames.Length;
            bool byDistance = _clip.strideDistance > 0.01f && _player != null;

            // What one frame is worth, and how much of it this update earned.
            float perFrame = byDistance
                ? _clip.strideDistance / count
                : 1f / Mathf.Max(1f, _clip.fps);

            float earned = byDistance
                ? Mathf.Abs(_player.CurrentSpeed) * Time.deltaTime
                : Time.deltaTime;

            _frameTimer += earned;
            if (_frameTimer < perFrame) return;

            // Whole frames at once, rather than one per update. The old single
            // step could not keep up whenever an update was longer than a frame
            // - at speed, or on a slower device - and the clip then played in
            // permanent slow motion instead of dropping frames to stay in time.
            int steps = (int)(_frameTimer / perFrame);
            _frameTimer -= steps * perFrame;
            _frameIndex += steps;

            if (_frameIndex >= count)
                _frameIndex = _clip.loop ? _frameIndex % count : count - 1;

            spriteRenderer.sprite = _clip.frames[_frameIndex];
        }

        private void UpdateAnimatorSpeed()
        {
            if (animator == null || animator.runtimeAnimatorController == null || _player == null) return;
            animator.SetFloat(_speedHash, _player.CurrentSpeed);
        }

        /// <summary>
        /// Evaluates the current trick and writes it onto the transforms. With no
        /// trick running it eases everything back to neutral, so a state with no
        /// procedural move never leaves the runner stuck mid-spin.
        /// </summary>
        private void ApplyTrick()
        {
            if (_trick == null)
            {
                ReturnToNeutral();
                return;
            }

            _trickTime += Time.deltaTime;

            float span = Mathf.Max(0.01f, _trick.duration);
            float t = _trick.loop
                ? Mathf.Repeat(_trickTime / span, 1f)
                : Mathf.Clamp01(_trickTime / span);

            // Rotation, about the body centre.
            float angle = _trick.leanDegrees + _trick.spinDegrees * _trick.spin.Evaluate(t);
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);

            // Offset, applied to the same object so it travels with the spin.
            Vector2 shift = _trick.offset * _trick.offsetCurve.Evaluate(t);
            transform.localPosition = _baseLocalPosition + new Vector3(shift.x, shift.y, 0f);

            // Squash, from the feet.
            float vertical = _trick.squash.Evaluate(t);
            float horizontal = 1f + (1f - vertical) * _trick.squashCounter;

            _spriteTransform.localScale = new Vector3(
                _spriteBaseScale.x * horizontal,
                _spriteBaseScale.y * vertical,
                _spriteBaseScale.z);
        }

        private void ReturnToNeutral()
        {
            transform.localRotation = Quaternion.Slerp(transform.localRotation,
                                                       Quaternion.identity,
                                                       Time.deltaTime * 12f);

            transform.localPosition = Vector3.Lerp(transform.localPosition,
                                                   _baseLocalPosition,
                                                   Time.deltaTime * 15f);

            _spriteTransform.localScale = Vector3.Lerp(_spriteTransform.localScale,
                                                       _spriteBaseScale,
                                                       Time.deltaTime * 15f);
        }
    }
}
