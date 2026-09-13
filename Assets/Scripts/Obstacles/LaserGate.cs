using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A beam that pulses on and off. Drives a Hazard plus the beam's renderer,
    /// so the visual always matches the state that can actually kill you.
    ///
    /// <b>The cycle runs on the runner's approach, not on the clock.</b> That is
    /// the whole design of this thing and it is worth saying why, because the
    /// obvious version does not work in an auto-runner.
    ///
    /// Timed off Time.time, a beam presents whatever phase it happens to be in
    /// when the runner arrives - and since the runner cannot stop, cannot slow
    /// down and reaches it at a moment decided by everything that happened
    /// earlier in the level, that phase is effectively random. Worse, LoadLevel
    /// rebuilds in place without reloading the scene, so the clock never resets
    /// and <b>every retry shows a different pattern</b>. There is nothing to learn
    /// and nothing to get better at: the player either arrives in the gap or does
    /// not, and dying teaches them none of it.
    ///
    /// Keyed to distance instead, the beam is in the same state at the same place
    /// on every single attempt. The corridor becomes a fixed pattern to solve.
    /// <see cref="arrivalPhase"/> is then the authoring knob that matters - it
    /// says outright whether this beam is lit when the runner gets to it.
    /// </summary>
    [RequireComponent(typeof(Hazard))]
    public class LaserGate : MonoBehaviour
    {
        [Header("Cycle")]
        [Tooltip("World units of approach per full on/off cycle. Smaller blinks faster.")]
        [SerializeField] private float wavelength = 6f;

        [Tooltip("Share of the cycle the beam is lethal.")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float armedFraction = 0.5f;

        [Tooltip("The phase the beam holds at the moment the runner reaches it. " +
                 "Below armedFraction it is lit and has to be jumped; above, the " +
                 "runner can go straight through. This is what makes a corridor a " +
                 "rhythm rather than a dice roll.")]
        [Range(0f, 1f)]
        [SerializeField] private float arrivalPhase;

        [Tooltip("Inside this distance the beam settles into its arrival state and " +
                 "holds it. Without it the beam could flip on the frame of contact, " +
                 "which is unreadable and unfair.")]
        [SerializeField] private float lockDistance = 2.5f;

        [Header("Fallback cycle")]
        [Tooltip("Used only when there is no runner to measure - a prefab preview, " +
                 "mostly - so the beam still animates when it is looked at.")]
        [SerializeField] private float fallbackCycle = 2f;

        [Header("Look")]
        [SerializeField] private SpriteRenderer beamRenderer;
        [SerializeField] private Color armedColour = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color idleColour = new Color(1f, 0.25f, 0.2f, 0.12f);

        [Tooltip("Share of the dark phase spent fading back in as a warning.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float warnFraction = 0.35f;

        private Hazard _hazard;
        private Transform _player;

        private void Awake()
        {
            _hazard = GetComponent<Hazard>();
            if (beamRenderer == null) beamRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Update()
        {
            float phase = CurrentPhase();
            bool armed = phase < armedFraction;

            _hazard.Armed = armed;

            if (beamRenderer == null) return;

            if (armed)
            {
                beamRenderer.color = armedColour;
                return;
            }

            // Fade back in over the last of the dark phase. Measured as a share of
            // the gap rather than in seconds, because the gap's length in seconds
            // now depends on how fast the runner is closing on it.
            float throughGap = Mathf.InverseLerp(armedFraction, 1f, phase);
            float warn = warnFraction > 0.001f
                ? Mathf.InverseLerp(1f - warnFraction, 1f, throughGap)
                : 0f;

            beamRenderer.color = Color.Lerp(idleColour, armedColour, warn);
        }

        /// <summary>
        /// Where in the cycle the beam is. Zero to one, with anything under
        /// <see cref="armedFraction"/> lethal.
        /// </summary>
        private float CurrentPhase()
        {
            Transform player = Player();

            if (player == null || wavelength <= 0.01f)
            {
                // No runner: fall back to a plain clock so the beam still blinks
                // when the prefab is being looked at in isolation.
                float cycle = Mathf.Max(0.1f, fallbackCycle);
                return Mathf.Repeat(Time.time + arrivalPhase * cycle, cycle) / cycle;
            }

            float distance = transform.position.x - player.position.x;

            // Settled, and staying settled once passed.
            if (distance <= lockDistance) return arrivalPhase;

            return Mathf.Repeat(arrivalPhase + (distance - lockDistance) / wavelength, 1f);
        }

        private Transform Player()
        {
            if (_player != null) return _player;

            PlayerController player = GameManager.Instance != null
                ? GameManager.Instance.Player
                : null;

            if (player == null) player = FindObjectOfType<PlayerController>();

            _player = player != null ? player.transform : null;
            return _player;
        }

        private void OnDrawGizmosSelected()
        {
            // Where it settles, so the authored arrival state is visible in the
            // scene rather than only in play.
            Gizmos.color = arrivalPhase < armedFraction
                ? new Color(1f, 0.2f, 0.15f, 0.9f)
                : new Color(0.3f, 1f, 0.4f, 0.9f);

            Vector3 at = transform.position + Vector3.left * lockDistance;
            Gizmos.DrawLine(at + Vector3.down * 2f, at + Vector3.up * 6f);
        }
    }
}
