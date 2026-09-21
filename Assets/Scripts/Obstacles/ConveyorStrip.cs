using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A belt let into the quay. While the runner is on it, it adds its own
    /// speed to his: with him if it runs forward, against him if it runs back.
    ///
    /// <b>It changes the run speed rather than pushing the body.</b> A push
    /// through AddExternalForce, which is how the wind zones work, does almost
    /// nothing here: DriveHorizontal pulls the runner back to his target speed
    /// at 30 a second every step, so any force under that is rubbed out again
    /// before the next frame. Wind can get away with it because it acts while
    /// the runner is in the air and nothing is driving him. A belt acts while he
    /// is on his feet, so it has to move the target he is being driven to.
    ///
    /// <b>What it is for is the jump after it.</b> On its own a belt is only a
    /// stretch of faster or slower running. Put a gap at the end of one and it
    /// is an obstacle: the same take-off that clears it from the plain roof
    /// comes up short off a belt running the other way, so the jump has to be
    /// taken earlier - and a belt running with the runner throws him further
    /// than he means to go.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ConveyorStrip : MonoBehaviour
    {
        [Tooltip("Added to the runner's speed while he is on it. Negative runs back " +
                 "against him. He can never be dragged to a standstill - the speed he " +
                 "is driven to has a floor - so a belt slows a run down and never " +
                 "quietly kills it.")]
        [SerializeField] private float drift = -3.2f;

        [Tooltip("The chevrons painted on it, which are slid along to show which way " +
                 "it runs. Without them a belt is a differently coloured piece of floor.")]
        [SerializeField] private Transform[] chevrons;

        [Tooltip("How far apart the chevrons are, so one can be sent back to the start " +
                 "when it reaches the end.")]
        [SerializeField] private float chevronSpacing = 1.2f;

        [Tooltip("How fast they slide, against the belt's own speed. Under 1, because " +
                 "a belt that scrolls at its true speed reads as a blur.")]
        [SerializeField] private float chevronScale = 0.45f;

        private PlayerController _on;
        private float _slide;

        private void Reset()
        {
            var box = GetComponent<Collider2D>();
            if (box != null) box.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null) _on = player;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() == _on) _on = null;
        }

        private void FixedUpdate()
        {
            // Only underfoot. In the air over a belt he is carrying whatever speed
            // he left it with, which is the whole point of putting a gap at the end.
            if (_on == null || !_on.IsAlive || !_on.Grounded) return;

            _on.SurfaceDrift = drift;
        }

        private void Update()
        {
            if (chevrons == null || chevrons.Length == 0) return;

            float span = Mathf.Max(0.05f, chevronSpacing * chevrons.Length);
            _slide = Mathf.Repeat(_slide + drift * chevronScale * Time.deltaTime, span);

            for (int i = 0; i < chevrons.Length; i++)
            {
                if (chevrons[i] == null) continue;

                Vector3 at = chevrons[i].localPosition;
                at.x = Mathf.Repeat(i * chevronSpacing + _slide, span);
                chevrons[i].localPosition = at;
            }
        }
    }
}
