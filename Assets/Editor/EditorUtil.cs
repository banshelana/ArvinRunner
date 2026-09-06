using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArvinRunner.EditorTools
{
    /// <summary>Small helpers shared by the setup tools.</summary>
    public static class EditorUtil
    {
        /// <summary>Creates a project folder and every missing parent above it.</summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Creates the asset, replacing anything already at that path.</summary>
        public static T CreateAsset<T>(T asset, string path) where T : Object
        {
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));

            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);

            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ---------------------------------------------------------------- //
        // Writing [SerializeField] private fields from editor code
        // ---------------------------------------------------------------- //

        public static void SetObject(Object target, string field, Object value)
        {
            Apply(target, field, p => p.objectReferenceValue = value);
        }

        public static void SetFloat(Object target, string field, float value)
        {
            Apply(target, field, p => p.floatValue = value);
        }

        public static void SetInt(Object target, string field, int value)
        {
            Apply(target, field, p => p.intValue = value);
        }

        public static void SetBool(Object target, string field, bool value)
        {
            Apply(target, field, p => p.boolValue = value);
        }

        public static void SetVector2(Object target, string field, Vector2 value)
        {
            Apply(target, field, p => p.vector2Value = value);
        }

        public static void SetColor(Object target, string field, Color value)
        {
            Apply(target, field, p => p.colorValue = value);
        }

        private static void Apply(Object target, string field, System.Action<SerializedProperty> write)
        {
            if (target == null) return;

            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);

            if (property == null)
            {
                Debug.LogWarning($"[ArvinRunner] No serialized field '{field}' on {target.GetType().Name}.");
                return;
            }

            write(property);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- //
        // Layers
        // ---------------------------------------------------------------- //

        /// <summary>
        /// Writes the game's layer names into ProjectSettings/TagManager.asset,
        /// at the exact indices <see cref="GameLayers"/> expects.
        /// </summary>
        public static void EnsureLayers()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[ArvinRunner] Could not open TagManager.asset to add layers.");
                return;
            }

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            if (layers == null || !layers.isArray)
            {
                Debug.LogError("[ArvinRunner] TagManager.asset has no layers array.");
                return;
            }

            for (int i = 0; i < GameLayers.Names.Length; i++)
            {
                int index = GameLayers.FirstIndex + i;
                if (index >= layers.arraySize) break;

                SerializedProperty slot = layers.GetArrayElementAtIndex(index);
                string wanted = GameLayers.Names[i];

                if (slot.stringValue == wanted) continue;

                if (!string.IsNullOrEmpty(slot.stringValue))
                    Debug.LogWarning($"[ArvinRunner] Layer {index} was '{slot.stringValue}', now '{wanted}'.");

                slot.stringValue = wanted;
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }
}
