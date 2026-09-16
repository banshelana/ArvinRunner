using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Someone minding their own business until the runner goes over their head.
    ///
    /// Three clips, each a hand-picked list of drawings rather than a run of the
    /// folder, because the artist's file numbers are not the order the moment
    /// happens in:
    ///
    ///  * <b>Idle</b> - looped until the runner arrives.
    ///  * <b>Startle</b> - played once, the moment the runner is over them.
    ///  * <b>After</b> - looped from then on; they do not go back to what they were
    ///    doing with someone having just leapt over their head.
    ///
    /// The drawings carry the pose. What they cannot carry is the jolt - the whole
    /// body jumping off the seat in the instant of the fright - so that is added
    /// here as a short hop of the picture, up and settling back.
    ///
    /// The startle is keyed to where the runner is, not to a trigger collider: it
    /// has to go off for a runner passing over the top, and a trigger only knows
    /// about contact. A runner who crashes into them, or dies close by, sets it off
    /// too.
    ///
    /// This is scenery with a reaction. What stops the runner is the solid box on
    /// the obstacle itself; nothing here decides life or death.
    /// </summary>
    public class StartledBystander : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;

        [Header("Clips")]
        [SerializeField] private Sprite[] idle;
        [SerializeField] private float idleFps = 5f;

        [SerializeField] private Sprite[] startle;
        [SerializeField] private float startleFps = 10f;

        [SerializeField] private Sprite[] after;
        [SerializeField] private float afterFps = 6f;

        [Header("Trigger")]
        [Tooltip("Height of the top of their head above the ground they sit on.")]
        [SerializeField] private float headHeight = 1.7f;

        [Tooltip("How far either side of them counts as over their head.")]
        [SerializeField] private float overHeadReach = 1.2f;

        [Tooltip("A runner who dies this close sets them off as well.")]
        [SerializeField] private float scareDistance = 3f;

        [Header("Jolt")]
        [Tooltip("How far the picture jumps off the seat at the fright.")]
        [SerializeField] private float hopHeight = 0.09f;

        [Tooltip("Seconds from the jump to settling back on the seat.")]
        [SerializeField] private float hopTime = 0.28f;

        private enum Mood { Idle, Startled, After }

        private Mood _mood = Mood.Idle;
        private float _clock;
        private float _sinceStartle = -1f;
        private Vector3 _rest;
        private PlayerController _player;

        /// <summary>True once they have seen the runner go over.</summary>
        public bool Startled => _mood != Mood.Idle;

        private void Awake()
        {
            if (body == null) body = GetComponentInChildren<SpriteRenderer>();
            if (body != null) _rest = body.transform.localPosition;

            // Two of them in one level should not fidget in step.
            _clock = Random.value * 2f;
            Show(idle, idleFps, true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _clock += dt;

            switch (_mood)
            {
                case Mood.Idle:
                    if (!ShouldStartle())
                    {
                        Show(idle, idleFps, true);
                        break;
                    }

                    _mood = Mood.Startled;
                    _clock = 0f;
                    _sinceStartle = 0f;
                    Show(startle, startleFps, false);
                    break;

                case Mood.Startled:
                    if (startle == null || _clock * startleFps >= startle.Length)
                    {
                        _mood = Mood.After;
                        _clock = 0f;
                        Show(after, afterFps, true);
                        break;
                    }

                    Show(startle, startleFps, false);
                    break;

                default:
                    Show(after, afterFps, true);
                    break;
            }

            Hop(dt);
        }

        /// <summary>Up fast and down a little slower, the way a body drops back onto a seat.</summary>
        private void Hop(float dt)
        {
            if (body == null || _sinceStartle < 0f) return;

            _sinceStartle += dt;
            float t = _sinceStartle / Mathf.Max(0.01f, hopTime);

            if (t >= 1f)
            {
                body.transform.localPosition = _rest;
                _sinceStartle = -1f;
                return;
            }

            float lift = t < 0.35f
                ? Mathf.Sin(t / 0.35f * Mathf.PI * 0.5f)
                : Mathf.Cos((t - 0.35f) / 0.65f * Mathf.PI * 0.5f);

            body.transform.localPosition = _rest + Vector3.up * (lift * hopHeight);
        }

        private bool ShouldStartle()
        {
            PlayerController player = Player();
            if (player == null) return false;

            Vector3 at = player.transform.position;
            float across = at.x - transform.position.x;

            if (!player.IsAlive) return Mathf.Abs(across) < scareDistance;

            // The runner's transform is at their feet, so this is feet over head -
            // a jump over, or a runner who landed on top of them.
            return Mathf.Abs(across) < overHeadReach &&
                   at.y - transform.position.y > headHeight - 0.3f;
        }

        private void Show(Sprite[] clip, float fps, bool looping)
        {
            if (body == null || clip == null || clip.Length == 0) return;

            int frame = Mathf.FloorToInt(_clock * Mathf.Max(0.1f, fps));
            frame = looping ? (int)Mathf.Repeat(frame, clip.Length) : Mathf.Min(frame, clip.Length - 1);

            Sprite sprite = clip[frame];
            if (sprite != null && body.sprite != sprite) body.sprite = sprite;
        }

        private PlayerController Player()
        {
            if (_player != null) return _player;

            _player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (_player == null) _player = FindObjectOfType<PlayerController>();

            return _player;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position;

            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.8f);
            Gizmos.DrawLine(origin + new Vector3(-overHeadReach, headHeight, 0f),
                            origin + new Vector3(overHeadReach, headHeight, 0f));
        }
    }
}
