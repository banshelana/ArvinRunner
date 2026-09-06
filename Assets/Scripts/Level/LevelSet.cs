using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// The campaign: every level in play order. Add a level by dropping its
    /// asset into this list - nothing else needs to change.
    ///
    /// Create via Assets > Create > ArvinRunner > Level Set.
    /// </summary>
    [CreateAssetMenu(menuName = "ArvinRunner/Level Set", fileName = "Campaign")]
    public class LevelSet : ScriptableObject
    {
        public LevelDefinition[] levels;

        public int Count => levels != null ? levels.Length : 0;

        public LevelDefinition Get(int index)
        {
            if (levels == null || index < 0 || index >= levels.Length) return null;
            return levels[index];
        }

        public bool IsLastLevel(int index) => index >= Count - 1;
    }
}
