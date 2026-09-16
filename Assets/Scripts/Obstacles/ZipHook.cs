using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A hook on a trolley that runs down a cable. Caught at the top, it carries
    /// the runner along the cable at a steady speed and drops them off the end
    /// with a small lift, so they leave it rather than slide off it.
    ///
    /// Waits at the start until caught, and is spent once ridden - it stays at
    /// the far end, where it was left.
    /// </summary>
    public class ZipHook : GrabPoint
    {
        [Tooltip("The trolley that runs along the cable. The hook hangs under it.")]
        [SerializeField] private Transform trolley;

        [Tooltip("How far below the trolley the hand closes.")]
        [SerializeField] private float hookDrop = 0.7f;

        [Tooltip("Where the trolley starts and stops, in this object's local space.")]
        [SerializeField] private Vector2 rideFrom;
        [SerializeField] private Vector2 rideTo = new Vector2(12f, -1f);

        [Tooltip("Speed along the cable while carrying the runner.")]
        [SerializeField] private float speed = 12f;

        [Tooltip("Upward speed given on dropping off the end.")]
        [SerializeField] private float releaseLift = 6f;

        [Tooltip("Degrees the hook rocks while it runs.")]
        [SerializeField] private float wobble = 5f;

        private float _t;
        private float _fromT;
        private bool _riding;
        private bool _spent;
        private Vector2 _catchHand;

        public override bool Available => !_riding && !_spent;
        public override string HangClip => "hang_hook";
        public override Vector2 Grip => Trolley(_t) + Vector2.down * hookDrop;

        public override Vector2 ReleaseVelocity
        {
            get
            {
                Vector2 direction = Along.normalized;
                return new Vector2(direction.x * speed, Mathf.Max(0f, direction.y * speed) + releaseLift);
            }
        }

        private Vector2 Along => (Vector2)transform.TransformPoint(rideTo) - (Vector2)transform.TransformPoint(rideFrom);

        private void Awake() => Place();

        public override float BeginRide(Vector2 hand)
        {
            _riding = true;
            _fromT = _t;
            _catchHand = hand;
            return Mathf.Max(0.1f, Along.magnitude * (1f - _t) / Mathf.Max(0.1f, speed));
        }

        public override Vector2 RideGrip(float time)
        {
            float cable = Mathf.Max(0.01f, Along.magnitude);
            _t = Mathf.Min(1f, _fromT + time * speed / cable);
            Place();

            if (trolley != null)
                trolley.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 9f) * wobble);

            return Vector2.Lerp(_catchHand, Grip, Mathf.Clamp01(time / CatchBlend));
        }

        public override void EndRide()
        {
            _riding = false;
            _spent = true;
            if (trolley != null) trolley.localRotation = Quaternion.identity;
        }

        private Vector2 Trolley(float t) =>
            Vector2.Lerp(transform.TransformPoint(rideFrom), transform.TransformPoint(rideTo), t);

        private void Place()
        {
            if (trolley != null) trolley.position = Trolley(_t);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
            Gizmos.DrawLine(transform.TransformPoint(rideFrom), transform.TransformPoint(rideTo));
        }
    }
}
