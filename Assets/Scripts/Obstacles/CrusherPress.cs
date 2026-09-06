using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A press that slams down on a cycle. You slide under it during the gap.
    /// The head should carry a Hazard so a mistimed run is fatal.
    /// </summary>
    public class CrusherPress : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private float travel = 3f;
        [SerializeField] private float slamSpeed = 14f;
        [SerializeField] private float riseSpeed = 3.5f;
        [SerializeField] private float holdDown = 0.35f;
        [SerializeField] private float holdUp = 1.1f;
        [SerializeField, Range(0f, 1f)] private float phase;

        private Vector3 _topPosition;
        private float _timer;
        private int _stage;   // 0 up, 1 slamming, 2 down, 3 rising

        private void Awake()
        {
            if (head == null) head = transform;
            _topPosition = head.localPosition;
            _timer = phase * (holdUp + holdDown);
        }

        private void Update()
        {
            Vector3 bottom = _topPosition + Vector3.down * travel;

            switch (_stage)
            {
                case 0:
                    _timer += Time.deltaTime;
                    if (_timer >= holdUp) { _timer = 0f; _stage = 1; }
                    break;

                case 1:
                    head.localPosition = Vector3.MoveTowards(head.localPosition, bottom, slamSpeed * Time.deltaTime);
                    if (Vector3.Distance(head.localPosition, bottom) < 0.01f) { _timer = 0f; _stage = 2; }
                    break;

                case 2:
                    _timer += Time.deltaTime;
                    if (_timer >= holdDown) { _timer = 0f; _stage = 3; }
                    break;

                case 3:
                    head.localPosition = Vector3.MoveTowards(head.localPosition, _topPosition, riseSpeed * Time.deltaTime);
                    if (Vector3.Distance(head.localPosition, _topPosition) < 0.01f) { _timer = 0f; _stage = 0; }
                    break;
            }
        }
    }
}
