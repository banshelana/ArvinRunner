using System.Collections;
using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Owns the run: builds the level, starts and ends it, tracks progress, and
    /// moves the player through the campaign. Everything else listens to it.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Scene references")]
        [SerializeField] private LevelBuilder builder;
        [SerializeField] private PlayerController player;
        [SerializeField] private CameraFollow cameraFollow;
        [SerializeField] private ParallaxBackground background;

        [Header("Content")]
        [SerializeField] private LevelSet campaign;
        [Tooltip("Forces a level for testing. Leave at -1 so the main menu decides, " +
                 "falling back to saved progress.")]
        [SerializeField] private int startLevelIndex = -1;

        [Header("Flow")]
        [Tooltip("Delay before the death screen appears, so the fall is readable.")]
        [SerializeField] private float deathScreenDelay = 0.9f;
        [SerializeField] private float finishScreenDelay = 1.2f;
        [Tooltip("Start running immediately instead of waiting for the first swipe.")]
        [SerializeField] private bool autoStart;

        // ---- Events ------------------------------------------------------ //
        public event System.Action<GameState> OnStateChanged;
        public event System.Action<LevelDefinition> OnLevelStarted;
        public event System.Action<DeathCause> OnPlayerDied;
        /// <summary>(time, newRecord)</summary>
        public event System.Action<float, bool> OnLevelCompleted;
        public event System.Action<int> OnPickupsChanged;

        public GameState State { get; private set; } = GameState.Ready;
        public int CurrentLevelIndex { get; private set; }
        public LevelDefinition CurrentLevel { get; private set; }
        public int Pickups { get; private set; }
        public float RunTime { get; private set; }
        public bool HasNextLevel => campaign != null && !campaign.IsLastLevel(CurrentLevelIndex);

        /// <summary>How far through the level the runner is, 0 to 1.</summary>
        public float Progress
        {
            get
            {
                if (builder == null || player == null || builder.LevelLength <= 0f) return 0f;
                return Mathf.Clamp01(player.DistanceTravelled / builder.LevelLength);
            }
        }

        private Coroutine _pendingFlow;

        // ================================================================= //

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (!ValidateReferences()) return;

            player.OnDied += HandlePlayerDied;

            int index = startLevelIndex >= 0 ? startLevelIndex : GameSession.ConsumeRequestedLevel();
            LoadLevel(index);
        }

        private bool ValidateReferences()
        {
            if (builder == null) { Debug.LogError("[GameManager] LevelBuilder not assigned.", this); return false; }
            if (player == null) { Debug.LogError("[GameManager] PlayerController not assigned.", this); return false; }
            if (campaign == null || campaign.Count == 0)
            {
                Debug.LogError("[GameManager] No campaign assigned, or it has no levels.", this);
                return false;
            }
            return true;
        }

        private void Update()
        {
            if (State == GameState.Running)
            {
                RunTime += Time.deltaTime;
                return;
            }

            // Waiting on the start line: the first gesture sets the runner off.
            if (State == GameState.Ready && SwipeInput.Instance != null &&
                (SwipeInput.Instance.IsPending(Swipe.Tap) || SwipeInput.Instance.IsPending(Swipe.Up)))
            {
                SwipeInput.Instance.ClearBuffer();
                BeginRun();
            }
        }

        // ================================================================= //
        // Level flow
        // ================================================================= //

        public void LoadLevel(int index)
        {
            if (campaign == null) return;

            index = Mathf.Clamp(index, 0, campaign.Count - 1);
            LevelDefinition level = campaign.Get(index);
            if (level == null)
            {
                Debug.LogError($"[GameManager] Level slot {index} in '{campaign.name}' is empty.", this);
                return;
            }

            StopPendingFlow();
            Time.timeScale = 1f;

            CurrentLevelIndex = index;
            CurrentLevel = level;
            Pickups = 0;
            RunTime = 0f;

            builder.Build(level);

            player.ResetForLevel(builder.SpawnPoint);
            if (cameraFollow != null) cameraFollow.SnapToTarget();
            if (background != null && level.theme != null) background.SetTheme(level.theme);

            SetState(GameState.Ready);
            OnLevelStarted?.Invoke(level);
            OnPickupsChanged?.Invoke(Pickups);

            if (autoStart) BeginRun();
        }

        /// <summary>Take the runner off the start line.</summary>
        public void BeginRun()
        {
            if (State != GameState.Ready) return;

            player.ControlEnabled = true;
            SetState(GameState.Running);
        }

        public void RestartLevel() => LoadLevel(CurrentLevelIndex);

        public void NextLevel()
        {
            if (HasNextLevel) LoadLevel(CurrentLevelIndex + 1);
            else LoadLevel(0);   // campaign finished - loop back to the start
        }

        public void CompleteLevel()
        {
            if (State != GameState.Running) return;

            SetState(GameState.Finished);
            player.Finish();

            bool record = SaveData.SubmitTime(CurrentLevelIndex, RunTime);
            SaveData.UnlockedLevelIndex = CurrentLevelIndex + 1;
            SaveData.TotalPickups += Pickups;

            _pendingFlow = StartCoroutine(AfterDelay(finishScreenDelay,
                () => OnLevelCompleted?.Invoke(RunTime, record)));
        }

        private void HandlePlayerDied(DeathCause cause)
        {
            if (State == GameState.Dead) return;

            SetState(GameState.Dead);
            _pendingFlow = StartCoroutine(DeathSequence(cause));
        }

        private IEnumerator DeathSequence(DeathCause cause)
        {
            PlayerConfig config = player.Config;

            if (config != null && config.deathSlowMoDuration > 0f)
            {
                Time.timeScale = config.deathTimeScale;
                yield return new WaitForSecondsRealtime(config.deathSlowMoDuration);
                Time.timeScale = 1f;
            }

            yield return new WaitForSecondsRealtime(deathScreenDelay);

            OnPlayerDied?.Invoke(cause);
            _pendingFlow = null;
        }

        // ================================================================= //
        // Pause and score
        // ================================================================= //

        public void SetPaused(bool paused)
        {
            if (paused && State == GameState.Running)
            {
                Time.timeScale = 0f;
                SetState(GameState.Paused);
            }
            else if (!paused && State == GameState.Paused)
            {
                Time.timeScale = 1f;
                SetState(GameState.Running);
            }
        }

        public void AddPickup(int amount)
        {
            Pickups += amount;
            OnPickupsChanged?.Invoke(Pickups);
        }

        // ================================================================= //

        private void SetState(GameState next)
        {
            if (State == next) return;
            State = next;
            OnStateChanged?.Invoke(next);
        }

        private void StopPendingFlow()
        {
            if (_pendingFlow != null)
            {
                StopCoroutine(_pendingFlow);
                _pendingFlow = null;
            }
        }

        private IEnumerator AfterDelay(float seconds, System.Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action?.Invoke();
            _pendingFlow = null;
        }
    }
}
