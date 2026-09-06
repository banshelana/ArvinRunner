using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A beam that pulses on and off. Read the rhythm, then run through the gap.
    /// Drives a Hazard plus the beam's renderer so the visual always matches the
    /// state that can actually kill you.
    /// </summary>
    [RequireComponent(typeof(Hazard))]
    public class LaserGate : MonoBehaviour
    {
        [SerializeField] private float onDuration = 1.2f;
        [SerializeField] private float offDuration = 0.9f;
        [Tooltip("Offsets the cycle so a row of gates does not fire in unison.")]
        [SerializeField] private float startOffset;
        [Tooltip("Beam fades in over this long as a warning before it arms.")]
        [SerializeField] private float warnDuration = 0.25f;

        [SerializeField] private SpriteRenderer beamRenderer;
        [SerializeField] private Color armedColour = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color idleColour = new Color(1f, 0.25f, 0.2f, 0.12f);

        private Hazard _hazard;

        private void Awake()
        {
            _hazard = GetComponent<Hazard>();
            if (beamRenderer == null) beamRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Update()
        {
            float cycle = onDuration + offDuration;
            if (cycle <= 0.01f) return;

            float t = Mathf.Repeat(Time.time + startOffset, cycle);
            bool armed = t < onDuration;

            _hazard.Armed = armed;

            if (beamRenderer == null) return;

            if (armed)
            {
                beamRenderer.color = armedColour;
            }
            else
            {
                // Fade the beam back in during the last moments of the gap.
                float untilArmed = cycle - t;
                float warn = warnDuration > 0f ? Mathf.InverseLerp(warnDuration, 0f, untilArmed) : 0f;
                beamRenderer.color = Color.Lerp(idleColour, armedColour, warn);
            }
        }
    }
}
