using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Progress persistence. Thin wrapper over PlayerPrefs - enough for a
    /// runner, and easy to swap for a file or cloud save later.
    /// </summary>
    public static class SaveData
    {
        private const string UnlockedKey = "arvin.unlocked";
        private const string BestTimeKey = "arvin.best.";
        private const string PickupsKey = "arvin.pickups";
        private const string DifficultyKey = "arvin.difficulty";

        /// <summary>Highest level index the player may enter. Level 0 is always open.</summary>
        public static int UnlockedLevelIndex
        {
            get => PlayerPrefs.GetInt(UnlockedKey, 0);
            set
            {
                PlayerPrefs.SetInt(UnlockedKey, Mathf.Max(value, UnlockedLevelIndex));
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// How forgiving the game is. Normal until the player says otherwise, and
        /// remembered between sessions - it is a preference, not progress, so
        /// ResetAll leaves it alone.
        /// </summary>
        public static Difficulty Difficulty
        {
            get => (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, (int)Difficulty.Normal),
                                           0, (int)Difficulty.Hard);
            set { PlayerPrefs.SetInt(DifficultyKey, (int)value); PlayerPrefs.Save(); }
        }

        public static int TotalPickups
        {
            get => PlayerPrefs.GetInt(PickupsKey, 0);
            set { PlayerPrefs.SetInt(PickupsKey, value); PlayerPrefs.Save(); }
        }

        public static bool IsUnlocked(int levelIndex) => levelIndex <= UnlockedLevelIndex;

        /// <summary>Returns the previous best, or 0 if the level has never been cleared.</summary>
        public static float GetBestTime(int levelIndex) => PlayerPrefs.GetFloat(BestTimeKey + levelIndex, 0f);

        /// <summary>Stores the time if it beats the record. Returns true if it was a new best.</summary>
        public static bool SubmitTime(int levelIndex, float seconds)
        {
            float best = GetBestTime(levelIndex);
            if (best > 0f && seconds >= best) return false;

            PlayerPrefs.SetFloat(BestTimeKey + levelIndex, seconds);
            PlayerPrefs.Save();
            return true;
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(UnlockedKey);
            PlayerPrefs.DeleteKey(PickupsKey);
            for (int i = 0; i < 200; i++) PlayerPrefs.DeleteKey(BestTimeKey + i);
            PlayerPrefs.Save();
        }
    }
}
