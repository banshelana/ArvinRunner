using System;
using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// The runner's power meter: fills while they are running, and once full
    /// buys one super jump.
    ///
    /// It does not tick itself. <see cref="PlayerController"/> charges it from
    /// inside FixedUpdate, past the guards that stop the run - so it does not
    /// fill on the start line, while paused, after the finish, or while dead.
    /// A meter that charged through the death screen would hand the player a
    /// free jump for having just failed, and one that charged on the start line
    /// would make the first fifteen seconds of a level depend on how long they
    /// looked at the menu.
    ///
    /// Charge is kept once full. It waits to be spent rather than draining, so
    /// hesitating is never punished and the player can hold it for a stretch
    /// they know is coming.
    /// </summary>
    public class SuperJumpMeter : MonoBehaviour
    {
        [Tooltip("Seconds of running to fill the meter from empty.")]
        [SerializeField] private float chargeSeconds = 15f;

        [Tooltip("How full it starts a level. Above zero gives the player " +
                 "something early rather than a flat fifteen second wait.")]
        [Range(0f, 1f)]
        [SerializeField] private float startCharge;

        /// <summary>How full, 0 to 1. What the gauge draws.</summary>
        public float Charge01 { get; private set; }

        public bool IsReady => Charge01 >= 1f;

        /// <summary>Raised when it fills, and again when it is spent.</summary>
        public event Action<bool> OnReadyChanged;

        public float ChargeSeconds => chargeSeconds;

        private void Awake() => ResetCharge();

        /// <summary>Back to the starting charge. Called when a level loads.</summary>
        public void ResetCharge()
        {
            bool wasReady = IsReady;
            Charge01 = Mathf.Clamp01(startCharge);

            if (wasReady != IsReady) OnReadyChanged?.Invoke(IsReady);
        }

        /// <summary>Adds a slice of running time.</summary>
        public void Charge(float deltaSeconds)
        {
            if (IsReady || chargeSeconds <= 0.01f) return;

            bool wasReady = IsReady;
            Charge01 = Mathf.Clamp01(Charge01 + deltaSeconds / chargeSeconds);

            if (!wasReady && IsReady) OnReadyChanged?.Invoke(true);
        }

        /// <summary>
        /// Spends the charge if there is one. Returns whether there was, so the
        /// caller can decide what to do in the same breath as asking.
        /// </summary>
        public bool TrySpend()
        {
            if (!IsReady) return false;

            Charge01 = 0f;
            OnReadyChanged?.Invoke(false);
            return true;
        }
    }
}
