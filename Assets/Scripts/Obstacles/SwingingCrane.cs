using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A load swinging on a crane. Time your jump or get swept off the roof. Put
    /// a Hazard on the part that hits - the crate, or the ball.
    ///
    /// Two ways to show it. With <see cref="frames"/> set, the load is the
    /// crane art from Art/MovingObstacles/Crane: one swing, drawn at even steps
    /// in time, each drawing measured for the angle it shows and for where its
    /// crate is. Without them it is the plain pendulum - the pivot rotated
    /// through a sine - which is also what the crane falls back to when no art
    /// was found.
    /// </summary>
    public class SwingingCrane : MonoBehaviour
    {
        [Tooltip("Maximum swing angle either side of straight down, in degrees. " +
                 "The plain pendulum only - drawn frames carry their own angles.")]
        [SerializeField] private float amplitude = 55f;
        [SerializeField] private float period = 2.4f;
        [SerializeField, Range(0f, 1f)] private float phase;
        [Tooltip("The arm that rotates. Defaults to this transform.")]
        [SerializeField] private Transform pivot;

        [Header("Drawn swing")]
        [Tooltip("The load's drawings: one whole swing, evenly spaced in time.")]
        [SerializeField] private Sprite[] frames;

        [Tooltip("The swing angle each drawing shows, in degrees; positive swings " +
                 "towards +x.")]
        [SerializeField] private float[] frameAngles;

        [Tooltip("Shows the drawings. A child of the pivot, with the sprite's own " +
                 "pivot on the point the load swings about.")]
        [SerializeField] private SpriteRenderer load;

        [Tooltip("The crate's collider. A child of the load, moved onto the crate " +
                 "of whichever drawing is showing.")]
        [SerializeField] private Transform crate;

        [Tooltip("Per drawing, the crate's centre relative to the swing pivot, in " +
                 "the drawing's own frame.")]
        [SerializeField] private Vector2[] crateCentres;

        [Tooltip("Per drawing, how far the crate is turned as drawn, in degrees.")]
        [SerializeField] private float[] crateTilts;

        private int _shown = -1;

        private void Awake()
        {
            if (pivot == null) pivot = transform;
        }

        private bool HasDrawnSwing =>
            load != null && frames != null && frameAngles != null &&
            frames.Length > 1 && frameAngles.Length == frames.Length;

        private bool HasCrate =>
            crate != null && crateCentres != null && crateTilts != null &&
            crateCentres.Length == frames.Length && crateTilts.Length == frames.Length;

        private void Update()
        {
            float cycle = Time.time / Mathf.Max(0.01f, period) + phase;

            if (HasDrawnSwing)
            {
                DrawnSwing(cycle);
                return;
            }

            float angle = Mathf.Sin(cycle * Mathf.PI * 2f) * amplitude;
            pivot.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>
        /// The drawings step at the rate they were drawn - 24 over a four-second
        /// swing is under six a second - so the load is not shown as the drawing
        /// alone. The pivot turns to the true angle at this instant, taken off a
        /// smooth curve through the drawings' own angles, and the nearest drawing
        /// is turned back by the angle it was drawn at. What is on screen is always
        /// an actual drawing, off by no more than half a step's rotation, and the
        /// swing moves on every rendered frame instead of six times a second.
        ///
        /// The crate's collider sits on the load, on that drawing's crate, so it
        /// turns with exactly what is shown and hits exactly where the crate is.
        /// </summary>
        private void DrawnSwing(float cycle)
        {
            int count = frames.Length;
            float position = Mathf.Repeat(cycle, 1f) * count;
            int index = Mathf.FloorToInt(position) % count;
            float t = position - Mathf.Floor(position);

            float angle = CatmullRom(frameAngles[(index + count - 1) % count],
                                     frameAngles[index],
                                     frameAngles[(index + 1) % count],
                                     frameAngles[(index + 2) % count], t);

            int shown = t < 0.5f ? index : (index + 1) % count;

            pivot.localRotation = Quaternion.Euler(0f, 0f, angle);

            if (shown == _shown) return;
            _shown = shown;

            load.sprite = frames[shown];
            load.transform.localRotation = Quaternion.Euler(0f, 0f, -frameAngles[shown]);

            if (HasCrate)
            {
                crate.localPosition = crateCentres[shown];
                crate.localRotation = Quaternion.Euler(0f, 0f, crateTilts[shown]);
            }
        }

        /// <summary>Through b at 0 and c at 1, with the slope set by a and d, so the
        /// swing's speed changes smoothly across drawings rather than jolting at
        /// each one as straight-line blending would.</summary>
        private static float CatmullRom(float a, float b, float c, float d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * b + (c - a) * t +
                           (2f * a - 5f * b + 4f * c - d) * t2 +
                           (3f * b - a - 3f * c + d) * t3);
        }
    }
}
