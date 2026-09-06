using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Drives the runner's visuals. Works two ways, so you can pick whichever
    /// suits the art you have:
    ///
    ///   - Assign a <see cref="SpriteAnimationSet"/> and it plays flip-book
    ///     frames on the SpriteRenderer. Nothing else to set up.
    ///   - Or assign a Unity Animator and it sets an integer parameter named
    ///     "State" (plus a "Speed" float) matching the PlayerAnim enum value.
    ///
    /// If neither is assigned the game still runs - the runner is just a
    /// static sprite, which is fine while you build levels.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerAnimatorDriver : MonoBehaviour
    {
        [Header("Option A - sprite flip-book")]
        [SerializeField] private SpriteAnimationSet animationSet;

        [Header("Option B - Unity Animator")]
        [SerializeField] private Animator animator;
        [SerializeField] private string stateParameter = "State";
        [SerializeField] private string speedParameter = "Speed";

        [Header("Squash and stretch")]
        [Tooltip("Adds a little life even before real animation frames exist.")]
        [SerializeField] private bool proceduralSquash = true;
        [SerializeField] private float squashAmount = 0.12f;

        [Header("Single-frame motion")]
        [Tooltip("Bob and lean the sprite when a slot has only one frame, so a " +
                 "still pose does not look frozen. Ignored once real frames exist.")]
        [SerializeField] private bool animateStillFrames = true;
        [SerializeField] private float bobHeight = 0.07f;
        [SerializeField] private float bobSpeed = 11f;
        [Tooltip("Forward lean while running, in degrees.")]
        [SerializeField] private float runLean = 4f;

        private SpriteRenderer _renderer;
        private PlayerController _player;
        private Vector3 _basePosition;

        private SpriteAnimationClip _clip;
        private PlayerAnim _current = PlayerAnim.Idle;
        private float _frameTimer;
        private int _frameIndex;

        private int _stateHash, _speedHash;
        private Vector3 _baseScale;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _player = GetComponentInParent<PlayerController>();
            _baseScale = transform.localScale;
            _basePosition = transform.localPosition;

            if (animator == null) animator = GetComponent<Animator>();
            _stateHash = Animator.StringToHash(stateParameter);
            _speedHash = Animator.StringToHash(speedParameter);
        }

        private void Start()
        {
            Play(PlayerAnim.Idle);
        }

        /// <summary>Switch to an animation slot. Safe to call every frame.</summary>
        public void Play(PlayerAnim anim)
        {
            if (_current == anim && _clip != null) return;

            _current = anim;
            _frameTimer = 0f;
            _frameIndex = 0;

            if (animationSet != null)
            {
                _clip = animationSet.Get(anim);
                if (_clip != null && _clip.frames.Length > 0)
                    _renderer.sprite = _clip.frames[0];
            }

            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetInteger(_stateHash, (int)anim);
        }

        private void Update()
        {
            AdvanceFlipbook();
            UpdateAnimatorSpeed();
            if (proceduralSquash) ApplySquash();
            if (animateStillFrames) ApplyStillFrameMotion();
        }

        /// <summary>True once a slot has enough frames to animate on its own.</summary>
        private bool HasRealAnimation => _clip != null && _clip.frames != null && _clip.frames.Length > 1;

        /// <summary>
        /// Gives a one-frame pose some motion: a footfall bob while running and
        /// a forward lean. Backs off entirely the moment real frames are
        /// assigned, so it never fights hand-made animation.
        /// </summary>
        private void ApplyStillFrameMotion()
        {
            float bob = 0f;
            float lean = 0f;

            if (!HasRealAnimation)
            {
                switch (_current)
                {
                    case PlayerAnim.Run:
                    case PlayerAnim.WallRun:
                        // Absolute sine so the sprite only ever rises - dipping
                        // would push the feet through the floor.
                        bob = Mathf.Abs(Mathf.Sin(Time.time * bobSpeed)) * bobHeight;
                        lean = -runLean;
                        break;

                    case PlayerAnim.JumpRise:
                    case PlayerAnim.DoubleJump:
                        lean = -runLean * 1.5f;
                        break;

                    case PlayerAnim.JumpFall:
                        lean = runLean * 0.5f;
                        break;

                    case PlayerAnim.Slide:
                    case PlayerAnim.Roll:
                        lean = -runLean * 4f;
                        break;
                }
            }

            transform.localPosition = Vector3.Lerp(transform.localPosition,
                                                   _basePosition + Vector3.up * bob,
                                                   Time.deltaTime * 20f);

            transform.localRotation = Quaternion.Slerp(transform.localRotation,
                                                       Quaternion.Euler(0f, 0f, lean),
                                                       Time.deltaTime * 12f);
        }

        private void AdvanceFlipbook()
        {
            if (_clip == null || _clip.frames == null || _clip.frames.Length <= 1) return;

            _frameTimer += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, _clip.fps);
            if (_frameTimer < frameDuration) return;

            _frameTimer -= frameDuration;
            _frameIndex++;

            if (_frameIndex >= _clip.frames.Length)
                _frameIndex = _clip.loop ? 0 : _clip.frames.Length - 1;

            _renderer.sprite = _clip.frames[_frameIndex];
        }

        private void UpdateAnimatorSpeed()
        {
            if (animator == null || animator.runtimeAnimatorController == null || _player == null) return;
            animator.SetFloat(_speedHash, _player.CurrentSpeed);
        }

        /// <summary>
        /// Stretches the runner vertically when rising and squashes on landing.
        /// Purely cosmetic, and it reads well even with placeholder art.
        /// </summary>
        private void ApplySquash()
        {
            float target = 1f;

            switch (_current)
            {
                case PlayerAnim.JumpRise:
                case PlayerAnim.DoubleJump:
                    target = 1f + squashAmount;
                    break;
                case PlayerAnim.Slide:
                case PlayerAnim.Roll:
                    target = 1f - squashAmount;
                    break;
            }

            Vector3 wanted = new Vector3(_baseScale.x * (2f - target), _baseScale.y * target, _baseScale.z);
            transform.localScale = Vector3.Lerp(transform.localScale, wanted, Time.deltaTime * 12f);
        }
    }
}
