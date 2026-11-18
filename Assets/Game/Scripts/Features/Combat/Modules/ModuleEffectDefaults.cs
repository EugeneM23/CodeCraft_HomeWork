namespace Game
{
    /// <summary>
    /// The modules' effects in code (GDD §6), used when <see cref="CombatConfig.Modules"/> lacks one. Every level
    /// gives +15 % to the module's two main numbers: a value that grows is ×1.15^(L−1), one that shrinks (reload,
    /// interval, crush threshold, a penalty) is ÷1.15^(L−1).
    /// </summary>
    public static class ModuleEffectDefaults
    {
        public const float LEVEL_GROWTH = 1.15f;

        private static ModuleEffectConfig[] s_all;

        /// <summary>Every module with a code default.</summary>
        public static ModuleEffectConfig[] All => s_all ??= Build();

        public static ModuleEffectConfig Find(ModuleId id)
        {
            ModuleEffectConfig[] all = All;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Id == id)
                    return all[i];
            }

            return null;
        }

        /// <summary>The value at levels 1..3, growing by <see cref="LEVEL_GROWTH"/> a level.</summary>
        public static float[] Up(float value) =>
            new[] { value, value * LEVEL_GROWTH, value * LEVEL_GROWTH * LEVEL_GROWTH };

        /// <summary>The value at levels 1..3, shrinking by <see cref="LEVEL_GROWTH"/> a level.</summary>
        public static float[] Down(float value) =>
            new[] { value, value / LEVEL_GROWTH, value / (LEVEL_GROWTH * LEVEL_GROWTH) };

        /// <summary>The same value at every level.</summary>
        public static float[] Flat(float value) => new[] { value, value, value };

        private static ModuleStatEffect Set(ModuleStat stat, float[] levels) => new(stat, ModuleStatOp.Set, levels);

        private static ModuleStatEffect Mul(ModuleStat stat, float[] levels) => new(stat, ModuleStatOp.Multiply, levels);

        /// <summary>1 + bonus, the bonus growing a level: (1 + b, 1 + b·1.15, 1 + b·1.15²).</summary>
        private static float[] OnePlus(float[] bonus) => new[] { 1f + bonus[0], 1f + bonus[1], 1f + bonus[2] };

        /// <summary>1 − penalty, the penalty shrinking a level.</summary>
        private static float[] OneMinus(float[] penalty) => new[] { 1f - penalty[0], 1f - penalty[1], 1f - penalty[2] };

        private static ModuleEffectConfig[] Build()
        {
            float[] skirtsSpeed = OneMinus(Down(0.08f));
            float[] engineBoosted = Up(1.2f);

            return new[]
            {
                // Gun: the standard HE gun scales the base cannon; the AP gun replaces it.
                new ModuleEffectConfig(ModuleIds.Gun88He, ModuleSlot.Gun,
                    Mul(ModuleStat.CannonDamage, Up(1f)),
                    Mul(ModuleStat.CannonRadius, Up(1f))),
                new ModuleEffectConfig(ModuleIds.GunArmorPiercing, ModuleSlot.Gun,
                    Set(ModuleStat.CannonDamage, Up(250f)),
                    Set(ModuleStat.CannonReload, Down(1.6f)),
                    Set(ModuleStat.CannonRadius, Flat(2f)),
                    Set(ModuleStat.CannonPierce, Flat(3f))),

                // Machine gun: damage and rate.
                new ModuleEffectConfig(ModuleIds.MachineGun, ModuleSlot.MachineGun,
                    Mul(ModuleStat.MgDamage, Up(1f)),
                    Mul(ModuleStat.MgInterval, Down(1f))),

                // Armor: standard +0 and +15 % health a level; screens +40 % health, −8 % speed.
                new ModuleEffectConfig(ModuleIds.ArmorStandard, ModuleSlot.Armor,
                    Mul(ModuleStat.Health, new[] { 1f, 1.15f, 1.3f })),
                new ModuleEffectConfig(ModuleIds.ArmorSkirts, ModuleSlot.Armor,
                    Mul(ModuleStat.Health, OnePlus(Up(0.4f))),
                    Mul(ModuleStat.Speed, skirtsSpeed),
                    Mul(ModuleStat.BoostSpeed, skirtsSpeed)),

                // Engine: speed and boost.
                new ModuleEffectConfig(ModuleIds.EngineStandard, ModuleSlot.Engine,
                    Mul(ModuleStat.Speed, Up(1f)),
                    Mul(ModuleStat.BoostSpeed, Up(1f))),
                new ModuleEffectConfig(ModuleIds.EngineBoosted, ModuleSlot.Engine,
                    Mul(ModuleStat.Speed, engineBoosted),
                    Mul(ModuleStat.BoostSpeed, engineBoosted)),

                // Tracks: a wider crush, crushing at a lower speed.
                new ModuleEffectConfig(ModuleIds.TracksStandard, ModuleSlot.Tracks,
                    Mul(ModuleStat.CrushWidthFactor, Up(1f)),
                    Mul(ModuleStat.CrushSpeed, Down(1f))),
                new ModuleEffectConfig(ModuleIds.TracksWide, ModuleSlot.Tracks,
                    Mul(ModuleStat.CrushWidthFactor, Up(1.3f)),
                    Mul(ModuleStat.CrushSpeed, Down(1f))),

                // Special (E): smoke launchers, 12 s of smoke every 30 s.
                new ModuleEffectConfig(ModuleIds.SpecialSmoke, ModuleSlot.Special,
                    Set(ModuleStat.SpecialDuration, Up(12f)),
                    Set(ModuleStat.SpecialCooldown, Down(30f)))
            };
        }
    }
}
