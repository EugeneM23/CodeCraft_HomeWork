using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// How far the camera leans towards the cursor. One set for everyone: in a vehicle both the
    /// shift and the pull-back are scaled by its CameraDistance, like the rest of the offset.
    /// </summary>
    [Serializable]
    public class CameraLookSettings
    {
        [field: Tooltip("Ground distance the camera shifts towards the cursor at the edge of the screen, in metres.")]
        [field: SerializeField, Min(0f)]
        public float MaxShift { get; private set; } = 3.45f;

        [field: Tooltip("How much further the camera pulls back at the edge of the screen: 0.15 is 15 % further.")]
        [field: SerializeField, Range(0f, 1f)]
        public float MaxZoom { get; private set; } = 0.15f;

        [field: Tooltip("Part of the way from the centre to the edge of the screen where the cursor moves nothing.")]
        [field: SerializeField, Range(0f, 0.9f)]
        public float DeadZone { get; private set; } = 0.2f;

        public CameraLookSettings()
        {
        }

        public CameraLookSettings(float maxShift, float maxZoom, float deadZone)
        {
            MaxShift = maxShift;
            MaxZoom = maxZoom;
            DeadZone = deadZone;
        }
    }
}
