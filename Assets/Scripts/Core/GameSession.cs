namespace ArvinRunner
{
    /// <summary>
    /// The handful of things that need to survive a scene change - which is
    /// really just "which level did the menu ask for". Static rather than a
    /// DontDestroyOnLoad object, because there is nothing here worth keeping a
    /// GameObject alive for.
    /// </summary>
    public static class GameSession
    {
        /// <summary>Scene names, so nothing depends on build indices.</summary>
        public const string MainMenuScene = "MainMenu";
        public const string GameScene = "Game";

        /// <summary>
        /// Level the player picked, or -1 to carry on from saved progress.
        /// Consumed by the GameManager when the Game scene loads.
        /// </summary>
        public static int RequestedLevel = -1;

        /// <summary>Reads the request and resets it, so a reload does not repeat it.</summary>
        public static int ConsumeRequestedLevel()
        {
            int requested = RequestedLevel;
            RequestedLevel = -1;
            return requested >= 0 ? requested : SaveData.UnlockedLevelIndex;
        }
    }
}
