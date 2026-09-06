using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Anything that ends the run on contact: spikes, saw blades, laser beams,
    /// electric fences. Put it on a trigger collider.
    /// </summary>
    public class Hazard : MonoBehaviour
    {
        [Tooltip("Reported to the UI so the death message can differ per trap.")]
        [SerializeField] private DeathCause cause = DeathCause.Hazard;

        [Tooltip("Turn off to disable this hazard without destroying it (laser gates use this).")]
        public bool Armed = true;

        [Header("Feedback")]
        [SerializeField] private GameObject hitEffect;

        private void OnTriggerEnter2D(Collider2D other) => TryKill(other);
        private void OnCollisionEnter2D(Collision2D collision) => TryKill(collision.collider);

        private void TryKill(Collider2D other)
        {
            if (!Armed) return;

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.IsAlive) return;

            if (hitEffect != null)
                Instantiate(hitEffect, other.bounds.center, Quaternion.identity);

            player.Kill(cause);
        }
    }
}
