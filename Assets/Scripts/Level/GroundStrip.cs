using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// A resizable slab of ground. The builder uses these for the run-up and
    /// run-out pads, and chunk prefabs can use them for rooftops too.
    ///
    /// The sprite renderer should be set to Tiled draw mode so the texture
    /// repeats instead of stretching.
    /// </summary>
    [ExecuteAlways]
    public class GroundStrip : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private BoxCollider2D box;

        [Tooltip("Width in world units.")]
        public float width = 10f;
        [Tooltip("Thickness in world units. The top surface stays at y = 0 locally.")]
        public float thickness = 2f;

        private void Awake() => Cache();
        private void OnValidate() { Cache(); Apply(); }

        private void Cache()
        {
            if (body == null) body = GetComponentInChildren<SpriteRenderer>();
            if (box == null) box = GetComponent<BoxCollider2D>();
        }

        /// <summary>Resize and re-anchor so the walkable surface sits at local y = 0.</summary>
        public void Resize(float newWidth, float newThickness)
        {
            width = Mathf.Max(0.1f, newWidth);
            thickness = Mathf.Max(0.1f, newThickness);
            Apply();
        }

        public void Apply()
        {
            if (box != null)
            {
                box.size = new Vector2(width, thickness);
                box.offset = new Vector2(width * 0.5f, -thickness * 0.5f);
            }

            if (body != null)
            {
                if (body.drawMode == SpriteDrawMode.Simple) body.drawMode = SpriteDrawMode.Tiled;
                body.size = new Vector2(width, thickness);
                body.transform.localPosition = new Vector3(width * 0.5f, -thickness * 0.5f, 0f);
            }
        }
    }
}
