using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArvinRunner
{
    /// <summary>
    /// The whole in-game interface: the run readout, the progress bar, and the
    /// start / death / finish panels.
    ///
    /// Uses uGUI Text rather than TextMeshPro so the scene works without
    /// importing TMP Essentials first. Every field is optional - leave one
    /// empty and that piece is simply skipped.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("Readouts")]
        [SerializeField] private Text timeText;
        [SerializeField] private Text pickupText;
        [SerializeField] private Text levelText;
        [SerializeField] private Image progressFill;
        [SerializeField] private RectTransform progressMarker;
        [SerializeField] private RectTransform progressTrack;

        [Header("Panels")]
        [SerializeField] private GameObject readyPanel;
        [SerializeField] private Text readyHintText;
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private Text deathReasonText;
        [SerializeField] private GameObject finishPanel;
        [SerializeField] private Text finishSummaryText;
        [SerializeField] private GameObject pausePanel;

        [Header("Buttons")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button retryFromFinishButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [Tooltip("Any number of back-to-menu buttons, on any panel.")]
        [SerializeField] private Button[] menuButtons;

        private GameManager _game;

        private void Start()
        {
            _game = GameManager.Instance;
            if (_game == null)
            {
                Debug.LogWarning("[GameHUD] No GameManager in the scene.", this);
                enabled = false;
                return;
            }

            _game.OnLevelStarted += HandleLevelStarted;
            _game.OnPlayerDied += HandleDeath;
            _game.OnLevelCompleted += HandleFinish;
            _game.OnPickupsChanged += HandlePickups;
            _game.OnStateChanged += HandleStateChanged;

            WireButtons();
            ShowOnly(null);

            // The manager builds the first level in its own Start, which may
            // already have run. Sync up rather than waiting for an event that
            // has been and gone.
            if (_game.CurrentLevel != null) HandleLevelStarted(_game.CurrentLevel);
            HandlePickups(_game.Pickups);
        }

        private void OnDestroy()
        {
            if (_game == null) return;

            _game.OnLevelStarted -= HandleLevelStarted;
            _game.OnPlayerDied -= HandleDeath;
            _game.OnLevelCompleted -= HandleFinish;
            _game.OnPickupsChanged -= HandlePickups;
            _game.OnStateChanged -= HandleStateChanged;
        }

        private void WireButtons()
        {
            if (restartButton != null) restartButton.onClick.AddListener(() => _game.RestartLevel());
            if (retryFromFinishButton != null) retryFromFinishButton.onClick.AddListener(() => _game.RestartLevel());
            if (nextLevelButton != null) nextLevelButton.onClick.AddListener(() => _game.NextLevel());
            if (pauseButton != null) pauseButton.onClick.AddListener(() => _game.SetPaused(true));
            if (resumeButton != null) resumeButton.onClick.AddListener(() => _game.SetPaused(false));
            if (menuButtons != null)
                foreach (Button button in menuButtons)
                    if (button != null) button.onClick.AddListener(ReturnToMenu);
        }

        /// <summary>
        /// Back to the front end. Time is restored first because the pause
        /// screen is the most likely place this is pressed from, and a scene
        /// loaded at timeScale zero looks broken.
        /// </summary>
        private static void ReturnToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameSession.MainMenuScene);
        }

        private void Update()
        {
            // Escape goes back to the front end. It is checked before the null
            // guard below so it still works on a scene with no GameManager, and
            // it uses unscaled input rather than anything time-dependent so it
            // works from the pause screen too.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ReturnToMenu();
                return;
            }

            if (_game == null) return;

            if (timeText != null) timeText.text = FormatTime(_game.RunTime);

            float progress = _game.Progress;
            if (progressFill != null) progressFill.fillAmount = progress;

            if (progressMarker != null && progressTrack != null)
            {
                float width = progressTrack.rect.width;
                progressMarker.anchoredPosition = new Vector2(width * progress, progressMarker.anchoredPosition.y);
            }
        }

        // ---------------------------------------------------------------- //

        private void HandleLevelStarted(LevelDefinition level)
        {
            if (levelText != null)
                levelText.text = $"{level.levelNumber}. {level.displayName}";

            if (readyHintText != null)
                readyHintText.text = level.hint;

            ShowOnly(readyPanel);
        }

        private void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Running:
                    ShowOnly(null);
                    break;
                case GameState.Paused:
                    ShowOnly(pausePanel);
                    break;
            }
        }

        private void HandleDeath(DeathCause cause)
        {
            if (deathReasonText != null)
                deathReasonText.text = DescribeDeath(cause);

            ShowOnly(deathPanel);
        }

        private void HandleFinish(float time, bool newRecord)
        {
            if (finishSummaryText != null)
            {
                float best = SaveData.GetBestTime(_game.CurrentLevelIndex);
                string record = newRecord ? "  NEW BEST!" : $"  best {FormatTime(best)}";
                finishSummaryText.text = $"Time {FormatTime(time)}{record}\nPickups {_game.Pickups}";
            }

            if (nextLevelButton != null)
            {
                Text label = nextLevelButton.GetComponentInChildren<Text>();
                if (label != null) label.text = _game.HasNextLevel ? "Next Level" : "Play Again";
            }

            ShowOnly(finishPanel);
        }

        private void HandlePickups(int total)
        {
            if (pickupText != null) pickupText.text = total.ToString();
        }

        // ---------------------------------------------------------------- //

        private void ShowOnly(GameObject panel)
        {
            SetActive(readyPanel, panel == readyPanel);
            SetActive(deathPanel, panel == deathPanel);
            SetActive(finishPanel, panel == finishPanel);
            SetActive(pausePanel, panel == pausePanel);
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }

        private static string FormatTime(float seconds)
        {
            if (seconds <= 0f) return "--:--";
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return $"{minutes:00}:{rest:00.00}";
        }

        private static string DescribeDeath(DeathCause cause)
        {
            switch (cause)
            {
                case DeathCause.Fell:    return "You fell.";
                case DeathCause.Crushed: return "You hit the wall.";
                default:                 return "You did not make it.";
            }
        }
    }
}
