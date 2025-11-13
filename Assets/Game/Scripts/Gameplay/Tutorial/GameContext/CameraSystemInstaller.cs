using System;
using Atomic.Contexts;
using Atomic.Elements;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class CameraSystemInstaller : IContextInstaller<IGameContext>
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private int _cameraSpeed;

        [SerializeField] private Transform _character;

        public void Install(IGameContext context)
        {
            context.AddCamera(_camera);
            context.AddCameraSpeed(new BaseVariable<int>(_cameraSpeed));
            context.AddCameraOffset(new Const<Vector3>(_camera.transform.position - _character.position));
            
            context.AddController<CameraFollowController>();
        }
    }
}