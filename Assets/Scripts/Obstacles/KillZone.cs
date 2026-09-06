using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// The floor of the world. Falling into a pit lands here and ends the run.
    /// The level builder stretches one of these under the whole level.
    /// </summary>
    public class KillZone : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null && player.IsAlive)
                player.Kill(DeathCause.Fell);
        }
    }
}
