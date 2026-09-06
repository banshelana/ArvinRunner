using System.Collections;
using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A rooftop section that gives way shortly after you step on it. Punishes
    /// hesitation, which is exactly what a runner wants.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CollapsingPlatform : MonoBehaviour
    {
        [SerializeField] private float delayBeforeCollapse = 0.35f;
        [SerializeField] private float shakeAmount = 0.06f;
        [Tooltip("Seconds before the debris is cleaned up.")]
        [SerializeField] private float destroyAfter = 2.5f;
        [SerializeField] private bool respawn;
        [SerializeField] private float respawnDelay = 3f;

        private Rigidbody2D _rb;
        private Collider2D _collider;
        private SpriteRenderer[] _renderers;
        private Vector3 _origin;
        private bool _triggered;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            _renderers = GetComponentsInChildren<SpriteRenderer>();
            _origin = transform.position;

            _rb = GetComponent<Rigidbody2D>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.gravityScale = 0f;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_triggered) return;
            if (collision.collider.GetComponentInParent<PlayerController>() == null) return;
            StartCoroutine(Collapse());
        }

        private IEnumerator Collapse()
        {
            _triggered = true;

            float elapsed = 0f;
            while (elapsed < delayBeforeCollapse)
            {
                elapsed += Time.deltaTime;
                transform.position = _origin + (Vector3)(Random.insideUnitCircle * shakeAmount);
                yield return null;
            }

            transform.position = _origin;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 2.5f;
            _rb.AddTorque(Random.Range(-40f, 40f));
            _collider.enabled = false;

            yield return new WaitForSeconds(destroyAfter);

            if (respawn) StartCoroutine(Respawn());
            else Destroy(gameObject);
        }

        private IEnumerator Respawn()
        {
            foreach (SpriteRenderer r in _renderers) r.enabled = false;
            yield return new WaitForSeconds(respawnDelay);

            transform.position = _origin;
            transform.rotation = Quaternion.identity;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.velocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _collider.enabled = true;
            foreach (SpriteRenderer r in _renderers) r.enabled = true;
            _triggered = false;
        }
    }
}
