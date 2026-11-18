namespace Game
{
    /// <summary>The six slots of a tank (GDD §6); every module fits exactly one.</summary>
    public enum ModuleSlot
    {
        /// <summary>Орудие: the main gun.</summary>
        Gun,

        /// <summary>Пулемёт: the starting automatic weapon.</summary>
        MachineGun,

        /// <summary>Броня.</summary>
        Armor,

        /// <summary>Двигатель.</summary>
        Engine,

        /// <summary>Ходовая.</summary>
        Tracks,

        /// <summary>Спецмодуль on E.</summary>
        Special
    }
}
