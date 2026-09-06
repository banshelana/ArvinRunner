using System.Collections.Generic;
using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Assembles a playable level out of chunk prefabs.
    ///
    /// Layout rule: every chunk is authored with its entry point at local
    /// (0, 0), so the builder just walks right, dropping each chunk at the
    /// previous one's exit point. Chunks that step up or down carry the ground
    /// height with them via entryHeight / exitHeight.
    /// </summary>
    public class LevelBuilder : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("Resizable slab used for the run-up and run-out pads.")]
        [SerializeField] private GroundStrip padPrefab;
        [SerializeField] private FinishLine finishLinePrefab;
        [Tooltip("Trigger stretched under the level to catch falls.")]
        [SerializeField] private KillZone killZonePrefab;

        [Header("Layout")]
        [Tooltip("How far below the lowest ground the kill plane sits.")]
        [SerializeField] private float killZoneDepth = 14f;
        [SerializeField] private float padThickness = 3f;

        /// <summary>World position the runner starts from.</summary>
        public Vector3 SpawnPoint { get; private set; }

        /// <summary>Total X distance from spawn to the finish line.</summary>
        public float LevelLength { get; private set; }

        public LevelDefinition CurrentLevel { get; private set; }

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private Transform _root;

        /// <summary>Tears down the old level and builds the given one.</summary>
        public void Build(LevelDefinition level)
        {
            Clear();

            CurrentLevel = level;
            if (level == null)
            {
                Debug.LogError("[LevelBuilder] Asked to build a null level.", this);
                return;
            }

            _root = new GameObject("Level_" + level.levelNumber).transform;
            _root.SetParent(transform, false);

            float cursorX = 0f;
            float groundY = 0f;
            float lowestY = 0f;

            // --- run-up pad ------------------------------------------------
            if (level.startPadLength > 0f)
            {
                SpawnPad(cursorX, groundY, level.startPadLength);
                SpawnPoint = new Vector3(cursorX + level.startPadLength * 0.35f, groundY + 1.5f, 0f);
                cursorX += level.startPadLength;
            }
            else
            {
                SpawnPoint = new Vector3(cursorX, groundY + 1.5f, 0f);
            }

            // --- chunks ----------------------------------------------------
            List<LevelChunk> order = level.BuildChunkOrder();
            if (order.Count == 0)
                Debug.LogWarning($"[LevelBuilder] Level '{level.name}' produced no chunks.", this);

            foreach (LevelChunk prefab in order)
            {
                var instance = Instantiate(prefab, new Vector3(cursorX, groundY - prefab.entryHeight, 0f),
                                           Quaternion.identity, _root);
                instance.name = prefab.name;
                _spawned.Add(instance.gameObject);

                groundY += prefab.exitHeight - prefab.entryHeight;
                cursorX += prefab.length + prefab.trailingGap;
                lowestY = Mathf.Min(lowestY, groundY);
            }

            // --- run-out pad and finish -----------------------------------
            if (level.finishPadLength > 0f)
            {
                SpawnPad(cursorX, groundY, level.finishPadLength);
                cursorX += level.finishPadLength;
            }

            if (finishLinePrefab != null)
            {
                FinishLine finish = Instantiate(finishLinePrefab,
                                                new Vector3(cursorX - 3f, groundY + 2.5f, 0f),
                                                Quaternion.identity, _root);
                _spawned.Add(finish.gameObject);
            }

            LevelLength = Mathf.Max(1f, (cursorX - 3f) - SpawnPoint.x);

            // --- kill plane ------------------------------------------------
            if (killZonePrefab != null)
            {
                KillZone zone = Instantiate(killZonePrefab, _root);
                float depth = lowestY - killZoneDepth;
                zone.transform.position = new Vector3(cursorX * 0.5f, depth, 0f);

                var box = zone.GetComponent<BoxCollider2D>();
                if (box != null) box.size = new Vector2(cursorX + 200f, 4f);

                _spawned.Add(zone.gameObject);
            }
        }

        private void SpawnPad(float x, float y, float length)
        {
            if (padPrefab == null) return;

            GroundStrip pad = Instantiate(padPrefab, new Vector3(x, y, 0f), Quaternion.identity, _root);
            pad.Resize(length, padThickness);
            _spawned.Add(pad.gameObject);
        }

        public void Clear()
        {
            // Destroy() only takes effect at the end of the frame, and the new
            // level is built immediately after. Deactivating first keeps the old
            // geometry from colliding with the respawned player in between.
            foreach (GameObject go in _spawned)
            {
                if (go == null) continue;
                go.SetActive(false);
                Destroy(go);
            }

            _spawned.Clear();

            if (_root != null)
            {
                _root.gameObject.SetActive(false);
                Destroy(_root.gameObject);
            }

            _root = null;
        }
    }
}
