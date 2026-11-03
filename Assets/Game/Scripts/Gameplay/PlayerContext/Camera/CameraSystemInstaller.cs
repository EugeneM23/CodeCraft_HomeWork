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

        public void Install(IPlayerContext context)
        {
            context.AddCamera(_camera);
            context.AddCameraRoot(_cameraRoot);
            context.AddCameraSpeed(new Variable<int>(_cameraSpeed));
            context.AddCameraOffset(new Const<Vector3>(_cameraRoot.transform.position - _character.position));

            context.AddBehaviour<CameraFollowController>();
            context.AddBehaviour<CharacterShakeController>();
        }
    }
}