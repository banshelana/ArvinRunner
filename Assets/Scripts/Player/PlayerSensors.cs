using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// All the world queries the player controller needs, in one place.
    /// Refreshed once per FixedUpdate so every state reads a consistent picture.
    ///
    /// The runner always faces +X, which keeps the probes simple.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider2D))]
    public class PlayerSensors : MonoBehaviour
    {
        [Header("Probe tuning")]
        [SerializeField] private float groundProbeThickness = 0.12f;
        [Tooltip("Shrinks the ground box slightly so we do not catch adjacent walls.")]
        [SerializeField] private float groundProbeInset = 0.06f;
        [SerializeField] private float ceilingProbeDistance = 0.15f;
        [Tooltip("Height above the feet used for the low (vault) probe.")]
        [SerializeField] private float kneeHeight = 0.35f;

        [Header("Gizmos")]
        [SerializeField] private bool drawGizmos = true;

        // ---- Results, refreshed by Sample() ---------------------------- //

        public bool Grounded { get; private set; }
        public Vector2 GroundNormal { get; private set; } = Vector2.up;
        public Collider2D GroundCollider { get; private set; }

        /// <summary>True when standing up would clip into geometry (stay sliding).</summary>
        public bool CeilingBlocked { get; private set; }

        public bool WallAhead { get; private set; }
        public Collider2D WallCollider { get; private set; }

        /// <summary>A wall whose top edge is within grabbing range.</summary>
        public bool LedgeAhead { get; private set; }
        public float LedgeTopY { get; private set; }

        /// <summary>A low obstacle that can be vaulted rather than jumped.</summary>
        public bool VaultAhead { get; private set; }
        public float VaultTopY { get; private set; }
        public Collider2D VaultCollider { get; private set; }

        private CapsuleCollider2D _capsule;
        private PlayerConfig _config;

        private Vector2 FeetPosition
        {
            get
            {
                Bounds b = _capsule.bounds;
                return new Vector2(b.center.x, b.min.y);
            }
        }

        public float StandingHeight { get; private set; }
        public float Width => _capsule != null ? _capsule.bounds.size.x : 0.5f;

        private void Awake()
        {
            _capsule = GetComponent<CapsuleCollider2D>();
            StandingHeight = _capsule.size.y;
        }

        public void Initialise(PlayerConfig config)
        {
            _config = config;
            if (_capsule == null) _capsule = GetComponent<CapsuleCollider2D>();
        }

        /// <summary>Run every physics step before the state machine ticks.</summary>
        public void Sample()
        {
            SampleGround();
            SampleCeiling();
            SampleWallAndLedge();
            SampleVault();
        }

        // ---------------------------------------------------------------- //

        private void SampleGround()
        {
            Bounds b = _capsule.bounds;
            Vector2 size = new Vector2(Mathf.Max(0.05f, b.size.x - groundProbeInset * 2f), groundProbeThickness);
            Vector2 center = new Vector2(b.center.x, b.min.y - groundProbeThickness * 0.5f + 0.02f);

            Collider2D hit = Physics2D.OverlapBox(center, size, 0f, GameLayers.SolidMask);
            Grounded = hit != null;
            GroundCollider = hit;

            if (Grounded)
            {
                // A short downward ray gives us the surface normal for slopes.
                RaycastHit2D ray = Physics2D.Raycast(
                    new Vector2(b.center.x, b.min.y + 0.05f),
                    Vector2.down,
                    groundProbeThickness + 0.15f,
                    GameLayers.SolidMask);
                GroundNormal = ray.collider != null ? ray.normal : Vector2.up;
            }
            else
            {
                GroundNormal = Vector2.up;
            }
        }

        private void SampleCeiling()
        {
            Bounds b = _capsule.bounds;
            // Probe the full standing height from the feet, not the current
            // (possibly shrunk) collider, so we know if it is safe to stand.
            Vector2 size = new Vector2(Mathf.Max(0.05f, b.size.x - groundProbeInset * 2f), 0.1f);
            Vector2 center = new Vector2(b.center.x, FeetPosition.y + StandingHeight + ceilingProbeDistance);

            CeilingBlocked = Physics2D.OverlapBox(center, size, 0f, GameLayers.SolidMask) != null;
        }

        private void SampleWallAndLedge()
        {
            WallAhead = false;
            LedgeAhead = false;
            WallCollider = null;

            if (_config == null) return;

            Bounds b = _capsule.bounds;
            float front = b.max.x;
            float distance = _config.wallProbeDistance;

            // Chest-height probe: is there a vertical surface right in front?
            Vector2 chest = new Vector2(front, b.center.y);
            RaycastHit2D chestHit = Physics2D.Raycast(chest, Vector2.right, distance,
                                                      GameLayers.WallMask | GameLayers.GroundMask);

            if (chestHit.collider == null) return;

            // Only near-vertical faces count as walls.
            if (Mathf.Abs(chestHit.normal.x) < 0.7f) return;

            WallAhead = true;
            WallCollider = chestHit.collider;

            if (!_config.ledgeGrabEnabled) return;

            // Grabbable when the top edge is above the feet - you cannot grab
            // something you have already cleared - but no higher than the runner
            // can reach.
            //
            // This used to be a narrow band either side of head height, which
            // left a dead zone: approach a wall slightly too high and the grab
            // never offered itself, so the runner could only bounce off the face.
            float wallTop = chestHit.collider.bounds.max.y;
            float head = b.max.y;
            float feet = b.min.y;

            if (wallTop > feet + 0.2f && wallTop <= head + _config.ledgeGrabTolerance)
            {
                LedgeAhead = true;
                LedgeTopY = wallTop;
            }
        }

        private void SampleVault()
        {
            VaultAhead = false;
            VaultCollider = null;

            if (_config == null || !Grounded) return;

            Bounds b = _capsule.bounds;
            float front = b.max.x;
            Vector2 feet = FeetPosition;
            float distance = _config.vaultProbeDistance;
            LayerMask mask = GameLayers.VaultableMask | GameLayers.GroundMask | GameLayers.WallMask;

            // Something at knee height...
            RaycastHit2D low = Physics2D.Raycast(new Vector2(front, feet.y + kneeHeight),
                                                 Vector2.right, distance, mask);
            if (low.collider == null) return;

            // ...but nothing above the vault ceiling, or it is too tall to clear.
            RaycastHit2D high = Physics2D.Raycast(new Vector2(front, feet.y + _config.maxVaultHeight + 0.1f),
                                                  Vector2.right, distance, mask);
            if (high.collider != null && high.collider == low.collider) return;

            float top = low.collider.bounds.max.y;
            if (top - feet.y > _config.maxVaultHeight) return;

            // There has to be somewhere to come down. Without this a low step
            // with a tall stack right behind it reads as vaultable, and the
            // vault arc ends inside the stack.
            float landingX = front + _config.vaultProbeDistance + Width * 0.5f;
            Vector2 landingCentre = new Vector2(landingX, top + StandingHeight * 0.5f + 0.05f);
            Vector2 landingSize = new Vector2(Width * 0.9f, StandingHeight * 0.9f);

            if (Physics2D.OverlapBox(landingCentre, landingSize, 0f, mask) != null) return;

            VaultAhead = true;
            VaultCollider = low.collider;
            VaultTopY = top;
        }

        // ---------------------------------------------------------------- //

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || _capsule == null) return;

            Bounds b = _capsule.bounds;

            Gizmos.color = Grounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(new Vector2(b.center.x, b.min.y - groundProbeThickness * 0.5f),
                                new Vector2(b.size.x - groundProbeInset * 2f, groundProbeThickness));

            if (_config != null)
            {
                Gizmos.color = WallAhead ? Color.cyan : Color.grey;
                Gizmos.DrawLine(new Vector2(b.max.x, b.center.y),
                                new Vector2(b.max.x + _config.wallProbeDistance, b.center.y));

                Gizmos.color = VaultAhead ? Color.yellow : Color.grey;
                Vector2 knee = new Vector2(b.max.x, b.min.y + kneeHeight);
                Gizmos.DrawLine(knee, knee + Vector2.right * _config.vaultProbeDistance);
            }
        }
    }
}
