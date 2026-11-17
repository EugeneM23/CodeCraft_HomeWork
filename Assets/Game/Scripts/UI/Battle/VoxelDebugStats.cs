namespace Game
{
    /// <summary>The voxel world's numbers for the HUD's debug block (formerly VoxelDemoHud's lines).</summary>
    public struct VoxelDebugStats
    {
        public int Voxels;
        public int Bodies;
        public int LoosePieces;
        public int Debris;
        public int RemeshChunks;
        public float RemeshMs;
        public int AsyncInFlight;
        public int SmokeClouds;
        public int SmokeCubes;
    }
}
