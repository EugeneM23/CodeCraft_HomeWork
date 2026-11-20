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

        /// <summary>
        /// A piece broke off a body (VoxelBody.Detach: a blast, a collapse, a split): the middle of the gap it leaves (world),
        /// its half size (m, the body's axes) and its voxels.
        /// </summary>
        public static event Action<Vector3, Vector3, int> Detached;

        public static void RaiseDetached(Vector3 middle, Vector3 half, int voxels) => Detached?.Invoke(middle, half, voxels);

        /// <summary>A loose piece broke on a hit: contact point, impact speed (m/s).</summary>
        public static event Action<Vector3, float> Fractured;

        public static void RaiseCarved(Vector3 point, float radius, int voxels) => Carved?.Invoke(point, radius, voxels);

        public static void RaiseAirBlasted(Vector3 point, float radius, int voxels) =>
            AirBlasted?.Invoke(point, radius, voxels);

        public static void RaiseFractured(Vector3 point, float speed) => Fractured?.Invoke(point, speed);

        /// <summary>
        /// A blast blew a building's windows out (VoxelBody.ShatterGlass): the blast's point, glass voxels broken.
        /// Once per house per blast. For a glass-break sound (the destruction bank has none yet).
        /// </summary>
        public static event Action<Vector3, int> GlassShattered;

        public static void RaiseGlassShattered(Vector3 point, int voxels) => GlassShattered?.Invoke(point, voxels);
    }
}
