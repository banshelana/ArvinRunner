using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A machine gun's stream of fire, drawn: tracer rounds streaking out of the
    /// muzzle along a band at waist height, and a flickering muzzle flash.
    ///
    /// It decides nothing. Whether the gun is firing is the Hazard's Armed flag,
    /// which a LaserGate on the same object drives off the runner's approach - so
    /// the bursts are the same at the same place on every attempt, and the
    /// rounds on screen are exactly the moments the band can kill. A gun that
    /// drew fire while harmless, or went quiet while lethal, would be a lie.
    ///
    /// Rounds fade out over the last part of <see cref="range"/> and never travel
    /// past it, because range is the length of the lethal band: nothing drawn
    /// reaches further than the fire does.
    /// </summary>
    [RequireComponent(typeof(Hazard))]
    public class TracerFire : MonoBehaviour
    {
        [Tooltip("Where the rounds leave from. They fly left, toward the runner.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("The flash at the muzzle, flickered while firing.")]
        [SerializeField] private SpriteRenderer flash;

        [Tooltip("A horizontal streak, stretched to the round length.")]
        [SerializeField] private Sprite round;

        [Header("Rounds")]
        [Tooltip("How far the rounds reach - the length of the lethal band.")]
        [SerializeField] private float range = 3.3f;

        [SerializeField] private float roundsPerSecond = 18f;
        [SerializeField] private float roundSpeed = 36f;
        [SerializeField] private float roundLength = 0.6f;
        [SerializeField] private float roundThickness = 0.06f;

        [Tooltip("Up and down scatter about the muzzle line. Keep it inside the band.")]
        [SerializeField] private float scatter = 0.12f;

        [SerializeField] private Color roundColour = new Color(1f, 0.86f, 0.4f, 1f);

        private struct Round
        {
            public SpriteRenderer Renderer;
            public float Travelled;
            public float Height;
        }

        private readonly List<Round> _rounds = new List<Round>();
        private readonly Stack<SpriteRenderer> _spare = new Stack<SpriteRenderer>();

        private Hazard _hazard;
        private float _untilNext;
        private Vector3 _flashScale = Vector3.one;

        private void Awake()
        {
            _hazard = GetComponent<Hazard>();

            if (flash != null)
            {
                _flashScale = flash.transform.localScale;
                flash.enabled = false;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool firing = _hazard.Armed && muzzle != null && round != null;

            if (firing)
            {
                _untilNext -= dt;
                int guard = 0;
                while (_untilNext <= 0f && guard++ < 8)
                {
                    Fire();
                    _untilNext += 1f / Mathf.Max(1f, roundsPerSecond);
                }
            }
            else
            {
                _untilNext = 0f;
            }

            if (flash != null)
            {
                bool lit = firing && Random.value < 0.6f;
                flash.enabled = lit;

                if (lit)
                {
                    flash.transform.localScale = new Vector3(_flashScale.x * Random.Range(0.7f, 1.2f),
                                                             _flashScale.y * Random.Range(0.8f, 1.2f),
                                                             _flashScale.z);
                }
            }

            MoveRounds(dt);
        }

        private void Fire()
        {
            SpriteRenderer renderer = _spare.Count > 0 ? _spare.Pop() : NewRenderer();
            renderer.sprite = round;
            renderer.enabled = true;

            var shot = new Round
            {
                Renderer = renderer,
                Travelled = 0f,
                Height = Random.Range(-scatter, scatter)
            };

            _rounds.Add(shot);
            Apply(shot);
        }

        private void MoveRounds(float dt)
        {
            for (int i = _rounds.Count - 1; i >= 0; i--)
            {
                Round shot = _rounds[i];
                shot.Travelled += roundSpeed * dt;

                if (shot.Travelled >= range)
                {
                    shot.Renderer.enabled = false;
                    _spare.Push(shot.Renderer);
                    _rounds.RemoveAt(i);
                    continue;
                }

                _rounds[i] = shot;
                Apply(shot);
            }
        }

        /// <summary>
        /// Lays a round between its head and its tail. The tail is held at the
        /// muzzle until the round has cleared it, so a streak never pokes out of
        /// the back of the gun.
        /// </summary>
        private void Apply(Round shot)
        {
            if (muzzle == null) return;

            Vector3 start = muzzle.position + Vector3.up * shot.Height;
            float head = Mathf.Min(shot.Travelled, range);
            float tail = Mathf.Max(0f, shot.Travelled - roundLength);
            float length = Mathf.Max(0.01f, head - tail);

            Vector2 size = round != null ? (Vector2)round.bounds.size : Vector2.one;

            Transform t = shot.Renderer.transform;
            t.position = new Vector3(start.x - (head + tail) * 0.5f, start.y, start.z);
            t.localScale = new Vector3(length / Mathf.Max(0.01f, size.x),
                                       roundThickness / Mathf.Max(0.01f, size.y), 1f);

            Color colour = roundColour;
            colour.a *= Mathf.Clamp01((range - head) / Mathf.Max(0.01f, range * 0.3f));
            shot.Renderer.color = colour;
        }

        private SpriteRenderer NewRenderer()
        {
            var go = new GameObject("Round");
            go.transform.SetParent(transform, true);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 13;
            return renderer;
        }

        private void OnDrawGizmosSelected()
        {
            if (muzzle == null) return;

            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.9f);
            Gizmos.DrawLine(muzzle.position, muzzle.position + Vector3.left * range);
        }
    }
}
