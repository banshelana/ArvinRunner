using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// One horizontal band of scenery - a skyline, a row of towers, a layer of
    /// haze. Layers are listed back to front.
    /// </summary>
    [System.Serializable]
    public class ParallaxLayerDef
    {
        public string name = "Layer";

        [Tooltip("Should tile seamlessly left-to-right. Set its Mesh Type to Full Rect on import.")]
        public Sprite sprite;

        [Tooltip("0 = pinned to the camera (infinitely far, e.g. sky). " +
                 "1 = locked to the world (moves like the level itself).")]
        [Range(0f, 1f)] public float parallax = 0.5f;

        [Tooltip("How much this layer follows the camera vertically. Usually lower than the horizontal value.")]
        [Range(0f, 1f)] public float verticalParallax = 0.3f;

        [Tooltip("Vertical placement relative to the camera centre.")]
        public float yOffset;

        [Tooltip("Draw order. Lower numbers render further back.")]
        public int sortingOrder = -100;

        public Color tint = Color.white;

        [Tooltip("Extra horizontal scroll per second, on top of the parallax. " +
                 "Good for drifting clouds.")]
        public float autoScrollSpeed;

        [Tooltip("Vertical size in world units. 0 keeps the sprite's own height.")]
        public float heightOverride;

        [Tooltip("Repeat the sprite along the strip. Off for a single object such as the " +
                 "sun, which is placed once and follows the camera instead.")]
        public bool tiled = true;

        [Tooltip("For a layer that is not tiled: horizontal position relative to the " +
                 "camera centre, in world units.")]
        public float xOffset;
    }

    /// <summary>
    /// The complete backdrop for a level. Swap themes between levels to change
    /// the time of day and the weather.
    ///
    /// Create via Assets > Create > ArvinRunner > Parallax Theme.
    /// </summary>
    [CreateAssetMenu(menuName = "ArvinRunner/Parallax Theme", fileName = "Theme_City")]
    public class ParallaxTheme : ScriptableObject
    {
        [Tooltip("Solid colour painted behind every layer.")]
        public Color skyColour = new Color(0.06f, 0.07f, 0.13f);

        [Tooltip("Back to front.")]
        public ParallaxLayerDef[] layers;
    }
}
