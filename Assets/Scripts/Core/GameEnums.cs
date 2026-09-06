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

    /// <summary>Animation slots the artist needs to fill. Kept separate from
    /// PlayerState so several states can share one clip.</summary>
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
        Death
    }

    /// <summary>What killed the player - used for the death message / VFX.</summary>
    public enum DeathCause
    {
        Hazard,
        Fell,
        Crushed
    }
}
