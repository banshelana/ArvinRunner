using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A slow rise and fall for anything holding itself up in the air, and a
    /// jolt that springs back when something knocks it - a bomb let go, a
    /// missile off the rail.
    ///
    /// The bob is two sine waves out of step rather than one, so it never quite
    /// repeats: a single wave reads as a machine on a crank, which is exactly
    /// what a hovering aircraft should not look like.
    ///
    /// Put it on the visual child, never on the object that carries the collider
    /// or the movement: it owns this transform's local position and rotation.
    /// </summary>
    public class HoverBob : MonoBehaviour
    {
        [Tooltip("How far above and below its authored height it drifts.")]
        [SerializeField] private float amplitude = 0.12f;

        [Tooltip("Seconds for one full rise and fall of the main wave.")]
        [SerializeField] private float period = 1.9f;

        [Tooltip("Degrees of nose-down at the bottom of the bob. Keep it small when " +
                 "the frames already draw the aircraft pitching.")]
        [SerializeField] private float tilt = 2f;

        [Header("Jolt")]
        [Tooltip("How hard a jolt is pulled back to rest. Higher settles faster.")]
        [SerializeField] private float stiffness = 30f;

        [Tooltip("How quickly the jolt's wobble dies away.")]
        [SerializeField] private float damping = 6f;

        [Tooltip("Degrees of pitch per unit of upward jolt - a lift raises the nose.")]
        [SerializeField] private float joltPitch = 8f;

        private Vector3 _rest;
        private float _offset;
        private Vector2 _jolt;
        private Vector2 _joltVelocity;

        private void Awake()
        {
            _rest = transform.localPosition;

            // Two of them on screen together should not bob in step.
            _offset = Random.value * 10f;
        }

        /// <summary>Knocks it, as a velocity in world units per second. It springs back on its own.</summary>
        public void Kick(Vector2 velocity) => _joltVelocity += velocity;

        private void Update()
        {
            float dt = Time.deltaTime;

            float phase = (Time.time + _offset) / Mathf.Max(0.05f, period) * Mathf.PI * 2f;
            float s = (Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2.7f + 1.3f)) / 1.35f;

            // A damped spring, integrated velocity first so it stays stable.
            Vector2 pull = -stiffness * _jolt - damping * _joltVelocity;
            _joltVelocity += pull * dt;
            _jolt += _joltVelocity * dt;

            transform.localPosition = _rest + Vector3.up * (s * amplitude) + (Vector3)_jolt;

            // Drawn facing left, so a positive z turn lifts the nose.
            transform.localRotation = Quaternion.Euler(0f, 0f, -s * tilt - _jolt.y * joltPitch);
        }
    }
}
