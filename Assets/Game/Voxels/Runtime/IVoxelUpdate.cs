namespace Game.Voxels
{
    /// <summary>Every frame: the voxel side of Update. Ticked by <see cref="VoxelTicks"/> when an owner drives it, otherwise by the component's own Unity method.</summary>
    public interface IVoxelUpdate
    {
        void VoxelUpdate(float deltaTime);
    }
}
