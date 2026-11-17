namespace Game.Voxels
{
    /// <summary>Every frame after the updates: the voxel side of LateUpdate. Ticked by <see cref="VoxelTicks"/> when an owner drives it, otherwise by the component's own Unity method.</summary>
    public interface IVoxelLateUpdate
    {
        void VoxelLateUpdate(float deltaTime);
    }
}
