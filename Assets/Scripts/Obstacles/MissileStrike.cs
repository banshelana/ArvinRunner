using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// The thing the helicopter puts on the ground: a marked spot, then a
    /// missile into it, then a moment of fire the runner has to be over.
    ///
    /// It is authored into the chunk at the point it will land, dormant, and the
    /// helicopter calls <see cref="Launch"/> as it passes. Pre-placing it rather
    /// than spawning it keeps the strike point visible in the prefab, where it
    /// can be laid out against the ground like every other obstacle.
    ///
    /// The marker is the whole reason this is fair. A blast that simply appeared
    /// would be memorisation; a blast that announces itself a second early is a
    /// read. That second is also why the helicopter fires off the runner's
    /// arrival time rather than its own position - see HelicopterStrike.
    /// </summary>
    [RequireComponent(typeof(Hazard))]
    public class MissileStrike : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private SpriteRenderer marker;
        [SerializeField] private SpriteRenderer blast;
        [SerializeField] private SpriteRenderer missile;

        [Header("Timing")]
        [Tooltip("How long the marker shows before the missile lands. This is the " +
                 "player's warning, and the helicopter fires so that it ends just " +
                 "as the runner arrives.")]
        [SerializeField] private float warnDuration = 0.75f;

        [Tooltip("How long the fire stays lethal after impact.")]
        [SerializeField] private float blastDuration = 0.9f;

        [Header("Contact")]
        [Tooltip("Radius of the lethal area, matched to the blast collider.")]
        [SerializeField] private float blastRadius = 1.3f;

        private Hazard _hazard;
        private float _time;
        private bool _running;
        private Vector3 _from;

        // The authored sizes. The animation scales relative to these rather than
        // writing localScale outright, so the factory can size the marker to the
        // lethal radius and have the pulse respect it.
        private Vector3 _markerScale = Vector3.one;
        private Vector3 _blastScale = Vector3.one;

        /// <summary>How much warning this strike gives, so the firer can lead by it.</summary>
        public float WarnDuration => warnDuration;

        private void Awake()
        {
            _hazard = GetComponent<Hazard>();
            _hazard.Armed = false;

            if (marker != null) _markerScale = marker.transform.localScale;
            if (blast != null) _blastScale = blast.transform.localScale;

            Show(marker, false);
            Show(blast, false);
            Show(missile, false);
        }

        /// <summary>Starts the sequence, with the missile flying in from <paramref name="from"/>.</summary>
        public void Launch(Vector3 from)
        {
            _from = from;
            _time = 0f;
            _running = true;
        }

        private void Update()
        {
            if (!_running) return;

            _time += Time.deltaTime;

            if (_time < warnDuration) Incoming(_time / Mathf.Max(0.01f, warnDuration));
            else Burning((_time - warnDuration) / Mathf.Max(0.01f, blastDuration));
        }

        /// <summary>Marker on the ground, missile on its way down to it.</summary>
        private void Incoming(float t)
        {
            Show(marker, true);
            Show(missile, true);

            if (marker != null)
            {
                // Tightens and beats faster as it closes, so the urgency is
                // readable without having to count the seconds.
                float scale = Mathf.Lerp(1.7f, 1f, t);
                marker.transform.localScale = _markerScale * scale;

                float beat = Mathf.PingPong(t * 6f, 1f);
                Color colour = marker.color;
                colour.a = Mathf.Lerp(0.35f, 1f, beat);
                marker.color = colour;
            }

            if (missile != null)
            {
                Vector3 position = Vector3.Lerp(_from, transform.position, t);
                missile.transform.position = position;

                Vector3 heading = transform.position - _from;
                if (heading.sqrMagnitude > 0.001f)
                {
                    float angle = Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg;
                    missile.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                }
            }
        }

        /// <summary>Impact. Lethal for blastDuration, then gone.</summary>
        private void Burning(float t)
        {
            Show(marker, false);
            Show(missile, false);

            if (t >= 1f)
            {
                _running = false;
                _hazard.Armed = false;
                Show(blast, false);
                gameObject.SetActive(false);
                return;
            }

            Show(blast, true);
            _hazard.Armed = true;

            if (blast != null)
            {
                float scale = Mathf.Lerp(0.5f, 1.15f, Mathf.Sqrt(t));
                blast.transform.localScale = _blastScale * scale;

                Color colour = blast.color;
                colour.a = Mathf.Lerp(1f, 0f, t * t);
                blast.color = colour;
            }

            KillAnyoneStandingInIt();
        }

        /// <summary>
        /// Hazard kills on trigger *entry*, which is the wrong event for this one.
        /// The strike is timed to go off exactly as the runner arrives, so the
        /// likeliest case by far is that they are already inside the collider when
        /// it arms and no entry is ever reported. This closes that hole; the
        /// Hazard still handles anyone who runs in afterwards.
        /// </summary>
        private void KillAnyoneStandingInIt()
        {
            Collider2D hit = Physics2D.OverlapCircle(transform.position, blastRadius,
                                                     1 << GameLayers.Player);
            if (hit == null) return;

            PlayerController player = hit.GetComponentInParent<PlayerController>();
            if (player != null && player.IsAlive) player.Kill(DeathCause.Hazard);
        }

        private static void Show(SpriteRenderer renderer, bool visible)
        {
            if (renderer != null) renderer.enabled = visible;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, blastRadius);
        }
    }
}
