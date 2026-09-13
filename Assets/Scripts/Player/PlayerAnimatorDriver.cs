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

        // How far through the clip, from 0 to 1. A fraction rather than seconds
        // or a frame count, because a clip is driven one of three ways - by the
        // clock, by ground covered, or along the jump arc - and each of them
        // measures the length of a clip in its own units.
        private float _phase;

        // Where each frame starts, as a fraction of the clip, with a closing 1.
        // Even steps unless the clip carries weights.
        private float[] _starts;

        private int _frameIndex = -1;
        private bool _finished;
        private bool _pastApex;
        private float _trickTime;

        // The jump clip that carried the runner into the air, whose own landing
        // plays on touchdown. Anything else clears it, so a drop off a roof with
        // no trick behind it lands the ordinary way.
        private SpriteAnimationClip _airClip;

        private Vector3 _baseLocalPosition;
        private Vector3 _spriteBaseScale;
        private Vector3 _spriteBaseLocalPosition;

        private int _stateHash, _speedHash;

        // ---------------------------------------------------------------- //

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _spriteTransform = spriteRenderer != null ? spriteRenderer.transform : transform;

            _player = GetComponentInParent<PlayerController>();
            _baseLocalPosition = transform.localPosition;
            _spriteBaseScale = _spriteTransform.localScale;
            _spriteBaseLocalPosition = _spriteTransform.localPosition;

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
        /// work when the slot actually changes, which is also when a new random
        /// variant gets picked.
        /// </summary>
        public void Play(PlayerAnim anim)
        {
            if (_current == anim && _clip != null) return;

            SpriteAnimationClip previous = _clip;
            _current = anim;
            _trickTime = 0f;

            if (animationSet != null)
            {
                // A landing belongs to the jump it ends - the somersault lands
                // out of the somersault - so a jump that names one gets it.
                SpriteAnimationClip landing = anim == PlayerAnim.Land && _airClip != null
                    ? animationSet.Find(_airClip.landing)
                    : null;

                _clip = landing ?? animationSet.Get(anim);
            }

            if (_clip != null && (_clip.followJump || _clip.followVault)) _airClip = _clip;
            else if (anim != PlayerAnim.Land) _airClip = null;

            BeginClip(previous);

            // A fresh pick each time, so repeated double jumps alternate between
            // a front flip and a back flip rather than looking canned.
            _trick = TricksAllowed ? trickSet.Pick(anim) : null;

            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetInteger(_stateHash, (int)anim);
        }

        /// <summary>
        /// True once a one-shot clip has played out - and always true for a
        /// looping clip, which never finishes, and for a held pose, which starts
        /// finished. A jump clip finishes when the runner has fallen well past
        /// where its landing should have been, which is the cue for the fall loop.
        ///
        /// The controller reads this to let an air trick own the whole jump
        /// rather than being cut off at the apex, and to know when a landing has
        /// taken its weight and the run can have the slot back.
        /// </summary>
        public bool ClipFinished
        {
            get
            {
                if (_clip == null || _clip.frames == null || _clip.frames.Length <= 1) return true;
                return _clip.loop || _finished;
            }
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

        private void BeginClip(SpriteAnimationClip previous)
        {
            _phase = 0f;
            _finished = false;
            _pastApex = false;
            _frameIndex = -1;

            if (_clip == null || _clip.frames == null || _clip.frames.Length == 0)
            {
                _starts = null;
                if (_spriteTransform != transform) _spriteTransform.localPosition = _spriteBaseLocalPosition;
                return;
            }

            _starts = FrameStarts(_clip);

            // Carry the pose across. A landing that finishes with the knee coming
            // through hands the run that point of its stride, instead of the run
            // restarting from whatever its first frame happens to be.
            if (_clip.loop && previous != null && previous != _clip && previous.exitToFrame >= 0)
                _phase = _starts[Mathf.Min(previous.exitToFrame, _clip.frames.Length - 1)];

            ShowFrame(IndexAt(_phase));
        }

        private static float[] FrameStarts(SpriteAnimationClip clip)
        {
            int count = clip.frames.Length;
            var starts = new float[count + 1];

            float total = 0f;
            for (int i = 0; i < count; i++) total += Weight(clip, i);

            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                starts[i] = sum / total;
                sum += Weight(clip, i);
            }

            starts[count] = 1f;
            return starts;
        }

        private static float Weight(SpriteAnimationClip clip, int index)
        {
            if (clip.weights == null || index >= clip.weights.Length) return 1f;
            return Mathf.Max(0.0001f, clip.weights[index]);
        }

        private int IndexAt(float phase)
        {
            for (int i = _clip.frames.Length - 1; i > 0; i--)
                if (phase >= _starts[i]) return i;
            return 0;
        }

        private void ShowFrame(int index)
        {
            _frameIndex = index;
            if (spriteRenderer != null) spriteRenderer.sprite = _clip.frames[index];

            // Registration belongs to the use of a drawing, not to the drawing -
            // see ArvinRunnerSetup.RegisterFrames. Only applied to a sprite child:
            // on this object it would fight the tricks for the position.
            if (_spriteTransform == transform) return;

            Vector2 offset = _clip.offsets != null && index < _clip.offsets.Length
                ? _clip.offsets[index]
                : Vector2.zero;

            _spriteTransform.localPosition = _spriteBaseLocalPosition + (Vector3)offset;
        }

        /// <summary>
        /// Moves the flip-book on, one of three ways.
        ///
        /// <b>Along the jump</b>, for a clip with followJump. The frame comes from
        /// the runner's vertical speed, which falls in a straight line from the
        /// launch speed to zero at the apex and grows in a straight line on the
        /// way down - so the apex frame is on screen at the top of the arc and the
        /// last one at touchdown, for a hop onto a crate and a super jump alike.
        /// A clock can only be right for one jump height. It used to be the
        /// clock: a somersault paced for the rise was cut in half at the apex,
        /// and paced for the whole jump it still finished early whenever the
        /// runner landed on something higher, and hung on its last frame for a
        /// drop.
        ///
        /// <b>By ground covered</b>, for a clip with a
        /// <see cref="SpriteAnimationClip.strideDistance"/>: the run. The speed
        /// ramps across a level, so any fixed rate is right at one speed and has
        /// the feet skating either side of it. Tied to distance, each drawing is
        /// on screen for exactly the stretch of ground its foot covers.
        /// <see cref="SpriteAnimationClip.strideGrowth"/> decides how much of a
        /// speed change lengthens the step rather than quickening the legs.
        ///
        /// <b>By the clock</b>, for everything else.
        ///
        /// All three move a fraction through the clip rather than stepping whole
        /// frames, and the frame is looked up from where that fraction lands - so
        /// a slow device drops drawings to keep time instead of playing in slow
        /// motion, and the clip's weights decide how much of the move each
        /// drawing covers.
        /// </summary>
        private void AdvanceFlipbook()
        {
            if (_clip == null || _clip.frames == null || _clip.frames.Length <= 1 || _starts == null) return;

            if (_clip.followVault && _player != null)
            {
                // Keyed to the vault: the plant frame as the hands meet the
                // obstacle, whatever the distance and speed. Once the vault has
                // ended, the last frame holds through the drop to the ground.
                _phase = _player.State == PlayerState.Vaulting ? Mathf.Max(_phase, VaultPhase()) : 1f;
            }
            else if (_clip.followJump && _player != null && _player.Config != null && _player.LaunchSpeed > 0.01f)
            {
                // Never backwards: a gust or a bump that nudges the vertical speed
                // must not rewind a somersault.
                _phase = Mathf.Max(_phase, JumpPhase());
            }
            else
            {
                bool byDistance = _clip.strideDistance > 0.01f && _player != null;

                float length = byDistance
                    ? StrideNow()
                    : _clip.frames.Length / Mathf.Max(1f, _clip.fps);

                float earned = byDistance
                    ? Mathf.Abs(_player.CurrentSpeed) * Time.deltaTime
                    : Time.deltaTime;

                _phase += earned / Mathf.Max(0.0001f, length);

                if (_phase >= 1f)
                {
                    if (_clip.loop)
                    {
                        _phase = Mathf.Repeat(_phase, 1f);
                    }
                    else
                    {
                        _phase = 1f;
                        _finished = true;
                    }
                }
            }

            int index = IndexAt(_phase);
            if (index != _frameIndex) ShowFrame(index);
        }

        /// <summary>
        /// Where along the current vault the runner is, as a fraction of the clip:
        /// take-off to the start of the plant frame, then the plant frame to the
        /// end, each stretched over its own share of the vault.
        ///
        /// The start, not the middle - unlike a jump's apex, which is an instant
        /// inside its frame. Contact is the moment the hands are drawn down, and
        /// keying the middle of the frame to it put the planted hands on screen a
        /// third of a frame early, 0.4 units short of the obstacle and below its
        /// top. From the start, they appear as they arrive and ride the top while
        /// the body carries over them.
        /// </summary>
        private float VaultPhase()
        {
            int plant = Mathf.Clamp(_clip.apexFrame, 0, _clip.frames.Length - 1);
            float plantPhase = _starts[plant];

            float t = _player.VaultProgress;
            float tp = Mathf.Clamp(_player.VaultPlant, 0.01f, 0.99f);

            return t < tp
                ? plantPhase * t / tp
                : plantPhase + (1f - plantPhase) * (t - tp) / (1f - tp);
        }

        /// <summary>
        /// Where along the current jump the runner is, as a fraction of the clip.
        /// The rise fills the clip up to the middle of the apex frame, and the
        /// fall fills the rest.
        /// </summary>
        private float JumpPhase()
        {
            PlayerConfig config = _player.Config;
            float launch = _player.LaunchSpeed;
            float rising = _player.VerticalSpeed;
            float gravity = Mathf.Abs(Physics2D.gravity.y);

            int apex = Mathf.Clamp(_clip.apexFrame, 0, _clip.frames.Length - 1);
            float apexPhase = (_starts[apex] + _starts[apex + 1]) * 0.5f;

            if (!_pastApex && rising > 0f)
                return apexPhase * Mathf.Clamp01(1f - rising / launch);

            _pastApex = true;

            // The speed it will be falling at on getting back to the height it
            // left from: the same climb, undone under the heavier fall gravity.
            float height = launch * launch / (2f * gravity * Mathf.Max(0.01f, config.riseGravity));
            float landing = Mathf.Sqrt(2f * gravity * Mathf.Max(0.01f, config.fallGravity) * height);
            float fallen = -rising / Mathf.Max(0.01f, landing);

            // Well past that and still in the air - off the edge of something -
            // so the fall loop can take over from the last frame.
            if (fallen > 1.25f) _finished = true;

            return apexPhase + (1f - apexPhase) * Mathf.Clamp01(fallen);
        }

        /// <summary>
        /// The distance one cycle of the current clip covers at the speed the
        /// runner is going, which is the authored stride stretched by
        /// <see cref="SpriteAnimationClip.strideGrowth"/>.
        ///
        /// The ratio is clamped at both ends. Below, because a runner pressed to
        /// a standstill against a wall would otherwise drive the stride - and so
        /// the time one frame is worth - to zero. Above, because nothing in the
        /// game should be moving fast enough to need it and a bounce pad should
        /// not be able to prove otherwise.
        /// </summary>
        private float StrideNow()
        {
            float stride = _clip.strideDistance;

            if (_clip.strideGrowth <= 0.001f || _clip.strideReferenceSpeed <= 0.01f)
                return stride;

            float ratio = Mathf.Clamp(Mathf.Abs(_player.CurrentSpeed) / _clip.strideReferenceSpeed,
                                      0.3f, 3f);

            return stride * Mathf.Pow(ratio, _clip.strideGrowth);
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
