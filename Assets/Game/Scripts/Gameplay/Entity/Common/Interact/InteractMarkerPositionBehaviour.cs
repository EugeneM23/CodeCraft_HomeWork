using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class InteractMarkerPositionBehaviour : IEntityUpdate, IEntityInit
    {
        private readonly GameObject _view;
        private readonly Transform _transfrom;
        private readonly Vector3 _offset;
        private IReactiveVariable<IEntity> _character;
        private Camera _camera;

        public InteractMarkerPositionBehaviour(Vector3 positionOffset, Transform transform, GameObject view)
        {
            _view = view;
            _offset = positionOffset;
            _transfrom = transform;
        }

        public void Init(in IEntity entity)
        {
            _character = GameContext.Instance.GetPlayerCharacter();
            _camera = Camera.main;
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            IEntity item = _character.Value.GetTargetInteractable().Value;
            Debug.Log(item == null);
            if (item != null)
            {
                _view.SetActive(true);
                _transfrom.position = item.GetTransform().position + _offset;
                
                Vector3 directionToCamera = _camera.transform.position - _transfrom.position;
                directionToCamera.z = 0;
                _transfrom.rotation = Quaternion.LookRotation(directionToCamera);
            }
            else
            {
                _view.SetActive(false);
            }
        }
    }
}