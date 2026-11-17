using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{
    /// <summary>
    /// Assembles the player context. It holds no game code of its own: each field below is one
    /// feature's system on the context, and the code of that system lives in the feature's folder.
    /// </summary>
    public class PlayerContextInstaller : MonoEntityInstaller<PlayerContext>
    {
        [SerializeField] private PlayerID _playerID;
        [SerializeField] private MonoEntity _playerEntity;

        [Tooltip("Features/Controls: the Character slot and the controllers that read keys and mouse.")]
        [FormerlySerializedAs("_characterSystem")]
        [SerializeField] private ControlSystemInstaller _controlSystem;

        [Tooltip("Features/Cameras: follow, look ahead, shake on shots.")]
        [SerializeField] private CameraSystemInstaller _cameraSystem;

        [Tooltip("Features/Abilities: dash, stun, teleport.")]
        [SerializeField] private AbilitySystemInstaller _abilitySystem;

        [Tooltip("Features/Abilities/Mana.")]
        [SerializeField] private ManaSystemInstaller _manaSystem;

        [Tooltip("Features/FogOfWar.")]
        [SerializeField] private FogOfWarView _fogOfWar;

        public override void Install(PlayerContext context)
        {
            _controlSystem.Install(context);
            _cameraSystem.Install(context);
            _abilitySystem.Install(context);
            _manaSystem.Install(context);

            if (_fogOfWar != null)
                context.AddBehaviour(new FogOfWarController(_fogOfWar));

            // The on-screen joysticks are gone: movement is keyboard and aiming follows the mouse,
            // so MoveJoystick/RotateJoystick are no longer registered on the context.
        }
    }
}
