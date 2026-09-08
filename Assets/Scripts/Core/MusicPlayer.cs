using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// The looping track for whichever scene it sits in: the chase during a run,
    /// the menu theme on the front end.
    ///
    /// One per scene rather than one that survives the load. The two scenes want
    /// different music and a scene change is already a hard cut, so nothing is
    /// gained by keeping an object alive across it - and a persistent player
    /// would have to be taught not to duplicate itself on every return to the
    /// menu, which is more machinery than the problem deserves.
    ///
    /// Fades run on unscaled time. The pause screen sets timeScale to zero, and
    /// a fade that stalls there would leave the track stuck half way up.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;

        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.55f;

        [Tooltip("Seconds to come up from silence when the scene opens.")]
        [SerializeField] private float fadeIn = 1.2f;

        [Tooltip("Seconds to fade the old track out when a new one is asked for.")]
        [SerializeField] private float crossFade = 0.6f;

        private AudioSource _source;

        private AudioClip _pending;
        private float _fadeRemaining;
        private float _fadeLength;
        private bool _fadingOut;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;   // music is not in the world
            _source.volume = 0f;
        }

        private void Start()
        {
            if (clip != null) Begin(clip, fadeIn);
        }

        /// <summary>
        /// Switches to another track, fading down and back up. Asking for the
        /// track that is already playing does nothing, so a level that names the
        /// scene's own music does not restart it.
        /// </summary>
        public void Play(AudioClip next)
        {
            if (next == null || _source == null) return;
            if (_source.clip == next && _source.isPlaying) return;

            if (!_source.isPlaying)
            {
                Begin(next, fadeIn);
                return;
            }

            _pending = next;
            _fadingOut = true;
            _fadeLength = Mathf.Max(0.01f, crossFade);
            _fadeRemaining = _fadeLength;
        }

        /// <summary>Fades out and stops, for whatever wants silence.</summary>
        public void StopMusic()
        {
            _pending = null;
            _fadingOut = true;
            _fadeLength = Mathf.Max(0.01f, crossFade);
            _fadeRemaining = _fadeLength;
        }

        public void SetVolume(float value)
        {
            volume = Mathf.Clamp01(value);
            if (!_fadingOut && _fadeRemaining <= 0f && _source != null)
                _source.volume = volume;
        }

        private void Begin(AudioClip next, float fade)
        {
            _source.clip = next;
            _source.volume = 0f;
            _source.Play();

            _fadingOut = false;
            _fadeLength = Mathf.Max(0.01f, fade);
            _fadeRemaining = _fadeLength;
        }

        private void Update()
        {
            if (_source == null || _fadeRemaining <= 0f) return;

            _fadeRemaining -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(1f - _fadeRemaining / _fadeLength);

            if (_fadingOut)
            {
                _source.volume = Mathf.Lerp(volume, 0f, t);

                if (_fadeRemaining > 0f) return;

                if (_pending != null)
                {
                    AudioClip next = _pending;
                    _pending = null;
                    Begin(next, crossFade);
                }
                else
                {
                    _source.Stop();
                }

                return;
            }

            _source.volume = Mathf.Lerp(0f, volume, t);
        }
    }
}
