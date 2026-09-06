using UnityEngine;

namespace ArvinRunner
{
    /// <summary>
    /// Central place for layer indices. The editor setup tool
    /// (ArvinRunner/Setup) writes these names into the project's TagManager,
    /// so the numbers here and the names in Unity always agree.
    /// </summary>
    public static class GameLayers
    {
        public const int Ground      = 8;
        public const int Wall        = 9;
        public const int Vaultable   = 10;
        public const int Hazard      = 11;
        public const int Player      = 12;
        public const int Collectible = 13;

        public static readonly string[] Names =
        {
            "Ground", "Wall", "Vaultable", "Hazard", "Player", "Collectible"
        };

        /// <summary>First layer index used by <see cref="Names"/>.</summary>
        public const int FirstIndex = Ground;

        public static LayerMask GroundMask      => 1 << Ground;
        public static LayerMask WallMask        => 1 << Wall;
        public static LayerMask VaultableMask   => 1 << Vaultable;
        public static LayerMask HazardMask      => 1 << Hazard;

        /// <summary>Anything the player physically stands on or bumps into.</summary>
        public static LayerMask SolidMask => (1 << Ground) | (1 << Wall) | (1 << Vaultable);
    }
}
