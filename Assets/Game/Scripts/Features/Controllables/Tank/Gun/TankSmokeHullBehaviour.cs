using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Experiment: the tank's hull pushes the small voxel smoke clouds out of itself
    /// (VoxelPuffField), so the tank driving through smoke cuts a wake in it.
    /// </summary>
    public sealed class TankSmokeHullBehaviour : IEntityInit, IEntityDispose
    {
        private readonly BoxCollider _hull;
        private readonly Rigidbody _body;

        public TankSmokeHullBehaviour(BoxCollider hull, Rigidbody body)
        {
            _hull = hull;
            _body = body;
        }

        public void Init(IEntity entity) => VoxelPuffField.AddHull(_hull, _body);

        public void Dispose(IEntity entity) => VoxelPuffField.RemoveHull(_hull);
    }
}
