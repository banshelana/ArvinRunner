using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A pickup. Feeds the score and gives players a reason to take risky lines.
    ///
    /// With <see cref="spinFrames"/> set it is the painted coin: it turns through
    /// its frames, glints now and then while it faces the camera, and on pickup
    /// flares, lifts and fades instead of vanishing. Coins in a row turn and bob
    /// a little out of step with each other, by where they are along the level,
    /// so an arc of them ripples rather than moving as one. Without frames it is
    /// the plain sprite, turned about its upright axis as before.
    /// </summary>
    public class Collectible : MonoBehaviour
    {
        [SerializeField] private int value = 1;
        [SerializeField] private GameObject pickupEffect;
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float spinSpeed = 90f;

        [Header("Painted coin")]
        [SerializeField] private SpriteRenderer body;

        [Tooltip("Half a turn, face-on to edge-on and round to face-on again.")]
        [SerializeField] private Sprite[] spinFrames;

        [SerializeField] private float halfTurnSeconds = 0.8f;

        [Tooltip("World units along the level per whole cycle of offset between coins.")]
        [SerializeField] private float waveLength = 6f;

        [Header("Glint")]
        [SerializeField] private SpriteRenderer glint;
        [SerializeField] private float glintEvery = 2.6f;
        [SerializeField] private float glintSeconds = 0.4f;
        [SerializeField] private float glintScale = 1f;

        [Header("Collected")]
        [SerializeField] private float collectSeconds = 0.28f;
        [SerializeField] private float collectRise = 0.45f;

        private Vector3 _origin;
        private Vector3 _scale;
        private float _wave;
        private float _glintOffset;

        private bool _collected;
        private float _collectedAt;
        private Vector3 _collectedFrom;

        private bool HasFrames => body != null && spinFrames != null && spinFrames.Length > 0;

        private void Awake()
        {
            _origin = transform.position;
            _scale = transform.localScale;
            _wave = waveLength > 0.01f ? _origin.x / waveLength : 0f;

            // Scattered, not in step: a fraction of the interval from the position.
            _glintOffset = Mathf.Repeat(_origin.x * 12.9898f + _origin.y * 78.233f, 1f) * glintEvery;

            if (glint != null) glint.transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            if (_collected)
            {
                Collecting();
                return;
            }

            float time = Time.time;
            float bob = Mathf.Sin(time * bobSpeed - _wave * Mathf.PI * 2f) * bobHeight;
            transform.position = _origin + Vector3.up * bob;

            if (!HasFrames)
            {
                transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
                return;
            }

            float turn = Mathf.Repeat(time / Mathf.Max(0.05f, halfTurnSeconds) - _wave, 1f);
            ShowTurn(turn);
            Glint(time, turn);
        }

        private void ShowTurn(float turn)
        {
            int frame = Mathf.Min(spinFrames.Length - 1, (int)(turn * spinFrames.Length));
            body.sprite = spinFrames[frame];
        }

        /// <summary>
        /// A sparkle that swells and turns away, every so often - and only while
        /// the coin faces the camera, where the face could actually catch the light.
        /// </summary>
        private void Glint(float time, float turn)
        {
            if (glint == null) return;

            float t = Mathf.Repeat(time + _glintOffset, Mathf.Max(glintSeconds, glintEvery));
            float size = 0f;

            if (t < glintSeconds)
            {
                float k = t / glintSeconds;
                float facing = Mathf.Abs(Mathf.Cos(turn * Mathf.PI));
                size = Mathf.Sin(k * Mathf.PI) * Mathf.SmoothStep(0.55f, 0.9f, facing);
                glint.transform.localRotation = Quaternion.Euler(0f, 0f, k * 90f);
            }

            glint.transform.localScale = Vector3.one * (glintScale * size);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_collected) return;

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.IsAlive) return;

            if (GameManager.Instance != null) GameManager.Instance.AddPickup(value);
            if (pickupEffect != null) Instantiate(pickupEffect, transform.position, Quaternion.identity);

            if (!HasFrames)
            {
                Destroy(gameObject);
                return;
            }

            _collected = true;
            _collectedAt = Time.time;
            _collectedFrom = transform.position;

            foreach (Collider2D collider in GetComponents<Collider2D>())
                collider.enabled = false;
        }

        /// <summary>
        /// Lifts, swells and fades, spinning three times as fast, while the
        /// sparkle flares large over it - then gone.
        /// </summary>
        private void Collecting()
        {
            float elapsed = Time.time - _collectedAt;
            float k = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, collectSeconds));
            float ease = 1f - (1f - k) * (1f - k);

            transform.position = _collectedFrom + Vector3.up * (collectRise * ease);
            transform.localScale = _scale * (1f + 0.45f * ease);

            ShowTurn(Mathf.Repeat(elapsed * 3f / Mathf.Max(0.05f, halfTurnSeconds), 1f));

            Color colour = body.color;
            colour.a = 1f - k * k;
            body.color = colour;

            if (glint != null)
            {
                float flare = Mathf.Sin(Mathf.Min(1f, k * 1.3f) * Mathf.PI);
                glint.transform.localScale = Vector3.one * (glintScale * 1.8f * flare);
                glint.transform.localRotation = Quaternion.Euler(0f, 0f, k * 120f);
            }

            if (k >= 1f) Destroy(gameObject);
        }
    }
}
