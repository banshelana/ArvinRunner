using UnityEngine;

namespace ArvinRunner
{
    /// <summary>A pickup. Feeds the score and gives players a reason to take risky lines.</summary>
    public class Collectible : MonoBehaviour
    {
        [SerializeField] private int value = 1;
        [SerializeField] private GameObject pickupEffect;
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float spinSpeed = 90f;

        private Vector3 _origin;

        private void Awake() => _origin = transform.position;

        private void Update()
        {
            transform.position = _origin + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.IsAlive) return;

            if (GameManager.Instance != null) GameManager.Instance.AddPickup(value);
            if (pickupEffect != null) Instantiate(pickupEffect, transform.position, Quaternion.identity);

            Destroy(gameObject);
        }
    }
}
