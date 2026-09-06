using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A wind tunnel between towers. Pushes the runner while they are inside it,
    /// so jumps have to be re-timed. Pulses so the force is never constant.
    /// </summary>
    public class GustZone : MonoBehaviour
    {
        [SerializeField] private Vector2 force = new Vector2(-6f, 0f);
        [Tooltip("0 = steady force. Higher values make the gust pulse.")]
        [SerializeField, Range(0f, 1f)] private float pulseDepth = 0.6f;
        [SerializeField] private float pulseSpeed = 1.5f;
        [SerializeField] private ParticleSystem windParticles;

        private PlayerController _inside;

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null) _inside = player;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() == _inside) _inside = null;
        }

        private void FixedUpdate()
        {
            if (_inside == null || !_inside.IsAlive) return;

            float pulse = 1f - pulseDepth * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI));
            _inside.AddExternalForce(force * pulse);
        }
    }
}
