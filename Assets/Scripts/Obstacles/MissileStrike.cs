using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// The thing the helicopter puts on the ground: a marked spot, then a
    /// missile into it, then a moment of fire the runner has to be over.
    ///
    /// It is authored into the chunk at the point it will land, dormant, and the
    /// helicopter calls <see cref="Launch(Transform)"/> as it passes. Pre-placing
    /// it rather than spawning it keeps the strike point visible in the prefab,
    /// where it can be laid out against the ground like every other obstacle.
    ///
    /// The marker is the whole reason this is fair. A blast that simply appeared
    /// would be memorisation; a blast that announces itself a second early is a
    /// read. That second is also why the helicopter fires off the runner's
    /// arrival time rather than its own position - see HelicopterStrike.
    ///
    /// <b>What happens, in order.</b> The aircraft lases the roof: a beam from its
    /// designator to a mark in perspective on the roof, pulsing faster as the shot
    /// closes. The missile drops off its rail, lights its motor and accelerates
    /// into a curving dive, trailing smoke that drifts, spreads and thins. Impact
    /// shakes the camera, the fireball swells and cools into rising smoke, and the
    /// roof keeps a scorch mark for a few seconds after. None of the timing a
    /// player reads has changed: the warning is still <see cref="warnDuration"/>
    /// and the fire is lethal for exactly <see cref="blastDuration"/> - the smoke
    /// and the scorch that outlast it are harmless.
    /// </summary>
    [RequireComponent(typeof(Hazard))]
    public class MissileStrike : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private SpriteRenderer marker;
        [SerializeField] private SpriteRenderer laser;
        [SerializeField] private SpriteRenderer missile;

        [Tooltip("The motor flame, a child of the missile.")]
        [SerializeField] private SpriteRenderer flame;

        [SerializeField] private SpriteRenderer blast;
        [SerializeField] private SpriteRenderer scorch;

        [Tooltip("The fireball, played once from impact. Left empty, the blast " +
                 "sprite is grown and faded instead.")]
        [SerializeField] private Sprite[] blastFrames;

        [Tooltip("Puffs the smoke trail is drawn from, one at random per puff.")]
        [SerializeField] private Sprite[] smokePuffs;

        [Tooltip("The motor nozzle's position along the missile, in its local units.")]
        [SerializeField] private float nozzleOffset = -0.5f;

        [Header("Timing")]
        [Tooltip("How long the marker shows before the missile lands. This is the " +
                 "player's warning, and the helicopter fires so that it ends just " +
                 "as the runner arrives.")]
        [SerializeField] private float warnDuration = 0.75f;

        [Tooltip("How long the fire stays lethal after impact.")]
        [SerializeField] private float blastDuration = 0.9f;

        [Tooltip("How long the fireball and its smoke take to play out. Longer than " +
                 "blastDuration: smoke still hangs after the fire stops being dangerous.")]
        [SerializeField] private float blastVisualDuration = 1.5f;

        [Tooltip("How long the scorch stays on the roof, fading over its last third.")]
        [SerializeField] private float scorchDuration = 3.5f;

        [Header("Flight")]
        [Tooltip("How far below the launch point the missile sags before its motor " +
                 "pulls it into the dive - the drop off the rail.")]
        [SerializeField] private float railDrop = 0.8f;

        [Tooltip("Exponent on the flight clock. Above 1 the missile leaves slowly and " +
                 "accelerates the whole way in, as a rocket does.")]
        [Range(1f, 3f)]
        [SerializeField] private float acceleration = 1.9f;

        [Tooltip("Share of the flight before the motor lights and the trail starts.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float ignitionAt = 0.12f;

        [Header("Smoke trail")]
        [Tooltip("Distance flown between puffs. Emitting by distance rather than by " +
                 "time keeps the trail continuous as the missile accelerates: at a fixed " +
                 "rate the puffs sat 0.3 units apart by impact and the trail broke into beads.")]
        [SerializeField] private float trailSpacing = 0.09f;
        [SerializeField] private float puffLife = 1.1f;
        [SerializeField] private float puffStartSize = 0.22f;
        [SerializeField] private float puffEndSize = 1.0f;

        [Header("Impact")]
        [SerializeField] private float impactShake = 0.22f;
        [SerializeField] private float impactShakeDuration = 0.3f;

        [Header("Contact")]
        [Tooltip("Radius of the lethal area, matched to the blast collider.")]
        [SerializeField] private float blastRadius = 1.3f;

        private struct Puff
        {
            public SpriteRenderer Renderer;
            public float Age;
            public float Life;
            public Vector3 Velocity;
            public float Spin;
            public float Shade;
        }

        private Hazard _hazard;
        private float _time;
        private bool _running;
        private bool _impacted;
        private Vector3 _from;
        private Transform _source;
        private float _trailDistance;
        private Vector3 _lastNozzle;
        private bool _trailStarted;
        private float _beat;

        // The authored sizes and colours. Animation works relative to these, so the
        // factory can size a part to the lethal radius and have the effect respect it.
        private Vector3 _markerScale = Vector3.one;
        private Vector3 _blastScale = Vector3.one;
        private Vector3 _flameScale = Vector3.one;
        private Vector3 _laserScale = Vector3.one;
        private Color _scorchColour = Color.white;

        private readonly List<Puff> _puffs = new List<Puff>();
        private readonly Stack<SpriteRenderer> _sparePuffs = new Stack<SpriteRenderer>();

        /// <summary>How much warning this strike gives, so the firer can lead by it.</summary>
        public float WarnDuration => warnDuration;

        private void Awake()
        {
            _hazard = GetComponent<Hazard>();
            _hazard.Armed = false;

            if (marker != null) _markerScale = marker.transform.localScale;
            if (blast != null) _blastScale = blast.transform.localScale;
            if (flame != null) _flameScale = flame.transform.localScale;
            if (laser != null) _laserScale = laser.transform.localScale;
            if (scorch != null) _scorchColour = scorch.color;

            Show(marker, false);
            Show(laser, false);
            Show(missile, false);
            Show(flame, false);
            Show(blast, false);
            Show(scorch, false);
        }

        /// <summary>Starts the sequence, lased and fired from <paramref name="source"/>, which may move.</summary>
        public void Launch(Transform source)
        {
            Begin(source != null ? source.position : transform.position);
            _source = source;
        }

        /// <summary>Starts the sequence, with the missile flying in from a fixed point.</summary>
        public void Launch(Vector3 from)
        {
            Begin(from);
            _source = null;
        }

        private void Begin(Vector3 from)
        {
            _from = from;
            _time = 0f;
            _trailDistance = 0f;
            _trailStarted = false;
            _beat = 0f;
            _impacted = false;
            _running = true;
        }

        private void Update()
        {
            UpdatePuffs(Time.deltaTime);
            if (!_running) return;

            _time += Time.deltaTime;

            if (_time < warnDuration) Incoming(_time / Mathf.Max(0.01f, warnDuration));
            else Impact(_time - warnDuration);
        }

        // ---- incoming ----------------------------------------------------------- //

        /// <summary>The roof lased and marked, the missile on its way down to it.</summary>
        private void Incoming(float t)
        {
            Vector3 target = transform.position;

            if (marker != null)
            {
                Show(marker, true);

                // Beats faster as the shot closes, so the urgency is readable
                // without counting, and settles from a touch wide onto the edge.
                _beat += Time.deltaTime * Mathf.Lerp(2.5f, 9f, t);
                float pulse = 0.5f + 0.5f * Mathf.Sin(_beat * Mathf.PI * 2f);

                marker.transform.localScale = _markerScale * Mathf.Lerp(1.12f, 1f, t);
                Color colour = marker.color;
                colour.a = Mathf.Lerp(0.55f, 1f, pulse);
                marker.color = colour;
            }

            if (laser != null)
            {
                Show(laser, true);
                Vector3 source = _source != null ? _source.position : _from;
                Stretch(laser, source, target, _laserScale.y, Random.Range(0.35f, 0.6f));
            }

            if (missile == null) return;
            Show(missile, true);

            // A curve rather than a straight line: the missile leaves the rail
            // nearly level, sags, and is pulled into a steepening dive. The flight
            // clock is raised to a power so it accelerates all the way in.
            Vector3 p0 = _from;
            Vector3 p2 = target + Vector3.up * 0.1f;
            var p1 = new Vector3(Mathf.Lerp(p0.x, p2.x, 0.3f), p0.y - railDrop, p0.z);

            float s = Mathf.Pow(t, acceleration);
            float inv = 1f - s;
            missile.transform.position = inv * inv * p0 + 2f * inv * s * p1 + s * s * p2;

            Vector3 heading = 2f * inv * (p1 - p0) + 2f * s * (p2 - p1);
            if (heading.sqrMagnitude > 1e-6f)
            {
                float angle = Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg;
                missile.transform.rotation = Quaternion.Euler(0f, 0f, angle);

                // Flying left the art would be upside down, lit from below.
                bool leftward = heading.x < 0f;
                missile.flipY = leftward;
                if (flame != null) flame.flipY = leftward;
            }

            bool lit = t >= ignitionAt;

            if (flame != null)
            {
                Show(flame, lit);
                if (lit)
                {
                    flame.transform.localScale = new Vector3(_flameScale.x * Random.Range(0.75f, 1.2f),
                                                             _flameScale.y * Random.Range(0.85f, 1.1f),
                                                             _flameScale.z);
                }
            }

            if (!lit) return;

            Vector3 back = -heading.normalized;
            Vector3 nozzle = missile.transform.TransformPoint(new Vector3(nozzleOffset, 0f, 0f));

            if (!_trailStarted)
            {
                _trailStarted = true;
                _lastNozzle = nozzle;
                EmitPuff(nozzle, back);
                return;
            }

            // Puffs laid along the path flown since last frame, evenly spaced.
            Vector3 travelled = nozzle - _lastNozzle;
            float length = travelled.magnitude;
            float spacing = Mathf.Max(0.02f, trailSpacing);
            int emitted = 0;

            _trailDistance += length;
            while (_trailDistance >= spacing && emitted < 40)
            {
                _trailDistance -= spacing;
                float along = length > 1e-5f ? 1f - _trailDistance / length : 1f;
                EmitPuff(Vector3.Lerp(_lastNozzle, nozzle, Mathf.Clamp01(along)), back);
                emitted++;
            }

            _lastNozzle = nozzle;
        }

        // ---- impact ---------------------------------------------------------------- //

        private void Impact(float elapsed)
        {
            if (!_impacted)
            {
                _impacted = true;
                Show(marker, false);
                Show(laser, false);
                Show(missile, false);
                Show(flame, false);
                ShakeCamera();
            }

            bool lethal = elapsed < blastDuration;
            _hazard.Armed = lethal;
            if (lethal) KillAnyoneStandingInIt();

            PlayBlast(elapsed);
            PlayScorch(elapsed);

            if (elapsed < Mathf.Max(blastVisualDuration, scorchDuration) || _puffs.Count > 0) return;

            _running = false;
            _hazard.Armed = false;
            Show(blast, false);
            Show(scorch, false);
            gameObject.SetActive(false);
        }

        private void PlayBlast(float elapsed)
        {
            if (blast == null) return;

            if (blastFrames != null && blastFrames.Length > 0)
            {
                float u = elapsed / Mathf.Max(0.01f, blastVisualDuration);
                if (u >= 1f)
                {
                    Show(blast, false);
                    return;
                }

                Show(blast, true);
                blast.sprite = blastFrames[Mathf.Min(blastFrames.Length - 1, (int)(u * blastFrames.Length))];
                return;
            }

            // No frames: one sprite, grown and faded across the lethal window.
            float t = elapsed / Mathf.Max(0.01f, blastDuration);
            if (t >= 1f)
            {
                Show(blast, false);
                return;
            }

            Show(blast, true);
            blast.transform.localScale = _blastScale * Mathf.Lerp(0.5f, 1.15f, Mathf.Sqrt(t));
            Color colour = blast.color;
            colour.a = Mathf.Lerp(1f, 0f, t * t);
            blast.color = colour;
        }

        private void PlayScorch(float elapsed)
        {
            if (scorch == null) return;

            float fade = Mathf.Max(0.01f, scorchDuration / 3f);
            float alpha = Mathf.Clamp01(elapsed / 0.15f) *
                          Mathf.Clamp01((scorchDuration - elapsed) / fade);

            Show(scorch, alpha > 0f);
            Color colour = _scorchColour;
            colour.a *= alpha;
            scorch.color = colour;
        }

        private void ShakeCamera()
        {
            if (impactShake <= 0f) return;

            Camera view = Camera.main;
            if (view == null) return;

            // Only a strike the player can see moves the picture.
            if (Mathf.Abs(view.transform.position.x - transform.position.x) > 20f) return;

            var follow = view.GetComponent<CameraFollow>();
            if (follow != null) follow.Shake(impactShake, impactShakeDuration);
        }

        // ---- smoke trail ---------------------------------------------------------- //

        private void EmitPuff(Vector3 at, Vector3 back)
        {
            if (smokePuffs == null || smokePuffs.Length == 0) return;

            SpriteRenderer renderer = _sparePuffs.Count > 0 ? _sparePuffs.Pop() : NewPuffRenderer();
            renderer.sprite = smokePuffs[Random.Range(0, smokePuffs.Length)];
            renderer.transform.position = at + (Vector3)(Random.insideUnitCircle * 0.05f);
            renderer.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            renderer.enabled = true;

            var puff = new Puff
            {
                Renderer = renderer,
                Life = puffLife * Random.Range(0.8f, 1.2f),
                Velocity = back * Random.Range(0.3f, 0.8f) + Vector3.up * Random.Range(0.25f, 0.5f),
                Spin = Random.Range(-40f, 40f),
                Shade = Random.Range(0.85f, 1f)
            };

            _puffs.Add(puff);
            ApplyPuff(puff);
        }

        private SpriteRenderer NewPuffRenderer()
        {
            var go = new GameObject("SmokePuff");
            go.transform.SetParent(transform, true);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 11;
            return renderer;
        }

        private void UpdatePuffs(float dt)
        {
            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                Puff puff = _puffs[i];
                puff.Age += dt;

                if (puff.Age >= puff.Life)
                {
                    puff.Renderer.enabled = false;
                    _sparePuffs.Push(puff.Renderer);
                    _puffs.RemoveAt(i);
                    continue;
                }

                // Air drag slows the thrown-back smoke; warmth lifts it.
                puff.Velocity *= Mathf.Max(0f, 1f - 1.8f * dt);
                puff.Velocity += Vector3.up * (0.15f * dt);
                puff.Renderer.transform.position += puff.Velocity * dt;
                puff.Renderer.transform.Rotate(0f, 0f, puff.Spin * dt);

                _puffs[i] = puff;
                ApplyPuff(puff);
            }
        }

        /// <summary>Spreads as it ages and thins away, greying as it goes.</summary>
        private void ApplyPuff(Puff puff)
        {
            float u = puff.Age / puff.Life;
            float size = Mathf.Lerp(puffStartSize, puffEndSize, 1f - (1f - u) * (1f - u));

            float width = puff.Renderer.sprite != null ? puff.Renderer.sprite.bounds.size.x : 1f;
            float scale = size / Mathf.Max(0.01f, width);
            puff.Renderer.transform.localScale = new Vector3(scale, scale, 1f);

            float alpha = 0.6f * (1f - u) * (1f - u) * Mathf.Clamp01(puff.Age * 30f);
            float shade = Mathf.Lerp(0.95f, 0.8f, u) * puff.Shade;
            puff.Renderer.color = new Color(shade, shade, shade, alpha);
        }

        // ---- helpers ----------------------------------------------------------------- //

        /// <summary>Lays a sprite along the line from a to b.</summary>
        private static void Stretch(SpriteRenderer renderer, Vector3 a, Vector3 b, float thickness, float alpha)
        {
            Vector3 along = b - a;
            float length = along.magnitude;

            if (renderer.sprite == null || length < 0.01f)
            {
                renderer.enabled = false;
                return;
            }

            renderer.transform.position = (a + b) * 0.5f;
            renderer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
            renderer.transform.localScale = new Vector3(length / renderer.sprite.bounds.size.x, thickness, 1f);

            Color colour = renderer.color;
            colour.a = alpha;
            renderer.color = colour;
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
