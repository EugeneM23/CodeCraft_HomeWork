using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>Voxel blood on hits. Put on Regax.prefab's &lt;VFX&gt; by Tools/Regax/Use Voxel Death.</summary>
    public sealed class VoxelBloodInstaller : MonoEntityInstaller
    {
        [Tooltip("The body's collider: drops come out of its surface nearest the hit.")]
        [SerializeField] private Collider _body;

        [Tooltip("Where a melee hit bleeds from.")]
        [SerializeField] private Transform _chest;

        [SerializeField] private VoxelBloodConfig _config = new();

        public override void Install(IEntity entity) =>
            entity.AddBehaviour(new VoxelBloodBehaviour(_body, _chest, _config));
    }
}
