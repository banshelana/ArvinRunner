using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A glass panel or thin floor that only breaks when you hit it hard enough
    /// - a dive-slam or a fall from height. Approach it too gently and it stops
    /// you dead, so it rewards commitment.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BreakableGlass : MonoBehaviour
    {
        [Tooltip("Impact speed required to smash through.")]
        [SerializeField] private float breakSpeed = 14f;
        [Tooltip("Player states that smash through regardless of speed.")]
        [SerializeField] private bool breakOnSlide = true;
        [SerializeField] private GameObject shatterEffect;
        [Tooltip("Speed kept after smashing through, as a fraction.")]
        [SerializeField, Range(0.1f, 1f)] private float speedRetained = 0.75f;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            PlayerController player = collision.collider.GetComponentInParent<PlayerController>();
            if (player == null || !player.IsAlive) return;

            Rigidbody2D rb = collision.rigidbody;
            float impact = rb != null ? rb.velocity.magnitude : 0f;

            bool sliding = breakOnSlide &&
                           (player.State == PlayerState.Sliding || player.State == PlayerState.Rolling);

            if (impact < breakSpeed && !sliding) return;

            if (shatterEffect != null)
                Instantiate(shatterEffect, collision.GetContact(0).point, Quaternion.identity);

            if (rb != null) rb.velocity *= speedRetained;

            Destroy(gameObject);
        }
    }
}
