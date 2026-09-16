using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Placeholder fire: a row of flame blocks that rise, fall and brighten on
    /// noise, each on its own rhythm, standing on their own bottom edges.
    ///
    /// Stands in until there is drawn fire. Scenery only - the Hazard on the pit
    /// is what kills.
    /// </summary>
    public class FireFlicker : MonoBehaviour
    {
        [Tooltip("Tiled sprites, each standing on its bottom edge.")]
        [SerializeField] private SpriteRenderer[] flames;

        [SerializeField] private float speed = 6f;

        [Tooltip("How far a flame's height swings, as a share of its authored height.")]
        [Range(0f, 1f)]
        [SerializeField] private float spread = 0.5f;

        private float[] _height;
        private float[] _bottom;
        private float[] _seed;
        private Color[] _colour;

        private void Awake()
        {
            int count = flames != null ? flames.Length : 0;
            _height = new float[count];
            _bottom = new float[count];
            _seed = new float[count];
            _colour = new Color[count];

            for (int i = 0; i < count; i++)
            {
                if (flames[i] == null) continue;

                _height[i] = flames[i].size.y;
                _bottom[i] = flames[i].transform.localPosition.y - _height[i] * 0.5f;
                _seed[i] = i * 1.37f + Random.value * 10f;
                _colour[i] = flames[i].color;
            }
        }

        private void Update()
        {
            if (flames == null) return;

            float time = Time.time * speed;

            for (int i = 0; i < flames.Length; i++)
            {
                SpriteRenderer flame = flames[i];
                if (flame == null) continue;

                float noise = Mathf.PerlinNoise(time * 0.35f + _seed[i], _seed[i]);
                float height = _height[i] * (1f - spread * 0.5f + spread * noise);

                flame.size = new Vector2(flame.size.x, height);

                Vector3 at = flame.transform.localPosition;
                at.y = _bottom[i] + height * 0.5f;
                flame.transform.localPosition = at;

                Color colour = _colour[i];
                colour.a *= 0.7f + 0.3f * noise;
                flame.color = colour;
            }
        }
    }
}
