using UnityEngine;

namespace ArvinRunner
{
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard
    }

    /// <summary>
    /// What the difficulty setting actually changes.
    ///
    /// <b>It does not change how fast the runner goes.</b> That is the obvious
    /// knob and it is the one thing that cannot be touched here. Every gap,
    /// every rope, every drone in the game is laid out against a run of 9 to 11 -
    /// a jump carries 6.2 units at 9 and 7.5 at 11, and the pits, the window in
    /// the ruin and the belt gaps at the docks are all measured against that.
    /// Slow the runner by a tenth for an easy mode and gaps that were laid out
    /// with a unit to spare stop being clearable at all: "easy" would hand the
    /// player a game they cannot finish. Speed it up and the timed obstacles
    /// tighten in a way nothing was checked against.
    ///
    /// So difficulty moves two things that leave the geometry alone:
    ///
    /// <b>How forgiving the controls are.</b> The coyote time, the swipe buffer
    /// and the grace before a stalled runner counts as crashed are all windows
    /// on the player's own timing. Widening them lets a late swipe still work;
    /// narrowing them asks for the input on the beat. None of it changes where
    /// anything is.
    ///
    /// <b>How quickly the pace ramps.</b> <see cref="PlayerConfig.speedRampPerSecond"/>
    /// only ever adds to the base speed and is capped at maxRunSpeed, so turning
    /// it down never puts the runner below the 9 the levels were built for. It
    /// is the safe half of the speed knob: a long level gets hard more slowly.
    ///
    /// And on the levels that have something chasing, how hard it chases.
    /// </summary>
    public static class DifficultyTuning
    {
        /// <summary>
        /// A runtime copy of the config with the difficulty applied.
        ///
        /// A copy, because PlayerConfig is a shared asset: writing the tuned
        /// numbers into it would edit the project's own data, leave it changed on
        /// disk after a session in the editor, and quietly become the new Normal.
        /// </summary>
        public static PlayerConfig Tune(PlayerConfig source, Difficulty difficulty)
        {
            if (source == null) return null;

            PlayerConfig tuned = Object.Instantiate(source);
            tuned.name = source.name + " (" + difficulty + ")";

            switch (difficulty)
            {
                case Difficulty.Easy:
                    tuned.coyoteTime = 0.22f;
                    tuned.wallCrashGrace = 0.60f;
                    tuned.speedRampPerSecond = source.speedRampPerSecond * 0.55f;

                    // Lands harder before it forces a roll, so an ordinary drop
                    // is run out of rather than recovered from.
                    tuned.hardLandingSpeed = source.hardLandingSpeed * 1.2f;

                    // The power move comes round half as often again.
                    tuned.superJumpChargeTime = source.superJumpChargeTime * 0.7f;
                    break;

                case Difficulty.Hard:
                    tuned.coyoteTime = 0.06f;
                    tuned.wallCrashGrace = 0.22f;
                    tuned.speedRampPerSecond = source.speedRampPerSecond * 1.5f;
                    tuned.hardLandingSpeed = source.hardLandingSpeed * 0.88f;
                    tuned.superJumpChargeTime = source.superJumpChargeTime * 1.35f;
                    break;
            }

            return tuned;
        }

        /// <summary>How long a swipe is remembered for, in seconds.</summary>
        public static float SwipeBuffer(Difficulty difficulty, float normal)
        {
            switch (difficulty)
            {
                case Difficulty.Easy: return normal * 1.7f;
                case Difficulty.Hard: return normal * 0.7f;
                default: return normal;
            }
        }

        /// <summary>
        /// What a chaser's pace becomes.
        ///
        /// Small numbers on purpose. A chase level is laid out so that the
        /// slowest clean line through it still keeps Venom about five units back
        /// - see VenomChase - and that margin is only a few units wide. A
        /// difficulty that moved his pace by a tenth would eat all of it and turn
        /// a measured chase into one nobody can finish.
        /// </summary>
        public static float ChasePace(Difficulty difficulty, float authored)
        {
            switch (difficulty)
            {
                case Difficulty.Easy: return authored * 0.94f;
                case Difficulty.Hard: return authored * 1.04f;
                default: return authored;
            }
        }

        /// <summary>How far back a chaser is allowed to fall, which is how much room there is to lose.</summary>
        public static float ChaseLeash(Difficulty difficulty, float authored)
        {
            switch (difficulty)
            {
                case Difficulty.Easy: return authored + 3f;
                case Difficulty.Hard: return authored - 2f;
                default: return authored;
            }
        }

        /// <summary>For the menu: what each setting is called and what it says it does.</summary>
        public static string Describe(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy: return "more forgiving timing, and the pace builds slowly";
                case Difficulty.Hard: return "tight timing, and the pace builds fast";
                default: return "the game as it was built";
            }
        }
    }
}
