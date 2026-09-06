using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner
{
    public enum Swipe { None, Up, Down, Left, Right, Tap }

    /// <summary>
    /// Turns raw touches into buffered swipe gestures.
    ///
    /// Two details make this feel responsive rather than sluggish:
    ///  1. A swipe fires the moment the finger passes the distance threshold -
    ///     we do not wait for the finger to lift.
    ///  2. Gestures are buffered for a short window, so a swipe made slightly
    ///     before landing still triggers the jump on touchdown.
    ///
    /// Keyboard is read as well so the game is testable in the Editor.
    /// </summary>
    public class SwipeInput : MonoBehaviour
    {
        public static SwipeInput Instance { get; private set; }

        [Header("Gesture recognition")]
        [Tooltip("Swipe length needed to register, as a fraction of screen height.")]
        [SerializeField, Range(0.01f, 0.25f)] private float minSwipeFraction = 0.05f;
        [Tooltip("A gesture slower than this is treated as a drag, not a swipe.")]
        [SerializeField] private float maxSwipeTime = 0.5f;
        [Tooltip("A touch that moves less than this fraction counts as a tap on release.")]
        [SerializeField, Range(0.001f, 0.05f)] private float tapMoveFraction = 0.02f;

        [Header("Buffering")]
        [Tooltip("How long a gesture stays available for a state to consume.")]
        [SerializeField] private float bufferTime = 0.15f;

        [Header("Debug")]
        [SerializeField] private bool logGestures;

        /// <summary>Raised the instant a gesture is recognised.</summary>
        public event System.Action<Swipe> OnSwipe;

        /// <summary>True while at least one finger (or the mouse) is held down.</summary>
        public bool IsHolding { get; private set; }

        private readonly Dictionary<int, TouchTrack> _tracks = new Dictionary<int, TouchTrack>();
        private Swipe _buffered = Swipe.None;
        private float _bufferedAt = -99f;

        private struct TouchTrack
        {
            public Vector2 Start;
            public float StartTime;
            public bool Resolved;   // gesture already fired for this finger
            public bool Moved;
        }

        private float MinSwipePixels => Screen.height * minSwipeFraction;
        private float TapMovePixels => Screen.height * tapMoveFraction;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            ReadTouches();
            ReadMouse();
            ReadKeyboard();

            // Expire the buffer so a stale gesture cannot fire much later.
            if (_buffered != Swipe.None && Time.unscaledTime - _bufferedAt > bufferTime)
                _buffered = Swipe.None;
        }

        // ---------------------------------------------------------------- //
        // Public API
        // ---------------------------------------------------------------- //

        /// <summary>
        /// Returns true (and clears the buffer) if the given gesture was made
        /// within the buffer window. States call this to claim an input.
        /// </summary>
        public bool Consume(Swipe swipe)
        {
            if (_buffered != swipe) return false;
            if (Time.unscaledTime - _bufferedAt > bufferTime) return false;
            _buffered = Swipe.None;
            return true;
        }

        /// <summary>Peek without consuming.</summary>
        public bool IsPending(Swipe swipe)
        {
            return _buffered == swipe && Time.unscaledTime - _bufferedAt <= bufferTime;
        }

        public void ClearBuffer() => _buffered = Swipe.None;

        // ---------------------------------------------------------------- //
        // Recognition
        // ---------------------------------------------------------------- //

        private void ReadTouches()
        {
            if (Input.touchCount == 0)
            {
                if (_tracks.Count > 0) _tracks.Clear();
                IsHolding = false;
                return;
            }

            IsHolding = true;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        _tracks[t.fingerId] = new TouchTrack
                        {
                            Start = t.position,
                            StartTime = Time.unscaledTime,
                            Resolved = false,
                            Moved = false
                        };
                        break;

                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        EvaluateDrag(t.fingerId, t.position);
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        EvaluateRelease(t.fingerId, t.position);
                        _tracks.Remove(t.fingerId);
                        break;
                }
            }
        }

        // Mouse stands in for a finger so the game is playable in the Editor.
        private void ReadMouse()
        {
            if (Input.touchCount > 0) return;   // real touches win
            const int mouseId = -1;

            if (Input.GetMouseButtonDown(0))
            {
                _tracks[mouseId] = new TouchTrack
                {
                    Start = Input.mousePosition,
                    StartTime = Time.unscaledTime,
                    Resolved = false,
                    Moved = false
                };
                IsHolding = true;
            }
            else if (Input.GetMouseButton(0))
            {
                EvaluateDrag(mouseId, Input.mousePosition);
                IsHolding = true;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                EvaluateRelease(mouseId, Input.mousePosition);
                _tracks.Remove(mouseId);
                IsHolding = false;
            }
        }

        private void ReadKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                Fire(Swipe.Up);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                Fire(Swipe.Down);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                Fire(Swipe.Right);
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                Fire(Swipe.Left);
        }

        /// <summary>Fires as soon as the finger travels far enough - no wait for release.</summary>
        private void EvaluateDrag(int id, Vector2 position)
        {
            if (!_tracks.TryGetValue(id, out TouchTrack track) || track.Resolved) return;

            Vector2 delta = position - track.Start;
            if (delta.sqrMagnitude > TapMovePixels * TapMovePixels)
            {
                track.Moved = true;
                _tracks[id] = track;
            }

            if (Time.unscaledTime - track.StartTime > maxSwipeTime) return;
            if (delta.magnitude < MinSwipePixels) return;

            Fire(DirectionOf(delta));
            track.Resolved = true;
            _tracks[id] = track;
        }

        private void EvaluateRelease(int id, Vector2 position)
        {
            if (!_tracks.TryGetValue(id, out TouchTrack track) || track.Resolved) return;

            Vector2 delta = position - track.Start;
            bool quick = Time.unscaledTime - track.StartTime <= maxSwipeTime;

            if (quick && delta.magnitude >= MinSwipePixels)
                Fire(DirectionOf(delta));
            else if (!track.Moved)
                Fire(Swipe.Tap);
        }

        private static Swipe DirectionOf(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                return delta.x > 0f ? Swipe.Right : Swipe.Left;
            return delta.y > 0f ? Swipe.Up : Swipe.Down;
        }

        private void Fire(Swipe swipe)
        {
            if (swipe == Swipe.None) return;
            _buffered = swipe;
            _bufferedAt = Time.unscaledTime;
            if (logGestures) Debug.Log("[SwipeInput] " + swipe);
            OnSwipe?.Invoke(swipe);
        }
    }
}
