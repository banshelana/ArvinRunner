using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A wrecking ball on a pendulum arm. Time your jump or get swept off the
    /// roof. Put a Hazard on the ball child object.
    /// </summary>
    public class SwingingCrane : MonoBehaviour
    {
        [Tooltip("Maximum swing angle either side of straight down, in degrees.")]
        [SerializeField] private float amplitude = 55f;
        [SerializeField] private float period = 2.4f;
        [SerializeField, Range(0f, 1f)] private float phase;
        [Tooltip("The arm that rotates. Defaults to this transform.")]
        [SerializeField] private Transform pivot;

        private void Awake()
        {
            if (pivot == null) pivot = transform;
        }

        private void Update()
        {
            float t = (Time.time / Mathf.Max(0.01f, period) + phase) * Mathf.PI * 2f;
            float angle = Mathf.Sin(t) * amplitude;
            pivot.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
