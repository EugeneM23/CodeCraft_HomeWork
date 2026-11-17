using System;
using Game.Horde;
using Unity.Mathematics;
using UnityEngine;

namespace Game.AI
{
    /// <summary>Where the level's navigation grid lies and how it reads the ground (the level AI installer's inspector).</summary>
    [Serializable]
    public sealed class LevelNavOptions
    {
        [Tooltip("Navigation grid: XZ min and max corner of the level, m.")]
        public Vector2 mapMin = new(-40f, -40f);

        public Vector2 mapMax = new(40f, 40f);

        [Tooltip("Navigation rays start this high and look this far down, m.")]
        public float rayTop = 20f;

        public float rayLength = 30f;

        /// <summary>The grid settings these options make.</summary>
        public HordeNavGridSettings ToSettings()
        {
            HordeNavGridSettings settings = HordeNavGridSettings.Cover((float2)mapMin, (float2)mapMax);
            settings.RayTop = rayTop;
            settings.RayLength = rayLength;
            return settings;
        }
    }
}
