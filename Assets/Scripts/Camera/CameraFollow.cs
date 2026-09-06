using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Runner camera. Keeps the player left of centre so there is room to read
    /// what is coming, leads the run at speed, and smooths vertical movement so
    /// every small hop does not shake the screen.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private PlayerController player;

        [Header("Framing")]
        [Tooltip("Where the runner sits horizontally, as a fraction of the screen width.")]
        [SerializeField, Range(0.1f, 0.9f)] private float screenAnchorX = 0.32f;
        [SerializeField] private float verticalOffset = 1.5f;

        [Header("Smoothing")]
        [SerializeField] private float horizontalSmoothing = 0.08f;
        [SerializeField] private float verticalSmoothing = 0.28f;
        [Tooltip("Vertical movement smaller than this is ignored, so small hops do not pan the camera.")]
        [SerializeField] private float verticalDeadZone = 1.2f;

        [Header("Look ahead")]
        [Tooltip("Extra distance ahead at top speed, in world units.")]
        [SerializeField] private float maxLookAhead = 2.5f;
        [SerializeField] private float lookAheadSmoothing = 0.4f;

        [Header("Limits")]
        [SerializeField] private bool clampBottom = true;
        [SerializeField] private float minimumY = -6f;

        [Header("Shake")]
        [SerializeField] private float deathShakeAmount = 0.35f;
        [SerializeField] private float deathShakeDuration = 0.4f;

        private Camera _camera;
        private float _xVelocity, _yVelocity, _lookVelocity;
        private float _lookAhead;
        private float _anchoredY;
        private float _shakeTimer, _shakeStrength;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (player == null && target != null) player = target.GetComponentInParent<PlayerController>();
            if (target == null && player != null) target = player.transform;
            if (target != null) _anchoredY = target.position.y;
        }

        private void OnEnable()
        {
            if (player != null) player.OnDied += HandleDeath;
        }

        private void OnDisable()
        {
            if (player != null) player.OnDied -= HandleDeath;
        }

        /// <summary>Re-point the camera, e.g. after the player respawns.</summary>
        public void SetTarget(PlayerController newPlayer)
        {
            if (player != null) player.OnDied -= HandleDeath;

            player = newPlayer;
            target = newPlayer != null ? newPlayer.transform : null;

            if (player != null) player.OnDied += HandleDeath;
            if (target != null) SnapToTarget();
        }

        /// <summary>Jump straight to the framing with no easing.</summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            _anchoredY = target.position.y;
            _lookAhead = 0f;
            transform.position = new Vector3(DesiredX(), DesiredY(), transform.position.z);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            UpdateLookAhead();
            UpdateVerticalAnchor();

            float x = Mathf.SmoothDamp(transform.position.x, DesiredX(), ref _xVelocity, horizontalSmoothing);
            float y = Mathf.SmoothDamp(transform.position.y, DesiredY(), ref _yVelocity, verticalSmoothing);

            transform.position = new Vector3(x, y, transform.position.z) + ShakeOffset();
        }

        private void UpdateLookAhead()
        {
            float desired = 0f;

            if (player != null && player.Config != null && player.Config.maxRunSpeed > 0f)
            {
                float speedRatio = Mathf.Clamp01(player.CurrentSpeed / player.Config.maxRunSpeed);
                desired = maxLookAhead * speedRatio;
            }

            _lookAhead = Mathf.SmoothDamp(_lookAhead, desired, ref _lookVelocity, lookAheadSmoothing);
        }

        /// <summary>
        /// Only re-anchor vertically once the runner leaves the dead zone. This
        /// is what stops the camera bobbing on every jump.
        /// </summary>
        private void UpdateVerticalAnchor()
        {
            float delta = target.position.y - _anchoredY;

            if (Mathf.Abs(delta) > verticalDeadZone)
                _anchoredY += delta - Mathf.Sign(delta) * verticalDeadZone;
        }

        private float DesiredX()
        {
            float halfWidth = _camera != null && _camera.orthographic
                ? _camera.orthographicSize * _camera.aspect
                : 8f;

            // Offset so the runner sits at screenAnchorX rather than dead centre.
            float shift = halfWidth * (1f - screenAnchorX * 2f);
            return target.position.x + shift + _lookAhead;
        }

        private float DesiredY()
        {
            float y = _anchoredY + verticalOffset;
            return clampBottom ? Mathf.Max(y, minimumY) : y;
        }

        private void HandleDeath(DeathCause cause)
        {
            _shakeTimer = deathShakeDuration;
            _shakeStrength = deathShakeAmount;
        }

        private Vector3 ShakeOffset()
        {
            if (_shakeTimer <= 0f) return Vector3.zero;

            _shakeTimer -= Time.unscaledDeltaTime;
            float falloff = Mathf.Clamp01(_shakeTimer / Mathf.Max(0.01f, deathShakeDuration));

            return (Vector3)(Random.insideUnitCircle * (_shakeStrength * falloff));
        }
    }
}
