using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Venom, running the level down behind the runner. When he reaches him he
    /// takes hold of him and drives a spike through him.
    ///
    /// A level-wide rule rather than an obstacle: he is armed by the level
    /// (<see cref="LevelDefinition.chase"/>) and changes what every chunk in it
    /// costs. Without him a crawl or a climb costs time; with him, they cost
    /// ground, and ground is the only thing between the runner and being caught.
    ///
    /// <b>How fast he comes</b> is a share of the runner's own nominal pace -
    /// the speed the runner would be doing now if he had done nothing but run -
    /// so Venom speeds up through the level exactly as the runner does, and a
    /// clean run always pulls away. Only the moves that slow the runner down let
    /// him gain. He is not faster than the runner; he is relentless, which is a
    /// different thing and the one that can be outrun.
    ///
    /// <b>The leash.</b> He is never allowed further behind than
    /// <see cref="LevelDefinition.chaseLeash"/>. Without one a strong start
    /// would bank a lead the rest of the level could never spend, and he would
    /// be gone for good by the second chunk. With it the lead is capped just off
    /// the left of the screen, and every slow move is paid out of the same
    /// fourteen units.
    ///
    /// <b>Fairness is set against the slowest line, not an average one.</b> The
    /// pace, the leash and the chunks in a chased level are chosen together so
    /// that the slowest clean way through - every crawl, every climb - still
    /// never lets him within about five units. That is on screen, which is the
    /// point: he is seen at the moments the runner is slow, and nowhere else.
    ///
    /// <b>His reach is what catches, not his nose.</b> <see cref="Front"/> - the
    /// line the runner has to stay ahead of - is <see cref="reach"/> of the way
    /// across the drawing, out at the claws.
    /// </summary>
    [DefaultExecutionOrder(100)]   // after the camera has moved, so the edge warning is drawn where the camera is
    public class VenomChase : MonoBehaviour
    {
        [Header("Venom")]
        [Tooltip("Carries the drawing. Its origin is where the frames are pinned - under " +
                 "his head, on the ground - and it is what gets moved along the level.")]
        [SerializeField] private Transform body;

        [Tooltip("His run, so it can be held on one frame for the grab.")]
        [SerializeField] private SpriteFlipbook run;

        [Tooltip("How wide the drawing is, in world units.")]
        [SerializeField] private float width = 2.7f;

        [Tooltip("How far in front of the drawing's origin his claws reach, as a share " +
                 "of its width. What actually catches the runner, and it is measured " +
                 "rather than guessed: across the 24 frames his right-most pixel sits " +
                 "0.30 of a crop width ahead of the head he is pinned by (0.26 at his " +
                 "narrowest, 0.35 at full stretch). The mean is used, so the catch " +
                 "happens at the same place whatever his arms are doing.")]
        [SerializeField] private float reach = 0.28f;

        [Tooltip("The frame he holds while he has hold of the runner: the one with his " +
                 "reach at its longest. Only used if there is no catch animation.")]
        [SerializeField] private int grabFrame = 16;

        [Header("The catch")]
        [Tooltip("Carries the catch animation, which is a different drawing of him at a " +
                 "different scale. Swapped in for the run the moment he arrives.")]
        [SerializeField] private Transform grabBody;

        [SerializeField] private SpriteFlipbook grab;

        [Tooltip("How far behind the runner he plants himself for it.")]
        [SerializeField] private float grabOffset = 0.9f;

        [Tooltip("Rate for the catch. 24 frames at 24 is a second, which is about what " +
                 "there is before the death panel covers the scene.")]
        [SerializeField] private float grabFps = 24f;

        [Header("Running")]
        [Tooltip("How far he covers in one turn of the 24 drawings. His legs are paced " +
                 "off this and his real speed, so they keep up with the ground instead " +
                 "of skating over it.")]
        [SerializeField] private float strideDistance = 4.5f;

        [Tooltip("What he runs on. Roofs and tower faces - not the Vaultable decks and " +
                 "crates, or he would climb onto every one of them.")]
        [SerializeField] private LayerMask groundMask = (1 << GameLayers.Ground) | (1 << GameLayers.Wall);

        [SerializeField] private float riseTime = 0.25f;
        [SerializeField] private float fallTime = 0.45f;

        [Header("The kill")]
        [Tooltip("The spike he drives into the runner once he has hold of him.")]
        [SerializeField] private Sprite block;

        [SerializeField] private Color spike = new Color(0.06f, 0.05f, 0.09f);
        [SerializeField] private Color blood = new Color(0.55f, 0.05f, 0.09f, 0.9f);

        [Tooltip("Seconds the spike takes to go in.")]
        [SerializeField] private float strikeTime = 0.12f;

        [Header("Off screen")]
        [SerializeField] private Color warning = new Color(0.62f, 0.58f, 0.72f, 1f);

        [Tooltip("How far past the left edge of the screen he starts to be felt.")]
        [SerializeField] private float warningRange = 6f;

        [SerializeField] private int sortingOrder = 40;

        private bool _armed;
        private float _front;
        private float _pace;
        private float _leash;
        private float _y;
        private float _yVelocity;

        private bool _caught;
        private float _caughtTime;
        private Vector2 _grip;

        private SpriteRenderer _edge;
        private SpriteRenderer _spike;
        private SpriteRenderer _wound;

        // Where along his base the ground is felt for, as shares of his width
        // behind the claws: his back foot, under his hips, and his leading foot.
        // All behind the front, because that is where his legs are - the claws
        // reach out over ground he has not got to yet.
        private static readonly float[] GroundSamples = { -0.75f, -0.45f, -0.15f };

        /// <summary>The line the runner has to stay ahead of, in world x.</summary>
        public float Front => _front;

        /// <summary>True while this level has him in it.</summary>
        public bool Armed => _armed;

        private void Awake()
        {
            _edge = Piece("EdgeWarning", sortingOrder + 3);
            _spike = Piece("Spike", sortingOrder + 1);
            _wound = Piece("Wound", sortingOrder + 2);

            Show(false);
        }

        /// <summary>
        /// Sets him up for a level that has him, a little behind the spawn - on
        /// screen at the start, so the player sees what is behind them before
        /// they are asked to outrun it - and stands him down for one that does not.
        /// </summary>
        public void Arm(LevelDefinition level, Vector3 spawn)
        {
            _armed = level != null && level.chase;
            Show(_armed);
            if (!_armed) return;

            if (body == null)
            {
                Debug.LogError($"[VenomChase] Level '{level.name}' is a chase, but no body is " +
                               "assigned - nothing will be seen behind the runner.", this);
                _armed = false;
                return;
            }

            // The authored numbers, moved a little by the difficulty. Only a
            // little: the margin a chase level is laid out against is a few units
            // wide - see DifficultyTuning.ChasePace.
            Difficulty difficulty = GameManager.Instance != null
                ? GameManager.Instance.Difficulty
                : Difficulty.Normal;

            _pace = Mathf.Max(0f, DifficultyTuning.ChasePace(difficulty, level.chasePace));
            _leash = Mathf.Max(1f, DifficultyTuning.ChaseLeash(difficulty, level.chaseLeash));
            _front = spawn.x - level.chaseStartLag;

            _caught = false;
            _caughtTime = 0f;

            if (_spike != null) _spike.enabled = false;
            if (_wound != null) _wound.enabled = false;
            if (run != null) run.PlayRange(0, int.MaxValue, true);

            body.gameObject.SetActive(true);
            if (grabBody != null) grabBody.gameObject.SetActive(false);

            // Straight onto the ground under him, not eased there from nowhere.
            // The level was instantiated a moment ago and its colliders are not in
            // the physics world until the next step unless they are pushed there.
            Physics2D.SyncTransforms();
            _y = GroundUnder(spawn.y + 20f, spawn.y - 1.5f);
            _yVelocity = 0f;
            Place();
        }

        private void Update()
        {
            if (!_armed) return;

            GameManager game = GameManager.Instance;
            PlayerController player = game != null ? game.Player : null;
            if (player == null) return;

            if (_caught)
            {
                _caughtTime += Time.deltaTime;

                // The drawn catch does its own talking. The spike is only for when
                // there is no catch animation to play.
                if (grabBody == null) Strike();
                return;
            }

            float speed = _pace * NominalSpeed(player, game.RunTime);
            float runnerX = player.transform.position.x;

            float target = GroundUnder(Mathf.Max(_y, player.transform.position.y) + 20f, _y);
            _y = Mathf.SmoothDamp(_y, target, ref _yVelocity, target > _y ? riseTime : fallTime);

            switch (game.State)
            {
                case GameState.Running:
                    _front = Mathf.Max(_front + speed * Time.deltaTime, runnerX - _leash);

                    if (player.IsAlive && _front >= runnerX) Catch(player);
                    break;

                case GameState.Dead:
                    // Whatever killed the runner, he is still coming.
                    _front += speed * Time.deltaTime;
                    break;
            }

            // His legs against the ground he is covering, rather than a fixed rate
            // that is right at one speed and skating at every other.
            if (run != null && strideDistance > 0.01f)
                run.SetRate(24f * speed / strideDistance);
        }

        /// <summary>
        /// He has hold of the runner. The run stops on its longest reach, he holds
        /// station on the body, and the spike goes in.
        /// </summary>
        private void Catch(PlayerController player)
        {
            _caught = true;
            _caughtTime = 0f;

            // Where the spike starts and ends: from his claws into the middle of
            // the runner, taken now, because the runner stops moving from here.
            Vector2 runner = player.transform.position;
            _grip = runner + Vector2.up * 0.85f;
            _front = runner.x;

            if (grabBody != null)
            {
                // He stops being a runner and becomes the thing standing over the
                // body, which is a different drawing at a different scale - so the
                // two are separate objects and one is swapped for the other.
                body.gameObject.SetActive(false);
                grabBody.gameObject.SetActive(true);
                grabBody.position = new Vector3(runner.x - grabOffset, _y, 0f);

                if (grab != null)
                {
                    grab.SetRate(grabFps);
                    grab.PlayRange(0, int.MaxValue, false);
                }
            }
            else if (run != null)
            {
                run.PlayRange(grabFrame, grabFrame, false);
            }

            player.Kill(DeathCause.Caught);
        }

        /// <summary>The spike going in, and the mark it leaves.</summary>
        private void Strike()
        {
            if (_spike == null) return;

            float t = Mathf.Clamp01(_caughtTime / Mathf.Max(0.01f, strikeTime));

            // From his chest, which is a little behind the claws, at about the
            // height of the runner's back.
            Vector2 from = new Vector2(_front - 0.35f, _y + 1.5f);
            Vector2 to = Vector2.Lerp(from, _grip, t);
            Vector2 along = to - from;

            _spike.enabled = true;
            _spike.color = spike;
            _spike.transform.position = (from + to) * 0.5f;
            _spike.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
            _spike.size = new Vector2(Mathf.Max(0.02f, along.magnitude), 0.16f);

            // Only once it is in.
            _wound.enabled = t >= 1f;
            if (!_wound.enabled) return;

            Color c = blood;
            c.a *= Mathf.Clamp01(1f - (_caughtTime - strikeTime) * 0.6f);
            _wound.color = c;
            _wound.transform.position = _grip;
            _wound.size = new Vector2(0.5f + (_caughtTime - strikeTime) * 0.7f, 0.34f);
        }

        /// <summary>
        /// The highest roof under him, felt for from <paramref name="from"/> down.
        /// <paramref name="fallback"/> if there is nothing there at all.
        /// </summary>
        private float GroundUnder(float from, float fallback)
        {
            float best = float.NegativeInfinity;

            foreach (float offset in GroundSamples)
            {
                RaycastHit2D hit = Physics2D.Raycast(new Vector2(_front + offset * width, from),
                                                     Vector2.down, 60f, groundMask);
                if (hit.collider != null) best = Mathf.Max(best, hit.point.y);
            }

            return float.IsNegativeInfinity(best) ? fallback : best;
        }

        /// <summary>
        /// The speed the runner would be doing now if he had only ever run: the
        /// same ramp PlayerController follows, without the slides, crawls and stops
        /// that are the whole reason Venom can gain.
        /// </summary>
        private static float NominalSpeed(PlayerController player, float runTime)
        {
            PlayerConfig config = player.Config;
            if (config == null) return 9f;

            return Mathf.Min(config.runSpeed + config.speedRampPerSecond * runTime, config.maxRunSpeed);
        }

        private void LateUpdate()
        {
            if (!_armed) return;

            Place();

            Camera view = Camera.main;
            if (view == null) return;

            float halfHeight = view.orthographicSize;
            float left = view.transform.position.x - halfHeight * view.aspect;

            // Down the left edge, strengthening as he closes on the screen from
            // behind, and gone once he is on it - by then he can be seen.
            float behind = left - (_front + (1f - reach) * width);
            float warn = behind > 0f ? 1f - Mathf.Clamp01(behind / Mathf.Max(0.1f, warningRange)) : 0f;

            _edge.enabled = warn > 0.01f;
            if (!_edge.enabled) return;

            _edge.transform.position = new Vector3(left + 0.45f, view.transform.position.y, 0f);
            _edge.size = new Vector2(0.9f, halfHeight * 2f + 2f);

            Color c = warning;
            c.a = warn * (0.28f + 0.16f * Mathf.Sin(Time.time * 6.5f));
            _edge.color = c;
        }

        /// <summary>His pinned point, set so his claws are on the front.</summary>
        private void Place()
        {
            if (body != null) body.position = new Vector3(_front - reach * width, _y, 0f);
        }

        private SpriteRenderer Piece(string pieceName, int order)
        {
            var go = new GameObject(pieceName);
            go.transform.SetParent(transform, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = block;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void Show(bool on)
        {
            if (body != null) body.gameObject.SetActive(on);
            if (grabBody != null) grabBody.gameObject.SetActive(false);
            if (_edge != null) _edge.gameObject.SetActive(on);
            if (_spike != null) _spike.gameObject.SetActive(on);
            if (_wound != null) _wound.gameObject.SetActive(on);
        }
    }
}
