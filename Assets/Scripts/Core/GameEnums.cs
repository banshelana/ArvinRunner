namespace ArvinRunner
{
    /// <summary>High level flow of a play session.</summary>
    public enum GameState
    {
        Ready,      // level built, waiting for the first input
        Running,    // normal play
        Dead,       // player hit a hazard / fell
        Finished,   // player crossed the finish line
        Paused
    }

    /// <summary>Every distinct movement state the player can be in.</summary>
    public enum PlayerState
    {
        Idle,
        Running,
        Jumping,      // rising after a ground jump
        DoubleJump,   // rising after the air jump
        Falling,
        Sliding,
        Rolling,      // hard-landing recovery / dive roll
        Vaulting,     // going over a low obstacle
        WallRunning,
        LedgeGrab,
        LedgeClimb,
        Dead
    }

    /// <summary>
    /// Animation slots the artist needs to fill. Kept separate from PlayerState
    /// so several states can share one clip - and so one state can offer more
    /// than one slot, which is what LowFlip is for.
    ///
    /// Add new slots at the end. PlayerAnimatorDriver passes the enum's integer
    /// value to the optional Unity Animator as its "State" parameter, so
    /// inserting one in the middle would silently renumber every slot after it.
    /// </summary>
    public enum PlayerAnim
    {
        Idle,
        Run,
        JumpRise,
        JumpFall,
        DoubleJump,
        Slide,
        Roll,
        Vault,
        WallRun,
        LedgeGrab,
        LedgeClimb,
        Death,

        /// <summary>A ground jump taken over something small. Same state as
        /// JumpRise, picked instead of it when the sensors see a low obstacle.</summary>
        LowFlip,

        /// <summary>The charged super jump. Also the Jumping state, and also
        /// chosen instead of JumpRise - the power move gets the somersault
        /// every time rather than drawing for it.</summary>
        SuperJump,

        /// <summary>
        /// Scrabbling at whatever has just stopped the run dead.
        ///
        /// Not a state: the runner is still Running as far as the state machine
        /// is concerned, and this is painted over the top of that by
        /// PlayerController.SetClinging for as long as they are stuck. The three
        /// deliberate wall states have their own slots - WallRun, LedgeGrab and
        /// LedgeClimb - and are drawn from the same folder.
        /// </summary>
        Climb,

        /// <summary>
        /// Taking the weight on touching down. Painted over Running for the
        /// moment it lasts and then handed back to the run - see
        /// PlayerController.ArrivalAnim. A jump clip can name its own landing,
        /// so a somersault lands out of the somersault; this slot is the one
        /// used when it does not.
        /// </summary>
        Land,

        /// <summary>Coming up off the ground at the end of a slide, handed back
        /// to the run the same way the landing is.</summary>
        GetUp,

        /// <summary>Dying by falling out of the level. Death is being stopped by
        /// something - hit, thrown back, down on the knees - which is wrong for a
        /// body dropping off the bottom of the screen.</summary>
        DeathFall
    }

    /// <summary>What killed the player - used for the death message / VFX.</summary>
    public enum DeathCause
    {
        Hazard,
        Fell,
        Crushed
    }
}
