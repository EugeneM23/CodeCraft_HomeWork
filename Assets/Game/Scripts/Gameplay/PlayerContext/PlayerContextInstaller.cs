using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class PlayerContextInstaller : MonoEntityInstaller<PlayerContext>
    {
        [SerializeField] private PlayerID _playerID;
        [SerializeField] private MonoEntity _playerEntity;
        [SerializeField] private CharacterSystemInstaller _characterSystem;
        [SerializeField] private CameraSystemInstaller _cameraSystem;
        [SerializeField] private AbilitySystemInstaller _abilitySystem;
        [SerializeField] private ManaSystemInstaller _manaSystem;

        public override void Install(PlayerContext context)
        {
            _characterSystem.Install(context);
            _cameraSystem.Install(context);
            _abilitySystem.Install(context);
            _manaSystem.Install(context);

            // The on-screen joysticks are gone: movement is keyboard and aiming follows the mouse,
            // so MoveJoystick/RotateJoystick are no longer registered on the context.
        }
    }
}
