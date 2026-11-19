using System;
using Game.Horde;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>Level-side settings of the monsters that leap onto the tank (the horde installer's inspector, <see cref="HordeOptions.riders"/>).</summary>
    [Serializable]
    public sealed class HordeRiderOptions
    {
        [Tooltip("Monsters leap onto the tank and ride it.")]
        public bool enabled = true;

        [Tooltip("Kind rows that leap, a bit per row (1: the crawler only; 2: the spitter; 4: the big one).")]
        public int kindMask = 1 << HordeKinds.CRAWLER;

        [Tooltip("Scale of the riders' strikes on the tank on top of the horde's own (0: they ride but do no harm).")]
        public float damageScale = 1f;

        public HordeRidingSettings settings = HordeRidingSettings.Default;

        [Header("Placeholder clips (count 0: the art's own)")]
        [Tooltip("In the air. Default: the run clip.")]
        public HordeClip leapClip;

        [Tooltip("Just landed, holding on. Default: the idle clip.")]
        public HordeClip clingClip;

        [Tooltip("Tearing at the armour. Default: the attack clip.")]
        public HordeClip tearClip;

        [Tooltip("Falling off. Default: the idle clip.")]
        public HordeClip fallClip;

        /// <summary>The tuning; an unset one (a horde serialized before riders existed) is the default.</summary>
        public HordeRidingSettings Settings => settings.MaxRiders > 0 ? settings : HordeRidingSettings.Default;

        public bool Leaps(int row) => enabled && row >= 0 && row < 31 && (kindMask & (1 << row)) != 0;
    }
}
