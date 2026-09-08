using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// An obstacle that travels the other way down the level, so it closes on
    /// the runner at both speeds at once.
    ///
    /// Everything else in the game either sits still or moves on a cycle, which
    /// means it can just run from the moment the level is built. A one-way
    /// traveller cannot: laid out with the chunk it belongs to, it would drive
    /// off the far end of the level long before the runner arrived. So it waits
    /// where it was authored and starts when the runner is close enough.
    ///
    /// <b>That distance has a floor, and it is not a matter of taste.</b> The
    /// camera shows about 18 units ahead of the runner. Wake the obstacle any
    /// nearer than that and the player watches it stand still on screen and then
    /// snap into motion. It has to already be moving by the time it is first
    /// seen, so activationDistance is well clear of the view.
    /// </summary>
    public class MovingObstacle : MonoBehaviour
    {
        [Header("Travel")]
        [Tooltip("Leftward speed, in world units per second. Add the runner's own " +
                 "speed to get how fast the two actually close.")]
        [SerializeField] private float speed = 9f;

        [Tooltip("How far ahead of the runner this starts moving. Must exceed the " +
                 "~18 units the camera shows, or it is seen parked first.")]
        [SerializeField] private float activationDistance = 30f;

        [Header("Clean-up")]
        [Tooltip("Removed once it is this far behind the runner.")]
        [SerializeField] private float despawnBehind = 18f;

        /// <summary>True once the runner has come close enough to set it off.</summary>
        public bool Moving { get; private set; }

        private Transform _player;

        private void Update()
        {
            Transform player = Player();
            if (player == null) return;

            if (!Moving)
            {
                if (transform.position.x - player.position.x > activationDistance) return;
                Moving = true;
            }

            transform.position += Vector3.left * (speed * Time.deltaTime);

            if (transform.position.x < player.position.x - despawnBehind)
                gameObject.SetActive(false);
        }

        /// <summary>
        /// The runner, found once and kept. GameManager holds the authoritative
        /// reference; the search is a fallback so a chunk still behaves when it
        /// is dropped into a scene on its own to be looked at.
        /// </summary>
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
            // Where it wakes up, and the edge of what the camera shows. The first
            // marker has to sit outside the second.
            Vector3 origin = transform.position;

            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.9f);
            Gizmos.DrawLine(origin + Vector3.down * 3f, origin + Vector3.up * 3f);

            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.35f);
            Gizmos.DrawLine(origin, origin + Vector3.left * activationDistance);
        }
    }
}
