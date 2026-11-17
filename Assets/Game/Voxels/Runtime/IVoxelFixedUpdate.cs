namespace Game.Voxels
{
    /// <summary>Every fixed step: the voxel side of FixedUpdate. Ticked by <see cref="VoxelTicks"/> when an owner drives it, otherwise by the component's own Unity method.</summary>
    public interface IVoxelFixedUpdate
    {
        void VoxelFixedUpdate(float deltaTime);
    }
}
