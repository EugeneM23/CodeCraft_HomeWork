using System;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// What happened to the voxels, for listeners that are not part of the destruction itself: sound,
    /// camera shake, HUD. Raised on the main thread; no allocation per raise.
    /// </summary>
    public static class VoxelSignals
    {
        /// <summary>A blast or a bullet carved voxels out: point, radius (m), voxels taken.</summary>
        public static event Action<Vector3, float, int> Carved;

        /// <summary>An air bomb dug a crater: point, crater radius (m), voxels taken.</summary>
        public static event Action<Vector3, float, int> AirBlasted;

        /// <summary>A loose piece broke on a hit: contact point, impact speed (m/s).</summary>
        public static event Action<Vector3, float> Fractured;

        public static void RaiseCarved(Vector3 point, float radius, int voxels) => Carved?.Invoke(point, radius, voxels);

        public static void RaiseAirBlasted(Vector3 point, float radius, int voxels) =>
            AirBlasted?.Invoke(point, radius, voxels);

        public static void RaiseFractured(Vector3 point, float speed) => Fractured?.Invoke(point, speed);
    }
}
