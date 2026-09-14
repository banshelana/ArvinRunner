using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A press that slams down on a cycle. You slide under it while it waits.
    /// The head should carry a Hazard so a mistimed run is fatal.
    ///
    /// Two ways to show it. With <see cref="frames"/> set, it is the press art
    /// from Art/MovingObstacles/presser: one slam cycle, each frame shown for its
    /// own time, and the head's colliders moved onto the head as each frame draws
    /// it. Without them it is the plain block moved between two heights, which is
    /// also what the press falls back to when no art was found.
    ///
    /// <b>The drawn press runs on the runner's approach, not on the clock</b> -
    /// for the reason <see cref="LaserGate"/> gives. The drawn head comes all the
    /// way down onto the plate, so unlike the old block there is no gap to slide
    /// through while it is down, and on a clock the runner - who cannot stop or
    /// slow down - would arrive at a random point in the cycle: about half of
    /// arrivals could not be survived however well the slide was timed. Keyed to
    /// distance, the press is at the same point of its cycle at the same place on
    /// every attempt, and <see cref="arrivalTime"/> puts the runner under a
    /// waiting head. It still slams over and over as the runner comes in, at
    /// about its drawn speed, and slams just behind a runner who slid through.
    /// </summary>
    public class CrusherPress : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private float travel = 3f;
        [SerializeField] private float slamSpeed = 14f;
        [SerializeField] private float riseSpeed = 3.5f;
        [SerializeField] private float holdDown = 0.35f;
        [SerializeField] private float holdUp = 1.1f;
        [SerializeField, Range(0f, 1f)] private float phase;

        [Header("Drawn press")]
        [SerializeField] private SpriteRenderer body;

        [Tooltip("One cycle of the press, starting with the head waiting at the top.")]
        [SerializeField] private Sprite[] frames;

        [Tooltip("Seconds each frame is shown.")]
        [SerializeField] private float[] frameDurations;

        [Tooltip("Kills: sized and placed onto the head block in each frame.")]
        [SerializeField] private BoxCollider2D headBox;

        [Tooltip("Kills: the piston rod, from the top of the head up to the clamp.")]
        [SerializeField] private BoxCollider2D rodBox;

        [Tooltip("Per frame, the head block's centre and size, relative to the press.")]
        [SerializeField] private Vector2[] headCentres;
        [SerializeField] private Vector2[] headSizes;

        [Tooltip("The rod's left and right edges, relative to the press.")]
        [SerializeField] private Vector2 rodSpan;
        [SerializeField] private float rodTop;

        [Tooltip("Seconds into the cycle at the moment the runner reaches the press.")]
        [SerializeField] private float arrivalTime;

        [Tooltip("World units of the runner's approach per whole cycle. The cycle's " +
                 "length times a typical run speed plays it at about its drawn pace.")]
        [SerializeField] private float wavelength = 23f;

        private Vector3 _topPosition;
        private float _timer;
        private int _stage;   // 0 up, 1 slamming, 2 down, 3 rising

        private float[] _ends;
        private float _cycle;
        private int _shown = -1;
        private Transform _player;

        private bool HasDrawnPress =>
            body != null && frames != null && frameDurations != null && headBox != null &&
            headCentres != null && headSizes != null && frames.Length > 1 &&
            frameDurations.Length == frames.Length && headCentres.Length == frames.Length &&
            headSizes.Length == frames.Length;

        private void Awake()
        {
            if (HasDrawnPress)
            {
                _ends = new float[frames.Length];
                for (int i = 0; i < frames.Length; i++)
                {
                    _cycle += Mathf.Max(0.001f, frameDurations[i]);
                    _ends[i] = _cycle;
                }
                return;
            }

            if (head == null) head = transform;
            _topPosition = head.localPosition;
            _timer = phase * (holdUp + holdDown);
        }

        private void Update()
        {
            if (_ends != null)
            {
                DrawnPress();
                return;
            }

            Vector3 bottom = _topPosition + Vector3.down * travel;

            switch (_stage)
            {
                case 0:
                    _timer += Time.deltaTime;
                    if (_timer >= holdUp) { _timer = 0f; _stage = 1; }
                    break;

                case 1:
                    head.localPosition = Vector3.MoveTowards(head.localPosition, bottom, slamSpeed * Time.deltaTime);
                    if (Vector3.Distance(head.localPosition, bottom) < 0.01f) { _timer = 0f; _stage = 2; }
                    break;

                case 2:
                    _timer += Time.deltaTime;
                    if (_timer >= holdDown) { _timer = 0f; _stage = 3; }
                    break;

                case 3:
                    head.localPosition = Vector3.MoveTowards(head.localPosition, _topPosition, riseSpeed * Time.deltaTime);
                    if (Vector3.Distance(head.localPosition, _topPosition) < 0.01f) { _timer = 0f; _stage = 0; }
                    break;
            }
        }

        /// <summary>
        /// Finds the frame for this point of the cycle, and when it changes, shows
        /// it and moves the colliders onto what it draws. The colliders follow the
        /// drawings exactly, a frame at a time: the head kills where the head is
        /// shown, never ahead of it.
        /// </summary>
        private void DrawnPress()
        {
            float t = Mathf.Repeat(CycleTime(), _cycle);

            int index = 0;
            while (index < _ends.Length - 1 && t >= _ends[index]) index++;

            if (index == _shown) return;
            _shown = index;

            body.sprite = frames[index];

            Vector2 size = headSizes[index];
            headBox.offset = headCentres[index];
            headBox.size = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));

            if (rodBox != null)
            {
                float headTop = headCentres[index].y + size.y * 0.5f;
                float height = Mathf.Max(0.01f, rodTop - headTop);
                rodBox.offset = new Vector2((rodSpan.x + rodSpan.y) * 0.5f, headTop + height * 0.5f);
                rodBox.size = new Vector2(Mathf.Max(0.01f, rodSpan.y - rodSpan.x), height);
            }
        }

        /// <summary>Seconds into the cycle: from how far the runner is from
        /// arriving, or from the clock when there is no runner to measure - a
        /// prefab being looked at on its own.</summary>
        private float CycleTime()
        {
            Transform player = Player();
            if (player == null || wavelength <= 0.01f)
                return Time.time + arrivalTime + phase * _cycle;

            float approach = player.position.x - transform.position.x;
            return arrivalTime + approach / wavelength * _cycle;
        }

        private Transform Player()
        {
            if (_player != null) return _player;

            PlayerController player = GameManager.Instance != null
                ? GameManager.Instance.Player
                : null;

            if (player == null) player = FindObjectOfType<PlayerController>();

            _player = player != null ? player.transform : null;
            return _player;
        }
    }
}
