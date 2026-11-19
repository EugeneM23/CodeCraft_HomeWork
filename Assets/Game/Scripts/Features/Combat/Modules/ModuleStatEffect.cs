using System;
using UnityEngine;

namespace Game
{
    /// <summary>One number a module changes, with its value at each module level (1..3).</summary>
    [Serializable]
    public struct ModuleStatEffect
    {
        public ModuleStat Stat;
        public ModuleStatOp Op;

        [Tooltip("The value at level 1, 2, 3.")]
        public float[] Levels;

        public ModuleStatEffect(ModuleStat stat, ModuleStatOp op, float[] levels)
        {
            Stat = stat;
            Op = op;
            Levels = levels;
        }

        /// <summary>The value at the level, clamped to the levels given; the neutral value without any.</summary>
        public float ValueAt(int level)
        {
            if (Levels == null || Levels.Length == 0)
                return Op == ModuleStatOp.Multiply ? 1f : 0f;

            int index = Mathf.Clamp(level - 1, 0, Levels.Length - 1);
            return Levels[index];
        }
    }
}
