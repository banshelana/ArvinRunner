using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// The helicopter's one job: come in over the runner, fire once at a marked
    /// point on the ground, and carry on out of the level.
    ///
    /// The aircraft itself is only the delivery. It flies too high to be jumped
    /// into in normal play - the top of a jump puts the runner's head around
    /// 4.95, and it cruises above that - and what the player actually has to
    /// clear is the fire it leaves behind. Touching it still kills, for anyone
    /// who finds a way up.
    ///
    /// <b>It fires off the runner's arrival, not its own position.</b> Firing at
    /// a fixed point in its flight would put the blast on the ground at a moment
    /// decided by the helicopter's speed, and the runner's speed ramps from 9 to
    /// 15 over a level - so the same chunk would be a fair read early on and an
    /// unreachable or already-spent blast later. Leading by the runner's own time
    /// to the target means the warning always ends just as they get there, at any
    /// speed.
    /// </summary>
    [RequireComponent(typeof(MovingObstacle))]
    public class HelicopterStrike : MonoBehaviour
    {
        [Tooltip("The strike this one sets off. Authored into the chunk at the " +
                 "point it lands, so the target is laid out against the ground.")]
        [SerializeField] private MissileStrike strike;

        [Tooltip("Where the missile leaves from. Falls back to the aircraft itself.")]
        [SerializeField] private Transform muzzle;

        [Header("Frames")]
        [Tooltip("The frames looped while it comes in - the cruise and the dip " +
                 "onto its firing line.")]
        [SerializeField] private int approachFirst;
        [SerializeField] private int approachLast = 1;

        [Tooltip("The frames played once as it shoots: the launch, the missile " +
                 "clearing the rail, and the pull-up. Held on the last one.")]
        [SerializeField] private int fireFirst = 2;
        [SerializeField] private int fireLast = 5;

        [Tooltip("Extra lead on top of the strike's own warning, so the fire is " +
                 "alight a fraction before the runner reaches it rather than after.")]
        [SerializeField] private float extraLead = 0.15f;

        private MovingObstacle _travel;
        private SpriteFlipbook _flipbook;
        private PlayerController _player;
        private bool _fired;

        private void Awake()
        {
            _travel = GetComponent<MovingObstacle>();
            _flipbook = GetComponentInChildren<SpriteFlipbook>();
        }

        private void Start()
        {
            // Loop the approach until there is a shot to take. Done in Start so
            // the flipbook has had its own Awake and knows how many frames it has.
            if (_flipbook != null) _flipbook.PlayRange(approachFirst, approachLast, looping: true);
        }

        private void Update()
        {
            if (_fired || strike == null || !_travel.Moving) return;

            PlayerController player = Player();
            if (player == null) return;

            float toTarget = strike.transform.position.x - player.transform.position.x;

            // Already past it - there is no shot left to take that could be
            // jumped, so hold fire rather than drop one behind them.
            if (toTarget <= 0f) return;

            float speed = Mathf.Max(1f, player.CurrentSpeed);
            float arrival = toTarget / speed;

            if (arrival > strike.WarnDuration + extraLead) return;

            Fire();
        }

        private void Fire()
        {
            _fired = true;

            // The launch is drawn into the frames, so the aircraft tells that
            // half of the story and the ground tells the other. Played once and
            // held on the recovery pose, because it never fires twice.
            if (_flipbook != null) _flipbook.PlayRange(fireFirst, fireLast, looping: false);

            strike.Launch(muzzle != null ? muzzle.position : transform.position);
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
            if (strike == null) return;

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.7f);
            Gizmos.DrawLine(muzzle != null ? muzzle.position : transform.position,
                            strike.transform.position);
        }
    }
}
