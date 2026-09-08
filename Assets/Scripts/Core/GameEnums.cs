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
        LowFlip
    }

    /// <summary>What killed the player - used for the death message / VFX.</summary>
    public enum DeathCause
    {
        Hazard,
        Fell,
        Crushed
    }
}
