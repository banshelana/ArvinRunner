using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// The runner. Auto-runs to the right; every other move comes from a swipe.
    ///
    ///   swipe up    - jump, then double jump. Near a low obstacle it becomes a
    ///                 vault; against a wall in mid-air it becomes a wall jump.
    ///   swipe down  - slide on the ground, dive-slam in the air.
    ///   swipe right - climb up from a ledge grab.
    ///   swipe left  - the charged super jump, when the meter is full.
    ///
    /// Contextual moves (vault, wall run, ledge grab, hard-landing roll) are
    /// chosen by <see cref="PlayerSensors"/>, so the same gesture does the
    /// right thing depending on what is in front of the runner.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    [RequireComponent(typeof(PlayerSensors))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private PlayerConfig config;
        [SerializeField] private PlayerAnimatorDriver animator;

        [Tooltip("The power meter. Optional - with none assigned the super jump " +
                 "simply never becomes available and everything else is unchanged.")]
        [SerializeField] private SuperJumpMeter meter;

        [Header("Death")]
        [Tooltip("How fast the runner is thrown back off whatever killed them.")]
        [SerializeField] private float knockbackSpeed = 2.5f;

        [Tooltip("Drag that brings a dead runner to rest. The capsule is frictionless " +
                 "so that it can run, so without this a body would slide forever.")]
        [SerializeField] private float deathDrag = 2.5f;

        // ---- Events other systems hook into ----------------------------- //
        public event System.Action<PlayerState, PlayerState> OnStateChanged; // (from, to)
        public event System.Action<DeathCause> OnDied;
        public event System.Action OnJumped;
        public event System.Action OnDoubleJumped;
        public event System.Action OnLanded;
        public event System.Action OnVaulted;
        public event System.Action OnSlideStarted;
        public event System.Action OnWallRunStarted;

        public PlayerState State { get; private set; } = PlayerState.Idle;
        public PlayerConfig Config => config;
        public bool IsAlive => State != PlayerState.Dead;

        /// <summary>The power meter, for the HUD gauge. Null if none is wired.</summary>
        public SuperJumpMeter Meter => meter;

        /// <summary>Raised when a super jump is actually launched.</summary>
        public event System.Action OnSuperJumped;

        /// <summary>Set false to freeze the runner (menus, level intro, finish).</summary>
        public bool ControlEnabled { get; set; }

        /// <summary>Distance travelled along the level, in world units.</summary>
        public float DistanceTravelled => Mathf.Max(0f, transform.position.x - _startX);

        public float CurrentSpeed { get; private set; }

        /// <summary>Upward speed the current jump left with. The animator reads
        /// the arc off it, so a jump clip keeps pace with the jump.</summary>
        public float LaunchSpeed { get; private set; }

        /// <summary>Vertical velocity right now, for the same reason.</summary>
        public float VerticalSpeed => _rb != null ? _rb.velocity.y : 0f;

        private Rigidbody2D _rb;
        private CapsuleCollider2D _capsule;
        private PlayerSensors _sensors;

        private Vector2 _standSize;
        private Vector2 _standOffset;
        private float _startX;

        private float _stateTime;          // seconds spent in the current state
        private float _lastGroundedTime = -99f;
        private bool _usedDoubleJump;
        private float _blockedTime;

        // The slot the state machine last asked for, and whether the climb is
        // currently painted over the top of it. See SetClinging.
        private PlayerAnim _stateAnim = PlayerAnim.Idle;
        private bool _clinging;

        // The state before the current one, so a state can start differently
        // depending on how it was reached - see ArrivalAnim.
        private PlayerState _previousState;
        private float _speedBoost = 1f;    // slide / powerup multiplier
        private bool _superJumpQueued;     // set the instant before SetState(Jumping)
        private float _runTime;            // drives the difficulty speed ramp

        // The wall we already used. Cleared on landing, so one jump buys one
        // wall run per wall - without it the runner can re-trigger a wall run
        // the instant the previous one ends and hang there forever.
        private Collider2D _wallRunClaimed;

        // Vault / climb interpolation
        private Vector2 _moveFrom, _moveTo;
        private float _moveArc;
        private Collider2D _ignoredCollider;

        // Ledge
        private float _ledgeTargetY;

        private SwipeInput Input_ => SwipeInput.Instance;

        // ================================================================= //
        // Lifecycle
        // ================================================================= //

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _capsule = GetComponent<CapsuleCollider2D>();
            _sensors = GetComponent<PlayerSensors>();

            _standSize = _capsule.size;
            _standOffset = _capsule.offset;

            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            if (config == null)
                Debug.LogError("[PlayerController] No PlayerConfig assigned.", this);
            else
                _sensors.Initialise(config);

            _startX = transform.position.x;
            CurrentSpeed = 0f;
        }

        /// <summary>Called by the GameManager when a level starts.</summary>
        public void ResetForLevel(Vector3 spawnPosition)
        {
            transform.position = spawnPosition;
            _startX = spawnPosition.x;
            _rb.velocity = Vector2.zero;
            _rb.simulated = true;

            RestoreCollider();
            ClearIgnoredCollider();

            _runTime = 0f;
            _speedBoost = 1f;
            _rb.drag = 0f;
            _blockedTime = 0f;
            _usedDoubleJump = false;
            _superJumpQueued = false;
            LaunchSpeed = 0f;
            if (meter != null) meter.ResetCharge();
            _wallRunClaimed = null;
            CurrentSpeed = config != null ? config.runSpeed : 9f;

            ControlEnabled = false;
            SetState(PlayerState.Idle);
        }

        private void FixedUpdate()
        {
            if (config == null) return;

            _sensors.Sample();
            _stateTime += Time.fixedDeltaTime;

            if (State == PlayerState.Dead)
                return;

            if (!ControlEnabled)
            {
                // Held at the start line: stand still but stay grounded.
                _rb.velocity = new Vector2(0f, Mathf.Min(0f, _rb.velocity.y));
                return;
            }

            _runTime += Time.fixedDeltaTime;

            // Past the Dead and ControlEnabled guards above, so this is exactly
            // the time the runner is actually running.
            if (meter != null) meter.Charge(Time.fixedDeltaTime);

            if (_sensors.Grounded)
            {
                _lastGroundedTime = Time.time;
                _wallRunClaimed = null;   // touching down re-arms the wall run
            }

            TickState();
            ApplyGravity();
            CheckCrash();
        }

        // ================================================================= //
        // State machine
        // ================================================================= //

        private void SetState(PlayerState next)
        {
            if (State == next) return;

            ExitState(State);
            PlayerState previous = State;
            State = next;
            _stateTime = 0f;
            _previousState = previous;
            EnterState(next);

            OnStateChanged?.Invoke(previous, next);
        }

        private void EnterState(PlayerState state)
        {
            switch (state)
            {
                case PlayerState.Idle:
                    PlayAnim(PlayerAnim.Idle);
                    break;

                case PlayerState.Running:
                    PlayAnim(ArrivalAnim(_previousState));
                    _usedDoubleJump = false;
                    break;

                case PlayerState.Jumping:
                    if (_superJumpQueued)
                    {
                        _superJumpQueued = false;

                        _rb.velocity = new Vector2(_rb.velocity.x,
                                                   config.JumpVelocityFor(config.superJumpHeight));
                        LaunchSpeed = _rb.velocity.y;

                        // Carried rather than impulsed, so it reads as the runner
                        // powering through the arc instead of being flicked
                        // forward. Cleared on landing, with the slide boost.
                        _speedBoost = config.superJumpSpeedMultiplier;

                        PlayAnim(PlayerAnim.SuperJump);
                        OnSuperJumped?.Invoke();
                        OnJumped?.Invoke();
                        break;
                    }

                    _rb.velocity = new Vector2(_rb.velocity.x, config.JumpVelocityFor(config.jumpHeight));
                    LaunchSpeed = _rb.velocity.y;

                    // Read while the sensors still have a ground reading - they
                    // stop sampling the vault and low-obstacle probes the moment
                    // the runner leaves the roof, and this runs on that frame.
                    PlayAnim(_sensors.LowObstacleAhead ? PlayerAnim.LowFlip : PlayerAnim.JumpRise);
                    OnJumped?.Invoke();
                    break;

                case PlayerState.DoubleJump:
                    _usedDoubleJump = true;
                    _rb.velocity = new Vector2(_rb.velocity.x, config.JumpVelocityFor(config.doubleJumpHeight));
                    LaunchSpeed = _rb.velocity.y;
                    PlayAnim(PlayerAnim.DoubleJump);
                    OnDoubleJumped?.Invoke();
                    break;

                case PlayerState.Falling:
                    // The apex is the middle of a somersault, not the end of
                    // one. This used to play the fall clip the instant upward
                    // speed reached zero, which cut every trick in half: the
                    // flip was paced across the 0.38s rise and then replaced,
                    // mid-rotation, by a different clip. Half a somersault at
                    // twice the speed it should run, with a visible join in the
                    // middle of it. A trick still in the air keeps the slot, and
                    // TickAirborne hands over the moment it lands the figure.
                    if (ClipFinished) PlayAnim(PlayerAnim.JumpFall);
                    break;

                case PlayerState.Sliding:
                    SetColliderFraction(config.slideHeightFraction);
                    _speedBoost = config.slideSpeedMultiplier;
                    PlayAnim(PlayerAnim.Slide);
                    OnSlideStarted?.Invoke();
                    break;

                case PlayerState.Rolling:
                    SetColliderFraction(config.rollHeightFraction);
                    PlayAnim(PlayerAnim.Roll);
                    break;

                case PlayerState.Vaulting:
                    BeginVault();
                    break;

                case PlayerState.WallRunning:
                    _usedDoubleJump = false;   // a wall run refreshes the air jump
                    _rb.velocity = new Vector2(0f, config.wallRunSpeed);
                    PlayAnim(PlayerAnim.WallRun);
                    OnWallRunStarted?.Invoke();
                    break;

                case PlayerState.LedgeGrab:
                    _rb.velocity = Vector2.zero;
                    _rb.gravityScale = 0f;
                    _ledgeTargetY = _sensors.LedgeTopY;
                    PlayAnim(PlayerAnim.LedgeGrab);
                    break;

                case PlayerState.LedgeClimb:
                    BeginLedgeClimb();
                    break;

                case PlayerState.Dead:
                    PlayAnim(PlayerAnim.Death);
                    break;
            }
        }

        private void ExitState(PlayerState state)
        {
            switch (state)
            {
                case PlayerState.Sliding:
                case PlayerState.Rolling:
                    RestoreCollider();
                    _speedBoost = 1f;
                    break;

                case PlayerState.Vaulting:
                case PlayerState.LedgeClimb:
                    ClearIgnoredCollider();
                    _rb.isKinematic = false;
                    break;

                case PlayerState.LedgeGrab:
                    _rb.gravityScale = config.fallGravity;
                    break;
            }
        }

        private void TickState()
        {
            switch (State)
            {
                case PlayerState.Idle:      TickIdle();       break;
                case PlayerState.Running:   TickRunning();    break;
                case PlayerState.Jumping:
                case PlayerState.DoubleJump:
                case PlayerState.Falling:   TickAirborne();   break;
                case PlayerState.Sliding:   TickSliding();    break;
                case PlayerState.Rolling:   TickRolling();    break;
                case PlayerState.Vaulting:  TickVaulting();   break;
                case PlayerState.WallRunning: TickWallRun();  break;
                case PlayerState.LedgeGrab: TickLedgeGrab();  break;
                case PlayerState.LedgeClimb: TickLedgeClimb(); break;
            }
        }

        // ---- Ground states ---------------------------------------------- //

        private void TickIdle()
        {
            SetState(PlayerState.Running);
        }

        private void TickRunning()
        {
            DriveHorizontal();

            if (!_sensors.Grounded)
            {
                SetState(PlayerState.Falling);
                return;
            }

            // A landing or a get-up is painted over the run and gives it back
            // once it has played. Not while clinging: the cling holds the slot
            // until the runner is free, and hands it back to whichever of these
            // it covered.
            if (!_clinging && (_stateAnim == PlayerAnim.Land || _stateAnim == PlayerAnim.GetUp) && ClipFinished)
                PlayAnim(PlayerAnim.Run);

            // The super jump is its own gesture rather than an upgrade of the
            // normal one, so a charged player never has a jump turn into
            // something bigger than they asked for. It also deliberately ignores
            // VaultAhead below: the whole point is to go over the thing.
            if (ConsumeLeft() && meter != null && meter.TrySpend())
            {
                _superJumpQueued = true;
                SetState(PlayerState.Jumping);
                return;
            }

            if (ConsumeUp())
            {
                // A low obstacle in front turns the jump into a vault.
                if (_sensors.VaultAhead) SetState(PlayerState.Vaulting);
                else SetState(PlayerState.Jumping);
                return;
            }

            if (ConsumeDown())
            {
                SetState(PlayerState.Sliding);
            }
        }

        private void TickSliding()
        {
            DriveHorizontal();

            if (!_sensors.Grounded)
            {
                SetState(PlayerState.Falling);
                return;
            }

            // Sliding under a low ceiling: keep going until there is headroom.
            bool expired = _stateTime >= config.slideDuration;
            if (expired && _sensors.CeilingBlocked) return;

            if (ConsumeUp() && !_sensors.CeilingBlocked)
            {
                SetState(PlayerState.Jumping);
                return;
            }

            if (expired) SetState(PlayerState.Running);
        }

        private void TickRolling()
        {
            DriveHorizontal();

            if (!_sensors.Grounded)
            {
                SetState(PlayerState.Falling);
                return;
            }

            bool expired = _stateTime >= config.rollDuration;
            if (expired && _sensors.CeilingBlocked) return;
            if (expired) SetState(PlayerState.Running);
        }

        // ---- Air states -------------------------------------------------- //

        private void TickAirborne()
        {
            DriveHorizontal();

            // Rising -> falling swap keeps the animation honest.
            if (State != PlayerState.Falling && _rb.velocity.y <= 0.01f)
                SetState(PlayerState.Falling);

            // The trick that carried the runner over the apex has finished, so
            // the fall can have the slot back. Usually it never comes up - the
            // air clips are paced to land on touchdown - but a jump onto a roof
            // ends early and a jump off one runs long.
            if (State == PlayerState.Falling && _stateAnim != PlayerAnim.JumpFall && ClipFinished)
                PlayAnim(PlayerAnim.JumpFall);

            if (Land()) return;

            // A wall in front while airborne starts a wall run automatically -
            // but only once per wall per jump.
            if (config.wallRunEnabled && _sensors.WallAhead && !_sensors.LedgeAhead
                && _sensors.WallCollider != _wallRunClaimed)
            {
                _wallRunClaimed = _sensors.WallCollider;
                SetState(PlayerState.WallRunning);
                return;
            }

            if (config.ledgeGrabEnabled && _sensors.LedgeAhead && _rb.velocity.y < config.maxLedgeGrabRiseSpeed)
            {
                SetState(PlayerState.LedgeGrab);
                return;
            }

            if (ConsumeUp() && CanAirJump())
            {
                SetState(PlayerState.DoubleJump);
                return;
            }

            if (ConsumeDown())
            {
                // Dive-slam: cut straight down, then roll out of the landing.
                _rb.velocity = new Vector2(_rb.velocity.x, -config.airDiveSpeed);
            }
        }

        private bool CanAirJump()
        {
            // Coyote time: a jump just after running off a ledge is the ground
            // jump, not the air jump.
            if (Time.time - _lastGroundedTime <= config.coyoteTime && State == PlayerState.Falling)
                return true;
            return !_usedDoubleJump;
        }

        /// <summary>Returns true if we touched down and changed state.</summary>
        private bool Land()
        {
            if (!_sensors.Grounded || _rb.velocity.y > 0.01f) return false;

            float impact = Mathf.Abs(_rb.velocity.y);
            OnLanded?.Invoke();

            // The super jump's forward carry ends on touchdown. Cleared here
            // rather than on leaving the air states, because this runs before
            // the landing state is entered - so a landing that goes straight
            // into a slide still gets to set its own multiplier afterwards.
            _speedBoost = 1f;

            if (impact >= config.hardLandingSpeed)
                SetState(PlayerState.Rolling);      // hard landing forces a roll
            else if (Input_ != null && Input_.IsPending(Swipe.Down))
                SetState(PlayerState.Sliding);      // buffered slide fires on touchdown
            else
                SetState(PlayerState.Running);

            return true;
        }

        // ---- Wall run ----------------------------------------------------- //

        private void TickWallRun()
        {
            // Push gently into the wall so we keep contact, and climb.
            float vy = _rb.velocity.y - config.wallRunGravity * Time.fixedDeltaTime;
            _rb.velocity = new Vector2(1.5f, vy);

            if (ConsumeUp())
            {
                _rb.velocity = new Vector2(config.wallJumpImpulse.x, config.wallJumpImpulse.y);
                SetState(PlayerState.Jumping);
                return;
            }

            if (config.ledgeGrabEnabled && _sensors.LedgeAhead)
            {
                SetState(PlayerState.LedgeGrab);
                return;
            }

            // Made it onto the top, or back down beside the wall.
            if (_sensors.Grounded && vy <= 0f)
            {
                SetState(PlayerState.Running);
                return;
            }

            // The run ends on its own timer or when the climb dies out - not
            // when the chest clears the wall top. Cutting it there stranded the
            // runner just below the ledge with nothing left to grab.
            if (_stateTime >= config.wallRunDuration || vy <= 0f)
                SetState(PlayerState.Falling);
        }

        // ---- Ledge -------------------------------------------------------- //

        private void TickLedgeGrab()
        {
            _rb.velocity = Vector2.zero;

            // Swipe up or forward to climb; hanging too long drops you.
            if (ConsumeUp() || ConsumeRight() || _stateTime > 0.6f)
                SetState(PlayerState.LedgeClimb);
            else if (ConsumeDown())
                SetState(PlayerState.Falling);
        }

        private void BeginLedgeClimb()
        {
            _rb.isKinematic = true;
            _rb.velocity = Vector2.zero;

            _moveFrom = _rb.position;
            _moveTo = new Vector2(_rb.position.x + _sensors.Width * 1.2f,
                                  _ledgeTargetY + FeetOffset + 0.05f);
            _moveArc = 0.2f;

            IgnoreCollider(_sensors.WallCollider);
            PlayAnim(PlayerAnim.LedgeClimb);
        }

        private void TickLedgeClimb()
        {
            float t = Mathf.Clamp01(_stateTime / config.ledgeClimbDuration);
            _rb.MovePosition(ArcLerp(_moveFrom, _moveTo, _moveArc, t));

            if (t >= 1f)
            {
                _rb.isKinematic = false;
                _rb.velocity = new Vector2(CurrentSpeed, 0f);
                SetState(PlayerState.Running);
            }
        }

        // ---- Vault -------------------------------------------------------- //

        private void BeginVault()
        {
            _rb.isKinematic = true;
            _rb.velocity = Vector2.zero;

            _moveFrom = _rb.position;
            _moveTo = new Vector2(_rb.position.x + _sensors.Width + config.vaultProbeDistance + 0.4f,
                                  _sensors.VaultTopY + FeetOffset + 0.05f);
            _moveArc = config.vaultArcHeight;

            IgnoreCollider(_sensors.VaultCollider);
            PlayAnim(PlayerAnim.Vault);
            OnVaulted?.Invoke();
        }

        private void TickVaulting()
        {
            float t = Mathf.Clamp01(_stateTime / config.vaultDuration);
            _rb.MovePosition(ArcLerp(_moveFrom, _moveTo, _moveArc, t));

            if (t >= 1f)
            {
                _rb.isKinematic = false;
                ClearIgnoredCollider();
                _rb.velocity = new Vector2(CurrentSpeed, 0f);
                SetState(PlayerState.Falling);
            }
        }

        // ================================================================= //
        // Movement helpers
        // ================================================================= //

        private void DriveHorizontal()
        {
            float target = TargetSpeed();
            float vx = Mathf.MoveTowards(_rb.velocity.x, target, config.acceleration * Time.fixedDeltaTime);
            _rb.velocity = new Vector2(vx, _rb.velocity.y);
            CurrentSpeed = vx;
        }

        private float TargetSpeed()
        {
            float ramped = config.runSpeed + config.speedRampPerSecond * _runTime;
            return Mathf.Min(ramped, config.maxRunSpeed) * _speedBoost;
        }

        private void ApplyGravity()
        {
            if (_rb.isKinematic) return;

            switch (State)
            {
                case PlayerState.LedgeGrab:
                    _rb.gravityScale = 0f;
                    return;
                case PlayerState.WallRunning:
                    _rb.gravityScale = 0f;   // wall run drives velocity itself
                    return;
            }

            _rb.gravityScale = _rb.velocity.y > 0f ? config.riseGravity : config.fallGravity;

            if (_rb.velocity.y < -config.maxFallSpeed)
                _rb.velocity = new Vector2(_rb.velocity.x, -config.maxFallSpeed);
        }

        /// <summary>
        /// Ends the run when the runner is stuck against something - and, first,
        /// shows them that it is happening.
        ///
        /// This deliberately tests actual speed rather than "is there a wall in
        /// front", because plenty of things block the runner that the wall probe
        /// never sees - a low overhang caught with the head, a crate that was
        /// not vaulted, the underside of a press. Any of those would otherwise
        /// leave the runner grinding against geometry with no way to fail.
        /// </summary>
        private void CheckCrash()
        {
            bool scripted = State == PlayerState.Vaulting
                            || State == PlayerState.LedgeClimb
                            || State == PlayerState.LedgeGrab
                            || State == PlayerState.WallRunning;

            // The speed check needs the runner to have got going first, or the
            // acceleration from a standing start reads as a crash.
            bool blocked = !scripted
                           && _sensors.Grounded
                           && _runTime > 0.5f
                           && _rb.velocity.x < TargetSpeed() * 0.3f;

            // Pressed against it and scrabbling. The run cycle used to keep
            // playing through this whole window, so the runner jogged on the
            // spot into a wall and then died of it with nothing on screen
            // having said why - and the window is short enough that it read as
            // the game killing them at random rather than as a mistake they
            // had a third of a second to undo.
            SetClinging(blocked && Upright);

            if (blocked)
            {
                _blockedTime += Time.fixedDeltaTime;
                if (_blockedTime >= config.wallCrashGrace) Kill(DeathCause.Crushed);
            }
            else
            {
                _blockedTime = 0f;
            }
        }

        /// <summary>True when the clip on screen has nothing left to show - an air
        /// trick has brought the figure round, a landing has taken its weight.
        /// Without a driver there is nothing to wait for.</summary>
        private bool ClipFinished => animator == null || animator.ClipFinished;

        /// <summary>
        /// How the run starts, given where the runner has come from. Out of the
        /// air the legs take the landing first; up off a slide the runner gets
        /// up first. Both are brief and both are handed back to the run by
        /// TickRunning when they finish, while the runner is already moving at
        /// full speed - the state machine never waits for them.
        ///
        /// A hard landing never gets here: <see cref="Land"/> sends it to the
        /// roll, which is its own recovery.
        /// </summary>
        private static PlayerAnim ArrivalAnim(PlayerState from)
        {
            switch (from)
            {
                case PlayerState.Jumping:
                case PlayerState.DoubleJump:
                case PlayerState.Falling:
                    return PlayerAnim.Land;

                case PlayerState.Sliding:
                    return PlayerAnim.GetUp;

                default:
                    return PlayerAnim.Run;
            }
        }

        /// <summary>States the runner is stuck on their feet in, rather than
        /// along the ground - the only ones the climb is drawn from.</summary>
        private bool Upright => State == PlayerState.Running || State == PlayerState.Idle;

        /// <summary>
        /// Paints the climb over the state's own animation while the runner is
        /// stuck against something, and hands the slot back when they are not.
        ///
        /// <b>The state machine is untouched by this - it is the picture, not
        /// the move.</b> The runner is still Running, still driving into the
        /// wall, still killed by <see cref="CheckCrash"/> when the grace runs
        /// out, and the swipe up that gets them out of it works exactly as it
        /// did. All that changes is that the pose now matches the situation:
        /// the same frames that carry a wall run, so a scramble that turns into
        /// one is continuous rather than a cut.
        ///
        /// Only from the upright states. Sliding into a dead end still ends the
        /// run, but a figure lying flat along the ground does not climb, and
        /// drawing them doing it looks worse than the thing this fixes.
        ///
        /// Death is not one of the states this can reach - FixedUpdate returns
        /// before CheckCrash once the runner is dead - and <see cref="PlayAnim"/>
        /// drops the cling on the way into any new state, so the death pose is
        /// never climbed over.
        /// </summary>
        private void SetClinging(bool clinging)
        {
            if (_clinging == clinging) return;

            _clinging = clinging;
            if (animator != null) animator.Play(clinging ? PlayerAnim.Climb : _stateAnim);
        }

        /// <summary>
        /// Distance from the rigidbody origin down to the soles. Lets vault and
        /// climb targets be expressed as "put the feet on this surface",
        /// whatever pivot the sprite happens to use.
        /// </summary>
        private float FeetOffset => _rb.position.y - _capsule.bounds.min.y;

        private static Vector2 ArcLerp(Vector2 from, Vector2 to, float arcHeight, float t)
        {
            Vector2 flat = Vector2.Lerp(from, to, t);
            flat.y += arcHeight * Mathf.Sin(t * Mathf.PI);   // 0 at both ends, peak in the middle
            return flat;
        }

        // ---- Collider resizing -------------------------------------------- //

        private void SetColliderFraction(float fraction)
        {
            float height = _standSize.y * fraction;
            float bottomOffset = _standOffset.y - _standSize.y * 0.5f;

            _capsule.size = new Vector2(_standSize.x, height);
            _capsule.offset = new Vector2(_standOffset.x, bottomOffset + height * 0.5f);
        }

        private void RestoreCollider()
        {
            _capsule.size = _standSize;
            _capsule.offset = _standOffset;
        }

        private void IgnoreCollider(Collider2D other)
        {
            ClearIgnoredCollider();
            if (other == null) return;
            _ignoredCollider = other;
            Physics2D.IgnoreCollision(_capsule, other, true);
        }

        private void ClearIgnoredCollider()
        {
            if (_ignoredCollider == null) return;
            Physics2D.IgnoreCollision(_capsule, _ignoredCollider, false);
            _ignoredCollider = null;
        }

        // ---- Input shorthands ---------------------------------------------- //

        private bool ConsumeUp()    => Input_ != null && Input_.Consume(Swipe.Up);
        private bool ConsumeDown()  => Input_ != null && Input_.Consume(Swipe.Down);
        private bool ConsumeRight() => Input_ != null && Input_.Consume(Swipe.Right);
        private bool ConsumeLeft()  => Input_ != null && Input_.Consume(Swipe.Left);

        // ================================================================= //
        // Death / finish
        // ================================================================= //

        public void Kill(DeathCause cause)
        {
            if (State == PlayerState.Dead) return;

            SetState(PlayerState.Dead);
            ControlEnabled = false;

            _rb.isKinematic = false;
            RestoreCollider();
            ClearIgnoredCollider();

            if (cause == DeathCause.Fell)
            {
                // Out of the bottom of the screen: carry on falling, tumbling.
                _rb.velocity = new Vector2(_rb.velocity.x * 0.2f, _rb.velocity.y);
                PlayAnim(PlayerAnim.DeathFall);
            }
            else
            {
                // Hit something: thrown back off it and brought to rest. The
                // death frames show the runner flung backwards, and the body
                // carrying on forwards underneath them read as sliding on ice.
                _rb.velocity = new Vector2(-knockbackSpeed, _rb.velocity.y);
                _rb.drag = deathDrag;
            }

            OnDied?.Invoke(cause);
        }

        /// <summary>Called by the finish line - coast to a stop, do not kill.</summary>
        public void Finish()
        {
            ControlEnabled = false;
            _rb.velocity = new Vector2(0f, _rb.velocity.y);
            if (State != PlayerState.Dead) SetState(PlayerState.Idle);
        }

        /// <summary>Bounce pads and springs call this.</summary>
        public void Launch(Vector2 velocity)
        {
            if (State == PlayerState.Dead) return;
            _rb.isKinematic = false;
            _rb.velocity = new Vector2(_rb.velocity.x, velocity.y);
            if (velocity.x != 0f) _rb.velocity = velocity;
            _usedDoubleJump = false;
            SetState(PlayerState.Jumping);
        }

        /// <summary>Wind zones and conveyor belts nudge the runner.</summary>
        public void AddExternalForce(Vector2 force)
        {
            if (State == PlayerState.Dead || _rb.isKinematic) return;
            _rb.velocity += force * Time.fixedDeltaTime;
        }

        private void PlayAnim(PlayerAnim anim)
        {
            // Remembered so a cling can hand the slot back afterwards, and
            // cleared because a state change outranks something that was only
            // ever painted over the top of the state it is leaving.
            _stateAnim = anim;
            _clinging = false;

            if (animator != null) animator.Play(anim);
        }
    }
}
