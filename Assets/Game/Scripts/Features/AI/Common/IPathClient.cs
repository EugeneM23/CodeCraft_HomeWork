using Game.Horde;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.AIKit.Adapters
{
    /// <summary>Who asked <see cref="LevelPathService"/> for a path: gets the result when its search is done.</summary>
    public interface IPathClient
    {
        /// <summary>
        /// The search for this client finished. <paramref name="path"/> belongs to the service and is rewritten by the
        /// next search: copy what is needed now. <paramref name="gridVersion"/> is the grid the path was found on.
        /// </summary>
        void OnPathFound(HordePathStatus status, NativeList<float3> path, int gridVersion);
    }
}
