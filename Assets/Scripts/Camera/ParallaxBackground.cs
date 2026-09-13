using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Builds and drives the scrolling backdrop behind the runner.
    ///
    /// Each layer is one tiled SpriteRenderer, three screens wide. Rather than
    /// spawning and recycling copies, the layer is re-anchored every frame to
    /// the nearest whole tile width, so it appears to scroll forever with a
    /// fixed number of objects.
    /// </summary>
    [ExecuteAlways]
    public class ParallaxBackground : MonoBehaviour
    {
        [SerializeField] private ParallaxTheme theme;
        [SerializeField] private new Camera camera;
        [Tooltip("How many screen widths each layer covers. 3 is plenty.")]
        [SerializeField] private float widthMultiplier = 3f;

        private readonly List<LayerRuntime> _layers = new List<LayerRuntime>();
        private ParallaxTheme _builtTheme;
        private Vector3 _origin;

        private class LayerRuntime
        {
            public ParallaxLayerDef Def;
            public Transform Transform;
            public SpriteRenderer Renderer;
            public float TileWidth;
            public float Drift;
        }

        private void Awake()
        {
            if (camera == null) camera = Camera.main;
            _origin = transform.position;
        }

        private void OnEnable()
        {
            if (camera == null) camera = Camera.main;
            Rebuild();
        }

        /// <summary>Swap the backdrop, e.g. when loading a new level.</summary>
        public void SetTheme(ParallaxTheme newTheme)
        {
            theme = newTheme;
            Rebuild();
        }

        private void Rebuild()
        {
            ClearLayers();
            _builtTheme = theme;

            if (theme == null || camera == null) return;

            if (camera.orthographic)
                camera.backgroundColor = theme.skyColour;

            float screenWidth = camera.orthographicSize * 2f * camera.aspect;
            float totalWidth = Mathf.Max(1f, screenWidth * widthMultiplier);

            if (theme.layers == null) return;

            for (int i = 0; i < theme.layers.Length; i++)
            {
                ParallaxLayerDef def = theme.layers[i];
                if (def == null || def.sprite == null) continue;

                var go = new GameObject("Parallax_" + def.name);
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetParent(transform, false);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = def.sprite;
                renderer.color = def.tint;
                renderer.sortingOrder = def.sortingOrder;

                if (def.tiled)
                {
                    renderer.drawMode = SpriteDrawMode.Tiled;
                    renderer.tileMode = SpriteTileMode.Continuous;

                    float spriteHeight = def.sprite.bounds.size.y;
                    float height = def.heightOverride > 0f ? def.heightOverride : spriteHeight;
                    renderer.size = new Vector2(totalWidth, height);
                }
                else
                {
                    renderer.drawMode = SpriteDrawMode.Simple;
                }

                _layers.Add(new LayerRuntime
                {
                    Def = def,
                    Transform = go.transform,
                    Renderer = renderer,
                    TileWidth = Mathf.Max(0.01f, def.sprite.bounds.size.x)
                });
            }
        }

        private void ClearLayers()
        {
            foreach (LayerRuntime layer in _layers)
            {
                if (layer.Transform == null) continue;
                if (Application.isPlaying) Destroy(layer.Transform.gameObject);
                else DestroyImmediate(layer.Transform.gameObject);
            }

            _layers.Clear();
        }

        private void LateUpdate()
        {
            if (camera == null) camera = Camera.main;
            if (camera == null) return;

            // Picks up a theme swap, in play mode or while editing. Done here
            // rather than in OnValidate, which must not create or destroy
            // objects.
            if (_builtTheme != theme) Rebuild();

            Vector3 cam = camera.transform.position;

            foreach (LayerRuntime layer in _layers)
            {
                ParallaxLayerDef def = layer.Def;

                layer.Drift += def.autoScrollSpeed * Time.deltaTime;

                if (!def.tiled)
                {
                    // One object, carried along with the camera at its parallax -
                    // at 0 it sits at the same place on screen, as the sun does.
                    float fx = cam.x * (1f - def.parallax) + def.xOffset;
                    float fy = _origin.y + def.yOffset + cam.y * (1f - def.verticalParallax);
                    layer.Transform.position = new Vector3(fx, fy, layer.Transform.position.z);
                    continue;
                }

                // How far this layer has slipped behind the camera.
                float slip = cam.x * def.parallax - layer.Drift;

                // Snap the anchor to whole tiles so the seam never shows.
                float wrapped = Mathf.Floor(slip / layer.TileWidth) * layer.TileWidth;

                float x = cam.x - slip + wrapped;
                float y = _origin.y + def.yOffset + cam.y * (1f - def.verticalParallax);

                // Centre the strip on the camera horizontally.
                layer.Transform.position = new Vector3(x, y, layer.Transform.position.z);
            }
        }
    }
}
