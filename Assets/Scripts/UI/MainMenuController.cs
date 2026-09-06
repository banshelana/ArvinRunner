using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArvinRunner
{
    /// <summary>
    /// The front end: play, pick a level, wipe progress, quit.
    ///
    /// The level grid is built at runtime from the campaign asset, so adding a
    /// level to <see cref="LevelSet"/> puts a button here with no extra work.
    /// Locked levels stay visible but disabled - seeing what is ahead is part of
    /// the pull.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private LevelSet campaign;

        [Header("Panels")]
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject levelsPanel;

        [Header("Home")]
        [SerializeField] private Button playButton;
        [SerializeField] private Text playLabel;
        [SerializeField] private Button levelsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Text totalPickupsText;

        [Header("Levels")]
        [SerializeField] private RectTransform levelGrid;
        [SerializeField] private Button levelButtonTemplate;
        [SerializeField] private Button backButton;
        [SerializeField] private Button resetProgressButton;

        private readonly List<Button> _levelButtons = new List<Button>();

        private void Start()
        {
            if (playButton != null) playButton.onClick.AddListener(PlayFromProgress);
            if (levelsButton != null) levelsButton.onClick.AddListener(() => ShowLevels(true));
            if (backButton != null) backButton.onClick.AddListener(() => ShowLevels(false));
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            if (resetProgressButton != null) resetProgressButton.onClick.AddListener(ResetProgress);

            // The menu can be reached mid-run from the pause screen, which may
            // have left time stopped.
            Time.timeScale = 1f;

            BuildLevelGrid();
            Refresh();
            ShowLevels(false);
        }

        // ---------------------------------------------------------------- //

        private void Refresh()
        {
            int unlocked = SaveData.UnlockedLevelIndex;

            if (playLabel != null)
            {
                LevelDefinition next = campaign != null ? campaign.Get(unlocked) : null;
                playLabel.text = next != null && unlocked > 0
                    ? "Continue - " + next.displayName
                    : "Play";
            }

            if (totalPickupsText != null)
                totalPickupsText.text = SaveData.TotalPickups + " collected";

            RefreshLevelButtons();
        }

        private void BuildLevelGrid()
        {
            if (levelGrid == null || levelButtonTemplate == null || campaign == null) return;

            levelButtonTemplate.gameObject.SetActive(false);

            for (int i = 0; i < campaign.Count; i++)
            {
                LevelDefinition level = campaign.Get(i);
                if (level == null) continue;

                Button button = Instantiate(levelButtonTemplate, levelGrid);
                button.gameObject.SetActive(true);
                button.name = "Level_" + (i + 1);

                int index = i;   // captured per iteration, not shared
                button.onClick.AddListener(() => PlayLevel(index));

                _levelButtons.Add(button);
            }
        }

        private void RefreshLevelButtons()
        {
            for (int i = 0; i < _levelButtons.Count; i++)
            {
                Button button = _levelButtons[i];
                if (button == null) continue;

                bool unlocked = SaveData.IsUnlocked(i);
                button.interactable = unlocked;

                LevelDefinition level = campaign.Get(i);
                Text[] labels = button.GetComponentsInChildren<Text>(true);
                if (labels.Length == 0) continue;

                labels[0].text = unlocked ? (i + 1).ToString() : "-";

                if (labels.Length > 1)
                {
                    float best = SaveData.GetBestTime(i);
                    labels[1].text = !unlocked ? "Locked"
                                   : best > 0f ? FormatTime(best)
                                   : level != null ? level.displayName : "";
                }
            }
        }

        // ---------------------------------------------------------------- //

        private void PlayFromProgress()
        {
            GameSession.RequestedLevel = -1;   // -1 means carry on from the save
            SceneManager.LoadScene(GameSession.GameScene);
        }

        private void PlayLevel(int index)
        {
            if (!SaveData.IsUnlocked(index)) return;

            GameSession.RequestedLevel = index;
            SceneManager.LoadScene(GameSession.GameScene);
        }

        private void ShowLevels(bool show)
        {
            if (show) RefreshLevelButtons();
            if (homePanel != null) homePanel.SetActive(!show);
            if (levelsPanel != null) levelsPanel.SetActive(show);
        }

        private void ResetProgress()
        {
            SaveData.ResetAll();
            Refresh();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            return $"{minutes:00}:{seconds - minutes * 60f:00.00}";
        }
    }
}
