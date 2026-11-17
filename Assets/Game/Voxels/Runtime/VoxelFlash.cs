using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// A blast's flash on voxels, lit by the voxel shader itself (Game/Voxel, _VoxelFlash*):
    /// per pixel, in the pass that draws the chunk anyway. A real point light in forward
    /// rendering draws every lit renderer once more — a 45 m bomb flash made hundreds of chunks
    /// draw twice for a third of a second. The flash's own Light keeps lighting everything else
    /// (cull <see cref="VoxelLayers"/> out of it, so voxels are not lit twice).
    /// </summary>
    public static class VoxelFlash
    {
        private static readonly int PositionId = Shader.PropertyToID("_VoxelFlashPos");
        private static readonly int ColorId = Shader.PropertyToID("_VoxelFlashColor");

        /// <summary>
        /// Layers whose voxels take the flash in the shader, culled out of the flash's Light:
        /// Default (buildings, debris), Ground, Rubble. The tank's skin shares the Character layer
        /// with its track belts and the player, which the Light still lights; skin chunks there
        /// opt out of the shader's flash instead (<see cref="LitByLight"/>).
        /// </summary>
        public static int VoxelLayers => (1 << 0) | (1 << 3) | (1 << VoxelWorld.PieceLayer);

        /// <summary>A voxel renderer on this layer is lit by the flash's real Light, not the shader.</summary>
        public static bool LitByLight(int layer) => (VoxelLayers & (1 << layer)) == 0;

        internal static readonly int ReceiveId = Shader.PropertyToID("_VoxelFlashReceive");

        /// <summary>Lights the voxels round <paramref name="position"/> like a shadowless point light.</summary>
        public static void Set(Vector3 position, float range, Color color, float intensity)
        {
            if (intensity <= 0f || range <= 0f)
            {
                Clear();
                return;
            }

            // The colour Built-in lit with in this project: linear colour space without linear light
            // intensity, so colour × intensity taken from gamma to linear. URP lights the flash's real Light
            // the same way through LightIntensity.FromBuiltIn, so the two still agree.
            Color c = color * intensity;
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                c = c.linear;
            Shader.SetGlobalVector(PositionId, new Vector4(position.x, position.y, position.z, range));
            Shader.SetGlobalVector(ColorId, new Vector4(c.r, c.g, c.b, 1f));
        }

        public static void Clear() => Shader.SetGlobalVector(ColorId, Vector4.zero);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Clear();
    }
}
