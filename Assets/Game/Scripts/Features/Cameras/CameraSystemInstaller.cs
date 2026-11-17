using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{
    [Serializable]
    public class CameraSystemInstaller : IEntityInstaller<IPlayerContext>
    {
        [SerializeField] private MonoEntity _camera;
        [SerializeField] private Transform _cameraRoot;
        [SerializeField] private int _cameraSpeed;
        [SerializeField] private Transform _character;
        [SerializeField] private CameraLookSettings _look = new();
        [SerializeField] private CameraCollisionSettings _collision = new();

        public void Install(IPlayerContext context)
        {
            context.AddCamera(_camera);
            context.AddCameraRoot(_cameraRoot);
            context.AddCameraSpeed(new Variable<int>(_cameraSpeed));
            // A variable, not a constant: the voxel sandbox swings it round the vehicle with the mouse
            // (VoxelSandboxOrbit). Nothing else in the game writes it.
            context.AddCameraOffset(new Variable<Vector3>(_cameraRoot.transform.position - _character.position));
            context.AddCameraLook(new Variable<Vector3>(Vector3.zero));
            context.AddCameraLookSettings(_look);
            context.AddCameraCollisionSettings(_collision);

            context.AddBehaviour<CameraLookController>();
            context.AddBehaviour<CameraFollowController>();
            context.AddBehaviour<PlayerShakeController>();
        }
    }
}