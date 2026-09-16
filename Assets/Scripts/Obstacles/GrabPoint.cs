using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Something the runner can catch in the air and be carried by - a rope's
    /// end, a hook on a cable.
    ///
    /// The runner does the catching: PlayerController asks for the nearest one
    /// within reach of its raised hand while airborne, and then, every step of
    /// the ride, asks where the hand is now. What carries it - a pendulum, a
    /// trolley - is entirely the grab point's business, and so is the velocity
    /// the runner leaves with. That keeps a new kind of thing to hang from down
    /// to one small class.
    ///
    /// Live ones register themselves, so the controller never searches the scene.
    /// </summary>
    public abstract class GrabPoint : MonoBehaviour
    {
        /// <summary>
        /// Seconds over which the hand is carried from where it actually closed onto
        /// the grip proper. A catch is rarely dead on, and snapping the body the
        /// last half-unit in a single step reads as a jolt.
        /// </summary>
        protected const float CatchBlend = 0.08f;

        private static readonly List<GrabPoint> Live = new List<GrabPoint>();

        protected virtual void OnEnable() => Live.Add(this);
        protected virtual void OnDisable() => Live.Remove(this);

        /// <summary>Where a hand closes on it, right now, in world space.</summary>
        public abstract Vector2 Grip { get; }

        /// <summary>False while it cannot be caught - already carrying someone, or used up.</summary>
        public virtual bool Available => true;

        /// <summary>A named clip to hang in, or null for the Swing slot's own.</summary>
        public virtual string HangClip => null;

        /// <summary>Starts carrying a runner whose hand closed at <paramref name="hand"/>. Returns how long the ride lasts.</summary>
        public abstract float BeginRide(Vector2 hand);

        /// <summary>Where the hand is, <paramref name="time"/> seconds into the ride.</summary>
        public abstract Vector2 RideGrip(float time);

        /// <summary>The velocity the runner lets go with.</summary>
        public abstract Vector2 ReleaseVelocity { get; }

        /// <summary>The runner has let go, or died holding on.</summary>
        public virtual void EndRide() { }

        /// <summary>The nearest available one whose grip is within <paramref name="radius"/> of the hand.</summary>
        public static GrabPoint Nearest(Vector2 hand, float radius, GrabPoint skip)
        {
            GrabPoint best = null;
            float bestSqr = radius * radius;

            for (int i = 0; i < Live.Count; i++)
            {
                GrabPoint point = Live[i];
                if (point == null || point == skip || !point.Available) continue;

                float sqr = (point.Grip - hand).sqrMagnitude;
                if (sqr > bestSqr) continue;

                bestSqr = sqr;
                best = point;
            }

            return best;
        }
    }
}
