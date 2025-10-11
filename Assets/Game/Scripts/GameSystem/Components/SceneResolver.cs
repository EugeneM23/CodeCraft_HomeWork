using UnityEngine;

namespace Gameplay
{
    public class SceneResolver : MonoBehaviour
    {
        private void Start()
        {
            var moveController = ServiceLocator.Get<MoveController>(PlayerId.MoveController);

            var moveComponent = ServiceLocator.Get<MoveComponent>(PlayerId.MoveComponent);
            var rotationComponent = ServiceLocator.Get<RotationComponent>(PlayerId.RotationComponent);
            var inputReader = ServiceLocator.Get<InputReader>(GameID.InpuReader);

            moveController.Construct(moveComponent, rotationComponent, inputReader);
        }
    }
}