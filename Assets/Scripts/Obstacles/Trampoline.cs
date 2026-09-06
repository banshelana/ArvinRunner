using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Awnings, air vents and spring pads. Launches the runner and refreshes the
    /// double jump, which opens up big vertical routes.
    /// </summary>
    public class Trampoline : MonoBehaviour
    {
        [SerializeField] private float launchSpeed = 18f;
        [Tooltip("Optional forward push. Leave at 0 for a purely vertical pop.")]
        [SerializeField] private float forwardBoost;
        [SerializeField] private float cooldown = 0.2f;
        [SerializeField] private Transform squashVisual;

        private float _lastLaunch = -99f;

        private void OnCollisionEnter2D(Collision2D collision) => TryLaunch(collision.collider);
        private void OnTriggerEnter2D(Collider2D other) => TryLaunch(other);

        private void TryLaunch(Collider2D other)
        {
            if (Time.time - _lastLaunch < cooldown) return;

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.IsAlive) return;

            _lastLaunch = Time.time;
            player.Launch(new Vector2(forwardBoost, launchSpeed));

            if (squashVisual != null) StartCoroutine(Squash());
        }

        private System.Collections.IEnumerator Squash()
        {
            Vector3 baseScale = squashVisual.localScale;
            squashVisual.localScale = new Vector3(baseScale.x * 1.15f, baseScale.y * 0.5f, baseScale.z);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 6f;
                squashVisual.localScale = Vector3.Lerp(squashVisual.localScale, baseScale, t);
                yield return null;
            }

            squashVisual.localScale = baseScale;
        }
    }
}
