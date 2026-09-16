using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Placeholder crocodile: patrols a stretch of the pit, turning to face the
    /// way it is going, and opens its jaw slowly before snapping it shut.
    ///
    /// Stands in until there is a drawn crocodile. Scenery only - the Hazard on
    /// the pit is what kills.
    /// </summary>
    public class CrocodileSnap : MonoBehaviour
    {
        [Tooltip("The upper jaw, on a pivot at its hinge, drawn pointing forward (+x).")]
        [SerializeField] private Transform jaw;

        [SerializeField] private float openAngle = 38f;
        [SerializeField] private float snapPeriod = 1.8f;

        [Tooltip("How far either side of where it was placed it swims.")]
        [SerializeField] private float patrol = 1.5f;
        [SerializeField] private float patrolSpeed = 0.5f;

        [Tooltip("Offsets its rhythm, so several in one pit do not move as one.")]
        [SerializeField] private float phase;

        private Vector3 _home;
        private float _facing = 1f;

        private void Awake() => _home = transform.localPosition;

        private void Update()
        {
            // Opens over most of the cycle, snaps shut over the last fifth.
            float cycle = Mathf.Repeat(Time.time / Mathf.Max(0.1f, snapPeriod) + phase, 1f);
            float open = cycle < 0.8f
                ? Mathf.SmoothStep(0f, 1f, cycle / 0.8f)
                : 1f - (cycle - 0.8f) / 0.2f;

            if (jaw != null) jaw.localRotation = Quaternion.Euler(0f, 0f, open * openAngle);

            float swim = (Time.time + phase * 7f) * patrolSpeed;
            transform.localPosition = _home + Vector3.right * (Mathf.Sin(swim) * patrol);

            float heading = Mathf.Cos(swim);
            if (Mathf.Abs(heading) > 0.05f) _facing = Mathf.Sign(heading);

            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * _facing;
            transform.localScale = scale;
        }
    }
}
