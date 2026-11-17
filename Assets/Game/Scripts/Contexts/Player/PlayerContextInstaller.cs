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

        [Tooltip("Features/Controls: the Character slot (the player's tank) and the controllers that read keys and mouse.")]
        [FormerlySerializedAs("_characterSystem")]
        [SerializeField] private ControlSystemInstaller _controlSystem;

        [Tooltip("Features/Cameras: follow, look ahead, shake on shots.")]
        [SerializeField] private CameraSystemInstaller _cameraSystem;

        public override void Install(PlayerContext context)
        {
            _controlSystem.Install(context);
            _cameraSystem.Install(context);
        }
    }
}
