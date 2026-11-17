using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Makes the character die into voxel flesh. Put on Regax.prefab by Tools/Regax/Use Voxel Death
    /// (RegaxVoxelDeathBuilder), in place of CharacterGibsInstaller.
    /// </summary>
    public sealed class CharacterVoxelGibsInstaller : MonoEntityInstaller
    {
        [SerializeField] private SkinnedMeshRenderer _skin;

        [Tooltip("The animated hierarchy holding the bones the parts name.")]
        [SerializeField] private Transform _skeleton;

        [Tooltip("What disappears when the flesh takes the body's place.")]
        [SerializeField] private Renderer[] _hide;

        [SerializeField] private VoxelGibsConfig _config = new();

        public override void Install(IEntity entity)
        {
            if (_skin == null || _skeleton == null)
            {
                // A prefab that lost a reference would otherwise just die without flesh, unnoticed.
                Debug.LogWarning($"[VOXEL GIBS] {name}: no {(_skin == null ? "skin" : "skeleton")} set; " +
                                 "the body will not die into voxels.", this);
                return;
            }

            entity.AddBehaviour(new VoxelGibsOnDeathBehaviour(VoxelGibsRig.Resolve(_skin, _skeleton, _hide, _config), _config));
        }
    }
}
