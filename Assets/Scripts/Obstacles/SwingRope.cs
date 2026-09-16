using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A rope hanging from a beam. Caught on the way past, it swings the runner
    /// forward and lets them go at <see cref="releaseAngle"/>, up and across.
    ///
    /// <b>The swing is authored, not simulated.</b> A real pendulum would carry
    /// the runner as far as the speed they caught it at, which ramps from 9 to 11
    /// over a level and depends on how they jumped - so the same rope would land
    /// them short one time and long the next. Here the ride always ends at the
    /// same angle with the same velocity, wherever in its reach the rope was
    /// caught, and the pit it crosses can be laid out against one landing.
    ///
    /// Left alone it is simulated: a pendulum settling toward a slow sway, and
    /// swinging on past the release before it settles, so it never looks pinned.
    /// </summary>
    public class SwingRope : GrabPoint
    {
        [Tooltip("The rope: a thin tiled sprite, stretched from the anchor to the grip every frame. " +
                 "The anchor is this object's position.")]
        [SerializeField] private SpriteRenderer line;

        [Tooltip("The knot the runner holds, kept on the end of the rope.")]
        [SerializeField] private Transform knot;

        [SerializeField] private float length = 4f;

        [Header("Swing")]
        [Tooltip("Degrees forward of straight down at which the runner lets go.")]
        [SerializeField] private float releaseAngle = 45f;

        [Tooltip("How fast the rope carries the runner, radians per second.")]
        [SerializeField] private float angularSpeed = 2.3f;

        [Tooltip("The catch angle is clamped into this range, so a grab well behind or " +
                 "ahead of the rope still swings forward the same way.")]
        [SerializeField] private float earliestCatch = -35f;
        [SerializeField] private float latestCatch = 20f;

        [Tooltip("Speed along the swing at the moment of letting go. At 45 degrees, " +
                 "14.1 is 10 forward and 10 up.")]
        [SerializeField] private float releaseSpeed = 14.1f;

        [Header("At rest")]
        [Tooltip("Degrees either side it drifts while nobody is on it.")]
        [SerializeField] private float sway = 4f;
        [SerializeField] private float swayPeriod = 2.8f;

        [Tooltip("How quickly a swing dies away once the runner has let go.")]
        [SerializeField] private float damping = 1.4f;

        // Degrees from straight down, positive forward (+x).
        private float _angle;
        private float _angularVelocity;

        private bool _riding;
        private float _from;
        private float _duration;
        private Vector2 _catchHand;
        private float _thickness = 0.1f;
        private float _swayOffset;

        public override bool Available => !_riding;
        public override Vector2 Grip => Point(_angle);

        public override Vector2 ReleaseVelocity
        {
            get
            {
                float r = releaseAngle * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * releaseSpeed;
            }
        }

        private void Awake()
        {
            if (line != null) _thickness = line.size.x;
            _swayOffset = Random.value * swayPeriod;
            Draw(Grip);
        }

        public override float BeginRide(Vector2 hand)
        {
            Vector2 fromAnchor = hand - (Vector2)transform.position;

            _from = Mathf.Clamp(Mathf.Atan2(fromAnchor.x, -fromAnchor.y) * Mathf.Rad2Deg,
                                earliestCatch, latestCatch);
            _angle = _from;
            _catchHand = hand;
            _riding = true;

            _duration = Mathf.Max(0.25f, Mathf.Abs(releaseAngle - _from) * Mathf.Deg2Rad /
                                         Mathf.Max(0.1f, angularSpeed));
            return _duration;
        }

        public override Vector2 RideGrip(float time)
        {
            float u = Mathf.Clamp01(time / _duration);
            _angle = Mathf.Lerp(_from, releaseAngle, u);

            Vector2 grip = Vector2.Lerp(_catchHand, Point(_angle), Mathf.Clamp01(time / CatchBlend));
            Draw(grip);
            return grip;
        }

        public override void EndRide()
        {
            _riding = false;

            // On past the release a little, and back: a rope does not stop dead
            // the moment it is let go.
            _angularVelocity = (releaseAngle - _from) / Mathf.Max(0.01f, _duration) * 0.5f;
        }

        private void Update()
        {
            if (_riding) return;

            float dt = Time.deltaTime;
            float rest = sway * Mathf.Sin((Time.time + _swayOffset) / Mathf.Max(0.1f, swayPeriod) * Mathf.PI * 2f);

            // A pendulum pulled toward its sway, losing energy as it goes.
            float gravity = Mathf.Abs(Physics2D.gravity.y);
            float pull = -(gravity / Mathf.Max(0.5f, length)) *
                         Mathf.Sin((_angle - rest) * Mathf.Deg2Rad) * Mathf.Rad2Deg;

            _angularVelocity += (pull - damping * _angularVelocity) * dt;
            _angle += _angularVelocity * dt;

            Draw(Grip);
        }

        private Vector2 Point(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return (Vector2)transform.position + new Vector2(Mathf.Sin(r), -Mathf.Cos(r)) * length;
        }

        private void Draw(Vector2 grip)
        {
            Vector2 top = transform.position;
            Vector2 along = grip - top;

            if (line != null)
            {
                line.transform.position = (top + grip) * 0.5f;
                line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.x, -along.y) * Mathf.Rad2Deg);
                line.size = new Vector2(_thickness, along.magnitude);
            }

            if (knot != null) knot.position = grip;
        }

        private void OnDrawGizmosSelected()
        {
            // The catch range and the release, so the swing can be laid out
            // against the pit in the scene view.
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);

            for (float a = earliestCatch; a < releaseAngle; a += 5f)
                Gizmos.DrawLine(Point(a), Point(Mathf.Min(a + 5f, releaseAngle)));

            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.DrawLine(transform.position, Point(releaseAngle));
        }
    }
}
