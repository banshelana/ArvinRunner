using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Ping-pongs between two points. Carries the player by parenting them on
    /// contact, which is the cheapest reliable way to do this in 2D.
    /// </summary>
    public class MovingPlatform : MonoBehaviour
    {
        [Tooltip("Travel offset from the starting position, in world units.")]
        [SerializeField] private Vector2 travel = new Vector2(0f, 3f);
        [SerializeField] private float speed = 2f;
        [Tooltip("Shifts where in the cycle this platform starts (0-1).")]
        [SerializeField, Range(0f, 1f)] private float phase;
        [SerializeField] private bool smooth = true;

        private Vector3 _origin;

        private void Awake() => _origin = transform.position;

        private void FixedUpdate()
        {
            float t = (Time.time * speed + phase * Mathf.PI * 2f);
            float wave = smooth ? (Mathf.Sin(t) * 0.5f + 0.5f) : Mathf.PingPong(t / Mathf.PI, 1f);
            transform.position = _origin + (Vector3)(travel * wave);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.collider.GetComponentInParent<PlayerController>() != null)
                collision.transform.SetParent(transform, true);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (collision.collider.GetComponentInParent<PlayerController>() != null)
                collision.transform.SetParent(null, true);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 from = Application.isPlaying ? _origin : transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(from, from + (Vector3)travel);
            Gizmos.DrawWireSphere(from + (Vector3)travel, 0.15f);
        }
    }
}
