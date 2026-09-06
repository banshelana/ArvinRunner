using UnityEngine;

namespace ArvinRunner
{
    /// <summary>The end of a level. Crossing it completes the run.</summary>
    public class FinishLine : MonoBehaviour
    {
        [SerializeField] private GameObject celebrationEffect;

        private bool _triggered;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_triggered) return;

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || !player.IsAlive) return;

            _triggered = true;

            if (celebrationEffect != null)
                Instantiate(celebrationEffect, transform.position, Quaternion.identity);

            if (GameManager.Instance != null) GameManager.Instance.CompleteLevel();
            else player.Finish();
        }
    }
}
