using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// An animal waiting at the bottom of a pit: idling, then rearing up at
    /// whatever is crossing over it, then settling back to idle.
    ///
    /// One folder, two speeds, three parts. The art for both the crocodile and
    /// the snake is a single run of drawings that goes from lying still to a
    /// full display and back, so the two halves are addressed as ranges of the
    /// same flipbook rather than split into separate clips: <see cref="restFirst"/>
    /// to <see cref="restLast"/> loops while nothing is happening, and
    /// <see cref="lungeFirst"/> to <see cref="lungeLast"/> plays once when
    /// something is.
    ///
    /// Looping the whole folder instead would have the animal rearing without
    /// pause, forever, like a mechanism. The rest is what makes it read as an
    /// animal deciding to do something - so it is rolled again after every
    /// display, and <see cref="phase"/> offsets the first one, which keeps
    /// several in one pit from ever coming up together or settling into a beat.
    ///
    /// Scenery. The Hazard on the pit is what kills, and it kills anywhere in it
    /// whether the animal happens to be rearing there or not - the runner is
    /// never asked to read a jaw and time a landing between displays.
    /// </summary>
    public class LurkingAnimal : MonoBehaviour
    {
        [SerializeField] private SpriteFlipbook book;

        [Header("Idling")]
        [SerializeField] private int restFirst;
        [SerializeField] private int restLast;
        [SerializeField] private float restFps = 9f;

        [Tooltip("Seconds of idling between displays, before the spread.")]
        [SerializeField] private float restTime = 1.6f;

        [Tooltip("Up to this much is added to each rest, rolled again every time.")]
        [SerializeField] private float restSpread = 2.2f;

        [Tooltip("Offsets the first rest, so several in one pit do not rear as one.")]
        [SerializeField] private float phase;

        [Header("Rearing up")]
        [SerializeField] private int lungeFirst;
        [SerializeField] private int lungeLast;
        [SerializeField] private float lungeFps = 15f;

        [Header("Where it lies")]
        [SerializeField] private float bobHeight = 0.07f;
        [SerializeField] private float bobPeriod = 3.1f;

        [Tooltip("How far it drifts back and forth. Small: it is holding station, " +
                 "not travelling past.")]
        [SerializeField] private float drift = 0.18f;

        private Vector3 _home;
        private float _wait;
        private bool _lunging;

        private void Awake()
        {
            _home = transform.localPosition;
            if (book == null) book = GetComponentInChildren<SpriteFlipbook>();
        }

        // Not Awake: the flipbook sets itself up in its own Awake, and settling it
        // from here first would be undone a moment later - every animal in the
        // level would open on one un-staggered display before the rests took hold.
        private void Start()
        {
            _wait = phase;
            Settle();
        }

        private void Update()
        {
            Float();

            if (book == null) return;

            if (_lunging)
            {
                if (!book.Finished) return;

                Settle();
                _wait = restTime + Random.value * restSpread;
                return;
            }

            _wait -= Time.deltaTime;
            if (_wait > 0f) return;

            book.SetRate(lungeFps);
            book.PlayRange(lungeFirst, lungeLast, false);
            _lunging = true;
        }

        /// <summary>Back into the idle, looping.</summary>
        private void Settle()
        {
            _lunging = false;
            if (book == null) return;

            book.SetRate(restFps);
            book.PlayRange(restFirst, restLast, true);
        }

        /// <summary>
        /// The water or the sand under it. Two periods that do not divide into
        /// each other, so the rise and the drift never line up into a circle being
        /// traced.
        /// </summary>
        private void Float()
        {
            float t = Time.time + phase * 7f;
            float period = Mathf.Max(0.1f, bobPeriod);

            transform.localPosition = _home
                + Vector3.up * (Mathf.Sin(t / period * Mathf.PI * 2f) * bobHeight)
                + Vector3.right * (Mathf.Sin(t / (period * 1.7f) * Mathf.PI * 2f) * drift);
        }
    }
}
