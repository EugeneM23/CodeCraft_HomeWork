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
        /// pieces, debris, rubble and bullets. An area query over all layers can drown in wall pieces
        /// and debris (the destructible walls alone have over two thousand): the fixed result buffer
        /// filled with them and the characters standing there were never returned. That is how the
        /// flash grenade first blinded no one.
        /// </summary>
        public const int Targets =
            ~((1 << Ground) | (1 << Bullet) | (1 << Destructible) | (1 << Debris) | (1 << Rubble));

        /// <summary>
        /// Every layer a piece of something destructible can be on, standing or knocked out. Standing
        /// pieces are on Destructible; RayFire moves a piece that leaves a wall to Debris or Rubble
        /// (the activation layer every wall sets), and DemolitionQueue moves the pieces of a brick
        /// split in flight there too. A few loose bodies still stay on Destructible (the groups the
        /// slab wall breaks into, listed by LooseOnDestructibleLayer), so standing and loose are told
        /// apart by the body, not by the layer alone.
        /// </summary>
        public const int DestructiblePieces = (1 << Destructible) | (1 << Debris) | (1 << Rubble);
    }
}
