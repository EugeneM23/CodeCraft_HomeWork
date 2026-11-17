namespace Game
{
    /// <summary>
    /// Layer numbers and the layer masks shared by area queries. The numbers mirror
    /// ProjectSettings/TagManager.asset; masks elsewhere are built from these names rather than
    /// from bare numbers, so a mask reads as the list of what it leaves out.
    /// </summary>
    public static class PhysicsLayers
    {
        public const int IgnoreRaycast = 2;
        public const int Ground = 3;
        public const int Interactable = 6;
        public const int Dummy = 7;
        public const int Enemy = 8;
        public const int Character = 9;
        public const int ThrowItem = 11;
        public const int Bullet = 12;
        public const int Weapon = 13;
        public const int Destructible = 14;
        public const int Debris = 15;
        public const int Rubble = 16;

        /// <summary>
        /// Where a damageable entity can have a collider: everything but the ground, destructible
        /// pieces, debris, rubble and bullets. An area query over all layers can drown in voxel
        /// debris and flesh: a fixed result buffer would fill with them and the characters standing
        /// there would never be returned.
        /// </summary>
        public const int Targets =
            ~((1 << Ground) | (1 << Bullet) | (1 << Destructible) | (1 << Debris) | (1 << Rubble));

        /// <summary>
        /// Every layer a piece of something destructible can be on, standing or knocked out: standing
        /// on Destructible, loose debris and flesh on Debris and Rubble.
        /// </summary>
        public const int DestructiblePieces = (1 << Destructible) | (1 << Debris) | (1 << Rubble);
    }
}
