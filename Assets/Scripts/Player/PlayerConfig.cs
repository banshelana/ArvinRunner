using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Every number that defines how the runner feels, in one asset so you can
    /// tune the game without touching code. Create via
    /// Assets > Create > ArvinRunner > Player Config.
    /// </summary>
    [CreateAssetMenu(menuName = "ArvinRunner/Player Config", fileName = "PlayerConfig")]
    public class PlayerConfig : ScriptableObject
    {
        [Header("Run")]
        [Tooltip("Horizontal speed the runner holds automatically.")]
        public float runSpeed = 9f;
        [Tooltip("Speed gained per second of survival, up to maxRunSpeed. Paced " +
                 "so the ramp spans a whole level rather than topping out in the " +
                 "first twenty seconds and sitting there.")]
        public float speedRampPerSecond = 0.045f;

        [Tooltip("Ceiling on the speed ramp. Held down to what the run art can " +
                 "actually sell - see the run notes in ArvinRunnerSetup. " +
                 "Past about 11 the drawn stride cannot keep up with the ground " +
                 "and the feet visibly skate.")]
        public float maxRunSpeed = 11f;
        [Tooltip("How fast the runner recovers speed after a stumble.")]
        public float acceleration = 30f;

        [Header("Jump")]
        [Tooltip("Peak height of the first jump, in world units.")]
        public float jumpHeight = 3.2f;
        [Tooltip("Peak height of the air jump, measured from where it is used.")]
        public float doubleJumpHeight = 2.6f;
        [Tooltip("Gravity while rising. Lower = floatier.")]
        public float riseGravity = 4.5f;
        [Tooltip("Gravity while falling. Higher than riseGravity gives a snappy arc.")]
        public float fallGravity = 7f;
        public float maxFallSpeed = 25f;
        [Tooltip("Grace period after leaving a ledge where a jump still works.")]
        public float coyoteTime = 0.12f;

        [Header("Slide")]
        public float slideDuration = 0.7f;
        [Tooltip("Speed multiplier applied while sliding.")]
        public float slideSpeedMultiplier = 1.15f;
        [Tooltip("Collider height while sliding, as a fraction of the standing height.")]
        [Range(0.2f, 0.9f)] public float slideHeightFraction = 0.45f;
        [Tooltip("A swipe down in mid-air slams the runner to the ground this fast.")]
        public float airDiveSpeed = 22f;

        [Header("Roll")]
        [Tooltip("Falling faster than this on landing forces a roll.")]
        public float hardLandingSpeed = 16f;
        public float rollDuration = 0.5f;
        [Range(0.2f, 0.9f)] public float rollHeightFraction = 0.5f;

        [Header("Super jump")]
        [Tooltip("Seconds of running to charge one super jump.")]
        public float superJumpChargeTime = 15f;
        [Tooltip("Peak height of the super jump. Around twice the normal jump, " +
                 "so it clears a tower face rather than just a tall crate.")]
        public float superJumpHeight = 6.4f;
        [Tooltip("Speed multiplier carried through the super jump, so it covers " +
                 "ground as well as height. Cleared on landing.")]
        public float superJumpSpeedMultiplier = 1.35f;

        [Header("Vault")]
        [Tooltip("Obstacles no taller than this (above the feet) can be vaulted.")]
        public float maxVaultHeight = 1.6f;
        [Tooltip("How far ahead a swipe up turns into a vault. 1.1 was a tenth of a " +
                 "second at running speed, so vaults almost never happened; further " +
                 "out, the vault is keyed to where the obstacle actually is, so a " +
                 "longer take-off is the natural one.")]
        public float vaultProbeDistance = 2.4f;
        [Tooltip("Only the fallback pace of the vault clip. The vault itself takes " +
                 "as long as running its distance would.")]
        public float vaultDuration = 0.35f;
        [Tooltip("Extra height the arc adds as the body passes over the hands.")]
        public float vaultArcHeight = 0.55f;
        [Tooltip("How far ahead of the body the hands are drawn as they plant. " +
                 "Measured off the handJump art, and what puts the hands on the " +
                 "obstacle rather than in front of it or inside it.")]
        public float vaultHandReach = 0.33f;
        [Tooltip("Obstacles up to this wide are vaulted clean over; wider ones are " +
                 "vaulted up onto.")]
        public float vaultOverWidth = 2.0f;
        [Tooltip("Share of the move after the plant spent with the weight on the hands, " +
                 "before the body arcs over them. 0.4 is the share of the handJump " +
                 "clip its three planted frames take - 11, 10 and 5 - so the hands stay " +
                 "on the obstacle for exactly as long as they are drawn on it.")]
        [Range(0f, 0.9f)] public float vaultSupportShare = 0.4f;

        [Header("Wall run")]
        public bool wallRunEnabled = true;
        public float wallRunDuration = 0.85f;
        [Tooltip("Upward speed held while running up the wall.")]
        public float wallRunSpeed = 7f;
        [Tooltip("Gravity applied during the wall run - decays the climb.")]
        public float wallRunGravity = 6f;
        [Tooltip("How far ahead we look for a wall.")]
        public float wallProbeDistance = 0.45f;
        public Vector2 wallJumpImpulse = new Vector2(6f, 11f);

        [Header("Ledge grab")]
        public bool ledgeGrabEnabled = true;
        [Tooltip("Vertical band near the wall top that counts as a grabbable ledge.")]
        public float ledgeGrabTolerance = 0.6f;
        [Tooltip("How long the climb-up takes once triggered.")]
        public float ledgeClimbDuration = 0.45f;
        [Tooltip("Ledge grab is ignored while rising faster than this.")]
        public float maxLedgeGrabRiseSpeed = 2f;

        [Header("Crash")]
        [Tooltip("Time spent stuck against geometry before the run counts as a " +
                 "crash. The climb animation is paced to exactly this, so the " +
                 "scramble running out is the moment the runner does.")]
        public float wallCrashGrace = 0.35f;

        [Header("Feel")]
        [Tooltip("Seconds of slow motion when the player dies.")]
        public float deathSlowMoDuration = 0.6f;
        [Range(0.05f, 1f)] public float deathTimeScale = 0.25f;

        // ---------------------------------------------------------------- //

        /// <summary>Impulse needed to reach a given height under riseGravity.</summary>
        public float JumpVelocityFor(float height)
        {
            return Mathf.Sqrt(2f * Mathf.Abs(Physics2D.gravity.y) * riseGravity * height);
        }
    }
}
